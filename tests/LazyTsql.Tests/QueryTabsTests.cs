using System.Text;
using LazyTsql.Core;
using LazyTsql.Core.Batches;
using LazyTsql.Core.Editing;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;
using LazyTsql.Infrastructure;
using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class QueryTabsTests
{
    private static ScriptFile Script(string name) => new(Path.GetFullPath(name), Encoding.UTF8, "\n");

    private static ShownResult Result() =>
        new(new ResultSet([new ColumnInfo("id", "int")], [[1]], IsTruncated: false), new FakeSession(), new SqlBatch("SELECT 1;", 1), 0, "master");

    private sealed class FakeSession : IDatabaseSession
    {
        public string ServerVersion => "fake";
        public string CurrentDatabase => "master";
        public string? ConfiguredDatabase => null;
        public Task<BatchResult> ExecuteAsync(SqlBatch batch, QueryOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<long> StreamResultSetAsync(SqlBatch batch, int resultSetIndex, IRowSink sink, QueryOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ChangeDatabaseAsync(string database, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObject>> GetObjectsAsync(string database, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(string database, DbObject table, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(string database, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObject>> GetSynonymsAsync(string database, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetDefinitionAsync(DbObject routine, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<SourceColumn>> DescribeResultAsync(string database, SqlBatch batch, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

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
    public void A_new_tab_starts_with_no_result_sets()
    {
        var tabs = new QueryTabs("SELECT 1;");
        Assert.Empty(tabs.Current.ResultSets);
        Assert.Equal(0, tabs.Current.ResultIndex);
    }

    [Fact]
    public void Each_tab_keeps_its_own_result_sets_when_switching()
    {
        var tabs = new QueryTabs("SELECT 1;");
        tabs.Current.ResultSets = [Result()];
        tabs.Current.ResultIndex = 0;

        tabs.Add();
        Assert.Empty(tabs.Current.ResultSets);

        tabs.Select(0);
        Assert.Single(tabs.Current.ResultSets);
    }

    [Fact]
    public void Reusing_an_untouched_untitled_tab_for_a_script_keeps_its_result_sets()
    {
        var tabs = new QueryTabs("SELECT 1;");
        tabs.Current.ResultSets = [Result()];
        tabs.Current.ResultIndex = 0;

        tabs.Open(Script("a.sql"), "A");

        Assert.Equal(1, tabs.Count);
        Assert.Single(tabs.Current.ResultSets);
    }

    [Fact]
    public void A_new_tab_has_no_connection_and_reusing_an_untouched_one_keeps_it()
    {
        var tabs = new QueryTabs("SELECT 1;");
        Assert.Null(tabs.Current.ConnectionName);

        tabs.Current.ConnectionName = "prod";
        tabs.Open(Script("a.sql"), "A");

        Assert.Equal(1, tabs.Count);
        Assert.Equal("prod", tabs.Current.ConnectionName);
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
