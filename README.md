# Deskbridge

A local MCP server for Windows that lets AI agents read and act in desktop apps without moving your mouse, typing into the window you are using, or taking focus.

**Status:** early development. Not ready for use. The plan is in [docs/plan.md](docs/plan.md) and progress by phase is in [docs/phases/](docs/phases/README.md).

## The rule it is built around

Your AI works in your apps while you keep working. Deskbridge reaches each app its own way first (its COM interface, an extension, its command line, its own MCP server), then through UI Automation patterns, then through messages sent to one window. It never moves the mouse and never types into the window you are using. A test checks the cursor position, the foreground window and the keyboard focus around every action.

Every app has its own permission (Off, Read or Act), changes are confirmed in a small window that does not take focus, and every call is written to a local audit log.

## What works today

25 tools. Every call goes through the app's permission, a confirmation when the call changes something, and one line in the audit log.

| Group | Tools |
|---|---|
| Windows and controls | `windows_list`, `windows_describe`, `ui_read`, `ui_tree`, `ui_find`, `ui_press`, `ui_toggle`, `ui_select`, `ui_set_text`, `ui_scroll` |
| Apps | `apps_list_installed`, `apps_list_running`, `apps_launch`, `settings_open` |
| Files | `files_search`, `files_read`, `files_write`, `files_move`, `files_delete` (to the Recycle Bin) |
| Clipboard, media and notifications | `clipboard_read`, `clipboard_write`, `media_now_playing`, `media_control`, `volume_set`, `notify_show` |

- Apps start at Read. The settings window (from the tray icon) sets each app to Off, Read or Act, and lists the approved clients and remembered grants.
- Each new MCP client is approved once. Deleting, sending and similar buttons ask every time.
- Ctrl+Alt+Shift+P pauses or resumes Deskbridge from anywhere.
- The UI tools refuse Chromium and Electron apps (browsers, VS Code, Slack, Discord) until their adapters arrive in later phases.

Any MCP client starts it as `deskbridge.exe mcp`.

## Build and test

Requires Windows 10 or 11 and the .NET 10 SDK or later.

```powershell
dotnet build Deskbridge.slnx
dotnet test --project tests/Deskbridge.Tests      # unit tests
dotnet test --project tests/Deskbridge.E2E        # end-to-end tests: they open and close real windows
```

`scripts/publish-broker.ps1` publishes the broker (ReadyToRun) to `artifacts/broker` and the relay (Native AOT) to `artifacts/relay`:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/publish-broker.ps1
```

## Licence and signing

MIT, see [LICENSE](LICENSE). Code signing: see [docs/code-signing-policy.md](docs/code-signing-policy.md).
