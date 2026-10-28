using LazyTsql.Core.Batches;

namespace LazyTsql.Core.Execution;

public sealed record ColumnInfo(string Name, string TypeName);

/// <summary>One result set. Rows are capped by <see cref="QueryOptions.MaxRows"/> to keep memory bounded.</summary>
public sealed record ResultSet(IReadOnlyList<ColumnInfo> Columns, IReadOnlyList<object?[]> Rows, bool IsTruncated);

/// <summary>Informational message or error raised by the server (PRINT, RAISERROR, errors).</summary>
public sealed record ServerMessage(string Text, int Number, byte Severity, int LineNumber)
{
    public bool IsError => Severity > 10;
}

public sealed record BatchResult(
    SqlBatch Batch,
    IReadOnlyList<ResultSet> ResultSets,
    IReadOnlyList<ServerMessage> Messages,
    int RecordsAffected,
    TimeSpan Elapsed)
{
    public bool HasErrors => Messages.Any(m => m.IsError);
}

public sealed record QueryOptions(int MaxRows = 1000, int CommandTimeoutSeconds = 0);
