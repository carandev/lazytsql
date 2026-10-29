using LazyTsql.Core.Editing;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;
using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class TuiTests
{
    [Fact]
    public void Table_source_formats_cells_on_demand_as_single_lines()
    {
        var source = new ResultSetTableSource(new ResultSet(
            [new ColumnInfo("id", "int"), new ColumnInfo("note", "nvarchar")],
            [[1, "a\nb\tc"], [2, null]],
            IsTruncated: false));

        Assert.Equal(["id", "note"], source.ColumnNames);
        Assert.Equal((2, 2), (source.Rows, source.Columns));
        Assert.Equal("a⏎b c", source[0, 1]);
        Assert.Equal("NULL", source[1, 1]);
    }

    [Fact]
    public void Table_source_shows_pending_edits_in_place_of_the_read_values()
    {
        var set = new ResultSet([new ColumnInfo("id", "int"), new ColumnInfo("note", "nvarchar")], [[1, "old"], [2, "keep"]], IsTruncated: false);
        var edits = ResultEdits.Create(set, [
            new SourceColumn("id", false, null, "dbo", "notes", "id", true, true, "int"),
            new SourceColumn("note", false, null, "dbo", "notes", "note", false, true, "nvarchar(10)"),
        ], "shop");
        var source = new ResultSetTableSource(set) { Edits = edits };

        edits.Set(0, 1, "new\nline");
        edits.Set(1, 1, null);

        Assert.Equal("new⏎line", source[0, 1]);
        Assert.Equal("NULL", source[1, 1]);
        Assert.Equal("old", set.Rows[0][1]);
    }

    [Fact]
    public void Results_footer_shows_the_edit_keys_and_the_cell_editor_its_own()
    {
        var results = KeyHelp.Footer(KeyContext.Results);
        Assert.Contains("e/Enter edit cell", results, StringComparison.Ordinal);
        Assert.Contains("s save edits", results, StringComparison.Ordinal);
        Assert.Contains("u/U undo cell/all", results, StringComparison.Ordinal);
        Assert.StartsWith("Enter set value · Ctrl+N set NULL · Esc cancel", KeyHelp.Footer(KeyContext.CellEdit), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_tables_and_views_expand_to_columns()
    {
        Assert.True(new ObjectNode("shop", new DbObject("dbo", "t", DbObjectKind.Table)).CanExpand);
        Assert.True(new ObjectNode("shop", new DbObject("dbo", "v", DbObjectKind.View)).CanExpand);
        Assert.False(new ObjectNode("shop", new DbObject("dbo", "p", DbObjectKind.Procedure)).CanExpand);
        Assert.Equal("Tables (0)", new FolderNode(DbObjectKind.Table, []).Label);
    }

    [Fact]
    public void Footer_shows_the_context_keys_then_the_global_keys_that_work_there()
    {
        var schema = KeyHelp.Footer(KeyContext.Schema);
        Assert.StartsWith("Enter expand", schema, StringComparison.Ordinal);
        Assert.Contains("s SELECT TOP 100", schema, StringComparison.Ordinal);
        Assert.EndsWith("? help · q quit", schema, StringComparison.Ordinal);

        // In the editor letters and digits are text, so its footer points to keys that still work.
        Assert.EndsWith("F1 help · Ctrl+Q quit", KeyHelp.Footer(KeyContext.Query), StringComparison.Ordinal);

        var picker = KeyHelp.Footer(KeyContext.FilePicker);
        Assert.Contains("Tab complete", picker, StringComparison.Ordinal);
        Assert.DoesNotContain("1-6", picker, StringComparison.Ordinal);
    }

    [Fact]
    public void Files_can_be_searched_with_a_slash_like_in_vim()
    {
        Assert.Contains("/ search", KeyHelp.Footer(KeyContext.Files), StringComparison.Ordinal);
        Assert.Contains("o add folder", KeyHelp.Footer(KeyContext.Files), StringComparison.Ordinal);
        Assert.Contains("d remove folder", KeyHelp.Footer(KeyContext.Files), StringComparison.Ordinal);
        Assert.Contains("n/N next/prev match", KeyHelp.Footer(KeyContext.Files), StringComparison.Ordinal);
        Assert.StartsWith("type to search · Enter keep · Esc cancel", KeyHelp.Footer(KeyContext.FileSearch), StringComparison.Ordinal);
    }

    [Fact]
    public void Confirmation_buttons_show_their_key_and_can_be_chosen_with_the_arrows()
    {
        Assert.Equal("Discard (y)", ConfirmDialog.ButtonText("Discard", 'y'));
        Assert.Equal("Cancel (n)", ConfirmDialog.ButtonText("Cancel", 'n'));
        Assert.Contains("←/→ choose", KeyHelp.Footer(KeyContext.Confirm), StringComparison.Ordinal);
    }

    [Fact]
    public void Help_lists_how_to_resize_panels()
    {
        Assert.Contains(KeyHelp.Render(context: null), line => line.Contains("Alt+arrows", StringComparison.Ordinal) && line.Contains("resize", StringComparison.Ordinal));
    }

    [Fact]
    public void Help_lists_how_to_close_a_script()
    {
        Assert.Contains(KeyHelp.Render(KeyContext.Query).Concat(KeyHelp.Render(context: null)), line => line.Contains("Ctrl+W", StringComparison.Ordinal) && line.Contains("close .sql", StringComparison.Ordinal));
    }

    [Fact]
    public void Files_footer_shows_how_to_open_a_script()
    {
        var files = KeyHelp.Footer(KeyContext.Files);

        Assert.StartsWith("Enter open/expand", files, StringComparison.Ordinal);
        Assert.Contains("r refresh", files, StringComparison.Ordinal);
        Assert.EndsWith("1-6 panels · ? help · q quit", files, StringComparison.Ordinal);
    }

    [Fact]
    public void Help_shows_only_the_current_context_and_expands_to_every_key()
    {
        var schema = KeyHelp.Render(KeyContext.Schema);
        Assert.Contains("Schema [3]", schema);
        Assert.DoesNotContain(schema, line => line.Contains("Connections [1]", StringComparison.Ordinal));
        Assert.Contains(schema, line => line.Contains("show every key", StringComparison.Ordinal));

        var all = KeyHelp.Render(context: null);
        foreach (var title in new[] { "Global", "Connections [1]", "Files [2]", "Schema [3]", "Query [4]", "Results [5]", "Messages [6]", "File picker", "Confirmation", "Connection form", "Edit cell" })
            Assert.Contains(all, line => line.Contains(title, StringComparison.Ordinal));
        Assert.Contains(all, line => line.Contains("export to CSV", StringComparison.Ordinal));
        Assert.Contains(all, line => line.Contains("show only", StringComparison.Ordinal));
        Assert.All(all, line => Assert.True(line.Length <= 74, $"too wide: '{line}'"));
    }

    [Fact]
    public void Every_context_has_registered_keys()
    {
        foreach (var context in Enum.GetValues<KeyContext>())
        {
            Assert.NotEmpty(KeyHelp.Footer(context));
            Assert.True(KeyHelp.Render(context).Count > 2, context.ToString());
        }
    }

    [Theory]
    [InlineData("all", 0)]
    [InlineData(" ALL ", 0)]
    [InlineData("", 0)]
    [InlineData("0", 0)]
    [InlineData("5000", 5000)]
    public void Row_limit_accepts_a_count_or_all(string input, int expected)
    {
        Assert.Equal(expected, RowLimitDialog.Parse(input));
    }

    [Theory]
    [InlineData("-3")]
    [InlineData("ten")]
    [InlineData("1.5")]
    public void Row_limit_rejects_anything_else(string input)
    {
        Assert.Null(RowLimitDialog.Parse(input));
    }

    [Fact]
    public void Results_footer_shows_the_row_limit_key()
    {
        Assert.Contains("m row limit", KeyHelp.Footer(KeyContext.Results), StringComparison.Ordinal);
    }

    [Fact]
    public void Schema_footer_shows_the_bacpac_keys()
    {
        var footer = KeyHelp.Footer(KeyContext.Schema);

        Assert.Contains("x export bacpac", footer, StringComparison.Ordinal);
        Assert.Contains("i import bacpac", footer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/backups/shop.bacpac", "shop")]
    [InlineData("/backups/shop-20261028-101500.bacpac", "shop-20261028-101500")]
    public void An_imported_database_is_named_after_its_file(string file, string expected)
    {
        Assert.Equal(expected, NameDialog.DefaultDatabaseName(file));
    }

    [Theory]
    [InlineData("  shop_copy ", "shop_copy")]
    [InlineData("   ", null)]
    public void A_database_name_is_trimmed_and_cannot_be_empty(string input, string? expected)
    {
        Assert.Equal(expected, NameDialog.Parse(input));
    }
}
