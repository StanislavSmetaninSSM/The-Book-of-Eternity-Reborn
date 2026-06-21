# Tasks: Browser NPC Detail Sections

**Input**: `specs/1090-browser-npc-detail/spec.md`, `specs/1090-browser-npc-detail/plan.md`, GitHub issue #1090

**Tests**: Production changes require failing tests first.

## Phase 1: Setup

- [x] T001 Confirm #1090 exists and is open.
- [x] T002 Add `codex-agent in-progress` label to #1090.
- [x] T003 Create isolated worktree `work/1090-browser-npc-detail`.
- [x] T004 Restore/install dependencies if needed after audit.

## Phase 2: Audit Current Behavior

- [x] T005 Inspect current NPC command builder and detail routes.
- [x] T006 Inspect seeded NPC data helpers/tests for thoughts, quests, relationships, and skills.
- [x] T007 Inspect browser action rendering to confirm C# actions are enough.

## Phase 3: Regression Coverage

- [x] T008 Add failing xUnit test for NPC overview section actions.
- [x] T009 Add failing xUnit test for thoughts/journal section detail.
- [x] T010 Add failing xUnit test for personal quests and quest detail action.
- [x] T011 Add failing xUnit test for relationships/social status section.
- [x] T012 Add failing xUnit test for skills/capabilities section and missing-section fallback.

## Phase 4: Implementation

- [x] T013 Implement minimal NPC section routing and actions.
- [x] T014 Render player-facing section detail blocks without raw JSON.
- [x] T015 Add quest detail drilldown if quest records exist.
- [x] T016 Refactor only after green.

## Phase 5: Verification & Closure

- [x] T017 Run focused C# NPC/browser command verification.
- [x] T018 Run frontend verify if frontend files changed. (N/A: no frontend files changed.)
- [x] T019 Capture Browser Act evidence for `/нпс`, thoughts, and personal quests.
- [x] T020 Run `git diff --check`.
- [ ] T021 Commit, open PR for #1090, merge if clean, close issue through PR, remove labels/branches/worktree.
