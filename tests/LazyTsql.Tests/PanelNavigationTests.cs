using System.Drawing;
using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class PanelNavigationTests
{
    // The main view at 120x40: connections, files and schema on the left; query, results and messages on the right.
    private static readonly Rectangle[] Panels =
    [
        new(0, 0, 34, 8),
        new(0, 8, 34, 12),
        new(0, 20, 34, 19),
        new(34, 0, 86, 15),
        new(34, 15, 86, 17),
        new(34, 32, 86, 7),
    ];

    [Theory]
    [InlineData(3, 'h', 0)]
    [InlineData(4, 'h', 2)]
    [InlineData(5, 'h', 2)]
    [InlineData(1, 'l', 3)]
    [InlineData(2, 'l', 4)]
    [InlineData(3, 'j', 4)]
    [InlineData(4, 'j', 5)]
    [InlineData(5, 'k', 4)]
    [InlineData(1, 'k', 0)]
    [InlineData(0, 'j', 1)]
    public void Moves_to_the_neighbouring_panel_on_that_side(int from, char direction, int expected) =>
        Assert.Equal(expected, PanelNavigation.Next(Panels, from, direction));

    [Theory]
    [InlineData(0, 'h')]
    [InlineData(3, 'k')]
    [InlineData(3, 'l')]
    [InlineData(5, 'j')]
    public void Stays_when_there_is_no_panel_on_that_side(int from, char direction) =>
        Assert.Null(PanelNavigation.Next(Panels, from, direction));
}
