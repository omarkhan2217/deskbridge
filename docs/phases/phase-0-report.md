# Phase 0 report

Date: 2026-11-22. Branch: `phase-0-setup`. Nothing is committed yet (D26): the owner reviews this report and the changes, then the work is committed one commit per task.

## Result

Every gate item that can be checked on this PC passes. Two items wait on the owner: CI on GitHub needs the repo location and a first push, and the SignPath application needs a public repo.

| Gate item | Result |
|---|---|
| `dotnet build Deskbridge.slnx -warnaserror` | Passed, 0 warnings |
| Every unit and end-to-end test passes, none skipped | Passed: 48 unit, 22 end-to-end, 0 skipped |
| The hands-off check catches a deliberate mouse move | Passed (`Catches_deliberate_mouse_move`) |
| A client on `2025-11-25` and one on `2026-07-28` list and call tools through the relay | Passed (`A_client_on_either_spec_reaches_the_tools_through_the_relay`, both versions) |
| `ui_read` reads a background window, Notepad and Calculator with no cursor, foreground or focus change | Passed |
| Idle budgets: broker CPU, broker memory, relay memory; warm start under 1 s | Passed (see "Idle cost"); cold start is 0.9 to 2.2 s, see below |
| Claude Code called `windows_list` through the relay | Passed |
| CI green on GitHub | Waiting: repo location and first push |
| SignPath application sent | Waiting: public repo |

## What was built

| Project | What it is |
|---|---|
| `src/Deskbridge.Core` | Names (one place to rename), the user-facing error type, and the hello frame the relay sends first. Native AOT compatible. |
| `src/Deskbridge.Engines.Os` | The window list, from window-manager calls only. |
| `src/Deskbridge.Engines.Uia` | The UI Automation worker (one MTA thread, `AutoSetFocus` off, 5 s and 2 s UI Automation timeouts, an 8 s backstop that replaces a stuck thread) and the text reader. |
| `src/Deskbridge.Mcp` | The two tools and one MCP server per connection. |
| `src/Deskbridge.Broker` | `deskbridge-broker.exe`: the per-user pipe, one session per client, single instance, idle exit. |
| `src/Deskbridge.Relay` | `deskbridge.exe`: the Native AOT relay every client starts. |
| `tests/Deskbridge.Tests` | Unit tests (run in CI). |
| `tests/Deskbridge.E2E` and `tests/Deskbridge.E2E.Target` | End-to-end tests with the hands-off probe, and a target app that opens without taking focus. |
| `scripts/measure-idle.ps1` | Idle cost measurement. |
| `.github/workflows/ci.yml` | Build, unit tests and the AOT relay publish on x64 and Arm64 runners. |

**Tools:** `windows_list` (open windows, front to back, with handle, title, process and PID) and `ui_read` (text of a control in any window, returned as `untrusted_text`). Both are marked read-only.

## Measurements

| Measure | Result | Budget |
|---|---|---|
| Relay size (Native AOT, win-x64) | 2.6 MB (2,567,680 bytes), no AOT or trim warnings | under 10 MB (CI check) |
| Cold start: relay start, broker start, handshake, tool list | 0.9 to 2.2 s over five runs | none set for Phase 0 (see "Idle cost") |
| Warm start: relay start to tool list, broker already running | 128 ms | under 1 s |
| A hung app | `ui_read` answers "The app did not answer within 5 seconds" in about 5 s; the next call to a healthy window works within 3 s | fail within 8 s |

### Idle cost

Measured with `scripts/measure-idle.ps1 -Minutes 5`: one client connected (handshake and `tools/list` done), then 10 s to settle and 5 minutes of sampling every 10 s. Broker built in Release (not yet single-file or ReadyToRun), relay published with Native AOT.

| Measure | Measured | Budget | Verdict |
|---|---|---|---|
| Broker idle CPU | 0.016% of one core (47 ms of CPU in 5 min) | under 0.5% | within budget |
| Broker idle memory, peak working set | 43 MB | under 80 MB | within budget |
| Broker private bytes | 9.7 MB | | |
| Relay memory, peak working set | 9.9 MB | under 10 MB | within budget, barely |
| Relay private bytes | 3.2 MB | | |
| Relay idle CPU | 0% | | |

The relay's working set is mostly shared Windows code: its own private memory is 3.2 MB. The budget should be read against private bytes from Phase 1 on, or the relay will look over budget on any PC whose shared pages happen to be counted larger.

**Cold start is the weak spot.** Five measurements of relay start, broker start, handshake and tool list: 909, 890, 1,352, 1,993 and 2,176 ms. Warm start (broker already running) is 128 ms and meets the gate. A client that is the first after sign-in, or the first after the broker's 30-minute idle exit, waits 1 to 2 s. Phase 1 candidates, cheapest first: publish the broker ReadyToRun (removes most start-up JIT), create the UI Automation worker on first use instead of at start, and the "start at sign-in" setting already planned.

## The real client check

Claude Code 2.1.303, headless, with a temporary MCP config in the scratchpad (the owner's settings were not touched):

> There are 13 windows open, including Edge, Chrome, two VS Code windows, two Unity windows, Claude, GitHub Desktop, Unity Hub and four File Explorer windows.

## Problems met and how they were handled

1. **The .NET 10+ SDK no longer runs xUnit v3 through VSTest.** The repo opts into the Microsoft Testing Platform in `global.json`. Test commands are now `dotnet test --project <path>`.
2. **`dotnet sln add` in SDK 11 fails** when a new project references one already listed. `Deskbridge.slnx` is edited by hand.
3. **The hands-off probe blamed the owner's own mouse movement on Deskbridge.** Windows stamps input with a 15.6 ms tick, so "the last-input time changed" missed movement inside one tick. The probe now treats any input stamped after the first snapshot as the user's, waits for 250 ms of quiet before each attempt, and allows 5 attempts. Input that Deskbridge injected would also count, so such a bug still fails the test ("inconclusive"); it can never pass.
4. **Store apps detach their controls while minimized, and take activation back when restored.** Notepad is read while minimized; Calculator is read while Notepad sits in front of it. Both tests assert the target is not the foreground window.
5. **Closing a Store app with `WM_CLOSE` can leave it running and hidden.** The real-app fixture refuses to run if Notepad or Calculator is already open, and ends only the processes it started.
6. **Native AOT publish needs `vswhere.exe` on PATH.** Local publishes add the Visual Studio Installer folder; the CI workflow does the same.
7. **A hung app returned the SDK's opaque error** (found in the final review). UI Automation's own 5 s timeout fires before Deskbridge's 8 s backstop and surfaces as `System.TimeoutException`. The reader now maps it, and any other COM error, to a plain message. Pinned by `Hung_window_gives_a_plain_timeout_error`.

## Deferred minor findings

- A client path long enough to push the hello past 4,095 bytes would make the relay exit with an unhandled error.
- An idle exit that fires at the very moment a client connects ends that new session; the client's next start launches a fresh broker.
- FlaUI.Core contains mouse and keyboard helpers. Nothing calls them, and the banned-call search covers only Deskbridge's own source. A test that checks Deskbridge's assemblies never reference them would make this permanent.

## What Phase 1 needs to know

- `AutoSetFocus` off works: reading a background window changed nothing the user owns.
- Minimized Store apps (Calculator, Settings) have no controls to read. Phase 1's tools need a plain message for this, and acting on such an app needs rung 5 (bring to front, after asking).
- End-to-end tests open, minimize and close real windows. Run them when the owner is not working, or expect retries while they are.
- Claude Desktop is installed on this PC (a "Claude" window was open), so the Phase 1 gate can use it.
- The broker currently creates its UI Automation worker at start-up; Phase 1 can make it lazy if idle memory needs the room.

## Owner actions before the gate closes

1. Name: keep "Deskbridge" for now, or choose the final name.
2. Repo location: the personal account `alikhan866` or a new organisation, and a go for the first push so CI runs.
3. SignPath: apply once the repo is public.
