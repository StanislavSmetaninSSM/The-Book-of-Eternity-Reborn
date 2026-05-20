# Web UI First Read-Only Command Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the first safe read-only ExplorerMode command group to `ExplorerCommandResult` before console rendering.

**Architecture:** Start with `/help` and `/помощь` because they are read-only and available in every realm. Move help table construction into a UI-neutral DTO builder, render the DTO through `ExplorerCommandResultConsoleRenderer`, and mark the migrated aliases in `ExplorerCommandMigrationRegistry`.

**Tech Stack:** C#/.NET, xUnit, existing `BookOfEternityClient.CommandProtocol` DTOs, existing `IExplorerConsole` renderer.

---

### Task 1: Add DTO Content Coverage For Help

**Files:**
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`
- Test: `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`

- [ ] Add a registry test asserting `/help` and `/помощь` are `Migrated`.
- [ ] Add a behavior test asserting `/помощь` still renders the same key Russian help text after migration.
- [ ] Run the targeted tests and verify the registry test fails before production changes.

### Task 2: Build Help As ExplorerCommandResult

**Files:**
- Create: `BookOfEternityClient/CommandProtocol/ExplorerHelpCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs`

- [ ] Create an internal `ExplorerHelpCommandResultBuilder` with `Build(...)` returning `ExplorerCommandResult`.
- [ ] Convert existing help rows to plain DTO table rows without Spectre markup in the DTO.
- [ ] Replace `ShowHelp` table construction with `ExplorerCommandResultConsoleRenderer.Render(...)` and keep `WaitForKey()`.
- [ ] Preserve realm-specific command visibility and Russian wording.

### Task 3: Mark Commands Migrated And Verify

**Files:**
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandMigrationRegistry.cs`

- [ ] Mark `/help` and `/помощь` as `Migrated`.
- [ ] Run targeted tests for help, registry, and command protocol.
- [ ] Run `dotnet build BookOfEternityClient.sln --no-restore`.
- [ ] Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore`.
- [ ] Commit, merge to `main`, push, and close GitHub issue #564.
