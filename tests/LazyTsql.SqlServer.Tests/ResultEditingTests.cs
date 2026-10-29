using LazyTsql.Core;
using LazyTsql.Core.Batches;
using LazyTsql.Core.Editing;
using LazyTsql.Core.Execution;

namespace LazyTsql.SqlServer.Tests;

/// <summary>Edits a query's results the way the TUI does: describe the query, change cells, run the script.</summary>
public sealed class ResultEditingTests
{
    private const string Schema = """
        CREATE TABLE dbo.customers (id int IDENTITY PRIMARY KEY, name nvarchar(20) NOT NULL, total AS id * 2);
        CREATE TABLE dbo.orders (oid int PRIMARY KEY, cid int NOT NULL, note nvarchar(10) NULL);
        CREATE TABLE dbo.audit (what nvarchar(20));
        CREATE TABLE dbo.events (at datetime NOT NULL PRIMARY KEY, label nvarchar(10) NOT NULL);
        INSERT dbo.customers (name) VALUES (N'Ana'), (N'Bo');
        INSERT dbo.orders VALUES (10, 1, N'first'), (11, 1, N'second'), (12, 2, NULL);
        INSERT dbo.events VALUES ('2024-02-29T10:05:01.003', N'odd tick');
        """;

    private const string Trigger = """
        CREATE TRIGGER dbo.customers_audit ON dbo.customers AFTER UPDATE AS
            INSERT dbo.audit VALUES (N'one'), (N'two'), (N'three');
        """;

    [SqlServerFact]
    public async Task Edits_of_a_joined_query_are_saved_by_key_to_each_table()
    {
        await WithDatabaseAsync(async (session, database) =>
        {
            var query = new SqlBatch("SELECT c.name, o.note, c.id, o.oid, UPPER(c.name) AS up, c.total FROM dbo.customers c JOIN dbo.orders o ON o.cid = c.id ORDER BY o.oid", 1);
            var set = (await session.ExecuteAsync(query, new QueryOptions(), CancellationToken.None)).ResultSets[0];

            var edits = ResultEdits.Create(set, await session.DescribeResultAsync(database, query, CancellationToken.None), database);
            edits.Set(0, 0, "Anna");
            edits.Set(2, 1, "it's late");
            edits.Set(1, 1, null);

            var result = await RunAsync(session, edits.Script());

            Assert.False(result.HasErrors, string.Join("\n", result.Messages.Select(m => m.Text)));
            Assert.Equal(
                [null, null, "id cannot be changed: it is an identity, computed or rowversion column.", null,
                 "up is computed by the query, not read from a table.", "total cannot be changed: it is an identity, computed or rowversion column."],
                Enumerable.Range(0, 6).Select(edits.ReadOnlyReason));
            var rows = (await RunAsync(session, "SELECT c.name, o.note FROM dbo.customers c JOIN dbo.orders o ON o.cid = c.id ORDER BY o.oid")).ResultSets[0].Rows;
            Assert.Equal([["Anna", "first"], ["Anna", null], ["Bo", "it's late"]], rows);
        });
    }

    [SqlServerFact]
    public async Task A_row_deleted_since_the_query_ran_saves_none_of_the_changes()
    {
        await WithDatabaseAsync(async (session, database) =>
        {
            var query = new SqlBatch("SELECT oid, note FROM dbo.orders ORDER BY oid", 1);
            var set = (await session.ExecuteAsync(query, new QueryOptions(), CancellationToken.None)).ResultSets[0];
            var edits = ResultEdits.Create(set, await session.DescribeResultAsync(database, query, CancellationToken.None), database);
            edits.Set(0, 1, "changed");
            edits.Set(1, 1, "changed");
            await RunAsync(session, "DELETE dbo.orders WHERE oid = 11");

            var result = await RunAsync(session, edits.Script());

            var error = Assert.Single(result.Messages, m => m.IsError);
            Assert.Equal(50000, error.Number);
            Assert.Equal($"Row 2 no longer matches exactly one row of {SqlValue.Quote(database)}.[dbo].[orders]. Nothing was saved.", error.Text);
            Assert.Equal("first", (await RunAsync(session, "SELECT note FROM dbo.orders WHERE oid = 10")).ResultSets[0].Rows[0][0]);
            Assert.Equal(0, (await RunAsync(session, "SELECT @@TRANCOUNT")).ResultSets[0].Rows[0][0]);
        });
    }

    [SqlServerFact]
    public async Task Saving_inside_an_open_transaction_is_refused_and_leaves_it_untouched()
    {
        await WithDatabaseAsync(async (session, database) =>
        {
            var query = new SqlBatch("SELECT oid, note FROM dbo.orders ORDER BY oid", 1);
            var set = (await session.ExecuteAsync(query, new QueryOptions(), CancellationToken.None)).ResultSets[0];
            var edits = ResultEdits.Create(set, await session.DescribeResultAsync(database, query, CancellationToken.None), database);
            edits.Set(0, 1, "changed");
            await RunAsync(session, "BEGIN TRANSACTION; INSERT dbo.audit VALUES (N'mine');");

            var result = await RunAsync(session, edits.Script());

            Assert.Equal("A transaction is open in this session. Commit or roll it back before saving edits.", Assert.Single(result.Messages, m => m.IsError).Text);
            Assert.Equal(1, (await RunAsync(session, "SELECT @@TRANCOUNT")).ResultSets[0].Rows[0][0]);
            Assert.Equal("mine", (await RunAsync(session, "SELECT what FROM dbo.audit")).ResultSets[0].Rows[0][0]);
            Assert.Equal("first", (await RunAsync(session, "SELECT note FROM dbo.orders WHERE oid = 10")).ResultSets[0].Rows[0][0]);
            await RunAsync(session, "ROLLBACK");
        });
    }

    [SqlServerFact]
    public async Task A_self_join_is_read_only_instead_of_updating_the_other_instance_row()
    {
        await WithDatabaseAsync(async (session, database) =>
        {
            var query = new SqlBatch("SELECT e.id, e.name, b.id AS bid, b.name AS bname FROM dbo.customers e JOIN dbo.customers b ON b.id = e.id + 1", 1);
            var set = (await session.ExecuteAsync(query, new QueryOptions(), CancellationToken.None)).ResultSets[0];
            var edits = ResultEdits.Create(set, await session.DescribeResultAsync(database, query, CancellationToken.None), database);

            Assert.NotNull(edits.ReadOnlyReason(1));
            Assert.NotNull(edits.ReadOnlyReason(3));
        });
    }

    [SqlServerFact]
    public async Task A_trigger_that_writes_other_rows_does_not_fail_the_row_check()
    {
        await WithDatabaseAsync(async (session, database) =>
        {
            await RunAsync(session, Trigger);
            var query = new SqlBatch("SELECT id, name FROM dbo.customers ORDER BY id", 1);
            var set = (await session.ExecuteAsync(query, new QueryOptions(), CancellationToken.None)).ResultSets[0];
            var edits = ResultEdits.Create(set, await session.DescribeResultAsync(database, query, CancellationToken.None), database);
            edits.Set(1, 1, "Bob");

            var result = await RunAsync(session, edits.Script());

            Assert.False(result.HasErrors, string.Join("\n", result.Messages.Select(m => m.Text)));
            Assert.Equal("Bob", (await RunAsync(session, "SELECT name FROM dbo.customers WHERE id = 2")).ResultSets[0].Rows[0][0]);
        });
    }

    [SqlServerFact]
    public async Task A_datetime_key_read_from_the_server_finds_its_row()
    {
        await WithDatabaseAsync(async (session, database) =>
        {
            var query = new SqlBatch("SELECT at, label FROM dbo.events", 1);
            var set = (await session.ExecuteAsync(query, new QueryOptions(), CancellationToken.None)).ResultSets[0];
            var edits = ResultEdits.Create(set, await session.DescribeResultAsync(database, query, CancellationToken.None), database);
            edits.Set(0, 1, "renamed");

            var result = await RunAsync(session, edits.Script());

            Assert.False(result.HasErrors, string.Join("\n", result.Messages.Select(m => m.Text)));
            Assert.Equal("renamed", (await RunAsync(session, "SELECT label FROM dbo.events")).ResultSets[0].Rows[0][0]);
        });
    }

    [SqlServerFact]
    public async Task A_query_the_server_cannot_describe_fails_with_its_reason()
    {
        await WithDatabaseAsync(async (session, database) =>
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() =>
                session.DescribeResultAsync(database, new SqlBatch("SELECT * FROM #missing", 1), CancellationToken.None));

            Assert.Contains("#missing", error.Message, StringComparison.Ordinal);
        });
    }

    [SqlServerTheory]
    [InlineData("tinyint", "255")]
    [InlineData("smallint", "-300")]
    [InlineData("int", "42")]
    [InlineData("bigint", "9000000000")]
    [InlineData("decimal(10,2)", "12.50")]
    [InlineData("numeric(18,4)", "-0.0001")]
    [InlineData("money", "3.1")]
    [InlineData("smallmoney", "2.5")]
    [InlineData("float", "0.00001")]
    [InlineData("real", "2.25")]
    [InlineData("bit", "1")]
    [InlineData("date", "2024-02-29")]
    [InlineData("datetime", "2024-02-29 10:05:01.5")]
    [InlineData("smalldatetime", "2024-02-29 10:05")]
    [InlineData("datetime2", "2024-02-29 10:05:01.1234567")]
    [InlineData("datetimeoffset", "2024-02-29 10:05:01.1234567 -05:00")]
    [InlineData("time", "23:59:58.1234567")]
    [InlineData("uniqueidentifier", "0f8fad5b-d9cb-469f-a165-70867728950e")]
    [InlineData("char(3)", "abc")]
    [InlineData("varchar(10)", "it's")]
    [InlineData("nchar(3)", "año")]
    [InlineData("nvarchar(10)", "☃ snow")]
    [InlineData("binary(2)", "0x1FA0")]
    [InlineData("varbinary(10)", "0x00FF")]
    public async Task Each_type_written_as_a_literal_reads_back_the_same_value_and_matches_it(string type, string text)
    {
        await using var session = await OpenAsync();
        Assert.True(SqlValue.TryParse(text, type, out var value, out var error), error);

        var read = (await RunAsync(session, $"SELECT CAST({SqlValue.Literal(value, type)} AS {type})")).ResultSets[0].Rows[0][0];
        var match = await RunAsync(session, $"DECLARE @v {type} = {SqlValue.Literal(read, type)}; SELECT CASE WHEN @v = {SqlValue.Literal(read, type)} THEN 1 ELSE 0 END");

        Assert.Equal(value, read);
        Assert.Equal(1, match.ResultSets[0].Rows[0][0]);
    }

    private static Task<IDatabaseSession> OpenAsync() =>
        new SqlServerConnector().OpenAsync(SqlServerFactAttribute.ConnectionString!, CancellationToken.None);

    private static Task<BatchResult> RunAsync(IDatabaseSession session, string sql) =>
        session.ExecuteAsync(new SqlBatch(sql, 1), new QueryOptions(), CancellationToken.None);

    private static async Task WithDatabaseAsync(Func<IDatabaseSession, string, Task> test)
    {
        await using var session = await OpenAsync();
        // A ] and a quote in the name prove names are quoted, not concatenated.
        var database = $"lazytsql_{Guid.NewGuid():N}]'x";
        var quoted = SqlValue.Quote(database);
        await RunAsync(session, $"CREATE DATABASE {quoted}");
        try
        {
            await session.ChangeDatabaseAsync(database, CancellationToken.None);
            var setup = await RunAsync(session, Schema);
            Assert.False(setup.HasErrors, string.Join("\n", setup.Messages.Select(m => m.Text)));
            await test(session, database);
        }
        finally
        {
            await session.ChangeDatabaseAsync("master", CancellationToken.None);
            await RunAsync(session, $"ALTER DATABASE {quoted} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {quoted}");
        }
    }
}
