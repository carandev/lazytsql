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

    Task ChangeDatabaseAsync(string database, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetDatabasesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DbObject>> GetObjectsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(DbObject table, CancellationToken cancellationToken);

    Task<string> GetDefinitionAsync(DbObject routine, CancellationToken cancellationToken);
}
