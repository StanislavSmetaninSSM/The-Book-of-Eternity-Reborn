# Requirements Checklist: Daren Branch Consequences

**Feature**: `specs/959-daren-branch-consequences`
**Reviewed**: 2026-06-11

## Completeness

- [x] Source issues are linked: #959, #955, #956, #957, #958, #919.
- [x] Scope separates #959 branch consequences from #960 ending/reward presentation and #961 broad content-quality gates.
- [x] Console/browser shared-route parity is explicit.
- [x] No new branch-memory engine, campaign-state system, QTE check type, frontend-only story fork, reward/profile change, or New Game grant change is allowed by this slice.
- [x] Objective regression guards are defined for branch distinction, carry-forward references, playable bad outcomes, and standard-contract boundaries.

## Player-Facing Consequence Boundaries

- [x] Strong/partial/poor QTE performance must produce distinct consequence prose beyond generic pass/fail.
- [x] At least several earlier choices or QTE results must be referenced later in the run.
- [x] At least one #958 dialogue/planning decision must affect later consequence prose.
- [x] Non-terminal poor outcomes must remain playable where the existing route allows, with specific increased pressure or detour text.
- [x] Consequences must use shared route/action/result/score/spine data rather than browser-only or console-only copy.

## Verification Expectations

- [x] Focused Daren tests are required.
- [x] Affected Daren/QTE/docs/browser C# slice is required.
- [x] Client and test-project builds are required when C# changes.
- [x] Spec Kit prerequisite helper and `git diff --check` are required before PR.
- [x] Frontend verify is required only if React/frontend files change or a browser display bug is found.

## Ambiguity Review

- [x] #959 may add or revise result prose and spine consequence notes, but it must not implement new campaign state.
- [x] Poor outcomes can remain terminal only when the existing route design already treats that action as terminal; otherwise they should continue play with clear pressure.
- [x] Ending-specific payoffs, epilogues, and reward copy remain #960 even if #959 adds setup/carry-forward text.
- [x] Broad content-quality scoring/gates remain #961; #959 tests should focus on objective branch-consequence structure.
