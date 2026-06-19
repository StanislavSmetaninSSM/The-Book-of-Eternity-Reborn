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

## Decision: MVP proves general worker delegation with validation repair and narrative drafting

**Rationale**: Validation repair has a crisp input and output: validation issues in, corrected proposal out, validator pass/fail after apply. Narrative drafting proves the other half of the architecture: the main GM can delegate useful creative work, receive a draft, and decide whether to use or rewrite it without letting the worker own the player-facing response. Supporting both task classes in the first wave prevents the architecture from hardening into a validation-only repair tool.

**Alternatives considered**:

- Start with validation repair only. Rejected because it would not prove the user-requested multi-purpose worker model.
- Start with creative/lore delegation only. Rejected because acceptance is subjective and less suitable for proving the safety model.
- Build a general task marketplace first. Rejected as too broad before the validation repair contract exists.

## Decision: Narrative drafts are main-GM private proposals

**Rationale**: A worker may be good at drafting prose, but the main GM must keep tone, continuity, state authority, and final response ownership. Narrative drafts therefore enter the proposal inbox and are not shown to the player unless the main GM explicitly uses them.

**Alternatives considered**:

- Let narrative workers answer the player directly. Rejected because it creates competing GMs and weakens continuity.
- Treat narrative drafts as canonical files. Rejected because many drafts are ephemeral and should not mutate state.

## Decision: Audit every dispatch and proposal decision

**Rationale**: Multi-agent systems are hard to debug without attribution. The game needs to answer who proposed a change, what scope was granted, why it was accepted or rejected, and which validation command proved it.

**Alternatives considered**:

- Log only failures. Rejected because successful worker changes also need attribution.
- Keep audit only in transient console output. Rejected because live E2E and postmortem debugging need durable artifacts.

## Decision: Keep browser UI out of MVP

**Rationale**: Browser frontend work is separately assigned. MVP can expose diagnostics through files, console advanced diagnostics, and tests without touching the browser UI.

**Alternatives considered**:

- Add a browser worker dashboard. Rejected for this scope; it should be a follow-up after GLM browser parity work stabilizes.
