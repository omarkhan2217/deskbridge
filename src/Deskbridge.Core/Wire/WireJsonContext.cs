using System.Text.Json.Serialization;

namespace Deskbridge.Core.Wire;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    RespectNullableAnnotations = true,
    RespectRequiredConstructorParameters = true)]
[JsonSerializable(typeof(ClientHello))]
internal partial class WireJsonContext : JsonSerializerContext;
