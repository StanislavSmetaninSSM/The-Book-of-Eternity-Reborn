# Feature Specification: QTE Practice Mode

**Feature Branch**: `work/925-qte-practice-mode`
**Created**: 2026-06-11
**Status**: Draft for autonomous implementation
**Source Issues**: [#925 QTE Practice Mode](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/925), parent [#911 QTE v2](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/911), related [#918 Browser QTE parity](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/918), [#920 layout-independent QTE input](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/920), [#924 QTE scoring](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/924), consumer [#919 Daren training mode](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

## User Scenarios & Testing

### Scenario 1 - Player opens QTE Practice without a campaign (Priority: P1)

A player can launch a client-owned QTE Practice Mode from an appropriate console and browser surface without creating, loading, advancing, or repairing a normal campaign session. The mode is clearly labeled as training and states that it grants no rewards or story progress.

**Independent Test**: Add console/menu and browser route/API tests that open practice mode from a no-campaign state and assert no `game_session` turn, pending action, achievement, Ink Feather, XP, inventory, quest, or Daren reward state is written.

**Acceptance Scenarios**:
1. **Given** no active campaign session, **When** the player opens QTE Practice Mode, **Then** the practice catalog is available and no campaign state is created.
2. **Given** an existing campaign session, **When** the player opens and exits practice mode, **Then** the session files and permanent reward/profile state are unchanged except for explicitly local, non-campaign practice UI state if such state is needed.
3. **Given** the player sees the practice entry point, **Then** the copy says this is a training/helper mode with no rewards.

---

### Scenario 2 - Player selects any implemented QTE type and difficulty (Priority: P1)

Practice mode lists implemented QTE mini-games only: `BranchChoice`, `TimingBar`, `PromptChain`, `BalanceMeter`, `ChargeRelease`, `MashInput`, `PatternMemory`, `RhythmPulse`, `PrecisionChoice`, `StealthNoise`, and `LockPinSet`. Future or unavailable types must be hidden or clearly unavailable, never broken. The player can choose a basic difficulty preset or curated presets.

**Independent Test**: Add a practice catalog test that asserts each currently implemented QTE type has a practice definition, instructions, supported surface metadata, and at least one difficulty preset. Assert unavailable types are not rendered as playable options.

**Acceptance Scenarios**:
1. **Given** the practice catalog, **When** the player chooses an implemented QTE type, **Then** the mode starts a real practice attempt for that type.
2. **Given** a QTE type has no implementation in the current client, **When** the catalog renders, **Then** that type is hidden or marked unavailable with player-facing copy and no broken start button.
3. **Given** a difficulty preset is selected, **When** a practice attempt starts, **Then** the generated practice QTE config uses that preset deterministically.

---

### Scenario 3 - Practice attempts use real QTE implementations (Priority: P1)

Each attempt runs the existing QTE implementation path rather than a fake explanatory screen. Console practice uses the same C# QTE action resolution/keyboard handling as normal QTE play. Browser practice reuses the browser QTE mini-game parity from #918 and submits computed grades through the existing C# authority. RU/EN layout-independent key handling from #920 applies to practice prompts.

**Independent Test**: Add focused tests for at least representative v1 and v2 practice attempts that prove the attempt reaches a real `success`, `partial`, or `fail` result through existing QTE resolution helpers/endpoints/components. Add browser/frontend tests for practice mini-game rendering and QTE key normalization on the practice surface.

**Acceptance Scenarios**:
1. **Given** a `LockPinSet`, `StealthNoise`, or `RhythmPulse` practice attempt, **When** the player completes it, **Then** the result comes from the same grade/resolution logic used by normal QTE play.
2. **Given** the player's active keyboard layout emits Cyrillic fallback characters for QTE keys, **When** practice input is processed, **Then** the same #920 physical-key/fallback behavior is used.
3. **Given** browser practice starts a supported mini-game, **When** the player interacts with it, **Then** React remains presentation/input handling and C# remains result/write authority.

---

### Scenario 4 - Player gets feedback, retry, and local practice scoring (Priority: P2)

After every attempt, the player sees `success`, `partial`, or `fail` plus enough player-facing explanation to improve. The result screen lets the player retry, change difficulty, choose another QTE type, or exit. If the practiced QTE uses the standard #924 score model, scores/ranks are local/session-only training feedback and do not persist as rewards.

**Independent Test**: Add console/browser tests that complete practice attempts with deterministic grades, assert feedback copy and retry/change/exit affordances, and assert no score/rank is written to campaign progression, achievement/profile, Ink Feather, XP, inventory, quest, or Daren reward state.

**Acceptance Scenarios**:
1. **Given** a completed practice attempt, **When** the result renders, **Then** it shows grade, explanation, and next actions without raw DTO/API/debug wording.
2. **Given** a scored practice attempt, **When** the score summary appears, **Then** it is labeled as local practice feedback and does not persist as a reward.
3. **Given** the player chooses retry or another type, **When** the next attempt starts, **Then** prior practice output does not mutate campaign state.

---

### Scenario 5 - Documentation explains training boundaries (Priority: P2)

Player/help documentation and GM-facing QTE guidance explain that practice mode is client-owned training content, requires no GM-authored practice scenes, grants no rewards, and is separate from Daren showcase #919.

**Independent Test**: Documentation/source guard tests assert the practice docs mention no rewards, no campaign mutation, no GM-authored practice scenes, and the relationship to Daren practice help without making #919 a dependency.

## Requirements

### Functional Requirements

- **FR-001**: QTE Practice Mode MUST be reachable from an appropriate console and browser player-facing surface without starting or loading a normal campaign.
- **FR-002**: Practice Mode MUST present a catalog of implemented QTE types: `BranchChoice`, `TimingBar`, `PromptChain`, `BalanceMeter`, `ChargeRelease`, `MashInput`, `PatternMemory`, `RhythmPulse`, `PrecisionChoice`, `StealthNoise`, and `LockPinSet`.
- **FR-003**: Unimplemented or future QTE types MUST be hidden or clearly unavailable; they MUST NOT appear as playable broken options.
- **FR-004**: Practice Mode MUST provide at least basic difficulty presets and generate deterministic practice configs from the selected type and difficulty.
- **FR-005**: Practice attempts MUST run through real QTE implementation/resolution paths; fake explanatory-only screens do not satisfy the feature.
- **FR-006**: Practice prompts MUST reuse #920 layout-independent QTE key normalization and player-facing RU/EN key labels.
- **FR-007**: Browser Practice Mode MUST reuse #918 browser mini-game components/grade submission while preserving C# authority for result resolution/write semantics.
- **FR-008**: Practice attempt results MUST show `success`, `partial`, or `fail`, player-facing improvement feedback, and actions to retry, change difficulty, choose another type, or exit.
- **FR-009**: Practice score/rank feedback from #924 MUST be local/session-only training feedback and MUST NOT grant or persist achievements, Ink Feathers, XP, inventory, quests, Daren ending progress, or campaign progression.
- **FR-010**: Opening, completing, retrying, and exiting practice attempts MUST NOT mutate ordinary campaign state or permanent reward/profile state.
- **FR-011**: Default player UI MUST NOT expose raw endpoint, DTO, JSON, file-path, debug, manual-grade, or agent/workflow language.
- **FR-012**: Documentation/help/source guards MUST explain the training/no-reward/no-GM-scene boundary and the relationship to #919 Daren showcase.
- **FR-013**: Console client behavior outside Practice Mode MUST remain unchanged.

### Key Entities

- **Practice Catalog Entry**: Client-owned metadata for one implemented QTE type, including player-facing name, description, supported surfaces, instructions, and difficulty presets.
- **Practice Preset**: Deterministic difficulty/config template for a QTE type that can generate a practice attempt without GM-authored scene content.
- **Practice Attempt**: Ephemeral training run for a selected QTE type/difficulty that produces a grade and feedback without campaign or reward mutation.
- **Practice Feedback**: Player-facing result summary that may include local score/rank information but is explicitly non-persistent.

## Out of Scope

- Daren mini-adventure content, Daren endings, permanent achievements/profile unlocks, New Game Ink Feather grants, or reward balancing (#919).
- New GM-authored QTE action types or new canonical QTE pending/control contracts.
- Changing campaign QTE semantics outside the practice entry point and attempt loop.
- Adding a generic achievement/resource/profile reward system.
- Replacing existing browser QTE mini-games from #918 or scoring model from #924.
- Afterlife/Chaos Sea/Shining Abode pending/control contracts.

## Success Criteria

- Player can open practice mode with no active campaign and select implemented QTE types.
- Practice attempts resolve through real QTE implementation paths and produce player-facing feedback.
- Tests prove practice attempts do not mutate campaign, achievement/profile, reward, Ink Feather, XP, inventory, quest, or Daren state.
- Console and browser practice surfaces are semantically aligned.
- QTE docs/help/source guards describe no rewards, no GM-authored practice scenes, and Daren #919 relationship.
- Focused QTE/browser/docs tests, frontend verification, builds, Spec Kit prerequisites, diff hygiene, and independent review pass before PR/merge.
