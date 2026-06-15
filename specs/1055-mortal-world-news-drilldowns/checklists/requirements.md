# Requirements Checklist: Mortal World-News Read-Only Detail Drill-Downs

**Feature**: `specs/1055-mortal-world-news-drilldowns`

**Source Issue**: #1055 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1055

## Completeness

- [x] Source GitHub issue is linked in spec, plan, tasks, and contract.
- [x] Spec Kit justification is explicit and matches AGENTS.md/constitution policy.
- [x] Scope boundaries name #1056, #1057, #949, afterlife, GM prompt/schema, and browser redesign as out of scope.
- [x] User stories are independently testable and prioritized.
- [x] Acceptance criteria cover overview preservation, event detail, non-event subsection detail, progression detail, console/browser parity, and raw-only regression prevention.
- [x] Edge cases cover missing/sparse files, id fallback, dynamic text escaping, debug-term leakage, and unclear subsection authority.
- [x] Verification plan names focused C# tests, broader slice, builds, Spec Kit prerequisite check, diff hygiene, and static scan.

## Ambiguity Review

- [x] Exact final subcommand words may be refined by Codex after code inspection, but the contract requires tests/task evidence to record the final syntax.
- [x] Non-event subsection coverage is defined as one representative major subsection already rendered by the command; missing canonical authority must become a follow-up rather than invented schema.
- [x] GM-facing docs/examples are explicitly not required unless runtime/GM-authored contracts change.

## Constitution Alignment

- [x] GitHub issue traceability is satisfied.
- [x] Player-facing integrity constraints are included.
- [x] Contract/state authority boundary is included.
- [x] Test-first verification is required.
- [x] Hermes/Codex orchestration ownership is recorded.
