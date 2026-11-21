# Phase 6: Release plan

> Task-level plan (D25). Refined to step level, with an artifact, before Phase 6 starts. No subagents. Every outward step (publishing, submitting, posting) waits for the owner's explicit go.

**Goal:** Ship v1: signed builds, every install channel, the docs site, a security review, and launch material.

**Weeks:** 13 to 15.

**Spec:** `docs/plan.md` (Release and launch, Cost, Risks) and D4, D5, D30.

## Needs before it starts

- Phase 5 gate passed.
- SignPath Foundation has approved the project (applied in Phase 0).
- The final product name (a rename, if any, happens in Task 1).
- Accounts for winget submissions (a GitHub fork of `microsoft/winget-pkgs`) and the MCP registry.

## Tasks

### Task 1: Final name

- **Behaviour:** if the name changed, rename in one change: `Names.cs`, exe and assembly names, namespaces, pipe and mutex prefixes, docs. A test asserts no old name remains outside `docs/decisions.md`.

### Task 2: Release builds and signing

- **Files:** `.github/workflows/release.yml`
- **Behaviour:** on a `v*` tag: build x64 and Arm64; relay with Native AOT; broker single-file, self-contained, ReadyToRun, compressed; submit both exes to SignPath with its GitHub action; package a zip per architecture with SHA-256 checksums; create a draft GitHub Release with the SignPath attribution line. The owner publishes the draft.
- **Tests:** the signed exes pass `Get-AuthenticodeSignature` with status Valid; the zip runs from any folder.

### Task 3: Install channels

- **Files:** `packaging/winget/`, `packaging/mcpb/manifest.json`, `packaging/registry/server.json`
- **Behaviour:** a winget manifest (zip with nested portable exes, command alias `deskbridge`) submitted as a pull request to `microsoft/winget-pkgs`; an MCP bundle (`server.type` "binary", entry `server/deskbridge.exe` with args `mcp`, Windows only) built with `mcpb pack` and submitted to Claude Desktop's extension directory; a `server.json` published to the MCP registry with `mcp-publisher`.
- **Tests:** install through each channel in a fresh VM and connect Claude Desktop or Claude Code.

### Task 4: Docs site

- **Files:** `site/` (extends the Phase 4 site)
- **Pages:** install (each channel), permissions and confirmations, every adapter with its tools and tested app versions, how to write a recipe or hub entry, the plugin protocol, security model, the audit log.

### Task 5: Security review

- **Files:** `docs/security/threat-model.md`, `docs/security/review-<date>.md`
- **Checks:** the pipe refuses another Windows user (tested in a VM with two accounts); tokens only in Credential Manager; secret guards; the safety suite; `dotnet list package --vulnerable`; signatures on every shipped exe; the OAuth listener closes; no network calls from Deskbridge's own code (checked with a firewall log during the suites). Findings are fixed or logged as decisions before release.

### Task 6: Clean installs

- **Behaviour:** fresh Windows 10 and 11 evaluation VMs in VirtualBox (D30), nothing preinstalled: install, connect a client, run the core suite.

### Task 7: Launch material

- **Files:** `docs/launch/` (write-up draft, post drafts, adapter bounty list)
- **Behaviour:** Claude drafts the write-up and the posts under the writing rules; the owner records the 60-second demo and decides when and where to post.

## Gate

- Clean install on fresh Windows 10 and 11 machines.
- Security review findings closed.
- Signed draft release ready for the owner to publish.
- Report written.
