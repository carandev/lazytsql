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
            var stopAtLimit = batch.IsSingleQuery;
            do
            {
                if (reader.FieldCount > 0)
                {
                    resultSets.Add(await ReadResultSetAsync(reader, options.MaxRows, cancellationToken));
                    if (stopAtLimit && resultSets[^1].IsTruncated)
                    {
                        // Nothing follows a lone SELECT, so stopping the server skips only the rows past the limit,
                        // which would otherwise all be read and discarded.
                        command.Cancel();
                        await CloseCancelledAsync(reader);
                        return new BatchResult(batch, resultSets, messages, recordsAffected, stopwatch.Elapsed);
                    }
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

    public Task<IReadOnlyList<DbObject>> GetObjectsAsync(string database, CancellationToken cancellationToken) =>
        QueryAsync(
            InDatabase(database, """
            SELECT s.name, o.name, RTRIM(o.type)
            FROM sys.objects o
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            WHERE o.type IN ('U', 'V', 'P', 'FN', 'IF', 'TF') AND o.is_ms_shipped = 0
            ORDER BY s.name, o.name
            """),
            r => new DbObject(r.GetString(0), r.GetString(1), ToKind(r.GetString(2))),
            cancellationToken);

    public Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(string database, DbObject table, CancellationToken cancellationToken) =>
        QueryAsync(
            InDatabase(database, """
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
            """, "@name nvarchar(512)"),
            r => new ColumnDefinition(
                r.GetString(0),
                SqlTypeFormatter.Format(r.GetString(1), r.GetInt16(2), r.GetByte(3), r.GetByte(4)),
                r.GetBoolean(5),
                r.GetBoolean(7),
                r.GetBoolean(6)),
            cancellationToken,
            new SqlParameter("@name", SqlDbType.NVarChar, 512) { Value = table.QualifiedName });

    public Task<IReadOnlyList<DbObject>> GetSynonymsAsync(string database, CancellationToken cancellationToken) =>
        QueryAsync(
            InDatabase(database, """
            SELECT s.name, o.name
            FROM sys.synonyms o
            JOIN sys.schemas s ON s.schema_id = o.schema_id
            ORDER BY s.name, o.name
            """),
            r => new DbObject(r.GetString(0), r.GetString(1), DbObjectKind.Synonym),
            cancellationToken);

    public async Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(string database, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync(
            InDatabase(database, """
            SELECT fk.object_id, fs.name, ft.name, ts.name, tt.name, fc.name, tc.name
            FROM sys.foreign_keys fk
            JOIN sys.tables ft ON ft.object_id = fk.parent_object_id
            JOIN sys.schemas fs ON fs.schema_id = ft.schema_id
            JOIN sys.tables tt ON tt.object_id = fk.referenced_object_id
            JOIN sys.schemas ts ON ts.schema_id = tt.schema_id
            JOIN sys.foreign_key_columns kc ON kc.constraint_object_id = fk.object_id
            JOIN sys.columns fc ON fc.object_id = kc.parent_object_id AND fc.column_id = kc.parent_column_id
            JOIN sys.columns tc ON tc.object_id = kc.referenced_object_id AND tc.column_id = kc.referenced_column_id
            ORDER BY fk.object_id, kc.constraint_column_id
            """),
            r => (Key: r.GetInt32(0), FromSchema: r.GetString(1), FromTable: r.GetString(2), ToSchema: r.GetString(3), ToTable: r.GetString(4),
                  Column: new KeyColumn(r.GetString(5), r.GetString(6))),
            cancellationToken);
        return [.. rows.GroupBy(row => row.Key).Select(key =>
        {
            var first = key.First();
            return new ForeignKey(first.FromSchema, first.FromTable, first.ToSchema, first.ToTable, [.. key.Select(row => row.Column)]);
        })];
    }

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

    /// <summary>Closing a reader whose command was cancelled reports the cancellation, which the caller asked for.</summary>
    private static async Task CloseCancelledAsync(SqlDataReader reader)
    {
        try
        {
            await reader.CloseAsync();
        }
        catch (SqlException)
        {
        }
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

    /// <summary>
    /// Runs <paramref name="sql"/> in <paramref name="database"/> through its sp_executesql, so the session keeps its
    /// current database. The name is bracket-quoted, so it cannot inject SQL.
    /// </summary>
    private static string InDatabase(string database, string sql, string? parameters = null)
    {
        var call = $"EXEC [{database.Replace("]", "]]")}].sys.sp_executesql N'{sql.Replace("'", "''")}'";
        return parameters is null ? call : $"{call}, N'{parameters}', {parameters.Split(' ')[0]} = {parameters.Split(' ')[0]}";
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
