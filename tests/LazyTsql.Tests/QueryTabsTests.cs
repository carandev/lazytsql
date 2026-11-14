using System.Text;
using LazyTsql.Infrastructure;
using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class QueryTabsTests
{
    private static ScriptFile Script(string name) => new(Path.GetFullPath(name), Encoding.UTF8, "\n");

    [Fact]
    public void Starts_with_one_untitled_tab_holding_the_initial_text()
    {
        var tabs = new QueryTabs("SELECT 1;");

        Assert.Equal(1, tabs.Count);
        Assert.Equal(0, tabs.Index);
        Assert.Equal("SELECT 1;", tabs.Current.Text);
        Assert.Equal("untitled", tabs.Current.Name);
        Assert.False(tabs.Current.IsDirty);
    }

    [Fact]
    public void Opening_a_script_reuses_an_untouched_untitled_tab_and_otherwise_adds_one_after_the_current()
    {
        var tabs = new QueryTabs("SELECT 1;");

        tabs.Open(Script("a.sql"), "A");
        Assert.Equal(1, tabs.Count);
        Assert.Equal("a.sql", tabs.Current.Name);

        tabs.Open(Script("b.sql"), "B");
        tabs.Select(0);
        tabs.Open(Script("c.sql"), "C");

        Assert.Equal(["a.sql", "c.sql", "b.sql"], tabs.All.Select(tab => tab.Name));
        Assert.Equal(1, tabs.Index);
    }

    [Fact]
    public void An_edited_untitled_tab_is_kept_when_a_script_opens()
    {
        var tabs = new QueryTabs("SELECT 1;");
        tabs.Current.Text = "SELECT 2;";

        tabs.Open(Script("a.sql"), "A");

        Assert.Equal(2, tabs.Count);
        Assert.Equal("SELECT 2;", tabs.All[0].Text);
    }

    [Fact]
    public void Opening_a_script_already_open_switches_to_its_tab_keeping_its_edits()
    {
        var tabs = new QueryTabs("x");
        tabs.Open(Script("a.sql"), "A");
        tabs.Current.Text = "A edited";
        tabs.Open(Script("b.sql"), "B");

        tabs.Open(Script("a.sql"), "A from disk");

        Assert.Equal(2, tabs.Count);
        Assert.Equal(0, tabs.Index);
        Assert.Equal("A edited", tabs.Current.Text);
    }

    [Fact]
    public void Generated_sql_always_opens_a_new_untitled_tab()
    {
        var tabs = new QueryTabs("SELECT 1;");

        tabs.Open(null, "SELECT TOP (100) * FROM t;");

        Assert.Equal(2, tabs.Count);
        Assert.Equal(1, tabs.Index);
        Assert.Equal("SELECT TOP (100) * FROM t;", tabs.Current.Text);
    }

    [Fact]
    public void Select_wraps_around_both_ends()
    {
        var tabs = new QueryTabs("x");
        tabs.Add();
        tabs.Add();

        tabs.Select(tabs.Index + 1);
        Assert.Equal(0, tabs.Index);
        tabs.Select(tabs.Index - 1);
        Assert.Equal(2, tabs.Index);
    }

    [Fact]
    public void Closing_moves_to_the_previous_tab_and_the_last_one_leaves_an_empty_untitled_tab()
    {
        var tabs = new QueryTabs("x");
        tabs.Open(Script("a.sql"), "A");
        tabs.Add();
        tabs.Add();
        tabs.Select(1);

        tabs.Close();
        Assert.Equal(["a.sql", "untitled"], tabs.All.Select(tab => tab.Name));
        Assert.Equal(0, tabs.Index);

        tabs.Close();
        tabs.Close();
        Assert.Equal(1, tabs.Count);
        Assert.Equal(string.Empty, tabs.Current.Text);
        Assert.Null(tabs.Current.Script);
    }

    [Fact]
    public void A_script_is_dirty_when_its_text_differs_from_the_saved_one_and_an_untitled_tab_has_unsaved_text_when_edited()
    {
        var tabs = new QueryTabs("SELECT 1;");
        Assert.False(tabs.Current.HasUnsavedText);
        tabs.Current.Text = "SELECT 2;";
        Assert.False(tabs.Current.IsDirty);
        Assert.True(tabs.Current.HasUnsavedText);
        tabs.Current.Text = string.Empty;
        Assert.False(tabs.Current.HasUnsavedText);

        tabs.Open(Script("a.sql"), "A");
        Assert.False(tabs.Current.IsDirty);
        tabs.Current.Text = "B";
        Assert.True(tabs.Current.IsDirty);
        Assert.True(tabs.Current.HasUnsavedText);
        Assert.Equal(["a.sql"], tabs.Dirty.Select(tab => tab.Name));
    }

    [Fact]
    public void Position_shows_only_with_more_than_one_tab()
    {
        var tabs = new QueryTabs("x");
        Assert.Equal(string.Empty, tabs.Position);
        tabs.Add();
        Assert.Equal(" 2/2", tabs.Position);
    }
}
