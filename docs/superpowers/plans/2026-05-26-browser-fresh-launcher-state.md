# Browser Fresh Launcher / No-Session State Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close #741 by making a fresh empty Browser Client root render as onboarding/no-session, not repair/validation/waiting.

**Architecture:** C# remains authoritative for whether a playable chapter exists. `/api/game-screen` returns a typed 404 no-active-session JSON payload when lifecycle state lacks a readable soul/current chapter; React consumes that as its existing empty route state and adds small copy helpers so no-session hero/sidebar text stays player-facing.

**Tech Stack:** .NET 8, ASP.NET Core minimal APIs, xUnit, React 18, TypeScript, Vite.

---

### Task 1: Add failing host regression for empty `/api/game-screen`

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] **Step 1: Write the failing test**

Insert this test after `HealthEndpoint_ReturnsLocalSessionStatus`:

```csharp
    [Fact]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task GameScreenEndpoint_ReturnsNoActiveSessionForFreshEmptyRoot()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), CreateHostOptions(url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        using var response = await client.GetAsync("/api/game-screen");
        var body = await response.Content.ReadAsStringAsync();
        var root = JsonNode.Parse(body)!.AsObject();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("game_session", root["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("актив", root["error"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("soul_state.json", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("валидац", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("repair", body, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 2: Run test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "GameScreenEndpoint_ReturnsNoActiveSessionForFreshEmptyRoot"
```

Expected: FAIL because `/api/game-screen` currently returns `200 OK` with an active game-screen DTO for the empty host root.

- [ ] **Step 3: Commit?**

Do not commit yet; Task 2 supplies the minimal production code for this failing test.

### Task 2: Gate `/api/game-screen` behind playable session detection

**Files:**
- Modify: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`
- Test: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] **Step 1: Implement the no-active-session exception and guard**

In `BrowserGameScreenService.BuildAsync()`, after building `lifecycle` and before building QTE/narrative/media, add:

```csharp
        if (!HasPlayableSession(lifecycle))
        {
            throw new BrowserNoActiveSessionException(
                "game_session пока не содержит активную главу. Начните новую главу или загрузите сохранение из главного меню.");
        }
```

Add this helper inside `BrowserGameScreenService`:

```csharp
    private static bool HasPlayableSession(BrowserLifecycleDashboardDto lifecycle) =>
        lifecycle.Session.GameSessionExists && lifecycle.Soul.IsReadable;
```

Add this record/class near the DTO declarations in the same file:

```csharp
public sealed class BrowserNoActiveSessionException : InvalidOperationException
{
    public BrowserNoActiveSessionException(string message) : base(message)
    {
    }
}
```

- [ ] **Step 2: Map the exception to HTTP 404**

Replace the `/api/game-screen` map in `LocalWebUiHost.cs` with:

```csharp
        app.MapGet("/api/game-screen", async (BrowserGameScreenService gameScreen) =>
        {
            try
            {
                return Results.Json(await gameScreen.BuildAsync(), WebJsonOptions);
            }
            catch (BrowserNoActiveSessionException ex)
            {
                return Results.Json(new { error = ex.Message }, WebJsonOptions, statusCode: StatusCodes.Status404NotFound);
            }
        });
```

- [ ] **Step 3: Run focused test to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "GameScreenEndpoint_ReturnsNoActiveSessionForFreshEmptyRoot|MainMenuEndpoint_ReturnsSessionActionsAndBrowserFriendlyDisabledStates"
```

Expected: PASS. The first test confirms empty root is no-active-session; the existing main menu test confirms playable session behavior still works.

- [ ] **Step 4: Run browser smoke/parity API tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiSmoke|Category=BrowserWebUiParity"
```

Expected: PASS.

### Task 3: Add failing React source guard for neutral no-session hero/sidebar copy

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Extend source guard test**

In `BrowserSidebar_RendersPlayerFacingStatusInsteadOfDebugDashboard`, replace the direct fixed-title/copy expectations with checks for helper-driven no-session copy:

```csharp
        Assert.Contains("function getTurnSidebarTitle(", app, StringComparison.Ordinal);
        Assert.Contains("Ход ещё не начат", app, StringComparison.Ordinal);
        Assert.Contains("<StatusSummaryCard title={getTurnSidebarTitle(hasGame, sidebarGameFailure)}", app, StringComparison.Ordinal);
        Assert.DoesNotContain("<StatusSummaryCard title=\"Ожидание ГМа\"", app, StringComparison.Ordinal);
        Assert.Contains("formatHeroStatusLabel(gameScreen, menu)", app, StringComparison.Ordinal);
        Assert.DoesNotContain("menu?.session.validationLabel ?? 'Книга ждёт открытия'", app, StringComparison.Ordinal);
        Assert.Contains("formatSidebarLayerStatus(menu)", app, StringComparison.Ordinal);
        Assert.DoesNotContain("toPlayerFacingText(menu.session.validationLabel, 'Книга ждёт открытия')", app, StringComparison.Ordinal);
```

Keep the existing general assertions for `Сводка книги`, `Слой книги`, `Герой и душа`, `Сохранение`, and advanced mode.

- [ ] **Step 2: Run test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserSidebar_RendersPlayerFacingStatusInsteadOfDebugDashboard"
```

Expected: FAIL because `getTurnSidebarTitle`, `formatHeroStatusLabel`, and `formatSidebarLayerStatus` do not exist yet, and the sidebar still has a fixed `Ожидание ГМа` title.

### Task 4: Implement neutral no-session React copy helpers

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Test: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Replace hero status fallback**

Replace:

```tsx
            <span>{gameScreen ? formatTurnStateTitle(gameScreen.turnState) : menu?.session.validationLabel ?? 'Книга ждёт открытия'}</span>
```

with:

```tsx
            <span>{formatHeroStatusLabel(gameScreen, menu)}</span>
```

- [ ] **Step 2: Add helper functions near sidebar helpers**

Add these functions near `getSidebarEmptyGameMessage`:

```tsx
function formatHeroStatusLabel(gameScreen: BrowserGameScreenDto | null, menu: BrowserMainMenuDto | null): string {
  if (gameScreen) {
    return formatTurnStateTitle(gameScreen.turnState);
  }

  if (menu && !menu.session.canContinue) {
    return 'Глава ещё не открыта';
  }

  return 'Книга ждёт открытия';
}

function formatSidebarLayerStatus(menu: BrowserMainMenuDto | null): string {
  if (!menu) {
    return 'Книга ждёт открытия.';
  }

  if (!menu.session.canContinue) {
    return 'Откройте новую главу или загрузите сохранение, чтобы увидеть состояние мира.';
  }

  return toPlayerFacingText(menu.session.validationLabel, 'Книга ждёт открытия');
}

function getTurnSidebarTitle(hasGame: boolean, sidebarGameFailure: string | null): string {
  if (!hasGame && !sidebarGameFailure) {
    return 'Ход ещё не начат';
  }

  return 'Ожидание ГМа';
}
```

- [ ] **Step 3: Use helper in the layer card**

Replace:

```tsx
          <p className="muted">{menu ? toPlayerFacingText(menu.session.validationLabel, 'Книга ждёт открытия') : 'Книга ждёт открытия.'}</p>
```

with:

```tsx
          <p className="muted">{formatSidebarLayerStatus(menu)}</p>
```

- [ ] **Step 4: Treat empty auto-created game_session as no active save**

At the top of `formatSidebarSessionSummary`, before `if (session?.gameSessionExists)`, add:

```tsx
  if (menu && !menu.session.canContinue && !menu.session.hasReadableSoul) {
    return 'Активной главы пока нет — начните новую или загрузите сохранение.';
  }
```

- [ ] **Step 5: Use helper for turn card title**

Replace:

```tsx
      <StatusSummaryCard title="Ожидание ГМа" eyebrow="ход" attention={turnNeedsAttention}>
```

with:

```tsx
      <StatusSummaryCard title={getTurnSidebarTitle(hasGame, sidebarGameFailure)} eyebrow="ход" attention={turnNeedsAttention}>
```

- [ ] **Step 6: Run focused frontend source guard**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserSidebar_RendersPlayerFacingStatusInsteadOfDebugDashboard|BrowserDefaultScreen_UsesPlayerFacingCopyAndNeutralEmptyStates|BrowserHomeRoute_RendersPlayerFacingLauncherWithPrimaryCta"
```

Expected: PASS.

### Task 5: Add fresh-empty first-screen visual smoke coverage

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`

- [ ] **Step 1: Extend visual artifact assertions**

In `BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics`, after the existing first-screen visual QA artifact route/copy assertions, add:

```csharp
        Assert.Contains("data-state=\"fresh-empty\"", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Активной главы пока нет", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("Ожидание ГМа", firstScreenVisualQaArtifact, StringComparison.Ordinal);
```

- [ ] **Step 2: Run test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics"
```

Expected: FAIL because the dependency-light visual QA artifact does not yet mark a fresh-empty state and still contains `Ожидание ГМа` in the status rail.

- [ ] **Step 3: Update visual QA artifact markup**

In `BuildFirstScreenVisualQaArtifact`, change the desktop frame opening tag to:

```html
<section class="frame" data-viewport="desktop" data-state="fresh-empty" aria-label="Desktop first-screen visual QA">
```

Replace the status rail summary with:

```html
<p class="muted">Слой книги · Герой и душа · Сохранение · Активной главы пока нет.</p>
```

- [ ] **Step 4: Run test to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics"
```

Expected: PASS.

### Task 6: Verify, review, PR, and close #741

**Files:**
- Create: `docs/superpowers/specs/2026-05-26-browser-fresh-launcher-state-design.md`
- Create: `docs/superpowers/plans/2026-05-26-browser-fresh-launcher-state.md`
- Modify: code/tests from prior tasks

- [ ] **Step 1: Run frontend verification**

Run:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS.

- [ ] **Step 2: Run browser-focused .NET tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|BrowserFrontend|BrowserApi|BrowserWebUiSmoke|BrowserWebUiParity"
```

Expected: PASS.

- [ ] **Step 3: Run broader test project**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Run whitespace and static scan**

Run:

```bash
git diff --check
git diff origin/main...HEAD -- . ':(exclude)docs/superpowers/plans/*.md' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

Expected: no whitespace errors and `NO_MATCHES`.

- [ ] **Step 5: Independent review**

Dispatch a spec reviewer and code-quality reviewer with the issue acceptance criteria, diff, and verification output. Fix Critical/Important findings and re-review.

- [ ] **Step 6: Commit, PR, CI, merge**

Stage only intentional files:

```bash
git add BookOfEternityClient/WebUi/BrowserGameScreenService.cs BookOfEternityClient/WebUi/LocalWebUiHost.cs BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.Tests/LocalWebUiHostTests.cs BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs docs/superpowers/specs/2026-05-26-browser-fresh-launcher-state-design.md docs/superpowers/plans/2026-05-26-browser-fresh-launcher-state.md
git commit -m "fix(web-ui): make fresh browser launcher neutral"
git push -u origin HEAD
gh pr create --title "fix(web-ui): make fresh browser launcher neutral" --body-file .hermes/tmp/pr-741.md
gh pr checks --watch --interval 10
gh pr merge --squash --delete-branch
```

Expected: PR merges green, #741 closes via `Closes #741`.
