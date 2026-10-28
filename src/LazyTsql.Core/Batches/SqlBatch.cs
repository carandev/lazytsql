namespace LazyTsql.Core.Batches;

/// <summary>A batch of T-SQL sent to the server in one round trip.</summary>
/// <param name="StartLine">1-based line in the original script, used to map server error lines back to the editor.</param>
public sealed record SqlBatch(string Text, int StartLine, int RepeatCount = 1);
