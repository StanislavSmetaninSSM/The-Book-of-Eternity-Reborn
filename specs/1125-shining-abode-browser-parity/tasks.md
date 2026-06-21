# Tasks: Shining Abode Browser Command Output Parity

**Input**: `specs/1125-shining-abode-browser-parity/spec.md`, `specs/1125-shining-abode-browser-parity/plan.md`, GitHub issue #1125

**Tests**: Production changes require failing tests first. Existing passing behavior should be preserved as regression evidence.

## Phase 1: Setup

- [x] T001 Confirm #1125 exists, is open, and is labelled for browser-client work.
- [x] T002 Move #1125 from GLM to Codex in-progress label.
- [x] T003 Create isolated worktree `work/1125-shining-browser`.
- [x] T004 Restore test project dependencies in the isolated worktree.

## Phase 2: Audit Existing Coverage

- [x] T005 Inspect `docs/audits/afterlife-drilldown-audit.md` and `OtherGuides/Afterlife_Contract_Matrix.md` for Shining Abode command expectations.
- [x] T006 Inspect `ExplorerMode.Afterlife.ShiningAbode*.cs` console surfaces for overview/detail/selector behavior.
- [x] T007 Inspect `ExplorerShiningAbodeCommandResultBuilder` and `ExplorerWebCommandService` browser route behavior.
- [x] T008 Inspect existing Shining browser tests for covered and missing parity surfaces.

## Phase 3: Regression Coverage

- [x] T009 Add focused browser tests for every audited Shining overview/detail gap.
- [x] T010 Add hidden/GM-only leak assertions for covered Shining surfaces.
- [x] T011 Add missing-target/unavailable assertions for new selected detail routes.
- [x] T012 Add preview/form safety assertions if a mutating Shining route is touched.

## Phase 4: Implementation

- [x] T013 Run focused new Shining tests and confirm RED for real gaps.
- [x] T014 Implement minimal shared C# builder fixes for failing tests.
- [x] T015 Rerun focused Shining tests and keep production changes read-only unless explicitly required.
- [x] T016 No runtime afterlife contracts changed; documentation coverage fallback not required.

## Phase 5: Verification & Closure

- [x] T017 Run broad `Shining|Afterlife|ExplorerWebCommand` verification.
- [x] T018 Run `git diff --check`.
- [ ] T019 Commit, open PR for #1125, merge if clean, close issue through PR, remove temporary labels/branches/worktree.
