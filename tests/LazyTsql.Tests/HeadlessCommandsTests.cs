using LazyTsql.Cli;
using LazyTsql.Core;
using LazyTsql.Core.Batches;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;
using LazyTsql.Infrastructure;

namespace LazyTsql.Tests;

public sealed class HeadlessCommandsTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("lazytsql-tests-").FullName;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private HeadlessCommands Commands() => new(
        new FakeConnector(),
        new FileConnectionStore(Path.Combine(_directory, "connections.json")),
        _stdout,
        Stream.Null,
        _stderr,
        TextReader.Null,
        _ => null);

    [Fact]
    public async Task Csv_writes_rows_to_stdout_and_messages_to_stderr()
    {
        var exitCode = await Commands().QueryAsync(CliArguments.Parse(["query", "-c", "Server=x;", "-q", "SELECT 1", "--csv"]), CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("id,name\n1,\"a,b\"\n\nid,name\n1,\"a,b\"\n", _stdout.ToString());
        Assert.Contains("hello", _stderr.ToString());
    }

    [Fact]
    public async Task Csv_and_json_together_are_a_usage_error()
    {
        await Assert.ThrowsAsync<CliException>(() =>
            Commands().QueryAsync(CliArguments.Parse(["query", "-q", "SELECT 1", "--csv", "--json"]), CancellationToken.None));
    }

    [Fact]
    public async Task Text_output_is_unchanged_without_format_flags()
    {
        await Commands().QueryAsync(CliArguments.Parse(["query", "-c", "Server=x;", "-q", "SELECT 1"]), CancellationToken.None);

        Assert.Contains("id | name", _stdout.ToString());
        Assert.Contains("hello", _stdout.ToString());
        Assert.Empty(_stderr.ToString());
    }

    private sealed class FakeConnector : IDatabaseConnector
    {
        public Task<IDatabaseSession> OpenAsync(string connectionString, CancellationToken cancellationToken) =>
            Task.FromResult<IDatabaseSession>(new FakeSession());
    }

    private sealed class FakeSession : IDatabaseSession
    {
        public string ServerVersion => "fake";
        public string CurrentDatabase => "master";
        public string? ConfiguredDatabase => null;

        public Task<BatchResult> ExecuteAsync(SqlBatch batch, QueryOptions options, CancellationToken cancellationToken)
        {
            var resultSet = new ResultSet([new ColumnInfo("id", "int"), new ColumnInfo("name", "nvarchar")], [[1, "a,b"]], false);
            return Task.FromResult(new BatchResult(batch, [resultSet, resultSet], [new ServerMessage("hello", 0, 0, 1)], -1, TimeSpan.Zero));
        }

        public Task ChangeDatabaseAsync(string database, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObject>> GetObjectsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(DbObject table, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetDefinitionAsync(DbObject routine, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
