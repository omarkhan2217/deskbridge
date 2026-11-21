# Phase 4: Nook milestone 2 plan

> Task-level plan (D25). Refined to step level, with an artifact, before Phase 4 starts. No subagents.

**Goal:** The Nook tasks that need sign-in or chat apps (D9): read a Slack channel and draft a reply; create a Linear issue and a Notion page. Discord read only.

**Weeks:** 7 to 9.

**Spec:** `docs/plan.md` (v1 apps, Safety and permissions: network connectors) and D7, D8, D32.

## Needs before it starts

- Phase 3 gate passed.
- The product name decided and the domain bought: the OAuth client metadata document must live at an HTTPS address Deskbridge controls.
- Test workspaces created by the owner: Slack, Notion, Linear, and a Discord test server with a test account.
- The Slack path decided: register a Deskbridge Slack app, or UI Automation only for v1.

## Tasks

### Task 1: Minimal docs site and client metadata document

- **Files:** `site/` (static site on Cloudflare Pages), `site/oauth/client.json`
- **Interfaces:** the client metadata document names Deskbridge, its loopback redirect URIs and its homepage. The full docs site comes in Phase 6; this task only needs the domain, hosting and this file.
- **Tests:** the published URL serves the JSON with the right content type; a validator script checks the fields the 2026-07-28 spec requires.

### Task 2: Remote hub transport and sign-in

- **Files:** `src/Deskbridge.Hub/Remote/RemoteHubServer.cs`, `LoopbackListener.cs`, `CredentialTokenCache.cs`; settings window "Network connectors" section
- **Interfaces:** `RemoteHubServer` uses the SDK's `HttpClientTransport` with `ClientOAuthOptions`: `ClientMetadataDocumentUri` set to the document from Task 1, `RedirectUri` from `LoopbackListener` (127.0.0.1, random port, one request, closes after it or after 5 minutes; D32), `AuthorizationRedirectDelegate` opens the default browser, `TokenCache` backed by Windows Credential Manager (`Deskbridge/hub/<id>`), dynamic registration as a fallback. Each connector shows its domain, the signed-in account and a Sign out button that deletes its tokens.
- **Tests:** against a local test OAuth server: sign-in completes and tokens land in Credential Manager; the listener is closed afterwards (no port open); refresh happens before expiry; sign out removes the tokens; tokens never appear in logs or config.

### Task 3: Linear and Notion

- **Files:** `adapters/linear-hub/manifest.json`, `adapters/notion-hub/manifest.json`
- **Interfaces:** hub entries for `https://mcp.linear.app/mcp` (prefix `linear`) and `https://mcp.notion.com/mcp` (prefix `notion`), with effect overrides so creating, updating and commenting ask, and deleting is Destructive.
- **Tests (end to end, test workspaces):** create a Linear issue and a Notion page after Allow; reading needs no confirmation; a hostile instruction inside a Notion page does not lead to an unconfirmed change.

### Task 4: Electron cost measurement

- **Files:** `scripts/measure-electron.ps1`, `docs/measurements/electron.md`
- **Behaviour:** for Slack, Discord, Notion, Linear and VS Code: the app's CPU and memory before a UI Automation read, during it, and for 5 minutes after. An adapter may set `allow_uia: true` only for apps whose cost stays under the threshold the owner approves at phase start (proposal: memory up by less than 50 MB, CPU back to its baseline within 60 s).

### Task 5: Slack composer spike (1 day)

- **Question:** can text be placed into Slack's message box through UI Automation (Value or Text pattern) without focus?
- **Output:** a short note in `docs/measurements/slack-composer.md` and the chosen draft path for Task 6.

### Task 6: Slack adapter

- **Files:** `adapters/slack/` (manifest, recipes)
- **Interfaces (tools):** `slack_read_channel(max_messages=50)` reads the open channel in the Slack app (author, time, text as `untrusted_text`); `slack_draft_reply(text)` Change: places the draft in the composer if the spike worked, else shows it on the confirmation card and copies it to the clipboard after Allow. Never sends. If the owner registered a Slack app, a `slack-hub` entry for Slack's server is added with sends marked Destructive.
- **Tests (end to end, test workspace):** read the test channel inside the hands-off probe; draft a reply and confirm nothing was sent.

### Task 7: Discord adapter (read only)

- **Files:** `adapters/discord/` (manifest, recipe)
- **Interfaces (tools):** `discord_read_channel(max_messages=50)`. The manifest has no Change or Destructive functions (D8).
- **Tests:** read the test server's channel; a policy test proves no Discord function can be anything but Read.

### Task 8: Gate scenario

- **Files:** `tests/Deskbridge.E2E/Gates/Phase4GateTests.cs`, `docs/phases/phase-4-report.md`

## Gate

- Nook reads a Slack channel and drafts a reply (not sent).
- Nook creates a Linear issue and a Notion page.
- No OAuth listener left open; no token in any file or log.
- All tests pass; report written.
