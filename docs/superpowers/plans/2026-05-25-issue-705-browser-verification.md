# Issue #705 Browser Verification Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a local/CI verification path that restores, typechecks, builds, serves, smokes, and documents the Browser Client frontend integration.

**Architecture:** Keep C# as the browser host/API authority and React as presentation. CI builds the frontend first, then .NET tests launch `LocalWebUiHost` against the built `dist/` output and capture HTML/API/network diagnostics under `TestResults/browser-smoke/`.

**Tech Stack:** .NET 8, xUnit, ASP.NET Core Minimal API host, Vite, React, TypeScript, npm, GitHub Actions.

---

## File Structure

- Modify `.github/workflows/dotnet-ci.yml`: add Node setup/cache, frontend `npm ci`, frontend verify/build, and browser smoke artifact upload.
- Modify `.gitignore`: ignore local `TestResults/` diagnostics.
- Modify `BookOfEternityClient.WebFrontend/package.json`: add `verify` script.
- Modify `BookOfEternityClient.WebFrontend/README.md`: document #705 verification commands and diagnostics.
- Modify `docs/web-ui/local-web-host.md`: add #705 to tracked tasks and document local/CI browser verification.
- Modify `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`: add guard test for npm verify script and CI workflow steps.
- Modify `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`: add guard test for docs/runbook.
- Create `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`: launch the C# host with built Vite assets, assert root/API/static behavior, and write artifacts.

---

### Task 1: Guard the frontend verify script and CI workflow

**Files:**
- Modify: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`
- Later implementation files: `BookOfEternityClient.WebFrontend/package.json`, `.github/workflows/dotnet-ci.yml`

- [ ] **Step 1: Write the failing guard test**

Add this test to `BrowserFrontendWorkspaceTests`:

```csharp
[Fact]
public void FrontendWorkspace_HasVerifyScriptAndCiFrontendWorkflow()
{
    var packageJsonPath = Path.Combine(FrontendRoot, "package.json");
    using var document = JsonDocument.Parse(File.ReadAllText(packageJsonPath));
    var scripts = document.RootElement.GetProperty("scripts");
    Assert.Equal("npm run typecheck && npm run build", scripts.GetProperty("verify").GetString());

    var workflow = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "dotnet-ci.yml"));
    Assert.Contains("Setup Node", workflow, StringComparison.Ordinal);
    Assert.Contains("node-version: 22.x", workflow, StringComparison.Ordinal);
    Assert.Contains("cache-dependency-path: BookOfEternityClient.WebFrontend/package-lock.json", workflow, StringComparison.Ordinal);
    Assert.Contains("npm ci --prefix BookOfEternityClient.WebFrontend", workflow, StringComparison.Ordinal);
    Assert.Contains("npm run verify --prefix BookOfEternityClient.WebFrontend", workflow, StringComparison.Ordinal);
    Assert.Contains("browser-smoke-artifacts", workflow, StringComparison.Ordinal);
    Assert.Contains("TestResults/browser-smoke", workflow, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests.FrontendWorkspace_HasVerifyScriptAndCiFrontendWorkflow" --logger "console;verbosity=minimal"
```

Expected: FAIL because `scripts.verify` does not exist and the CI workflow has no Node/frontend smoke artifact steps.

- [ ] **Step 3: Add minimal implementation**

Update `BookOfEternityClient.WebFrontend/package.json` scripts to include:

```json
"verify": "npm run typecheck && npm run build"
```

Update `.github/workflows/dotnet-ci.yml` with Node setup before .NET restore:

```yaml
      - name: Setup Node 22
        uses: actions/setup-node@v4
        with:
          node-version: 22.x
          cache: npm
          cache-dependency-path: BookOfEternityClient.WebFrontend/package-lock.json

      - name: Restore browser frontend dependencies
        run: npm ci --prefix BookOfEternityClient.WebFrontend

      - name: Verify browser frontend
        run: npm run verify --prefix BookOfEternityClient.WebFrontend
```

Add artifact upload after test results:

```yaml
      - name: Upload browser smoke artifacts
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: browser-smoke-artifacts
          path: TestResults/browser-smoke
          if-no-files-found: ignore
```

- [ ] **Step 4: Run test to verify GREEN**

Run the same focused test. Expected: PASS.

---

### Task 2: Add built-frontend host smoke test and artifact capture

**Files:**
- Create: `BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs`
- Modify: `.gitignore`

- [ ] **Step 1: Write the failing smoke test**

Create `LocalWebUiBuiltFrontendSmokeTests.cs` containing a test named `BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics`. It should:

- require `BookOfEternityClient.WebFrontend/dist/index.html`;
- start `LocalWebUiHost` with that `dist/` path;
- GET `/`, `/game`, `/api/main-menu`, `/api/session`, `/api/game-screen`, `/api/not-real`, `/assets/not-real.js`;
- write artifacts to `TestResults/browser-smoke/`;
- assert API/static misses are 404 and the shell/API responses contain expected player state.

- [ ] **Step 2: Run test to verify RED**

Run before frontend build:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests.BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics" --logger "console;verbosity=minimal"
```

Expected: FAIL if `dist/index.html` is missing, with a message instructing to run `npm run verify --prefix BookOfEternityClient.WebFrontend`.

- [ ] **Step 3: Build frontend and implement artifact-friendly smoke**

Run:

```bash
npm ci --prefix BookOfEternityClient.WebFrontend
npm run verify --prefix BookOfEternityClient.WebFrontend
```

Ensure `.gitignore` includes:

```gitignore
/TestResults/
```

- [ ] **Step 4: Run smoke to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests.BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics" --logger "console;verbosity=minimal"
```

Expected: PASS and files created under `TestResults/browser-smoke/`.

---

### Task 3: Document and guard the #705 runbook

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs`
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Write failing documentation guard**

Add this test to `LocalWebUiDocumentationTests`:

```csharp
[Fact]
public void LocalWebHostDocs_DocumentFrontendVerificationPipeline()
{
    var hostDoc = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "docs", "web-ui", "local-web-host.md"));
    var readme = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "README.md"));

    Assert.Contains("#705", hostDoc, StringComparison.Ordinal);
    Assert.Contains("npm ci --prefix BookOfEternityClient.WebFrontend", hostDoc, StringComparison.Ordinal);
    Assert.Contains("npm run verify --prefix BookOfEternityClient.WebFrontend", hostDoc, StringComparison.Ordinal);
    Assert.Contains("Category=BrowserWebUiBuiltFrontend", hostDoc, StringComparison.Ordinal);
    Assert.Contains("TestResults/browser-smoke", hostDoc, StringComparison.Ordinal);
    Assert.Contains("browser-smoke-artifacts", hostDoc, StringComparison.Ordinal);
    Assert.Contains("HTML/network diagnostics", hostDoc, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("screenshots", hostDoc, StringComparison.OrdinalIgnoreCase);

    Assert.Contains("#705", readme, StringComparison.Ordinal);
    Assert.Contains("npm run verify", readme, StringComparison.Ordinal);
    Assert.Contains("Category=BrowserWebUiBuiltFrontend", readme, StringComparison.Ordinal);
    Assert.Contains("TestResults/browser-smoke", readme, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run docs test to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiDocumentationTests.LocalWebHostDocs_DocumentFrontendVerificationPipeline" --logger "console;verbosity=minimal"
```

Expected: FAIL because docs do not mention #705 verification commands/artifacts yet.

- [ ] **Step 3: Update docs**

Update docs with exact command sequence:

```bash
npm ci --prefix BookOfEternityClient.WebFrontend
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiBuiltFrontend|Category=BrowserWebUiSmoke|Category=BrowserWebUiParity" --logger "console;verbosity=minimal"
```

Document `TestResults/browser-smoke/`, `browser-smoke-artifacts`, and that screenshots are deferred until a future Playwright/Selenium-style task.

- [ ] **Step 4: Run docs test to verify GREEN**

Run the same focused docs test. Expected: PASS.

---

### Task 4: Verify, review, commit, PR, CI, and merge

**Files:** all changed files from Tasks 1-3.

- [ ] **Step 1: Run local verification**

```bash
npm ci --prefix BookOfEternityClient.WebFrontend
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests.FrontendWorkspace_HasVerifyScriptAndCiFrontendWorkflow|FullyQualifiedName~LocalWebUiDocumentationTests.LocalWebHostDocs_DocumentFrontendVerificationPipeline|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserFrontend|LocalWebUi|BrowserWebUiSmoke|BrowserApi" --logger "console;verbosity=minimal"
git diff --check
```

Expected: all commands exit 0.

- [ ] **Step 2: Static scan added lines**

```bash
git diff -- . ':!docs/superpowers/plans/*' ':!docs/superpowers/specs/*' ':!BookOfEternityClient.WebFrontend/package-lock.json' | grep '^+' | grep -iE '(api_key|secret|password|token|passwd)\s*=\s*["'"'][^"'"']{6,}["'"']|os\.system\(|subprocess.*shell=True|\beval\(|\bexec\(|pickle\.loads?\(|execute\(f"|\.format\(.*SELECT|\.format\(.*INSERT' || true
```

Expected: no findings.

- [ ] **Step 3: Independent review**

Dispatch an independent reviewer with the issue criteria and git diff. Fix Critical/Important findings and re-review.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/dotnet-ci.yml .gitignore BookOfEternityClient.WebFrontend/package.json BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs docs/superpowers/specs/2026-05-25-issue-705-browser-verification-design.md docs/superpowers/plans/2026-05-25-issue-705-browser-verification.md
git commit -m "feat(web-ui): add browser frontend verification pipeline"
```

- [ ] **Step 5: PR and merge**

Create a PR with `Closes #705`, wait for `.NET CI / Build and test` to pass, squash-merge to `main`, delete remote branch, update local `main`, and verify issue #705 is closed.

---

## Self-review

- Spec coverage: tasks cover frontend restore/typecheck/build, CI docs, built host smoke, diagnostics, artifact upload, player/advanced guard evidence, and local/CI commands.
- Placeholder scan: no TBD/TODO/implement-later placeholders.
- Type consistency: test names and file paths match the planned implementation.
