namespace Deskbridge.Relay;

/// <summary>
/// Copies the client's stdin to the broker pipe and the pipe to the client's stdout.
/// When the broker side ends, the relay ends. When the client's stdin ends, replies still in flight
/// get a short grace period, then the relay ends.
/// </summary>
internal static class Pump
{
    static readonly TimeSpan DrainGrace = TimeSpan.FromMilliseconds(500);

    public static async Task RunAsync(Stream input, Stream output, Stream pipe, CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var up = CopyAsync(input, pipe, stop.Token);
        var down = CopyAsync(pipe, output, stop.Token);

        var first = await Task.WhenAny(up, down).ConfigureAwait(false);
        if (first == up)
        {
            await Task.WhenAny(down, Task.Delay(DrainGrace, ct)).ConfigureAwait(false);
        }

        await stop.CancelAsync().ConfigureAwait(false);
        await Settle(up).ConfigureAwait(false);
        await Settle(down).ConfigureAwait(false);
    }

    static async Task CopyAsync(Stream from, Stream to, CancellationToken ct)
    {
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await from.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await to.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            await to.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    static async Task Settle(Task copy)
    {
        try
        {
            await copy.ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // One side is gone; that is how a relay ends.
        }
    }
}
