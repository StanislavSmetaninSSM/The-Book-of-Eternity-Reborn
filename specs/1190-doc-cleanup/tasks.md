# Tasks: Repository Documentation Cleanup

**Input**: Design documents from `specs/1190-doc-cleanup/`

**Prerequisites**: `plan.md`, `spec.md`, GitHub issue #1190

**Tests**: This is documentation cleanup. Test-first runtime coverage is not applicable to deleting obsolete unreferenced docs; reference searches and docs/source-guard tests are required.

## Phase 1: Setup

- [x] T001 Confirm `git status --short`, active branch `task/1190-doc-cleanup`, and source issue #1190.
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `specs/1190-doc-cleanup/spec.md`, and `specs/1190-doc-cleanup/plan.md`.
- [x] T003 Inventory candidate documentation files outside `docs/superpowers/**` using `rg --files`.

---

## Phase 2: Foundational Audit

- [x] T004 [P] Classify `OtherGuides/**` files as keep/remove/relocate/follow-up.
- [x] T005 [P] Classify root README-like and guide-like files as keep/remove/relocate/follow-up.
- [x] T006 [P] Classify `TaskGuides/**`, `Rules/**`, and `Examples/**` for obvious obsolete agent/dev noise while preserving live contracts.
- [x] T007 For every removal candidate, search for filename and path references before deleting.

---

## Phase 3: Remove Obsolete Noise

- [x] T008 [US1] Delete clearly obsolete unreferenced agent implementation plans and completed development reports from game-facing directories.
- [x] T009 [US1] Keep ambiguous files and record them in the final summary or a follow-up issue rather than deleting them silently.
- [x] T010 [US2] Update or remove live references to deleted files in docs, prompts, tests, manifests, or scripts.

---

## Phase 4: Verification

- [x] T011 Run `git diff --check`.
- [x] T012 Run `rg` for every deleted filename/path and confirm no unintended live references remain.
- [x] T013 Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|BrowserFrontendWorkspaceTests"`.
  - Result: docs/afterlife subset passed separately (`103/103`). The full listed filter was also run and failed in unrelated `BrowserFrontendWorkspaceTests` assertions for existing frontend source-guard expectations; this cleanup diff does not touch frontend files.
- [x] T014 Inspect final `OtherGuides` and root guidance file lists for remaining obvious noise.
- [ ] T015 Commit changes with issue reference #1190 and open a PR.

## Dependencies & Execution Order

- Setup tasks precede audit.
- Audit tasks precede deletion.
- Deletion precedes reference updates and verification.
- Verification precedes commit/PR.

## Implementation Strategy

1. Audit before deleting.
2. Delete only clear unreferenced noise.
3. Preserve contract docs and ambiguous files.
4. Verify references and docs/source guards.
