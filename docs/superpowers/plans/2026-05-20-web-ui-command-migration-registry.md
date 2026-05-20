# Web UI Command Migration Registry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Track browser/DTO migration status for every registered player slash command.

**Architecture:** Add a UI-neutral static registry in `BookOfEternityClient.CommandProtocol`. Tests instantiate `ExplorerMode`, read the actually registered command aliases, and require a registry entry for each alias. The registry is metadata-only; command execution remains unchanged until later migration tasks.

**Tech Stack:** C# 12 / .NET 8, xUnit reflection coverage tests.

---

### Task 1: Coverage Tests

**Files:**
- Create: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`

- [ ] **Step 1: Write failing test for missing metadata**

Instantiate `ExplorerMode`, read `_allCommandNames` by reflection, and assert every registered command is present in `ExplorerCommandMigrationRegistry.Entries`.

- [ ] **Step 2: Write failing test for temporary console-only rules**

Assert entries with `ConsoleOnlyTemporarily` or `Blocked` have a non-empty reason and `FollowUpIssue` containing `#`.

- [ ] **Step 3: Verify RED**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter ExplorerCommandMigrationRegistryTests
```

Expected: fails because registry types do not exist.

### Task 2: Registry Implementation

**Files:**
- Create: `BookOfEternityClient/CommandProtocol/ExplorerCommandMigrationRegistry.cs`

- [ ] **Step 1: Add metadata types**

Add `ExplorerCommandMigrationStatus`, `ExplorerCommandGroup`, and `ExplorerCommandMigrationEntry`.

- [ ] **Step 2: Enumerate all current commands**

Add entries for universal, Mortal World, Chaos Sea, Shining Abode, afterlife combat/entity, and Saref story commands. Assign `Planned` status with the relevant follow-up issue (`#569`-`#575`) unless the command is explicitly console-only or blocked.

- [ ] **Step 3: Verify GREEN**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter ExplorerCommandMigrationRegistryTests
```

Expected: registry tests pass.

### Task 3: Full Verification And Merge

- [ ] **Step 1: Build**

Run:

```powershell
dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:UseSharedCompilation=false --verbosity:minimal
```

- [ ] **Step 2: Full tests**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-build --logger "console;verbosity=minimal"
```

- [ ] **Step 3: Commit, merge, close #562**

Commit the registry, merge to `main`, push, and close issue #562 with verification evidence.
