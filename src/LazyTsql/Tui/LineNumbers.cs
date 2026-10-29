using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

#pragma warning disable CS0618

namespace LazyTsql.Tui;

/// <summary>
/// Line numbers beside the editor, so an error's line in the messages panel is easy to find. It follows the editor's
/// scrolling and highlights the cursor's line; the editor itself is untouched, so cursor and mouse mapping stay its own.
/// </summary>
public sealed class LineNumbers : View
{
    private readonly TextView _editor;
    private int _width;

    public LineNumbers(TextView editor)
    {
        _editor = editor;
        CanFocus = false;
        Height = Dim.Fill();
        Resize();
        editor.ViewportChanged += (_, _) => SetNeedsDraw();
        editor.UnwrappedCursorPositionChanged += (_, _) => SetNeedsDraw();
        editor.ContentsChanged += (_, _) => Resize();
    }

    /// <summary>Columns for the largest line number (at least two digits) and a space before the text.</summary>
    public static int WidthFor(int lines) => Math.Max(lines, 1).ToString(System.Globalization.CultureInfo.InvariantCulture).Length switch
    {
        < 2 => 3,
        var digits => digits + 1,
    };

    /// <summary>The one-based number of a zero-based line, right-aligned in <paramref name="width"/> with a trailing space.</summary>
    public static string Label(int line, int width) => (line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(width - 1) + " ";

    private void Resize()
    {
        var width = WidthFor(_editor.Lines);
        if (width != _width)
        {
            _width = width;
            Width = width;
            SuperView?.SetNeedsLayout();
        }

        SetNeedsDraw();
    }

    protected override bool OnDrawingContent(DrawContext? context)
    {
        var top = _editor.Viewport.Y;
        for (var row = 0; row < Viewport.Height; row++)
        {
            var line = top + row;
            SetAttribute(line == _editor.CurrentRow ? Theme.CurrentLineNumber : Theme.LineNumber);
            AddStr(0, row, line < _editor.Lines ? Label(line, Viewport.Width) : new string(' ', Viewport.Width));
        }

        return true;
    }
}
