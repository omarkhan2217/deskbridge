using System.Text;
using Deskbridge.Core.Wire;

namespace Deskbridge.Tests;

public class HelloFrameTests
{
    static readonly ClientHello Sample =
        new(1, HelloModes.Classic, new ClientProcess(4242, @"C:\Apps\Code.exe", "Code"));

    [Fact]
    public async Task Round_trip_keeps_every_field()
    {
        var ms = new MemoryStream();
        HelloFrame.Write(ms, Sample);
        ms.Position = 0;
        Assert.Equal(Sample, await HelloFrame.ReadAsync(ms, CancellationToken.None));
    }

    [Fact]
    public void Wire_format_is_one_snake_case_json_line()
    {
        var ms = new MemoryStream();
        HelloFrame.Write(ms, Sample with { PairingToken = "t1" });
        var line = Encoding.UTF8.GetString(ms.ToArray());
        Assert.EndsWith("\n", line);
        Assert.Equal(1, line.Count(c => c == '\n'));
        Assert.Contains("\"pairing_token\":\"t1\"", line);
        Assert.Contains("\"mode\":\"classic\"", line);
        Assert.Contains("\"pid\":4242", line);
    }

    [Fact]
    public async Task Read_leaves_the_bytes_after_the_newline_in_the_stream()
    {
        var ms = new MemoryStream();
        HelloFrame.Write(ms, Sample);
        ms.Write("{\"jsonrpc\":\"2.0\"}\n"u8);
        ms.Position = 0;
        await HelloFrame.ReadAsync(ms, CancellationToken.None);
        Assert.Equal("{\"jsonrpc\":\"2.0\"}\n", new StreamReader(ms).ReadToEnd());
    }

    [Fact]
    public async Task Frame_without_newline_in_4096_bytes_is_rejected() =>
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            HelloFrame.ReadAsync(new MemoryStream(new byte[5000]), CancellationToken.None));

    [Fact]
    public async Task Stream_ending_before_newline_is_rejected() =>
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            HelloFrame.ReadAsync(new MemoryStream("{\"v\":1"u8.ToArray()), CancellationToken.None));

    [Theory]
    [InlineData("{\"v\":2,\"mode\":\"classic\",\"client\":{\"pid\":1}}\n")]
    [InlineData("{\"v\":1,\"mode\":\"shell\",\"client\":{\"pid\":1}}\n")]
    [InlineData("{\"v\":1,\"mode\":\"classic\"}\n")]
    [InlineData("not json\n")]
    public async Task Invalid_content_is_rejected(string line) =>
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            HelloFrame.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(line)), CancellationToken.None));
}
