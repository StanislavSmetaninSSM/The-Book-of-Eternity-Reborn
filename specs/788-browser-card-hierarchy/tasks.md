# Tasks: Browser card hierarchy and status styling

**Input**: Design documents from `/specs/788-browser-card-hierarchy/`

**Prerequisites**: `spec.md`, `plan.md`, GitHub issue #788 (https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/788), constitution `.specify/memory/constitution.md`

**Tests**: Use test-first/source-guard discipline for the visual hierarchy and status behavior. Visual smoke evidence supplements automated guards; it does not replace `npm run verify`.

**Organization**: Tasks are grouped by independently testable player-facing outcomes.

## Phase 1: Setup and baseline

- [x] T001 Confirm source GitHub issue #788 and active branch/worktree (`task/788-browser-card-hierarchy`, `E:/Games/worktrees/boe-788-card-hierarchy`).
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #788 issue body, and Browser Client reference lessons for card spacing polish, React app shell boundaries, and design-system visual smoke.
- [x] T003 Record clean frontend baseline: `npm ci --prefix BookOfEternityClient.WebFrontend` added 52 packages with 0 vulnerabilities; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed on 2026-06-09 (typecheck, player-facing node guards, Vitest 3 files / 32 tests, Vite build 46 modules).

## Phase 2: Guard the #788 visual hierarchy contract

- [x] T004 Inspect current card/action/status/kv-list selectors in `BookOfEternityClient.WebFrontend/src/styles/components.css`, `BookOfEternityClient.WebFrontend/src/styles/command-ui.css`, `BookOfEternityClient.WebFrontend/src/components/StatusBar.tsx`, and launcher/action components.
- [x] T005 Add or update focused frontend guard(s) under `BookOfEternityClient.WebFrontend/test/` that assert #788 requirements:
  - action/CTA cards use a golden/accent border plus gradient/glow and hover/focus feedback distinct from passive info cards;
  - disabled action cards do not look active;
  - `StatusBar` exposes good/warning/danger semantic classes using thresholds `>66`, `>33`, `<=33`;
  - `.kv-list dt` is muted and `.kv-list dd` is brighter/stronger with grid/tokenized spacing.
- [x] T006 Run the focused guard(s) and confirm RED for missing #788 behavior. If current production already satisfies one assertion, create mutation/temporary edit proof or document why the guard catches the old same-card regression.

## Phase 3: Implement the browser card hierarchy polish

- [x] T007 Apply minimal CSS/React changes so launcher/action cards have distinct CTA/interactive treatment while passive cards keep calmer info styling.
- [x] T008 Implement `StatusBar` severity class derivation and scoped CSS fills for good/warning/danger thresholds without broad `.status-bar div` cascade regressions.
- [x] T009 Polish `.kv-list` label/value typography and responsive layout using scoped selectors and existing spacing/color tokens.
- [x] T010 Preserve player-facing/default UI boundaries: do not introduce `/api/`, endpoint, DTO, debug, raw JSON, or agent meta-language in default browser surfaces.
- [x] T011 Keep implementation presentation-only: do not change C# runtime, game state, prompts, examples, validation, command contracts, or GM-facing docs unless a discovered dependency proves it is necessary and is reflected back into `spec.md`/`plan.md`.

## Phase 4: Verification and visual evidence

- [x] T012 Re-run the focused guard(s) and confirm GREEN.
- [x] T013 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record the exact pass counts/output summary.
- [x] T014 Run `git diff --check origin/main...HEAD`.
- [x] T015 Run added-line static scan excluding plan docs and record `NO_MATCHES` or any inspected false positives.
- [x] T016 Produce Browser Client visual smoke evidence for action-card hierarchy, status colors, and key-value typography: either Vite preview inspection or a dependency-light artifact under `TestResults/browser-smoke/`. Do not call the artifact a screenshot unless a real screenshot is captured.

## Phase 5: Reconciliation, review, PR, and closure

- [x] T017 Update this `tasks.md` with implementation and verification evidence after the code changes are real.
- [x] T018 Commit the implementation with `[skip ci]` in the message.
- [x] T019 Obtain independent review against #788 acceptance criteria, this Spec Kit feature, and `git diff origin/main...HEAD`; fix any Critical/Important findings and re-review if needed.
- [ ] T020 Create a PR that closes #788 and includes local verification evidence; do not wait for GitHub Actions.
- [ ] T021 Squash-merge after local gates and independent review pass; verify PR is `MERGED` and issue #788 is `CLOSED`/`COMPLETED`.
- [ ] T022 Fast-forward primary `main`, run a focused post-merge frontend sanity check if practical, and clean up the issue worktree/branch.

## Dependencies & Execution Order

1. Phase 1 must stay complete before implementation.
2. Phase 2 guard work precedes Phase 3 CSS/React changes.
3. Phase 4 verification precedes review/PR/merge.
4. Phase 5 closure happens only after Hermes verifies diff, tests, review, PR merge, and issue state.

## Spec Kit reconciliation notes

- Contract/docs impact is expected to be `N/A`: no runtime, GM-facing, validation, or example changes.
- #787 spacing has already landed and should be preserved.
- Sibling issues #790 and #791 own navigation sidebar and broader Home page layout/CTA acceptance if they require surfaces beyond the shared card hierarchy.
- If implementation discovers that #788 cannot be satisfied without broader navigation/home redesign, stop and record the blocker instead of silently expanding scope.
- Implementation evidence (Codex, 2026-06-09):
  - T004 inspection found launcher/action hooks in `GameLauncher.tsx`, `TurnStatePanel.tsx`, `StatusView.tsx`, `StatusBar.tsx`, `components.css`, and `command-ui.css`; no C# runtime, prompt, example, validation, or game-contract dependency was needed.
  - T005 added `BookOfEternityClient.WebFrontend/test/browserCardHierarchy.test.tsx` and wired it into `test:player-facing`; `playerStatusSidebar.test.tsx` was updated to guard semantic status meters.
  - T006 RED: `npm exec -- vitest run test/browserCardHierarchy.test.tsx` failed before production changes with 4/4 tests failing on missing primary CTA selector, disabled inactive styling, `StatusBar` severity classes, and `.kv-list` grid typography. A second RED after extending status-meter coverage failed 1/5 on the old inline-color `StatusView` meter.
  - T007-T011 implementation stayed frontend presentation-only: `StatusBar`/`StatusView` now derive good/warning/danger classes; action/CTA CSS uses golden gradient/glow hover/focus treatment; disabled actions suppress active chrome; passive launcher info/save cards keep calmer surfaces; `.kv-list` uses grid label/value typography and mobile wrapping.
  - T012 GREEN: `npm exec -- vitest run test/browserCardHierarchy.test.tsx` passed 1 file / 5 tests.
  - T013 GREEN: `npm run verify --prefix BookOfEternityClient.WebFrontend` passed typecheck, player-facing source guards, Vitest 4 files / 37 tests, and Vite build with 46 modules.
  - T014 post-commit `git diff --check origin/main...HEAD` exited 0 with no whitespace errors.
  - T015 post-commit added-line security scan matched only the documented scan command text in `specs/788-browser-card-hierarchy/plan.md`; inspected as a false positive, with no secret literal or unsafe dynamic execution added.
  - T016 visual smoke fallback artifact created at ignored path `TestResults/browser-smoke/788-card-hierarchy.html`; it is a dependency-light HTML artifact, not a screenshot. `browser-act get-skills core --skill-version 2.0.0` was attempted after detecting `browser-act.exe`, but the CLI failed before automation with Python `AssertionError: SRE module mismatch`.
  - Hermes reconciliation RED: broader Browser frontend/source smoke filter `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests"` initially failed 3/31 because existing C# source guards still expected the pre-#788 `test:player-facing` script and literal `className="status-meter"` while the implementation moved to semantic `status-meter--${severity}` classes.
  - Hermes reconciliation GREEN: after synchronizing those C# guard expectations, the same Browser frontend/source smoke filter passed 31/31; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed Vitest 4 files / 37 tests and Vite build 46 modules; `git diff --check origin/main...HEAD` exited 0.
  - T019 independent read-only review completed in `E:/Games/codex-runs/20260609-031351-boe-788-review`; final verdict `APPROVED`, blocking findings: none.
  - T020-T022 intentionally remain open for Hermes PR, merge, issue closure, and worktree cleanup.
