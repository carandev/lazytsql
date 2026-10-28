using LazyTsql.Core.Batches;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;

namespace LazyTsql.Core.Tests;

public class ScriptRunnerTests
{
    [Fact]
    public async Task Runs_each_batch_repeat_count_times()
    {
        var session = new FakeSession();

        var results = await ToListAsync(ScriptRunner.RunAsync(session, "SELECT 1\nGO 3\nSELECT 2", new QueryOptions()));

        Assert.Equal(4, results.Count);
        Assert.Equal(["SELECT 1", "SELECT 1", "SELECT 1", "SELECT 2"], session.Executed);
    }

    [Fact]
    public async Task Stops_after_the_first_batch_with_errors()
    {
        var session = new FakeSession { FailingText = "BAD" };

        var results = await ToListAsync(ScriptRunner.RunAsync(session, "SELECT 1\nGO\nBAD\nGO\nSELECT 2", new QueryOptions()));

        Assert.Equal(2, results.Count);
        Assert.True(results[^1].HasErrors);
        Assert.Equal(["SELECT 1", "BAD"], session.Executed);
    }

    [Fact]
    public async Task Informational_messages_do_not_stop_the_script()
    {
        var session = new FakeSession { InfoText = "PRINT 'x'" };

        var results = await ToListAsync(ScriptRunner.RunAsync(session, "PRINT 'x'\nGO\nSELECT 2", new QueryOptions()));

        Assert.Equal(2, results.Count);
    }

    private static async Task<List<T>> ToListAsync<T>(IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (var item in source) list.Add(item);
        return list;
    }

    private sealed class FakeSession : IDatabaseSession
    {
        public List<string> Executed { get; } = [];
        public string? FailingText { get; init; }
        public string? InfoText { get; init; }
        public string ServerVersion => "fake";
        public string CurrentDatabase => "master";
        public string? ConfiguredDatabase => null;

        public Task<BatchResult> ExecuteAsync(SqlBatch batch, QueryOptions options, CancellationToken cancellationToken)
        {
            Executed.Add(batch.Text);
            List<ServerMessage> messages = [];
            if (batch.Text == FailingText) messages.Add(new ServerMessage("Incorrect syntax", 102, 15, 1));
            if (batch.Text == InfoText) messages.Add(new ServerMessage("x", 0, 0, 1));
            return Task.FromResult(new BatchResult(batch, [], messages, -1, TimeSpan.Zero));
        }

        public Task ChangeDatabaseAsync(string database, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObject>> GetObjectsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(DbObject table, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetDefinitionAsync(DbObject routine, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
