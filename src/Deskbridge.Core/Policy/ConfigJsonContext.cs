using System.Text.Json.Serialization;

namespace Deskbridge.Core.Policy;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    WriteIndented = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(DeskbridgeConfig))]
internal partial class ConfigJsonContext : JsonSerializerContext;
