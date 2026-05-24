# Issue 681 Browser Main Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the browser client root page open a player-facing Russian main menu with session status and browser DTO save/load flow instead of the technical Local Web UI shell.

**Architecture:** Add a small `LocalWebUiMainMenuService` that composes existing session/lifecycle/status services and `SaveLoadService`. Keep gameplay/application logic in the C# client layer; the browser root consumes DTOs and uses existing command execution only as a frontend action for already-migrated flows such as `/world_setup`.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, xUnit, inline HTML/CSS/JS in `LocalWebUiHost.cs`.

---

## File structure

- Create: `BookOfEternityClient/WebUi/LocalWebUiMainMenuService.cs` — DTOs and safe menu/save-load orchestration.
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs` — DI registration, `/api/main-menu`, `/api/saves/load`, root HTML/CSS/JS presentation.
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs` — failing tests for root main menu, main-menu DTO, and safe save loading.
- Create: `docs/superpowers/specs/2026-05-24-issue-681-browser-main-menu-design.md` — autonomous design note.
- Create: `docs/superpowers/plans/2026-05-24-issue-681-browser-main-menu.md` — this plan.

## Tasks

### Task 1: Main-menu API tests

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] **Step 1: Write failing root-page test**

Add assertions that `/` contains `id="main-menu"`, `data-menu-action="continue"`, `data-menu-action="new-game"`, `data-menu-action="load"`, `data-menu-action="options"`, `data-menu-action="about"`, `data-menu-action="exit"`, and `id="advanced-shell-toggle"`; assert the page does not contain `Local Web UI`.

- [ ] **Step 2: Write failing DTO test**

Create a session with `game_state/meta/soul_state.json` and a story entry, call `GET /api/main-menu`, and assert `schemaVersion == 1`, `session.canContinue == true`, Russian realm label is present, `actions` contains Continue/New Game/Load/Options/About/Exit, and save actions are disabled with Russian reasons when no saves exist.

- [ ] **Step 3: Write failing save-load DTO test**

Use `SaveLoadService` to create a manual save, mutate `soul_state.json`, call `GET /api/main-menu` to obtain a safe `saveId`, then `POST /api/saves/load` with that ID and assert the response succeeds and the original saved soul is restored. Also assert an unknown `saveId` returns `400` with a Russian error.

- [ ] **Step 4: Run targeted tests for RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"
```

Expected: FAIL because `/api/main-menu`, `/api/saves/load`, and new root landmarks do not exist yet.

### Task 2: Main-menu service and endpoints

**Files:**
- Create: `BookOfEternityClient/WebUi/LocalWebUiMainMenuService.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Register services**

Add `SaveLoadService` and `LocalWebUiMainMenuService` to Web UI DI.

- [ ] **Step 2: Add endpoint mappings**

Map `GET /api/main-menu` to `LocalWebUiMainMenuService.BuildAsync()` and `POST /api/saves/load` to `LocalWebUiMainMenuService.LoadSaveAsync(request)`.

- [ ] **Step 3: Implement DTOs**

Define immutable records for menu DTO, session summary, menu actions, save slots, options summary, load request, and load result.

- [ ] **Step 4: Implement safe save IDs**

Enumerate `SaveLoadService.GetAvailableSavesAsync()`, expose IDs as `manual:<file name>`, and resolve load requests only by comparing against the enumerated save list.

- [ ] **Step 5: Run targeted tests for GREEN**

Run the LocalWebUiHostTests command from Task 1. Expected: root tests may still fail until Task 3; API/save DTO tests should pass.

### Task 3: Player-facing root menu

**Files:**
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Update CSS/markup**

Make the first visible section `id="main-menu"` with a Reborn title, menu action cards, session status card, save list panel, options/about panels, and a collapsed advanced shell section.

- [ ] **Step 2: Update JavaScript**

Fetch `/api/main-menu` on load, render action enabled/disabled states and save slots, wire Continue to reveal the existing game shell, New Game to execute `/world_setup`, Load to call `/api/saves/load`, Options/About to show informational panels, Exit to show local-session shutdown guidance, and the advanced toggle to reveal the command palette.

- [ ] **Step 3: Keep existing command shell behavior**

Do not remove existing command execution, prompt session, lifecycle dashboard, QTE, media, or map rendering functions.

- [ ] **Step 4: Run targeted tests for GREEN**

Run the LocalWebUiHostTests command. Expected: PASS.

### Task 4: Verification, review, PR, and closure

**Files:**
- All intentional files above.

- [ ] **Step 1: Run focused tests**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 2: Run broader project tests**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: PASS or document any pre-existing unrelated failure with evidence.

- [ ] **Step 3: Independent review**

Dispatch a fresh reviewer against issue #681, the design/plan, and `git diff`. Fix Critical/Important findings and rerun tests.

- [ ] **Step 4: Commit and PR**

Commit intentional files only with a conventional message, push `task/681-browser-main-menu`, create a PR with `Closes #681`, verification evidence, docs/prompts impact, and design-reference note.

- [ ] **Step 5: CI and merge**

Check PR CI. If checks pass or no checks are configured, squash-merge and delete branch. Verify issue #681 is closed and sync local `main`.

## Self-review

All issue acceptance criteria map to tasks above. No placeholders/TBD remain. DTO and method names are consistent across the plan. The work is scoped to Browser Client UI/API; no GM-facing afterlife contract documentation update is needed unless implementation unexpectedly changes runtime contracts.