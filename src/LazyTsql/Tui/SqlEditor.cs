using LazyTsql.Core.Syntax;
using Terminal.Gui.Drawing;
using Terminal.Gui.Views;

// TextView is marked obsolete in favor of tui-cs/Editor; its color hook is enough for T-SQL highlighting.
#pragma warning disable CS0618

namespace LazyTsql.Tui;

/// <summary>Query editor with T-SQL highlighting. The script is re-tokenized on the first draw after an edit.</summary>
public sealed class SqlEditor : TextView
{
    private SqlTokenKind[][]? _kinds;

    public SqlEditor() => ContentsChanged += (_, _) => _kinds = null;

    protected override void OnDrawNormalColor(List<Cell> line, int idxCol, int idxRow)
    {
        _kinds ??= SqlTokenizer.Classify(Text.ReplaceLineEndings("\n").Split('\n'));
        var kinds = idxRow < _kinds.Length ? _kinds[idxRow] : [];
        if (idxCol < kinds.Length && Theme.Syntax(kinds[idxCol]) is { } attribute) SetAttribute(attribute);
        else base.OnDrawNormalColor(line, idxCol, idxRow);
    }
}
