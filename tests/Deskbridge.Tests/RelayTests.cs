using System.Text;
using Deskbridge.Relay;

namespace Deskbridge.Tests;

public class RelayTests
{
    [Fact]
    public async Task Pump_copies_both_ways_and_ends_with_the_input()
    {
        var input = new MemoryStream("to broker\n"u8.ToArray());
        var output = new MemoryStream();
        var pipe = new LoopbackStream(replyWith: "from broker\n"u8.ToArray());
        await Pump.RunAsync(input, output, pipe, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("to broker\n", Encoding.UTF8.GetString(pipe.Received));
        Assert.Equal("from broker\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public async Task Pump_ends_when_the_broker_side_closes()
    {
        var input = new BlockingStream(); // a client that never closes its stdin
        var output = new MemoryStream();
        var pipe = new LoopbackStream(replyWith: "bye\n"u8.ToArray());
        await Pump.RunAsync(input, output, pipe, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("bye\n", Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public void Parent_process_of_the_test_runner_is_found()
    {
        var parent = ParentProcess.Get(Environment.ProcessId);
        Assert.NotNull(parent);
        Assert.True(parent!.Pid > 0);
        Assert.EndsWith(".exe", parent.Path, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrEmpty(parent.Name));
    }

    [Fact]
    public void Unknown_process_has_no_parent() => Assert.Null(ParentProcess.Get(-1));

    [Fact]
    public void A_shell_between_the_client_and_the_relay_is_skipped()
    {
        // MCP clients often start servers through "cmd /c". The client is whoever started the shell.
        using var shell = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c ping -n 6 127.0.0.1 >nul")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        try
        {
            int? child = null;
            for (var i = 0; i < 50 && child is null; i++)
            {
                Thread.Sleep(100);
                child = ParentProcess.ChildrenOf(shell.Id).FirstOrDefault() is var c && c != 0 ? c : null;
            }

            Assert.NotNull(child);
            var client = ParentProcess.Get(child!.Value);
            Assert.Equal(Environment.ProcessId, client!.Pid);
        }
        finally
        {
            shell.Kill(entireProcessTree: true);
        }
    }

    /// <summary>Records what is written; returns <c>replyWith</c> once on read, then end of stream.</summary>
    sealed class LoopbackStream(byte[] replyWith) : Stream
    {
        readonly MemoryStream received = new();
        bool replied;

        public byte[] Received => received.ToArray();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (replied)
            {
                return 0;
            }

            replied = true;
            replyWith.CopyTo(buffer, offset);
            return replyWith.Length;
        }

        public override void Write(byte[] buffer, int offset, int count) => received.Write(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    /// <summary>A read that only ends when cancelled, like a client's stdin that stays open.</summary>
    sealed class BlockingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
