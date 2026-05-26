# Browser Visual QA Artifacts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add reproducible Browser Client first-screen visual QA artifacts and regression guards for issue #723, then use them to close parent issue #718 if all visual follow-up criteria are satisfied.

**Architecture:** Extend the existing built-frontend smoke pipeline instead of adding a new browser automation dependency. The C# test host continues to serve the built Vite React app and writes ignored artifacts under `TestResults/browser-smoke/`; new source/docs guards verify the artifact path, visual checklist, and player-vs-advanced boundaries.

**Tech Stack:** .NET 8/xUnit, Vite + React + TypeScript, existing GitHub Actions `browser-smoke-artifacts` upload.

---

### Task 1: Add RED guards for #723 first-screen visual QA artifact

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`

- [ ] **Step 1: Add failing artifact assertions to `LocalWebUiBuiltFrontendSmokeTests.cs`**

Insert after the existing `rebornPanelsArtifactPath` declaration:

```csharp
        var firstScreenVisualQaArtifactPath = Path.Combine(artifactRoot, "first-screen-visual-qa.html");
```

Insert after the existing Reborn panels artifact assertions:

```csharp
        Assert.True(File.Exists(firstScreenVisualQaArtifactPath), $"Missing browser first-screen visual QA artifact at {firstScreenVisualQaArtifactPath}");
        var firstScreenVisualQaArtifact = await File.ReadAllTextAsync(firstScreenVisualQaArtifactPath);
        Assert.Contains("data-artifact=\"browser-first-screen-visual-qa\"", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"desktop\"", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("data-viewport=\"mobile\"", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Книга Вечности: Перерождение", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Открыть книгу", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Продолжить главу", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Загрузить сохранение", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Настроить клиент", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("Главная → Игра → Душа → Мир → Журнал → Инвентарь", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.Contains("old React UI/UX reference only", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("advanced debug secondary", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Локальный игровой клиент", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("источник истины", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Главное меню недоступно", firstScreenVisualQaArtifact, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Debug", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Network", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command coverage", firstScreenVisualQaArtifact, StringComparison.OrdinalIgnoreCase);
        foreach (var emojiIcon in new[] { "✦", "📖", "🕯️", "🗺️", "✍️", "🎒", "🎞️", "⚙️" })
        {
            Assert.DoesNotContain(emojiIcon, firstScreenVisualQaArtifact, StringComparison.Ordinal);
        }
```

- [ ] **Step 2: Add failing source/docs guard to `BrowserFrontendWorkspaceTests.cs`**

Insert a new `[Fact]` before `ReactAppShell_DocumentsIssue704RoutingAndPlayerAdvancedBoundary`:

```csharp
    [Fact]
    public void BrowserVisualQa_DocumentsFirstScreenArtifactAndRegressionChecklist()
    {
        var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
        var smokeTest = File.ReadAllText(Path.Combine(RepoRoot, "BookOfEternityClient.Tests", "LocalWebUiBuiltFrontendSmokeTests.cs"));
        var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
        var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

        Assert.Contains("#723", readme, StringComparison.Ordinal);
        Assert.Contains("#723", hostDoc, StringComparison.Ordinal);
        Assert.Contains("first-screen-visual-qa.html", smokeTest, StringComparison.Ordinal);
        Assert.Contains("first-screen-visual-qa.html", readme, StringComparison.Ordinal);
        Assert.Contains("first-screen-visual-qa.html", hostDoc, StringComparison.Ordinal);
        Assert.Contains("old React UI/UX reference only", smokeTest, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("old React UI/UX reference", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("old React UI/UX reference", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("primary CTA", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no technical hero copy", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no repeated unavailable alerts", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no emoji route icons", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("advanced debug secondary", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BrowserFrontendWorkspaceTests", readme, StringComparison.Ordinal);
        Assert.Contains("LocalWebUiDocumentationTests", readme, StringComparison.Ordinal);

        var playerDefaultSlice = app[..app.IndexOf("function AdvancedDiagnosticsPanel", StringComparison.Ordinal)];
        Assert.DoesNotContain("/api/", playerDefaultSlice, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("command coverage", playerDefaultSlice, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 3: Add failing documentation guard to `LocalWebUiDocumentationTests.cs`**

Insert a new `[Fact]` after `LocalWebHostDocs_DocumentFrontendVerificationPipeline`:

```csharp
    [Fact]
    public void LocalWebHostDocs_DocumentBrowserVisualQaArtifactWorkflow()
    {
        var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
        var readme = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "README.md"));

        Assert.Contains("#723", hostDoc, StringComparison.Ordinal);
        Assert.Contains("#723", readme, StringComparison.Ordinal);
        Assert.Contains("first-screen-visual-qa.html", hostDoc, StringComparison.Ordinal);
        Assert.Contains("first-screen-visual-qa.html", readme, StringComparison.Ordinal);
        Assert.Contains("BrowserVisualQa_DocumentsFirstScreenArtifactAndRegressionChecklist", hostDoc, StringComparison.Ordinal);
        Assert.Contains("BrowserVisualQa_DocumentsFirstScreenArtifactAndRegressionChecklist", readme, StringComparison.Ordinal);
        Assert.Contains("old React UI/UX reference", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("primary CTA", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no technical hero copy", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no repeated unavailable alerts", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no emoji route icons", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("advanced debug secondary", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HTML visual smoke artifact", hostDoc, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not automated PNG screenshots", hostDoc, StringComparison.OrdinalIgnoreCase);
    }
```

- [ ] **Step 4: Run focused RED command**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~BrowserVisualQa_DocumentsFirstScreenArtifactAndRegressionChecklist|FullyQualifiedName~LocalWebHostDocs_DocumentBrowserVisualQaArtifactWorkflow" --logger "console;verbosity=minimal"
```

Expected: FAIL because `first-screen-visual-qa.html` generation and docs text are not implemented yet.

- [ ] **Step 5: Commit RED tests if the failure is correct**

```bash
git add BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs
git commit -m "test(web-ui): guard browser visual qa artifact"
```

### Task 2: Implement first-screen visual QA artifact generation

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`

- [ ] **Step 1: Write the artifact file during the built-frontend smoke test**

Insert after the existing artifact writes:

```csharp
        await File.WriteAllTextAsync(firstScreenVisualQaArtifactPath, BuildFirstScreenVisualQaArtifact(appSource));
```

- [ ] **Step 2: Add the artifact builder**

Insert before `BuildRebornPanelsArtifact`:

```csharp
    private static string BuildFirstScreenVisualQaArtifact(string appSource)
    {
        var routes = ExtractPlayerRoutes(appSource);
        var primaryRoutes = routes.Where(route => route.Kind == "primary").ToArray();
        var utilityRoutes = routes.Where(route => route.Kind == "utility").ToArray();
        var primarySequence = string.Join(" → ", primaryRoutes.Select(route => route.Label));
        var utilitySequence = string.Join(" → ", utilityRoutes.Select(route => route.Label));

        Assert.Equal(new[] { "home", "game", "soul", "world", "journal", "inventory" }, primaryRoutes.Select(route => route.Id));
        Assert.Equal(new[] { "media", "settings" }, utilityRoutes.Select(route => route.Id));
        Assert.Contains("Книга Вечности: Перерождение", appSource, StringComparison.Ordinal);
        Assert.Contains("Открыть книгу", appSource, StringComparison.Ordinal);
        Assert.Contains("Продолжить главу", appSource, StringComparison.Ordinal);
        Assert.Contains("Загрузить сохранение", appSource, StringComparison.Ordinal);
        Assert.Contains("Настроить клиент", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("<h1 id=\"browser-client-title\">Локальный игровой клиент</h1>", appSource, StringComparison.Ordinal);

        return $$"""
        <!doctype html>
        <html lang="ru" data-artifact="browser-first-screen-visual-qa">
        <head>
          <meta charset="utf-8">
          <title>Browser Client First Screen Visual QA</title>
          <style>
            :root { color-scheme: dark; font-family: Inter, "Segoe UI", sans-serif; background: #100b17; color: #f9ecd1; }
            body { margin: 0; padding: 24px; background: radial-gradient(circle at top left, rgba(216, 179, 106, 0.2), transparent 32%), #100b17; }
            .artifact { display: grid; gap: 20px; max-width: 1180px; margin: 0 auto; }
            .frame { border: 1px solid rgba(249, 236, 209, 0.18); border-radius: 28px; background: rgba(31, 24, 45, 0.88); box-shadow: 0 24px 80px rgba(0, 0, 0, 0.34); overflow: hidden; }
            .desktop-shell { display: grid; grid-template-columns: 280px 1fr 280px; min-height: 560px; }
            .mobile-shell { width: min(100%, 390px); margin: 0 auto; }
            .sidebar, .status, .mobile-nav { padding: 18px; background: rgba(16, 12, 24, 0.74); }
            .content { padding: 28px; display: grid; gap: 18px; align-content: start; }
            .brand { color: #ffe2a6; letter-spacing: 0.04em; }
            .primary { border: 1px solid rgba(216, 179, 106, 0.5); border-radius: 22px; padding: 18px; background: linear-gradient(135deg, rgba(216, 179, 106, 0.24), rgba(155, 107, 255, 0.12)); }
            .secondary, .route-card, .check { border: 1px solid rgba(216, 179, 106, 0.24); border-radius: 18px; padding: 12px; background: rgba(255, 255, 255, 0.055); }
            .route-list, .checks, .secondary-row { display: grid; gap: 10px; }
            .secondary-row { grid-template-columns: repeat(3, minmax(0, 1fr)); }
            .muted { color: rgba(249, 236, 209, 0.72); }
            .locked { color: rgba(249, 236, 209, 0.62); border-style: dashed; }
            .advanced { margin-top: 16px; color: rgba(249, 236, 209, 0.62); border: 1px dashed rgba(249, 236, 209, 0.24); border-radius: 16px; padding: 12px; }
            @media (max-width: 860px) { .desktop-shell, .secondary-row { grid-template-columns: 1fr; } }
          </style>
        </head>
        <body>
          <main class="artifact">
            <section class="frame" data-viewport="desktop" aria-label="Desktop first-screen visual QA">
              <div class="desktop-shell">
                <nav class="sidebar" aria-label="Player routes">
                  <p class="brand">{{WebUtility.HtmlEncode(primarySequence)}}</p>
                  <div class="route-list">{{RenderRouteCards(primaryRoutes)}}</div>
                  <p class="brand">{{WebUtility.HtmlEncode(utilitySequence)}}</p>
                  <div class="route-list">{{RenderRouteCards(utilityRoutes)}}</div>
                  <div class="advanced">advanced debug secondary: Расширенный режим остаётся отдельным вторичным входом.</div>
                </nav>
                <section class="content" aria-label="Launcher visual target">
                  <p class="brand">Книга Вечности: Перерождение</p>
                  <h1>Открыть книгу</h1>
                  <p class="muted">Default first screen reads as a game launcher, not a local runtime dashboard.</p>
                  <article class="primary"><strong>Primary CTA: Продолжить главу</strong><p>Если продолжение недоступно, CTA переключается на Загрузить сохранение или Начать новую главу.</p></article>
                  <div class="secondary-row">
                    <article class="secondary">Загрузить сохранение</article>
                    <article class="secondary">Начать новую главу</article>
                    <article class="secondary">Настроить клиент</article>
                  </div>
                  <article class="secondary locked">Обычная no-session пауза выглядит приглушённо, без красных повторяющихся unavailable alerts.</article>
                </section>
                <aside class="status" aria-label="Player status rail">
                  <h2>Сводка книги</h2>
                  <p class="muted">Слой книги · Герой и душа · Сохранение · Ожидание ГМа.</p>
                  <div class="checks">
                    <div class="check">old React UI/UX reference only: central launcher, tabs/sections, polished cards, save/config actions.</div>
                    <div class="check">no technical hero copy</div>
                    <div class="check">no repeated unavailable alerts</div>
                    <div class="check">no emoji route icons</div>
                  </div>
                </aside>
              </div>
            </section>
            <section class="frame mobile-shell" data-viewport="mobile" aria-label="Mobile first-screen visual QA">
              <div class="mobile-nav">
                <p class="brand">Книга Вечности: Перерождение</p>
                <h1>Открыть книгу</h1>
                <article class="primary">Primary CTA: Продолжить главу</article>
                <p class="muted">{{WebUtility.HtmlEncode(primarySequence)}}</p>
                <div class="route-list">{{RenderRouteCards(primaryRoutes)}}</div>
                <div class="advanced">advanced debug secondary</div>
              </div>
            </section>
          </main>
        </body>
        </html>
        """;
    }
```

- [ ] **Step 3: Re-run focused GREEN command**

Run the same focused command from Task 1 Step 4.

Expected: remaining failures only in docs guards until docs are updated, or PASS if docs were already updated.

- [ ] **Step 4: Commit artifact generation when focused smoke artifact assertions pass**

```bash
git add BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs
git commit -m "test(web-ui): generate first-screen visual qa artifact"
```

### Task 3: Document the #723 runbook and checklist

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Update `README.md`**

Append a section after `## Route iconography and states (#721)`:

```markdown
## First-screen visual QA (#723)

Issue #723 adds a dependency-light HTML visual smoke artifact for the Browser Client default first screen. Run:

```powershell
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
```

The test writes `TestResults/browser-smoke/first-screen-visual-qa.html` next to `root.html`, `game-route.html`, `network.json`, `navigation-ia.html`, `detail-surfaces.html`, and `reborn-panels.html`. CI uploads the directory as `browser-smoke-artifacts`.

The artifact is an HTML visual smoke artifact, not automated PNG screenshots. It intentionally remains dependency-light until a future tracked task selects a browser screenshot automation stack. Review it against the old React UI/UX reference only: central game launcher, primary CTA, polished cards, tabs/sections, and save/config actions. Do not copy old prompts, mortal-life-only mechanics, or runtime rules.

Regression checklist: primary CTA present; no technical hero copy; no repeated unavailable alerts; no emoji route icons; advanced debug secondary and behind explicit opt-in. Source guards live in `BrowserVisualQa_DocumentsFirstScreenArtifactAndRegressionChecklist` and documentation guards live in `LocalWebHostDocs_DocumentBrowserVisualQaArtifactWorkflow`.
```

- [ ] **Step 2: Update `docs/web-ui/local-web-host.md` tracked task line and verification section**

Add `#723` to the tracked tasks list on line 3.

Add a paragraph to the verification pipeline section:

```markdown
Issue #723 adds `TestResults/browser-smoke/first-screen-visual-qa.html`, a dependency-light HTML visual smoke artifact for the default Browser Client first screen. It has explicit desktop/mobile frames and should be reviewed against the old React UI/UX reference only: central launcher, primary CTA, polished cards, tabs/sections, and save/config actions. It is not automated PNG screenshots; it keeps CI offline-friendly until a future tracked task selects browser screenshot automation.

Regression checklist: primary CTA; no technical hero copy; no repeated unavailable alerts; no emoji route icons; advanced debug secondary behind explicit opt-in. Guard tests: `BrowserVisualQa_DocumentsFirstScreenArtifactAndRegressionChecklist` and `LocalWebHostDocs_DocumentBrowserVisualQaArtifactWorkflow`.
```

- [ ] **Step 3: Run focused docs/source guard command**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserVisualQa_DocumentsFirstScreenArtifactAndRegressionChecklist|FullyQualifiedName~LocalWebHostDocs_DocumentBrowserVisualQaArtifactWorkflow" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Commit docs**

```bash
git add BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs
git commit -m "docs(web-ui): document browser visual qa artifact"
```

### Task 4: Verify, review, PR, and close issues

**Files:**
- Verify all files changed by Tasks 1–3 plus `docs/superpowers/specs/2026-05-26-browser-visual-qa-artifacts-design.md` and this plan.

- [ ] **Step 1: Run frontend verify**

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Expected: PASS.

- [ ] **Step 2: Run focused browser/docs tests**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
```

Expected: PASS and `TestResults/browser-smoke/first-screen-visual-qa.html` exists.

- [ ] **Step 3: Run broad browser suite**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|Browser" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 4: Run diff/security checks**

```bash
git diff --check
git diff --cached | grep '^+' | grep -iE '(api_key|secret|password|token|passwd)\s*=\s*["'\'''][^"'\''']{6,}["'\''']|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f"|\.format\(.*SELECT|\.format\(.*INSERT' || echo NO_STATIC_SCAN_MATCHES
```

Expected: `git diff --check` exit 0 and `NO_STATIC_SCAN_MATCHES`.

- [ ] **Step 5: Request independent review**

Dispatch a reviewer with the issue criteria, changed files, and verification evidence. Fix Critical/Important findings, then re-run focused verification.

- [ ] **Step 6: Push, create PR, and wait for CI**

```bash
git push -u origin HEAD
gh pr create --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --base main --title "test(web-ui): add browser first-screen visual QA artifacts" --body-file .hermes/pr-723-body.md
gh pr checks --watch
```

PR body must include `Closes #723` and `Closes #718` only after mapping #718's child visual follow-up criteria to closed child tasks and this verification.

- [ ] **Step 7: Merge green PR and verify closure**

```bash
gh pr merge --squash --delete-branch
git checkout main
git pull --ff-only origin main
gh issue view 723 --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --json state,stateReason,url
gh issue view 718 --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --json state,stateReason,url
```

Expected: issues #723 and #718 are closed as completed, or #718 receives a closure evidence comment and is closed manually if auto-close did not apply.
