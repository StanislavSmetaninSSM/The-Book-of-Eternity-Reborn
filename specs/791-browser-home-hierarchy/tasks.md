# Tasks: Browser Home Action Hierarchy

**Input**: `specs/791-browser-home-hierarchy/spec.md`, `plan.md`, GitHub issue #791, parent epic #680, Browser Client launcher/current-direction references.

**Tracked issue**: [#791](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/791)
**Parent epic**: [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680)

## Phase 1: Setup and Baseline

- [x] T001 Create isolated ASCII worktree `E:/Games/worktrees/boe-791-browser-home-hierarchy` on branch `work/791-browser-home-hierarchy` from `origin/main`.
- [x] T002 Create Spec Kit artifacts under `specs/791-browser-home-hierarchy/` linked to #791 and #680.
- [x] T003 Run Spec Kit prerequisite/discoverability check and record `FEATURE_DIR` evidence.
- [x] T004 Run frontend baseline: `npm run verify --prefix BookOfEternityClient.WebFrontend` after `npm ci` if needed; record exact counts/output summary.
- [x] T005 Run focused C# browser workspace/source guard baseline with `-p:IsTestProject=true`; record exact pass/fail/skip counts.

## Phase 2: RED / Guard Coverage

- [x] T006 Extend or add focused frontend guard(s) for #791 launcher hierarchy before production changes.
  - Guard action affordance: enabled launcher actions have explicit action affordance selectors, icon/arrow affordance, hover/focus-visible styling, and primary-action treatment distinct from passive panels.
  - Guard disabled action reason: unavailable `load` and `new-game` menu actions surface player-facing reasons from the DTO and do not receive active/primary treatment.
  - Guard warning treatment: session validation/error copy renders through a launcher warning/accent element, not plain muted body text.
  - Guard ambient fallback: Home keeps decorative local art or CSS ambient pattern with readability overlay and no broken-image/raw-path wording.
- [x] T007 Run the focused guard and capture RED evidence or a temporary mutation proof if current code already satisfies part of the guard. Revert any temporary mutation before implementation.

## Phase 3: Implementation

- [x] T008 Update `BookOfEternityClient.WebFrontend/src/components/GameLauncher.tsx` with minimal launcher-scoped markup/state changes for action affordance, disabled reason, validation warning, and image fallback handling.
- [x] T009 Update `BookOfEternityClient.WebFrontend/src/styles/components.css` with scoped launcher action/disabled/warning/ambient fallback styles; avoid global card rewrites.
- [x] T010 Add or update dependency-light visual-smoke artifact under `TestResults/browser-smoke/` if Vite screenshot automation is not used. Label it as local visual-smoke artifact, not an automated screenshot.

## Phase 4: Verification

- [x] T011 Run focused launcher guard(s) and record GREEN evidence.
- [x] T012 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact observed typecheck/test/build result.
- [x] T013 Run focused C# browser workspace/source guard command and record exact pass/fail/skip counts.
- [x] T014 Run `git diff --check origin/main...HEAD` and record result.
- [x] T015 Run added-line static security scan over changed non-Spec/non-doc code for hardcoded secrets, shell execution/injection, `eval`, unsafe deserialization, and SQL raw formatting; inspect false positives from tests/docs separately.
- [x] T016 Verify default Home UI remains Russian/player-facing and no raw `/api`, endpoint, DTO, JSON, debug, Spec Kit, or agent language is introduced in player-visible text.
- [x] T017 Verify no C# runtime, console behavior, GM-facing docs/examples, afterlife/mortal contracts, runtime image generation, or save/load/new-chapter authority changed.

## Phase 5: Orchestration (Hermes-owned after Codex implementation)

- [x] T018 Independent review of the implementation against #791/spec acceptance.
- [x] T019 Create PR with local verification evidence and safe closing reference to #791 only.
- [ ] T020 Squash-merge PR after local gates and review approve; do not wait for GitHub Actions unless explicitly requested.
- [ ] T021 Verify PR merged and #791 closed; update labels if needed.
- [ ] T022 Clean up issue worktree/branch when safe.
- [ ] T023 Send Russian closure report naming local verification and next target.

## Evidence Log

- T001: Worktree created from `origin/main` at `af624a6` on branch `work/791-browser-home-hierarchy`.
- T002: Hermes created `spec.md`, `plan.md`, and this `tasks.md` before implementation because #791 is player-facing Browser Client UX work requiring durable Spec Kit handoff.
- T003: Spec Kit prerequisite command `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` resolved `FEATURE_DIR` to `E:\\Games\\worktrees\\boe-791-browser-home-hierarchy\\specs\\791-browser-home-hierarchy` with `AVAILABLE_DOCS=["tasks.md"]`.
- T004: `npm ci --prefix BookOfEternityClient.WebFrontend` installed 52 packages and reported 1 high severity npm audit advisory. Baseline `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: TypeScript typecheck passed; player-facing compiled Node guards passed; Vitest reported 11 files passed / 77 tests passed; Vite build transformed 51 modules and built `dist/index.html`, CSS, and JS.
- T005: Baseline focused C# browser guard command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"` passed: 31 passed / 0 failed / 0 skipped / 31 total.
- Primary checkout caveat: `E:/Games/The Book of Eternity Reborn` currently has unrelated pre-existing tracked modification in `BookOfEternityClient/Services/QteSceneService.Daren.cs`; this feature uses the isolated ASCII worktree and must not touch that dirty primary checkout change.
- T006: Extended `BookOfEternityClient.WebFrontend/test/gameLauncherMenuLayout.test.ts` before production edits to require #791 launcher action state data attributes, enabled-primary-only styling, DTO-derived disabled reasons, inline action affordance copy, validation warning markup, decorative image error hiding, CSS ambient fallback, disabled affordance styling, and launcher warning styles.
- T007 RED: `npm run test:player-facing --prefix BookOfEternityClient.WebFrontend` failed before production edits with `Error: GameLauncher should expose active and enabled-primary launcher-menu item states.` from `gameLauncherMenuLayout.test.js`. No temporary mutation was needed because the unmodified implementation failed the new #791 guard.
- T008: `GameLauncher.tsx` now derives session warning text from `menu.session.validationLabel`, hides the decorative image on load failure, exposes `data-launcher-mode` and `data-action-state`, suppresses primary styling on disabled actions, renders disabled button reasons from the matching `BrowserMainMenuDto` action, and keeps save/load/new-chapter execution authority unchanged.
- T009: `components.css` now scopes launcher action hierarchy to `.launcher-menu__item`, `.launcher-menu__item-copy`, `.launcher-menu__item-affordance`, disabled state selectors, `.launcher-session-warning`, and `.launcher-art-bg::before` ambient fallback. No global card rewrite was made.
- T010: Updated `LocalWebUiBuiltFrontendSmokeTests.BuiltFrontendSmoke_GeneratesMainMenuBackgroundArtArtifact` to generate `TestResults/browser-smoke/home-launcher-hierarchy.html` as a dependency-light local HTML visual smoke artifact, explicitly labelled as not an automated screenshot.
- T011 GREEN: Focused `npm run test:player-facing --prefix BookOfEternityClient.WebFrontend` passed after implementation: TypeScript player-facing tests compiled, Node source guards passed, Vitest reported 11 files passed / 77 tests passed.
- T012: Final `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: TypeScript app/node typecheck passed; player-facing compiled guards and Vitest passed with 11 files / 77 tests; Vite 8.0.14 transformed 51 modules and built `dist/index.html`, `dist/assets/index-C3D43sr7.css`, and `dist/assets/index-DoXq4Ksn.js`.
- T013: Final focused C# browser guard command `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"` passed: 31 passed / 0 failed / 0 skipped / 31 total.
- T014: `git diff --check origin/main...HEAD` returned exit 0 with no output before commit; additional working-tree `git diff --check` returned exit 0 with only normal Windows LF-to-CRLF notices for touched files.
- T015: Added-line security scan over changed non-Spec/non-doc code scanned 247 added lines and reported `NO_ADDED_LINE_SECURITY_HITS` for hardcoded secret, shell execution/injection, `eval`, unsafe deserialization, and raw SQL patterns.
- T016: Player-facing boundary verified by the final frontend guard run, including `playerCopyRobustness.test.ts`. An added-line raw-term scan over Home production code found only `BrowserMainMenuDto` in a TypeScript type signature; this was inspected as non-player-visible implementation type text. No new player-visible `/api`, endpoint, DTO, JSON, debug, Spec Kit, or agent wording was introduced.
- T017: Scope check from `git diff --name-only -- . ':!specs/**'` showed only `BookOfEternityClient.WebFrontend/src/components/GameLauncher.tsx`, `BookOfEternityClient.WebFrontend/src/styles/components.css`, `BookOfEternityClient.WebFrontend/test/gameLauncherMenuLayout.test.ts`, `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`, and `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`. No C# runtime, console, GM docs/examples, afterlife/mortal contracts, runtime image generation, or save/load/new-chapter authority files changed.
- Hermes fresh verification after Codex completion: Spec Kit prerequisite resolved `FEATURE_DIR=E:\\Games\\worktrees\\boe-791-browser-home-hierarchy\\specs\\791-browser-home-hierarchy`; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 11 files / 77 tests and Vite 51 modules; focused .NET browser guards passed 31/31; `git diff --check origin/main...HEAD` clean; static/security scan over 247 added non-Spec/non-doc lines `NO_MATCHES`; default player-facing prose/meta scan over 127 launcher source additions `NO_MATCHES`.
- T018: Detached independent Codex review `E:/Games/codex-runs/20260617-1105-review-boe-791-browser-home-hierarchy` at reviewed head `db470611456ea6635e7786ac95393f2a903ce698` returned `VERDICT: APPROVED` with no Critical/Important/Minor findings. Non-blocking risk noted: coverage uses source-string guards plus dependency-light static HTML artifact rather than live rendered screenshot/DOM assertion; disabled affordance arrow after `Закрыто` remains visually acceptable but should be watched in future visual QA.
- T019: PR #1076 (`https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1076`) was created from `work/791-browser-home-hierarchy` to `main` with local verification and independent review evidence. GitHub readback reported `mergeStateStatus=CLEAN`, draft=false, and `closingIssuesReferences` containing only #791; parent #680 is mentioned as non-closing context only.
