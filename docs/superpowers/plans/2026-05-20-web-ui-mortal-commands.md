# Web UI Mortal Commands Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate Mortal World read-only Explorer commands to browser-safe `ExplorerCommandResult` DTOs for issue #570, while keeping mutating commands explicitly blocked until browser local-turn UX is available.

**Architecture:** Add a focused Mortal DTO builder that reads canonical Mortal World files and returns summaries plus raw JSON audit blocks. The browser command service delegates to this builder after universal/meta handling. The migration registry marks read-only Mortal commands as migrated and keeps mutating commands blocked with explicit local-turn/session-lock rationale.

**Tech Stack:** C#/.NET 8, existing `ExplorerCommandResult` DTOs, xUnit command service/registry tests.

---

### Task 1: Regression Tests

**Files:**
- Modify: `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`

- [x] Add browser service tests proving representative Mortal commands (`/inv`, `/npc`, `/quests`, `/map`, `/stats`, `/combat`, `/weather`, `/books`, `/interactions`) return completed DTOs.
- [x] Add registry tests proving read-only Mortal aliases are marked `Migrated`.
- [x] Keep mutating Mortal commands (`/distribute`, `/companion_directive`, `/faction_directive`, `/craft`) blocked with reasons.

### Task 2: Mortal DTO Builder

**Files:**
- Create: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`

- [x] Implement `ExplorerMortalWorldCommandResultBuilder.TryBuildAsync(...)` with alias normalization for Mortal read-only commands.
- [x] Return browser-safe summaries for inventory, NPCs, quests, map/location, factions, skills/stats, world news, rival threads, guardian corrections, locations, transport, effects, combat, weather/time, item texts, storage access, and player interactions.
- [x] Include raw JSON audit blocks for file-backed commands and message blocks for missing/malformed files.

### Task 3: Registry, Docs, Verification

**Files:**
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandMigrationRegistry.cs`
- Modify: `docs/web-ui/local-web-host.md`

- [x] Move Mortal read-only aliases from planned to `Migrated`.
- [x] Keep Mortal mutating aliases `Blocked` with a follow-up local-turn/browser write UX issue.
- [x] Document the expanded Mortal command set and blocked mutating limitations.
- [ ] Run targeted tests, build, full tests, merge to `main`, push, and close #570.
