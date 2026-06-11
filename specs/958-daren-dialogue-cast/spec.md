# Feature Specification: Daren NPC Dialogue Cast

**Feature Branch**: `work/958-daren-dialogue-cast`
**Created**: 2026-06-11
**Status**: Draft for autonomous implementation
**Source Issues**: [#958](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958), parent [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955), prerequisite spine [#956](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956), prerequisite prose [#957](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957), base scenario [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## Source Issues & Scope

- **Source GitHub issue**: #958 — add NPC cast, dialogue choices, and response variants to the Daren QTE heist.
- **Parent**: #955 — move Daren's QTE training mode toward an interactive-book heist while staying inside the existing QTE engine.
- **Prerequisites**:
  - #956 — `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` defines the beat order, cast slots, pacing, and handoff hooks.
  - #957 — shared C# Daren route prose now frames every QTE node as story text.
- **Related base implementation**: #919 — Daren standalone QTE showcase route, reward profile, New Game reward grant, console/browser surfaces.
- **Spec Kit justification**: #958 is medium player-facing UX/content work that changes shared QTE route flow/copy and console/browser presentation semantics. It must be durable and handoff-safe under parent #955, so Spec Kit artifacts are required.
- **In scope**: a named/personified Daren NPC cast, multiple dialogue moments with answer choices, NPC response variants for success/partial/fail or choice outcomes, score/risk/prose influence through existing QTE route fields, and regression guards that prove the scenario contains people-driven interactions.
- **Out of scope**: a new dialogue runtime/engine, a separate browser-only or console-only dialogue path, new QTE mini-game/check types, reward-profile/New Game grant changes, expanded endings/reward presentation (#960), broad content-quality gates (#961), and branch-specific consequence systems beyond this slice (#959).

## User Scenarios & Testing

### Scenario 1 - Daren meets identifiable people, not only obstacles (Priority: P1)

A player moving through the heist can name or recognize the recurring/personified figures who shape the route: a contact or informant, estate staff/guard, magical-security authority or house representative, and a pursuit figure.

**Independent Test**: Add a focused route/content guard over `QteSceneService.GetDarenShowcaseRoute()` and/or `DarenQteNarrativeSpine.json` that fails unless the cast has at least four named/personified figures tied to the required #956 cast slots and visible in route copy or choice text.

**Acceptance Scenarios**:
1. **Given** the early approach/preparation beats, **When** the player reads the route copy, **Then** an informant/contact role is visible as a person rather than an abstract hint.
2. **Given** interior stealth or pressure beats, **When** the player reads the route copy, **Then** estate staff/guard reactions are visible as human witnesses.
3. **Given** magical-security and pursuit beats, **When** the player reads route copy, **Then** a house/ward authority and a named pursuer are visible as people who respond to Daren's choices.

---

### Scenario 2 - Dialogue choices use existing QTE choice mechanics (Priority: P1)

The player sees multiple dialogue or social-pressure moments implemented with existing `BranchChoice` / `PrecisionChoice` style route actions/configuration, not a bespoke dialogue engine.

**Independent Test**: Add tests that locate multiple Daren dialogue/social-choice actions or chapters and assert they use existing QTE check types (`BranchChoice` and/or `PrecisionChoice`), have player-facing answer labels/descriptions, and contain success/partial/fail response text.

**Acceptance Scenarios**:
1. **Given** a dialogue moment with the contact/informant, **When** choices are inspected, **Then** the options are authored as route action/config data and resolve to success/partial/fail grades.
2. **Given** a social-pressure moment with staff/guard or house authority, **When** the action resolves, **Then** the NPC response differs between strong, risky, and poor answers.
3. **Given** the browser receives the route DTO, **When** it renders the choice check, **Then** it uses the same shared route action/config data as the console client.

---

### Scenario 3 - Dialogue outcomes influence later route pressure without new state (Priority: P1)

Dialogue choices should matter in the existing route model by changing immediate response prose, score/risk deltas, routing within the authored QTE flow, or later scene text that clearly references prior social pressure.

**Independent Test**: Add focused tests that assert dialogue actions include non-empty score deltas or route targets and that later copy/result text references at least one earlier person/choice consequence. If Codex chooses to add small dialogue chapters, tests should also assert the original Daren heist beats remain present in order as a subsequence.

**Acceptance Scenarios**:
1. **Given** a strong answer, **When** the result text appears, **Then** it names the NPC's useful reaction or reduced risk.
2. **Given** a risky/partial answer, **When** later prose appears, **Then** the player sees uncertainty, suspicion, or evidence pressure carry forward.
3. **Given** a failed answer, **When** scoring and result text are inspected, **Then** existing metrics such as stealth, pursuit control, evidence, or hideout safety reflect the increased risk.

---

### Scenario 4 - The shared QTE contract remains the authority (Priority: P1)

Console and browser consume the same Daren route data; implementation remains inside the existing QTE scenario flow.

**Independent Test**: Existing Daren/browser contract tests continue to pass, and new tests must inspect shared C# route data rather than React-only copy. A guard should fail if a new dialogue runtime/DTO family is introduced for this issue.

**Acceptance Scenarios**:
1. **Given** the route is shown in console, **When** a dialogue choice appears, **Then** it is a normal QTE action/chapter in the route flow.
2. **Given** the route is shown in browser, **When** the same choice appears, **Then** the browser receives it through existing QTE route/action/config serialization.
3. **Given** implementation is complete, **When** code is reviewed, **Then** there is no new one-off dialogue service, state file, endpoint, or frontend-only story fork.

## Edge Cases

- Existing #957 prose must remain intact and player-facing; dialogue additions should enrich it rather than reverting to bare mechanics.
- If dialogue is implemented by adding extra route chapters, the original 12 Daren heist beats must remain present in their original relative order and the #956 spine must be updated to document the inserted dialogue beats.
- If dialogue is implemented by changing existing `BranchChoice`/`PrecisionChoice` actions, update the #956 spine and tests so route/spine QTE-type expectations remain truthful.
- NPC and answer labels should stay concise enough for console and browser choice surfaces.
- Branch-specific expanded consequences beyond immediate response/risk/prose hooks remain #959 unless needed to satisfy #958 acceptance.
- Ending/epilogue and reward-presentation expansion remains #960.

## Functional Requirements

- **FR-001**: The Daren showcase MUST define at least four named/personified figures corresponding to the #956 cast slots: contact/informant, estate staff/guard, magical-security authority or house representative, and pursuit figure.
- **FR-002**: At least three Daren route moments MUST present dialogue/social-pressure choices through existing QTE action/check data.
- **FR-003**: Dialogue/social-choice actions MUST use existing QTE check types only (`BranchChoice`, `PrecisionChoice`, or other already-supported choice-like mechanics if justified by current code); no new QTE check type may be introduced.
- **FR-004**: NPC response text MUST differ for success, partial, and fail outcomes or equivalent choice grades.
- **FR-005**: At least one dialogue/social-choice outcome MUST affect existing score/risk metrics such as stealth, pursuit control, evidence, loot, or hideout safety.
- **FR-006**: Later route prose or result text MUST visibly reference at least one earlier NPC interaction or social consequence.
- **FR-007**: Console and browser MUST consume the same authored Daren dialogue/choice content from shared C# QTE route data.
- **FR-008**: The implementation MUST preserve Daren route id, reward profile semantics, permanent reward writes, New Game grants, and ordinary campaign mutation boundaries from #919.
- **FR-009**: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json` and this Spec Kit feature MUST stay aligned with the new cast/dialogue scope and source links.
- **FR-010**: The implementation MUST NOT introduce a separate dialogue runtime, new state file, new endpoint, React-only copy fork, or GM-authored campaign contract change.

## Non-Functional Requirements

- **NFR-001**: Dialogue should reinforce the dark-fantasy heist tone: whispered bargains, staff suspicion, magical household authority, and personal pursuit pressure.
- **NFR-002**: Tests should verify objective structure and player-facing boundaries, not subjective prose quality.
- **NFR-003**: Guard failures should name the missing cast slot, dialogue action/chapter, or route copy surface.
- **NFR-004**: The implementation should stay reviewable and avoid broad refactors of the QTE engine.

## Success Criteria

- The player can identify at least four named/personified figures in the Daren heist.
- The route contains multiple dialogue/social-choice moments with answer options and distinct NPC response variants.
- Dialogue choices influence immediate prose and at least one existing risk/score surface without new runtime/state machinery.
- Focused Daren tests and the affected QTE/docs/browser slice pass locally with exact counts recorded.
- Spec Kit artifacts link #958/#955/#956/#957/#919 and are discoverable through the repo-local prerequisite helper.
- The final PR documents that #959-#961 remain follow-ups and that no separate dialogue engine was introduced.
