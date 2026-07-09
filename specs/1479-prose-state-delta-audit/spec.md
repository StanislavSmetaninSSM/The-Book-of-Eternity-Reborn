# Feature Specification: Prose State Delta Audit

**Feature Branch**: `main`

**Created**: 2026-07-09

**Status**: Draft

**Input**: Live Agent Console golden-route blocker: GM prose used a learned skill and revealed quest clues, but accepted state did not record skill mastery progress or quest progress.

## Source Issues & Scope

- **Source GitHub issue(s)**:
  - #1479 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1479
- **Issue type**: validation and harness/RLM bug.
- **Spec Kit justification**: This changes accepted-turn validation, GM repair expectations, player-facing state authority, and live-test methodology.
- **Contract scope**: validation, runtime-state, GM-facing prompts/docs/examples, live-test notes.
- **Out of scope**:
  - Full natural-language understanding of every possible quest clue.
  - Rebalancing skill mastery gains.
  - Replacing GM judgment when the scene truly has no mechanical progress.

## User Scenarios & Testing

### User Story 1 - Skill Use Must Have State Evidence (Priority: P1)

As a player, when the GM says one of my known skills helped in the scene, the accepted state records a corresponding skill/mastery delta or explicitly records why no mechanical progress was awarded.

**Why this priority**: The game must not tell the player a skill mattered while leaving progression unchanged without explanation.

**Independent Test**: Build a Mortal accepted-turn fixture where `output/narrative_response.json` says `Чтение печатей` helped, the player knows that skill, and no skill delta/no-progress rationale exists. Validation must reject the turn with a repair-friendly issue.

**Acceptance Scenarios**:

1. **Given** the player knows `Чтение печатей`, **When** accepted prose says that skill helped reveal a detail, **Then** validation requires a skill mastery delta, a `skill_mastery.json` record, or an explicit accepted no-progress rationale.
2. **Given** the GM intentionally awards no mastery progress, **When** the prose still mentions the skill, **Then** the accepted state must include a player-safe no-progress rationale explaining why the skill did not advance.
3. **Given** prose merely lists a skill name in a menu, detail screen, or debug-free summary without claiming scene use, **When** validation runs, **Then** it must not reject the turn.

---

### User Story 2 - Quest Clues Must Persist (Priority: P1)

As a player, when a scene reveals a concrete clue for an active quest, the quest log records that clue or the GM records why it is only color text.

**Why this priority**: Investigation progress becomes useless if it exists only in transient prose.

**Independent Test**: Build a Mortal accepted-turn fixture where the active quest `Печать Серебряной Луны` is referenced and prose reveals a concrete clue, but `regular_quests.json` is unchanged and no no-progress rationale exists. Validation must reject the turn with a repair-friendly issue.

**Acceptance Scenarios**:

1. **Given** an active Mortal quest exists, **When** accepted prose says a clue was found, discovered, noticed, revealed, proved, or opened for that quest, **Then** validation requires an update to the relevant quest file or an explicit accepted no-progress rationale.
2. **Given** the prose only repeats an old quest title without new discovery language, **When** validation runs, **Then** it must not reject the turn.
3. **Given** a repair packet is generated, **When** the GM reads it, **Then** it names the missing state surfaces and asks for a narrow correction instead of broad rewrites.

## Requirements

### Functional Requirements

- **FR-001**: Accepted-turn validation MUST scan player-facing narrative/interface text for known player skill display names used with action/success verbs.
- **FR-002**: When FR-001 matches, validation MUST require evidence in skill mastery/progression state or an explicit no-progress rationale.
- **FR-003**: Accepted-turn validation MUST scan active quest titles and discovery/clue verbs for likely quest progress.
- **FR-004**: When FR-003 matches, validation MUST require a quest state/log update or an explicit no-progress rationale.
- **FR-005**: Repair messages MUST be narrow, Russian-readable for GM context, and include the skill/quest name and expected files.
- **FR-006**: The GM-facing guidance MUST explain that prose success, skill use, and clue discovery must be backed by canonical state deltas or a rationale.
- **FR-007**: Live-test notes MUST include a check that scenes using trained skills and investigation clues persist state.

### Non-Functional Requirements

- **NFR-001**: The audit must be conservative enough to avoid rejecting ordinary command output or passive skill lists.
- **NFR-002**: The implementation must not require a full LLM/NLP pass inside the validator.
- **NFR-003**: Validation must stay deterministic and file-backed.

## Success Criteria

- **SC-001**: A regression test reproducing issue #1479 fails before implementation and passes after implementation.
- **SC-002**: Focused validation tests pass.
- **SC-003**: Documentation/source-guard tests for GM-facing examples pass.
- **SC-004**: A repeated live golden-route scene that uses a learned skill either records skill/quest progress or enters repair with a useful packet.
