# Tasks: Browser Inventory and Document Detail Paths

**Input**: `specs/1089-browser-inventory-document-details/spec.md`, `specs/1089-browser-inventory-document-details/plan.md`, issue [#1089](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1089)

## Phase 1: Setup and Baseline

- [x] T001 Create branch/worktree `work/1089-browser-inventory-details` at `E:/Games/worktrees/boe-1089-browser-inventory-details` from `origin/main`.
- [x] T002 Create Spec Kit artifacts for #1089 without changing repo-global `AGENTS.md` or `.specify/feature.json`; use `SPECIFY_FEATURE_DIRECTORY=specs/1089-browser-inventory-document-details` for helper commands.
- [x] T003 Run focused baseline before production changes and record exact counts.
  - Command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Inventory|FullyQualifiedName~Books|FullyQualifiedName~ReadableInventoryDocument|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"`
  - Evidence: baseline on 2026-06-19 passed 481, failed 0, skipped 0.

## Phase 2: RED Coverage

- [x] T004 Add RED C# browser inventory summary/detail tests.
  - Test should prove `/инв` exposes a player-facing detail action for a seeded item.
  - Test should prove selected item detail renders description, ordinary effects, structured bonus target/value/value-type, combat effect, and properties with Russian labels.
  - Test should fail on current behavior before production changes.
- [x] T005 Add RED C# browser books/documents list/detail tests.
  - Test should prove `/книги` or `/books` exposes a shelf/list with stable selectors and read actions.
  - Test should prove selected document detail renders only the selected document/book content.
  - Include numeric-stable-id-before-row-index coverage when practical.
- [x] T006 Add raw/default player-facing guard coverage.
  - Assert default output for item/document details excludes raw JSON, `game_state/`, `.json`, API/DTO/protocol/debug wording, and Spec Kit/acceptance framing.
  - If frontend rendering changes are needed, add/adjust frontend guards for safe block preservation and existing `executeCommand`/prompt-session routing.

## Phase 3: Implementation

- [x] T007 Implement read-only item detail command-result projection in C#.
  - Reuse canonical inventory/item data and existing command-result block/action patterns.
  - Do not implement drop/split/merge/storage/transport mutation behavior in this slice.
- [x] T008 Implement readable document/book shelf and selected-document detail projection in C#.
  - Reuse `ReadableInventoryDocumentAuthority` and existing sidecar/embedded text sources.
  - Preserve unreadable/sealed/unavailable reasons as Russian player-facing copy.
- [x] T009 Wire browser summary actions to selected-detail commands through existing command-result metadata.
  - Add command catalog/action descriptors only when needed.
  - Keep React presentation-only; no React-side inventory/book gameplay rules.
- [x] T010 Adjust frontend rendering/source guards only if existing `CommandResultView` / `BlockRenderer` cannot render the new safe blocks/actions correctly.
  - Evidence: no frontend rendering change was needed; existing command-result blocks/actions render the updated C# projections.

## Phase 4: Verification

- [x] T011 Run focused GREEN tests for new inventory/books/detail coverage and record exact counts.
- [x] T012 Run broader affected C# filter and record exact counts.
  - Command: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Inventory|FullyQualifiedName~Books|FullyQualifiedName~ReadableInventoryDocument|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"`
- [x] T013 Run frontend verification when frontend files or fixtures changed.
  - Command: `npm run verify --prefix BookOfEternityClient.WebFrontend`
- [x] T014 Run build, whitespace, and static scans.
  - Commands: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity minimal`, `git diff --check origin/main...HEAD`, added-line security/raw-output scan excluding Spec Kit docs.
- [x] T015 Capture dependency-light browser visual smoke artifact if the rendered UI changes materially.
  - Evidence: not captured; no React/CSS/browser rendering code changed, only C# command-result text/projection data changed.

## Phase 5: Review, PR, Merge, Closure

- [ ] T016 Run independent review against issue #1089 acceptance criteria, Spec Kit artifacts, and `git diff origin/main...HEAD`.
- [ ] T017 Fix any Critical/Important review findings and rerun focused verification.
- [ ] T018 Create PR with local-gated verification evidence and safe closing reference `Closes #1089`.
- [ ] T019 Squash-merge after local verification + independent review, delete remote branch, verify issue #1089 is closed/completed.
- [ ] T020 Post issue evidence comment, update labels from `codex-agent in-progress` / `status: in-progress` to `status: verified` if labels exist, and clean up worktrees.

## Evidence Log

- 2026-06-19: Hermes selected #1089 after #1112/#1086 report-focused closures. Open issues #1089-#1091 had `codex-agent in-progress` labels but no matching run directories, branches, PRs, comments, or worktrees; #1089 is the lowest-numbered Browser detail follow-up and matches the next read-only inventory/books gap.
- 2026-06-19: Spec Kit artifacts created in `specs/1089-browser-inventory-document-details/`. Repo-global `AGENTS.md` and `.specify/feature.json` intentionally left unchanged to avoid landing stale active-feature pointers; use `SPECIFY_FEATURE_DIRECTORY` for helper commands.
- 2026-06-19: RED focused command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_InventoryItemDetail_TranslatesRawMechanicalTypeValues|FullyQualifiedName~ExecuteAsync_Books_ShowsReadableInventoryDocumentsAndSealedReasons|FullyQualifiedName~ExecuteAsync_Books_WithStableDocumentId_ShowsOnlySelectedDocumentDetail" --logger "console;verbosity=minimal"` failed as expected: 0 passed, 3 failed, 0 skipped. Failures showed visible raw inventory type/quality text (`Оружие` missing) and visible document selectors (`doc_inline_1`, `doc_sidecar_1`) in book shelf/detail text.
- 2026-06-19: GREEN focused command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_InventoryItemDetail_TranslatesRawMechanicalTypeValues|FullyQualifiedName~ExecuteAsync_Books_ShowsReadableInventoryDocumentsAndSealedReasons|FullyQualifiedName~ExecuteAsync_Books_WithStableDocumentId_ShowsOnlySelectedDocumentDetail" --logger "console;verbosity=minimal"` passed: 3 passed, 0 failed, 0 skipped.
- 2026-06-19: Focused regression command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_InventoryItemDetail_RendersReadableItemAndSidecarDetails|FullyQualifiedName~ExecuteAsync_InventoryItemDetail_TranslatesRawMechanicalTypeValues|FullyQualifiedName~ExecuteAsync_Books_ShowsReadableInventoryDocumentsAndSealedReasons|FullyQualifiedName~ExecuteAsync_Books_WithStableDocumentId_ShowsOnlySelectedDocumentDetail|FullyQualifiedName~ExecuteAsync_Books_WithNumericStableDocumentId_PrefersSelectorOverShelfIndex" --logger "console;verbosity=minimal"` passed: 5 passed, 0 failed, 0 skipped.
- 2026-06-19: Nearby console inventory command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests&FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"` passed: 11 passed, 0 failed, 0 skipped.
- 2026-06-19: Broader affected C# command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Inventory|FullyQualifiedName~Books|FullyQualifiedName~ReadableInventoryDocument|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"` passed: 482 passed, 0 failed, 0 skipped.
- 2026-06-19: Build command `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity minimal` passed: 0 warnings, 0 errors.
- 2026-06-19: Frontend verification not run because no `BookOfEternityClient.WebFrontend` files or frontend fixtures changed.
- 2026-06-19: Working-tree `git diff --check` exited 0; Git reported only CRLF normalization warnings for touched text files.
- 2026-06-19: Added-line security/raw-output scan over changed C# production/test lines excluding Spec Kit docs found no true findings. Benign matches were test fixture paths/ids (`game_state/inventory/items.json`, `raw_weapon_1`) and new calls to the existing `FormatInventoryProtocolValue` formatter.
