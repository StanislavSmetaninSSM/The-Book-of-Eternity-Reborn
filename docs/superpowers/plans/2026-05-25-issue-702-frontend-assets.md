# Issue #702 Frontend Asset Host Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the Browser Client root shell out of `LocalWebUiHost.cs` and make the C# local host serve standalone frontend assets while preserving local APIs and `--web` usability.

**Architecture:** Add a small frontend asset resolver and static-file host wiring. Prefer `BookOfEternityClient.WebFrontend/dist/` when built, copy build artifacts to `wwwroot/browser` when present, and fall back to a tracked standalone shell asset extracted from the existing inline shell. C# remains the runtime/API authority; frontend assets are presentation only.

**Tech Stack:** .NET 8 Minimal APIs, ASP.NET Core static files, Vite + React + TypeScript workspace, xUnit.

---

### Task 1: Add failing host-contract tests

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests that express the #702 contract before implementation:

```csharp
[Fact]
public async Task RootEndpoint_ServesConfiguredFrontendIndexAndStaticAssets()
{
    var frontendRoot = Path.Combine(_rootPath, "frontend-dist");
    Directory.CreateDirectory(Path.Combine(frontendRoot, "assets"));
    await File.WriteAllTextAsync(Path.Combine(frontendRoot, "index.html"), """
    <!doctype html>
    <html lang="ru"><head><title>External Browser Shell</title></head><body><div id="root"></div><script type="module" src="/assets/app.js"></script></body></html>
    """);
    await File.WriteAllTextAsync(Path.Combine(frontendRoot, "assets", "app.js"), "console.log('frontend asset');");

    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(
        Array.Empty<string>(),
        new LocalWebUiHostOptions(_rootPath, url, frontendRoot));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    var html = await client.GetStringAsync("/");
    var js = await client.GetStringAsync("/assets/app.js");
    var health = JsonNode.Parse(await client.GetStringAsync("/api/health"))!.AsObject();

    Assert.Contains("External Browser Shell", html, StringComparison.Ordinal);
    Assert.Contains("/assets/app.js", html, StringComparison.Ordinal);
    Assert.Contains("frontend asset", js, StringComparison.Ordinal);
    Assert.Equal("ok", health["status"]!.GetValue<string>());
}

[Fact]
public async Task FallbackEndpoint_ReturnsIndexForClientRoutesButNotApiOrAssetMisses()
{
    var frontendRoot = Path.Combine(_rootPath, "frontend-dist");
    Directory.CreateDirectory(frontendRoot);
    await File.WriteAllTextAsync(Path.Combine(frontendRoot, "index.html"), "<!doctype html><title>SPA Shell</title>");

    var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
    await using var app = LocalWebUiHost.Build(
        Array.Empty<string>(),
        new LocalWebUiHostOptions(_rootPath, url, frontendRoot));
    await app.StartAsync();

    using var client = new HttpClient { BaseAddress = new Uri(url) };
    var clientRoute = await client.GetStringAsync("/game/screen");
    using var missingApi = await client.GetAsync("/api/not-real");
    using var missingAsset = await client.GetAsync("/assets/not-real.js");

    Assert.Contains("SPA Shell", clientRoute, StringComparison.Ordinal);
    Assert.Equal(HttpStatusCode.NotFound, missingApi.StatusCode);
    Assert.Equal(HttpStatusCode.NotFound, missingAsset.StatusCode);
}
```

Add a workspace/source guard test:

```csharp
[Fact]
public void FrontendHostContract_UsesExternalAssetsInsteadOfInlineShellBlob()
{
    var hostSource = File.ReadAllText(Path.Combine(RepoRoot, "BookOfEternityClient", "WebUi", "LocalWebUiHost.cs"));
    var fallbackShell = Path.Combine(FrontendRoot, "public", "local-web-ui-shell.html");

    Assert.True(File.Exists(fallbackShell), $"Missing {fallbackShell}");
    Assert.DoesNotContain("BuildShellHtml", hostSource, StringComparison.Ordinal);
    Assert.DoesNotContain("data-menu-action=\"continue\"", hostSource, StringComparison.Ordinal);
    Assert.DoesNotContain("<!doctype html>", hostSource, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "RootEndpoint_ServesConfiguredFrontendIndexAndStaticAssets|FallbackEndpoint_ReturnsIndexForClientRoutesButNotApiOrAssetMisses|FrontendHostContract_UsesExternalAssetsInsteadOfInlineShellBlob" --logger "console;verbosity=minimal"
```

Expected: FAIL because `LocalWebUiHostOptions` has no third constructor argument and no fallback shell asset exists.

### Task 2: Extract the existing shell to a tracked frontend asset

**Files:**
- Create: `BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Extract the raw shell content**

Use a mechanical script to copy the current `BuildShellHtml()` raw string body into `BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html`. Preserve all player-facing HTML/CSS/JS exactly in this step.

- [ ] **Step 2: Remove `BuildShellHtml()` only after the asset exists**

Delete the large raw-string method from `LocalWebUiHost.cs`; the next task will add the replacement resolver/wiring.

- [ ] **Step 3: Do not commit generated output**

Verify that `BookOfEternityClient.WebFrontend/dist/`, `node_modules/`, and `BookOfEternityClient/bin/` remain untracked/generated and are not staged.

### Task 3: Implement frontend asset resolver and static-file host wiring

**Files:**
- Create: `BookOfEternityClient/WebUi/LocalWebUiFrontendAssets.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`
- Modify: `BookOfEternityClient/BookOfEternityClient.csproj`

- [ ] **Step 1: Add resolver**

Create `LocalWebUiFrontendAssets` with methods to resolve:

```csharp
internal sealed class LocalWebUiFrontendAssets
{
    public static LocalWebUiFrontendAssets Resolve(string? overridePath = null);
    public string RootPath { get; }
    public string IndexPath { get; }
    public bool IsFallbackShell { get; }
}
```

Resolution order:
1. explicit override path containing `index.html`;
2. repo `BookOfEternityClient.WebFrontend/dist/index.html`;
3. output `wwwroot/browser/index.html`;
4. tracked fallback `BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html`.

- [ ] **Step 2: Wire static files and root/fallback routes**

Update `LocalWebUiHost.Build()` to:
- resolve assets once;
- call `app.UseStaticFiles()` with a `PhysicalFileProvider` rooted at the resolved asset root;
- map `/` to the resolved index file;
- map SPA fallback paths to the index except `/api/*` and `/assets/*` misses;
- leave all existing `/api/*`, `/api/media/*`, and `/assets/map-viewer.*` endpoints unchanged.

- [ ] **Step 3: Copy build output when present**

Add a nullable wildcard item to `BookOfEternityClient.csproj` that copies `../BookOfEternityClient.WebFrontend/dist/**` to `wwwroot/browser/**` when the Vite build exists. Do not add or track `dist/` files.

- [ ] **Step 4: Run tests to verify GREEN**

Run the same focused RED command. Expected: PASS.

### Task 4: Update frontend/docs contract tests and documentation

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`
- Modify: `docs/web-ui/local-web-host.md`
- Modify: `BookOfEternityClient.WebFrontend/README.md`

- [ ] **Step 1: Add doc expectations**

Add/adjust assertions so docs mention:
- `BookOfEternityClient.WebFrontend/dist/` as preferred build output;
- `BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html` as tracked fallback shell;
- `LocalWebUiHost` serving static frontend assets while C# keeps `/api/*` authority;
- generated `dist/` remains ignored.

- [ ] **Step 2: Update docs**

Rewrite the `Frontend Workspace` / current MVP sections of `docs/web-ui/local-web-host.md` and README to describe the new #702 host contract.

- [ ] **Step 3: Verify docs tests**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
```

Expected: PASS.

### Task 5: Full local verification, review, PR, CI, merge

**Files:** all changed files from Tasks 1–4.

- [ ] **Step 1: Run frontend verification**

```bash
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
```

Expected: both commands exit 0.

- [ ] **Step 2: Run focused and broader .NET verification**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "LocalWebUi" --logger "console;verbosity=minimal"
```

Expected: PASS.

- [ ] **Step 3: Run hygiene checks**

```bash
git diff --check
```

Then run the requesting-code-review static scans for added-line secrets, shell injection, dynamic evaluation, unsafe deserialization, and SQL string-formatting patterns.

Expected: no whitespace errors; no added-line security findings in production, test, or frontend source.

- [ ] **Step 4: Independent review**

Dispatch independent spec/code reviewers with the issue body, design summary, plan, and git diff. Fix Critical/Important findings and re-review.

- [ ] **Step 5: Commit, push, PR, CI, merge**

```bash
git add BookOfEternityClient/WebUi/LocalWebUiHost.cs BookOfEternityClient/WebUi/LocalWebUiFrontendAssets.cs BookOfEternityClient/BookOfEternityClient.csproj BookOfEternityClient.Tests/LocalWebUiHostTests.cs BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs BookOfEternityClient.WebFrontend/public/local-web-ui-shell.html BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md docs/superpowers/specs/2026-05-25-issue-702-frontend-assets-design.md docs/superpowers/plans/2026-05-25-issue-702-frontend-assets.md
git commit -m "feat(web-ui): serve frontend assets from local host"
git push -u origin task/702-serve-frontend-assets
gh pr create --title "feat(web-ui): serve frontend assets from local host" --body-file <prepared-body>
gh pr checks --watch
gh pr merge --squash --delete-branch
```

Expected: PR closes #702 after green CI and squash merge.
