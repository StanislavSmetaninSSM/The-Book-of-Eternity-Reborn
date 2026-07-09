# Tasks: Prose State Delta Audit

**Input**: `specs/1479-prose-state-delta-audit/spec.md` and `plan.md`

**Source issue**: #1479 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1479

## Phase 1: Setup

- [x] T001 Confirm source issue #1479 and current clean worktree.
- [x] T002 Create Spec Kit artifacts for the validation/harness contract.
- [x] T003 Read nearby accepted-turn validation, skill mastery, and quest validation code.

## Phase 2: Failing Tests

- [x] T004 Add a failing accepted-turn validation test for known skill prose without skill progress or no-progress rationale.
- [x] T005 Add a failing accepted-turn validation test for active quest clue prose without quest update or no-progress rationale.
- [x] T006 Add a non-regression test proving ordinary skill/quest listings are not rejected.

## Phase 3: Validator Implementation

- [x] T007 Implement deterministic text extraction from accepted narrative/interface output.
- [x] T008 Implement known-player-skill claim detection with conservative verbs.
- [x] T009 Implement active-quest clue/progress detection with conservative verbs.
- [x] T010 Implement state evidence checks and no-progress rationale lookup.
- [x] T011 Add repair-friendly issue codes and messages.

## Phase 4: GM Docs and Live-Test Methodology

- [x] T012 Update GM-facing guidance/examples so skill-use prose and clue discovery require canonical state deltas or a rationale.
- [x] T013 Update live-test notes/checklists to verify trained skill use and quest clue persistence.

## Phase 5: Verification and Integration

- [x] T014 Run focused validation tests.
- [x] T015 Run documentation/source-guard tests.
- [x] T016 Run `git diff --check`.
- [x] T017 Comment on issue #1479 with verification evidence.
- [x] T018 Commit the fix and return to the golden-route live test.
