using System.Drawing;
using LazyTsql.Core.Completion;
using LazyTsql.Core.Schema;
using LazyTsql.Tui;
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.Views;

#pragma warning disable CS0618

namespace LazyTsql.Tests;

/// <summary>The suggestion popup only draws inside a running application, so these tests host the editor in one.</summary>
[Collection(TerminalCollection.Name)]
public sealed class EditorCompletionTests : IDisposable
{
    private readonly IApplication _app = Application.Create();
    private readonly SessionToken _session;
    private readonly SqlEditor _editor;

    public EditorCompletionTests()
    {
        _app.Init("ansi");
        var catalog = new SchemaCatalog { Databases = ["shop"] };
        var orders = new DbObject("dbo", "orders", DbObjectKind.Table);
        catalog.SetObjects("shop", [orders, new DbObject("dbo", "order_lines", DbObjectKind.Table)]);
        catalog.SetColumns("shop", orders, [new ColumnDefinition("id", "int", false, true, false), new ColumnDefinition("total", "int", false, false, false)]);
        _editor = new SqlEditor { Width = 60, Height = 10 };
        var window = new Window { Width = 70, Height = 14 };
        window.Add(_editor);
        _session = _app.Begin(window)!;
        _editor.EnableCompletion(() => (catalog, "shop"));
        _editor.SetFocus();
    }

    public void Dispose()
    {
        _app.End(_session);
        _app.Dispose();
    }

    private bool PopupVisible => ((PopupAutocomplete)_editor.Autocomplete).Visible;

    private void Start(string text, Point cursor)
    {
        _editor.Text = text;
        _editor.InsertionPoint = cursor;
        _app.LayoutAndDraw(true);
    }

    private void Press(params Key[] keys)
    {
        foreach (var key in keys)
        {
            _editor.NewKeyDownEvent(key);
            _app.LayoutAndDraw(false);
        }
    }

    [Fact]
    public void Arrows_move_through_the_script_without_opening_suggestions()
    {
        Start("SELECT o.id\nFROM orders o\nWHERE o.total > 1", new Point(3, 2));

        Press(Key.CursorUp);
        Assert.False(PopupVisible);
        Press(Key.CursorUp);

        Assert.Equal(new Point(3, 0), _editor.InsertionPoint);
        Assert.False(PopupVisible);
        Press(Key.End, Key.CursorLeft, Key.Home, Key.CursorDown);
        Assert.Equal(new Point(0, 1), _editor.InsertionPoint);
        Assert.False(PopupVisible);
    }

    [Fact]
    public void Typing_opens_suggestions_and_moving_away_closes_them()
    {
        Start("SELECT * FROM or", new Point(16, 0));

        Press(Key.D);
        Assert.True(PopupVisible);

        Press(Key.CursorLeft);
        Assert.False(PopupVisible);
        Press(Key.CursorRight);
        Assert.False(PopupVisible);

        Press(Key.Backspace);
        Assert.True(PopupVisible);
    }

    [Fact]
    public void Up_and_down_move_through_open_suggestions_and_tab_takes_the_selected_one()
    {
        Start("SELECT * FROM or", new Point(16, 0));

        Press(Key.D, Key.CursorDown, Key.Tab);

        // "orders" sorts first, so ↓ selects "order_lines".
        Assert.Equal("SELECT * FROM order_lines", _editor.Text);
        Assert.False(PopupVisible);
        Press(Key.CursorLeft, Key.CursorRight);
        Assert.False(PopupVisible);
    }

    [Fact]
    public void Ctrl_space_opens_suggestions_where_the_cursor_is_even_after_esc()
    {
        Start("SELECT * FROM orders o WHERE o.t", new Point(32, 0));

        Press(Key.Space.WithCtrl);
        Assert.True(PopupVisible);
        Press(Key.Esc);
        Assert.False(PopupVisible);
        Press(Key.Space.WithCtrl);
        Assert.True(PopupVisible);

        Press(Key.Tab);
        Assert.Equal("SELECT * FROM orders o WHERE o.total", _editor.Text);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TerminalCollection
{
    public const string Name = "Terminal";
}
