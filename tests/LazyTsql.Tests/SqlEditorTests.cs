using System.Drawing;
using LazyTsql.Core.Completion;
using LazyTsql.Core.Schema;
using LazyTsql.Tui;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618

namespace LazyTsql.Tests;

public class SqlEditorTests
{
    private static SqlEditor Editor(string text, Point cursor, bool focused = true)
    {
        var editor = new SqlEditor { Text = text, Width = 40, Height = 5 };
        editor.BeginInit();
        editor.EndInit();
        if (focused) editor.SetFocus();
        editor.InsertionPoint = cursor;
        return editor;
    }

    public static TheoryData<string, Key, string> SilentEdits => new()
    {
        { "Ctrl+Delete", Key.Delete.WithCtrl, "SELECT 1\nFROM tx\nWHERE x = 1" },
        { "Ctrl+Backspace", Key.Backspace.WithCtrl, "SELECT 1\nFROM  x\nWHERE x = 1" },
        { "Ctrl+Shift+Backspace", Key.Backspace.WithCtrl.WithShift, "SELECT 1\n x\nWHERE x = 1" },
        { "Ctrl+K", Key.K.WithCtrl, "SELECT 1\nFROM t\nWHERE x = 1" },
    };

    [Theory]
    [MemberData(nameof(SilentEdits))]
    public void Word_and_line_cuts_report_the_change(string name, Key key, string expected)
    {
        var editor = Editor("SELECT 1\nFROM t x\nWHERE x = 1", new Point(6, 1));
        var changes = 0;
        editor.ContentsChanged += (_, _) => changes++;

        Assert.True(editor.NewKeyDownEvent(key), name);

        Assert.Equal(expected, editor.Text.ReplaceLineEndings("\n"));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void Typing_deleting_and_moving_keep_working()
    {
        var editor = Editor("SELECT 1\nFROM t x", new Point(6, 1));
        var changes = 0;
        editor.ContentsChanged += (_, _) => changes++;

        foreach (var key in new[] { Key.A, Key.Delete, Key.Backspace, Key.Enter, Key.CursorUp, Key.End }) Assert.True(editor.NewKeyDownEvent(key), key.ToString());

        Assert.Equal("SELECT 1\nFROM t\nx", editor.Text.ReplaceLineEndings("\n"));
        Assert.Equal(new Point(6, 1), editor.InsertionPoint);
        Assert.Equal(4, changes);
    }

    [Fact]
    public void A_cut_that_changes_nothing_reports_no_change()
    {
        var editor = Editor("SELECT 1", new Point(8, 0));
        var changes = 0;
        editor.ContentsChanged += (_, _) => changes++;

        editor.NewKeyDownEvent(Key.K.WithCtrl);

        Assert.Equal("SELECT 1", editor.Text);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void Completion_reads_the_script_as_it_is_after_a_line_cut()
    {
        var catalog = new SchemaCatalog { Databases = ["shop"] };
        var orders = new DbObject("dbo", "orders", DbObjectKind.Table);
        catalog.SetObjects("shop", [orders]);
        catalog.SetColumns("shop", orders, [new ColumnDefinition("total", "int", false, false, false)]);
        var editor = Editor("SELECT o.t\nFROM orders o", new Point(10, 0));
        editor.EnableCompletion(() => (catalog, "shop"));

        Assert.Contains("total", Suggestions(editor));

        editor.InsertionPoint = new Point(0, 1);
        editor.NewKeyDownEvent(Key.K.WithCtrl);
        editor.InsertionPoint = new Point(10, 0);

        Assert.DoesNotContain("total", Suggestions(editor));
    }

    [Fact]
    public void Columns_loaded_after_the_keystroke_are_suggested_on_refresh()
    {
        var catalog = new SchemaCatalog { Databases = ["shop"] };
        var orders = new DbObject("dbo", "orders", DbObjectKind.Table);
        catalog.SetObjects("shop", [orders]);
        // Unfocused, the refresh skips drawing the popup, which needs a running application.
        var editor = Editor("SELECT o.t\nFROM orders o", new Point(10, 0), focused: false);
        editor.EnableCompletion(() => (catalog, "shop"));

        Assert.DoesNotContain("total", Suggestions(editor));

        catalog.SetColumns("shop", orders, [new ColumnDefinition("total", "int", false, false, false)]);
        editor.RefreshCompletion();

        Assert.Contains("total", Suggestions(editor));
    }

    private static List<string> Suggestions(SqlEditor editor)
    {
        var line = editor.GetCurrentLine();
        var context = new AutocompleteContext(line, editor.CurrentColumn, false);
        return [.. editor.Autocomplete.SuggestionGenerator.GenerateSuggestions(context).Select(s => s.Replacement)];
    }
}
