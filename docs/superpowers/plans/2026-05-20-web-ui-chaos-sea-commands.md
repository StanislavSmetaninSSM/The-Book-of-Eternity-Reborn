# Web UI Chaos Sea Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate Chaos Sea read-only Explorer commands to browser-safe `ExplorerCommandResult` DTOs for issue #571, while keeping pending-contract creation commands blocked until browser local-turn write UX exists.

**Architecture:** Add a focused Chaos Sea DTO builder that reads canonical afterlife files and returns summaries plus raw JSON audit blocks. `ExplorerWebCommandService` delegates to it after universal/meta and Mortal World handlers. The migration registry marks read-only Chaos Sea commands as migrated and keeps `/abode_offering` and `/found_guardian_mantle` blocked with explicit local-turn/session-safety rationale.

**Tech Stack:** C#/.NET 8, existing `ExplorerCommandResult` DTOs, xUnit command service/registry tests.

---

### Task 1: Regression Tests

**Files:**
- Modify: `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`

- [x] Add browser service tests proving representative Chaos Sea commands (`/chaos_sea`, `/guardians`, `/abode_power`, `/guardian_projects`, `/abodes`, `/gacha`) return completed DTOs.
- [x] Add registry tests proving read-only Chaos Sea aliases are marked `Migrated`.
- [x] Keep Chaos Sea mutating commands (`/abode_offering`, `/found_guardian_mantle`) blocked with local-turn/session-safe reasons.

### Task 2: Chaos Sea DTO Builder

**Files:**
- Create: `BookOfEternityClient/UI/ExplorerChaosSeaCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`

- [x] Implement `ExplorerChaosSeaCommandResultBuilder.TryBuildAsync(...)` with alias normalization for Chaos Sea read-only commands.
- [x] Return browser-safe summaries for guardians, active Guardian, Chaos Sea navigation, abode power, Guardian project tracker, known abodes, and guardian/direct gacha surfaces.
- [x] Include raw JSON audit blocks for file-backed commands and message blocks for missing/malformed files.

### Task 3: Registry, Docs, Verification

**Files:**
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandMigrationRegistry.cs`
- Modify: `docs/web-ui/local-web-host.md`

- [x] Move Chaos Sea read-only aliases from planned to `Migrated`.
- [x] Keep Chaos Sea mutating aliases `Blocked` with a follow-up local-turn/browser write UX issue.
- [x] Document the expanded Chaos Sea command set and blocked pending-contract limitations.
- [ ] Run targeted tests, build, full tests, merge to `main`, push, and close #571.
