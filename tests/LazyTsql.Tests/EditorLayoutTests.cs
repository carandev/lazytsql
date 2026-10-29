using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class EditorLayoutTests
{
    private static string[] Glyphs(string text) => [.. text.Select(c => c.ToString())];

    [Fact]
    public void A_line_is_laid_out_from_the_left_edge_until_the_right_one()
    {
        var glyphs = EditorLayout.Visible(Glyphs("SELECT 1"), scrollX: 0, width: 4, tabWidth: 4);

        Assert.Equal([(0, 0, 1), (1, 1, 1), (2, 2, 1), (3, 3, 1)], glyphs.Select(g => (g.Index, g.Column, g.Width)));
    }

    [Fact]
    public void A_tab_reaches_the_next_tab_stop()
    {
        var glyphs = EditorLayout.Visible(["a", "\t", "b"], scrollX: 0, width: 20, tabWidth: 4);

        Assert.Equal([(0, 0, 1), (1, 1, 3), (2, 4, 1)], glyphs.Select(g => (g.Index, g.Column, g.Width)));
    }

    [Fact]
    public void Scrolling_right_skips_what_is_off_the_left_edge()
    {
        var glyphs = EditorLayout.Visible(Glyphs("abcdef"), scrollX: 2, width: 3, tabWidth: 4);

        Assert.Equal([(2, 0, 1), (3, 1, 1), (4, 2, 1)], glyphs.Select(g => (g.Index, g.Column, g.Width)));
    }

    [Fact]
    public void A_wide_glyph_takes_two_columns_and_is_not_split_at_an_edge()
    {
        Assert.Equal([(0, 0, 1), (1, 1, 2), (2, 3, 1)], EditorLayout.Visible(["a", "漢", "b"], 0, 10, 4).Select(g => (g.Index, g.Column, g.Width)));
        // At the right edge it does not fit; at the left edge it starts off screen.
        Assert.Equal([(0, 0, 1)], EditorLayout.Visible(["a", "漢"], 0, 2, 4).Select(g => (g.Index, g.Column, g.Width)));
        Assert.Equal([(1, -1, 2), (2, 1, 1)], EditorLayout.Visible(["a", "漢", "b"], 2, 10, 4).Select(g => (g.Index, g.Column, g.Width)));
    }
}
