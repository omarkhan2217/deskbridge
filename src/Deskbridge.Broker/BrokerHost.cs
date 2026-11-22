using System.Collections.Concurrent;
using System.IO.Pipes;
using Deskbridge.Core.Wire;
using Deskbridge.Mcp;
using Microsoft.Extensions.Logging;

namespace Deskbridge.Broker;

/// <summary>
/// Accepts relay connections on the user's pipe and runs one MCP session per connection.
/// No polling: it waits on the pipe, and its only timer is the idle-exit timer, which runs
/// only while no client is connected.
/// </summary>
public sealed class BrokerHost(BrokerOptions options, ToolServices services, ILoggerFactory? loggerFactory = null, Func<bool>? keepRunning = null) : IAsyncDisposable
{
    readonly ConcurrentDictionary<int, Task> sessions = new();
    readonly TaskCompletionSource idleExpired = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly object gate = new();
    CancellationTokenSource? idleTimer;
    int active;
    int nextSessionId;

    public int ActiveSessions
    {
        get
        {
            lock (gate)
            {
                return active;
            }
        }
    }

    /// <summary>Runs until <paramref name="ct"/> is cancelled or the broker has been idle for IdleExit.</summary>
    public async Task RunAsync(CancellationToken ct)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (gate)
        {
            StartIdleTimer();
        }

        var accept = AcceptAsync(stop.Token);
        await Task.WhenAny(accept, idleExpired.Task, Task.Delay(Timeout.Infinite, ct)).ConfigureAwait(false);
        await stop.CancelAsync().ConfigureAwait(false);
        await Task.WhenAll(sessions.Values.Append(accept)).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }

    async Task AcceptAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                options.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await pipe.DisposeAsync().ConfigureAwait(false);
                return;
            }

            OnConnected();
            var id = Interlocked.Increment(ref nextSessionId);
            sessions[id] = SessionAsync(id, pipe, ct);
        }
    }

    async Task SessionAsync(int id, NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            await using (pipe.ConfigureAwait(false))
            {
                ClientHello hello;
                using (var helloLimit = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    helloLimit.CancelAfter(options.HelloTimeout);
                    hello = await HelloFrame.ReadAsync(pipe, helloLimit.Token).ConfigureAwait(false);
                }

                var server = McpSessionFactory.Create(pipe, hello, services, loggerFactory);
                await using (server.ConfigureAwait(false))
                {
                    await server.RunAsync(ct).ConfigureAwait(false);
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or OperationCanceledException or IOException or ObjectDisposedException)
        {
            // A bad hello, a vanished client or shutdown ends this session only.
        }
        finally
        {
            sessions.TryRemove(id, out _);
            OnDisconnected();
        }
    }

    void OnConnected()
    {
        lock (gate)
        {
            active++;
            idleTimer?.Cancel();
            idleTimer?.Dispose();
            idleTimer = null;
        }
    }

    void OnDisconnected()
    {
        lock (gate)
        {
            active--;
            if (active == 0)
            {
                StartIdleTimer();
            }
        }
    }

    void StartIdleTimer()
    {
        idleTimer = new CancellationTokenSource();
        Task.Delay(options.IdleExit, idleTimer.Token).ContinueWith(
            t =>
            {
                if (t.IsCanceled)
                {
                    return;
                }

                if (keepRunning?.Invoke() == true)
                {
                    lock (gate)
                    {
                        if (active == 0)
                        {
                            StartIdleTimer(); // "keep running" is on: check again after another idle period
                        }
                    }

                    return;
                }

                idleExpired.TrySetResult();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    public ValueTask DisposeAsync()
    {
        lock (gate)
        {
            idleTimer?.Cancel();
            idleTimer?.Dispose();
            idleTimer = null;
        }

        return ValueTask.CompletedTask;
    }
}
