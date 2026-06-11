# Requirements Checklist: Daren Endings and Reward Presentation

**Feature**: `specs/960-daren-endings-rewards`
**Reviewed**: 2026-06-11

## Completeness

- [x] Source issues are linked: #960, #955, #956, #957, #958, #959, #919.
- [x] Scope separates #960 endings/reward presentation from #961 broad content-quality gates.
- [x] Console/browser shared ending data parity is explicit.
- [x] Existing #919 reward mechanics are listed as invariants.
- [x] No new reward profile file, ending-state runtime, QTE check type, campaign-state side effect, or frontend-only ending mapping is allowed by this slice.
- [x] Objective regression guards are defined for ending epilogue presence/distinctness, reward explanation, shared DTO/console availability, and unchanged mechanics.
- [x] User correction is captured: short/dry ending summaries are unacceptable, and Daren must remain the protagonist of substantial dark-fantasy ending pages.

## Player-Facing Ending Boundaries

- [x] Every outcome, including `no_reward_failure`, must have substantial multi-sentence authored epilogue prose.
- [x] Ending prose must be Daren-centered and readable outside the game as authored dark-fantasy fiction, with tests guarding only structural proxies.
- [x] Ending epilogues must distinguish poor, mixed, good, and excellent/perfect outcomes.
- [x] Ending copy must react to score/performance and consequence categories from the route without inventing a new branch-memory runtime.
- [x] Reward-granting endings must explain the permanent achievement and future New Game Ink Feather amount in-world, without raw `+N` receipt or "future bonus" wording.
- [x] No-reward outcomes must explain why no permanent profile write happens.
- [x] Browser completion must not label a lower replay tier as the saved future New Game reward when a higher best tier is already present.

## Verification Expectations

- [x] Focused Daren tests are required.
- [x] Affected Daren/QTE/docs/browser C# slice is required.
- [x] Client and test-project builds are required when C# changes.
- [x] Spec Kit prerequisite helper and `git diff --check` are required before PR.
- [x] Frontend verify is required only if React/frontend files change or a browser display bug is found.

## Ambiguity Review

- [x] #960 may add shared C# ending/DTO fields, but it must not change reward thresholds, bonus values, profile path, or New Game idempotency.
- [x] Tier-level consequence language is acceptable when route-specific branch memory is not stored; route-specific choices remain visible through #959 result/carry-forward text.
- [x] Daren showcase content is client-owned, so GM-facing docs are not expected unless the GM-authored QTE contract changes.
- [x] Broad scenario-wide content-quality scoring/checks remain #961; #960 tests should focus on ending/reward presentation only.
