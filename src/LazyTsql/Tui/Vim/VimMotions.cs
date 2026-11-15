namespace LazyTsql.Tui.Vim;

/// <summary>
/// Where vim's motions, text objects and searches land, read from the buffer's lines. Positions walk the text as one
/// sequence in which each line ends in its line break.
/// </summary>
internal sealed class VimMotions(IReadOnlyList<string> lines)
{
    private const string Brackets = "()[]{}";

    public int LastRow => lines.Count - 1;

    public int Length(int row) => lines[row].Length;

    public string Line(int row) => lines[row];

    public int FirstNonBlank(int row)
    {
        var line = lines[row];
        var col = 0;
        while (col < line.Length && char.IsWhiteSpace(line[col])) col++;
        return col == line.Length ? Math.Max(line.Length - 1, 0) : col;
    }

    public string Indent(int row) => lines[row][..(lines[row].Length - lines[row].TrimStart().Length)];

    public VimPos End => new(LastRow, Length(LastRow));

    public char CharAt(VimPos pos) => pos.Col < lines[pos.Row].Length ? lines[pos.Row][pos.Col] : '\n';

    public VimPos? Next(VimPos pos) =>
        pos.Col < lines[pos.Row].Length ? pos with { Col = pos.Col + 1 }
        : pos.Row < LastRow ? new VimPos(pos.Row + 1, 0)
        : null;

    public VimPos? Prev(VimPos pos) =>
        pos.Col > 0 ? pos with { Col = pos.Col - 1 }
        : pos.Row > 0 ? new VimPos(pos.Row - 1, lines[pos.Row - 1].Length)
        : null;

    /// <summary>0 blank, 1 word, 2 punctuation, 3 an empty line (a word of its own); big words are any non-blank run.</summary>
    public int Class(VimPos pos, bool big)
    {
        if (lines[pos.Row].Length == 0) return 3;
        var ch = CharAt(pos);
        if (char.IsWhiteSpace(ch)) return 0;
        return big || IsWordChar(ch) ? 1 : 2;
    }

    public static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_';

    /// <summary>w: the start of the next word, stopping at empty lines; the end of the text when there is none.</summary>
    public VimPos WordForward(VimPos pos, bool big)
    {
        var start = Class(pos, big);
        VimPos? at = pos;
        if (start != 0)
        {
            do at = Next(at.Value);
            while (at is { } p && start != 3 && Class(p, big) == start);
        }

        while (at is { } p && Class(p, big) == 0) at = Next(p);
        return at ?? End;
    }

    /// <summary>e: the end of this word, or of the next one when already at an end.</summary>
    public VimPos WordEnd(VimPos pos, bool big)
    {
        var at = Next(pos);
        while (at is { } p && Class(p, big) is 0 or 3) at = Next(p);
        if (at is not { } found) return pos;
        var cls = Class(found, big);
        while (Next(found) is { } next && Class(next, big) == cls) found = next;
        return found;
    }

    /// <summary>The last character of the word under <paramref name="pos"/>, which cw changes up to.</summary>
    public VimPos EndOfCurrentWord(VimPos pos, bool big)
    {
        var cls = Class(pos, big);
        while (Next(pos) is { } next && next.Row == pos.Row && Class(next, big) == cls) pos = next;
        return pos;
    }

    /// <summary>b: the start of this word, or of the one before when already at a start.</summary>
    public VimPos WordBackward(VimPos pos, bool big)
    {
        var at = Prev(pos);
        while (at is { } p && Class(p, big) == 0) at = Prev(p);
        if (at is not { } found) return new VimPos(0, 0);
        var cls = Class(found, big);
        if (cls == 3) return found;
        while (Prev(found) is { } prev && Class(prev, big) == cls) found = prev;
        return found;
    }

    /// <summary>f, t, F and T: the <paramref name="count"/>th <paramref name="target"/> on the line, or null.</summary>
    public VimPos? Find(VimPos pos, char kind, char target, int count)
    {
        var line = lines[pos.Row];
        var forward = kind is 'f' or 't';
        var col = pos.Col;
        // Repeating t or T from right before the character would not move; it goes on to the next one, as vim does.
        if (kind == 't' && col + 1 < line.Length && line[col + 1] == target) col++;
        if (kind == 'T' && col - 1 >= 0 && line[col - 1] == target) col--;
        for (var found = 0; found < count;)
        {
            col += forward ? 1 : -1;
            if (col < 0 || col >= line.Length) return null;
            if (line[col] == target) found++;
        }

        return pos with { Col = kind switch { 't' => col - 1, 'T' => col + 1, _ => col } };
    }

    /// <summary>%: the bracket matching the one under the cursor, or the next bracket on the line.</summary>
    public VimPos? MatchBracket(VimPos pos)
    {
        var line = lines[pos.Row];
        var col = pos.Col;
        while (col < line.Length && !Brackets.Contains(line[col])) col++;
        if (col == line.Length) return null;
        var open = Brackets.IndexOf(line[col]) % 2 == 0;
        var bracket = line[col];
        var partner = Brackets[Brackets.IndexOf(bracket) + (open ? 1 : -1)];
        return Walk(pos with { Col = col }, open, bracket, partner);
    }

    /// <summary>The bracket closing (or opening) the one at <paramref name="from"/>, counting nested pairs.</summary>
    private VimPos? Walk(VimPos from, bool forward, char same, char partner)
    {
        var depth = 0;
        for (VimPos? at = from; at is { } p; at = forward ? Next(p) : Prev(p))
        {
            var ch = CharAt(p);
            if (ch == same) depth++;
            else if (ch == partner && --depth == 0) return p;
        }

        return null;
    }

    /// <summary>
    /// iw/aw, iW/aW, i"/a" (and ', `), i(/a( (and ), b, [, ], {, }, B): the range as [start, end). Null when the cursor is
    /// not inside one.
    /// </summary>
    public (VimPos Start, VimPos End)? TextObject(VimPos pos, bool around, char kind) => kind switch
    {
        'w' or 'W' => Word(pos, around, kind == 'W'),
        '"' or '\'' or '`' => Quoted(pos, around, kind),
        '(' or ')' or 'b' => Enclosed(pos, around, '(', ')'),
        '[' or ']' => Enclosed(pos, around, '[', ']'),
        '{' or '}' or 'B' => Enclosed(pos, around, '{', '}'),
        _ => null,
    };

    private (VimPos, VimPos)? Word(VimPos pos, bool around, bool big)
    {
        var line = lines[pos.Row];
        if (line.Length == 0) return null;
        var col = Math.Min(pos.Col, line.Length - 1);
        var cls = Class(pos with { Col = col }, big);
        int start = col, end = col + 1;
        while (start > 0 && Class(pos with { Col = start - 1 }, big) == cls) start--;
        while (end < line.Length && Class(pos with { Col = end }, big) == cls) end++;
        if (around)
        {
            var trailing = end;
            while (trailing < line.Length && char.IsWhiteSpace(line[trailing])) trailing++;
            if (trailing > end) end = trailing;
            else while (start > 0 && char.IsWhiteSpace(line[start - 1])) start--;
        }

        return (pos with { Col = start }, pos with { Col = end });
    }

    /// <summary>Quotes pair up from the start of the line; the pair around the cursor, or else the next one.</summary>
    private (VimPos, VimPos)? Quoted(VimPos pos, bool around, char quote)
    {
        var line = lines[pos.Row];
        var quotes = Enumerable.Range(0, line.Length).Where(i => line[i] == quote).ToList();
        for (var i = 0; i + 1 < quotes.Count; i += 2)
        {
            var (open, close) = (quotes[i], quotes[i + 1]);
            if (close < pos.Col) continue;
            return around ? (pos with { Col = open }, pos with { Col = close + 1 }) : (pos with { Col = open + 1 }, pos with { Col = close });
        }

        return null;
    }

    private (VimPos, VimPos)? Enclosed(VimPos pos, bool around, char open, char close)
    {
        var start = CharAt(pos) == open ? pos
            : CharAt(pos) == close ? Walk(pos, forward: false, close, open)
            : EnclosingOpen(pos, open, close);
        if (start is not { } from || Walk(from, forward: true, open, close) is not { } to) return null;
        return around ? (from, Next(to) ?? End) : (Next(from)!.Value, to);
    }

    /// <summary>The nearest <paramref name="open"/> before <paramref name="pos"/> that is not closed before it.</summary>
    private VimPos? EnclosingOpen(VimPos pos, char open, char close)
    {
        var depth = 0;
        for (var at = Prev(pos); at is { } p; at = Prev(p))
        {
            if (CharAt(p) == close) depth++;
            else if (CharAt(p) == open && depth-- == 0) return p;
        }

        return null;
    }

    /// <summary>
    /// The next match of <paramref name="pattern"/> after <paramref name="pos"/> (before it going backward), ignoring
    /// case and wrapping around the end; a whole word only when <paramref name="wholeWord"/>.
    /// </summary>
    public VimPos? Search(VimPos pos, string pattern, bool forward, bool wholeWord)
    {
        if (pattern.Length == 0) return null;
        // Step 0 is the rest of the cursor's line; the last step comes back to the part of it before the cursor.
        for (var step = 0; step <= lines.Count; step++)
        {
            var row = ((pos.Row + (forward ? step : -step)) % lines.Count + lines.Count) % lines.Count;
            var matches = Matches(lines[row], pattern, wholeWord).Where(col => step switch
            {
                0 => forward ? col > pos.Col : col < pos.Col,
                _ when step == lines.Count => forward ? col <= pos.Col : col >= pos.Col,
                _ => true,
            }).ToList();
            if (matches.Count > 0) return new VimPos(row, forward ? matches[0] : matches[^1]);
        }

        return null;
    }

    private static List<int> Matches(string line, string pattern, bool wholeWord)
    {
        var found = new List<int>();
        for (var at = line.IndexOf(pattern, StringComparison.OrdinalIgnoreCase); at >= 0; at = line.IndexOf(pattern, at + 1, StringComparison.OrdinalIgnoreCase))
        {
            var end = at + pattern.Length;
            if (wholeWord && ((at > 0 && IsWordChar(line[at - 1])) || (end < line.Length && IsWordChar(line[end])))) continue;
            found.Add(at);
        }

        return found;
    }

    /// <summary>The word under (or after) the cursor on its line, for *.</summary>
    public string? WordAt(VimPos pos)
    {
        var line = lines[pos.Row];
        var col = pos.Col;
        while (col < line.Length && !IsWordChar(line[col])) col++;
        if (col == line.Length) return null;
        var start = col;
        while (start > 0 && IsWordChar(line[start - 1])) start--;
        var end = col;
        while (end < line.Length && IsWordChar(line[end])) end++;
        return line[start..end];
    }

    /// <summary>The text from <paramref name="start"/> up to, not including, <paramref name="end"/>, lines joined by \n.</summary>
    public string Text(VimPos start, VimPos end)
    {
        if (start.Row == end.Row) return lines[start.Row][start.Col..Math.Min(end.Col, lines[start.Row].Length)];
        var parts = new List<string> { lines[start.Row][start.Col..] };
        for (var row = start.Row + 1; row < end.Row; row++) parts.Add(lines[row]);
        parts.Add(lines[end.Row][..Math.Min(end.Col, lines[end.Row].Length)]);
        return string.Join('\n', parts);
    }

    /// <summary>Where the cursor is after <paramref name="text"/> is inserted at <paramref name="at"/>.</summary>
    public static VimPos After(VimPos at, string text)
    {
        var breaks = text.Count(ch => ch == '\n');
        return breaks == 0 ? at with { Col = at.Col + text.Length } : new VimPos(at.Row + breaks, text.Length - text.LastIndexOf('\n') - 1);
    }
}
