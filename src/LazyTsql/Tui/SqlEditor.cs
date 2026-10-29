using LazyTsql.Core.Completion;
using LazyTsql.Core.Syntax;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
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
    private Func<(SchemaCatalog Catalog, string Database)?>? _source;
    private IReadOnlyList<Diagnostic>? _diagnostics;
    private (int Row, int Column) _diagnosedAt;

    public SqlEditor()
    {
        ContentsChanged += (_, _) => (_kinds, _diagnostics) = (null, null);
        // TextView attaches it to itself in EndInit, once the view is in the tree.
        Autocomplete = new PrefixAutocomplete();
    }

    /// <summary>Turns on completion from <paramref name="source"/>: the catalog and current database, or null when offline.</summary>
    public void EnableCompletion(Func<(SchemaCatalog Catalog, string Database)?> source)
    {
        _source = source;
        Autocomplete.SuggestionGenerator = new SqlSuggestionGenerator(this, source);
        Autocomplete.SelectionKey = Key.Tab;
        Autocomplete.CloseKey = Key.Esc;
        Autocomplete.Reopen = Key.Space.WithCtrl;
        Autocomplete.MaxHeight = 8;
        Autocomplete.MaxWidth = 60;
    }

    /// <summary>
    /// Recomputes the unknown-name marks and the open suggestions, for names that finished loading after the last
    /// keystroke.
    /// </summary>
    public void RefreshCompletion()
    {
        _diagnostics = null;
        SetNeedsDraw();
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

    /// <summary>
    /// Terminal.Gui 2.5's suggestion popup moves its selection on ↑/↓ but asks only the editor to redraw, not the popup
    /// drawn over it, so the highlight stayed on the first suggestion while Tab took the selected one.
    /// </summary>
    protected override bool OnKeyDown(Key key)
    {
        if (!base.OnKeyDown(key)) return false;
        if (key == Key.CursorDown || key == Key.CursorUp) App?.TopRunnableView?.SetNeedsDraw();
        return true;
    }

    /// <summary>
    /// Terminal.Gui 2.5's TextView inserts a paste without asking to redraw unless it replaced a selection, so a paste
    /// stayed invisible until something else redrew the editor.
    /// </summary>
    protected override bool OnPaste(string text)
    {
        if (!base.OnPaste(text)) return false;
        SetNeedsDraw();
        return true;
    }

    /// <summary>
    /// Terminal.Gui 2.5's TextView keeps drawing past the bottom of the view, through every line to the end of the text,
    /// so each keystroke or save in a long script took seconds. This draws only the visible rows, with the same colors
    /// and the same handling of tabs, wide glyphs and horizontal scrolling.
    /// </summary>
    protected override bool OnDrawingContent(DrawContext? context)
    {
        SetAttributeForRole(Enabled ? VisualRole.Editable : VisualRole.Disabled);
        var (right, bottom) = (Viewport.Width, Viewport.Height);
        var row = 0;
        for (var idxRow = Viewport.Y; idxRow < Lines && row < bottom; idxRow++, row++)
        {
            var line = GetLine(idxRow);
            var end = 0;
            Move(0, row);
            foreach (var glyph in EditorLayout.Visible([.. line.Select(cell => cell.Grapheme)], Viewport.X, right, TabWidth))
            {
                var (idxCol, col) = (glyph.Index, glyph.Column);
                if (IsSelecting && InSelection(idxCol, idxRow)) OnDrawSelectionColor(line, idxCol, idxRow);
                else if (idxCol == CurrentColumn && idxRow == CurrentRow && !IsSelecting && !Used && HasFocus) OnDrawUsedColor(line, idxCol, idxRow);
                else if (ReadOnly) OnDrawReadOnlyColor(line, idxCol, idxRow);
                else OnDrawNormalColor(line, idxCol, idxRow);

                // A tab is spaces; a wide glyph cut by the left edge shows as a space, like the base view.
                var text = line[idxCol].Grapheme;
                if (text == "\t" || col < 0)
                {
                    for (var i = Math.Max(col, 0); i < col + glyph.Width && i < right; i++) AddRune(i, row, (System.Text.Rune)' ');
                }
                else
                {
                    AddStr(col, row, text);
                }

                end = col + glyph.Width;
            }

            if (end < right)
            {
                SetAttributeForRole(ReadOnly ? VisualRole.ReadOnly : VisualRole.Editable);
                Blank(Math.Max(end, 0), row, right, row + 1);
            }
        }

        if (row < bottom)
        {
            SetAttributeForRole(ReadOnly ? VisualRole.ReadOnly : VisualRole.Editable);
            Blank(0, row, right, bottom);
        }

        return false;
    }

    private void Blank(int left, int top, int right, int bottom)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++) AddRune(x, y, (System.Text.Rune)' ');
        }
    }

    /// <summary>Whether a cell is between the selection start and the cursor, in either order.</summary>
    private bool InSelection(int column, int row)
    {
        var (start, cursor, point) = ((SelectionStartRow, SelectionStartColumn), (CurrentRow, CurrentColumn), (row, column));
        var (from, to) = start.CompareTo(cursor) <= 0 ? (start, cursor) : (cursor, start);
        return point.CompareTo(from) >= 0 && point.CompareTo(to) < 0;
    }

    protected override void OnDrawNormalColor(List<Cell> line, int idxCol, int idxRow)
    {
        _kinds ??= SqlTokenizer.Classify(Text.ReplaceLineEndings("\n").Split('\n'));
        var kinds = idxRow < _kinds.Length ? _kinds[idxRow] : [];
        if (UnknownNames().Any(d => d.Row == idxRow && d.Start <= idxCol && idxCol < d.End)) SetAttribute(Theme.UnknownName);
        else if (idxCol < kinds.Length && Theme.Syntax(kinds[idxCol]) is { } attribute) SetAttribute(attribute);
        else base.OnDrawNormalColor(line, idxCol, idxRow);
    }

    /// <summary>Names the catalog does not have; recomputed after an edit, a cursor move or a catalog load.</summary>
    private IReadOnlyList<Diagnostic> UnknownNames()
    {
        var at = (CurrentRow, CurrentColumn);
        if (_diagnostics is not null && _diagnosedAt == at) return _diagnostics;
        _diagnosedAt = at;
        _diagnostics = _source?.Invoke() is var (catalog, database)
            ? SqlCompletion.Diagnose(Text.ReplaceLineEndings("\n").Split('\n'), catalog, database, HasFocus ? at : null)
            : [];
        return _diagnostics;
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
