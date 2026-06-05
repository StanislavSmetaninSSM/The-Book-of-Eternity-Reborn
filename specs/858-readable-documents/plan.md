# Implementation Plan: Readable Inventory Document Authority

**Source Issue**: [#858](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/858)
**Spec**: `specs/858-readable-documents/spec.md`
**Branch / Worktree**: `fix/858-readable-documents` at `E:/Games/worktrees/boe-858-readable-documents`
**Constitution**: `.specify/memory/constitution.md` v1.1.0

## Technical Context

- Runtime: .NET 8 C# client, tests under `BookOfEternityClient.Tests`.
- Relevant command code: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs` currently maps `/books` / `/книги` / `/читать` to a bundle built from `game_state/inventory/item_text_updates.json` and `game_state/npcs/item_journals.json`.
- Relevant validation code: `BookOfEternityClient/Services/Validation/` partial `ValidationService` files, especially inventory/item validation and cross-reference patterns.
- Relevant data mapping: `BookOfEternityClient/Configuration/FileMapping.cs` maps `updateItemTextContents` to `game_state/inventory/item_text_updates.json` and `itemJournalUpdates` to `game_state/npcs/item_journals.json`.
- Host note: use `-p:IsTestProject=true` for `dotnet test` so SDK 10 discovers real tests.

## Architecture

Add a small reusable inventory-readable-document helper or validation path that can classify text-bearing inventory items, resolve text authority from inline item text, item text updates, and item journals, and expose an explicit unreadable reason. Keep the authority local to existing file-backed game state; do not invent new remote services or browser-only gameplay logic.

The command path should keep existing `/книги` behavior for text updates and item journals, then add readable/unreadable inventory document rows when inventory contains document-like items. Validation should enforce the same invariant so a GM-authored document cannot be player-visible without text authority or a reason.

## Spec Kit Applicability

Spec Kit is required because #858 changes validation, player-facing command UX, and Mortal World GM-authored state contract. This feature directory links #858 in `spec.md`, `plan.md`, and `tasks.md`; keep tasks unchecked until implementation and verification evidence exist.

## Testing Strategy

Use TDD:

1. Add focused failing tests for validation and `/книги` command behavior before production code.
2. Verify RED failures against current `origin/main` behavior.
3. Implement the smallest shared resolution/classification behavior that makes tests pass.
4. Verify focused tests and neighboring suites.
5. Run docs/contract tests if GM-facing docs/examples are changed.

## Verification Commands

Run these before PR/merge:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ReadableDocument|FullyQualifiedName~Books|FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

If documentation/prompts/examples are changed, also run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|Documentation" --logger "console;verbosity=minimal"
```

Run an added-line static scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting, excluding docs/superpowers plan recipes if any are created.

## Docs / Prompt Impact

Expected: yes. Because validation will require readable detail authority or explicit unreadable reason for Mortal World document-like inventory items, update the closest GM-facing rules/prompts/examples or create a follow-up issue if no suitable document exists. Do not change afterlife contract docs unless implementation unexpectedly touches afterlife surfaces.

## Risks

- The existing data shape for `item_text_updates.json` and `item_journals.json` may vary; inspect current code/tests before choosing exact matching fields.
- Overbroad document classification could flag narrative/flavor items that are not meant to be readable; allow explicit narrative-only or unreadable reason where appropriate.
- Default `/книги` copy must stay player-facing and avoid raw path/API/debug terms.
- Browser command parity likely uses shared command result; verify if a browser-specific test exists before adding separate frontend logic.
