# Browser Media Map QTE Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make issue #688's Browser Client Media route a player-facing section for gallery images, realm map/atlas, and QTE interaction without exposing raw endpoints as the primary UI.

**Architecture:** Add a read-only `media` DTO to C# `/api/game-screen` using `LocalMediaService` and `LocalMapViewService`, mirror it in TypeScript contracts/fixtures, then render the route through React panels. QTE actions continue through existing typed browser API methods; gameplay rules stay in the C# runtime.

**Tech Stack:** .NET 8, xUnit, ASP.NET Core Minimal APIs, React, TypeScript, Vite, CSS modules via existing `src/styles/*.css` imports.

---

### Task 1: Add media DTO contract and failing tests

**Objective:** Prove `/api/game-screen` must carry safe media/gallery/map data before production code changes.

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`
- Modify later: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- Modify later: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Modify later: `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`

- [ ] **Step 1: Write failing C# contract test**

Add this test near the existing `BrowserGameScreenContract_IncludesPlayerCommandActionMenu` test:

```csharp
[Fact]
public void BrowserGameScreenContract_IncludesPlayerFacingMediaMapAndGalleryData()
{
    var screen = BuildGameScreen();
    var json = JsonSerializer.Serialize(screen, WebJsonOptions);

    Assert.NotNull(screen.Media);
    Assert.Equal("mystic road at dusk", screen.Media.SceneImagePrompt);
    var item = Assert.Single(screen.Media.Gallery);
    Assert.Equal("scene-road.png", item.FileName);
    Assert.StartsWith("/api/media/", item.Url, StringComparison.Ordinal);
    Assert.DoesNotContain("E:/", item.Url, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("game_session", item.Url, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("relativePath", json, StringComparison.OrdinalIgnoreCase);
    Assert.NotNull(screen.Media.Map);
    Assert.Equal("Карта смертного мира", screen.Media.Map.Title);
    Assert.Contains(screen.Media.Map.Layers, layer => layer.Id == "world" && layer.IsDefault);
    Assert.Contains(screen.Media.Map.ZLevels, level => level.Z == 0);
    Assert.Contains(screen.Media.Map.Nodes, node => node.IsCurrent);
    Assert.Contains("\"media\"", json, StringComparison.Ordinal);
    Assert.True(
        json.IndexOf("\"media\"", StringComparison.Ordinal) < json.IndexOf("\"qte\"", StringComparison.Ordinal),
        "BrowserGameScreenDto must serialize media before qte so frontend fixtures evolve predictably.");
}
```

Update `BuildGameScreen()` with the desired constructor argument so the test compiles only after production DTOs are added:

```csharp
Media: BuildMedia(),
```

Add helper methods in the same file after `BuildGameScreen()`:

```csharp
private static BrowserGameScreenMediaDto BuildMedia() =>
    new(
        SchemaVersion: 1,
        SceneImagePrompt: "mystic road at dusk",
        Gallery:
        [
            new BrowserGameScreenMediaItemDto(
                MediaId: "images-scenes-scene-road",
                Url: "/api/media/images-scenes-scene-road",
                FileName: "scene-road.png",
                ContentType: "image/png",
                Length: 2048,
                ModifiedAtUtc: SampleUtc)
        ],
        Map: new MapViewDto
        {
            Realm = "Mortal World",
            Title = "Карта смертного мира",
            CurrentNodeId = "ash-road",
            Layers = [new MapLayerDto { Id = "world", Label = "Мир", IsDefault = true }],
            ZLevels = [new MapZLevelDto { Z = 0, Label = "земля" }],
            Nodes =
            [
                new MapNodeDto
                {
                    Id = "ash-road",
                    Label = "Пепельная дорога",
                    Type = "current",
                    X = 0,
                    Y = 0,
                    Z = 0,
                    Layer = "world",
                    IsCurrent = true,
                    OwnerFactionId = "",
                    OwnerFactionName = "",
                    Influence = new Dictionary<string, int>(),
                    Details = [new MapDetailItemDto { Key = "Время", Value = "Сумерки" }]
                }
            ],
            Links = [],
            Regions = []
        });
```

- [ ] **Step 2: Run test and verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserGameScreenContract_IncludesPlayerFacingMediaMapAndGalleryData" --logger "console;verbosity=minimal"
```

Expected: FAIL to compile because `BrowserGameScreenDto.Media`, `BrowserGameScreenMediaDto`, and `BrowserGameScreenMediaItemDto` do not exist yet.

- [ ] **Step 3: Commit after green implementation in Task 2**

Do not commit this task by itself while RED; commit after Task 2 passes.

### Task 2: Implement C# media DTOs and fixture alignment

**Objective:** Add read-only C# media/map data to `/api/game-screen` and keep frontend contracts/fixtures synchronized.

**Files:**
- Modify: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- Modify: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Modify: `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json`

- [ ] **Step 1: Add C# service dependency and DTO construction**

In `BrowserGameScreenService`, add `using BookOfEternityClient.CommandProtocol;` and `using BookOfEternityClient.Services;` if not already present. Inject `LocalMediaService` into the constructor and store it in `_media`.

In `BuildAsync()`, after `var narrative = await BuildNarrativeAsync(state);`, add:

```csharp
var media = await BuildMediaAsync(narrative);
```

Add `Media: media,` between `Narrative:` and `Afterlife:` in the `BrowserGameScreenDto` constructor.

Add helper methods:

```csharp
private async Task<BrowserGameScreenMediaDto> BuildMediaAsync(BrowserGameScreenNarrativeDto narrative)
{
    var map = await LocalMapViewService.BuildCurrentRealmMapAsync(_fs);
    var gallery = _media.EnumerateGallery(24)
        .Select(static item => new BrowserGameScreenMediaItemDto(
            MediaId: item.MediaId,
            Url: item.Url,
            FileName: item.FileName,
            ContentType: item.ContentType,
            Length: item.Length,
            ModifiedAtUtc: item.ModifiedAtUtc))
        .ToList();

    return new BrowserGameScreenMediaDto(
        SchemaVersion: 1,
        SceneImagePrompt: narrative.ImagePrompt,
        Gallery: gallery,
        Map: map);
}
```

- [ ] **Step 2: Add record types**

Change `BrowserGameScreenDto` to include `BrowserGameScreenMediaDto Media` between `Narrative` and `Afterlife`. Add these records after `BrowserGameScreenNarrativeDto`:

```csharp
public sealed record BrowserGameScreenMediaDto(
    int SchemaVersion,
    string SceneImagePrompt,
    IReadOnlyList<BrowserGameScreenMediaItemDto> Gallery,
    MapViewDto Map);

public sealed record BrowserGameScreenMediaItemDto(
    string MediaId,
    string Url,
    string FileName,
    string ContentType,
    long Length,
    DateTimeOffset ModifiedAtUtc);
```

- [ ] **Step 3: Update TypeScript contracts**

In `BookOfEternityClient.WebFrontend/src/api/contracts.ts`, add `media: BrowserGameScreenMediaDto;` between `narrative` and `afterlife` in `BrowserGameScreenDto`. Add interfaces:

```ts
export interface BrowserGameScreenMediaDto {
  schemaVersion: number;
  sceneImagePrompt: string;
  gallery: BrowserGameScreenMediaItemDto[];
  map: MapViewDto;
}

export interface BrowserGameScreenMediaItemDto {
  mediaId: string;
  url: string;
  fileName: string;
  contentType: string;
  length: number;
  modifiedAtUtc: IsoDateTimeString;
}
```

- [ ] **Step 4: Update fixture**

Regenerate or manually update `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json` so it includes `media` between `narrative` and `afterlife`, matching `BuildMedia()` exactly.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add BookOfEternityClient/WebUi/BrowserGameScreenService.cs BookOfEternityClient.Tests/BrowserApiContractTests.cs BookOfEternityClient.WebFrontend/src/api/contracts.ts BookOfEternityClient.WebFrontend/src/api/contract-fixtures/game-screen.json docs/superpowers/specs/2026-05-26-browser-media-map-qte-design.md docs/superpowers/plans/2026-05-26-browser-media-map-qte.md
git commit -m "feat(web-ui): add browser media game-screen contract"
```

### Task 3: Render player-facing Media route panels

**Objective:** Replace the placeholder Media route with gallery, atlas, and QTE panels over the typed media DTO.

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Write failing React source guard**

Add test `BrowserMediaRoute_RendersGalleryMapAndQteAsPlayerSections` to `BrowserFrontendWorkspaceTests.cs`:

```csharp
[Fact]
public void BrowserMediaRoute_RendersGalleryMapAndQteAsPlayerSections()
{
    var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
    var styles = ReadFrontendStyles();
    var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
    var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

    Assert.Contains("function QteScenePanel", app, StringComparison.Ordinal);
    Assert.Contains("function MediaGalleryPanel", app, StringComparison.Ordinal);
    Assert.Contains("function MediaAtlasPanel", app, StringComparison.Ordinal);
    Assert.Contains("browserApi.resolveQteOffer", app, StringComparison.Ordinal);
    Assert.Contains("browserApi.resolveQteAction", app, StringComparison.Ordinal);
    Assert.Contains("game.media.gallery", app, StringComparison.Ordinal);
    Assert.Contains("game.media.map", app, StringComparison.Ordinal);
    Assert.Contains("sceneImagePrompt", app, StringComparison.Ordinal);
    Assert.Contains("Политическое влияние", app, StringComparison.Ordinal);
    Assert.Contains("Выберите уровень", app, StringComparison.Ordinal);
    Assert.Contains("Открыть изображение", app, StringComparison.Ordinal);

    var mediaRouteStart = app.IndexOf("function MediaRoute", StringComparison.Ordinal);
    var settingsRouteStart = app.IndexOf("function SettingsRoute", StringComparison.Ordinal);
    Assert.True(mediaRouteStart >= 0 && settingsRouteStart > mediaRouteStart, "MediaRoute should remain before SettingsRoute.");
    var mediaRouteSource = app[mediaRouteStart..settingsRouteStart];
    Assert.DoesNotContain("/api/qte/state", mediaRouteSource, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("/api/media/", mediaRouteSource, StringComparison.OrdinalIgnoreCase);
    Assert.DoesNotContain("relativePath", mediaRouteSource, StringComparison.OrdinalIgnoreCase);

    Assert.Contains(".qte-scene-panel", styles, StringComparison.Ordinal);
    Assert.Contains(".media-gallery-grid", styles, StringComparison.Ordinal);
    Assert.Contains(".media-atlas-panel", styles, StringComparison.Ordinal);
    Assert.Contains("#688", readme, StringComparison.Ordinal);
    Assert.Contains("#688", hostDoc, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run test and verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserMediaRoute_RendersGalleryMapAndQteAsPlayerSections" --logger "console;verbosity=minimal"
```

Expected: FAIL because the panels and CSS classes do not exist yet.

- [ ] **Step 3: Implement React panels**

In `App.tsx`, replace `MediaRoute` with a route that renders:

```tsx
const game = state.game.data;
return (
  <ShellPanel title="Медиа" eyebrow="галерея, атлас и быстрые сцены">
    <div className="split-grid three media-section-grid">
      <QteScenePanel qte={game.qte} />
      <MediaGalleryPanel media={game.media} />
      <MediaAtlasPanel map={game.media.map} realmLabel={game.theme.label} />
    </div>
  </ShellPanel>
);
```

Add helper components below `MediaRoute`:

- `QteScenePanel` with local notice/result state, accept/decline buttons for `qte.offer`, action/grade buttons for `qte.activeScene.currentChapter.actions`, and completion/empty summaries.
- `MediaGalleryPanel` showing `media.sceneImagePrompt`, a safe gallery grid of `<a href={item.url} target="_blank" rel="noreferrer"><img src={item.url} alt={item.fileName} /></a>`, file names, content types, and sizes without relative/local paths.
- `MediaAtlasPanel` with local `selectedLayer`, `selectedZ`, `showPolitical` state, layer/z selectors, map node cards filtered by selected values, and a political influence list when enabled.

Use `toPlayerFacingText()` for QTE messages. Use existing typed DTO property names from `contracts.ts`.

- [ ] **Step 4: Add CSS**

In `BookOfEternityClient.WebFrontend/src/styles/components.css`, add classes for:

```css
.qte-scene-panel { ... }
.qte-action-list { ... }
.media-gallery-grid { ... }
.media-gallery-card { ... }
.media-atlas-panel { ... }
.media-atlas-controls { ... }
.media-atlas-node-grid { ... }
.media-atlas-node { ... }
```

Keep dark fantasy card styling consistent with existing `.summary-card`, `.detail-surface-grid`, and `.action-card` tokens.

- [ ] **Step 5: Update docs**

In `BookOfEternityClient.WebFrontend/README.md` and `docs/web-ui/local-web-host.md`, add a short #688 note: Media route consumes `/api/game-screen.media`, renders gallery/atlas/QTE as player-facing sections, and raw endpoint diagnostics remain advanced-only.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 7: Run frontend verification**

Run:

```bash
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
```

Expected: both PASS.

- [ ] **Step 8: Commit**

```bash
git add BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles/components.css BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md
git commit -m "feat(web-ui): render browser media game sections"
```

### Task 4: Final verification, review, PR, CI, merge

**Objective:** Verify #688 locally and in CI, get independent review, merge, and close.

**Files:**
- No planned production edits unless review or CI finds a defect.

- [ ] **Step 1: Run local verification**

Run:

```bash
git diff --check
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalMediaServiceTests|FullyQualifiedName~LocalMapViewerServiceTests|Qte" --logger "console;verbosity=minimal"
```

Expected: all PASS.

- [ ] **Step 2: Static added-line scan**

Run:

```bash
git diff main...HEAD | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

Expected: `NO_MATCHES`.

- [ ] **Step 3: Independent review**

Dispatch an independent reviewer with the issue criteria, plan, and `git diff main...HEAD`. Fix any Critical/Important findings and re-run the focused verification commands.

- [ ] **Step 4: Push and create PR**

Run:

```bash
git push -u origin HEAD
gh pr create --title "feat(web-ui): add browser media game sections" --body-file .hermes/tmp/pr-688.md
```

PR body must include `Closes #688`, summary, test plan, review result, and docs impact.

- [ ] **Step 5: Watch CI and merge**

Run:

```bash
gh pr checks --watch
gh pr merge --squash --delete-branch
```

Expected: checks PASS, PR merged to `main`, issue #688 closes.

- [ ] **Step 6: Update local main and verify closure**

Run:

```bash
git checkout main
git pull origin main
gh issue view 688 --json state,url,title
```

Expected: issue state is `CLOSED`.
