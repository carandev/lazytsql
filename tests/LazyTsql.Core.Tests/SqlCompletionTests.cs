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
        return catalog;
    }

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
    public void After_FROM_suggests_tables_views_and_functions_by_name_with_their_schema()
    {
        var result = Suggest("SELECT * FROM cu|");

        Assert.Equal(2, result.Remove);
        Assert.Equal(["dbo.customers", "sales.customer_totals", "dbo.customer_rank"], Texts(result));
        Assert.Equal([CompletionKind.Table, CompletionKind.View, CompletionKind.Function], result.Items.Select(item => item.Kind));
    }

    [Fact]
    public void After_JOIN_names_that_need_brackets_get_them()
    {
        Assert.Contains("dbo.[order details]", Texts(Suggest("SELECT * FROM a\nJOIN ord|")));
    }

    [Fact]
    public void After_EXEC_suggests_only_procedures()
    {
        Assert.Equal(["dbo.get_customers"], Texts(Suggest("EXEC get|")));
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
}
