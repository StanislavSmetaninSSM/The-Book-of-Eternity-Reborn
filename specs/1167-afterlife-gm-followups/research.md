# Research: Afterlife and GM Bridge Follow-ups

## Decision: Player output and audit output remain separate modes

**Rationale**: Existing afterlife command tests rely on raw canonical state for verification, but normal players need readable summaries. Keeping an explicit audit path preserves developer value while cleaning the default experience.

**Alternatives considered**:
- Remove raw output entirely: rejected because contract verification and repair diagnostics still need it.
- Keep raw output below summaries: rejected because it still makes default player screens feel like debug terminals.

## Decision: GM bridge Codex defaults should avoid repository worktree cwd

**Rationale**: The live test in #1166 showed Codex reading coding-agent context from the repo worktree and taking 430.3 seconds. A hidden GM should see a game-session context, not repository implementation instructions.

**Alternatives considered**:
- Rely only on prompt text: rejected because Codex also discovers local agent context by cwd.
- Remove custom cwd entirely: rejected because advanced users may intentionally configure a runner environment.

## Decision: Daemon logging must force UTF-8 at script/runtime boundaries

**Rationale**: The underlying JSON can remain valid while stdout becomes unreadable through PowerShell encoding defaults. The fix belongs near daemon launch/logging boundaries and should have a regression check with representative Cyrillic text.

**Alternatives considered**:
- Tell users to change terminal code page manually: rejected because the game launcher should handle normal Russian play diagnostics.
