# Handover

What someone taking over Deskbridge needs on day one: where the project stands, what is open, how to build and test it, and how work is done here. State as of 2026-11-22.

## What Deskbridge is

A local MCP server for Windows. It lets any AI agent read and act in desktop apps without moving the user's mouse, typing into the window the user is using, or taking focus. It ships as a public product first, then becomes the PC part of Nook's action layer. Nook is a separate, private project; ask the owner for its integration notes, and never change Nook from work in this repo.

| Document | What it holds |
|---|---|
| [plan.md](plan.md) | The plan of record: product, architecture, principles, roadmap |
| [decisions.md](decisions.md) | Every decision with its date, options, reason and who made it (D1 to D47) |
| [phases/README.md](phases/README.md) | The seven phases, their gates, and what each needs from the owner |
| [phases/phase-1-report.md](phases/phase-1-report.md) | The latest gate report: results, measurements, problems met, deferred findings |
| [../CLAUDE.md](../CLAUDE.md) | The rules every session follows, for people and for Claude Code alike |

## Where it stands

| Phase | State |
|---|---|
| 0. Setup | Done. Relay and broker, two tools, the hands-off probe, CI on x64 and Arm64. |
| 1. Core | Built and gated: 25 tools, per-app permissions, client approval, the confirmation window, remembered grants, the audit log, the tray and settings. 139 unit and 65 end-to-end tests pass. Cold start median 736 ms. |
| 2. Adapter SDK and local hub | Next. [phase-2-sdk-hub.md](phases/phase-2-sdk-hub.md) is written at task level and must be refined to step level before work starts. |

## Open items

| Item | Who | Where |
|---|---|---|
| Approve or change the proposed decisions D38 to D45 | Owner | [decisions.md](decisions.md) |
| Apply to SignPath Foundation for free code signing (needs multi-factor sign-in on GitHub) | Owner | D41, [code-signing-policy.md](code-signing-policy.md) |
| Optional: one more real agent run of the Phase 1 gate on the build that has the D45 fixes | Owner decides | [phase-1-report.md](phases/phase-1-report.md) |
| Six deferred minor findings | Whoever picks them up | "Deferred minor findings" in [phase-1-report.md](phases/phase-1-report.md) |
| Refine Phase 2 to step level, publish its plan, and get the owner's go | Next maintainer | [phases/README.md](phases/README.md) |

## Access and accounts

- The repo is `github.com/omarkhan2217/deskbridge` (D46). The owner account invites collaborators. `alikhan866` is a collaborator with push access.
- The repo is public. Nothing private goes in it: no details of Nook's private code, no local paths, no personal information, no secrets (`CLAUDE.md` rule 6).
- Commits use a GitHub noreply address (D35). Set yours per clone with `git config user.email`.
- Code signing goes through SignPath from CI once the application is approved (Phase 6). No keys or certificates live in the repo.

## Repo layout

| Path | What it is |
|---|---|
| `src/Deskbridge.Core` | Names, the tool pipeline, policy, config, audit log, client approval. No Win32 calls. |
| `src/Deskbridge.Engines.Os` | Windows APIs: windows, apps, files, clipboard, media, volume, window messages. |
| `src/Deskbridge.Engines.Uia` | UI Automation on one worker thread with `AutoSetFocus` off: reading, finding and acting on controls. |
| `src/Deskbridge.Ui` | WinForms: the confirmation window, tray icon, pause hotkey and settings window. |
| `src/Deskbridge.Mcp` | The MCP tools and one MCP server per connection. |
| `src/Deskbridge.Broker` | `deskbridge-broker.exe`: one per user, serves every client over a named pipe. |
| `src/Deskbridge.Relay` | `deskbridge.exe`: the Native AOT relay each MCP client starts. |
| `tests/Deskbridge.Tests` | Unit tests. CI runs these. |
| `tests/Deskbridge.E2E` | End-to-end tests with the hands-off probe. They open and close real windows. |
| `tests/Deskbridge.E2E.Target` | A WinForms app the end-to-end tests drive. |
| `scripts/` | `publish-broker.ps1` (publish both executables) and `measure-idle.ps1` (cold start and idle cost). |

## Build, test and publish

Requires Windows 10 or 11 and the .NET 10 SDK or later (`global.json` rolls forward to newer major versions).

```powershell
dotnet build Deskbridge.slnx -warnaserror
dotnet test --project tests/Deskbridge.Tests      # unit tests, about 5 s
dotnet test --project tests/Deskbridge.E2E        # end-to-end, about 1 to 2 min; opens real windows
powershell -ExecutionPolicy Bypass -File scripts/publish-broker.ps1
powershell -ExecutionPolicy Bypass -File scripts/measure-idle.ps1 -Minutes 5
```

- Tests run on the Microsoft Testing Platform (set in `global.json`), so filters go after `--`: `dotnet test --project tests/Deskbridge.E2E -- --filter-class "Deskbridge.E2E.UiActionsTests"`.
- Run end-to-end tests when nobody is using the PC. They refuse to start if Notepad or Calculator is already open, and setup steps retry when the user's own input moves the foreground.
- The Native AOT publish needs `vswhere.exe` on `PATH`: add `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer`.
- `Deskbridge.slnx` is edited by hand; `dotnet sln add` fails on it in SDK 11.

## How the pieces run

An MCP client starts `deskbridge.exe mcp`. The relay finds the client's process (skipping shells such as `cmd`), starts the broker if none is running, and connects to the pipe `deskbridge-<user SID>`. The broker runs one MCP session per connection and exits after 30 minutes with no client.

| Environment variable | What it does | Used by |
|---|---|---|
| `DESKBRIDGE_PIPE_SUFFIX` | A private pipe and broker, so tests never meet the user's broker | Tests, measurements |
| `DESKBRIDGE_CONFIG_DIR` | Another folder for `config.json` and logs, so tests never touch the user's permissions | Tests, measurements (D40) |
| `DESKBRIDGE_BROKER_PATH` | Which broker the relay starts | Tests, measurements |
| `DESKBRIDGE_IDLE_EXIT_MS` | A shorter idle exit | Tests |

By default `config.json` is in `%APPDATA%\Deskbridge` and the audit logs are in `%LOCALAPPDATA%\Deskbridge\logs`.

To try it from Claude Code, publish both executables, then point an MCP config at the relay:

```json
{
  "mcpServers": {
    "deskbridge": {
      "type": "stdio",
      "command": "<repo>\\artifacts\\relay\\deskbridge.exe",
      "args": ["mcp"],
      "env": { "DESKBRIDGE_BROKER_PATH": "<repo>\\artifacts\\broker\\deskbridge-broker.exe" }
    }
  }
}
```

The first call shows a confirmation asking whether the client may use Deskbridge.

## How work is done here

1. **Plan first.** Before a feature or a decision, publish what will be done (the change, the files, how it is checked), add an entry to `decisions.md`, and wait for the owner's approval.
2. **One branch per phase** (`phase-<n>-<name>`). Each task is test first: write the test, watch it fail, make it pass. Every end-to-end action runs inside the hands-off probe, which fails on any cursor, foreground or focus change.
3. **Log every decision.** A decision is never edited away; a later entry supersedes it and says so.
4. **No commit before the owner has reviewed the change.**
5. **At the gate,** run every check, write `phase-<n>-report.md`, and get the owner's review. Then commit, merge and push.
6. **Writing rules** in `CLAUDE.md` apply to docs, comments, commit messages and UI text: no em or en dashes as punctuation, no hype words, short active sentences, numbers where they exist. Check with `rg -n "[\x{2013}\x{2014}]" --glob "!*.pdf"`.

## Product rules the code must keep

When two conflict, the earlier one wins.

1. **Hands off.** Never move the mouse, never type into the window the user is using, never take focus without asking.
2. **Safe by default.** Apps start read-only. Changes need the user's permission for that app; destructive actions ask every time.
3. **The app's own way first.** App API, COM, CLI or plugin, then UI Automation patterns, then window messages, then nothing.
4. **Light.** No polling and no timers when idle. Never put a Chromium or Electron app into accessibility mode unless its adapter allows it.
5. **Local only.** The Deskbridge process makes no network calls and sends no telemetry.
6. **Works with any agent.** Standard MCP, so every client uses it the same way.
