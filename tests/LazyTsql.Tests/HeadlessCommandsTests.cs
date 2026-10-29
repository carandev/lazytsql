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
    private readonly FakeBacpacTool _bacpac = new();

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private HeadlessCommands Commands() => new(
        new FakeConnector(),
        _bacpac,
        new FileConnectionStore(Path.Combine(_directory, "connections.json")),
        _stdout,
        Stream.Null,
        _stderr,
        TextReader.Null,
        _ => null,
        new ActivityLog(Path.Combine(_directory, "logs"), TimeProvider.System));

    private string ActivityText() => File.ReadAllText(Directory.GetFiles(Path.Combine(_directory, "logs")).Single());

    [Fact]
    public async Task A_query_is_logged_with_its_sql_messages_and_exit_code_but_not_the_connection_string()
    {
        await Commands().QueryAsync(CliArguments.Parse(["query", "-c", "Server=db1;User Id=sa;Password=S3cret", "-q", "SELECT 1\nGO\nSELECT 2"]), CancellationToken.None);

        var log = ActivityText();
        Assert.Matches(@"\[cli\] query on db1:\n    SELECT 1\n    GO\n    SELECT 2\n", log);
        Assert.Contains("[cli] hello\n", log);
        Assert.Contains("[cli] query finished: 2 batches, 4 result sets, exit code 0", log);
        Assert.DoesNotContain("S3cret", log);
    }

    [Fact]
    public async Task A_bacpac_export_and_its_failure_are_logged()
    {
        _bacpac.Failure = "sqlpackage export failed (exit code 1): boom";

        await Commands().ExportAsync(CliArguments.Parse(["export", "-c", "Server=db1;Password=S3cret", "-d", "shop", "shop.bacpac"]), CancellationToken.None);

        var log = ActivityText();
        Assert.Contains($"[cli] export shop on db1 to {Path.GetFullPath("shop.bacpac")}", log);
        Assert.Contains("[cli] ✗ sqlpackage export failed (exit code 1): boom", log);
        Assert.DoesNotContain("S3cret", log);
    }

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

    [Fact]
    public async Task Export_writes_the_database_to_a_bacpac_with_sqlpackage_output_on_stderr()
    {
        var file = Path.Combine(_directory, "shop");

        var exitCode = await Commands().ExportAsync(CliArguments.Parse(["export", "-c", "Server=x;Password=p", "-d", "shop", file]), CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(("Export", "Server=x;Password=p", "shop", $"{file}.bacpac"), _bacpac.Calls.Single());
        Assert.Equal("Processing...\n", _stderr.ToString().ReplaceLineEndings("\n"));
        Assert.Equal($"Exported shop to {file}.bacpac\n", _stdout.ToString().ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Export_defaults_to_the_database_of_the_connection()
    {
        await Commands().ExportAsync(CliArguments.Parse(["export", "-c", "Server=x;Database=app", "app.bacpac"]), CancellationToken.None);

        Assert.Equal("app", _bacpac.Calls.Single().Database);
    }

    [Fact]
    public async Task Import_creates_the_database_from_an_existing_file()
    {
        var file = Path.Combine(_directory, "shop.bacpac");
        File.WriteAllText(file, "");

        var exitCode = await Commands().ImportAsync(CliArguments.Parse(["import", "-c", "Server=x;", "-d", "shop_copy", file]), CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal(("Import", "Server=x;", "shop_copy", file), _bacpac.Calls.Single());
        Assert.Equal($"Imported {file} as shop_copy\n", _stdout.ToString().ReplaceLineEndings("\n"));
    }

    [Theory]
    [InlineData(new[] { "import", "-c", "Server=x;", "-d", "shop", "missing.bacpac" }, "File not found: missing.bacpac")]
    [InlineData(new[] { "export", "-c", "Server=x;", "shop.bacpac" }, "Name the database with -d <database>.")]
    [InlineData(new[] { "export", "-c", "Server=x;", "-d", "shop" }, "Usage: lazytsql export [-c <conn>] [-d <db>] <file.bacpac>")]
    [InlineData(new[] { "export", "-c", "Server=x;", "-d", "shop", "shop.bacpac", "--json" }, "export does not support --json or --csv.")]
    public async Task Bacpac_usage_errors(string[] args, string message)
    {
        var commands = Commands();
        var arguments = CliArguments.Parse(args);

        var error = await Assert.ThrowsAsync<CliException>(() => args[0] == "export"
            ? commands.ExportAsync(arguments, CancellationToken.None)
            : commands.ImportAsync(arguments, CancellationToken.None));

        Assert.Equal(message, error.Message);
        Assert.Empty(_bacpac.Calls);
    }

    [Fact]
    public async Task A_sqlpackage_failure_exits_with_one()
    {
        _bacpac.Failure = "sqlpackage export failed (exit code 1): boom";

        var exitCode = await Commands().ExportAsync(CliArguments.Parse(["export", "-c", "Server=x;", "-d", "shop", "shop.bacpac"]), CancellationToken.None);

        Assert.Equal(1, exitCode);
        Assert.EndsWith("lazytsql: sqlpackage export failed (exit code 1): boom\n", _stderr.ToString().ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Empty(_stdout.ToString());
    }

    private sealed class FakeBacpacTool : IBacpacTool
    {
        public List<(string Action, string ConnectionString, string Database, string File)> Calls { get; } = [];

        public string? Failure { get; set; }

        public Task ExportAsync(string connectionString, string database, string file, Action<string> output, CancellationToken cancellationToken) =>
            Run("Export", connectionString, database, file, output);

        public Task ImportAsync(string connectionString, string database, string file, Action<string> output, CancellationToken cancellationToken) =>
            Run("Import", connectionString, database, file, output);

        private Task Run(string action, string connectionString, string database, string file, Action<string> output)
        {
            Calls.Add((action, connectionString, database, file));
            output("Processing...");
            return Failure is null ? Task.CompletedTask : Task.FromException(new BacpacException(Failure));
        }
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

        public Task<long> StreamResultSetAsync(SqlBatch batch, int resultSetIndex, IRowSink sink, QueryOptions options, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task ChangeDatabaseAsync(string database, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObject>> GetObjectsAsync(string database, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(string database, DbObject table, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(string database, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<DbObject>> GetSynonymsAsync(string database, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetDefinitionAsync(DbObject routine, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
