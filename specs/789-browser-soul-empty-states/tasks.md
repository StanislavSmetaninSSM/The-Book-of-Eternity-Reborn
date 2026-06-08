# Tasks: Browser Soul page empty states and player copy

**Input**: Design documents from `/specs/789-browser-soul-empty-states/`

**Prerequisites**: `spec.md`, `plan.md`, GitHub issue #789 (https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/789), constitution `.specify/memory/constitution.md`

**Tests**: Use test-first/source-guard discipline for missing-data empty states, populated values, player-facing copy, and status-meter semantics. Visual smoke evidence supplements automated guards; it does not replace `npm run verify`.

**Organization**: Tasks are grouped by independently testable player-facing outcomes.

## Phase 1: Setup and baseline

- [x] T001 Confirm source GitHub issue #789 and active branch/worktree (`task/789-browser-soul-empty-states`, `E:/Games/worktrees/boe-789-soul-page-empty-states`).
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #789 issue body, and Browser Client reference lessons for status meters, sidebar/player status, detail surfaces, card hierarchy/status, and Vite workspace boundaries.
- [x] T003 Record clean frontend/browser baseline: `npm ci --prefix BookOfEternityClient.WebFrontend` added 52 packages with 0 vulnerabilities; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed on 2026-06-09 (typecheck, player-facing node guards, Vitest 4 files / 37 tests, Vite build 46 modules); focused `.NET` browser source-smoke initially failed with missing `project.assets.json` under `--no-restore`, then passed 31/31 after restore.

## Phase 2: Guard the #789 Soul/status page contract

- [x] T004 Inspect current `StatusView.tsx`, `StatusBar.tsx`, `command-ui.css`, `components.css`, existing frontend tests, and focused `.NET` browser source guards/smoke expectations.
  - Evidence: inspected current `StatusView.tsx` direct `<dd>{value}</dd>` rendering, #788 `StatusBar`/`status-meter` tests and CSS, `playerCopyRobustness.test.ts`, `browserCardHierarchy.test.tsx`, `playerStatusSidebar.test.tsx`, and `BrowserFrontendWorkspaceTests`/`LocalWebUiBuiltFrontendSmokeTests` source guards. Current architecture is `StatusView`, not obsolete `SoulRoute`/`DetailSurfaceCard`.
- [x] T005 Add focused frontend guard/component tests under `BookOfEternityClient.WebFrontend/test/` that assert #789 requirements:
  - missing player/soul identity fields render a polished empty-state treatment instead of blank values or repeated raw placeholders;
  - meaningful player/soul values remain visible in readable sections by default;
  - default Soul/status copy is Russian/player-facing and excludes raw/system/debug/API/DTO/JSON terms such as `detailsIntro`, `/api/`, endpoint, debug, raw JSON, and agent meta-language;
  - status meters keep semantic good/warning/danger classes and accessible meter metadata.
  - Evidence: added `BookOfEternityClient.WebFrontend/test/browserSoulEmptyStates.test.tsx`, rendering `StatusView` through `ShellContext.Provider` with missing and populated game-screen fixtures.
- [x] T006 Run the focused guard(s) and confirm RED for missing #789 behavior. If a current behavior already satisfies part of an assertion, create mutation/temporary edit proof or document why the guard catches the old placeholder regression.
  - RED evidence: `npm exec -- vitest run test/browserSoulEmptyStates.test.tsx` failed before implementation. First RED was 1 failed / 2 passed because `status-empty-state` was absent and raw blanks/placeholders rendered in `<dd>` cells. After adding the zero-afterlife edge assertion, RED was 2 failed / 1 passed because all-zero afterlife details were hidden.

## Phase 3: Implement the Soul/status page polish

- [x] T007 Add local missing-value helpers that treat empty strings, whitespace, `не указан`, `не указана`, `не назначен`, `unknown`, `n/a`, and `—` as missing where appropriate while preserving meaningful numeric zero values.
  - Evidence: `StatusView.tsx` now has local `missingStatusValues`, `isMissingStatusValue`, `formatStatusValue`, and `StatusRow`; numeric values return directly, so `0` remains visible.
- [x] T008 Render player-facing empty-state copy/classes for missing character/soul identity data without hiding real API/session failures.
  - Evidence: missing player, soul, and world fields render `status-empty-state` notes plus `status-empty-inline` values; the existing no-game branch still returns visible `Данные недоступны.` rather than masking failed state loads.
- [x] T009 Ensure meaningful character, soul, world, and afterlife details are visible in readable sections by default rather than only through a collapsed summary/click target.
  - Evidence: `StatusView.tsx` keeps open `<section className="status-card">` blocks for character, soul, world, and now always-visible afterlife values; all-zero afterlife state renders `Следы посмертия пока не открыты.` while preserving `0` values.
- [x] T010 Preserve and, if necessary, reuse #788 semantic status-meter classes/accessibility for health, energy, and poise on the page.
  - Evidence: existing `StatusMeter` path and `className={\`status-meter status-meter--${severity}\`}` remain; focused test asserts good/warning/danger classes and `role="meter"`/ARIA metadata.
- [x] T011 Update scoped CSS for the empty-state/status cards so the surface looks intentional and does not compete with sibling #790 sidebar or #791 Home page work.
  - Evidence: added scoped `.status-empty-state` and `.status-empty-inline` styles in `BookOfEternityClient.WebFrontend/src/styles/command-ui.css`; no sidebar/Home selectors changed.
- [x] T012 Preserve player-facing/default UI boundaries: do not introduce `/api/`, endpoint, DTO, debug, raw JSON, validation dashboard, or agent meta-language in default browser surfaces.
  - Evidence: focused #789 test bans `detailsIntro`, `/api/`, DTO, endpoint, debug, raw JSON, validation-dashboard, and agent terms in rendered default StatusView markup; existing player-facing guards remain in `npm run verify`.
- [x] T013 Keep implementation presentation-only: do not change C# runtime, game state, prompts, examples, validation, command contracts, afterlife/mortal contracts, or console behavior unless a discovered dependency proves it is necessary and is reflected back into `spec.md`/`plan.md`.
  - Evidence: implementation changed React/CSS/frontend test/package guard plus C# source-guard expectation only. No runtime DTO, endpoint, prompt, example, validation, afterlife/mortal contract, or console behavior files were changed.

## Phase 4: Verification and visual evidence

- [x] T014 Re-run the focused #789 guard(s) and confirm GREEN.
  - GREEN evidence: `npm exec -- vitest run test/browserSoulEmptyStates.test.tsx` from `BookOfEternityClient.WebFrontend` passed 1 file / 3 tests after implementation; rerun after guard synchronization also passed 1 file / 3 tests.
- [x] T015 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact pass counts/output summary.
  - Evidence: passed. Typecheck completed; player-facing script ran Vitest 5 files / 40 tests; Vite build transformed 46 modules and produced `dist/index.html`, CSS, and JS assets.
- [x] T016 Run focused `.NET` browser frontend/source-smoke guard if React source expectations or built-frontend smoke assets changed: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"`.
  - Evidence: first run exposed one source-guard sync failure in `BrowserFrontendWorkspaceTests.FrontendWorkspace_HasVerifyScriptAndCiFrontendWorkflow` because `test:player-facing` now includes `browserSoulEmptyStates.test.tsx`; after updating that guard, rerun passed 31/31.
- [x] T017 Run `git diff --check origin/main...HEAD`.
  - Evidence: post-commit `git diff --check origin/main...HEAD` passed with no output.
- [x] T018 Run added-line static scan excluding plan/spec docs and record `NO_MATCHES` or inspected false positives.
  - Evidence: post-commit added-line scan over `git diff origin/main...HEAD -- . ':(exclude)docs/superpowers/plans/*.md' ':(exclude)specs/**/*.md'` returned `NO_MATCHES`.
- [x] T019 Produce Browser Client visual smoke evidence for missing-data and populated-data Soul/status states: either Vite/browser inspection or a dependency-light artifact under `TestResults/browser-smoke/`. Do not call the artifact a screenshot unless a real screenshot is captured.
  - Evidence: generated dependency-light visual-smoke HTML at `TestResults/browser-smoke/issue-789-soul-status-visual-smoke.html` with missing-data and populated Soul/status states. Artifact is ignored by `/TestResults/` and is not described as a screenshot.

## Phase 5: Reconciliation, review, PR, and closure

- [x] T020 Update this `tasks.md` with implementation and verification evidence after the code changes are real.
- [x] T021 Commit the implementation with `[skip ci]` in the message.
  - Evidence: committed with message `feat(browser): polish soul empty states [skip ci]`.
- [x] T022 Obtain independent review against #789 acceptance criteria, this Spec Kit feature, and `git diff origin/main...HEAD`; fix any Critical/Important findings and re-review if needed.
  - Evidence: independent Codex review run `E:/Games/codex-runs/20260609-0405-boe-789-soul-empty-states-review` returned `APPROVED` with no blocking findings. Review noted the focused Vitest rerun could not start in the detached review worktree because dependencies were intentionally not installed, but Hermes had already rerun the focused Vitest, frontend verify, .NET focused browser guard, `dotnet build`, `git diff --check`, and added-line static scan in the implementation worktree.
- [x] T023 Create a PR that closes #789 and includes local verification evidence; do not wait for GitHub Actions.
  - Evidence: created PR #921 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/921`) with `Closes #789`, local verification evidence, independent review verdict, and `GitHub Actions: not required` note. Readback showed `mergeStateStatus=CLEAN` and closing reference to #789.
- [ ] T024 Squash-merge after local gates and independent review pass; verify PR is `MERGED` and issue #789 is `CLOSED`/`COMPLETED`.
- [ ] T025 Fast-forward primary `main`, run a focused post-merge frontend sanity check if practical, and clean up the issue worktree/branch.

## Dependencies & Execution Order

1. Phase 1 must stay complete before implementation.
2. Phase 2 guard work precedes Phase 3 React/CSS changes.
3. Phase 4 verification precedes review/PR/merge.
4. Phase 5 closure happens only after Hermes verifies diff, tests, review, PR merge, and issue state.

## Spec Kit reconciliation notes

- Contract/docs impact is expected to be `N/A`: no runtime, GM-facing, validation, or example changes.
- #788 card hierarchy/status styling has already landed and should be preserved.
- Sibling issue #790 owns sidebar navigation polish; sibling issue #791 owns broader Home page launcher/action hierarchy and hero/pattern work.
- If implementation discovers that #789 cannot be satisfied without runtime DTO/contract changes, stop and record/update the Spec Kit conflict instead of silently expanding scope.
