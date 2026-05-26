# Browser Settings, Profile, and Locality Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issue #689 by making the Browser Client settings route read/write player-safe shared `GameSettings`, local status, GM thoughts, audio, and accessibility preferences.

**Architecture:** Add a C# `BrowserClientSettingsService` over `StateManager.Settings` and expose `/api/client/settings` GET/POST from the loopback-only local web host. Update TypeScript contracts/client/fixtures and render a player-facing React settings screen that applies accessibility state without moving game logic into React.

**Tech Stack:** .NET 8 minimal APIs, xUnit, React 19, TypeScript, Vite, shared `GameSettings` JSON persistence.

---

### Task 1: Add failing backend settings endpoint tests

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] **Step 1: Write the failing tests**

Add two xUnit tests near the existing audio settings endpoint tests:

```csharp
[Fact]
[Trait("Category", "BrowserWebUiSmoke")]
public async Task ClientSettingsEndpoint_LoadsPlayerSafeSharedSettingsAndLocality()
{
    WriteSessionFile("config.json", """
    {
      "language": "en",
      "difficulty": "hard",
      "showGmThoughts": true,
      "musicEnabled": false,
      "musicVolume": 27,
      "soundEnabled": true,
      "soundVolume": 81,
      "browserFontScalePercent": 115,
      "browserReducedMotion": true,
      "browserContrastFriendly": true,
      "gmBridgeEnabled": false,
      "openRouterApiKey": "secret-token-not-for-browser",
      "gmCliLaunchCommand": "secret-shell-command"
    }
    """);
    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    var root = JsonNode.Parse(await client.GetStringAsync("/api/client/settings"))!.AsObject();

    Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
    Assert.Equal("en", root["language"]!["value"]!.GetValue<string>());
    Assert.Equal("hard", root["difficulty"]!["value"]!.GetValue<string>());
    Assert.True(root["showGmThoughts"]!.GetValue<bool>());
    Assert.False(root["audio"]!["musicEnabled"]!.GetValue<bool>());
    Assert.Equal(27, root["audio"]!["musicVolume"]!.GetValue<int>());
    Assert.Equal(115, root["accessibility"]!["fontScalePercent"]!.GetValue<int>());
    Assert.True(root["accessibility"]!["reducedMotion"]!.GetValue<bool>());
    Assert.True(root["accessibility"]!["contrastFriendly"]!.GetValue<bool>());
    Assert.True(root["locality"]!["localhostOnly"]!.GetValue<bool>());
    Assert.Contains("game_session", root["locality"]!["sessionLabel"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    Assert.False(root["locality"]!["gmBridgeEnabled"]!.GetValue<bool>());
    Assert.DoesNotContain(_rootPath, root.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("secret-token-not-for-browser", root.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("secret-shell-command", root.ToJsonString(), StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task ClientSettingsEndpoint_UpdatesWhitelistedSettingsAndWritesGmProjection()
{
    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    using var response = await client.PostAsJsonAsync("/api/client/settings", new
    {
        language = "en",
        difficulty = "impossible",
        showGmThoughts = true,
        musicEnabled = false,
        musicVolume = 150,
        soundEnabled = false,
        soundVolume = -20,
        browserFontScalePercent = 175,
        browserReducedMotion = true,
        browserContrastFriendly = true
    });
    var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    var config = JsonNode.Parse(File.ReadAllText(Path.Combine(_rootPath, "game_session", "config.json")))!.AsObject();
    var gmProjection = JsonNode.Parse(File.ReadAllText(Path.Combine(_rootPath, "game_session", "game_state", "core", "game_settings.json")))!.AsObject();

    response.EnsureSuccessStatusCode();
    Assert.Equal("en", root["language"]!["value"]!.GetValue<string>());
    Assert.Equal("impossible", root["difficulty"]!["value"]!.GetValue<string>());
    Assert.True(root["showGmThoughts"]!.GetValue<bool>());
    Assert.Equal(100, root["audio"]!["musicVolume"]!.GetValue<int>());
    Assert.Equal(0, root["audio"]!["soundVolume"]!.GetValue<int>());
    Assert.Equal(140, root["accessibility"]!["fontScalePercent"]!.GetValue<int>());
    Assert.True(config["showGmThoughts"]!.GetValue<bool>());
    Assert.Equal("impossible", config["difficulty"]!.GetValue<string>());
    Assert.Equal(140, config["browserFontScalePercent"]!.GetValue<int>());
    Assert.True(gmProjection["impossibleMode"]!.GetValue<bool>());
    Assert.Equal("impossible", gmProjection["difficulty"]!.GetValue<string>());
}
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "ClientSettingsEndpoint" --logger "console;verbosity=minimal"
```

Expected: FAIL because `/api/client/settings` and DTOs are missing.

### Task 2: Implement C# browser client settings service and host endpoints

**Files:**
- Modify: `BookOfEternityClient/Configuration/GameSettings.cs`
- Create: `BookOfEternityClient/WebUi/BrowserClientSettingsService.cs`
- Modify: `BookOfEternityClient/WebUi/BrowserAudioService.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Add minimal implementation**

Add `BrowserFontScalePercent`, `BrowserReducedMotion`, and `BrowserContrastFriendly` to `GameSettings`; normalize them in `ApplyLoadedValues`.

Create `BrowserClientSettingsService` with GET/POST DTOs, using the shared browser settings write gate from `BrowserAudioService`, updating only whitelisted fields, clamping values, saving config, applying audio, and writing `game_state/core/game_settings.json`.

Register the service and map:

```csharp
app.MapGet("/api/client/settings", async (BrowserClientSettingsService settings) => await settings.BuildAsync());
app.MapPost("/api/client/settings", async (BrowserClientSettingsUpdateRequest request, BrowserClientSettingsService settings) => await settings.UpdateAsync(request));
```

- [ ] **Step 2: Run backend tests to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "ClientSettingsEndpoint|AudioSettingsEndpoint" --logger "console;verbosity=minimal"
```

Expected: PASS.

### Task 3: Add TypeScript contracts, API client, and fixtures

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/api/client.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contract-fixture-checks.ts`
- Create: `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/client-settings.json`
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`

- [ ] **Step 1: Write failing contract guard updates**

Update `BrowserApiContractTests` to require `BrowserClientSettingsDto`, `BrowserClientSettingsUpdateRequest`, `getClientSettings`, `updateClientSettings`, and a `client-settings.json` fixture built from representative C# DTO records.

- [ ] **Step 2: Run contract tests to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Expected: FAIL because TypeScript contracts/client/fixture are missing.

- [ ] **Step 3: Implement contracts and fixture**

Add TypeScript interfaces matching the C# DTOs, client endpoint docs for `/api/client/settings`, client methods, fixture import/satisfies check, and JSON fixture.

- [ ] **Step 4: Run contract tests and frontend typecheck**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserApiContractTests" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS.

### Task 4: Render the player-facing settings route and accessibility state

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/layout.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/motion.css`
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Write failing source guard**

Add a `BrowserSettingsRoute_RendersSharedGameSettingsAndLocalityControls` test asserting:

- `BrowserShellState` includes `settings: BrowserApiResult<BrowserClientSettingsDto>`.
- `loadBrowserState` calls `browserApi.getClientSettings()`.
- `SettingsRoute` renders language/difficulty/GM thoughts/audio/accessibility/locality copy.
- `browserApi.updateClientSettings` is called from the settings route.
- Default settings route does not contain dangerous strings: `OpenRouterApiKey`, `PollinationsApiKey`, `GmCliLaunchCommand`, `GmBridgePipeNameOverride`.
- CSS includes `.browser-shell.is-reduced-motion` and `.browser-shell.is-contrast-friendly`.

- [ ] **Step 2: Run source guard to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserSettingsRoute_RendersSharedGameSettingsAndLocalityControls" --logger "console;verbosity=minimal"
```

Expected: FAIL.

- [ ] **Step 3: Implement React route and styles**

Add settings result to loaded state, apply `browser-shell` classes and CSS custom font scale from settings data, render grouped settings cards with controlled inputs, and post partial updates through `browserApi.updateClientSettings`.

- [ ] **Step 4: Run guard and npm verification**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserSettingsRoute_RendersSharedGameSettingsAndLocalityControls|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS.

### Task 5: Documentation, review, and final verification

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Document issue #689 settings workflow**

Add concise notes that browser settings read/write shared `GameSettings`, dangerous technical settings remain advanced-only, and accessibility settings are frontend presentation over C# settings authority.

- [ ] **Step 2: Run final local verification**

Run:

```bash
git diff --check
git diff origin/main...HEAD -- . ':(exclude)docs/superpowers/plans/*.md' ':(exclude)docs/superpowers/specs/*.md' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "ClientSettingsEndpoint|AudioSettingsEndpoint|BrowserApiContractTests|BrowserFrontendWorkspaceTests|Category=BrowserWebUiSmoke|Category=BrowserWebUiParity" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: all commands exit 0; static scan prints `NO_MATCHES` or only documented plan/spec false positives.

- [ ] **Step 3: Independent review**

Dispatch a reviewer with issue #689 criteria, the diff, and verification output. Fix Critical/Important findings and re-run focused verification.

- [ ] **Step 4: Commit, PR, CI, merge**

Commit only tracked issue files (exclude scratch dirs), push branch, open PR with `Closes #689`, wait for green checks, squash-merge to `main`, and verify issue #689 is closed.
