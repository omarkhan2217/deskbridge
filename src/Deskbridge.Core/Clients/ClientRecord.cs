namespace Deskbridge.Core.Clients;

/// <summary>An MCP client the user approved: matched by its exe path and its signer.</summary>
public sealed record ClientRecord(string Path, string? Publisher, string Name, DateTimeOffset ApprovedAt);
