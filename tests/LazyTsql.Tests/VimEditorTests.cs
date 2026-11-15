using System.Drawing;
using LazyTsql.Tui;
using LazyTsql.Tui.Vim;
using Terminal.Gui.Input;

#pragma warning disable CS0618

namespace LazyTsql.Tests;

public class VimEditorTests
{
    private static SqlEditor Editor(string text, Point cursor, bool vim = true)
    {
        var editor = new SqlEditor { Text = text, Width = 40, Height = 6 };
        editor.BeginInit();
        editor.EndInit();
        editor.SetFocus();
        editor.InsertionPoint = cursor;
        if (vim) editor.SetVimEnabled(true);
        return editor;
    }

    private static void Press(SqlEditor editor, params Key[] keys)
    {
        foreach (var key in keys) editor.NewKeyDownEvent(key);
    }

    private static void Type(SqlEditor editor, string text)
    {
        foreach (var ch in text) editor.NewKeyDownEvent(new Key(ch));
    }

    [Fact]
    public void Normal_mode_edits_the_text_and_u_and_ctrl_r_use_the_editor_history()
    {
        var editor = Editor("SELECT a, b", new Point(7, 0));
        var changes = 0;
        editor.ContentsChanged += (_, _) => changes++;

        Type(editor, "dw");
        Assert.Equal("SELECT , b", editor.Text);
        Assert.True(changes > 0);

        Type(editor, "u");
        Assert.Equal("SELECT a, b", editor.Text);
        Press(editor, Key.R.WithCtrl);
        Assert.Equal("SELECT , b", editor.Text);
    }

    [Fact]
    public void Letters_in_normal_mode_are_commands_and_insert_mode_types_them()
    {
        var editor = Editor("ab", new Point(0, 0));

        Type(editor, "z");
        Assert.Equal("ab", editor.Text);

        Type(editor, "ixy");
        Assert.Equal(VimMode.Insert, editor.Vim.Mode);
        Press(editor, Key.Esc);
        Assert.Equal("xyab", editor.Text);
        Assert.Equal(VimMode.Normal, editor.Vim.Mode);
        Assert.Equal(new Point(1, 0), editor.InsertionPoint);
    }

    [Fact]
    public void Whole_lines_yank_paste_and_delete_across_rows()
    {
        var editor = Editor("a\nb\nc", new Point(0, 0));

        Type(editor, "yyjp");
        Assert.Equal("a\nb\na\nc", editor.Text);
        Assert.Equal(new Point(0, 2), editor.InsertionPoint);

        Type(editor, "2dd");
        Assert.Equal("a\nb", editor.Text);
    }

    [Fact]
    public void Lines_open_above_and_below_and_replaced_text_keeps_the_rest_of_the_line()
    {
        var editor = Editor("  ab\ncd", new Point(2, 0));

        Type(editor, "Ox");
        Press(editor, Key.Esc);
        Assert.Equal("  x\n  ab\ncd", editor.Text);

        Type(editor, "jox");
        Press(editor, Key.Esc);
        Assert.Equal("  x\n  ab\n  x\ncd", editor.Text);

        Type(editor, "G~>>");
        Assert.Equal("  x\n  ab\n  x\n    Cd", editor.Text);

        Type(editor, "kyyjP");
        Assert.Equal("  x\n  ab\n  x\n  x\n    Cd", editor.Text);

        Type(editor, "u");
        Assert.Equal("  x\n  ab\n  x\n    Cd", editor.Text);
    }

    [Fact]
    public void Shift_letters_arrows_and_ctrl_keys_reach_vim()
    {
        var editor = Editor("ab\ncd", new Point(0, 0));

        Press(editor, Key.CursorDown, Key.A.WithShift);
        Assert.Equal(VimMode.Insert, editor.Vim.Mode);
        Assert.Equal(new Point(2, 1), editor.InsertionPoint);
    }

    [Fact]
    public void A_stay_in_insert_mode_undoes_in_one_step()
    {
        var editor = Editor("ab", new Point(0, 0));

        Type(editor, "cwxyz");
        Press(editor, Key.Esc);
        Assert.Equal("xyz", editor.Text);

        Type(editor, "u");
        Assert.Equal("ab", editor.Text);
    }

    [Fact]
    public void Completion_stays_closed_outside_insert_mode()
    {
        var editor = Editor("a", new Point(0, 0));
        Assert.True(editor.CompletionBlocked);

        Type(editor, "i");
        Assert.False(editor.CompletionBlocked);
    }

    [Fact]
    public void Without_vim_mode_keys_type_as_before()
    {
        var editor = Editor("ab", new Point(0, 0), vim: false);

        Type(editor, "dw");

        Assert.Equal("dwab", editor.Text);
        Assert.False(editor.CompletionBlocked);
    }

    [Fact]
    public void Turning_vim_off_leaves_visual_mode_and_its_selection()
    {
        var editor = Editor("abc", new Point(0, 0));
        Type(editor, "vl");
        Assert.Equal(VimMode.Visual, editor.Vim.Mode);

        editor.SetVimEnabled(false);

        Assert.Equal(VimMode.Normal, editor.Vim.Mode);
        Type(editor, "x");
        Assert.Equal("axbc", editor.Text);
    }
}
