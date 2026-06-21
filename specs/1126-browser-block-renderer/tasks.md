# Tasks: Browser Block Renderer Rich Command Output

**Input**: `specs/1126-browser-block-renderer/spec.md`, `specs/1126-browser-block-renderer/plan.md`, GitHub issue #1126

**Tests**: Production renderer/CSS changes require failing frontend tests first.

## Phase 1: Setup

- [x] T001 Confirm #1126 exists, is open, and is labelled for browser-client work.
- [x] T002 Move #1126 from GLM to Codex in-progress label.
- [x] T003 Create isolated worktree `work/1126-block-renderer`.
- [x] T004 Inspect frontend package scripts and restore/install dependencies if required.

## Phase 2: Audit Existing Renderer

- [x] T005 Inspect `contracts.ts` to enumerate actual command-result and block DTO shapes.
- [x] T006 Inspect `BlockRenderer.tsx` for block-kind coverage, hierarchy handling, and diagnostic gating.
- [x] T007 Inspect `CommandResultView.tsx` for action layout, advanced mode, and section composition.
- [x] T008 Inspect `components.css` and design tokens for existing dark-fantasy renderer styles.
- [x] T009 Inspect existing frontend tests and fixture patterns.

## Phase 3: Regression Coverage

- [x] T010 Add failing tests for nested panel/list hierarchy rendering.
- [x] T011 Add failing tests for raw JSON/diagnostic blocks hidden by default and visible in advanced mode.
- [x] T012 Add failing tests for clear action/back button rendering.
- [x] T013 Add failing tests for dense table/readability behavior.

## Phase 4: Implementation

- [x] T014 Run focused new tests and confirm RED for real renderer gaps.
- [x] T015 Implement minimal React renderer fixes.
- [x] T016 Implement minimal CSS fixes using existing dark-fantasy tokens.
- [x] T017 Rerun focused tests and refactor only after green.

## Phase 5: Verification & Closure

- [x] T018 Run `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- [x] T019 Capture browser screenshots for overview/detail/nested/table-heavy output.
- [ ] T020 Run `git diff --check`.
- [ ] T021 Commit, open PR for #1126, merge if clean, close issue through PR, remove temporary labels/branches/worktree.
