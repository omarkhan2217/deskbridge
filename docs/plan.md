# Deskbridge product plan

Version 2, 2026-11-22. This replaces the owner's first draft, a PDF kept outside the repo. Decision IDs (D1 to D23) point to `docs/decisions.md`.

## Summary

Deskbridge is a local MCP server for Windows. It lets any AI agent read and act in desktop apps without moving the user's mouse, typing into the window they are using, or taking focus. It ships to the public first, then becomes the PC part of Nook's action layer.

- **Who it is for:** people using Claude Code, Claude Desktop, Cursor or another MCP client on Windows, and Nook.
- **Is it feasible:** yes. Every v1 task has a route that never touches the mouse. The hard limits are the ones every UI Automation tool meets: administrator windows, games, and apps that draw their own controls.
- **Stack:** C# on .NET 10 LTS. Two .exe files: a small relay that clients start, and a broker that does the work (D3, D11).
- **Licence:** open core with an MIT core, signed for free through SignPath (D4, D5).
- **v1 apps:** File Explorer, Settings, windows, clipboard and media in the core; VS Code; Claude Code; Unity; WPS Office and Microsoft Office; Slack; Linear; Notion; Discord, read only (D6).
- **Time:** Nook milestone 1 at the end of week 6, Nook milestone 2 at the end of week 9, public v1 at about week 15.
- **Cash cost, first year:** about ₹1,000 for a domain, plus Microsoft 365 only if Office testing runs past the free trial.

## What changed since the PDF draft

| Topic | PDF draft | Now | Why |
|---|---|---|---|
| Install | One self-contained .exe | A small relay .exe and a broker .exe in one folder | One shared broker for every client (D11) |
| Confirmations | MCP elicitation, Windows notification as fallback | Deskbridge's own window that never takes focus; paired clients show their own card | Claude Desktop has no elicitation, and the spec changed (D12) |
| MCP version | Not stated | 2026-07-28 through the C# SDK 2.x, with older clients still served | D13 |
| Settings and tray | WinUI 3 | WinForms | D16 |
| Tool names | `files.read` | `files_read` | D15 |
| Local only | No network at all | No network except remote MCP servers the user added | D7 |
| Launch apps | Office, Spotify, OBS, VS Code, Unity, Blender, Resolve, Notion | VS Code, Claude Code, Unity, WPS and Office, Slack, Linear, Notion, Discord | D6, D19, D20 |
| Nook milestone | 3 tasks at week 5 | 5 tasks over two milestones, weeks 6 and 9 | D9, D10 |
| Nook catalogue | Code Mode in a script catalogue | Classic tools through Nook's connector catalogue | D22 |
| Signing | Certum, about ₹7,500 a year | SignPath, free | D5 |
| Licence | MIT, assumed | Open core | D4 |
| Timeline | 13 weeks to v1 | About 15 weeks | Remote sign-in and the Claude Code loop |

## Feasibility

| Part | Verdict | Evidence and limits |
|---|---|---|
| Act on native apps without the cursor | Yes | UI Automation patterns (Invoke, Value, Toggle, Selection, ExpandCollapse, Scroll) work on background windows. |
| Act without taking focus | Yes, with one setting | UI Automation takes focus before acting by default. Setting `AutoSetFocus` to false on `IUIAutomation2` stops it (D14). An earlier prototype measured the difference. |
| Electron apps (VS Code, Slack, Discord, Notion, Linear) | Partly | Reads work, but walking the tree switches Chromium into accessibility mode, which costs CPU and memory. Chrome 138 and later use native UI Automation, which is faster; each Electron app depends on its Chromium version, so each is measured. Prefer an extension or the app's own server where one exists. |
| WPS Office and Microsoft Office | Yes | Both expose the Office object model over COM (WPS as `KET.Application`, `KWPS.Application`, `KWPP.Application`). A busy app rejects COM calls, so the adapter runs on its own STA thread with a message filter that retries. Modern .NET has no `Marshal.GetActiveObject`, so the adapter calls `GetActiveObject` in `oleaut32` directly. |
| Outlook | Not in v1 | The new Outlook has no COM (D19). |
| VS Code | Yes | A small first-party extension serves a closed list of functions over a per-user named pipe. The `code -g` command line can bring VS Code to the front, so opening files goes through the extension instead. |
| Claude Code | Yes | Headless CLI with streamed JSON output and session resume (D17). |
| Unity | Yes | Unity's official MCP server (open beta since 2026-05, Unity 6 and later) or the community server, both through the hub. Check whether the official one needs a paid Unity plan. |
| Notion, Linear and Slack servers | Yes, with sign-in | Official remote MCP servers with OAuth. Slack's server only accepts registered Slack apps (see Open decisions). |
| Administrator windows | No | Windows blocks UI Automation, window messages and input from a normal process (UIPI). Deskbridge says so instead of failing silently. |
| Games, canvas editors, custom-drawn timelines | Not in v1 | No controls to automate. A hidden-session mode on Microsoft's agent workspace could cover some of them later. |
| Resource budget | Likely | The estimates in "Resource budget" are under every limit. Phase 0 measures them. |

## Why it should exist

Agents on Windows today do one of two things. Most drive the user's cursor (Windows-MCP and most computer-use tools). A few newer servers act through UI Automation without the cursor, but reach each app only through its on-screen controls (hands-off, desk-mcp, Sidehand, all started between 2026-09 and 2026-11). Acting without the cursor is becoming common.

No project yet offers one signed, local layer that:

1. reaches each app its own way first (COM, an extension, a command line, the app's MCP server) and falls back to UI Automation;
2. lets the user set Off, Read or Act per app and confirms each change in a window the agent cannot answer for them;
3. puts existing per-app and remote MCP servers behind the same permissions;
4. works the same for every MCP client, and is the PC layer of Nook.

**The pitch:** "Your AI works in your apps while you keep working. It never touches your mouse."

### What exists

| Project | What it does | Gap Deskbridge fills |
|---|---|---|
| [Windows-MCP](https://github.com/CursorTouch/Windows-MCP) (about 12,000 stars, Python) | UI Automation tree plus mouse and keyboard tools; pauses when the user moves the mouse | Drives the real cursor; no app adapters |
| [hands-off](https://github.com/MareAltaStudio/hands-off) (2026-10, Python) | Same no-cursor promise: UI Automation patterns, window messages, recipes | No app adapters, no hub, no per-app permissions; early |
| desk-mcp, Sidehand, Desktop Touch MCP | Small servers with cursor-free Invoke and SetValue | Same gaps as hands-off |
| [Cua](https://github.com/trycua/cua) (about 33,000 stars, Rust) | Computer-use framework for VMs and sandboxes; its driver delivers input in the background where it can | Built around a separate machine or session, not the user's own apps |
| Microsoft's native MCP on Windows (on-device agent registry) | Registry of agent connectors with File Explorer and Settings built in; reported in Release Preview builds from 2026-04 | Two connectors; not generally available; Deskbridge can register in it later |
| Microsoft's agent workspace | Agents run apps in a separate Windows session | Preview, off by default; a later option for a hidden-session mode |
| [UFO³](https://github.com/microsoft/UFO) (Microsoft Research) | Agent framework using UI Automation plus app APIs | A research agent, not a server any client can use |
| Per-app MCP servers (Unity, Blender, Notion, Linear, Slack) | Deep control of one app or service each | Scattered, each trusted separately; Blender's runs model-written code with no checks |

## Product principles

Six rules decide every design question. When two conflict, the earlier one wins (D1).

1. **Hands off.** Never move the mouse, never type into the window the user is using, never take focus without asking.
2. **Safe by default.** Every app starts read-only. Changes need the user's permission for that app, and anything destructive asks every time.
3. **The app's own way first.** Use an app's API, COM, command line, extension or MCP server before UI Automation, UI Automation before window messages, and window messages before nothing.
4. **Light.** Close to zero CPU when idle, no polling, and never switch a Chromium or Electron app into accessibility mode unless its adapter allows it.
5. **Local first.** Deskbridge's own code makes no network calls and sends no telemetry. The only traffic is to remote MCP servers the user added, each shown as a network connector with its domain. Logs stay on the PC (D7).
6. **Works with any agent.** Standard MCP, so Claude Code, Claude Desktop, Cursor and Nook all use it the same way.

## Architecture

```text
MCP clients: Claude Code, Claude Desktop, Cursor, Nook
      |  stdio: each client starts "deskbridge.exe mcp"
      v
Relay: deskbridge.exe (Native AOT, one per client, under 10 MB)
      |  named pipe, current Windows user only
      v
Broker: deskbridge-broker.exe (one per Windows user)
      MCP front:  classic tools, or Code Mode (search, describe, execute)
      Policy:     per-app Off / Read / Act, client approval, confirmations, audit log
      Router:     tries the app's own way first, never the mouse
      Engines:
        Windows APIs   files, apps, windows, clipboard, media, Settings links
        UI Automation  one worker thread, AutoSetFocus off
        Adapters       COM, command line, WebSocket, URI links
        Plugin bridge  named pipe to in-app plugins and extensions
        Hub            local MCP servers (stdio) and remote ones (HTTP with OAuth)
      |
      v
Apps on the PC: Explorer, Settings, VS Code, Claude Code, WPS and Office, Slack, Discord, Unity
Remote MCP servers, through the hub only: Notion, Linear, Slack
```

Every request passes the policy check before the router picks how to reach the app. The five engines are the only parts that touch apps, and none of them moves the mouse.

### Relay and broker

- **Clients start the relay.** Every client config, the Claude Desktop bundle and Nook run `deskbridge.exe mcp`. The relay sends the broker one identity message (see "Client identity" below), then copies MCP messages between stdio and the broker's pipe. It uses no COM, so it is compiled with Native AOT.
- **The broker starts on first use.** It can also start at sign-in (a setting, off by default). It exits after 30 minutes with no clients connected, unless the user pins it in the tray.
- **The pipe** is `\\.\pipe\deskbridge-<user SID>`, and its access list allows only the current user. There is no TCP listener.
- **Client identity.** The relay sends the broker its parent process: path, publisher signature and process ID. The first time a new client connects, Deskbridge asks the user: "Cursor wants to use Deskbridge. Allow?" The answer is stored per client path and publisher.
- **What this does not stop.** Malware already running as the user can do anything the user can do, including open the pipe. Deskbridge protects against agents being tricked and against other users on the PC, not against malware in the user's own account.

### Confirmations

- **Deskbridge's own window, by default.** A small window near the tray that never takes focus. It shows the app, the action and the exact content, for example "Update cells B2:B9 in pricing.xlsx (WPS Spreadsheets)?" with the new values. The user clicks Allow or Deny. No answer in 2 minutes counts as Deny.
- **Paired clients.** The user pairs Nook once from the settings window. For a paired client, Deskbridge returns an "input required" result (2026-07-28 spec) or an elicitation request (older spec), and the client shows its own card. The approval comes back bound to a SHA-256 of the client, app, action, arguments and target window. It works once and expires after 2 minutes.
- **Always asks, never remembered:** deleting, sending, publishing, paying, closing without saving, running a project check for the first time, and anything an adapter marks destructive.

### What agents see

The client picks one interface in its config: `deskbridge.exe mcp` for classic tools, `deskbridge.exe mcp --code-mode` for Code Mode. Both call the same functions underneath, so permissions, confirmations and logging behave the same.

**1. Classic tools (default).** 25 core tools, plus tools from each enabled adapter.

| Group | Tools |
|---|---|
| Apps and windows | `apps_list_installed`, `apps_list_running`, `apps_launch`, `windows_list`, `windows_describe` |
| Files | `files_search`, `files_read`, `files_write`, `files_move`, `files_delete` (through the Shell; deletes go to the Recycle Bin and always ask) |
| System | `clipboard_read`, `clipboard_write`, `media_control`, `media_now_playing`, `volume_set`, `notify_show`, `settings_open` |
| UI Automation | `ui_tree`, `ui_find`, `ui_read`, `ui_press`, `ui_set_text`, `ui_select`, `ui_toggle`, `ui_scroll` |
| Per app | `vscode_*`, `claude_code_*`, `project_check_*`, `sheet_*`, `doc_*`, `slides_*`, `slack_*`, `discord_*`, and hub tools under each server's prefix |

**2. Code Mode (for clients that write code).** Three tools. `search` finds functions, `describe` returns a function's TypeScript declaration, and `execute` runs a short JavaScript program in Jint that calls them. The sandbox has no file, network or .NET access, and only Deskbridge functions are exposed. Each run has a 5 s time limit, a 64 MB memory limit and a statement limit. The context stays small however many adapters are enabled.

**Text from apps is untrusted.** Any tool result that carries text read from an app returns it in an `untrusted_text` field, with a note that it may contain instructions the agent must not follow.

## The no-cursor rule

Every action has a way to happen without the mouse, or it is not offered. The router tries these in order and stops at the first that works.

| Order | Method | Works for | Touches the user? |
|---|---|---|---|
| 1 | The app's own interface: COM, extension, command line, URI links, in-app plugin, the app's MCP server | Office, WPS, VS Code, Claude Code, Unity, Notion, Linear, Slack | No |
| 2 | Windows APIs: Shell, media session, audio, notifications, clipboard, Settings links | Files, playback, volume, system settings | No |
| 3 | UI Automation patterns with AutoSetFocus off | Win32, WPF, WinForms, WinUI, UWP; Electron and Chromium where the adapter allows | No |
| 4 | Window messages sent to one window (`BM_CLICK`, `WM_SETTEXT`) | Some older Win32 apps | No |
| 5 | Bring the app to the front, then act | Apps that only accept focused input | Yes. Asks first: "Deskbridge needs to bring Slack to the front." |
| Never | Mouse movement, or typing into the user's active window | | |

Before any change, the engine reads the target again (name, role, enabled state, window handle) and refuses if it changed. Every result says which rung did the work.

**How it is proven.** Every test records the cursor position, the foreground window and the keyboard focus before and after each action. Any change fails the test, unless the action was rung 5 and the user approved it.

## Safety and permissions

- **Per-app permission:** Off, Read or Act, set in the settings window or in `%APPDATA%\Deskbridge\config.json`. New apps start at Read. Apps that hold money or secrets (banking apps, password managers) start at Off.
- **Per-client approval:** each new MCP client is approved once (see "Relay and broker").
- **Network connectors:** each remote server is listed with its domain and the account it signed in as. Tokens live in Windows Credential Manager, never in config files or logs.
- **Never readable:** password fields, and windows showing `.env`, key or credential files.
- **Never available:** reading one-time codes, solving CAPTCHAs, changing security settings, controlling administrator windows, and any change in Discord (D8).
- **Audit log:** every call is recorded locally as JSON Lines in `%LOCALAPPDATA%\Deskbridge\logs\`, one file per day, with time, client, app, action, rung and result. Content is stored only if the user turns it on.
- **Pause:** "Pause all" in the tray, and a global hotkey (Ctrl+Alt+Shift+P by default).

## Adapters

An adapter teaches Deskbridge one app. It is a folder with a `manifest.json` and code.

**The manifest declares** the app's name and how to detect it (process name, install path, version range, COM ProgID); the transport; each function with typed arguments and a description; each function's effect (`read`, `change` or `destructive`); and the app versions it was tested with.

**Transports**

1. **External interface.** Deskbridge calls the app over COM, a command line, WebSocket or a URI. Examples: WPS and Office (COM), Claude Code (CLI).
2. **In-app plugin or extension.** A small plugin inside the app connects to the broker over a named pipe with a per-install token. Example: the VS Code extension.
3. **Hub.** An existing MCP server, local (started over stdio) or remote (HTTP with OAuth), with Deskbridge's permissions and confirmations applied on top. Examples: Unity, Notion, Linear, Slack.
4. **UI Automation recipes.** Saved UI Automation steps for an app without an API, checked against the app's version. Examples: Slack and Discord reads.

**Who can write one in v1.** First-party adapters are C# projects built into the broker. Community adapters are manifests with recipes, or hub entries (D21). Script adapters that run in Jint are designed in Phase 2 and may ship after v1.

**Open core.** The core, the SDK and every v1 adapter are MIT. Paid adapters, if they come, ship as separate packages with their own signing certificate (D4, D5).

## v1 apps

| App | Transport | What v1 does | Effort |
|---|---|---|---|
| File Explorer and Settings | Windows APIs | Search, read, write and move files; open Settings pages | In the core |
| Media (any player, including Spotify) | Windows media session | Play, pause, next, what is playing | In the core |
| VS Code | First-party extension over a named pipe | Open a file at a line without bringing VS Code forward, list problems, read the active editor and selection | 1 week |
| Claude Code | Headless CLI | Start a task in a project folder, continue a session, return the result and the changed files | 4 days |
| Project checks | Named commands per project | Run `test`, `build` or `lint`; return the exit code and the last 200 lines of output | 2 days |
| WPS Office and Microsoft Office | COM with `dynamic`, no interop assemblies | Spreadsheets: read and write cells and ranges, list sheets, save. Documents: read and insert text. Presentations: read slides. | 1.5 weeks |
| Slack | UI Automation recipe on the desktop app; hub to Slack's server where a Slack app is available | Read the open channel; draft a reply (see below) | 3 days |
| Linear | Hub, remote, OAuth | Create and read issues | 2 days |
| Notion | Hub, remote, OAuth | Create and read pages | 2 days |
| Discord | UI Automation recipe, read only | Read the open channel | 1 day |
| Unity | Hub to Unity's official MCP server or the community server | That server's tools, under Deskbridge's permissions | 3 days |
| Remote sign-in for the hub | OAuth with PKCE and a loopback redirect; Deskbridge's client ID is a metadata document on the docs site (the 2026-07-28 way), with dynamic registration for servers that still need it; tokens in Credential Manager | Sign in, refresh and sign out per server | 1.5 weeks |

**Slack drafts.** Putting text into Slack's message box without focus depends on whether its editor accepts UI Automation text input. Phase 4 starts with a one-day test. If it does not work, the draft appears on the confirmation card and is copied to the clipboard for the user to paste. Deskbridge never sends a Slack message without an Allow.

**After v1:** Blender (an own add-on with a closed function list), OBS (WebSocket), Photoshop (UXP plugin), DaVinci Resolve (Studio only), a Spotify adapter, Figma, Teams, classic Outlook, and community adapters.

## The Claude Code build loop

This is Nook task 5 (D9). Nook runs the loop; Deskbridge supplies each step.

1. **Look.** Nook reads the context: `vscode_list_problems`, the open file and selection (`vscode_active_editor`), and the git diff through Nook's existing git connector.
2. **Prompt.** `claude_code_start(project, prompt)` runs Claude Code headless in that folder. It returns a session ID, a summary and the changed files. The first use in a project asks the user.
3. **Test.** `project_check_run(project, "test")` and `vscode_list_problems` show whether the work holds.
4. **Prompt again.** `claude_code_continue(session, prompt)` passes on what failed.
5. **Stop** when the checks pass, after 5 rounds, when two rounds in a row change nothing, or when the user presses Stop. Nook reports what was built and what still fails.

Claude Code keeps its own permission rules throughout. Deskbridge never passes the flag that skips them, and never handles Anthropic credentials (D17). Checks are named commands the user approved for that project; there is no tool that runs an arbitrary command (D18).

## Resource budget

Deskbridge runs all day beside the user's apps, so it must cost almost nothing when no agent is using it. These limits are release gates, measured on a mid-range laptop. The last column is an estimate for C#; Phase 0 replaces it with measurements.

| Measure | Idle limit | Limit during a task | Expected for C# |
|---|---|---|---|
| Broker CPU | Under 0.5% of one core | Under 15% of one core on average | About 0% idle (no timers) |
| Broker memory | Under 80 MB | Under 200 MB | About 35 to 60 MB idle |
| Relay memory | Under 10 MB per client | Under 10 MB | About 4 to 8 MB |
| Start-up, relay to first tool list | Under 1 s | | About 0.2 to 0.4 s |
| One UI Automation action | | Under 300 ms | Set mostly by the target app |

**How:**

- No timers and no polling. Work happens only when a tool is called.
- UI Automation reads are scoped to one window and cached for the length of a task.
- One UI Automation worker thread (MTA), created once. COM adapters get their own STA thread.
- Adapters, Jint, WinForms and hub servers load on first use. Hub servers stop after 10 idle minutes.
- Chromium and Electron windows are never walked unless their adapter allows it.

## Stack

| Need | Choice | Version on 2026-11-22 |
|---|---|---|
| Runtime | .NET 10 LTS (supported to 2028-11), target `net10.0-windows10.0.19041.0` | 10.0 |
| MCP protocol | Official MCP C# SDK (`ModelContextProtocol`), stdio and stream transports | 2.4.0 |
| UI Automation | FlaUI.UIA3 behind `IUiaEngine`, AutoSetFocus off | 5.0.0 |
| Media session and notifications | Windows Runtime APIs through the built-in C#/WinRT projection | |
| Office and WPS | COM late binding with `dynamic`, on a dedicated STA thread | |
| Code Mode sandbox | Jint | 4.18.0 |
| Tray, confirmation and settings windows | WinForms on .NET 10 | |
| Secrets | Windows Credential Manager | |
| Audit log | JSON Lines files | |
| VS Code extension | TypeScript, VS Code extension API | |
| Tests | xUnit for unit tests; E2E tests on real apps | |
| Packaging | Relay: Native AOT. Broker: single file, self-contained, ReadyToRun. x64 and Arm64. A winget manifest, an MCP bundle (`.mcpb` with `server.type` "binary") for Claude Desktop, and an MCP registry entry. | |
| Signing | SignPath Foundation, from GitHub Actions | |
| CI | GitHub Actions on Windows runners | |

This PC has the .NET 11 SDK, which builds `net10.0`. CI pins the SDK with `global.json`.

## Repo layout

```text
deskbridge/
  src/
    Deskbridge.Relay/          deskbridge.exe: Native AOT relay and command line
    Deskbridge.Broker/         deskbridge-broker.exe: hosts everything below
    Deskbridge.Mcp/            MCP front: classic tools and Code Mode
    Deskbridge.Core/           policy, confirmations, pairing, router, audit log
    Deskbridge.Engines.Os/     files, apps, windows, clipboard, media, notifications, Settings
    Deskbridge.Engines.Uia/    UI Automation worker, find, read, act
    Deskbridge.Hub/            local and remote MCP servers, OAuth
    Deskbridge.Sdk/            adapter manifest, transports, test helpers
    Deskbridge.Ui/             tray, confirmation window, settings window
  adapters/
    office/  vscode/  claude-code/  slack/  discord/  unity-hub/  linear-hub/  notion-hub/
  extensions/
    vscode/                    TypeScript extension for the VS Code adapter
  tests/
    Deskbridge.Tests/          unit tests
    Deskbridge.E2E/            real apps, with the hands-off check
  docs/
    plan.md                    this file
    decisions.md               decision log
  site/                        docs website
```

## Using it inside Nook

Nook becomes one more MCP client, and Deskbridge replaces the PC part of Nook's action layer over time.

- **Connection.** Nook's MCP client starts `deskbridge.exe mcp` like any other MCP server. If a broker is already running for Claude Code or Cursor, Nook shares it.
- **Catalogue.** Nook gives each Deskbridge tool its own risk and consequence words. Deskbridge's `read`, `change` and `destructive` effects map onto Nook's risk levels (D22).
- **One card per action.** Nook pairs with Deskbridge once. For Deskbridge tools, Nook's policy still checks origin, rate limit and kill switch, then leaves the confirmation to Deskbridge's "input required" result, which Nook shows on its own card. Nook needs to support "input required" and elicitation for this.
- **Memory.** Nook resolves "the pricing sheet" or "my Unity project" from memory first, then passes the exact file or window to Deskbridge.
- **Shipping.** The Nook installer bundles Deskbridge. A user who already has Deskbridge keeps one copy, shared by both.
- **Who changes Nook.** The Nook side (its catalogue entry, "input required" support, the build loop) is built in Nook, never from this repo. Milestones 1 and 2 depend on it.
- **Moving actions over.** Nook's existing PC actions keep working, and each moves to Deskbridge once its Deskbridge version passes the same tests. Browser and messaging actions stay in Nook.

## Build plan

About 15 weeks to a public v1, assuming Claude Code builds and the owner reviews for 1 to 2 hours a day. Each phase starts only after the previous gate passes. Before each phase, Claude publishes an artifact of what the phase will build and logs any new decision (D2).

| Phase | Weeks | Builds | Gate |
|---|---|---|---|
| 0. Setup | Week 1 (3 days) | Repo, CI, relay and broker with `windows_list` and `ui_read`, the hands-off check, the SignPath application | CI green. The hands-off check catches a deliberate mouse move. A client on the older spec (Claude Desktop) and one on 2026-07-28 both list and call tools. `ui_read` reads text from a background Notepad with no focus change. Idle memory and CPU measured. |
| 1. Core | Weeks 1 to 3 | Windows engine, UI Automation engine, policy, client approval, confirmation window, audit log, tray, settings | An agent in Claude Desktop renames a file, reads a Notepad document and sets a value in Settings, with zero cursor or focus changes. Idle CPU under 0.5%. |
| 2. Adapter SDK and local hub | Weeks 4 to 5 | Manifest format; COM, CLI and pipe transports; plugin protocol with tokens; local hub; Code Mode | A sample adapter built only from the SDK works. A local MCP server runs behind the hub with confirmations. Code Mode runs a 3-step script. |
| 3. Nook milestone 1 | Weeks 5 to 6 | VS Code extension and adapter, spreadsheet functions for WPS and Office, Claude Code adapter, project checks, pairing and "input required" | Nook runs tasks 1, 2 and 5 (D9): opens a file at a line and lists problems; updates cells in a WPS sheet; runs a Claude Code build loop of at least 2 rounds on a sample project. |
| 4. Nook milestone 2 | Weeks 7 to 9 | Remote hub with OAuth, Linear and Notion, Slack read and draft, Discord read | Nook runs tasks 3 and 4: reads a Slack channel and drafts a reply; creates a Linear issue and a Notion page. |
| 5. Rest of v1 | Weeks 10 to 12 | Unity hub, documents and presentations for WPS and Office, Electron cost measurements, a task suite for every adapter | Every adapter passes its task suite on Windows 10 and 11. |
| 6. Release | Weeks 13 to 15 | Signed builds, winget, MCP registry, Claude Desktop bundle, docs site, demo video, security review | Clean install on fresh Windows 10 and 11 machines. Security review findings closed. |

**Faster option.** In Phases 4 and 5, the owner can run 2 or 3 separate Claude Code sessions in parallel, one adapter each, on separate branches. That can save about 2 weeks. Only one session may start or stop the broker.

## Testing

Every test runs on real apps, and every action must pass the hands-off check.

- **Hands-off check:** cursor position, foreground window and keyboard focus are recorded before and after each action. Any change fails, unless the action was rung 5 and the user approved it. The confirmation window is tested the same way: showing it must not change the foreground window.
- **Task suite per adapter:** 10 to 20 real tasks each, such as "add a row to this sheet", "open main.ts at line 40" or "create a Linear issue", with the result checked in the app.
- **Machines:** Windows 10 and 11, x64 and Arm64, at 100%, 150% and 200% scaling, one and two monitors, a non-admin user.
- **Resource tests:** an hour idle and an hour of scripted tasks against the budget.
- **Safety tests:** hostile text inside a document, a Slack message and a Notion page tries to make the agent delete or send something, and the confirmation must catch it. Password fields and secret windows return nothing. Discord refuses every change.
- **Spec tests:** one client on the older spec and one on 2026-07-28, each through the full tool list and a confirmation.
- **Clean install:** a fresh Windows Sandbox or evaluation VM with nothing preinstalled.
- **Test accounts only:** a test Slack workspace, Notion workspace, Linear workspace and Discord server. Never real contacts, and nothing sent to real people. The Office path is tested on a Microsoft 365 trial.

## Release and launch

Ship where Windows developers and AI users already look, and lead with a demo of the hands-off promise.

**Channels**

- GitHub Releases, with SignPath-signed builds
- winget, as `winget install <name>` once the name is chosen
- The official MCP registry
- Claude Desktop's extension directory, as an MCP bundle
- A docs site on Cloudflare Pages: install, permissions, every adapter, and how to write one

**Launch content**

1. A 60-second video. While the user keeps typing in another window, an agent has Claude Code fix a failing test, writes the result into a WPS sheet, and files a Linear issue. The mouse visibly never moves.
2. A short write-up on how the no-cursor rule works and why each app is reached its own way.
3. Posts on Hacker News (Show HN), r/ClaudeAI, r/LocalLLaMA and X, plus an adapter bounty list.

**Versioning:** semantic versions. Adapters declare the app versions they were tested with, and a monthly release runs the full task suite.

## Cost

| Item | Cost per year |
|---|---|
| Claude Code | In the existing Claude plan; extra only if usage limits are hit |
| Code signing (SignPath Foundation) | ₹0 |
| Domain for the docs site | About ₹1,000 |
| Docs hosting (Cloudflare Pages), public GitHub repo and Windows CI, MCP registry, winget | ₹0 |
| Test apps and accounts: VS Code, Unity Personal, WPS Office, and the free tiers of Slack, Notion, Linear and Discord | ₹0 |
| Microsoft 365 for testing the Office path | ₹0 on the one-month trial; a subscription only if testing runs longer |
| Test machines: Windows Sandbox and Microsoft's evaluation VMs | ₹0 |
| Standard code signing certificate for paid adapters | About ₹18,000, only once paid adapters exist |

## Risks

The biggest risk is time: this competes with Nook's own roadmap for the same review hours.

| Risk | Effect | Response |
|---|---|---|
| Splits review time with Nook | Both slip | Build to Nook milestone 1 (week 6) before Nook's next release, then continue |
| Acting without the cursor is already common | Deskbridge looks like one more UI Automation server | Lead with app-native adapters, per-app permissions and the hub |
| Microsoft ships broad native connectors | Overlap with built-in features | Register in Windows' agent registry once it is generally available; use Microsoft's connectors through the hub |
| MCP spec churn | Some clients fail to connect | The Phase 0 gate tests both spec versions; follow SDK releases |
| Slack's server needs a registered Slack app | Slack through the hub is blocked for most users | v1 reads Slack through UI Automation; the Slack app is an open decision |
| Discord's terms | User accounts flagged | Read only, enforced by the manifest (D8) |
| Electron apps cost CPU and memory under UI Automation | Slows the user's PC | Never walk them by default; measure each app; prefer extensions and servers |
| App updates break adapters | Tasks fail after an update | Tested versions in each manifest; monthly full-suite run |
| FlaUI stops being maintained | UI Automation fixes stall | `IUiaEngine` interface; FlaUI is a thin layer over the Windows API |
| Unity's MCP server is a beta | Tools change or disappear | The hub keeps the community server as a fallback |
| Claude Code CLI flags change | The build loop breaks | The adapter checks the installed version; its task suite covers the flags |
| Prompt injection through app content | An agent is tricked into a harmful action | `untrusted_text` field; confirmations for every change; destructive actions always ask |
| Stolen remote tokens | Someone acts in the user's Notion, Linear or Slack | Tokens in Credential Manager, never logged; sign-out removes them |
| A tool that can act in every app is misused | Reputation damage | No network listener; pipe limited to the current user; each client approved once |
| Name clash | Confusing search results | Pick a name before anything is public (see Open decisions) |

## Open decisions

Settle the first two before Phase 0 ends. The rest can wait until the phase that needs them.

- [ ] **Name.** "Deskbridge" is used by two macOS apps, and Microsoft's "Desktop Bridge" (its tool for packaging Win32 apps for the Store) will crowd search results. Pick a name and check the domain, winget ID and GitHub name before anything is public.
- [ ] **Where it lives.** Your personal GitHub account or a new organisation. SignPath needs a public repo, and an organisation keeps the product separate from personal projects.
- [ ] **Slack path.** Register a Deskbridge Slack app and list it in Slack's directory, which opens the official server to every user, or stay with UI Automation for v1.
- [ ] **Microsoft 365.** Test the Office path on the trial only, or buy a subscription.
- [ ] **Paid adapters.** Which ones, at what price, and how they are sold. Not needed before v1.
- [ ] **Nook bundling.** Whether Nook ships Deskbridge in its next release, or only after the public v1.

## Next step

When the owner approves this plan and the proposed decisions (D10 to D23), Claude writes the implementation plan for Phase 0, publishes an artifact of what Phase 0 builds, and waits for approval before writing code.

## Sources

- MCP spec 2026-07-28: [changelog](https://modelcontextprotocol.io/specification/2026-07-28/changelog), [announcement](https://blog.modelcontextprotocol.io/posts/2026-07-28/)
- MCP C# SDK: [v1.0 announcement](https://devblogs.microsoft.com/dotnet/release-v10-of-the-official-mcp-csharp-sdk/), [releases](https://github.com/modelcontextprotocol/csharp-sdk/releases)
- Client support for elicitation: [MCP client features](https://www.developersdigest.tech/blog/mcp-client-features-explained-2026), [Claude Desktop report](https://github.com/nuccio/google-chat-mcp/issues/63)
- UI Automation focus: [IUIAutomation2::put_AutoSetFocus](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationclient/nf-uiautomationclient-iuiautomation2-put_autosetfocus)
- Chromium and UI Automation: [Native UI Automation in Chromium](https://developer.chrome.com/blog/windows-uia-support-update)
- Competitors: [Windows-MCP](https://github.com/CursorTouch/Windows-MCP), [hands-off](https://github.com/MareAltaStudio/hands-off), [desk-mcp](https://github.com/DigitalDog1/desk-mcp), [Cua](https://github.com/trycua/cua), [UFO](https://github.com/microsoft/UFO), [MCP servers on Windows](https://learn.microsoft.com/en-us/windows/ai/mcp/servers/mcp-server-overview), [Experimental agentic features](https://support.microsoft.com/en-us/windows/ai/ai-features/experimental-agentic-features)
- Apps: [new and classic Outlook compared](https://support.microsoft.com/office/feature-comparison-between-new-outlook-and-classic-outlook-de453583-1e76-48bf-975a-2e9cd2ee16dd), [DaVinci Resolve 21.1 scripting change](https://www.pugetsystems.com/blog/2026/09/10/how-davinci-resolve-free-v21-1-scripting-changes-affect-puget-bench/), [Unity MCP](https://unity.com/blog/unity-ai-mcp-how-to-get-started), [Blender MCP](https://www.blender.org/lab/mcp-server/), [Notion MCP](https://developers.notion.com/docs/get-started-with-mcp), [Linear MCP](https://linear.app/docs/mcp), [Slack MCP server](https://docs.slack.dev/ai/slack-mcp-server/)
- Packaging and signing: [MCP bundles](https://github.com/modelcontextprotocol/mcpb), [Artifact Signing quickstart](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart), [Certum open-source code signing](https://shop.certum.eu/open-source-code-signing.html), [SignPath Foundation](https://signpath.org/)
- Windows 10 support: [free security updates extended to 2027-10-12](https://www.bleepingcomputer.com/news/microsoft/microsoft-quietly-extends-free-windows-10-esu-support-to-october-2027)
- Libraries: [FlaUI](https://github.com/FlaUI/FlaUI), [Jint](https://github.com/sebastienros/jint)
