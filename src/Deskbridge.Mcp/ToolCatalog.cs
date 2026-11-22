using Deskbridge.Core.Clients;
using Deskbridge.Mcp.Tools;
using ModelContextProtocol.Server;

namespace Deskbridge.Mcp;

/// <summary>The classic tools, gathered from each tool group. Every tool runs through the pipeline.</summary>
public static class ToolCatalog
{
    public static IReadOnlyList<McpServerTool> Create(ToolServices services, ClientContext client) =>
        WindowTools.Create(services, client)
            .Concat(AppToolGroup.Create(services, client))
            .Concat(FileToolGroup.Create(services, client))
            .Concat(SystemToolGroup.Create(services, client))
            .Concat(UiToolGroup.Create(services, client))
            .ToList();
}
