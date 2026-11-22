using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Deskbridge.Core.Audit;

/// <summary>
/// Writes one JSON line per tool call to audit-YYYY-MM-DD.jsonl (local date). Content is written only
/// while the user has content logging on. A failed write is counted and never fails the call.
/// </summary>
public sealed class AuditLog(string directory, Func<bool> contentLoggingOn, TimeProvider time) : IAuditLog
{
    readonly object gate = new();
    int failedWrites;

    public string Directory => directory;

    public int FailedWrites => Volatile.Read(ref failedWrites);

    public void Append(AuditEntry entry)
    {
        var line = JsonSerializer.Serialize(
            contentLoggingOn() ? entry : entry with { Content = null },
            AuditJsonContext.Default.AuditEntry);
        var file = Path.Combine(directory, $"audit-{time.GetLocalNow():yyyy-MM-dd}.jsonl");
        lock (gate)
        {
            try
            {
                System.IO.Directory.CreateDirectory(directory);
                using var stream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                stream.Write(Encoding.UTF8.GetBytes(line + "\n"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failedWrites++;
            }
        }
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AuditEntry))]
internal partial class AuditJsonContext : JsonSerializerContext;
