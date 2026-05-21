# Web UI Afterlife Combat And Entity Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate afterlife spiritual combat, inbox, and entity profile read-only Explorer commands to browser-safe `ExplorerCommandResult` DTOs for issue #573.

**Architecture:** Add one focused DTO builder for afterlife combat/entity surfaces and route the web command service through it after the realm builders. Keep `/spiritual_action` blocked until #574 because it creates pending/local-turn state. The browser DTOs summarize canonical state and include raw JSON audit blocks for the same files the console surfaces inspect.

**Tech Stack:** C#/.NET 8, existing `ExplorerCommandResult` DTOs, xUnit service/registry tests.

---

### Task 1: Regression Tests

**Files:**
- Modify: `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`

- [x] Add web command tests proving `/afterlife_profiles`, `/afterlife_inbox`, `/spiritual_conflict`, `/spiritual_combat_log`, `/spiritual_combat_help`, and `/spiritual_arts` return `Completed` DTOs with Russian player-facing labels.
- [x] Add a registry test proving the same commands and Russian aliases are marked `Migrated`.
- [x] Keep `/spiritual_action` blocked and point it at #574 local-turn/write UX.
- [x] Run the focused tests and confirm they fail before production code changes.

### Task 2: DTO Builder

**Files:**
- Create: `BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`

- [x] Implement alias routing for the six read-only afterlife combat/entity command surfaces.
- [x] Summarize `afterlife_entity_profiles.json`: resources, progression, standard arts, special arts, custom states, soul dissipation danger, and progression strategy.
- [x] Summarize `afterlife_notifications.json` as a read-only inbox; do not mark notifications as read in the browser.
- [x] Summarize `afterlife_spiritual_conflict_state.json`: active conflict, sides, position, strain, control, action points, exchange log, recent conflicts, dice, rewards.
- [x] Provide a static Russian help DTO for `/spiritual_combat_help` and a read-only progression/upgrade status DTO for `/spiritual_arts`.

### Task 3: Registry, Docs, Verification

**Files:**
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandMigrationRegistry.cs`
- Modify: `docs/web-ui/local-web-host.md`

- [x] Move read-only afterlife combat/entity aliases from planned to `Migrated`.
- [x] Keep `/spiritual_action` blocked under #574.
- [x] Document migrated afterlife combat/entity commands and the browser read-only limitation for local spiritual action/upgrades.
- [ ] Run targeted tests, build, full tests, merge to `main`, push, and close #573.
