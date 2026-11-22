# Phase 1 report

Date: 2026-11-22. Branch: `phase-1-core`. Nothing is committed yet (`CLAUDE.md` rule 5): the owner reviews this report and the changes, then the work is committed one commit per task.

## Result

Every gate item that can be checked on this PC passes. CI waits on the owner's review, because the work is pushed only after it is committed.

| Gate item | Result |
|---|---|
| All unit and end-to-end tests pass, none skipped; 0 build warnings | Passed: 139 unit, 65 end-to-end, 0 skipped, 0 warnings |
| The gate scenario passes with zero cursor or focus changes during agent calls | Passed (`Phase1GateTests`, 3 tests, through the AOT relay) |
| Idle with the tray loaded: broker CPU, broker memory, relay private bytes | Passed (see "Measurements") |
| Cold start measured with the ReadyToRun broker (target: median under 1 s) | Passed: median 736 ms over 5 runs |
| A real agent ran the three gate steps | Passed on the second run (see "The real agent run") |
| CI green on GitHub | Waiting: the owner's review, then commit and push |
| `phase-1-report.md` written; README, LICENSE and code signing policy ready for the SignPath application (D41) | Done; the application follows the push |

## What was built

| Project | What Phase 1 added |
|---|---|
| `src/Deskbridge.Core` | The tool pipeline (policy, confirmation, run, one audit line), app identity, the config store, the policy engine, never-available apps, destructive words, the audit log and client approval. |
| `src/Deskbridge.Engines.Os` | Files (deletes go to the Recycle Bin), installed and running apps, launching without activation, window descriptions, clipboard, media, volume, Settings pages, window messages, publisher checks with no network call. |
| `src/Deskbridge.Engines.Uia` | Finding controls, the control tree, and press, toggle, select, set text and scroll through UI Automation patterns, then window messages. Every action reads its control again and refuses if it changed. |
| `src/Deskbridge.Ui` | The confirmation window that never takes focus, the tray icon, the pause hotkey (Ctrl+Alt+Shift+P) and the settings window. |
| `src/Deskbridge.Mcp` | 25 tools in five groups, each through the pipeline. |
| `src/Deskbridge.Broker` | Config, policy, tray and notifier wired in; "keep running" re-arms the idle timer. |
| `src/Deskbridge.Relay` | Skips shells (cmd, PowerShell, bash) to find the real MCP client. |
| `scripts/publish-broker.ps1` | Publishes the broker ReadyToRun and the relay with Native AOT. |
| `scripts/measure-idle.ps1` | Now times 5 cold starts, uses a temporary config folder and reports relay private bytes. |

**Tools:** `windows_list`, `windows_describe`, `ui_read`, `ui_tree`, `ui_find`, `ui_press`, `ui_toggle`, `ui_select`, `ui_set_text`, `ui_scroll`, `apps_list_installed`, `apps_list_running`, `apps_launch`, `settings_open`, `files_search`, `files_read`, `files_write`, `files_move`, `files_delete`, `clipboard_read`, `clipboard_write`, `media_now_playing`, `media_control`, `volume_set`, `notify_show`.

## Measurements

Measured with `scripts/measure-idle.ps1`: the published relay starts its own ReadyToRun broker on a private pipe, completes the MCP handshake and `tools/list`, then sits idle with the tray icon loaded.

| Measure | Result | Budget | Verdict |
|---|---|---|---|
| Cold start, relay launch to `tools/list` answer | median 736 ms (709, 723, 736, 743, 921) | under 1 s | within budget |
| Broker idle CPU over 5 min | 0% of one core | under 0.5% | within budget |
| Broker peak working set | 56.1 MB | under 80 MB | within budget |
| Broker private bytes | 13.3 MB | | |
| Relay private bytes | 3.3 MB | under 10 MB (D38) | within budget |
| Relay peak working set, mostly shared Windows code | 9.8 MB | | |
| Relay size, Native AOT | 2.5 MB | under 10 MB | within budget |
| Broker folder, self-contained ReadyToRun | 178.7 MB in 291 files | | |

Phase 0 measured cold starts of 0.9 to 2.2 s. ReadyToRun and creating the UI Automation worker on first use brought the median under 1 s. The runs were made with a warm disk cache, so the first start after a reboot can be slower.

## The real agent run

Claude Code 2.1.303, headless, with only the Deskbridge tools and a temporary MCP config and Deskbridge config (the owner's settings were not touched). A helper set up the scene as the gate test does (Settings at Date & time, Notepad in front of it), pressed Allow in each Deskbridge confirmation through UI Automation, and logged the cursor, the foreground window and the keyboard focus every 50 ms.

| Run | Rename | Read Notepad | Clock seconds on and off | Cursor, foreground, focus changes |
|---|---|---|---|---|
| 1 | Done | Done | Stopped: Settings was minimized | 149, 16, 20, each during or within 150 ms of the owner's own input |
| 2 | Done | Done | Done: expanded "Show more settings", turned it on, checked, turned it off | 0, 0, 0 (no user input during the run) |

In run 1 the owner was using the PC and minimized Settings by clicking its taskbar button. Deskbridge refused to act in the minimized Store app and did not restore it, because restoring takes focus; the agent stopped and said so. Run 2 took 67 s and 6 confirmations (client approval, the rename, the expander, and three toggles). The registry value `ShowSecondsInSystemClock` was 0 before and after.

The agent reported three problems in run 2. All three are fixed and covered by tests (D45); the agent has not been run again on the fixed build.

1. `ui_find` matched whole names only, and the Settings switch is named "Show seconds in system tray clock (uses more power)".
2. `ui_read` gave a checkbox's label but not whether it was ticked.
3. Asking `ui_toggle` for the state a switch already had still showed a confirmation.

## Problems met and how they were handled

1. **MCP clients start servers through `cmd /c`.** The relay saw `cmd` as its client, so every client was asked about as "cmd". The relay now skips up to 4 shells to find the real client.
2. **Two confirmation windows could overlap.** The presenter now returns an answer only after the window's handle is destroyed.
3. **Notepad's window took longer than 2.5 s to appear after a launch.** Launches wait up to 8 s for a new window or a foreground change, then 500 ms for activation to settle (D39).
4. **`ui_find` broke its own JSON** with raw window handles. Handles are formatted like `0x1A2B`, and any unexpected exception now becomes a plain tool error that names it.
5. **Deskbridge asked the user to approve a press on a text label.** A WinForms label has its own window, so the window-message rung counted it as pressable. Actions now check what the control supports before asking, and window messages go only to buttons and edit boxes (D43).
6. **Windows moved the clock seconds switch.** On build 26200 it is under Date & time, not Taskbar behaviors (D42). Settings repeats the name "Show more settings" on several buttons, so `ui_tree` nodes now name their parent (D44).
7. **UI actions read the target control before the policy ran** (found in the final review). An app set to Off, or a paused Deskbridge, still had its controls read, and a refusal for a control that could not be pressed wrote no audit line. The pipeline now decides the look as a read first, and every path writes one audit line.
8. **WinForms hides raw child windows from UI Automation, and combo box items can be missing while collapsed.** The test app's legacy control is a Button-class window with no pattern, and the select test uses a list box.
9. **End-to-end tests compete with the owner for the foreground.** Setup steps that need a window in front retry and note it; the hands-off checks themselves never relax.

## Deferred minor findings

- `files_search` runs synchronously and cannot be cancelled; a pattern that matches nothing under a large folder can take a long time.
- `files_move` and `files_delete` check secret names on a folder itself, not on the files inside it. Deletes still go to the Recycle Bin and always ask.
- An element found more than 60 s earlier can be dropped from the cache while its confirmation is open (up to 2 min), giving "element ID is no longer known" after the user said Allow.
- `ui_find` cannot search by what a control supports; Settings switches are Buttons with a Toggle pattern, so `control_type: checkbox` finds none of them.
- File tools accept UNC paths, so a file call can reach a network share the user's account can already open.
- The audit log and confirmations name Store Notepad "Notepad.exe" (its file description).

## What Phase 2 needs to know

- The pipeline has two entry points: `RunAsync` for calls that know their target, and `RunPreparedAsync` for calls that must look at the app first. Adapters that read an app before describing the change use the second one.
- Store apps hide their controls while minimized, and restoring them takes focus. Agents get a plain message asking the user to restore the window.
- Settings and other XAML apps repeat control names and nest switches inside collapsed expanders. Agents found them through `ui_tree` and its `parent` field.
- End-to-end tests open real windows; run them when the owner is not working, or expect retries.

## Owner actions before the gate closes

1. Review the changes and this report, and approve or change D38 to D45.
2. After the review: commit one commit per task, merge to `main`, push, and check CI.
3. Apply to SignPath Foundation (D41).
