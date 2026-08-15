# Unified Resource Authority Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task.

**Goal:** Replace every included player, NPC, combat, item, afterlife, and effect-specific bounded quantity with one sealed resource definition catalog, one live ledger, immutable transition history, and one atomic accepted-mechanics publication plan, then resume Effect Task 9 on that shared authority.

**Architecture:** Keep the existing .NET 8 file-backed runtime and partial validation/normalizer structure. Add strict resource contracts, pure exact-decimal reducers, composed owner authority, and an immutable `AcceptedMechanicsPlan`; generalize the existing effect combat identity and publication handoff instead of adding a parallel adapter. All domain writers become authorized mutation producers, all readers consume non-persisted projections, and the final branch removes every legacy resource mirror without migration or fallback.

**Tech Stack:** C#/.NET 8, `System.Text.Json`/`JsonNode`, SHA-256 authority fingerprints, xUnit 2.9.2, existing `FileSystemManager` canonical write lease and pending-turn snapshot/rollback infrastructure, Spectre.Console, existing browser DTO/service layer, PowerShell 7 bounded test lanes.

**Global Constraints:** GitHub issues [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543) and [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535) own the work. Use the existing `1535-effect-materialization` worktree; do not create an interim mergeable compatibility slice. Do not add migration, dual reads/writes, legacy promotion, raw UI fallback, arbitrary expressions/paths, cloud dependencies, telemetry, or GitHub Actions. Every behavior change is RED before production code. Use only `pwsh -NoProfile -File .\scripts\test-csharp.ps1`; run the smallest Focused filter during a slice, one meaningful Fast checkpoint, conditional FullValidation/LifecycleIntegration for their touched boundaries, and exactly one final PreMerge. Never mark a Spec Kit task complete from a report alone.

## Contract and execution references

Read these completely before implementation and keep them synchronized:

- `AGENTS.md`
- `.specify/memory/constitution.md`
- `specs/1543-unified-resource-authority/spec.md`
- `specs/1543-unified-resource-authority/plan.md`
- `specs/1543-unified-resource-authority/tasks.md`
- `specs/1543-unified-resource-authority/data-model.md`
- every file in `specs/1543-unified-resource-authority/contracts/`
- `specs/1543-unified-resource-authority/quickstart.md`
- `docs/superpowers/specs/2026-08-15-unified-resource-authority-design.md`
- `specs/1535-complete-effect-materialization/` before Tasks 6 and 11
- `docs/testing.md`

The Spec Kit checklist `T001`–`T121` is the authoritative fine-grained completion ledger. The tasks below are reviewer-sized execution slices and name the Spec Kit ranges they discharge.

---

### Task 1: Freeze the executable inventory and build the file-backed test harness

**Spec Kit tasks:** T001–T007

**Files:**

- Modify: `specs/1543-unified-resource-authority/research.md`
- Modify: `specs/1543-unified-resource-authority/plan.md`
- Modify: `specs/1543-unified-resource-authority/quickstart.md`
- Create: `BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.Owners.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.Publication.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContextTests.cs`

**Step 1: Confirm the owned workspace before any behavior edit**

Run:

```powershell
git status --short
git branch --show-current
git rev-parse --show-toplevel
gh issue view 1543 --json number,state,url,title
gh issue view 1535 --json number,state,url,title,body
```

Expected: branch `1535-effect-materialization`, root `E:/Games/worktrees/boe-1535-effect-materialization`, both issues open, and #1535 names #1543 as the Effect Task 9 blocker. Do not change repository settings or Actions.

**Step 2: Capture the current bounded baseline**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: exit `0`, no timeout, failures, duplicate test IDs, or incomplete owned-process cleanup. Record the exact `summary.json` path, counts, wall time, and cleanup result in `quickstart.md`; a failure is baseline evidence to diagnose before implementation, not permission to weaken the lane.

**Step 3: Inventory every writer, reader, validator, normalizer, prompt, example, and fixture**

Use read-only `rg` searches for all current/max/percentage/delta/durability/charge/ammunition/action-economy/reroll fields and all response mappings. Reconcile exact paths in `plan.md`; classify each hit as removed authority, retained narrative/entitlement/audit, or explicitly out-of-scope accounting. Include `LiveTurnPreparationService.cs`, save/load paths, player/browser projections, GM daemon prompt entrypoints, and afterlife docs.

**Step 4: Write the first failing harness test**

Add a test named:

```csharp
[Fact]
public async Task Context_CapturesBytesAndPriorAbsenceForEveryResourcePath()
{
    await using var context = await ResourceMaterializationTestContext.CreateAsync();
    var before = await context.CaptureAsync(ResourceMaterializationTestContext.AllResourcePaths);

    Assert.Contains("game_state/resources/resource_definitions.json", before.Keys);
    Assert.Contains("game_state/resources/resource_state.json", before.Keys);
    Assert.Contains("game_state/resources/resource_history.json", before.Keys);
    Assert.Contains("game_state/resources/resource_commands.json", before.Keys);
    Assert.All(before.Values, image => Assert.True(image.Bytes is not null || !image.Existed));
}
```

Keep these literals test-owned until Task 2 introduces the production constants; Task 2 must add an agreement assertion rather than letting the harness depend on missing production code.

**Step 5: Implement only reusable test infrastructure**

The context must:

- own one isolated root per test;
- construct `FileSystemManager`, `ValidationService`, and a lease-bound `CanonicalStateNormalizer` using existing test conventions;
- seed exact JSON without lossy parse/reserialize when byte evidence matters;
- capture `CanonicalBeforeImage(bool Existed, byte[]? Bytes)` equivalents;
- assert no mutation, exact bytes, prior absence, and command consumption;
- expose owner builders for all eight owner kinds without supplying protected IDs from GM payloads.

Do not add production resource behavior in this task.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceMaterializationTestContextTests"
```

Expected GREEN: the isolated root, byte/existence snapshots, and no-mutation assertion work without any resource production implementation.

**Step 6: Review and commit the inventory/harness slice**

Run:

```powershell
git diff --check
git diff -- specs/1543-unified-resource-authority BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext*.cs
```

Commit only after the inventory is complete:

```powershell
git add specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.cs BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.Owners.cs BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContext.Publication.cs BookOfEternityClient.IntegrationTests/ResourceMaterializationTestContextTests.cs
git commit -m "test: lock resource authority inventory (#1543)"
```

---

### Task 2: Implement strict resource definitions and capacity/initialization policies

**Spec Kit tasks:** T008–T013

**Files:**

- Create: `BookOfEternityClient/Services/ResourceMaterializationContract.cs`
- Create: `BookOfEternityClient/Services/ResourceDefinitionCatalog.cs`
- Create: `BookOfEternityClient/Services/ResourceCapacityFormulaCatalog.cs`
- Create: `BookOfEternityClient.Tests/ResourceMaterializationContractTests.cs`
- Create: `BookOfEternityClient.Tests/ResourceDefinitionCatalogTests.cs`
- Create: `BookOfEternityClient.Tests/ResourceCapacityFormulaCatalogTests.cs`

**Step 1: RED strict-root and scalar tests**

Cover missing versus present `null`, empty/whitespace, wrong root kind, malformed JSON, duplicate properties at every depth, unknown closed fields, trimmed/case/confusable identifiers, unsupported numbers, precision, non-integral integer values, invalid quantum, and every technical limit boundary.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceMaterializationContractTests"
```

Expected RED: new tests fail because `ResourceMaterializationContract` does not exist; no production file is written.

**Step 2: Add the strict shared contract**

Use a closed API equivalent to:

```csharp
internal static class ResourceMaterializationContract
{
    internal const string DefinitionsPath = "game_state/resources/resource_definitions.json";
    internal const string StatePath = "game_state/resources/resource_state.json";
    internal const string HistoryPath = "game_state/resources/resource_history.json";
    internal const string CommandPath = "game_state/resources/resource_commands.json";

    internal const int MaxDefinitions = 256;
    internal const int MaxLiveEntries = 20_000;
    internal const int MaxCapacityTransitionsPerTurn = 256;
    internal const int MaxMutationsBeforeTriggers = 512;
    internal const int MaxTriggerNodes = 1_024;
    internal const int MaxTriggerDepth = 32;
    internal const int MaxPendingRequestsPerTurn = 64;

    internal static ResourceRootParseResult ParseDefinitions(string? json, bool allowMissingPristine);
    internal static bool TryReadExactDecimal(JsonElement value, out decimal result);
    internal static bool IsExactIdentifier(string? value);
}
```

Parse through duplicate-safe `JsonDocument` traversal before materializing `JsonNode`. Never treat present whitespace or JSON `null` as a missing file.

**Step 3: RED definition and formula tests**

Test the exact required fields and closed variants from `data-model.md`, including:

- built-ins `health`, `energy`, `poise`, `durability`, `charges`, `ammunition`, `spiritual_action_points`, `gacha_attempts`, and `blessing_rerolls`;
- `initializationPolicy` values `minimum`, `maximum`, `fixed`, and `registered_formula`;
- capacity kinds `definition_fixed`, `instance_fixed`, and `registered_formula`;
- exact owner/operation/visibility catalogs;
- setting proposal acceptance with client-generated seal;
- direct seal submission, duplicate/confusable key, definition rewrite, arbitrary formula/path/expression, stale formula input, and inexact formula result rejection.

Run the two new filters and preserve their RED summaries.

**Step 4: Implement sealed definition and formula records**

Use immutable records equivalent to:

```csharp
internal sealed record ResourceDefinition(
    string ResourceKey,
    int DefinitionVersion,
    string DisplayName,
    ResourceNumericKind NumericKind,
    string Unit,
    decimal Quantum,
    ResourceMinimumPolicy MinimumPolicy,
    ResourceCapacityPolicy CapacityPolicy,
    ResourceInitializationPolicy InitializationPolicy,
    IReadOnlySet<ResourceOwnerKind> AllowedOwnerKinds,
    IReadOnlySet<ResourceOperation> AllowedOperations,
    ResourceBoundPolicy FloorPolicy,
    ResourceBoundPolicy CapPolicy,
    ResourceVisibility Visibility,
    ResourceDefinitionMaterialization Materialization);

internal interface IResourceCapacityFormula
{
    string FormulaKey { get; }
    ResourceFormulaResult Evaluate(ResourceFormulaInput input);
}
```

The registry chooses formulas by an exact closed key. It accepts only the declared owner/context snapshot and returns exact decimal results; no reflection, script, expression language, or arbitrary parameter bag is allowed.

**Step 5: GREEN and commit**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceMaterializationContractTests|FullyQualifiedName~ResourceDefinitionCatalogTests|FullyQualifiedName~ResourceCapacityFormulaCatalogTests"
git diff --check
```

Expected GREEN: all selected tests execute and pass with no timeout/duplicates/cleanup failure.

Commit:

```powershell
git add BookOfEternityClient/Services/ResourceMaterializationContract.cs BookOfEternityClient/Services/ResourceDefinitionCatalog.cs BookOfEternityClient/Services/ResourceCapacityFormulaCatalog.cs BookOfEternityClient.Tests/ResourceMaterializationContractTests.cs BookOfEternityClient.Tests/ResourceDefinitionCatalogTests.cs BookOfEternityClient.Tests/ResourceCapacityFormulaCatalogTests.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: define sealed resource policies (#1543)"
```

---

### Task 3: Implement canonical state, immutable history, and replay authority

**Spec Kit tasks:** T014–T017

**Files:**

- Create: `BookOfEternityClient/Services/ResourceStateContract.cs`
- Create: `BookOfEternityClient/Services/ResourceHistoryState.cs`
- Create: `BookOfEternityClient.Tests/ResourceStateContractTests.cs`
- Create: `BookOfEternityClient.Tests/ResourceHistoryStateTests.cs`

**Step 1: RED state-coordinate tests**

Test exact `(realm, ownerKind, resourceOwnerId, resourceKey)` uniqueness, stable sort, active/suspended states, current/minimum/maximum/quantum agreement, capacity binding, chronology, case/confusable duplicates, malformed roots, 20,000/20,001 entries, and direct unknown fields.

**Step 2: RED history/replay tests**

Test append-only transition shape, exact before/after chain, event/source/receipt binding, terminal evidence, exact replay no-op, conflicting replay failure, duplicate/confusable operation and transition IDs, chronology mismatch, and no truncation.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceStateContractTests|FullyQualifiedName~ResourceHistoryStateTests"
```

Expected RED: missing state/history types or unimplemented validations.

**Step 3: Implement immutable state and history models**

Use explicit keys and results:

```csharp
internal sealed record ResourceCoordinate(
    string Realm,
    ResourceOwnerKind OwnerKind,
    string ResourceOwnerId,
    string ResourceKey);

internal sealed record ResourceStateEntry(
    ResourceCoordinate Coordinate,
    decimal Current,
    decimal Maximum,
    ResourceCapacityBinding CapacityBinding,
    ResourceLifecycleState State,
    ResourceChronology Chronology);

internal sealed record ResourceTransition(
    string TransitionId,
    string OperationId,
    string EventRef,
    ResourceMutationOrigin Origin,
    ResourceOperation Operation,
    ResourceCoordinate Coordinate,
    decimal RequestedAmount,
    decimal AppliedAmount,
    decimal Before,
    decimal After,
    JsonObject SourceEvidence,
    string? ReceiptId,
    int Turn);
```

`ResourceStateContract.Parse` returns both a canonical coordinate index and issues. `ResourceHistoryState.Parse` builds transition/replay indexes once. Public getters return immutable values or defensive clones.

**Step 4: Prove fingerprints and ordering**

Add tests that reordering JSON entries without changing semantic coordinates yields the same catalog fingerprint, while any value/chronology/history mutation changes it. Order transitions by accepted chronology, not file iteration.

**Step 5: GREEN and commit**

Run the same Focused filter, inspect its `summary.json`, then:

```powershell
git diff --check
git add BookOfEternityClient/Services/ResourceStateContract.cs BookOfEternityClient/Services/ResourceHistoryState.cs BookOfEternityClient.Tests/ResourceStateContractTests.cs BookOfEternityClient.Tests/ResourceHistoryStateTests.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: add resource ledger and history authority (#1543)"
```

---

### Task 4: Generalize stable owner and combat/group-member identity authority

**Spec Kit tasks:** T018–T021

**Files:**

- Create: `BookOfEternityClient/Services/ResourceOwnerAuthority.cs`
- Create: `BookOfEternityClient/Services/CombatantIdentityState.cs`
- Delete after cutover: `BookOfEternityClient/Services/EffectCombatantIdentityState.cs`
- Modify: `BookOfEternityClient/Services/EffectTargetAuthority.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- Create: `BookOfEternityClient.Tests/ResourceOwnerAuthorityTests.cs`
- Create: `BookOfEternityClient.Tests/CombatantIdentityStateTests.cs`
- Modify: `BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs`

**Step 1: RED every owner family and identity transition**

Cover `player_current`, permanent NPC, anonymous combatant, stable group member, permanent item, persistent afterlife actor/profile, conflict side, and conflict scope. Include exact same-turn refs, realm/capability mismatch, duplicate/confusable identity, display-name/index inference, lifecycle state, named-NPC combat binding, reordered group rows, submitted permanent IDs, unreferenced sibling refs, and ref-only turns with no effect/resource command.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceOwnerAuthorityTests|FullyQualifiedName~CombatantIdentityStateTests|FullyQualifiedName~EffectAcceptedTurnPlannerTests"
```

Expected RED: common owner/member identity types are missing.

**Step 2: Add the composed owner catalog**

Use an owner authority entry that cannot carry arbitrary state:

```csharp
internal sealed record ResourceOwnerAuthorityEntry(
    ResourceOwnerKey Key,
    string Realm,
    ResourceOwnerKind Kind,
    ResourceOwnerLifecycle Lifecycle,
    IReadOnlySet<string> ResourceCapabilities,
    string? SameTurnRef,
    string? BoundNpcId);

internal sealed record ResourceOwnerAuthorityResult(
    IReadOnlyDictionary<ResourceOwnerKey, ResourceOwnerAuthorityEntry> Entries,
    IReadOnlyDictionary<string, ResourceOwnerAuthorityEntry> SameTurnRefs,
    string Fingerprint,
    IReadOnlyList<ValidationIssue> Issues);
```

Build from validated pre-turn plus accepted owner after-images. Exact stable IDs win; names and array positions never resolve authority.

**Step 3: Replace the effect-only combat identity type**

Move allocation/continuity/ref consumption into `CombatantIdentityState`. Allocate random `combatantId` and `memberId` once for every accepted ref-bearing row, including rows not targeted by an effect. Bind named combatants to exact composed NPC authority and reject residual refs canonically.

Update effect callers to consume common results; do not retain a wrapper that becomes a second owner authority.

**Step 4: GREEN and commit**

Run the Focused filter again. Then inspect all references:

```powershell
rg -n "EffectCombatantIdentityState|combatantRef|memberRef|combatantId|memberId" BookOfEternityClient BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests
git diff --check
```

Expected: the old class has zero production references, canonical refs are rejected, and all selected tests pass.

Commit:

```powershell
git add BookOfEternityClient/Services/ResourceOwnerAuthority.cs BookOfEternityClient/Services/CombatantIdentityState.cs BookOfEternityClient/Services/EffectCombatantIdentityState.cs BookOfEternityClient/Services/EffectTargetAuthority.cs BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs BookOfEternityClient.Tests/ResourceOwnerAuthorityTests.cs BookOfEternityClient.Tests/CombatantIdentityStateTests.cs BookOfEternityClient.Tests/EffectAcceptedTurnPlannerTests.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "refactor: unify combat and resource owner identity (#1543)"
```

---

### Task 5: Establish the unified accepted-mechanics input, plan, and cache

**Spec Kit tasks:** T022–T024, T026, T029–T031

**Files:**

- Create: `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs`
- Create: `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs`
- Create: `BookOfEternityClient/Services/ResourceAcceptedTurnInputComposer.cs`
- Modify: `BookOfEternityClient/Models/GameResponse.cs`
- Modify: `BookOfEternityClient/Configuration/FileMapping.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs`
- Create: `BookOfEternityClient.Tests/AcceptedMechanicsPlanCacheTests.cs`
- Modify: `BookOfEternityClient.Tests/ResourceMaterializationContractTests.cs`

**Step 1: RED response and command-envelope tests**

Add exact serialization/mapping cases for optional `resourceDefinitionCreations`, `resourceCapacityChanges`, and `resourceChanges`. Assert all three stage into `game_state/resources/resource_commands.json` and duplicate-safe merging preserves each supplied array. Add the executable legacy-property removal assertions only in the owning Mortal/item cutover tasks; do not commit skipped future assertions.

Expected response properties:

```csharp
[JsonPropertyName("resourceDefinitionCreations")]
public JsonElement[]? ResourceDefinitionCreations { get; set; }

[JsonPropertyName("resourceCapacityChanges")]
public JsonElement[]? ResourceCapacityChanges { get; set; }

[JsonPropertyName("resourceChanges")]
public JsonElement[]? ResourceChanges { get; set; }
```

Run `ResourceMaterializationContractTests`; expect RED on missing fields/mappings.

**Step 2: RED cache identity and invalidation tests**

Test that:

- identical complete input returns the same plan instance and random IDs;
- definitions, owners, state, history, sources, targets, carriers, indexes, events, commands, pending state, or internal adapter input change the fingerprint;
- failed validation clears the handoff;
- successful publication consumes the handoff;
- a stale prior request cannot make a commandless turn non-no-op;
- literal `null`, duplicate, or late-mutated command input cannot retrieve a validated plan.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlanCacheTests|FullyQualifiedName~ResourceMaterializationContractTests"
```

Expected RED: common plan/cache types are absent.

**Step 3: Define one immutable cross-domain plan**

Implement records equivalent to:

```csharp
internal sealed record CanonicalBeforeImage(bool Existed, byte[]? Bytes);

internal sealed record AcceptedMechanicsAuthorityFingerprints(
    string Definitions,
    string Owners,
    string ResourceState,
    string ResourceHistory,
    string EffectSources,
    string EffectTargets,
    string EffectCarriers,
    string EffectIdentityIndex,
    string AcceptedEvents,
    string Commands,
    string Pending,
    string InternalInputs);

internal sealed class AcceptedMechanicsPlan
{
    internal string InputFingerprint { get; }
    internal JsonObject DefinitionAfterImage { get; }
    internal JsonObject StateAfterImage { get; }
    internal JsonObject HistoryAfterImage { get; }
    internal IReadOnlyDictionary<string, JsonObject> EffectCarrierAfterImages { get; }
    internal JsonObject EffectIdentityAfterImage { get; }
    internal IReadOnlyDictionary<string, JsonObject?> PendingAfterImages { get; }
    internal IReadOnlyDictionary<string, JsonObject> OwnerCompanionAfterImages { get; }
    internal IReadOnlyDictionary<string, CanonicalBeforeImage> BeforeImages { get; }
    internal IReadOnlyList<string> TouchedPaths { get; }
    internal IReadOnlyList<string> ConsumedPaths { get; }
    internal IReadOnlyList<ResourceAppliedEvent> ResourceEvents { get; }
    internal ResourceProjectionInput ProjectionInput { get; }
}
```

Every JSON property is defensively cloned; every collection is immutable and canonically ordered.

**Step 4: Implement one validated handoff**

`AcceptedMechanicsPlanCache.GetOrBuildValidated(input)` owns random identity allocation. `TryTakeValidated(liveBinding, out result)` must compare the full input binding and consume exactly once. Validation entry invalidates before every possible early return. The existing `EffectAcceptedTurnPlan` becomes a subplan/value inside the common plan; remove its independent validated cache only after all callers are switched in Task 7.

**Step 5: Implement the strict command composer**

Parse the transient root as:

```json
{
  "resourceDefinitionCreations": [],
  "resourceCapacityChanges": [],
  "resourceChanges": []
}
```

Absent arrays mean empty; present wrong types fail. Reject protected after-state, client IDs, phase, priority, floor/cap selection, arbitrary paths/selectors, and nonpositive/unquantized amounts. Bind event authority by exact command ordinal and source route; do not allocate IDs here.

**Step 6: GREEN and commit**

Run the Task 5 Focused filter and existing `EffectAcceptedTurnPlannerTests`. Expected: new cache tests pass and existing effect plan semantics remain green.

Commit:

```powershell
git diff --check
git add BookOfEternityClient/Services/AcceptedMechanicsPlan.cs BookOfEternityClient/Services/AcceptedMechanicsPlanCache.cs BookOfEternityClient/Services/ResourceAcceptedTurnInputComposer.cs BookOfEternityClient/Models/GameResponse.cs BookOfEternityClient/Configuration/FileMapping.cs BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs BookOfEternityClient/Services/EffectAcceptedTurnPlanCache.cs BookOfEternityClient.Tests/AcceptedMechanicsPlanCacheTests.cs BookOfEternityClient.Tests/ResourceMaterializationContractTests.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "refactor: create accepted mechanics plan authority (#1543)"
```

---

### Task 6: Implement the exact reducer, source registry, replay, events, and finite graph

**Spec Kit tasks:** T039–T047

**Files:**

- Create: `BookOfEternityClient/Services/ResourceMutationReducer.cs`
- Create: `BookOfEternityClient/Services/ResourceMutationSourceCatalog.cs`
- Create: `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- Create: `BookOfEternityClient.Tests/ResourceMutationReducerTests.cs`
- Create: `BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs`
- Create: `BookOfEternityClient.Tests/ResourceTriggerGraphTests.cs`

**Step 1: RED exact arithmetic and capacity tests**

Test `damage`, `restore`, `spend`, and `gain` across integer/decimal/quantum/min/max/overflow boundaries. Include reject versus clamp, non-commutative ordered mutations, exact and conflicting replay, initialize, preserve/clamp/ratio capacity changes, inexact ratio, suspend/resume/retire, stale formula input, and immutable terminal evidence.

Representative assertion:

```csharp
[Fact]
public void Reduce_AppliesSequentialClampWithoutPreSumming()
{
    var first = ResourceMutationReducer.Reduce(LedgerAt(9, max: 10), Gain(5, clamp: true));
    var second = ResourceMutationReducer.Reduce(first.Ledger, Damage(4));

    Assert.Equal(6m, second.Entry.Current);
    Assert.Equal((9m, 10m), (first.Transition.Before, first.Transition.After));
    Assert.Equal((10m, 6m), (second.Transition.Before, second.Transition.After));
}
```

**Step 2: Implement a source-neutral pure reducer**

```csharp
internal static ResourceMutationResult Reduce(
    ResourceWorkingLedger ledger,
    AuthorizedResourceMutation mutation,
    ResourceDefinitionCatalog definitions,
    ResourceHistoryState history);

internal static ResourceCapacityResult ApplyCapacityTransition(
    ResourceWorkingLedger ledger,
    AuthorizedCapacityTransition transition,
    ResourceDefinitionCatalog definitions);
```

The reducer reads no files, selects no source policy, and creates no pending work. It returns a new ledger, transition, registered applied events, replay result, or bounded issues. Use checked `decimal`; never coerce to `double` or round to quantum.

**Step 3: RED source-route and four-phase tests**

Test the closed mapping from ordinary/local/system/effect routes to `direct_cost`, `direct_outcome`, `registered_system_outcome`, and `effect_trigger`, then stable priority/origin/operation order. Reject GM-selected phase/priority/policy, unsupported route, invalid sibling, and incomplete after-image.

**Step 4: RED graph tests**

Test deterministic applied/depleted/filled events, nested effect/resource dependencies, duplicate nodes, missing dependencies, cycles, 1,024/1,025 nodes, depth 32/33, exact replay, and stable order across randomized input enumeration.

**Step 5: Implement the planner and graph**

```csharp
internal static AcceptedMechanicsPlanningResult Build(
    AcceptedMechanicsInput input,
    string fingerprint,
    AcceptedMechanicsIdentityFactory identityFactory);
```

Build all indexes once, create the working ledger, apply capacity/lifecycle transitions, traverse the four phases, emit registered events after actual results, build the entire DAG before trigger execution, and return either one complete plan or issues with zero after-images. The planner must not write files or create pending work for deterministic mutations.

**Step 6: GREEN, purity review, and commit**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceMutationReducerTests|FullyQualifiedName~AcceptedMechanicsPlannerTests|FullyQualifiedName~ResourceTriggerGraphTests"
rg -n "FileSystemManager|WriteFile|DeleteFile" BookOfEternityClient/Services/ResourceMutationReducer.cs BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs
git diff --check
```

Expected: tests GREEN and the purity search has no runtime file access.

Commit:

```powershell
git add BookOfEternityClient/Services/ResourceMutationReducer.cs BookOfEternityClient/Services/ResourceMutationSourceCatalog.cs BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs BookOfEternityClient.Tests/ResourceMutationReducerTests.cs BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs BookOfEternityClient.Tests/ResourceTriggerGraphTests.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: reduce resource mutations deterministically (#1543)"
```

---

### Task 7: Integrate raw/canonical validation, bootstrap, atomic publication, snapshots, and rollback

**Spec Kit tasks:** T025, T027–T038, T048–T050

**Files:**

- Create: `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs`
- Create: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- Modify: `BookOfEternityClient/Services/LiveTurnPreparationService.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceMaterializationValidationTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Resources.cs`

**Step 1: RED raw/canonical and publication tests**

Add tests for pristine bootstrap, strict roots, direct definition/state/history mutation, legacy state, exact same-turn definition/owner refs, wrong realm, command consumption, untouched subtree preservation, present literal-null/malformed late change, every authority-fingerprint TOCTOU change, post-state agreement, and failed-publication zero writes.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests.Resources"
```

Expected RED: validation and common publication entrypoints are missing.

**Step 2: Implement raw and canonical validation phases**

Expose:

```csharp
public Task<IReadOnlyList<ValidationIssue>> ValidateAcceptedTurnRawResourceMaterializationAsync();
internal Task<IReadOnlyList<ValidationIssue>> ValidateAcceptedTurnCanonicalResourceMaterializationAsync(
    FileSystemManager.CanonicalWriteLease writeLease);
```

Raw validation must invalidate the common cache at entry, require a usable validated pending-turn snapshot, compare protected current files to their snapshot before-images, compose exact owners/commands/events/effect inputs, build and cache one plan only after all resource/effect owner checks succeed, and surface bounded issues. Canonical validation reparses all three roots and verifies definition/state/history/owner/effect/pending agreement without rebuilding identities.

**Step 3: Implement lease-bound publication**

Add:

```csharp
internal Task<AcceptedMechanicsPlan?> NormalizeAcceptedMechanicsAsync(
    IReadOnlyDictionary<string, string>? backups,
    MortalLocationAcceptedTurnPlan? mortalLocationPlan = null);
```

Under the existing canonical write lease:

1. duplicate-safely parse live command/event input;
2. take the exact validated plan;
3. compare every byte/existence before-image and authority fingerprint;
4. write owner companions, resource definitions/state/history, effect carriers/index, and pending after-images in deterministic path order;
5. delete only consumed command paths;
6. run resource/effect/full-state post-validation;
7. return the plan for output gating;
8. let the existing outer transaction restore exact bytes/prior absence on any exception.

Replace `NormalizeEffectsAsync` as an independent transaction; retain only effect after-image helpers invoked by the common publisher.

**Step 4: Wire snapshots, rollback, and bootstrap**

Add all four resource paths to canonical accumulated, backup, rollback, pending-snapshot, session cleanup, and save/load contours. Bootstrap built-in definitions plus player health/energy/poise states and empty history. Missing roots are legal only within the proven pristine bootstrap transition; an old technical save fails as incompatible.

**Step 5: GREEN and Phase checkpoint**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceMaterializationContractTests|FullyQualifiedName~ResourceDefinitionCatalogTests|FullyQualifiedName~ResourceStateContractTests|FullyQualifiedName~ResourceHistoryStateTests|FullyQualifiedName~ResourceOwnerAuthorityTests|FullyQualifiedName~ResourceMutationReducerTests|FullyQualifiedName~AcceptedMechanicsPlannerTests|FullyQualifiedName~AcceptedMechanicsPlanCacheTests|FullyQualifiedName~ResourceTriggerGraphTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerTests.Resources"
git diff --check
```

Review that there is one cache and one publication lease. Record exact RED/GREEN summary paths in this plan and `quickstart.md` during execution.

Commit:

```powershell
git add BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs BookOfEternityClient/Services/CanonicalStateNormalizer.cs BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs BookOfEternityClient/Services/LiveTurnPreparationService.cs BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Effects.cs BookOfEternityClient.IntegrationTests/ResourceMaterializationValidationTests.cs BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Resources.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: publish accepted resource state atomically (#1543)"
```

---

### Task 8: Cut Mortal player, NPC, and combat resources to common owners and mutations

**Spec Kit tasks:** T051, T054–T062

**Files:**

- Modify: `BookOfEternityClient/Models/GameResponse.cs`
- Modify: `BookOfEternityClient/Core/StateManager.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.MathAssistant.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.InventoryNpcWorldCrossRefs.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.QuestsRivalsFactionsAndWorld.cs`
- Modify: `BookOfEternityClient/Services/EffectCarrierCatalog.cs`
- Modify: `BookOfEternityClient/Services/ResourceAcceptedTurnInputComposer.cs`
- Modify: `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceOwnerMaterializationTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceCombatOwnerTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/MortalResourceCutoverTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceCombatIntegrationTests.cs`

**Step 1: RED owner continuity and legacy rejection**

Test player bootstrap and ordinary damage/heal/spend/gain; named NPC inside/outside combat; anonymous combatant; stable group member across reorder/add/remove/defeat; named-NPC binding ambiguity; same-turn NPC/combat refs; terminal cleanup; direct percentage/delta/current/max/positional-array submissions; and one invalid sibling causing zero state/history/owner writes.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceOwnerMaterializationTests|FullyQualifiedName~ResourceCombatOwnerTests|FullyQualifiedName~MortalResourceCutoverTests|FullyQualifiedName~ResourceCombatIntegrationTests"
```

Expected RED: current domain fields remain authoritative.

**Step 2: Cut player response and state authority**

Remove `CurrentHealthChange`, `CurrentEnergyChange`, `CurrentPoiseChange` and persisted `HealthPercentage`, `EnergyPercentage`, `PoisePercentage` as mechanical inputs/state. Keep narrative condition and money. Ordinary player commands/adapters produce authorized resource mutations against `player_current`; bootstrap and `StateManager` obtain values from the resource projection, never raw fallbacks.

**Step 3: Cut named NPC authority**

Remove NPC current/max health as writable authority. Compose exact permanent NPC owner entries and let combat reference that same coordinate. NPC creation may initialize allowed resources through its validated owner plan; later NPC updates cannot replace ledger state.

**Step 4: Cut individual and group combat authority**

Remove individual `currentHealth/currentPoise` and positional group `healthStates[]`. Store only stable `combatantId`/`memberId`, exact named-NPC binding when applicable, and narrative/action state. All damage/heal/poise operations become common mutations. Defeat/exit either preserves named/persistent owners or terminally retires scoped owners according to the owner plan.

**Step 5: Preserve Shining survival/restoration as registered outcomes**

Update `ShiningBlessingEffectState.cs` only through the common system-outcome adapter; do not route currencies or boolean entitlements into the ledger.

**Step 6: GREEN, search removed authority, and commit**

Run the same Focused integration filter plus:

```powershell
rg -n "currentHealthChange|currentEnergyChange|currentPoiseChange|healthPercentage|energyPercentage|poisePercentage|currentHealth|currentPoise|healthStates" BookOfEternityClient
git diff --check
```

Every remaining hit must be a rejected-legacy detector, historical documentation allow-list, or an explicitly out-of-scope non-authority with a recorded rationale. No normal writer/reader may remain.

Commit:

```powershell
git add BookOfEternityClient BookOfEternityClient.IntegrationTests/ResourceOwnerMaterializationTests.cs BookOfEternityClient.IntegrationTests/ResourceCombatOwnerTests.cs BookOfEternityClient.IntegrationTests/MortalResourceCutoverTests.cs BookOfEternityClient.IntegrationTests/ResourceCombatIntegrationTests.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: cut mortal and combat resources to ledger (#1543)"
```

---

### Task 9: Cut item durability, charges, ammunition, and bounded reserves to item-owned resources

**Spec Kit tasks:** T052, T063–T067

**Files:**

- Modify: `BookOfEternityClient/Services/MortalItemMaterializationContract.cs`
- Modify: `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`
- Modify: `BookOfEternityClient/Services/MortalItemTransitionWriter.Stacks.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.FactionAndInventoryHelpers.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs`
- Modify: `BookOfEternityClient/Configuration/FileMapping.cs`
- Modify: `BookOfEternityClient/Models/GameResponse.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceItemOwnerTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceItemIntegrationTests.cs`

**Step 1: RED item lifecycle and operation matrix**

Cover player/NPC/location/offscreen carriers, same-turn materialization, move, split, merge, destroy, terminal consume, nonempty removal, use/repair/fire/reload, instance capacity, exact replay, mutation during movement, and rollback. Add negative cases for `durability`, `maxDurability`, current/max item resource objects, `inventoryItemsResources`, `NPCInventoryResourcesChanges`, unknown resource capability, and direct item-owned state.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceItemOwnerTests|FullyQualifiedName~ResourceItemIntegrationTests"
```

Expected RED: legacy sidecars/commands still own values.

**Step 2: Add item resource capabilities to accepted item materialization**

An item definition/candidate may declare a closed capability such as:

```csharp
internal sealed record ItemResourceCapability(
    string ResourceKey,
    ResourceCapacityProposal Capacity,
    ResourceInitializationProposal Initialization);
```

The item plan resolves the permanent item identity first, then supplies initialization/capacity authority to `AcceptedMechanicsPlanner`. It never writes `current` or `maximum` into the item.

**Step 3: Route all item operations through common mutations**

Map use/repair/fire/reload and accepted inventory/NPC operations to registered source routes. Preserve item identity across carrier movement. Split/merge must have an explicit resource disposition per capability; reject any unregistered arithmetic. Destruction/terminal consumption retires every live item coordinate and appends terminal history in the same plan.

**Step 4: Remove the sidecar authority**

Remove `inventoryItemsResources` and `NPCInventoryResourcesChanges` mappings/properties/application. Delete `game_state/inventory/item_resources.json` from active canonical, bootstrap, validator, normalizer, snapshot, and reader paths. Keep only a strict incompatible-save detector/source guard until the final cleanup slice.

**Step 5: GREEN and commit**

Run the item integration filter, then:

```powershell
rg -n "item_resources\.json|inventoryItemsResources|NPCInventoryResourcesChanges|maxDurability|\"durability\"" BookOfEternityClient FileSystemExample Examples Rules
git diff --check
```

Classify all remaining hits; active production writers/readers are forbidden.

Commit:

```powershell
git add BookOfEternityClient BookOfEternityClient.IntegrationTests/ResourceItemOwnerTests.cs BookOfEternityClient.IntegrationTests/ResourceItemIntegrationTests.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: move item reserves to resource authority (#1543)"
```

---

### Task 10: Cut afterlife action economy, per-return attempts, and numeric rerolls to the ledger

**Spec Kit tasks:** T053, T068–T074

**Files:**

- Modify: `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`
- Modify: `BookOfEternityClient/Services/AfterlifeSpiritualConflictTurnPreviewService.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- Modify: `BookOfEternityClient/Services/GuardianGachaChargeRules.cs`
- Modify: `BookOfEternityClient/Services/ShiningAbodeState.cs`
- Modify: `BookOfEternityClient/Services/ShiningAbodeState.Gacha.cs`
- Modify: `BookOfEternityClient/Services/ShiningBlessingEffectState.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs`
- Modify: `OtherGuides/Afterlife_Contract_Matrix.md`
- Modify: `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`
- Modify: `Examples/E_CLI_Afterlife_Turns.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify: `Rules/Block_21.txt`
- Modify: `Rules/Block_32_Guardians.txt`
- Modify: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceAfterlifeOwnerTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.cs`
- Modify: `BookOfEternityClient.Tests/ResourceCapacityFormulaCatalogTests.cs`
- Modify: `BookOfEternityClient.Tests/ResourceContractSourceGuardTests.cs`

**Step 1: RED persistent/scoped owner lifecycle**

Test persistent profile/actor resources, conflict-side/scope identity allocation, start/cost/recovery/close, realm mismatch, same-turn owner, return-cycle reset, and terminal cleanup. Named persistent actors survive conflict closure; only scoped entries retire.

**Step 2: RED closed capacity/formula behavior**

Test spirit-focus action-point formulas, opposition instance capacity, Guardian/Shining per-return gacha attempts, numeric blessing rerolls, stale formula inputs, exact initialization, insufficient spend, replay, and capacity transition. Add negative admission cases proving currencies, treasury, faction accounting, relationships, progression, spiritual axes, conditions, and boolean/free-shape/free-retune entitlements remain outside.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceCapacityFormulaCatalogTests|FullyQualifiedName~ResourceContractSourceGuardTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceAfterlifeOwnerTests|FullyQualifiedName~AfterlifeResourceCutoverTests"
```

Expected RED: afterlife domain fields still carry current/max/counter authority.

**Step 3: Cut spiritual conflict action economy**

Replace `activeConflict.actionEconomy.*.current/max` with exact resource owner bindings and common mutations. Keep spiritual axes, costs as audit/definition inputs where still needed, and narrative conflict state. The preview service must resolve the same accepted projection used by execution; it cannot calculate against a mirror.

**Step 4: Cut gacha attempts and rerolls**

Use `gacha_attempts` with registered per-return capacity/initialization formulas and common spend/gain transitions. Use persistent actor `blessing_rerolls` for numeric rerolls. Preserve gacha history and typed boolean/free entitlements, but remove `chargesUsedThisReturn`, `chargesPerReturn`, and numeric reroll mirrors as mechanical values.

**Step 5: Synchronize and verify the afterlife contract in the same slice**

Update the afterlife matrix, glossary, rules, manifest, and worked turn example with the executable owner/resource/formula contract and the explicit currency/axis/entitlement exclusions. Then run the two Focused commands again followed by the mandatory documentation boundary:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

Expected: executable afterlife state and its GM-facing contract pass together. Task 13 may reconcile later cross-domain wording, but no code-only afterlife capability is committed here.

Commit:

```powershell
git diff --check
git add BookOfEternityClient BookOfEternityClient.Tests/ResourceCapacityFormulaCatalogTests.cs BookOfEternityClient.Tests/ResourceContractSourceGuardTests.cs BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs BookOfEternityClient.IntegrationTests/ResourceAfterlifeOwnerTests.cs BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.cs BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs OtherGuides/Afterlife_Contract_Matrix.md OtherGuides/Afterlife_Combat_Terminology_Glossary.md Examples/E_CLI_Afterlife_Turns.txt Examples/example_validation_manifest.json Rules/Block_21.txt Rules/Block_32_Guardians.txt specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: unify afterlife resource authority (#1543)"
```

---

### Task 11: Resume Effect Task 9 with common resource mutations, lifecycle, and bounded pending receipts

**Spec Kit tasks:** T075–T086; reconcile #1535 T042–T043, T047, T049–T050 only when evidence exists

**Files:**

- Modify: `BookOfEternityClient/Services/EffectComponentProfiles.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs`
- Modify: `BookOfEternityClient/Services/EffectLifecycleScheduler.cs`
- Modify: `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- Create: `BookOfEternityClient/Services/ResourcePendingResolutionState.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.ResourceMaterialization.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`
- Modify: `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs`
- Modify: `BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs`
- Modify: `BookOfEternityClient.Tests/ResourceTriggerGraphTests.cs`
- Create: `BookOfEternityClient.Tests/ResourcePendingResolutionTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/EffectResourceMaterializationTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourcePendingResolutionIntegrationTests.cs`
- Modify: `specs/1535-complete-effect-materialization/spec.md`
- Modify: `specs/1535-complete-effect-materialization/plan.md`
- Modify: `specs/1535-complete-effect-materialization/tasks.md`
- Modify: `specs/1535-complete-effect-materialization/data-model.md`
- Modify: `specs/1535-complete-effect-materialization/quickstart.md`
- Modify: relevant contracts in `specs/1535-complete-effect-materialization/contracts/`

**Step 1: RED ordinary/effect equivalence and 100-run determinism**

Add periodic damage/restore cases for player, NPC, combatant, group member, and afterlife owner; unsupported resource/owner; quantum/bounds; exact replay; and byte-equivalent semantic resource/history output between ordinary and effect-generated mutations across 100 fresh randomized-enumeration runs.

```csharp
[Fact]
public void OrdinaryAndPeriodicDamage_AreByteEquivalentAcrossOneHundredRuns()
{
    var expected = SerializeSemanticResult(BuildOrdinaryDamagePlan());
    for (var run = 0; run < 100; run++)
        Assert.Equal(expected, SerializeSemanticResult(BuildPeriodicDamagePlan(run)));
}
```

Run `AcceptedMechanicsPlannerTests`; expect RED until periodic components use the common reducer.

**Step 2: RED resource-event and lifetime integration**

Test depletion/fill/nested triggers, cycle/expansion boundaries, effect uses, time/turn/scene/source/condition lifetime, source-loss `no_change`, terminal identity/index cleanup, and downstream resource mutations. The whole graph must be known before execution.

**Step 3: Implement periodic and event adapters**

Resolve `periodic_damage`/`periodic_restore` to exact `ResourceCoordinate` plus an internal `AuthorizedResourceMutation`. Feed actual `ResourceAppliedEvent` values into trigger selection; feed downstream operations back into the same working ledger. Effect code may select mechanics from sealed definitions but may not perform resource arithmetic.

**Step 4: RED the pending/receipt contract**

Test strict request and receipt roots, safe GM packet, narrated no-change, bounded delta, missing/stale/partial/extra/repeated/cross-target/wrong-operation/out-of-bound receipts, and explicit proof that a deterministic ordinary operation can never create pending work. Integration cases must prove no resource/effect change before receipt, coherent full-turn resubmission, exact-once consumption, rollback, and stale-output suppression.

**Step 5: Implement bounded story resolution**

`ResourcePendingResolutionState` owns at most 64 requests per turn. A request binds the protected coordinate/operation/bounds internally and exposes only safe labels/allowed results to the GM. A receipt contains request ID, allowed result operands, and narrative reason; it cannot select target, resource, operation, phase, or policy. A valid receipt becomes an ordinary authorized mutation in the common plan and is terminally consumed.

**Step 6: GREEN full effect/resource integration**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests|FullyQualifiedName~ResourceTriggerGraphTests|FullyQualifiedName~ResourcePendingResolutionTests|FullyQualifiedName~EffectLifecycleSchedulerTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectResourceMaterializationTests|FullyQualifiedName~ResourcePendingResolutionIntegrationTests"
```

Expected: all selected tests pass; the 100-run equivalence assertion is stable; no deterministic route creates pending state; one invalid effect/resource/pending sibling yields zero after-images.

**Step 7: Reconcile #1535 only from inspected evidence**

Update Task 9 interfaces/evidence in #1535 artifacts. Mark only the exact checkboxes whose production diff and fresh test summaries were inspected. Do not mark later wound/source-loss/other lifecycle tasks complete merely because the common planner now supports their foundation.

Commit:

```powershell
git diff --check
git add BookOfEternityClient BookOfEternityClient.Tests/AcceptedMechanicsPlannerTests.cs BookOfEternityClient.Tests/ResourceTriggerGraphTests.cs BookOfEternityClient.Tests/ResourcePendingResolutionTests.cs BookOfEternityClient.IntegrationTests/EffectResourceMaterializationTests.cs BookOfEternityClient.IntegrationTests/ResourcePendingResolutionIntegrationTests.cs specs/1535-complete-effect-materialization specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: execute effect resources through common plan (#1535 #1543)"
```

---

### Task 12: Replace every player and GM reader with safe derived projections

**Spec Kit tasks:** T087–T097

**Files:**

- Create: `BookOfEternityClient/Services/ResourceProjectionService.cs`
- Create: `BookOfEternityClient/Services/ResourceRepairPacketBuilder.cs`
- Create: `BookOfEternityClient/Services/ResourcePlayerFailureMessages.cs`
- Modify: `BookOfEternityClient/Core/StateManager.cs`
- Modify: `BookOfEternityClient/Models/GameState/AggregatedGameState.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.AgentConsole.cs`
- Modify: `BookOfEternityClient/UI/GameInterface.cs`
- Modify: `BookOfEternityClient/UI/ExplorerUniversalMetaCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/UI/ExplorerAfterlifeCombatCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.WorldAndStatus.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.MetaStoryAndStatus.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.ListAndDetails.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Npcs.Rendering.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.GuardiansProjectsTrade.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.ActionPreviews.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.Actions.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.StatusAudit.cs`
- Modify: `BookOfEternityClient/WebUi/BrowserGameScreenService.cs`
- Create: `BookOfEternityClient.Tests/ResourceProjectionServiceTests.cs`
- Create: `BookOfEternityClient.Tests/ResourcePlayerPrivacyTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/ResourceConsoleBrowserParityTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.GeneralPanels.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.TradeAndInventory.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerModeCommandTests.Afterlife.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExplorerWebCommandServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/BrowserGameScreenDialogueOptionTests.cs`

**Step 1: RED visibility, localization, action, and fail-closed tests**

Cover player-visible, owner-visible, hidden, and GM-only values; Russian label/unit/percentage; recent visible delta; exact action eligibility; unavailable/malformed/stale authority; and no raw fallback. Missing safe projection must omit the mechanical block or show fixed in-world failure copy, never internal values.

**Step 2: RED recursive privacy and parity tests**

Serialize console/browser/game-screen/GM-context results and assert absence of owner IDs, transition/operation IDs, event refs, fingerprints, file paths, pending/receipt DTOs, validation codes, repair guidance, agent terms, and hidden values at every nested depth. Assert console and browser expose the same visible facts/actions/blocking reason.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceProjectionServiceTests|FullyQualifiedName~ResourcePlayerPrivacyTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceConsoleBrowserParityTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
```

Expected RED: existing consumers still read domain fields or lack the safe projection.

**Step 3: Implement one non-authoritative projection service**

```csharp
internal sealed record ResourceProjectionRow(
    string SafeOwnerSelector,
    string ResourceKey,
    string DisplayName,
    string Unit,
    decimal Current,
    decimal Maximum,
    decimal? Percentage,
    ResourceLifecycleState State,
    decimal? RecentVisibleDelta,
    IReadOnlyList<string> AvailableOperations,
    ResourceVisibility Visibility);

internal ResourceProjectionResult Project(
    ResourceProjectionInput input,
    ResourceProjectionAudience audience);
```

Parse an immutable accepted snapshot once; never replay full history in a view. The result is in memory only and cannot be saved as a second ledger.

**Step 4: Cut all readers/actions**

Switch status bar, console status/stats/meta, agent console, NPC/combat/item details, action eligibility, afterlife conflict/gacha/status/previews, shared result builders, and `BrowserGameScreenService` to projection-only values. If the existing React components consume the resulting DTO without changes, document why `npm run verify` is unnecessary; if TypeScript/React is modified, run it.

**Step 5: Implement safe failures and repair projection**

Player copy is fixed Russian in-world text with no technical cause. Operator logs retain technical detail. `ResourceRepairPacketBuilder` emits a GM-actionable packet only for one unambiguous GM-owned omission and requires complete coherent resubmission; protected failures emit no actionable selectors or raw DTO.

**Step 6: GREEN, manual parity spot-check, and commit**

Run the two Focused commands again. Manually compare console/browser status, one NPC/combatant, one item, one spiritual-conflict resource, and one gacha/reroll resource. Inspect serialized results for nested leakage.

Commit:

```powershell
git diff --check
git add BookOfEternityClient BookOfEternityClient.Tests/ResourceProjectionServiceTests.cs BookOfEternityClient.Tests/ResourcePlayerPrivacyTests.cs BookOfEternityClient.IntegrationTests/ResourceConsoleBrowserParityTests.cs specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: project resource state safely to players (#1543)"
```

---

### Task 13: Prove atomic failure, remove legacy authority, and synchronize every active contract/template

**Spec Kit tasks:** T098–T110

**Files:**

- Create: `BookOfEternityClient.IntegrationTests/ResourceMaterializationLifecycleTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Resources.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ResourceMaterializationValidationTests.cs`
- Create: `BookOfEternityClient.Tests/ResourceContractSourceGuardTests.cs`
- Modify: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/FileSystemExampleFixtureIntegrityTests.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AcceptedMechanics.cs`
- Modify: `CLI_API_Specification.md`
- Modify: `CLI_Agent_Daemon_Specification.md`
- Modify: `BookOfEternityClient/Launcher/CLI_Launch_Script.md`
- Modify: `BookOfEternityClient/game_master_daemon.ps1`
- Modify: `TaskGuides/CLI_Step_Main.txt`
- Modify: `Rules/Block_0.txt`
- Modify: `Rules/Block_2.txt`
- Modify: `Rules/Block_5.txt`
- Modify: `Rules/Block_6.txt`
- Modify: `Rules/Block_10.txt`
- Modify: `Rules/Block_12.txt`
- Modify: `Rules/Block_13.txt`
- Modify: `Rules/Block_15.txt`
- Modify: `Rules/Block_15.A.txt`
- Modify: `Rules/Block_17.txt`
- Modify: `Rules/Block_32_Guardians.txt`
- Modify: `Rules/Block_CLI_Operations.txt`
- Modify: `Rules/Block_FINAL.txt`
- Create: `Examples/E_CLI_Mortal_Resources.txt`
- Modify: `Examples/E_Block_5.txt`
- Modify: `Examples/E_Block_6.txt`
- Modify: `Examples/E_Block_10.txt`
- Modify: `Examples/E_Block_13.txt`
- Modify: `Examples/E_Block_15.A.txt`
- Modify: `Examples/E_Block_16.txt`
- Modify: `Examples/E_Block_17.txt`
- Modify: `Examples/E_Block_21.txt`
- Modify: `Examples/E_Block_32.txt`
- Modify: `Examples/E_CLI_Afterlife_Turns.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify: `OtherGuides/Afterlife_Contract_Matrix.md`
- Modify: `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`
- Create: `FileSystemExample/game_session/game_state/resources/resource_definitions.json`
- Create: `FileSystemExample/game_session/game_state/resources/resource_state.json`
- Create: `FileSystemExample/game_session/game_state/resources/resource_history.json`
- Modify: affected active state under `FileSystemExample/game_session/game_state/`
- Create: `FileSystemExample/validator_fixtures/resource_materialization/`

**Step 1: RED exhaustive failure-injection and TOCTOU tests**

Inject failure after every owner companion, definition, state, history, effect carrier/index, pending, command, narrative, and interface write plus post-validation. Capture bytes and prior existence for the entire touched set before each case. Add late mutations for definition, owner, state, history, source, target, carrier, index, accepted event, command, pending request/receipt, and internal adapter input.

Required assertion shape:

```csharp
await Assert.ThrowsAsync<InvalidDataException>(() => context.PublishAsync());
await context.AssertExactSnapshotAsync(before);
await context.AssertNoStaleOutputAsync();
```

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceMaterializationLifecycleTests|FullyQualifiedName~CanonicalStateNormalizerTests.Resources"
```

Expected RED: any missing rollback/TOCTOU edge fails before production changes.

**Step 2: Harden rollback and repair classification**

Ensure one write lease is held from final before-image comparison through writes, post-validation, rollback, and decision. Diagnostics/report cleanup is best-effort and may never prevent the caller from returning operational failure for rollback. Preserve `SessionReplaced` semantics. Suppress stale narrative/interface output. Only one unambiguous GM-owned omission may produce a bounded repair packet; identity, replay, arithmetic, capacity, cycle, owner, TOCTOU, direct mutation, and privacy failures remain protected.

**Step 3: RED a complete no-legacy source guard**

The guard must scan active production code, response models/mappings, prompts, examples, templates, and manifests for every removed field/write route. Use a narrow explicit allow-list only for historical design/audit text and rejection tests. It must also reject accidental admission of currency, treasury, faction accounting, market balance, progression, relationships, spiritual axes, effect lifetime counters, QTE-local progress, and boolean entitlements.

**Step 4: Delete remaining runtime compatibility and fallback paths**

Remove every active legacy reader, writer, validator, mapper, normalizer, sidecar, and UI fallback identified by the guard. Old technical saves containing any removed authority must fail as incompatible; do not add a migration, upgrader, legacy parser, or fallback branch to make them pass.

**Step 5: Synchronize Mortal GM contracts and add a worked example**

Document the three resource command arrays, exact temporary owner refs, closed operations, no direct current/max/IDs/phase/policy, client-owned publication, setting definition creation, capacity transition, ordinary mutation, and bounded receipt. `Examples/E_CLI_Mortal_Resources.txt` must include at least:

1. legal setting resource definition and initialization;
2. player or NPC ordinary mutation;
3. item use or combat mutation;
4. bounded effect receipt;
5. an explicit illegal direct-write contrast.

Register the example in `example_validation_manifest.json` and validate it through production parsers.

**Step 6: Synchronize afterlife contracts and worked examples**

Update the contract matrix, glossary, launcher/daemon guidance, relevant Rules, and `E_CLI_Afterlife_Turns.txt` for spiritual action points, per-return gacha attempts, and numeric blessing rerolls. State explicitly that currencies, faction ledgers, spiritual axes, and boolean/free entitlements are not resources. Update manifest and coverage assertions in the same change.

**Step 7: Replace the active template and fixtures**

Seed only the three canonical resource roots; omit `resource_commands.json` outside a staged turn. Remove player/NPC/combat/item/afterlife mechanical mirrors. Update fixture manifests/readmes and add positive/negative validator fixtures. Never preserve an old fixture merely to demonstrate compatibility.

**Step 8: GREEN documentation, rollback, source guard, and FullValidation**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceContractSourceGuardTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceMaterializationLifecycleTests|FullyQualifiedName~CanonicalStateNormalizerTests.Resources|FullyQualifiedName~ResourceMaterializationValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~FileSystemExampleFixtureIntegrityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
git diff --check
```

Expected: all selected tests and FullValidation pass within their bounded lanes; every summary reports no timeout, duplicate IDs, or cleanup failure.

**Step 9: Commit the breaking cutover**

```powershell
git add BookOfEternityClient BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests CLI_API_Specification.md CLI_Agent_Daemon_Specification.md TaskGuides Rules OtherGuides Examples FileSystemExample specs/1535-complete-effect-materialization specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
git commit -m "feat: complete breaking resource authority cutover (#1543)"
```

Before committing, inspect `git status --short` and exclude unrelated user files. The broad `git add` above is permitted only after that inspection proves every listed change belongs to #1543/#1535.

---

### Task 14: Prove scale, reconcile specifications, review the complete range, and run final gates

**Spec Kit tasks:** T111–T121

**Files:**

- Create: `BookOfEternityClient.Tests/ResourceAuthorityScaleTests.cs`
- Create: `BookOfEternityClient.IntegrationTests/AcceptedMechanicsPlannerScaleTests.cs`
- Modify only when evidence requires it: resource/accepted-mechanics services and tests
- Modify: `specs/1543-unified-resource-authority/tasks.md`
- Modify: `specs/1543-unified-resource-authority/quickstart.md`
- Modify: `docs/superpowers/plans/2026-08-15-unified-resource-authority.md`

**Step 1: RED closed limits and scale guards**

Test 256/257 definitions, 20,000/20,001 live entries, 256/257 capacity transitions, 512/513 pre-trigger mutations, 1,024/1,025 graph nodes, depth 32/33, and 64/65 pending requests. Measure representative doubled definition/owner/state/history and planner/trigger populations; require work or elapsed benchmark ratio at or below 2.5x using the repository's stable measurement convention.

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceAuthorityScaleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AcceptedMechanicsPlannerScaleTests"
```

Expected RED if any catalog is rebuilt per mutation/consumer or a bound is not enforced.

**Step 2: Optimize only measured repeated work**

Cache definition, owner, state, history, replay, effect source/target/carrier, pending, and event indexes inside one immutable plan input. Do not change limits, filters, assertions, test counts, lane timeout, or concurrency to make the guard pass. Re-run only the two Focused scale commands until GREEN.

**Step 3: Run one meaningful Fast checkpoint**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: exit `0`, complete fast project, no timeout/failure/duplicates/cleanup leak. Record exact path/count/time. Do not repeat Fast immediately before PreMerge.

**Step 4: Reconcile all durable artifacts**

Map every FR-001–FR-060 and SC-001–SC-010 to implemented code, test evidence, docs/examples, or an explicitly still-open task. Update `tasks.md` checkboxes only after inspecting the corresponding diff and summary. Re-run `speckit-analyze`; resolve every Critical/High and every implementation-relevant Medium finding without weakening the constitution or requirements.

**Step 5: Perform a fresh complete read-only review**

Review the exact #1543/#1535 range for:

- definition/state/history/owner uniqueness and closed contracts;
- exact arithmetic, ordering, replay, graph, and pending semantics;
- same-turn owner identity and terminal cleanup;
- full-input cache invalidation and lease-bound before-images;
- rollback bytes/prior absence and stale-output suppression;
- source/target/effect/resource agreement;
- projection parity and recursive privacy;
- GM prompts/examples/manifests/template validity;
- zero legacy writers/readers/mirrors and zero out-of-scope resource admission.

Use the `requesting-code-review` checklist locally unless the user explicitly authorizes a subagent. Fix every Critical/Important finding through fresh RED/GREEN evidence, then repeat the narrow review of each fix.

**Step 6: Run the conditional lifecycle gate**

This feature changes accepted-turn lifecycle, pending work, rollback, and output suppression, so run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
```

Record the complete result. Only `Focused` accepts `-Filter`; do not change the runner to admit a LifecycleIntegration filter.

**Step 7: Inspect final scope before the only PreMerge run**

Run:

```powershell
git diff --check
git status --short
git diff --name-status $(git merge-base HEAD origin/main)..HEAD
git diff --name-status
rg -n "\.github/workflows|GitHub Actions" specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md
```

Expected: only #1543/#1535 paths, no workflow/settings changes, every new file inspected, and all implementation work committed or intentionally staged. The documentation may state Actions are disabled; no workflow is enabled or invoked.

**Step 8: Run exactly one final PreMerge**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Expected: within the approved 20-minute deadline, exit `0`, no timeout, failed tests, duplicate IDs, cleanup failure, or leaked owned processes. Do not rerun automatically. On failure, inspect `summary.json`, logs, and TRX; fix with the smallest Focused/diagnostic lane and obtain user direction before another final attempt.

**Step 9: Record evidence and make the final implementation commit**

Write exact commands, result directories, counts, wall times, exit/timeout/duplicates/cleanup, changed files, prompt/docs/example updates, no-migration decision, no-Actions status, and residual risks into `quickstart.md` and this plan. Confirm no unchecked required task is being represented as complete.

```powershell
git add specs/1535-complete-effect-materialization specs/1543-unified-resource-authority docs/superpowers/plans/2026-08-15-unified-resource-authority.md BookOfEternityClient BookOfEternityClient.Tests BookOfEternityClient.IntegrationTests CLI_API_Specification.md CLI_Agent_Daemon_Specification.md TaskGuides Rules OtherGuides Examples FileSystemExample
git commit -m "feat: finalize unified resource materialization (#1535 #1543)"
git status --short
```

Use the same unrelated-file safeguard as Task 13 before the broad add. Expected final status: clean, or only explicitly preserved unrelated user-owned files outside the commit.

**Step 10: Stop at the owner-controlled integration boundary**

Present the reviewed commit SHA, exact verification evidence, changed paths, and residual risks. Do not push, open/merge a PR, change visibility/settings, or re-enable GitHub Actions unless the user explicitly directs that action. The repository owner may merge to `main` directly; every other contributor must use a PR and wait for owner approval.

---

## Completion definition

#1543 is complete only when all of the following are simultaneously true:

- every included mechanic has one accepted resource definition/state/history authority;
- all eight owner families use stable exact identity and complete lifecycle rules;
- ordinary, item, afterlife, and effect operations use one reducer and one plan;
- Effect Task 9 periodic/resource-event/pending behavior is integrated without an effect-only adapter;
- console, browser, and GM context use safe non-persisted projections;
- every legacy persisted mirror, response route, writer, reader, validator, fallback, active prompt/example/template occurrence is removed or rejects the old save;
- Mortal and afterlife worked examples pass production validation;
- exact rollback, TOCTOU, replay, bounds, graph, privacy, scale, Fast, conditional FullValidation/LifecycleIntegration, and final PreMerge evidence is recorded;
- no migration, compatibility reader, dual write, GitHub Actions, or unapproved integration action was introduced.

No earlier slice is independently merge-ready. If execution stops between tasks, keep #1543 and Effect Task 9 open and record the exact first unchecked Spec Kit task as the resume point.
