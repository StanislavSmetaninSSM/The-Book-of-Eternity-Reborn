# Tasks: /books document selection and reading flow

**Source issue**: #947 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/947

**Spec**: `specs/947-books-reading-flow/spec.md`

**Plan**: `specs/947-books-reading-flow/plan.md`

## TDD / Implementation Tasks

- [X] T001 Record focused baseline for `/books`, readable-document authority, Explorer command rendering, browser command result, and validation slices before implementation.
- [X] T002 Capture RED for shelf-first behavior: multiple long embedded/sidecar/journal documents currently render as one giant combined first view instead of concise document rows.
- [X] T003 Capture RED for selected-document detail: selecting or requesting one document currently cannot show only that document's content/reason with other full bodies absent.
- [X] T004 Capture RED for browser parity: `/books` browser command-result output lacks list/detail data/action metadata or still forces full content into one table cell.
- [X] T005 Implement a shared read-only document shelf/detail projection over `ReadableInventoryDocumentAuthority` with deterministic selection identity, title, source/context hint, access status, preview/count, content entries, and unreadable reason.
- [X] T006 Wire console `/книги` / `/books` flow to show the shelf first, open one selected document, handle long entries cleanly, and support back navigation.
- [X] T007 Wire browser `/books` / `/книги` command-result output to expose equivalent shelf/detail authority through C# blocks/action metadata, or create/link a dedicated browser follow-up if full interactivity exceeds #947.
- [X] T008 Preserve stable-id-before-name matching, standalone sidecar records, unreadable/sealed document visibility, and existing readable-document validation semantics.
- [X] T009 Update GM-facing prompts/docs/examples/source guards if supported authoring fields or documented command capability changes; otherwise record docs/prompts impact as presentation-only over existing authority.
- [X] T010 Run focused RED/GREEN tests and record exact pass/fail counts.
- [X] T011 Run affected C# test slices and build gates.
- [X] T012 Run frontend verification if React/frontend files changed.
- [X] T013 Run Spec Kit prerequisite check, `git diff --check`, and added-line static scan.
- [X] T014 Independent review: verify #947 acceptance, console/browser parity, no raw/debug default output, readable authority intact, and no scope creep into #948/#949.
- [ ] T015 Hermes-owned lifecycle: PR, squash merge, issue evidence comment, issue close, worktree cleanup.

## Notes

- Spec Kit applies because #947 changes a player-facing `/books` UX flow, crosses console/browser read-only parity, and depends on readable-document authority for GM-authored Mortal World state.
- Browser parity may be satisfied by typed C# command-result/action metadata; React must remain presentation-only.
- Do not mark T015 complete from Codex. Hermes owns PR/merge/issue closure.

## Implementation Evidence

- Baseline (2026-06-11, before implementation):
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Books|FullyQualifiedName~ReadableInventoryDocument|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"` passed 425/425, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"` passed 1115/1115, 0 failed, 0 skipped.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-947-books-flow\\specs\\947-books-reading-flow` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
- RED (2026-06-11, #947 focused tests before implementation):
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~TryProcessCommand_Books_FirstViewShowsShelfWithoutDumpingLongBodies|FullyQualifiedName~TryProcessCommand_Books_SelectedDocumentShowsOnlyThatDocumentAndReturnsToShelf|FullyQualifiedName~ExecuteAsync_Books_ShowsReadableInventoryDocumentsAndSealedReasons|FullyQualifiedName~ExecuteAsync_Books_WithStableDocumentId_ShowsOnlySelectedDocumentDetail" --logger "console;verbosity=minimal"` failed 4/4, 0 passed, 0 skipped.
  - Expected failures proved the old console first view lacked shelf/status rows and dumped full bodies, selected console flow still included other document bodies, browser `/books` still returned the old `Книги и тексты` full-body table, and `/books doc_sidecar_1` was reduced to `/books`.
- Implementation (2026-06-11):
  - Added shared C# projection `BookOfEternityClient/UI/ReadableInventoryDocumentShelfProjection.cs` over `ReadableInventoryDocumentAuthority`; stable ids remain preferred by the authority, standalone sidecar rows are preserved, unreadable rows remain visible with reasons, and no React/browser authority logic was added.
  - Console `/книги` / `/books` now renders a `Книжная полка` shelf first, opens one selected document, supports `← Назад`, and supports direct `/books <selector>` detail.
  - Browser `/books` / `/книги` now returns a four-column shelf (`Документ`, `Источник`, `Доступ`, `Кратко`) plus read-only `UiAction` commands such as `/books doc_sidecar_1`; `/books <selector>` returns only the selected document detail and a back action.
  - Docs/prompts impact: presentation-only over existing GM-authored readable document fields (`textContent`, `item_text_updates.json`, `item_journals.json`, unreadable reason fields). No supported GM authoring fields, prompts, examples, manifests, validation rules, afterlife contracts, or pending/control surfaces changed.
- GREEN / verification (2026-06-11):
  - Focused #947 filter above passed 4/4, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Books|FullyQualifiedName~ReadableInventoryDocument|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"` passed 428/428, 0 failed, 0 skipped.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"` passed 1115/1115, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings, 0 errors.
  - `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` succeeded with 0 warnings, 0 errors.
  - `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\\Games\\worktrees\\boe-947-books-flow\\specs\\947-books-reading-flow` and `AVAILABLE_DOCS=["contracts/","tasks.md"]`.
  - `git diff --check origin/main...HEAD` passed with no output after implementation and evidence commits.
  - Added-line static scan over the committed non-spec diff found no matches for secret/shell/eval/path traversal/security patterns.
  - `npm run verify --prefix BookOfEternityClient.WebFrontend` was not run because no React/frontend files changed.
- Review fix / final review (2026-06-11):
  - Independent review run `E:/Games/codex-runs/20260611-155359-boe-947-books-reading-flow-finalreview` returned `CHANGES_REQUIRED`: numeric stable selectors such as `/books 2` could be treated as one-based shelf indexes before exact stable-id matching.
  - RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_Books_WithNumericStableDocumentId_PrefersSelectorOverShelfIndex" --logger "console;verbosity=minimal"` failed 1/1 before the fix because `/books 2` opened the second shelf row instead of the document whose stable id is `2`.
  - Fix commit `75827e8`: `ReadableInventoryDocumentShelfProjection.FindBySelector` now matches exact selector/aliases before numeric index fallback, preserving generated stable-id actions while keeping index fallback for non-conflicting manual selection.
  - GREEN after fix: focused #947 tests passed 5/5, affected slice passed 429/429, validation slice passed 1115/1115, both C# builds succeeded with 0 warnings/0 errors, `git diff --check origin/main...HEAD` passed, and added-line static scan returned `NO_MATCHES`.
  - Independent re-review run `E:/Games/codex-runs/20260611-160533-boe-947-books-reading-flow-rereview` returned `APPROVED`; blocking findings none. Detached review worktree Spec Kit helper failure was classified as review-harness limitation because Hermes supplied fresh feature-branch evidence.
