# Complete Mortal Item Materialization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every durable ordinary Mortal World item enter canonical state through one complete GM-authored materialization package, receive immutable client-owned identity evidence, and preserve that identity through transfers, stacks, repair, and player-facing projections.

**Architecture:** Keep semantic item objects in their existing player, NPC, storage, or already-valid vehicle carrier. Add an embedded immutable GM envelope, an embedded client receipt, and a client-owned `game_state/inventory/item_identity_index.json`; one-pass catalogs validate global identity and one coordinated writer commits carrier/index transitions under a canonical lease. Raw accepted-turn validation runs before sealing, item normalization assigns permanent IDs and rewrites `creationRef` references, and post-seal validation rejects any receipt-less or multiply-carried item.

**Tech Stack:** C# 12, .NET 8, `System.Text.Json`/`JsonNode`, xUnit 2.9.2, existing `FileSystemManager.CanonicalWriteLease`, `CoordinatedStateWriteHelper`, `CanonicalStateNormalizer`, `ValidationService`, PowerShell 7 test lanes.

## Global Constraints

- Source task is GitHub issue [#1511](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1511); every code, test, fixture, prompt, example, and documentation change stays linked to it.
- The game is unreleased. Receipt-less positive state is invalid; do not add save migration, runtime promotion, or a fallback reader.
- Scope is ordinary Mortal World items only. Soul Relics and other afterlife items remain #1512.
- Permanent IDs, creation refs, receipt IDs, materialization IDs, owner IDs, and carrier IDs compare with `StringComparer.Ordinal`.
- The GM owns item semantics and the immutable `materialization` envelope. The client owns `itemId`, equal `existedId`, `materializationReceipt`, identity-index entries, transitions, retirement, and lineage.
- Every durable active item has exactly one carrier and one active index entry. Retired IDs are never deleted, reused, or reactivated.
- `contentsPath` contains permanent parent item IDs, or is `null` for a root item. Name-based canonical container paths are removed.
- Every governed optional section is physically represented and marked exactly `populated` or `empty_by_design`.
- Use `pwsh -NoProfile -File .\scripts\test-csharp.ps1`; run the smallest `Focused` filter during implementation, one meaningful `Fast`, conditional `FullValidation` and `LifecycleIntegration`, and one final `PreMerge` without a duplicate final Fast run.
- Preserve the pre-existing untracked `.serena/` and `BookOfEternityClient.*/*/bin|obj` artifacts; stage only feature-owned files.
- Any changed GM-authored contract must update rules, task guides, examples, the example manifest, and documentation/source guards in the same change.
- No Chaos Sea, Shining Abode, Saref, or other afterlife contract changes are expected; record the checked no-update rationale.

## File Responsibility Map

| File | Responsibility |
| --- | --- |
| `BookOfEternityClient/Services/MortalItemMaterializationContract.cs` | Exact envelope, receipt, governed semantic-field, disposition, and seal rules. |
| `BookOfEternityClient/Services/MortalItemIdentityState.cs` | Parse/serialize the client index, create receipts/transitions, and validate index continuity. |
| `BookOfEternityClient/Services/MortalItemCarrierCatalog.cs` | Enumerate governed carriers and companion references once with ordinal dictionaries and scan metrics. |
| `BookOfEternityClient/Services/MortalItemRouteAuthorityCatalog.cs` | Resolve the eight creation routes to exact existing request/receipt/turn/storage authority. |
| `BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs` | Raw pre-seal and canonical post-seal orchestration and bounded item issues. |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs` | Project item commands, assign IDs, rewrite references, seal receipts, and update the index. |
| `BookOfEternityClient/Services/MortalItemTransitionWriter.cs` | Validate operation intents and atomically write carrier/companion/index after-images. |
| `BookOfEternityClient/Services/MortalItemRepairPacketBuilder.cs` | Group issues by exact item coordinate and produce the minimal GM-owned repair packet. |
| `BookOfEternityClient.TestSupport/MortalItemTestFixture.cs` | Deterministic current-schema raw/canonical/index test data shared by both test projects. |

---

### Task 1: Confirm Baseline and Classify Repository Fixtures (Spec Kit T001–T004)

**Files:**
- Create: `specs/1511-complete-item-materialization/fixture-migration-inventory.md`
- Modify: `specs/1511-complete-item-materialization/quickstart.md`
- Modify: `specs/1511-complete-item-materialization/tasks.md`

**Interfaces:**
- Consumes: branch `1511-complete-item-materialization`, base `fa1e85276661717ae805c5ff6f0460c438892f25`, `docs/testing.md`.
- Produces: a path-by-path fixture classification and two clean baseline result directories.

- [ ] **Step 1: Reconfirm branch and user-owned artifacts**

Run:

```powershell
git branch --show-current
git merge-base HEAD fa1e85276661717ae805c5ff6f0460c438892f25
git status --short
```

Expected: branch is `1511-complete-item-materialization`; merge-base is the named commit; only the known untracked `.serena/` and `bin/obj` paths appear.

- [ ] **Step 2: Inventory every item-shaped repository fixture**

Run:

```powershell
rg -l '"(UpdateInventory|inventory|items|NPCInventoryAdds|itemData)"' FileSystemExample BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests Examples | Sort-Object
```

Expected: a finite list that can be classified as `positive-current`, `negative-receiptless`, or `fragment-only`.

- [ ] **Step 3: Write the migration inventory**

Add this exact table header and one row for every path returned by Step 2:

```markdown
# Mortal Item Fixture Migration Inventory

| Path | Fixture/member | Classification | #1511 action | Receipt-less allowed after #1511 |
| --- | --- | --- | --- | --- |
| `FileSystemExample/game_session/game_state/inventory/items.json` | active sword | positive-current | add complete envelope, receipt, and index entry | no |
```

Every receipt-less row must name a test whose title says `RejectsReceiptless`; fragment-only rows must never be fed to canonical validation.

- [ ] **Step 4: Run the two baseline controls**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests.NormalizeAccumulatedStateAsync_StripsPlayerFacingItemJournalTurnAnchors"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~BrowserInventoryManagementTests"
```

Expected: both commands exit `0`, with no timeout or cleanup error.

- [ ] **Step 5: Record baseline evidence and mark T001–T004**

Append a `Baseline` table to `quickstart.md` with command, result directory, total, passed, failed, timeout, and cleanup columns. Change only T001–T004 from `[ ]` to `[x]` after inspecting both `summary.json` files.

- [ ] **Step 6: Commit the baseline record**

```powershell
git add -- specs/1511-complete-item-materialization/fixture-migration-inventory.md specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "test: record item materialization baseline (#1511)"
```

---

### Task 2: Build the Deterministic Test Fixture Vocabulary (T005–T007)

**Files:**
- Create: `BookOfEternityClient.TestSupport/MortalItemTestFixture.cs`
- Create: `BookOfEternityClient.Tests/MortalItemTestFixtureTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.cs`
- Modify: `specs/1511-complete-item-materialization/tasks.md`

**Interfaces:**
- Consumes: JSON field sets from `data-model.md` and `contracts/mortal-item-materialization-envelope.md`.
- Produces: `MortalItemTestFixture.CreateRawRoot`, `CreateCanonicalRoot`, `CreateIndex`, `CreateCarrier`, and `MortalItemMaterializationTestContext.CreateAsync`.

- [ ] **Step 1: Write fixture-shape tests**

Create tests with these assertions:

```csharp
[Fact]
public void CreateRawRoot_UsesCurrentPreSealIdentityShape()
{
    var item = MortalItemTestFixture.CreateRawRoot();
    Assert.Null(item["existedId"]);
    Assert.Equal("new_item_test", item["creationRef"]!.GetValue<string>());
    Assert.Null(item["itemId"]);
    Assert.Null(item["materializationReceipt"]);
    Assert.Equal(12, item["materialization"]!["sections"]!.AsObject().Count);
}

[Fact]
public void CreateCanonicalRoot_HasEqualIdsReceiptAndMatchingIndex()
{
    var item = MortalItemTestFixture.CreateCanonicalRoot();
    var index = MortalItemTestFixture.CreateIndex(item);
    Assert.Equal("itm_test", item["itemId"]!.GetValue<string>());
    Assert.Equal("itm_test", item["existedId"]!.GetValue<string>());
    Assert.Equal("itm_test", index["entries"]![0]!["itemId"]!.GetValue<string>());
}
```

- [ ] **Step 2: Run the new tests and observe compilation failure**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemTestFixtureTests"
```

Expected: FAIL because `MortalItemTestFixture` does not exist.

- [ ] **Step 3: Implement the deterministic fixture API**

Use this public surface; keep all IDs and the seal deterministic so byte-level rollback assertions are stable:

```csharp
public static class MortalItemTestFixture
{
    public const string ItemId = "itm_test";
    public const string CreationRef = "new_item_test";
    public const string MaterializationId = "mat_item_test";
    public const string ReceiptId = "mirec_test";

    public static JsonObject CreateRawRoot(
        string route = "player_acquisition",
        string authorityKind = "turn_outcome",
        string authorityId = "turn_42",
        int sourceTurn = 42);

    public static JsonObject CreateCanonicalRoot(
        string itemId = ItemId,
        string carrierKind = "player_inventory",
        string ownerId = "player",
        string? containerId = null,
        JsonArray? containerPath = null);

    public static JsonObject CreateIndex(params JsonObject[] canonicalItems);
    public static JsonObject CreateCarrier(JsonObject item, string kind, string ownerId, string? containerId = null);
    public static JsonObject CreateReceiptlessNegative();
    public static JsonObject CreateFragmentOnly(string name = "Фрагмент");
}
```

`CreateRawRoot` must physically emit every field and the twelve exact section dispositions; `CreateCanonicalRoot` must remove top-level `creationRef`, write equal IDs and an immutable root receipt; `CreateIndex` must emit one active entry and one `create` transition per supplied item.

- [ ] **Step 4: Add the file-backed context helper**

Expose this exact lifecycle:

```csharp
internal sealed partial class MortalItemMaterializationTestContext : IAsyncDisposable
{
    internal FileSystemManager FileSystem { get; }
    internal ValidationService Validator { get; }
    internal CanonicalStateNormalizer Normalizer { get; }
    internal string RootPath { get; }

    internal static Task<MortalItemMaterializationTestContext> CreateAsync();
    internal Task WriteJsonAsync(string relativePath, JsonNode root);
    internal Task<JsonNode?> ReadJsonAsync(string relativePath);
    internal Task CaptureValidatedPendingSnapshotAsync(int turn = 42);
    public ValueTask DisposeAsync();
}
```

Use the existing integration-test temporary-session and pending-snapshot helpers; disposal removes only the helper-owned temporary root.

- [ ] **Step 5: Run fixture tests green and the old inventory control**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemTestFixtureTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests.Inventory"
```

Expected: PASS for both selections.

- [ ] **Step 6: Commit the fixture vocabulary**

```powershell
git add -- BookOfEternityClient.TestSupport/MortalItemTestFixture.cs BookOfEternityClient.Tests/MortalItemTestFixtureTests.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.cs specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "test: add mortal item materialization fixtures (#1511)"
```

---

### Task 3: Enforce the Exact Envelope and Receipt Contract (T008, T015)

**Files:**
- Create: `BookOfEternityClient.Tests/MortalItemMaterializationContractTests.cs`
- Create: `BookOfEternityClient/Services/MortalItemMaterializationContract.cs`
- Modify: `specs/1511-complete-item-materialization/tasks.md`

**Interfaces:**
- Consumes: `MortalItemTestFixture.CreateRawRoot` and `CreateCanonicalRoot`.
- Produces: `MortalItemMaterializationContract.Validate`, `ComputeSeal`, `HasCompleteEnvelope`, and constants used by validators/normalizers.

- [ ] **Step 1: Write the contract Theory matrix**

```csharp
[Theory]
[InlineData("materialization.realm", "Shining", "mortal_item_materialization_wrong_realm")]
[InlineData("materialization.state", "partial", "mortal_item_materialization_invalid_envelope")]
[InlineData("materialization.sections.mechanics.state", "omitted", "mortal_item_materialization_section_state_mismatch")]
public void Validate_RawRoot_RejectsExactContractViolation(string path, string replacement, string code)
{
    var item = MortalItemTestFixture.CreateRawRoot();
    SetNode(item, path, replacement == "omitted" ? null : JsonValue.Create(replacement));
    using var document = JsonDocument.Parse(item.ToJsonString());
    var issues = MortalItemMaterializationContract.Validate(document.RootElement, "items[0]", MortalItemMaterializationPhase.RawPreSeal);
    Assert.Contains(issues, issue => issue.Code == code);
}

private static void SetNode(JsonObject root, string path, JsonNode? value)
{
    var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
    var parent = root;
    foreach (var segment in segments[..^1])
        parent = parent[segment]?.AsObject() ?? throw new InvalidOperationException($"Missing object segment: {segment}");
    if (value == null)
        parent.Remove(segments[^1]);
    else
        parent[segments[^1]] = value;
}
```

Add named tests for duplicate JSON properties, unknown fields, missing physical empty shapes, GM-authored `itemId`/receipt, invalid image prompt, mismatched creation refs, unequal canonical IDs, wrong receipt seal, and immutable envelope comparison.

- [ ] **Step 2: Run the contract filter red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemMaterializationContractTests"
```

Expected: FAIL because the production contract types do not exist.

- [ ] **Step 3: Define the exact contract surface and field sets**

```csharp
internal enum MortalItemMaterializationPhase { RawPreSeal, CanonicalPostSeal }

internal static class MortalItemMaterializationContract
{
    internal const int SchemaVersion = 1;
    internal const string EnvelopeProperty = "materialization";
    internal const string ReceiptProperty = "materializationReceipt";

    internal static readonly string[] SectionNames =
    {
        "presentation", "physical", "mechanics", "equipment", "container",
        "consumption", "readableOrSentient", "craftingAndDisassembly",
        "bondsAndFateCards", "questRole", "provenance", "ownershipAndPlacement"
    };

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement item,
        string context,
        MortalItemMaterializationPhase phase);

    internal static bool HasCompleteEnvelope(JsonElement item);
    internal static string ComputeSeal(JsonObject item, JsonObject receiptWithoutSeal);
    internal static bool ImmutableEvidenceEquals(JsonNode previous, JsonNode current);
}
```

Use exact `HashSet<string>(StringComparer.Ordinal)` field allowlists. Enumerate `JsonElement` properties before any `JsonNode` conversion so duplicate names remain observable; the duplicate-property test constructs raw JSON containing the same property twice. The seal input writes receipt fields in contract order and the envelope through deterministic canonical property ordering before SHA-256.

- [ ] **Step 4: Implement section disposition/evidence checks**

Use this complete section-to-fields map:

```csharp
private static readonly IReadOnlyDictionary<string, string[]> GovernedFields =
    new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["presentation"] = new[] { "name", "description", "image_prompt", "type", "group", "quality", "rarity" },
        ["physical"] = new[] { "price", "count", "weight", "volume", "durability", "maxDurability" },
        ["mechanics"] = new[] { "bonuses", "effects", "structuredBonuses", "combatEffect", "customProperties", "mechanicalSummaryAuthority", "mechanicalSummaryUnresolvedReason" },
        ["equipment"] = new[] { "equipmentSlot", "accessoryForSlot", "requiresTwoHands" },
        ["container"] = new[] { "isContainer", "capacity", "containerWeight", "weightReduction", "contentsPath" },
        ["consumption"] = new[] { "isConsumption" },
        ["readableOrSentient"] = new[] { "textContent", "journalEntries", "isSentient", "unreadableReason", "sealedReason", "lockedReason" },
        ["craftingAndDisassembly"] = new[] { "disassembleTo" },
        ["bondsAndFateCards"] = new[] { "ownerBondLevelCurrent", "ownerBondLevelMax", "fateCards" },
        ["questRole"] = new[] { "questLinks" },
        ["provenance"] = Array.Empty<string>(),
        ["ownershipAndPlacement"] = new[] { "contentsPath" }
    };
```

For `empty_by_design`, assert the exact null/false/empty array shape from the envelope contract; for `populated`, require non-empty structured evidence and `reason: null`.

- [ ] **Step 5: Run contract tests green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemMaterializationContractTests"
```

Expected: all contract cases PASS.

- [ ] **Step 6: Commit the contract**

```powershell
git add -- BookOfEternityClient/Services/MortalItemMaterializationContract.cs BookOfEternityClient.Tests/MortalItemMaterializationContractTests.cs specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: define mortal item materialization contract (#1511)"
```

---

### Task 4: Add the Client-Owned Identity Index (T013, T016)

**Files:**
- Create: `BookOfEternityClient/Services/MortalItemIdentityState.cs`
- Modify: `BookOfEternityClient.Tests/MortalItemMaterializationContractTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/PendingTurnSnapshotTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Bootstrap.cs`
- Modify: `BookOfEternityClient/Services/MortalBootstrapStateBuilder.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`

**Interfaces:**
- Consumes: `MortalItemMaterializationContract.ComputeSeal`.
- Produces: `MortalItemIdentityState.Parse`, `CreateEmptyRoot`, `CreateRootReceipt`, `AppendTransition`, `ValidateAgainst`, and constant `StatePath`.

- [ ] **Step 1: Write red index and bootstrap tests**

```csharp
[Fact]
public void Parse_RejectsDuplicateItemAndReceiptIds()
{
    var item = MortalItemTestFixture.CreateCanonicalRoot();
    var root = MortalItemTestFixture.CreateIndex(item, item.DeepClone().AsObject());
    var result = MortalItemIdentityState.Parse(root);
    Assert.Contains(result.Issues, issue => issue.Code == "mortal_item_materialization_duplicate_item_id");
    Assert.Contains(result.Issues, issue => issue.Code == "mortal_item_materialization_duplicate_receipt_id");
}

[Fact]
public async Task Bootstrap_WritesEmptyCurrentSchemaItemIndex()
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.BuildMortalBootstrapAsync();
    Assert.True(JsonNode.DeepEquals(
        JsonNode.Parse("{\"schemaVersion\":1,\"entries\":[]}"),
        await context.ReadJsonAsync(MortalItemIdentityState.StatePath)));
}
```

The pending-snapshot test must assert that `StatePath` is present in the manifest, rollback baselines, and QTE/browser-derived tracked sets.

- [ ] **Step 2: Run the red filters**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemMaterializationContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalBootstrapValidationTests|FullyQualifiedName~PendingTurnSnapshotTests"
```

Expected: FAIL on missing identity-state and bootstrap behavior.

- [ ] **Step 3: Implement the immutable index model**

```csharp
internal static class MortalItemIdentityState
{
    internal const string StatePath = "game_state/inventory/item_identity_index.json";
    internal const int SchemaVersion = 1;

    internal static MortalItemIdentityParseResult Parse(JsonNode? root);
    internal static JsonObject CreateEmptyRoot() => new()
    {
        ["schemaVersion"] = SchemaVersion,
        ["entries"] = new JsonArray()
    };

    internal static JsonObject CreateRootReceipt(JsonObject rawItem, string itemId, int acceptedTurn);
    internal static JsonObject CreateSplitReceipt(JsonObject parent, string childItemId, int turn);
    internal static JsonObject CreateTransition(
        string kind,
        int turn,
        IEnumerable<string> sourceItemIds,
        JsonObject? sourceCarrier,
        JsonObject? destinationCarrier,
        int quantityBefore,
        int quantityAfter,
        string authorityKind,
        string authorityId);
}

internal sealed record MortalItemIdentityParseResult(
    JsonObject Root,
    IReadOnlyDictionary<string, JsonObject> EntriesByItemId,
    IReadOnlyList<ValidationIssue> Issues);
```

Parse exact root/entry/transition fields, sort entries and origin IDs ordinally, require append-only transition histories, and keep retired entries rather than deleting them.

- [ ] **Step 4: Register bootstrap/snapshot paths**

Add `MortalItemIdentityState.StatePath` to `CanonicalAccumulatedFiles`, which automatically feeds backup and rollback lists. Add `MortalItemIdentityState.CreateEmptyRoot()` to `MortalBootstrapStateBuilder` output. Do not add the index to `Configuration/FileMapping.cs` because no GM command maps to it.

- [ ] **Step 5: Run index/bootstrap tests green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemMaterializationContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalBootstrapValidationTests|FullyQualifiedName~PendingTurnSnapshotTests"
```

Expected: PASS and the empty bootstrap still validates.

- [ ] **Step 6: Commit identity authority**

```powershell
git add -- BookOfEternityClient/Services/MortalItemIdentityState.cs BookOfEternityClient/Services/MortalBootstrapStateBuilder.cs BookOfEternityClient/Services/CanonicalStateNormalizer.cs BookOfEternityClient.Tests/MortalItemMaterializationContractTests.cs BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs BookOfEternityClient.IntegrationTests/PendingTurnSnapshotTests.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Bootstrap.cs specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: add client-owned item identity index (#1511)"
```

---

### Task 5: Build the One-Pass Carrier and Companion Catalog (T009, T017)

**Files:**
- Create: `BookOfEternityClient.Tests/MortalItemCarrierCatalogTests.cs`
- Create: `BookOfEternityClient/Services/MortalItemCarrierCatalog.cs`

**Interfaces:**
- Consumes: canonical carrier JSON and `MortalItemIdentityState.StatePath`.
- Produces: `MortalItemCarrierCoordinate`, `MortalItemCarrierOccurrence`, `MortalItemCarrierCatalog.Build`, and `MortalItemCatalogScanMetrics`.

- [ ] **Step 1: Write exact identity, duplicate carrier, and scale tests**

```csharp
[Fact]
public void Build_DoesNotCollapseCaseDistinctIds()
{
    var lower = MortalItemTestFixture.CreateCanonicalRoot("itm_case");
    var upper = MortalItemTestFixture.CreateCanonicalRoot("ITM_CASE");
    var result = MortalItemCarrierCatalog.Build(InputWithPlayerItems(lower, upper));
    Assert.Equal(2, result.ByItemId.Count);
    Assert.Contains(result.Issues, issue => issue.Code == "mortal_item_materialization_identity_ambiguity");
}

[Fact]
public void Build_DoublingPopulationStaysWithinTwoPointFiveTimesWork()
{
    var one = MortalItemCarrierCatalog.Build(RepresentativeInput(200));
    var two = MortalItemCarrierCatalog.Build(RepresentativeInput(400));
    Assert.True(two.Metrics.TotalVisited <= one.Metrics.TotalVisited * 2.5);
}

private static MortalItemCarrierCatalogInput InputWithPlayerItems(params JsonObject[] items) =>
    new(
        new JsonObject { ["items"] = new JsonArray(items.Select(item => item.DeepClone()).ToArray()) },
        null,
        null,
        null,
        null,
        new Dictionary<string, JsonObject>(StringComparer.Ordinal));

private static MortalItemCarrierCatalogInput RepresentativeInput(int itemCount) =>
    InputWithPlayerItems(Enumerable.Range(0, itemCount)
        .Select(index => MortalItemTestFixture.CreateCanonicalRoot($"itm_{index:D5}"))
        .ToArray());
```

Add player, NPC, location-storage, vehicle, nested-container, duplicate-carrier, whitespace, and Unicode-normalization cases.

- [ ] **Step 2: Run the catalog tests red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemCarrierCatalogTests"
```

Expected: FAIL because the catalog types do not exist.

- [ ] **Step 3: Implement one-pass catalog types**

```csharp
internal sealed record MortalItemCarrierCoordinate(
    string Kind,
    string OwnerId,
    string? ContainerId,
    IReadOnlyList<string> ContainerPath);

internal sealed record MortalItemCarrierOccurrence(
    string ItemId,
    string? CreationRef,
    string? ReceiptId,
    string? MaterializationId,
    string FilePath,
    string JsonPath,
    MortalItemCarrierCoordinate Carrier,
    JsonObject Item);

internal sealed record MortalItemCarrierCatalogInput(
    JsonObject? PlayerInventory,
    JsonObject? NpcCore,
    JsonObject? NpcInventoryCommands,
    JsonObject? CurrentLocation,
    JsonObject? Vehicles,
    IReadOnlyDictionary<string, JsonObject> CompanionRoots);

internal sealed record MortalItemCatalogScanMetrics(int Items, int Companions, int Routes)
{
    internal int TotalVisited => Items + Companions + Routes;
}
```

`Build` must enumerate each root array once, walk nested items once, and populate ordinal dictionaries by item ID, creation ref, receipt ID, and materialization ID. Ambiguity detection may inspect dictionary keys but must not rescan carriers.

- [ ] **Step 4: Run the catalog tests green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemCarrierCatalogTests"
```

Expected: PASS; the scale counter meets the 2.5x bound.

- [ ] **Step 5: Commit the catalog**

```powershell
git add -- BookOfEternityClient/Services/MortalItemCarrierCatalog.cs BookOfEternityClient.Tests/MortalItemCarrierCatalogTests.cs
git diff --cached --check
git commit -m "feat: catalog mortal item identity once (#1511)"
```

---

### Task 6: Register Raw and Canonical Validation Phases (T010, T014, T019–T020, T025)

**Files:**
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Validation.cs`
- Modify: `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`
- Create: `BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs`
- Modify: `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs`
- Modify: `BookOfEternityClient/Services/ValidationService.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs`

**Interfaces:**
- Consumes: contract, index, and catalog APIs from Tasks 3–5.
- Produces: `ValidateAcceptedTurnRawMortalItemMaterializationAsync`, `ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync`, and phase flag `AcceptedTurnItemMaterializationCompleteness`.

- [ ] **Step 1: Write raw/canonical lifecycle tests**

```csharp
[Fact]
public async Task RawCompletePlayerCreation_PassesBeforeSeal()
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.WritePlayerUpdateAsync(MortalItemTestFixture.CreateRawRoot());
    await context.CaptureValidatedPendingSnapshotAsync();
    var issues = await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
    Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
}

[Fact]
public async Task CanonicalReceiptlessItem_IsRejectedWithoutPromotion()
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.WriteCanonicalPlayerItemAsync(MortalItemTestFixture.CreateReceiptlessNegative());
    var issues = await context.Validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();
    Assert.Contains(issues, issue => issue.Code == "mortal_item_materialization_receiptless_current_item");
}
```

Add a clean empty-bootstrap test and phase selection/order/equivalence assertions.

- [ ] **Step 2: Run both focused filters red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests|FullyQualifiedName~MortalItemMaterializationContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests"
```

Expected: FAIL on missing phase and validator entrypoints.

- [ ] **Step 3: Add the phase without dropping existing selectable bits**

```csharp
AcceptedTurnFactionMaterializationCompleteness = 1u << 28,
AcceptedTurnItemMaterializationCompleteness = 1u << 29,
All = ((1u << 26) - 1) |
      AcceptedTurnFactionMaterializationCompleteness |
      AcceptedTurnItemMaterializationCompleteness,
Selectable = All | RivalAndResidentCrossReferences | GuardianProjectStateFiles
```

Call canonical item validation from `ValidateGameStateInternalAsync` when this phase is selected. Call raw item validation in `CollectAcceptedTurnRawStateIssuesAsync` after raw actor/faction validation and before normalization.

- [ ] **Step 4: Implement the validation orchestration**

Expose both ordinary and lease-aware canonical entrypoints:

```csharp
public Task<IReadOnlyList<ValidationIssue>> ValidateAcceptedTurnRawMortalItemMaterializationAsync();
public Task<IReadOnlyList<ValidationIssue>> ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();
internal Task<IReadOnlyList<ValidationIssue>> ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(
    FileSystemManager.CanonicalWriteLease writeLease);
```

The raw pass must reject GM-authored IDs/receipt/index, require a usable pending snapshot for existing authority, and bind every new creation ref to one route. The canonical pass must require equal IDs, one immutable receipt, one matching active index entry, one carrier, exact container path, companion agreement, and pre-turn envelope/receipt continuity.

- [ ] **Step 5: Protect the identity index from GM validation/repair routing**

Add this predicate term to both `IsClientOwnedSurfacePath` and `IsClientOwnedSurfaceValidationPath`:

```csharp
normalizedPath.Equals(MortalItemIdentityState.StatePath, StringComparison.OrdinalIgnoreCase)
```

Remove `id` and `initialId` from positive full-item identity acceptance; canonical items require both `itemId` and `existedId`, ordinal-equal. Keep receipt-less JSON only in the named negative test.

- [ ] **Step 6: Run validation filters green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests|FullyQualifiedName~MortalItemMaterializationContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests"
```

Expected: PASS for raw complete, canonical complete, receipt-less rejection, and empty bootstrap.

- [ ] **Step 7: Commit validation registration**

```powershell
git add -- BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs BookOfEternityClient/Services/ValidationService.cs BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Validation.cs specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: validate mortal item materialization (#1511)"
```

---

### Task 7: Seal Player Creations Atomically (T021, T024)

**Files:**
- Create: `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.MortalItems.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Normalization.cs`
- Create: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`

**Interfaces:**
- Consumes: `MortalItemIdentityState`, `MortalItemMaterializationContract`, lease-aware canonical validator.
- Produces: `NormalizeMortalItemsAsync`, permanent ID/reference mapping, and exact accepted-turn before-image rollback.

- [ ] **Step 1: Write raw-to-sealed and failure rollback tests**

```csharp
[Fact]
public async Task Normalize_PlayerCreation_SealsOneCanonicalItemAndIndexEntry()
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.WritePlayerUpdateAsync(MortalItemTestFixture.CreateRawRoot());
    await context.CaptureValidatedPendingSnapshotAsync();
    await context.NormalizeAcceptedTurnAsync();
    var items = await context.ReadJsonAsync("game_state/inventory/items.json");
    Assert.Single(items!["items"]!.AsArray());
    Assert.Null(items["UpdateInventory"]);
    Assert.Equal(items["items"]![0]!["itemId"]!.GetValue<string>(), items["items"]![0]!["existedId"]!.GetValue<string>());
    Assert.Single((await context.ReadJsonAsync(MortalItemIdentityState.StatePath))!["entries"]!.AsArray());
}

[Fact]
public async Task Normalize_PostSealFailure_RestoresEveryTrackedFileByteForByte()
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    var before = await context.CaptureExactBytesAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
    context.InjectWriteFailureAfter(MortalItemIdentityState.StatePath);
    await Assert.ThrowsAsync<InvalidOperationException>(() => context.NormalizeAcceptedTurnAsync());
    await context.AssertExactBytesAsync(before);
}
```

- [ ] **Step 2: Run the normalizer filter red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests.MortalItems"
```

Expected: FAIL because `UpdateInventory` is not projected/sealed.

- [ ] **Step 3: Add player materialization normalization**

At the start of `NormalizeAccumulatedStateAsync`, after prerequisites are readable and before sidecar normalization, call:

```csharp
await NormalizeMortalItemsAsync(backups);
```

`NormalizeMortalItemsAsync` must:

```csharp
private async Task NormalizeMortalItemsAsync(IReadOnlyDictionary<string, string>? backups)
{
    var current = await ReadObjectAsync("game_state/inventory/items.json") ?? new JsonObject();
    var index = await ReadObjectAsync(MortalItemIdentityState.StatePath)
                ?? MortalItemIdentityState.CreateEmptyRoot();
    var creationMap = SealNewPlayerItems(current, index, await TryReadCurrentTurnNumberAsync());
    RewritePlayerItemReferences(current, creationMap);
    current.Remove("UpdateInventory");
    await WriteCanonicalFileAtomicAsync("game_state/inventory/items.json", current.ToJsonString(JsonOpts));
    await WriteCanonicalFileAtomicAsync(MortalItemIdentityState.StatePath, index.ToJsonString(JsonOpts));
}
```

Generate IDs from client GUIDs with `itm_`, `mirec_`, and `mitrn_` prefixes. Tests assert shape and uniqueness, not literal random values.

- [ ] **Step 4: Bind refresh to one lease and restore exact before-images**

Replace the unbound refresh contour with this structure. Return item issues to
the existing repair loop instead of converting them into a snapshot-baseline
failure:

```csharp
private async Task<IReadOnlyList<ValidationIssue>> RefreshCanonicalStateAsync(
    IReadOnlyDictionary<string, string> backups)
{
    IReadOnlyList<ValidationIssue> postSealIssues;
    await using (var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync())
    {
        var before = await CaptureCanonicalBeforeImagesAsync(
            writeLease,
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        try
        {
            await _normalizer.BindTo(writeLease).NormalizeAccumulatedStateAsync(backups);
            postSealIssues = await _validator
                .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync(writeLease);
            if (postSealIssues.Any(issue => issue.Severity == IssueSeverity.Error))
            {
                await RestoreCanonicalBeforeImagesAsync(writeLease, before);
                return postSealIssues;
            }
        }
        catch
        {
            await RestoreCanonicalBeforeImagesAsync(writeLease, before);
            throw;
        }
    }
    await RefreshRuntimeStateAsync();
    return postSealIssues;
}
```

`RestoreCanonicalBeforeImagesAsync` constructs `CoordinatedStateWriteHelper.PlannedWrite(path, current, previous, RequireCurrentBaseline: true)` for every tracked path and throws if the coordinated restore returns false. This keeps the same tracked arrays used by QTE and browser paths.

Change `RefreshAcceptedTurnCanonicalStateForValidationAsync` to return a small
record containing `BaselineUsable` and `PostSealIssues`. Its caller retains the
existing invalid-snapshot packet only when `BaselineUsable` is false; non-empty
item issues go through `WaitForContractRepairAsync`, preserving their exact
`mortal_item:*` coordinates.

- [ ] **Step 5: Run normalizer and snapshot filters green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests.MortalItems|FullyQualifiedName~PendingTurnSnapshotTests|FullyQualifiedName~MortalItemMaterializationValidationTests"
```

Expected: PASS including byte-for-byte injected failure rollback.

- [ ] **Step 6: Commit atomic player sealing**

```powershell
git add -- BookOfEternityClient/Services/CanonicalStateNormalizer.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.MortalItems.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Normalization.cs specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: seal player item creations atomically (#1511)"
```

---

### Task 8: Bind All Creation Routes and Same-Turn Companions (T011–T012, T018, T022–T023, T026)

**Files:**
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Routes.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Companions.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Routes.cs`
- Create: `BookOfEternityClient/Services/MortalItemRouteAuthorityCatalog.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.FactionAndInventoryHelpers.cs`
- Modify: `BookOfEternityClient/Services/QuestRewardAuthority.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.QuestRewardAuthority.cs`

**Interfaces:**
- Consumes: raw/canonical validator and creation-map normalizer from Tasks 6–7.
- Produces: `MortalItemRouteAuthorityCatalog.BuildAsync` and route/companion resolution for all eight creation routes.

- [ ] **Step 1: Write the eight-route Theory**

```csharp
[Theory]
[InlineData("player_acquisition", "turn_outcome")]
[InlineData("npc_acquisition", "npc_inventory_add")]
[InlineData("new_npc_inventory", "new_npc")]
[InlineData("loot_acquisition", "loot_template")]
[InlineData("craft_output", "craft_request")]
[InlineData("trade_output", "npc_trade_receipt")]
[InlineData("quest_reward", "quest_reward")]
[InlineData("storage_placement", "location_storage")]
public async Task CompleteRoute_SealsExactlyOneItem(string route, string authorityKind)
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.ArrangeRouteAsync(route, authorityKind, MortalItemTestFixture.CreateRawRoot(route, authorityKind));
    var outcome = await context.ValidateNormalizeAndValidateAsync();
    Assert.Empty(outcome.Errors);
    Assert.Equal(1, outcome.NewReceipts);
    Assert.Equal(1, outcome.NewActiveIndexEntries);
}
```

For every row, add a missing-section case that asserts unchanged carrier, companion, request/reward, and index bytes.

- [ ] **Step 2: Write same-turn reference tests**

Create Theory rows for equipment, nested `contentsPath`, item text, journal, bond, recipe, quest reward, and storage placement. Each raw surface uses `creationRef`; each accepted surface must contain only the assigned permanent ID.

- [ ] **Step 3: Run route/companion filters red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests.Routes|FullyQualifiedName~MortalItemMaterializationValidationTests.Companions"
```

Expected: non-player routes and companion rewrites FAIL.

- [ ] **Step 4: Implement exact route authority records**

```csharp
internal sealed record MortalItemRouteAuthority(
    string Route,
    string AuthorityKind,
    string AuthorityId,
    MortalItemCarrierCoordinate Destination,
    IReadOnlyList<string> SourceItemIds);

internal sealed class MortalItemRouteAuthorityCatalog
{
    internal IReadOnlyDictionary<string, MortalItemRouteAuthority> ByCreationRef { get; }
    internal static Task<MortalItemRouteAuthorityCatalog> BuildAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease = null);
}
```

Derive loot IDs exactly as `loot_template:<turn>:<ordinal>:<baseName>`. Match craft request ID, NPC trade receipt/offer, quest/reward ID, existing NPC ID, new-NPC creation reference, and existing location/storage IDs with ordinal comparison.

- [ ] **Step 5: Normalize NPC routes and companions**

Use one `Dictionary<string,string>(StringComparer.Ordinal)` creation map for the whole accepted turn. Rewrite only exact matching `creationRef` properties in equipment/container, resources, texts, journals, bonds/Fate Cards, recipes, quest reward details, and storage destinations. Remove raw aliases after every reference resolves; unresolved and multiply-resolved refs fail before writes.

- [ ] **Step 6: Bind quest rewards to the accepted index transition**

Require the structured reward detail to resolve to the newly assigned item ID and the index `create` transition authority to equal the quest/reward ID:

```csharp
if (!string.Equals(rewardItemId, indexEntry.ItemId, StringComparison.Ordinal) ||
    !string.Equals(indexEntry.LastTransition.AuthorityId, rewardAuthorityId, StringComparison.Ordinal))
{
    issues.Add(new ValidationIssue(
        rewardPath,
        IssueSeverity.Error,
        "Quest item reward must resolve to the accepted item identity transition.",
        code: "mortal_item_materialization_quest_reward_authority_mismatch",
        actor: $"mortal_item:existing:{rewardItemId}",
        section: "MortalItemMaterialization",
        expected: rewardAuthorityId,
        actual: indexEntry.LastTransition.AuthorityId,
        repairHint: "Restore the exact quest reward authority and do not replay the reward."));
}
```

- [ ] **Step 7: Run route/companion filters green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests|FullyQualifiedName~QuestRewardAuthorityValidationTests|FullyQualifiedName~NpcTradeRequestValidationTests"
```

Expected: all complete routes pass; malformed rows leave exact before-images.

- [ ] **Step 8: Commit route completion**

```powershell
git add -- BookOfEternityClient/Services/MortalItemRouteAuthorityCatalog.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.FactionAndInventoryHelpers.cs BookOfEternityClient/Services/QuestRewardAuthority.cs BookOfEternityClient/Services/Validation/ValidationService.QuestRewardAuthority.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Routes.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Companions.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Routes.cs specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: materialize every mortal item creation route (#1511)"
```

---

### Task 9: Run and Record the Complete-Creation Checkpoint (T027)

**Files:**
- Modify: `specs/1511-complete-item-materialization/quickstart.md`
- Modify: `specs/1511-complete-item-materialization/tasks.md`

**Interfaces:**
- Consumes: Tasks 2–8.
- Produces: reviewed US1 evidence before transfer work begins.

- [ ] **Step 1: Run the fast-project US1 filter**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemMaterializationContractTests|FullyQualifiedName~MortalItemCarrierCatalogTests|FullyQualifiedName~ValidationPhaseSelectionTests"
```

Expected: exit `0`, no timeout, cleanup complete.

- [ ] **Step 2: Run the integration US1 filter**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests.MortalItems|FullyQualifiedName~MortalBootstrapValidationTests|FullyQualifiedName~PendingTurnSnapshotTests"
```

Expected: exit `0`; all eight creation-route rows and empty bootstrap pass.

- [ ] **Step 3: Record evidence and mark T005–T027**

Read both `summary.json` files, append their exact paths/counters to `quickstart.md`, inspect the diff for each task, and change only proven T005–T027 boxes to `[x]`.

- [ ] **Step 4: Commit the US1 checkpoint**

```powershell
git add -- specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "test: verify complete mortal item creation (#1511)"
```

---

### Task 10: Preserve Identity Across Player, Storage, and Vehicle Transfers (T028–T033, T036–T037)

**Files:**
- Create: `BookOfEternityClient.Tests/MortalItemIdentityTransitionTests.cs`
- Create: `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Transfers.cs`
- Modify: `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs`
- Modify: `BookOfEternityClient/Services/StorageTransportMoveService.cs`
- Modify: `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs`

**Interfaces:**
- Consumes: identity state/catalog and `CoordinatedStateWriteHelper`.
- Produces: `MortalItemTransitionIntent`, `MortalItemTransitionWriter.ExecuteAsync`, and exact transfer outcomes.

- [ ] **Step 1: Write transition and browser parity tests**

```csharp
[Fact]
public async Task Transfer_PlayerToStorage_PreservesReceiptAndMovesOneCarrier()
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.ArrangeCanonicalPlayerItemAsync();
    var beforeReceipt = await context.ReadReceiptAsync(MortalItemTestFixture.ItemId);
    var result = await context.MovePlayerItemToStorageAsync(MortalItemTestFixture.ItemId, "loc_1", "storage_1");
    Assert.True(result.Success);
    Assert.Equal(beforeReceipt, await context.ReadReceiptAsync(MortalItemTestFixture.ItemId));
    Assert.Equal("location_storage", (await context.ReadIndexEntryAsync(MortalItemTestFixture.ItemId))["currentCarrier"]!["kind"]!.GetValue<string>());
    Assert.Equal(1, await context.CountActiveCarrierOccurrencesAsync(MortalItemTestFixture.ItemId));
}
```

Add player→storage→player→vehicle→player, duplicate carrier, exact-name collision, immutable receipt/envelope, retired-ID reuse, and injected second-write failure cases.

- [ ] **Step 2: Run transfer filters red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemIdentityTransitionTests|FullyQualifiedName~BrowserStorageTransportParityTests"
```

Expected: FAIL because the existing move service does not update the identity index.

- [ ] **Step 3: Define operation intents and coordinated writer**

```csharp
internal enum MortalItemTransitionKind { Transfer, Split, Merge, Consume, Destroy, SemanticUpdate }

internal sealed record MortalItemTransitionIntent(
    MortalItemTransitionKind Kind,
    IReadOnlyList<string> SourceItemIds,
    MortalItemCarrierCoordinate? SourceCarrier,
    MortalItemCarrierCoordinate? DestinationCarrier,
    int Quantity,
    int Turn,
    string AuthorityKind,
    string AuthorityId,
    string? SurvivorItemId = null);

internal sealed record MortalItemTransitionResult(bool Success, string? ItemId, string? DerivedItemId, string Message);

internal sealed class MortalItemTransitionWriter
{
    internal Task<MortalItemTransitionResult> ExecuteAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalItemTransitionIntent intent);
}
```

The writer reads all touched carriers/companions/index through the lease, checks exact current baselines, builds after-images in memory, validates the composed catalog/index, and commits one `CoordinatedStateWriteHelper.TryCommitAsync` write set. Callers never supply index JSON.

- [ ] **Step 4: Route storage/vehicle moves through the writer**

Resolve the user-facing name to one exact `itemId` before creating the intent. Preserve the same item `JsonObject`; append one `transfer` transition and replace only `currentCarrier`. Include `MortalItemIdentityState.StatePath` in `BrowserMortalWorldWriteService` tracked local-write paths without changing its session-lock order.

- [ ] **Step 5: Reconcile governed companions**

For transfer, preserve item text, journal, bond, recipe, and quest references; rewrite owner/equipment/container coordinates only when the existing companion contract requires it. Clear equipment atomically when an item leaves the equipping owner. Validate `contentsPath` parents in the destination root carrier.

- [ ] **Step 6: Run transfer filters green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemIdentityTransitionTests|FullyQualifiedName~BrowserStorageTransportParityTests"
```

Expected: PASS including exact rollback after injected write failure.

- [ ] **Step 7: Commit transfer authority**

```powershell
git add -- BookOfEternityClient/Services/MortalItemTransitionWriter.cs BookOfEternityClient/Services/StorageTransportMoveService.cs BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs BookOfEternityClient.Tests/MortalItemIdentityTransitionTests.cs BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Transfers.cs specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: preserve item identity across carrier moves (#1511)"
```

---

### Task 11: Preserve Identity Across NPC Commands and Trade (T029, T031, T034–T035, T038)

**Files:**
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Transfers.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Trade.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient/Services/NpcTradeService.cs`
- Modify: `BookOfEternityClient/Services/NpcTradeRequestState.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`

**Interfaces:**
- Consumes: `MortalItemTransitionWriter.ExecuteAsync`.
- Produces: exact player↔NPC transfer classification and trade/buyback continuity.

- [ ] **Step 1: Write GM-command and trade continuity tests**

```csharp
[Theory]
[InlineData("sell")]
[InlineData("buy")]
[InlineData("buyback")]
public async Task ExistingPhysicalTrade_PreservesIdentityAndReceipt(string operation)
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.ArrangeTradeAsync(operation, MortalItemTestFixture.CreateCanonicalRoot());
    var before = await context.ReadReceiptAsync(MortalItemTestFixture.ItemId);
    var result = await context.ExecuteTradeAsync(operation);
    Assert.True(result.Success);
    Assert.Equal(before, await context.ReadReceiptAsync(MortalItemTestFixture.ItemId));
    Assert.Equal(1, await context.CountActiveCarrierOccurrencesAsync(MortalItemTestFixture.ItemId));
}
```

Add tests rejecting an existing-item remove followed by null-ID recreation and preserving all companions across GM-command transfer.

- [ ] **Step 2: Run trade/transfer integration filters red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests.Transfers|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
```

Expected: new continuity assertions FAIL.

- [ ] **Step 3: Classify accepted GM commands as transitions**

Join removal and addition by exact pre-turn item ID and source/destination coordinates. Require unchanged receipt/envelope and reject a null-ID add whose semantics match a just-removed item. Emit one transfer intent, not a destroy plus create pair.

- [ ] **Step 4: Route NPC trade settlement through the transition writer**

Resolve request/receipt/offer authority before writes. Existing stock, sell, and buyback transfer the same item; only merchant-generated stock absent from pre-turn authority may use independent materialization. Currency, stock, carrier, and index writes belong to one coordinated write set.

- [ ] **Step 5: Run the US2 filters green and record evidence**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemIdentityTransitionTests|FullyQualifiedName~BrowserStorageTransportParityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests.Transfers|FullyQualifiedName~NpcTradeRequestValidationTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
```

Expected: PASS; update `quickstart.md` with both result directories and mark proven T028–T038.

- [ ] **Step 6: Commit NPC/trade continuity**

```powershell
git add -- BookOfEternityClient/Services/NpcTradeService.cs BookOfEternityClient/Services/NpcTradeRequestState.cs BookOfEternityClient/Services/Validation/ValidationService.MortalItemMaterialization.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationValidationTests.Transfers.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Trade.cs BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: preserve item identity through npc trade (#1511)"
```

---

### Task 12: Conserve Stack Quantity and Lineage (T039–T047)

**Files:**
- Create: `BookOfEternityClient.Tests/MortalItemIdentityTransitionTests.Stacks.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Stacks.cs`
- Modify: `BookOfEternityClient.Tests/WebUi/BrowserInventoryManagementTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs`
- Modify: `BookOfEternityClient/Services/InventoryManagementService.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs`
- Modify: `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs`
- Modify: `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`

**Interfaces:**
- Consumes: transition writer and identity-state receipt/transition factories.
- Produces: split-derived receipts, deterministic merge retirement, and destroy transitions.

- [ ] **Step 1: Write split/merge/discard tests**

```csharp
[Fact]
public async Task Split_TenIntoSevenAndThree_CreatesDerivedReceiptAndConservesQuantity()
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.ArrangeCanonicalStackAsync(count: 10);
    var result = await context.SplitAsync(MortalItemTestFixture.ItemId, 3);
    Assert.True(result.Success);
    Assert.Equal(new[] { 3, 7 }, (await context.ReadActiveCountsAsync()).Order().ToArray());
    var child = await context.ReadIndexEntryAsync(result.DerivedItemId!);
    Assert.Equal("split_derived", (await context.ReadReceiptAsync(result.DerivedItemId!))["instanceKind"]!.GetValue<string>());
    Assert.Equal(MortalItemTestFixture.ItemId, child["parentItemIds"]![0]!.GetValue<string>());
}

[Fact]
public async Task Merge_SelectedStackSurvivesAndContributorsRetire()
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.ArrangeCompatibleStacksAsync(("itm_a", 2), ("itm_b", 3));
    await context.MergeAsync("itm_b");
    Assert.Equal(5, await context.ReadCountAsync("itm_b"));
    Assert.Equal("merged", (await context.ReadIndexEntryAsync("itm_a"))["state"]!.GetValue<string>());
    Assert.Equal("itm_b", (await context.ReadIndexEntryAsync("itm_a"))["mergedIntoItemId"]!.GetValue<string>());
}
```

Add incompatible readable/sentient/bonded/quest/equipped/container/mechanics cases, failure rollback, and discard→`destroyed` with no ground-loot carrier.

- [ ] **Step 2: Run stack filters red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemIdentityTransitionTests.Stacks|FullyQualifiedName~BrowserInventoryManagementTests"
```

Expected: FAIL because split copies identity evidence and merge uses the old raw JSON signature.

- [ ] **Step 3: Implement split through the transition writer**

Keep the selected parent ID/receipt, decrement its count, assign a new child item/receipt ID, set child `instanceKind: split_derived`, include exactly the parent ID, copy the ordinal-sorted origin set, and append one split transition. Commit carrier and index together.

- [ ] **Step 4: Implement governed semantic merge compatibility**

Compare every item property after excluding exactly:

```csharp
private static readonly HashSet<string> StackIdentityFields = new(StringComparer.Ordinal)
{
    "itemId", "existedId", "count", "materializationReceipt"
};
```

Require the same root carrier and `contentsPath`; keep the user-selected survivor; union origins ordinally; retire every contributor as `merged`; conserve the exact sum.

- [ ] **Step 5: Implement destroy and consumption distinctions**

`DropAsync` uses a `Destroy` intent, removes the sole carrier occurrence, clears equipment, sets index state `destroyed`, and appends a `destroy` transition. A partial count reduction stays active; a full authorized use sets `consumed`. Neither operation creates a ground-loot file.

- [ ] **Step 6: Run stack and console/browser filters green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemIdentityTransitionTests.Stacks|FullyQualifiedName~BrowserInventoryManagementTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerModeCommandTests.TradeAndInventory"
```

Expected: PASS with exact conservation and rollback assertions.

- [ ] **Step 7: Record and commit the US3 checkpoint**

Update `quickstart.md`, mark proven T039–T047, then commit:

```powershell
git add -- BookOfEternityClient/Services/MortalItemTransitionWriter.cs BookOfEternityClient/Services/InventoryManagementService.cs BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs BookOfEternityClient.Tests/MortalItemIdentityTransitionTests.Stacks.cs BookOfEternityClient.Tests/WebUi/BrowserInventoryManagementTests.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Stacks.cs BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: preserve mortal item stack lineage (#1511)"
```

---

### Task 13: Produce Narrow Atomic Repair Packets (T048–T054)

**Files:**
- Create: `BookOfEternityClient.Tests/MortalItemRepairPacketBuilderTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationLifecycleTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationLifecycleTests.Output.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Repair.cs`
- Create: `BookOfEternityClient/Services/MortalItemRepairPacketBuilder.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- Modify: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`

**Interfaces:**
- Consumes: item issue codes/coordinates and accepted-turn exact rollback contour.
- Produces: packet kind `mortal_item_materialization_repair` and replay-safe repair integration.

- [ ] **Step 1: Write packet allowlist/protection tests**

```csharp
[Fact]
public void Build_NeverTargetsClientOwnedIdentityIndex()
{
    var issue = new ValidationIssue(
        "game_state/inventory/items.json.UpdateInventory[0].materializationReceipt",
        IssueSeverity.Error,
        "The GM cannot author a client-owned item receipt.",
        code: "mortal_item_materialization_gm_authored_client_field",
        actor: "mortal_item:new:new_item_test",
        section: "MortalItemMaterialization");
    var packet = MortalItemRepairPacketBuilder.Build("mortal_item:new:new_item_test", new[] { issue });
    Assert.DoesNotContain(MortalItemIdentityState.StatePath, packet.TargetFiles);
    Assert.DoesNotContain(packet.Steps, step => step.Contains("author", StringComparison.OrdinalIgnoreCase) && step.Contains("receipt", StringComparison.OrdinalIgnoreCase));
}
```

Add grouping, target allowlist, exact correction, required companion, expected/actual authority, and FileMapping/source-guard cases.

- [ ] **Step 2: Write lifecycle rollback/idempotence matrix**

```csharp
[Theory]
[InlineData("craft_output")]
[InlineData("trade_output")]
[InlineData("quest_reward")]
[InlineData("loot_acquisition")]
[InlineData("transfer")]
[InlineData("split")]
[InlineData("merge")]
public async Task RepeatedRepair_SettlesOnceAndDoesNotDuplicateIdentity(string route)
{
    await using var context = await MortalItemMaterializationTestContext.CreateAsync();
    await context.ArrangeRepairableLifecycleAsync(route);
    await context.RunRepairAttemptAsync();
    await context.RunRepairAttemptAsync();
    Assert.Equal(1, await context.CountSettlementAsync(route));
    Assert.True(await context.QuantitiesAreConservedAsync());
    Assert.False(await context.HasDuplicateItemReceiptOrTransitionAsync());
}
```

Add a stale-output test that proves rejected acquisition confirmation is absent after rollback.

- [ ] **Step 3: Run packet and lifecycle filters red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemRepairPacketBuilderTests|FullyQualifiedName~PromptDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationLifecycleTests"
```

Expected: FAIL because no item packet builder/idempotence integration exists.

- [ ] **Step 4: Implement exact packet grouping and allowlist**

```csharp
internal static class MortalItemRepairPacketBuilder
{
    internal const string PacketKind = "mortal_item_materialization_repair";
    internal static ValidationRepairHarnessPacket Build(
        string coordinate,
        IReadOnlyList<ValidationIssue> issues);
}
```

Allow only the files listed in `contracts/mortal-item-repair-packet.md`; intersect issue paths with that set; always exclude `MortalItemIdentityState.StatePath`, receipts, snapshots, and client transitions. Steps instruct the GM to repair the exact `creationRef`/`itemId`, preserve route request identity, and never replay settlement.

- [ ] **Step 5: Integrate packet selection and freshness**

Group `mortal_item_materialization_` issues by exact `mortal_item:new:<creationRef>` or `mortal_item:existing:<itemId>` actor coordinate. Build one packet per coordinate; do not fall back to faction/actor targets. Keep the pending snapshot and before-images until post-seal item validation succeeds; clear raw item command/output surfaces only after acceptance.

- [ ] **Step 6: Run repair filters green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemRepairPacketBuilderTests|FullyQualifiedName~PromptDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationLifecycleTests"
```

Expected: PASS for every route, one settlement, conserved quantity, exact rollback, no client-owned target, and no stale output.

- [ ] **Step 7: Record and commit repair behavior**

Update `quickstart.md`, mark proven T048–T054, then commit:

```powershell
git add -- BookOfEternityClient/Services/MortalItemRepairPacketBuilder.cs BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs BookOfEternityClient.Tests/MortalItemRepairPacketBuilderTests.cs BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationLifecycleTests.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationLifecycleTests.Output.cs BookOfEternityClient.IntegrationTests/MortalItemMaterializationTestContext.Repair.cs specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "feat: repair mortal item materialization atomically (#1511)"
```

---

### Task 14: Keep Client Authority Out of Player Views (T055–T061)

**Files:**
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/WebUi/BrowserInventoryManagementTests.cs`
- Modify: `BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.Rendering.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.Trade.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs`
- Conditional Modify: `BookOfEternityClient.WebFrontend/src/api/contracts.ts`
- Conditional Modify: `BookOfEternityClient.WebFrontend/src/playerFacingCommandResult.ts`
- Conditional Modify: `BookOfEternityClient.WebFrontend/src/utils/playerCopy.ts`

**Interfaces:**
- Consumes: accepted current-schema semantic fixtures.
- Produces: console/browser semantic projections containing no envelope, receipt, index, carrier/path, or repair internals.

- [ ] **Step 1: Write console/browser privacy assertions**

```csharp
private static readonly string[] ForbiddenPlayerTokens =
{
    "materialization", "materializationReceipt", "creationRef", "receiptId",
    "seal", "originMaterializationIds", "parentItemIds", "currentCarrier",
    "item_identity_index.json", "mortal_item_materialization_repair"
};

[Fact]
public async Task ItemDetails_ShowSemanticsButNoAuthorityInternals()
{
    var payload = await RenderAcceptedMechanicBearingItemAsync();
    Assert.Contains("Клинок", payload, StringComparison.Ordinal);
    Assert.Contains("урон", payload, StringComparison.OrdinalIgnoreCase);
    Assert.All(ForbiddenPlayerTokens, token => Assert.DoesNotContain(token, payload, StringComparison.OrdinalIgnoreCase));
}
```

Use the same token set for console output, browser result DTO, and local-action prompt payload.

- [ ] **Step 2: Run projection tests red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~BrowserInventoryManagementTests|FullyQualifiedName~BrowserStorageTransportParityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
```

Expected: at least one new internal-token assertion FAILS until denylist/allowlist projection is applied.

- [ ] **Step 3: Harden console projection**

Add these exact keys to the internal-field denylist:

```csharp
"materialization", "materializationReceipt", "creationRef",
"receiptId", "seal", "instanceKind", "parentItemIds",
"originMaterializationIds", "currentCarrier", "transitions"
```

Keep the semantic catch-all for GM-authored scalar/array mechanics so new accepted semantics remain visible.

- [ ] **Step 4: Harden browser projection**

Map item JSON into an explicit semantic DTO/`JsonObject` allowlist containing display, physical, mechanics, equipment, container, consumption, readable, crafting, bond, and quest fields. Never serialize the identity-index root. Change frontend TypeScript only if the C# payload test proves internal fields reach it; otherwise record the no-frontend-change rationale.

- [ ] **Step 5: Run projection tests green and inspect payloads**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~BrowserInventoryManagementTests|FullyQualifiedName~BrowserStorageTransportParityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
```

Expected: PASS. Save one simple and one mechanic-bearing payload excerpt in `quickstart.md`, with all forbidden tokens absent.

- [ ] **Step 6: Commit player projection privacy**

```powershell
git add -- BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.Rendering.cs BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.Trade.cs BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs BookOfEternityClient/WebUi/ExplorerWebCommandService.cs BookOfEternityClient.Tests/WebUi/BrowserInventoryManagementTests.cs BookOfEternityClient.Tests/WebUi/BrowserStorageTransportParityTests.cs BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "fix: hide item authority from player views (#1511)"
```

If frontend source changed, add only its exact modified paths to this command and run its repository-defined test/build command before committing.

---

### Task 15: Migrate Positive State and Synchronize GM Contracts (T062–T071)

**Files:**
- Modify: `FileSystemExample/game_session/game_state/inventory/items.json`
- Create: `FileSystemExample/game_session/game_state/inventory/item_identity_index.json`
- Modify: `FileSystemExample/game_session/game_state/npcs/item_journals.json`
- Modify: `Rules/Block_2.txt`, `Rules/Block_5.txt`, `Rules/Block_9.txt`, `Rules/Block_10.txt`, `Rules/Block_11.txt`, `Rules/Block_19.A.txt`, `Rules/Block_20.txt`, `Rules/Block_CLI_Operations.txt`
- Modify: `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`, `TaskGuides/CLI_Step_Main.txt`, `Examples/E_CLI_Step_Main.txt`
- Modify: `Examples/E_Block_9.txt`, `Examples/E_Block_10.txt`, `Examples/E_Block_11.txt`, `Examples/E_Block_19.A.txt`, `Examples/E_Block_20.txt`
- Create: `Examples/E_CLI_Mortal_Item_Materialization.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify: `BookOfEternityClient/game_master_daemon.ps1`
- Modify: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs`

**Interfaces:**
- Consumes: final runtime field names and route behavior from Tasks 3–14.
- Produces: no receipt-less positive fixture, complete GM authoring instructions, and executable examples.

- [ ] **Step 1: Write red source-guard and fixture assertions**

```csharp
[Fact]
public void MortalItemDocs_NameEveryRouteAndForbidClientAuthoredIdentity()
{
    var corpus = ReadRequiredDocs();
    Assert.All(new[] { "player_acquisition", "npc_acquisition", "new_npc_inventory", "loot_acquisition", "craft_output", "trade_output", "quest_reward", "storage_placement" }, route => Assert.Contains(route, corpus, StringComparison.Ordinal));
    Assert.Contains("не создавай itemId", corpus, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("не создавай materializationReceipt", corpus, StringComparison.OrdinalIgnoreCase);
}
```

Add tests requiring Blocks 9/11/20 and examples E_Block_9/E_Block_11/E_Block_20, current-schema `contentsPath`, no legacy promotion text, one mundane empty-section example, one mechanic-bearing craft/trade example, and a named receipt-less rejection example.

- [ ] **Step 2: Run docs/fixture filters red**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~MortalItemTestFixtureTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~ExampleDocumentationValidationTests"
```

Expected: FAIL until fixtures, rules, examples, and manifest are synchronized.

- [ ] **Step 3: Migrate the active sword and shared positive builders**

Give the sword equal permanent IDs, a complete envelope, a valid deterministic receipt seal, and one matching active index entry with a `create` transition. Rewrite journal/sidecar links to the permanent ID. Use the fixture migration inventory to convert every `positive-current` row and keep receipt-less state only in tests whose names say `RejectsReceiptless`.

- [ ] **Step 4: Update GM rules and lifecycle guidance**

Every affected rule must state:

```text
- New durable Mortal items use existedId = null plus one turn-unique creationRef.
- The GM writes the complete materialization envelope and all governed semantic fields.
- The GM never writes itemId, materializationReceipt, item_identity_index.json, seals, transitions, or lineage.
- Existing physical transfers use the exact accepted itemId and preserve the envelope/receipt.
- Split creates a client-derived child identity; merge preserves the selected survivor; discard retires as destroyed.
- Receipt-less current items are invalid because the game has not shipped and no save-compatibility path exists.
```

Block 9 covers craft, Block 11 transfer, Block 20 storage placement, and Blocks 10/19.A player/NPC inventory/trade behavior.

- [ ] **Step 5: Add executable worked examples and manifest rows**

`E_CLI_Mortal_Item_Materialization.txt` must include:

1. a mundane player-acquisition item with all empty sections and physical empty shapes;
2. a mechanic-bearing craft or trade item with matching structured evidence;
3. a transfer preserving exact ID/receipt;
4. split/merge lineage outcomes;
5. a receipt-less input and its exact rejection code.

Register every example and expected diagnostic in `example_validation_manifest.json`.

- [ ] **Step 6: Run docs/fixture filters green**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~MortalItemTestFixtureTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~ExampleDocumentationValidationTests"
```

Expected: PASS for source guards, fixture integrity, and executable manifest examples.

- [ ] **Step 7: Check afterlife documentation boundary**

Inspect:

```powershell
git diff -- OtherGuides/Afterlife_Contract_Matrix.md Examples/E_CLI_Afterlife_Turns.txt BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs
```

Expected: no diff because no afterlife contract changed. Record that rationale in `quickstart.md`; if shared behavior actually changed an afterlife surface, update the matrix/example/manifest/source guard and run the required afterlife documentation filters.

- [ ] **Step 8: Commit synchronized state and GM contracts**

```powershell
git add -- FileSystemExample/game_session/game_state/inventory/items.json FileSystemExample/game_session/game_state/inventory/item_identity_index.json FileSystemExample/game_session/game_state/npcs/item_journals.json Rules/Block_2.txt Rules/Block_5.txt Rules/Block_9.txt Rules/Block_10.txt Rules/Block_11.txt Rules/Block_19.A.txt Rules/Block_20.txt Rules/Block_CLI_Operations.txt TaskGuides/CLI_Step_Main.txt Examples/E_CLI_Step_Main.txt Examples/E_Block_9.txt Examples/E_Block_10.txt Examples/E_Block_11.txt Examples/E_Block_19.A.txt Examples/E_Block_20.txt Examples/E_CLI_Mortal_Item_Materialization.txt Examples/example_validation_manifest.json CLI_API_Specification.md CLI_Agent_Daemon_Specification.md BookOfEternityClient/game_master_daemon.ps1 BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs specs/1511-complete-item-materialization/fixture-migration-inventory.md specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "docs: teach complete mortal item materialization (#1511)"
```

---

### Task 16: Run Bounded Verification and Review the Candidate (T072–T078)

**Files:**
- Modify: `specs/1511-complete-item-materialization/quickstart.md`
- Modify: `specs/1511-complete-item-materialization/tasks.md`

**Interfaces:**
- Consumes: complete implementation candidate.
- Produces: fresh focused, Fast, FullValidation, LifecycleIntegration, manual privacy, hygiene, and review evidence.

- [ ] **Step 1: Check hygiene without deleting user-owned artifacts**

```powershell
git diff --check
git status --short
git ls-files --others --exclude-standard
```

Expected: only known `.serena/` and `bin/obj` untracked paths; no feature-owned generated output is staged.

- [ ] **Step 2: Run the fast-project focused group**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemMaterializationContractTests|FullyQualifiedName~MortalItemCarrierCatalogTests|FullyQualifiedName~MortalItemIdentityTransitionTests|FullyQualifiedName~MortalItemRepairPacketBuilderTests|FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~BrowserInventoryManagementTests|FullyQualifiedName~BrowserStorageTransportParityTests"
```

Expected: exit `0`, failures `0`, timeout false, cleanup complete.

- [ ] **Step 3: Run the integration focused groups**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalItemMaterializationValidationTests|FullyQualifiedName~MortalItemMaterializationLifecycleTests|FullyQualifiedName~CanonicalStateNormalizerTests.MortalItems"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~QuestRewardAuthorityValidationTests|FullyQualifiedName~NpcTradeRequestValidationTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~ExampleDocumentationValidationTests"
```

Expected: each command exits `0` with no timeout or cleanup failure.

- [ ] **Step 4: Run one Fast checkpoint**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: complete `BookOfEternityClient.Tests` project passes inside five minutes.

- [ ] **Step 5: Run the two boundary diagnostics**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
```

Expected: both exit `0`; FullValidation proves docs/examples pipeline, LifecycleIntegration proves accepted-turn lease/snapshot/repair/rollback lifecycle.

- [ ] **Step 6: Perform manual privacy and authority inspection**

Inspect one mundane and one mechanic-bearing console/browser payload. Confirm semantic name/description/mechanics are visible and every token from `ForbiddenPlayerTokens` is absent. Inspect the index and receipt directly to confirm they remain present in canonical state.

- [ ] **Step 7: Self-review against every FR/SC and task**

For every FR-001–FR-043 and SC-001–SC-010, cite an implementation/test/doc path in `quickstart.md`. Mark T055–T078 only after the cited diff and result evidence exists. Leave T079–T080 unchecked.

- [ ] **Step 8: Apply the code-review skills**

Use `requesting-code-review`, then process findings with `receiving-code-review`. Review exact identity, envelope/receipt immutability, one lease, exact rollback, one-pass complexity, route idempotence, minimal repair targets, docs synchronization, and player privacy. Add resolved findings and verification reruns to `quickstart.md`.

- [ ] **Step 9: Commit the reviewed candidate evidence**

```powershell
git add -- specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "test: verify mortal item materialization candidate (#1511)"
```

---

### Task 17: PreMerge, PR, and Merge to Main (T079–T080)

**Files:**
- Modify: `specs/1511-complete-item-materialization/quickstart.md`
- Modify: `specs/1511-complete-item-materialization/tasks.md`

**Interfaces:**
- Consumes: reviewed exact candidate commit from Task 16.
- Produces: one clean-checkout PreMerge result, merged PR linked to #1511, and a closed issue.

- [ ] **Step 1: Create a clean-checkout verification worktree for the exact commit**

```powershell
$candidateCommit = git rev-parse HEAD
$verifyRoot = Join-Path (Split-Path -Parent (Get-Location)) 'boe-1511-premerge'
git worktree add --detach $verifyRoot $candidateCommit
```

Expected: `$verifyRoot` resolves below `E:\Games\worktrees` and points to the exact candidate commit. Do not remove any existing worktree.

- [ ] **Step 2: Run one final PreMerge in the clean checkout**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Run from `$verifyRoot`. Expected: exit `0`, failures `0`, duplicate IDs `0`, timeout false, cleanup complete. Do not run another Fast immediately before or after it.

- [ ] **Step 3: Record exact PreMerge evidence on the feature branch**

Back in `E:\Games\worktrees\boe-1510-design`, append candidate commit, result directory, totals, and cleanup evidence to `quickstart.md`; mark T079 `[x]`; commit only those files:

```powershell
git add -- specs/1511-complete-item-materialization/quickstart.md specs/1511-complete-item-materialization/tasks.md
git diff --cached --check
git commit -m "test: record clean item materialization premerge (#1511)"
```

Because this evidence commit changes only durable test records, verify `git diff HEAD^ --` contains no runtime/test source; the tested runtime candidate remains byte-identical.

- [ ] **Step 4: Push and open the issue-linked PR**

```powershell
git push -u origin 1511-complete-item-materialization
gh pr create --base main --head 1511-complete-item-materialization --title "Complete Mortal item materialization" --body-file specs/1511-complete-item-materialization/quickstart.md
```

The PR body must say `Closes #1511`, list exact focused/Fast/FullValidation/LifecycleIntegration/PreMerge evidence, state that the game is unreleased and no save compatibility was added, and record the no-afterlife-contract-change rationale.

- [ ] **Step 5: Confirm checks and merge the reviewed commit**

```powershell
gh pr checks --watch
gh pr view --json number,state,mergeStateStatus,headRefOid
gh pr merge --merge --delete-branch
```

Expected: required checks pass, `headRefOid` matches the reviewed branch head, and the PR becomes merged.

- [ ] **Step 6: Confirm issue closure and main integration**

```powershell
gh issue view 1511 --json state,url
git fetch origin main
git log -1 --oneline origin/main
```

Expected: issue #1511 is closed and `origin/main` contains the merged PR. Update the roadmap status if the project board does not close it automatically.

- [ ] **Step 7: Mark T080 only after external state is verified**

Change T080 to `[x]` only if the PR is merged, issue #1511 is closed, and the roadmap status is updated. Do not amend or force-push the already merged branch merely to record this final checkbox; report the external evidence in the final handoff.

---

## Self-Review Result

- Every FR-001–FR-043 and SC-001–SC-010 maps to at least one task above and to the corresponding Spec Kit T001–T080 task range.
- All eight creation routes, four carrier kinds, same-turn companions, transfers, split/merge/consume/destroy, repair, projection, fixtures, GM documentation, and final integration have explicit red/green commands.
- Type names remain consistent across tasks: `MortalItemMaterializationContract`, `MortalItemIdentityState`, `MortalItemCarrierCatalog`, `MortalItemRouteAuthorityCatalog`, `MortalItemTransitionWriter`, and `MortalItemRepairPacketBuilder`.
- The plan contains no legacy promotion, afterlife item implementation, new ground-loot carrier, GM-authored identity path, or unbounded test command.
