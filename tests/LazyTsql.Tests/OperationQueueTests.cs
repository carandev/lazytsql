using LazyTsql.Tui;

namespace LazyTsql.Tests;

public class OperationQueueTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Operations_run_one_at_a_time_in_the_order_they_were_asked()
    {
        using var queue = new OperationQueue();
        var first = new TaskCompletionSource();
        var order = new List<int>();

        var running = queue.Run(async _ => { order.Add(1); await first.Task; order.Add(2); }, cancellable: true);
        var second = queue.Run(_ => { order.Add(3); return Task.CompletedTask; }, cancellable: false);
        var third = queue.Run(_ => { order.Add(4); return Task.CompletedTask; }, cancellable: true);

        Assert.True(queue.IsBusy);
        first.SetResult();
        await Task.WhenAll(running, second, third).WaitAsync(Timeout);

        Assert.Equal([1, 2, 3, 4], order);
        Assert.False(queue.IsBusy);
    }

    [Fact]
    public async Task Cancel_stops_the_running_operation_and_every_waiting_one_that_can_be_cancelled()
    {
        using var queue = new OperationQueue();
        var started = new TaskCompletionSource();
        var ran = new List<string>();

        var running = queue.Run(async token => { started.SetResult(); await Task.Delay(Timeout, token); ran.Add("running"); }, cancellable: true);
        var waiting = queue.Run(_ => { ran.Add("waiting"); return Task.CompletedTask; }, cancellable: true);
        var schema = queue.Run(_ => { ran.Add("schema"); return Task.CompletedTask; }, cancellable: false);
        await started.Task.WaitAsync(Timeout);

        Assert.True(queue.Cancel());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(Timeout));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Timeout));
        await schema.WaitAsync(Timeout);
        Assert.Equal(["schema"], ran);
    }

    [Fact]
    public async Task Operations_asked_after_a_cancel_are_not_cancelled()
    {
        using var queue = new OperationQueue();
        var started = new TaskCompletionSource();
        var running = queue.Run(async token => { started.SetResult(); await Task.Delay(Timeout, token); }, cancellable: true);
        await started.Task.WaitAsync(Timeout);
        queue.Cancel();

        var token = CancellationToken.None;
        await queue.Run(t => { token = t; return Task.CompletedTask; }, cancellable: true).WaitAsync(Timeout);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.True(token.CanBeCanceled);
        Assert.False(token.IsCancellationRequested);
    }

    [Fact]
    public async Task Cancel_with_nothing_to_cancel_is_false()
    {
        using var queue = new OperationQueue();
        Assert.False(queue.Cancel());

        var gate = new TaskCompletionSource();
        var schema = queue.Run(_ => gate.Task, cancellable: false);
        Assert.False(queue.Cancel());
        gate.SetResult();
        await schema.WaitAsync(Timeout);
    }
}
