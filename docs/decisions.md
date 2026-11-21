# Decision log

Every decision about Deskbridge, oldest first. An entry is never deleted or rewritten. When a decision changes, a new entry supersedes the old one and names it, and the old entry's status changes to "superseded by Dn".

**Status values**

- **accepted**: the owner made the decision, or approved Claude's proposal.
- **proposed**: Claude's call, written into `docs/plan.md` or a phase plan in `docs/phases/`, waiting for the owner's review.
- **superseded by Dn**: replaced by a later entry.

## Index

| ID | Date | Decision | Status | By |
|---|---|---|---|---|
| D1 | 2026-11-22 | The six product principles from the PDF draft are the product's rules | accepted | owner |
| D2 | 2026-11-22 | Working rules: no subagents, artifact and log before acting, writing rules, file conventions | accepted | owner |
| D3 | 2026-11-22 | Build in C# on .NET 10 LTS | accepted | owner |
| D4 | 2026-11-22 | Open core: MIT core, paid adapters possible later | accepted | owner |
| D5 | 2026-11-22 | Sign the open-source core through SignPath Foundation | accepted | owner |
| D6 | 2026-11-22 | v1 covers developer, documents and work-chat apps; creative apps come after v1 | accepted | owner |
| D7 | 2026-11-22 | The hub also proxies remote MCP servers; principle 5 is restated | accepted | owner |
| D8 | 2026-11-22 | Discord is read only | accepted | owner |
| D9 | 2026-11-22 | Five tasks Nook must run through Deskbridge | accepted | owner |
| D10 | 2026-11-22 | Split the Nook milestone in two: local tasks, then remote and chat tasks | accepted | Claude, approved by owner |
| D11 | 2026-11-22 | One broker process per user, reached through a small relay per client | accepted | Claude, approved by owner |
| D12 | 2026-11-22 | Deskbridge's own no-focus window confirms changes; only paired clients show their own card | accepted | Claude, approved by owner |
| D13 | 2026-11-22 | Target MCP spec 2026-07-28 and keep working with 2025-11-25 clients | accepted | Claude, approved by owner |
| D14 | 2026-11-22 | FlaUI for UI Automation, with AutoSetFocus switched off, behind an engine interface | accepted | Claude, approved by owner |
| D15 | 2026-11-22 | Tool names use underscores, not dots | accepted | Claude, approved by owner |
| D16 | 2026-11-22 | Tray, confirmation and settings windows in WinForms instead of WinUI 3 | accepted | Claude, approved by owner |
| D17 | 2026-11-22 | Claude Code is driven through its headless CLI, never by typing into its panel | accepted | Claude, approved by owner |
| D18 | 2026-11-22 | Project checks are named commands the user approves per project; no free-form shell | accepted | Claude, approved by owner |
| D19 | 2026-11-22 | Outlook leaves the v1 list | accepted | Claude, approved by owner |
| D20 | 2026-11-22 | Spotify playback is covered by core media control; a Spotify adapter comes after v1 | accepted | Claude, approved by owner |
| D21 | 2026-11-22 | Community adapters in v1 are manifests, recipes and hub entries; no third-party DLLs | accepted | Claude, approved by owner |
| D22 | 2026-11-22 | Nook starts on classic tools through its connector catalogue; Code Mode for Nook comes later | accepted | Claude, approved by owner |
| D23 | 2026-11-22 | Windows 10 stays in the test matrix | accepted | Claude, approved by owner |
| D24 | 2026-11-22 | Plan every phase before building; building starts with Phase 0 once the plans exist | accepted | owner |
| D25 | 2026-11-22 | Phase plans live in `docs/phases/`; Phase 0 step by step, later phases task by task, refined before each starts | proposed | Claude |
| D26 | 2026-11-22 | One branch per phase; no commits until the owner reviews at the phase gate | proposed | Claude |
| D27 | 2026-11-22 | Create each project when its phase needs it; `Deskbridge.Core` stays Native AOT compatible | proposed | Claude |
| D28 | 2026-11-22 | Broker idle exit and test isolation (pipe suffix) are built in Phase 0 | proposed | Claude |
| D29 | 2026-11-22 | End-to-end tests use a purpose-built target app plus Notepad and Calculator | proposed | Claude |
| D30 | 2026-11-22 | VirtualBox with Microsoft evaluation images replaces Windows Sandbox; Arm64 runs on GitHub's Arm64 runners | proposed | Claude |
| D31 | 2026-11-22 | Long-running tools use start, then wait | proposed | Claude |
| D32 | 2026-11-22 | The OAuth callback listens on 127.0.0.1 only during sign-in | proposed | Claude |
| D33 | 2026-11-22 | The WebSocket transport waits until an adapter needs it | proposed | Claude |

## Entries

### D1. The six product principles are the product's rules

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner (from the PDF draft)
- **Decision:** Hands off; safe by default; the app's own way first; light; local only; works with any agent. When two conflict, the earlier one wins.
- **Note:** Principle 5 is restated by D7.

### D2. Working rules for every session

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Decision:** Never use subagents. Before any decision or feature, publish an artifact showing what will be done and log the decision here, then wait for approval. No em dashes and no stock AI phrasing in anything written. Files follow the conventions in `CLAUDE.md`.
- **Where it lives:** `CLAUDE.md`.

### D3. Build in C# on .NET 10 LTS

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Options:**

  | Option | For | Against |
  |---|---|---|
  | C# on .NET 10 | Calls Office and WPS through COM directly (`dynamic`); official MCP C# SDK 2.4 in the top SDK tier; Jint for Code Mode; FlaUI for UI Automation | About 35 to 60 MB idle (estimate); Native AOT is ruled out by COM; different language from Nook |
  | Python | Could share code with Nook's Python backend | Single-exe start likely misses the 1 s budget; Python COM wrappers can break when a Windows update replaces the UI Automation DLL |
  | Rust | About 5 to 15 MB idle; could be built into a Rust app shell later | Core about 2 to 3 weeks longer; Office/WPS adapter about twice as long; hand-written COM late binding; rmcp is a lower-tier SDK |

- **Reason:** C# meets every budget and is the fastest path to the Nook milestone. Rust's advantages do not change any release gate, and the Office/WPS adapter the owner chose is where Rust costs most.

### D4. Open core

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Options:** MIT; closed source; open core.
- **Decision:** The core, the SDK and the v1 adapters are MIT. Paid adapters may be added after v1.
- **Consequence:** Paid adapters must ship as separate packages with their own signing certificate (see D5). How they are licensed and sold is an open decision in `docs/plan.md`.

### D5. Sign the core through SignPath Foundation

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Options:** SignPath Foundation (free); Certum open-source certificate (about ₹7,500 a year); Certum standard certificate (about ₹18,000 a year). Azure Artifact Signing is not open to individuals outside the US and Canada.
- **Decision:** SignPath signs the open-source core from GitHub Actions.
- **Consequences:** Windows shows the publisher as "SignPath Foundation". The repo must be public under an OSI licence, builds must be fully automated, and every maintainer needs MFA on GitHub and SignPath. Releases carry the line "Free code signing provided by SignPath.io, certificate by SignPath Foundation." Paid adapters cannot be signed this way; they need a standard certificate when they exist.

### D6. v1 app groups

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Decision:** v1 covers developer apps (VS Code, Unity), documents (WPS Office and Microsoft Office), and work chat (Slack, Discord, Notion, Linear), on top of the core (File Explorer, Settings, windows, clipboard, media).
- **Moved after v1:** Blender, OBS, Photoshop, DaVinci Resolve.
- **Facts behind it:** This PC has WPS Office, not Microsoft Office; DaVinci Resolve 21.1 removed scripting from the free edition; Blender's official MCP needs Blender 5.1 (this PC has 5.0) and runs model-written code with no checks.

### D7. The hub proxies remote MCP servers

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Options:** local MCP servers only; local and remote.
- **Decision:** The hub can start local MCP servers and connect to remote ones (Notion, Linear, Slack), with Deskbridge's permissions and confirmations on top.
- **Principle 5 becomes:** "Local first. Deskbridge's own code makes no network calls and sends no telemetry. The only traffic is to remote MCP servers the user added, each shown as a network connector with its domain. Logs stay on the PC."
- **Consequence:** Deskbridge becomes an OAuth client: sign-in through the browser, tokens kept in Windows Credential Manager, refresh handled by Deskbridge. About 1 to 2 weeks of extra work.

### D8. Discord is read only

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Options:** read only; read and act; leave it out.
- **Decision:** Deskbridge can read the open Discord channel through UI Automation, with the user's permission. The Discord adapter declares no change or send functions, so the policy cannot allow them.
- **Reason:** Discord's terms ban automating user accounts, and acting through the app could get the user's account flagged.

### D9. Tasks Nook must run through Deskbridge

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Decision:** Nook must be able to:
  1. VS Code: open a file at a line and list the problems VS Code reports.
  2. Spreadsheet: read or update cells in WPS or Excel.
  3. Slack desktop: read a channel and draft a reply.
  4. Linear or Notion: create an issue or a page.
  5. Claude Code: look at the context, prompt Claude Code, test what it built, and prompt it again until the work is done.
- **Note:** The PDF asked for 3 tasks at week 5. Five tasks do not fit there; see D10.

### D10. Two Nook milestones

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** Milestone 1 (end of week 6) covers the local tasks: VS Code, spreadsheet, and the Claude Code loop. Milestone 2 (end of week 9) covers the tasks that need sign-in or chat apps: Linear or Notion, and Slack.
- **Reason:** The remote tasks need the OAuth work from D7, which is the largest single addition. Putting the local tasks first gives Nook something real three weeks earlier.

### D11. One broker per user, one relay per client

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Options:** each client starts its own full Deskbridge (the PDF's stdio model); one shared process reached through a relay.
- **Decision:** `deskbridge.exe` is a small Native AOT relay that each MCP client starts over stdio. It connects to `deskbridge-broker.exe` over a named pipe that only the current Windows user can open, and starts the broker if it is not running. The broker holds the UI Automation worker, the policy, the tray icon and the audit log.
- **Reason:** With three clients open, the PDF's model means three tray icons, three permission states and three UI Automation workers. A shared broker keeps one of each. The relay does no COM work, so it can be compiled ahead of time and stay under 10 MB.
- **Supersedes:** the PDF's "one self-contained .exe". The install is two .exe files in one folder.

### D12. Who shows confirmations

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** By default, Deskbridge shows its own confirmation window. The window never takes focus (`WS_EX_NOACTIVATE`), shows the app, the action and the exact content, and denies after 2 minutes with no answer. A client the user has paired with Deskbridge (Nook first) can show the confirmation on its own card instead: Deskbridge returns an "input required" result, the client asks the user, and the approval comes back bound to that exact action.
- **Reason:** Claude Desktop does not support MCP elicitation. The 2026-07-28 spec replaced elicitation with "input required" plus a retry, and clients are still catching up. A client can also answer a question without a person seeing it (for example, Claude Code running headless). Only Deskbridge's own window proves a person saw the question, so it is the default.
- **Supersedes:** the PDF's "confirmation through MCP elicitation, with a Windows notification as fallback".

### D13. MCP spec version

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** Build on the MCP C# SDK 2.x (2.4.0 on 2026-11-22), which implements the 2026-07-28 spec. Phase 0 must prove that clients still on 2025-11-25 (Claude Desktop is the main one) can connect and call tools.
- **Reason:** New clients speak the stateless spec; Claude Desktop and many others do not yet.

### D14. UI Automation library

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** Use FlaUI.UIA3 5.0.0. After creating the automation object, cast it to `IUIAutomation2` and set `AutoSetFocus` to false. All UI Automation calls go through an `IUiaEngine` interface so FlaUI can be replaced.
- **Reason:** FlaUI creates `CUIAutomation8` but never switches off `AutoSetFocus`, and UI Automation takes focus before Invoke and SetValue by default. An earlier prototype measured this: typing into a background window pulled the user's window away until `AutoSetFocus` was cleared. FlaUI's last release was 2025-02, so the interface limits the risk if it stops being maintained.

### D15. Tool names use underscores

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** `files_read`, `ui_press`, `vscode_open_file`, not `files.read`.
- **Reason:** The Claude API accepts tool names made of letters, digits, `_` and `-` only. Dots risk breaking clients that pass MCP tools to the API unchanged.

### D16. WinForms for the small windows

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** The tray icon, the confirmation window and the settings window use WinForms on .NET 10 (with dark mode), loaded the first time one is needed.
- **Reason:** WinUI 3 has no tray icon, needs the Windows App SDK runtime, and makes a single-file release larger and harder. WinForms is part of .NET, has `NotifyIcon`, and costs nothing until a window opens.
- **Supersedes:** the PDF's "a small WinUI 3 window".

### D17. Claude Code through its headless CLI

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** The Claude Code adapter runs the user's own installed `claude` CLI in print mode with streamed JSON output, in the project folder, and continues the same session with `--resume`. It never types into the Claude Code panel in VS Code. Deskbridge never sees or stores Anthropic credentials, and never passes the flag that skips Claude Code's own permission checks.
- **Reason:** Principle 1 forbids typing into the user's window, and principle 3 says to use the app's own interface first. The CLI returns structured results and a session ID, so Nook can follow up. The user can open the same session in VS Code later.

### D18. Project checks

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** To test what Claude Code built, Deskbridge runs named checks (for example `test`, `build`, `lint`) declared per project in `.deskbridge/checks.json` or suggested from `package.json`, `*.csproj` or `pyproject.toml`. The user approves each check once per project. There is no tool that runs an arbitrary command.
- **Reason:** A tool that runs any command would hand an agent the whole PC. Named, approved checks give the build loop what it needs without one.

### D19. Outlook leaves v1

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** No Outlook adapter in v1.
- **Reason:** The new Outlook for Windows has no COM, VBA or object model, and it is what this PC runs. Classic Outlook keeps COM until at least 2029 but is being replaced. Nook already reaches email through its own connectors.

### D20. Spotify through core media control

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** The core `media_control` and `media_now_playing` tools use the Windows media session, which covers Spotify playback. A Spotify adapter (search, queue, playlists) comes after v1.
- **Reason:** Spotify is not installed here, and the owner did not pick it for v1.

### D21. What community adapters can be in v1

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** In v1 a community adapter is a manifest plus UI Automation recipes (data only), or a hub entry for an existing MCP server. Deskbridge does not load third-party DLLs. Script adapters that run in the Jint sandbox with granted transports are designed in Phase 2 and may ship after v1.
- **Reason:** A DLL loaded into the broker runs with every right the broker has, and its code would sit between the agent and every app.

### D22. How Nook uses Deskbridge first

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** Nook connects through its existing MCP client and tool catalogue, and calls Deskbridge's classic tools, each with Nook's own risk and consequence words. Code Mode for Nook waits until Nook can run scripts.
- **Reason:** The PDF assumed Nook loads Deskbridge in Code Mode into a script catalogue. Nook has no Code Mode or script catalogue today; its planner uses only declared tools.
- **Supersedes:** the PDF's "Catalog: Nook loads Deskbridge in Code Mode".

### D23. Windows 10 stays in the test matrix

- **Date:** 2026-11-22
- **Status:** accepted (owner approved 2026-11-22)
- **By:** Claude
- **Decision:** Test on Windows 10 and Windows 11.
- **Reason:** Microsoft extended free consumer security updates for Windows 10 to 2027-10-12, so many users will still be on it through 2027.

### D24. Plan every phase before building

- **Date:** 2026-11-22
- **Status:** accepted
- **By:** owner
- **Decision:** The owner approved D10 to D23 and asked that every phase be planned (goal, what it needs, what it builds, how it is checked) before any code is written. Building starts with Phase 0 once the plans exist.

### D25. Where phase plans live and how detailed they are

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** `docs/phases/README.md` is the roadmap. Each phase has its own file (`phase-0-setup.md` to `phase-6-release.md`). Phase 0 is written step by step, ready to build. Phases 1 to 6 are written task by task (files, interfaces, tests, gate) and are refined to step level, with an artifact, just before each phase starts.
- **Reason:** Later phases build on interfaces and measurements that Phase 0 produces. Writing their code-level steps now would mean rewriting them after Phase 0.

### D26. Branches and commits

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** Each phase is built on its own branch (`phase-0-setup`, `phase-1-core`, and so on). Nothing is committed during the phase. At the gate the owner reviews the work, then it is committed as one commit per task and merged.
- **Reason:** `CLAUDE.md` rule 5 forbids commits before review, and per-task commits keep the history readable.

### D27. Projects appear when needed; Core stays AOT compatible

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** A project is created in the phase that first puts code in it; there are no empty shells. `Deskbridge.Core` sets `IsAotCompatible` and must never take a dependency that breaks Native AOT, because the relay compiles it ahead of time.

### D28. Idle exit and test isolation in Phase 0

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** The broker exits 30 minutes after its last client disconnects (one timer, started only when the count reaches zero). The environment variable `DESKBRIDGE_PIPE_SUFFIX` adds a suffix to the pipe and mutex names, and `DESKBRIDGE_IDLE_EXIT_MS` overrides the idle time, so tests run their own broker and clean it up.
- **Reason:** Without both, end-to-end tests would share or leave behind the user's real broker.

### D29. What the end-to-end tests drive

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** A purpose-built WinForms app (`Deskbridge.E2E.Target`) opens without taking focus and holds known text, so "read a window that is not in front" is deterministic. Notepad and Calculator are also tested, minimized with `SW_SHOWMINNOACTIVE` before the hands-off snapshot.
- **Reason:** This PC's foreground lock timeout is at its maximum (measured on this PC), so a test cannot reliably put another window in front once Notepad has taken focus.

### D30. Test machines

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** Clean-install and Windows 10 tests run in VirtualBox (already installed) with Microsoft's evaluation images. Arm64 builds and unit tests run on GitHub's Windows Arm64 runners; Arm64 end-to-end tests need an Arm64 device or a rented Arm64 VM, decided in Phase 5.
- **Reason:** Windows Sandbox needs Windows Pro or Enterprise, and this PC runs Windows 11 Home.
- **Supersedes:** the plan's "Windows Sandbox" for clean installs.

### D31. Long-running tools use start, then wait

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** A tool that can run for minutes (`claude_code_start`, `claude_code_continue`, `project_check_run`) returns a run ID at once. `run_wait(run_id, timeout_s)` returns progress or the final result, waiting at most 120 s per call.
- **Reason:** MCP clients time out long tool calls, and a Claude Code run can take many minutes.

### D32. The OAuth callback listener

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** During a remote sign-in only, Deskbridge listens on `127.0.0.1` on a random port for the browser's redirect, then closes the listener. This is the one exception to "no TCP listener".
- **Reason:** Native-app OAuth (RFC 8252) needs a loopback redirect.

### D33. WebSocket transport waits

- **Date:** 2026-11-22
- **Status:** proposed
- **By:** Claude
- **Decision:** The manifest format reserves the `websocket` transport, but it is built with the first adapter that needs it (OBS, after v1).
- **Reason:** No v1 adapter uses it.
