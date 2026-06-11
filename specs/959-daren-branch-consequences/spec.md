# Feature Specification: Daren Branch Consequences

**Feature Branch**: `work/959-daren-branch-consequences`
**Created**: 2026-06-11
**Status**: Draft for autonomous implementation
**Source Issues**: [#959](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite spine [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), prerequisite prose [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), prerequisite dialogue/cast [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), base scenario [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Source Issues & Scope

- **Source GitHub issue**: #959 — add branch-specific consequences for choices and QTE performance in the Daren interactive-book heist.
- **Parent**: #955 — make the Daren QTE training route feel like an interactive book while staying inside the existing QTE engine.
- **Prerequisites**:
  - #956 — `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` defines the beat order, pacing, cast slots, and consequence hooks.
  - #957 — shared C# Daren route prose frames each QTE node.
  - #958 — named NPC cast and dialogue/social-choice moments now exist in the shared route.
- **Related base implementation**: #919 — Daren standalone showcase route, reward profile, New Game reward grant, console/browser surfaces.
- **Spec Kit justification**: #959 is medium player-facing UX/story work that changes shared route consequence text, scoring/outcome semantics, and console/browser presentation. It depends on previous durable Daren artifacts and must remain handoff-safe under parent #955, so Spec Kit artifacts are required.
- **In scope**: distinct narrative feedback for strong/partial/poor QTE results, choice-dependent consequence text after dialogue/planning decisions, carry-forward echoes into later scenes, playable bad outcomes where the existing route allows continued play, and regression guards that prove branch text is not removed.
- **Out of scope**: a separate campaign-state or branch-memory system, new QTE check types, new dialogue/runtime/state files, browser-only consequence copy, reward-profile/New Game grant changes, expanded ending/epilogue/reward presentation (#960), and broad content-quality gate infrastructure (#961).

## User Scenarios & Testing

### Scenario 1 - QTE quality produces visible story consequences (Priority: P1)

A player who performs strongly, partially, or poorly in key Daren QTE scenes sees distinct narrative results that explain how the heist changes, instead of only seeing generic pass/fail text or silent score changes.

**Independent Test**: Add focused guards over `QteSceneService.GetDarenShowcaseRoute()` that locate key Daren actions and fail unless success/partial/fail branches contain materially distinct consequence prose for stealth, time/noise, suspicion/evidence, pursuit pressure, or hideout safety.

**Acceptance Scenarios**:
1. **Given** a stealth or infiltration QTE, **When** its success/partial/fail texts are inspected, **Then** the texts describe different degrees of secrecy, delay, or evidence pressure.
2. **Given** a magical-security or lock QTE, **When** the player performs poorly but the route can continue, **Then** the result text explains an improvised detour or increased risk rather than collapsing into a generic failure.
3. **Given** a chase or pursuit QTE, **When** the player performs strongly or poorly, **Then** the branch text changes pursuit control and immediate narrative pressure.

---

### Scenario 2 - Dialogue and planning decisions carry consequences forward (Priority: P1)

The player can make choices in the already-landed Daren dialogue/planning moments, and later scene text references at least several earlier decisions or results.

**Independent Test**: Add tests that identify multiple earlier Daren choice/result hooks and assert later route prose or result text refers back to them by NPC, clue, route choice, ward pressure, evidence, or pursuit consequence.

**Acceptance Scenarios**:
1. **Given** the player negotiates with the informant, **When** later infiltration or security scenes render, **Then** route text can reference the clue quality or social bargain.
2. **Given** the player handles a guard/staff social moment poorly, **When** later theft or pursuit text renders, **Then** suspicion or witness pressure is visible.
3. **Given** the player chooses or performs around a house/ward authority, **When** later chase/hideout text renders, **Then** the magical-security consequence remains visible.

---

### Scenario 3 - Bad outcomes stay playable where scenario design allows (Priority: P1)

Failure should be interesting and legible. Bad outcomes may reduce score, increase risk, change route pressure, reduce reward potential, or alter later prose; they should not become immediate generic failure unless the existing route contract already treats that beat as terminal.

**Independent Test**: Add tests that fail if key non-terminal Daren actions use identical/generic failure copy, route directly to an unrelated terminal failure, or lack a continued route/score consequence when the surrounding heist should keep moving.

**Acceptance Scenarios**:
1. **Given** a non-terminal action fails, **When** routing/result text is inspected, **Then** the route still offers a plausible next scene with increased danger where the existing QTE contract supports continuation.
2. **Given** a poor branch produces lost time/noise/suspicion, **When** later scene text is inspected, **Then** the player can see why the heist is harder.
3. **Given** a terminal failure is intentionally retained, **When** it is inspected, **Then** the text is specific to the Daren scene rather than generic.

---

### Scenario 4 - Consequences use the standard shared QTE contract (Priority: P1)

Console and browser receive the same branch-specific consequence data from the shared C# Daren route. No frontend-only or console-only consequence system is introduced.

**Independent Test**: Existing Daren/browser contract tests continue passing, and new tests inspect shared C# route data and `DarenQteNarrativeSpine.json`. A guard should fail if implementation adds a new consequence engine, campaign-state file, endpoint, React-only story fork, or QTE check type for this issue.

**Acceptance Scenarios**:
1. **Given** the console renders a result branch, **When** the same route data is serialized for browser, **Then** both clients see the same authored consequence text and score/risk deltas.
2. **Given** a branch-specific consequence is added, **When** code is reviewed, **Then** it lives in standard route/action/result/score fields, not a new campaign-state mechanism.
3. **Given** the feature is complete, **When** #960/#961 are planned, **Then** ending/reward presentation and broad quality-gate work remain separate follow-ups.

## Edge Cases

- #958 dialogue/social-choice moments must remain intact; #959 may deepen their consequences but must not replace the cast/dialogue work with a new system.
- If new branch text is represented by additional helper metadata, it must remain private/shared-route support only unless the existing QTE DTO contract requires a small test-backed extension.
- Non-terminal poor outcomes should continue route play only where existing routing allows it; do not weaken intentional terminal safety/failure behavior.
- Consequence prose must remain concise enough for console and browser surfaces.
- Daren reward-profile thresholds and New Game reward grants from #919 must not change.
- Expanded ending/epilogue/reward presentation belongs to #960, even if this feature adds setup text that endings can consume later.

## Functional Requirements

- **FR-001**: Key Daren QTE actions MUST have distinct success, partial, and fail consequence prose that goes beyond generic pass/fail wording.
- **FR-002**: At least several earlier choices or QTE results MUST be referenced later in route prose or result text.
- **FR-003**: At least one dialogue/planning decision from #958 MUST affect later consequence prose through existing QTE route/action/result data.
- **FR-004**: Poor outcomes in non-terminal scenes MUST remain playable where the existing route design allows, with specific increased risk, detour, suspicion, noise, lost-time, or pursuit pressure text.
- **FR-005**: Branch consequences MUST use existing QTE route/routing/result/score fields and shared C# route authority; no separate campaign-state or branch-memory system may be added.
- **FR-006**: Console and browser MUST consume the same branch consequence content through shared route data.
- **FR-007**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` MUST record #959 source/consequence handoff truth without drifting from the actual route.
- **FR-008**: The implementation MUST preserve Daren route id, route availability, QTE check types, reward profile semantics, permanent reward writes, New Game grants, and ordinary campaign mutation boundaries from #919.
- **FR-009**: Tests/source guards MUST fail if branch-specific consequence text is removed, collapses to identical/generic text, or moves into a browser-only/console-only fork.
- **FR-010**: The feature MUST NOT implement #960 ending/reward presentation or #961 broad content-quality gates as side effects.

## Non-Functional Requirements

- **NFR-001**: Consequence prose should reinforce dark-fantasy heist tension: suspicion, witnesses, ward resonance, noise, lost time, improvised detours, and personal pursuit.
- **NFR-002**: Tests should verify objective structure, branch distinction, carry-forward references, and contract boundaries rather than subjective literary quality.
- **NFR-003**: Guard failures should name the missing action/branch/carry-forward reference so future agents can fix content drift quickly.
- **NFR-004**: The implementation should stay reviewable and avoid QTE engine refactors unless a failing test proves a narrow shared-route support fix is necessary.

## Success Criteria

- Key Daren QTE actions have visibly different strong/partial/poor consequence prose.
- Multiple earlier decisions or QTE results are referenced later in the run.
- Non-terminal bad outcomes remain playable with specific pressure/detour/reduced-control text where route design allows.
- Focused Daren tests and the affected QTE/docs/browser slice pass locally with exact counts recorded.
- Spec Kit artifacts link #959/#955/#956/#957/#958/#919 and are discoverable through the repo-local prerequisite helper.
- The final PR documents that #960 and #961 remain follow-ups and that no new campaign-state/consequence engine was introduced.
