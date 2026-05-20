# Web UI Command Protocol DTOs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a UI-neutral command result protocol for future console/browser command rendering.

**Architecture:** Keep the protocol in `BookOfEternityClient.CommandProtocol`, away from Spectre-specific UI adapters. The first slice defines DTOs only: command execution state, render blocks, actions, prompts, and notifications. Console/browser renderers and migrated commands are intentionally left to follow-up issues.

**Tech Stack:** C# 12 / .NET 8, `System.Text.Json` polymorphism, xUnit.

---

### Task 1: Protocol Shape Tests

**Files:**
- Create: `BookOfEternityClient.Tests/ExplorerCommandProtocolTests.cs`

- [ ] **Step 1: Write failing serialization tests**

Add tests that serialize and deserialize an `ExplorerCommandResult` containing:
- text, panel, table, list, key-value grid, warning/error, and raw JSON blocks
- actions
- confirmation, selection, text input, and long text input prompts
- notifications

- [ ] **Step 2: Write failing Spectre isolation test**

Add a reflection test that all DTO types live in `BookOfEternityClient.CommandProtocol` and do not expose public properties from `Spectre.Console` or `Spectre.Console.Rendering`.

- [ ] **Step 3: Verify RED**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-build --filter ExplorerCommandProtocolTests
```

Expected: fails because the protocol namespace/types do not exist yet.

### Task 2: DTO Implementation

**Files:**
- Create: `BookOfEternityClient/CommandProtocol/ExplorerCommandResult.cs`
- Create: `BookOfEternityClient/CommandProtocol/UiBlocks.cs`
- Create: `BookOfEternityClient/CommandProtocol/UiPrompts.cs`
- Create: `BookOfEternityClient/CommandProtocol/UiActions.cs`
- Create: `BookOfEternityClient/CommandProtocol/UiNotifications.cs`

- [ ] **Step 1: Add root result and execution state**

Implement `ExplorerCommandResult` with `Command`, `State`, `Blocks`, `Actions`, `Prompts`, and `Notifications`.

- [ ] **Step 2: Add block DTOs**

Implement polymorphic `UiBlock` DTOs for text, panel, table, list, key-value grid, message, and raw JSON content.

- [ ] **Step 3: Add prompt/action/notification DTOs**

Implement polymorphic prompts plus plain action and notification DTOs.

- [ ] **Step 4: Verify GREEN**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-build --filter ExplorerCommandProtocolTests
```

Expected: protocol tests pass.

### Task 3: Full Verification And Merge

**Files:**
- All files above.

- [ ] **Step 1: Build**

Run:

```powershell
dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:UseSharedCompilation=false --verbosity:minimal
```

Expected: 0 errors.

- [ ] **Step 2: Full tests**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-build --logger "console;verbosity=minimal"
```

Expected: all tests pass.

- [ ] **Step 3: Commit, merge, close #561**

Commit on the feature branch, merge to `main`, push, and close issue #561 with the verification summary.
