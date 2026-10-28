using LazyTsql.Core.Completion;
using LazyTsql.Core.Schema;

namespace LazyTsql.Core.Tests;

public class SqlDiagnosticsTests
{
    private static SchemaCatalog Catalog()
    {
        var catalog = new SchemaCatalog { Databases = ["shop", "archive"] };
        var customers = new DbObject("dbo", "customers", DbObjectKind.Table);
        catalog.SetObjects("shop", [customers, new DbObject("sales", "totals", DbObjectKind.View)]);
        catalog.SetColumns("shop", customers, [new ColumnDefinition("id", "int", false, true, false), new ColumnDefinition("name", "nvarchar(50)", true, false, false)]);
        return catalog;
    }

    // '|' marks the cursor; without it the cursor is nowhere in the script.
    private static IReadOnlyList<Diagnostic> Diagnose(string script, SchemaCatalog? catalog = null)
    {
        var lines = script.Split('\n');
        (int, int)? cursor = null;
        var row = Array.FindIndex(lines, line => line.Contains('|'));
        if (row >= 0)
        {
            cursor = (row, lines[row].IndexOf('|'));
            lines[row] = lines[row].Remove(cursor.Value.Item2, 1);
        }

        return SqlCompletion.Diagnose(lines, catalog ?? Catalog(), "shop", cursor);
    }

    [Fact]
    public void An_unknown_table_is_marked_where_it_is_written()
    {
        var diagnostic = Assert.Single(Diagnose("SELECT * FROM customers c\nJOIN dbo.custs x ON 1 = 1"));

        Assert.Equal(new Diagnostic(1, 5, 14, "Unknown table or view: dbo.custs"), diagnostic);
        Assert.Empty(Diagnose("SELECT * FROM customers; SELECT * FROM sales.totals; UPDATE dbo.customers SET name = ''"));
        Assert.Single(Diagnose("SELECT * FROM dbo.totals"));
        Assert.Single(Diagnose("INSERT INTO nope (id) VALUES (1)"));
    }

    [Fact]
    public void The_name_being_typed_is_not_marked()
    {
        Assert.Empty(Diagnose("SELECT * FROM cust|"));
        Assert.Single(Diagnose("SELECT * FROM cust |"));
    }

    [Fact]
    public void An_unknown_column_of_an_alias_or_table_is_marked()
    {
        var diagnostic = Assert.Single(Diagnose("SELECT c.id, c.nmae FROM customers c"));

        Assert.Equal(new Diagnostic(0, 15, 19, "Unknown column: dbo.customers.nmae"), diagnostic);
        Assert.Single(Diagnose("SELECT customers.nope FROM customers"));
        Assert.Empty(Diagnose("SELECT c.[name], c.ID FROM customers AS c"));
    }

    [Fact]
    public void Names_it_cannot_know_are_not_marked()
    {
        Assert.Empty(Diagnose("SELECT * FROM #work JOIN ##shared s ON 1 = 1"));
        Assert.Empty(Diagnose("WITH recent AS (SELECT 1 AS id), older (id) AS (SELECT 2) SELECT * FROM recent r JOIN older ON r.anything = 1"));
        Assert.Empty(Diagnose("CREATE TABLE dbo.fresh (id int);\nGO\nSELECT * FROM fresh"));
        Assert.Empty(Diagnose("SELECT * INTO dbo.copy FROM customers"));
        Assert.Empty(Diagnose("ALTER TABLE dbo.later ADD x int; DROP TABLE dbo.gone"));
        Assert.Empty(Diagnose("SELECT * FROM sys.objects JOIN INFORMATION_SCHEMA.TABLES t ON 1 = 1"));
        Assert.Empty(Diagnose("UPDATE c SET name = '' FROM customers c"));
        Assert.Empty(Diagnose("SELECT * FROM linked.db.dbo.t; SELECT * FROM nodb.dbo.t; SELECT x.y FROM (SELECT 1 AS z) x"));
        Assert.Empty(Diagnose("SELECT * FROM archive.dbo.anything"));
    }

    [Fact]
    public void Unloaded_names_are_requested_and_not_marked()
    {
        var catalog = Catalog();
        var columns = new List<string>();
        catalog.ColumnsNeeded += (database, table) => columns.Add(table.Name);
        catalog.SetObjects("shop", [.. catalog.Objects("shop")!, new DbObject("dbo", "orders", DbObjectKind.Table)]);

        Assert.Empty(Diagnose("SELECT o.anything FROM orders o", catalog));
        Assert.Equal(["orders"], columns);
    }
}
