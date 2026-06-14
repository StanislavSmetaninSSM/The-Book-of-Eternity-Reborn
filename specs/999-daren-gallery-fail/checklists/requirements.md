# Requirements Checklist: Daren Silent Gallery Fail Literary Aftermath

**Feature**: `specs/999-daren-gallery-fail/`
**Source issue**: [#999](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/999)
**Created**: 2026-06-15

## Completeness

- [x] Source GitHub issue #999 is linked in `spec.md`, `plan.md`, `tasks.md`, and the contract.
- [x] Parent #955 and scene prerequisite #972 are linked.
- [x] Completed gallery siblings #997 and #998 are linked with explicit preservation boundaries.
- [x] Completed downstream result issues #1000-#1008 are linked with explicit preservation boundaries.
- [x] In-scope result surface is exactly `stealth_crossing` / `stealth_crossing_action` / `fail`.
- [x] Out-of-scope surfaces include scene opening, #997 success, #998 partial, downstream result trios, other Daren scenes/results, parent #955 closure, mechanics, endpoints, runtime state, frontend-only forks, and GM-facing docs/examples.

## Requirement Quality

- [x] The user story is independently testable through shared C# route data.
- [x] The fail grade semantics distinguish dangerous failure from clean success and mixed partial.
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
- [x] No requirement allows changing #997, #998, or #1000-#1008 while implementing #999.
- [x] The phrase "fail" is operationalized as dangerous gallery aftermath with concrete noise/evidence/witness/pursuit pressure, not as a route mechanics change.
