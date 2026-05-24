# Issue 682 Browser Game Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close #682 by making the default browser UI show a player-facing game screen, backed by a richer read-only game-screen DTO and a primary prose action composer instead of a central slash-command terminal.

**Architecture:** Keep gameplay/application logic in the existing C# client layer. `BrowserGameScreenService` remains a read-only DTO aggregator over `StateManager`, output files, lifecycle status, validation, and QTE state; the root HTML renders that DTO as the default game surface. The raw slash-command palette stays inside the explicit advanced panel.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, xUnit, `System.Text.Json`, local browser HTML/CSS/vanilla JS.

---

## Design Note

### Problem

Issue #682 says the browser should feel like the game after entry: narrative, soul/hero summary, realm, status, available choices, last GM reaction, QTE/waiting/repair states, and ordinary player prose input. The current root page is improved compared with the first technical shell, but `Continue` only shows an explanatory card and the rich command palette remains the first concrete play surface once the user wants to act.

### Constraints

- Tracked task: GitHub issue #682.
- Browser is a frontend/UI surface only; no separate game rules in JavaScript.
- Read-only game-screen rendering must not mutate `game_session`.
- Destructive or turn-writing browser work remains gated by the existing local write/pending-turn model; this issue will not bypass pending-turn snapshots or afterlife contracts.
- Advanced diagnostics and slash commands must stay opt-in.
- GM-facing afterlife runtime contracts are not changed by this slice.

### Approaches considered

1. **Full browser turn submission now.** Add `POST /api/player/action` that writes `input/turn_request.json` and pending-turn snapshot artifacts. Rejected for this issue because the console turn pipeline has substantial snapshot/rollback/progression/dice behavior that should be extracted deliberately in a separate lifecycle task, not copied partially.
2. **Player-facing game screen with read-only state and composer bridge.** Expand `GET /api/game-screen`, render the screen by default, and make ordinary prose the primary composer while clearly routing slash/technical use to advanced mode. Selected because it satisfies the visual/game-screen acceptance criteria without risking unsafe turn writes.
3. **Only add DTO fields, leave root UI unchanged.** Rejected because the issue specifically asks for an actual browser game screen and non-central slash palette.

### Selected approach

Implement approach 2. The browser game screen will show:

- realm-aware theme card (mortal / Chaos Sea / Shining Abode / pending handoff);
- soul/player/world summary;
- latest narrative response, combat log, dialogue options, and optionally GM thoughts behind a disclosure;
- lifecycle cards for ready/pending/error/repair using existing `LocalWebUiSessionStatusService`, `BrowserLifecycleDashboardService`, and `QteWebInteractionService` data;
- primary prose action composer in the default player surface. If the player types a slash command, the UI can intentionally open advanced mode and execute it. If the player types ordinary prose, this slice displays a player-facing queued-action explanation that the browser turn writer is a gated follow-up, rather than pretending to write a turn unsafely.

### Docs/prompts impact

No GM-facing afterlife or mortal mechanics contracts change. Update browser docs/checklist only to document the game screen/default composer boundary and that `GET /api/game-screen` is read-only.

### Test strategy

Use strict TDD for behavior changes:

1. Add failing host/DTO smoke tests that require `/api/game-screen` to expose dialogue options, GM thoughts, combat log, lifecycle, QTE, action composer metadata, and realm theme.
2. Add failing root HTML tests that require `id="game-screen"`, `id="player-action-composer"`, rendering functions, and that the default player area before `advanced-shell` does not include the command palette.
3. Implement minimal C# DTO/service and HTML/JS/CSS changes.
4. Update `docs/web-ui/local-web-host.md` and `docs/web-ui/browser-parity-checklist.md` tests/doc text.
5. Verify focused browser tests and then broader WebUI tests.

## File structure

- Modify `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`: enrich the read-only DTO and derive browser-facing game state from shared state/output/lifecycle/QTE services.
- Modify `BookOfEternityClient/WebUi/QteWebInteractionService.cs`: expose a read-only QTE state builder so `/api/game-screen` does not normalize or delete runtime files.
- Modify `BookOfEternityClient/Core/StateManager.cs`: expose current turn number from story history to the shared aggregated state.
- Modify `BookOfEternityClient/WebUi/LocalWebUiHost.cs`: add default game-screen HTML/CSS/JS rendering and keep advanced command tools opt-in.
- Modify `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`: add endpoint/root assertions for the game screen, composer, read-only QTE rendering, turn-state distinctions, validation repair gating, and turn-number aggregation.
- Modify `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`: require docs to mention the player-facing game screen/composer/read-only boundary.
- Modify `docs/web-ui/local-web-host.md`: document game-screen rendering and read-only boundaries.
- Modify `docs/web-ui/browser-parity-checklist.md`: add manual smoke checklist items for game screen and composer.

### Task 1: Add failing tests for the richer game-screen DTO

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs`

- [ ] **Step 1: Add a focused failing DTO test**

Add a test near existing browser endpoint tests:

```csharp
[Fact]
[Trait("Category", "BrowserWebUiSmoke")]
public async Task GameScreenEndpoint_ReturnsNarrativeChoicesLifecycleQteAndActionComposer()
{
    WriteSessionFile("game_state/meta/soul_state.json", """
    {
      "soulName": "Экранная душа",
      "currentRealm": "Shining Abode",
      "currentIncarnation": 3,
      "inkFeathers": { "current": 9 },
      "enlightenment": { "currentTier": "Сияющий знак" }
    }
    """);
    WriteSessionFile("game_state/meta/shining_abode_state.json", """
    {
      "availability": "active",
      "radiance": { "experience": 120, "tier": 2 },
      "lightSparks": 4,
      "halls": [{ "hallId": "hall_dawn" }],
      "factions": [{ "factionId": "faction_scribes" }]
    }
    """);
    WriteSessionFile("game_state/world/current_location.json", """
    { "name": "Зал рассветных чернил" }
    """);
    WriteSessionFile("output/narrative_response.json", """
    { "response": "Сияние ложится на страницы." }
    """);
    WriteSessionFile("output/interface_updates.json", """
    {
      "dialogueOptions": [
        { "text": "Спросить хранителя о Вратах", "category": "диалог" },
        { "text": "Осмотреть зал", "category": "исследование" }
      ]
    }
    """);
    WriteSessionFile("output/debug_logs.json", """
    { "gm_thoughts_markdown": "GM видит скрытый конфликт фракций." }
    """);
    WriteSessionFile("game_state/combat/combat_log.json", """
    { "combat_log_markdown": "Последний духовный обмен завершён." }
    """);
    WriteSessionFile("input/turn_request.json", "{}",
        createParentDirectory: true);

    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    var root = JsonNode.Parse(await client.GetStringAsync("/api/game-screen"))!.AsObject();

    Assert.Equal(2, root["schemaVersion"]!.GetValue<int>());
    Assert.Equal("shining-abode", root["theme"]!["key"]!.GetValue<string>());
    Assert.Equal("✨", root["theme"]!["icon"]!.GetValue<string>());
    Assert.Equal("Экранная душа", root["soul"]!["name"]!.GetValue<string>());
    Assert.Equal("Зал рассветных чернил", root["world"]!["location"]!.GetValue<string>());
    Assert.Contains("Сияние", root["narrative"]!["text"]!.GetValue<string>(), StringComparison.Ordinal);
    Assert.Contains("духовный", root["narrative"]!["combatLog"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    Assert.Equal(2, root["narrative"]!["dialogueOptions"]!.AsArray().Count);
    Assert.Contains("скрытый конфликт", root["narrative"]!["gmThoughts"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    Assert.False(root["actionComposer"]!["canSubmit"]!.GetValue<bool>());
    Assert.Contains("Ожидает", root["turnState"]!["title"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    Assert.Equal("NoScene", root["qte"]!["state"]!.GetValue<string>());
}
```

- [ ] **Step 2: Run the focused test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter GameScreenEndpoint_ReturnsNarrativeChoicesLifecycleQteAndActionComposer
```

Expected: FAIL because the existing DTO has schemaVersion 1 and lacks `theme`, rich narrative, `actionComposer`, `turnState`, and `qte`.

### Task 2: Add failing tests for the default player-facing root game screen

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs`

- [ ] **Step 1: Add root HTML assertions**

Add a test near `RootEndpoint_ReturnsPlayerFacingBrowserMainMenu`:

```csharp
[Fact]
[Trait("Category", "BrowserWebUiSmoke")]
public async Task RootEndpoint_DefaultPlayerAreaContainsGameScreenAndPrimaryActionComposer()
{
    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    var html = await client.GetStringAsync("/");
    var advancedIndex = html.IndexOf("<section id=\"advanced-shell\"", StringComparison.Ordinal);
    Assert.True(advancedIndex > 0, "Advanced shell must follow default player game content.");
    var playerDefault = html[..advancedIndex];

    Assert.Contains("id=\"game-screen\"", playerDefault, StringComparison.Ordinal);
    Assert.Contains("id=\"player-action-composer\"", playerDefault, StringComparison.Ordinal);
    Assert.Contains("name=\"player-action\"", playerDefault, StringComparison.Ordinal);
    Assert.Contains("renderGameScreen", html, StringComparison.Ordinal);
    Assert.Contains("loadGameScreen", html, StringComparison.Ordinal);
    Assert.Contains("submitPlayerAction", html, StringComparison.Ordinal);
    Assert.Contains("/api/game-screen", html, StringComparison.Ordinal);
    Assert.DoesNotContain("Командная палитра", playerDefault, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("id=\"command-form\"", playerDefault, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the focused test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter RootEndpoint_DefaultPlayerAreaContainsGameScreenAndPrimaryActionComposer
```

Expected: FAIL because the default player root does not yet include a dedicated game screen/composer.

### Task 3: Implement the richer read-only `BrowserGameScreenService`

**Files:**
- Modify: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`

- [ ] **Step 1: Inject shared services and output readers**

Update constructor dependencies to include `FileSystemManager`, `BrowserLifecycleDashboardService`, and `QteWebInteractionService`.

- [ ] **Step 2: Build DTO fields from existing shared state**

Implementation requirements:

```csharp
public async Task<BrowserGameScreenDto> BuildAsync()
{
    await _stateManager.RefreshGameStateAsync();
    var state = _stateManager.CurrentState;
    var lifecycle = await _lifecycle.BuildDashboardAsync();
    var qte = await _qte.BuildReadOnlyStateAsync();
    var narrative = await BuildNarrativeAsync(state);

    return new BrowserGameScreenDto(
        SchemaVersion: 2,
        Theme: BrowserGameScreenThemeDto.FromState(state),
        Soul: ...,
        Player: ...,
        World: ...,
        Narrative: narrative,
        Afterlife: BrowserGameScreenAfterlifeDto.FromState(state),
        TurnState: BrowserGameScreenTurnStateDto.From(lifecycle, qte),
        ActionComposer: BrowserGameScreenActionComposerDto.From(lifecycle),
        Qte: qte,
        Flags: ...);
}
```

Use helper methods to read:

- `output/interface_updates.json.dialogueOptions` into `BrowserGameScreenDialogueOptionDto[]`.
- `output/interface_updates.json.image_prompt` into `Narrative.ImagePrompt`.
- `output/debug_logs.json.gm_thoughts_markdown` into `Narrative.GmThoughts`.
- `game_state/combat/combat_log.json.combat_log_markdown` into `Narrative.CombatLog`.

Do not write files.

- [ ] **Step 3: Run DTO focused test to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter GameScreenEndpoint_ReturnsNarrativeChoicesLifecycleQteAndActionComposer
```

Expected: PASS.

### Task 4: Render the default browser game screen and composer

**Files:**
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Add default HTML section before advanced shell**

Add a `<section id="game-screen" class="card game-screen" aria-live="polite">` and a `<form id="player-action-composer">` before `advanced-shell`. Keep `advanced-shell` hidden.

- [ ] **Step 2: Add CSS for game screen layout**

Add styles for `game-screen`, `game-hero`, `game-summary-grid`, `status-bars`, `dialogue-options`, `turn-state`, `theme-*`, and composer textarea/button.

- [ ] **Step 3: Add JS loading/rendering**

Add:

```javascript
const gameScreenRoot = document.getElementById('game-screen');
const playerActionComposer = document.getElementById('player-action-composer');
const playerActionInput = document.querySelector('[name="player-action"]');
loadGameScreen();
playerActionComposer.addEventListener('submit', submitPlayerAction);
```

`loadGameScreen()` fetches `/api/game-screen` and calls `renderGameScreen(payload)`. `renderGameScreen` renders the theme, summaries, narrative, dialogue options, lifecycle/QTE state, GM thoughts disclosure, and composer enabled/disabled text. `submitPlayerAction` treats leading slash input as an explicit advanced command handoff; ordinary prose shows a player-facing pending implementation card and does not write a turn request in this slice.

- [ ] **Step 4: Make Continue load the game screen, not a generic placeholder**

Change the `continue` menu action to scroll the game screen into view and refresh it instead of opening advanced diagnostics.

- [ ] **Step 5: Run root focused test to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter RootEndpoint_DefaultPlayerAreaContainsGameScreenAndPrimaryActionComposer
```

Expected: PASS.

### Task 5: Update browser docs/checklist and docs tests

**Files:**
- Modify: `docs/web-ui/local-web-host.md`
- Modify: `docs/web-ui/browser-parity-checklist.md`
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`

- [ ] **Step 1: Add documentation assertions**

Update docs test to require text for:

```csharp
Assert.Contains("player-facing game screen", hostDoc, StringComparison.OrdinalIgnoreCase);
Assert.Contains("primary prose action composer", hostDoc, StringComparison.OrdinalIgnoreCase);
Assert.Contains("read-only game-screen", hostDoc, StringComparison.OrdinalIgnoreCase);
Assert.Contains("primary prose action composer", checklist, StringComparison.OrdinalIgnoreCase);
```

- [ ] **Step 2: Run docs test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter LocalWebHostDocs_DocumentBrowserSmokeAndGameScreenState
```

Expected: FAIL until docs are updated.

- [ ] **Step 3: Update docs**

Document that the root page shows a player-facing game screen and primary prose action composer, while turn-writing remains behind future safe browser turn pipeline work; the read-only screen itself does not mutate files.

- [ ] **Step 4: Run docs test to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter LocalWebHostDocs_DocumentBrowserSmokeAndGameScreenState
```

Expected: PASS.

### Task 6: Focused and broader verification

**Files:**
- No new files beyond the files above.

- [ ] **Step 1: Run focused browser smoke tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|Category=BrowserWebUiParity"
```

Expected: PASS.

- [ ] **Step 2: Run broader WebUI-related tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|ExplorerWeb|CommandMigration"
```

Expected: PASS.

- [ ] **Step 3: Run whitespace check**

Run:

```bash
git diff --check
```

Expected: no output and exit 0.

## Self-review

- Spec coverage: the plan covers endpoint state, default browser game screen, primary prose composer boundary, lifecycle/QTE/repair states, realm themes, docs, and tests.
- Placeholder scan: no TBD/TODO/fill-in placeholders remain.
- Type consistency: DTO names are consistently `BrowserGameScreen*Dto`; root functions are consistently `loadGameScreen`, `renderGameScreen`, and `submitPlayerAction`.
- Scope check: full safe browser turn submission is explicitly out of this issue because copying the console pending-turn snapshot pipeline would be unsafe. The visual game screen and primary composer are a coherent closure unit for #682; future lifecycle/turn-writing work remains in the browser-client issue series.


## Independent review fix addendum

After independent review, the implementation scope was tightened with additional TDD regression tests and fixes:

- `/api/game-screen` uses `QteWebInteractionService.BuildReadOnlyStateAsync()` so rendering does not normalize, delete, or rewrite `game_state/control/qte_runtime.json`.
- `BrowserGameScreenTurnStateDto` distinguishes `pending-gm-turn`, `ready-gm-response`, `gm-turn-error`, `pending-turn-repair`, `validation-errors`, `qte`, `ready`, and `blocked` instead of collapsing all pending artifacts into one waiting state.
- `BrowserGameScreenActionComposerDto` disables ordinary prose submission with mode `repair-required` when validation has errors.
- `StateManager.RefreshGameStateAsync()` now fills `AggregatedGameState.TurnNumber` from story history `.jsonl` and `.json` files so the browser game screen can show the real maximum turn number written by `StoryService`.
- `LocalWebUiMainMenuService` now reuses `StateManager.CurrentState.TurnNumber`, so the main menu summary and game screen report the same JSONL-derived turn number.
- Added focused regression tests for all four review findings before applying the fixes.

A second independent code-quality review found two player/advanced boundary gaps, fixed with additional RED/GREEN regressions:

- `/api/game-screen` no longer exposes `output/debug_logs.json.gm_thoughts_markdown` in the default DTO; GM/debug notes remain in the explicit Advanced / developer command surface.
- The default prose composer no longer auto-executes slash commands. Slash input shows a player-facing explanation and can prefill the Advanced command field, but execution requires a separate deliberate advanced-mode action.
