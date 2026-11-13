using System.Drawing;
using LazyTsql.Core.Completion;
using LazyTsql.Core.Schema;
using LazyTsql.Tui;

#pragma warning disable CS0618

namespace LazyTsql.Tests;

public class EditorDiagnosticsTests
{
    private static (SqlEditor Editor, SchemaCatalog Catalog) Editor(string text, Action<SchemaCatalog>? load = null)
    {
        var catalog = new SchemaCatalog { Databases = ["shop"] };
        load?.Invoke(catalog);
        var editor = new SqlEditor { Text = text, Width = 60, Height = 10, DiagnosticsDelay = TimeSpan.Zero, PostToUi = action => action() };
        editor.BeginInit();
        editor.EndInit();
        editor.EnableCompletion(() => (catalog, "shop"));
        return (editor, catalog);
    }

    private static void LoadShop(SchemaCatalog catalog) => catalog.SetObjects("shop", [new DbObject("dbo", "orders", DbObjectKind.Table)]);

    private static async Task Diagnose(SqlEditor editor)
    {
        editor.ScheduleDiagnostics();
        await editor.PendingDiagnostics;
    }

    [Fact]
    public async Task Unknown_names_are_found_in_the_background_and_shown_when_they_arrive()
    {
        var (editor, _) = Editor("SELECT * FROM orders\nSELECT * FROM nope", LoadShop);

        Assert.Empty(editor.UnknownNames);
        await Diagnose(editor);

        Assert.Equal([new Diagnostic(1, 14, 18, "Unknown table or view: nope")], editor.UnknownNames);
    }

    [Fact]
    public async Task Names_the_search_did_not_have_are_requested_and_searched_again_once_loaded()
    {
        var (editor, catalog) = Editor("SELECT * FROM nope");
        var requested = new List<string>();
        catalog.ObjectsNeeded += requested.Add;

        await Diagnose(editor);
        Assert.Equal(["shop"], requested);
        Assert.Empty(editor.UnknownNames);

        LoadShop(catalog);
        editor.RefreshCompletion();
        await Diagnose(editor);

        Assert.Equal(["shop"], requested);
        Assert.Equal("Unknown table or view: nope", Assert.Single(editor.UnknownNames).Message);
    }

    [Fact]
    public async Task A_search_replaced_by_a_newer_one_shows_nothing()
    {
        var (editor, _) = Editor("SELECT * FROM nope", LoadShop);
        editor.DiagnosticsDelay = TimeSpan.FromSeconds(30);
        editor.ScheduleDiagnostics();
        var replaced = editor.PendingDiagnostics;

        editor.DiagnosticsDelay = TimeSpan.Zero;
        editor.Text = "SELECT * FROM orders";
        await Diagnose(editor);
        await replaced;

        Assert.Empty(editor.UnknownNames);
    }

    [Fact]
    public async Task An_edit_keeps_the_marks_of_other_rows_until_the_new_ones_arrive()
    {
        var (editor, _) = Editor("SELECT * FROM nope\nSELECT * FROM gone", LoadShop);
        await Diagnose(editor);
        Assert.Equal(2, editor.UnknownNames.Count);

        editor.InsertionPoint = new Point(18, 1);
        editor.InsertText("x");
        Assert.Equal([0], editor.UnknownNames.Select(d => d.Row));

        editor.InsertionPoint = new Point(18, 0);
        editor.InsertText("\n");
        Assert.Equal("SELECT * FROM nope\n\nSELECT * FROM gonex", editor.Text.ReplaceLineEndings("\n"));
        Assert.Empty(editor.UnknownNames);

        await Diagnose(editor);
        Assert.Equal([0, 2], editor.UnknownNames.Select(d => d.Row));
    }

    [Fact]
    public async Task Without_a_connection_nothing_is_marked()
    {
        var editor = new SqlEditor { Text = "SELECT * FROM nope", DiagnosticsDelay = TimeSpan.Zero, PostToUi = action => action() };
        editor.EnableCompletion(() => null);

        await Diagnose(editor);

        Assert.Empty(editor.UnknownNames);
    }
}
