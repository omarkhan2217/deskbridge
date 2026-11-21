using Deskbridge.Engines.Uia;

namespace Deskbridge.Mcp;

/// <summary>The engines the tools use. One set per broker, shared by every session.</summary>
public sealed record ToolServices(UiaReader Reader);
