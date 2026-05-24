# Issue 690 Browser Advanced Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close #690 by proving and tightening the split between the default player-facing browser screen and the advanced/developer diagnostics panel.

**Architecture:** Keep the browser frontend as a local shell over the existing C# client and existing `/api/*` endpoints. The root HTML defaults to the player main menu; raw command console, lifecycle validation, QTE probes, command palette, raw JSON rendering, and endpoint details remain reachable only through an explicitly labelled advanced panel. Documentation describes this as a UI separation, not a new gameplay rule.

**Tech Stack:** .NET 8, ASP.NET Core minimal host, xUnit, static HTML/CSS/JS returned by `LocalWebUiHost.BuildShellHtml()`.

---

## Design note

### Problem
Issue #690 says the browser root currently feels like a debug/API dashboard because raw command controls, validation/lifecycle tools, endpoint hints, and API details are too close to the normal player path.

### Goal
The default landing screen must read as a game client: title, session summary, Continue/New/Load/Options/About/Exit, and understandable player text. Technical controls must be intentionally opened through an advanced/developer affordance and carry an explicit warning.

### Constraints
- Do not duplicate or move game logic into JavaScript; browser UI remains presentation over C# command/API services.
- Do not change afterlife or mortal-world runtime contracts.
- Do not introduce new mutating write paths or bypass `BrowserLocalWriteCoordinator`.
- Keep the existing command renderer available for developer/power-user workflows because other browser tasks still rely on it.

### Approaches considered
1. **Remove debug/command UI entirely.** Clean default UX, but breaks existing browser command renderer coverage and power-user workflow.
2. **Move debug/command UI to a separate route.** Clean separation, but larger routing/documentation change and not needed for this closure unit.
3. **Keep one root route, but make advanced mode explicit, hidden by default, accessible, and documented.** Minimal risk, preserves current command/API services, and directly satisfies #690 acceptance criteria.

Selected approach: option 3.

### Docs/prompts impact
No GM-facing runtime contract changes. Update browser-user documentation only: `docs/web-ui/local-web-host.md` and `docs/web-ui/browser-parity-checklist.md`.

### Review fix addendum

Independent review found two important gaps in the first implementation: Continue/New Game still revealed the advanced shell automatically, and ordinary menu errors did not have an explicit concise-message-plus-details pattern. The final implementation therefore also adds a player action panel for normal menu flows, keeps advanced opening behind an explicit user action, and introduces `renderPlayerError()` with `Подробности` disclosure for main-menu and save-load errors.

### Test strategy
Add xUnit tests that first fail against the current root HTML/docs:
- default player fragment before `#advanced-shell` does not contain command palette, diagnostics, raw API endpoints, or debug slash commands;
- advanced toggle has `aria-controls="advanced-shell"` and `aria-expanded="false"`;
- advanced panel is hidden by default and contains an explicit `Технический режим` warning;
- Continue/New Game cases call `showPlayerAction` instead of `showAdvancedShell`;
- normal player-menu errors use `renderPlayerError()` with a `Подробности` disclosure;
- docs state that root defaults to the player main menu and raw command/API tools live in advanced mode.

## File structure

- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
  - Adds the root-page regression test for default player content and advanced-panel accessibility/warning.
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`
  - Adds documentation guard for advanced/developer mode wording.
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`
  - Adds accessible advanced toggle attributes, explicit warning copy, and `aria-expanded` state updates.
- Modify: `docs/web-ui/local-web-host.md`
  - Reframes the root page as player-facing by default and moves raw command/API details under advanced/developer mode.
- Modify: `docs/web-ui/browser-parity-checklist.md`
  - Records that diagnostics/API/raw JSON surfaces belong to the advanced panel.

### Task 1: Add failing tests

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`

- [ ] **Step 1: Add root HTML guard test**

Add this test after `RootEndpoint_ReturnsPlayerFacingBrowserMainMenu`:

```csharp
    [Fact]
    public async Task RootEndpoint_KeepsDebugToolsInsideExplicitAdvancedPanel()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");
        var advancedIndex = html.IndexOf("<section id=\"advanced-shell\"", StringComparison.Ordinal);

        Assert.True(advancedIndex > 0, "The advanced panel must be a separate section after the default player menu.");
        var playerDefault = html[..advancedIndex];
        var advancedPanel = html[advancedIndex..];

        Assert.Contains("id=\"advanced-shell-toggle\"", playerDefault, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"advanced-shell\"", playerDefault, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"false\"", playerDefault, StringComparison.Ordinal);
        Assert.DoesNotContain("Командная палитра", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Диагностика", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/lifecycle", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/api/explorer", playerDefault, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-command=\"/debug\"", playerDefault, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("id=\"advanced-shell\" class=\"advanced-shell\" hidden", advancedPanel, StringComparison.Ordinal);
        Assert.Contains("Технический режим", advancedPanel, StringComparison.Ordinal);
        Assert.Contains("id=\"command-form\"", advancedPanel, StringComparison.Ordinal);
        Assert.Contains("id=\"lifecycle-panel\"", advancedPanel, StringComparison.Ordinal);
        Assert.Contains("data-command=\"/validate\"", advancedPanel, StringComparison.Ordinal);
    }
```

- [ ] **Step 2: Add docs guard test**

Add this test at the end of `LocalWebUiDocumentationTests`:

```csharp
    [Fact]
    public void LocalWebHostDocs_SeparatePlayerDefaultFromAdvancedDiagnostics()
    {
        var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var checklist = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "browser-parity-checklist.md"));

        Assert.Contains("root page defaults to the player-facing main menu", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Advanced / developer panel", hostDoc, StringComparison.Ordinal);
        Assert.Contains("raw command console", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("`/api/*` endpoint details", hostDoc, StringComparison.Ordinal);
        Assert.Contains("Advanced / developer panel", checklist, StringComparison.Ordinal);
        Assert.Contains("player-facing default", checklist, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 3: Run RED test command**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "RootEndpoint_KeepsDebugToolsInsideExplicitAdvancedPanel|LocalWebHostDocs_SeparatePlayerDefaultFromAdvancedDiagnostics"
```

Expected before implementation: FAIL because the current toggle lacks `aria-controls`/`aria-expanded`, the advanced section lacks the exact `Технический режим` warning, and docs lack the new guard wording.

### Task 2: Implement advanced-panel separation copy and accessibility

**Files:**
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Add accessible toggle attributes**

Replace the existing advanced toggle button with:

```html
                <button class="secondary" type="button" id="advanced-shell-toggle" aria-controls="advanced-shell" aria-expanded="false">Расширенный режим</button>
```

- [ ] **Step 2: Add explicit warning text in advanced panel**

Change the advanced panel intro from:

```html
                <h2>Расширенный командный режим</h2>
                <p>Этот раздел оставлен для перенесённых DTO-команд, диагностики и ручной проверки. Обычный игровой путь начинается выше, в главном меню.</p>
```

to:

```html
                <h2>Технический режим</h2>
                <p>Расширенный командный режим предназначен для разработчика, диагностики и ручной проверки. Обычный игровой путь начинается выше, в главном меню; raw-команды и подробности API здесь не являются основным интерфейсом игрока.</p>
```

- [ ] **Step 3: Update JavaScript expanded state**

Change `showAdvancedShell()` to set `aria-expanded`:

```javascript
            function showAdvancedShell() {
              advancedShell.hidden = false;
              advancedToggle.setAttribute('aria-expanded', 'true');
              if (!lifecycleLoaded) {
                lifecycleLoaded = true;
                loadLifecycleDashboard();
              }
            }
```

- [ ] **Step 4: Run focused GREEN command**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter RootEndpoint_KeepsDebugToolsInsideExplicitAdvancedPanel
```

Expected after implementation: PASS.

### Task 3: Update browser documentation

**Files:**
- Modify: `docs/web-ui/local-web-host.md`
- Modify: `docs/web-ui/browser-parity-checklist.md`

- [ ] **Step 1: Update `local-web-host.md` Current Browser MVP section**

Replace the paragraph that says `/` serves the first browser command shell with text that says the root page defaults to the player-facing main menu and the raw command console, lifecycle dashboard, validation controls, QTE probes, and `/api/*` endpoint details live in the Advanced / developer panel.

- [ ] **Step 2: Update `browser-parity-checklist.md` shell and diagnostics sections**

Change the shell checklist so command palette/diagnostics are described as advanced-mode surfaces, and add an `Advanced / developer panel` bullet stating that raw command console, validation, lifecycle details, QTE probes, raw JSON, and API endpoint hints must not be primary default content.

- [ ] **Step 3: Run docs GREEN command**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter LocalWebHostDocs_SeparatePlayerDefaultFromAdvancedDiagnostics
```

Expected after docs update: PASS.

### Task 4: Verify issue #690 and prepare PR

**Files:**
- All modified files above.

- [ ] **Step 1: Run focused browser UI tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "LocalWebUiHostTests|LocalWebUiDocumentationTests"
```

Expected: PASS.

- [ ] **Step 2: Run broader relevant tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|ExplorerWeb"
```

Expected: PASS or no extra matching tests beyond the focused set.

- [ ] **Step 3: Independent review**

Dispatch an independent reviewer with the issue body, acceptance criteria, and `git diff`. Required result: no critical/important issues before commit.

- [ ] **Step 4: Commit intentional files only**

Run:

```bash
git add BookOfEternityClient.Tests/LocalWebUiHostTests.cs BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs BookOfEternityClient/WebUi/LocalWebUiHost.cs docs/web-ui/local-web-host.md docs/web-ui/browser-parity-checklist.md docs/superpowers/plans/2026-05-24-issue-690-browser-advanced-panel.md
git commit -m "fix(web-ui): separate advanced browser diagnostics"
```

- [ ] **Step 5: Push, PR, CI, merge**

Create a PR whose body includes `Closes #690`, project context read, verification commands, docs impact, and no GM-facing contract impact. Merge only after local verification and CI/check status are acceptable.

## Self-review

- Spec coverage: issue #690 root/default UI, advanced access, concise errors/raw details via advanced docs, and regression test are covered.
- Placeholder scan: no `TODO`, `TBD`, or unspecified implementation steps remain.
- Type/name consistency: test names and HTML ids match existing `LocalWebUiHost.cs` markup.
