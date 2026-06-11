# Requirements Checklist: Daren NPC Dialogue Cast

**Feature**: `specs/958-daren-dialogue-cast`
**Reviewed**: 2026-06-11

## Completeness

- [x] Source issues are linked: #958, #955, #956, #957, #919.
- [x] Scope separates #958 dialogue/NPC work from #959 branch consequence expansion, #960 endings/rewards, and #961 broad quality gates.
- [x] Console/browser shared-route parity is explicit.
- [x] No new dialogue runtime, QTE check type, frontend-only story fork, reward/profile change, or New Game grant change is allowed by this slice.
- [x] Objective regression guards are defined for cast/dialogue/choice structure without subjective prose scoring.

## Player-Facing Dialogue Boundaries

- [x] Required cast slots are explicit: contact/informant, estate staff/guard, magical-security authority/house representative, pursuit figure.
- [x] Dialogue/social-choice moments must be visible through existing QTE route data.
- [x] Player-selectable answers should use existing choice config with labels/descriptions/hints.
- [x] NPC response variants must differ across success/partial/fail or equivalent grades.
- [x] At least one dialogue/social outcome must affect existing risk/score metrics.
- [x] Later prose/result text must reference at least one earlier NPC/social consequence.

## Verification Expectations

- [x] Focused Daren tests are required.
- [x] Affected Daren/QTE/docs/browser C# slice is required.
- [x] Client and test-project builds are required when C# changes.
- [x] Spec Kit prerequisite helper and `git diff --check` are required before PR.
- [x] Frontend verify is required only if React/frontend files change or browser display bug is found.

## Ambiguity Review

- [x] The feature may add dialogue/social-choice route chapters if that is the cleanest way to satisfy player-facing choices, but the original Daren heist beats must remain ordered.
- [x] The feature may update route/spine QTE-type alignment tests if the route truth changes, but it must not weaken route/spine drift protection.
- [x] `BranchChoice` is not enough by itself for interactive player answer selection unless surrounding route behavior already provides the player-facing choice; `PrecisionChoice` is preferred for explicit answer options.
- [x] Deeper branch-state memory and ending-specific consequence expansion remain #959/#960 unless minimal references are needed to prove #958 choices matter.
