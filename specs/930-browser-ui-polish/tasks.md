# Tasks: Browser UI Dark-Fantasy Polish

**Input**: `specs/930-browser-ui-polish/spec.md`, `specs/930-browser-ui-polish/plan.md`, issue [#930](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/930)
**Source Issues**: #930, parent #680, related generated-art task #929
**Branch**: `boe/930-browser-ui-polish`

## Phase 1: Setup and Spec Kit

- [x] T001 Confirm tracked task #930, clean worktree, current branch, `AGENTS.md`, and constitution before implementation.
- [x] T002 Create `specs/930-browser-ui-polish/spec.md`, `plan.md`, `tasks.md`, `checklists/requirements.md`, and `ui-audit.md` with links to #930, #680, and #929.
- [x] T003 Run a Spec Kit cross-artifact consistency check after tasks exist and fix any critical issues before implementation.

## Phase 2: RED Coverage

- [x] T004 [P] [US1] Add failing frontend/source guard coverage for the current 5-tab player navigation and non-emoji navigation glyph contract in `BookOfEternityClient.WebFrontend/test/sidebarNavigation.test.ts` or a focused test included by verify.
- [x] T005 [P] [US1] Add failing source guard coverage for command shell CSS drift in `BookOfEternityClient.WebFrontend/test/browserPolishDesignSystem.test.ts`.
- [x] T006 [P] [US2] Add or update player-copy/source guard coverage for default UI technical-language leakage in `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` and/or frontend tests.
- [x] T007 [P] [US3] Add failing .NET smoke expectations for the current player tab model and #930 visual artifact text in `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`.

## Phase 3: User Story 1 - Coherent Game Client Shell (P1)

- [x] T008 [US1] Replace tab emoji payloads with semantic glyph ids in `BookOfEternityClient.WebFrontend/src/components/tabBarConfig.ts`.
- [x] T009 [US1] Render local styled tab glyphs in `BookOfEternityClient.WebFrontend/src/components/TabBar.tsx` without adding an icon package.
- [x] T010 [US1] Consolidate legacy design-token aliases and command-shell fallback colors in `BookOfEternityClient.WebFrontend/src/styles/tokens.css`.
- [x] T011 [US1] Polish `.tab-bar`, `.content-area`, `.scene-view`, `.status-view`, `.settings-view`, `.unified-input`, and command-result selectors in `BookOfEternityClient.WebFrontend/src/styles/command-ui.css` so they align with the dark-fantasy design system.
- [x] T012 [US1] Adjust responsive/focus/reduced-motion rules in existing CSS modules where the touched shell surfaces need stable Russian text fitting.

## Phase 4: User Story 2 - Advanced Details Stay Explicit (P1)

- [x] T013 [US2] Keep advanced diagnostics and raw endpoint/DTO details gated behind existing `advancedEnabled` checks while polishing touched default surfaces.
- [x] T014 [US2] Update source guards so default player UI excludes raw endpoint/DTO/debug/file-path/API wording while advanced diagnostics may retain technical language.

## Phase 5: User Story 3 - Verified Polish Evidence (P2)

- [x] T015 [US3] Update .NET visual smoke artifact generation to include the current 5-tab sequence and #930 first-pass polish checklist without weakening technical-surface exclusion assertions.
- [x] T016 [US3] Update `specs/930-browser-ui-polish/ui-audit.md` with before findings, implementation evidence, skills/guidelines used, and screenshot/artifact limitations.
- [x] T017 [US3] Run Browser or browser-act local verification when feasible and record whether evidence is a real screenshot or generated offline artifact.

## Phase 6: Verification and Handoff

- [x] T018 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact observed counts.
- [x] T019 Run required focused .NET browser/local-web test command and record exact observed pass/fail counts.
- [x] T020 Run `git diff --check`.
- [x] T021 Run added-line static scan for raw endpoint/DTO/debug/file-path/API wording, secrets, and injection hazards.
- [x] T022 Reconcile task checkboxes only after implementation and verification evidence exists; leave Hermes-owned PR/issue lifecycle untouched.

## Dependencies & Execution Order

- Setup/spec tasks T001-T003 precede all code edits.
- RED tests T004-T007 precede implementation tasks T008-T015.
- US1 and US2 implementation can proceed together after RED coverage because they mostly touch separate CSS/test/source-guard concerns.
- US3 evidence tasks depend on implementation and smoke guard updates.
- Verification tasks T018-T022 run last.

## MVP Scope

Complete T001-T015 and pass frontend plus focused .NET browser/local-web verification. T016-T022 provide handoff evidence and hygiene for Hermes.
