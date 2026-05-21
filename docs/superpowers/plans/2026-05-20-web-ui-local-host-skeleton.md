# Web UI Local Host Skeleton Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a local-only ASP.NET Core host skeleton for the future browser UI without changing default console startup.

**Architecture:** Introduce a small startup-options parser, a `LocalWebUiHost` builder, and a session status service. The console remains the default mode; `--web` starts a localhost-only web app that serves a basic HTML shell plus health/session JSON endpoints.

**Tech Stack:** C#/.NET 8, ASP.NET Core minimal APIs via `Microsoft.AspNetCore.App`, xUnit.

---

### Task 1: Startup Option Parsing

**Files:**
- Create: `BookOfEternityClient/Configuration/ClientStartupOptions.cs`
- Test: `BookOfEternityClient.Tests/ClientStartupOptionsTests.cs`

- [ ] Add tests for legacy base-path argument, `--web`, `--web-url`, and default URL.
- [ ] Implement a parser that preserves console mode unless `--web` is present.

### Task 2: Local Web Host Skeleton

**Files:**
- Modify: `BookOfEternityClient/BookOfEternityClient.csproj`
- Create: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`
- Create: `BookOfEternityClient/WebUi/LocalWebUiSessionStatusService.cs`
- Test: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] Add tests proving non-loopback URLs are rejected.
- [ ] Add tests proving `/api/health` returns local-only session metadata.
- [ ] Add tests proving `/` returns the browser shell HTML.
- [ ] Add the ASP.NET Core framework reference and implement minimal API endpoints.

### Task 3: Program Integration And Docs

**Files:**
- Modify: `BookOfEternityClient/Program.cs`
- Create: `docs/web-ui/local-web-host.md`

- [ ] Wire `--web` to run `LocalWebUiHost` instead of the console `GameEngine`.
- [ ] Document the local-only model, default URL, base-path argument, and current skeleton limitations.
- [ ] Verify targeted tests, build, full tests, then merge and close #565.

