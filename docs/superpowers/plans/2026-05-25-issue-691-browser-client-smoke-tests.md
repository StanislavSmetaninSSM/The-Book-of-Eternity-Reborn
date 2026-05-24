# Issue 691 Browser Client Smoke Tests Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close #691 by giving the browser client a focused local smoke/parity test suite, a read-only game-screen state API, and a guard that prevents new Explorer commands from silently skipping browser UX decisions.

**Architecture:** Keep the browser frontend a local presentation shell over the existing C# client state and command services. Add one read-only `/api/game-screen` DTO service that refreshes `StateManager.CurrentState` and exposes player-screen summary data without writing `game_session`; existing root/menu/session/lifecycle/command endpoints remain unchanged. Tests live in xUnit with `Category=BrowserWebUiSmoke` / `Category=BrowserWebUiParity` traits so CI and agents can run the browser-specific contract set directly.

**Tech Stack:** .NET 8, ASP.NET Core minimal host, xUnit, `HttpClient`, `System.Text.Json.Nodes`.

---

## Design note

### Problem
Issue #691 asks for regression protection around the full browser client: root/menu smoke, session and lifecycle DTOs, game-screen state, command parity, Russian player-facing text guards, and a lightweight menu-to-command/form flow. Existing `LocalWebUiHostTests` cover many individual endpoints, but there is no focused browser smoke suite, no read-only game-screen endpoint, and `ExplorerCommandCatalog.D(...)` still has a default browser status that lets future commands accidentally claim browser parity without an explicit decision.

### Goal
Add a minimal, locally runnable browser contract set that proves a fresh host can serve the player-facing root, main menu, session status, game-screen state, lifecycle dashboard, command DTO, and prompt/form flow. Make the browser parity decision explicit at command-definition time. Document the endpoint and the focused test command.

### Constraints
- Tracked task is #691; do not implement unrelated UI redesign work.
- Browser/web client remains frontend/presentation only; gameplay logic stays in the shared C# client layer.
- `/api/game-screen` is read-only and must not mutate `game_session` or bypass local write coordination.
- No afterlife runtime contract, GM-facing prompt, or mortal-world mechanic changes are intended.
- Tests must run offline against a temp local session root.

### Approaches considered
1. **Docs-only closure.** Low risk, but does not satisfy the smoke/parity acceptance criteria.
2. **Only add tests around existing endpoints.** Good regression coverage, but leaves the requested game-screen state DTO/API gap unresolved.
3. **Add a focused smoke suite, minimal read-only game-screen DTO, and explicit command parity guard.** Best match for #691: small production surface, high regression value, and no separate gameplay logic.

Selected approach: option 3.

### Docs/prompts impact
Browser docs only: `docs/web-ui/local-web-host.md` and `docs/web-ui/browser-parity-checklist.md`. No GM-facing prompts or afterlife/mortal runtime contract docs need changes because the new endpoint is read-only state presentation and command metadata only becomes more explicit.

### Test strategy
Use RED/GREEN for behavior-changing pieces:
- RED: add `LocalWebUiSmokeTests` expecting `/api/game-screen` and run it before the endpoint exists.
- RED: add a registry guard asserting `ExplorerCommandCatalog.D(...)` has no default browser status.
- RED: add documentation guard for the focused `Category=BrowserWebUiSmoke|Category=BrowserWebUiParity` command and `/api/game-screen` docs.
- GREEN: add `BrowserGameScreenService`, map `/api/game-screen`, make command catalog statuses explicit, and update docs.
- Verify with the focused browser categories and broader `WebUi|LocalWebUi|ExplorerWeb|CommandMigration` filters.

## File structure

- Create: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
  - Builds `BrowserGameScreenDto` from `StateManager.CurrentState` after `RefreshGameStateAsync()`.
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`
  - Registers `BrowserGameScreenService` and maps `GET /api/game-screen`.
- Create: `BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs`
  - Browser smoke category tests for root/menu/session/game-screen/lifecycle/command and prompt submission flow.
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`
  - Adds a browser parity category guard that fails if the command catalog helper has a default browser status.
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`
  - Adds docs guard for smoke/parity command and game-screen endpoint documentation.
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`
  - Removes the default browser status from `D(...)` and `ExplorerCommandSubcommandDescriptor`; supplies explicit `browserStatus:` / `BrowserStatus:` at every descriptor call.
- Modify: `docs/web-ui/local-web-host.md`
  - Documents `/api/game-screen` and focused smoke/parity verification.
- Modify: `docs/web-ui/browser-parity-checklist.md`
  - Records the automated smoke/parity test categories.

### Task 1: Add RED tests

**Files:**
- Create: `BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs`
- Modify: `BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`

- [ ] **Step 1: Add `LocalWebUiSmokeTests`**

Create a test class with `[Trait("Category", "BrowserWebUiSmoke")]` methods:

```csharp
[Fact]
[Trait("Category", "BrowserWebUiSmoke")]
public async Task BrowserWebUiSmoke_CoversRootMenuSessionGameScreenLifecycleAndCommandFlow()
{
    WriteSessionFile("game_state/meta/soul_state.json", """
    { "soulName": "Дымовая душа", "currentRealm": "Chaos Sea", "currentIncarnation": 7,
      "inkFeathers": { "current": 11 }, "enlightenment": { "currentTier": "Тлеющий знак" } }
    """);
    WriteSessionFile("game_state/world/current_location.json", """{ "name": "Причал между мирами" }""");
    WriteSessionFile("output/narrative_response.json", """{ "response": "Туман расступается перед книгой." }""");

    using var client = await StartClientAsync();
    var rootHtml = await client.GetStringAsync("/");
    var menu = JsonNode.Parse(await client.GetStringAsync("/api/main-menu"))!.AsObject();
    var session = JsonNode.Parse(await client.GetStringAsync("/api/session"))!.AsObject();
    var screen = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();
    var lifecycle = JsonNode.Parse(await client.GetStringAsync("/api/lifecycle/dashboard"))!.AsObject();
    using var commandResponse = await client.PostAsJsonAsync("/api/explorer/command", new { command = "/status" });
    var command = JsonNode.Parse(await commandResponse.Content.ReadAsStringAsync())!.AsObject();

    Assert.Contains("id=\"main-menu\"", rootHtml, StringComparison.Ordinal);
    Assert.Equal("Дымовая душа", menu["session"]!["soulName"]!.GetValue<string>());
    Assert.True(session["localOnly"]!.GetValue<bool>());
    Assert.Equal("Дымовая душа", screen["soul"]!["name"]!.GetValue<string>());
    Assert.Equal("Причал между мирами", screen["world"]!["location"]!.GetValue<string>());
    Assert.Contains("Туман", screen["narrative"]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
    Assert.Equal(1, lifecycle["schemaVersion"]!.GetValue<int>());
    commandResponse.EnsureSuccessStatusCode();
    Assert.Equal("Completed", command["state"]!.GetValue<string>());
}
```

Add a second smoke test for the existing browser form path using `/world_setup` and `/api/explorer/prompt-sessions/submit`; assert `Completed` and the pending setup file exists.

- [ ] **Step 2: Add command parity guard test**

Add this test to `ExplorerCommandMigrationRegistryTests`:

```csharp
[Fact]
[Trait("Category", "BrowserWebUiParity")]
public void CommandCatalog_RequiresExplicitBrowserStatusForEveryDescriptor()
{
    var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "CommandProtocol", "ExplorerCommandCatalog.cs"));

    Assert.DoesNotContain("browserStatus = ExplorerCommandMigrationStatus.ReadOnlyParity", source, StringComparison.Ordinal);
    Assert.DoesNotContain("ExplorerCommandMigrationStatus browserStatus =", source, StringComparison.Ordinal);
}
```

- [ ] **Step 3: Add docs guard test**

Add this test to `LocalWebUiDocumentationTests`:

```csharp
[Fact]
[Trait("Category", "BrowserWebUiSmoke")]
public void LocalWebHostDocs_DocumentBrowserSmokeAndGameScreenState()
{
    var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
    var checklist = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "browser-parity-checklist.md"));

    Assert.Contains("GET /api/game-screen", hostDoc, StringComparison.Ordinal);
    Assert.Contains("Category=BrowserWebUiSmoke|Category=BrowserWebUiParity", hostDoc, StringComparison.Ordinal);
    Assert.Contains("game-screen state", hostDoc, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("BrowserWebUiSmoke", checklist, StringComparison.Ordinal);
    Assert.Contains("BrowserWebUiParity", checklist, StringComparison.Ordinal);
}
```

- [ ] **Step 4: Run RED command**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserWebUiSmoke|BrowserWebUiParity|CommandCatalog_RequiresExplicitBrowserStatusForEveryDescriptor|LocalWebHostDocs_DocumentBrowserSmokeAndGameScreenState"
```

Expected: FAIL because `/api/game-screen` is not mapped, docs do not mention the new smoke command, and command catalog still has a default browser status.

### Task 2: Implement read-only game-screen DTO endpoint

**Files:**
- Create: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Create service and DTO records**

```csharp
using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserGameScreenService
{
    private readonly StateManager _stateManager;

    public BrowserGameScreenService(StateManager stateManager)
    {
        _stateManager = stateManager;
    }

    public async Task<BrowserGameScreenDto> BuildAsync()
    {
        await _stateManager.RefreshGameStateAsync();
        var state = _stateManager.CurrentState;

        return new BrowserGameScreenDto(
            SchemaVersion: 1,
            Soul: new BrowserGameScreenSoulDto(state.SoulName, state.CurrentRealm, state.Incarnation, state.InkFeathers, state.EnlightenmentTier, state.ActiveGuardianName),
            Player: new BrowserGameScreenPlayerDto(state.CharacterName, state.CharacterClass, state.CharacterRace, state.PlayerStatus.CurrentCondition, state.PlayerStatus.HealthPercentage, state.PlayerStatus.EnergyPercentage, state.PlayerStatus.PoisePercentage, state.PlayerStatus.ActiveConditions),
            World: new BrowserGameScreenWorldDto(state.CurrentLocation, state.WorldTime, state.TurnNumber, state.SessionId),
            Narrative: new BrowserGameScreenNarrativeDto(state.Narrative),
            Flags: new BrowserGameScreenFlagsDto(state.IsInChaosSea, state.IsInAnyShiningAbodeState, state.IsInShiningAbode, state.IsInShiningAbodePendingBootstrap, state.IsInAfterlifeRealm, state.CanReenterShiningAbode));
    }
}
```

- [ ] **Step 2: Register and map endpoint**

Add `builder.Services.AddSingleton<BrowserGameScreenService>();` after `LocalWebUiSessionStatusService` and map:

```csharp
app.MapGet("/api/game-screen", async (BrowserGameScreenService gameScreen) => await gameScreen.BuildAsync());
```

- [ ] **Step 3: Run focused GREEN smoke test**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter BrowserWebUiSmoke_CoversRootMenuSessionGameScreenLifecycleAndCommandFlow
```

Expected: PASS.

### Task 3: Make browser parity decisions explicit

**Files:**
- Modify: `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs`

- [ ] **Step 1: Remove defaults from helper and subcommand signatures**

Change:

```csharp
ExplorerCommandMigrationStatus browserStatus = ExplorerCommandMigrationStatus.ReadOnlyParity,
```

to:

```csharp
ExplorerCommandMigrationStatus browserStatus,
```

Also change `ExplorerCommandSubcommandDescriptor` so `BrowserStatus` has no default value:

```csharp
ExplorerCommandMigrationStatus BrowserStatus,
```

- [ ] **Step 2: Add explicit `browserStatus:` to descriptor calls**

Every `D(...)` call must now include one of:

```csharp
browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity
browserStatus: ExplorerCommandMigrationStatus.MutatingParity
browserStatus: ExplorerCommandMigrationStatus.StatusOnly
browserStatus: ExplorerCommandMigrationStatus.InteractiveFormPending
browserStatus: ExplorerCommandMigrationStatus.Planned
browserStatus: ExplorerCommandMigrationStatus.Blocked
browserStatus: ExplorerCommandMigrationStatus.ConsoleOnlyTemporarily
```

Existing calls that already specify `browserStatus: ExplorerCommandMigrationStatus.MutatingParity` stay as-is. Calls that previously relied on the default get `browserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity` because current tests already establish those commands have read-only browser parity. Existing subcommands that previously relied on the default get `BrowserStatus: ExplorerCommandMigrationStatus.ReadOnlyParity`.

- [ ] **Step 3: Run parity guard**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserWebUiParity|ExplorerCommandMigrationRegistryTests"
```

Expected: PASS.

### Task 4: Document endpoint and focused verification command

**Files:**
- Modify: `docs/web-ui/local-web-host.md`
- Modify: `docs/web-ui/browser-parity-checklist.md`

- [ ] **Step 1: Add `/api/game-screen` to endpoint list and description**

Add `GET /api/game-screen` to the Current Browser MVP endpoint list. Add a paragraph saying it is a read-only game-screen state DTO for browser rendering: soul/player/world/narrative/realm flags, refreshed from `StateManager`, no write path.

- [ ] **Step 2: Add smoke/parity verification command**

Add this command to local-web-host docs:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|Category=BrowserWebUiParity"
```

Use the slash-form in final reports on MSYS:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|Category=BrowserWebUiParity"
```

- [ ] **Step 3: Update checklist**

Add an automated guard bullet under Shell And Navigation saying `BrowserWebUiSmoke` covers root/menu/session/game-screen/lifecycle/command/form flow and `BrowserWebUiParity` forces explicit command browser UX decisions.

- [ ] **Step 4: Run docs guard**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter LocalWebHostDocs_DocumentBrowserSmokeAndGameScreenState
```

Expected: PASS.

### Task 5: Verify, review, PR, and merge

**Files:** all intentional files from tasks 1-4.

- [ ] **Step 1: Run focused browser verification**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|Category=BrowserWebUiParity"
```

Expected: PASS.

- [ ] **Step 2: Run broader browser verification**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|ExplorerWeb|CommandMigration"
```

Expected: PASS.

- [ ] **Step 3: Independent review**

Dispatch an independent reviewer with issue #691 body, this plan, and `git diff -- BookOfEternityClient BookOfEternityClient.Tests docs/web-ui docs/superpowers/plans/2026-05-25-issue-691-browser-client-smoke-tests.md`. Fix important findings and rerun focused verification.

- [ ] **Step 4: Commit and PR**

```bash
git add BookOfEternityClient/WebUi/BrowserGameScreenService.cs BookOfEternityClient/WebUi/LocalWebUiHost.cs BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs BookOfEternityClient.Tests/ExplorerCommandMigrationRegistryTests.cs BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs docs/web-ui/local-web-host.md docs/web-ui/browser-parity-checklist.md docs/superpowers/plans/2026-05-25-issue-691-browser-client-smoke-tests.md
git commit -m "test(web-ui): add browser client smoke guards"
```

Push, create PR with `Closes #691`, wait for checks, squash-merge after local verification and CI are green, then confirm #691 is closed.

## Self-review

- Spec coverage: smoke root/menu/session/game-screen/lifecycle/command/form flow, command parity guard, Russian/default UI text guard via root smoke, offline local tests, docs command.
- Placeholder scan: no TODO/TBD markers; every command and file path is explicit.
- Scope check: one read-only endpoint plus tests/docs; no unrelated browser redesign or game mechanics change.
- Type consistency: endpoint path `/api/game-screen`, category names `BrowserWebUiSmoke` and `BrowserWebUiParity`, DTO record names match implementation task.
