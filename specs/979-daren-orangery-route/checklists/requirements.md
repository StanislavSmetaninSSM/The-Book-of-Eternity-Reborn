# Requirements Checklist: Daren Scene 11 Full Literary Page (#979)

## Scope and Traceability

- [X] Source issue #979 is linked in `spec.md`, `plan.md`, `tasks.md`, and contract.
- [X] Parent #955 is linked and explicitly remains open.
- [X] Next scene #980 and other open children remain out of scope.
- [X] Spec Kit is justified by player-facing console/browser UX content and durable handoff requirements.
- [X] No GM-facing prompt/example/runtime contract change is intended.

## Product Requirements

- [X] `route_decision` prose rejects the current one/two-sentence synopsis form.
- [X] Daren remains the active point-of-view protagonist.
- [X] Orangery/wet glass/plants/condensation atmosphere is present.
- [X] Prior alarm/staff-case/pursuit pressure is carried forward.
- [X] Three exits/routes and route-choice stakes are concrete.
- [X] Pursuit, trace-washing, misdirection, noise, light, or evidence pressure is visible.
- [X] The scene naturally narrows into the existing `route_decision_action`.
- [X] Default prose contains no implementation or agent terminology.

## Mechanics Preservation

- [X] Route id and beat order remain unchanged.
- [X] `route_decision` beat id/title and shared `beat.PlayerText == chapter.Narrative` remain intact.
- [X] `route_decision_action` id/label/check type/config/routing/scoring remain unchanged.
- [X] Reward tiers/profile/New Game grants/endpoints/runtime state/frontend files remain unchanged.
- [X] Sibling scenes and result/aftermath surfaces are not rewritten.

## Verification

- [X] Focused Daren baseline was captured before implementation.
- [X] Affected Daren/QTE/docs/browser baseline was captured before implementation.
- [X] Spec Kit prerequisite check resolves `specs/979-daren-orangery-route`.
- [X] RED focused guard failure was observed before production prose changes.
- [X] GREEN focused Daren tests pass after implementation.
- [X] Affected slice and builds pass after implementation.
- [X] `git diff --check` and added-line static scan pass.
- [ ] Independent review approves before PR/merge.
