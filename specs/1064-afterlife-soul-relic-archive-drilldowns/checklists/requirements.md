# Requirements Quality Checklist: #1064 Afterlife Soul Relic/Archive Browser Drill-Downs

## Traceability

- [x] Spec links source issue #1064.
- [x] Spec links origin audit #949 and `docs/audits/afterlife-drilldown-audit.md` row AFD-003.
- [x] Plan and tasks link #1064 and #949.
- [x] Out-of-scope sibling follow-ups #1065-#1067 are named as non-closing context.

## Scope Clarity

- [x] Covered commands are explicit.
- [x] Existing overview outputs and local action forms are preserved.
- [x] Added detail actions are read-only.
- [x] Runtime/GM contract changes are out of scope unless same-PR docs/tests are added.
- [x] React/TypeScript remains presentation-only unless an existing rendering bug requires a tracked UI fix.

## Testability

- [x] User stories include independent focused tests.
- [x] RED/GREEN behavior is required before production code.
- [x] Missing/stale/hidden ids are covered.
- [x] Raw/API/DTO/debug/path leakage guard is covered.
- [x] Verification commands include focused and broader .NET slices, C# builds, Spec Kit prerequisite, diff/static scans, and conditional frontend/docs gates.

## Ambiguity Review

- [x] Detail action shape is defined in a contract file.
- [x] Selected-detail result shape is defined.
- [x] Existing local write forms remain authoritative and are not reimplemented.
- [x] Follow-up policy for too-broad sub-surfaces is explicit.

## Autonomous Implementation Gate

- [x] This feature is suitable for Codex implementation after Hermes records baseline evidence and marks issue ownership.
- [x] Hermes remains responsible for final acceptance, review, PR/merge, issue evidence, and closure.
