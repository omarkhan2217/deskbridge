namespace Deskbridge.Broker;

/// <summary>One broker per Windows user: the first one to create the named mutex runs, any other exits.</summary>
public static class SingleInstance
{
    /// <returns>A handle to keep for the broker's lifetime, or null if another broker already runs.</returns>
    public static IDisposable? TryAcquire(string mutexName)
    {
        // The mutex is never owned, only held open: its existence is the lock, so no thread affinity applies.
        var mutex = new Mutex(initiallyOwned: false, mutexName, out var createdNew);
        if (createdNew)
        {
            return mutex;
        }

        mutex.Dispose();
        return null;
    }
}
