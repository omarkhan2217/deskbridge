using System.Text.Json;

namespace Deskbridge.Core.Wire;

/// <summary>
/// Writes and reads the hello: one line of UTF-8 JSON ending in '\n'.
/// Reading takes one byte at a time so the MCP bytes after the line stay in the stream.
/// </summary>
public static class HelloFrame
{
    public const int MaxBytes = 4096;
    public const int CurrentVersion = 1;

    public static void Write(Stream stream, ClientHello hello)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(hello, WireJsonContext.Default.ClientHello);
        if (json.Length >= MaxBytes)
        {
            throw new ArgumentException($"The hello is {json.Length} bytes; the limit is {MaxBytes - 1}.", nameof(hello));
        }

        stream.Write(json);
        stream.WriteByte((byte)'\n');
        stream.Flush();
    }

    public static async Task<ClientHello> ReadAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[MaxBytes];
        var length = 0;
        while (true)
        {
            if (length == MaxBytes)
            {
                throw new InvalidDataException($"The hello has no line end within {MaxBytes} bytes.");
            }

            var read = await stream.ReadAsync(buffer.AsMemory(length, 1), ct).ConfigureAwait(false);
            if (read == 0)
            {
                throw new InvalidDataException("The connection closed before the hello ended.");
            }

            if (buffer[length] == (byte)'\n')
            {
                break;
            }

            length++;
        }

        return Parse(buffer.AsSpan(0, length));
    }

    static ClientHello Parse(ReadOnlySpan<byte> json)
    {
        ClientHello? hello;
        try
        {
            hello = JsonSerializer.Deserialize(json, WireJsonContext.Default.ClientHello);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The hello is not valid JSON of the expected shape.", ex);
        }

        if (hello is null || hello.Client is null)
        {
            throw new InvalidDataException("The hello does not name its client.");
        }

        if (hello.V != CurrentVersion)
        {
            throw new InvalidDataException($"Hello version {hello.V} is not supported; this broker speaks version {CurrentVersion}.");
        }

        if (!HelloModes.IsKnown(hello.Mode))
        {
            throw new InvalidDataException($"Unknown mode '{hello.Mode}'. Use '{HelloModes.Classic}' or '{HelloModes.Code}'.");
        }

        return hello;
    }
}
