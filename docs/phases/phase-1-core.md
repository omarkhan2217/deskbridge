# Phase 1: Core plan

> Task-level plan (D25). Before Phase 1 starts, Claude refines each task to step level, publishes an artifact, and the owner says go. No subagents.

**Goal:** Everything that makes Deskbridge safe and useful on its own: all 25 core tools, per-app permissions, client approval, the no-focus confirmation window, the audit log, the tray and the settings window.

**Weeks:** 1 to 3.

**Spec:** `docs/plan.md` (What agents see, The no-cursor rule, Safety and permissions, Confirmations) and D12, D14, D15, D16.

## Needs before it starts

- Phase 0 gate passed; its report read for measured memory and timing.
- Claude Desktop installed, if the owner wants the gate run in it (Claude Code works otherwise).
- Answers to the questions at the end of this file.

## Tasks

### Task 1: Tool pipeline and effects

- **Files:** `src/Deskbridge.Core/Tools/ToolEffect.cs`, `ToolCall.cs`, `ToolResult.cs`, `Rung.cs`, `ToolPipeline.cs`
- **Interfaces:** `enum ToolEffect { Read, Change, Destructive }`; `enum Rung { AppInterface, WindowsApi, UiAutomation, WindowMessage, Foreground }`; `record ToolCall(string Tool, JsonElement Args, AppId App, ToolEffect Effect, string Summary)`; `class ToolPipeline { Task<ToolResult> InvokeAsync(ToolCall call, ClientContext client, Func<CancellationToken, Task<ToolResult>> execute, CancellationToken ct); }`. Every tool from now on runs through this: policy, then confirmation, then execute, then audit.
- **Tests:** a denied call never runs `execute`; an allowed call writes one audit entry; a confirmation answered Deny never runs `execute` and is audited as `denied_by_user`.

### Task 2: App identity

- **Files:** `src/Deskbridge.Core/Apps/AppId.cs`, `src/Deskbridge.Engines.Os/AppResolver.cs`
- **Interfaces:** `record AppId(string Key, string DisplayName)` where `Key` is the lowercase full exe path, or a pseudo-app `system.files`, `system.clipboard`, `system.media`, `system.settings`, `system.windows`; `AppResolver.FromWindow(nint)`, `FromProcess(int)`, `FromPath(string)`.
- **Tests:** the same app from window and from process gives equal `AppId`; a packaged app (Calculator) resolves to its package exe; files resolve to `system.files`.

### Task 3: Permission store

- **Files:** `src/Deskbridge.Core/Policy/PermissionLevel.cs`, `PermissionStore.cs`, `DeskbridgeConfig.cs`
- **Interfaces:** `enum PermissionLevel { Off, Read, Act }`; `PermissionStore(string configPath)` with `Get(AppId)`, `Set(AppId, PermissionLevel)`, `IsPaused`, `SetPaused(bool)`. Config at `%APPDATA%\Deskbridge\config.json`: `{"version":1,"apps":{"<key>":"act"},"clients":{},"paused":false,"content_logging":false,"pinned":false,"start_at_sign_in":false}`. Re-read when the file's write time changes (checked on each call; no watcher thread).
- **Tests:** new app is Read; password managers (1Password, Bitwarden, KeePass, KeePassXC, Dashlane, LastPass, Enpass) are Off; a set value survives a reload; a malformed file falls back to Read for everything and never to Act.

### Task 4: Policy engine

- **Files:** `src/Deskbridge.Core/Policy/PolicyEngine.cs`, `PolicyDecision.cs`, `NeverAvailable.cs`
- **Interfaces:** `PolicyDecision` is one of `Allow`, `Deny(string reason)`, `Confirm(ConfirmationRequest request)`; `PolicyEngine.Decide(ToolCall, ClientContext)`. Order: paused gives Deny; client not approved gives Confirm (client approval); never-available gives Deny; app Off gives Deny; Read effect needs Read or Act; Change needs Act and gives Confirm unless a remembered allow covers it; Destructive needs Act and always gives Confirm.
- **Never available (`NeverAvailable.cs`):** any window of an elevated process (detected from the target's token elevation; Windows would block it anyway, and the error says why); the Windows Security app and the `ms-settings:windowsdefender` and UAC pages; authenticator apps (Microsoft Authenticator, Authy, and others on a list in config); any element whose name or AutomationId contains "captcha"; setting text in any password field.
- **Tests:** one test per rule above, plus: a remembered allow never covers a Destructive call; pausing denies a call that was allowed a moment before; an elevated window is refused with the plain reason.

### Task 5: Confirmation window

- **Files:** `src/Deskbridge.Ui/Deskbridge.Ui.csproj` (WinForms), `UiThread.cs`, `ConfirmationForm.cs`, `ConfirmationPresenter.cs`; `src/Deskbridge.Core/Policy/ConfirmationRequest.cs`
- **Interfaces:** `record ConfirmationRequest(string ClientName, AppId App, string Action, string Content, ToolEffect Effect, bool CanRemember)`; `enum ConfirmationAnswer { Allow, AllowAndRemember, Deny, TimedOut }`; `interface IConfirmationPresenter { Task<ConfirmationAnswer> AskAsync(ConfirmationRequest, CancellationToken); }`. One STA UI thread for all Deskbridge windows, started on first use. The form uses `WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`, `ShowWithoutActivation`, sits above the tray, shows the exact content in a read-only monospace box, and times out after 2 minutes.
- **Tests (end to end):** showing the window does not change the foreground window or focus (hands-off probe); pressing Allow through UI Automation `Invoke` (no mouse) returns Allow; no answer returns TimedOut (timeout overridden to 300 ms); two requests at once queue one after another.

### Task 6: Client approval

- **Files:** `src/Deskbridge.Core/Clients/ClientRegistry.cs`, `ClientRecord.cs`; `src/Deskbridge.Engines.Os/Publisher.cs`
- **Interfaces:** `ClientRecord(string Path, string? Publisher, string Name, DateTimeOffset ApprovedAt)`; `Publisher.Get(string exePath)` via `WinVerifyTrust` and the signer certificate's subject; `ClientRegistry.IsApproved(ClientProcess)`, `Approve(ClientProcess)`, `Remove(string path)`.
- **Tests:** an unknown client gets the approval confirmation on its first tool call, not on connect (so `tools/list` works before approval); an approved client is not asked again; an exe whose publisher changed is asked again.

### Task 7: Audit log

- **Files:** `src/Deskbridge.Core/Audit/AuditLog.cs`, `AuditEntry.cs`
- **Interfaces:** `AuditLog(string directory, Func<bool> contentLoggingOn)`; `Append(AuditEntry)`. One JSON line per call in `%LOCALAPPDATA%\Deskbridge\logs\audit-YYYY-MM-DD.jsonl`: `time` (UTC, ISO 8601), `client`, `app`, `tool`, `effect`, `decision`, `rung`, `result`, `duration_ms`, and `content` only when content logging is on.
- **Tests:** fields and names exact; no `content` by default; a call after midnight writes to the next day's file; a full disk or locked file never fails the tool call.

### Task 8: Windows engine tools

- **Files:** `src/Deskbridge.Engines.Os/` `Files/*`, `Apps/*`, `System/*`; `src/Deskbridge.Mcp/Tools/OsTools.cs`. `Deskbridge.Engines.Os` moves to `net10.0-windows10.0.19041.0` for the media session API.
- **Interfaces (tools):** `files_search(root, pattern, max_results=200)`, `files_read(path, max_bytes=1048576)`, `files_write(path, content, mode: create|overwrite)` Change, `files_move(from, to)` Change, `files_delete(path)` Destructive (Recycle Bin through `IFileOperation`); `apps_list_installed`, `apps_list_running`, `apps_launch(app)` Change (launched with `SW_SHOWNOACTIVATE`), `windows_list`, `windows_describe(window)`; `clipboard_read` (returns `untrusted_text`), `clipboard_write(text)` Change, `media_control(action: play|pause|toggle|next|previous)` Change, `media_now_playing`, `volume_set(percent)` Change, `notify_show(title, text)`, `settings_open(page)` Change (an `ms-settings:` page, opened without activation).
- **Secret files:** `files_read` refuses `.env`, `.env.*`, `*.pem`, `*.key`, `*.pfx`, `*.p12`, `id_rsa*`, `id_ed25519*`, `*.kdbx`, and names containing `credential` or `secret`, with the reason.
- **Tests:** each tool against a temp folder or a target app, inside the hands-off probe; delete lands in the Recycle Bin; a secret file is refused; `apps_launch` of Notepad does not take the foreground (if an app does take it, the result says so and the test records which apps).

### Task 9: UI Automation tools

- **Files:** `src/Deskbridge.Engines.Uia/` `ElementRef.cs`, `ElementCache.cs`, `UiaActions.cs`, `UiaTree.cs`, `SecretWindows.cs`; `src/Deskbridge.Engines.Os/WindowMessages.cs`; `src/Deskbridge.Mcp/Tools/UiTools.cs`
- **Interfaces (tools):** `ui_tree(window, max_depth=4, max_nodes=300)`, `ui_find(window, name?, automation_id?, control_type?)` returning `element_id`s, `ui_read(window, control?)`, `ui_press(window, element)`, `ui_set_text(window, element, text)`, `ui_select(window, element)`, `ui_toggle(window, element, on?)`, `ui_scroll(window, element, direction, amount)`. `element` is an `element_id` from `ui_find` or a name or AutomationId.
- **Behaviour:** re-read the element (name, control type, enabled, window) right before acting and refuse if it changed; patterns first (Invoke, Toggle, ExpandCollapse, SelectionItem, Value, Scroll, LegacyIAccessible default action), then window messages (`BM_CLICK` for Win32 buttons, `WM_SETTEXT` for Win32 edits); report the rung; never set text in a password field; refuse windows whose title shows a secret file; refuse Chromium windows unless an adapter allows them.
- **Tests:** each tool on the target app inside the hands-off probe; the re-validation catches a control renamed between find and press; a Win32 button with no Invoke falls to `BM_CLICK` and reports rung `window_message`.

### Task 10: Tray, settings, pause and hotkey

- **Files:** `src/Deskbridge.Ui/TrayIcon.cs`, `SettingsForm.cs`, `Hotkey.cs`; `src/Deskbridge.Broker/` wiring
- **Behaviour:** tray menu: Pause all or Resume, Settings, Open log folder, Quit. Global hotkey Ctrl+Alt+Shift+P (`RegisterHotKey`) toggles pause. Settings: apps with Off, Read or Act; approved clients with Remove; start at sign-in (`HKCU\...\Run`); keep running (pinned); content logging. The broker's idle exit respects "keep running".
- **Tests:** pause from the tray denies the next call; the hotkey toggles pause; changing an app to Off in settings denies its next call; start at sign-in writes and removes the Run value.

### Task 11: Gate scenario and measurements

- **Files:** `tests/Deskbridge.E2E/Gates/Phase1GateTests.cs`, `docs/phases/phase-1-report.md`
- **Tests:** through the relay with a scripted MCP client: rename a temp file, read a Notepad document, toggle a harmless setting in the Settings app and put it back, each inside the hands-off probe with the confirmation answered through UI Automation. Re-run `measure-idle.ps1` with the tray loaded.

## Gate

- An agent in Claude Desktop (or Claude Code) renames a file, reads a Notepad document and sets a value in Settings with zero cursor or focus changes.
- Idle broker CPU under 0.5% of one core and memory under 80 MB, with the tray icon loaded.
- All tests pass; report written.

## Questions for the phase-start artifact

1. **Remembering changes:** after "Allow and remember", should Deskbridge stop asking about non-destructive changes in that app for that client for 30 minutes, for the rest of the day, or until the user revokes it? (Destructive actions ask every time regardless.)
2. **Which setting** the gate should toggle (it must be harmless and reversible).
