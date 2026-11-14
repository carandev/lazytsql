namespace LazyTsql.Tui.Vim;

/// <summary>A place in the text: zero-based row and column. A column equal to the line's length is its line break.</summary>
public readonly record struct VimPos(int Row, int Col) : IComparable<VimPos>
{
    public int CompareTo(VimPos other) => Row != other.Row ? Row.CompareTo(other.Row) : Col.CompareTo(other.Col);

    public static bool operator <(VimPos left, VimPos right) => left.CompareTo(right) < 0;
    public static bool operator >(VimPos left, VimPos right) => left.CompareTo(right) > 0;
    public static bool operator <=(VimPos left, VimPos right) => left.CompareTo(right) <= 0;
    public static bool operator >=(VimPos left, VimPos right) => left.CompareTo(right) >= 0;
}

/// <summary>A key as vim reads it: a character, with Ctrl or not; Esc, Enter and Backspace are control characters.</summary>
public readonly record struct VimKey(char Char, bool Ctrl = false)
{
    public const char Escape = '\u001b';
    public const char Enter = '\r';
    public const char Backspace = '\b';
}

public enum VimMode
{
    Normal,
    Insert,
    Visual,
    VisualLine,
    Command,
}

/// <summary>The text vim edits. Every change goes through <see cref="Replace"/>, so the editor's own undo history keeps it.</summary>
public interface IVimBuffer
{
    IReadOnlyList<string> Lines { get; }

    VimPos Cursor { get; set; }

    /// <summary>Rows the view shows, for Ctrl+D and Ctrl+U.</summary>
    int PageRows { get; }

    /// <summary>Replaces the text from <paramref name="start"/> up to, not including, <paramref name="end"/>.</summary>
    void Replace(VimPos start, VimPos end, string text);

    void Undo();

    void Redo();

    /// <summary>Shows the visual selection, both ends included; a linewise one covers whole lines.</summary>
    void Select(VimPos anchor, VimPos cursor, bool linewise);

    void ClearSelection();
}
