namespace Deskbridge.Ui;

/// <summary>
/// The one STA thread that owns every Deskbridge window (confirmation, tray, settings). It starts on first
/// use, so a broker that never shows a window never loads WinForms' message loop.
/// </summary>
public sealed class UiThread : IDisposable
{
    readonly object gate = new();
    Thread? thread;
    SynchronizationContext? context;

    public Task<T> InvokeAsync<T>(Func<T> work)
    {
        var context = EnsureStarted();
        var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.Post(
            _ =>
            {
                try
                {
                    result.SetResult(work());
                }
                catch (Exception ex)
                {
                    result.SetException(ex);
                }
            },
            null);
        return result.Task;
    }

    public void Post(Action work) => EnsureStarted().Post(_ => work(), null);

    SynchronizationContext EnsureStarted()
    {
        lock (gate)
        {
            if (context is not null)
            {
                return context;
            }

            using var ready = new ManualResetEventSlim();
            thread = new Thread(() =>
            {
                Application.EnableVisualStyles();
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                context = SynchronizationContext.Current;
                ready.Set();
                Application.Run();
            })
            {
                IsBackground = true,
                Name = "Deskbridge UI",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            ready.Wait();
            return context!;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            context?.Post(_ => Application.ExitThread(), null);
            context = null;
        }
    }
}
