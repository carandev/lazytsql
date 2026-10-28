using LazyTsql.Core.Batches;

namespace LazyTsql.Core.Execution;

/// <summary>Runs a script batch by batch, honoring <c>GO n</c> and stopping at the first failing batch like sqlcmd -b.</summary>
public static class ScriptRunner
{
    public static async IAsyncEnumerable<BatchResult> RunAsync(
        IDatabaseSession session,
        string script,
        QueryOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var batch in BatchSplitter.Split(script))
        {
            for (var run = 0; run < batch.RepeatCount; run++)
            {
                var result = await session.ExecuteAsync(batch, options, cancellationToken);
                yield return result;
                if (result.HasErrors) yield break;
            }
        }
    }
}
