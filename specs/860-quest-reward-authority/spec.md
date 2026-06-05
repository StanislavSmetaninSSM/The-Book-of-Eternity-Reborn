# Feature Specification: Quest Reward Detail Authority

**Feature Branch**: `fix/860-quest-reward-authority`
**Created**: 2026-06-05
**Status**: Implemented locally; pending Hermes independent verification, PR creation, merge, and issue closure
**Source Issue**: [#860 [Validation][Quests] Quest reward references must resolve to detail authority or explicit history](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/860)
**Parent Audit**: [#857 [Audit][Validation] Enforce player-facing summary/detail authority links](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/857)

## User Stories & Testing

### User Story 1 - Reward references resolve to inspectable authority (Priority: P1)

A player who opens `/квесты` and sees quest rewards can trust that item, skill, and relationship rewards either point at canonical detail state or explicitly say they are historical/unavailable.

**Independent Test**: Validate minimal `game_state/quests/quest_history.json` fixtures where `questRewards[]` references item rewards, skill unlocks, and relationship changes. Confirm resolved references pass and orphan references produce validation issues.

**Acceptance Scenarios**:

1. **Given** `questRewards[].itemsReceived[]` references an item that exists in current inventory/detail authority, **When** validation runs, **Then** no quest reward authority issue is emitted for that item.
2. **Given** `questRewards[].itemsReceived[]` references an item id that cannot be resolved and has no explicit historical/unavailable reason, **When** validation runs, **Then** validation reports a quest reward item authority issue.
3. **Given** `questRewards[].skillsUnlocked[]` references a skill found in active/passive skill state or skill history authority, **When** validation runs, **Then** no missing-authority issue is emitted for that skill.
4. **Given** `questRewards[].skillsUnlocked[]` references an unknown skill with no explicit historical/unavailable reason, **When** validation runs, **Then** validation reports a quest reward skill authority issue.
5. **Given** `questRewards[].relationshipChanges[]` references an NPC/actor relationship that resolves to relationship state/history, **When** validation runs, **Then** no missing-authority issue is emitted.
6. **Given** a reward is intentionally historical-only, unavailable, sold, consumed, forgotten, or from a prior incarnation, **When** the reward record includes a player-facing reason, **Then** validation accepts it and `/квесты` shows that reason instead of an orphan id.

### User Story 2 - `/квесты` remains player-facing (Priority: P1)

A player viewing completed quests can see reward labels, resolved details, or a clear unavailable/historical status without raw file paths, API language, DTO names, or validator jargon.

**Independent Test**: Command-result/ExplorerMode tests render quest history rewards for resolved and historical-only records and assert the text is in-world/player-readable.

### User Story 3 - GM can author valid quest rewards (Priority: P1)

A GM updating quest history has documented choices for current detail references and historical-only rewards, including examples for item, skill, and relationship rewards.

**Independent Test**: Documentation/source-guard tests confirm the GM-facing rules/examples describe reward authority, explicit historical/unavailable reasons, and structured reward object shapes.

## Requirements

### Functional Requirements

- **FR-001**: The validator MUST examine `game_state/quests/quest_history.json` `questRewards[]` reward references instead of only type-checking `itemsReceived`, `skillsUnlocked`, and `relationshipChanges` as string arrays.
- **FR-002**: Item rewards MUST resolve to current inventory/item detail authority or to an explicit historical/unavailable reward record with a player-facing reason.
- **FR-003**: Skill rewards MUST resolve to active/passive skill state, skill history/detail authority, or an explicit historical/unavailable reward record with a player-facing reason.
- **FR-004**: Relationship rewards MUST resolve to NPC/actor relationship state/history authority, or an explicit historical/unavailable reward record with a player-facing reason.
- **FR-005**: Bare legacy strings MAY remain accepted only when they resolve to canonical detail authority. Bare unresolved strings MUST produce validation issues.
- **FR-006**: Structured reward objects SHOULD support stable ids plus player labels and explicit authority status/reason, so historical rewards are not forced into current inventory/skill state.
- **FR-007**: `/квесты` MUST render resolved reward labels or historical/unavailable states in player-facing Russian, not raw orphan ids alone.
- **FR-008**: GM-facing rules/examples MUST document how to author resolvable and historical-only quest rewards.

### Validation Issue Expectations

Use stable, surface-specific issue codes so #857 can distinguish this surface from status effects, readable documents, and inventory bonus authority. Suggested codes:

- `quest_reward_item_missing_detail_authority`
- `quest_reward_skill_missing_detail_authority`
- `quest_reward_relationship_missing_detail_authority`
- `quest_reward_history_reason_missing`

Codex may adjust names to fit existing conventions, but codes must be specific and regression-tested.

### Out of Scope

- Do not implement a broad #857 summary/detail audit in this issue.
- Do not require historical rewards from prior incarnations to appear in current inventory/skills.
- Do not redesign the quest UI beyond the reward detail/unavailable display needed for this issue.
- Do not change afterlife pending/control runtime contracts unless investigation proves quest rewards depend on them; if they do, update afterlife docs/tests in the same PR.

## Contract Scope

- Runtime state: `game_state/quests/quest_history.json` reward records and their links to inventory, skills, NPC/relationship state/history.
- Validation: quest reward authority checks.
- Console/player surface: `/квесты` completed quest reward display.
- GM-facing docs/examples: quest reward authoring contract and worked examples.
- Browser: read-only `/quests` parity should remain consistent if it consumes the shared command result/DTO path; no new browser write flow is required.

## Verification Commands

Baseline before implementation:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Quest|FullyQualifiedName~Validation|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"
```

Expected baseline from 2026-06-05 on `fix/860-quest-reward-authority` at `63c11e9`: passed 1596/1596, 0 failed, 0 skipped.

Minimum final verification:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Quest|FullyQualifiedName~Validation|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|Documentation|PromptDocumentationCoverageTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Run broader `dotnet test` if the implementation touches shared validation helpers broadly enough to risk unrelated surfaces.
