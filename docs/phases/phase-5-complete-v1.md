# Phase 5: Rest of v1 plan

> Task-level plan (D25). Refined to step level, with an artifact, before Phase 5 starts. No subagents.

**Goal:** Finish the v1 app list and prove every adapter on Windows 10 and 11, at three display scales, with one and two monitors, as a non-admin user.

**Weeks:** 10 to 12.

**Spec:** `docs/plan.md` (v1 apps, Testing, Resource budget) and D23, D30.

## Needs before it starts

- Phase 4 gate passed.
- A Microsoft 365 trial (Excel, Word, PowerPoint) on this PC or a VM.
- A Windows 10 evaluation VM in VirtualBox, and a fresh Windows 11 evaluation VM.
- A decision on Arm64 end-to-end testing: an Arm64 device, a rented Arm64 VM, or Arm64 unit tests only for v1.
- A small Unity 6 test project.

## Tasks

### Task 1: Unity through the hub

- **Files:** `adapters/unity-hub/manifest.json`
- **Interfaces:** a hub entry for Unity's official MCP server (how it starts is checked against Unity's docs at phase start) with the community server as a fallback entry; scene changes ask; build and delete actions are Destructive.
- **Tests (end to end, test project):** list scene objects; create an object after Allow; the fallback entry works when the official one is not installed.

### Task 2: Documents and presentations

- **Files:** `adapters/office/DocFunctions.cs`, `SlideFunctions.cs`
- **Interfaces (tools):** `doc_read_text(file, max_chars=20000)`, `doc_insert_text(file, position: start|end|after_text, text, anchor?)` Change; `slides_list(file)` returning each slide's title and text. Writer and Word through `KWPS.Application` and `Word.Application`; Presentation and PowerPoint through `KWPP.Application` and `PowerPoint.Application`.
- **Tests (end to end):** each function on WPS and on the Microsoft 365 trial, open and closed files, inside the hands-off probe.

### Task 3: Microsoft Office pass

- **Behaviour:** run every spreadsheet, document and presentation test from Phases 3 and 5 against Microsoft 365. Differences between WPS and Office are fixed in the adapter or recorded in `adapters/office/README.md`.

### Task 4: Task suites

- **Files:** `tests/Deskbridge.E2E/Suites/<adapter>/` with 10 to 20 tasks each; `scripts/run-suites.ps1`
- **Behaviour:** each task states its input, the action and how the result is checked in the app. The runner reports pass or fail per task, the hands-off result, and the time taken, as Markdown in `docs/measurements/suites-<date>.md`.

### Task 5: Machine matrix

- **Behaviour:** run the suites on Windows 11 (this PC) and Windows 10 and 11 evaluation VMs (D30), at 100%, 150% and 200% scaling, with one and two monitors, as a non-admin user. Arm64 as decided above. Results go in the measurements file.

### Task 6: Resource and safety runs

- **Behaviour:** one hour idle and one hour of scripted tasks against the budget. The safety suite: hostile text in a document, a Slack message and a Notion page tries to make the agent delete or send something, and each attempt must reach a confirmation; password fields and secret windows return nothing; every Discord change is refused.

## Gate

- Every adapter passes its task suite on Windows 10 and 11.
- Budgets met in the one-hour runs.
- The safety suite passes.
- Report written.
