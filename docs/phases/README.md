# Phase roadmap

Every phase of Deskbridge v1: what it is for, what it needs, what it builds, and the check that ends it. Each phase has its own plan file in this folder. The product plan is `docs/plan.md`; decisions are in `docs/decisions.md`.

## How every phase runs

1. **Before it starts:** Claude refines the phase plan to step level, publishes an artifact showing what the phase builds, logs any new decision, and the owner says go (D2, D25).
2. **Branch:** work happens on `phase-<n>-<name>`. Nothing is committed during the phase (D26).
3. **Each task:** write the failing test, run it and see it fail, write the code, run it and see it pass. Every end-to-end action runs inside the hands-off check.
4. **At the gate:** Claude runs every gate check, writes `phase-<n>-report.md` with results and measurements, and the owner reviews. Then the work is committed one commit per task and merged.
5. **No subagents.** Claude does all of it in the main session (CLAUDE.md rule 1).

## The phases

| Phase | Weeks | Goal | Gate that ends it | Plan |
|---|---|---|---|---|
| 0. Setup | Week 1 (3 days) | Prove the shape: a relay and broker any client can start, two tools, and proof they never touch the cursor or focus | Hands-off check catches a deliberate mouse move; old and new protocol versions both work; background read with no focus change; idle cost measured | [phase-0-setup.md](phase-0-setup.md) |
| 1. Core | Weeks 1 to 3 | Everything that makes Deskbridge safe and useful on its own: all core tools, permissions, confirmations, audit log, tray | An agent renames a file, reads Notepad and changes a setting with zero cursor or focus changes; idle CPU under 0.5% | [phase-1-core.md](phase-1-core.md) |
| 2. Adapter SDK and local hub | Weeks 4 to 5 | The way apps get taught: manifests, transports, plugins, the hub for local MCP servers, Code Mode | A sample adapter built only from the SDK works; a local MCP server runs behind the hub with confirmations; Code Mode runs a 3-step script | [phase-2-sdk-hub.md](phase-2-sdk-hub.md) |
| 3. Nook milestone 1 | Weeks 5 to 6 | The local Nook tasks: VS Code, spreadsheets, the Claude Code build loop; pairing with Nook | Nook opens a file at a line and lists problems, updates cells in a WPS sheet, and runs a Claude Code loop of at least 2 rounds | [phase-3-nook-m1.md](phase-3-nook-m1.md) |
| 4. Nook milestone 2 | Weeks 7 to 9 | The tasks that need sign-in or chat apps: Linear, Notion, Slack, Discord | Nook reads a Slack channel and drafts a reply, and creates a Linear issue and a Notion page | [phase-4-nook-m2.md](phase-4-nook-m2.md) |
| 5. Rest of v1 | Weeks 10 to 12 | Finish the v1 app list and prove every adapter on Windows 10 and 11 | Every adapter passes its task suite on Windows 10 and 11 | [phase-5-complete-v1.md](phase-5-complete-v1.md) |
| 6. Release | Weeks 13 to 15 | Ship: signed builds, every install channel, docs, security review | Clean install on fresh Windows 10 and 11; security review findings closed | [phase-6-release.md](phase-6-release.md) |

## What each phase needs from the owner

| Phase | Owner action | Needed by |
|---|---|---|
| 0 | Choose the product name (or keep "Deskbridge" until later) | Before the Phase 0 gate |
| 0 | Choose where the repo lives (personal account `alikhan866` or a new organisation) and allow the first push, so CI can run | Before the Phase 0 gate |
| 0 | Apply to SignPath Foundation once the repo is public (needs MFA on GitHub) | Phase 0 gate; approval can take weeks |
| 1 | Install Claude Desktop if the Phase 1 gate should use it (Claude Code works otherwise) | Phase 1 gate |
| 3 | Schedule the Nook-side work: a Deskbridge catalogue entry, "input required" support and the build loop | Start of Phase 3 |
| 4 | Buy the domain (the sign-in client document lives there) | Start of Phase 4 |
| 4 | Create test workspaces: Slack, Notion, Linear, and a Discord test server with a test account | Start of Phase 4 |
| 4 | Decide the Slack path: register a Deskbridge Slack app, or UI Automation only | Start of Phase 4 |
| 5 | Start a Microsoft 365 trial; set up a Windows 10 evaluation VM in VirtualBox; decide on an Arm64 test device | Start of Phase 5 |
| 5 | Create a small Unity 6 test project | Start of Phase 5 |
| 6 | Accounts for winget and the MCP registry; record the demo video | Phase 6 |

## Order and overlap

```text
Phase 0 -> Phase 1 -> Phase 2 -> Phase 3 -> Phase 4 -> Phase 5 -> Phase 6
                                   |           |
                    Nook-side work (done in Nook, not here) runs alongside 3 and 4
```

Phases run in order because each builds on the interfaces of the one before. The only parallel work is on Nook's side, and the optional parallel adapter sessions in Phases 4 and 5 (see `docs/plan.md`, Build plan).

## Detail level

Phase 0 is written step by step and is ready to build. Phases 1 to 6 are written task by task: files, interfaces, tests and gate. Each is refined to step level, with its own artifact, just before it starts, because its interfaces build on what the phase before it produces and measures (D25).
