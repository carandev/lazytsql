using LazyTsql.Output;

namespace LazyTsql.Tests;

public class CellCopyTests
{
    private static readonly object?[][] Rows = [[1, "Ana", null], [2, "two\nlines", "say \"hi\""], [3, "a\tb", 0.5m]];

    private static string Copy(params (int Row, int Column)[] cells) => CellCopy.Text(cells, (row, column) => Rows[row][column]);

    [Fact]
    public void One_cell_is_its_whole_value()
    {
        Assert.Equal("two\nlines", Copy((1, 1)));
        Assert.Equal("0.5", Copy((2, 2)));
    }

    [Fact]
    public void Null_is_empty()
    {
        Assert.Equal(string.Empty, Copy((0, 2)));
    }

    [Fact]
    public void A_selection_is_tab_separated_rows_in_table_order()
    {
        Assert.Equal("1\tAna\n2\t\"two\nlines\"", Copy((1, 1), (0, 0), (1, 0), (0, 1)));
    }

    [Fact]
    public void Fields_with_tabs_or_quotes_are_quoted_and_gaps_are_empty()
    {
        Assert.Equal("\t\"say \"\"hi\"\"\"\n\"a\tb\"\t", Copy((1, 2), (2, 1)));
    }
}
