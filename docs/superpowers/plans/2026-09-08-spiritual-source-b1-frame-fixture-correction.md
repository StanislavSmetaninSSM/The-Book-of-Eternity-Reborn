# Spiritual source B1 ConflictFrame fixture correction implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. The parent SDD controller owns application, verification, review, and the eventual combined commit.

**Goal:** Restore the three existing `ConflictFrame_` rows without weakening B1 original spiritual-source authority or any detached-frame assertion.

**Architecture:** Complete genuine raw/common publication against the original signed dice/settings first. Construct the absent/empty-dice frame condition afterward by changing only test manifest metadata while retaining the original pre-exchange snapshot bytes; construct the settings condition before raw validation by placing the required audit in both proposed exchange copies. Extract the existing raw/peek/normalizer publication tail once so the settings fixture can prepare a valid proposal without duplicating production orchestration.

**Tech Stack:** C#/.NET 8, xUnit 2.9, `System.Text.Json.Nodes`, existing `PendingTurnSnapshotTestAuthority`, PowerShell 7 bounded C# runner.

## Global Constraints

- Source issue/task: GitHub #1536, T081-B2C-J2-B1. Exact worktree/branch/base: `E:/Games/worktrees/boe-1536-wound-materialization`, `1536-complete-wound-materialization`, `4044e8cf6a0f068127019195c6800ec3afaa2a7e`.
- Modify only `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs`. The nine uncommitted B1 source files and parent-owned planning documents remain frozen; `.serena/` is unrelated.
- No production source, GM prompt, documentation, example, manifest, spec, index, HEAD, remote, or cleanup change. Do not add a current/default dice or settings fallback to B1 and do not make a diagnostic frame spiritual-source authority.
- Preserve all 26 existing `AfterlifeResourceCutoverTests.ConflictFrame_` rows and all final assertions. Do not recapture snapshot files after publication; the signed conflict baseline must remain pre-exchange so the changed-live-die assertion cannot become a historical false-green.
- Use only the exact 3-row Focused Integration correction control, then the same 26-row Focused Integration cohort, both at the default five-minute limit. Do not rerun the already-green 60-row source, 20-row pure, or four-row re-trauma controls.
- Preserve every run artifact. Stop on the first unexpected build/test failure and diagnose it before any further edit or lane.

---

## File map

| File | Responsibility |
|---|---|
| `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs` | Reorder the two failing fixture setups, extract the existing publication tail, and add a test-local manifest-only dice rewrite with an explicit pre-exchange snapshot assertion. |
| `docs/superpowers/plans/2026-09-08-spiritual-source-b1-frame-fixture-correction.patch` | Complete reviewed `apply_patch` companion; metadata only until the parent applies it. |

### Task 1: Correct the three diagnostic fixtures without changing runtime authority

**Files:**
- Modify: `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs:97-122`
- Modify: `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs:371-403`
- Modify: `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs:597-604`

**Interfaces:**
- Consumes: `WriteCompleteConflictFrameExchangeAsync(ResourceMaterializationTestContext)`, `PeekPlanAsync(ResourceMaterializationTestContext)`, `PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(JsonObject)`, and `PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(FileSystemManager, bool = true)`.
- Produces: test-local `PublishPreparedCompleteConflictFrameAsync(ResourceMaterializationTestContext)` and `RewritePublishedFrameManifestDiceForDiagnosticAsync(ResourceMaterializationTestContext, bool)`; no production API.

- [ ] **Step 1: Confirm the retained RED and untouched boundary**

Do not rerun it. Inspect retained artifact `TestResults/test-lanes/20260908-210752-660-18096-dc78a01fcd6b49c7b8c1a35d96e3a472-focused`: 26 executed, 23 passed, and only the two `ConflictFrame_SignedAbsentOrEmptyDiceUseCapturedLiveFallback` rows plus `ConflictFrame_CapturedImagesSurviveAllFilesystemInputsChangingWithoutReads` failed during `PublishCompleteConflictFrameAsync`. Confirm `git rev-parse HEAD` remains the base above and `git status --short` contains only the known nine B1 files, two parent planning files, and unrelated `.serena/` before application.

- [ ] **Step 2: Apply the exact companion**

Apply only `spiritual-source-b1-frame-fixture-correction.patch`. Its three unique hunks make these exact changes:

1. The settings test calls `WriteCompleteConflictFrameExchangeAsync`, adds one detached `difficultyAudit` object to `response.exchange.diceAudit` and a deep clone to `response.activeConflictAfter.exchangeLog[0].diceAudit`, writes the proposal, then calls the extracted publication tail.
2. The absent/empty-dice theory completes genuine publication while `[15,5,12,8]` remains original authority, then calls the manifest-only diagnostic helper before writing the live fallback request.
3. `PublishPreparedCompleteConflictFrameAsync` contains exactly the former raw validation, `PeekPlanAsync`, lease, and normalizer steps. `RewritePublishedFrameManifestDiceForDiagnosticAsync` removes or empties only `preGeneratedDices1d20`, recomputes the existing test manifest hash, synchronizes detached test authority, and asserts the signed conflict snapshot's original `exchangeLog` is empty.

The complete new helper bodies are:

```csharp
private static async Task PublishPreparedCompleteConflictFrameAsync(
    ResourceMaterializationTestContext context)
{
    AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
    var plan = await PeekPlanAsync(context);
    await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
    Assert.Same(plan, await context.Normalizer.BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
}

private static async Task RewritePublishedFrameManifestDiceForDiagnosticAsync(
    ResourceMaterializationTestContext context,
    bool emptyDice)
{
    var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(FrameManifestPath));
    if (emptyDice)
        manifest["preGeneratedDices1d20"] = new JsonArray();
    else
        Assert.True(manifest.Remove("preGeneratedDices1d20"));
    manifest["manifestPayloadHash"] =
        PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
    await context.WriteExactJsonAsync(FrameManifestPath, manifest.ToJsonString());
    await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(context.FileSystem);

    var files = Assert.IsType<JsonObject>(manifest["files"]);
    var originalConflictPath = files[AfterlifeSpiritualConflictState.StatePath]!.GetValue<string>();
    var originalConflict = Assert.IsType<JsonObject>(
        await context.ReadJsonAsync(originalConflictPath));
    Assert.Empty(originalConflict["activeConflict"]!["exchangeLog"]!.AsArray());
}
```

- [ ] **Step 3: Inspect the one-file diff before execution**

Run:

```powershell
git diff --check -- BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs
git diff --stat -- BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs
git diff -- BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.ConflictFrameValidation.cs
```

Expected: one modified Integration test file, companion-equivalent `37` insertions and `5` deletions (net `+32`), no whitespace errors, no source or assertion removal beyond the obsolete fixture ordering. Confirm the explicit original-snapshot `Assert.Empty(...exchangeLog...)` is present and no call to `CaptureValidatedPendingSnapshotAsync` remains in the absent/empty-dice theory.

- [ ] **Step 4: Run the minimal corrected control**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_SignedAbsentOrEmptyDiceUseCapturedLiveFallback|FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_CapturedImagesSurviveAllFilesystemInputsChangingWithoutReads"
```

Expected: exactly `3/3` discovered/executed/pass, build warnings/errors `0/0`, no timeout, no duplicate IDs, successful owned-tree cleanup. Inspect `summary.json`, the log, and TRX before continuing.

- [ ] **Step 5: Run the unchanged complete frame cohort**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests.ConflictFrame_"
```

Expected: the same `26/26` IDs as retained artifact `20260908-210752-660-18096-dc78a01fcd6b49c7b8c1a35d96e3a472-focused`, all passing, build warnings/errors `0/0`, no timeout/duplicates, successful cleanup. This must retain offline live fallback, signed-input capture, no-further-read replay, snapshot tamper, wrapper equivalence, and all final changed-die assertions.

- [ ] **Step 6: Hand back for parent review and combined commit**

Re-run the three read-only diff commands from Step 3, inspect `git status --short` and `git rev-parse HEAD`, and report exact artifact paths/counts/timings. Do not commit from this correction task. The parent reviews the one-file correction and coordinates one eventual ten-file commit containing the frozen nine B1 source files plus this Integration test guard. Because this is test-only and changes no runtime or GM-authored contract, no GM synchronization or FullValidation is required.

## Controller self-review

- Spec coverage: one file and one coherent fixture correction cover all three failures; no runtime/source/GM surface is added.
- Placeholder scan: no incomplete code or deferred implementation instruction is present.
- Type/order check: both helpers use existing test-visible types; the source-valid publication precedes manifest-only dice mutation; settings audit is present in both proposal copies before the extracted publication tail; original snapshot files are never recaptured.
- Mechanical companion replay against the unchanged current file: all three old hunks match exactly once; replay succeeds to a 687-line postimage with SHA-256 `A6E7B1F3AA587A12B896BBFFB2A524155D4AC02B99EC6EF6C8E55ECCB9E78BCE` (LF-normalized replay bytes).
