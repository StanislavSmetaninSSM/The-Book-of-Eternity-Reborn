# Issue #703 Typed Browser API Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a typed TypeScript API contract/client for the Browser Client and fixture-backed .NET/TypeScript contract checks for the main browser DTO path.

**Architecture:** Keep C# as the gameplay/application authority. Add hand-written TypeScript DTOs and a typed client under `BookOfEternityClient.WebFrontend/src/api/`, then verify representative C# DTOs against tracked JSON fixtures that TypeScript imports with `satisfies` checks.

**Tech Stack:** .NET 8/xUnit, System.Text.Json, Vite + React + TypeScript, GitHub issue #703.

---

## File structure

- Create: `BookOfEternityClient.Tests/BrowserApiContractTests.cs` — .NET guard tests that serialize representative C# DTOs, compare frontend fixtures, and assert docs/API files exist.
- Create: `BookOfEternityClient.WebFrontend/src/api/contracts.ts` — TypeScript DTO/request/result interfaces and endpoint metadata types.
- Create: `BookOfEternityClient.WebFrontend/src/api/client.ts` — typed fetch wrapper and `BrowserApiClient` methods for current local endpoints.
- Create: `BookOfEternityClient.WebFrontend/src/api/contract-fixture-checks.ts` — imports JSON fixtures and checks them with `satisfies`.
- Create fixtures under `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/`:
  - `main-menu.json`
  - `session-status.json`
  - `game-screen.json`
  - `lifecycle-dashboard.json`
  - `explorer-command-result.json`
  - `qte-state.json`
  - `api-error.json`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx` — consume typed API contract summary metadata instead of hard-coded endpoint references.
- Modify: `BookOfEternityClient.WebFrontend/README.md` — document typed contract/client workflow.
- Modify: `docs/web-ui/local-web-host.md` — document #703 endpoint contract, fixtures, normalized errors, and safe update workflow.

### Task 1: Add failing .NET contract guard tests

**Objective:** Capture #703 acceptance criteria in tests before adding TypeScript production files.

**Files:**
- Create: `BookOfEternityClient.Tests/BrowserApiContractTests.cs`

- [ ] **Step 1: Write failing tests**

Create `BrowserApiContractTests.cs` with tests that require:

```csharp
[Fact]
public void FrontendApiContractFiles_ArePresentAndDocumentEndpointMethods()
{
    Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "api", "contracts.ts")));
    Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "api", "client.ts")));
    Assert.True(File.Exists(Path.Combine(FrontendRoot, "src", "api", "contract-fixture-checks.ts")));
    var client = File.ReadAllText(Path.Combine(FrontendRoot, "src", "api", "client.ts"));
    Assert.Contains("getMainMenu", client, StringComparison.Ordinal);
    Assert.Contains("getSessionStatus", client, StringComparison.Ordinal);
    Assert.Contains("getGameScreen", client, StringComparison.Ordinal);
    Assert.Contains("executeExplorerCommand", client, StringComparison.Ordinal);
}
```

Also add semantic fixture comparison tests for `main-menu.json`, `session-status.json`, `game-screen.json`, `lifecycle-dashboard.json`, `explorer-command-result.json`, `qte-state.json`, and `api-error.json` using representative C# DTO builders and `JsonNode.DeepEquals`.

- [ ] **Step 2: Run focused tests to verify RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"
```

Expected: FAIL because `BrowserApiContractTests` requires API contract files/fixtures that do not exist yet.

- [ ] **Step 3: Commit RED test**

```bash
git add BookOfEternityClient.Tests/BrowserApiContractTests.cs
git commit -m "test: add browser API contract guards"
```

### Task 2: Add TypeScript contracts, client, and fixtures

**Objective:** Implement the typed frontend API boundary required by the failing guard tests.

**Files:**
- Create: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Create: `BookOfEternityClient.WebFrontend/src/api/client.ts`
- Create: `BookOfEternityClient.WebFrontend/src/api/contract-fixture-checks.ts`
- Create: `BookOfEternityClient.WebFrontend/src/api/contract-fixtures/*.json`
- Modify: `BookOfEternityClient.WebFrontend/src/App.tsx`

- [ ] **Step 1: Add `contracts.ts`**

Define exported interfaces for the current browser DTO path, including `BrowserMainMenuDto`, `LocalWebUiSessionStatus`, `BrowserGameScreenDto`, `BrowserLifecycleDashboardDto`, `ExplorerCommandResult`, `QteWebStateDto`, request DTOs, `BrowserApiResult<T>`, and `BrowserApiErrorKind`.

- [ ] **Step 2: Add `client.ts`**

Implement:

```ts
export interface BrowserApiClient {
  getMainMenu(): Promise<BrowserApiResult<BrowserMainMenuDto>>;
  getSessionStatus(): Promise<BrowserApiResult<LocalWebUiSessionStatus>>;
  getGameScreen(): Promise<BrowserApiResult<BrowserGameScreenDto>>;
  getLifecycleDashboard(): Promise<BrowserApiResult<BrowserLifecycleDashboardDto>>;
  validateLifecycle(): Promise<BrowserApiResult<BrowserValidationSummaryDto>>;
  loadSave(request: BrowserLoadSaveRequest): Promise<BrowserApiResult<BrowserLoadSaveResultDto>>;
  executeExplorerCommand(request: ExplorerWebCommandRequest): Promise<BrowserApiResult<ExplorerCommandResult>>;
  getPromptSession(sessionId: string): Promise<BrowserApiResult<ExplorerCommandResult>>;
  submitPromptSession(request: ExplorerPromptSessionSubmitRequest): Promise<BrowserApiResult<ExplorerCommandResult>>;
  cancelPromptSession(request: ExplorerPromptSessionCancelRequest): Promise<BrowserApiResult<ExplorerCommandResult>>;
  getQteState(): Promise<BrowserApiResult<QteWebStateDto>>;
  resolveQteOffer(request: QteWebOfferDecisionRequest): Promise<BrowserApiResult<QteWebStateDto>>;
  resolveQteAction(request: QteWebActionRequest): Promise<BrowserApiResult<QteWebStateDto>>;
}
```

Use a shared `requestJson<T>()` that normalizes HTTP/network failures into `BrowserApiError`.

- [ ] **Step 3: Add fixtures and TypeScript fixture checks**

Create fixture JSON files matching the representative C# DTOs from Task 1. In `contract-fixture-checks.ts`, import each fixture and assign it with `satisfies` to the corresponding interface so `npm run typecheck` validates the shape.

- [ ] **Step 4: Update `App.tsx`**

Import `browserApiContractSummary` from `src/api/client.ts` and render its endpoint labels so the React shell consumes the typed API boundary rather than hard-coded endpoint strings.

- [ ] **Step 5: Run focused tests/typecheck to verify GREEN**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
```

Expected: both commands exit 0.

- [ ] **Step 6: Commit implementation**

```bash
git add BookOfEternityClient.WebFrontend/src/api BookOfEternityClient.WebFrontend/src/App.tsx
git commit -m "feat: add typed browser API client contract"
```

### Task 3: Document the typed contract workflow

**Objective:** Make future DTO updates safe and discoverable.

**Files:**
- Modify: `BookOfEternityClient.WebFrontend/README.md`
- Modify: `docs/web-ui/local-web-host.md`
- Modify: `BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs` if a docs guard needs explicit #703 assertions.

- [ ] **Step 1: Write/update docs guard if needed**

Add assertions that local web-host docs mention `src/api/contracts.ts`, `src/api/client.ts`, `contract-fixtures`, #703, `BrowserApiContractTests`, and `npm run typecheck`.

- [ ] **Step 2: Verify docs guard RED**

Run:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
```

Expected: FAIL if docs text is not present yet.

- [ ] **Step 3: Update README and local web-host docs**

Document:

- C# owns runtime/API behavior.
- TypeScript owns typed endpoint consumption and request state.
- How to update DTOs safely: edit C# DTO, update TypeScript type, update JSON fixture, run focused .NET + frontend typecheck/build.
- Normalized player-facing vs advanced diagnostic error handling.

- [ ] **Step 4: Verify docs GREEN**

Run the same docs test filter. Expected: PASS.

- [ ] **Step 5: Commit docs**

```bash
git add BookOfEternityClient.WebFrontend/README.md docs/web-ui/local-web-host.md BookOfEternityClient.Tests/LocalWebUiDocumentationTests.cs
git commit -m "docs: document browser API contract workflow"
```

### Task 4: Final verification, review, and PR

**Objective:** Prove #703 is complete, reviewed, and safe to merge.

**Files:** all changed files.

- [ ] **Step 1: Run focused verification**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
npm run typecheck --prefix BookOfEternityClient.WebFrontend
npm run build --prefix BookOfEternityClient.WebFrontend
```

Expected: all exit 0.

- [ ] **Step 2: Run broader browser verification**

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "WebUi|LocalWebUi|BrowserFrontend|BrowserApi" --logger "console;verbosity=minimal"
```

Expected: exit 0.

- [ ] **Step 3: Run final repository checks**

```bash
git diff --check
git diff --cached --check
```

Expected: no whitespace errors. Also scan added lines for obvious secrets/injection/eval/deserialization before commit/PR.

- [ ] **Step 4: Independent review**

Dispatch independent spec/code review with the issue body, design, plan, and `git diff main...HEAD`. Fix Critical/Important findings and re-review before PR.

- [ ] **Step 5: Push and create PR**

```bash
git push -u origin HEAD
gh pr create --title "feat: add typed browser API contract" --body "## Summary
- Adds typed TypeScript DTO contracts and BrowserApiClient for local web endpoints
- Adds fixture-backed C#/TypeScript contract checks for main browser DTO path
- Documents the #703 contract update workflow and normalized error handling

Closes #703

## Test Plan
- dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter \"FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests\" --logger \"console;verbosity=minimal\"
- npm run typecheck --prefix BookOfEternityClient.WebFrontend
- npm run build --prefix BookOfEternityClient.WebFrontend
- dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter \"WebUi|LocalWebUi|BrowserFrontend|BrowserApi\" --logger \"console;verbosity=minimal\"
- git diff --check"
```

- [ ] **Step 6: CI and merge**

Watch checks with `gh pr checks --watch`. If green, squash-merge with branch deletion. If red, use systematic debugging, fix with a regression test, push, and re-watch. After merge, pull `main` and verify issue #703 is closed.
