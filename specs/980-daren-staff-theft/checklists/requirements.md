# Requirements Checklist: Daren Scene 12 Full Literary Page (#980)

## Scope and Traceability

- [X] Source issue #980 is linked in `spec.md`, `plan.md`, `tasks.md`, and contract.
- [X] Parent #955 is linked and explicitly remains open.
- [X] Next scene #981 and other open children remain out of scope.
- [X] Spec Kit is justified by player-facing console/browser UX content and durable handoff requirements.
- [X] No GM-facing prompt/example/runtime contract change is intended.

## Product Requirements

- [X] `staff_theft` prose rejects the current one/two-sentence synopsis form.
- [X] Daren remains the active point-of-view protagonist.
- [X] Staff/relic, velvet holders/supports, rings/suspension, and theft setting are present.
- [X] Daren's hand, breath, body, and balance control are present.
- [X] Belt/strap/futlyar securing is concrete and leads into the existing action.
- [X] Old lock/scratch, trace, alarm/listening-house, guard, or pursuit pressure is visible.
- [X] The scene naturally narrows into the existing `staff_theft_action`.
- [X] Default prose contains no implementation or agent terminology.

## Mechanics Preservation

- [X] Route id and beat order remain unchanged.
- [X] `staff_theft` beat id/title and shared `beat.PlayerText == chapter.Narrative` remain intact.
- [X] `staff_theft_action` id/label/check type/routing/scoring remain unchanged.
- [X] Reward tiers/profile/New Game grants/endpoints/runtime state/frontend files remain unchanged.
- [X] Sibling scenes and result/aftermath surfaces are not rewritten.

## Verification

- [X] Focused Daren baseline was captured before implementation.
- [X] Affected Daren/QTE/docs/browser baseline was captured before implementation.
- [X] Spec Kit prerequisite check resolves `specs/980-daren-staff-theft`.
- [X] RED focused guard failure was observed before production prose changes.
- [X] GREEN focused Daren tests pass after implementation.
- [X] Affected slice and builds pass after implementation.
- [X] `git diff --check` and added-line static scan pass.
- [ ] Independent review approves before PR/merge.
