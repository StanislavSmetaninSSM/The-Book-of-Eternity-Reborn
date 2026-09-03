# Fast Lane Boundary Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the complete Fast project to stable ordinary feedback below its unchanged five-minute limit, preferably near three minutes, without deleting or filtering out coverage.

**Architecture:** Keep the existing physical project boundary as the source of truth. Fixture-free deterministic tests remain in `BookOfEternityClient.Tests`; canonical file/restart/rollback/contention workflows move to `BookOfEternityClient.IntegrationTests` with `RegressionIntegration`, real worker processes use `ProcessIntegration`, and complete host/browser flows use `E2E`. Exact relative-path/category manifests make drift fail closed.

**Tech Stack:** C# 12, .NET 8, xUnit, Roslyn source guards, PowerShell 7, `scripts/test-csharp.ps1`.

## Global Constraints

- Work only on existing branch `1536-complete-wound-materialization`; do not create another branch or worktree.
- GitHub issue #1551 tracks this test-infrastructure change; issue #1536 remains the parent gameplay work exposed by the failing Fast checkpoint.
- Fast remains the complete `BookOfEternityClient.Tests.csproj` with no category-exclusion filter, a five-minute hard limit, and at most two Fast hosts.
- Do not delete, skip, duplicate, or weaken any test row or assertion.
- Use PowerShell 7 and `scripts/test-csharp.ps1` for every C# test run; never invoke raw `dotnet test`.
- Run one test slot at a time and do not edit files while a lane is active.
- Preserve the untracked `.serena/` directory and stage only exact repository paths.
- Do not run PreMerge unless the user later explicitly requests merge preparation.
- This changes test placement only; production code, gameplay, canonical state, GM prompts/examples/manifests, browser UI, console UI, and afterlife runtime contracts remain unchanged.

---

### Task 1: Lock the #1551 Durable Contract

**Files:**
- Modify: `specs/1505-test-suite-performance/spec.md`
- Modify: `specs/1505-test-suite-performance/plan.md`
- Modify: `specs/1505-test-suite-performance/tasks.md`
- Modify: `specs/1505-test-suite-performance/research.md`
- Modify: `specs/1505-test-suite-performance/data-model.md`
- Modify: `specs/1505-test-suite-performance/quickstart.md`
- Create: `docs/superpowers/plans/2026-09-04-fast-lane-boundary-repair.md`

**Interfaces:**
- Consumes: approved design `docs/superpowers/specs/2026-09-04-fast-lane-boundary-repair-design.md` and GitHub issue #1551.
- Produces: requirements FR-018, success criteria SC-011/SC-012, and executable tasks T054-T064.

- [ ] **Step 1: Link #1551 and record the measured baseline**

Record 7,797 discovered Fast cases, four mixed descriptor walls of about 3:30/3:55/1:05/4:00 without build, the five-minute timeout, and the distinction between integration-heavy workflows and cheap detached contracts.

- [ ] **Step 2: Record the semantic routing contract**

Use this exact mapping:

```text
canonical file/restart/rollback/lifecycle/contention -> RegressionIntegration
real child process -> ProcessIntegration
complete HTTP host/browser flow -> E2E
fixture-free deterministic unit/parser/reducer/contract/source guard -> Fast
```

- [ ] **Step 3: Run a read-only Spec Kit consistency pass**

Check `spec.md`, `plan.md`, and `tasks.md` against the constitution. Expected: #1551 is linked in all three, every new requirement maps to T055-T064, and no gameplay/GM documentation work is claimed.

- [ ] **Step 4: Commit the planning artifacts**

```powershell
git add -- docs/superpowers/plans/2026-09-04-fast-lane-boundary-repair.md specs/1505-test-suite-performance/spec.md specs/1505-test-suite-performance/plan.md specs/1505-test-suite-performance/tasks.md specs/1505-test-suite-performance/research.md specs/1505-test-suite-performance/data-model.md specs/1505-test-suite-performance/quickstart.md
git commit -m "docs(tests): plan Fast boundary repair (#1551)"
```

---

### Task 2: Add RED Exact Boundary Guards

**Files:**
- Modify: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`

**Interfaces:**
- Consumes: project directories resolved through `TestRepoPaths.RepoRoot`.
- Produces: exact reviewed source ownership plus exact required category manifests.

- [ ] **Step 1: Make the Fast guard path-aware and add the mandatory sources**

Change the manifest to relative paths and include:

```csharp
private static readonly string[] ReviewedHeavySourcePaths =
[
    // existing entries,
    "MortalWoundRecoveryTests.cs",
    "MortalWoundTreatmentCapabilityAuthorityTests.cs",
    "QteSceneServiceTests.cs",
    "GmWorkerLiveSmokeTests.cs",
    "LocalWebUiSmokeTests.cs",
    Path.Combine("WebUi", "BrowserMortalWorldGenerationFencingTests.cs"),
    Path.Combine("WebUi", "BrowserStorageTransportParityTests.cs")
];
```

Enumerate matches by `Path.GetFileName(relativePath)` and require the only match to equal `Path.Combine(integrationRoot, relativePath)`.

- [ ] **Step 2: Add exact Integration category expectations**

Add the five canonical/QTE paths to `RegressionIntegrationSources`, add
`GmWorkerLiveSmokeTests.cs` with only `ProcessIntegrationTrait`, and add
`LocalWebUiSmokeTests.cs` with only `E2ETrait`.

- [ ] **Step 3: Run guards and retain RED evidence**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests.ReviewedHeavySources_ExistOnlyUnderIntegrationTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IntegrationTestBoundaryTests.ProcessAndE2ETestSources_MatchReviewedManifest|FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest"
```

Expected: failure names the still-Fast or absent Integration sources and their missing categories; no timeout or cleanup debt.

- [ ] **Step 4: Commit the RED guards**

```powershell
git add -- BookOfEternityClient.Tests/FastTestBoundaryTests.cs BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
git commit -m "test(tests): specify Fast integration boundaries (#1551)"
```

---

### Task 3: Move the Mandatory Canonical Group

**Files:**
- Move: `BookOfEternityClient.Tests/MortalWoundRecoveryTests.cs` -> `BookOfEternityClient.IntegrationTests/MortalWoundRecoveryTests.cs`
- Move: `BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityAuthorityTests.cs` -> `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentCapabilityAuthorityTests.cs`
- Move: `BookOfEternityClient.Tests/WebUi/BrowserMortalWorldGenerationFencingTests.cs` -> `BookOfEternityClient.IntegrationTests/WebUi/BrowserMortalWorldGenerationFencingTests.cs`
- Move: `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs` -> `BookOfEternityClient.IntegrationTests/WebUi/BrowserStorageTransportParityTests.cs`

**Interfaces:**
- Consumes: shared `BookOfEternityClient.TestSupport` fixtures and existing `BookOfEternityClient.Tests` namespaces.
- Produces: four Integration-owned sources, each with class-level `RegressionIntegration`.

- [ ] **Step 1: Verify exact source and destination paths**

Require every source to exist, every destination to be absent, and every resolved path to remain below the current worktree before using exact `git mv` commands.

- [ ] **Step 2: Move the files and add class traits**

Add immediately before each public test class:

```csharp
[Trait("Category", "RegressionIntegration")]
```

- [ ] **Step 3: Run exact focused Integration selections**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 15 -Filter "FullyQualifiedName~MortalWoundRecoveryTests|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~BrowserMortalWorldGenerationFencingTests|FullyQualifiedName~BrowserStorageTransportParityTests"
```

Expected: every previously green contour remains green; known deferred wound REDs retain their existing issue classification rather than becoming skipped or missing.

After the mandatory canonical group is green, record a Fast PlanOnly checkpoint
before continuing to the next coherent group:

```powershell
.\scripts\test-csharp.ps1 -Lane Fast -PlanOnly
```

- [ ] **Step 4: Commit the canonical group**

```powershell
git add -- BookOfEternityClient.Tests/MortalWoundRecoveryTests.cs BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityAuthorityTests.cs BookOfEternityClient.Tests/WebUi/BrowserMortalWorldGenerationFencingTests.cs BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs BookOfEternityClient.IntegrationTests/MortalWoundRecoveryTests.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentCapabilityAuthorityTests.cs BookOfEternityClient.IntegrationTests/WebUi/BrowserMortalWorldGenerationFencingTests.cs BookOfEternityClient.IntegrationTests/WebUi/BrowserStorageTransportParityTests.cs
git commit -m "test(tests): route canonical workflows to integration (#1551)"
```

---

### Task 4: Split Deterministic QTE Logic from Canonical Lifecycle

**Files:**
- Create: `BookOfEternityClient.Tests/QteDeterministicLogicTests.cs`
- Move/modify: `BookOfEternityClient.Tests/QteSceneServiceTests.cs` -> `BookOfEternityClient.IntegrationTests/QteSceneServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`

**Interfaces:**
- Consumes: static deterministic APIs on `QteKeyInput` and `QteSceneService`.
- Produces: fixture-free `QteDeterministicLogicTests` plus categorized file-backed `QteSceneServiceTests`.

- [ ] **Step 1: Extract the deterministic method set**

Move the methods from `QteKeyInput_NormalizesConsoleFallbackCharacters` through
`LockPinSetEffectiveRequirement_HigherDifficultyDoesNotMakeLockEasier`, together
with only their deterministic helper methods (`RepeatKey`, `Key`, grade/effective
requirement wrappers, input factories, windows/thresholds, and
`AssertStrictlyIncreasing`), into:

```csharp
public sealed class QteDeterministicLogicTests
{
    [Fact]
    public void QteKeyInput_LeavesUnsupportedCharactersUnmatched()
    {
        Assert.Null(QteKeyInput.NormalizeCharacter('ж'));
        Assert.False(QteKeyInput.MatchesConsoleKey(
            new ConsoleKeyInfo('ж', 0, false, false, false),
            ConsoleKey.Q));
    }
}
```

The class has no constructor, `IDisposable`, `FileSystemManager`, temporary root,
logger, host, or category trait.

- [ ] **Step 2: Move and categorize the remaining lifecycle class**

Keep methods starting at `EnsureRuntimeStateHealthyAsync_InvalidJsonFailsWithoutMutation`
and the canonical fixture/console/save helpers in `QteSceneServiceTests`, move
that file to Integration, and add:

```csharp
[Trait("Category", "RegressionIntegration")]
public sealed class QteSceneServiceTests : IDisposable
```

- [ ] **Step 3: Run the split inventory and both focused selections**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~QteDeterministicLogicTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -TimeoutMinutes 15 -Filter "FullyQualifiedName~QteSceneServiceTests"
```

Require the two result sets together to equal the pre-split 117 discovered
cases, with no duplicate fully-qualified test IDs.

After the QTE split is verified, record a Fast PlanOnly checkpoint before
continuing to the process and host smoke group:

```powershell
.\scripts\test-csharp.ps1 -Lane Fast -PlanOnly
```

- [ ] **Step 4: Commit the QTE split**

```powershell
git add -- BookOfEternityClient.Tests/QteDeterministicLogicTests.cs BookOfEternityClient.Tests/QteSceneServiceTests.cs BookOfEternityClient.IntegrationTests/QteSceneServiceTests.cs BookOfEternityClient.Tests/FastTestBoundaryTests.cs
git commit -m "test(qte): separate deterministic and lifecycle coverage (#1551)"
```

---

### Task 5: Move Real-Process and End-to-End Smoke Tests

**Files:**
- Move: `BookOfEternityClient.Tests/GmWorkerLiveSmokeTests.cs` -> `BookOfEternityClient.IntegrationTests/GmWorkerLiveSmokeTests.cs`
- Move: `BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs` -> `BookOfEternityClient.IntegrationTests/LocalWebUiSmokeTests.cs`

**Interfaces:**
- Produces: process lane ownership for the worker smoke and E2E ownership for the in-process HTTP/browser flow.

- [ ] **Step 1: Move and categorize the worker smoke**

```csharp
[Trait("Category", "ProcessIntegration")]
public sealed class GmWorkerLiveSmokeTests
```

- [ ] **Step 2: Move and categorize the web smoke**

```csharp
[Trait("Category", "E2E")]
public sealed class LocalWebUiSmokeTests : IDisposable
```

- [ ] **Step 3: Run exact focused Integration selections**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~GmWorkerLiveSmokeTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~LocalWebUiSmokeTests"
```

Expected: 2 worker tests and 3 web-host tests remain discovered; owned cleanup succeeds.

After the mandatory smoke group is verified, record a Fast PlanOnly checkpoint
before measuring headroom and considering the conditional second group:

```powershell
.\scripts\test-csharp.ps1 -Lane Fast -PlanOnly
```

- [ ] **Step 4: Commit the smoke-test moves**

```powershell
git add -- BookOfEternityClient.Tests/GmWorkerLiveSmokeTests.cs BookOfEternityClient.Tests/LocalWebUiSmokeTests.cs BookOfEternityClient.IntegrationTests/GmWorkerLiveSmokeTests.cs BookOfEternityClient.IntegrationTests/LocalWebUiSmokeTests.cs
git commit -m "test(tests): route process and web smokes (#1551)"
```

---

### Task 6: Measure Headroom and Route the Reviewed Second Group

**Files:**
- Modify: `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`
- Move conditionally: exact measured file-backed sources listed in Phase 7 of `specs/1505-test-suite-performance/plan.md`

**Interfaces:**
- Consumes: retained Fast PlanOnly and bounded run summaries.
- Produces: a Fast lane below four minutes before final two-run acceptance, or measured evidence that no second move is needed.

- [ ] **Step 1: Verify the mandatory boundary GREEN**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~IntegrationTestBoundaryTests.ProcessAndE2ETestSources_MatchReviewedManifest|FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest"
.\scripts\test-csharp.ps1 -Lane Fast -PlanOnly
```

- [ ] **Step 2: Run one Fast checkpoint**

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
```

If wall time is at most four minutes with no timeout, proceed to Task 7. If it
exceeds four minutes, use the retained TRX class ranking and move the complete
exact second group in the Spec Kit plan, including the required
`GmWorkerValidationRepairDelegatorTests.cs` real-worker source. Add every path
to both exact manifests before moving it, observe RED, then add class-level
`RegressionIntegration` or `ProcessIntegration` according to the source's
required category.

- [ ] **Step 3: Verify any second group**

Run one combined Focused Integration filter for the moved class names, then
Fast PlanOnly and one Fast checkpoint. Expected: all moved tests remain
discovered once and Fast is at most four minutes.

- [ ] **Step 4: Commit the measured second group**

```powershell
git add -- `
  BookOfEternityClient.Tests/ShiningCoreActionResolutionValidationTests.cs `
  BookOfEternityClient.IntegrationTests/ShiningCoreActionResolutionValidationTests.cs `
  BookOfEternityClient.Tests/GuardianCorrectionServiceTests.cs `
  BookOfEternityClient.IntegrationTests/GuardianCorrectionServiceTests.cs `
  BookOfEternityClient.Tests/ShiningBlessingEffectStateTests.cs `
  BookOfEternityClient.IntegrationTests/ShiningBlessingEffectStateTests.cs `
  BookOfEternityClient.Tests/AfterlifeNotificationStateTests.cs `
  BookOfEternityClient.IntegrationTests/AfterlifeNotificationStateTests.cs `
  BookOfEternityClient.Tests/DarenQteShowcaseTests.cs `
  BookOfEternityClient.IntegrationTests/DarenQteShowcaseTests.cs `
  BookOfEternityClient.Tests/ShiningTradeRequestStateTests.cs `
  BookOfEternityClient.IntegrationTests/ShiningTradeRequestStateTests.cs `
  BookOfEternityClient.Tests/BrowserLocalWriteCoordinatorTests.cs `
  BookOfEternityClient.IntegrationTests/BrowserLocalWriteCoordinatorTests.cs `
  BookOfEternityClient.Tests/TrainingServiceTests.cs `
  BookOfEternityClient.IntegrationTests/TrainingServiceTests.cs `
  BookOfEternityClient.Tests/NpcTradeServiceRequestFlowTests.cs `
  BookOfEternityClient.IntegrationTests/NpcTradeServiceRequestFlowTests.cs `
  BookOfEternityClient.Tests/MortalWoundOpportunityAdapterTests.cs `
  BookOfEternityClient.IntegrationTests/MortalWoundOpportunityAdapterTests.cs `
  BookOfEternityClient.Tests/ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs `
  BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs `
  BookOfEternityClient.Tests/WebUi/BrowserShiningRelicForgeParityTests.cs `
  BookOfEternityClient.IntegrationTests/WebUi/BrowserShiningRelicForgeParityTests.cs `
  BookOfEternityClient.Tests/WebUi/BrowserResidentInteractionsParityTests.cs `
  BookOfEternityClient.IntegrationTests/WebUi/BrowserResidentInteractionsParityTests.cs `
  BookOfEternityClient.Tests/WebUi/BrowserAfterlifeArchiveParityTests.cs `
  BookOfEternityClient.IntegrationTests/WebUi/BrowserAfterlifeArchiveParityTests.cs `
  BookOfEternityClient.Tests/WebUi/BrowserShiningIncarnationGatesParityTests.cs `
  BookOfEternityClient.IntegrationTests/WebUi/BrowserShiningIncarnationGatesParityTests.cs `
  BookOfEternityClient.Tests/WebUi/BrowserInkFeatherFateParityTests.cs `
  BookOfEternityClient.IntegrationTests/WebUi/BrowserInkFeatherFateParityTests.cs `
  BookOfEternityClient.Tests/WebUi/BrowserNpcSocialParityTests.cs `
  BookOfEternityClient.IntegrationTests/WebUi/BrowserNpcSocialParityTests.cs `
  BookOfEternityClient.Tests/WebUi/BrowserTradeParityTests.cs `
  BookOfEternityClient.IntegrationTests/WebUi/BrowserTradeParityTests.cs `
  BookOfEternityClient.Tests/GmWorkerValidationRepairDelegatorTests.cs `
  BookOfEternityClient.IntegrationTests/GmWorkerValidationRepairDelegatorTests.cs `
  BookOfEternityClient.Tests/FastTestBoundaryTests.cs `
  BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
git commit -m "test(tests): restore Fast runtime headroom (#1551)"
```

Before commit, inspect the staged names and abort if anything beyond the complete
exact second-group source moves (including the required worker delegator),
category attributes, and two boundary guards is staged.

---

### Task 7: Synchronize Documentation and Retain Acceptance Evidence

**Files:**
- Modify: `docs/testing.md`
- Modify: `specs/1505-test-suite-performance/research.md`
- Modify: `specs/1505-test-suite-performance/data-model.md`
- Modify: `specs/1505-test-suite-performance/quickstart.md`
- Modify: `specs/1505-test-suite-performance/tasks.md`

**Interfaces:**
- Consumes: final source/category manifests and retained result IDs.
- Produces: current contributor guidance and evidence-backed completion state.

- [ ] **Step 1: Document the semantic placement rule and Focused command**

State explicitly that Fast is not a synonym for every test in the repository;
it is the entire physically isolated fast project. Document
`-FocusedProject Integration` for moved classes and keep the Fast timeout at
five minutes.

- [ ] **Step 2: Run changed diagnostic lanes**

```powershell
.\scripts\test-csharp.ps1 -Lane RegressionIntegration
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
```

Run only categories present in the final moved set. Classify known intentional
wound REDs by exact test and owning task; do not skip them or call the lane
green if official failures remain.

- [ ] **Step 3: Run exactly two final Fast controls**

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
.\scripts\test-csharp.ps1 -Lane Fast
```

Expected for both: elapsed time below five minutes, preferably near three;
timeout false; zero duplicate IDs; complete owned-tree cleanup.

- [ ] **Step 4: Record exact evidence and task status**

Add run IDs, counts, wall times, timeout/duplicate/cleanup fields, moved source
manifest, and any classified intentional REDs. Mark T055-T063 complete only
from inspected diffs and executable evidence.

- [ ] **Step 5: Commit documentation**

```powershell
git add -- docs/testing.md specs/1505-test-suite-performance/research.md specs/1505-test-suite-performance/data-model.md specs/1505-test-suite-performance/quickstart.md specs/1505-test-suite-performance/tasks.md
git commit -m "docs(tests): record repaired Fast lane evidence (#1551)"
```

---

### Task 8: Review Without Merge-Only Verification

**Files:**
- Review: all #1551 diffs on the current branch

**Interfaces:**
- Consumes: committed implementation and retained lane summaries.
- Produces: reviewed branch ready to resume issue #1536.

- [ ] **Step 1: Run structural checks**

```powershell
git status --short
git diff --check 3dbf0572...HEAD
git diff --stat 3dbf0572...HEAD
```

- [ ] **Step 2: Run Spec Kit consistency analysis**

Confirm FR-013/FR-018 and SC-011/SC-012 map to T055-T064 and the final evidence.
Resolve every critical/high inconsistency before continuing.

- [ ] **Step 3: Perform independent code review**

Review test ownership, exact category manifests, QTE test inventory, project
dependency direction, process cleanup, duplicate detection, and docs. Resolve
all Critical or Important findings with the smallest focused rerun.

- [ ] **Step 4: Mark T064 complete and resume #1536**

Do not push, open/merge a PR, close #1551, or run PreMerge in this task. Those
actions require the user's later explicit integration request.
