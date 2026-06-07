# Implementation Plan: Browser Ink Feather Fate Reveal and Rewrite

**Source Issue**: [#815](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/815)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Feature Spec**: [spec.md](spec.md)
**Feature Branch**: `codex/815-ink-feathers-fate`

## Summary

Implement Browser Client parity for the console Ink Feather fate flows: reveal the pending dice/gacha base and rewrite that locked fate state after explicit confirmation and Ink Feather spending. Reuse C# authority for `soul_state.json` and `PendingTurnStateService`; React remains generic prompt/result presentation and must not own gameplay rules.

## Technical Context

- Runtime: .NET 8 C# client in `BookOfEternityClient/`.
- Tests: xUnit in `BookOfEternityClient.Tests/`.
- Browser shell: C# web command/prompt-session services plus tracked React fixtures in `BookOfEternityClient.WebFrontend/`.
- Existing console reference: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs` methods `HandleRevealFate` and `HandleRewriteFate`.
- Existing pending fate authority: `BookOfEternityClient/Services/PendingTurnStateService.cs`, path `game_state/control/pending_dice_state.json`.
- Existing Ink Feather balance authority: `game_state/meta/soul_state.json`, helper logic in `ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs` `DeductInkFeathers`.
- Existing browser local-turn/prompt patterns: #806 inventory management, #812 Shining gates, #813 relic forge, #814 storage/transport in `ExplorerCommandCatalog.cs`, `ExplorerWebPromptSessionService.cs`, `BrowserAfterlifeWriteService.cs`, `BrowserPlayerCommandMenuBuilder.cs`, `BrowserCommandCoverageService.cs`, command result builders, and focused browser parity tests.

## Constitution / Governance Checks

- GitHub issue traceability: all implementation changes are tied to #815 and umbrella #817.
- Spec Kit fit: #815 changes browser/console parity, player-facing prompt UX, local state-write behavior, and command coverage across multiple code/test surfaces, so Spec Kit is required.
- Player-facing integrity: default labels/blockers/results must be Russian/in-world and must not expose raw `.json`, local paths, DTO/API, endpoint, validation, debug, or exception wording.
- Contract/state authority: no new GM-authored pending/control contract is planned. `pending_dice_state.json` is an existing client-owned pending turn state file. If a contract/state shape change becomes required, stop, revise `spec.md`/`plan.md`/`tasks.md`, and update GM-facing docs/examples/tests before continuing.
- Test-first path: add focused RED tests/source guards before production implementation.
- Verification evidence: focused C# tests, docs coverage if contract docs change, frontend verify when fixtures/React change, `git diff --check`, and static scan are required before merge.
- Agent orchestration: Hermes owns final PR/merge/issue closure; Codex implements and reports evidence.

## Project Structure / Files

### Expected production files

- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`
  - Add or update browser-discoverable command descriptors/aliases for reveal/rewrite fate guided forms if separate command tokens are used.
- `BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs`
  - Add/update player-facing help rows for Ink Feather fate actions if command-addressable.
- `BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.cs` and/or related Explorer command-result builders
  - Add `/feathers` action metadata or prompt-open result metadata that lets browser players discover reveal/rewrite fate from the existing Ink Feather surface.
- `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs`
  - Route #815 mutating command(s)/actions through browser prompt sessions and local write/GM-turn safety gates.
  - Build prompt forms for reveal/rewrite cost summary, confirmation, and current state blockers.
- `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs` and/or a focused C# local write service
  - Add write handlers for reveal/rewrite fate submissions.
  - Re-read `soul_state.json` and `pending_dice_state.json` at submit time, recompute current cost, validate confirmation and lock state, serialize writes, and return player-facing results.
- `BookOfEternityClient/Services/PendingTurnStateService.cs`
  - Reuse as the authority for pending dice/gacha state; add narrowly scoped helper methods only if needed for browser testability/result shaping.
- `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`
  - Expose player-facing action entries when Ink Feather fate actions are available; labels remain Russian/player-facing.
- `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs`
  - Remove #815 as an open browser parity gap once reveal/rewrite fate are covered; keep #816/#817 open.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/command-coverage.json`
  - Refresh if command coverage changes.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`
  - Refresh if action metadata changes affect the fixture.

### Expected test files

- `BookOfEternityClient.Tests/WebUi/BrowserInkFeatherFateParityTests.cs` (create)
  - RED/GREEN tests for opening reveal/rewrite prompts, confirming spends, and inspecting resulting state/result blocks.
  - Tests for insufficient balance, missing/malformed `soul_state.json`, unlocked/missing/malformed pending fate state, stale prompt submissions, and no-write failures.
  - Player-facing copy guards for blockers and results.
- Existing browser command/menu/help/coverage tests as needed:
  - `ExplorerWebPromptSessionService` / `ExplorerWebCommandServiceTests`
  - `BrowserPlayerCommandMenuBuilderTests`
  - `BrowserCommandCoverageServiceTests`
  - `BrowserApiContractTests`
  - relevant source-guard tests for default browser copy.
- Existing console/afterlife tests:
  - `ExplorerModeCommandTests.Afterlife.cs` remains the console behavior reference and should stay green.
  - `AfterlifeDocumentationCoverageTests` / `ExampleDocumentationValidationTests` only need changes if a runtime contract/GM-facing surface changes.

### Spec Kit artifacts

- `specs/815-browser-ink-feathers-fate/spec.md`
- `specs/815-browser-ink-feathers-fate/plan.md`
- `specs/815-browser-ink-feathers-fate/tasks.md`

## Implementation Phases

### Phase 1 — Spec Kit setup and source inspection

1. Confirm `AGENTS.md`, `.specify/memory/constitution.md`, issue #815/#817, and this feature directory are aligned.
2. Inspect the console reveal/rewrite flow completely before implementing browser parity:
   - `HandleRevealFate`
   - `HandleRewriteFate`
   - `DeductInkFeathers`
   - `FormatDiceDisplay`
   - `GetRarityColor` / `DescribeRarityLabel`
   - `PendingTurnStateService.GetOrCreateAsync`, `RevealAsync`, and `RewriteAsync`
3. Inspect #812/#813/#814 browser prompt/write patterns and reuse their local-write safety model instead of inventing a React gameplay handler.

### Phase 2 — RED tests and source guards

1. Add a focused browser parity test for opening reveal fate with cost/remaining/confirmation prompt fields.
2. Add a reveal submit test proving one spend, locked pending state, dice/gacha result, and no raw technical copy.
3. Add a focused browser parity test for opening rewrite fate only when fate is already locked.
4. Add a rewrite submit test proving one spend, replacement locked state, and old/new dice/gacha result.
5. Add guard tests for insufficient balance at open and submit, missing/malformed soul state, missing/malformed/unlocked pending fate state, cancelled/unconfirmed submit, realm/local-write blockers, and stale prompt state.
6. Add/update command coverage, command menu/help, API fixture, and source guard tests proving #815 actions are browser-supported and player-facing.
7. Run the focused RED command and record expected failures in `tasks.md` before production implementation.

### Phase 3 — Minimal implementation

1. Add/adjust command descriptors, aliases, and player-facing metadata for reveal/rewrite fate flows.
2. Add prompt-session local UI lock coverage for #815 mutating Ink Feather fate command(s).
3. Add prompt builders that enumerate reveal/rewrite availability and show current cost/remaining from C# state.
4. Extract or reuse focused C# helpers for reading/deducting Ink Feathers and formatting dice/gacha summaries; do not copy formulas into React.
5. Add browser write handlers that re-read current state, recompute cost, validate confirmation, block stale submissions, serialize local writes, and update only existing `soul_state.json` / `pending_dice_state.json`.
6. Update menu/help/coverage fixtures and command results.
7. Keep runtime contract shape unchanged; do not modify GM-facing docs unless implementation discovers a required contract change.

### Phase 4 — GREEN verification and reconciliation

1. Run the focused RED/GREEN filter and record exact counts.
2. Run a broader browser/API/afterlife parity sweep covering prompt sessions, command/menu/coverage, `/feathers`, and console afterlife command tests.
3. Run docs-sensitive tests if any contract/doc-impacting surface changed, otherwise record why not required.
4. Run C# builds, frontend verification when frontend fixtures/assets change, `git diff --check`, and added-line static scan excluding Spec Kit docs if necessary.
5. Update `tasks.md` with actual verification evidence and final task statuses.
6. Commit focused changes with `[skip ci]`; leave PR/merge/issue closure to Hermes.

## Verification Commands

Baseline/focused commands expected for this feature:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInkFeatherFateParityTests|ExplorerWebPromptSession|ExplorerWebCommandServiceTests|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal
npm run verify --prefix BookOfEternityClient.WebFrontend
git diff --check origin/main...HEAD
git diff --unified=0 origin/main...HEAD -- . ':(exclude)specs/815-browser-ink-feathers-fate/*' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

## Risk Notes

- Rewrite must not spend if fate is not locked at submit time; this is the highest-risk stale prompt path.
- Cost must be recomputed from current balance on submit, not blindly trusted from prompt-open state.
- Missing/malformed `pending_dice_state.json` should be safe: reveal may recreate through existing service; rewrite must block unless current state is valid and locked.
- If command names are added, keep aliases/player copy discoverable but avoid exposing implementation-only command tokens in default UI where action cards can hide them.
