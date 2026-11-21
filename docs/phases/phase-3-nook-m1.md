# Phase 3: Nook milestone 1 plan

> Task-level plan (D25). Refined to step level, with an artifact, before Phase 3 starts. No subagents.

**Goal:** The local Nook tasks (D9): VS Code, spreadsheets, and the Claude Code build loop, plus pairing so Nook shows Deskbridge's confirmations on its own cards.

**Weeks:** 5 to 6.

**Spec:** `docs/plan.md` (v1 apps, The Claude Code build loop, Using it inside Nook, Confirmations) and D9, D10, D12, D17, D18, D22, D31.

## Needs before it starts

- Phase 2 gate passed (COM, CLI and plugin transports exist).
- Node 24 and npm (installed), `@vscode/vsce` for packaging, VS Code 1.147 (installed), WPS Office with `KET.Application` (installed), Claude Code CLI (2.1.303 installed).
- The Nook-side work (Task 9) scheduled by the owner. Deskbridge's work here never changes Nook.

## Tasks

### Task 1: Long-running runs

- **Files:** `src/Deskbridge.Core/Runs/RunManager.cs`, `RunStatus.cs`; `src/Deskbridge.Mcp/Tools/RunTools.cs`
- **Interfaces:** `RunManager.Start(string kind, Func<IProgress<string>, CancellationToken, Task<ToolResult>> work)` returns a `run_id`; tools `run_wait(run_id, timeout_s ≤ 120)` returning `{status: running|done|failed|cancelled, progress, result}` and `run_cancel(run_id)` (D31). Runs belong to the client that started them.
- **Tests:** `run_wait` returns `running` with progress before the end and the result after; it never waits past 120 s; another client cannot read or cancel the run; cancel stops the work.

### Task 2: Pairing and bound approvals

- **Files:** `src/Deskbridge.Core/Clients/PairingService.cs`, `src/Deskbridge.Core/Policy/ApprovalBinder.cs`, `src/Deskbridge.Mcp/ClientConfirmations.cs`; settings window "Pair a client" button
- **Interfaces:** the owner clicks "Pair a client" and Deskbridge shows a 6-digit code valid for 5 minutes. The client starts the relay once with `DESKBRIDGE_PAIRING_CODE`; Deskbridge answers with a pairing token the client stores and passes on later starts as `DESKBRIDGE_PAIRING_TOKEN` (the relay puts it in the hello). `ApprovalBinder.Bind(ConfirmationRequest, ClientContext, WindowIdentity?)` gives a SHA-256 over canonical JSON; `Redeem(hash)` succeeds once within 2 minutes. For a paired client, a confirmation is sent as an "input required" result (`InputRequiredException` with an elicitation form `{allow: boolean}` and the hash in its request state) on the 2026-07-28 spec, or `ElicitAsync` on an older spec with elicitation; otherwise Deskbridge's own window is used.
- **Tests:** a paired client receives "input required" and its retry with `allow: true` runs the action; a replayed approval is refused; changing any argument makes the approval invalid; an unpaired client gets the Deskbridge window; a wrong or expired code pairs nothing.

### Task 3: VS Code extension

- **Files:** `extensions/vscode/` (`package.json`, `src/extension.ts`, `src/bridge.ts`, `src/handlers.ts`, tests under `src/test/`)
- **Interfaces:** activates on `onStartupFinished`; connects to the plugin pipe and registers `{adapter_id: "vscode", instance: {window_id, workspace_folders}}`. Handlers: `open_file(path, line, column)` with `showTextDocument(uri, {preserveFocus: true, preview: false, selection})`; `list_problems(path?)` from `languages.getDiagnostics` (severity, range, message, source); `active_editor()` with file, selection text and visible range. Packaged with `vsce package`; Deskbridge installs it with `code --install-extension <vsix>` after asking.
- **Tests:** extension unit tests with `@vscode/test-electron`: each handler's output shape; reconnects when the broker restarts.

### Task 4: VS Code adapter

- **Files:** `adapters/vscode/` (manifest, `VsCodeAdapter.cs`)
- **Interfaces (tools):** `vscode_open_file(path, line, column?)`, `vscode_list_problems(path?)`, `vscode_active_editor()`, all Read (they change only what VS Code shows). A path is routed to the VS Code window whose workspace contains it; if none does, the error says which folders are open.
- **Tests (end to end):** with real VS Code open on a test folder, `vscode_open_file` puts the cursor on the line, inside the hands-off probe (the VS Code window must not come to the front); problems from a file with a known error are listed.

### Task 5: Spreadsheet functions for WPS and Office

- **Files:** `adapters/office/` (manifest, `OfficeApps.cs`, `SheetFunctions.cs`, `RunningDocuments.cs`)
- **Interfaces (tools):** `sheet_list_open()` (workbooks open in Excel or WPS Spreadsheets, found through the Running Object Table), `sheet_list_sheets(file)`, `sheet_read_range(file, sheet, range)` (up to 10,000 cells, values as a 2D array), `sheet_write_range(file, sheet, range, values)` Change (the confirmation shows old and new values side by side), `sheet_save(file)` Change. A file already open is changed in the user's own window, without focus; a closed file is opened in a hidden instance (`Visible = false`, `DisplayAlerts = false`) that closes after 2 idle minutes. ProgIDs: `Excel.Application` and `KET.Application`.
- **Tests (end to end, WPS):** read and write a temp workbook while closed and while open in the user's WPS window, each inside the hands-off probe; a busy WPS (a modal dialog open) gives a plain "WPS is busy" error after the retry window.

### Task 6: Claude Code adapter

- **Files:** `adapters/claude-code/` (manifest, `ClaudeCodeAdapter.cs`, `StreamJsonParser.cs`); `tests/fakes/fake-claude/` (a test exe that replays recorded stream-json)
- **Interfaces (tools):** `claude_code_start(project, prompt)` and `claude_code_continue(session_id, prompt)`, both Change, both return a `run_id` (Task 1). The run executes `claude -p <prompt> --output-format stream-json --verbose` (plus `--resume <session_id>` to continue) with the project as the working folder, never with the flag that skips permissions. The final result holds the session ID, the summary text (`untrusted_text`), and changed files (from edit and write events, checked against `git status --porcelain` when the folder is a git repo). Before first use, the adapter checks the CLI version and that these flags exist in `claude --help`.
- **Tests:** with `fake-claude`: session ID and changed files parsed; a non-zero exit gives a failed run with the tail of stderr. End to end with the real CLI on `samples/loop-sandbox`: a start then a continue in the same session.

### Task 7: Project checks

- **Files:** `adapters/claude-code/ProjectChecks.cs`, `docs/project-checks.md`
- **Interfaces (tools):** `project_check_list(project)` and `project_check_run(project, check)` (Change, returns a `run_id`). Checks come from `.deskbridge/checks.json` (`{"checks": {"test": {"command": ["npm", "test"], "timeout_s": 600}}}`) or are suggested from `package.json` scripts, a `*.csproj` (`dotnet test`) or `pyproject.toml` (`pytest`). The first run of each check in each project asks, showing the exact command. Runs through `CliRunner`, no shell; the result has the exit code and the last 200 lines.
- **Tests:** suggestions for each project type; a check not on the list is refused; the first run asks and the second does not; a timeout kills the process tree.

### Task 8: Loop sandbox

- **Files:** `samples/loop-sandbox/` (a tiny TypeScript library with one failing test and a `.deskbridge/checks.json`)
- **Use:** the gate's build loop runs here so it never touches a real project.

### Task 9: Nook-side work (done in Nook, not here)

Listed so the dependency is visible. Not built from this repo.

1. A Deskbridge entry in Nook's tool catalogue with Nook's risk and consequence word for each tool.
2. "Input required" and elicitation support in Nook's MCP client, shown on Nook's existing approval card.
3. Pairing: store the pairing token and pass it as `DESKBRIDGE_PAIRING_TOKEN`.
4. The build loop in Nook's planner: look, prompt, test, prompt again, stop at green checks, 5 rounds, two rounds without change, or Stop.

### Task 10: Gate scenario

- **Files:** `tests/Deskbridge.E2E/Gates/Phase3GateTests.cs` (a scripted client doing the three tasks, as a regression test), `docs/phases/phase-3-report.md`

## Gate

- Nook opens a file at a line in VS Code and lists its problems.
- Nook updates cells in a WPS sheet.
- Nook runs a Claude Code build loop of at least 2 rounds on `samples/loop-sandbox`.
- All of it with one approval card per action in Nook, and no cursor or focus changes.
- All tests pass; report written.
