using System.ComponentModel;
using Deskbridge.Core;
using Deskbridge.Core.Apps;
using Deskbridge.Core.Clients;
using Deskbridge.Core.Tools;
using Deskbridge.Engines.Os.Files;
using ModelContextProtocol.Server;
using Files = Deskbridge.Engines.Os.Files.FileTools;

namespace Deskbridge.Mcp.Tools;

/// <summary>files_search, files_read, files_write, files_move, files_delete.</summary>
public static class FileToolGroup
{
    public static IEnumerable<McpServerTool> Create(ToolServices services, ClientContext client)
    {
        Task<object> Run(string tool, ToolEffect effect, string action, string? content, Func<object> work, CancellationToken ct) =>
            services.Pipeline.RunAsync(new ToolCall(tool, AppId.Files, effect, action, content), client,
                _ => Task.FromResult(new Executed<object>(work(), Rung.WindowsApi)), ct);

        yield return McpServerTool.Create(
            ([Description("The folder to search in, as a full path.")] string root,
             [Description("A file name pattern such as *.txt or report*. Default: every file.")] string pattern = "*",
             [Description("Most results to return, up to 1000. Default 200.")] int max_results = 200,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => Run("files_search", ToolEffect.Read, $"Search {root} for {pattern}", null,
                    () => new { files = Files.Search(root, pattern, max_results) }, ct)),
            ToolKit.Options("files_search", ToolEffect.Read,
                "Finds files under a folder by name pattern, including subfolders, and returns their paths, sizes and change times. Skips folders Windows will not let it read."));

        yield return McpServerTool.Create(
            ([Description("The file's full path.")] string path,
             [Description("Most bytes to read. Default 1048576 (1 MB).")] int max_bytes = 1_048_576,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => Run("files_read", ToolEffect.Read, $"Read {path}", null, () =>
                {
                    var file = Files.Read(path, max_bytes);
                    return new { path = file.Path, untrusted_text = file.Text, truncated = file.Truncated, size = file.Size, note = ToolKit.UntrustedNote };
                }, ct)),
            ToolKit.Options("files_read", ToolEffect.Read,
                "Reads a text file. Refuses binary files and secret files (.env files, keys, certificates, password databases). The text comes back in 'untrusted_text'."));

        yield return McpServerTool.Create(
            ([Description("The file's full path.")] string path,
             [Description("The text to write, as UTF-8.")] string content,
             [Description("'create' for a new file (refuses an existing one) or 'overwrite' to replace an existing file.")] string mode = "create",
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() =>
                {
                    var writeMode = ParseMode(mode);
                    return Run("files_write", ToolEffect.Change,
                        writeMode == WriteMode.Create ? $"Create {path}" : $"Overwrite {path}", content, () =>
                        {
                            Files.Write(path, content, writeMode);
                            return new { path, written_chars = content.Length };
                        }, ct);
                }),
            ToolKit.Options("files_write", ToolEffect.Change,
                "Writes a text file. Asks the user first, showing the exact content. 'create' never replaces an existing file."));

        yield return McpServerTool.Create(
            ([Description("The file or folder to move or rename, as a full path.")] string from,
             [Description("The new full path.")] string to,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => Run("files_move", ToolEffect.Change, $"Move {from} to {to}", null, () =>
                {
                    Files.Move(from, to);
                    return new { from, to };
                }, ct)),
            ToolKit.Options("files_move", ToolEffect.Change,
                "Moves or renames a file or folder. Asks the user first. Never overwrites: it refuses if something is already at the new path."));

        yield return McpServerTool.Create(
            ([Description("The file or folder to delete, as a full path.")] string path,
             CancellationToken ct = default) =>
                ToolJson.RunAsync(() => Run("files_delete", ToolEffect.Destructive, $"Delete {path} (it goes to the Recycle Bin)", null, () =>
                {
                    Files.Delete(path);
                    return new { path, recycle_bin = true };
                }, ct)),
            ToolKit.Options("files_delete", ToolEffect.Destructive,
                "Sends a file or folder to the Recycle Bin. Always asks the user first, every time."));
    }

    static WriteMode ParseMode(string mode) => mode.ToLowerInvariant() switch
    {
        "create" => WriteMode.Create,
        "overwrite" => WriteMode.Overwrite,
        _ => throw new DeskbridgeException("mode is 'create' or 'overwrite'."),
    };
}
