---
description: "Tasks for Browser lifecycle panel player-facing copy fix (#1088)"
---

# Tasks: Browser Lifecycle Panel Player Copy

**Input**: `specs/1088-browser-lifecycle-copy/spec.md`, `specs/1088-browser-lifecycle-copy/plan.md`
**Source Issue**: #1088 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1088
**Prerequisites**: `AGENTS.md`, `.specify/memory/constitution.md`, browser lifecycle UX reference, issue #1088 acceptance criteria
**Tests**: Test-first is required. The focused regression must fail on the pre-fix copy before production code changes.

## Phase 1: Setup and Scope

- [x] T001 Confirm source GitHub issue #1088 is open and this worktree is `work/1088-browser-lifecycle-copy` based on `origin/main`.
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, issue #1088, and browser lifecycle/player-copy references.
- [x] T003 Decide Spec Kit applicability: required by `AGENTS.md` because this changes player-facing Browser Client UX copy.
- [x] T004 Create lightweight Spec Kit artifacts at `specs/1088-browser-lifecycle-copy/` with issue links in `spec.md`, `plan.md`, and `tasks.md`.

## Phase 2: Regression Test (RED)

- [x] T005 Update `BookOfEternityClient.Tests/BrowserApiContractTests.cs` test `GameScreenIdleTurnState_UsesPlayerFacingRussianCopy` to assert:
  - exact ready-state message `Опишите следующее действие персонажа в прозе. После подтверждения ход будет подготовлен для ГМ.`;
  - exact recommended action description `Заполните основной художественный ввод и подтвердите действие, когда будете готовы передать ход ГМ.`;
  - absence of `Браузерный`, `DTO`, `pending`, `protocol`, `interactive/write`, `API`, and implementation phrasing around `подключен` in both fields.
- [x] T006 Run the focused test and record RED evidence: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserApiContractTests.GameScreenIdleTurnState_UsesPlayerFacingRussianCopy" --logger "console;verbosity=minimal"` fails because the current message says `Запись хода будет подключена отдельным безопасным шагом.`

## Phase 3: Implementation (GREEN)

- [x] T007 Update `BookOfEternityClient/WebUi/BrowserGameScreenService.cs` ready-state message to the accepted player-facing Russian sentence.
- [x] T008 Update the `compose-action` description in `BookOfEternityClient/WebUi/BrowserGameScreenService.cs` to tell the player to confirm when ready to pass the turn to the GM.
- [x] T009 Preserve ready-state ID, title, phase, severity, `CanStartBrowserWrite`, action ID, label, surface, and enabled behavior.

## Phase 4: Verification

- [x] T010 Run focused GREEN test: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserApiContractTests.GameScreenIdleTurnState_UsesPlayerFacingRussianCopy" --logger "console;verbosity=minimal"` — 1 passed / 0 failed / 0 skipped.
- [x] T011 Run browser API/source guard filter: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"` — 28 passed / 0 failed / 0 skipped.
- [x] T012 Run frontend verification: `npm run verify --prefix BookOfEternityClient.WebFrontend` — typecheck passed; player-facing tests 80 passed / 0 failed; Vite build passed.
- [x] T013 Run `git diff --check origin/main...HEAD` — passed with no whitespace errors.
- [x] T014 Run an added-line static scan over non-Spec/non-doc changed code for hardcoded secrets, shell execution, eval/exec, unsafe deserialization, and SQL string-formatting patterns — `NO_MATCHES`.

## Phase 5: Review, PR, Merge, Closure

- [x] T015 Request independent review against issue #1088, `spec.md`, `plan.md`, `tasks.md`, and the committed diff — first review returned `CHANGES_REQUIRED` for stale representative game-screen fixture/helper copy and committed trailing whitespace in Spec Kit docs; re-review approved current head after fixes with no Critical/Important/Minor findings.
- [x] T016 Fix any Critical/Important review findings and rerun the focused verification commands — expanded the regression test to cover runtime DTO, representative `BuildGameScreen()` helper, and `game-screen.json`; updated stale fixture/helper copy; removed trailing whitespace; reran focused C# tests and frontend verify successfully.
- [x] T017 Create a PR with local-gated verification evidence and `Closes #1088` — PR https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1107 created from `work/1088-browser-lifecycle-copy`.
- [ ] T018 Squash-merge after local verification and independent review; do not wait for GitHub Actions.
- [ ] T019 Verify PR merged, issue #1088 is closed/completed, and the main branch contains the fix.
- [ ] T020 Post or preserve closure evidence listing verification commands, review verdict, docs/prompts impact, and Spec Kit artifacts.
