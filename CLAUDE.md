# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this repo is

Deskbridge (working name) is a local MCP server for Windows. It lets any AI agent read and act in desktop apps without moving the user's mouse, typing into the window they are using, or taking focus. It ships as a public product first and then becomes the PC part of Nook's action layer.

- The plan of record is `docs/plan.md`. It grew from the owner's first draft, a PDF kept outside the repo.
- Every decision, with its reason and date, is in `docs/decisions.md`.
- Nook is a separate, private project. Its integration notes are kept outside this repo; ask the owner where. Never change Nook from a session in this repo.

## How to work here

These rules come from the owner and apply to every session.

1. **Never use subagents.** Do all research, reading and writing in the main session. Do not call the Agent tool, the Workflow tool, or any skill step that dispatches one. If a skill asks for a subagent review, do that review yourself.
2. **Show the plan before acting.** Before making a decision or building a feature, publish an artifact that shows what will be done (the change, the files it touches, how it will be checked) and add an entry to `docs/decisions.md`. Wait for the owner's approval before writing code.
3. **Log every decision.** Each entry in `docs/decisions.md` has an ID, a date, the decision, the options considered, the reason, and who made it (owner, or Claude as a proposal the owner approved). A decision is never edited away; a later entry supersedes it and says so.
4. **Ask when the answer changes what gets built.** Otherwise make the call, state it in one line, and log it.
5. **Do not commit until the owner has reviewed the change.**
6. **This repo is public.** Never add details of Nook's private code (file paths, internal decisions, roadmap), local paths on the owner's PC, or personal information. Those belong in the owner's private notes.

## Writing rules

These apply to everything written in this repo: docs, README, code comments, commit messages, UI text, error messages, and replies to the owner.

**Punctuation**

- No em dashes (U+2014). Use a comma, colon, period, or parentheses instead.
- No en dashes (U+2013) as sentence punctuation. Write ranges as "3 to 5" or "3-5" with a hyphen.
- Before finishing any change, search for both characters and remove them: `rg -n "[\x{2013}\x{2014}]" --glob "!*.pdf"`.

**Patterns that are banned**

- Contrast framing: "This is not just X, it's Y", "not X, but Y", "X isn't about A. It's about B", "less X, more Y".
- Hype words: seamless, robust, powerful, cutting-edge, game-changer, revolutionary, unlock, unleash, empower, elevate, supercharge, leverage (as a verb), delve, harness, streamline, effortless.
- Filler openers and closers: "In today's fast-paced world", "Let's dive in", "It's worth noting that", "At the end of the day", "In conclusion", "Here's the thing", a final paragraph that repeats what was already said.
- Padding in threes ("fast, reliable, and secure") when one concrete fact would do.
- Rhetorical questions used as openers or section intros.
- Emoji in docs, comments, and commit messages.

**What to do instead**

- Say what the thing does, in plain words, with a number where one exists ("ready in under 1 s", "under 80 MB idle").
- Use short sentences and the active voice. Name the actor: "Deskbridge asks the user", not "the user is asked".
- Lead with the fact the reader needs. Put reasons after it.
- Prefer a table when comparing options, and a list when steps happen in order.
- Use the same term for the same thing everywhere. Do not swap synonyms for variety.

## File and folder conventions

- Docs and non-code folders use lowercase kebab-case with no spaces: `docs/plan.md`, `docs/decisions.md`, `adapters/obs/`.
- .NET projects follow .NET naming: `src/Deskbridge.Core/`, `Deskbridge.Core.csproj`, PascalCase types and methods.
- Markdown: one `#` title per file, sentence-case headings, ATX headings (`##`), fenced code blocks with a language tag, tables for comparisons.
- Dates are ISO 8601 (`2026-11-22`). Times, if needed, are 24-hour with a time zone.
- Text files use UTF-8 without a BOM and LF line endings, except `.ps1`, `.cmd` and `.bat`, which use CRLF (set in `.gitattributes`).
- No secrets in the repo: no `.env`, keys, tokens, or certificates.

## Product rules that code must follow

The plan explains each one; these are the short forms. When two conflict, the earlier one wins.

1. **Hands off.** Never move the mouse, never type into the window the user is using, never take focus without asking.
2. **Safe by default.** Apps start read-only. Changes need the user's permission for that app, and destructive actions ask every time.
3. **The app's own way first.** App API, COM, CLI or plugin, then UI Automation patterns, then window messages, then nothing.
4. **Light.** No polling and no timers when idle. Never put a Chromium or Electron app into accessibility mode unless its adapter allows it.
5. **Local only.** The Deskbridge process makes no network calls and sends no telemetry. Logs stay on the PC.
6. **Works with any agent.** Standard MCP, so every client uses it the same way.
