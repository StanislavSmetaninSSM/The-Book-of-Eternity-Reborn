# T070-B.4 Treatment Item Publication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish selected Mortal wound-treatment `item_quantity` consumption, including partial stacks and item-owned resources, through the existing immutable common plan and full accepted-turn transaction.

**Architecture:** Freeze one closed skill-plus-item response envelope, extract the accepted Mortal item phase and every later root-touching ordinary normalizer transform into shared pure projectors, and seal their exact production-order output immediately before common publication. Apply the sealed B.2 skill projection to that verified live baseline, then layer one shared pure consumption planner over the skill-composed root. The existing treatment resource authority privately seals item carrier/index after-images plus registered capacity/terminal-owner intents, and the common resource reducer and sole normalizer publish item, resource, skill, wound, history, output, and command state atomically.

**Tech Stack:** C#/.NET 8, `System.Text.Json.Nodes`, file-backed canonical JSON, xUnit, PowerShell 7 bounded test lanes.

## Global Constraints

- Tracked authority is GitHub issue #1536 and open Spec Kit task T070.
- Work only in `E:\Games\worktrees\boe-1536-wound-materialization` on `1536-complete-wound-materialization`.
- Preserve the six-argument `ComposeMortalWoundTreatmentPublication`, three-argument publication fingerprint helper, single `Finalize(resolution)`, and all public request/resolution/finalization DTO surfaces.
- The treatment response envelope admits only the existing six skill fields plus `UpdateInventory`, `moveInventoryItems`, `removeInventoryItems`, `NPCInventoryAdds`, `NPCInventoryUpdates`, `NPCInventoryRemovals`, and `NPCEquipmentChanges`; reject every other non-null property.
- B.4 is a guaranteed-treatment publication slice. Procedure/course/Fate/heal/recovery and T069-C stay later and are not B.4 GREEN gates.
- No migration or legacy-save compatibility path is required.
- Never call `MortalItemTransitionWriter` from treatment publication and never add a second canonical writer or normalizer.
- Never use raw deltas, caller paths, caller after-images, caller IDs, or caller history as authority.
- Preserve and do not stage the untracked `.serena/` directory.
- Use only `pwsh .\scripts\test-csharp.ps1`; do not run raw or unbounded `dotnet test`.
- During implementation use the smallest owning `Focused` selection, then one meaningful `Fast` checkpoint; reserve `PreMerge` for an explicitly requested integration/merge.
- This slice is client-owned implementation of the already documented treatment policy. It adds no GM-authored field, command, response, pending/control surface, or afterlife contract; record that no-update rationale in the final report.

---

### Task 1: Freeze the B.4 RED oracle

**Files:**

- Create: `BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ResourcePublication.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`

**Interfaces:**

- Consumes: production-created T067 request/resolution and T068 `Finalize(resolution)` authority.
- Produces: an immutable RED contract for the pure projection, mixed publication, replay, and rollback behavior; no production helper or test-created authority.

- [ ] **Step 1: Convert the old selected-item rejection test into the positive mixed atomic oracle**

Rename `GuaranteedResourceQuantity_SelectedItemConsumptionFailsClosedWithoutPartialSpend` to `GuaranteedMixedItemAndResourceConsumption_PublishesAtomicallyOnce`. Keep the existing production fixture, select both its item and resource requirements, and assert:

```csharp
Assert.True(composed.IsValid, Describe(composed.Issues));
Assert.Equal(1, ReadItemCount(composed.Plan!, selectedItemId));
Assert.Equal(expectedResourceAfter, ReadResourceCurrent(composed.Plan!, coordinate));
AssertItemIdentityActive(composed.Plan!, selectedItemId, expectedCount: 1);
Assert.DoesNotContain(
    composed.Issues,
    issue => issue.Code ==
        "mortal_wound_treatment_publication_item_consumption_unsupported");
```

- [ ] **Step 2: Add pure planner RED cases**

Add tests with these exact names and production-shaped canonical roots:

```text
Plan_PartialStackPreservesIdentityReceiptAndCarrier
Plan_FullStackConsumesIdentityClearsEquipmentAndReturnsTerminalOwner
Plan_FullStackRejectsContainerQuestBondOrOtherCompanionWithoutAfterImages
Plan_RepeatedClaimsEmitSequentialTransitionsInFinalizationOrder
Plan_ResourceBearingPartialScalesMaximumAndCurrentExactly
Plan_InexactOrNonInstanceFixedCapacityRejectsWithoutAfterImages
Plan_IsWriteFreeAndDeterministicAcrossDetachedInputs
Project_SameTurnCreateAndTransferUseSnapshotDeterministicReceiptAndTransitionIds
```

For the deterministic case, invoke the future pure planner twice with detached equivalent inputs and compare every carrier/index root, transition, capacity intent, terminal owner, issue, and fingerprint; read all canonical files before and after and assert byte equality.

- [ ] **Step 3: Add closed-envelope and final-baseline RED cases**

Add `GuaranteedItemConsumption_ClosedItemEnvelopePreservesEverySupportedFieldAndRejectsEveryOtherField` and `GuaranteedItemConsumption_FinalPrepublicationBaselineIncludesEveryRootTouchingNormalizer`. The first freezes all seven ordinary item properties together with the six skill properties and rejects each unrelated non-null `GameResponse` property. The second proves the sealed baseline equals the live output immediately before common publication after item transfer/materialization/equipment/identity, ordinary NPC core/trade, and inventory-journal transforms; it also proves NPC-core authority and NPC-trade/training pending-byte drift reject. The live baseline must exclude the B.2 treatment skill projection, which the common candidate applies afterward. Do not use a procedure/course publication test in this task.

- [ ] **Step 4: Add publication/lifecycle RED cases**

Add exact tests for:

```text
GuaranteedMixedItemAndResourceConsumption_PostWriteFailureRestoresEveryRootAndRearmsSamePlan
GuaranteedItemConsumption_PostSealCountCarrierOrIndexDriftRejectsBeforeAnySpend
GuaranteedItemConsumption_NpcSkillAndItemShareOneComposedNpcCoreRoot
GuaranteedItemConsumption_ColdAcceptedReplayDoesNotAppendItemOrResourceTransitions
GuaranteedItemConsumption_SameTurnItemNormalizationSurvivesPublication
GuaranteedItemConsumption_UnsupportedCompanionReferenceRejectsBeforeAnyWrite
```

The rollback test captures bytes for every touched carrier, `item_identity_index.json`, the resource quartet, wound carrier/index/history, output, and command roots; after the injected failure every byte must match and the exact same plan object/confirmed hold must be available for one retry.

- [ ] **Step 5: Run the smallest RED selections**

Run:

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemConsumptionPlannerTests"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~GuaranteedMixedItemAndResourceConsumption_PublishesAtomicallyOnce"
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~GuaranteedItemConsumption_"
```

Expected: tests execute, fail only at the absent pure planner or the typed selected-item publication boundary, and leave complete cleanup with no duplicate IDs.

- [ ] **Step 6: Commit the RED oracle**

```powershell
git add -- BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ResourcePublication.cs BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs
git commit -m "test(wounds): specify treatment item publication transaction (#1536)"
```

### Task 2: Extract the deterministic final pre-publication projection

**Files:**

- Create: `BookOfEternityClient/Services/MortalItemCanonicalProjectionPlanner.cs`
- Create: `BookOfEternityClient/Services/MortalItemPublicationBaselinePlanner.cs`
- Create: `BookOfEternityClient/Services/MortalItemTransferPlanner.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs`
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs`
- Modify: `BookOfEternityClient/Services/MortalItemAcceptedEffectSourceAuthority.cs`
- Modify: `BookOfEternityClient/Services/MortalItemIdentityState.cs`
- Modify: `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentSkillPublication.cs`
- Test: `BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.cs`
- Test: existing Mortal item normalization/transfer/equipment/NPC skill/trade/inventory-journal test classes selected by source search before editing.

**Interfaces:**

- Consumes: the closed seven-property item envelope (sealed alongside but not merged into the B.2 skill envelope), exact current and backup carrier/command/index roots, route catalog, accepted turn, detached `NpcCoreChangesContract.Authority`, exact NPC-trade/training pending-file bytes, and `MortalItemAcceptedTurnNormalizationSnapshot`.
- Produces: one item-phase projection plus one detached exact final carrier/index baseline immediately before common publication, issues, and recomputable fingerprints; no filesystem write.

- [ ] **Step 1: Define the closed projection input/result**

Create `MortalTreatmentItemCommandEnvelope` by freezing exactly the seven item properties listed in Global Constraints, with detached canonical arrays and a fingerprint. Bind that fingerprint alongside the already sealed six-field skill envelope in the outer publication authority, but do not feed skill commands into the ordinary live-baseline projection. Create internal immutable projection types with this shape:

```csharp
internal sealed record MortalItemCanonicalProjectionInput(
    int Turn,
    MortalItemAcceptedTurnNormalizationSnapshot Snapshot,
    MortalItemRouteAuthorityCatalog RouteCatalog,
    IReadOnlyDictionary<string, JsonObject?> CurrentRoots,
    IReadOnlyDictionary<string, JsonObject?> BackupRoots,
    MortalItemIdentityParseResult IdentityState);

internal sealed record MortalItemCanonicalProjectionResult(
    IReadOnlyDictionary<string, JsonObject> ItemPhaseAfterImages,
    JsonObject IdentityIndexAfterImage,
    IReadOnlyList<ValidationIssue> Issues,
    string Fingerprint)
{
    internal bool IsValid => Issues.Count == 0;
}

internal sealed record MortalItemPublicationBaselineInput(
    MortalItemCanonicalProjectionResult ItemPhase,
    MortalTreatmentItemCommandEnvelope Envelope,
    NpcCoreChangesContract.Authority NpcCoreAuthority,
    CanonicalBeforeImage NpcTradePending,
    CanonicalBeforeImage TrainingPending,
    IReadOnlyDictionary<string, JsonObject?> BackupRoots);

internal sealed record MortalItemPublicationBaselineResult(
    IReadOnlyDictionary<string, JsonObject> FinalCarrierRoots,
    JsonObject IdentityIndexAfterImage,
    IReadOnlyList<ValidationIssue> Issues,
    string Fingerprint);

internal static class MortalItemCanonicalProjectionPlanner
{
    internal static MortalItemCanonicalProjectionResult Project(
        MortalItemCanonicalProjectionInput input);
}
```

Both results must clone all roots and recompute fingerprints from the full input/output graph. The final result must identify its ordered transform versions and represent the exact roots expected after ordinary normalization and immediately before `PublishAcceptedMechanicsAsync`. Its seal includes the semantic NPC-core authority fingerprint plus presence and SHA-256 for both pending snapshots. It excludes the B.2 skill mutation. Do not expose mutable `JsonObject` instances retained from input.

- [ ] **Step 2: Move accepted item-phase transformation into `Project`**

Extract transfer catalog classification, whole-stack transition application, inline-equipment clearing, and exact command-row removal into `MortalItemTransferPlanner`. It accepts complete detached current/backup roots and explicit client-minted transition IDs; it never calls `MortalItemTransitionWriter` or writes. Add explicit-ID overloads for `MortalItemIdentityState.CreateRootReceipt` and `CreateTransition` here, retaining random overloads only for ordinary non-accepted callers, and refactor the ordinary writer's transfer branch to call the same pure planner with its own client-minted ID. Extend `MortalItemAcceptedTurnNormalizationSnapshot` to derive and expose accepted creation root receipt/create-transition IDs plus transfer-transition IDs from session/snapshot/turn, exact route or transfer authority fingerprint, and ordinal. Then lift the remaining deterministic transformation portion of `NormalizeMortalItemsAsync` without changing validations, order, path selection, item ID allocation, mirror rules, or canonical serialization. The item phase applies transfers before creation and returns detached roots including removed command rows. File reads, lease checks, backups, writes, readback, and rollback remain in the normalizer wrapper.

- [ ] **Step 3: Extract and compose every later selected-root transform**

Factor the root transformations used by `NormalizeNpcCoreChangesAsync`, `NormalizeNpcTradeCoreAsync`, and `NormalizeInventoryItemsAsync` into shared pure functions. The NPC-core function accepts only the detached `NpcCoreChangesContract.Authority` and exact NPC-trade/training pending snapshots used to build `MortalActorAcceptedTurnAuthority`; it performs no live read. `MortalItemPublicationBaselinePlanner` applies those transforms to item-phase roots in the same order as `NormalizeAccumulatedStateCoreAsync`: ordinary NPC core, NPC trade canonicalization, inventory journal normalization. Each ordinary normalizer invokes the same function and retains only its read/write/lease/rollback wrapper. `NormalizeMortalItemsAsync` likewise commits only its pure item-phase result. Separately refactor the B.2 treatment NPC skill projector to accept a supplied verified baseline root; do not include its output in the live baseline. No transformation may have a second treatment-only implementation.

- [ ] **Step 4: Bind the closed envelope and final baseline to the accepted item cache**

Extend `MortalItemAcceptedTurnNormalizationSnapshot` so its sealed value proves the exact item allocation/route authority, complete current/backup transfer roots, ordered accepted transfers and deterministic creation receipt/create-transition plus transfer-transition IDs, closed seven-item-property envelope, item-phase projection, detached NPC-core authority, NPC-trade/training pending bytes, ordered ordinary tail transforms, and final pre-publication fingerprint. Preserve the current one-use take/rearm fence. A copied snapshot with changed allocation, turn, session, snapshot, envelope, backup/input root, transfer/receipt/transition-ID map, authority/pending snapshot, transform order/version, item-phase root, or final root must not match. Immediately before common publication, compare every live selected carrier/index root to this final ordinary baseline; never recapture or patch it. Only after equality may the common candidate apply the sealed B.2 skill projection and item consumption.

- [ ] **Step 5: Run parity controls**

Run the new pure-planner filter plus the smallest existing item creation, transfer, equipment, NPC mirror/skill/trade, and inventory-journal filters found by source search. Include one real six-argument accepted-normalizer regression with all seven item properties null except the selected same-turn case, one case for each unsupported non-null property, and a source guard proving accepted item normalization does not call `MortalItemTransitionWriter` or random receipt/transition-ID overloads.

Expected: item-phase and final-baseline tests pass; the live pre-publication roots equal the sealed final projection; all selected existing normalization tests pass unchanged.

- [ ] **Step 6: Commit the extraction**

```powershell
git add -- BookOfEternityClient/Services/MortalItemCanonicalProjectionPlanner.cs BookOfEternityClient/Services/MortalItemPublicationBaselinePlanner.cs BookOfEternityClient/Services/MortalItemTransferPlanner.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.MortalItems.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Npcs.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.InventorySidecars.cs BookOfEternityClient/Services/MortalItemAcceptedEffectSourceAuthority.cs BookOfEternityClient/Services/MortalItemIdentityState.cs BookOfEternityClient/Services/MortalItemTransitionWriter.cs BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentSkillPublication.cs BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.cs
git commit -m "refactor(items): extract accepted item state projection (#1536)"
```

### Task 3: Implement one shared pure item-consumption model

**Files:**

- Create: `BookOfEternityClient/Services/MortalItemConsumptionPlanner.cs`
- Create: `BookOfEternityClient/Services/MortalItemOwnedResourceConsumptionPlanner.cs`
- Modify: `BookOfEternityClient/Services/MortalItemIdentityState.cs`
- Modify: `BookOfEternityClient/Services/MortalItemTransitionWriter.cs`
- Modify: `BookOfEternityClient/Services/MortalItemTransitionWriter.Stacks.cs`
- Modify: `BookOfEternityClient/Services/MortalItemTransitionWriter.Resources.cs`
- Test: `BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.cs`
- Test: `BookOfEternityClient.IntegrationTests/ResourceItemIntegrationTests.cs`

**Interfaces:**

- Consumes: exact projected item roots/index, ordered client-minted consumption commands, resource definitions/state, and deterministic treatment authority.
- Produces: exact item after-images, ordered identity transitions, registered capacity intents, terminal owner keys, issues, and one fingerprint.

- [ ] **Step 1: Define the pure consumption command/result**

```csharp
internal sealed record MortalItemConsumptionCommand(
    int FinalizationOrdinal,
    string ItemId,
    int Quantity,
    string ClaimFingerprint,
    string TransitionId,
    string AuthorityKind,
    string AuthorityId);

internal sealed record MortalItemConsumptionPlanningResult(
    IReadOnlyDictionary<string, JsonObject> CarrierAfterImages,
    JsonObject IdentityIndexAfterImage,
    IReadOnlyList<ResourceCapacityIntent> CapacityTransitions,
    IReadOnlyList<ResourceOwnerKey> TerminalOwners,
    IReadOnlyList<ValidationIssue> Issues,
    string Fingerprint)
{
    internal bool IsValid => Issues.Count == 0;
}
```

`Plan` accepts commands only in exact contiguous finalization-ordinal order and validates item carrier/index/current-transition quantity agreement before changing a detached working set.

- [ ] **Step 2: Use the shared explicit transition-ID contract for consumption**

Use the production-internal explicit-ID `MortalItemIdentityState.CreateTransition` overload added by Task 2. Keep the random-ID overload for ordinary non-accepted callers. Consumption transition IDs come only from the treatment command and must pass the same uniqueness, confusable-identity, prefix/history, carrier, and quantity validation as create/transfer IDs.

- [ ] **Step 3: Implement partial and full stack transitions**

For partial consumption, replace only `count`, preserve receipt/materialization/origin/carrier fields, and append one `consume` transition whose source and destination carrier are equal. For full consumption, reuse the exact #1511 companion scan: clear only supported inline equipment. If a container, quest, bond, or any other companion reference exists without its own genuine atomic transition authority, reject the entire result without after-images. B.4 mints no such companion authority. Otherwise remove the item occurrence, set identity state to `consumed`, null the destination carrier, and return the exact Mortal item `ResourceOwnerKey` as terminal.

- [ ] **Step 4: Extract strict item-owned resource planning**

For each partial command and each live item-owned coordinate compute:

```csharp
var newMaximum = ExactScale(oldMaximum, remainingCount, sourceCount, definition.Quantum);
var newCurrent = ExactScale(oldCurrent, remainingCount, sourceCount, definition.Quantum);
```

Require `instance_fixed`, checked decimal arithmetic, exact division, and quantum alignment. Emit `ResourceCapacityOperation.Reconfigure` with `ResourceCurrentDisposition.ScaleRatioExact`, registered-system-outcome phase, and private source/policy evidence. For full consumption emit no manual per-coordinate retirement intent; return the terminal owner for the common terminal-owner reducer.

Use one attempt-derived `OriginId` for all partial rows, fixed priority `70`, and
`FinalizationOrdinal.ToString("D4", CultureInfo.InvariantCulture)` in `EventRef`.
The existing capacity sorter will then execute repeated claims in finalization order;
priority `90` terminal-owner retirement follows every partial reconfiguration.

- [ ] **Step 5: Make the ordinary writer reuse the planner**

Replace its terminal-only consumption branch with `load -> Plan -> existing common resource reducer -> existing commit/rollback`. Preserve other split/merge/transfer routes and their public behavior. Remove duplicated proportional/terminal calculations only after parity tests cover them.

- [ ] **Step 6: Run planner and existing item-resource controls**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemConsumptionPlannerTests|FullyQualifiedName~ResourceItemIntegrationTests.Split_ProportionalExact_PartitionsEveryResourceWithoutRounding|FullyQualifiedName~ResourceItemIntegrationTests.TerminalConsume_RetiresResourcesAndMarksTheItemConsumed"
```

Expected: all selected tests pass with warning-free builds and complete cleanup.

- [ ] **Step 7: Commit the shared model**

```powershell
git add -- BookOfEternityClient/Services/MortalItemConsumptionPlanner.cs BookOfEternityClient/Services/MortalItemOwnedResourceConsumptionPlanner.cs BookOfEternityClient/Services/MortalItemIdentityState.cs BookOfEternityClient/Services/MortalItemTransitionWriter.cs BookOfEternityClient/Services/MortalItemTransitionWriter.Stacks.cs BookOfEternityClient/Services/MortalItemTransitionWriter.Resources.cs BookOfEternityClient.Tests/MortalItemConsumptionPlannerTests.cs BookOfEternityClient.IntegrationTests/ResourceItemIntegrationTests.cs
git commit -m "feat(items): plan exact partial and terminal consumption (#1536)"
```

### Task 4: Integrate item consumption into the common treatment plan

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentItemPublication.cs`
- Modify: `BookOfEternityClient/Services/ResourceRegisteredSystemOutcomeAdapter.cs`
- Modify: `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`
- Modify: `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentResourcePublication.cs`
- Modify: `BookOfEternityClient/Services/AcceptedMechanicsWoundCommonInputComposer.cs`
- Modify: `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalTreatmentSkillPublication.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentCapabilityAuthority.cs`
- Modify: `BookOfEternityClient/Services/CanonicalResourceOwnerAuthorityComposer.cs`
- Test: B.4 RED files from Task 1.

**Interfaces:**

- Consumes: the exact T068 finalization and pure item projection.
- Produces: one genuine private item publication capability embedded in the existing treatment resource authority and validated by cache admission.

- [ ] **Step 1: Add private registered capacity contribution**

Add:

```csharp
internal interface IResourceRegisteredSystemCapacityDraft
{
    IReadOnlyList<ResourceCapacityIntent> CapacityTransitions { get; }
}
```

`ComposeCapacityTransitions` concatenates these private registered intents before GM-command adapter matching. Command-supplied capacity intents remain subject to the existing exact command binding and unbound rejection. Duplicate replay keys across owner, command, and registered capacities reject.

- [ ] **Step 2: Make treatment resource identity allocation key-aware**

Add virtual overloads on `AcceptedMechanicsIdentityFactory` for a `ResourceCapacityIntent` and `ResourceMutationIntent`. Default behavior delegates to existing random methods. The treatment factory hashes `IdentitySeed + domain + canonical intent key` so adding capacity rows cannot shift B.3 mutation IDs and cold reconstruction is order-independent.

- [ ] **Step 3: Build and seal the item publication capability**

`MortalWoundTreatmentItemPublicationAuthority` owns the closed response envelope, item-phase projection, ordered final pre-publication baseline, consumption result, capacity intents, terminal owners, item carrier/index before/after-images, final owner authority, and fingerprint. Its constructor requires the existing private treatment mint and exact reference agreement with accepted state, request, resolution, finalization, continuation, and publication reservation. Its candidate validator recomputes every seal and compares exact live final-baseline roots, plan roots, transitions, and resources.

- [ ] **Step 4: Replace the typed unsupported branch only for genuine authority**

In `BuildTreatmentResourcePublication`, map each selected `item_quantity` intent to one `MortalItemConsumptionCommand` using permanent `AuthorityRef`, claim quantity/fingerprint, and finalization ordinal. Derive all IDs privately. If any command cannot produce the complete item authority, return a stable typed issue before building resource mutations; never publish a resource-only subset. Keep the old unsupported code as the fail-closed fallback for missing/foreign/incomplete capability.

- [ ] **Step 5: Compose final item and owner roots**

In `AcceptedMechanicsWoundCommonInputComposer`, obtain the exact pure item phase and apply the ordered ordinary NPC core/trade and inventory-journal tail to produce the final pre-publication baseline. Require byte/JSON agreement between that sealed baseline and the live roots produced by ordinary normalization. Apply the sealed B.2 skill projection to the verified baseline, then apply item consumption to the skill-composed root. Compose one final `npc_core.json` and `items.json` after-image for each touched path. Build the final `ResourceOwnerAuthority` from projected active owners plus terminal historical item keys, pass item capacity transitions/terminal owners into the common reducer, and add carrier/index after-images to the one common plan.

- [ ] **Step 6: Narrowly validate shared skill/item roots**

Refactor the treatment NPC skill projector to consume the verified final-baseline `npc_core.json` supplied by the sealed baseline planner rather than rereading the raw root. `ValidateTreatmentSkillProjectionCandidate` may accept a root different from its skill-only result only when the genuine final-baseline, skill, and item authorities prove the exact baseline-to-skill-to-consumed transition for that same path. Re-derive active/passive skill catalogs from the final root and compare all touched actor entries. Keep all other path-set and whole-root comparisons exact.

- [ ] **Step 7: Green the mixed and pure production filters**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalItemConsumptionPlannerTests|FullyQualifiedName~GuaranteedMixedItemAndResourceConsumption_PublishesAtomicallyOnce|FullyQualifiedName~GuaranteedItemConsumption_"
```

Expected: selected tests pass; no direct writer call or alternate normalizer appears in source guards.

- [ ] **Step 8: Commit common-plan integration**

Stage only the listed production/test files and commit:

```powershell
git commit -m "feat(wounds): publish held treatment items atomically (#1536)"
```

### Task 5: Close lifecycle, replay, drift, and rollback boundaries

**Files:**

- Modify: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ResourcePublication.cs`
- Modify only when an oracle proves a defect: B.3 transaction/coordinator/normalizer production files.

**Interfaces:**

- Consumes: completed B.4 common plan and existing one-use B.3 take receipt.
- Produces: fresh evidence for atomic rollback, same-plan rearm, cold once-only publication, and accepted replay.

- [ ] **Step 1: Prove post-seal drift fails before partial spend**

Mutate count, carrier, item index tail, one item-owned resource capacity/current, and the shared NPC mirror independently after plan sealing. Each variant must fail admission/preflight with all item/resource/wound/command bytes unchanged and the confirmed hold preserved or terminally handled according to the existing B.3 fence.

- [ ] **Step 2: Prove every transaction fault restores the expanded root set**

Reuse the B.3 failure-injection matrix at helper readback, runtime refresh, wound output seal, critical state, full state, cleanup, and final refresh. For each point assert exact restoration of item carrier/index/equipment roots and resource state/history/owner authority in addition to the retained B.3 roots, then prove one exact retry commits once.

- [ ] **Step 3: Prove guaranteed-treatment cold publication and accepted replay**

Create a guaranteed-stabilization cold fixture through the same six-argument production entry point and exact durable request reconstruction used by B.3. The first fresh filesystem/process-local registry must reconstruct the held selected item/resource claims and publish one sequence; a second restart with accepted history must neither prepare/finalize again nor append item/resource transitions. Keep existing procedure/course persistence cases RED at their later publication boundary; do not broaden B.4 to green them.

- [ ] **Step 4: Run narrow lifecycle controls**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentResourcePublicationLifecycleTests|FullyQualifiedName~GuaranteedItemConsumption_"
```

Expected: every guaranteed B.4 lifecycle case passes. Procedure/course and mixed recovery classes are not B.4 success gates while their separately open publication/recovery contours remain RED.

- [ ] **Step 5: Commit lifecycle closure**

```powershell
git add -- BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.ResourcePublication.cs
git commit -m "test(wounds): close treatment item publication lifecycle (#1536)"
```

If a RED exposed a production lifecycle defect, stage only its individually inspected
production file in the same command. Before committing, inspect
`git diff --cached --name-only` and unstage every file not required by that exact defect.

### Task 6: Verify, review, and record the bounded slice

**Files:**

- Modify: `specs/1536-complete-wound-materialization/tasks.md`
- Create outside the tracked worktree: `.git/worktrees/boe-1536-wound-materialization/sdd/task-t070b4-item-publication-report.md`

**Interfaces:**

- Consumes: all B.4 commits and test artifacts.
- Produces: inspectable evidence; T070 and #1536 remain open.

- [ ] **Step 1: Run complete owning Focused controls**

Run one measured B.4 unit filter, one B.4 integration filter, retained B.3, B.1/B.2, and T068 filters. Increase the bounded timeout when measured runtime exceeds the old temporary limit; do not optimize tests merely to save seconds.

- [ ] **Step 2: Run one meaningful Fast checkpoint**

```powershell
pwsh .\scripts\test-csharp.ps1 -Lane Fast
```

Record completed host counts, intentional later-contour REDs, timeout state, duplicate IDs, cleanup, and warnings. Do not rerun Fast immediately before a future PreMerge.

- [ ] **Step 3: Run structural checks**

```powershell
git diff --check
git status --short
git log --oneline -8
```

Expected: no whitespace errors; only intentional task/report changes plus untracked `.serena/`; no migration, raw writer shortcut, public DTO widening, or GM/afterlife contract change.

- [ ] **Step 4: Request independent code review**

Review against #1536/T070, all 2026-09-02 clarifications, this plan, the closed response envelope, exact final pre-publication baseline, inline-equipment-only cleanup, exact item/resource authority, shared NPC root, rollback, replay, and test evidence. Resolve every P0/P1/P2 finding with a new RED where behavioral.

- [ ] **Step 5: Record evidence and commit the bounded report update**

Add the exact test artifact IDs and B.4 closure note to `tasks.md`; write the external report with commits, commands, outcomes, review findings, residual later contours, and the no-GM-update rationale. Commit tracked changes locally. Do not push, merge, close T070, close #1536, or run PreMerge unless the user explicitly asks.

## Self-Review

- Spec coverage: partial/full stacks, unsupported companions, repeated claims, item-owned resources, closed response envelope, complete final pre-publication baseline, shared NPC roots, mixed atomicity, replay, drift, and rollback each map to a task and named test.
- Placeholder scan: no `TBD`, `TODO`, generic “handle errors,” or unnamed test step remains.
- Type consistency: the item-phase result, closed envelope, NPC authority, and pending snapshots feed the ordered ordinary final-baseline projection; the verified baseline feeds the sealed B.2 skill projection; the skill-composed root feeds consumption; the consumption result feeds the item publication authority; registered capacities and terminal owners feed the existing common resource reducer; the final plan remains the only publication input.
