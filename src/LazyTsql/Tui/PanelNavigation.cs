using System.Drawing;

namespace LazyTsql.Tui;

/// <summary>
/// Ctrl+h/j/k/l in vim mode, like moving between vim windows: the panel next to the focused one on that side, read from
/// where the panels are, so it follows resized panels.
/// </summary>
public static class PanelNavigation
{
    /// <summary>
    /// The nearest panel on the <paramref name="direction"/> side (h, j, k or l) that lines up with the focused one,
    /// preferring the one beside its middle; null at the edge.
    /// </summary>
    public static int? Next(IReadOnlyList<Rectangle> panels, int from, char direction)
    {
        var current = panels[from];
        var horizontal = direction is 'h' or 'l';
        var middle = horizontal ? current.Y + current.Height / 2 : current.X + current.Width / 2;
        return Enumerable.Range(0, panels.Count)
            .Where(i => i != from && IsOnSide(current, panels[i], direction))
            .OrderBy(i => Distance(current, panels[i], direction))
            .ThenBy(i => Contains(panels[i], middle, horizontal) ? 0 : 1)
            .ThenBy(i => Math.Abs((horizontal ? panels[i].Y + panels[i].Height / 2 : panels[i].X + panels[i].Width / 2) - middle))
            .Cast<int?>()
            .FirstOrDefault();
    }

    private static bool IsOnSide(Rectangle current, Rectangle other, char direction) => direction switch
    {
        'h' => other.Right <= current.Left && Overlap(current.Top, current.Bottom, other.Top, other.Bottom),
        'l' => other.Left >= current.Right && Overlap(current.Top, current.Bottom, other.Top, other.Bottom),
        'k' => other.Bottom <= current.Top && Overlap(current.Left, current.Right, other.Left, other.Right),
        'j' => other.Top >= current.Bottom && Overlap(current.Left, current.Right, other.Left, other.Right),
        _ => false,
    };

    private static int Distance(Rectangle current, Rectangle other, char direction) => direction switch
    {
        'h' => current.Left - other.Right,
        'l' => other.Left - current.Right,
        'k' => current.Top - other.Bottom,
        _ => other.Top - current.Bottom,
    };

    private static bool Overlap(int start, int end, int otherStart, int otherEnd) => start < otherEnd && otherStart < end;

    private static bool Contains(Rectangle panel, int middle, bool horizontal) =>
        horizontal ? middle >= panel.Top && middle < panel.Bottom : middle >= panel.Left && middle < panel.Right;
}
