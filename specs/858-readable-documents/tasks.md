# Tasks: Readable Inventory Document Authority

**Source Issue**: [#858](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/858)
**Spec**: `specs/858-readable-documents/spec.md`
**Plan**: `specs/858-readable-documents/plan.md`

## Phase 1: Exploration and Baseline

- [X] Inspect existing `/книги` command rendering in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`.
- [X] Inspect existing inventory validation and cross-reference patterns under `BookOfEternityClient/Services/Validation/`.
- [X] Inspect existing tests for ExplorerMode books/inventory command output and validation fixtures.
- [X] Run focused baseline tests with `-p:IsTestProject=true` and record real total/pass/fail counts.

## Phase 2: Regression Tests (TDD RED)

- [X] Add a validation regression test for a document-like inventory item with no inline text, no sidecar text/journal entry, and no unreadable reason; verify it fails because current validation accepts the item.
- [X] Add a validation regression test for an equivalent sealed/unreadable document with player-facing reason; verify the missing implementation cannot distinguish it yet, then make it pass in Phase 3.
- [X] Add command-result regression coverage for `/книги` showing a sealed/unreadable document placeholder reason.
- [X] Add command-result or validation coverage for a sidecar `item_text_updates` entry resolving a document by stable id, with name fallback only if the current contract already supports name fallback.
- [X] Add coverage for a readable inline `textContent` document remaining valid and visible.

## Phase 3: Minimal Implementation (GREEN)

- [X] Implement document-like inventory item classification with narrow terms from the issue and existing readable-document synonyms (`book`, `letter`, `scroll`, `note`, `document`, `inscription`, `diary`, `journal`, `книга`, `письмо`, `свиток`, `записка`, `документ`, `надпись`, `дневник`, `журнал`) across existing type/group/name fields.
- [X] Implement text authority resolution from inline `textContent`, `game_state/inventory/item_text_updates.json`, and `game_state/npcs/item_journals.json` using stable id first and existing name fallback only where supported.
- [X] Implement explicit unreadable/sealed/locked/unknown reason handling and expose that reason to `/книги` in player-facing wording.
- [X] Add validation issue code(s) for document-like items missing detail authority and reason; ensure issue messages avoid raw debug framing in player-facing surfaces.
- [X] Keep existing structured books/text/journal output intact when it exists.

## Phase 4: Docs / Contract Reconciliation

- [X] Update the closest GM-facing prompt/rules/example/manifest to document that readable-looking Mortal inventory documents need text authority or explicit unreadable reason.
- [X] If no suitable GM-facing document exists, create a GitHub follow-up issue and cite it in the PR/issue notes rather than leaving the contract undocumented.
- [X] Update documentation/source-guard tests if docs/examples changed.

## Phase 5: Verification and Review Prep

- [X] Run focused new tests and confirm non-zero pass counts.
- [X] Run focused ExplorerMode books/inventory tests with `-p:IsTestProject=true`.
- [X] Run focused validation tests with `-p:IsTestProject=true`.
- [X] Run docs/contract tests if documentation/prompts/examples changed.
- [X] Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- [X] Run `git diff --check origin/main...HEAD`.
- [X] Run added-line static security scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting.
- [X] Reconcile this Spec Kit feature; mark tasks complete only after implementation and verification evidence exist.

## Phase 6: Review Reconciliation

- [X] Keep console `/книги` parity with the shared readable-document authority, including sealed/unreadable reason rows when no readable text exists.
- [X] Fail closed on sidecar stable-id mismatches: name fallback may satisfy authority only for sidecar entries that do not supply an identity.
- [X] Add regressions for wrong-id/same-name sidecars, non-document negative classification, and console sealed-document placeholder output.

## Verification Evidence

- Baseline before implementation:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 280/280.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"`: passed 926/926.
- RED:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ReadableDocument|FullyQualifiedName~Books" --logger "console;verbosity=minimal"`: failed 3, passed 5, total 8. Expected failures were missing `/книги` document rows and missing `readable_document_missing_detail_authority`.
- GREEN / verification:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ReadableDocument|FullyQualifiedName~Books" --logger "console;verbosity=minimal"`: passed 8/8.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|Documentation|PromptDocumentationCoverageTests" --logger "console;verbosity=minimal"`: passed 111/111.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 280/280.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"`: passed 931/931.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: passed, 0 warnings, 0 errors.
  - `git diff --check`: passed before task reconciliation.
  - Added-line static scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting: `NO_MATCHES`.
- Review reconciliation rerun after independent review findings:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ReadableDocument|FullyQualifiedName~Books" --logger "console;verbosity=minimal"`: passed 11/11.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests" --logger "console;verbosity=minimal"`: passed 281/281.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests" --logger "console;verbosity=minimal"`: passed 120/120.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~Validation" --logger "console;verbosity=minimal"`: passed 933/933.
  - Docs/contract tests: passed 111/111.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: passed, 0 warnings, 0 errors.
  - Spec Kit prerequisites found `specs/858-readable-documents`.
  - Added-line static scan: `NO_MATCHES`.
