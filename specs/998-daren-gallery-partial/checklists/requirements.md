# Requirements Checklist: Daren Silent Gallery Partial Literary Aftermath

**Feature**: `specs/998-daren-gallery-partial/`
**Source issue**: [#998](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/998)
**Created**: 2026-06-15

## Completeness

- [x] Source GitHub issue #998 is linked in `spec.md`, `plan.md`, `tasks.md`, and the contract.
- [x] Parent #955 and scene prerequisite #972 are linked.
- [x] Completed success sibling #997 and future fail sibling #999 are linked with explicit boundaries.
- [x] Completed downstream result issues #1000-#1008 are linked with explicit preservation boundaries.
- [x] In-scope result surface is exactly `stealth_crossing` / `stealth_crossing_action` / `partial`.
- [x] Out-of-scope surfaces include scene opening, #997 success, #999 fail, downstream result trios, other Daren scenes/results, parent #955 closure, mechanics, endpoints, runtime state, frontend-only forks, and GM-facing docs/examples.

## Requirement Quality

- [x] The user story is independently testable through shared C# route data.
- [x] The partial grade semantics distinguish mixed success from clean success and full fail.
- [x] Objective measurable criteria exist for length, sentence count, Daren POV, and grouped motif checks.
- [x] Route/mechanics invariants are explicit.
- [x] Default player-facing forbidden technical terms are explicit.
- [x] Browser/console parity is preserved through shared route authority.
- [x] GM-facing impact is classified as not applicable because this is client-owned authored showcase prose with no runtime or GM-authored contract change.

## Verification Readiness

- [x] Focused Daren test command is listed.
- [x] Affected Daren/QTE/docs/browser C# slice command is listed.
- [x] Client and test-project build commands are listed.
- [x] `git diff --check origin/main...HEAD` is listed.
- [x] Added-line static scan is required before PR/merge.
- [x] Frontend verification trigger is scoped to actual frontend/browser file changes or discovered browser rendering bugs.
- [x] Hermes-owned lifecycle tasks are separated from Codex-owned implementation tasks.

## Ambiguity Review

- [x] No requirement depends on the model inventing a new gameplay mechanic.
- [x] No requirement asks for a new dialogue runtime, state file, endpoint, or browser-only fork.
- [x] No requirement allows closing #955 from this child issue alone.
- [x] No requirement allows changing #997, #999, or #1000-#1008 while implementing #998.
- [x] The phrase "partial" is operationalized as passage achieved with lingering consequence, not as a full alarm/fail.
