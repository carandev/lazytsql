using LazyTsql.Tui.Vim;

namespace LazyTsql.Tests;

public class VimEngineTests
{
    /// <summary>A buffer over plain lines with snapshot undo, standing in for the editor.</summary>
    private sealed class FakeBuffer(string text) : IVimBuffer
    {
        private readonly Stack<(string, VimPos)> _undo = new(), _redo = new();
        public string Text { get; private set; } = text;
        public IReadOnlyList<string> Lines => Text.Split('\n');
        public VimPos Cursor { get; set; }
        public int PageRows => 10;
        public (VimPos Anchor, VimPos Cursor, bool Linewise)? Selection { get; private set; }

        public void Replace(VimPos start, VimPos end, string replacement)
        {
            _undo.Push((Text, Cursor));
            _redo.Clear();
            Text = Text[..Offset(start)] + replacement + Text[Offset(end)..];
        }

        public void Undo() => Swap(_undo, _redo);
        public void Redo() => Swap(_redo, _undo);
        public void Select(VimPos anchor, VimPos cursor, bool linewise) => Selection = (anchor, cursor, linewise);
        public void ClearSelection() => Selection = null;

        private void Swap(Stack<(string, VimPos)> from, Stack<(string, VimPos)> to)
        {
            if (from.Count == 0) return;
            to.Push((Text, Cursor));
            (Text, Cursor) = from.Pop();
        }

        public int Offset(VimPos pos) => Lines.Take(pos.Row).Sum(line => line.Length + 1) + pos.Col;

        /// <summary>The text with <c>|</c> where the cursor is.</summary>
        public string Shown => Text.Insert(Offset(Cursor), "|");
    }

    private static (FakeBuffer Buffer, VimEngine Vim, List<string> Commands) Start(string marked)
    {
        var at = marked.IndexOf('|', StringComparison.Ordinal);
        var buffer = new FakeBuffer(marked.Remove(at, 1));
        var before = buffer.Text[..at].Split('\n');
        buffer.Cursor = new VimPos(before.Length - 1, before[^1].Length);
        var vim = new VimEngine(buffer);
        var commands = new List<string>();
        vim.CommandEntered += commands.Add;
        return (buffer, vim, commands);
    }

    /// <summary>Types keys: plain characters, or &lt;Esc&gt;, &lt;CR&gt;, &lt;BS&gt; and &lt;C-x&gt;; insert mode types into the buffer.</summary>
    private static void Type(FakeBuffer buffer, VimEngine vim, string keys)
    {
        for (var i = 0; i < keys.Length; i++)
        {
            VimKey key;
            if (keys[i] == '<' && keys.IndexOf('>', i) is var close and > 0 && close - i > 2)
            {
                var name = keys[(i + 1)..close];
                key = name switch
                {
                    "Esc" => new VimKey(VimKey.Escape),
                    "CR" => new VimKey(VimKey.Enter),
                    "BS" => new VimKey(VimKey.Backspace),
                    _ when name.StartsWith("C-", StringComparison.Ordinal) => new VimKey(name[2], Ctrl: true),
                    _ => throw new ArgumentException(name),
                };
                i = close;
            }
            else
            {
                key = new VimKey(keys[i]);
            }

            if (vim.HandleKey(key) || key.Ctrl || vim.Mode != VimMode.Insert) continue;
            buffer.Replace(buffer.Cursor, buffer.Cursor, key.Char.ToString());
            buffer.Cursor = buffer.Cursor with { Col = buffer.Cursor.Col + 1 };
        }
    }

    private static string Run(string marked, string keys)
    {
        var (buffer, vim, _) = Start(marked);
        Type(buffer, vim, keys);
        return buffer.Shown;
    }

    [Theory]
    [InlineData("ab|c\nde", "l", "ab|c\nde")]
    [InlineData("ab|c\nd\nefg", "jj", "abc\nd\nef|g")]
    [InlineData("|a\nb\nc\nd", "3j", "a\nb\nc\n|d")]
    [InlineData("a|bc", "h", "|abc")]
    [InlineData("  a|b", "^", "  |ab")]
    [InlineData("  a|b", "0", "|  ab")]
    [InlineData("|abc", "$", "ab|c")]
    [InlineData("a\nb\n|c", "gg", "|a\nb\nc")]
    [InlineData("|a\nb\n  c", "G", "a\nb\n  |c")]
    [InlineData("|a\nb\nc", "2G", "a\n|b\nc")]
    public void Moves_by_characters_lines_and_line_ends(string text, string keys, string expected) =>
        Assert.Equal(expected, Run(text, keys));

    [Theory]
    [InlineData("|ab cd, ef", "w", "ab |cd, ef")]
    [InlineData("|ab cd, ef", "ww", "ab cd|, ef")]
    [InlineData("|ab cd, ef", "3w", "ab cd, |ef")]
    [InlineData("|ab cd", "e", "a|b cd")]
    [InlineData("ab cd|, ef", "b", "ab |cd, ef")]
    [InlineData("|a.b c", "W", "a.b |c")]
    [InlineData("|a.b c", "E", "a.|b c")]
    [InlineData("a.b |c", "B", "|a.b c")]
    [InlineData("a|b\ncd", "w", "ab\n|cd")]
    [InlineData("|a\n\nb", "w", "a\n|\nb")]
    [InlineData("|a\n\nb", "ww", "a\n\n|b")]
    public void Moves_by_words(string text, string keys, string expected) => Assert.Equal(expected, Run(text, keys));

    [Theory]
    [InlineData("|a,b,c", "f,", "a|,b,c")]
    [InlineData("|a,b,c", "f,;", "a,b|,c")]
    [InlineData("|a,b,c", "f,;,", "a|,b,c")]
    [InlineData("|ab,c", "t,", "a|b,c")]
    [InlineData("a,b|c", "F,", "a|,bc")]
    [InlineData("a,b|c", "T,", "a,|bc")]
    [InlineData("|(a(b))", "%", "(a(b)|)")]
    [InlineData("x |y (a)", "%", "x y (a|)")]
    public void Finds_characters_on_the_line_and_matching_brackets(string text, string keys, string expected) =>
        Assert.Equal(expected, Run(text, keys));

    [Theory]
    [InlineData("|abc", "x", "|bc")]
    [InlineData("|abcd", "3x", "|d")]
    [InlineData("ab|c", "X", "a|c")]
    [InlineData("|abc", "xp", "b|ac")]
    [InlineData("|ab", "yl3p", "aaa|ab")]
    [InlineData("|abc", "rx", "|xbc")]
    [InlineData("|abc", "2rx", "x|xc")]
    [InlineData("|a\n  b", "J", "a| b")]
    [InlineData("|ab", "~", "A|b")]
    [InlineData("|a", ">>", "    |a")]
    [InlineData("    |a", "<<", "|a")]
    [InlineData("|abc", "xu", "|abc")]
    [InlineData("|abc", "xu<C-r>", "|bc")]
    public void Edits_characters_and_lines_in_place(string text, string keys, string expected) =>
        Assert.Equal(expected, Run(text, keys));

    [Theory]
    [InlineData("a\n|b\nc", "dd", "a\n|c")]
    [InlineData("|a\nb\nc", "2dd", "|c")]
    [InlineData("a\n|b", "dd", "|a")]
    [InlineData("|a", "dd", "|")]
    [InlineData("|a\nb\nc", "dj", "|c")]
    [InlineData("|a\nb", "yyp", "a\n|a\nb")]
    [InlineData("a\n|b", "yyP", "a\n|b\nb")]
    [InlineData("|a\nb", "Yjp", "a\nb\n|a")]
    [InlineData("SELECT |a, b FROM t", "dw", "SELECT |, b FROM t")]
    [InlineData("SELECT a|, b", "dw", "SELECT a|b")]
    [InlineData("a |bc\nd", "dw", "a| \nd")]
    [InlineData("|a b c d", "d2w", "|c d")]
    [InlineData("ab|cd", "d$", "a|b")]
    [InlineData("ab|cd", "D", "a|b")]
    [InlineData("|a,b", "df,", "|b")]
    [InlineData("SELECT |abc def", "cwx<Esc>", "SELECT |x def")]
    [InlineData("ab|cd", "Cx<Esc>", "ab|x")]
    [InlineData("  ab|c\nd", "ccx<Esc>", "  |x\nd")]
    [InlineData("|ab", "sx<Esc>", "|xb")]
    [InlineData("  a|b", "Sx<Esc>", "  |x")]
    public void Operators_act_on_motions_and_whole_lines(string text, string keys, string expected) =>
        Assert.Equal(expected, Run(text, keys));

    [Theory]
    [InlineData("SELECT ab|c FROM", "diw", "SELECT | FROM")]
    [InlineData("SELECT ab|c FROM", "daw", "SELECT |FROM")]
    [InlineData("x = 'ab|c' y", "ci'z<Esc>", "x = '|z' y")]
    [InlineData("x = 'ab|c' y", "da'", "x = | y")]
    [InlineData("f(a,\n  |b)", "ci(x<Esc>", "f(|x)")]
    [InlineData("f|(a)", "di(", "f(|)")]
    [InlineData("f(|a)", "dab", "|f")]
    public void Operators_act_on_text_objects(string text, string keys, string expected) =>
        Assert.Equal(expected, Run(text, keys));

    [Theory]
    [InlineData("a|b", "ix<Esc>", "a|xb")]
    [InlineData("a|b", "ax<Esc>", "ab|x")]
    [InlineData("  a|b", "Ix<Esc>", "  |xab")]
    [InlineData("|ab", "Ax<Esc>", "ab|x")]
    [InlineData("  |a\nb", "ox<Esc>", "  a\n  |x\nb")]
    [InlineData("  |a", "Ox<Esc>", "  |x\n  a")]
    public void Insert_mode_starts_where_vim_starts_it_and_esc_steps_back(string text, string keys, string expected) =>
        Assert.Equal(expected, Run(text, keys));

    [Theory]
    [InlineData("|abc def", "vwd", "|ef")]
    [InlineData("ab|c d", "viwd", "| d")]
    [InlineData("|abc", "vlohd", "|c")]
    [InlineData("|abc d", "vecx<Esc>", "|x d")]
    [InlineData("|ab", "vl~", "|AB")]
    [InlineData("|ab cd", "yiwwviwp", "ab a|b")]
    [InlineData("|a\nb\nc", "Vjd", "|c")]
    [InlineData("|a\nb\nc", "VjyGp", "a\nb\nc\n|a\nb")]
    [InlineData("|a\nb", "Vj>", "    |a\n    b")]
    public void Visual_modes_select_with_motions_and_operators_act_on_the_selection(string text, string keys, string expected) =>
        Assert.Equal(expected, Run(text, keys));

    [Fact]
    public void Visual_mode_shows_its_selection_and_esc_leaves_it()
    {
        var (buffer, vim, _) = Start("|abc\nd");

        Type(buffer, vim, "vl");
        Assert.Equal(VimMode.Visual, vim.Mode);
        Assert.Equal((new VimPos(0, 0), new VimPos(0, 1), false), buffer.Selection);

        Type(buffer, vim, "V");
        Assert.Equal(VimMode.VisualLine, vim.Mode);
        Assert.True(buffer.Selection!.Value.Linewise);

        Type(buffer, vim, "<Esc>");
        Assert.Equal(VimMode.Normal, vim.Mode);
        Assert.Null(buffer.Selection);
        Assert.Equal("a|bc\nd", buffer.Shown);
    }

    [Fact]
    public void The_command_line_collects_a_command_and_runs_it_on_enter()
    {
        var (buffer, vim, commands) = Start("|a\nb\n  c\nd");

        Type(buffer, vim, ":w");
        Assert.Equal(VimMode.Command, vim.Mode);
        Assert.Equal(":w", vim.CommandLine);
        Type(buffer, vim, "q<BS><CR>");
        Assert.Equal(["w"], commands);
        Assert.Equal(VimMode.Normal, vim.Mode);
        Assert.Equal(string.Empty, vim.CommandLine);

        Type(buffer, vim, ":3<CR>");
        Assert.Equal("a\nb\n  |c\nd", buffer.Shown);

        Type(buffer, vim, ":x<Esc>:<BS>");
        Assert.Equal(["w"], commands);
        Assert.Equal(VimMode.Normal, vim.Mode);
    }

    [Fact]
    public void Tab_and_quit_keys_become_commands()
    {
        var (buffer, vim, commands) = Start("|a");

        Type(buffer, vim, "gtgTZZZQ");

        Assert.Equal(["tabnext", "tabprevious", "x", "q!"], commands);
    }

    [Fact]
    public void Search_finds_text_ignoring_case_repeats_with_n_and_wraps()
    {
        var (buffer, vim, _) = Start("|select a from T FROM");

        Type(buffer, vim, "/from<CR>");
        Assert.Equal(new VimPos(0, 9), buffer.Cursor);
        Type(buffer, vim, "n");
        Assert.Equal(new VimPos(0, 16), buffer.Cursor);
        Type(buffer, vim, "n");
        Assert.Equal(new VimPos(0, 9), buffer.Cursor);
        Type(buffer, vim, "N");
        Assert.Equal(new VimPos(0, 16), buffer.Cursor);
        Type(buffer, vim, "0?a<CR>");
        Assert.Equal(new VimPos(0, 7), buffer.Cursor);
    }

    [Fact]
    public void Star_searches_the_whole_word_under_the_cursor()
    {
        Assert.Equal("a ab a abc |a", Run("|a ab a abc a", "**"));
    }

    [Fact]
    public void A_search_that_finds_nothing_reports_it()
    {
        var (buffer, vim, _) = Start("|abc");
        var errors = new List<string>();
        vim.Error += errors.Add;

        Type(buffer, vim, "/zz<CR>");

        Assert.Equal(["E486: Pattern not found: zz"], errors);
        Assert.Equal(new VimPos(0, 0), buffer.Cursor);
    }

    [Fact]
    public void Normal_mode_swallows_unknown_letters_and_passes_other_ctrl_keys_on()
    {
        var (buffer, vim, _) = Start("|abc");

        Assert.True(vim.HandleKey(new VimKey('Q')));
        Assert.False(vim.HandleKey(new VimKey('c', Ctrl: true)));
        Assert.Equal("|abc", buffer.Shown);

        Type(buffer, vim, "i");
        Assert.False(vim.HandleKey(new VimKey('z')));
    }

    [Fact]
    public void Ctrl_d_and_ctrl_u_move_half_a_page()
    {
        var (buffer, vim, _) = Start("|" + string.Join('\n', Enumerable.Range(0, 30)));

        Type(buffer, vim, "<C-d><C-d>");
        Assert.Equal(10, buffer.Cursor.Row);
        Type(buffer, vim, "<C-u>");
        Assert.Equal(5, buffer.Cursor.Row);
    }

    [Fact]
    public void Pending_keys_show_until_the_command_completes()
    {
        var (buffer, vim, _) = Start("|a b");

        Type(buffer, vim, "2d");
        Assert.Equal("2d", vim.PendingKeys);
        Type(buffer, vim, "<Esc>");
        Assert.Equal(string.Empty, vim.PendingKeys);
        Assert.Equal("|a b", buffer.Shown);
    }

    [Fact]
    public void Yanking_reports_the_text_for_the_clipboard()
    {
        var (buffer, vim, _) = Start("|a\nb");
        var yanked = new List<string>();
        vim.Yanked += yanked.Add;

        Type(buffer, vim, "yyyiw");

        Assert.Equal(["a\n", "a"], yanked);
    }
}
