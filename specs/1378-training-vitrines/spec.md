# Feature Specification: Training Vitrines

**Feature Branch**: `feature/1378-training-system`

**Created**: 2026-07-03

**Status**: Draft

**Input**: User request to add mentor/teacher training vitrines for Mortal World skills and afterlife Spiritual Arts while preserving free roleplay training.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1377 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1377
  - #1378 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1378
  - #1379 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1379
  - #1380 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1380
  - #1381 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1381
  - #1382 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1382
  - #1383 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1383
  - #1384 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1384
  - #1385 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1385
  - #1455 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1455
  - #1460 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1460
- **Issue type**: epic plus implementation tasks.
- **Spec Kit justification**: This feature changes player-facing progression, client-owned purchases, GM-authored NPC/mentor contracts, validation/normalizer behavior, console/browser command output, examples, and live-test coverage.
- **Contract scope**: player-facing, GM-facing prompts, runtime-state, validation, docs, examples, console, browser, frontend.
- **Out of scope**:
  - Replacing free roleplay training. The GM can still grant skills/arts through scenes when canonical requirements are met.
  - Full economy rebalance outside training prices.
  - Multiplayer training synchronization.
  - Browser visual redesign beyond rendering the new training data according to the accepted data-card prototype.

## User Scenarios & Testing

### User Story 1 - Mortal NPC Teacher Showcase (Priority: P1)

As a Mortal World player, I can open a teacher's training showcase, see what the NPC can teach, why some offers are locked, and buy a skill or mastery upgrade with money plus current-level XP progress.

**Why this priority**: This is the primary new gameplay loop. It turns hidden roleplay-only learning into a readable game system without removing roleplay freedom.

**Independent Test**: Use a Mortal World test session with an NPC teacher who knows Knife Handling, Skinning, and Archery. Open the training command, buy a new skill, then buy one legal mastery upgrade.

**Acceptance Scenarios**:

1. **Given** the current location has an NPC teacher with a fresh showcase, **When** the player opens training, **Then** the UI lists teachable offers with teacher cap, player status, price, requirements, and lock reasons.
2. **Given** the player has enough money, enough current-level XP progress, and relationship requirements are met, **When** the player buys a new skill, **Then** the client grants the skill, deducts money and XP progress, records a receipt, and refreshes display without requiring a GM turn.
3. **Given** the player already knows the skill, **When** the offer is shown, **Then** it becomes a mastery upgrade and cannot exceed the teacher's own mastery/cap.
4. **Given** the teacher profile or relationship changed since the showcase was built, **When** the player tries to buy, **Then** purchase is blocked and the UI asks to refresh the showcase.

---

### User Story 2 - Afterlife Mentor Showcase (Priority: P1)

As a soul in the Chaos Sea or Shining Abode, I can train standard Spiritual Arts through a mentor/representative showcase at a discount, while self-training remains possible but expensive.

**Why this priority**: Afterlife progression already exists, but mentors should matter and special arts need a clearer source than raw self-upgrade.

**Independent Test**: Use an afterlife save with a Guardian/Abode mentor. Open mentor training, upgrade one standard Spiritual Art through the mentor, then compare the fallback self-upgrade cost.

**Acceptance Scenarios**:

1. **Given** the active afterlife mentor has teachable Spiritual Arts, **When** the player opens mentor training, **Then** the UI lists legal art upgrades with mentor cap, player tier, Enlightenment/progression cap, reputation modifier, and price.
2. **Given** relationship is neutral, good, or excellent, **When** the mentor price is computed, **Then** the multiplier is 100%, 80%, or 60% respectively.
3. **Given** the player uses fallback self-training for a standard Spiritual Art, **When** the price is shown, **Then** the cost is 400% of the base upgrade price.
4. **Given** the player uses fallback self-training for Soul Focus/base AP capacity, **When** the price is shown, **Then** the cost is 300% of the base upgrade price.
5. **Given** the player tries to open a new special Spiritual Art through fallback self-training, **When** the action is requested, **Then** it is blocked and the UI explains that special arts require a mentor, story reward, Shining Abode source, or explicit receipt.

---

### User Story 3 - Showcase Refresh and Validation Guards (Priority: P1)

As the system, I can detect stale or impossible training data before it mutates canonical progression.

**Why this priority**: Training purchases are client-owned after the showcase is prepared. Without staleness and validation, old GM data could grant illegal skills or cheap upgrades.

**Independent Test**: Create a showcase, mutate the teacher/mentor relationship or cap, then attempt purchase and validation.

**Acceptance Scenarios**:

1. **Given** a training showcase has a teacher state hash, relationship snapshot, player progression snapshot, and synced turn, **When** any authority changes, **Then** purchase is blocked until refresh.
2. **Given** the GM authors a teacher offer above the teacher's own known skill/art cap, **When** validation runs, **Then** the turn is rejected with a clear repair message.
3. **Given** a purchase receipt claims a skill/art without matching fresh offer and legal resource deduction, **When** validation runs, **Then** the receipt is rejected.

---

### User Story 4 - Console Training UI (Priority: P2)

As a console player, I can open training menus, inspect offers, buy legal upgrades, request refresh, and return back without raw JSON or technical keys.

**Why this priority**: Console is the most mature client and must remain the source of correct player-facing information.

**Independent Test**: Run `/обучение` in Mortal World and afterlife saves. Inspect offer list, details, purchase, refresh, and back actions.

**Acceptance Scenarios**:

1. **Given** training is available, **When** `/обучение` is entered, **Then** the console shows a compact selector of teachers/mentors and offer cards in Russian.
2. **Given** an offer is selected, **When** details are opened, **Then** requirements, price, caps, resource impact, and lock reasons are shown as readable sections.
3. **Given** training is unavailable because a showcase is missing or stale, **When** the command is entered, **Then** the console explains what is missing and dispatches the GM refresh request immediately instead of waiting for the next player input.

---

### User Story 5 - Browser Training UI (Priority: P2)

As a browser player, I can inspect and buy training offers through the approved nested-card format rather than raw tables or one-line serialized data.

**Why this priority**: Browser client must match console information and the project's accepted rich data-card prototype.

**Independent Test**: Open the browser client on Mortal and afterlife test saves and compare training details with console output.

**Acceptance Scenarios**:

1. **Given** a training command result has offers, **When** the browser renders it, **Then** each teacher/mentor and offer uses nested cards, localized labels, readable lists, and no semicolon-packed fields.
2. **Given** many offers exist, **When** the browser renders them, **Then** the player can select/filter the target teacher/mentor/offer instead of scrolling an unbounded wall.
3. **Given** an offer has an image-capable teacher/mentor, **When** the card is opened, **Then** image preview/open actions follow the data prototype rules.

---

### User Story 6 - GM Authoring Examples and Live-Test Coverage (Priority: P2)

As the GM and harness, I know how to author teacher/mentor vitrines and how to test them in live play.

**Why this priority**: The GM cannot read implementation code during play. The feature needs docs/examples and must become part of the live-test checklist.

**Independent Test**: Validate examples and run a short live test that requests a teacher showcase, buys training, refreshes stale data, and checks afterlife mentor pricing.

**Acceptance Scenarios**:

1. **Given** a Mortal World teacher scene, **When** the GM prepares state, **Then** an example shows legal teacher profile, offer list, requirements, and receipt closure.
2. **Given** an afterlife mentor scene, **When** the GM prepares state, **Then** an example shows mentor teaching standard art, special-art source limits, and fallback costs.
3. **Given** live-test planning, **When** the checklist is updated, **Then** training vitrines are explicitly tested after implementation.

### Edge Cases

- The player has enough money but not enough current-level XP progress.
- The player has enough total lifetime XP, but spending current-level progress would cross below zero.
- The player is at a progression cap and cannot legally raise the skill/art.
- The NPC/mentor can teach the skill, but relationship/reputation, quest, faction, branch, or flag requirements are not met.
- The showcase exists in the wrong realm: Mortal NPC training while afterlife is active, or afterlife mentor training while Mortal World is active.
- A showcase references a missing teacher/mentor, missing skill/art, duplicate offer id, negative cost, or unknown currency.
- A teacher/mentor was changed by the GM after showcase sync.
- A special Spiritual Art exists on a mentor but `canTeachPlayer` or training conditions are false.
- Browser receives nested details or many offers and must not flatten them into unreadable rows.
- Console receives dynamic names/descriptions and must escape markup safely.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST support Mortal World teacher profiles that declare teachable skills, maximum mastery/cap per skill, relationship/quest/flag/faction requirements, price, and descriptive training text.
- **FR-002**: The system MUST support afterlife mentor profiles or mentor-training metadata for Guardian/Abode/resident/Shining actors that declare teachable standard Spiritual Arts and, when legal, special Spiritual Arts.
- **FR-003**: The system MUST expose a training showcase command in console and browser for the active realm.
- **FR-004**: The showcase MUST list available and locked offers with localized labels, price, player current value, teacher/mentor cap, progression cap, requirements, and clear lock reasons.
- **FR-005**: The client MUST be able to complete legal training purchases locally after a fresh showcase exists.
- **FR-006**: Mortal training purchases MUST spend money plus a percent of the player's current-level XP progress budget and MUST NOT delevel the character.
- **FR-007**: Mortal skill mastery upgrades MUST NOT exceed the teacher's own cap or the player's progression cap.
- **FR-008**: Afterlife mentor purchases MUST spend afterlife currencies and MUST NOT exceed mentor tier or player Enlightenment/progression caps.
- **FR-009**: Afterlife mentor prices MUST use 100% base cost at neutral relation, 80% at good relation, and 60% at excellent relation or relevant personal-quest trust.
- **FR-010**: Self-training fallback MUST remain available for standard Spiritual Art upgrades at 400% base cost and for Soul Focus/base AP capacity at 300% base cost.
- **FR-011**: Self-training fallback MUST allow upgrades of already-known special Spiritual Arts only at 500% base cost and MUST NOT unlock a new special Spiritual Art.
- **FR-012**: New special Spiritual Arts MUST require a mentor, story reward, Shining Abode source, or explicit validated learning receipt.
- **FR-013**: Training showcases MUST carry staleness metadata: realm, source actor id, source actor snapshot hash, relationship/reputation snapshot, player progression snapshot, synced turn/cycle, and offer revision.
- **FR-014**: The client MUST block purchase from stale showcases and provide a refresh/request action.
- **FR-014a**: When opening `/обучение` creates or reuses a pending showcase request and no usable fresh showcase is available, console and browser clients MUST dispatch the corresponding dedicated GM action immediately without waiting for the player to close the command screen, press a key, or type an unrelated next turn.
- **FR-015**: Validation MUST reject impossible offers, stale receipts, resource mismatches, wrong realm updates, illegal special-art fallback unlocks, and teacher/mentor caps that exceed source actor capabilities.
- **FR-016**: GM-facing prompts, TaskGuides, OtherGuides, examples, manifests, and source-guard/documentation tests MUST be updated when the contract is implemented.
- **FR-017**: Console output MUST avoid raw JSON, internal keys, untranslated enums, and semicolon-packed details.
- **FR-018**: Browser output MUST follow the approved data-card prototype: nested cards, localized labels, collapsible large sections, readable lists, selectors for large collections, image preview when available, and no raw flattened tables for structured gameplay data.
- **FR-019**: Live-test checklists MUST include Mortal teacher training, afterlife mentor training, fallback self-training costs, stale showcase refresh, and roleplay training materialization.

### Key Entities

- **TrainingTeacherProfile**: Mortal NPC training metadata: teacher id/name, realm, relationship gates, teachable skill offers, source skill caps, refresh metadata.
- **TrainingMentorProfile**: Afterlife actor training metadata: mentor id/name/type, realm, reputation gates, teachable art offers, source art tiers, special-art rules, refresh metadata.
- **TrainingShowcase**: Client/GM shared offer surface with staleness snapshot, localized title/summary, offers, lock reasons, and refresh status.
- **TrainingOffer**: A single learn/upgrade option with target kind, current player value, target value, teacher/mentor cap, cost, requirements, lock state, and descriptive text.
- **TrainingPurchaseReceipt**: Canonical record of a successful client-owned purchase, including source offer id, pre/post values, deductions, snapshot reference, and timestamp/turn.
- **TrainingRefreshRequest**: Pending/control request asking the GM to materialize or refresh a teacher/mentor showcase when missing or stale.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A Mortal player can learn one new skill and upgrade one existing skill from a teacher through console without a GM turn after the showcase is fresh.
- **SC-002**: An afterlife player can upgrade one standard Spiritual Art through a mentor and sees the discounted price compared with self-training fallback.
- **SC-003**: Validator rejects at least three illegal cases: stale showcase purchase, teacher cap violation, and special-art fallback unlock.
- **SC-004**: Console and browser show the same training offer facts for at least one Mortal and one afterlife test save.
- **SC-005**: Browser training output contains no raw JSON, semicolon-packed nested details, or internal English keys in player-facing labels.
- **SC-006**: Documentation/example tests prove both Mortal World and afterlife GM authoring paths.

## Verification Plan

- **C# verification**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "Training|Skill|SpiritualArt|Validation"
```

- **Documentation/contract verification**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

- **Frontend verification**:

```powershell
npm --prefix BookOfEternityClient.WebFrontend run verify
```

- **Manual/player-facing verification**:
  - Run console `/обучение` in Mortal World, Chaos Sea, and Shining Abode test saves.
  - Run browser training command results and compare against console facts.
  - Include training in the next live GM test checklist after implementation.

## Assumptions

- The client already owns parts of progression purchases, so training purchases can follow the same authority style as shops and Spiritual Art upgrades.
- Current-level XP progress can be represented or derived without deleveling; if the current model lacks a direct field, implementation must add a safe field or block XP spending until canonical support exists.
- Existing NPC/guardian/resident profiles can be extended rather than replaced.
- The first implementation may ship with command-driven refresh requests; fully automated GM showcase generation can be improved later.
- Browser visual work should reuse the existing accepted data-card renderer and not introduce a second incompatible presentation system.
