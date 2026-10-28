using LazyTsql.Core.Completion;
using LazyTsql.Core.Syntax;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

// TextView is marked obsolete in favor of tui-cs/Editor; its color hook is enough for T-SQL highlighting.
#pragma warning disable CS0618

namespace LazyTsql.Tui;

/// <summary>
/// Query editor with T-SQL highlighting and completion. The script is re-tokenized on the first draw after an edit.
/// </summary>
public sealed class SqlEditor : TextView
{
    private SqlTokenKind[][]? _kinds;

    public SqlEditor()
    {
        ContentsChanged += (_, _) => _kinds = null;
        // TextView attaches it to itself in EndInit, once the view is in the tree.
        Autocomplete = new PrefixAutocomplete();
    }

    /// <summary>Turns on completion from <paramref name="source"/>: the catalog and current database, or null when offline.</summary>
    public void EnableCompletion(Func<(SchemaCatalog Catalog, string Database)?> source)
    {
        Autocomplete.SuggestionGenerator = new SqlSuggestionGenerator(this, source);
        Autocomplete.SelectionKey = Key.Tab;
        Autocomplete.CloseKey = Key.Esc;
        Autocomplete.Reopen = Key.Space.WithCtrl;
        Autocomplete.MaxHeight = 8;
        Autocomplete.MaxWidth = 60;
    }

    /// <summary>Recomputes the open suggestions, for names that finished loading after the last keystroke.</summary>
    public void RefreshCompletion()
    {
        if (!HasFocus || SelectedLength > 0) return;

        // Mirrors what TextView does after each edit, the only time it updates the popup.
        var line = GetCurrentLine();
        Autocomplete.Context = new AutocompleteContext(line, Math.Min(CurrentColumn, line.Count), false);
        Autocomplete.GenerateSuggestions(Autocomplete.Context);
        var at = new System.Drawing.Point(Autocomplete.Context.CursorPosition, Autocomplete.PopupInsideContainer ? InsertionPoint.Y + 1 - Viewport.Y : 0);
        // A closed popup only opens on the first call; the second one draws it.
        Autocomplete.RenderOverlay(at);
        Autocomplete.RenderOverlay(at);
    }

    protected override void OnDrawNormalColor(List<Cell> line, int idxCol, int idxRow)
    {
        _kinds ??= SqlTokenizer.Classify(Text.ReplaceLineEndings("\n").Split('\n'));
        var kinds = idxRow < _kinds.Length ? _kinds[idxRow] : [];
        if (idxCol < kinds.Length && Theme.Syntax(kinds[idxCol]) is { } attribute) SetAttribute(attribute);
        else base.OnDrawNormalColor(line, idxCol, idxRow);
    }

    /// <summary>
    /// TextView's popup moves the cursor forward by <see cref="Suggestion.Remove"/> before deleting backwards, which
    /// eats the text after the cursor when completing mid-line. This one deletes only the typed prefix.
    /// </summary>
    private sealed class PrefixAutocomplete : TextViewAutocomplete
    {
        // Where the cursor was left by the last accepted suggestion. A name typed in full is suggested, so without this
        // the popup would reopen right after accepting it, offering the same name.
        private (int Row, int Column)? _accepted;

        protected override bool InsertSelection(Suggestion accepted)
        {
            var editor = (TextView)HostControl!;
            _accepted = (-1, -1);
            for (var i = 0; i < accepted.Remove; i++) editor.DeleteCharLeft();
            editor.InsertText(accepted.Replacement);
            _accepted = (editor.CurrentRow, editor.CurrentColumn);
            return true;
        }

        public override void GenerateSuggestions(AutocompleteContext context)
        {
            if (HostControl is TextView editor && _accepted is { } at
                && (at == (-1, -1) || at == (editor.CurrentRow, editor.CurrentColumn)))
            {
                ClearSuggestions();
                return;
            }

            _accepted = null;
            base.GenerateSuggestions(context);
        }
    }
}
