# Requirements Checklist: Daren Scene 15 Full Literary Page (#983)

## Scope and Traceability

- [X] Source issue #983 is linked in `spec.md`, `plan.md`, `tasks.md`, and contract.
- [X] Parent #955 is linked and explicitly remains open.
- [X] Previous scene #982 and result/aftermath issues #988-#1014 remain related context rather than current source scope.
- [X] Spec Kit is justified by player-facing console/browser UX content and durable handoff requirements.
- [X] No GM-facing prompt/example/runtime contract change is intended.

## Product Requirements

- [X] `hideout_return` prose rejects the current one/two-sentence synopsis form.
- [X] Daren remains the active point-of-view protagonist.
- [X] Bridge, water, wet stone, hideout/shelter, cache/taynik/stone details are present.
- [X] Daren's breath, body, hands, shoulders, listening, and deliberate cleanup choices are present.
- [X] Stolen staff/futlyar, belt/strap balance, sealing/hiding, and quiet handling are visible.
- [X] Orvald/guards/voices/lanterns/dogs or equivalent pursuit pressure is visible.
- [X] Trace/mud/footprint/blood/evidence cleanup or misdirection is visible.
- [X] The scene naturally narrows into the existing `hideout_return_action` / `BranchChoice` action.
- [X] Default prose contains no implementation or agent terminology.

## Mechanics Preservation

- [X] Route id and beat order remain unchanged.
- [X] `hideout_return` beat id/title and shared `beat.PlayerText == chapter.Narrative` remain intact.
- [X] `hideout_return_action` id/label/check type/config/terminal routing/scoring remain unchanged.
- [X] Reward tiers/profile/New Game grants/endpoints/runtime state/frontend files remain unchanged.
- [X] Sibling/result scenes and parent #955 are not rewritten or closed.

## Verification

- [X] Focused Daren baseline was captured before implementation: 82 passed / 0 failed / 0 skipped / 82 total.
- [X] Affected Daren/QTE/docs/browser baseline was captured before implementation: 351 passed / 0 failed / 0 skipped / 351 total.
- [X] Spec Kit prerequisite check resolves `specs/983-daren-hideout-return`.
- [X] RED focused guard failure is observed before production prose changes. Evidence: focused Daren suite failed 82 passed / 1 failed / 0 skipped / 83 total because the existing synopsis was 239 chars, 2 scene sentences, 1 Daren mention, and missed required motif groups.
- [X] GREEN focused Daren tests pass after implementation. Evidence: 83 passed / 0 failed / 0 skipped / 83 total.
- [X] Affected slice and builds pass after implementation. Evidence: affected slice 352 passed / 0 failed / 0 skipped / 352 total; client and test builds succeeded with 0 warnings / 0 errors.
- [X] `git diff --check` and added-line static scan pass. Evidence: working-tree `git diff --check` returned exit 0; added-line static scan over production/test code returned `NO_MATCHES`.
- [X] Independent review approves before PR/merge. Evidence: read-only Codex review run `E:/Games/codex-runs/20260615-181020-review-boe-983-daren-hideout-return` returned `APPROVED`; no blocking findings; confirmed 3117 chars / 27 sentences / 9 Daren mentions with 483 CRLF-counted chars below the 3600 cap, preserved mechanics contract, and no forbidden player-facing implementation terminology.
