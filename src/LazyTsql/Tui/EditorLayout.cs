using Terminal.Gui.Text;

namespace LazyTsql.Tui;

/// <summary>Where the glyphs of one editor line land on screen, for the rows <see cref="SqlEditor"/> draws.</summary>
public static class EditorLayout
{
    /// <summary>A glyph's position in the line, its screen column (negative when it starts left of the edge) and width.</summary>
    public readonly record struct Glyph(int Index, int Column, int Width);

    /// <summary>
    /// The glyphs visible between <paramref name="scrollX"/> and <paramref name="scrollX"/> + <paramref name="width"/>:
    /// tabs reach the next multiple of <paramref name="tabWidth"/> (clipped at the right edge), wide glyphs take two
    /// columns and are never split at the right edge, and a glyph straddling the left edge keeps its negative column.
    /// </summary>
    public static List<Glyph> Visible(IReadOnlyList<string> graphemes, int scrollX, int width, int tabWidth)
    {
        var glyphs = new List<Glyph>();
        var column = 0;
        for (var i = 0; i < graphemes.Count; i++)
        {
            var text = graphemes[i];
            var tab = text == "\t";
            var cells = tab ? tabWidth > 0 ? tabWidth - column % tabWidth : 0 : Math.Max(text.GetColumns(false), 1);
            var start = column - scrollX;
            column += cells;
            if (start + cells <= 0 && cells > 0) continue;
            if (start >= width) break;
            if (start + cells > width)
            {
                if (tab) glyphs.Add(new Glyph(i, start, width - start));
                break;
            }

            glyphs.Add(new Glyph(i, start, cells));
        }

        return glyphs;
    }
}
