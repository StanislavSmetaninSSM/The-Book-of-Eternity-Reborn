# Web UI Browser Command Renderer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render `ExplorerCommandResult` DTOs in the local browser shell.

**Architecture:** Keep rendering in the browser shell as a thin vanilla HTML/CSS/JS adapter over `/api/explorer/command`. The renderer understands DTO block kinds and never duplicates command/game logic.

**Tech Stack:** ASP.NET Core minimal API static HTML response, vanilla JavaScript, xUnit endpoint tests.

---

### Task 1: Browser Shell Renderer Tests

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [x] Add tests proving `/` contains the command form, `renderCommandResult`, block renderers, and error/empty-state text.
- [x] Add tests proving the shell references `POST /api/explorer/command`.

### Task 2: Browser Shell Renderer

**Files:**
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [x] Replace placeholder shell body with command input and quick `/help` button.
- [x] Add JS fetch wrapper for `/api/explorer/command`.
- [x] Add renderers for `text`, `panel`, `table`, `list`, `keyValueGrid`, `message`, `rawJson`, actions, prompts, errors, and empty states.

### Task 3: Documentation And Verification

**Files:**
- Modify: `docs/web-ui/local-web-host.md`

- [x] Document the current browser renderer scope and limitations.
- [x] Run targeted tests, build, full tests, then merge and close #567.
