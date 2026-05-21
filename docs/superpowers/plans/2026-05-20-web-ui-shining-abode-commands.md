# Web UI Shining Abode Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate Shining Abode Explorer command surfaces to browser-safe `ExplorerCommandResult` DTOs for issue #572 without allowing browser writes before local-turn UX is available.

**Architecture:** Add a focused Shining Abode DTO builder that reads Shining canonical state, resident state, soul state, and pending-control files. The browser command service delegates to it after universal, Mortal, and Chaos Sea builders. `/shining_treasury` and `/source_of_light` return read-only status/guard panels in browser and do not create pending files or mutate economy state.

**Tech Stack:** C#/.NET 8, existing `ExplorerCommandResult` DTOs, xUnit command service/registry tests.

---

### Task 1: Regression Tests

**Files:**
- Modify: `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`

- [x] Add browser service tests proving Shining commands (`/shining_abode`, `/shining_politics`, `/shining_treasury`, `/source_of_light`) return completed DTOs.
- [x] Add registry tests proving Shining aliases are marked `Migrated`.
- [x] Assert Shining browser DTOs do not expose blocked/console-only command fallback text.

### Task 2: Shining DTO Builder

**Files:**
- Create: `BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`

- [x] Implement `ExplorerShiningAbodeCommandResultBuilder.TryBuildAsync(...)` with alias normalization for Shining Abode commands.
- [x] Return browser-safe summaries for overview, politics, treasury, Source of Light, gates, factions, residents, core receipts, trade/forge, pending requests, and progression state.
- [x] Include raw JSON audit blocks for file-backed commands and message blocks for missing/malformed files.
- [x] Keep treasury and Source of Light write operations read-only in browser until #574 local-turn/write UX is implemented.

### Task 3: Registry, Docs, Verification

**Files:**
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandMigrationRegistry.cs`
- Modify: `docs/web-ui/local-web-host.md`

- [x] Move Shining aliases from planned/blocked to `Migrated` read-only browser DTO surfaces.
- [x] Document the expanded Shining command set and read-only limitations for treasury/source actions.
- [ ] Run targeted tests, build, full tests, merge to `main`, push, and close #572.
