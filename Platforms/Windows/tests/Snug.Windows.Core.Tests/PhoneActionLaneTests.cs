using Snug.Windows.Core;

namespace Snug.Windows.Core.Tests;

public sealed class PhoneActionLaneTests
{
    [Fact]
    public async Task ConcurrentActionsExecuteInSubmissionOrder()
    {
        var lane = new PhoneActionLane();
        var firstStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstToFinish = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var order = new List<int>();

        var first = lane.RunAsync(async () =>
        {
            order.Add(1);
            firstStarted.SetResult(true);
            await allowFirstToFinish.Task;
            order.Add(2);
        });
        await firstStarted.Task;
        var second = lane.RunAsync(() =>
        {
            order.Add(3);
            return Task.CompletedTask;
        });
        var third = lane.RunAsync(() =>
        {
            order.Add(4);
            return Task.CompletedTask;
        });

        Assert.Equal([1], order);
        allowFirstToFinish.SetResult(true);
        await Task.WhenAll(first, second, third);
        Assert.Equal([1, 2, 3, 4], order);
    }

    [Fact]
    public async Task NestedActionsExecuteWithoutDeadlocking()
    {
        var lane = new PhoneActionLane();
        var calls = 0;
        var operation = lane.RunAsync(async () =>
        {
            calls++;
            await lane.RunAsync(() =>
            {
                calls++;
                return Task.CompletedTask;
            });
        });

        var winner = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(operation, winner);
        await operation;
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task FailedActionDoesNotPoisonNextAction()
    {
        var lane = new PhoneActionLane();
        var broken = lane.RunAsync(() => Task.FromException(
            new InvalidOperationException("Expected test failure")));
        var success = false;
        var next = lane.RunAsync(() =>
        {
            success = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => broken);
        await next;
        Assert.True(success);
    }

    [Fact]
    public async Task DrainWaitsForQueuedActions()
    {
        var lane = new PhoneActionLane();
        var hold = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var ran = false;
        var first = lane.RunAsync(() => hold.Task);
        var second = lane.RunAsync(() =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        var drain = lane.DrainAsync();
        Assert.False(drain.IsCompleted);
        hold.SetResult(true);
        await Task.WhenAll(first, second, drain);
        Assert.True(ran);
    }
}
