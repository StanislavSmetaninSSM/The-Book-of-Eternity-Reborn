# Web UI QTE Interactive Protocol Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close GitHub issue #575 by exposing QTE offers and active QTE scenes through a browser-compatible local JSON protocol while preserving existing console QTE behavior.

**Architecture:** Keep `QteSceneService` as the source of truth for QTE state, routing, terminal outcome application, and history. Add a thin `QteWebInteractionService` plus local endpoints that read/write the same `qte_offer.json` and `qte_runtime.json` files and return UI-neutral DTOs. The browser shell renders the protocol controls, but game-state mutations still flow through existing C# services.

**Tech Stack:** C#/.NET 8, ASP.NET Core minimal APIs, existing `QteSceneService`, `ExplorerCommandResult`-style DTOs, xUnit web-host tests.

---

### Task 1: Add QTE Web Endpoint Tests

**Files:**
- Modify: `BookOfEternityClient.Tests/LocalWebUiHostTests.cs`

- [ ] **Step 1: Add a test proving the browser can see a pending QTE offer.**

Add a test that writes `output/qte_offer.json`, calls `GET /api/qte/state`, and expects `state == "Offer"` with the offer id/title and an `accept` operation.

- [ ] **Step 2: Add a test proving browser accept and action submission advance QTE state.**

Write an offer with one `BranchChoice` action routing to a terminal outcome, call `POST /api/qte/offer` with `accept`, then `POST /api/qte/action` with the action id. Expect the runtime file to clear `activeScene`, history to contain the QTE id, and the response to report `state == "Completed"`.

- [ ] **Step 3: Run the focused tests and verify RED.**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests"
```

Expected before implementation: failures for missing `/api/qte/state` and `/api/qte/*` endpoints.

### Task 2: Add Service-Side QTE Interaction Primitives

**Files:**
- Modify: `BookOfEternityClient/Services/QteSceneService.cs`

- [ ] **Step 1: Add public methods for browser-safe state progression without console prompts.**

Add methods that:

- read runtime state;
- begin an accepted scene without entering the console loop;
- resolve one active action from an explicit action id and optional submitted grade;
- apply terminal outcomes through the existing validated state-application path;
- append QTE history and clear runtime state on terminal completion.

- [ ] **Step 2: Keep existing console behavior unchanged.**

Refactor `StartAcceptedSceneAsync` to call the new begin method and then continue into the existing `ExecuteActiveSceneAsync` console loop.

- [ ] **Step 3: Run QTE service tests.**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~QteSceneServiceTests"
```

Expected: existing QTE tests stay green.

### Task 3: Add Web QTE Protocol Service and Endpoints

**Files:**
- Create: `BookOfEternityClient/WebUi/QteWebInteractionService.cs`
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`

- [ ] **Step 1: Implement protocol DTOs and service.**

`QteWebInteractionService` builds these states:

- `NoScene`: no offer and no active runtime scene;
- `Offer`: `output/qte_offer.json` is present and parseable;
- `Active`: `qte_runtime.json.activeScene` has a current chapter;
- `Completed`: a submitted action resolved a terminal outcome;
- `Declined`: the offer was declined and decline marker recorded;
- `Failed`: malformed input or invalid runtime state.

- [ ] **Step 2: Register required QTE dependencies in the web host.**

Register `CharacteristicsService`, `ImageService`, `AudioService`, `StateDistributor`, `CanonicalStateNormalizer`, `QteSceneService`, and `QteWebInteractionService` in `LocalWebUiHost.Build`.

- [ ] **Step 3: Add endpoints.**

Add:

```text
GET  /api/qte/state
POST /api/qte/offer
POST /api/qte/action
```

`/api/qte/offer` accepts `{ "decision": "accept" | "decline" }`. `/api/qte/action` accepts `{ "actionId": "...", "grade": "success|partial|fail" }`; `BranchChoice` may ignore the submitted grade and use the offer's configured grade.

- [ ] **Step 4: Run focused web tests and verify GREEN.**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests"
```

### Task 4: Render QTE Controls in Browser Shell and Update Docs

**Files:**
- Modify: `BookOfEternityClient/WebUi/LocalWebUiHost.cs`
- Modify: `docs/web-ui/local-web-host.md`

- [ ] **Step 1: Add shell UI controls for QTE state.**

Add a browser button that calls `/api/qte/state`, renders offer accept/decline buttons, renders active chapter action buttons, and posts action grades for non-branch interactive checks.

- [ ] **Step 2: Update documentation.**

Add `#575` to tracked tasks and document the three QTE endpoints, protocol states, and the current limitation: browser timed mini-game widgets are protocol-ready but initially submit the grade through local UI controls rather than duplicating console `Console.ReadKey` mini-games.

- [ ] **Step 3: Run docs/web focused tests.**

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~LocalWebUiHostTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests"
```

### Task 5: Final Verification and Merge

**Files:**
- All changed files

- [ ] **Step 1: Run build.**

```powershell
dotnet build BookOfEternityClient\BookOfEternityClient.csproj --no-restore
```

- [ ] **Step 2: Run full test suite.**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

- [ ] **Step 3: Commit, merge to main, push, and close #575.**

Use a commit message tied to the tracked task, then merge into `main`, push, and add a GitHub issue comment with the verification commands.
