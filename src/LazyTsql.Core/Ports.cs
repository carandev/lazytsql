using LazyTsql.Core.Batches;
using LazyTsql.Core.Execution;
using LazyTsql.Core.Schema;

namespace LazyTsql.Core;

public interface IDatabaseConnector
{
    Task<IDatabaseSession> OpenAsync(string connectionString, CancellationToken cancellationToken);
}

/// <summary>An open connection. Schema is loaded lazily, one level at a time, to keep startup and memory low.</summary>
public interface IDatabaseSession : IAsyncDisposable
{
    string ServerVersion { get; }

    string CurrentDatabase { get; }

    /// <summary>Database named in the connection string; null when the connection is not scoped to one.</summary>
    string? ConfiguredDatabase { get; }

    Task<BatchResult> ExecuteAsync(SqlBatch batch, QueryOptions options, CancellationToken cancellationToken);

    /// <summary>
    /// Runs <paramref name="batch"/> again and streams every row of its result set number <paramref name="resultSetIndex"/>
    /// (zero-based) to <paramref name="sink"/>, ignoring <see cref="QueryOptions.MaxRows"/>. Returns the row count.
    /// </summary>
    Task<long> StreamResultSetAsync(SqlBatch batch, int resultSetIndex, IRowSink sink, QueryOptions options, CancellationToken cancellationToken);

    Task ChangeDatabaseAsync(string database, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken);

    /// <summary>Objects of <paramref name="database"/>; reading another database does not change <see cref="CurrentDatabase"/>.</summary>
    Task<IReadOnlyList<DbObject>> GetObjectsAsync(string database, CancellationToken cancellationToken);

    Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(string database, DbObject table, CancellationToken cancellationToken);

    /// <summary>Every foreign key of <paramref name="database"/>, for join completion.</summary>
    Task<IReadOnlyList<ForeignKey>> GetForeignKeysAsync(string database, CancellationToken cancellationToken);

    Task<string> GetDefinitionAsync(DbObject routine, CancellationToken cancellationToken);
}
