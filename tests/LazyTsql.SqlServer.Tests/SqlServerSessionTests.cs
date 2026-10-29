using LazyTsql.Core;
using LazyTsql.Core.Batches;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;

namespace LazyTsql.SqlServer.Tests;

/// <summary>Runs only when LAZYTSQL_TEST_CONNECTION points at a disposable SQL Server.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public static string? ConnectionString => Environment.GetEnvironmentVariable("LAZYTSQL_TEST_CONNECTION");

    public SqlServerFactAttribute()
    {
        if (string.IsNullOrEmpty(ConnectionString)) Skip = "LAZYTSQL_TEST_CONNECTION is not set.";
    }
}

public class SqlServerSessionTests
{
    private static Task<IDatabaseSession> OpenAsync() =>
        new SqlServerConnector().OpenAsync(SqlServerFactAttribute.ConnectionString!, CancellationToken.None);

    private static Task<BatchResult> RunAsync(IDatabaseSession session, string sql, int maxRows = 1000) =>
        session.ExecuteAsync(new SqlBatch(sql, 1), new QueryOptions(maxRows), CancellationToken.None);

    [SqlServerFact]
    public async Task Returns_every_result_set_with_columns_and_nulls()
    {
        await using var session = await OpenAsync();

        var result = await RunAsync(session, "SELECT 1 AS id, N'a' AS name; SELECT CAST(NULL AS int) AS empty;");

        Assert.Equal(2, result.ResultSets.Count);
        Assert.Equal(["id", "name"], result.ResultSets[0].Columns.Select(c => c.Name));
        Assert.Equal([1, "a"], result.ResultSets[0].Rows[0]);
        Assert.Null(result.ResultSets[1].Rows[0][0]);
        Assert.False(result.HasErrors);
    }

    [SqlServerFact]
    public async Task Caps_rows_and_keeps_reading_later_result_sets()
    {
        await using var session = await OpenAsync();

        var result = await RunAsync(session, "SELECT TOP (50) object_id FROM sys.all_objects; SELECT 2 AS second;", maxRows: 10);

        Assert.Equal(10, result.ResultSets[0].Rows.Count);
        Assert.True(result.ResultSets[0].IsTruncated);
        Assert.Equal(2, result.ResultSets[1].Rows[0][0]);
    }

    [SqlServerFact]
    public async Task Collects_print_messages_and_errors_with_line_numbers()
    {
        await using var session = await OpenAsync();

        var info = await RunAsync(session, "PRINT 'hello'");
        var error = await RunAsync(session, "SELECT 1;\nSELECT * FROM dbo.does_not_exist;");

        Assert.Equal("hello", Assert.Single(info.Messages).Text);
        Assert.False(info.HasErrors);
        var failure = Assert.Single(error.Messages, m => m.IsError);
        Assert.Equal(208, failure.Number);
        Assert.Equal(2, failure.LineNumber);
        Assert.Single(error.ResultSets);
    }

    [SqlServerFact]
    public async Task Exposes_the_database_named_in_the_connection_string()
    {
        var withDatabase = SqlServerFactAttribute.ConnectionString + ";Database=tempdb";
        await using var scoped = await new SqlServerConnector().OpenAsync(withDatabase, CancellationToken.None);
        await using var unscoped = await OpenAsync();

        Assert.Equal("tempdb", scoped.ConfiguredDatabase);
        Assert.Null(unscoped.ConfiguredDatabase);
    }

    [SqlServerFact]
    public async Task Reports_records_affected()
    {
        await using var session = await OpenAsync();

        var result = await RunAsync(session, "DECLARE @t TABLE (id int); INSERT INTO @t VALUES (1), (2), (3);");

        Assert.Equal(3, result.RecordsAffected);
    }

    [SqlServerFact]
    public async Task Introspects_tables_columns_and_routines()
    {
        await using var session = await OpenAsync();
        var database = $"lazytsql_{Guid.NewGuid():N}";
        await RunAsync(session, $"CREATE DATABASE [{database}]");
        try
        {
            await session.ChangeDatabaseAsync(database, CancellationToken.None);
            await RunAsync(session, "CREATE TABLE dbo.customers (id int IDENTITY PRIMARY KEY, name nvarchar(100) NULL, balance decimal(10,2) NOT NULL)");
            await RunAsync(session, "CREATE PROCEDURE dbo.get_customers AS SELECT * FROM dbo.customers");

            var databases = await session.GetDatabasesAsync(CancellationToken.None);
            var objects = await session.GetObjectsAsync(database, CancellationToken.None);
            var table = Assert.Single(objects, o => o.Kind == DbObjectKind.Table);
            var procedure = Assert.Single(objects, o => o.Kind == DbObjectKind.Procedure);
            var columns = await session.GetColumnsAsync(database, table, CancellationToken.None);
            var definition = await session.GetDefinitionAsync(procedure, CancellationToken.None);

            Assert.Contains(database, databases);
            Assert.Equal(database, session.CurrentDatabase);
            Assert.Equal(
                [
                    new ColumnDefinition("id", "int", false, true, true),
                    new ColumnDefinition("name", "nvarchar(100)", true, false, false),
                    new ColumnDefinition("balance", "decimal(10,2)", false, false, false),
                ],
                columns);
            Assert.Contains("CREATE PROCEDURE dbo.get_customers", definition);
        }
        finally
        {
            await session.ChangeDatabaseAsync("master", CancellationToken.None);
            await RunAsync(session, $"ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]");
        }
    }

    [SqlServerFact]
    public async Task Reads_another_database_without_changing_the_current_one()
    {
        await using var session = await OpenAsync();
        // A ] and a quote in the name prove it is quoted, not concatenated.
        var database = $"lazytsql_{Guid.NewGuid():N}]'x";
        var quoted = $"[{database.Replace("]", "]]")}]";
        await RunAsync(session, $"CREATE DATABASE {quoted}");
        try
        {
            await RunAsync(session, $"EXEC {quoted}.sys.sp_executesql N'CREATE TABLE dbo.items (id int NOT NULL)'");

            var objects = await session.GetObjectsAsync(database, CancellationToken.None);
            var table = Assert.Single(objects);
            var columns = await session.GetColumnsAsync(database, table, CancellationToken.None);

            Assert.Equal(new DbObject("dbo", "items", DbObjectKind.Table), table);
            Assert.Equal("id", Assert.Single(columns).Name);
            Assert.Equal("master", session.CurrentDatabase);
        }
        finally
        {
            await RunAsync(session, $"ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quoted}");
        }
    }

    [SqlServerFact]
    public async Task Reads_foreign_keys_and_synonyms_of_another_database()
    {
        await using var session = await OpenAsync();
        var database = $"lazytsql_{Guid.NewGuid():N}]'x";
        var quoted = $"[{database.Replace("]", "]]")}]";
        await RunAsync(session, $"CREATE DATABASE {quoted}");
        try
        {
            await RunAsync(session, $"""
                EXEC {quoted}.sys.sp_executesql N'
                CREATE TABLE dbo.lines (order_id int NOT NULL, line int NOT NULL, PRIMARY KEY (order_id, line));
                CREATE TABLE dbo.shipments (id int NOT NULL, line int NOT NULL, order_id int NOT NULL,
                    FOREIGN KEY (order_id, line) REFERENCES dbo.lines (order_id, line));'
                """);

            await RunAsync(session, $"EXEC {quoted}.sys.sp_executesql N'CREATE SYNONYM dbo.deliveries FOR dbo.shipments; CREATE SYNONYM dbo.objs FOR master.sys.objects; CREATE SYNONYM dbo.remote FOR srv.db.dbo.t; CREATE SYNONYM dbo.bare FOR shipments'");

            var key = Assert.Single(await session.GetForeignKeysAsync(database, CancellationToken.None));
            var synonyms = await session.GetSynonymsAsync(database, CancellationToken.None);

            Assert.Equal(("dbo", "shipments", "dbo", "lines"), (key.FromSchema, key.FromTable, key.ToSchema, key.ToTable));
            Assert.Equal([new KeyColumn("order_id", "order_id"), new KeyColumn("line", "line")], key.Columns);
            Assert.Equal(
            [
                new DbObject("dbo", "bare", DbObjectKind.Synonym, new SynonymTarget(null, null, "shipments")),
                new DbObject("dbo", "deliveries", DbObjectKind.Synonym, new SynonymTarget(null, "dbo", "shipments")),
                new DbObject("dbo", "objs", DbObjectKind.Synonym, new SynonymTarget("master", "sys", "objects")),
                // A synonym for an object on a linked server has no target lazytsql can read.
                new DbObject("dbo", "remote", DbObjectKind.Synonym),
            ], synonyms);
            Assert.Equal("master", session.CurrentDatabase);
        }
        finally
        {
            await RunAsync(session, $"ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quoted}");
        }
    }

    [SqlServerFact]
    public async Task Max_rows_zero_returns_every_row()
    {
        await using var session = await OpenAsync();

        var result = await RunAsync(session, "SELECT TOP (1500) object_id FROM sys.all_objects", maxRows: 0);

        Assert.Equal(1500, result.ResultSets[0].Rows.Count);
        Assert.False(result.ResultSets[0].IsTruncated);
    }

    [SqlServerFact]
    public async Task Streams_every_row_of_the_chosen_result_set()
    {
        await using var session = await OpenAsync();
        var sink = new RecordingSink();
        var batch = new SqlBatch("PRINT 'x'; SELECT 1 AS first; SELECT TOP (1500) object_id AS id FROM sys.all_objects;", 1);

        var count = await session.StreamResultSetAsync(batch, 1, sink, new QueryOptions(), CancellationToken.None);

        Assert.Equal(1500, count);
        Assert.Equal(["id"], sink.Columns.Select(c => c.Name));
        Assert.Equal(1500, sink.Rows.Count);
    }

    [SqlServerFact]
    public async Task Streaming_a_missing_result_set_fails()
    {
        await using var session = await OpenAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.StreamResultSetAsync(new SqlBatch("SELECT 1", 1), 1, new RecordingSink(), new QueryOptions(), CancellationToken.None));
    }

    [SqlServerFact]
    public async Task Cancelling_a_stream_stops_the_server_instead_of_reading_the_remaining_rows()
    {
        await using var session = await OpenAsync();
        using var cancellation = new CancellationTokenSource();
        var sink = new RecordingSink();
        // Cancelled from another thread while rows are streaming, as Ctrl+K does in the TUI.
        cancellation.CancelAfter(TimeSpan.FromSeconds(1));
        var huge = new SqlBatch("SELECT a.object_id, b.name FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c", 1);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var error = await Record.ExceptionAsync(() => session.StreamResultSetAsync(huge, 0, sink, new QueryOptions(), cancellation.Token));

        Assert.NotNull(error);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
    }

    [SqlServerFact]
    public async Task A_lone_SELECT_stops_the_server_at_the_row_limit_instead_of_reading_the_rest()
    {
        await using var session = await OpenAsync();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Billions of rows: reading past the limit would not finish.
        var result = await RunAsync(session, "SELECT a.object_id, b.name FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c", maxRows: 10);

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"took {stopwatch.Elapsed}");
        Assert.Equal(10, result.ResultSets[0].Rows.Count);
        Assert.True(result.ResultSets[0].IsTruncated);
        Assert.Empty(result.Messages);
        Assert.Equal(1, (await RunAsync(session, "SELECT 1 AS still_usable")).ResultSets[0].Rows[0][0]);
    }

    [SqlServerFact]
    public async Task A_capped_SELECT_followed_by_another_statement_still_runs_it()
    {
        await using var session = await OpenAsync();

        var result = await RunAsync(session, "SELECT TOP (50) object_id FROM sys.all_objects\nPRINT 'after'", maxRows: 10);

        Assert.True(result.ResultSets[0].IsTruncated);
        Assert.Equal("after", Assert.Single(result.Messages).Text);
    }

    private sealed class RecordingSink : IRowSink
    {
        public IReadOnlyList<ColumnInfo> Columns { get; private set; } = [];
        public List<object?[]> Rows { get; } = [];
        public void Begin(IReadOnlyList<ColumnInfo> columns) => Columns = columns;
        public int Count { get; private set; }
        public void Row(object?[] values)
        {
            // Only counts: a huge stream must not be held in memory.
            if (Count++ < 2000) Rows.Add(values);
        }
    }
}
