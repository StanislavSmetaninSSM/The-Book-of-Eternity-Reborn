# Offscreen Location Storage Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve every accepted location-storage item across same-location refreshes and movement between locations without trusting GM-authored contents, duplicating carriers, or changing logical item identity.

**Architecture:** Add a closed client-owned `game_state/world/location_storage_contents.json` document for non-current storage contents. The location planner atomically swaps contents between that document and `current_location.json`; both physical representations map to the same logical `location_storage(locationId, storageId)` carrier, while `world_map.json` remains metadata-only. Raw and canonical validation, item catalogs, snapshots, rollback, and local transition validation all read the new authority, but player projections and GM repair packets never do.

**Tech Stack:** C# 12, .NET 8, `System.Text.Json.Nodes`, xUnit, PowerShell 7, `scripts/test-csharp.ps1`, existing coordinated canonical-write/snapshot infrastructure.

## Global Constraints

- GitHub issue #1513 and Spec Kit feature `specs/1520-complete-location-materialization/` remain the task/spec authority.
- Use ordinal exact identities; case, whitespace, Unicode-normalization, and confusable aliases fail closed.
- The GM never authors, repairs, or receives `location_storage_contents.json` as an editable target.
- `world_map.json` contains storage metadata only; no item contents.
- Exactly one physical occurrence exists for every active item, while its logical carrier remains `location_storage(locationId, storageId)` across current/offscreen relocation.
- Location repair never creates, seals, moves between logical carriers, or repairs an item.
- Every production change follows RED → GREEN; verification uses bounded PowerShell lanes only.

---

### Task 1: Closed offscreen storage state contract

**Files:**
- Create: `BookOfEternityClient/Services/MortalLocationStorageContentsState.cs`
- Create: `BookOfEternityClient.Tests/MortalLocationStorageContentsStateTests.cs`

**Interfaces:**
- Produces: `MortalLocationStorageKey(string LocationId, string StorageId)`.
- Produces: `MortalLocationStorageContentsParseResult` with canonical root, exact entry index, and validation issues.
- Produces: `MortalLocationStorageContentsState.StatePath`, `CreateEmptyRoot()`, `Parse(JsonObject?)`, and `BuildCanonicalRoot(...)`.
- Ordering: entries sort by ordinal `locationId`, then ordinal `storageId`; item order is unchanged.

- [ ] **Step 1: Write the failing parser/serializer tests**

```csharp
[Fact]
public void Parse_AcceptsOneExactNonEmptyOffscreenCoordinate()
{
    var item = MortalItemTestFixture.CreateCanonicalRoot("itm_offscreen");
    var root = new JsonObject
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(new JsonObject
        {
            ["locationId"] = "loc_remote",
            ["storageId"] = "storage_chest",
            ["contents"] = new JsonArray(item)
        })
    };

    var parsed = MortalLocationStorageContentsState.Parse(root);

    Assert.Empty(parsed.Issues);
    var contents = Assert.Single(parsed.Entries).Value;
    Assert.Equal("itm_offscreen", contents[0]!["itemId"]!.GetValue<string>());
}

```

Add four separate tests with complete roots and exact bounded codes:

- two identical coordinates → `mortal_location_storage_contents_coordinate_duplicate`;
- `loc_remote` and `LOC_REMOTE` with the same storage identity → `mortal_location_storage_contents_coordinate_confusable`;
- an empty `contents[]` entry → `mortal_location_storage_contents_empty_entry`;
- `schemaVersion: 2` or any extra root property → `mortal_location_storage_contents_invalid_root`.

- [ ] **Step 2: Run the unit RED**

Run:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationStorageContentsStateTests"
```

Expected: build/test failure because `MortalLocationStorageContentsState` does not exist.

- [ ] **Step 3: Implement the minimal closed contract**

```csharp
internal readonly record struct MortalLocationStorageKey(string LocationId, string StorageId);

internal sealed record MortalLocationStorageContentsParseResult(
    JsonObject Root,
    IReadOnlyDictionary<MortalLocationStorageKey, JsonArray> Entries,
    IReadOnlyList<ValidationIssue> Issues);

internal static class MortalLocationStorageContentsState
{
    internal const string StatePath = "game_state/world/location_storage_contents.json";
    internal static JsonObject CreateEmptyRoot() => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray()
    };

    internal static MortalLocationStorageContentsParseResult Parse(JsonObject? root);
    internal static JsonObject BuildCanonicalRoot(
        IReadOnlyDictionary<MortalLocationStorageKey, JsonArray> entries);
}
```

`Parse` treats `null` as `CreateEmptyRoot()`, requires exactly
`schemaVersion`/`entries` at the root and exactly
`locationId`/`storageId`/`contents` per entry, indexes only entries whose
coordinate and non-empty array are valid, and retains every validation issue.
`BuildCanonicalRoot` rejects empty arrays, deep-clones every item array, and
sorts by the two ordinal key components before constructing `entries[]`.

Use `MortalItemIdentityRules.IsExactIdentity` and `MortalLocationIdentityState.BuildConfusableKey`; never trim or normalize a stored identity.

- [ ] **Step 4: Run the unit GREEN**

Run the command from Step 2. Expected: every `MortalLocationStorageContentsStateTests` case passes, no cleanup/duplicate failures.

- [ ] **Step 5: Commit the contract slice**

```powershell
git add -- BookOfEternityClient/Services/MortalLocationStorageContentsState.cs BookOfEternityClient.Tests/MortalLocationStorageContentsStateTests.cs
git commit -m "feat: add offscreen location storage state (#1513)"
```

### Task 2: Planner-owned current/offscreen relocation

**Files:**
- Modify: `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlan.cs`
- Modify: `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.GovernedCommands.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Lifecycle.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.StorageThreats.cs`

**Interfaces:**
- `MortalLocationAcceptedTurnInput` gains optional `JsonObject? PreTurnStorageContents`.
- `MortalLocationAcceptedTurnPlan` gains `JsonObject FinalStorageContents`.
- Planner returns the state path in `TouchedPaths` only when the authority must be created or its canonical content changes.

- [ ] **Step 1: Write two failing movement-preservation tests**

```csharp
[Fact]
public void Lifecycle_StorageItems_SameLocationMinimalRefreshPreservesAcceptedCurrentStorageItems()
{
    var accepted = CreateAcceptedDirectedTopology(includeLink: false);
    var item = MortalItemTestFixture.CreateCanonicalRoot("itm_same_location_storage");
    AttachCurrentStorage(accepted.FinalWorldMap, accepted.FinalCurrentLocation, "storage_source", item);

    var result = Build(
        accepted.FinalWorldMap,
        accepted.FinalCurrentLocation,
        accepted.FinalIdentityIndex,
        rawCurrentLocationData: MinimalMove(accepted.SourceLocationId),
        preTurnStorageContents: MortalLocationStorageContentsState.CreateEmptyRoot());

    var plan = AssertSuccessfulPlan(result);
    AssertStorageContainsExactly(plan.FinalCurrentLocation!, "storage_source", item);
    Assert.Empty(plan.FinalStorageContents["entries"]!.AsArray());
}

[Fact]
public void Lifecycle_StorageItems_ChangedLocationAtomicallyParksSourceAndHydratesTargetStorageItems()
{
    var accepted = CreateAcceptedDirectedTopology();
    var sourceItem = MortalItemTestFixture.CreateCanonicalRoot("itm_source_storage");
    var targetItem = MortalItemTestFixture.CreateCanonicalRoot("itm_target_storage");
    AttachCurrentStorage(accepted.FinalWorldMap, accepted.FinalCurrentLocation, "storage_source", sourceItem);
    var offscreen = OffscreenEntry(accepted.TargetLocationId, "storage_target", targetItem);

    var result = Build(
        accepted.FinalWorldMap,
        accepted.FinalCurrentLocation,
        accepted.FinalIdentityIndex,
        rawCurrentLocationData: MinimalMove(accepted.TargetLocationId),
        preTurnStorageContents: offscreen);

    var plan = AssertSuccessfulPlan(result);
    AssertStorageContainsExactly(plan.FinalCurrentLocation!, "storage_target", targetItem);
    AssertOffscreenContainsExactly(plan.FinalStorageContents, accepted.SourceLocationId, "storage_source", sourceItem);
    Assert.DoesNotContain(FindLocation(plan.FinalWorldMap, accepted.SourceLocationId)["locationStorages"]!.AsArray().OfType<JsonObject>(), s => s.ContainsKey("contents"));
}
```

- [ ] **Step 2: Run the planner RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests.Lifecycle_StorageItems"
```

If VSTest cannot express the conjunction as intended, use a unique shared method-name prefix and filter on that prefix. Expected: same-location loses `contents`; changed-location has no offscreen output.

- [ ] **Step 3: Implement one atomic relocation helper inside the planner**

Add this exact private interface:

```csharp
private static JsonObject ReconcileLocationStorageContents(
    JsonObject finalWorldMap,
    JsonObject? preTurnCurrent,
    JsonObject? preTurnOffscreen,
    string? selectedLocationId,
    JsonObject? finalCurrent,
    List<ValidationIssue> issues)
```

It parses the offscreen state, rejects unknown storage coordinates, rejects a pre-existing offscreen entry for the selected pre-turn location, parks non-empty source-current contents when selection changes, removes/hydrates target entries, and validates that every remaining coordinate resolves to exactly one storage metadata object. `CreateCurrentProjection` must take contents only from this client-owned result, never from `existingSelection.RawSelection`.

- [ ] **Step 4: Forbid item contents in existing movement payloads**

Add a bounded protected issue such as `mortal_location_movement_item_contents_forbidden` whenever an existing selection contains `locationStorages[].contents`. Reject the entire GM-authored `locationStorages` array on existing movement, including metadata-only echoes: storage metadata already comes from the validated canonical destination, and all physical item contents are restored from client-owned current/offscreen authority.

- [ ] **Step 5: Extend storage-removal protection to offscreen contents**

`storagesToRemove` must reject a coordinate when either pre-turn current or parsed pre-turn offscreen authority contains a non-empty item array. Metadata updates preserve the matching contents automatically.

- [ ] **Step 6: Run planner/storage GREEN**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests&Name~Storage"
```

Expected: new preservation tests and existing governed storage tests pass.

- [ ] **Step 7: Commit the planner slice**

```powershell
git add -- BookOfEternityClient/Services/MortalLocationAcceptedTurnPlan.cs BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.GovernedCommands.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Lifecycle.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.StorageThreats.cs
git commit -m "fix: preserve location storage items during movement (#1513)"
```

### Task 3: Full item catalog, raw authority, publishing, and rollback

**Files:**
- Modify: `BookOfEternityClient/Services/MortalItemCarrierCatalog.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.MortalLocationMaterialization.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- Modify: `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`
- Modify: `BookOfEternityClient.Tests/MortalItemCarrierCatalogTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationTestContext.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Lifecycle.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationLifecycleTests.cs`

**Interfaces:**
- `MortalItemCarrierCatalogInput` gains optional `JsonObject? OffscreenLocationStorageContents = null` at the end to avoid breaking positional callers.
- Catalog scans current plus offscreen storage arrays into the same logical carrier coordinate and reports duplicates normally.
- Normalizer publishes map/current/index/offscreen under the existing coordinated before-image boundary.

- [ ] **Step 1: Write failing catalog and authority tests**

Add tests proving:

```csharp
// one offscreen canonical item resolves to location_storage(loc_remote, storage_chest)
// the same item in current and offscreen is a duplicate occurrence
// unknown/confusable coordinate state is rejected
// GM add/remove/change of the client-owned file relative to the pending snapshot fails closed
```

- [ ] **Step 2: Write failing accepted-turn integration tests**

Create both same-location and changed-location fixtures with canonical receipt-bearing items and matching `item_identity_index.json` entries. Assert:

```csharp
Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(), IsError);
await context.NormalizeAndValidateAsync();
Assert.DoesNotContain(await context.Validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(), IsError);
AssertExactlyOnePhysicalOccurrenceAcrossCurrentAndOffscreen(itemId);
AssertIndexCarrierUnchanged(itemId, "location_storage", locationId, storageId);
```

Add a forced publication failure for `location_storage_contents.json` and assert every rollback-tracked file is byte-exact/existence-exact to the pre-turn baseline.

- [ ] **Step 3: Run the integration RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterialization&Name~StorageItem"
```

Expected: raw continuity reports a disappeared item and/or canonical validation reports a missing active occurrence; forced-write test cannot name the new path.

- [ ] **Step 4: Extend the carrier catalog and raw continuity view**

Scan `entries[].contents[]` with file/json paths from `MortalLocationStorageContentsState.StatePath`, but emit `MortalItemCarrierCoordinate("location_storage", locationId, storageId, [])`. During an existing movement wrapper, raw item validation builds its effective source view from the validated pre-turn current/offscreen authority rather than requiring GM to re-echo `contents`. Raw creation routes continue to scan the actual raw current scene.

- [ ] **Step 5: Protect the client-owned document in snapshot/raw validation**

Add the state path to `CanonicalAccumulatedFiles` (therefore pending snapshots and rollback tracking). Compare existence plus parsed canonical content against the validated pre-turn snapshot before planning. Emit a client-owned/protected issue and no repair packet for add/remove/change; formatting-only rewrites are equivalent.

- [ ] **Step 6: Publish and validate the composed state**

Load pre-turn storage state into `MortalLocationAcceptedTurnInput`; write `plan.FinalStorageContents` only when `TouchedPaths` contains the state path. Include the offscreen root in item normalizer catalogs and in `MortalItemTransitionWriter` composed-state validation, while leaving `ResolveCarrierArray`/player local actions limited to `current_location.json`.

- [ ] **Step 7: Run focused GREEN and rollback matrix**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemCarrierCatalogTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterialization&Name~StorageItem"
pwsh .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
```

Expected: all selected tests pass; lifecycle summary reports zero failures/duplicates and cleanup complete.

- [ ] **Step 8: Commit the authority/publication slice**

```powershell
git add -- BookOfEternityClient/Services BookOfEternityClient.Tests/MortalItemCarrierCatalogTests.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationTestContext.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Lifecycle.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationLifecycleTests.cs
git commit -m "fix: validate offscreen storage item authority (#1513)"
```

### Task 4: Spec Kit, GM guidance, fixtures, and privacy/source guards

**Files:**
- Modify: `specs/1520-complete-location-materialization/spec.md`
- Modify: `specs/1520-complete-location-materialization/plan.md`
- Modify: `specs/1520-complete-location-materialization/tasks.md`
- Modify: `specs/1520-complete-location-materialization/research.md`
- Modify: `specs/1520-complete-location-materialization/data-model.md`
- Modify: `specs/1520-complete-location-materialization/quickstart.md`
- Modify: `specs/1520-complete-location-materialization/contracts/mortal-location-routes-and-topology.md`
- Modify: `specs/1520-complete-location-materialization/contracts/mortal-location-player-projection.md`
- Modify: relevant `Rules/Block_2.txt`, `Rules/Block_20.txt`, `CLI_API_Specification.md`, and worked examples only where GM behavior must explicitly omit item contents.
- Modify: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.Tests/MortalLocationPlayerProjectionTests.cs`

**Interfaces:**
- No new GM-authored command or field; documentation says movement never echoes storage item contents.
- Player projection tests prove the new client-owned file/path/DTO cannot appear in console/browser/news output.

- [ ] **Step 1: Add RED documentation/source guards**

Require the guidance to say that existing movement sends only exact selection plus operational fields, never `contents`, and forbid `location_storage_contents`, `entries`, or offscreen item payloads in GM examples/repair targets.

- [ ] **Step 2: Run documentation RED**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests.MortalLocationMaterializationGuidance"
```

Expected: failure until the guidance and worked example are synchronized.

- [ ] **Step 3: Update durable artifacts and guidance**

Replace Decision 9 with the current/offscreen single-carrier model; document the closed state shape, ownership, atomic swap, legacy-missing behavior, lifecycle removal guard, rollback, and privacy. Add explicit task/evidence rows for the two movement regressions and failure injection.

- [ ] **Step 4: Add projection/privacy regressions**

Feed a canonical map/current pair plus a serialized offscreen authority/DTO nested into setting-specific data and assert neither client exposes the state path, coordinate IDs, raw item array, receipt, or authority vocabulary. Preserve adjacent legitimate in-world storage semantics.

- [ ] **Step 5: Run docs/projection GREEN and FullValidation**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests.MortalLocationMaterializationGuidance|FullyQualifiedName~MortalLocationPlayerProjectionTests"
pwsh .\scripts\test-csharp.ps1 -Lane FullValidation
```

- [ ] **Step 6: Commit synchronized contracts**

```powershell
git add -- specs/1520-complete-location-materialization docs/superpowers/specs/2026-08-12-mortal-location-materialization-design.md Rules CLI_API_Specification.md Examples BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs BookOfEternityClient.Tests/MortalLocationPlayerProjectionTests.cs
git commit -m "docs: synchronize location storage continuity contract (#1513)"
```

### Task 5: Final integration, review, and PR gate

**Files:**
- Review all files in `git diff origin/main...HEAD` plus remaining intended dirty #1513 paths.
- Update: `specs/1520-complete-location-materialization/tasks.md` and `quickstart.md` with exact result artifacts.

- [ ] **Step 1: Run the smallest final focused controls and one Fast checkpoint**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"
pwsh .\scripts\test-csharp.ps1 -Lane Fast
```

Record exact summary paths/counts; classify any external flaky failure with isolated evidence rather than changing unrelated code.

- [ ] **Step 2: Reconcile the feature branch**

Commit every intended #1513 dirty path, fetch, and rebase `1520-complete-location-materialization` onto current `origin/main`. Resolve only overlaps supported by both features; preserve #1526's 20-minute PreMerge contract and publication changes.

- [ ] **Step 3: Run post-rebase verification**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"
pwsh .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
```

- [ ] **Step 4: Perform final code review and close only evidence-backed tasks**

Check authority, exact identity, single physical occurrence, rollback after each planned write, recursive privacy, console/browser parity, and synchronized GM docs/examples. Do not mark T102 or issue #1513 complete from an agent report alone.

- [ ] **Step 5: Run exactly one final PreMerge**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane PreMerge
```

Expected: exit 0, timeout false under the inherited 20-minute limit, zero failed/duplicate tests, ProcessIntegration and E2E complete, cleanup complete.

- [ ] **Step 6: Push and open a PR; do not self-merge**

Push the feature branch, create/update the PR linked to #1513, attach exact verification artifacts and the no-afterlife-contract-change rationale, and wait for the repository owner's approval.
