namespace Snug.Windows.Core;

/// <summary>
/// A FIFO action lane equivalent to Snug's original SerialLane. Nested calls
/// in the same logical operation execute inline, avoiding queue deadlocks.
/// A failed action cannot poison subsequent queued actions.
/// </summary>
public sealed class PhoneActionLane
{
    private readonly object _gate = new();
    private readonly AsyncLocal<int> _depth = new();
    private Task _tail = Task.CompletedTask;

    public Task RunAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_depth.Value > 0) return action();

        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task previous;
        lock (_gate)
        {
            previous = _tail;
            _tail = completion.Task;
        }

        return ExecuteAsync(previous, action, completion);
    }

    public async Task DrainAsync()
    {
        while (true)
        {
            Task current;
            lock (_gate) current = _tail;
            await current;
            lock (_gate)
            {
                if (ReferenceEquals(_tail, current)) return;
            }
        }
    }

    private async Task ExecuteAsync(Task previous, Func<Task> action,
        TaskCompletionSource<bool> completion)
    {
        try
        {
            await previous;
            _depth.Value++;
            try { await action(); }
            finally { _depth.Value--; }
        }
        finally
        {
            completion.TrySetResult(true);
        }
    }
}
