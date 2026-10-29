using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class LineNumbersTests
{
    [Theory]
    [InlineData(1, 3)]
    [InlineData(9, 3)]
    [InlineData(10, 3)]
    [InlineData(999, 4)]
    [InlineData(20000, 6)]
    public void The_gutter_fits_the_largest_line_number_and_a_space(int lines, int width)
    {
        Assert.Equal(width, LineNumbers.WidthFor(lines));
    }

    [Fact]
    public void Numbers_are_one_based_and_right_aligned()
    {
        Assert.Equal(" 1 ", LineNumbers.Label(0, 3));
        Assert.Equal("42 ", LineNumbers.Label(41, 3));
        Assert.Equal("   7 ", LineNumbers.Label(6, 5));
    }
}
