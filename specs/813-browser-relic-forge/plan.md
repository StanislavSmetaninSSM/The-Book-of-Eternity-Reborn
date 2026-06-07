# Implementation Plan: Browser Shining Abode Relic Forge

**Source Issue**: [#813](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/813)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Feature Spec**: [spec.md](spec.md)
**Feature Branch**: `task/813-browser-relic-forge`

## Summary

Implement Browser Client parity for the console Shining Abode relic forge flow: choose faction, forge action, Soul Relic, action-specific parameters, preview quoted costs, and write the existing Shining core forge request contract. Reuse existing C# forge quote/write authority; React remains generic prompt/result presentation.

## Technical Context

- Runtime: .NET 8 C# client in `BookOfEternityClient/`.
- Tests: xUnit in `BookOfEternityClient.Tests/`.
- Browser shell: C# web command/prompt-session services plus tracked React fixtures in `BookOfEternityClient.WebFrontend/`.
- Existing console authority: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs`.
- Existing forge preview authority: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs`.
- Existing write authority: `ShiningCoreActionRequestState`, `ShiningAbodeState.TryQuoteForgeAction`, and `WriteForgeRequestWithRelicRerollCommitAsync`.
- Existing browser Shining patterns: #810/#811/#812 implementations in `ExplorerCommandCatalog.cs`, `ExplorerWebPromptSessionService.cs`, `BrowserAfterlifeWriteService.cs`, `BrowserPlayerCommandMenuBuilder.cs`, `BrowserCommandCoverageService.cs`, `ExplorerHelpCommandResultBuilder.cs`, `ExplorerShiningAbodeCommandResultBuilder.cs`, and focused browser parity tests.

## Constitution / Governance Checks

- GitHub issue traceability: all implementation changes are tied to #813 and umbrella #817.
- Player-facing integrity: default browser labels/blockers/results must be in-world Russian and must not expose raw `.json`, `pending_`, DTO/API, endpoint, validation, or debug wording.
- Contract authority: no afterlife runtime contract shape change is planned. If implementation requires one, update this plan plus afterlife contract docs/examples/tests before continuing.
- Test-first verification: add focused RED tests/source guards before production code.
- Orchestration: Hermes owns final PR/merge/issue closure; Codex implements and reports evidence.

## Project Structure / Files

### Expected production files

- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`
  - Add browser-discoverable command descriptor/aliases for the #813 Shining relic forge guided flow if missing.
- `BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs`
  - Add player-facing help rows for the forge command/action where Shining browser actions are enumerated.
- `BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs`
  - Add command-result blocks/open-prompt metadata for forge, following #811/#812 Shining action patterns.
- `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs`
  - Route #813 mutating command(s) through the local UI lock and Shining prompt/write flow.
  - Build prompt forms for faction, action type, relic, action-specific fields, optional reroll, and confirmation.
- `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs`
  - Add write handlers for #813 prompt submissions.
  - Use `ShiningAbodeState.TryQuoteForgeAction` and `ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync` before writing.
  - Use `ShiningCoreActionRequestState.WriteForgeRequestWithRelicRerollCommitAsync` so relic-reroll entitlements commit only on confirmed writes.
- `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`
  - Expose player-facing action menu entry for forge when appropriate; keep default labels Russian/player-facing.
- `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs`
  - Remove #813 as an open forge gap once commands are covered; keep #817 open for remaining sibling parity.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/command-coverage.json`
  - Refresh if command coverage changes.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`
  - Refresh if command/action metadata changes affect the fixture.

### Expected test files

- `BookOfEternityClient.Tests/WebUi/BrowserShiningRelicForgeParityTests.cs` (create)
  - RED/GREEN tests for opening the forge prompt, submitting each forge action, stale prompt blockers, and player-facing copy guards.
- Existing browser command/menu/help/coverage tests as needed:
  - `BrowserCommandCoverageServiceTests`
  - `BrowserPlayerCommandMenuBuilderTests`
  - `ExplorerWebCommandServiceTests`
  - `BrowserApiContractTests`
  - `AfterlifeShiningPlayerFacingSourceGuardTests`

### Spec Kit artifacts

- `specs/813-browser-relic-forge/spec.md`
- `specs/813-browser-relic-forge/plan.md`
- `specs/813-browser-relic-forge/tasks.md`

## Implementation Phases

### Phase 1 — Spec Kit setup and source inspection

1. Confirm `AGENTS.md`, `.specify/memory/constitution.md`, issue #813, umbrella #817, and this feature directory are aligned.
2. Inspect console forge flow completely before implementing browser parity:
   - `HandleShiningForgeRequestAsync`
   - `PromptForShiningForgeActionType`
   - `PromptForForgeReshapeTargetFormTagAsync`
   - `PromptForForgeReplacementPropertyAsync`
   - `PromptForForgeAddedProperties`
   - `PromptForSoulRelic`
   - `PromptForRelicPropertyIndex`
   - `ConfirmShiningCoreActionRequestPreview`
3. Inspect #811/#812 browser Shining prompt/write patterns and reuse structure instead of inventing React gameplay handlers.

### Phase 2 — RED tests and source guards

1. Add focused browser parity test for opening a forge prompt with available faction/action/relic choices.
2. Add reshape submit tests covering `targetFormTag`, quoted costs, `ActionTypeForgeRelicReshape`, and relic-reroll commit behavior.
3. Add retune submit tests covering `propertyIndex`, `replacementProperty`, and optional reroll commit behavior.
4. Add strengthen submit tests covering `propertyIndex` and quoted costs.
5. Add stabilize submit tests covering `ActionTypeForgeRelicStabilizeEcho` and no browser-only mutation.
6. Add uplift submit tests covering `addedProperties` and quoted costs.
7. Add stale/direct guard tests for realm, pending core action/local write, missing relic, invalid action, invalid property, invalid target form, exhausted reroll, and insufficient-resource blockers.
8. Add/update command coverage, command menu/help, API fixture, and source guard tests proving #813 actions are browser-supported and player-facing.
9. Run the focused test command and record expected RED counts in `tasks.md`.

### Phase 3 — Minimal implementation

1. Add/adjust command descriptors and aliases for the Shining relic forge flow.
2. Add prompt builders that enumerate player-facing factions, forge actions, relics, properties, form choices, and property templates from canonical C# state.
3. Add `BrowserAfterlifeWriteService` handlers that re-check state at submit time and write only through existing C# authority.
4. Update menu/help/coverage fixtures and command results.
5. Keep runtime contract shape unchanged; do not modify GM-facing afterlife contracts unless implementation discovers a required contract change.

### Phase 4 — GREEN verification and reconciliation

1. Run the focused RED/GREEN filter and record exact counts.
2. Run broader Shining/browser/API/docs gates appropriate to the final diff.
3. Run C# builds, frontend verification when frontend fixtures/assets change, `git diff --check`, and added-line static scan excluding Spec Kit docs if necessary.
4. Update `tasks.md` with actual verification evidence and final task statuses.
5. Commit focused changes with `[skip ci]`; leave PR/merge/issue closure to Hermes.

## Acceptance Criteria Mapping

- Spec US1 / FR-001/FR-002 -> forge command + prompt builder + open/blocker tests.
- Spec US2 / FR-003/FR-004 -> reshape submit handler + reroll commit tests.
- Spec US3 / FR-005/FR-006/FR-007/FR-008 -> retune/strengthen/stabilize/uplift prompt and write tests.
- Spec US4 / FR-010/FR-011 -> help/menu/coverage/API/source guard tests.
- Spec FR-012 / SC-005 -> no contract shape changes; docs tests only required if a contract shape or GM-facing guidance changes.

## Verification Plan

Baseline before implementation in the fresh worktree:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningTradeAndForge|BrowserAfterlifeWriteServiceTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Baseline observed by Hermes before implementation on 2026-06-07: passed, 0 failed / 199 passed / 0 skipped / 199 total. Restore/build ran first in the fresh worktree and produced test binaries normally. Spec Kit prerequisite check returned `FEATURE_DIR=E:\Games\worktrees\boe-813-relic-forge\specs\813-browser-relic-forge`.

Focused expected after adding RED tests:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningRelicForgeParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserCommandCoverageServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Final local gates to run when relevant:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningTradeAndForge|BrowserAfterlifeWriteServiceTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal
git diff --check origin/main...HEAD
```

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if React/TypeScript frontend files or generated browser fixtures/build artifacts change in a way that affects frontend verification.

## Risks and Mitigations

- **Risk**: Browser submit path bypasses local write/GM-turn safety. **Mitigation**: use existing prompt-session/local-write owner patterns and stale-submit tests.
- **Risk**: Browser forge logic drifts from console quote/write authority. **Mitigation**: call existing `ShiningAbodeState.TryQuoteForgeAction`, `ValidateRequestAgainstCurrentStateAsync`, and `WriteForgeRequestWithRelicRerollCommitAsync` instead of duplicating rules.
- **Risk**: Player-facing blockers leak raw pending/control diagnostics. **Mitigation**: sanitize shared validation messages for browser defaults and add source/result guards.
- **Risk**: Uplift/retune structured properties are hard to express as a guided form. **Mitigation**: use player-facing template/choice prompts first; if manual structured entry is necessary, keep it behind guided fields and validate through C# authority before write.
- **Risk**: Runtime contract shape change becomes necessary. **Mitigation**: stop, revise Spec Kit artifacts, and update afterlife contract matrix/examples/coverage tests before continuing.
