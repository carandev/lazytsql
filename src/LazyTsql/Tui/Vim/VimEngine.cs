namespace LazyTsql.Tui.Vim;

/// <summary>
/// Vim's modes over a buffer: normal-mode commands with counts, operators on motions and text objects, visual
/// selections, and a command line for <c>:</c>, <c>/</c> and <c>?</c>. Insert mode leaves the keys to the editor.
/// Commands the editor runs (<c>:w</c>, <c>gt</c>, <c>ZZ</c>...) come out through <see cref="CommandEntered"/>.
/// </summary>
public sealed class VimEngine(IVimBuffer buffer)
{
    private const int MaxCount = 10_000;
    private const string MotionKeys = "hjklwbeWBE0^$G;,%nN*";
    private const string Indentation = "    ";

    private string _pending = string.Empty;
    private VimPos _anchor;
    // The column j and k keep while passing through shorter lines; int.MaxValue after $.
    private int? _column;
    private (string Text, bool Linewise) _register = (string.Empty, false);
    private (char Kind, char Target)? _lastFind;
    private (string Pattern, bool Forward, bool WholeWord)? _lastSearch;
    private char _prompt;
    private string _typed = string.Empty;

    public VimMode Mode { get; private set; }

    /// <summary>What is being typed after <c>:</c>, <c>/</c> or <c>?</c>, prompt included; empty in the other modes.</summary>
    public string CommandLine => Mode == VimMode.Command ? _prompt + _typed : string.Empty;

    /// <summary>The keys of a command not finished yet, like vim's showcmd.</summary>
    public string PendingKeys => _pending;

    /// <summary>An ex command to run (<c>w</c>, <c>q!</c>, <c>tabnext</c>...), trimmed; line numbers are handled here.</summary>
    public event Action<string>? CommandEntered;

    /// <summary>Text yanked with <c>y</c>, for the system clipboard.</summary>
    public event Action<string>? Yanked;

    public event Action<string>? Error;

    /// <summary>Raised after every key, for the mode and command line shown around the editor.</summary>
    public event Action? Changed;

    private VimMotions Text => new(buffer.Lines);

    private bool InVisual => Mode is VimMode.Visual or VimMode.VisualLine;

    /// <summary>Handles a key; false leaves it to the editor (typing in insert mode, Ctrl keys vim does not use).</summary>
    public bool HandleKey(VimKey key)
    {
        var handled = Mode switch
        {
            VimMode.Insert => InsertKey(key),
            VimMode.Command => CommandKey(key),
            _ => NormalOrVisualKey(key),
        };
        Changed?.Invoke();
        return handled;
    }

    /// <summary>Back to normal mode with nothing pending, as when vim mode is switched on or a tab changes.</summary>
    public void Reset()
    {
        if (InVisual) buffer.ClearSelection();
        Mode = VimMode.Normal;
        (_pending, _typed, _column) = (string.Empty, string.Empty, null);
        buffer.Cursor = Clamp(buffer.Cursor);
        Changed?.Invoke();
    }

    private bool InsertKey(VimKey key)
    {
        if (key.Ctrl || key.Char != VimKey.Escape) return false;
        Mode = VimMode.Normal;
        buffer.Cursor = Clamp(buffer.Cursor with { Col = buffer.Cursor.Col - 1 });
        return true;
    }

    private bool NormalOrVisualKey(VimKey key)
    {
        if (key.Ctrl)
        {
            _pending = string.Empty;
            switch (char.ToLowerInvariant(key.Char))
            {
                case 'd': MoveHalfPage(+1); return true;
                case 'u': MoveHalfPage(-1); return true;
                case 'r' when !InVisual:
                    buffer.Redo();
                    buffer.Cursor = Clamp(buffer.Cursor);
                    return true;
                default: return false;
            }
        }

        if (key.Char == VimKey.Escape)
        {
            _pending = string.Empty;
            if (InVisual) ExitVisual();
            return true;
        }

        _pending += key.Char switch { VimKey.Enter => 'j', VimKey.Backspace => 'h', var ch => ch };
        if (InVisual ? VisualCommand(_pending) : NormalCommand(_pending)) _pending = string.Empty;
        return true;
    }

    /// <summary>Runs a complete normal-mode command; false while more keys are needed. Unknown commands are dropped.</summary>
    private bool NormalCommand(string keys)
    {
        var i = 0;
        var count = ReadCount(keys, ref i);
        var n = Math.Max(count, 1);
        if (i == keys.Length) return false;
        var key = keys[i];
        var more = keys.Length - i - 1;
        var at = buffer.Cursor;
        var text = Text;

        switch (key)
        {
            case ':' or '/' or '?': StartCommand(key); break;
            case 'v': StartVisual(VimMode.Visual); break;
            case 'V': StartVisual(VimMode.VisualLine); break;
            case 'i': StartInsert(at); break;
            case 'a': StartInsert(at with { Col = Math.Min(at.Col + 1, text.Length(at.Row)) }); break;
            case 'I': StartInsert(at with { Col = text.Indent(at.Row).Length }); break;
            case 'A': StartInsert(at with { Col = text.Length(at.Row) }); break;
            case 'o': OpenLine(below: true); break;
            case 'O': OpenLine(below: false); break;
            case 'x': DeleteChars(n, forward: true); break;
            case 'X': DeleteChars(n, forward: false); break;
            case 's': Charwise('c', at, at with { Col = Math.Min(at.Col + n, text.Length(at.Row)) }); break;
            case 'S': Linewise('c', at.Row, at.Row + n - 1); break;
            case 'D': OperatorOn('d', new Target(new VimPos(Math.Min(at.Row + n - 1, text.LastRow), text.Length(Math.Min(at.Row + n - 1, text.LastRow))), false, false), '$'); break;
            case 'C': OperatorOn('c', new Target(new VimPos(Math.Min(at.Row + n - 1, text.LastRow), text.Length(Math.Min(at.Row + n - 1, text.LastRow))), false, false), '$'); break;
            case 'Y': Linewise('y', at.Row, at.Row + n - 1); break;
            case 'p' or 'P': Paste(before: key == 'P', n); break;
            case 'u':
                for (var k = 0; k < n; k++) buffer.Undo();
                buffer.Cursor = Clamp(buffer.Cursor);
                break;
            case 'J': Join(n); break;
            case '~': ToggleCase(n); break;
            case 'r':
                if (more == 0) return false;
                ReplaceChars(keys[i + 1], n);
                break;
            case 'Z':
                if (more == 0) return false;
                if (keys[i + 1] == 'Z') CommandEntered?.Invoke("x");
                else if (keys[i + 1] == 'Q') CommandEntered?.Invoke("q!");
                break;
            case 'g' when more == 0: return false;
            case 'g' when keys[i + 1] is 't' or 'T': CommandEntered?.Invoke(keys[i + 1] == 't' ? "tabnext" : "tabprevious"); break;
            case 'd' or 'c' or 'y' or '>' or '<': return Operator(keys, i + 1, key, count);
            default:
                var (status, motion, arg) = ParseMotion(keys, i);
                if (status == Parse.Incomplete) return false;
                if (status == Parse.Done && Move(motion, arg, n, count > 0) is { } target) MoveCursor(target, motion);
                break;
        }

        return true;
    }

    /// <summary>An operator followed by itself (whole lines), a motion or a text object; counts before and after multiply.</summary>
    private bool Operator(string keys, int i, char op, int count)
    {
        if (i == keys.Length) return false;
        var at = buffer.Cursor;
        if (keys[i] == op)
        {
            Linewise(op, at.Row, at.Row + Math.Max(count, 1) - 1);
            return true;
        }

        var count2 = ReadCount(keys, ref i);
        if (i == keys.Length) return false;
        var n = Math.Min(Math.Max(count, 1) * Math.Max(count2, 1), MaxCount);
        if (keys[i] is 'i' or 'a')
        {
            if (i + 1 == keys.Length) return false;
            if (Text.TextObject(at, keys[i] == 'a', keys[i + 1]) is { } range) Charwise(op, range.Start, range.End);
            return true;
        }

        var (status, motion, arg) = ParseMotion(keys, i);
        if (status == Parse.Incomplete) return false;
        if (status == Parse.Invalid) return true;

        var text = Text;
        Target? target;
        // cw on a word changes to its end, like ce, but never past the word the cursor is on.
        if (op == 'c' && motion is 'w' or 'W' && text.Class(at, motion == 'W') is 1 or 2)
        {
            var end = text.EndOfCurrentWord(at, motion == 'W');
            for (var k = 1; k < n; k++) end = text.WordEnd(end, motion == 'W');
            target = new Target(end, false, true);
        }
        else
        {
            target = Move(motion, arg, n, count > 0 || count2 > 0);
        }

        if (target is { } found) OperatorOn(op, found, motion);
        return true;
    }

    private void OperatorOn(char op, Target target, char motion)
    {
        var from = buffer.Cursor;
        var to = target.Pos;
        var text = Text;
        if (target.Linewise || op is '>' or '<')
        {
            Linewise(op, Math.Min(from.Row, to.Row), Math.Max(from.Row, to.Row));
            return;
        }

        // dw on a line's last word stops at the line's end instead of joining the next line.
        if (motion is 'w' or 'W' && to.Row > from.Row && to.Col <= text.Indent(to.Row).Length)
            to = new VimPos(to.Row - 1, text.Length(to.Row - 1));
        var (start, end) = from <= to ? (from, to) : (to, from);
        if (target.Inclusive) end = end with { Col = Math.Min(end.Col + 1, text.Length(end.Row)) };
        Charwise(op, start, end);
    }

    /// <summary>An operator on [<paramref name="start"/>, <paramref name="end"/>).</summary>
    private void Charwise(char op, VimPos start, VimPos end)
    {
        var text = Text.Text(start, end);
        switch (op)
        {
            case 'y':
                SetRegister(text, linewise: false, yank: true);
                buffer.Cursor = Clamp(start);
                break;
            case 'd':
                SetRegister(text, linewise: false);
                if (start != end) buffer.Replace(start, end, string.Empty);
                buffer.Cursor = Clamp(start);
                break;
            case 'c':
                SetRegister(text, linewise: false);
                if (start != end) buffer.Replace(start, end, string.Empty);
                StartInsert(start);
                break;
            case '~':
                buffer.Replace(start, end, Toggle(text));
                buffer.Cursor = Clamp(start);
                break;
            case '>' or '<':
                Linewise(op, start.Row, end.Row);
                break;
        }
    }

    /// <summary>An operator on whole lines <paramref name="first"/> to <paramref name="last"/>, each change one edit.</summary>
    private void Linewise(char op, int first, int last)
    {
        var text = Text;
        last = Math.Min(last, text.LastRow);
        var rows = Enumerable.Range(first, last - first + 1).Select(text.Line).ToList();
        var lineStart = new VimPos(first, 0);
        var lineEnd = new VimPos(last, text.Length(last));
        switch (op)
        {
            case 'y':
                SetRegister(string.Join('\n', rows) + "\n", linewise: true, yank: true);
                if (buffer.Cursor.Row > first) buffer.Cursor = Clamp(buffer.Cursor with { Row = first });
                break;
            case 'd':
                SetRegister(string.Join('\n', rows) + "\n", linewise: true);
                if (last < text.LastRow) buffer.Replace(lineStart, new VimPos(last + 1, 0), string.Empty);
                else if (first > 0) buffer.Replace(new VimPos(first - 1, text.Length(first - 1)), lineEnd, string.Empty);
                else buffer.Replace(lineStart, lineEnd, string.Empty);
                var row = Math.Min(first, Text.LastRow);
                buffer.Cursor = new VimPos(row, Text.FirstNonBlank(row));
                break;
            case 'c':
                SetRegister(string.Join('\n', rows) + "\n", linewise: true);
                var indent = text.Indent(first);
                buffer.Replace(lineStart, lineEnd, indent);
                StartInsert(new VimPos(first, indent.Length));
                break;
            case '>' or '<' or '~':
                var changed = rows.Select(line => op switch
                {
                    '>' => line.Length == 0 ? line : Indentation + line,
                    '<' => Dedent(line),
                    _ => Toggle(line),
                });
                buffer.Replace(lineStart, lineEnd, string.Join('\n', changed));
                buffer.Cursor = new VimPos(first, op == '~' ? 0 : Text.FirstNonBlank(first));
                break;
        }
    }

    private void Paste(bool before, int count)
    {
        var (text, linewise) = _register;
        if (text.Length == 0) return;
        var repeated = string.Concat(Enumerable.Repeat(text, Math.Min(count, MaxCount)));
        var at = buffer.Cursor;
        var lines = Text;
        if (linewise)
        {
            int row;
            if (before)
            {
                buffer.Replace(at with { Col = 0 }, at with { Col = 0 }, repeated);
                row = at.Row;
            }
            else if (at.Row == lines.LastRow)
            {
                var end = at with { Col = lines.Length(at.Row) };
                buffer.Replace(end, end, "\n" + repeated[..^1]);
                row = at.Row + 1;
            }
            else
            {
                buffer.Replace(new VimPos(at.Row + 1, 0), new VimPos(at.Row + 1, 0), repeated);
                row = at.Row + 1;
            }

            buffer.Cursor = new VimPos(row, Text.FirstNonBlank(row));
            return;
        }

        var col = before || lines.Length(at.Row) == 0 ? at.Col : at.Col + 1;
        var insertAt = at with { Col = col };
        buffer.Replace(insertAt, insertAt, repeated);
        var after = VimMotions.After(insertAt, repeated);
        buffer.Cursor = Clamp(after with { Col = after.Col - 1 });
    }

    private void DeleteChars(int count, bool forward)
    {
        var at = buffer.Cursor;
        var length = Text.Length(at.Row);
        var (start, end) = forward ? (at.Col, Math.Min(at.Col + count, length)) : (Math.Max(at.Col - count, 0), at.Col);
        if (start < end) Charwise('d', at with { Col = start }, at with { Col = end });
    }

    private void ReplaceChars(char with, int count)
    {
        var at = buffer.Cursor;
        if (with is VimKey.Escape || at.Col + count > Text.Length(at.Row)) return;
        buffer.Replace(at, at with { Col = at.Col + count }, new string(with, count));
        buffer.Cursor = at with { Col = at.Col + count - 1 };
    }

    /// <summary>J: joins <paramref name="count"/> lines (at least two) with one space, dropping the next line's indent.</summary>
    private void Join(int count)
    {
        for (var k = 0; k < Math.Max(count, 2) - 1; k++)
        {
            var text = Text;
            var row = buffer.Cursor.Row;
            if (row >= text.LastRow) break;
            var (line, next) = (text.Line(row), text.Line(row + 1));
            var trimmed = next.TrimStart();
            var separator = line.Length == 0 || char.IsWhiteSpace(line[^1]) || trimmed.Length == 0 || trimmed[0] == ')' ? string.Empty : " ";
            buffer.Replace(new VimPos(row, line.Length), new VimPos(row + 1, next.Length - trimmed.Length), separator);
            buffer.Cursor = Clamp(new VimPos(row, separator.Length > 0 ? line.Length : line.Length - 1));
        }
    }

    private void ToggleCase(int count)
    {
        var at = buffer.Cursor;
        var length = Text.Length(at.Row);
        if (length == 0) return;
        var end = at with { Col = Math.Min(at.Col + count, length) };
        buffer.Replace(at, end, Toggle(Text.Text(at, end)));
        buffer.Cursor = Clamp(end);
    }

    private void OpenLine(bool below)
    {
        var at = buffer.Cursor;
        var text = Text;
        var indent = text.Indent(at.Row);
        if (below)
        {
            var end = at with { Col = text.Length(at.Row) };
            buffer.Replace(end, end, "\n" + indent);
            StartInsert(new VimPos(at.Row + 1, indent.Length));
        }
        else
        {
            var start = at with { Col = 0 };
            buffer.Replace(start, start, indent + "\n");
            StartInsert(new VimPos(at.Row, indent.Length));
        }
    }

    private void StartInsert(VimPos at)
    {
        buffer.Cursor = at;
        Mode = VimMode.Insert;
        _column = null;
    }

    private void StartVisual(VimMode mode)
    {
        _anchor = buffer.Cursor;
        Mode = mode;
        ShowSelection();
    }

    private void ExitVisual()
    {
        Mode = VimMode.Normal;
        buffer.ClearSelection();
    }

    private void ShowSelection() => buffer.Select(_anchor, buffer.Cursor, Mode == VimMode.VisualLine);

    /// <summary>A visual-mode command: v/V switch or leave, o swaps the ends, operators act on the selection, motions extend it.</summary>
    private bool VisualCommand(string keys)
    {
        var i = 0;
        var count = ReadCount(keys, ref i);
        if (i == keys.Length) return false;
        var key = keys[i];
        switch (key)
        {
            case 'v' or 'V':
                var mode = key == 'v' ? VimMode.Visual : VimMode.VisualLine;
                if (Mode == mode) ExitVisual();
                else
                {
                    Mode = mode;
                    ShowSelection();
                }

                break;
            case 'o':
                (_anchor, buffer.Cursor) = (buffer.Cursor, _anchor);
                ShowSelection();
                break;
            case ':': StartCommand(':'); break;
            case 'y' or 'd' or 'x' or 'c' or 's' or '>' or '<' or '~' or 'p' or 'P':
                OnSelection(key switch { 'x' => 'd', 's' => 'c', 'P' => 'p', _ => key });
                break;
            case 'i' or 'a':
                if (i + 1 == keys.Length) return false;
                if (Text.TextObject(buffer.Cursor, key == 'a', keys[i + 1]) is { } range)
                {
                    Mode = VimMode.Visual;
                    _anchor = range.Start;
                    buffer.Cursor = range.End > range.Start ? Text.Prev(range.End) ?? range.Start : range.Start;
                    ShowSelection();
                }

                break;
            case 'g' when i + 1 == keys.Length: return false;
            default:
                var (status, motion, arg) = ParseMotion(keys, i);
                if (status == Parse.Incomplete) return false;
                if (status == Parse.Done && Move(motion, arg, Math.Max(count, 1), count > 0) is { } target)
                {
                    MoveCursor(target, motion);
                    ShowSelection();
                }

                break;
        }

        return true;
    }

    private void OnSelection(char op)
    {
        var linewise = Mode == VimMode.VisualLine;
        var (start, last) = _anchor <= buffer.Cursor ? (_anchor, buffer.Cursor) : (buffer.Cursor, _anchor);
        ExitVisual();
        if (op == 'p') PasteOver(start, last, linewise);
        else if (linewise) Linewise(op, start.Row, last.Row);
        else Charwise(op, start, last with { Col = Math.Min(last.Col + 1, Text.Length(last.Row)) });
    }

    /// <summary>p in visual mode: the register replaces the selection, and the selection goes to the register.</summary>
    private void PasteOver(VimPos start, VimPos last, bool linewise)
    {
        var (text, registerLinewise) = _register;
        if (text.Length == 0) return;
        var lines = Text;
        if (linewise)
        {
            var end = new VimPos(last.Row, lines.Length(last.Row));
            var replaced = lines.Text(start with { Col = 0 }, end) + "\n";
            buffer.Replace(start with { Col = 0 }, end, registerLinewise ? text[..^1] : text);
            buffer.Cursor = new VimPos(start.Row, Text.FirstNonBlank(start.Row));
            _register = (replaced, true);
        }
        else
        {
            var end = last with { Col = Math.Min(last.Col + 1, lines.Length(last.Row)) };
            var replaced = lines.Text(start, end);
            var insert = registerLinewise ? text[..^1] : text;
            buffer.Replace(start, end, insert);
            var after = VimMotions.After(start, insert);
            buffer.Cursor = Clamp(after with { Col = after.Col - 1 });
            _register = (replaced, false);
        }
    }

    private void StartCommand(char prompt)
    {
        if (InVisual) ExitVisual();
        Mode = VimMode.Command;
        (_prompt, _typed) = (prompt, string.Empty);
    }

    private bool CommandKey(VimKey key)
    {
        if (key.Ctrl) return true;
        switch (key.Char)
        {
            case VimKey.Escape:
                Mode = VimMode.Normal;
                break;
            case VimKey.Backspace when _typed.Length == 0:
                Mode = VimMode.Normal;
                break;
            case VimKey.Backspace:
                _typed = _typed[..^1];
                break;
            case VimKey.Enter:
                Mode = VimMode.Normal;
                RunCommandLine(_prompt, _typed);
                break;
            default:
                _typed += key.Char;
                break;
        }

        return true;
    }

    private void RunCommandLine(char prompt, string typed)
    {
        if (prompt == ':')
        {
            var command = typed.Trim();
            if (command.Length == 0) return;
            if (command.All(char.IsAsciiDigit))
            {
                var row = Math.Clamp(int.TryParse(command, out var line) ? line - 1 : int.MaxValue, 0, Text.LastRow);
                buffer.Cursor = new VimPos(row, Text.FirstNonBlank(row));
                return;
            }

            CommandEntered?.Invoke(command);
            return;
        }

        var pattern = typed.Length > 0 ? typed : _lastSearch?.Pattern;
        if (pattern is null) return;
        _lastSearch = (pattern, prompt == '/', false);
        if (SearchTarget(forward: prompt == '/') is { } found) buffer.Cursor = Clamp(found);
    }

    private VimPos? SearchTarget(bool forward)
    {
        if (_lastSearch is not { } search) return null;
        var found = Text.Search(buffer.Cursor, search.Pattern, forward, search.WholeWord);
        if (found is null) Error?.Invoke($"E486: Pattern not found: {search.Pattern}");
        return found;
    }

    private void MoveHalfPage(int direction)
    {
        var row = Math.Clamp(buffer.Cursor.Row + direction * Math.Max(buffer.PageRows / 2, 1), 0, Text.LastRow);
        buffer.Cursor = new VimPos(row, Text.FirstNonBlank(row));
        if (InVisual) ShowSelection();
    }

    private readonly record struct Target(VimPos Pos, bool Linewise, bool Inclusive);

    private enum Parse
    {
        Incomplete,
        Invalid,
        Done,
    }

    private static (Parse Status, char Motion, char Arg) ParseMotion(string keys, int i)
    {
        if (i >= keys.Length) return (Parse.Incomplete, default, default);
        var key = keys[i];
        if (key is 'f' or 'F' or 't' or 'T') return i + 1 < keys.Length ? (Parse.Done, key, keys[i + 1]) : (Parse.Incomplete, default, default);
        if (key == 'g')
        {
            if (i + 1 == keys.Length) return (Parse.Incomplete, default, default);
            return keys[i + 1] == 'g' ? (Parse.Done, 'g', default) : (Parse.Invalid, default, default);
        }

        return MotionKeys.Contains(key) ? (Parse.Done, key, default) : (Parse.Invalid, default, default);
    }

    /// <summary>Where a motion lands from the cursor; null when it cannot move (no such character, no match).</summary>
    private Target? Move(char motion, char arg, int count, bool counted)
    {
        var text = Text;
        var at = buffer.Cursor;
        switch (motion)
        {
            case 'h': return new Target(at with { Col = Math.Max(at.Col - count, 0) }, false, false);
            case 'l': return new Target(at with { Col = Math.Min(at.Col + count, text.Length(at.Row)) }, false, false);
            case 'j' or 'k':
                var row = Math.Clamp(at.Row + (motion == 'j' ? count : -count), 0, text.LastRow);
                return new Target(new VimPos(row, at.Col), true, false);
            case '0': return new Target(at with { Col = 0 }, false, false);
            case '^': return new Target(at with { Col = text.FirstNonBlank(at.Row) }, false, false);
            case '$':
                var last = Math.Min(at.Row + count - 1, text.LastRow);
                return new Target(new VimPos(last, Math.Max(text.Length(last) - 1, 0)), false, true);
            case 'w' or 'W' or 'b' or 'B' or 'e' or 'E':
                var big = char.IsUpper(motion);
                var pos = at;
                for (var k = 0; k < count; k++)
                    pos = char.ToLowerInvariant(motion) switch { 'w' => text.WordForward(pos, big), 'b' => text.WordBackward(pos, big), _ => text.WordEnd(pos, big) };
                return new Target(pos, false, motion is 'e' or 'E');
            case 'G' or 'g':
                var line = counted ? Math.Clamp(count - 1, 0, text.LastRow) : motion == 'G' ? text.LastRow : 0;
                return new Target(new VimPos(line, text.FirstNonBlank(line)), true, false);
            case 'f' or 'F' or 't' or 'T':
                _lastFind = (motion, arg);
                return Find(motion, arg, count);
            case ';' or ',':
                if (_lastFind is not { } find) return null;
                var kind = motion == ';' ? find.Kind : find.Kind switch { 'f' => 'F', 'F' => 'f', 't' => 'T', _ => 't' };
                return Find(kind, find.Target, count);
            case '%':
                return text.MatchBracket(at) is { } match ? new Target(match, false, true) : null;
            case 'n' or 'N':
                if (_lastSearch is not { } search) return null;
                return SearchTarget(motion == 'n' ? search.Forward : !search.Forward) is { } next ? new Target(next, false, false) : null;
            case '*':
                if (text.WordAt(at) is not { } word) return null;
                _lastSearch = (word, true, true);
                return SearchTarget(forward: true) is { } hit ? new Target(hit, false, false) : null;
            default:
                return null;
        }

        Target? Find(char kind, char target, int times) =>
            text.Find(at, kind, target, times) is { } found ? new Target(found, false, kind is 'f' or 't') : null;
    }

    private void MoveCursor(Target target, char motion)
    {
        if (motion is 'j' or 'k')
        {
            var col = _column ?? buffer.Cursor.Col;
            buffer.Cursor = Clamp(target.Pos with { Col = col });
            _column = col;
            return;
        }

        buffer.Cursor = Clamp(target.Pos);
        _column = motion == '$' ? int.MaxValue : null;
    }

    /// <summary>Normal mode keeps the cursor on a character: never past the last one of its line.</summary>
    private VimPos Clamp(VimPos pos)
    {
        var lines = buffer.Lines;
        var row = Math.Clamp(pos.Row, 0, lines.Count - 1);
        return new VimPos(row, Math.Clamp(pos.Col, 0, Math.Max(lines[row].Length - 1, 0)));
    }

    private void SetRegister(string text, bool linewise, bool yank = false)
    {
        _register = (text, linewise);
        if (yank) Yanked?.Invoke(text);
    }

    private static int ReadCount(string keys, ref int i)
    {
        var start = i;
        while (i < keys.Length && char.IsAsciiDigit(keys[i]) && (i > start || keys[i] != '0')) i++;
        if (i == start) return 0;
        return int.TryParse(keys.AsSpan(start, i - start), out var count) ? Math.Min(count, MaxCount) : MaxCount;
    }

    private static string Toggle(string text) =>
        string.Concat(text.Select(ch => char.IsUpper(ch) ? char.ToLowerInvariant(ch) : char.ToUpperInvariant(ch)));

    private static string Dedent(string line)
    {
        if (line.StartsWith('\t')) return line[1..];
        var spaces = 0;
        while (spaces < Indentation.Length && spaces < line.Length && line[spaces] == ' ') spaces++;
        return line[spaces..];
    }
}
