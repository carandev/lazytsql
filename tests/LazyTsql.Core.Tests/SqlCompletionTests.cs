using LazyTsql.Core.Completion;
using LazyTsql.Core.Schema;

namespace LazyTsql.Core.Tests;

public class SqlCompletionTests
{
    private static SchemaCatalog Catalog()
    {
        var catalog = new SchemaCatalog { Databases = ["shop", "archive"] };
        catalog.SetObjects("shop",
        [
            new DbObject("dbo", "customers", DbObjectKind.Table),
            new DbObject("dbo", "orders", DbObjectKind.Table),
            new DbObject("dbo", "order details", DbObjectKind.Table),
            new DbObject("sales", "customer_totals", DbObjectKind.View),
            new DbObject("dbo", "get_customers", DbObjectKind.Procedure),
            new DbObject("dbo", "customer_rank", DbObjectKind.Function),
        ]);
        catalog.SetColumns("shop", new DbObject("dbo", "customers", DbObjectKind.Table),
            [Column("id", "int"), Column("name", "nvarchar(100)"), Column("email", "nvarchar(200)")]);
        catalog.SetColumns("shop", new DbObject("dbo", "orders", DbObjectKind.Table),
            [Column("id", "int"), Column("customer_id", "int"), Column("total", "decimal(10,2)")]);
        return catalog;
    }

    private static ColumnDefinition Column(string name, string type) => new(name, type, true, false, false);

    // '|' marks the cursor.
    private static CompletionResult Suggest(string script, SchemaCatalog? catalog = null)
    {
        var lines = script.Split('\n');
        var row = Array.FindIndex(lines, line => line.Contains('|'));
        var column = lines[row].IndexOf('|');
        lines[row] = lines[row].Remove(column, 1);
        return SqlCompletion.Suggest(lines, row, column, catalog ?? Catalog(), "shop");
    }

    private static string[] Texts(CompletionResult result) => [.. result.Items.Select(item => item.Text)];

    [Fact]
    public void After_FROM_suggests_tables_views_and_functions_with_their_schema_unless_it_is_dbo()
    {
        var result = Suggest("SELECT * FROM cu|");

        Assert.Equal(2, result.Remove);
        Assert.Equal(["customers", "sales.customer_totals", "customer_rank"], Texts(result));
        Assert.Equal([CompletionKind.Table, CompletionKind.View, CompletionKind.Function], result.Items.Select(item => item.Kind));
    }

    [Fact]
    public void After_JOIN_names_that_need_brackets_get_them()
    {
        Assert.Contains("[order details]", Texts(Suggest("SELECT * FROM a\nJOIN ord|")));
    }

    [Fact]
    public void After_EXEC_suggests_only_procedures()
    {
        Assert.Equal(["get_customers"], Texts(Suggest("EXEC get|")));
    }

    [Fact]
    public void A_dbo_name_that_another_schema_also_has_keeps_its_schema()
    {
        var catalog = Catalog();
        catalog.SetObjects("shop", [new DbObject("dbo", "orders", DbObjectKind.Table), new DbObject("sales", "orders", DbObjectKind.Table)]);

        Assert.Equal(["dbo.orders", "sales.orders"], Texts(Suggest("SELECT * FROM ord|", catalog)));
    }

    [Fact]
    public void After_a_schema_and_a_dot_suggests_that_schema_objects_by_bare_name()
    {
        var result = Suggest("SELECT * FROM sales.|");

        Assert.Equal(0, result.Remove);
        Assert.Equal(["customer_totals"], Texts(result));
    }

    [Fact]
    public void Elsewhere_suggests_keywords_in_the_case_being_typed()
    {
        Assert.Contains("SELECT", Texts(Suggest("SEL|")));
        Assert.Contains("select", Texts(Suggest("sel|")));
    }

    [Fact]
    public void Nothing_inside_strings_comments_or_without_a_prefix()
    {
        Assert.Empty(Suggest("SELECT 'FROM cu|").Items);
        Assert.Empty(Suggest("-- FROM cu|").Items);
        Assert.Empty(Suggest("/* FROM\ncu| */").Items);
        Assert.Empty(Suggest("SELECT * FROM |").Items);
        Assert.Empty(Suggest("SELECT * FROM dbo.\n|").Items);
        Assert.Empty(Suggest("SELECT * FROM dbo. |").Items);
    }

    [Fact]
    public void An_unloaded_database_is_requested_once_and_keywords_still_work()
    {
        var catalog = new SchemaCatalog();
        var requests = new List<string>();
        catalog.ObjectsNeeded += requests.Add;

        Assert.Empty(Suggest("SELECT * FROM cu|", catalog).Items);
        Assert.Empty(Suggest("SELECT * FROM cus|", catalog).Items);
        Assert.Contains("SELECT", Texts(Suggest("SEL|", catalog)));

        Assert.Equal(["shop"], requests);
    }

    [Fact]
    public void After_an_alias_and_a_dot_suggests_its_columns_with_their_type()
    {
        var result = Suggest("SELECT c.| FROM dbo.customers c");

        Assert.Equal(["email", "id", "name"], Texts(result));
        Assert.Equal("nvarchar(200)", result.Items[0].Detail);
        Assert.Equal(["name"], Texts(Suggest("SELECT c.na| FROM dbo.customers AS c")));
    }

    [Fact]
    public void A_table_name_schema_and_table_or_a_later_item_of_the_FROM_list_resolve_too()
    {
        Assert.Equal(["email", "id", "name"], Texts(Suggest("SELECT customers.| FROM customers")));
        Assert.Equal(["email", "id", "name"], Texts(Suggest("SELECT dbo.customers.| FROM dbo.customers")));
        Assert.Equal(["customer_id", "id", "total"], Texts(Suggest("SELECT o.| FROM dbo.customers c, dbo.orders o")));
        Assert.Equal(["customer_id"], Texts(Suggest("SELECT * FROM dbo.customers c\nJOIN dbo.orders o ON o.cu|")));
    }

    [Fact]
    public void Aliases_resolve_within_the_current_statement()
    {
        Assert.Equal(["email", "id", "name"], Texts(Suggest("SELECT * FROM dbo.orders c;\nSELECT c.| FROM dbo.customers c")));
    }

    [Fact]
    public void Without_a_qualifier_columns_of_the_statement_tables_come_before_aliases_and_keywords()
    {
        var result = Suggest("SELECT c| FROM dbo.customers cust JOIN dbo.orders o ON 1 = 1");

        Assert.Equal(["customer_id", "cust"], Texts(result)[..2]);
        Assert.Equal([CompletionKind.Column, CompletionKind.Alias], result.Items.Take(2).Select(item => item.Kind));
        Assert.Contains("case", Texts(result));
    }

    [Fact]
    public void Unloaded_columns_are_requested_once()
    {
        var catalog = Catalog();
        var requests = new List<string>();
        catalog.ColumnsNeeded += (database, table) => requests.Add($"{database}.{table.Schema}.{table.Name}");

        Assert.Empty(Suggest("SELECT d.| FROM dbo.[order details] d", catalog).Items);
        Assert.Empty(Suggest("SELECT d.s| FROM dbo.[order details] d", catalog).Items);

        Assert.Equal(["shop.dbo.order details"], requests);
    }

    private static SchemaCatalog WithArchive()
    {
        var catalog = Catalog();
        var oldOrders = new DbObject("dbo", "old_orders", DbObjectKind.Table);
        catalog.SetObjects("archive", [oldOrders, new DbObject("history", "events", DbObjectKind.Table), new DbObject("dbo", "purge", DbObjectKind.Procedure)]);
        catalog.SetColumns("archive", oldOrders, [Column("id", "int"), Column("closed_at", "datetime2")]);
        return catalog;
    }

    [Fact]
    public void After_FROM_database_names_are_suggested_too()
    {
        var result = Suggest("SELECT * FROM arc|", WithArchive());

        Assert.Equal(["archive"], Texts(result));
        Assert.Equal(CompletionKind.Database, result.Items[0].Kind);
    }

    [Fact]
    public void A_database_then_its_schemas_then_their_objects_then_their_columns()
    {
        Assert.Equal(["dbo", "history"], Texts(Suggest("SELECT * FROM archive.|", WithArchive())));
        Assert.Equal(["old_orders"], Texts(Suggest("SELECT * FROM archive.dbo.|", WithArchive())));
        Assert.Equal(["purge"], Texts(Suggest("EXEC archive.dbo.|", WithArchive())));
        Assert.Equal(["closed_at", "id"], Texts(Suggest("SELECT archive.dbo.old_orders.| FROM archive.dbo.old_orders", WithArchive())));
    }

    [Fact]
    public void An_alias_of_a_table_in_another_database_resolves_its_columns()
    {
        Assert.Equal(["closed_at", "id"], Texts(Suggest("SELECT a.| FROM archive.dbo.old_orders a", WithArchive())));
    }

    [Fact]
    public void Another_database_is_requested_when_first_named()
    {
        var catalog = Catalog();
        var requests = new List<string>();
        catalog.ObjectsNeeded += requests.Add;

        Assert.Empty(Suggest("SELECT * FROM archive.|", catalog).Items);

        Assert.Equal(["archive"], requests);
    }
}
