# Feature Spec: GM Turn Helper Harness

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1276

## Problem

Live Chaos Sea testing showed that the main GM can understand the turn but still fail the harness ceremony: it spent the full daemon timeout building ad-hoc PowerShell write scripts, hit parse errors, and wrote terminal artifacts after the pending turn context was already gone. Prompt reminders did not remove the failure mode.

## Goals

- Provide a repo-owned PowerShell helper for GM file writes and terminal signals.
- Provide a session-local bootstrap file so the GM can dot-source one short path for the current `game_session`.
- The helper must read exact `sessionId`, `requestId`, and `turnNumber` from the current client-authored request instead of asking the GM to copy them by hand.
- The helper must fail clearly if the current turn or repair request no longer exists, preventing stale terminal signals.
- The daemon turn prompt must point to the helper and recommend using it for the final terminal action.

## Non-Goals

- Do not automate game-state decisions or narration.
- Do not bypass validation.
- Do not replace the file-based protocol with an API in this issue.

## Acceptance

- Tests prove the helper can write a correlated `ready/turn_complete.json` from a temp `input/turn_request.json`.
- Source guards prove the daemon writes a session-local helper bootstrap and mentions `Complete-BoeTurn` in the dispatched prompt.
- GM-facing docs explain the helper workflow.
- A follow-up live test records whether the GM completes the turn faster or at least fails with a clearer stale-context error.
