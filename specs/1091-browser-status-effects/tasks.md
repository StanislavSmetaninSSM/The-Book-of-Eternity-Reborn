# Tasks: Browser Status and Effect Details

**Input**: `specs/1091-browser-status-effects/spec.md`, `specs/1091-browser-status-effects/plan.md`, GitHub issue #1091

**Tests**: Production changes require failing tests first.

## Phase 1: Setup

- [x] T001 Confirm #1091 exists and is open.
- [x] T002 Add `codex-agent in-progress` label to #1091.
- [x] T003 Create isolated worktree `work/1091-browser-status`.
- [x] T004 Restore/install dependencies if needed after audit.

## Phase 2: Audit Current Behavior

- [x] T005 Find the browser `/статус` command path and current effect detail routes, if any.
- [x] T006 Inspect console status/effects output for semantic reference.
- [x] T007 Inspect current status/effects canonical state models and test fixtures.
- [x] T008 Inspect existing browser/frontend status tests and visual component support.

## Phase 3: Regression Coverage

- [x] T009 Add failing xUnit test for browser `/статус` visual status resource output.
- [x] T010 Add failing xUnit test for localized realm/time labels.
- [x] T011 Add failing xUnit test for active effect summary actions from `/статус`.
- [x] T012 Add failing xUnit test for selected effect detail output and missing-target fallback.
- [x] T013 Add frontend tests only if React/CSS changes are required.

## Phase 4: Implementation

- [x] T014 Implement minimal browser command-result/status builder changes.
- [x] T015 Implement effect detail read-only route if missing.
- [x] T016 Implement frontend/CSS changes only if required.
- [x] T017 Rerun focused tests and refactor only after green.

## Phase 5: Verification & Closure

- [x] T018 Run focused/broad C# verification for status/effects/browser commands.
- [x] T019 Run frontend verify if frontend files changed.
- [x] T020 Capture Browser Act screenshots for `/статус` and one effect detail route.
- [x] T021 Run `git diff --check`.
- [ ] T022 Commit, open PR for #1091, merge if clean, close issue through PR, remove labels/branches/worktree.
