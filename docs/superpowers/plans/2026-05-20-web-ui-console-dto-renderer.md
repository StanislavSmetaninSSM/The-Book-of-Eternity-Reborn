# Web UI Console DTO Renderer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render `ExplorerCommandResult` DTOs through the existing Spectre console adapter.

**Architecture:** Add a console-only renderer in `BookOfEternityClient.UI` that consumes UI-neutral DTOs and writes Spectre `IRenderable` objects through `IExplorerConsole`. The renderer is an adapter only; command migration remains in later issues.

**Tech Stack:** C# 12 / .NET 8, Spectre.Console, xUnit.

---

### Task 1: Renderer Tests

**Files:**
- Create: `BookOfEternityClient.Tests/ExplorerCommandResultConsoleRendererTests.cs`

- [ ] **Step 1: Write failing fixture render test**

Create an `ExplorerCommandResult` with text, panel, table, list, key-value grid, warning/error message, raw JSON, action, prompt, and notification data. Render it into `TestExplorerConsole` and assert the extracted text contains representative Russian labels and values.

- [ ] **Step 2: Verify RED**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter ExplorerCommandResultConsoleRendererTests
```

Expected: fails because the renderer does not exist.

### Task 2: Renderer Implementation

**Files:**
- Create: `BookOfEternityClient/UI/ExplorerCommandResultConsoleRenderer.cs`

- [ ] **Step 1: Add public renderer entrypoint**

Implement `ExplorerCommandResultConsoleRenderer.Render(IExplorerConsole console, ExplorerCommandResult result)`.

- [ ] **Step 2: Render blocks**

Map DTO blocks to Spectre text/panels/tables/lists/key-value grids/raw JSON.

- [ ] **Step 3: Render actions, prompts, notifications**

Render actions and prompts as readable console summary panels/tables. Do not execute prompts yet.

- [ ] **Step 4: Verify GREEN**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter ExplorerCommandResultConsoleRendererTests
```

Expected: renderer tests pass.

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

- [ ] **Step 3: Commit, merge, close #563**

Commit renderer/tests, merge to `main`, push, and close issue #563 with verification evidence.
