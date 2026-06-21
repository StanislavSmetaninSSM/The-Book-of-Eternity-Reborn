# Tasks: Browser Map Atlas Drilldown

**Input**: `specs/1212-browser-map-atlas/spec.md`, `specs/1212-browser-map-atlas/plan.md`, GitHub issue #1212

**Tests**: Production changes require failing tests first.

## Phase 1: Setup

- [x] T001 Confirm #1212 exists and is open.
- [x] T002 Add `codex-agent in-progress` label to #1212.
- [x] T003 Create isolated worktree `work/1212-browser-map-atlas`.
- [x] T004 Restore/install .NET and frontend dependencies in the worktree.

## Phase 2: Audit Current Behavior

- [x] T005 Inspect current `BookOfEternityClient.WebFrontend/src/components/MapBlock.tsx`.
- [x] T006 Inspect map DTO and service coverage in `BookOfEternityClient/CommandProtocol/UiBlocks.cs` and `BookOfEternityClient.Tests/LocalMapViewerServiceTests.cs`.
- [x] T007 Run focused C# and frontend checks to determine which #1212 requirements already pass.
- [x] T008 Browser Act smoke `/карта` to confirm the remaining live thumbnail defect.

## Phase 3: Regression Coverage

- [x] T009 Add failing Vitest render coverage in `BookOfEternityClient.WebFrontend/test/blockRenderer.render.test.tsx` for `/api/media/...` map thumbnail rendering.

## Phase 4: Implementation

- [x] T010 Fix map image URL resolution in `BookOfEternityClient.WebFrontend/src/components/MapBlock.tsx` with a trusted media URL allowlist.
- [x] T011 Keep player-facing text sanitization for labels/alt text without applying it to media URLs.

## Phase 5: Verification & Closure

- [x] T012 Run the focused map media regression test.
- [x] T013 Run `npm run typecheck --prefix BookOfEternityClient.WebFrontend`.
- [x] T014 Run `npx vitest run test/playerCopyRobustness.test.ts test/blockRenderer.render.test.tsx`.
- [x] T015 Run `npm run build --prefix BookOfEternityClient.WebFrontend`.
- [x] T016 Run focused C# map/local web host tests.
- [x] T017 Browser Act verify `/карта`, placeholder selection, thumbnail visibility, and enlarged image dialog.
- [x] T018 Run `git diff --check`.
- [ ] T019 Commit, open PR for #1212, merge if clean, close issue through PR, remove labels/branches/worktree.
