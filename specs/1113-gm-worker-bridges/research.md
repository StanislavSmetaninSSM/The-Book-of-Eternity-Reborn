# Research: Explicit GM Worker Bridges

## Decision: Use explicit worker bridge processes, not implicit subagents hidden inside one GM CLI

**Rationale**: Explicit bridge processes are observable, configurable, restartable, and auditable. They allow the user to mix Codex, Gemini, or other local CLIs by profile. They also let the daemon enforce a uniform task/proposal/apply-gate contract.

**Alternatives considered**:

- Let the main GM create subagents inside its own CLI. Rejected for MVP because lifecycle, audit, and permissions become opaque to the game runtime.
- Run multiple full GMs that all write state. Rejected because it violates canonical state authority and creates race conditions.

## Decision: Workers are proposal-only by default

**Rationale**: The game already has strict validation, pending snapshot, and realm authority rules. Worker direct writes would bypass these protections. Proposal-only output lets the system inspect file scope, validate, and attribute changes before applying anything.

**Alternatives considered**:

- Give workers direct write access to a shared `game_session`. Rejected due to races, conflicting interpretations, and hard-to-debug validation loops.
- Give workers separate mutable sandboxes and merge automatically. Rejected for MVP; it can be revisited after proposal/apply gate is stable.

## Decision: MVP focuses on validation repair

**Rationale**: Validation repair has a crisp input and output: validation issues in, corrected proposal out, validator pass/fail after apply. It is easier to test than creative delegation and immediately helps live play.

**Alternatives considered**:

- Start with creative/lore delegation. Rejected for MVP because acceptance is subjective and less suitable for proving the safety model.
- Build a general task marketplace first. Rejected as too broad before the validation repair contract exists.

## Decision: Audit every dispatch and proposal decision

**Rationale**: Multi-agent systems are hard to debug without attribution. The game needs to answer who proposed a change, what scope was granted, why it was accepted or rejected, and which validation command proved it.

**Alternatives considered**:

- Log only failures. Rejected because successful worker changes also need attribution.
- Keep audit only in transient console output. Rejected because live E2E and postmortem debugging need durable artifacts.

## Decision: Keep browser UI out of MVP

**Rationale**: Browser frontend work is separately assigned. MVP can expose diagnostics through files, console advanced diagnostics, and tests without touching the browser UI.

**Alternatives considered**:

- Add a browser worker dashboard. Rejected for this scope; it should be a follow-up after GLM browser parity work stabilizes.
