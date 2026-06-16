# Requirements Checklist: Spiritual Conflict Exchange and Art Drill-Downs

**Feature**: `specs/1067-spiritual-conflict-art-drilldowns/`

**Issue**: #1067 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1067

**Date**: 2026-06-17

## Spec Quality

- [x] Source GitHub issue #1067 is linked in `spec.md`, `plan.md`, and `tasks.md`.
- [x] Origin audit #949 AFD-006 is linked and summarized.
- [x] The scope is bounded to spiritual-conflict exchange/log/art drill-downs.
- [x] Sibling issues #1063-#1066 are explicitly out of scope.
- [x] Runtime/GM contract changes are explicitly out of scope unless discovered and documented in the same PR.
- [x] User stories are independently testable.
- [x] Missing/stale/sparse/malformed state behavior is specified.
- [x] Default player-facing copy forbids raw JSON/API/DTO/debug/path/parser leakage.
- [x] Read-only/no-mutation boundary is specified for detail actions.
- [x] Verification commands are listed with focused and broad gates.

## Ambiguities Resolved

- [x] `/spiritual_combat_help` is explanatory and should not become a new selected-row lifecycle; it may be linked contextually where useful.
- [x] `/spiritual_arts` can expose read-only inspect actions while preserving existing local-turn upgrade/write authority.
- [x] Stable IDs are preferred; safe indices are acceptable only when no durable ID exists and stale-index tests cover the behavior.
- [x] No frontend/React gameplay authority should be added for this slice.

## Ready for Implementation

- [x] Spec, plan, tasks, contract, and checklist exist.
- [x] Acceptance criteria cover exchange detail, combat-log/recent-conflict detail, spiritual-art detail, missing-state safety, no-mutation, and no-docs-impact rationale.
- [x] Codex prompt can pass active artifact paths and baseline gate counts.
