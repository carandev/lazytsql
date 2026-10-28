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
            var objects = await session.GetObjectsAsync(CancellationToken.None);
            var table = Assert.Single(objects, o => o.Kind == DbObjectKind.Table);
            var procedure = Assert.Single(objects, o => o.Kind == DbObjectKind.Procedure);
            var columns = await session.GetColumnsAsync(table, CancellationToken.None);
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
}
