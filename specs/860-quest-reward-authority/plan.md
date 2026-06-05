# Implementation Plan: Quest Reward Detail Authority

**Source Issue**: [#860](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/860)
**Spec**: `specs/860-quest-reward-authority/spec.md`
**Branch / Worktree**: `fix/860-quest-reward-authority` at `E:/Games/worktrees/boe-860-quest-reward-authority`
**Constitution**: `.specify/memory/constitution.md` v1.1.0

## Technical Context

- Runtime: .NET 8 C# client, file-backed JSON game state.
- Validation entry point: `BookOfEternityClient/Services/Validation/ValidationService.QuestsAndSoulState.cs`, especially `ValidateQuestHistoryData`.
- Current symptom: `questRewards[].itemsReceived`, `skillsUnlocked`, and `relationshipChanges` are only `RequireArrayOfStrings(...)`; orphan ids pass validation.
- Quest player surface: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.QuestsAndRivals.cs` renders completed rewards under `🎁 Фактически получено`.
- Nearby result builder: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` may need parity if `/quests` browser/shared result consumes quest summaries.
- Normalizer: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.QuestsAndRivals.cs` and `CanonicalStateNormalizer.SharedAndSoulHelpers.cs` preserve quest history/rewards. Do not introduce broad normalizer rewrites unless needed to preserve accepted structured reward objects.
- GM docs/examples: search `Rules/`, `TaskGuides/`, `OtherGuides/`, `Examples/`, and documentation guard tests for `questRewards`, `itemsReceived`, `skillsUnlocked`, `relationshipChanges`, and `quest_history.json` before changing docs.
- Host note: use `-p:IsTestProject=true` for `dotnet test` so newer SDKs discover real tests.

## Baseline Evidence

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Quest|FullyQualifiedName~Validation|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 1596/1596, 0 failed, 0 skipped on clean `origin/main`/`63c11e9`.

## Architecture

Add a focused quest reward authority validator beside existing quest history validation. The validator should collect reward references from both legacy string arrays and structured reward objects, then resolve them against canonical detail state and explicit historical/unavailable records. Keep the check conservative: unresolved player-facing reward ids are errors; historical/prior-incarnation rewards are accepted only when a player-facing reason explains why no current detail object exists.

Rendering should reuse the same interpretation for `/квесты`: show player labels, resolved detail hints when available, or clear historical/unavailable text. Do not parse mechanics or create gameplay effects from reward strings. The change is validation/display/docs only.

## Proposed Contract Shape

Support existing legacy strings when they resolve, and add structured reward objects for authoring clarity. A structured record should include a stable id or actor id, a display label/name, and one of:

- resolvable authority fields such as current inventory/skill/relationship ids or detail links already used by the repo; or
- `authorityStatus`/`availability`/equivalent explicit status such as `HistoricalOnly` or `Unavailable`, plus `reason`/`historicalReason`/equivalent player-facing text.

Codex should inspect existing naming conventions before choosing exact property names. If new property names are introduced, document them in GM-facing rules/examples and cover them with tests.

## Implemented Contract Shape

The implementation keeps legacy strings valid only when they resolve to current canonical detail authority. Structured reward objects are accepted for all three quest reward arrays:

- `itemsReceived[]`: resolves by `itemId`, `existedId`, `inventoryItemId`, `authorityId`, `detailId`, `id`, `itemName`, or `name` against `game_state/inventory/items.json`.
- `skillsUnlocked[]`: resolves by `skillId`, `authorityId`, `detailId`, `id`, `skillName`, or `name` against `game_state/player/skills_active.json` and `skills_passive.json`.
- `relationshipChanges[]`: resolves by `npcId`, `NPCId`, `actorId`, `targetActorId`, `relationshipId`, `authorityId`, `detailId`, `id`, `npcName`, `NPCName`, `actorName`, or `name` against `game_state/npcs/npc_relationships.json` and `npc_core.json`. Legacy relationship strings with suffixes like `_+20` resolve by stripping the signed delta.

Explicit historical/unavailable reward objects are accepted when `authorityStatus`, `availability`, `availabilityStatus`, `authorityState`, `state`, or `status` carries values such as `HistoricalOnly`, `Unavailable`, `PriorIncarnation`, `Consumed`, `Sold`, `Forgotten`, `Lost`, or `Destroyed`, and a player-facing `reason`, `historicalReason`, `unavailableReason`, `unresolvedReason`, `authorityReason`, or `historyReason` is present.

Validation issue codes implemented exactly as spec-listed:

- `quest_reward_item_missing_detail_authority`
- `quest_reward_skill_missing_detail_authority`
- `quest_reward_relationship_missing_detail_authority`
- `quest_reward_history_reason_missing`

The `/квесты` completed-history display now uses the same resolver to show display labels, resolved names, or Russian historical/unavailable reason text without raw JSON/id leakage.

## Implementation Phases

1. **TDD RED - validation**
   - Add tests for resolved item reward, missing item reward, resolved skill unlock, missing skill unlock, resolved relationship change, missing relationship change, and historical-only reward with explicit reason.
   - Run focused test and verify failures are due to missing authority validation, not fixture syntax.
2. **GREEN - validation resolver**
   - Implement minimal helpers to load/cross-check inventory items, active/passive skills or skill history, and NPC/relationship state/history.
   - Add issue codes for missing item/skill/relationship reward authority and missing historical reason.
3. **TDD RED/GREEN - player display**
   - Add or update ExplorerMode command tests so `/квесты` renders resolved labels and historical/unavailable reasons instead of raw orphan ids.
   - Implement scoped rendering changes in `ExplorerMode.QuestsAndRivals.cs` and shared command-result code only if needed.
4. **Docs / contract reconciliation**
   - Update GM-facing rules/examples for quest reward authoring.
   - Add or update documentation/source-guard tests requiring the new contract language and at least one worked example.
5. **Spec reconciliation and verification**
   - Update `tasks.md` with RED/GREEN/final verification evidence.
   - Run focused and docs verification, build, diff check, and static added-line scan.

## Risks and Constraints

- Do not force old/prior-incarnation rewards into current inventory or skill state; accept explicit historical-only reasons.
- Do not accidentally allow object display text alone to authorize unresolved ids. Authority needs an id/detail link or explicit historical/unavailable status with reason.
- Do not expose raw file paths, validation issue codes, or DTO names in default player-facing `/квесты` output.
- If resolving relationships is ambiguous, prefer explicit structured relationship reward records over guessing from strings like `npc_guild_master_+20`.
- If broader #857 gaps are discovered, create follow-up issues; do not expand this PR into the entire audit.

## Verification Plan

Minimum commands:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Quest|FullyQualifiedName~Validation|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|Documentation|PromptDocumentationCoverageTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Also run the issue #860 added-line static security scan command from the implementation prompt.

Run broader `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --logger "console;verbosity=minimal"` if shared validation helpers are changed broadly or if focused tests suggest cross-surface risk.

## Verification Evidence

- TDD RED focused command: failed 6, passed 4, skipped 0, total 10, proving missing authority issue codes, historical reason enforcement, `/квесты` player-facing rendering, and docs example requirements were not implemented yet.
- Focused GREEN command: passed 10/10, 0 failed, 0 skipped.
- Required quest/validation/ExplorerMode filter: passed 1606/1606, 0 failed, 0 skipped.
- Required docs/contract filter: passed 113/113, 0 failed, 0 skipped.
- Required client build: succeeded, 0 warnings, 0 errors.
- Broader C# suite after generating ignored frontend `dist/`: passed 3375/3375, 0 failed, 0 skipped.
- `npm ci --prefix BookOfEternityClient.WebFrontend`: succeeded with 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend`: succeeded; vitest passed 23/23 and generated ignored built frontend assets required by the broad C# smoke tests.
- Working-tree `git diff --check`: passed with line-ending normalization warnings only.
- Working-tree added-line security scan: `NO_MATCHES`.
- Spec Kit prerequisite check: returned active `specs/860-quest-reward-authority` feature directory with `tasks.md` available.
- Post-commit `git diff --check origin/main...HEAD`: passed.
- Post-commit added-line security scan from the implementation prompt: initially found the raw scan regex documented in this plan; after replacing that raw command with a descriptive reference, rerun returned `NO_MATCHES`.
- Post-commit Spec Kit prerequisite check: returned active `specs/860-quest-reward-authority` feature directory with `tasks.md` available.
