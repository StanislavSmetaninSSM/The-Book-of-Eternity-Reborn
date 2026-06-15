# Requirements Checklist: Daren Scene 14 Full Literary Page (#982)

## Scope and Traceability

- [X] Source issue #982 is linked in `spec.md`, `plan.md`, `tasks.md`, and contract.
- [X] Parent #955 is linked and explicitly remains open.
- [X] Previous scene #981 and next scene #983 remain related context rather than current source scope.
- [X] Spec Kit is justified by player-facing console/browser UX content and durable handoff requirements.
- [X] No GM-facing prompt/example/runtime contract change is intended.

## Product Requirements

- [X] `chase_chain` prose rejects the current one/two-sentence synopsis form.
- [X] Daren remains the active point-of-view protagonist.
- [X] Rear courtyard, wall, cart/wagon, alley, lantern/guard, and bridgeward route details are present.
- [X] Daren's breath, body, steps, memory, and route rhythm are present.
- [X] Stolen staff/futlyar, belt/strap balance, and noise/evidence pressure are visible.
- [X] Orvald/guards/voices/lanterns/dogs or equivalent pursuit pressure is visible.
- [X] Trace/mud/footprint/readable-route pressure is visible.
- [X] The scene naturally narrows into the existing `chase_chain_action` / `PromptChain` action.
- [X] Default prose contains no implementation or agent terminology.

## Mechanics Preservation

- [X] Route id and beat order remain unchanged.
- [X] `chase_chain` beat id/title and shared `beat.PlayerText == chapter.Narrative` remain intact.
- [X] `chase_chain_action` id/label/check type/routing/scoring remain unchanged.
- [X] Reward tiers/profile/New Game grants/endpoints/runtime state/frontend files remain unchanged.
- [X] Sibling scenes and result/aftermath surfaces are not rewritten.

## Verification

- [X] Focused Daren baseline was captured before implementation: 81 passed / 0 failed / 0 skipped / 81 total.
- [X] Affected Daren/QTE/docs/browser baseline was captured before implementation: 350 passed / 0 failed / 0 skipped / 350 total.
- [X] Spec Kit prerequisite check resolves `specs/982-daren-chase-chain`.
- [X] RED focused guard failure is observed before production prose changes.
- [X] GREEN focused Daren tests pass after implementation.
- [X] Affected slice and builds pass after implementation.
- [X] `git diff --check` and added-line static scan pass.
- [ ] Independent review approves before PR/merge.
