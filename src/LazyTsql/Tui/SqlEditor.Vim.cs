using System.Drawing;
using LazyTsql.Tui.Vim;
using Terminal.Gui.Drivers;
using Terminal.Gui.Input;

#pragma warning disable CS0618

namespace LazyTsql.Tui;

/// <summary>
/// The editor as vim's buffer: with vim mode on, keys go to the <see cref="VimEngine"/> first, which edits through the
/// editor's own insert and delete. Undo is vim's: each command, and each stay in insert mode with the command that
/// started it, is one step. The visual selection is drawn here.
/// </summary>
public sealed partial class SqlEditor : IVimBuffer
{
    private VimEngine? _vim;
    private (VimPos Anchor, VimPos Cursor, bool Linewise)? _visual;
    // The text before each change, for u and Ctrl+R. Terminal.Gui's own history splits a several-line insert into pieces.
    private readonly Stack<(string Text, Point Cursor)> _undo = new(), _redo = new();
    // Set once the current key saved the text, so a command made of several edits is still one undo step.
    private bool _saved;

    public VimEngine Vim => _vim ??= new VimEngine(this);

    public bool VimEnabled { get; private set; }

    /// <summary>Turns vim mode on (in normal mode) or off (back to the plain editor).</summary>
    public void SetVimEnabled(bool enabled)
    {
        VimEnabled = enabled;
        Vim.Reset();
        ForgetVimHistory();
        SetNeedsDraw();
    }

    /// <summary>Drops vim's undo steps, when the editor shows other text (another tab, a file just opened).</summary>
    public void ForgetVimHistory()
    {
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>Normal, visual and command-line modes type no text, so the completion list stays closed in them.</summary>
    internal bool CompletionBlocked => VimEnabled && Vim.Mode != VimMode.Insert;

    IReadOnlyList<string> IVimBuffer.Lines => ScriptLines;

    VimPos IVimBuffer.Cursor
    {
        get => new(CurrentRow, CurrentColumn);
        set
        {
            IsSelecting = false;
            InsertionPoint = new Point(value.Col, value.Row);
        }
    }

    int IVimBuffer.PageRows => Math.Max(Viewport.Height, 1);

    void IVimBuffer.Replace(VimPos start, VimPos end, string text)
    {
        // Terminal.Gui 2.5 inserts several lines correctly only at a line's end: the rest of the line stays on the first
        // line. Replacing up to the end of the row, the rest typed back after the text, keeps it one edit.
        var lines = ScriptLines;
        if (text.Contains('\n') && end.Col < lines[end.Row].Length)
        {
            text += lines[end.Row][end.Col..];
            end = end with { Col = lines[end.Row].Length };
        }

        SaveForUndo();
        IsSelecting = false;
        InsertionPoint = new Point(start.Col, start.Row);
        if (start != end)
        {
            // Selected from the end back to the start, the delete or insert below replaces exactly that range.
            IsSelecting = true;
            SelectionStartRow = end.Row;
            SelectionStartColumn = end.Col;
            if (text.Length == 0) DeleteCharLeft();
        }

        if (text.Length > 0) InsertText(text);
        IsSelecting = false;
        Edited();
    }

    void IVimBuffer.Undo() => Restore(_undo, _redo);

    void IVimBuffer.Redo() => Restore(_redo, _undo);

    private void SaveForUndo()
    {
        if (_saved) return;
        _saved = true;
        _undo.Push((Text, InsertionPoint));
        _redo.Clear();
    }

    private void Restore(Stack<(string Text, Point Cursor)> from, Stack<(string Text, Point Cursor)> to)
    {
        if (from.Count == 0) return;
        to.Push((Text, InsertionPoint));
        var (text, cursor) = from.Pop();
        Text = text;
        InsertionPoint = cursor;
        Edited();
    }

    void IVimBuffer.Select(VimPos anchor, VimPos cursor, bool linewise)
    {
        _visual = (anchor, cursor, linewise);
        SetNeedsDraw();
    }

    void IVimBuffer.ClearSelection()
    {
        _visual = null;
        SetNeedsDraw();
    }

    /// <summary>The cached lines and colors follow the edit even when the editor did not report it.</summary>
    private void Edited()
    {
        if (_lines is not null) OnEdited();
        SetNeedsDraw();
    }

    /// <summary>Whether a cell is in the visual selection, both ends included.</summary>
    private bool InVisual(int column, int row)
    {
        if (_visual is not { } visual) return false;
        var (from, to) = visual.Anchor <= visual.Cursor ? (visual.Anchor, visual.Cursor) : (visual.Cursor, visual.Anchor);
        if (visual.Linewise) return row >= from.Row && row <= to.Row;
        var cell = new VimPos(row, column);
        return cell >= from && cell <= to;
    }

    /// <summary>Gives a key to vim first; true when vim used it.</summary>
    private bool HandleVimKey(Key key)
    {
        if (!VimEnabled || ToVimKey(key) is not { } vimKey) return false;
        // Esc closes the completion list and leaves insert mode at once, as in vim.
        if (vimKey.Char == VimKey.Escape && Vim.Mode == VimMode.Insert && Autocomplete.Visible) base.OnKeyDown(key);
        var wasInsert = Vim.Mode == VimMode.Insert;
        _saved = false;
        if (!Vim.HandleKey(vimKey)) return false;
        // i, a, o and the like: what gets typed next is undone together with them.
        if (!wasInsert && Vim.Mode == VimMode.Insert) SaveForUndo();
        SetNeedsDraw();
        return true;
    }

    /// <summary>The key as vim reads it; arrows are h j k l outside insert mode. Alt keys and the rest stay the editor's.</summary>
    private VimKey? ToVimKey(Key key)
    {
        if (key.IsAlt) return null;
        if (key == Key.Esc) return new VimKey(VimKey.Escape);
        if (key == Key.Enter) return new VimKey(VimKey.Enter);
        if (key == Key.Backspace) return new VimKey(VimKey.Backspace);
        if (Vim.Mode is not (VimMode.Insert or VimMode.Command))
        {
            if (key == Key.CursorLeft) return new VimKey('h');
            if (key == Key.CursorDown) return new VimKey('j');
            if (key == Key.CursorUp) return new VimKey('k');
            if (key == Key.CursorRight) return new VimKey('l');
        }

        if (key.IsCtrl)
        {
            var code = key.NoCtrl.NoShift.KeyCode;
            return code is >= KeyCode.A and <= KeyCode.Z ? new VimKey((char)('a' + (code - KeyCode.A)), Ctrl: true) : null;
        }

        var rune = key.AsRune.Value;
        return rune is >= 32 and < 0xFFFF and not 127 ? new VimKey((char)rune) : null;
    }
}
