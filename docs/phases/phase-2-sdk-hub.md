# Phase 2: Adapter SDK and local hub plan

> Task-level plan (D25). Refined to step level, with an artifact, before Phase 2 starts. No subagents.

**Goal:** The way Deskbridge learns apps: the adapter manifest, the transports, the plugin protocol, UI Automation recipes, the hub for local MCP servers, and Code Mode.

**Weeks:** 4 to 5.

**Spec:** `docs/plan.md` (Adapters, What agents see: Code Mode) and D21, D27, D33.

## Needs before it starts

- Phase 1 gate passed: every tool already runs through `ToolPipeline`.
- Jint 4.18.0 added to `Directory.Packages.props`.

## Tasks

### Task 1: Manifest format

- **Files:** `src/Deskbridge.Sdk/Deskbridge.Sdk.csproj`, `Manifest/AdapterManifest.cs`, `Manifest/ManifestLoader.cs`; `docs/schemas/adapter-manifest.schema.json`
- **Interfaces:** `AdapterManifest(string Id, string Name, string Version, DetectRule Detect, TransportSpec Transport, IReadOnlyList<FunctionSpec> Functions)`; `DetectRule(string[] ProcessNames, string[] ProgIds, string[] InstallPaths, string? VersionRange)`; `TransportSpec(string Kind)` where kind is `com`, `cli`, `plugin`, `hub`, `uia_recipe` (and `websocket`, reserved, D33); `FunctionSpec(string Name, string Description, ToolEffect Effect, JsonElement InputSchema, string[] TestedVersions)`; `ManifestLoader.Load(string json)` returns the manifest or a list of errors.
- **Tests:** a valid sample loads; a function without `effect` is rejected; names must match `^[a-z][a-z0-9_]*$`; `websocket` is rejected with "not supported yet"; the schema file and the C# model agree (the sample validates against both).

### Task 2: Adapter registry and detection

- **Files:** `src/Deskbridge.Sdk/IAdapter.cs`, `AdapterContext.cs`; `src/Deskbridge.Core/Adapters/AdapterRegistry.cs`, `AppDetector.cs`
- **Interfaces:** `interface IAdapter { AdapterManifest Manifest { get; } Task<ToolResult> InvokeAsync(string function, JsonElement args, AdapterContext context, CancellationToken ct); }`; tools are exposed as `<adapter id>_<function>`; adapters load on first use; `AppDetector.IsInstalled(DetectRule)` and `IsRunning(DetectRule)`.
- **Tests:** an adapter's tools appear only when its app is detected; its first call constructs it, later calls reuse it; every adapter call goes through `ToolPipeline` with the manifest's effect.

### Task 3: COM transport

- **Files:** `src/Deskbridge.Sdk/Transports/Com/ComThread.cs`, `MessageFilter.cs`, `ComObjects.cs`
- **Interfaces:** `ComThread` (one STA thread with a message pump per adapter) with `Task<T> RunAsync<T>(Func<T> work, TimeSpan timeout, CancellationToken)`; `MessageFilter` retries `RPC_E_CALL_REJECTED` and `SERVERCALL_RETRYLATER` for up to 10 s; `ComObjects.GetActive(string progId)` through `GetActiveObject` in `oleaut32` (P/Invoke), `ComObjects.Create(string progId)`.
- **Tests:** `Scripting.FileSystemObject` created and called on the STA thread; `GetActive` for a ProgID with no running instance returns null; a call that exceeds its timeout fails with a plain message. (Busy-app retries are tested against WPS in Phase 3.)

### Task 4: CLI transport

- **Files:** `src/Deskbridge.Sdk/Transports/Cli/CliRunner.cs`, `CliResult.cs`
- **Interfaces:** `CliRunner.RunAsync(string exe, IReadOnlyList<string> args, string workingDirectory, TimeSpan timeout, int maxOutputBytes, IReadOnlyDictionary<string,string>? env, CancellationToken ct)` returns `CliResult(int ExitCode, string StdoutTail, string StderrTail, bool TimedOut, bool Truncated)`. No shell; arguments passed as a list; the process tree runs in a job object and is killed on timeout.
- **Tests:** exit code and output captured; a timeout kills the child and its children; output over the cap is cut and marked; arguments with spaces and quotes arrive intact.

### Task 5: Plugin bridge

- **Files:** `src/Deskbridge.Sdk/Transports/Plugin/PluginBridge.cs`, `PluginProtocol.cs`, `PluginTokens.cs`; `docs/plugin-protocol.md`
- **Interfaces:** one pipe `deskbridge-plugin-<SID>` with `CurrentUserOnly`; newline-delimited JSON-RPC 2.0; a plugin opens with `register {adapter_id, token, instance}` (instance: e.g. a VS Code window and its workspace folders); the broker calls `invoke {function, args}` on a registered instance. Tokens: one per adapter install, stored DPAPI-protected in `%LOCALAPPDATA%\Deskbridge\plugins\<adapter>.token`, written when the plugin is installed.
- **Tests:** a fake plugin registers and answers a call; a wrong token is refused; two instances of one plugin are both registered and routed by instance; a plugin that disconnects is removed and its calls fail with "The app's plugin is not running."

### Task 6: UI Automation recipes

- **Files:** `src/Deskbridge.Sdk/Transports/Recipes/Recipe.cs`, `RecipeRunner.cs`; `docs/recipes.md`
- **Interfaces:** a recipe is JSON: `app_version` range, then steps `find` (automation_id, name, control_type, scope), `read`, `invoke`, `set_text`, `expect`. `RecipeRunner.RunAsync(Recipe, JsonElement args, nint window, CancellationToken)` runs on the UI Automation worker through the Phase 1 actions, so re-validation and rung reporting come for free.
- **Tests:** a recipe reads and sets text on the target app; a version outside the range is refused with the tested range in the message; a failed `expect` stops the recipe and names the step.

### Task 7: Local hub

- **Files:** `src/Deskbridge.Hub/Deskbridge.Hub.csproj`, `HubEntry.cs`, `HubServer.cs`, `HubRegistry.cs`; `tests/Deskbridge.TestMcpServer/` (a small MCP server used only in tests)
- **Interfaces:** `HubEntry(string Id, string Prefix, string Command, string[] Args, IReadOnlyDictionary<string,string> Env, IReadOnlyDictionary<string, ToolEffect> EffectOverrides)` stored in `config.json` under `hub`; `HubServer` starts the child with the SDK's `StdioClientTransport` on first use, lists its tools, and re-exposes them as `<prefix>_<tool>`. Effect: an override if set, else `ReadOnlyHint` gives Read, `DestructiveHint` gives Destructive, anything else Change. Stops after 10 idle minutes; restarts once after a crash.
- **Tests:** the test server's tools appear with the prefix; a read-only tool runs without confirmation; a change tool asks; the child stops after the idle time (overridden short) and restarts on the next call; a crashing child gives a plain error.

### Task 8: Code Mode

- **Files:** `src/Deskbridge.Mcp/CodeMode/CodeModeTools.cs`, `JintSandbox.cs`, `TypeScriptDeclarations.cs`
- **Interfaces:** with `mode: code` in the hello, the session offers three tools: `search(query)` returns matching function names and one-line descriptions; `describe(name)` returns a TypeScript declaration built from the tool's input schema and result shape; `execute(code)` runs JavaScript in Jint with `deskbridge.call(name, args)` as the only host function. Limits: 5 s time, 64 MB memory, 100,000 statements, recursion depth 64, no CLR access. Every `deskbridge.call` goes through `ToolPipeline`, so confirmations appear mid-script.
- **Tests:** a 3-step script (list windows, find the target app, read its Notes box) returns the text; `System`, `require`, `fetch` and `importNamespace` are undefined; an infinite loop stops at 5 s; a memory bomb stops at the limit; a Change call inside a script waits for the confirmation and continues after Allow.

### Task 9: Sample adapter from the SDK only

- **Files:** `samples/adapters/target-notes/` (manifest, recipe, a project that references `Deskbridge.Sdk` and nothing else)
- **Tests:** loaded by the registry, its `target_notes_read` and `target_notes_write` work on the target app; it builds with only the SDK reference.

## Gate

- A sample adapter built only from the SDK works.
- A local MCP server runs behind the hub with confirmations.
- Code Mode runs a 3-step script.
- All tests pass; report written.

## Questions for the phase-start artifact

1. Should community recipes and hub entries be loadable from a folder (`%APPDATA%\Deskbridge\adapters\`) in v1, or only bundled ones until after v1?
