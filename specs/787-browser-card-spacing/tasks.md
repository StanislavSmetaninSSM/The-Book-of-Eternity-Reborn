# Tasks: Browser card spacing polish

**Input**: Design documents from `/specs/787-browser-card-spacing/`

**Prerequisites**: `spec.md`, `plan.md`, GitHub issue #787, constitution `.specify/memory/constitution.md`

**Tests**: Use test-first/source-guard discipline for the spacing behavior. Visual smoke evidence supplements automated guards; it does not replace `npm run verify`.

**Organization**: Tasks are grouped by independently testable player-facing outcomes.

## Phase 1: Setup and baseline

- [x] T001 Confirm source GitHub issue #787 and active branch/worktree (`codex/787-browser-card-spacing`, `E:/Games/worktrees/boe-787-card-spacing`).
- [x] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #787 issue body, and Browser Client reference lessons for design-system visual smoke, detail surfaces, and sidebar/status spacing.
- [x] T003 Record clean frontend baseline: `npm run verify --prefix BookOfEternityClient.WebFrontend` passed on 2026-06-08 before implementation (typecheck, player-facing tests, Vitest 29/29, build).

## Phase 2: Guard the spacing contract

- [x] T004 Inspect current card/sidebar/detail style selectors in `BookOfEternityClient.WebFrontend/src/styles/components.css`, `layout.css`, `command-ui.css`, and related component class names.
- [x] T005 Add or update a focused frontend style/source guard under `BookOfEternityClient.WebFrontend/test/` that asserts #787 minimum spacing for affected selectors:
  - ShellPanel/card-like surfaces use at least `var(--space-4)` desktop padding.
  - Mobile card-like surfaces keep at least `var(--space-3)` padding.
  - Summary/narrative/detail/sidebar card stacks use tokenized gaps for eyebrow/title/content/value rows.
- [x] T006 Run the focused guard or `npm run test:player-facing --prefix BookOfEternityClient.WebFrontend` and confirm the new guard fails for the expected missing/cramped spacing assertion before implementation. If the current code already satisfies one assertion, add a mutation/temporary edit proof or document why the guard still catches regressions.

## Phase 3: Implement the browser spacing polish

- [x] T007 Apply minimal CSS/markup changes so `ShellPanel`, `.summary-card`, `.narrative-card`, DetailSurfaceCard summary/collapsed surfaces, and status/sidebar cards use the shared spacing rhythm.
- [x] T008 Preserve player-facing/default UI boundaries: do not introduce `/api/`, endpoint, DTO, debug, raw JSON, or agent meta-language in default browser surfaces.
- [x] T009 Keep implementation presentation-only: do not change C# runtime, game state, prompts, examples, validation, command contracts, or GM-facing docs unless a discovered dependency proves it is necessary and is reflected back into `spec.md`/`plan.md`.

## Phase 4: Verification and visual evidence

- [x] T010 Re-run the focused guard and confirm GREEN.
- [x] T011 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record the exact pass counts/output summary.
- [x] T012 Run `git diff --check origin/main...HEAD`.
- [x] T013 Run added-line static scan excluding plan docs and record `NO_MATCHES` or any inspected false positives.
- [x] T014 Produce Browser Client visual smoke evidence for desktop and mobile spacing: either Vite preview inspection or a dependency-light artifact under `TestResults/browser-smoke/`. Do not call the artifact a screenshot unless a real screenshot is captured.

## Phase 5: Reconciliation, review, PR, and closure

- [x] T015 Update this `tasks.md` with implementation and verification evidence after the code changes are real.
- [x] T016 Commit the implementation with `[skip ci]` in the message.
- [ ] T017 Obtain independent review against #787 acceptance criteria, this Spec Kit feature, and `git diff origin/main...HEAD`; fix any Critical/Important findings and re-review if needed.
- [ ] T018 Create a PR that closes #787 and includes local verification evidence; do not wait for GitHub Actions.
- [ ] T019 Squash-merge after local gates and independent review pass; verify PR is `MERGED` and issue #787 is `CLOSED`/`COMPLETED`.
- [ ] T020 Fast-forward primary `main`, run a focused post-merge frontend sanity check if practical, and clean up the issue worktree/branch.

## Dependencies & Execution Order

1. Phase 1 must stay complete before implementation.
2. Phase 2 guard work precedes Phase 3 CSS changes.
3. Phase 4 verification precedes review/PR/merge.
4. Phase 5 closure happens only after Hermes verifies diff, tests, review, PR merge, and issue state.

## Spec Kit reconciliation notes

- Contract/docs impact is expected to be `N/A`: no runtime, GM-facing, validation, or example changes.
- Sibling issues #788 and #791 own broader visual hierarchy/action-button differentiation; avoid absorbing them into this closure unit.
- If implementation discovers that #787 cannot be satisfied without broader hierarchy/navigation changes, stop and record the blocker instead of silently expanding scope.

## Implementation and verification evidence

- 2026-06-08 Codex inspection found the active minimalist shell uses `command-ui.css` for visible scene/status/settings cards while `components.css` still owns the shared ShellPanel, summary/narrative, detail-surface, and sidebar/status selectors named by #787.
- RED: `npx vitest run test/browserCardSpacing.test.ts` failed 3/3 tests before production CSS changes because shared shell cards lacked tokenized display/gap/padding, active browser cards used ad hoc rem spacing, and mobile spacing rules were missing.
- GREEN: `npx vitest run test/browserCardSpacing.test.ts` passed 3/3 after tokenized spacing updates.
- Frontend verify: `npm run verify --prefix BookOfEternityClient.WebFrontend` exited 0 after the guard was wired into `test:player-facing`; TypeScript passed, player-facing node guards passed, Vitest reported 3 files and 32 tests passed, and Vite built 46 modules.
- Diff/static checks: `git diff --check origin/main...HEAD` exited 0. The added-line static scan returned one inspected false positive from `specs/787-browser-card-spacing/plan.md` because the plan records the literal static-scan command text and regex; no application/source secret or injection line was found.
- Preview/smoke: Vite preview responded 200 at `http://127.0.0.1:4173`; in-app Browser was unavailable in this Codex session, so the fallback visual smoke artifact was generated at `TestResults/browser-smoke/card-spacing-smoke.html` with desktop and mobile iframe samples using the built CSS. This artifact is not a screenshot.
- Commit: implementation committed with `[skip ci]`; final SHA is reported in the Codex final response after amend.
- Scope: only Browser Client frontend CSS/test/script and this Spec Kit task evidence changed; no C# runtime, game state, validation, prompts, examples, GM-facing docs, or afterlife/mortal contracts were touched.
