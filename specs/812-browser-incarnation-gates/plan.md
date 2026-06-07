# Implementation Plan: Browser Shining Abode Incarnation Gates

**Source Issue**: [#812](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/812)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Feature Spec**: [spec.md](spec.md)
**Feature Branch**: `task/812-browser-incarnation-gates`

## Summary

Implement Browser Client parity for the console Shining Abode Gates lifecycle: open Gates, select/deselect blessing cards, reroll the draft, and prepare the incarnation package. Reuse existing C# Shining Abode state helpers and `ShiningCoreActionRequestState` authority; React remains presentation-only over existing browser prompt-session metadata.

## Technical Context

- Runtime: .NET 8 C# client in `BookOfEternityClient/`.
- Tests: xUnit in `BookOfEternityClient.Tests/`.
- Browser shell: C# web UI/prompt-session services plus tracked React fixtures in `BookOfEternityClient.WebFrontend/`.
- Existing console authority: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Gates.cs`.
- Existing browser action patterns: #810/#811 implementations in `ExplorerWebPromptSessionService.cs`, `BrowserAfterlifeWriteService.cs`, `BrowserPlayerCommandMenuBuilder.cs`, `BrowserCommandCoverageService.cs`, `ExplorerHelpCommandResultBuilder.cs`, and `ExplorerShiningAbodeCommandResultBuilder.cs`.
- Existing pending runtime contract: `game_state/control/pending_shining_abode_actions.json` via `ShiningCoreActionRequestState`.

## Constitution / Governance Checks

- GitHub issue traceability: all changes are tied to #812 and umbrella #817.
- Player-facing integrity: default browser labels/blockers/results must be in-world Russian and must not expose raw `.json`, `pending_`, DTO/API, endpoint, or debug wording.
- Contract authority: no afterlife runtime contract shape change is planned. If implementation requires one, update this plan plus afterlife contract docs/examples/tests before continuing.
- Test-first verification: write focused RED tests/source guards before production code.
- Orchestration: Hermes owns final PR/merge/issue closure; Codex implements and reports evidence.

## Project Structure / Files

### Expected production files

- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`
  - Add browser-discoverable command descriptors/aliases for #812 Gates lifecycle commands.
- `BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs`
  - Add player-facing help rows for #812 commands where command help enumerates supported Shining actions.
- `BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs`
  - Add command-result blocks/open-prompt metadata for Gates lifecycle commands, following #811 Shining action patterns.
- `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs`
  - Route #812 mutating commands through the local UI lock and Shining Abode prompt/write flow.
- `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs`
  - Add write/local-mutation handlers for #812 prompt submissions.
  - Use `ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync`/`WriteRequestAsync` for core-action requests.
  - Use `ShiningAbodeState.TrySelectBlessingCard`, `TryDeselectBlessingCard`, and `TryRerollGatesDraft` for local Gates state mutations.
- `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`
  - Expose player-facing action menu entries for #812 only when appropriate.
- `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs`
  - Remove #812 as an open gap once commands are covered; keep #817 open for remaining sibling issues.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/command-coverage.json`
  - Refresh after command coverage changes.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`
  - Refresh if command/action metadata changes affect the fixture.

### Expected test files

- `BookOfEternityClient.Tests/WebUi/BrowserShiningIncarnationGatesParityTests.cs` (create a focused browser parity/source-guard test file)
  - RED/GREEN tests for opening prompts and submitting each #812 action.
- Source guards for player-facing copy and no raw diagnostic leakage in #812 default browser paths live with the focused #812 browser parity tests.
- Existing browser command/menu/help/coverage tests as needed:
  - `BrowserCommandCoverageServiceTests`
  - `BrowserPlayerCommandMenuBuilderTests`
  - `ExplorerWebCommandServiceTests`
  - `BrowserApiContractTests`

### Spec Kit artifacts

- `specs/812-browser-incarnation-gates/spec.md`
- `specs/812-browser-incarnation-gates/plan.md`
- `specs/812-browser-incarnation-gates/tasks.md`

## Implementation Phases

### Phase 1 — Spec Kit setup and source inspection

1. Confirm `AGENTS.md`, `.specify/memory/constitution.md`, issue #812, and this Spec Kit feature are aligned.
2. Inspect console Gates lifecycle code completely before implementing browser parity.
3. Inspect #811 Shining action browser patterns and reuse structure instead of inventing new React/gameplay handlers.

### Phase 2 — RED tests and source guards

1. Add focused browser parity tests for core-action request forms:
   - open Gates writes `ActionTypeOpenGates`.
   - prepare incarnation package writes `ActionTypePrepareIncarnationPackage` with `sourceDraftVersion`, selected card ids, and selected card snapshots.
2. Add focused browser parity tests for local Gates mutations:
   - select blessing adds exactly one available card id.
   - deselect blessing removes exactly one selected card id.
   - reroll uses existing C# state helper and updates draft state without pending-control shape changes.
3. Add stale/direct guard tests:
   - outside Shining Abode blocks open and submit.
   - pending core action blocks core requests and local Gates mutations.
   - stale/closed draft, missing card, and no reroll states block local mutations.
4. Add command metadata/help/menu/coverage/source guards proving #812 is browser-supported and player-facing.
5. Run the focused test command and record expected RED counts in `tasks.md`.

### Phase 3 — Minimal implementation

1. Add command descriptors and aliases for the Gates lifecycle.
2. Add prompt builders that enumerate player-facing cards/package summaries from canonical C# state.
3. Add `BrowserAfterlifeWriteService` handlers that re-check state at submit time and write/mutate only through existing authority.
4. Update menu/help/coverage fixtures and command results.
5. Keep runtime contract shape unchanged; do not modify GM-facing afterlife contracts unless the implementation discovers a required contract change.

### Phase 4 — GREEN verification and reconciliation

1. Run the focused RED/GREEN filter and record exact counts.
2. Run broader Shining/browser/API/docs gates appropriate to the final diff.
3. Run C# builds, `git diff --check`, and added-line static scan excluding Spec Kit docs if necessary.
4. Update `tasks.md` with actual verification evidence and final task statuses.
5. Commit focused changes with `[skip ci]`; leave PR/merge/issue closure to Hermes.

## Acceptance Criteria Mapping

- Spec US1 / FR-003 -> open Gates command + prompt + write handler + pending request tests.
- Spec US2 / FR-004 -> select/deselect prompt + local mutation handlers + stale/missing-card tests.
- Spec US3 / FR-005 -> reroll prompt + local mutation handler + no-reroll/stale tests.
- Spec US4 / FR-006 -> prepare package prompt + pending request write + selected snapshot tests.
- Spec US5 / FR-008/FR-009 -> help/menu/coverage/API/source guard tests.
- Spec FR-010 / SC-005 -> no contract shape changes; docs tests only required if a contract change is introduced.

## Verification Plan

Baseline before implementation in fresh worktree:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningAbode|ShiningActions|ShiningPolitics|BrowserAfterlifeWriteServiceTests|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Baseline observed by Hermes on 2026-06-07: 0 failed / 368 passed / 0 skipped / 368 total.

Focused expected after adding RED tests:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserShiningIncarnationGatesParityTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserWebUiParity|BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Final local gates to run when relevant:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ShiningAbode|ShiningActions|ShiningPolitics|BrowserAfterlifeWriteServiceTests|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|AfterlifeShiningPlayerFacingSourceGuardTests|BrowserApiContractTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal
git diff --check origin/main...HEAD
```

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if React/TypeScript frontend files or generated browser fixtures/build artifacts are changed in a way that affects frontend verification.

## Risks and Mitigations

- **Risk**: Browser local Gates mutations accidentally bypass local write/GM-turn safety. **Mitigation**: use existing `ExecuteAsync`/local-write owner patterns and stale-submit tests.
- **Risk**: Player-facing blockers leak raw pending/control diagnostics. **Mitigation**: add source/result guards and sanitize shared validation messages for browser defaults.
- **Risk**: Preparing the incarnation package changes runtime contract shape. **Mitigation**: reuse `ShiningCoreActionRequestState` fields already used by console; stop and update contract docs if a new shape is required.
- **Risk**: Command coverage fixture drift. **Mitigation**: run `BrowserApiContractTests` and refresh tracked fixtures intentionally.
