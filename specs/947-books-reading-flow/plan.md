# Implementation Plan: /books document selection and reading flow

**Source issue**: #947 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/947

**Branch**: `work/947-books-reading-flow`

**Spec**: `specs/947-books-reading-flow/spec.md`

## Summary

Turn `/книги` / `/books` from an all-content dump into a read-only document shelf plus selected-document reading view. Preserve `ReadableInventoryDocumentAuthority` as the shared C# authority, keep console/browser parity explicit, and ensure unreadable/sealed documents remain player-visible with in-world reasons.

## Technical Context

- Main code: C#/.NET 8 in `BookOfEternityClient/`.
- Tests: xUnit in `BookOfEternityClient.Tests/`.
- Console `/книги` path currently lives in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs` (`ShowItemTexts`) and renders all text blocks into one panel.
- Browser/read-only command pipeline currently builds `/books` in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` (`BuildBooks`) and places full document bodies in the table `Запись` cell.
- Shared authority exists in `BookOfEternityClient/Services/ReadableInventoryDocumentAuthority.cs` and is used by validation in `BookOfEternityClient/Services/Validation/ValidationService.ReadableInventoryDocuments.cs`.
- Existing tests cover current readable-document rendering in `ExplorerModeCommandTests.GeneralPanels.cs` and `ExplorerWebCommandServiceTests.cs`; update or extend them test-first rather than replacing coverage.

## Architecture Decision

Create or extract a shared C# read-only document shelf/detail projection over `ReadableInventoryDocumentAuthority`. The projection should produce deterministic selection identities, shelf summaries with previews/counts/status, and focused detail entries for one document. Console can render an interactive shelf/detail loop using existing ExplorerMode selection/back patterns. Browser should receive typed list/detail blocks or read-only action metadata from C# so React remains presentation-only; avoid adding React-side document authority rules.

## Files likely to change

- `BookOfEternityClient/Services/ReadableInventoryDocumentAuthority.cs` — only if the existing authority needs small reusable helpers for shelf/detail identity or entry metadata; preserve validation semantics.
- `BookOfEternityClient/UI/ReadableInventoryDocumentShelfProjection.cs` or similar new helper — shared read-only shelf/detail projection for console and browser.
- `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs` — console `/книги` shelf, selected-document reading view, long text layout, and back navigation.
- `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` — browser `/books` shelf/detail command-result blocks or action metadata; remove all-content table-cell dumping.
- `BookOfEternityClient/CommandProtocol/*` — only if selected-document detail requires a typed read-only command/action descriptor.
- `BookOfEternityClient.Tests/ExplorerModeCommandTests.GeneralPanels.cs` — console RED/GREEN tests for shelf-first and selected reading views.
- `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs` — browser RED/GREEN tests for list/detail output and no raw/debug leakage.
- `BookOfEternityClient.Tests/*Validation*` or existing validation tests — only if validation coverage needs an explicit guard that readable authority remains intact.
- `Rules/`, `TaskGuides/`, `OtherGuides/`, `Examples/`, manifests, or documentation source guards — update if the GM-authored document contract or documented command capability changes.
- `specs/947-books-reading-flow/*` — keep tasks/evidence synchronized.

## Implementation Slices

1. **RED tests for shelf-first behavior**: Seed multiple long documents covering embedded `textContent`, `item_text_updates`, `item_journals`, and unreadable reasons. Prove current console/browser output dumps full content into the first view and lacks a selected-document model.
2. **Shared shelf/detail projection**: Implement minimal shared read-only projection with deterministic selector, title, source/context hint, access status, preview/count, content entries, and unreadable reason.
3. **Console flow**: Wire `/книги` to show the shelf first, support selecting one document, render only that document's content/reason, and provide back navigation consistent with existing ExplorerMode menus.
4. **Browser parity**: Wire `/books` command-result output to expose shelf summaries and focused detail data/action metadata via C# authority. If full interactive browser selection is too large, create/link a dedicated follow-up and still stop dumping all content into one cell.
5. **Validation/docs reconciliation**: Preserve readable-document validation and update GM-facing docs/examples only if supported authoring fields/command contract changed. Otherwise record why presentation-only client flow needs no GM prompt update.
6. **Review/verification**: Run focused gates, build(s), Spec Kit check, static scan, independent review, PR, squash merge, issue evidence comment, issue closure, and cleanup.

## Verification Commands

Baseline before implementation should be recorded in `tasks.md` after the first run. Planned gates:

```bash
# focused /books + readable authority + Explorer/browser command slice
 dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Books|FullyQualifiedName~ReadableInventoryDocument|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"

# validation authority slice
 dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"

# after implementation, run the focused #947 tests by their actual names and record the exact filter in tasks.md
# Example shape once tests exist: dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Books" --logger "console;verbosity=minimal"

# builds
 dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
 dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true

# Spec Kit and hygiene
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
 git diff --check origin/main...HEAD
```

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if React/frontend files change.

## Risks and Non-goals

- Do not weaken readable-document validation or hide malformed readable-looking items.
- Do not add document write/edit/read-receipt mutations.
- Do not duplicate document authority in React or browser TypeScript.
- Do not broaden into #948 mortal-wide detail drill-down audit or #949 afterlife detail surfaces.
- Do not change afterlife contracts or pending/control surfaces.
- Long text layout should be improved enough for readability, but rich pagination/book typography can be a follow-up if the current branch supplies a safe list/detail model.
