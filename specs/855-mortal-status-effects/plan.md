# Implementation Plan: Mortal Status Effect Fallback

**Source Issue**: [#855](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/855)
**Spec**: `specs/855-mortal-status-effects/spec.md`
**Branch / Worktree**: `fix/855-mortal-status-effects` at `E:/Games/worktrees/boe-855-mortal-status-effects`
**Constitution**: `.specify/memory/constitution.md` v1.1.0

## Technical Context

- Runtime: .NET 8 C# client, tests under `BookOfEternityClient.Tests`.
- Relevant command code: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`.
- Relevant state loading: `BookOfEternityClient/Core/StateManager.cs` and `BookOfEternityClient/Models/GameState/AggregatedGameState.cs`.
- Relevant tests: `BookOfEternityClient.Tests/ExplorerModeCommandTests.GeneralPanels.cs` and existing helpers in the partial `ExplorerModeCommandTests` class.
- Host note: `dotnet test` is a no-op under SDK 10 unless the test project is forced with `-p:IsTestProject=true`; use that property for actual test execution.

## Architecture

Implement a focused command-result fallback inside the Mortal World effects command path. Prefer reusing the refreshed `StateManager.CurrentState.PlayerStatus` for `currentCondition` and `activeConditions`. If `currentConditionDescription` is not currently loaded into the aggregate state, add the smallest state property needed so `/эффекты` can render it without introducing a new schema or gameplay authority.

The fallback should be a player-facing panel/table/text block that explains that a detailed effect card has not been recorded yet and then shows the status information already visible to the player. Keep existing structured `effects.json` summary/raw JSON behavior intact.

## Spec Kit Applicability

Spec Kit is applicable because #855 changes player-facing command UX and summary/detail authority behavior, and it is a concrete child of the broader validation audit #857. This feature directory links #855 in `spec.md`, `plan.md`, and `tasks.md`.

## Testing Strategy

Use TDD:

1. Add a failing regression test for `/эффекты` with Mortal World status conditions and missing `effects.json`.
2. Verify the new test fails for the old behavior.
3. Implement the smallest fallback.
4. Verify the focused test passes.
5. Run the focused ExplorerMode command suite.

Baseline already run in this worktree:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"
```

Result: passed, 277 total / 277 passed / 0 failed / 0 skipped.

## Verification Commands

Run these before PR/merge:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~TryProcessCommand_Effects_InMortalRealm_WithStatusConditionsAndMissingStructuredEffects_RendersStatusFallback" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Also run an added-line static scan for secrets/shell/eval/SQL risk excluding run artifacts.

## Docs / Prompt Impact

Expected: no GM prompt/docs update, because this is a client-owned fallback presentation for already-existing status fields. Re-evaluate this if implementation adds validation rules, changes accepted GM output shape, or modifies runtime contracts.

## Risks

- Fallback copy could accidentally become developer-facing; tests or review should check for raw file/API/debug terms.
- If `currentConditionDescription` is not loaded today, the state model change must stay presentation-only and avoid new GM contract semantics.
- `effects.json` structured data must not be hidden or replaced by fallback status text.
