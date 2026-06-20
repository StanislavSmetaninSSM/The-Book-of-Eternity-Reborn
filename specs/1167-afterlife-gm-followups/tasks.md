# Tasks: Afterlife and GM Bridge Follow-ups

**Input**: Design documents from `specs/1167-afterlife-gm-followups/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `quickstart.md`

**Tests**: Behavior changes and bug fixes require test-first work.

**Source issues**: #1167, #1168, #1169, #1170, #1171

## Phase 1: Setup

- [x] T001 Confirm branch/worktree state and source issue labels for #1167-#1171.
- [x] T002 Read `AGENTS.md`, constitution, source issue bodies, and nearby afterlife/GM bridge tests.
- [x] T003 Update `AGENTS.md` Spec Kit pointer to `specs/1167-afterlife-gm-followups/plan.md`.

## Phase 2: GM bridge reliability

- [x] T004 [P] [US5] Add a failing regression test for Cyrillic daemon logging in `BookOfEternityClient.Tests/`.
- [x] T005 [US5] Fix daemon/launcher encoding so Cyrillic player actions survive stdout/log output.
- [x] T006 [P] [US4] Add failing tests proving default Codex GM bridge launch does not use repository worktree context.
- [x] T007 [US4] Implement GM-only Codex launch isolation/default cwd and diagnostics without removing advanced overrides.
- [x] T008 [US4] Update launcher/bridge docs and examples for Codex-only hidden GM defaults.

## Phase 3: Afterlife status split

- [x] T009 [P] [US1] Add failing tests that default Chaos Sea `/status` hides raw JSON, paths, canonical fields, and internal closure hints.
- [x] T010 [P] [US1] Add failing tests that default Shining Abode `/status` hides raw JSON, paths, canonical fields, and internal closure hints.
- [x] T011 [US1] Implement player-facing `/status` summaries for Chaos Sea and Shining Abode.
- [x] T012 [US1] Preserve explicit audit/debug output and migrate existing raw-output tests to audit mode.

## Phase 4: Shining Abode details

- [x] T013 [P] [US2] Add failing tests for player-readable Shining Abode gate, package, receipt, and pending action details.
- [x] T014 [US2] Split Shining Abode default detail renderers from audit payload renderers.
- [x] T015 [US2] Verify summary-to-detail-to-back navigation stays available in default detail flows.

## Phase 5: Afterlife action previews

- [x] T016 [P] [US3] Add failing tests for one guardian trade preview, one resident preview, and one archive/offering preview.
- [x] T017 [US3] Implement player confirmation views with action, target, cost, risk, expected result, confirm/cancel, and back navigation.
- [x] T018 [US3] Preserve explicit audit views for pending payloads, request IDs, receipts, and GM authoring details.

## Phase 6: Documentation, review, verification

- [x] T019 Update `docs/console-afterlife-output-audit.md` with the new player/audit classification and references to #1167-#1169.
- [x] T020 Run documentation-sensitive afterlife verification tests.
- [x] T021 Run focused runtime verification for GM bridge/daemon/afterlife commands.
- [x] T022 Run broad non-browser C# verification excluding local web UI built frontend smoke tests.
- [x] T023 Review diff and fix Critical/Important findings. Independent subagent review was not used because the available multi-agent tool requires explicit user authorization for spawning.
- [ ] T024 Create/merge PR if verification and review are clean; close #1167-#1171 with evidence.
