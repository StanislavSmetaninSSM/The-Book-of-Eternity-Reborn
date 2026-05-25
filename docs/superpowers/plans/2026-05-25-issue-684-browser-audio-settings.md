# Issue #684 Browser Audio Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add browser-tab music/sound controls backed by the shared C# `GameSettings` audio fields and safe local audio asset metadata.

**Architecture:** C# remains the settings, persistence, and asset-catalog authority through a new `BrowserAudioService`. React consumes typed DTOs, renders Russian player-facing controls, and starts audio only after an explicit user gesture to satisfy browser autoplay policy.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, xUnit, React 19, TypeScript strict mode, Vite.

---

## File structure

- Create `BookOfEternityClient/WebUi/BrowserAudioService.cs`: builds audio settings DTO, persists shared audio settings, resolves safe asset IDs, and returns file results.
- Modify `BookOfEternityClient/WebUi/LocalWebUiHost.cs`: register `BrowserAudioService`, add `GET /api/audio/settings`, `POST /api/audio/settings`, and `GET /api/audio/assets/{assetId}`.
- Modify `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`: add C# RED/GREEN coverage for audio settings and asset safety.
- Modify `BookOfEternityClient.Tests/BrowserApiContractTests.cs`: add TypeScript contract/fixture guard coverage for the audio DTO.
- Modify `BookOfEternityClient.WebFrontend/src/api/contracts.ts`: add `BrowserAudioSettingsDto`, playlists, cues, assets, and update request types.
- Modify `BookOfEternityClient.WebFrontend/src/api/client.ts`: add typed audio endpoints and endpoint docs.
- Create `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/audio-settings.json`: representative browser audio contract fixture.
- Modify `BookOfEternityClient.WebFrontend/src/api/contract-fixture-checks.ts`: type-check the fixture.
- Modify `BookOfEternityClient.WebFrontend/src/App.tsx`: load audio settings and render/update/play controls in `SettingsRoute`.
- Modify `BookOfEternityClient.WebFrontend/src/styles.css`: style audio controls, sliders, and unlock notices.
- Modify `BookOfEternityClient.WebFrontend/README.md` and `docs/web-ui/local-web-host.md`: document the browser audio/settings workflow and verification.

---

### Task 1: Add failing C# browser audio endpoint tests

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] **Step 1: Write the failing tests**

Add these tests near the other Local Web UI endpoint tests:

```csharp
[Fact]
[Trait("Category", "BrowserWebUiSmoke")]
public async Task AudioSettingsEndpoint_LoadsSharedSettingsAndReturnsSafeCatalog()
{
    WriteSessionFile("config.json", """
    {
      "musicEnabled": false,
      "musicVolume": 32,
      "soundEnabled": true,
      "soundVolume": 54
    }
    """);
    WriteRootFile("Music/Main Theme.mp3", "fake-mp3");
    WriteRootFile("Sounds/sound-notification.wav", "fake-wav");
    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    var root = JsonNode.Parse(await client.GetStringAsync("/api/audio/settings"))!.AsObject();

    Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
    Assert.False(root["musicEnabled"]!.GetValue<bool>());
    Assert.Equal(32, root["musicVolume"]!.GetValue<int>());
    Assert.True(root["soundEnabled"]!.GetValue<bool>());
    Assert.Equal(54, root["soundVolume"]!.GetValue<int>());
    Assert.Contains("браузер", root["autoplayGuidance"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain(_rootPath, root.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    var mainMenu = root["playlists"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == "main-menu")!.AsObject();
    Assert.True(mainMenu["available"]!.GetValue<bool>());
    Assert.StartsWith("/api/audio/assets/", mainMenu["tracks"]!.AsArray()[0]!["url"]!.GetValue<string>(), StringComparison.Ordinal);
    Assert.Contains(root["cues"]!.AsArray(), cue => cue?["id"]?.GetValue<string>() == "turn-ready" && cue["available"]!.GetValue<bool>());
}

[Fact]
public async Task AudioSettingsEndpoint_UpdatesAndPersistsSharedSettingsWithClampedVolumes()
{
    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    using var response = await client.PostAsJsonAsync("/api/audio/settings", new
    {
        musicEnabled = false,
        musicVolume = 125,
        soundEnabled = false,
        soundVolume = -10
    });
    var root = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
    var config = JsonNode.Parse(File.ReadAllText(Path.Combine(_rootPath, "game_session", "config.json")))!.AsObject();

    response.EnsureSuccessStatusCode();
    Assert.False(root["musicEnabled"]!.GetValue<bool>());
    Assert.Equal(100, root["musicVolume"]!.GetValue<int>());
    Assert.False(root["soundEnabled"]!.GetValue<bool>());
    Assert.Equal(0, root["soundVolume"]!.GetValue<int>());
    Assert.False(config["musicEnabled"]!.GetValue<bool>());
    Assert.Equal(100, config["musicVolume"]!.GetValue<int>());
    Assert.False(config["soundEnabled"]!.GetValue<bool>());
    Assert.Equal(0, config["soundVolume"]!.GetValue<int>());
}

[Fact]
public async Task AudioAssetEndpoint_ServesOnlyCataloguedAssetsWithoutPathTraversal()
{
    WriteRootFile("Music/Main Theme.mp3", "fake-mp3");
    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    var settings = JsonNode.Parse(await client.GetStringAsync("/api/audio/settings"))!.AsObject();
    var assetId = settings["playlists"]!.AsArray()
        .Single(node => node!["id"]!.GetValue<string>() == "main-menu")!["tracks"]!.AsArray()[0]!["id"]!.GetValue<string>();
    using var ok = await client.GetAsync($"/api/audio/assets/{Uri.EscapeDataString(assetId)}");
    using var traversal = await client.GetAsync("/api/audio/assets/..%2Fconfig.json");

    Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    Assert.Equal("audio/mpeg", ok.Content.Headers.ContentType?.MediaType);
    Assert.Equal(HttpStatusCode.NotFound, traversal.StatusCode);
}
```

Add the helper after `WriteSessionFile`:

```csharp
private void WriteRootFile(string relativePath, string content)
{
    var fullPath = Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
    File.WriteAllText(fullPath, content);
}
```

- [ ] **Step 2: Run RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~AudioSettingsEndpoint_LoadsSharedSettingsAndReturnsSafeCatalog|FullyQualifiedName~AudioSettingsEndpoint_UpdatesAndPersistsSharedSettingsWithClampedVolumes|FullyQualifiedName~AudioAssetEndpoint_ServesOnlyCataloguedAssetsWithoutPathTraversal" --logger "console;verbosity=minimal"
```

Expected: FAIL with 404 responses because `/api/audio/settings` and `/api/audio/assets/{assetId}` do not exist.

---

### Task 2: Implement C# browser audio service and host endpoints

**Files:**
- Create: `BookOfEternityClient/WebUi/BrowserAudioService.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Create the service and DTO records**

Implement `BrowserAudioService` with these responsibilities:

```csharp
public sealed class BrowserAudioService
{
    public Task<BrowserAudioSettingsDto> BuildSettingsAsync();
    public Task<BrowserAudioSettingsDto> UpdateSettingsAsync(BrowserAudioSettingsUpdateRequest request);
    public IResult ServeAsset(string assetId);
}

public sealed record BrowserAudioSettingsUpdateRequest(bool? MusicEnabled, int? MusicVolume, bool? SoundEnabled, int? SoundVolume);
```

The service must call `StateManager.LoadSettingsAsync()`, read/write only the four shared audio fields, clamp volumes, call `StateManager.SaveSettingsAsync()`, call `AudioService.ApplySettingsAsync()`, and build safe IDs such as `music:main-menu:Main Theme.mp3` and `cue:turn-ready:sound-notification.wav`.

- [ ] **Step 2: Add host endpoints**

Register the service:

```csharp
builder.Services.AddSingleton<BrowserAudioService>();
```

Map endpoints:

```csharp
app.MapGet("/api/audio/settings", async (BrowserAudioService audio) => await audio.BuildSettingsAsync());
app.MapPost("/api/audio/settings", async (BrowserAudioSettingsUpdateRequest request, BrowserAudioService audio) => await audio.UpdateSettingsAsync(request));
app.MapGet("/api/audio/assets/{assetId}", (string assetId, BrowserAudioService audio) => audio.ServeAsset(assetId));
```

- [ ] **Step 3: Run GREEN**

Run the Task 1 test command again. Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add BookOfEternityClient/WebUi/BrowserAudioService.cs BookOfEternityClient/WebUi/LocalWebUiHost.cs BookOfEternityClient.Tests/LocalWebUiHostTests.cs
git commit -m "feat(web-ui): expose browser audio settings and assets"
```

---

### Task 3: Add TypeScript audio API contract and frontend guards

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/api/client.ts`
- Create: `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/audio-settings.json`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contract-fixture-checks.ts`

- [ ] **Step 1: Write RED contract/source guard tests**

Extend `FrontendApiContractFiles_ArePresentAndDocumentEndpointMethods()` with assertions for `BrowserAudioSettingsDto`, `getAudioSettings`, `updateAudioSettings`, `audio-settings`, and `audio-settings-update`.

Extend `ContractFixtures()` with an `audio-settings.json` fixture built from a representative `BrowserAudioSettingsDto`.

Extend `TypeScriptFixtureChecks_ImportEveryContractFixtureWithSatisfiesTypes()` with `satisfies BrowserAudioSettingsDto`.

- [ ] **Step 2: Run RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Expected: FAIL because the TypeScript contract and fixture do not yet include audio settings.

- [ ] **Step 3: Implement TypeScript contract/client/fixture updates**

Add interfaces for `BrowserAudioSettingsDto`, `BrowserAudioPlaylistDto`, `BrowserAudioAssetDto`, `BrowserAudioCueDto`, and `BrowserAudioSettingsUpdateRequest`. Add typed client methods and endpoint docs for `GET /api/audio/settings`, `POST /api/audio/settings`, and `GET /api/audio/assets/{assetId}`.

- [ ] **Step 4: Run GREEN**

Run the Task 3 test command and `npm run typecheck --prefix BookOfEternityClient.WebFrontend`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient.Tests/BrowserApiContractTests.cs BookOfEternityClient.WebFrontend/src/api/contracts.ts BookOfEternityClient.WebFrontend/src/api/client.ts BookOfEternityClient.WebFrontend/src/api/contract-fixtures/audio-settings.json BookOfEternityClient.WebFrontend/src/api/contract-fixture-checks.ts
git commit -m "feat(web-ui): add typed browser audio API contract"
```

---

### Task 4: Render player-facing browser audio controls

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles.css`
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`

- [ ] **Step 1: Write RED source guard tests**

Add assertions that `App.tsx` contains `AudioSettingsPanel`, `Включить музыку в браузере`, `autoplayGuidance`, `browserApi.updateAudioSettings`, `new Audio()`, and does not call `.play()` inside `useEffect`.

- [ ] **Step 2: Run RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"
```

Expected: FAIL because the React shell does not yet render audio controls.

- [ ] **Step 3: Implement React audio settings panel**

Load audio settings in `loadBrowserState`, pass the result to `SettingsRoute`, render toggles/sliders, update settings through `browserApi.updateAudioSettings`, and play browser audio only from button handlers. Use Russian player-facing notices for missing assets and autoplay failures.

- [ ] **Step 4: Run GREEN**

Run the Task 4 test command, `npm run typecheck --prefix BookOfEternityClient.WebFrontend`, and `npm run build --prefix BookOfEternityClient.WebFrontend`. Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles.css BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs
git commit -m "feat(web-ui): render browser audio controls"
```

---

### Task 5: Document and verify issue #684

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Add docs**

Document the audio endpoints, shared settings persistence, autoplay unlock behavior, missing asset safety, and verification commands.

- [ ] **Step 2: Run verification**

Run:

```bash
git diff --check
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~AudioSettingsEndpoint|FullyQualifiedName~AudioAssetEndpoint|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|Category=BrowserWebUiSmoke|Category=BrowserWebUiParity" --logger "console;verbosity=minimal"
```

Expected: all commands exit 0.

- [ ] **Step 3: Commit**

```bash
git add BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md docs/superpowers/specs/2026-05-25-issue-684-browser-audio-settings-design.md docs/superpowers/plans/2026-05-25-issue-684-browser-audio-settings.md
git commit -m "docs(web-ui): document browser audio settings workflow"
```
