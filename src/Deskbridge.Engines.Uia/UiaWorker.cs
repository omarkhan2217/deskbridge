using System.Collections.Concurrent;
using FlaUI.UIA3;

namespace Deskbridge.Engines.Uia;

/// <summary>
/// Runs every UI Automation call on one MTA thread that owns one UIA3Automation, created once.
/// On that thread AutoSetFocus is off, so Invoke and SetValue never pull focus to the target.
/// If a call is still running after CallTimeout, the thread is abandoned and a fresh one takes the queue.
/// </summary>
public sealed class UiaWorker : IDisposable
{
    readonly UiaWorkerOptions options;
    readonly object gate = new();
    WorkerThread current;
    bool disposed;

    public UiaWorker(UiaWorkerOptions? options = null)
    {
        this.options = options ?? UiaWorkerOptions.Default;
        current = new WorkerThread(this.options);
    }

    public UiaWorkerOptions Options => options;

    public int ThreadId
    {
        get
        {
            lock (gate)
            {
                return current.ManagedThreadId;
            }
        }
    }

    public async Task<T> RunAsync<T>(Func<UIA3Automation, T> work, CancellationToken ct = default)
    {
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        WorkerThread thread;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            thread = current;
            thread.Post(automation =>
            {
                try
                {
                    result.TrySetResult(work(automation));
                }
                catch (Exception ex)
                {
                    result.TrySetException(ex);
                }
            });
        }

        var finished = await Task.WhenAny(result.Task, Task.Delay(options.CallTimeout, ct)).ConfigureAwait(false);
        if (finished == result.Task)
        {
            return await result.Task.ConfigureAwait(false);
        }

        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (current == thread && !disposed)
            {
                current = new WorkerThread(options);
                thread.Abandon(current);
            }
        }

        throw new UiaTimeoutException(options.CallTimeout);
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            current.Stop();
        }
    }

    sealed class WorkerThread
    {
        readonly BlockingCollection<Action<UIA3Automation>> queue = new();
        readonly Thread thread;

        public WorkerThread(UiaWorkerOptions options)
        {
            thread = new Thread(() => Run(options))
            {
                IsBackground = true,
                Name = "Deskbridge UI Automation",
            };
            thread.SetApartmentState(ApartmentState.MTA);
            thread.Start();
        }

        public int ManagedThreadId => thread.ManagedThreadId;

        public void Post(Action<UIA3Automation> item) => queue.Add(item);

        /// <summary>Stops taking new work and moves anything still waiting to the replacement thread.</summary>
        public void Abandon(WorkerThread replacement)
        {
            queue.CompleteAdding();
            while (queue.TryTake(out var item))
            {
                replacement.Post(item);
            }
        }

        public void Stop() => queue.CompleteAdding();

        void Run(UiaWorkerOptions options)
        {
            using var automation = new UIA3Automation();
            automation.NativeAutomation2.AutoSetFocus = 0;
            automation.TransactionTimeout = options.TransactionTimeout;
            automation.ConnectionTimeout = options.ConnectionTimeout;
            foreach (var item in queue.GetConsumingEnumerable())
            {
                item(automation);
            }
        }
    }
}
