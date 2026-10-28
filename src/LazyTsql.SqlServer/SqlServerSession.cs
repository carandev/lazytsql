using System.Data;
using System.Diagnostics;
using LazyTsql.Core;
using LazyTsql.Core.Batches;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;
using Microsoft.Data.SqlClient;
using SqlBatch = LazyTsql.Core.Batches.SqlBatch;

namespace LazyTsql.SqlServer;

internal sealed class SqlServerSession : IDatabaseSession
{
    private readonly SqlConnection _connection;
    private List<ServerMessage>? _messageSink;

    public SqlServerSession(SqlConnection connection, string? configuredDatabase)
    {
        _connection = connection;
        ConfiguredDatabase = configuredDatabase;
        _connection.InfoMessage += (_, e) => _messageSink?.AddRange(e.Errors.Cast<SqlError>().Select(ToMessage));
    }

    public string ServerVersion => _connection.ServerVersion;

    public string CurrentDatabase => _connection.Database;

    public string? ConfiguredDatabase { get; }

    public async Task<BatchResult> ExecuteAsync(SqlBatch batch, QueryOptions options, CancellationToken cancellationToken)
    {
        var resultSets = new List<ResultSet>();
        var messages = new List<ServerMessage>();
        var recordsAffected = -1;
        var stopwatch = Stopwatch.StartNew();
        _messageSink = messages;

        try
        {
            await using var command = new SqlCommand(batch.Text, _connection) { CommandTimeout = options.CommandTimeoutSeconds };
            await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
            do
            {
                if (reader.FieldCount > 0)
                {
                    resultSets.Add(await ReadResultSetAsync(reader, options.MaxRows, cancellationToken));
                }
            }
            while (await reader.NextResultAsync(cancellationToken));

            await reader.CloseAsync();
            recordsAffected = reader.RecordsAffected;
        }
        catch (SqlException ex)
        {
            messages.AddRange(ex.Errors.Cast<SqlError>().Select(ToMessage));
        }
        finally
        {
            _messageSink = null;
        }

        return new BatchResult(batch, resultSets, messages, recordsAffected, stopwatch.Elapsed);
    }

    public async Task<long> StreamResultSetAsync(
        SqlBatch batch,
        int resultSetIndex,
        IRowSink sink,
        QueryOptions options,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(batch.Text, _connection) { CommandTimeout = options.CommandTimeoutSeconds };
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
        // Closing the reader reads every remaining row; cancelling the command stops the server instead.
        using var stopOnCancel = cancellationToken.Register(command.Cancel);
        var index = 0;
        do
        {
            if (reader.FieldCount == 0 || index++ < resultSetIndex) continue;

            sink.Begin(ReadColumns(reader));
            long count = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                sink.Row(await ReadRowAsync(reader, cancellationToken));
                count++;
            }

            return count;
        }
        while (await reader.NextResultAsync(cancellationToken));

        throw new InvalidOperationException($"The batch no longer returns result set {resultSetIndex + 1}.");
    }

    public Task ChangeDatabaseAsync(string database, CancellationToken cancellationToken) =>
        _connection.ChangeDatabaseAsync(database, cancellationToken);

    public Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            "SELECT name FROM sys.databases WHERE state = 0 AND HAS_DBACCESS(name) = 1 ORDER BY name",
            r => r.GetString(0),
            cancellationToken);

    public Task<IReadOnlyList<DbObject>> GetObjectsAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT s.name, o.name, RTRIM(o.type)
            FROM sys.objects o
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF') AND o.is_ms_shipped = 0
            ORDER BY s.name, o.name
            """,
            r => new DbObject(r.GetString(0), r.GetString(1), ToKind(r.GetString(2))),
            cancellationToken);

    public Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(DbObject table, CancellationToken cancellationToken) =>
        QueryAsync(
            """
            SELECT c.name, TYPE_NAME(c.user_type_id), c.max_length, c.precision, c.scale,
                   c.is_nullable, c.is_identity,
                   CAST(CASE WHEN pk.column_id IS NULL THEN 0 ELSE 1 END AS bit)
            FROM sys.columns c
            LEFT JOIN (
                SELECT ic.object_id, ic.column_id
                FROM sys.indexes i
                JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                WHERE i.is_primary_key = 1
            ) pk ON pk.object_id = c.object_id AND pk.column_id = c.column_id
            WHERE c.object_id = OBJECT_ID(@name)
            ORDER BY c.column_id
            """,
            r => new ColumnDefinition(
                r.GetString(0),
                SqlTypeFormatter.Format(r.GetString(1), r.GetInt16(2), r.GetByte(3), r.GetByte(4)),
                r.GetBoolean(5),
                r.GetBoolean(7),
                r.GetBoolean(6)),
            cancellationToken,
            new SqlParameter("@name", SqlDbType.NVarChar, 512) { Value = table.QualifiedName });

    public async Task<string> GetDefinitionAsync(DbObject routine, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            "SELECT OBJECT_DEFINITION(OBJECT_ID(@name))",
            r => r.IsDBNull(0) ? string.Empty : r.GetString(0),
            cancellationToken,
            new SqlParameter("@name", SqlDbType.NVarChar, 512) { Value = routine.QualifiedName });
        return rows.Count > 0 ? rows[0] : string.Empty;
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    private static async Task<ResultSet> ReadResultSetAsync(SqlDataReader reader, int maxRows, CancellationToken cancellationToken)
    {
        var columns = ReadColumns(reader);
        var rows = new List<object?[]>();
        var truncated = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            if (maxRows > 0 && rows.Count == maxRows)
            {
                // Remaining rows are discarded by NextResultAsync; only the capped page is kept in memory.
                truncated = true;
                break;
            }

            rows.Add(await ReadRowAsync(reader, cancellationToken));
        }

        return new ResultSet(columns, rows, truncated);
    }

    private static ColumnInfo[] ReadColumns(SqlDataReader reader)
    {
        var columns = new ColumnInfo[reader.FieldCount];
        for (var i = 0; i < columns.Length; i++)
        {
            columns[i] = new ColumnInfo(reader.GetName(i), reader.GetDataTypeName(i));
        }

        return columns;
    }

    private static async Task<object?[]> ReadRowAsync(SqlDataReader reader, CancellationToken cancellationToken)
    {
        var row = new object?[reader.FieldCount];
        for (var i = 0; i < row.Length; i++)
        {
            row[i] = await reader.IsDBNullAsync(i, cancellationToken) ? null : reader.GetValue(i);
        }

        return row;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<SqlDataReader, T> map,
        CancellationToken cancellationToken,
        params SqlParameter[] parameters)
    {
        await using var command = new SqlCommand(sql, _connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(map(reader));
        }

        return items;
    }

    private static ServerMessage ToMessage(SqlError error) =>
        new(error.Message, error.Number, error.Class, error.LineNumber);

    private static DbObjectKind ToKind(string type) => type switch
    {
        "U" => DbObjectKind.Table,
        "V" => DbObjectKind.View,
        "P" => DbObjectKind.Procedure,
        _ => DbObjectKind.Function,
    };
}
