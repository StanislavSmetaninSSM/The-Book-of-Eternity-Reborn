# Requirements Checklist: Daren Scene 13 Full Literary Page (#981)

## Scope and Traceability

- [X] Source issue #981 is linked in `spec.md`, `plan.md`, `tasks.md`, and contract.
- [X] Parent #955 is linked and explicitly remains open.
- [X] Prior scene #980 and next scene #982 remain related context rather than current source scope.
- [X] Spec Kit is justified by player-facing console/browser UX content and durable handoff requirements.
- [X] No GM-facing prompt/example/runtime contract change is intended.

## Product Requirements

- [X] `pursuit` prose rejects the current one/two-sentence synopsis form.
- [X] Daren remains the active point-of-view protagonist.
- [X] Hall/window/courtyard/lantern/guard first-dash setting details are present.
- [X] Daren's breath, body, steps, and timing control are present.
- [X] Stolen staff/futlyar, belt/strap balance, and noise/evidence pressure are visible.
- [X] Captain Orvald Shpil, Lukyan, guard, witness, voice, or equivalent pursuit pressure is visible.
- [X] The scene naturally narrows into the existing `pursuit_action` / `TimingBar` action.
- [X] Default prose contains no implementation or agent terminology.

## Mechanics Preservation

- [X] Route id and beat order remain unchanged.
- [X] `pursuit` beat id/title and shared `beat.PlayerText == chapter.Narrative` remain intact.
- [X] `pursuit_action` id/label/check type/routing/scoring remain unchanged.
- [X] Reward tiers/profile/New Game grants/endpoints/runtime state/frontend files remain unchanged.
- [X] Sibling scenes and result/aftermath surfaces are not rewritten.

## Verification

- [X] Focused Daren baseline was captured before implementation: 80 passed / 0 failed / 0 skipped / 80 total.
- [X] Affected Daren/QTE/docs/browser baseline was captured before implementation: 349 passed / 0 failed / 0 skipped / 349 total.
- [X] Spec Kit prerequisite check resolves `specs/981-daren-first-dash`.
- [X] RED focused guard failure was observed before production prose changes.
- [X] GREEN focused Daren tests pass after implementation.
- [X] Affected slice and builds pass after implementation.
- [X] `git diff --check` and added-line static scan pass.
- [ ] Independent review approves before PR/merge.
