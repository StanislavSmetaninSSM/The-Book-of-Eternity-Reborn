# Tasks: Afterlife Forbidden Realm Auto-Rollback

**Input**: `specs/1273-afterlife-auto-realm-rollback/spec.md`, `plan.md`

**Source issue**: #1273

## Phase 1: Setup

- [x] T001 Create GitHub issue #1273 and attach active labels.
- [x] T002 Stop the current #1249 live bridge/client/daemon processes.
- [x] T003 Create Spec Kit artifacts for #1273.

## Phase 2: Red Tests

- [x] T004 Add focused failing tests for restoring a mutated Mortal World file during Chaos Sea validation repair.
- [x] T005 Add focused failing tests for deleting a newly created Mortal World file during Chaos Sea validation repair.
- [x] T006 Add documentation/source-guard test for general prompt/docs impact checks across Mortal World and afterlife.

## Phase 3: Runtime Implementation

- [x] T007 Implement safe auto-rollback from validated pending snapshot.
- [x] T008 Write `game_state/control/validation_auto_rollback_report.json`.
- [x] T009 Wire auto-rollback into `GameEngine` before GM repair request creation.
- [x] T010 Ensure validation reruns after auto-rollback and repair requests omit resolved realm-segregation violations.

## Phase 4: Docs and Prompts

- [x] T011 Update daemon repair wording if needed.
- [x] T012 Update afterlife contract docs/examples.
- [x] T013 Update `AGENTS.md` general prompt/docs synchronization guardrail.

## Phase 5: Verification

- [x] T014 Run focused runtime tests.
- [x] T015 Run afterlife docs/source-guard tests.
- [x] T016 Run `git diff --check`.
- [x] T017 Repeat #1249 Chaos Sea live Codex GM bridge test.

## Live Test Notes

- 2026-06-25: Ran a Chaos Sea live turn through Agent Console + ConPTY bridge with `codex --dangerously-bypass-approvals-and-sandbox`.
- The GM first wrote forbidden Mortal World files during the afterlife turn. The client wrote `game_state/control/validation_auto_rollback_report.json`, restored `game_state/factions/faction_chronicles.json`, `faction_core.json`, `faction_custom.json`, `faction_projects.json`, and `game_state/npcs/npc_core.json` from the pending-turn snapshot, and deleted newly created forbidden faction files.
- The remaining repair request was not for restored Mortal World baseline data. It was for afterlife guardian scope/reasoning and materialized guardian mirror consistency; the client accepted the repaired state and returned to the next player prompt.
- Harness follow-up: the live run needed two repair attempts for guardian-scope consistency. Track this separately as a GM harness issue rather than weakening #1273's realm rollback scope.
