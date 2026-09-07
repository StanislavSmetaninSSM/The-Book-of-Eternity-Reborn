# Browser action contention test ownership Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Put the existing canonical-file/session-replacement contention test in its correct Integration owner while preserving its complete behavior and restoring the #1536 T081-B1 verification gate.

**Architecture:** Physically move one unchanged test class to Integration and add its class-level RegressionIntegration category. Extend the existing exact source/category guards and synchronize the two documented manifests; no production or runner change.

**Tech Stack:** C#, xUnit, existing source/category guards, PowerShell 7 bounded lanes.

## Global Constraints

- Tracked issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), T177 verification follow-through, applying the approved #1505 Fast taxonomy. Follow constitution and active #1536 spec/plan/tasks.
- Scope is exactly the moved BrowserPlayerActionGenerationTests source, the two existing boundary arrays, docs/testing.md and specs/1505-test-suite-performance/research.md. Parent owns this plan and #1536 acceptance/tracking.
- Preserve the full test body, method/namespace/class identity, hooks, unique temporary root, all assertions, four five-second waits and cleanup. The only source-text addition to the moved class is [Trait("Category", "RegressionIntegration")]. No production, shared helper, project, runner, timeout, dependency or test exclusion changes.
- Placement follows behavior: canonical file/restart/rollback/lifecycle/contention -> RegressionIntegration. A passing isolated run or a slow run does not establish an OS-level root cause or prove a production fix.
- Same worktree E:/Games/worktrees/boe-1536-wound-materialization and branch 1536-complete-wound-materialization. Use apply_patch with absolute paths. No remote operations, new branch/worktree, issue closure or session cleanup. Do not inspect, edit or stage unrelated .serena/.
- The implementer is the sole C# execution owner until reporting completion. PowerShell 7 and scripts/test-csharp.ps1 only, exact Focused selections, default five-minute bound. Parent runs the corrective Fast after the focused evidence is audited; no child Fast/PreMerge/full suite.
- Historical Fast failure remains exit1/2808 of2809 with one five-second preflight TimeoutException; do not relabel it green or call the race fixed. Final acceptance requires the relocated row, both ownership guards, parent Fast and independent scoped review.
- This is test placement only: no Mortal/afterlife GM capability, contract, command or UI behavior changes. No GM prompt/example/matrix/manifest/source-guard synchronization is needed.

### Task 1: Preserve the browser contention row under Integration ownership

**Files:**

- Move: BookOfEternityClient.Tests/WebUi/BrowserPlayerActionGenerationTests.cs -> BookOfEternityClient.IntegrationTests/WebUi/BrowserPlayerActionGenerationTests.cs.
- Modify: BookOfEternityClient.Tests/FastTestBoundaryTests.cs, ReviewedHeavySourcePaths only.
- Modify: BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs, RegressionIntegrationSources only.
- Modify: docs/testing.md, exact manifest counts and placement explanation only.
- Modify: specs/1505-test-suite-performance/research.md, current exact manifests/counts and placement explanation only; historical results remain unchanged.

**Interfaces:** Existing ReviewedHeavySources_ExistOnlyUnderIntegrationTests pins unique physical ownership; FileBackedRegressionIntegrationSources_MatchReviewedManifest pins the class-level category. The existing browser row continues to exercise real FileSystemManager/BrowserLocalWriteCoordinator/BrowserPlayerActionService, not substitutes.

- [x] **Step 1: Preserve the captured diagnostics.**

Parent audited actual summary/log/TRX for Fast artifact `20260908-043905-270-29108-443240a165e0415480a2922fbe3ed244-fast`: executed2809,passed2808,failed1; wall2:34.4254419, exit1, no lane timeout, build0/0, cleanup complete. The failed row is SubmitAsync_ConcurrentNewGameWaitsAndCannotKeepOldPendingAction, TimeoutException at line68 awaiting AfterPreflightAsync, before the replacement operation starts. Only two descriptor TRXs exist, so this is not a complete Fast inventory. The source path performs canonical-lease acquisition and file-backed preflight, and the test's own replacement/contention and publication checks are real filesystem integration.

Isolated unchanged-code diagnostic `20260908-044530-565-22424-9d89553bb425471d904056fd415f4303-focused` passed1/1; wall15.6770040s, row0.7747730s, exit0, clean build/cleanup, no timeout/duplicates/skips. It does not establish why the other execution exceeded five seconds. Do not repeat either diagnostic as a substitute for the ownership RED below.

- [ ] **Step 2: Add the Fast ownership requirement and observe the semantic RED.**

Apply only this first patch, then run the guard before moving the source:

```diff
*** Begin Patch
*** Update File: BookOfEternityClient.Tests/FastTestBoundaryTests.cs
@@
         Path.Combine("WebUi", "BrowserMortalWorldGenerationFencingTests.cs"),
+        Path.Combine("WebUi", "BrowserPlayerActionGenerationTests.cs"),
         Path.Combine("WebUi", "BrowserStorageTransportParityTests.cs"),
*** End Patch
```

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests.ReviewedHeavySources_ExistOnlyUnderIntegrationTests"
```

Expected: exactly1 failing ownership row, reporting the current Fast path instead of the required Integration path. A compilation/launch failure is not semantic RED.

- [ ] **Step 3: Move the complete source and synchronize exact ownership.**

Use apply_patch Move to so no body is retyped or lost. Resolve all patch headers against the absolute worktree.

```diff
*** Begin Patch
*** Update File: BookOfEternityClient.Tests/WebUi/BrowserPlayerActionGenerationTests.cs
*** Move to: BookOfEternityClient.IntegrationTests/WebUi/BrowserPlayerActionGenerationTests.cs
@@
 namespace BookOfEternityClient.Tests.WebUi;
 
+[Trait("Category", "RegressionIntegration")]
 public sealed class BrowserPlayerActionGenerationTests : IDisposable
*** Update File: BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
@@
         Path.Combine("WebUi", "BrowserMortalWorldGenerationFencingTests.cs"),
+        Path.Combine("WebUi", "BrowserPlayerActionGenerationTests.cs"),
         Path.Combine("WebUi", "BrowserStorageTransportParityTests.cs"),
*** Update File: docs/testing.md
@@
-The exact reviewed-heavy source/category manifest contains 66
+The exact reviewed-heavy source/category manifest contains 67
 `FastTestBoundaryTests.ReviewedHeavySourcePaths` entries, while the exact
-class-level Integration manifest contains 38
+class-level Integration manifest contains 39
 `IntegrationTestBoundaryTests.RegressionIntegrationSources` entries. Both are
 recorded in `specs/1505-test-suite-performance/research.md` and enforced by
 their respective boundary guards.
+
+`WebUi/BrowserPlayerActionGenerationTests.cs` is also owned by
+`RegressionIntegration`: its real canonical-file preflight, session replacement
+and lock-contention scenario is not a fixture-free unit test. The physical move
+preserves its single Fact and all assertions, waits and cleanup; it does not
+change the Fast limit, runner or production behavior.
*** Update File: specs/1505-test-suite-performance/research.md
@@
-### Exact executable manifests after #1536 scalar-course publication
+### Exact executable manifests after #1536 scalar-course publication and browser contention ownership
 
-The 66 entries below are the complete, ordinal contents of
+The 67 entries below are the complete, ordinal contents of
@@
 | `WebUi/BrowserMortalWorldGenerationFencingTests.cs` | `RegressionIntegration` |
+| `WebUi/BrowserPlayerActionGenerationTests.cs` | `RegressionIntegration` |
 | `WebUi/BrowserStorageTransportParityTests.cs` | `RegressionIntegration` |
@@
 is changed by this inventory synchronization.
+
+The subsequent #1536/T177 browser-action ownership correction adds the real
+canonical-file/session-replacement contention source to both exact manifests.
+Its single Fact, hooks, assertions, four five-second waits and cleanup remain
+unchanged; only physical ownership and the class-level RegressionIntegration
+trait change. The pre-move failed Fast and passing isolated diagnostic remain
+recorded in `docs/superpowers/plans/2026-09-08-browser-action-test-ownership.md`;
+neither is relabeled as proof of a production concurrency fix.
 
 The second executable array,
 `IntegrationTestBoundaryTests.RegressionIntegrationSources`, contains exactly
-these 38 ordinal entries after class-level ownership hardening:
+these 39 ordinal entries after class-level ownership hardening:
@@
 WebUi/BrowserMortalWorldGenerationFencingTests.cs
+WebUi/BrowserPlayerActionGenerationTests.cs
 WebUi/BrowserStorageTransportParityTests.cs
*** End Patch
```

- [ ] **Step 4: Prove both boundaries and the unchanged relocated behavior.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FastTestBoundaryTests.ReviewedHeavySources_ExistOnlyUnderIntegrationTests"
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~BrowserPlayerActionGenerationTests|FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest"
```

Expected:1/1 Fast guard;2/2 Integration (one original Fact plus one category guard). Verify actual discovery/TRX names, no duplicate or skipped row, build warnings/errors0, cleanup complete. If the original timeout recurs, stop and report it with artifacts instead of weakening waits/assertions or repeatedly rerunning. Parent will diagnose the retained failure separately.

- [ ] **Step 5: Verify preservation, commit only the scoped paths and report.**

```powershell
git diff --check
git diff --stat
git diff -- BookOfEternityClient.Tests/FastTestBoundaryTests.cs BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs docs/testing.md specs/1505-test-suite-performance/research.md
git add -- BookOfEternityClient.Tests/WebUi/BrowserPlayerActionGenerationTests.cs BookOfEternityClient.IntegrationTests/WebUi/BrowserPlayerActionGenerationTests.cs BookOfEternityClient.Tests/FastTestBoundaryTests.cs BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs docs/testing.md specs/1505-test-suite-performance/research.md
git diff --cached --find-renames
git commit -m "test: move browser action contention to integration (#1536)"
```

Before commit, compare the moved source after removing its one added Trait line to the pre-dispatch original, normalizing line endings only. It must match exactly. Count the two arrays and matching documentation entries:67 and39, including the inserted row once each. Report all RED/GREEN commands, exact artifact paths, executed/passed/failed/skipped/duplicate counts, wall time, build output and cleanup; no parent Fast success claim. Parent owns independent review and subsequent acceptance.

### Task 2: Restore the already-existing spiritual art Integration manifest entry

**Files:** Only BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs,
docs/testing.md and specs/1505-test-suite-performance/research.md. All other Global
Constraints remain binding. This evidence-based follow-through changes the final
Integration count from Task1's39 to40; Fast reviewed-heavy count stays67.

**Interfaces:** Existing FileBackedRegressionIntegrationSources_MatchReviewedManifest;
the existing SpiritualHealingArtValidationTests class and category are unchanged.

- [x] **Step1: Use the actual category RED and verify its cause.**

Task1 committed `4e9b7ceb`; moved browser Fact passed, but the category guard failed
with exactly one unreviewed source, SpiritualHealingArtValidationTests.cs, in
`20260908-045939-956-36892-f998f8cef0954bdcb6903ee707c3fb37-focused` (1/2,
wall57.5465613s,exit1,clean build/cleanup,no timeout/duplicates/skips). Parent read
summary/log/TRX, the full spiritual-art source, and pre-dispatch `02aa0dbf` source
and manifest: the Integration class/category existed from `f94aeafb` while its
manifest entry was absent. The class writes real entity/soul profiles and invokes
scoped validation, so its existing RegressionIntegration ownership is correct.
No runtime art behavior or test body correction is required; do not repeat RED.

- [ ] **Step2: Apply only the complete manifest/docs patch.**

```diff
*** Begin Patch
*** Update File: BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
@@
     private static readonly string[] RegressionIntegrationSources =
     [
         "AfterlifeSpiritualConflictValidationTests.cs",
+        "SpiritualHealingArtValidationTests.cs",
         "BrowserCommandPresentationAuditTests.cs",
*** Update File: docs/testing.md
@@
-class-level Integration manifest contains 39
+class-level Integration manifest contains 40
@@
 change the Fast limit, runner or production behavior.
+The Integration manifest also records the existing
+`SpiritualHealingArtValidationTests.cs` file-backed profile-validation owner;
+its category and all tests are unchanged.
*** Update File: specs/1505-test-suite-performance/research.md
@@
 neither is relabeled as proof of a production concurrency fix.
+The same category guard exposed one older missing manifest entry:
+`SpiritualHealingArtValidationTests.cs` already owned real file-backed profile
+validation in RegressionIntegration. Recording that existing owner changes no
+category, test body or gameplay and brings the second manifest to40 entries.
@@
-these 39 ordinal entries after class-level ownership hardening:
+these 40 ordinal entries after class-level ownership hardening:
@@
 AfterlifeSpiritualConflictValidationTests.cs
+SpiritualHealingArtValidationTests.cs
 BrowserCommandPresentationAuditTests.cs
*** End Patch
```

- [ ] **Step3: Prove the original two-row selection is green and commit/report.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~BrowserPlayerActionGenerationTests|FullyQualifiedName~IntegrationTestBoundaryTests.FileBackedRegressionIntegrationSources_MatchReviewedManifest"
git diff --check
git add -- BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs docs/testing.md specs/1505-test-suite-performance/research.md
git commit -m "test: record spiritual art integration ownership (#1536)"
```

Expected2/2 with the identical original Fact/guard names. No repeat of the already
green unchanged Fast source guard or art test suite is needed for a manifest-only
change. Inspect actual artifacts and report discovery/execution/pass/fail/skip/
duplicate counts, wall time, build output, cleanup, exact40-entry parity and commit.
On another failure, retain it and report rather than extending scope. Parent owns
full02aa0dbf..HEAD review, corrective Fast and final acceptance of both tasks.
