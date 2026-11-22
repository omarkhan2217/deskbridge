using System.Text.Json;
using Deskbridge.Core;
using ModelContextProtocol.Protocol;

namespace Deskbridge.Mcp;

/// <summary>How tool results are written: snake_case JSON in one text block, or a plain error.</summary>
public static class ToolJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static CallToolResult Ok(object value) => new()
    {
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(value, value.GetType(), Options) }],
    };

    public static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };

    /// <summary>Runs a tool body. A DeskbridgeException becomes a tool error with its message, word for word.</summary>
    public static async Task<CallToolResult> RunAsync(Func<Task<object>> body)
    {
        try
        {
            return Ok(await body().ConfigureAwait(false));
        }
        catch (DeskbridgeException ex)
        {
            return Error(ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Anything else is a Deskbridge bug or an app misbehaving: say what happened instead of
            // the SDK's generic message, so the agent and the user can act on it.
            return Error($"Deskbridge hit an unexpected problem ({ex.GetType().Name}): {ex.Message}");
        }
    }
}
