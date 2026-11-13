using LazyTsql.Core.Completion;

namespace LazyTsql.Tui;

/// <summary>
/// Finds the unknown names of a script off the UI thread, once the editor has been still for a moment. A newer request
/// cancels the waiting one, and a result that arrives after a newer request is dropped.
/// </summary>
internal sealed class DiagnosticsWorker(Action<Action> post)
{
    private CancellationTokenSource? _pending;

    // Changed only on the UI thread: by Schedule and Cancel, and read by the posted result.
    private int _generation;

    /// <summary>The last scheduled run, so tests can wait for it.</summary>
    public Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>
    /// Diagnoses <paramref name="lines"/> against a snapshot of <paramref name="catalog"/> after <paramref name="delay"/>,
    /// then on the UI thread requests the names the snapshot did not have and passes the marks to <paramref name="show"/>.
    /// </summary>
    public void Schedule(
        IReadOnlyList<string> lines,
        SchemaCatalog catalog,
        string database,
        (int Row, int Column)? cursor,
        TimeSpan delay,
        Action<IReadOnlyList<Diagnostic>> show)
    {
        Cancel();
        var cancellation = _pending = new CancellationTokenSource();
        var generation = _generation;
        var snapshot = catalog.Snapshot();
        Pending = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, cancellation.Token).ConfigureAwait(false);
                var diagnostics = SqlCompletion.Diagnose(lines, snapshot, database, cursor);
                if (cancellation.IsCancellationRequested) return;
                post(() =>
                {
                    if (generation != _generation) return;
                    catalog.RequestMissing(snapshot);
                    show(diagnostics);
                });
            }
            catch (OperationCanceledException)
            {
                // A newer edit or cursor move replaced this request.
            }
        });
    }

    /// <summary>Drops the waiting request and any result still on its way.</summary>
    public void Cancel()
    {
        _pending?.Cancel();
        _generation++;
    }
}
