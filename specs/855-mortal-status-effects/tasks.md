# Tasks: Mortal Status Effect Fallback

**Source Issue**: [#855](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/855)
**Spec**: `specs/855-mortal-status-effects/spec.md`
**Plan**: `specs/855-mortal-status-effects/plan.md`

## Phase 1: Regression Test (TDD RED)

- [X] Add a focused regression test in `BookOfEternityClient.Tests/ExplorerModeCommandTests.GeneralPanels.cs` named `TryProcessCommand_Effects_InMortalRealm_WithStatusConditionsAndMissingStructuredEffects_RendersStatusFallback`.
- [X] Seed Mortal World state, write `game_state/core/player_status.json` with `currentCondition`, `currentConditionDescription`, and two `activeConditions`, and ensure `game_state/player/effects.json` is absent or empty.
- [X] Run the focused test and verify it fails against the current implementation because `/эффекты` renders no status fallback.

## Phase 2: Minimal Implementation (GREEN)

- [X] Update the Mortal World effects command path in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` to render a status fallback from `StateManager.CurrentState.PlayerStatus` when structured effects have no visible detail.
- [X] If needed, update `BookOfEternityClient/Core/StateManager.cs` and `BookOfEternityClient/Models/GameState/AggregatedGameState.cs` to carry `currentConditionDescription` as presentation data.
- [X] Keep existing structured `effects.json` summary/raw JSON output intact when it exists.
- [X] Keep fallback copy player-facing and avoid raw file/API/DTO/debug language.

## Phase 3: Verification and Review Prep

- [X] Run the focused new test with `-p:IsTestProject=true` and confirm it passes.
- [X] Run the focused ExplorerMode command test suite with `-p:IsTestProject=true` and confirm it passes.
- [X] Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- [X] Run `git diff --check origin/main...HEAD`.
- [X] Run added-line static security scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting.
- [X] Reconcile this Spec Kit feature: no tasks should be marked complete in this file unless implementation and verification evidence exist.

## Phase 4: Review Reconciliation

- [X] Keep the console status fallback visible when `effects.json` has no visible details even if wounds or custom states render elsewhere on the effects panel.
- [X] Move the active Spec Kit feature directory to `specs/855-mortal-status-effects/` so branch-derived Spec Kit tooling can discover it.
- [X] Rerun the focused fallback regressions, ExplorerMode command suite, ExplorerWeb command suite, build, `git diff --check`, Spec Kit prerequisites, and added-line static scan after review fixes.
