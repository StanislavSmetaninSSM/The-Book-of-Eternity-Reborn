# Web UI Universal Meta Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate universal/help/status/meta commands to browser-safe `ExplorerCommandResult` DTOs for issue #569.

**Architecture:** Add a thin DTO builder for read-only universal/meta commands that reads canonical session files and returns summaries plus raw JSON audit blocks. The browser command service calls the builder for migrated commands; existing console-specific rich menus remain intact unless a command already uses DTO flow.

**Tech Stack:** C#/.NET 8, existing `ExplorerCommandResult` DTOs, xUnit service/registry tests.

---

### Task 1: Regression Tests

**Files:**
- Modify: `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`

- [x] Add failing tests for `/status`, `/soul`, `/codex`, `/story`, `/debug`, and `/галерея` returning completed DTOs instead of migration blockers.
- [x] Add failing registry tests proving the migrated aliases are marked `Migrated`.

### Task 2: Universal DTO Builder

**Files:**
- Create: `BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`

- [x] Implement `ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(...)` with command alias normalization.
- [x] Add browser-safe summaries for status, soul, story, codex, chronicle, achievements, behavior, lives, feathers, world rules, gallery, GM thoughts, debug, mods, and system guardians.
- [x] Return `UiMessageBlock` for absent files and malformed JSON instead of throwing.
- [x] Include raw JSON audit blocks when a command is file-backed.

### Task 3: Registry, Docs, Verification

**Files:**
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandMigrationRegistry.cs`
- Modify: `docs/web-ui/local-web-host.md`

- [x] Move browser-safe universal/meta aliases from planned/temporary status to `Migrated`.
- [x] Document the expanded migrated command set and remaining lifecycle limitations.
- [x] Run targeted tests, build, and full tests before merge.
- [ ] Merge to `main`, push, and close #569.
