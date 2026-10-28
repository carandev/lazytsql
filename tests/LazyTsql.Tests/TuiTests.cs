using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;
using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class TuiTests
{
    [Fact]
    public void Table_source_formats_cells_on_demand_as_single_lines()
    {
        var source = new ResultSetTableSource(new ResultSet(
            [new ColumnInfo("id", "int"), new ColumnInfo("note", "nvarchar")],
            [[1, "a\nb\tc"], [2, null]],
            IsTruncated: false));

        Assert.Equal(["id", "note"], source.ColumnNames);
        Assert.Equal((2, 2), (source.Rows, source.Columns));
        Assert.Equal("a⏎b c", source[0, 1]);
        Assert.Equal("NULL", source[1, 1]);
    }

    [Fact]
    public void Only_tables_and_views_expand_to_columns()
    {
        Assert.True(new ObjectNode("shop", new DbObject("dbo", "t", DbObjectKind.Table)).CanExpand);
        Assert.True(new ObjectNode("shop", new DbObject("dbo", "v", DbObjectKind.View)).CanExpand);
        Assert.False(new ObjectNode("shop", new DbObject("dbo", "p", DbObjectKind.Procedure)).CanExpand);
        Assert.Equal("Tables (0)", new FolderNode(DbObjectKind.Table, []).Label);
    }

    [Fact]
    public void Footer_shows_the_context_keys_then_the_global_keys_that_work_there()
    {
        var schema = KeyHelp.Footer(KeyContext.Schema);
        Assert.StartsWith("Enter expand", schema, StringComparison.Ordinal);
        Assert.Contains("s SELECT TOP 100", schema, StringComparison.Ordinal);
        Assert.EndsWith("? help · q quit", schema, StringComparison.Ordinal);

        // In the editor letters and digits are text, so its footer points to keys that still work.
        Assert.EndsWith("F1 help · Ctrl+Q quit", KeyHelp.Footer(KeyContext.Query), StringComparison.Ordinal);

        var picker = KeyHelp.Footer(KeyContext.FilePicker);
        Assert.Contains("Tab complete", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("1-5", picker, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_shows_only_the_current_context_and_expands_to_every_key()
    {
        var schema = KeyHelp.Render(KeyContext.Schema);
        Assert.Contains("Schema [2]", schema);
        Assert.DoesNotContain(schema, line => line.Contains("Connections [1]", StringComparison.Ordinal));
        Assert.Contains(schema, line => line.Contains("show every key", StringComparison.Ordinal));

        var all = KeyHelp.Render(context: null);
        foreach (var title in new[] { "Global", "Connections [1]", "Schema [2]", "Query [3]", "Results [4]", "Messages [5]", "File picker", "Confirmation", "Connection form" })
            Assert.Contains(all, line => line.Contains(title, StringComparison.Ordinal));
        Assert.Contains(all, line => line.Contains("export to CSV", StringComparison.Ordinal));
        Assert.Contains(all, line => line.Contains("show only", StringComparison.Ordinal));
        Assert.All(all, line => Assert.True(line.Length <= 74, $"too wide: '{line}'"));
    }

    [Fact]
    public void Every_context_has_registered_keys()
    {
        foreach (var context in Enum.GetValues<KeyContext>())
        {
            Assert.NotEmpty(KeyHelp.Footer(context));
            Assert.True(KeyHelp.Render(context).Count > 2, context.ToString());
        }
    }
}
