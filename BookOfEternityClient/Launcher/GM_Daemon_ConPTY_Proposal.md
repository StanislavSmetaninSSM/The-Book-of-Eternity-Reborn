# Windows-Native GM Bridge Proposal

## Status
Historical design note. The C# helper exists as `BookOfEternityGMBridge`; this document still records the intended behavior and operator-facing configuration.

This document captures the idea of replacing the current fragile `SendKeys`-based daemon transport with a proper Windows-native backend.

## Problem

The current `game_master_daemon.ps1` can:
- copy prompts to clipboard
- try to activate the CLI window
- try to paste text via:
  - `RightClick`
  - `Shift+Insert`
  - `Ctrl+V`

This is inherently fragile because:
- different Windows terminals handle paste differently
- window focus can be stolen
- `SendKeys` may fail silently or send the wrong input
- the daemon does not truly control the CLI process
- automation quality depends on the user's console host

The clipboard fallback is reliable, but it is still semi-manual and less convenient than a real bridge.

## Goal

Build a Windows-native GM bridge that:
- does not rely on `SendKeys`
- does not rely on mouse-click simulation
- does not require Linux, WSL, or `tmux`
- remains free and available to ordinary Windows users
- preserves the persistent CLI session and context
- still lets the player inspect what the GM CLI is doing

## Proposed Solution

Use a dedicated Windows helper process that runs the GM CLI inside a **ConPTY** (Windows pseudo-console).

### Core idea

Instead of:
- opening a normal terminal window manually
- trying to inject text into it

the client ecosystem would:
1. start a dedicated helper process
2. let that helper launch `codex --dangerously-bypass-approvals-and-sandbox` inside a ConPTY
3. send prompts directly into the pseudo-console input stream
4. read CLI output directly from the pseudo-console output stream

This makes the GM bridge deterministic and removes dependence on terminal-specific paste behavior.

## Target Architecture

### Components

#### 1. GM Bridge Helper
Suggested form:
- small native helper process
- recommended implementation language:
  - C#
  - or C++ if lower-level ConPTY control is needed

Responsibilities:
- create and own the ConPTY session
- launch the GM CLI process inside it
- accept prompt messages from the daemon/client
- write them into the pseudo-console input stream
- stream back output/log text
- expose lifecycle state:
  - starting
  - ready
  - busy
  - stopped
  - crashed

#### 2. Existing GM Daemon
The current daemon can be simplified and kept as:
- turn watcher
- request correlator
- ready/error watcher
- prompt dispatcher

After migration it should no longer:
- send `Ctrl+V`
- send `Shift+Insert`
- simulate right-click paste

Instead it would:
- pass prompt text to the helper over a local IPC channel

#### 3. Optional GM Monitor Window
Optional but desirable:
- a read-only viewer window or console output mirror
- lets the player see that the GM is working
- preserves the transparency advantage of the current visible-CLI approach

This can be added later and is not mandatory for the first implementation.

## Recommended IPC

### Preferred
Use **Named Pipes** on Windows between:
- daemon
- helper

Why:
- native Windows
- free
- no external dependency
- robust enough for local-only communication

### Message examples

#### Send prompt to helper
```json
{
  "type": "dispatch_prompt",
  "sessionId": "string",
  "requestId": "string",
  "turnNumber": 12,
  "prompt": "full text to feed into codex"
}
```

#### Helper status
```json
{
  "type": "bridge_status",
  "state": "ready",
  "cliProcessId": 12345
}
```

## Why ConPTY is better than window automation

### Advantages
- no dependence on console paste shortcuts
- no window focus problems
- no `SendKeys`
- no terminal-host-specific behavior
- true persistent GM context
- low latency after startup
- deterministic input delivery

### Remaining risks
- the GM CLI itself may still behave differently inside a pseudo-console than inside a normal terminal
- startup/login flow of the CLI must be tested inside the helper-owned session
- output parsing should stay optional; the bridge should not assume a brittle text protocol

## Behavior Model

### First implementation
The helper should:
1. start
2. create ConPTY
3. launch `codex --dangerously-bypass-approvals-and-sandbox`
4. wait for operator to confirm the CLI is ready
5. accept prompt dispatches from the daemon

This avoids trying to automate authentication blindly.

### Later improvement
If startup becomes stable enough, the helper can auto-launch directly into ready state.

## Player Experience

Desired UX:
- player starts the GM bridge helper once
- helper launches and keeps the GM CLI alive
- daemon watches `turn_request.json`
- daemon sends prompt text directly into the bridge
- no manual copy/paste on each turn
- player can still inspect progress if needed

## Migration Strategy

### Phase 1
Keep current daemon and add a new backend mode:
- `Clipboard`
- `WindowAutoPaste`
- `ConPTY`

Default recommendation:
- once stable, `ConPTY`

Fallbacks:
- `Clipboard` remains universal and reliable
- `WindowAutoPaste` becomes explicitly legacy/experimental

### Phase 2
Move launcher/help/docs to recommend:
- `ConPTY` as the preferred Windows backend
- `Clipboard` as the universal fallback

### Phase 3
Deprecate `WindowAutoPaste` once the bridge is proven stable.

## Open Questions

1. Should the helper be:
   - invisible/background only
   - or visible with a debug/monitor console?

2. Should GM CLI startup be:
   - fully manual once inside the helper
   - or partially automated?

3. Should the helper expose:
   - only input injection
   - or also an output mirror for diagnostics?

## Current Decision

Not implemented yet.

The idea is considered the preferred long-term Windows solution for the GM daemon, because it is:
- Windows-native
- free
- more reliable than `SendKeys`
- independent from Linux/WSL tooling like `tmux`

Until then:
- `Clipboard` remains the safest fallback
- `WindowAutoPaste` remains best-effort only

## Large Paste Visibility Markers

Before the bridge sends `Enter` after a bracketed paste, it verifies that the prompt is visible in the hosted CLI. Some CLIs collapse large pasted text into a marker instead of rendering the full prompt. This is controlled by `game_session/config.json`:

```json
{
  "gmBridgePasteVisibilityPolicy": "ExactTextOrConfiguredMarker",
  "gmBridgePasteVisibilityMarkers": [
    { "name": "Codex", "kind": "regex", "pattern": "\\[Pasted Content \\d+ chars\\]" }
  ]
}
```

`kind` supports:
- `contains` - case-insensitive substring match.
- `regex` - case-insensitive regular expression match; invalid regex markers are ignored instead of crashing the bridge.

Use `ExactTextOnly` if a CLI must never rely on collapsed paste markers. Use `ExactTextOrConfiguredMarker` for Codex or any future supported local CLI that reports accepted large pastes with a stable marker.
Configured custom markers are added on top of the built-in Codex defaults, so adding one local CLI marker does not disable existing compatibility.
