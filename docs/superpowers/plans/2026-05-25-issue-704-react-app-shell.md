# Issue #704 React Browser App Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first real React/TypeScript Browser Client shell with player-facing routes, shared typed API state, explicit advanced/debug opt-in, and local-host preference for the built React index.

**Architecture:** Keep C# as the gameplay/application authority. React consumes the typed #703 `BrowserApiClient`, manages presentation/request state only, and renders reusable route/panel components; `LocalWebUiFrontendAssets` serves `dist/index.html` before copied fallback shell when a frontend build exists.

**Tech Stack:** .NET 8/xUnit, ASP.NET Core local host static files, Vite + React + TypeScript, CSS, GitHub issue #704.

---

## File structure

- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs` — add RED guard that build-root resolution prefers React `index.html` over copied fallback shell.
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs` — add RED guard that the React app source exposes the #704 shell/routing/player-vs-advanced contract and docs mention it.
- Modify: `BookOfEternityClient/WebUi/LocalWebUiFrontendAssets.cs` — prefer `index.html` before `local-web-ui-shell.html` in build roots.
- Rewrite: `BookOfEternityClient.WebFrontend/src/App.tsx` — implement routed shell, typed API loading state, player route panels, advanced opt-in, and reusable components.
- Rewrite: `BookOfEternityClient.WebFrontend/src/styles.css` — responsive game-client layout, cards, nav, realm themes, status bars, forms, advanced panel styles.
- Modify: `BookOfEternityClient.WebFrontend/README.md` — document #704 app shell structure and commands.
- Modify: `docs/web-ui/local-web-host.md` — document #704 tracked task, built React root preference, and player/advanced separation.
- Create: `docs/superpowers/specs/2026-05-25-issue-704-react-app-shell-design.md` — design/spec.
- Create: `docs/superpowers/plans/2026-05-25-issue-704-react-app-shell.md` — this plan.

### Task 1: Add failing host/source guards

**Objective:** Capture the #704 root-routing and player/advanced shell requirements before production changes.

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Add host preference RED test**

Add a test near existing root/static asset tests in `LocalWebUiHostTests.cs`:

```csharp
[Fact]
public void FrontendAssets_PrefersReactIndexOverCopiedFallbackShellInBuildRoot()
{
    var previousCurrentDirectory = Directory.GetCurrentDirectory();
    var fakeRepo = Path.Combine(_rootPath, "fake-repo");
    var distRoot = Path.Combine(fakeRepo, "BookOfEternityClient.WebFrontend", "dist");
    Directory.CreateDirectory(distRoot);
    File.WriteAllText(Path.Combine(distRoot, "index.html"), "<!doctype html><title>React Shell</title>");
    File.WriteAllText(Path.Combine(distRoot, "local-web-ui-shell.html"), "<!doctype html><title>Fallback Shell</title>");

    try
    {
        Directory.SetCurrentDirectory(fakeRepo);
        var assets = LocalWebUiFrontendAssets.Resolve();

        Assert.False(assets.IsFallbackShell);
        Assert.Equal(Path.Combine(distRoot, "index.html"), assets.IndexPath);
    }
    finally
    {
        Directory.SetCurrentDirectory(previousCurrentDirectory);
    }
}
```

- [ ] **Step 2: Add React source/docs RED tests**

Add tests to `BrowserFrontendWorkspaceTests.cs`:

```csharp
[Fact]
public void ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn()
{
    var app = File.ReadAllText(Path.Combine(FrontendRoot, "src", "App.tsx"));
    var styles = File.ReadAllText(Path.Combine(FrontendRoot, "src", "styles.css"));

    Assert.Contains("playerRoutes", app, StringComparison.Ordinal);
    Assert.Contains("activeRoute", app, StringComparison.Ordinal);
    Assert.Contains("advancedEnabled", app, StringComparison.Ordinal);
    Assert.Contains("loadBrowserState", app, StringComparison.Ordinal);
    Assert.Contains("browserApi.getMainMenu", app, StringComparison.Ordinal);
    Assert.Contains("browserApi.getGameScreen", app, StringComparison.Ordinal);
    Assert.Contains("browserApi.getSessionStatus", app, StringComparison.Ordinal);
    Assert.Contains("browserApi.getLifecycleDashboard", app, StringComparison.Ordinal);
    Assert.Contains("Главная", app, StringComparison.Ordinal);
    Assert.Contains("Игра", app, StringComparison.Ordinal);
    Assert.Contains("Душа", app, StringComparison.Ordinal);
    Assert.Contains("Мир", app, StringComparison.Ordinal);
    Assert.Contains("Медиа", app, StringComparison.Ordinal);
    Assert.Contains("Настройки", app, StringComparison.Ordinal);
    Assert.Contains("Расширенный режим", app, StringComparison.Ordinal);
    Assert.Contains("AdvancedDiagnosticsPanel", app, StringComparison.Ordinal);
    Assert.Contains("ShellPanel", app, StringComparison.Ordinal);
    Assert.Contains("StatusBar", app, StringComparison.Ordinal);
    Assert.Contains("RealmTheme", app, StringComparison.Ordinal);
    Assert.Contains("playerMessage", app, StringComparison.Ordinal);
    Assert.DoesNotContain("endpoint.id", app, StringComparison.Ordinal);
    Assert.Contains(".browser-shell", styles, StringComparison.Ordinal);
    Assert.Contains(".route-grid", styles, StringComparison.Ordinal);
    Assert.Contains(".advanced-diagnostics", styles, StringComparison.Ordinal);
    Assert.Contains("@media (max-width: 840px)", styles, StringComparison.Ordinal);
}

[Fact]
public void ReactAppShell_DocumentsIssue704RoutingAndPlayerAdvancedBoundary()
{
    var readme = File.ReadAllText(Path.Combine(FrontendRoot, "README.md"));
    var hostDoc = File.ReadAllText(Path.Combine(RepoRoot, "docs", "web-ui", "local-web-host.md"));

    Assert.Contains("#704", readme, StringComparison.Ordinal);
    Assert.Contains("React app shell", readme, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("player-facing routes", readme, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("advanced", readme, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("dist/index.html", readme, StringComparison.Ordinal);
    Assert.Contains("#704", hostDoc, StringComparison.Ordinal);
    Assert.Contains("React app shell", hostDoc, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("dist/index.html", hostDoc, StringComparison.Ordinal);
    Assert.Contains("Расширенный режим", hostDoc, StringComparison.Ordinal);
}
```

- [ ] **Step 3: Run focused tests to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests.FrontendAssets_PrefersReactIndexOverCopiedFallbackShellInBuildRoot|FullyQualifiedName~BrowserFrontendWorkspaceTests.ReactAppShell" --logger "console;verbosity=minimal"
```

Expected: FAIL because `LocalWebUiFrontendAssets` still chooses the copied fallback shell in build roots and `App.tsx`/docs do not yet contain the #704 shell contract.

- [ ] **Step 4: Commit RED tests**

```bash
git add BookOfEternityClient.Tests/LocalWebUiHostTests.cs BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs
git commit -m "test: add browser React shell guards"
```

### Task 2: Implement host build-root preference

**Objective:** Make built React `dist/index.html` the served root when a Vite build exists.

**Files:**
- Modify: `BookOfEternityClient/WebUi/LocalWebUiFrontendAssets.cs`

- [ ] **Step 1: Change candidate build root order**

In `Resolve`, replace the build-root loop with index-first resolution:

```csharp
foreach (var root in CandidateBuildRoots())
{
    var indexPath = Path.Combine(root, IndexFileName);
    if (File.Exists(indexPath))
        return new LocalWebUiFrontendAssets(root, indexPath, isFallbackShell: false);

    var shellPath = Path.Combine(root, FallbackShellFileName);
    if (File.Exists(shellPath))
        return new LocalWebUiFrontendAssets(root, shellPath, isFallbackShell: true);
}
```

Do not change `ResolveOverride`; override directories already prefer `index.html`.

- [ ] **Step 2: Run host preference test to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests.FrontendAssets_PrefersReactIndexOverCopiedFallbackShellInBuildRoot" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 3: Commit host preference**

```bash
git add BookOfEternityClient/WebUi/LocalWebUiFrontendAssets.cs
git commit -m "fix(web-ui): prefer built React index shell"
```

### Task 3: Build the React shell and shared UI state

**Objective:** Replace the placeholder React roadmap with a player-facing app shell that consumes typed browser API data.

**Files:**
- Rewrite: `BookOfEternityClient.WebFrontend/src/App.tsx`
- Rewrite: `BookOfEternityClient.WebFrontend/src/styles.css`

- [ ] **Step 1: Rewrite `App.tsx`**

Implement a single-file shell with:

```ts
import { useCallback, useEffect, useMemo, useState } from 'react';
import { browserApi } from './api/client';
import type {
  BrowserApiFailure,
  BrowserGameScreenDto,
  BrowserLifecycleDashboardDto,
  BrowserMainMenuDto,
  LocalWebUiSessionStatus
} from './api/contracts';
```

Define `RouteId`, `playerRoutes`, `RouteCard`, `BrowserShellState`, `loadBrowserState()`, and reusable components named `ShellPanel`, `StatusBar`, `AdvancedDiagnosticsPanel`, and `RealmTheme`/`resolveRealmTheme`. The rendered default UI must contain Russian labels for `Главная`, `Игра`, `Душа`, `Мир`, `Медиа`, `Настройки`, and `Расширенный режим`.

The app must call only typed client methods for live data:

```ts
const [shellState, setShellState] = useState<BrowserShellState>({ status: 'loading' });
const [activeRoute, setActiveRoute] = useState<RouteId>('home');
const [advancedEnabled, setAdvancedEnabled] = useState(false);

const loadBrowserState = useCallback(async () => {
  setShellState({ status: 'loading' });
  const [menu, session, game, lifecycle] = await Promise.all([
    browserApi.getMainMenu(),
    browserApi.getSessionStatus(),
    browserApi.getGameScreen(),
    browserApi.getLifecycleDashboard()
  ]);
  setShellState({ status: 'ready', menu, session, game, lifecycle });
}, []);
```

Default route panels must render `playerMessage` for API failures and hide technical details behind an explicit advanced/details section. Slash command execution must not exist in the default route.

- [ ] **Step 2: Rewrite `styles.css`**

Implement responsive styles for `.browser-shell`, `.shell-hero`, `.route-grid`, `.workspace-grid`, `.shell-panel`, `.status-bar`, `.advanced-diagnostics`, route buttons, forms, cards, and `@media (max-width: 840px)`. Keep desktop and narrow viewport structurally usable.

- [ ] **Step 3: Run source guard and frontend typecheck**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests.ReactAppShell_DefinesPlayerRoutesSharedStateAndAdvancedOptIn" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
```

Expected: both commands exit 0.

- [ ] **Step 4: Commit React shell**

```bash
git add BookOfEternityClient.WebFrontend/src/App.tsx BookOfEternityClient.WebFrontend/src/styles.css
git commit -m "feat(web-ui): build React browser app shell"
```

### Task 4: Document #704 app-shell workflow

**Objective:** Make the new root behavior and player/advanced UI boundary discoverable for future tasks.

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Update docs**

Document:

- #704 adds the React app shell and player-facing routes.
- Built `BookOfEternityClient.WebFrontend/dist/index.html` is preferred for the root when present; `public/local-web-ui-shell.html` is the no-build fallback.
- React components consume `src/api/client.ts` and C# remains the authority.
- Command/API diagnostics stay behind explicit `Расширенный режим` opt-in.
- Verification commands: frontend typecheck/build and focused browser .NET filters.

- [ ] **Step 2: Run docs guard to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests.ReactAppShell_DocumentsIssue704RoutingAndPlayerAdvancedBoundary" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 3: Commit docs/spec/plan**

```bash
git add BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md docs/superpowers/specs/2026-05-25-issue-704-react-app-shell-design.md docs/superpowers/plans/2026-05-25-issue-704-react-app-shell.md
git commit -m "docs: document React browser app shell"
```

### Task 5: Final verification, independent review, PR, CI, merge

**Objective:** Prove #704 is complete, reviewed, and merged safely.

**Files:** all changed files.

- [ ] **Step 1: Run focused frontend and .NET verification**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests.FrontendAssets_PrefersReactIndexOverCopiedFallbackShellInBuildRoot|FullyQualifiedName~BrowserFrontendWorkspaceTests.ReactAppShell|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
```

Expected: all commands exit 0.

- [ ] **Step 2: Run broader browser verification**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|BrowserFrontend|BrowserApi" --logger "console;verbosity=minimal"
dotnet publish BookOfEternityClient/BookOfEternityClient.csproj --no-restore -o tmp/publish-browser-assets-test -v minimal
```

Expected: both commands exit 0 and publish has no duplicate frontend asset paths.

- [ ] **Step 3: Run final diff checks and static scan**

```bash
git diff --check
git diff --cached --check
git diff --name-only main...HEAD
```

Scan added lines for hardcoded secrets, shell injection, `eval`/`exec`, unsafe deserialization, and SQL string formatting. Expected: no findings.

- [ ] **Step 4: Independent review**

Dispatch independent spec/code review with the issue body, design, plan, verification evidence, and `git diff main...HEAD`. Fix Critical/Important findings and re-review before PR.

- [ ] **Step 5: Push and create PR**

```bash
git push -u origin task/704-browser-react-shell
gh pr create --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --base main --head task/704-browser-react-shell --title "feat(web-ui): build React browser app shell" --body-file .hermes/pr-704-body.md
```

PR body must include `Closes #704`, summary, tests, review result, docs impact, and note that no afterlife/GM prompt contracts changed.

- [ ] **Step 6: Wait for CI and merge green PR**

```bash
gh pr checks <PR_NUMBER> --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --watch
gh pr merge <PR_NUMBER> --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --squash --delete-branch
git switch main
git pull --ff-only origin main
gh issue view 704 --repo StanislavSmetaninSSM/The-Book-of-Eternity-Reborn --json state
```

Expected: CI green, PR merged, issue #704 closed by the merge.
