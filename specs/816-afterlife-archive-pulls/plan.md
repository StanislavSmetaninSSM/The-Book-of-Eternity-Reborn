# Implementation Plan: Browser Afterlife Archive Actions and Direct Pull

**Source Issue**: [#816](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/816)
**Umbrella Issue**: [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Feature Spec**: [spec.md](spec.md)
**Feature Branch**: `codex/816-afterlife-archive-pulls`

## Summary

Implement Browser Client parity for #816 by exposing guided prompt forms for existing console archive consultation and archive project fuel actions, and by auditing/filling any direct Chaos Sea gacha pull parity gaps. Reuse existing C# afterlife services and pending/GM action contracts. React remains generic prompt/result presentation.

## Technical Context

- Runtime: .NET 8 C# client in `BookOfEternityClient/`.
- Tests: xUnit in `BookOfEternityClient.Tests/`.
- Browser shell: C# web command/prompt-session services plus tracked React fixtures in `BookOfEternityClient.WebFrontend/`.
- Console reference: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs` methods `StartArchiveConsultationAsync`, `StartArchiveProjectFuelAsync`, `ReadGuardiansForArchiveOperationAsync`, `ResolveArchiveFuelTarget`, and `ShowGachaInfo`.
- Existing archive authority: `AfterlifeArchiveConsultationService`, `AfterlifeArchiveProjectFuelService`, `AfterlifeArchiveActionState`, `AfterlifeArchiveState`.
- Existing direct-gacha authority: `BrowserAfterlifeWriteService.ApplyGachaPullAsync`, `BrowserAfterlifeTurnRequestQueue.QueueDirectChaosSeaGachaAsync`, `PendingTurnStateService`, `ExplorerLocalTurnRollbackArtifacts`, and pending-turn snapshot authority.
- Relevant reference: `book-of-eternity-reborn/references/browser-direct-gacha-prompt.md`.

## Constitution / Governance Checks

- GitHub issue traceability: all implementation changes are tied to #816 and umbrella #817.
- Spec Kit fit: #816 changes browser/console parity, player-facing prompt UX, existing afterlife pending/GM contracts, direct gacha queueing evidence, command coverage, and fixtures across multiple files, so Spec Kit is required.
- Player-facing integrity: default labels/blockers/results must be Russian/in-world and must not expose raw `.json`, local paths, DTO/API, endpoint, validation, debug, or exception wording.
- Contract/state authority: no new GM-authored pending/control contract is planned. If a contract shape change becomes required, stop, revise `spec.md`/`plan.md`/`tasks.md`, and update GM-facing docs/examples/tests before continuing.
- Test-first path: add focused RED tests/source guards before production implementation. If a direct-gacha sub-slice is already implemented, add/adjust evidence tests before changing coverage metadata.
- Verification evidence: focused C# tests, docs coverage when contract docs are affected, frontend verify when fixtures/React change, `git diff --check`, and static scan are required before merge.
- Agent orchestration: Hermes owns final PR/merge/issue closure; Codex implements and reports evidence.

## Project Structure / Files

### Expected production files

- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`
  - Add/update browser-discoverable command descriptors, aliases, mutation mode, and handler kind for archive consultation/project fuel if command-addressable.
  - Ensure direct `/gacha` evidence is not incorrectly hidden as an open #816 gap if already supported.
- `BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs`
  - Add/update player-facing help rows for archive/direct-pull actions when command-addressable.
- `BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.cs` and related command-result builders
  - Add action metadata from `/feathers`, archive, or afterlife surfaces so browser players can discover archive consultation/project fuel and direct pull.
- `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs`
  - Route archive consultation/project fuel mutating commands through prompt sessions and local write/GM-turn safety gates.
  - Build prompt forms for archive entry selection, Guardian/project selection, confirmation, and stale-state blockers.
- `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs`
  - Add write handlers for consultation/project fuel submissions, or focused helper methods that call the existing archive services and preserve no-write guarantees.
  - Re-check current realm, archive entry, pending request, Guardian/project eligibility, local write/GM-turn state, and confirmation at submit time.
  - Audit/fix direct gacha support only where #816 evidence shows gaps.
- `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs`
  - Expose player-facing action entries when archive/direct-pull actions are available; labels remain Russian/player-facing.
- `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs`
  - Remove #816 as an open browser parity gap only after archive consultation, project fuel, and direct pull are verified; keep #817 open.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/command-coverage.json`
  - Refresh if command coverage changes.
- `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`
  - Refresh if action metadata changes affect the fixture.

### Expected test files

- `BookOfEternityClient.Tests/WebUi/BrowserAfterlifeArchiveParityTests.cs` (create or extend a focused nearby file)
  - RED/GREEN tests for opening/submitting archive consultation and project fuel.
  - Tests for no eligible entry/Guardian/project, existing pending request, malformed pending request, stale prompt submit, wrong realm, cancellation/unconfirmed submit, and no-write paths.
  - Player-facing copy guards for blockers and results.
- Existing direct-gacha tests under WebUi/afterlife write coverage
  - Add/adjust #816 evidence only if direct gacha is already implemented; otherwise add RED tests before fixing gaps.
- Existing browser command/menu/help/coverage tests as needed:
  - `ExplorerWebPromptSessionService` / `ExplorerWebCommandServiceTests`
  - `BrowserPlayerCommandMenuBuilderTests`
  - `BrowserCommandCoverageServiceTests`
  - `BrowserApiContractTests`
  - relevant source-guard tests for default browser copy.
- Existing afterlife/docs tests:
  - `AfterlifeArchiveActionStateTests`, `AfterlifeArchiveConsultationServiceTests`, `AfterlifeArchiveProjectFuelServiceTests`, `AfterlifeDocumentationCoverageTests`, `ExampleDocumentationValidationTests` as needed.

### Spec Kit artifacts

- `specs/816-afterlife-archive-pulls/spec.md`
- `specs/816-afterlife-archive-pulls/plan.md`
- `specs/816-afterlife-archive-pulls/tasks.md`

## Implementation Phases

### Phase 1 — Spec Kit setup and source inspection

1. Confirm `AGENTS.md`, `.specify/memory/constitution.md`, issue #816/#817, and this feature directory are aligned.
2. Inspect the console archive/direct-pull source completely before implementing browser parity:
   - `StartArchiveConsultationAsync`
   - `StartArchiveProjectFuelAsync`
   - `ReadGuardiansForArchiveOperationAsync`
   - `ResolveArchiveFuelTarget`
   - `ShowGachaInfo`
3. Inspect the existing services:
   - `AfterlifeArchiveConsultationService.CreateRequestAsync` / `CommitPreparedRequestAsync`
   - `AfterlifeArchiveProjectFuelService.CreateRequestAsync` / `CommitPreparedRequestAsync`
   - `AfterlifeArchiveActionState` read/parse/write helpers
   - `BrowserAfterlifeWriteService.ApplyGachaPullAsync`
   - `BrowserAfterlifeTurnRequestQueue.QueueDirectChaosSeaGachaAsync`
4. Inspect #812/#813/#814/#815 browser prompt/write patterns and reuse their local-write safety model.

### Phase 2 — RED tests and source guards

1. Add a focused browser parity test for opening archive consultation with eligible archive entry and friendly Guardian choices.
2. Add a consultation submit test proving the existing pending request shape, reserved archive entry, GM action tag/evidence, and no raw technical copy.
3. Add a focused browser parity test for opening archive project fuel only when a friendly Guardian has an active project.
4. Add a project fuel submit test proving `targetProjectId`, existing pending request shape, reserved archive entry, and player-facing copy.
5. Add guard tests for cancellation/unconfirmed submit, no eligible Guardian, no active project, existing/malformed pending request, wrong realm, local write/GM-turn blockers, stale prompt entry/Guardian/project changes, and no-write failures.
6. Audit direct gacha tests/coverage. If any #816 direct-pull behavior is missing, add RED tests before production fixes. If already complete, add evidence tests/coverage assertions that prove it is supported and linked to #816.
7. Add/update command coverage, command menu/help, API fixture, and source guard tests proving #816 actions are browser-supported and player-facing.
8. Run the focused RED command and record expected failures in `tasks.md` before production implementation.

### Phase 3 — Minimal implementation

1. Add/adjust command descriptors, aliases, and player-facing metadata for archive consultation/project fuel/direct pull surfaces.
2. Add prompt-session local UI lock coverage for mutating archive actions.
3. Add prompt builders that enumerate eligible archive entries, Guardians, active projects, current blockers, and confirmation.
4. Reuse existing C# archive services for request creation/commit; extract narrowly scoped helpers only when needed for browser testability/result shaping.
5. Add browser write handlers that re-read current state, recompute eligibility, validate confirmation, block stale submissions, serialize local writes, and update only existing `soul_state.json` plus existing archive pending request files.
6. Audit/fix direct `/gacha` only for true gaps; do not duplicate a second direct-gacha path or invent unsupported banners.
7. Update menu/help/coverage fixtures and command results.
8. Keep runtime contract shapes unchanged; if impossible, update contract matrix/examples/manifest/docs tests before continuing.

### Phase 4 — GREEN verification and reconciliation

1. Run the focused RED/GREEN filter and record exact counts.
2. Run a broader browser/API/afterlife parity sweep covering archive services, prompt sessions, command/menu/coverage, `/gacha`, and console afterlife command tests.
3. Run documentation-sensitive tests if any contract/doc-impacting surface changed, otherwise record why not required.
4. Run C# builds, frontend verification when frontend fixtures/assets change, `git diff --check`, and added-line static scan excluding Spec Kit docs if necessary.
5. Update `tasks.md` with actual verification evidence and final task statuses.
6. Commit focused changes with `[skip ci]`; leave PR/merge/issue closure to Hermes.

## Verification Commands

Baseline/focused commands expected for this feature:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "AfterlifeArchive|DirectGacha|Gacha|ExplorerWebPromptSession|BrowserAfterlifeWriteService|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|AfterlifeContractRegistryTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal
npm run verify --prefix BookOfEternityClient.WebFrontend
git diff --check origin/main...HEAD
git diff --unified=0 origin/main...HEAD -- . ':(exclude)specs/816-afterlife-archive-pulls/*' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

## Baseline Evidence Before Codex Implementation

- Worktree: `E:/Games/worktrees/boe-816-archive-pulls` on branch `codex/816-afterlife-archive-pulls` from `origin/main` at `801fd0e`.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "AfterlifeArchive|DirectGacha|Gacha|ExplorerWebPromptSession|BrowserAfterlifeWriteService|BrowserPlayerCommandMenuBuilderTests|BrowserCommandCoverageServiceTests|BrowserApiContractTests|ExplorerModeCommandTests" --logger "console;verbosity=minimal"` → passed, 0 failed / 452 passed / 0 skipped / 452 total.
- `npm ci --prefix BookOfEternityClient.WebFrontend` → added 54 packages, audited 55 packages, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` → typecheck passed, player-facing JS checks passed, Vitest 2 files / 29 tests passed, production build succeeded.

## Risk Notes

- Browser archive actions must not reserve an archive entry or write a pending request unless the submit path revalidates current realm, pending request absence, entry availability, Guardian/project eligibility, and confirmation.
- Direct gacha may already be implemented; duplicating it would create conflicting browser behavior. Audit before changing.
- Existing archive consultation/project fuel are GM-facing contracts. Reuse their exact request shapes and action tags unless the Spec Kit artifacts and GM-facing docs are deliberately updated.
- Avoid raw local path/JSON copy in default browser results even though services use those paths internally.
