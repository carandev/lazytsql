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
/// Query editor with T-SQL highlighting and completion. The script's lines are read once per edit and re-tokenized on
/// the first draw after it; highlighting, unknown-name marks and completion all share them. Unknown names are found off
/// the UI thread once the editor is still, so typing never waits for them.
/// </summary>
public sealed class SqlEditor : TextView
{
    // Terminal.Gui 2.5 runs these without raising ContentsChanged, so every cache keyed on edits missed them.
    private static readonly Command[] SilentEdits = [Command.KillWordRight, Command.KillWordLeft, Command.CutToStartOfLine, Command.CutToEndOfLine];

    private readonly DiagnosticsWorker _worker;
    private string[]? _lines;
    private SqlTokenKind[][]? _kinds;
    private Func<(SchemaCatalog Catalog, string Database)?>? _source;
    private IReadOnlyList<Diagnostic> _diagnostics = [];

    // What the marks were last asked for; a catalog load bumps the generation so they are asked for again.
    private (string[] Lines, (int Row, int Column)? Cursor, string Database, int Generation)? _diagnosedFor;
    private int _catalogGeneration;

    public SqlEditor()
    {
        PostToUi = action => App?.Invoke(action);
        _worker = new DiagnosticsWorker(action => PostToUi(action));
        ContentsChanged += (_, _) => OnEdited();
        // TextView attaches it to itself in EndInit, once the view is in the tree.
        Autocomplete = new PrefixAutocomplete();
    }

    /// <summary>How long the editor must be still before unknown names are looked for.</summary>
    internal TimeSpan DiagnosticsDelay { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Runs a background result on the UI thread; tests replace it.</summary>
    internal Action<Action> PostToUi { get; set; }

    /// <summary>The unknown names shown now.</summary>
    internal IReadOnlyList<Diagnostic> UnknownNames => _diagnostics;

    /// <summary>The last background search for unknown names.</summary>
    internal Task PendingDiagnostics => _worker.Pending;

    /// <summary>The script split into lines, read from the editor once per edit.</summary>
    internal IReadOnlyList<string> ScriptLines => ReadLines();

    private string[] ReadLines() => _lines ??= Text.ReplaceLineEndings("\n").Split('\n');

    /// <summary>
    /// After an edit the old marks stay until the new ones arrive, except on the edited row, whose columns moved; when
    /// rows were added or removed every mark moved, so none stay.
    /// </summary>
    private void OnEdited()
    {
        var rows = _lines?.Length;
        (_lines, _kinds) = (null, null);
        var row = CurrentRow;
        _diagnostics = rows == Lines ? [.. _diagnostics.Where(d => d.Row != row)] : [];
        (Autocomplete as PrefixAutocomplete)?.Edited();
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
        _catalogGeneration++;
        (Autocomplete.SuggestionGenerator as SqlSuggestionGenerator)?.Forget();
        SetNeedsDraw();
        if (!HasFocus || SelectedLength > 0) return;
        ShowSuggestions();
    }

    /// <summary>Mirrors what TextView does after each edit, the only time it updates the popup.</summary>
    private void ShowSuggestions()
    {
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
        // Terminal.Gui 2.5 does not reopen a closed popup on its Reopen key, so the editor does.
        if (key == Autocomplete.Reopen && _source is not null && SelectedLength == 0)
        {
            (Autocomplete as PrefixAutocomplete)?.OpenHere();
            ShowSuggestions();
            return true;
        }

        if (!base.OnKeyDown(key)) return IsSilentEdit(key) && CutAndReport(key);
        if (key == Key.CursorDown || key == Key.CursorUp) App?.TopRunnableView?.SetNeedsDraw();
        return true;
    }

    private bool IsSilentEdit(Key key) => KeyBindings.TryGet(key, out var binding) && binding.Commands.Length == 1 && SilentEdits.Contains(binding.Commands[0]);

    /// <summary>
    /// Runs a word or line cut in place of the key binding and raises ContentsChanged when it changed the text, which
    /// Terminal.Gui 2.5 does not do for these commands.
    /// </summary>
    private bool CutAndReport(Key key)
    {
        var before = Text;
        var handled = InvokeCommand(KeyBindings.GetCommands(key)[0]) == true;
        if (Text != before) OnContentsChanged();
        return handled;
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
        ScheduleDiagnostics();
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
        _kinds ??= SqlTokenizer.Classify(ScriptLines);
        var kinds = idxRow < _kinds.Length ? _kinds[idxRow] : [];
        if (IsUnknownName(idxRow, idxCol)) SetAttribute(Theme.UnknownName);
        else if (idxCol < kinds.Length && Theme.Syntax(kinds[idxCol]) is { } attribute) SetAttribute(attribute);
        else base.OnDrawNormalColor(line, idxCol, idxRow);
    }

    private bool IsUnknownName(int row, int column)
    {
        foreach (var diagnostic in _diagnostics)
        {
            if (diagnostic.Row == row && diagnostic.Start <= column && column < diagnostic.End) return true;
        }

        return false;
    }

    /// <summary>
    /// Asks the worker for the names the catalog does not have, after an edit, a cursor move (the name being typed is
    /// not marked) or a catalog load; the marks are drawn when they arrive.
    /// </summary>
    internal void ScheduleDiagnostics()
    {
        if (_source?.Invoke() is not var (catalog, database))
        {
            _worker.Cancel();
            (_diagnostics, _diagnosedFor) = ([], null);
            return;
        }

        var lines = ReadLines();
        (int Row, int Column)? cursor = HasFocus ? (CurrentRow, CurrentColumn) : null;
        if (_diagnosedFor is { } last && ReferenceEquals(last.Lines, lines) && last.Cursor == cursor && last.Database == database
            && last.Generation == _catalogGeneration)
        {
            return;
        }

        _diagnosedFor = (lines, cursor, database, _catalogGeneration);
        _worker.Schedule(lines, catalog, database, cursor, DiagnosticsDelay, diagnostics =>
        {
            _diagnostics = diagnostics;
            SetNeedsDraw();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _worker.Cancel();
        base.Dispose(disposing);
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

        // Where suggestions may show: where the last edit left the cursor, or where Ctrl+Space asked for them. TextView
        // asks for suggestions on every cursor move too, which opened the popup on any word the arrows reached and then
        // took ↑/↓ for the list; like other editors, moving shows none.
        private (int Row, int Column)? _openAt;
        private bool _edited;

        public void Edited() => _edited = true;

        public void OpenHere()
        {
            _accepted = null;
            _edited = false;
            if (HostControl is TextView editor) _openAt = (editor.CurrentRow, editor.CurrentColumn);
        }

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
            if (HostControl is not TextView editor)
            {
                base.GenerateSuggestions(context);
                return;
            }

            var cursor = (editor.CurrentRow, editor.CurrentColumn);
            if (_accepted is { } at && (at == (-1, -1) || at == cursor))
            {
                // Accepting inserts text, which is an edit, but not one that should reopen the list.
                (_edited, _openAt) = (false, null);
                ClearSuggestions();
                return;
            }

            _accepted = null;
            if (_edited) (_openAt, _edited) = (cursor, false);
            if (_openAt != cursor)
            {
                // Once the cursor left, coming back is still a move: only an edit or Ctrl+Space reopens the list.
                _openAt = null;
                ClearSuggestions();
                return;
            }

            base.GenerateSuggestions(context);
        }
    }
}
