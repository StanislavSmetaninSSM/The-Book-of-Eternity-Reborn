# Tasks: /books document selection and reading flow

**Source issue**: #947 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/947

**Spec**: `specs/947-books-reading-flow/spec.md`

**Plan**: `specs/947-books-reading-flow/plan.md`

## TDD / Implementation Tasks

- [X] T001 Record focused baseline for `/books`, readable-document authority, Explorer command rendering, browser command result, and validation slices before implementation.
- [ ] T002 Capture RED for shelf-first behavior: multiple long embedded/sidecar/journal documents currently render as one giant combined first view instead of concise document rows.
- [ ] T003 Capture RED for selected-document detail: selecting or requesting one document currently cannot show only that document's content/reason with other full bodies absent.
- [ ] T004 Capture RED for browser parity: `/books` browser command-result output lacks list/detail data/action metadata or still forces full content into one table cell.
- [ ] T005 Implement a shared read-only document shelf/detail projection over `ReadableInventoryDocumentAuthority` with deterministic selection identity, title, source/context hint, access status, preview/count, content entries, and unreadable reason.
- [ ] T006 Wire console `/книги` / `/books` flow to show the shelf first, open one selected document, handle long entries cleanly, and support back navigation.
- [ ] T007 Wire browser `/books` / `/книги` command-result output to expose equivalent shelf/detail authority through C# blocks/action metadata, or create/link a dedicated browser follow-up if full interactivity exceeds #947.
- [ ] T008 Preserve stable-id-before-name matching, standalone sidecar records, unreadable/sealed document visibility, and existing readable-document validation semantics.
- [ ] T009 Update GM-facing prompts/docs/examples/source guards if supported authoring fields or documented command capability changes; otherwise record docs/prompts impact as presentation-only over existing authority.
- [ ] T010 Run focused RED/GREEN tests and record exact pass/fail counts.
- [ ] T011 Run affected C# test slices and build gates.
- [ ] T012 Run frontend verification if React/frontend files changed.
- [ ] T013 Run Spec Kit prerequisite check, `git diff --check`, and added-line static scan.
- [ ] T014 Independent review: verify #947 acceptance, console/browser parity, no raw/debug default output, readable authority intact, and no scope creep into #948/#949.
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
