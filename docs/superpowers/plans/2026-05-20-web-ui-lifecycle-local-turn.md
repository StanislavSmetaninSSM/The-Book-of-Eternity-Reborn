# Web UI Lifecycle And Local-Turn Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close GitHub issue #574 by moving lifecycle/local-turn Explorer commands out of browser hard-blocks into explicit UI-neutral protocol DTOs.

**Architecture:** Add one focused browser-safe result builder for local-turn/lifecycle commands. It must not call console-bound prompt handlers; it returns structured state, prompts, action hints, current pending/ready/rollback artifacts, and runs `/validate` through `ValidationService`. Real multi-step prompt submission is deliberately kept for #575, but #574 commands become observable and safely representable in the browser instead of returning "blocked".

**Tech Stack:** C#/.NET, existing `ExplorerCommandResult` DTOs, xUnit, local ASP.NET web host command service.

---

### Task 1: Add RED Coverage For #574 Commands

**Files:**
- Modify: `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`

- [ ] **Step 1: Write failing web command tests**

Add tests proving `/validate`, `/world_setup`, `/spiritual_action`, `/abode_offering`, `/found_guardian_mantle`, `/distribute`, `/companion_directive`, `/faction_directive`, and `/craft` no longer return `Blocked`, and that active `input/turn_request.json` plus `pending_turn_snapshot.json` is shown as an active GM-turn state.

- [ ] **Step 2: Write failing registry tests**

Update registry assertions so `#574` commands must be marked `Migrated`, not `Blocked`.

- [ ] **Step 3: Run the targeted tests and verify RED**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExplorerWebCommandServiceTests|ExplorerCommandMigrationRegistryTests"
```

Expected: failures because the commands still return `Blocked` and registry entries still point at `#574`.

### Task 2: Implement Browser-Safe Lifecycle/Local-Turn DTO Builder

**Files:**
- Create: `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Add builder**

Create a static builder with `TryBuildAsync(command, stateManager, fs, validationService)`. It should:

- Execute `/validate` through `ValidationService.ValidateGameStateAsync()` and render grouped issues.
- Render `/world_setup` as current pending setup plus prompts for title, character, start, rules, profile mode, and clear/edit intent.
- Render `/spiritual_action` as active conflict status, active conflict raw JSON, and a long-text prompt for the player's spiritual action.
- Render `/abode_offering` as known guardians/offering options, pending request raw JSON, and selection/text prompts for offering target and type.
- Render `/found_guardian_mantle` as pending/completed/available foundation state plus required text prompts.
- Render `/distribute` as unspent stat points, current characteristic table, and text prompt for allocation JSON.
- Render `/companion_directive` and `/faction_directive` as target lists with text prompts.
- Render `/craft` as known recipe status and a protocol note that actual craft submission is a local-turn write flow.
- Add a shared local-turn status panel showing `input/turn_request.json`, `ready/turn_complete.json`, `ready/turn_error.json`, `game_state/control/pending_turn_snapshot.json`, non-empty `pending_turn_snapshot/`, and `game_state/control/explorer_local_turn_rollback/`.

- [ ] **Step 2: Wire builder into web service**

Inject `ValidationService` into `ExplorerWebCommandService` and call the new builder after the existing read-only builders.

- [ ] **Step 3: Register validation service in web host**

Add `ValidationService` to `LocalWebUiHost` DI so `/validate` can run in browser mode.

### Task 3: Mark #574 Commands Migrated

**Files:**
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandMigrationRegistry.cs`

- [ ] **Step 1: Move lifecycle/local-turn commands to migrated arrays**

Mark the #574 command groups as `Migrated` because they now have browser-safe protocol DTOs:

- `/validate`, `/валидация`, `/world_setup`, `/настройка_мира`
- `/distribute`, `/распределить`, `/companion_directive`, `/директива_компаньону`, `/faction_directive`, `/директива_фракции`, `/craft`, `/ремесло`
- `/abode_offering`, `/подношение_обители`, `/found_guardian_mantle`, `/учредить_хранителя`
- `/spiritual_action`, `/духовное_действие`

### Task 4: Update Web UI Documentation

**Files:**
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Add #574 to tracked tasks**

Update the tracked tasks list to include `#574`.

- [ ] **Step 2: Document lifecycle/local-turn protocol behavior**

Replace the old "blocked until #574" sections with the new browser-safe status: commands are migrated as protocol DTOs, show active GM-turn artifacts, and do not yet submit multi-step prompts until #575.

### Task 5: Verify, Commit, Merge, Close

**Files:**
- All changed files

- [ ] **Step 1: Run targeted tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExplorerWebCommandServiceTests|ExplorerCommandMigrationRegistryTests"
```

- [ ] **Step 2: Run build**

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore
```

- [ ] **Step 3: Run full test suite**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

- [ ] **Step 4: Commit, merge into main, push, close #574**

Use a normal feature branch commit, merge to `main`, push, add a GitHub issue comment with verification evidence, then close the issue.
