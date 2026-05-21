# Web UI Local Session Lock Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent two local UI owners from concurrently mutating the same `game_session`.

**Architecture:** Add a client-owned lease file under `game_state/control/local_ui_session_lock.json` with owner metadata and heartbeat. Expose a reusable service for console/browser owners, then gate known mutating ExplorerMode commands before their handlers run while preserving read-only commands.

**Tech Stack:** C#/.NET, xUnit, existing `FileSystemManager`, existing ExplorerMode command dictionaries.

---

### Task 1: Lock Service Contract

**Files:**
- Create: `BookOfEternityClient/Services/LocalUiSessionLockService.cs`
- Test: `BookOfEternityClient.Tests/LocalUiSessionLockServiceTests.cs`

- [ ] Add RED tests for fresh acquisition, same-owner heartbeat refresh, active other-owner rejection, and stale other-owner recovery.
- [ ] Implement `LocalUiSessionLockService` with exclusive file creation for first acquisition and atomic heartbeat refresh for same-owner locks.
- [ ] Treat malformed fresh locks as blockers, but recover malformed stale locks by file timestamp.

### Task 2: ExplorerMode Mutating Command Gate

**Files:**
- Modify: `BookOfEternityClient/UI/ExplorerMode.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs`
- Test: `BookOfEternityClient.Tests/ExplorerModeCommandTests.GeneralPanels.cs`

- [ ] Add RED tests proving `/help` works under another owner lock and `/spiritual_action` or another mutating command is blocked under another owner lock.
- [ ] Add optional lock owner parameters to `ExplorerMode` for tests and future browser host integration.
- [ ] Gate known mutating aliases through `LocalUiSessionLockService.AcquireOrRefreshAsync(...)` before executing handlers.
- [ ] Render a Russian blocker message and do not call the mutating handler if another owner has a live lease.

### Task 3: Verification And Merge

**Files:**
- Modify: `docs/superpowers/plans/2026-05-20-web-ui-local-session-lock.md`

- [ ] Run targeted lock and ExplorerMode tests.
- [ ] Run `dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore`.
- [ ] Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore`.
- [ ] Commit, merge to `main`, push, and close GitHub issue #568.
