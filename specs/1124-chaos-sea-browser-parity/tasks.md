# Tasks: Chaos Sea Browser Command Output Parity

**Input**: `specs/1124-chaos-sea-browser-parity/spec.md`, `specs/1124-chaos-sea-browser-parity/plan.md`, GitHub issue #1124

**Prerequisites**: Existing #949/#1063/#1064/#1067 afterlife drill-down implementation context.

**Tests**: Behavior changes require test-first. If tests pass immediately because shared C# routes already exist, keep the tests as #1124 regression/evidence coverage.

## Phase 1: Setup

- [x] T001 Confirm #1124 exists, is open, and is labelled for browser-client work.
- [x] T002 Create isolated worktree `work/1124-chaos-browser`.
- [x] T003 Restore test project dependencies in the isolated worktree.

## Phase 2: Audit Existing Coverage

- [x] T004 Inspect `docs/audits/afterlife-drilldown-audit.md` and related #1063/#1064/#1067 specs.
- [x] T005 Inspect `ExplorerAfterlifeCombatCommandResultBuilder` for profile, threat, chronicle, conflict, log, and art detail routes.
- [x] T006 Inspect `ExplorerWebCommandServiceTests` for existing Guardian/Abode and SoulRelic/Archive coverage.

## Phase 3: Regression Coverage

- [x] T007 Add overview action tests for `/afterlife_profiles`, `/afterlife_threats`, `/afterlife_chronicles`, `/spiritual_conflict`, `/spiritual_combat_log`, and `/spiritual_arts`.
- [x] T008 Add selected detail tests for profile, threat, chronicle, spiritual exchange, combat-log exchange, recent combat result, standard art, and special art.
- [x] T009 Add missing-target tests for profile, threat, chronicle, spiritual exchange, recent combat result, and standard art.
- [x] T010 Add afterlife threat fixture proving hidden threats and GM-only markers do not leak.

## Phase 4: Implementation

- [x] T011 Run focused #1124 tests and determine whether production code changes are required.
- [x] T012 Keep production code unchanged because focused tests prove existing shared C# routes already satisfy #1124.
- [x] T013 Broad verification exposed no production gap; no C# builder fix required.

## Phase 5: Verification & Closure

- [x] T014 Run focused #1124 regression tests.
- [x] T015 Run broad `Afterlife|Chaos|ExplorerWebCommand` verification.
- [x] T016 Run `git diff --check`.
- [ ] T017 Commit, open PR for #1124, merge if clean, close issue through PR, remove temporary labels/branches/worktree.
