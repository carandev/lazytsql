namespace LazyTsql.Tui;

/// <summary>
/// Runs the operations on the one database connection one at a time, in the order they were asked. Cancelling stops
/// the running operation and every waiting one that can be cancelled; operations asked afterwards run as usual.
/// </summary>
public sealed class OperationQueue : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource _cancellation = new();
    private int _cancellable;

    /// <summary>True while an operation runs, so a new one waits.</summary>
    public bool IsBusy => _gate.CurrentCount == 0;

    /// <summary>
    /// Queues <paramref name="work"/>; it gets a token that <see cref="Cancel"/> cancels when <paramref name="cancellable"/>.
    /// Its place is taken now, on the calling thread, so operations keep the order they were asked in.
    /// </summary>
    public Task Run(Func<CancellationToken, Task> work, bool cancellable)
    {
        var token = cancellable ? _cancellation.Token : CancellationToken.None;
        if (cancellable) Interlocked.Increment(ref _cancellable);
        var entered = _gate.WaitAsync(token);
        return Task.Run(async () =>
        {
            try
            {
                await entered;
                try
                {
                    await work(token);
                }
                finally
                {
                    _gate.Release();
                }
            }
            finally
            {
                if (cancellable) Interlocked.Decrement(ref _cancellable);
            }
        }, CancellationToken.None);
    }

    /// <summary>Cancels the running and the waiting cancellable operations; false when there was none.</summary>
    public bool Cancel()
    {
        if (Volatile.Read(ref _cancellable) == 0) return false;
        Interlocked.Exchange(ref _cancellation, new CancellationTokenSource()).Cancel();
        return true;
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _gate.Dispose();
    }
}
