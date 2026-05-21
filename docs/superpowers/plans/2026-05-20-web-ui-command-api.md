# Web UI Command API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Expose migrated ExplorerMode command results through a local HTTP API.

**Architecture:** Add a web command service that accepts command text, checks the migration registry, refreshes session state, and returns `ExplorerCommandResult`. The host maps `POST /api/explorer/command` and keeps non-migrated or blocked commands as structured blocked/failed DTOs instead of invoking console-bound handlers.

**Tech Stack:** C#/.NET 8, ASP.NET Core minimal APIs, existing command DTO protocol, xUnit.

---

### Task 1: Command Service Contract

**Files:**
- Create: `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`
- Test: `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`

- [ ] Add tests for migrated `/help`, blocked mutating `/spiritual_action`, planned non-migrated `/status`, and empty command.
- [ ] Implement service methods returning `ExplorerCommandResult` for all four cases.
- [ ] Refresh `StateManager` before building migrated read-only results.

### Task 2: HTTP Endpoint

**Files:**
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`
- Test: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] Add endpoint tests for `POST /api/explorer/command` returning DTO JSON for `/help`.
- [ ] Add endpoint tests for mutating command block response.
- [ ] Register `GameSettings`, `StateManager`, `LocalizationManager`, and `ExplorerWebCommandService`.

### Task 3: Documentation And Verification

**Files:**
- Modify: `docs/web-ui/local-web-host.md`

- [ ] Document the command endpoint request and current read-only/migrated command limitation.
- [ ] Run targeted tests, build, full tests, then merge and close #566.

