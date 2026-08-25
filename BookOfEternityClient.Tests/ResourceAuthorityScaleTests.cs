using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceAuthorityScaleTests
{
    private const string Fingerprint =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public void ClosedScaleLimits_RemainExact()
    {
        Assert.Equal(256, ResourceMaterializationContract.MaxDefinitions);
        Assert.Equal(20_000, ResourceMaterializationContract.MaxLiveEntries);
        Assert.Equal(256, ResourceMaterializationContract.MaxCapacityTransitionsPerTurn);
        Assert.Equal(512, ResourceMaterializationContract.MaxMutationsBeforeTriggers);
        Assert.Equal(1_024, ResourceMaterializationContract.MaxTriggerNodes);
        Assert.Equal(32, ResourceMaterializationContract.MaxTriggerDepth);
        Assert.Equal(64, ResourceMaterializationContract.MaxPendingRequestsPerTurn);
    }

    [Fact]
    public void AuthorityCatalogs_DoublingPopulationStaysWithinTwoPointFiveTimesWork()
    {
        var one = BuildPopulation(128);
        var two = BuildPopulation(256);

        Assert.Equal(128, one.Definitions.Definitions.Count);
        Assert.Equal(128, one.Owners.Entries.Count);
        Assert.Equal(128, one.State.Entries.Count);
        Assert.Equal(128, one.History.Transitions.Count);
        Assert.Equal(256, two.Definitions.Definitions.Count);
        Assert.Equal(256, two.Owners.Entries.Count);
        Assert.Equal(256, two.State.Entries.Count);
        Assert.Equal(256, two.History.Transitions.Count);
        AssertNearLinear(
            "definition catalog",
            one.Meter.DefinitionWorkUnits,
            two.Meter.DefinitionWorkUnits);
        AssertNearLinear(
            "owner authority",
            one.Meter.OwnerWorkUnits,
            two.Meter.OwnerWorkUnits);
        AssertNearLinear(
            "state catalog",
            one.Meter.StateWorkUnits,
            two.Meter.StateWorkUnits);
        AssertNearLinear(
            "initial history catalog",
            one.Meter.InitialHistoryWorkUnits,
            two.Meter.InitialHistoryWorkUnits);
        AssertNearLinear(
            "state/history agreement",
            one.Meter.StateAgreementWorkUnits,
            two.Meter.StateAgreementWorkUnits);
        AssertNearLinear(
            "history working set",
            one.WorkingSet.TotalWorkUnits,
            two.WorkingSet.TotalWorkUnits);
    }

    [Fact]
    public void HistoryWorkingSet_SeedsAndFreezesOnceWithoutRebuildingPerMutation()
    {
        var population = BuildPopulation(ResourceMaterializationContract.MaxDefinitions);
        var workingSet = population.WorkingSet;

        Assert.Equal(1, workingSet.BaselineSeedCount);
        Assert.Equal(
            ResourceMaterializationContract.MaxDefinitions,
            workingSet.BaselineTransitionVisitCount);
        Assert.Equal(
            ResourceMaterializationContract.MaxDefinitions,
            workingSet.IncrementalAppendCount);
        Assert.Equal(1, workingSet.FreezeCount);
        Assert.Equal(1, workingSet.WholeHistoryRebuildCount);
        Assert.Equal(
            ResourceMaterializationContract.MaxDefinitions * 2,
            workingSet.FullHistoryValidationTransitionVisitCount);
    }

    [Fact]
    public void DefinitionMaterializationBatch_DoublingBuildsOneCatalogNearLinearly()
    {
        var one = MaterializeDefinitions(128);
        var two = MaterializeDefinitions(256);

        Assert.Equal(128, one.Catalog.Definitions.Count);
        Assert.Equal(256, two.Catalog.Definitions.Count);
        Assert.Equal(128, one.Meter.DefinitionMaterializationVisitCount);
        Assert.Equal(256, two.Meter.DefinitionMaterializationVisitCount);
        Assert.Equal(1, one.Meter.DefinitionCatalogBuildCount);
        Assert.Equal(1, two.Meter.DefinitionCatalogBuildCount);
        AssertNearLinear(
            "same-turn definition materialization",
            one.Meter.DefinitionWorkUnits,
            two.Meter.DefinitionWorkUnits);
    }

    private static ScalePopulation BuildPopulation(int count)
    {
        var meter = new ResourceAuthorityWorkMeter();
        var definitions = ParseDefinitions(count, meter);
        var states = new List<ResourceStateEntry>(count);
        var transitions = new List<ResourceTransition>(count);
        var owners = new List<ResourceOwnerExport>(count);
        for (var index = 0; index < count; index++)
        {
            var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
            var coordinate = new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Item,
                "scale_item_" + suffix,
                "scale_resource_" + suffix);
            var binding = new ResourceCapacityBinding(
                ResourceCapacityKind.InstanceFixed,
                "scale_capacity_" + suffix,
                Fingerprint);
            var snapshot = new ResourceStateSnapshot(
                Current: 1m,
                Maximum: 1m,
                binding,
                ResourceLifecycleState.Active);
            var transition = new ResourceTransition(
                TransitionId: "scale_transition_initialize_" + suffix,
                OperationId: "scale_operation_initialize_" + suffix,
                EventRef: "scale_bootstrap_" + suffix,
                OriginKind: "setting_materialization",
                OriginId: "scale_origin_" + suffix,
                Phase: ResourceMutationPhase.RegisteredSystemOutcome,
                Priority: 50,
                ExecutionSequence: index,
                Coordinate: coordinate,
                Operation: ResourceTransitionOperation.Initialize,
                RequestedAmount: 0m,
                AppliedAmount: 0m,
                Outcome: ResourceTransitionOutcome.Applied,
                CapacityDisposition: ResourceCapacityDisposition.InitializeFromDefinition,
                BeforeState: null,
                AfterState: snapshot,
                SourceEvidence: new ResourceSourceEvidence(
                    "setting_materialization",
                    "scale_origin_" + suffix,
                    Fingerprint),
                PolicyFingerprint: Fingerprint,
                ReceiptId: null,
                Turn: 1);
            transitions.Add(transition);
            states.Add(new ResourceStateEntry(
                coordinate,
                snapshot.Current,
                snapshot.Maximum,
                binding,
                snapshot.State,
                new ResourceChronology(
                    CreatedAtTurn: 1,
                    CreatedEventRef: transition.EventRef,
                    LastTransitionId: transition.TransitionId,
                    LastEventRef: transition.EventRef,
                    LastTransitionTurn: 1)));
            owners.Add(new ResourceOwnerExport(
                new ResourceOwnerKey(
                    coordinate.Realm,
                    coordinate.OwnerKind,
                    coordinate.ResourceOwnerId),
                ResourceOwnerLifecycle.Active,
                SameTurn: false,
                OwnerRef: null,
                BoundNpcId: null,
                new HashSet<string>(StringComparer.Ordinal) { coordinate.ResourceKey },
                Fingerprint));
        }

        var historyResult = ResourceHistoryState.CreateValidated(
            transitions,
            definitions,
            meter);
        Assert.True(historyResult.IsValid, Format(historyResult.Issues));
        var stateJson = new ResourceStateLedger(states).ToCanonicalJson();
        var stateResult = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions,
            allowMissingPristine: false,
            meter);
        Assert.True(stateResult.IsValid, Format(stateResult.Issues));
        var state = stateResult.Ledger!;
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state, meter));
        var authority = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            owners,
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()), meter);
        Assert.Empty(authority.Issues);

        var workingSet = new ResourceHistoryWorkingSet(historyResult.History);
        for (var index = 0; index < count; index++)
        {
            var before = transitions[index].AfterState!;
            var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
            var appended = workingSet.TryAppend(new ResourceTransition(
                TransitionId: "scale_transition_spend_" + suffix,
                OperationId: "scale_operation_spend_" + suffix,
                EventRef: "scale_turn_2_" + suffix,
                OriginKind: "action_cost",
                OriginId: "scale_action_" + suffix,
                Phase: ResourceMutationPhase.DirectCost,
                Priority: 100,
                ExecutionSequence: index,
                Coordinate: transitions[index].Coordinate,
                Operation: ResourceTransitionOperation.Spend,
                RequestedAmount: 1m,
                AppliedAmount: 1m,
                Outcome: ResourceTransitionOutcome.Applied,
                CapacityDisposition: null,
                BeforeState: before,
                AfterState: before with { Current = 0m },
                SourceEvidence: new ResourceSourceEvidence(
                    "action_cost",
                    "scale_action_" + suffix,
                    Fingerprint),
                PolicyFingerprint: Fingerprint,
                ReceiptId: null,
                Turn: 2));
            Assert.True(appended.IsValid, Format(appended.Issues));
        }

        var frozen = workingSet.Freeze(definitions);
        Assert.True(frozen.IsValid, Format(frozen.Issues));
        return new ScalePopulation(
            definitions,
            authority,
            state,
            historyResult.History,
            workingSet,
            meter);
    }

    private static ResourceDefinitionCatalog ParseDefinitions(
        int count,
        ResourceAuthorityWorkMeter meter)
    {
        var definitions = new JsonArray();
        for (var index = 0; index < count; index++)
        {
            var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
            var definition = DefinitionNode(suffix);
            definition["materialization"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitionId"] = "scale_definition_" + suffix,
                ["seal"] = "scale_seal_" + suffix,
                ["createdAtTurn"] = 1,
                ["createdEventRef"] = "scale_bootstrap_" + suffix
            };
            definitions.Add(definition);
        }

        var result = ResourceDefinitionCatalog.ParseCanonical(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitions"] = definitions
            }.ToJsonString(),
            allowMissingPristine: false,
            meter);
        Assert.True(result.IsValid, Format(result.Issues));
        return result.Catalog!;
    }

    private static DefinitionBatchPopulation MaterializeDefinitions(int count)
    {
        var emptyResult = ResourceDefinitionCatalog.ParseCanonical(
            "{\"schemaVersion\":1,\"definitions\":[]}",
            allowMissingPristine: false);
        Assert.True(emptyResult.IsValid, Format(emptyResult.Issues));
        var meter = new ResourceAuthorityWorkMeter();
        var batch = ResourceDefinitionCatalog.BeginMaterializationBatch(
            emptyResult.Catalog!,
            meter);
        for (var index = 0; index < count; index++)
        {
            var suffix = index.ToString("D4", CultureInfo.InvariantCulture);
            using var document = JsonDocument.Parse(DefinitionNode(suffix).ToJsonString());
            var result = batch.MaterializeProposal(
                document.RootElement,
                createdAtTurn: 2,
                createdEventRef: "turn_2:scale_definition:" + suffix,
                () => new ResourceDefinitionIdentity(
                    "scale_definition_" + suffix,
                    "scale_seal_" + suffix));
            Assert.True(result.IsValid, Format(result.Issues));
        }

        return new DefinitionBatchPopulation(batch.Freeze(), meter);
    }

    private static JsonObject DefinitionNode(string suffix) => new()
    {
        ["resourceKey"] = "scale_resource_" + suffix,
        ["definitionVersion"] = 1,
        ["displayName"] = "Scale resource " + suffix,
        ["numericKind"] = "integer",
        ["unit"] = "point",
        ["quantum"] = 1,
        ["minimumPolicy"] = new JsonObject
        {
            ["kind"] = "definition_fixed",
            ["value"] = 0
        },
        ["capacityPolicy"] = new JsonObject
        {
            ["kind"] = "instance_fixed"
        },
        ["initializationPolicy"] = new JsonObject
        {
            ["kind"] = "maximum"
        },
        ["allowedOwnerKinds"] = new JsonArray("item"),
        ["allowedOperations"] = new JsonArray("spend", "gain"),
        ["defaultFloorPolicy"] = "reject_below_minimum",
        ["defaultCapPolicy"] = "clamp_to_maximum",
        ["visibility"] = "owner_visible"
    };

    private static string Format(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue =>
            $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}"));

    private static void AssertNearLinear(string subsystem, long one, long two)
    {
        Assert.True(one > 0, $"Expected measured {subsystem} work.");
        Assert.True(
            two <= one * 2.5,
            $"Expected near-linear {subsystem} work, but {one} units became {two}.");
    }

    private sealed record ScalePopulation(
        ResourceDefinitionCatalog Definitions,
        ResourceOwnerAuthority Owners,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceHistoryWorkingSet WorkingSet,
        ResourceAuthorityWorkMeter Meter);

    private sealed record DefinitionBatchPopulation(
        ResourceDefinitionCatalog Catalog,
        ResourceAuthorityWorkMeter Meter);
}
