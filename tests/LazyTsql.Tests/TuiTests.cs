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
}
