# Phase 1: Core implementation plan

> **For the builder:** use `superpowers:executing-plans` and do every task in the main session. No subagents (`CLAUDE.md` rule 1). Steps use checkboxes. No commits until the owner reviews at the gate (D26). This file was refined from task level to step level on 2026-11-22 (D25).

**Goal:** Everything that makes Deskbridge safe and useful on its own: 25 core tools, per-app permissions, client approval, the no-focus confirmation window, remembered grants, the audit log, the tray, the settings window, and a faster cold start.

**Architecture:** Every tool call becomes a `ToolCall` (tool, app, effect, summary, content) and runs through one `ToolPipeline`: the policy decides (allow, deny, or confirm), Deskbridge's own window asks when needed, the engine acts, the audit log records. Engines stay free of policy. The small windows run on one STA UI thread inside the broker, started on first use.

**Tech stack:** as Phase 0, plus WinForms (`Deskbridge.Ui`), the Windows Runtime media session API (`Engines.Os` moves to `net10.0-windows10.0.19041.0`), Shell and Core Audio COM.

**Spec:** `docs/plan.md` (What agents see, The no-cursor rule, Safety and permissions, Confirmations, Resource budget) and decisions D12, D14 to D16, D36 to D40.

## Global constraints

- Everything from Phase 0's global constraints still holds (no input injection, no `SetForegroundWindow`, no network, `untrusted_text`, Chromium guard, budgets).
- Tool names: `^[a-z][a-z0-9_]*$`. Every tool states its effect: `Read`, `Change` or `Destructive`.
- Every tool call goes through `ToolPipeline`. No engine call bypasses it.
- `Deskbridge.Core` stays Native AOT compatible: JSON through source-generated contexts only.
- The confirmation window never takes focus: `WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_TOOLWINDOW`, `ShowWithoutActivation`. No answer in 2 minutes is a Deny.
- Remembered grants (D36) cover only `Change` calls, for one client in one app, until revoked. `Destructive` always asks.
- Tests never touch the user's real config, Run key or logs: they set `DESKBRIDGE_CONFIG_DIR` (D40) and use a test-only Run value name.
- Config: `%APPDATA%\Deskbridge\config.json`. Logs: `%LOCALAPPDATA%\Deskbridge\logs\audit-YYYY-MM-DD.jsonl`. With `DESKBRIDGE_CONFIG_DIR` set, both live under that folder.
- Copy in the UI and in errors follows the writing rules in `CLAUDE.md`.

## Review focus

1. **A client changes its exe (update, different publisher):** it is asked about again. Pinned in Task 6 (`Changed_publisher_is_asked_again`).
2. **The config file is edited by hand into broken JSON while the broker runs:** nothing becomes Act; reads keep working. Pinned in Task 3 (`Malformed_config_never_grants_act`).
3. **Two clients ask for confirmation at the same moment:** the windows queue, each answer goes to the right caller. Pinned in Task 5 (`Two_requests_queue_and_each_gets_its_answer`).
4. **A control changes between `ui_find` and `ui_press`:** the press is refused. Pinned in Task 9 (`Press_refuses_a_control_that_changed`).
5. **A button whose name means delete or send:** pressing it always asks, even with a remembered grant. Pinned in Task 4 (`Destructive_word_on_a_control_makes_the_call_destructive`).

## How each task runs

Unless a task says otherwise: (1) write the tests listed, (2) run them and see them fail for the stated reason, (3) implement the listed signatures, (4) run them and see them pass, (5) run the whole unit suite, and the end-to-end suite when the task touched it. Close the task in the ledger with `task-done`. Unit tests: `dotnet test --project tests/Deskbridge.Tests`. End-to-end: `dotnet test --project tests/Deskbridge.E2E`.

## File structure (new or changed)

```text
README.md, LICENSE, docs/code-signing-policy.md          Task 0
src/Deskbridge.Core/
  Names.cs                                               + ConfigDirVariable, RunValueName
  Tools/ToolEffect.cs, Rung.cs, ToolCall.cs, Executed.cs, ToolPipeline.cs, DestructiveWords.cs
  Clients/ClientContext.cs, ClientRecord.cs, ClientRegistry.cs
  Apps/AppId.cs
  Policy/PermissionLevel.cs, DeskbridgeConfig.cs, ConfigStore.cs, ConfigJsonContext.cs,
         PolicyEngine.cs, PolicyDecision.cs, ConfirmationRequest.cs, IConfirmationPresenter.cs,
         NeverAvailable.cs, PolicyErrors.cs
  Audit/AuditEntry.cs, AuditLog.cs
src/Deskbridge.Engines.Os/
  AppResolver.cs, ProcessFacts.cs (path, elevation), Publisher.cs
  Files/FileTools.cs, SecretFiles.cs
  Apps/AppsFolder.cs, Launcher.cs, StaThread.cs
  System/ClipboardText.cs, MediaSession.cs, Volume.cs, SettingsPages.cs
  WindowDescriber.cs, WindowMessages.cs
src/Deskbridge.Engines.Uia/
  ElementRef.cs, ElementCache.cs, UiaTree.cs, UiaActions.cs, SecretWindows.cs, StoreApps.cs, Errors.cs (+ new errors)
src/Deskbridge.Ui/                                       WinForms
  UiThread.cs, ConfirmationForm.cs, ConfirmationPresenter.cs, TrayIcon.cs, SettingsForm.cs, Hotkey.cs, StartupRegistration.cs
src/Deskbridge.Mcp/
  ToolCatalog.cs (split into) Tools/WindowTools.cs, FileTools.cs, AppTools.cs, SystemTools.cs, UiTools.cs
  ToolServices.cs (grows), McpSessionFactory.cs (takes ClientContext + pipeline)
src/Deskbridge.Broker/Program.cs, BrokerHost.cs          wiring, pinned, lazy UIA worker
scripts/publish-broker.ps1, scripts/measure-idle.ps1     Task 11
tests/Deskbridge.Tests/...                               unit tests per task
tests/Deskbridge.E2E/Gates/Phase1GateTests.cs           Task 12
```

---

### Task 0: Public repo files and test isolation

**Files:** `README.md`, `LICENSE`, `docs/code-signing-policy.md`, `.gitignore` (add `.superpowers/`), `src/Deskbridge.Core/Names.cs`, `tests/Deskbridge.Tests/NamesTests.cs`

**Interfaces:** `Names.ConfigDirVariable = "DESKBRIDGE_CONFIG_DIR"`; `Names.ConfigDirectory()` returns the variable's value when set, else `%APPDATA%\Deskbridge`; `Names.LogDirectory()` returns `<config dir>\logs` when the variable is set, else `%LOCALAPPDATA%\Deskbridge\logs`; `Names.RunValueName = "Deskbridge"`.

**Tests:**

```csharp
[Fact] public void Config_and_logs_follow_the_override()
{
    Environment.SetEnvironmentVariable(Names.ConfigDirVariable, @"C:\t\db");
    try
    {
        Assert.Equal(@"C:\t\db", Names.ConfigDirectory());
        Assert.Equal(@"C:\t\db\logs", Names.LogDirectory());
    }
    finally { Environment.SetEnvironmentVariable(Names.ConfigDirVariable, null); }
}
[Fact] public void Without_override_config_is_in_appdata() =>
    Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Deskbridge"), Names.ConfigDirectory());
```

**Content:** README (what Deskbridge is, status "early, not ready for use", the hands-off rule, build and test commands, licence line, code signing line). LICENSE: MIT, "Copyright (c) 2026 The Deskbridge authors". Code signing policy in the form SignPath Foundation asks for: who commits, who reviews, who approves releases (the owner), the attribution line, and the privacy statement ("Deskbridge makes no network connections of its own, except to remote MCP servers the user adds").

### Task 1: Tool pipeline and effects

**Files:** `src/Deskbridge.Core/Tools/*`, `src/Deskbridge.Core/Clients/ClientContext.cs`, `src/Deskbridge.Core/Apps/AppId.cs`, `src/Deskbridge.Core/Policy/{PolicyDecision,ConfirmationRequest,IConfirmationPresenter,PolicyErrors}.cs`, `src/Deskbridge.Core/Audit/AuditEntry.cs`; test `tests/Deskbridge.Tests/ToolPipelineTests.cs`

**Interfaces:**
- `public enum ToolEffect { Read, Change, Destructive }`, `public enum Rung { AppInterface, WindowsApi, UiAutomation, WindowMessage, Foreground }` (JSON names: `app_interface`, `windows_api`, `ui_automation`, `window_message`, `foreground`).
- `public sealed record AppId(string Key, string DisplayName)` with statics `Files`, `Clipboard`, `Media`, `Settings`, `Windows`, `Notifications` (keys `system.files` and so on).
- `public sealed record ToolCall(string Tool, AppId App, ToolEffect Effect, string Action, string? Content = null)` where `Action` is the one line a person reads ("Rename notes.txt to plan.txt").
- `public sealed record ClientContext(ClientHello Hello)` with `string Name` (process name, or "An unknown client") and `string Key` (lowercase exe path, or `pid:<n>`).
- `public abstract record PolicyDecision` with `Allow`, `Deny(string Reason)`, `Confirm(ConfirmationRequest Request)`.
- `public sealed record ConfirmationRequest(ConfirmationKind Kind, string ClientName, AppId App, string Action, string? Content, ToolEffect Effect, bool CanRemember, string? Note)`; `enum ConfirmationKind { ToolCall, ClientApproval }`; `enum ConfirmationAnswer { Allow, AllowAndRemember, Deny, TimedOut }`.
- `public interface IConfirmationPresenter { Task<ConfirmationAnswer> AskAsync(ConfirmationRequest request, CancellationToken ct); }`
- `public interface IToolPolicy { PolicyDecision Decide(ToolCall call, ClientContext client); void Record(ToolCall call, ClientContext client, ConfirmationRequest request, ConfirmationAnswer answer); }`
- `public interface IAuditLog { void Append(AuditEntry entry); }`; `public sealed record AuditEntry(DateTimeOffset Time, string Client, string App, string Tool, ToolEffect Effect, string Decision, Rung? Rung, string Result, long DurationMs, string? Content)`. Decision values: `allowed`, `allowed_by_user`, `remembered`, `denied_by_policy`, `denied_by_user`, `timed_out`.
- `public sealed record Executed<T>(T Value, Rung Rung)`.
- `public sealed class ToolPipeline(IToolPolicy policy, IConfirmationPresenter presenter, IAuditLog audit, TimeProvider time) { public Task<T> RunAsync<T>(ToolCall call, ClientContext client, Func<CancellationToken, Task<Executed<T>>> execute, CancellationToken ct); }`. It asks the policy, loops while the answer is `Confirm` of kind `ClientApproval` (at most twice), asks the presenter for a `ToolCall` confirmation, records the answer through the policy, runs `execute` only on Allow, and appends exactly one audit entry. Denials throw `PolicyDeniedException(reason)` or `DeniedByUserException()` (both `DeskbridgeException`).

**Tests (fakes for policy, presenter and audit):**
- `Denied_call_never_runs` (policy returns Deny: execute not called, `PolicyDeniedException` with the reason, one audit entry `denied_by_policy`, result `not_run`).
- `Allowed_call_runs_and_is_audited_once` (`allowed`, rung recorded, result `ok`).
- `Confirmed_call_runs_after_allow` and `User_deny_never_runs` (`denied_by_user`), `Timeout_counts_as_deny` (`timed_out`).
- `Execute_failure_is_audited_as_error` (execute throws `DeskbridgeException`: audit result `error`, exception rethrown).
- `Content_is_not_audited` (audit entry `Content` is null; the audit log decides whether to keep it, Task 5).

### Task 2: App identity and process facts

**Files:** `src/Deskbridge.Engines.Os/AppResolver.cs`, `ProcessFacts.cs`; tests `tests/Deskbridge.Tests/AppIdTests.cs`, `tests/Deskbridge.E2E/AppResolverTests.cs`

**Interfaces:** `ProcessFacts.ImagePath(int pid)`, `ProcessFacts.IsElevated(int pid)` (token elevation; access denied counts as elevated), `AppResolver.FromProcess(int pid)`, `FromWindow(nint hwnd)`, `FromPath(string exe)`. A Store app's frame window (`ApplicationFrameWindow`) resolves to the process of its `Windows.UI.Core.CoreWindow` child. `DisplayName` is the exe's file description, else its file name.

**Tests:** `Same_app_from_window_and_process_is_equal` (target app); `Calculator_resolves_to_its_own_exe` (key ends with `calculatorapp.exe`); `Keys_are_lowercase_full_paths`; `This_process_is_not_elevated`.

### Task 3: Config and permission store

**Files:** `src/Deskbridge.Core/Policy/{PermissionLevel,DeskbridgeConfig,ConfigStore,ConfigJsonContext}.cs`; test `tests/Deskbridge.Tests/ConfigStoreTests.cs`

**Interfaces:**
- `enum PermissionLevel { Off, Read, Act }`.
- `DeskbridgeConfig` (snake_case JSON): `version`, `paused`, `pinned`, `start_at_sign_in`, `content_logging`, `apps` (key to level), `clients` (list of `ClientRecord`), `grants` (list of `{client, app, granted_at}`), `off_by_default` (process names).
- `ConfigStore(string directory)`: `Current` (re-reads when the file's write time changed, checked on each access), `Update(Func<DeskbridgeConfig, DeskbridgeConfig> change)` (writes to a temp file, then replaces), `Level(AppId app)`. Defaults: `Read` for any app; `Off` when the exe name is on `off_by_default` (1Password, Bitwarden, KeePass, KeePassXC, Dashlane, LastPass, Enpass, Authy, Microsoft Authenticator); `Act` for `system.notifications`.
- `ConfigError` (string or null) holds the reason the file could not be read.

**Tests:** `New_app_defaults_to_read`; `Password_manager_defaults_to_off`; `Set_level_survives_a_new_store`; `Hand_edit_is_picked_up_without_restart`; `Malformed_config_never_grants_act` (write `{ not json`, then `Level` of an app set to Act earlier is Read, and `ConfigError` names the problem); `Update_is_atomic` (no `.tmp` left behind).

### Task 4: Policy engine

**Files:** `src/Deskbridge.Core/Policy/{PolicyEngine,NeverAvailable}.cs`, `src/Deskbridge.Core/Tools/DestructiveWords.cs`; test `tests/Deskbridge.Tests/PolicyEngineTests.cs`

**Interfaces:** `PolicyEngine(ConfigStore config, ClientRegistry clients) : IToolPolicy`. Order: paused gives Deny ("Deskbridge is paused. Resume it from the tray icon."); unapproved client gives Confirm of kind `ClientApproval`; `NeverAvailable.Reason(call)` non-null gives Deny; app Off gives Deny ("Deskbridge is off for {app}. Change it in Deskbridge settings."); Read effect gives Allow; Change in an Act app with a grant gives Allow; Change in an Act app gives Confirm (`CanRemember: true`); Change in a Read app gives Confirm (`CanRemember: false`, note "{app} is read-only in Deskbridge. Allowing lets this one change through."); Destructive needs Act (else Deny) and always gives Confirm (`CanRemember: false`). `Record` stores a grant on `AllowAndRemember` and approves the client on an Allow of a `ClientApproval`. `DestructiveWords.IsDestructive(string controlName)` matches delete, remove, send, publish, post, pay, buy, purchase, uninstall, format, discard, "don't save", "close without saving", erase, empty (recycle bin).

**Tests:** one per rule, plus `Remembered_grant_never_covers_destructive`, `Grant_is_per_client_and_per_app`, `Pausing_denies_a_call_allowed_a_moment_before`, `Destructive_word_on_a_control_makes_the_call_destructive` (via `DestructiveWords`), `Elevated_window_is_never_available` (`NeverAvailable` with a call flagged elevated).

### Task 5: Audit log and client registry

**Files:** `src/Deskbridge.Core/Audit/AuditLog.cs`, `src/Deskbridge.Core/Clients/{ClientRecord,ClientRegistry}.cs`, `src/Deskbridge.Engines.Os/Publisher.cs`; tests `tests/Deskbridge.Tests/AuditLogTests.cs`, `ClientRegistryTests.cs`

**Interfaces:** `AuditLog(string directory, Func<bool> contentLoggingOn, TimeProvider time) : IAuditLog`: one JSON line per entry in `audit-YYYY-MM-DD.jsonl` (local date), `content` written only when logging is on, any write failure swallowed and counted (`FailedWrites`). `ClientRecord(string Path, string? Publisher, string Name, DateTimeOffset ApprovedAt)`; `ClientRegistry(ConfigStore config, Func<string, string?> publisherOf)` with `IsApproved(ClientProcess)`, `Approve(ClientProcess)`, `Remove(string path)`; approval matches on path and publisher. `Publisher.Of(string exePath)`: signer subject's CN from `WinVerifyTrust` plus the certificate, or null when unsigned.

**Tests:** `Entry_fields_are_exact` (snake_case names); `Content_only_when_logging_is_on`; `Midnight_starts_a_new_file` (fake `TimeProvider`); `Locked_file_never_fails_the_call`; `Approved_client_is_not_asked_again`; `Changed_publisher_is_asked_again`; `Unsigned_exe_has_no_publisher` (the test target app).

### Task 6: Confirmation window

**Files:** `src/Deskbridge.Ui/Deskbridge.Ui.csproj` (`UseWindowsForms`), `UiThread.cs`, `ConfirmationForm.cs`, `ConfirmationPresenter.cs`; tests `tests/Deskbridge.E2E/ConfirmationWindowTests.cs`

**Interfaces:** `UiThread` (one STA thread with a WinForms message loop, started on first `InvokeAsync<T>(Func<T>)`). `ConfirmationPresenter(UiThread ui, TimeSpan timeout) : IConfirmationPresenter`: one window at a time (a `SemaphoreSlim`), bottom right of the primary screen's working area. The form's text is "Deskbridge" and its controls have AutomationIds `ActionText`, `ContentBox`, `AllowButton`, `RememberButton` (only when `CanRemember`), `DenyButton`. Client approval wording: "{client} wants to use Deskbridge." with the exe path and publisher in the content box.

**Tests (end to end):** `Showing_the_window_does_not_touch_the_user` (hands-off probe around `AskAsync` until the window is visible); `Allow_through_ui_automation_returns_allow` (Invoke on `AllowButton` via `UiaWorker`); `Remember_button_only_when_allowed`; `No_answer_times_out` (timeout 300 ms gives `TimedOut`); `Two_requests_queue_and_each_gets_its_answer`.

### Task 7: Pipeline in every session

**Files:** `src/Deskbridge.Mcp/{ToolServices,McpSessionFactory}.cs`, `src/Deskbridge.Mcp/Tools/WindowTools.cs` (moves `windows_list` and `ui_read` here), `src/Deskbridge.Broker/{Program,BrokerHost}.cs`; tests update `SessionHarness`, `McpSessionTests`, `BrokerHarness`

**Interfaces:** `ToolServices` gains `ToolPipeline Pipeline`, `ConfigStore Config`, `Lazy<UiaWorker> Uia` (created on first use, D38). `McpSessionFactory.Create(Stream, ClientHello, ToolServices, ILoggerFactory?)` builds a `ClientContext` from the hello and passes it to every tool. `windows_list` runs as a Read call on `system.windows`; `ui_read` as a Read call on the window's app.

**Tests:** existing session tests pass with a test policy that allows everything; new `Off_app_is_refused_with_its_reason` (config sets the target app Off; `ui_read` returns the plain reason); `Every_call_writes_one_audit_line`.

### Task 8: Windows engine tools

**Files:** `src/Deskbridge.Engines.Os/{Files,Apps,System}/*`, `WindowDescriber.cs`, `src/Deskbridge.Mcp/Tools/{FileTools,AppTools,SystemTools}.cs`; tests `tests/Deskbridge.Tests/SecretFilesTests.cs`, `tests/Deskbridge.E2E/OsToolsTests.cs`

**Interfaces (tools, effect, app):**
- `files_search(root, pattern, max_results=200)` Read, `system.files`: path, size, modified; skips folders it cannot read.
- `files_read(path, max_bytes=1048576)` Read: UTF-8 text as `untrusted_text`, `truncated`; refuses secret files and files with a NUL byte in the first 8 KB ("binary").
- `files_write(path, content, mode: create|overwrite)` Change; `files_move(from, to)` Change (never overwrites); `files_delete(path)` Destructive (Recycle Bin through `SHFileOperationW` with `FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI | FOF_SILENT`).
- `apps_list_installed()` Read, `system.windows`: name and app ID from the `shell:AppsFolder` namespace on an STA thread.
- `apps_list_running()` Read: processes that own visible windows, with window counts.
- `apps_launch(app)` Change on the launched app: an app ID or exe path, started with `ShellExecuteExW` and `SW_SHOWNOACTIVATE`; the result says whether it took the foreground (D39).
- `windows_describe(window)` Read: title, process, path, class, bounds, state, foreground, elevated, store app, chromium.
- `clipboard_read()` Read, `system.clipboard` (`untrusted_text`); `clipboard_write(text)` Change.
- `media_now_playing()` Read and `media_control(action: play|pause|toggle|next|previous)` Change, `system.media`, through `GlobalSystemMediaTransportControlsSessionManager`.
- `volume_set(percent)` Change, `system.media`, through Core Audio `IAudioEndpointVolume`; returns before and after.
- `notify_show(title, text)` Change, `system.notifications`, as a tray balloon.
- `settings_open(page)` Change, `system.settings`: `page` must match `^[a-z0-9-]+$`; opens `ms-settings:<page>` like `apps_launch` (D39).
- `SecretFiles.IsSecret(path)`: `.env`, `.env.*`, `*.pem`, `*.key`, `*.pfx`, `*.p12`, `id_rsa*`, `id_ed25519*`, `*.kdbx`, names containing `credential` or `secret`.

**Tests:** `SecretFiles` theory (each pattern, plus `notes.txt` and `environment.md` are not secret); end to end in a temp folder inside the hands-off probe: search, read, write, move, delete (file gone and present in the Recycle Bin namespace); read refuses `.env`; clipboard round trip (restoring the user's clipboard after); `windows_describe` on the target app; `apps_list_installed` contains Notepad's app ID; media tools answer without error when nothing plays ("Nothing is playing."); volume set and restored.

### Task 9: UI Automation tools

**Files:** `src/Deskbridge.Engines.Uia/{ElementRef,ElementCache,UiaTree,UiaActions,SecretWindows,StoreApps}.cs`, `Errors.cs`; `src/Deskbridge.Engines.Os/WindowMessages.cs`; `src/Deskbridge.Mcp/Tools/UiTools.cs`; target app gains controls; tests `tests/Deskbridge.Tests/ElementRefTests.cs`, `tests/Deskbridge.E2E/UiActionsTests.cs`

**Interfaces:**
- `element_id` format `<window handle>:<runtime id joined with ".">`, cached for 60 s on the worker.
- Tools: `ui_tree(window, max_depth=4, max_nodes=300)` Read; `ui_find(window, name?, automation_id?, control_type?)` Read (up to 20 matches); `ui_read` (Phase 0); `ui_press(window, element)`, `ui_set_text(window, element, text)`, `ui_select(window, element)`, `ui_toggle(window, element, on?)`, `ui_scroll(window, element, direction: up|down|left|right, amount: small|page)`, all Change, Destructive when `DestructiveWords` matches the control's name. `element` is an `element_id`, an AutomationId or a name.
- Before every action: read the element again (name, control type, enabled) and refuse with "The control changed since it was found. Find it again." on any difference.
- Press order: Invoke, Toggle, ExpandCollapse, SelectionItem, LegacyIAccessible default action, then `BM_CLICK` when the element has its own native `Button` window; otherwise "There is no way to press this control without the mouse."
- Set text: `ValuePattern.SetValue`, then `WM_SETTEXT` for a native `Edit` window; never on a password field.
- Refusals before touching the tree: Chromium windows, elevated windows ("That window runs as administrator. Windows does not let other programs touch it."), windows whose title shows a secret file, minimized Store apps ("{app} is minimized, and minimized Store apps hide their controls.").
- Results report the rung (`ui_automation` or `window_message`).

**Target app additions:** a button "Save" (`SaveButton`) that sets a label `StatusLabel` to "Saved", a checkbox "Bold" (`BoldCheck`), a combo box with three items (`SizeCombo`), a native Win32 button via `--native-button` (a `Button` child created with `CreateWindowEx`, no Invoke support exposed) for the window-message rung.

**Tests (end to end, all inside the hands-off probe):** `Press_invokes_a_button_in_a_background_window`; `Toggle_sets_a_checkbox`; `Select_picks_a_combo_item`; `Set_text_writes_a_text_box`; `Set_text_refuses_a_password_field`; `Press_refuses_a_control_that_changed` (rename the button between find and press); `Native_button_falls_back_to_window_message` (rung `window_message`); `Tree_stays_within_limits`; `Minimized_store_app_gives_a_plain_message` (Calculator minimized).

### Task 10: Tray, settings window, pause, hotkey

**Files:** `src/Deskbridge.Ui/{TrayIcon,SettingsForm,Hotkey,StartupRegistration}.cs`; broker wiring; tests `tests/Deskbridge.E2E/TrayAndSettingsTests.cs`, `tests/Deskbridge.Tests/StartupRegistrationTests.cs`

**Interfaces:** tray menu: Pause all or Resume, Settings, Open log folder, Quit. Hotkey Ctrl+Alt+Shift+P (`RegisterHotKey` with `MOD_NOREPEAT`) toggles pause; if Windows refuses the hotkey, the settings window says so. Settings window tabs: Apps (level per app), Clients (Remove), Remembered (Revoke, D36), General (start at sign-in, keep running, content logging, Pause). `StartupRegistration(string valueName)` with `IsOn`, `Set(bool on, string exePath)` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. The broker's idle exit is skipped while `pinned` is true.

**Tests:** `Pause_from_the_tray_denies_the_next_call` (menu item invoked through UI Automation); `Revoking_a_grant_asks_again`; `Startup_value_is_written_and_removed` (test value name `Deskbridge-test-<guid>`, removed in `finally`); `Pinned_broker_does_not_idle_exit`.

### Task 11: Cold start and measurement

**Files:** `src/Deskbridge.Broker/Deskbridge.Broker.csproj` (`PublishReadyToRun`), `scripts/publish-broker.ps1`, `scripts/measure-idle.ps1` (uses `artifacts/broker` when present; reports relay private bytes; adds a cold-start series of 5 runs), `src/Deskbridge.Broker/Program.cs` (UI Automation worker on first use).

**Check:** cold start median under 1 s over 5 runs with the ReadyToRun broker; idle with the tray icon loaded: broker CPU under 0.5% of one core, broker working set under 80 MB, relay private bytes under 10 MB. If the cold-start median stays over 1 s, record the numbers and the remaining causes in the report rather than weakening the check.

### Task 12: Gate scenario and report

**Files:** `tests/Deskbridge.E2E/Gates/Phase1GateTests.cs`, `tests/Deskbridge.E2E/Fixtures/ConfirmationAnswerer.cs`, `docs/phases/phase-1-report.md`

**Test:** through the AOT relay with a private pipe suffix and `DESKBRIDGE_CONFIG_DIR` set to a temp folder (system.files and Settings at Act, the test client approved): (1) rename a temp file with `files_move`; (2) read a Notepad document with `ui_read`; (3) with Settings opened at `ms-settings:dateandtime` during setup and Notepad in front of it, turn on "Show seconds in system tray clock" through `ui_tree`, `ui_press` (expand "Show more settings" under "Show time and date in the System tray") and `ui_toggle`, check `ShowSecondsInSystemClock` is 1 in the registry, then turn it off again (D37, place updated by D42). Each agent call runs inside the hands-off probe; `ConfirmationAnswerer` presses Allow in Deskbridge's window through UI Automation.

**Also:** one run of the same three steps by Claude Code headless (or Claude Desktop by the owner), measurements from Task 11, and `phase-1-report.md` in the Phase 0 report's format.

## Gate

- [ ] All unit and end-to-end tests pass, none skipped; 0 build warnings
- [ ] The gate scenario passes with zero cursor or focus changes during agent calls
- [ ] Idle with the tray loaded: broker CPU under 0.5% of one core, broker under 80 MB, relay private bytes under 10 MB
- [ ] Cold start measured with the ReadyToRun broker (target: median under 1 s)
- [ ] A real agent ran the three gate steps
- [ ] CI green on GitHub
- [ ] `phase-1-report.md` written; README, LICENSE and code signing policy ready for the SignPath application (D41)
