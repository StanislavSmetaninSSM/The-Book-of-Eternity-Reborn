# Complete Mortal Location Materialization Implementation Plan

> **For implementation sessions:** REQUIRED SUB-SKILL: Use
> `executing-plans` to execute this plan task-by-task. Use
> `test-driven-development` for every behavior slice, `systematic-debugging` for
> unexpected failures, `requesting-code-review` before merge, and
> `verification-before-completion` before any success claim.

**Goal:** Make every durable Mortal World location and link enter canonical
state through one complete GM-authored materialization package, receive immutable
client-owned identity evidence, compose atomically with the current scene and
other materialized entities, and remain discovery-safe in console/browser views.

**Architecture:** `world_map.json.locations[]` is the sole durable location
semantic authority and `world_map.json.links[]` is the sole directed topology
authority. A pure accepted-turn planner validates raw current/remote routes,
assigns client IDs, builds receipts/index transitions, and prepares field-aware
reference rewrites. Under the existing canonical write lease, location state is
normalized before Mortal items, the remaining normalizers run, and combined
post-validation either commits every tracked file or restores exact before-images.
`current_location.json` is rebuilt from one canonical map object plus scene-local
operations. All player readers consume one discovery-aware safe projection.

**Tech Stack:** C# 12, .NET 8, `System.Text.Json` / `JsonNode`, xUnit 2.9.2,
existing `FileSystemManager.CanonicalWriteLease`, `CanonicalStateNormalizer`,
`ValidationService`, `StateDistributor`, Spectre.Console, browser C# DTO
builders, and PowerShell 7 test lanes.

## Global Constraints

- Source task is GitHub issue [#1513](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1513); no repository change may drift outside it.
- The game is unreleased. Receipt-less positive locations/links are invalid; do not add migration commands, promotion classifiers, or fallback readers.
- Mortal World only. Shining Abode halls and Chaos Sea Guardian planes remain #1514; transport/storage entity materialization remains #1515.
- The GM owns complete location/link semantics and the immutable root envelope. The client owns permanent IDs, receipts, seals, index state, transitions, scaffold reservations, and derived exits.
- Compare every operational identity with `StringComparer.Ordinal`; reject case, whitespace, Unicode-normalization, and confusable aliases without using them to select a target.
- Names, slugs, coordinates, ordinals, `knownExits`, and `adjacencyMap` never establish authority.
- A new selected location has exactly one `currentLocationData` carrier; a new remote location has exactly one `worldMapUpdates.newLocations[]` carrier; duplicates across routes are invalid.
- One directed link creates one edge. Never infer a reverse link.
- Map storage owns metadata only. Current storage contents remain governed by #1511 item materialization/transitions.
- Do not recursively replace arbitrary strings. Rewrite only catalogued identity fields.
- Keep raw validation, plan generation, all writes, and post-validation inside one accepted-turn rollback contour.
- Use `pwsh -NoProfile -File .\scripts\test-csharp.ps1`; during work run the smallest Focused filter, one meaningful Fast, conditional FullValidation/LifecycleIntegration, and one final PreMerge without a duplicate final Fast.
- Preserve unrelated/untracked `bin/obj` artifacts; stage only #1513-owned files.
- Update GM rules, CLI guidance, examples, manifest, fixtures, and source guards in the same change as the executable contract.

## File Responsibility Map

| File | Responsibility |
| --- | --- |
| `BookOfEternityClient/Services/MortalLocationMaterializationContract.cs` | Exact raw/canonical envelope, complete section, receipt, discovery, and link shapes. |
| `BookOfEternityClient/Services/MortalLocationIdentityState.cs` | Client index, receipts/seals, active/retired history, transitions, exact/confusable identity. |
| `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlan.cs` | Immutable route candidates, ID maps, canonical after-images, rewrites, touched paths, repair context. |
| `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs` | Build one-pass indexes, classify routes, resolve endpoints/references, and compose the plan without writes. |
| `BookOfEternityClient/Services/Validation/ValidationService.MortalLocationMaterialization.cs` | Raw pre-seal and canonical post-seal orchestration. |
| `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs` | Consume commands, materialize map/link/index/current projection, apply field-aware rewrites. |
| `BookOfEternityClient/Services/MortalLocationRepairPacketBuilder.cs` | Exact bounded GM repair packets and protected/ambiguous pre-dispatch stop. |
| `BookOfEternityClient/Services/MortalLocationPlayerProjection.cs` | Canonical acceptance, discovery visibility, semantic projection, recursive privacy. |
| `BookOfEternityClient.TestSupport/MortalLocationTestFixture.cs` | Deterministic complete raw/canonical/index/link fixtures shared by both test projects. |

---

### Task 1: Reconfirm Baseline and Classify Fixtures/Readers (Spec Kit T001–T005)

**Files:**
- Create: `specs/1520-complete-location-materialization/fixture-migration-inventory.md`
- Modify: `specs/1520-complete-location-materialization/research.md`
- Modify: `specs/1520-complete-location-materialization/quickstart.md`
- Modify: `specs/1520-complete-location-materialization/tasks.md`

- [ ] **Step 1: Reconfirm branch, issue, and worktree ownership**

Run:

```powershell
git branch --show-current
git status --short --branch
gh issue view 1513 --json number,state,title,url
```

Expected: branch `1520-complete-location-materialization`; issue #1513 open;
only planning files plus known generated `bin/obj` paths are uncommitted.

- [ ] **Step 2: Re-read durable requirements**

Read `AGENTS.md`, `.specify/memory/constitution.md`, every file under
`specs/1520-complete-location-materialization/`, and
`docs/superpowers/specs/2026-08-12-mortal-location-materialization-design.md`.
If GitHub scope differs, stop and update the spec before code.

- [ ] **Step 3: Inventory repository location fixtures**

Run:

```powershell
rg -l '"(currentLocationData|worldMapUpdates|newLocations|newLinks|knownExits|adjacencyMap|locationId|initialLocationId)"' FileSystemExample BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests Examples | Sort-Object
```

Create this table and classify every active path:

```markdown
# Mortal Location Fixture Migration Inventory

| Path | Fixture/member | Current authority shape | Classification | #1513 action | Receipt-less allowed after #1513 |
| --- | --- | --- | --- | --- | --- |
| `FileSystemExample/game_session/game_state/world/current_location.json` | active current scene | receipt-less root | positive-current | replace with projected receipt-bearing current scene and add map/index | no |
```

Allowed classifications: `positive-current`, `negative-receiptless`,
`negative-contract`, `backup-baseline`, `fragment-only`.

- [ ] **Step 4: Inventory all production readers/writers**

Run:

```powershell
rg -n 'current_location\.json|world_map\.json|knownExits|adjacencyMap|newLocations|newLinks|locationUpdates|ResolveLocationId|ReadKnownLocationIds' BookOfEternityClient -g '*.cs'
```

Append a table to `research.md` with path, method, current source, required
canonical replacement, and owning plan task. Do not omit news, locality,
training, trade, NPC/faction validation, actor memory, storage, or item routes.

- [ ] **Step 5: Record the already-run clean Fast baseline**

In `quickstart.md`, record:

```text
Lane: Fast
Result: 2937 passed, 0 failed, 0 timed out, 0 duplicate executions
Result directory: E:\Games\worktrees\boe-1513-location-materialization\TestResults\test-lanes\20260812-114707-101-31724-220f790e8f1b43a797dd7a5c968d90af-fast
```

Inspect its `summary.json` before marking T001–T005 complete.

- [ ] **Step 6: Commit only the baseline inventory**

```powershell
git add -- specs/1520-complete-location-materialization/fixture-migration-inventory.md specs/1520-complete-location-materialization/research.md specs/1520-complete-location-materialization/quickstart.md specs/1520-complete-location-materialization/tasks.md
git diff --cached --check
git commit -m "test: inventory Mortal location authority (#1513)"
```

---

### Task 2: Build Deterministic Test Fixtures (T006–T010)

**Files:**
- Create: `BookOfEternityClient.TestSupport/MortalLocationTestFixture.cs`
- Create: `BookOfEternityClient.Tests/MortalLocationTestFixtureTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationTestContext.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationAssertions.cs`

**Required public test API:**

```csharp
public static class MortalLocationTestFixture
{
    public const string LocationInitialId = "locref_test_black_ford";
    public const string LocationId = "loc_test_black_ford";
    public const string LocationMaterializationId = "mlocmat_test_black_ford";
    public const string LocationReceiptId = "mlocrec_test_black_ford";
    public const string LinkInitialId = "linkref_test_ford_to_tower";
    public const string LinkId = "lnk_test_ford_to_tower";

    public static JsonObject CreateRawLocation(string route = "world_map_creation");
    public static JsonObject CreateCanonicalLocation(string discoveryTier = "visited");
    public static JsonObject CreateRawLink(string sourceLocationId, string targetLocationId);
    public static JsonObject CreateCanonicalLink(string sourceLocationId, string targetLocationId);
    public static JsonObject CreateWorldMap(params JsonObject[] locations);
    public static JsonObject CreateIdentityIndex(JsonObject location, JsonObject? link = null);
    public static JsonObject CreateCurrentProjection(JsonObject canonicalLocation);
    public static JsonObject CreateReceiptlessNegative();
}
```

- [ ] **Step 1: Write fixture-shape tests first**

Add tests named:

```csharp
[Fact] public void CreateRawLocation_HasNullPermanentIdAndEverySection();
[Fact] public void CreateCanonicalLocation_HasReceiptAndNoTemporarySelectors();
[Fact] public void CreateCanonicalLink_BindsExactEndpointsAndIndex();
[Fact] public void CreateCurrentProjection_MatchesCanonicalSharedFields();
[Fact] public void CreateReceiptlessNegative_IsExplicitlyInvalid();
```

- [ ] **Step 2: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationTestFixtureTests"
```

Expected: compile failure because `MortalLocationTestFixture` does not exist.

- [ ] **Step 3: Implement deterministic fixtures**

Emit every governed field physically. Use fixed IDs and a fixed test seal; do not
call production seal logic from the fixture builder. Make mutations return deep
clones so tests cannot share state.

- [ ] **Step 4: Add file-backed context**

Implement:

```csharp
internal sealed class MortalLocationMaterializationTestContext : IAsyncDisposable
{
    internal FileSystemManager FileSystem { get; }
    internal ValidationService Validator { get; }
    internal CanonicalStateNormalizer Normalizer { get; }

    internal static Task<MortalLocationMaterializationTestContext> CreateAsync();
    internal Task WriteJsonAsync(string relativePath, JsonNode value);
    internal Task<JsonNode?> ReadJsonAsync(string relativePath);
    internal Task CaptureValidatedPendingSnapshotAsync(int turn = 42);
    internal Task<IReadOnlyDictionary<string, string?>> CaptureBytesAsync(params string[] paths);
    public ValueTask DisposeAsync();
}
```

Use an owned temporary session root and repository snapshot helpers. Never delete
outside that root.

- [ ] **Step 5: Add exact rollback/privacy assertions**

Provide helpers that compare file existence plus exact text bytes and scan
player payloads for the forbidden location/item authority token catalog.

- [ ] **Step 6: Run GREEN plus one existing map control**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationTestFixtureTests|FullyQualifiedName~LocalMapViewerServiceTests"
```

- [ ] **Step 7: Commit test infrastructure**

```powershell
git add -- BookOfEternityClient.TestSupport/MortalLocationTestFixture.cs BookOfEternityClient.Tests/MortalLocationTestFixtureTests.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationTestContext.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationAssertions.cs specs/1520-complete-location-materialization/tasks.md
git diff --cached --check
git commit -m "test: add Mortal location fixture vocabulary (#1513)"
```

---

### Task 3: Implement Contract and Identity Authority (T011–T023)

**Files:**
- Create: `BookOfEternityClient/Services/MortalLocationMaterializationContract.cs`
- Create: `BookOfEternityClient/Services/MortalLocationIdentityState.cs`
- Create: `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlan.cs`
- Create: `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs`
- Create: `BookOfEternityClient/Services/Validation/ValidationService.MortalLocationMaterialization.cs`
- Create: `BookOfEternityClient.Tests/MortalLocationMaterializationContractTests.cs`
- Create: `BookOfEternityClient.Tests/MortalLocationIdentityStateTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Routes.cs`
- Modify: validation phase files named in T023

**Production interfaces:**

```csharp
internal static class MortalLocationMaterializationContract
{
    internal const string WorldMapPath = "game_state/world/world_map.json";
    internal const string CurrentLocationPath = "game_state/world/current_location.json";
    internal static IReadOnlyList<ValidationIssue> ValidateRawLocation(JsonElement value, string context, string expectedRoute);
    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalLocation(JsonElement value, string context);
    internal static IReadOnlyList<ValidationIssue> ValidateRawLink(JsonElement value, string context, string expectedRoute);
    internal static IReadOnlyList<ValidationIssue> ValidateCanonicalLink(JsonElement value, string context);
}

internal sealed class MortalLocationIdentityState
{
    internal const string StatePath = "game_state/world/location_identity_index.json";
    internal static MortalLocationIdentityState Parse(JsonNode? node);
    internal bool ContainsHistoricalLocationOrigin(string? initialId, string? materializationId);
    internal bool ContainsHistoricalLinkOrigin(string? initialId, string? materializationId);
    internal JsonObject ToJson();
}
```

- [ ] **Step 1: Write RED contract rows**

Cover every closed field/disposition, indoor/outdoor exclusivity, discovery pair,
complete arrays, source authority, client-owned keys, duplicate properties, and
link endpoint xor rules. Each mutation should produce one stable issue code.

- [ ] **Step 2: Run contract RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationMaterializationContractTests"
```

- [ ] **Step 3: Implement minimal exact contract**

Use exact property lookup and explicit allowlists. Do not call `.Trim()` to
normalize identity, use `OrdinalIgnoreCase` to resolve it, or accept name aliases.

- [ ] **Step 4: Write RED identity/index rows**

Cover active/retired exact duplicates, case variants, leading/trailing
whitespace, NFC/NFD equivalents, confusables, independent field-wise historical
matching, receipt/index mismatch, malformed client state, and one-pass scan
metrics.

- [ ] **Step 5: Implement index and seal ownership**

Generate production IDs with `Guid.NewGuid().ToString("N")` once inside an
accepted plan. Keep test IDs injectable through an internal factory. Seal a
stable canonical serialization of receipt evidence plus the accepted envelope.

- [ ] **Step 6: Write route and complete semantic RED tests**

Add current creation, remote creation, duplicate cross-route, two valid remote
candidates, existing full resend, receipt-less canonical state, coordinate
collision, parent cycle, and isolated topology cases.

- [ ] **Step 7: Implement immutable plan models and pure planner skeleton**

The plan must expose final map/current/index objects, exact temporary-to-permanent
maps, accepted same-turn storage coordinates, governed rewrite operations,
touched paths, and repair contexts. No method in the planner writes a file.

- [ ] **Step 8: Register the validation phase**

Use the free bit:

```csharp
AcceptedTurnLocationMaterializationCompleteness = 1u << 30
```

Add it to `All`, selectable profiles, response validation, and game-state
validation in the established order. Prove profile selection with a focused
test before making callers depend on it.

- [ ] **Step 9: Run focused GREEN**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationMaterializationContractTests|FullyQualifiedName~MortalLocationIdentityStateTests|FullyQualifiedName~ValidationPhaseSelectionTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests"
```

- [ ] **Step 10: Commit contract foundation**

```powershell
git add -- BookOfEternityClient/Services/MortalLocationMaterializationContract.cs BookOfEternityClient/Services/MortalLocationIdentityState.cs BookOfEternityClient/Services/MortalLocationAcceptedTurnPlan.cs BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs BookOfEternityClient/Services/Validation/ValidationService.MortalLocationMaterialization.cs BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs BookOfEternityClient/Services/ValidationService.cs BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs BookOfEternityClient.Tests/MortalLocationMaterializationContractTests.cs BookOfEternityClient.Tests/MortalLocationIdentityStateTests.cs BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.cs BookOfEternityClient.IntegrationTests/MortalLocationMaterializationValidationTests.Routes.cs specs/1520-complete-location-materialization/tasks.md
git diff --cached --check
git commit -m "feat: define Mortal location authority (#1513)"
```

---

### Task 4: Normalize Map, Current Projection, and Index Atomically (T015, T024–T028)

**Files:**
- Create: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs`
- Create: `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.MortalLocations.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.InventoryNpcWorldCrossRefs.cs`
- Modify: state-file allowlist/protection files from T027

- [ ] **Step 1: Write normalizer RED tests**

Tests must assert exact after-images for current creation, remote creation,
multiple remote creations, wrapper removal, link endpoint rewrite, receipt/index
agreement, current shared-field equality, operational field retention, and no
write when the plan is invalid.

- [ ] **Step 2: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests.MortalLocations"
```

Expected: fail because the location normalizer partial and path registration do
not exist.

- [ ] **Step 3: Implement plan application**

Add `NormalizeMortalLocationsAsync(backups)` as the first accumulated-state
normalizer. It reads pre-turn backups, constructs one accepted plan, and writes
only its final map/current/index/governed after-images through the bound lease.
It must preserve current storage `contents` verbatim for the item normalizer.

- [ ] **Step 4: Extend tracked paths**

Add exact world map, current location, location index, and governed reference
files to `CanonicalAccumulatedFiles`, `NormalizerBackupInputFiles`, and
`NormalizerRollbackTrackedFiles`. Preserve distinct ordering but use ordinal
path equality for new logic.

- [ ] **Step 5: Remove old canonical aliases**

Replace recursive `EnumerateLocationLikeObjects`, name authority, wrapper
authority, missing-z compatibility, and durable adjacency validation with exact
`world_map.locations[]` / `links[]` plus current projection coherence. Keep raw
command validation in the new partial, not in canonical readers.

- [ ] **Step 6: Protect the new index**

Add it to client-owned state mappings and forbid it in GM-authorable paths,
repair targets, raw response fields, and generic distributor merge surfaces.

- [ ] **Step 7: Run normalizer and contract GREEN**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~CanonicalStateNormalizerTests.MortalLocations|FullyQualifiedName~MortalLocationMaterializationValidationTests"
```

- [ ] **Step 8: Commit canonical projection**

Stage the named production/test files explicitly, run `git diff --cached --check`,
then commit:

```powershell
git commit -m "feat: normalize Mortal location state (#1513)"
```

---

### Task 5: Replace Bootstrap Placeholders with Ordinary Materialization (T029–T037)

**Files:**
- Modify: `BookOfEternityClient/Services/MortalBootstrapStateBuilder.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs`
- Create or modify: `BookOfEternityClient.IntegrationTests/PendingTurnSnapshotTests.cs`

- [ ] **Step 1: Add neutral-root RED assertions**

Assert fresh state contains exact empty canonical map/index and pending current
root, not `loc_life_*_start`, a pseudo neighbor, raw `newLocations`, `newLinks`,
`knownExits`, or `adjacencyMap`.

- [ ] **Step 2: Add scaffold RED assertions**

Assert the client scaffold contains exact start/neighbor/link temporary
references, coordinate constraints, permanent reservation evidence, request ID,
and two allowed completion branches. Assert those client fields are not copied
into GM-writable files.

- [ ] **Step 3: Add golden and negative lifecycle tests**

Cover complete start+neighbor+link, narrative-only exit, missing start, partial
start, alias reservation, duplicate creation carrier, fake neighbor identity,
and settled reservation replay.

- [ ] **Step 4: Run bootstrap RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalBootstrapValidationTests|FullyQualifiedName~PendingTurnSnapshotTests"
```

- [ ] **Step 5: Implement neutral bootstrap files**

Change `BuildFreshMortalBootstrapFiles` to write the three exact neutral roots.
Delete `BuildCurrentLocation` / legacy command-shaped `BuildWorldMap` behavior or
replace them with current-schema builders; do not leave an unused compatibility
helper.

- [ ] **Step 6: Implement scaffold reservations and reminder**

Rewrite scaffold construction and system reminder to request ordinary complete
materialization. Remove all claims that current/map locations are already
materialized and all GM instructions to author adjacency summaries.

- [ ] **Step 7: Validate and consume scaffold exactly once**

The accepted planner must match exact refs, request authority, and coordinates.
Only successful post-validation consumes reservations. A failed/repair turn
retains the same pending scaffold.

- [ ] **Step 8: Run GREEN and commit**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalBootstrapValidationTests|FullyQualifiedName~PendingTurnSnapshotTests"
git add -- BookOfEternityClient/Services/MortalBootstrapStateBuilder.cs BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs BookOfEternityClient.IntegrationTests/MortalBootstrapValidationTests.cs BookOfEternityClient.IntegrationTests/PendingTurnSnapshotTests.cs specs/1520-complete-location-materialization/tasks.md
git diff --cached --check
git commit -m "feat: materialize fresh Mortal locations (#1513)"
```

---

### Task 6: Implement Exact Topology, Movement, and Locality (T038–T048)

**Files:**
- Create: topology/lifecycle integration test partials named in tasks.md
- Modify: `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/MortalLocationIdentityState.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalLocations.cs`
- Modify: `BookOfEternityClient/Services/LocalMapViewService.cs`
- Modify: `BookOfEternityClient/Services/LocalInteractionScopeService.cs`
- Modify: `BookOfEternityClient/Services/ActorMemoryService.cs`
- Modify: `BookOfEternityClient/Services/NpcTradeService.cs`

- [ ] **Step 1: Write directed topology RED matrix**

Use Theory rows for road, path, portal, one-way, hidden path, sealed passage, and
isolated location. Include exact endpoint rewrite, no reverse edge, self/dangling
endpoint, duplicate coordinate, parent cycle, and cross-realm failures.

- [ ] **Step 2: Write existing lifecycle RED matrix**

Cover exact movement selection, full resend rejection, narrow mutable fields,
immutable placement/envelope/receipt, discovery transition preconditions, exact
link update/removal, and retired-origin replay.

- [ ] **Step 3: Write map/locality RED tests**

Replace expected name/slug/case-insensitive/fallback-layout behavior with exact
receipt-bearing nodes and directed edges. Add case/name collisions proving
training, trade, actor memory, and local interactions do not select aliases.

- [ ] **Step 4: Run RED groups**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~LocalMapViewerServiceTests|FullyQualifiedName~LocalInteractionScopeServiceTests|FullyQualifiedName~TrainingServiceTests|FullyQualifiedName~ConsoleTrainingCommandTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationValidationTests.Topology|FullyQualifiedName~MortalLocationMaterializationValidationTests.Lifecycle"
```

- [ ] **Step 5: Implement link transitions and derived exits**

Resolve endpoints only through the exact index; assign one permanent link ID;
append transitions; retain retired evidence. Derive current exits after every
map/topology/discovery change. Never manufacture reverse links or fallback nodes.

- [ ] **Step 6: Implement existing movement/update/discovery**

Movement accepts exact `locationId` and operational fields only. Narrow updates
use a closed allowlist. Discovery validates exact pre-state and forward edges.
All preserve root envelope and receipt.

- [ ] **Step 7: Migrate internal locality consumers**

Use accepted canonical IDs for Mortal paths while retaining the separate
afterlife behavior of `LocalInteractionScopeService`. Remove name fallbacks only
from Mortal authority code; add tests to prevent accidental afterlife regression.

- [ ] **Step 8: Run GREEN and commit**

Run the two Step 4 commands again. Stage exact files, check cached diff, commit:

```powershell
git commit -m "feat: enforce exact Mortal topology (#1513)"
```

---

### Task 7: Extend Atomic Refresh and Bounded Repair (T049–T059)

**Files:**
- Create: `BookOfEternityClient/Services/MortalLocationRepairPacketBuilder.cs`
- Create: `BookOfEternityClient.Tests/MortalLocationRepairPacketBuilderTests.cs`
- Create: lifecycle/output integration test partials from T051–T052
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`

- [ ] **Step 1: Write packet RED tests**

Assert one exact actor/raw coordinate, deterministic missing/invalid/conflict
lists, only GM-owned target files, correct companion ownership, protected-field
instructions, and no index/snapshot/item target.

- [ ] **Step 2: Write fail-closed pre-dispatch RED tests**

Cover missing identity, literal identity `unknown`, duplicate route, historical
exact/case/Unicode replay, client permanent ID, receipt, seal, index mutation,
ambiguous coordinate, and malformed canonical baseline. Assert no GM request.

- [ ] **Step 3: Write byte-exact rollback RED tests**

Capture existence/content for map, current, index, NPC, faction, item carrier,
item index, and scaffold. Inject failure after each planned write and during
combined post-validation. Assert every path matches before-image exactly and no
player confirmation survives.

- [ ] **Step 4: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationRepairPacketBuilderTests|FullyQualifiedName~ValidationRepairRequestTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalLocationMaterializationLifecycleTests|FullyQualifiedName~GameEngineTurnLifecycleTests"
```

- [ ] **Step 5: Implement packet builder**

Use a structured `MortalLocationRepairContext` on `ValidationIssue`. Build a
packet only for exactly one actionable coordinate. Missing/ambiguous/protected
identity returns no packet and `RequiresFailClosedRollback=true`.

- [ ] **Step 6: Remove legacy location packets**

Delete or bypass old instructions that register a world-map location before
current use, repair by name/coordinates, or author `knownExits`/`adjacencyMap`.
Keep unrelated generic repair packets intact.

- [ ] **Step 7: Extend the existing transaction, not create a second one**

Inside `AcceptedTurnCanonicalStateRefresh`, capture all tracked before-images,
run the bound normalizer, then aggregate post-seal Mortal item and Mortal location
issues before lease release. Restore every path on exception or any issue. Do
not mark commands/scaffold settled until both checks pass.

- [ ] **Step 8: Sanitize player failures**

Every console/browser path that can display a location transition/repair/write
failure maps it to generic Russian player-safe text. Operator logs may retain
paths/codes/IDs. Exception text is never echoed to players.

- [ ] **Step 9: Run GREEN and commit**

Run Step 4 again. Stage exact files, run cached diff check, commit:

```powershell
git commit -m "feat: make Mortal location repair atomic (#1513)"
```

---

### Task 8: Compose Actor, Faction, Lore, Threat, Storage, and Item Authority (T070–T080)

**Files:**
- Modify: actor/faction/item validation and normalizer files named in T075–T079
- Create: companion/authority integration test partials named in T070–T074

- [ ] **Step 1: Write same-turn actor/faction RED cases**

Accept exact `initialActorId` / `initialFactionId` only when their own raw
materialization validates. Reject name, case, Unicode, duplicate, cross-realm,
and actor physical-location contradiction. Preserve the current actor/faction
effective identity model; do not assign them new IDs in #1513.

- [ ] **Step 2: Write lore/threat/storage RED cases**

Require exact `codexEntryId`, `questId`, `worldEventId`, threat identity, and
unique storage ID. Reject paths/names, remote storage contents, metadata mismatch,
owner ambiguity, and duplicate storage IDs.

- [ ] **Step 3: Write same-turn item-storage RED cases**

Create a complete current location with one complete #1511 item in one storage.
Assert location normalization preserves the raw item carrier, item normalization
assigns the item ID/receipt, and both post-checks pass. Assert remote map contents,
unaccepted location/storage refs, and location repair ownership of item issues fail.

- [ ] **Step 4: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ActorMaterializationValidationTests|FullyQualifiedName~NpcCoreChangesTests|FullyQualifiedName~FactionMaterializationValidationTests|FullyQualifiedName~MortalLocationMaterializationValidationTests.Companions|FullyQualifiedName~MortalItemMaterializationValidationTests.Routes"
```

- [ ] **Step 5: Implement field-aware rewrite catalog**

Represent each rewrite as path, expected raw value, target permanent/effective
value, and owning contract. Apply only if the exact field still matches at plan
application time; otherwise reject as stale. Never descend into arbitrary
custom/narrative strings.

- [ ] **Step 6: Replace actor/faction authority scans**

Change `ReadNpcCoreAuthorityAsync`, `NpcCoreChangesContract`, and Mortal faction
validation to use canonical map indexes plus accepted same-turn plan evidence.
Remove recursive scans of current/map wrappers and OrdinalIgnoreCase maps.

- [ ] **Step 7: Integrate same-turn current storage with #1511**

Run location normalization before items. Extend `MortalItemRouteAuthorityCatalog`
to recognize only an exact storage on an accepted same-turn current-location
receipt/plan. The location code must not generate item IDs or repair packets.

- [ ] **Step 8: Run GREEN and commit**

Run Step 4 again, then stage exact files and commit:

```powershell
git commit -m "feat: bind Mortal entities to exact locations (#1513)"
```

---

### Task 9: Move Every Player Surface to the Safe Projection (T060–T069)

**Files:**
- Create: `BookOfEternityClient/Services/MortalLocationPlayerProjection.cs`
- Create: `BookOfEternityClient.Tests/MortalLocationPlayerProjectionTests.cs`
- Modify: console/browser/news files named in T065–T067
- Modify only if required by C# DTO evidence: files under `BookOfEternityClient.WebFrontend/src/`

**Projection interface:**

```csharp
internal static class MortalLocationPlayerProjection
{
    internal static MortalLocationProjectionCatalog Build(JsonObject worldMap, JsonObject? currentLocation, JsonObject identityIndex);
    internal static JsonNode? CloneSemanticValue(JsonNode? value, bool locationContext);
    internal static bool IsInternalField(string propertyName, bool locationContext);
}
```

- [ ] **Step 1: Write projection unit RED matrix**

Cover accepted canonical, receipt-less, duplicate, hidden, rumored, discovered,
visited, current mismatch, hidden/sealed/one-way link, duplicate names, numeric
IDs, and nested full envelope/receipt/index/repair/transition/carrier DTO shapes.
Include adjacent legitimate semantic objects with `route`, `state`, `kind`,
`title`, and `steps` to prevent over-filtering.

- [ ] **Step 2: Write console/browser/news RED tests**

Assert identical visible semantics/action set for each tier, zero rejected/hidden
counts, safe rumor subset, accepted storage item projection, exact non-rendered
action target, and no authority tokens in serialized payload or rendered text.

- [ ] **Step 3: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationPlayerProjectionTests|FullyQualifiedName~LocalMapViewerServiceTests|FullyQualifiedName~ExplorerMortalWorldNewsCommandResultBuilderTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
```

- [ ] **Step 4: Implement canonical acceptance and discovery policy**

Build exact maps once. Suppress invalid/duplicate/hidden entities before counts
or DTO construction. Rumors receive a dedicated minimal record. Keep permanent
IDs only in internal action payloads and never visible labels.

- [ ] **Step 5: Implement recursive context-aware privacy**

Reuse item sanitizer concepts without globally denying legitimate non-item
semantics. Suppress complete/annotated internal DTO shapes as objects; strip
isolated internal fields from mixed semantic objects; preserve safe unknown
setting-specific fields.

- [ ] **Step 6: Migrate console and browser builders**

Delete direct reads of current/map raw wrappers and adjacency/name fallbacks.
Route map/list/detail/current/storage through the shared catalog. Do not redesign
visual layout or React components unless typed DTO compilation requires it.

- [ ] **Step 7: Migrate news and action/locality joins**

Make threat/location news use exact accepted locations and discovery visibility.
Revalidate exact selector and reachability at write time; map stale failures to
safe text.

- [ ] **Step 8: Run GREEN, inspect payloads, decide frontend scope**

Run Step 3 again. Inspect one hidden, rumored, and visited browser result. If no
internal field reaches React and all needed semantics already exist in DTOs,
record `No WebFrontend source change required` in quickstart and do not edit it.

- [ ] **Step 9: Commit projection**

```powershell
git add -- BookOfEternityClient/Services/MortalLocationPlayerProjection.cs BookOfEternityClient.Tests/MortalLocationPlayerProjectionTests.cs BookOfEternityClient/Services/LocalMapViewService.cs BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaLoreAndTravel.cs BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs BookOfEternityClient/UI/ExplorerMortalWorldNewsCommandResultBuilder.cs BookOfEternityClient.Tests/LocalMapViewerServiceTests.cs BookOfEternityClient.Tests/ExplorerMortalWorldNewsCommandResultBuilderTests.cs BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.GeneralPanels.cs BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs specs/1520-complete-location-materialization/quickstart.md specs/1520-complete-location-materialization/tasks.md
git diff --cached --check
git commit -m "feat: project safe Mortal locations (#1513)"
```

---

### Task 10: Migrate Active State and Positive Fixtures (T081–T082)

**Files:**
- Modify: `FileSystemExample/game_session/game_state/world/current_location.json`
- Create: `FileSystemExample/game_session/game_state/world/world_map.json`
- Create: `FileSystemExample/game_session/game_state/world/location_identity_index.json`
- Modify: every positive fixture listed in `fixture-migration-inventory.md`
- Modify: `BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs`

- [ ] **Step 1: Add RED active-fixture integrity test**

Validate the shipped example's map/current/index with the production location
phase, assert current shared fields match the map, all active entries have
receipts, and no wrapper/adjacency authoring field exists.

- [ ] **Step 2: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests"
```

- [ ] **Step 3: Migrate the active example**

Use deterministic example IDs and a receipt/seal generated with the production
contract or a checked-in generator path; do not handwave an invalid seal. Keep
current operational weather/interactions/storage contents, map storage metadata,
and #1511 item receipts coherent.

- [ ] **Step 4: Migrate every positive fixture row**

For each row, update the action/result columns in the inventory. Receipt-less
objects remain only in tests whose names and assertions explicitly require
rejection. Delete obsolete positive compatibility assertions instead of
weakening production validation.

- [ ] **Step 5: Run fixture-focused GREEN**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FileSystemExampleFixtureIntegrityTests|FullyQualifiedName~MortalLocationMaterializationValidationTests|FullyQualifiedName~MortalBootstrapValidationTests"
```

- [ ] **Step 6: Commit fixture migration**

Stage only paths enumerated by the inventory, run cached diff check, and commit:

```powershell
git commit -m "test: migrate Mortal location fixtures (#1513)"
```

---

### Task 11: Synchronize GM Rules, Examples, CLI, and Daemon (T083–T092)

**Files:**
- Modify: `Rules/Block_20.txt`
- Modify: `Examples/E_Block_20.txt`
- Modify: `CLI_API_Specification.md`
- Modify: `CLI_Agent_Daemon_Specification.md`
- Modify: `TaskGuides/CLI_Step_Main.txt`
- Modify: `Examples/E_CLI_Step_Main.txt`
- Modify: `Rules/Block_CLI_Operations.txt`
- Modify: `BookOfEternityClient/game_master_daemon.ps1`
- Modify: `Examples/example_validation_manifest.json`
- Modify: documentation/source-guard tests from T090–T091

- [ ] **Step 1: Write documentation/source-guard RED tests**

Require exact location/link envelopes, two creation routes, null GM IDs,
client-owned receipts/index, bootstrap ordinary materialization, derived
topology, exact movement, discovery privacy, bounded repair, no compatibility,
and all three worked examples. Require absence of legacy positive instructions.

- [ ] **Step 2: Add executable example RED tests**

Parse/validate the start+neighbor+link, hidden remote/reveal, and invalid repair
examples through the actual current phase. The invalid example must match stable
expected issue codes in the manifest.

- [ ] **Step 3: Run RED**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests"
```

- [ ] **Step 4: Rewrite Rule 20 and Example 20**

Use the exact schemas/contracts from `specs/1520-complete-location-materialization/`.
Remove missing-z, name identity, coordinate endpoint, durable adjacency, full
existing resend, bootstrap placeholder, and receipt-less compatibility text.

- [ ] **Step 5: Update CLI and daemon authoring workflow**

Explain transient raw carriers versus canonical roots, exact existing movement,
protected index/receipts, bounded repair, and required guide/example reading.
Ensure the daemon forces the GM to read changed guidance before authoring the
affected turn.

- [ ] **Step 6: Audit adjacent Mortal docs**

Use:

```powershell
rg -n 'knownExits|adjacencyMap|newLocations|newLinks|locationUpdates|targetCoordinates|currentLocationData|worldMapUpdates' Rules Examples TaskGuides OtherGuides *.md BookOfEternityClient/game_master_daemon.ps1
```

Change only genuine contract duplicates. Record each inspected-but-unchanged
surface and rationale in quickstart.

- [ ] **Step 7: Register examples and run GREEN**

Update the manifest, then rerun both Step 3 commands.

- [ ] **Step 8: Check afterlife boundary**

Inspect `OtherGuides/Afterlife_Contract_Matrix.md`,
`Examples/E_CLI_Afterlife_Turns.txt`, afterlife documentation tests, and touched
shared runtime callers. If no afterlife pending/control/response/normalizer or
player command changed, record that #1514 owns afterlife location materialization
and make no afterlife doc edit.

- [ ] **Step 9: Commit synchronized contract docs**

Stage exact changed docs/tests/manifest/daemon files, run cached diff check, and
commit:

```powershell
git commit -m "docs: specify Mortal location materialization (#1513)"
```

---

### Task 12: Integrated Controls and Review (T093–T099)

**Files:**
- Modify: `specs/1520-complete-location-materialization/quickstart.md`
- Modify: `specs/1520-complete-location-materialization/tasks.md`
- Modify only as regressions require: files already owned by prior tasks

- [ ] **Step 1: Run hygiene checks**

```powershell
git diff --check
git status --short
git ls-files --others --exclude-standard
```

Do not stage or delete unrelated generated artifacts.

- [ ] **Step 2: Run bounded fast-project Focused groups**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalLocationMaterializationContractTests|FullyQualifiedName~MortalLocationIdentityStateTests|FullyQualifiedName~MortalLocationPlayerProjectionTests|FullyQualifiedName~LocalMapViewerServiceTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~ConsoleTrainingCommandTests|FullyQualifiedName~TrainingServiceTests|FullyQualifiedName~TrainingWebCommandServiceTests"
```

- [ ] **Step 3: Run bounded integration Focused groups**

Run all four integration commands from `specs/1520-complete-location-materialization/plan.md`. If a group fails, use the smallest class/method filter to diagnose; do not broaden the lane.

- [ ] **Step 4: Run one meaningful Fast checkpoint**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Inspect `summary.json` and duplicate/timeout/cleanup evidence, not only exit code.

- [ ] **Step 5: Run conditional broad controls**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
```

- [ ] **Step 6: Record every result directory**

Add a table with command, directory, total, passed, failed, timeout, cleanup,
and commit SHA to quickstart. Never copy stale counts from an earlier commit.

- [ ] **Step 7: Request code review**

Use `requesting-code-review` against the entire merge-base diff. Review must
explicitly inspect raw/canonical authority, identity replay, transaction order,
same-turn item storage, repair dispatch, player privacy, legacy removal, docs,
and test evidence.

- [ ] **Step 8: Resolve findings with TDD**

For every Critical/Important finding: reproduce with a focused failing test,
implement the smallest fix, rerun the affected focused group, and record the
finding disposition. Do not mark review complete from static assurance alone.

- [ ] **Step 9: Reconcile Spec Kit tasks**

Map the final diff to all 60 FRs and 12 SCs; update T001–T099 only when code,
docs, and fresh evidence exist. Confirm no afterlife, #1515, GM-worker, network,
or redesign scope entered the branch.

- [ ] **Step 10: Commit review fixes/evidence**

```powershell
git add -- specs/1520-complete-location-materialization/quickstart.md specs/1520-complete-location-materialization/tasks.md
git diff --cached --check
git commit -m "test: verify Mortal location materialization (#1513)"
```

Include any reviewed production/test fixes explicitly in the same `git add`.

---

### Task 13: Final PreMerge, PR, and Merge (T100–T101)

**Files:**
- No planned production edits; any failure returns to the owning TDD task.
- Final evidence: `specs/1520-complete-location-materialization/quickstart.md`
- Final checklist: `specs/1520-complete-location-materialization/tasks.md`

- [ ] **Step 1: Confirm candidate cleanliness and commit identity**

```powershell
git status --short --branch
git diff --check
git log -1 --oneline
```

Expected: only known unrelated generated artifacts may be untracked; no unstaged
feature file and no staged file remain.

- [ ] **Step 2: Run exactly one final PreMerge**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Do not run another Fast immediately before or after this command. Inspect the
result summary, timeouts, duplicate execution report, cleanup, and exit code.

- [ ] **Step 3: Record final evidence without changing the tested code**

Append the PreMerge result directory and commit SHA to quickstart; mark T100.
If this creates a documentation-only evidence commit, rerun only the exact
documentation/source-guard Focused filter needed to prove that edit, not
PreMerge again, and identify the tested production SHA plus evidence-only SHA.

- [ ] **Step 4: Inspect final diff against issue/spec**

```powershell
git diff --stat origin/main...HEAD
git diff --name-status origin/main...HEAD
git log --oneline origin/main..HEAD
```

Confirm every path belongs to #1513 and all active fixture/doc changes are
present.

- [ ] **Step 5: Create PR**

Use a PR title and body that link `Fixes #1513`, summarize authority/identity/
bootstrap/topology/repair/projection behavior, list exact verification commands
and result directories, state that save compatibility is intentionally absent,
and record the afterlife no-update rationale.

- [ ] **Step 6: Merge only the reviewed tested commit**

After CI/review approval, verify the PR head SHA equals the reviewed candidate,
merge to `main`, confirm the merge commit contains the feature head, and only
then mark T101 / close #1513. Preserve the worktree until integration is verified.

## Completion Criteria

The feature is complete only when:

- every new/active canonical Mortal location and link is complete,
  receipt-bearing, indexed, and exact;
- bootstrap uses ordinary materialization and no pseudo-playable placeholder;
- current projection equals its map location and all exits derive from links;
- actor/faction/lore/threat/storage/item references compose without name or realm
  ambiguity;
- accepted-turn writes and repair retries are atomic and replay-safe;
- hidden/rejected state has zero player footprint and console/browser semantics agree;
- active fixtures and GM contracts use the current schema with three executable examples;
- all focused/broad/final test evidence is fresh for the candidate;
- the PR merged the exact reviewed/tested commit and #1513 is closed afterward.
