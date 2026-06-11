# Feature Specification: Daren Literary Scene Prose

**Feature Branch**: `work/957-daren-literary-prose`  
**Created**: 2026-06-11  
**Status**: Draft for autonomous implementation  
**Source Issues**: [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite spine [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), base scenario [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Source Issues & Scope

- **Source GitHub issue**: #957 — add literary scene prose around every current Daren QTE node.
- **Parent**: #955 — move Daren's QTE training mode toward an interactive-book heist while staying inside the existing QTE engine.
- **Prerequisite**: #956 — `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` defines the beat order, arc, pacing, and handoff hooks that this prose must follow.
- **Related base implementation**: #919 — Daren showcase route, reward profile, New Game reward grant, console/browser surfaces.
- **Spec Kit justification**: #957 is player-facing UX/content work over shared QTE route data. It affects console/browser parity, default player copy, and future child handoffs, so durable Spec Kit artifacts are required.
- **In scope**: concise book-like opening prose for every current Daren beat, transition/result prose after every QTE outcome, player-facing route intro copy, regression guards that reject bare mechanical nodes, and synchronization with the #956 narrative spine.
- **Out of scope**: adding NPC dialogue choices (#958), branch-specific consequence variants beyond the existing success/partial/fail result text surface (#959), expanded endings/reward presentation (#960), broad content-quality gates beyond this slice (#961), new QTE check types, new scenario/dialogue runtime, reward/profile changes, New Game grant changes, and browser-only or console-only story forks.

## User Scenarios & Testing

### Scenario 1 - Every Daren QTE beat starts with story context (Priority: P1)

A player enters any current Daren showcase chapter and sees where Daren is, what is at stake, and why the upcoming QTE matters before the mechanic begins.

**Independent Test**: Add a focused C# regression/source guard that enumerates `QteSceneService.GetDarenShowcaseRoute().Offer.Chapters` and fails if any chapter narrative is missing, too terse, too long for console, or equal to a bare one-sentence mechanic label.

**Acceptance Scenarios**:
1. **Given** the beat `lock_pick`, **When** the chapter renders, **Then** the text frames the cabinet/threshold/stakes before the lock mini-game instruction.
2. **Given** the beat `rune_memory`, **When** the chapter renders, **Then** magical security and Daren's observation goal are visible before the memory mechanic.
3. **Given** any current beat, **When** the chapter text is inspected, **Then** it contains at least two story sentences and remains concise enough for console reading.

---

### Scenario 2 - QTE result text reads as transition prose (Priority: P1)

After each QTE action, success, partial, and fail text carry the player into the next beat instead of returning only a short mechanical result.

**Independent Test**: Add a focused C# test over every Daren action that rejects empty or very short `SuccessText`, `PartialText`, and `FailText`, rejects default technical/debug wording, and verifies every result text is bounded for console.

**Acceptance Scenarios**:
1. **Given** success on `gadget_infiltration`, **When** result text appears, **Then** it reads like a quiet climb into the next location, not only “hook works.”
2. **Given** partial success on `stealth_crossing`, **When** result text appears, **Then** it names the near-miss and why tension carries forward.
3. **Given** failure on `pursuit`, **When** result text appears, **Then** it explains the immediate danger without changing route mechanics.

---

### Scenario 3 - Console and browser consume the same authored prose (Priority: P1)

Both clients receive the same Daren route chapter/action text through shared C# QTE data; no frontend-only story copy is invented.

**Independent Test**: Existing browser/console QTE contract tests continue to consume `QteSceneService.GetDarenShowcaseRoute()`. New tests should verify authored prose on the shared route object rather than React/static UI strings.

**Acceptance Scenarios**:
1. **Given** the browser Daren showcase loads a chapter, **When** it receives QTE DTOs, **Then** it receives the same chapter narrative authored in the C# route.
2. **Given** console Daren showcase renders a chapter, **When** it calls `ShowChapterPrelude`, **Then** it uses the same `QteChapter.Narrative` and action result text.
3. **Given** this issue is content/prose-only, **When** implementation is complete, **Then** React/browser files are unchanged unless a failing parity test proves a shared DTO display bug.

---

### Scenario 4 - #956 narrative spine remains the durable handoff source (Priority: P1)

The new prose follows the beat order and dramatic roles from the narrative spine, and the spine records #957 as consumed/implemented without becoming stale.

**Independent Test**: Keep #956 drift guards passing and, where useful, assert the route still covers the same beat ids/QTE types as the spine after prose updates.

**Acceptance Scenarios**:
1. **Given** the #956 spine lists 12 current beats, **When** route prose is updated, **Then** no beat is added, removed, reordered, or assigned a different QTE type.
2. **Given** future #958-#961 tasks read the spine, **When** #957 is complete, **Then** handoff notes still identify dialogue, branch, ending, and quality-gate work as separate tasks.
3. **Given** the route intro previously used technical boundary wording, **When** the new intro is shown to a player, **Then** it remains clear and in-world without exposing GM/API/debug/Spec Kit language.

## Edge Cases

- A route beat has a mechanical label that is useful for the mini-game: keep the label clear, but do not let it replace story framing in `QteChapter.Narrative`.
- A result text could become too long for console: use guard thresholds and split future richer variations into #959/#960 rather than expanding this slice into walls of text.
- Existing reward/profile/New Game tests must continue passing; prose must not change scoring, routing, ending thresholds, or permanent rewards.
- Browser parity should be preserved through shared DTOs; do not add duplicate React copy to hide missing shared route prose.

## Functional Requirements

- **FR-001**: Every current Daren showcase beat MUST have authored opening prose in shared route data, not only a bare mechanical instruction.
- **FR-002**: Every chapter narrative MUST explain Daren's location/context, immediate stakes, and why the upcoming QTE matters.
- **FR-003**: Every Daren action MUST have success, partial, and fail result text that reads as short transition prose toward the next beat or final resolution.
- **FR-004**: Route intro/offer copy MUST be player-facing and avoid raw GM/API/debug/DTO/Spec Kit/manual-grade wording in default UI.
- **FR-005**: Prose MUST remain concise for console use: rich enough for context, but not a wall of text.
- **FR-006**: Console and browser MUST continue to display the same authored route content through shared C# QTE data.
- **FR-007**: Implementation MUST preserve the existing Daren beat order, QTE types, routing, scoring, reward profile, New Game grants, and client-owned showcase boundary from #919.
- **FR-008**: Tests/source guards MUST fail if a current beat returns to bare/mechanical-only chapter text or if outcome text becomes empty/terse/debug-like.
- **FR-009**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` and this Spec Kit feature MUST stay aligned with #957 scope and source links.
- **FR-010**: Implementation MUST NOT introduce a new scenario/dialogue runtime, new QTE mechanic, browser-only story fork, console-only story fork, or GM-authored campaign contract change.

## Non-Functional Requirements

- **NFR-001**: Prose should use dark-fantasy heist tone: stealth, social danger, magical security, night pursuit, and Daren's thief perspective.
- **NFR-002**: Regression tests should verify objective structural/player-copy boundaries, not attempt subjective literary scoring.
- **NFR-003**: Tests must provide actionable failure messages naming the beat/action and missing copy surface.
- **NFR-004**: The implementation should stay small and reviewable; later dialogue, branch, ending, and broad quality gates remain separate issues.

## Success Criteria

- All 12 current Daren QTE nodes have shared authored opening prose and no bare mechanical chapter presentation remains.
- All success/partial/fail result texts are player-facing transition prose and remain concise.
- Focused Daren tests and the affected QTE/docs/browser slice pass locally with exact counts recorded.
- Spec Kit artifacts link #957/#955/#956/#919 and are discoverable through the repo-local prerequisite helper.
- The final PR documents that #958-#961 remain follow-ups and that no QTE execution behavior changed.
