# T066 Mortal Wound Treatment Authority Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the lease-bound accepted-state, requirement-witness, course-coordinate, and canonical skill-capability authorities required before Mortal wound treatment can be resolved.

**Architecture:** Keep the completed three-argument T060 API/result/diagnostic contract
frozen, while extracting its per-kind internals into one shared structured evaluator and
cumulative ledger used by typed witnesses. One strict skill-extension contract feeds
both canonical validation and proof export; one accepted-state exporter composes trusted
T060 context/snapshot values; separate coordinate, course, bundle, and proof types carry
deterministic evidence into later T067/T070 work.

**Tech Stack:** C#/.NET 8, `System.Text.Json`/`System.Text.Json.Nodes`, immutable records and read-only collections, SHA-256 versioned fingerprints, xUnit, PowerShell 7 bounded test lanes.

## Global Constraints

- Tracked work is GitHub issue #1536, Spec Kit task T066. Do not mark T067-T070 complete.
- The approved design is `docs/superpowers/specs/2026-08-30-t066-mortal-wound-treatment-authority-design.md`; the feature contracts are `specs/1536-complete-wound-materialization/spec.md` and `data-model.md`.
- Preserve the exact three-argument `MortalWoundTreatmentAuthority.ResolveRequirements(WoundTreatmentRoute, Context, Snapshot)` API and `MortalWoundResolvedRequirement` shape.
- No migration, compatibility reader, caller-authored JSON authority, caller IDs/fingerprints, display-name lookup, raw mutations, resource reservations, dice, outcome resolution, history publication, or T070 plan composition.
- Only canonical player/NPC active/passive skills may prove a guarantee. Provider-owned proof may target combatants; target-owned proof requires an already canonical player/NPC target.
- Treat a trustworthy negative predicate as `Unsatisfied`, including a structurally
  valid row whose lifecycle is retired (`retired`) or whose active flag is false
  (`inactive`). Treat malformed, ambiguous, cross-realm, incomplete, or stale
  lease/snapshot/seal authority as `InvalidAuthority`.
- Use `ExactIdentifierConfusableKey` for all new exact/confusable identity sets.
- Use `apply_patch` for edits. Do not touch `.serena/`.
- Run C# tests only through `pwsh -NoProfile -File .\scripts\test-csharp.ps1`. During implementation use the smallest `Focused` filter; run one `Fast` only at the completed T066 checkpoint.
- This slice adds no reachable command, pending/control file, GM-authored response field, player projection, or afterlife contract. T072-T074 own Mortal GM docs/examples; afterlife docs remain unchanged.

---

### Task 1: Canonical Skill Capability Extension Contract

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentCapabilityContract.cs`
- Create: `BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityContractTests.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs` (`ValidatePlayerStateFiles`, `ValidateActiveSkillObject`, `ValidatePassiveSkillObject`)
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs` (`ValidateNpcCoreObjectShape`)
- Modify: `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Skills.cs`

**Interfaces:**

- Consumes: current player `activeSkillChanges[]`/`passiveSkillChanges[]`; current NPC `activeSkills[]`/`passiveSkills[]`; `ExactIdentifierConfusableKey`; closed wound complication kinds and the eight-row legacy bound.
- Produces:

```csharp
internal sealed record MortalWoundTreatmentCapabilityOperationLimits(
    bool MayStabilize,
    int MaximumRecoveryPoints,
    int MaximumSeverityReductionSteps,
    IReadOnlyList<string> RemovableComplicationKinds,
    bool MayHealAtSeverityI,
    int MaximumCosmeticHealLegacies,
    int MaximumMechanicalEffectHealLegacies);

internal sealed record MortalWoundTreatmentCapabilityDefinition(
    int SchemaVersion,
    string CapabilityRef,
    string WoundDomain,
    int MinimumSeverityRank,
    int MaximumSeverityRank,
    MortalWoundTreatmentCapabilityOperationLimits OperationLimits);

internal sealed record MortalWoundTreatmentCapabilitySkillSource(
    string OwnerKind,
    string OwnerId,
    string SkillKind,
    string SkillId,
    string DisplayName,
    string Lifecycle,
    bool Active,
    string SourcePath,
    IReadOnlyList<MortalWoundTreatmentCapabilityDefinition> Capabilities);

internal sealed record MortalWoundTreatmentCapabilityCatalogResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    IReadOnlyList<MortalWoundTreatmentCapabilitySkillSource> Sources);
```

- These are the exact contract entry points; both parsers return detached values and neither repairs or creates an extension:

```csharp
internal static MortalWoundTreatmentCapabilityCatalogResult ParseActorCatalog(
    string ownerKind,
    string ownerId,
    JsonObject activeSkillsRoot,
    string activeSourcePath,
    JsonObject passiveSkillsRoot,
    string passiveSourcePath);

internal static void ValidateSkillExtension(
    JsonElement skill,
    string skillPath,
    ICollection<ValidationIssue> issues);

internal static IReadOnlyList<ValidationIssue> ValidateComposedActorCatalog(
    string ownerKind,
    string ownerId,
    JsonObject currentActiveRoot,
    string activeSourcePath,
    JsonObject currentPassiveRoot,
    string passiveSourcePath,
    JsonObject composedActiveRoot,
    JsonObject composedPassiveRoot);
```

- [ ] **Step 1: Add direct RED tests for the closed extension and actor-wide namespace**

Add cases proving all four source locations, optional absence, exact fields, `physical`, severity 1-4, checked operation limits, closed complication kinds, aggregate legacy <= 8, at least one positive limit, required `skillId`, cross-active/passive exact/confusable uniqueness for skill IDs and capability refs, detachment, display-name non-authority, and exact normalizer preservation.

```csharp
private sealed record CapabilityCatalogScenario(
    string OwnerKind,
    string OwnerId,
    JsonObject ActiveRoot,
    string ActiveSourcePath,
    JsonObject PassiveRoot,
    string PassiveSourcePath,
    bool ExpectedValid,
    string? ExpectedCode,
    int ExpectedSourceCount);

[Theory]
[MemberData(nameof(CapabilityCatalogRows))]
public void ParseActorCatalog_UsesOneClosedCrossSkillNamespace(CapabilityCatalogScenario row)
{
    var result = MortalWoundTreatmentCapabilityContract.ParseActorCatalog(
        row.OwnerKind,
        row.OwnerId,
        row.ActiveRoot,
        row.ActiveSourcePath,
        row.PassiveRoot,
        row.PassiveSourcePath);
    Assert.Equal(row.ExpectedValid, result.IsValid);
    Assert.Equal(row.ExpectedCode, result.Issues.FirstOrDefault()?.Code);
    Assert.Equal(row.ExpectedSourceCount, result.Sources.Count);
}
```

- [ ] **Step 2: Run the isolated RED filter**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentCapabilityContractTests"
```

Expected: build/test failure because `MortalWoundTreatmentCapabilityContract` and its immutable result types do not exist.

- [ ] **Step 3: Implement the closed parser/catalog and validator hooks**

Use exact field sets and a single actor-wide identity pass:

```csharp
var skillIds = new Dictionary<string, string>(StringComparer.Ordinal);
var capabilityRefs = new Dictionary<string, string>(StringComparer.Ordinal);
foreach (var source in activeSources.Concat(passiveSources))
{
    RegisterExactAndConfusable(source.SkillId, source.Path + ".skillId", skillIds, issues);
    foreach (var capability in source.Capabilities)
        RegisterExactAndConfusable(capability.CapabilityRef, capability.Path + ".capabilityRef", capabilityRefs, issues);
}
```

Validate individual extensions from `ValidateActiveSkillObject` and
`ValidatePassiveSkillObject`; validate player cross-root identity after both player skill
files are read; validate NPC cross-list identity inside `ValidateNpcCoreObjectShape`.
Refactor `NormalizePlayerSkillStateAsync` to read and compose both player roots before
either write. Call `ValidateComposedActorCatalog` once over the complete current and
composed active+passive namespace; it requires every pre-existing valid extension to be
byte-semantically preserved, forbids a composed-only synthesized extension, and reruns
exact/confusable uniqueness over the final combined catalog. Throw the existing
normalizer invariant exception before either `WriteIfChangedAsync` when any issue exists.
Only after that gate may it write both roots. Do not synthesize `skillId` for
extension-free legacy rows.

- [ ] **Step 4: Run contract, production-validator, and normalizer controls**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentCapabilityContractTests|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.FixtureControl_SelectedSkillRowsUseExistingProductionMaterializationShapes"
```

Expected: every selected row passes with zero build warnings, timeout, duplicate ID, or cleanup failure.

- [ ] **Step 5: Commit Task 1**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentCapabilityContract.cs BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityContractTests.cs BookOfEternityClient/Services/Validation/ValidationService.PlayerAndInventory.cs BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Skills.cs
git commit -m "feat(wounds): validate treatment skill capabilities (#1536)"
```

---

### Task 2: Lease-Bound Accepted Treatment State

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentAcceptedStateAuthority.cs`
- Create: `BookOfEternityClient/Services/MortalWoundTreatmentSceneAuthorityContract.cs`
- Modify: `BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs`
- Modify: `BookOfEternityClient/Services/MortalLocationCustomStateContract.cs`
- Modify: `BookOfEternityClient/Services/MortalLocationAcceptedTurnPlanner.cs` only if the
  existing location-update validation does not already route through the explicit
  location-container contract without dropping unrelated siblings
- Modify: `BookOfEternityClient/Services/PendingTurnSnapshotReader.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityAuthorityTests.cs` only where a fixture lacks required canonical actor/location/co-presence source rows
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs` only where the same source-shaped fixture omission exists
- Modify: `BookOfEternityClient.Tests/PendingTurnSnapshotReaderTests.cs`
- Modify: `BookOfEternityClient.Tests/MortalLocationMaterializationContractTests.cs`
- Modify: `BookOfEternityClient.IntegrationTests/MortalLocationMaterializationLifecycleTests.cs`
- Modify: `OtherGuides/Wound_Materialization_Contract.md`
- Create: `Examples/E_CLI_Wound_Materialization.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify: `BookOfEternityClient/game_master_daemon.ps1`
- Modify: `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs`

**Interfaces:**

- Consumes: `PendingTurnSnapshotReader.ReadCurrent`, the active `CanonicalWriteLease`, strict T060 `Context`, canonical wound carrier/identity/history parsers, accepted events, canonical clock/effect mechanics, and Task 1 skill catalogs.
- Produces:

```csharp
internal sealed record MortalWoundTreatmentAcceptedStateAuthorityResult(
    bool IsValid,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundTreatmentAcceptedStateAuthority? Authority);

internal sealed class MortalWoundTreatmentAcceptedStateAuthority
{
    internal static MortalWoundTreatmentAcceptedStateAuthorityResult ExportCurrent(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAuthority.Context context,
        string woundId);
}
```

- The authority privately owns the live lease/root-generation guard and exposes internal detached `Binding`, `RequirementContext`, `RequirementSnapshot`, selected `CurrentWound`, complete parsed `History`, current canonical game minute, player/NPC capability catalogs, and their semantic fingerprints.
- It also exposes the detached typed T065 `MortalWoundTreatmentDefinition` selected
  from `CurrentWound`; later factories never reparse a raw route to obtain course data.
- The public/internal reflection surface of the result remains exactly `IsValid`, `Issues`, `Authority`; the authority has no public constructor or raw JSON/dice/mutation member.
- The registry surface is candidate-first and exactly:

```csharp
internal static MortalWoundTreatmentAcceptedStateAuthorityResult
    BindMortalWoundTreatmentAcceptedState(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        MortalWoundTreatmentAcceptedStateAuthorityResult candidate);
```

- [ ] **Step 1: Re-run the one-test accepted-state RED**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~BookOfEternityClient.Tests.MortalWoundTreatmentResolverTests.AcceptedStateExport_UsesParsedSelectionAndCanonicalRootsToDeriveItsProductionBinding"
```

Expected: 0/1 because `MortalWoundTreatmentAcceptedStateAuthority` is absent.

- [ ] **Step 2: Add canonical source-fault RED cases**

Add method-level cases for stale lease/snapshot, changed wound carrier versus identity/history, missing accepted event, ambiguous player/NPC/combat coordinate, malformed required root, structurally valid withdrawn consent, structurally valid unavailable resource, and detached output after persisted-root mutation. The last two must export valid accepted state; later requirement classification owns their negative result. Build these tests from actual canonical resource definition/state/history/owner agreement, item carrier/identity agreement, exact current-location/world-map/identity agreement, all known NPC rows plus `NPCsInScene`, signed combat roots, canonical skill mastery, regular-quest `status`, and the registered scene-state contract. Add explicit RED cases proving projection-shaped NPC/location shadow fields grant nothing.

Pin the complete translation boundary: an off-scene known NPC remains projected with
`reachable=false`; completed/failed regular quests remain queryable by exact status; a
production-valid active skill without applicable mastery gets no invented tier; item
quantity/availability comes only from carrier/identity agreement and only
`player_inventory -> (player, player_current)` / `npc_inventory -> (npc, exact NPC ID)`
project. Valid location/offscreen-storage/vehicle item siblings neither poison export nor
satisfy actor-owned requirements; valid decimal and
non-actor resource siblings do not poison accepted state; integral overflow is not
silently truncated; `combat_group_member` maps to `combatant_member`; and suspended
resources remain trusted inactive/zero-availability rows. Add direct-constructed,
wrong-version, copied-with-mutated-fields, and missing-provenance Context cases.

For scene-state validation, pin raw/canonical/current-scene/new-location/location-update
location containers and link containers. Reserved kinds in links fail. Duplicate and
case-confusable member names fail. Two distinct environment IDs may share one exact state,
while duplicate/confusable environment IDs fail. The accepted location-update test must
prove the full replacement array preserves unrelated custom-state siblings and agrees
between `world_map` and `current_location` after publication.

```csharp
[Theory]
[InlineData("withdrawn_consent")]
[InlineData("resource_unavailable")]
public void AcceptedStateExport_RetainsTrustedNegativePredicateEvidence(string mutation)
{
    using var fixture = AcceptedStateFixture.Create(CreateScenario(
        "procedure_normal_uses_lowest_free_die",
        "procedure"));
    fixture.ApplyTrustedNegativePredicateMutation(mutation);
    var result = fixture.ExportCurrent();
    Assert.True(Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")));
    Assert.NotNull(ReadRequiredProperty(result, "Authority"));
}
```

Add `ApplyTrustedNegativePredicateMutation` to the nested fixture with only these two
closed branches: change the canonical consent status to withdrawn, or change the exact
canonical item/resource availability predicate to unavailable, before preparing a fresh
snapshot/lease. Any unknown branch throws `ArgumentOutOfRangeException`; the helper does
not edit detached authority output.

- [ ] **Step 3: Implement signed snapshot composition and registry caching**

Build the complete required logical-path set before calling
`PendingTurnSnapshotReader.ReadCurrent`; parse only validated snapshot bytes. Derive
`WoundAcceptedTurnBinding` from the signed current request/event authority, select the
exact carrier/identity/history wound, retain its typed T065 treatment definition,
compose Task 1 capability rows into the otherwise unchanged T060 snapshot, and seal
versioned fingerprints.

Compose and validate the complete candidate before consulting the registry. Add one
registry slot scoped by file-system identity and lease generation/revision. Cache only
a successful immutable candidate, and compare the complete binding/event, context,
wound/carrier/index/history, clock, effect, item/resource, actor/location, skill-source,
and accepted-state fingerprints on every call. Exact semantic equality may reuse the
entry; any difference replaces/invalidates it. Never decide a hit from only context and
wound IDs, and never let a hit skip current signed-snapshot validation.

Resource authority must validate the signed definition/state/history/persisted-owner
quartet against a recomposed owner authority using only signed source roots. Items must
agree with their canonical identity and current transition quantity; only player/NPC
inventory carriers map to the exact actor coordinates above, while other carriers are
omitted; before T068 their exact current count is available and unreserved. Resource projection admits only
non-negative signed-32-bit integer Mortal actor rows, maps
`combat_group_member -> combatant_member`, maps active/suspended as specified in the data
model, and omits otherwise-valid non-projectable siblings. Actor identity includes known
off-scene NPCs; only exact `NPCsInScene` and signed combat roots grant current presence.
Current-location identity is independently agreed with world map and location identity.
Skill tier follows the four explicit player/NPC active/passive mastery mappings and is
absent rather than invented when mastery is unavailable. Regular quests map `status` to
transient `state` without retiring terminal rows. Facility/environment/consent come only
from the three closed version-1 registered location `customStates[]` kinds in the exact
current location. `ExportCurrent` rejects any Context lacking recomputable parser-origin
provenance or exact version 1.

Harden optional snapshot selection in the same slice: resolve keys ordinally and reject
case variants, raw duplicate keys, exact-plus-case aliases, asymmetric file/hash rows,
or more than 64 actually enumerated requested paths. Preserve the sole three-argument
reader API.

Document the three registered GM-authored scene-state kinds in the wound guide and a new
`Examples/E_CLI_Wound_Materialization.txt` worked example. Show the complete
`worldMapUpdates.locationUpdates[]` replacement route for a known location, preservation
of unrelated custom-state siblings, and the complete new-location envelopes. Register the
example in the validation manifest and daemon context pack. Update the always-read compact
Mortal-location template/directive to require the copied wound guide/example whenever the
GM authors one of these reserved kinds. Add source/documentation guards for the exact
kinds, closed fields, legal route, link rejection, context-pack copy, and prompt trigger.

```csharp
var candidate = ExportCurrentCore(fileSystem, writeLease, context, woundId);
return AcceptedTurnAuthorityRegistry.BindMortalWoundTreatmentAcceptedState(
    fileSystem,
    writeLease,
    candidate);
```

- [ ] **Step 4: Run accepted-state and frozen T060 controls**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedStateExport_UsesParsedSelectionAndCanonicalRootsToDeriveItsProductionBinding|FullyQualifiedName~AcceptedStateExport_BindsThePreparedManifestToCanonicalSourceChanges|FullyQualifiedName~MortalWoundRequirementAuthorityTests"
```

Expected: accepted-state methods and all frozen T060 rows pass; no public raw-authority surface appears.

- [ ] **Step 5: Commit Task 2**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentAcceptedStateAuthority.cs BookOfEternityClient/Services/AcceptedTurnAuthorityRegistry.cs BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityAuthorityTests.cs BookOfEternityClient.Tests/MortalWoundTreatmentResolverTests.cs
git commit -m "feat(wounds): export accepted treatment state (#1536)"
```

---

### Task 3: Attempt Coordinates, Canonical Time, and First-Course Authority

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentAttemptAuthority.cs`
- Create: `BookOfEternityClient.Tests/MortalWoundTreatmentAttemptAuthorityTests.cs`

**Interfaces:**

- Consumes: Task 2 accepted state including its detached typed
  `MortalWoundTreatmentDefinition`, a canonical `WoundMaterializationEnvelope before`,
  exact route ID/operation key/event ref, parsed history, and the route's first course
  milestone.
- Produces `MortalWoundTreatmentAttemptCoordinates`, `MortalWoundTreatmentAttemptCoordinatesResult`, `MortalWoundGameTimeAuthority`, `MortalWoundGameTimeAuthorityResult`, `MortalWoundCourseStartAuthority`, `MortalWoundCourseModeAuthority`, and `MortalWoundCourseModeAuthorityResult` with the exact property names frozen in `data-model.md` sections “Transient Mortal treatment attempt authorities” and “Course mode”.
- Adds only this partial planner seam; T067 later adds request/resolution methods:

```csharp
internal static partial class MortalWoundTreatmentPlanner
{
    internal static MortalWoundTreatmentAttemptCoordinatesResult CreateAttemptCoordinates(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        WoundMaterializationEnvelope before,
        string operationKey,
        string routeId,
        string eventRef);
}
```

- [ ] **Step 1: Add RED tests for closed coordinate and clock/course factories**

Cover exact happy-path coordinates; before/route/target/realm/event/history mismatch; malformed operation key; deterministic same semantic input; distinct operation key; canonical clock source; first milestone ordinal 1/`afterMinutes=0`; checked due/deadline overflow; active-course rejection; detached starting wound; and absence of caller course/attempt IDs.

- [ ] **Step 2: Run the new RED class**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentAttemptAuthorityTests"
```

Expected: build/test failure on the missing T066 coordinate/time/course types.

- [ ] **Step 3: Implement deterministic factories with checked arithmetic**

Seal the exact event authority from `acceptedState.Binding.AcceptedEvents`, recompute the
wound and raw T060 route fingerprints, derive `AttemptId` from the semantic coordinates,
and exclude the derived ID from its own input. Resolve the same route ID exactly once in
`acceptedState.TreatmentDefinition.Routes`, require the typed route kind/mode and its
semantic fields to agree with the raw T060 route, and use that typed definition for all
course values. `MortalWoundGameTimeAuthority.Create` accepts only accepted state and
coordinates. `MortalWoundCourseModeAuthority.Create` constructs only a first-course
authority when `before.Care.ActiveCourseId` is null; history-driven continuation remains
T067-B.

```csharp
var due = checked(gameTime.CurrentTimeInMinutes + milestone.AfterMinutes);
var deadline = checked(due + courseRoute.Resolution.MaximumGapMinutes);
```

Return typed issues rather than throwing on checked overflow or invalid input.

- [ ] **Step 4: Run Task 3 plus accepted-state controls**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentAttemptAuthorityTests|FullyQualifiedName~AcceptedStateExport_UsesParsedSelectionAndCanonicalRootsToDeriveItsProductionBinding"
```

Expected: all selected rows pass.

- [ ] **Step 5: Commit Task 3**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentAttemptAuthority.cs BookOfEternityClient.Tests/MortalWoundTreatmentAttemptAuthorityTests.cs
git commit -m "feat(wounds): seal treatment attempt coordinates (#1536)"
```

---

### Task 4: Shared T060 Structured Evaluator, Writer, and Non-Course Bundles

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentRequirementEvidence.cs`
- Create: `BookOfEternityClient/Services/MortalWoundTreatmentRequirementEvaluator.cs`
- Create: `BookOfEternityClient/Services/MortalWoundTreatmentRequirementAuthorityBundle.cs`
- Create: `BookOfEternityClient.Tests/MortalWoundTreatmentRequirementAuthorityBundleTests.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentAuthority.cs`

**Interfaces:**

- Consumes: Tasks 2-3 accepted state/coordinates, current wound, exact route, and
  unchanged T060 context/snapshot inputs.
- Produces one internal structured per-kind evaluator shared by the legacy T060 adapter
  and typed bundles, one shared cumulative item/resource ledger per evaluation batch,
  and the immutable closed types `MortalWoundTreatmentRequirementSuccessWitness`,
  `MortalWoundTreatmentRequirementFailureWitness`, kind-specific observations,
  `MortalWoundTreatmentRequirementBinding`,
  `MortalWoundTreatmentRequirementScopeAuthority`,
  `MortalWoundTreatmentRequirementAuthorityBundle`, and
  `MortalWoundTreatmentRequirementAuthorityBundleResult`.
- The evaluator entry points are internal implementation seams, never accepted-authority
  factories:

```csharp
internal static MortalWoundTreatmentRequirementEvaluationBatch EvaluateCommon(
    WoundTreatmentRoute route,
    MortalWoundTreatmentAuthority.Context context,
    MortalWoundTreatmentAuthority.Snapshot snapshot);

internal static MortalWoundTreatmentRequirementEvaluationBatch EvaluateCourse(
    WoundTreatmentRoute route,
    int milestoneOrdinal,
    MortalWoundTreatmentAuthority.Context context,
    MortalWoundTreatmentAuthority.Snapshot snapshot);
```

`EvaluateCourse` selects the milestone itself and evaluates common then milestone rows
through one ledger. The batch/scopes/rows carry a closed disposition
`Satisfied|Unsatisfied|InvalidAuthority`, the unchanged resolved row when satisfied,
the exact typed success/failure observation, and the legacy `ValidationIssue` mapping.
They expose no public constructor or accepted caller surface.
- Produces exact factories:

```csharp
internal static MortalWoundTreatmentRequirementAuthorityBundleResult CreateForProcedure(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates,
    WoundMaterializationEnvelope before);

internal static MortalWoundTreatmentRequirementAuthorityBundleResult CreateForGuaranteed(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates,
    WoundMaterializationEnvelope before);
```

- [ ] **Step 1: Add RED tests for all eleven success-witness kinds and bundle closure**

For each requirement kind, assert one binding with the unchanged
`MortalWoundResolvedRequirement`, a detached kind-complete witness, exact local index,
correct source/context values, and recomputed identical T060 row fingerprint. Assert
procedure/guaranteed mode, one `common` scope, null course fields, no failure witnesses,
deterministic bundle seal, and rejection of unsatisfied/ambiguous/stale-snapshot/
cross-realm common requirements. Add parity rows proving the three-argument legacy adapter preserves
the existing issue code/path/precedence, partial resolved-row ordering, and aggregate
item/resource ledger results for all eleven kinds.

- [ ] **Step 2: Run the non-course RED class**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentRequirementAuthorityBundleTests"
```

Expected: failure on missing evidence/bundle types while the frozen T060 resolver remains unchanged.

- [ ] **Step 3: Extract the structured evaluator and row writer without changing T060 output**

Move every existing T060 per-kind selection/predicate branch into
`MortalWoundTreatmentRequirementEvaluator` rather than copying it. Each branch emits a
typed current observation before classifying it; trusted mechanical loss maps to
`Unsatisfied`, while malformed/ambiguous/cross-realm/incomplete authority maps to
`InvalidAuthority`. Preserve the existing cumulative ledger object across every row in
the requested batch.

Then move the existing `CreateResolved` body, including its exact ordered field
serialization, behind one internal writer used by both the evaluator and typed-witness
reconstruction. Its signature mirrors the current private method exactly:

```csharp
internal static MortalWoundResolvedRequirement Create(
    int requirementIndex,
    string kind,
    string authorityRef,
    string realm,
    MortalWoundTreatmentAuthority.Context context,
    JsonElement authoredRequirement,
    IReadOnlyList<string?> mechanicalFields,
    string? ownerKind = null,
    string? ownerId = null,
    string? providerKind = null,
    string? providerId = null,
    string? targetKind = null,
    string? targetId = null,
    string? locationId = null,
    int? requestedQuantity = null,
    int? minimumTier = null,
    int? currentTier = null,
    string? currentState = null);
```

Name the helper `MortalWoundTreatmentResolvedRequirementWriter`. Copy the existing
version tag, context fields, authored requirement fields, mechanical fields, and
`WoundAcceptedTurnFingerprintWriter.Compute` call byte-for-byte before deleting the
private duplicate. Make the existing three-argument `ResolveRequirements` a strict
adapter over `EvaluateCommon`: it returns the same resolved rows, issues, success flag,
and result fingerprint as before. Do not add fields, overload that resolver, change
issue precedence, or leave any duplicate per-kind predicate implementation.

- [ ] **Step 4: Implement common-scope success witnesses and factories**

Resolve the selected route through `before`, call `EvaluateCommon` with
`acceptedState.RequirementContext/RequirementSnapshot`, consume its already-typed
observations, recompute the T060 row fingerprint through the shared writer, and seal
binding/scope/bundle in authored order. Do not attempt to recover mechanical fields from
the lossy public T060 result. `CreateForGuaranteed` still contains only ordinary T060
requirement evidence; Task 6 supplies the separate capability proof.

- [ ] **Step 5: Run bundle plus complete frozen T060 controls**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentRequirementAuthorityBundleTests|FullyQualifiedName~MortalWoundRequirementAuthorityTests"
```

Expected: all selected rows pass and T060 count/shape remain unchanged.

- [ ] **Step 6: Commit Task 4**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentRequirementEvidence.cs BookOfEternityClient/Services/MortalWoundTreatmentRequirementEvaluator.cs BookOfEternityClient/Services/MortalWoundTreatmentRequirementAuthorityBundle.cs BookOfEternityClient/Services/MortalWoundTreatmentAuthority.cs BookOfEternityClient.Tests/MortalWoundTreatmentRequirementAuthorityBundleTests.cs
git commit -m "feat(wounds): seal treatment requirement bundles (#1536)"
```

---

### Task 5: Course-Milestone Requirement Classifier

**Files:**

- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentRequirementEvidence.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentRequirementEvaluator.cs`
- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentRequirementAuthorityBundle.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentRequirementAuthorityBundleTests.cs`

**Interfaces:**

- Consumes: Task 3 valid course mode/start authority, canonical before/history agreement, common route requirements, and the exact milestone requirements.
- Produces:

```csharp
internal sealed record MortalWoundCourseRequirementAuthorityResult(
    string Status,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundTreatmentRequirementAuthorityBundle? Authority);

internal static MortalWoundCourseRequirementAuthorityResult CreateForCourseMilestone(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates,
    WoundMaterializationEnvelope before,
    WoundHistoryParseResult history,
    MortalWoundCourseModeAuthority courseMode);
```

- [ ] **Step 1: Add RED `Satisfied|Unsatisfied|InvalidAuthority` matrix tests**

Cover common and milestone scopes, local indices, exact ordinal, all eleven kind-complete
success witnesses, every closed loss reason, `authority_absent` from a complete source
set, reserved/quantity/tier/state/lifecycle/availability/reachability/consent/location/
presence losses, malformed root, ambiguous/confusable row, cross-realm row, changed
history/route/course/coordinate seal, and deterministic failure observation
fingerprints. Include aggregate item/resource quantity whose individual common and
milestone rows fit but whose combined total exceeds availability; it must be
`Unsatisfied`, proving one ledger spans both scopes. Include first-course unsatisfied
with null `InterruptionReason`. Do not construct a test-only continuation authority:
production continuation restoration and its deadline-dominance GREEN control remain
T067-B-owned.

- [ ] **Step 2: Run only course classifier tests**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentRequirementAuthorityBundleTests&FullyQualifiedName~Course"
```

Expected: newly added course rows fail because the five-argument classifier and failure-witness union are absent.

- [ ] **Step 3: Implement typed observations and classification precedence**

Call `EvaluateCourse` once so common and milestone scopes share one cumulative ledger.
Use `Unsatisfied` only when the source universe is valid and the exact observed row is
absent or a mechanical predicate is false. Return `InvalidAuthority` with null authority
for malformed/ambiguous/cross-realm/incomplete source or seal disagreement. On
`Satisfied|Unsatisfied`, return both scopes and exact course coordinates.

```csharp
var status = batch.HasInvalidAuthority
    ? "InvalidAuthority"
    : batch.HasUnsatisfiedPredicate ? "Unsatisfied" : "Satisfied";
```

For ordinal 1 with `before.Care.ActiveCourseId=null`, trusted unsatisfied evidence
returns a complete bundle with `InterruptionReason=null`; T067 rejects the attempted
course start. A reason may be sealed only for a valid continuation whose supplied
course mode/start authority matches the already-active course and which T067-B is
resolving as an accepted interruption. In that path `deadline_exceeded` dominates
`requirements_unsatisfied`. Implement that branch against the closed course-mode type,
but leave its existing production continuation tests RED until T067-B provides the
history-backed constructor; there is no caller reason parameter or test-created
authority escape hatch.

- [ ] **Step 4: Run course, non-course, and T060 controls**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentRequirementAuthorityBundleTests|FullyQualifiedName~MortalWoundRequirementAuthorityTests"
```

Expected: all selected rows pass without altering T060 results.

- [ ] **Step 5: Commit Task 5**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentRequirementEvidence.cs BookOfEternityClient/Services/MortalWoundTreatmentRequirementEvaluator.cs BookOfEternityClient/Services/MortalWoundTreatmentRequirementAuthorityBundle.cs BookOfEternityClient.Tests/MortalWoundTreatmentRequirementAuthorityBundleTests.cs
git commit -m "feat(wounds): classify course treatment requirements (#1536)"
```

---

### Task 6: Canonical Capability Proof Exporters

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentCapabilityAuthority.cs`
- Modify: `BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityAuthorityTests.cs` only to add a T066-local exporter control or complete canonical source fixtures; do not weaken existing T070 rows

**Interfaces:**

- Consumes: Task 1 typed source catalog, Tasks 2-3 accepted state/coordinates, exact `capabilityRef`, exact `actorRole=provider|target`, and for final revalidation an existing validated `AcceptedMechanicsPlan`.
- Produces the exact immutable `MortalWoundTreatmentCapabilityProof`, operation-limit value, and result shape from `data-model.md`, plus:

```csharp
internal static MortalWoundTreatmentCapabilityProofResult ExportCurrent(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates,
    string capabilityRef,
    string actorRole);

internal static MortalWoundTreatmentCapabilityProofResult ExportForPublication(
    MortalWoundTreatmentAcceptedStateAuthority acceptedState,
    MortalWoundTreatmentAttemptCoordinates coordinates,
    string capabilityRef,
    string actorRole,
    AcceptedMechanicsPlan publicationPlan);
```

- [ ] **Step 1: Re-run current-export RED rows after Tasks 1-5**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.ExportCurrent_UsesOnlyCanonicalPlayerOrNpcSkillCapabilitySource|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.ExportCurrent_SameSessionDistinctAttemptCoordinatesShareOnlySourceFingerprint"
```

Expected: rows fail only because `MortalWoundTreatmentCapabilityAuthority`/proof are absent.

- [ ] **Step 2: Implement exact current source selection and proof sealing**

Derive owner from actor role; permit canonical player/NPC source only; reject target-owned combat coordinates without prior canonical promotion; select one active current skill/capability exact/confusable uniquely; validate severity/domain/limits; seal source semantics without display text, then bind context/accepted-state/coordinates into the proof seal. Clone every collection.

```csharp
var owner = actorRole == "provider"
    ? (coordinates.ProviderKind, coordinates.ProviderId)
    : (coordinates.TargetKind, coordinates.TargetId);
```

- [ ] **Step 3: Run all current-export rows and guaranteed bundle delegation controls**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.ExportCurrent|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.AcceptedStateExport|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.IndependentControl|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.FixtureControl"
```

Expected: every selected T066-owned current-source row passes. Existing `ExportForPublication`, `T070Publication`, and `PublicationPlan_*` rows remain unselected and RED-owned by the later integration contour.

- [ ] **Step 4: Add the five-argument publication seam without a detached plan path**

Validate the plan's accepted binding against accepted state/coordinates and derive the
exact selected skill-root path. Branch on `publicationPlan.TouchedPaths` first:

- if the exact path is untouched, it must be absent from
  `OwnerCompanionAfterImages`; re-read the lease-bound current source and require
  unchanged source semantics;
- if the exact path is touched, it must occur exactly once in
  `OwnerCompanionAfterImages`; strictly parse that final after-image and never fall back
  to current state;
- touched-without-after-image, untouched-with-after-image, foreign path, malformed root,
  or duplicate semantic source is `InvalidAuthority`.

Never accept a caller `JsonObject` or proof fingerprint. Return
`mortal_wound_treatment_capability_publication_mismatch` on mechanical drift. T070 will
compare this returned proof with request `ModeAuthority` and owns end-to-end plan
construction. Add a T066-local reflection control proving the only exporter surfaces
are the four-argument current and five-argument typed-plan methods; do not construct a
detached plan to fake the touched-root semantic path.

- [ ] **Step 5: Run the T066 exporter API/source guard and neighboring controls**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.ExportCurrent|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.IndependentControl_CapabilityExportersExposeOnlyTypedCurrentAndPublicationSurfaces|FullyQualifiedName~MortalWoundTreatmentCapabilityContractTests|FullyQualifiedName~MortalWoundTreatmentRequirementAuthorityBundleTests"
```

Expected: all selected T066 rows pass. Record the exact later-owned publication rows that remain RED rather than adding a test-built plan.

- [ ] **Step 6: Commit Task 6**

```powershell
git add -- BookOfEternityClient/Services/MortalWoundTreatmentCapabilityAuthority.cs BookOfEternityClient.Tests/MortalWoundTreatmentCapabilityAuthorityTests.cs
git commit -m "feat(wounds): export treatment capability proofs (#1536)"
```

---

### Task 7: Aggregate Verification, Review, and T066 Evidence

**Files:**

- Modify: `specs/1536-complete-wound-materialization/tasks.md`
- Modify: `docs/superpowers/plans/2026-08-30-t066-mortal-wound-treatment-authority.md` only to check completed steps or correct proven plan drift

**Interfaces:**

- Consumes: all T066 commits and their fresh result artifacts.
- Produces: checked T066 task/evidence, explicit later-RED ownership map, independent review verdict, and a clean local branch commit. It does not push, open/merge a PR, or close #1536.

- [ ] **Step 1: Run complete T066 and frozen-neighbor focused controls**

Run separate bounded commands so one expected later RED cannot hide a T066 failure:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentCapabilityContractTests|FullyQualifiedName~MortalWoundTreatmentAttemptAuthorityTests|FullyQualifiedName~MortalWoundTreatmentRequirementAuthorityBundleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.ExportCurrent|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.AcceptedStateExport|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.IndependentControl|FullyQualifiedName~MortalWoundTreatmentCapabilityAuthorityTests.FixtureControl"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.AcceptedStateExport_UsesParsedSelectionAndCanonicalRootsToDeriveItsProductionBinding|FullyQualifiedName~MortalWoundRecoveryTests.AcceptedStateExport_RejectsCanonicalTreatmentAuthorityFaultBeforeComposition"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundRequirementAuthorityTests|FullyQualifiedName~MortalWoundTreatmentContractTests"
```

Expected: every selected T066/T060/T065 row passes with zero warnings, timeout, duplicate IDs, or incomplete cleanup.

- [ ] **Step 2: Run one meaningful Fast checkpoint**

Run:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Expected: no T066 or unrelated failure. Every remaining RED must map exactly to T067/T068/T069/T070; record executed/pass/fail counts, wall time, result directory, timeout, duplicate IDs, and cleanup status.

- [ ] **Step 3: Request independent exact-diff review and fix accepted findings TDD-first**

Review focus: canonical versus transient skill authority, trusted `Unsatisfied` versus
`InvalidAuthority`, candidate-first cache admission, detached witnesses, one shared T060
evaluator/ledger across course scopes, exact legacy T060 parity, first-course versus
active-course interruption reason, touched-root after-image fail-closed behavior,
combatant promotion rules, fingerprint omissions, and T067/T070 scope leakage. For
every substantiated defect add a smallest RED, implement the fix, and rerun only the
affected focused filter before aggregate controls.

- [ ] **Step 4: Audit documentation and source boundaries**

Record this exact rationale in `tasks.md`: T066 is client-owned authority/validation only; no player command, GM response field, pending/control file, projection, publication path, or afterlife contract became reachable. Therefore T072-T074 retain Mortal GM docs/examples and afterlife matrix/example/manifest files do not change.

- [ ] **Step 5: Mark only T066 complete and commit evidence**

Run:

```powershell
git diff --check
git status --short
```

Expected: no whitespace error; only intended tracked changes and untouched `.serena/` remain. Then update only T066 to `[X]`, include exact RED/GREEN/Fast/review evidence, and commit:

```powershell
git add -- specs/1536-complete-wound-materialization/tasks.md docs/superpowers/plans/2026-08-30-t066-mortal-wound-treatment-authority.md
git commit -m "docs(wounds): record T066 authority evidence (#1536)"
```
