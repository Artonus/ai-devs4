namespace Agent.UI.Services;

/// <summary>
///     Singleton service that acts as a log channel between running tasks and Blazor components.
///     Tasks call <see cref="Log" /> to emit lines; UI components subscribe via <see cref="Subscribe" />.
/// </summary>
public class TaskLogService
{
    private readonly List<string> _history = [];

    private readonly object _lock = new();

    private readonly List<Action<string>> _subscribers = [];

    public bool IsRunning { get; private set; }

    /// <summary>Returns a snapshot of all log lines emitted since the last <see cref="Clear" />.</summary>
    public IReadOnlyList<string> History
    {
        get
        {
            lock (_lock)
            {
                return _history.ToList();
            }
        }
    }

    /// <summary>Subscribe to new log lines. The callback is invoked on the thread that calls <see cref="Log" />.</summary>
    public IDisposable Subscribe(Action<string> onLine)
    {
        lock (_lock)
        {
            _subscribers.Add(onLine);
        }

        return new Subscription(() =>
                                {
                                    lock (_lock)
                                    {
                                        _subscribers.Remove(onLine);
                                    }
                                });
    }

    /// <summary>Emit a log line to all subscribers and append it to history.</summary>
    public void Log(string line)
    {
        List<Action<string>> snapshot;

        lock (_lock)
        {
            _history.Add(line);
            snapshot = [.. _subscribers];
        }

        foreach (var sub in snapshot)
        {
            try
            {
                sub(line);
            }
            catch
            {
                /* don't let a bad subscriber kill the task */
            }
        }
    }

    /// <summary>Clears the history and marks a task as running.</summary>
    public void Begin()
    {
        lock (_lock)
        {
            _history.Clear();
            IsRunning = true;
        }
    }

    /// <summary>Marks the current task run as finished.</summary>
    public void End()
    {
        lock (_lock)
        {
            IsRunning = false;
        }

        // Notify subscribers so the UI can re-render with the final state
        List<Action<string>> snapshot;

        lock (_lock)
        {
            snapshot = [.. _subscribers];
        }

        foreach (var sub in snapshot)
        {
            try
            {
                sub(string.Empty);
            }
            catch {}
        }
    }

    private sealed class Subscription(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }
}