# Tasks: Browser Command Result UX Audit and Fixes

**Input**: Design documents from `/specs/1000-browser-command-result-ux/`

**Prerequisites**: plan.md, spec.md, research.md, quickstart.md

**Source Issue**: #1087 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1087

## Phase 1: Setup

- [x] T001 Confirm source GitHub issue #1087 and apply `codex-agent in-progress` label.
- [x] T002 Capture Browser Act evidence for the broken browser command-result surfaces.
- [x] T003 Document dirty-worktree branch constraint and avoid switching branches.
- [x] T004 Create Spec Kit artifacts for the player-facing browser UX/parity work.

## Phase 2: First Fix - Faction Detail Projection (P1)

- [x] T005 [US1] Add a failing `ExplorerWebCommandServiceTests` regression test proving faction detail default output does not leak generic `detail` labels, image prompts, color tokens, or implementation keys.
- [x] T006 [US1] Replace generic faction-detail projection with curated player-facing fields in `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`.
- [x] T007 [US1] Run the focused xUnit verification command and confirm the regression passes.
- [x] T008 [US1] Recheck `/фракции` faction detail in Browser Act and save after screenshot/markdown evidence.

## Phase 3: Follow-Up Slices

- [x] T009 [US2] Add issue/task slice for inventory item detail and document/book readability in browser output (#1089).
- [x] T010 [US2] Add issue/task slice for NPC detail navigation that preserves current summary while exposing thoughts, quests, skills, and relationships through useful menu/detail paths (#1090).
- [x] T011 [US2] Add a focused status localization regression for the `Realm` label and change `/status` and `/soul` to use `Царство`.
- [x] T012 [US2] Add issue/task slice for richer status presentation: visual bars, localized time, and effect details in browser output (#1091).
- [x] T013 [US3] Audit default vs advanced mode to ensure raw JSON remains developer-only.
- [x] T017 [US1] Add all-command default-mode regression gates for 50 read-only player-default commands and 48 local-turn player-default commands.
- [x] T018 [US1] Add central player-default projection in `ExplorerWebCommandService` to hide raw JSON, file paths, DTO/API/protocol wording, and technical local-turn tables while preserving advanced diagnostics.
- [x] T019 [US1] Fix player-facing local-turn copy for `/craft`, `/abode_offering`, `/gacha`, and `/chaos_sea` labels caught by the all-command gates and Browser Act.

## Final Verification

- [x] T014 Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExplorerWebCommandServiceTests"`.
- [x] T015 Record Browser Act before/after evidence for any fixed browser command flow.
- [x] T016 Reconcile remaining gaps with GitHub issue comments or follow-up issues before removing `codex-agent in-progress`.
