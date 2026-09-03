using System.Collections;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    private const string DeferredItemConsumptionCode =
        "mortal_wound_treatment_publication_item_consumption_unsupported";
    private const string TransactionTokenMismatchCode =
        "mortal_wound_treatment_publication_transaction_token_mismatch";
    private const string TransactionTokenReplayedCode =
        "mortal_wound_treatment_publication_transaction_token_replayed";
    private const string TransactionStaleCode =
        "mortal_wound_treatment_publication_transaction_stale";
    private const string TransactionReservationChangedCode =
        "mortal_wound_treatment_publication_transaction_reservation_changed";
    private const string PublicationBlockedCode =
        "mortal_wound_treatment_publication_compensation_restart_required";
    private const string TerminalReleaseFailureCode =
        "mortal_wound_treatment_publication_terminal_release_failed";
    private const string PublishedAgreementChangedCode =
        "mortal_wound_treatment_publication_published_agreement_changed";
    private const string ItemBaselineChangedCode =
        "mortal_wound_treatment_publication_item_baseline_changed";

    private static readonly HashSet<string> TreatmentItemAllowedResponsePropertyNames =
        new(
            new[]
            {
                nameof(GameResponse.ActiveSkillChanges),
                nameof(GameResponse.RemoveActiveSkills),
                nameof(GameResponse.PassiveSkillChanges),
                nameof(GameResponse.RemovePassiveSkills),
                nameof(GameResponse.NPCActiveSkillChanges),
                nameof(GameResponse.NPCPassiveSkillChanges),
                nameof(GameResponse.UpdateInventory),
                nameof(GameResponse.MoveInventoryItems),
                nameof(GameResponse.RemoveInventoryItems),
                nameof(GameResponse.NPCInventoryAdds),
                nameof(GameResponse.NPCInventoryUpdates),
                nameof(GameResponse.NPCInventoryRemovals),
                nameof(GameResponse.NPCEquipmentChanges)
            },
            StringComparer.Ordinal);

    private static readonly string[] TreatmentItemResponsePropertyNames =
    {
        nameof(GameResponse.UpdateInventory),
        nameof(GameResponse.MoveInventoryItems),
        nameof(GameResponse.RemoveInventoryItems),
        nameof(GameResponse.NPCInventoryAdds),
        nameof(GameResponse.NPCInventoryUpdates),
        nameof(GameResponse.NPCInventoryRemovals),
        nameof(GameResponse.NPCEquipmentChanges)
    };

    [Fact]
    public void GuaranteedResourceQuantity_PersistedConfirmedPublicationSpendsOnceAndCommitsAtFullPipelineEnd()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "confirmed_spend_once");
        var beforeComposition = CaptureResolverFixtureTree(fixture.Root);
        var itemCarrierBefore = CaptureItemCarrierBytes(fixture);
        var definitionsBefore = ReadCanonicalBytes(
            fixture,
            ResourceMaterializationContract.DefinitionsPath);
        var historyBefore = ReadResourceHistory(fixture);

        var plan = ComposeResourcePublication(fixture, flow);

        AssertResolverFixtureTreeUnchanged(fixture.Root, beforeComposition);
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResourcePlan(
            plan,
            expectedCurrent: 8,
            historyBefore.Transitions.Count,
            expectedSpendQuantities: new decimal[] { 2 });
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            Assert.Same(plan, publication.Plan);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
                fixture.FileSystem,
                fixture.Lease));
            Assert.Equal(8, ReadPlayerEnergy(fixture));
            AssertItemCarrierBytesEqual(fixture, itemCarrierBefore);
            Assert.Equal(definitionsBefore, ReadCanonicalBytes(
                fixture,
                ResourceMaterializationContract.DefinitionsPath));
            Assert.Equal(
                historyBefore.Transitions.Count + 1,
                ReadResourceHistory(fixture).Transitions.Count);
            Assert.Equal(
                "held",
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request)
                    .ResourceAuthority.ReservationDisposition);

            publication.CompleteAtFullPipelineEnd();
        }

        fixture.PrepareFreshSnapshot("resource_publication_history_replay");
        fixture.RestartForReplay();
        var recovered = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(fixture));
        Assert.True(recovered.IsValid, DescribeIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        Assert.Single(recovered.FinalizedRequests);
        var replay = ProbePublishedTreatment(fixture, flow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(
            replay,
            "Status")));
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        Assert.Equal(
            historyBefore.Transitions.Count + 1,
            ReadResourceHistory(fixture).Transitions.Count);
    }

    [Theory]
    [InlineData("provisional")]
    [InlineData("released")]
    [InlineData("changed_live_quantity")]
    [InlineData("changed_finalization")]
    public void GuaranteedResourceQuantity_ProvisionalOrStaleHoldRejectsBeforeCacheAndWrite(
        string axis)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_" + axis,
            scenario.RouteId);
        TreatmentFlow supplied = flow;
        if (string.Equals(axis, "released", StringComparison.Ordinal))
        {
            var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                flow.AcceptedState);
            var released = acceptedState.ReleaseTreatmentResources(
                ResourceLifecycleCapability(),
                new[] { Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request) },
                "validation_failed");
            Assert.True(released.IsValid, DescribeIssues(released.Issues));
            Assert.Equal(1, released.ChangedCount);
        }
        else if (string.Equals(axis, "changed_live_quantity", StringComparison.Ordinal))
        {
            supplied = PersistAndRehydrateResourcePublication(
                fixture,
                scenario,
                axis);
            WriteCanonicalPlayerEnergyAuthority(fixture.FileSystem, current: 9);
        }
        else if (string.Equals(axis, "changed_finalization", StringComparison.Ordinal))
        {
            var confirmed = PersistAndRehydrateResourcePublication(
                fixture,
                scenario,
                axis);
            var foreign = ResolveCurrentTreatment(
                fixture,
                "guaranteed",
                scenario.OperationKey + "_foreign_finalization",
                scenario.RouteId);
            var changedFinalization = MortalWoundTreatmentResourceComposer.Finalize(
                Assert.IsType<MortalWoundTreatmentResolution>(foreign.Resolution));
            Assert.True(
                changedFinalization.IsValid,
                DescribeIssues(changedFinalization.Issues));
            Assert.Equal("consume", changedFinalization.Finalization!.Disposition);
            supplied = new TreatmentFlow(
                confirmed.AcceptedState,
                confirmed.Request,
                foreign.Resolution,
                confirmed.Before,
                confirmed.History);
        }

        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var result = ComposeResourcePublicationResult(fixture, supplied);

        Assert.False(result.IsValid);
        Assert.Null(result.Plan);
        var expectedCode = axis switch
        {
            "provisional" =>
                "mortal_wound_treatment_publication_resource_reservation_not_confirmed",
            "released" =>
                "mortal_wound_treatment_publication_resource_reservation_missing",
            "changed_live_quantity" =>
                "mortal_wound_treatment_publication_resource_state_changed",
            "changed_finalization" =>
                "mortal_wound_treatment_publication_resource_finalization_mismatch",
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, null)
        };
        Assert.Equal(expectedCode, Assert.Single(result.Issues).Code);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Fact]
    public void GuaranteedMixedItemAndResourceConsumption_PublishesAtomicallyOnce()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true,
            reusableItemCount: 2);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "mixed_item_resource");
        var itemBytesBefore = CaptureItemCarrierBytes(fixture);
        var resourceBytesBefore = CaptureResourceBytes(fixture);
        var identityBefore = ReadItemIdentityEntry(
            fixture,
            "reusable_field_kit");
        var identityTransitionCountBefore =
            ReadIdentityTransitionCount(identityBefore);
        var resourceTransitionCountBefore =
            ReadResourceHistory(fixture).Transitions.Count;
        var composed = ComposeResourcePublicationResult(fixture, flow);

        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
        Assert.Equal(1, ReadPlanNpcItemCount(plan, "reusable_field_kit"));
        Assert.Equal(
            8m,
            plan.StateAfterImage["entries"]!.AsArray().OfType<JsonObject>()
                .Single(entry => entry["resourceKey"]!.GetValue<string>() == "energy")
                ["current"]!.GetValue<decimal>());
        AssertPlanContainsExactPartialItemIdentity(
            plan,
            identityBefore,
            "reusable_field_kit",
            identityTransitionCountBefore);
        Assert.Equal(2, fixture.ReadNpcItemCount("reusable_field_kit"));
        Assert.Equal(10, ReadPlayerEnergy(fixture));
        AssertItemCarrierBytesEqual(fixture, itemBytesBefore);
        AssertResourceBytesEqual(fixture, resourceBytesBefore);
        Assert.DoesNotContain(
            composed.Issues,
            issue => issue.Code == DeferredItemConsumptionCode);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            Assert.Equal(1, fixture.ReadNpcItemCount("reusable_field_kit"));
            Assert.Equal(8, ReadPlayerEnergy(fixture));
            Assert.Equal(
                identityTransitionCountBefore + 1,
                ReadIdentityTransitionCount(ReadItemIdentityEntry(
                    fixture,
                    "reusable_field_kit")));
            Assert.Equal(
                resourceTransitionCountBefore + 1,
                ReadResourceHistory(fixture).Transitions.Count);
            publication.CompleteAtFullPipelineEnd();
        }
        var itemBytesAfterPublication = CaptureItemCarrierBytes(fixture);
        var resourceBytesAfterPublication = CaptureResourceBytes(fixture);
        var identityTransitionsAfterPublication = ReadIdentityTransitionCount(
            ReadItemIdentityEntry(fixture, "reusable_field_kit"));
        var resourceTransitionsAfterPublication =
            ReadResourceHistory(fixture).Transitions.Count;
        fixture.PrepareFreshSnapshot("mixed_item_replay");
        fixture.RestartForReplay();
        var replay = ProbePublishedTreatment(fixture, flow.Request);
        Assert.Equal(
            "ExactReplay",
            Convert.ToString(ReadRequiredProperty(replay, "Status")));
        Assert.Equal(1, fixture.ReadNpcItemCount("reusable_field_kit"));
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        AssertItemCarrierBytesEqual(fixture, itemBytesAfterPublication);
        AssertResourceBytesEqual(fixture, resourceBytesAfterPublication);
        Assert.Equal(
            identityTransitionsAfterPublication,
            ReadIdentityTransitionCount(ReadItemIdentityEntry(
                fixture,
                "reusable_field_kit")));
        Assert.Equal(
            resourceTransitionsAfterPublication,
            ReadResourceHistory(fixture).Transitions.Count);
    }

    [Fact]
    public void GuaranteedItemConsumption_ItemCapacityUsesCommonFinalizationOrdinal()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true,
            reusableItemCount: 4);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        fixture.SetCanonicalReusableItemDurabilityForResourcePublicationTest(
            current: 8,
            maximum: 8);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "item_capacity_common_ordinal");

        var composed = ComposeResourcePublicationResult(fixture, flow);

        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
        var publication = Assert.IsType<
            MortalWoundTreatmentResourcePublicationAuthority>(
            plan.TreatmentResourcePublicationAuthority);
        var itemPublication = Assert.IsType<
            MortalWoundTreatmentItemPublicationAuthority>(
            publication.ItemPublicationAuthority);
        var expectedOrdinal = publication.Finalization.Consumptions
            .Select((consumption, index) => (consumption, index))
            .Single(row => string.Equals(
                row.consumption.Kind,
                "item_quantity",
                StringComparison.Ordinal))
            .index + 1;
        Assert.Equal(2, expectedOrdinal);
        var inputField = typeof(MortalWoundTreatmentItemPublicationAuthority)
            .GetField(
                "_consumptionInput",
                BindingFlags.Instance | BindingFlags.NonPublic);
        var input = Assert.IsType<MortalItemConsumptionPlanningInput>(
            Assert.IsAssignableFrom<FieldInfo>(inputField).GetValue(itemPublication));
        var command = Assert.Single(input.Commands);
        Assert.Equal(expectedOrdinal, command.FinalizationOrdinal);
        var capacity = Assert.Single(itemPublication.CapacityTransitions);
        Assert.Contains(
            $":{expectedOrdinal:D4}:",
            capacity.EventRef,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GuaranteedItemConsumption_RepeatedItemCapacityTransitionsChainAcrossCommonOrdinalGap()
    {
        var scenario = CreateRepeatedItemCapacityPublicationScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        fixture.SetCanonicalReusableItemDurabilityForResourcePublicationTest(
            current: 8,
            maximum: 8);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "repeated_item_capacity_chain");

        var composed = ComposeResourcePublicationResult(fixture, flow);

        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
        var publication = Assert.IsType<
            MortalWoundTreatmentResourcePublicationAuthority>(
            plan.TreatmentResourcePublicationAuthority);
        var itemPublication = Assert.IsType<
            MortalWoundTreatmentItemPublicationAuthority>(
            publication.ItemPublicationAuthority);
        var capacities = itemPublication.CapacityTransitions;
        Assert.Equal(2, capacities.Count);
        Assert.Contains(":0001:", capacities[0].EventRef, StringComparison.Ordinal);
        Assert.Contains(":0003:", capacities[1].EventRef, StringComparison.Ordinal);
        Assert.Equal(capacities[0].Coordinate, capacities[1].Coordinate);
        Assert.Equal(6m, capacities[0].ResolvedCapacity!.Maximum);
        Assert.Equal(4m, capacities[1].ResolvedCapacity!.Maximum);
        var durability = Assert.Single(
            plan.StateAfterImage["entries"]!.AsArray().OfType<JsonObject>(),
            entry => entry["resourceKey"]?.GetValue<string>() == "durability" &&
                     entry["resourceOwnerId"]?.GetValue<string>() ==
                     "reusable_field_kit");
        Assert.Equal(4m, durability["current"]!.GetValue<decimal>());
        Assert.Equal(4m, durability["maximum"]!.GetValue<decimal>());
    }

    [Fact]
    public void GuaranteedItemConsumption_FinalOwnerPreservesSurvivingSameTurnItemRefsExactly()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var response = SeedFinalBaselineProductionInputs(fixture);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "same_turn_owner_successor");

        var plan = ComposeResourcePublication(fixture, flow, response);
        var itemPublication = Assert.IsType<
            MortalWoundTreatmentItemPublicationAuthority>(
            Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(
                    plan.TreatmentResourcePublicationAuthority)
                .ItemPublicationAuthority);
        var baselineOwnerField = typeof(MortalWoundTreatmentItemPublicationAuthority)
            .GetField(
                "_baselineOwnerAuthority",
                BindingFlags.Instance | BindingFlags.NonPublic);
        var baselineOwnerAuthority = Assert.IsType<ResourceOwnerAuthority>(
            Assert.IsAssignableFrom<FieldInfo>(baselineOwnerField)
                .GetValue(itemPublication));
        var baselineOwners = baselineOwnerAuthority.ExportInput();
        var finalOwners = plan.OwnerAuthority.ExportInput();

        Assert.NotEmpty(baselineOwners.SameTurnOwners);
        AssertExactOwnerExports(
            baselineOwners.SameTurnOwners,
            finalOwners.SameTurnOwners);
        Assert.All(itemPublication.TerminalOwners, terminal =>
        {
            Assert.DoesNotContain(finalOwners.PreTurnOwners, owner =>
                owner.Key == terminal);
            Assert.DoesNotContain(finalOwners.SameTurnOwners, owner =>
                owner.Key == terminal);
            Assert.Contains(terminal, finalOwners.HistoricalOwners);
        });

        var sameTurnTerminal = baselineOwners.SameTurnOwners[0].Key;
        var sameTurnTerminalSuccessor =
            MortalWoundTreatmentItemOwnerSuccessor.Compose(
                baselineOwnerAuthority,
                new[] { sameTurnTerminal });
        Assert.True(
            sameTurnTerminalSuccessor.IsValid,
            DescribeIssues(sameTurnTerminalSuccessor.Issues));
        var terminalSuccessorOwners = sameTurnTerminalSuccessor.Authority!.ExportInput();
        Assert.DoesNotContain(terminalSuccessorOwners.SameTurnOwners, owner =>
            owner.Key == sameTurnTerminal);
        Assert.Contains(sameTurnTerminal, terminalSuccessorOwners.HistoricalOwners);
        AssertExactOwnerExports(
            baselineOwners.SameTurnOwners
                .Where(owner => owner.Key != sameTurnTerminal)
                .ToArray(),
            terminalSuccessorOwners.SameTurnOwners);
    }

    [Fact]
    public void GuaranteedItemConsumption_SealRejectsChangedBaselinePayloadWithOriginalFingerprint()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true,
            reusableItemCount: 2);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "same_fingerprint_payload_adversary");
        var plan = ComposeResourcePublication(fixture, flow);
        var itemPublication = Assert.IsType<
            MortalWoundTreatmentItemPublicationAuthority>(
            Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(
                    plan.TreatmentResourcePublicationAuthority)
                .ItemPublicationAuthority);
        Assert.True(itemPublication.HasValidSeal());
        var baselineField = typeof(MortalWoundTreatmentItemPublicationAuthority)
            .GetField("_baseline", BindingFlags.Instance | BindingFlags.NonPublic);
        var baseline = Assert.IsType<MortalItemPublicationBaselineResult>(
            Assert.IsAssignableFrom<FieldInfo>(baselineField)
                .GetValue(itemPublication));
        var npc = Assert.IsType<JsonObject>(baseline.FinalCarrierRoots[
            NpcCoreChangesContract.NpcCorePath]);
        npc["forgedPayloadWithOriginalFingerprint"] = true;

        Assert.False(itemPublication.HasValidSeal());
    }

    [Fact]
    public void GuaranteedItemConsumption_NpcProofRejectsMismatchedSkillIntermediate()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true,
            reusableItemCount: 2);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "mismatched_skill_intermediate");
        var plan = ComposeResourcePublication(fixture, flow);
        var itemPublication = Assert.IsType<
            MortalWoundTreatmentItemPublicationAuthority>(
            Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(
                    plan.TreatmentResourcePublicationAuthority)
                .ItemPublicationAuthority);
        var inputField = typeof(MortalWoundTreatmentItemPublicationAuthority)
            .GetField("_consumptionInput", BindingFlags.Instance | BindingFlags.NonPublic);
        var input = Assert.IsType<MortalItemConsumptionPlanningInput>(
            Assert.IsAssignableFrom<FieldInfo>(inputField)
                .GetValue(itemPublication));
        var forgedSkillIntermediate = input.CarrierRoots.NpcCore!
            .DeepClone()
            .AsObject();
        forgedSkillIntermediate["forgedSkillIntermediate"] = true;
        var finalNpc = Assert.IsType<JsonObject>(
            itemPublication.PublicationAfterImages[NpcCoreChangesContract.NpcCorePath]);

        Assert.False(itemPublication.ProvesNpcRootTransition(
            forgedSkillIntermediate,
            finalNpc));
    }

    [Fact]
    public void GuaranteedItemConsumption_ClosedItemEnvelopePreservesEverySupportedFieldAndRejectsEveryOtherField()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "closed_item_envelope");

        var response = CreateAllAllowedTreatmentItemResponseFieldsPresentEmpty();
        Assert.Equal(13, TreatmentItemAllowedResponsePropertyNames.Count);
        AssertGameResponseHasExactlyAllowedPresentEmpty(response);
        var responseBefore = CaptureGameResponsePropertyGraph(response);
        var composed = ComposeResourcePublicationResult(fixture, flow, response);

        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        Assert.DoesNotContain(composed.Issues, issue =>
            issue.Code == DeferredItemConsumptionCode);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
        Assert.Equal(responseBefore, CaptureGameResponsePropertyGraph(response));
        var sealedPublication = ReadSealedTreatmentPublication(plan);
        Assert.Equal(
            ComputeExpectedTreatmentSkillEnvelopeFingerprint(response),
            sealedPublication.SkillEnvelopeFingerprint);
        var emptyEnvelope = sealedPublication.ItemEnvelope;
        AssertTreatmentItemEnvelopeHasExactNullShape(
            emptyEnvelope,
            expectPresentEmpty: true);

        var sealedPlanFingerprint = plan.PreparedPlanFingerprint;
        var sealedOuterFingerprint = sealedPublication.OuterFingerprint;
        var sealedItemAuthorityFingerprint = sealedPublication.ItemAuthorityFingerprint;
        var sealedSkillAuthorityFingerprint =
            sealedPublication.SkillAuthorityFingerprint;
        var sealedSkillFingerprint = sealedPublication.SkillEnvelopeFingerprint;
        var sealedItemFingerprint = sealedPublication.ItemEnvelopeFingerprint;
        var sealedBaselineFingerprint = sealedPublication.Baseline.Fingerprint;
        foreach (var property in typeof(GameResponse).GetProperties(
                     BindingFlags.Instance | BindingFlags.Public)
                 .Where(property => TreatmentItemAllowedResponsePropertyNames.Contains(
                     property.Name)))
        {
            property.SetValue(response, null);
        }
        Assert.Equal(sealedPlanFingerprint, plan.PreparedPlanFingerprint);
        var detachedPublication = ReadSealedTreatmentPublication(plan);
        Assert.Same(sealedPublication.ItemAuthority, detachedPublication.ItemAuthority);
        Assert.Equal(sealedOuterFingerprint, detachedPublication.OuterFingerprint);
        Assert.Equal(
            sealedItemAuthorityFingerprint,
            detachedPublication.ItemAuthorityFingerprint);
        Assert.Equal(
            sealedSkillAuthorityFingerprint,
            detachedPublication.SkillAuthorityFingerprint);
        Assert.Equal(
            sealedSkillFingerprint,
            detachedPublication.SkillEnvelopeFingerprint);
        Assert.Equal(
            sealedItemFingerprint,
            detachedPublication.ItemEnvelopeFingerprint);
        AssertTreatmentItemEnvelopeHasExactNullShape(
            emptyEnvelope,
            expectPresentEmpty: true);

        using var nullSkillFixture = AcceptedStateFixture.Create(scenario);
        nullSkillFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var nullSkillFlow = PersistAndRehydrateResourcePublication(
            nullSkillFixture,
            scenario,
            "closed_item_envelope");
        var nullSkillResponse = CreateAllAllowedTreatmentItemResponseFieldsPresentEmpty();
        SetTreatmentSkillResponseFields(nullSkillResponse, value: null);
        var nullSkillPlan = ComposeResourcePublication(
            nullSkillFixture,
            nullSkillFlow,
            nullSkillResponse);
        var nullSkillPublication = ReadSealedTreatmentPublication(nullSkillPlan);
        Assert.Equal(
            ComputeExpectedTreatmentSkillEnvelopeFingerprint(nullSkillResponse),
            nullSkillPublication.SkillEnvelopeFingerprint);
        Assert.Equal(sealedItemFingerprint, nullSkillPublication.ItemEnvelopeFingerprint);
        Assert.Equal(sealedBaselineFingerprint, nullSkillPublication.Baseline.Fingerprint);
        Assert.NotEqual(sealedSkillFingerprint, nullSkillPublication.SkillEnvelopeFingerprint);
        Assert.NotEqual(
            sealedSkillAuthorityFingerprint,
            nullSkillPublication.SkillAuthorityFingerprint);
        Assert.NotEqual(
            sealedItemAuthorityFingerprint,
            nullSkillPublication.ItemAuthorityFingerprint);
        Assert.NotEqual(sealedOuterFingerprint, nullSkillPublication.OuterFingerprint);
        Assert.NotEqual(sealedPlanFingerprint, nullSkillPlan.PreparedPlanFingerprint);

        using var nullItemFixture = AcceptedStateFixture.Create(scenario);
        nullItemFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var nullItemFlow = PersistAndRehydrateResourcePublication(
            nullItemFixture,
            scenario,
            "closed_item_envelope");
        var nullItemResponse = CreateAllAllowedTreatmentItemResponseFieldsPresentEmpty();
        SetTreatmentItemResponseFields(nullItemResponse, value: null);
        var nullItemPlan = ComposeResourcePublication(
            nullItemFixture,
            nullItemFlow,
            nullItemResponse);
        var nullItemPublication = ReadSealedTreatmentPublication(nullItemPlan);
        Assert.Equal(
            ComputeExpectedTreatmentSkillEnvelopeFingerprint(nullItemResponse),
            nullItemPublication.SkillEnvelopeFingerprint);
        var nullEnvelope = nullItemPublication.ItemEnvelope;
        AssertTreatmentItemEnvelopeHasExactNullShape(
            nullEnvelope,
            expectPresentEmpty: false);
        Assert.Equal(sealedSkillFingerprint, nullItemPublication.SkillEnvelopeFingerprint);
        Assert.Equal(
            sealedSkillAuthorityFingerprint,
            nullItemPublication.SkillAuthorityFingerprint);
        Assert.NotEqual(sealedItemFingerprint, nullItemPublication.ItemEnvelopeFingerprint);
        Assert.NotEqual(sealedBaselineFingerprint, nullItemPublication.Baseline.Fingerprint);
        Assert.NotEqual(
            sealedItemAuthorityFingerprint,
            nullItemPublication.ItemAuthorityFingerprint);
        Assert.NotEqual(sealedOuterFingerprint, nullItemPublication.OuterFingerprint);
        Assert.NotEqual(sealedPlanFingerprint, nullItemPlan.PreparedPlanFingerprint);

        foreach (var propertyName in TreatmentItemResponsePropertyNames)
        {
            using var fieldFixture = AcceptedStateFixture.Create(scenario);
            fieldFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
            var fieldFlow = PersistAndRehydrateResourcePublication(
                fieldFixture,
                scenario,
                "closed_item_envelope");
            var fieldResponse = CreateAllAllowedTreatmentItemResponseFieldsPresentEmpty();
            var fieldProperty = Assert.Single(
                typeof(GameResponse).GetProperties(BindingFlags.Instance | BindingFlags.Public),
                property => string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.Ordinal));
            fieldProperty.SetValue(fieldResponse, null);

            var fieldPlan = ComposeResourcePublication(
                fieldFixture,
                fieldFlow,
                fieldResponse);
            var fieldPublication = ReadSealedTreatmentPublication(fieldPlan);

            AssertTreatmentItemEnvelopeMatchesResponse(
                fieldPublication.ItemEnvelope,
                fieldResponse);
            Assert.Equal(
                ComputeExpectedTreatmentSkillEnvelopeFingerprint(fieldResponse),
                fieldPublication.SkillEnvelopeFingerprint);
            Assert.Equal(
                sealedSkillFingerprint,
                fieldPublication.SkillEnvelopeFingerprint);
            Assert.Equal(
                sealedSkillAuthorityFingerprint,
                fieldPublication.SkillAuthorityFingerprint);
            Assert.NotEqual(
                sealedItemFingerprint,
                fieldPublication.ItemEnvelopeFingerprint);
            Assert.NotEqual(
                sealedBaselineFingerprint,
                fieldPublication.Baseline.Fingerprint);
            Assert.NotEqual(
                sealedItemAuthorityFingerprint,
                fieldPublication.ItemAuthorityFingerprint);
            Assert.NotEqual(
                sealedOuterFingerprint,
                fieldPublication.OuterFingerprint);
            Assert.NotEqual(
                sealedPlanFingerprint,
                fieldPlan.PreparedPlanFingerprint);
        }
    }

    public static IEnumerable<object[]> UnsupportedTreatmentItemEnvelopeProperties() =>
        typeof(GameResponse)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property =>
                property.CanRead &&
                property.GetIndexParameters().Length == 0 &&
                !TreatmentItemAllowedResponsePropertyNames.Contains(property.Name))
            .OrderBy(static property => property.Name, StringComparer.Ordinal)
            .Select(static property => new object[] { property.Name });

    public static IEnumerable<object[]> AllowedTreatmentItemEnvelopeProperties() =>
        TreatmentItemResponsePropertyNames.Select(static propertyName =>
            new object[] { propertyName });

    [Theory]
    [MemberData(nameof(UnsupportedTreatmentItemEnvelopeProperties))]
    public void GuaranteedItemConsumption_ClosedItemEnvelopeRejectsEveryOtherNonNullGameResponseProperty(
        string propertyName)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "closed_item_envelope_reject_" +
            B4Fingerprint(propertyName)[7..19]);
        var response = new GameResponse();
        var property = Assert.Single(
            typeof(GameResponse).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            candidate => string.Equals(
                candidate.Name,
                propertyName,
                StringComparison.Ordinal));
        Assert.True(property.CanWrite);
        property.SetValue(response, CreateNonNullGameResponsePropertyValue(property));
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);

        var composed = ComposeResourcePublicationResult(fixture, flow, response);

        Assert.False(composed.IsValid);
        Assert.Null(composed.Plan);
        var issue = Assert.Single(composed.Issues);
        Assert.Equal(
            "mortal_wound_treatment_publication_response_unsupported",
            issue.Code);
        Assert.Equal("treatmentPublication.response." + propertyName, issue.FilePath);
        Assert.Equal(propertyName, issue.Actual);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Theory]
    [MemberData(nameof(AllowedTreatmentItemEnvelopeProperties))]
    public void GuaranteedTreatmentPublication_ChangedAllowedItemEnvelopeCannotReuseExactCachedPlan(
        string propertyName)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "changed_item_envelope_" + propertyName);
        var originalPlan = ComposeResourcePublication(fixture, flow);
        var changedResponse = new GameResponse();
        var property = Assert.Single(
            typeof(GameResponse).GetProperties(
                BindingFlags.Instance | BindingFlags.Public),
            candidate => string.Equals(
                candidate.Name,
                propertyName,
                StringComparison.Ordinal));
        Assert.Equal(typeof(JsonElement[]), property.PropertyType);
        property.SetValue(changedResponse, Array.Empty<JsonElement>());

        var changed = ComposeResourcePublicationResult(
            fixture,
            flow,
            changedResponse);

        Assert.False(changed.IsValid);
        Assert.Null(changed.Plan);
        var issue = Assert.Single(changed.Issues);
        Assert.Equal("mortal_wound_treatment_publication_conflict", issue.Code);
        Assert.Same(originalPlan, PeekCachedPlan(fixture));
    }

    [Fact]
    public void TreatmentItemProjectionRoots_LegacyVehicleArrayPassesTreatmentCacheConfirmation()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "legacy_vehicle_array_projection");
        WriteTreatmentProjectionRootBytes(
            fixture,
            StorageTransportMoveService.VehiclesPath,
            Encoding.UTF8.GetBytes("[]"));

        var composed = ComposeResourcePublicationResult(fixture, flow);

        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        Assert.NotNull(composed.Plan);
        Assert.True(MortalItemAcceptedTurnAuthority.HasValidatedItems(
            fixture.FileSystem,
            fixture.Lease));
    }

    [Fact]
    public void SkillOnlyItemBootstrap_ExistingNonExactCacheRejectsWithoutMutationAndNoCacheBootstrapSucceeds()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true);

        using (var noCacheFixture = AcceptedStateFixture.Create(scenario))
        {
            noCacheFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
            var noCacheFlow = PersistAndRehydrateResourcePublication(
                noCacheFixture,
                scenario,
                "skill_only_no_item_cache");
            Assert.False(MortalItemAcceptedTurnAuthority.HasValidatedItems(
                noCacheFixture.FileSystem,
                noCacheFixture.Lease));

            var bootstrap = ComposeResourcePublicationResult(
                noCacheFixture,
                noCacheFlow);

            Assert.True(bootstrap.IsValid, DescribeIssues(bootstrap.Issues));
            Assert.NotNull(bootstrap.Plan);
            Assert.True(MortalItemAcceptedTurnAuthority.HasValidatedItems(
                noCacheFixture.FileSystem,
                noCacheFixture.Lease));
        }

        using var existingCacheFixture = AcceptedStateFixture.Create(scenario);
        existingCacheFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var existingCacheFlow = PersistAndRehydrateResourcePublication(
            existingCacheFixture,
            scenario,
            "skill_only_non_exact_item_cache");
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            existingCacheFlow.AcceptedState);
        var binding = acceptedState.Binding;
        RegisterNonExactOrdinaryItemCache(
            existingCacheFixture,
            binding);
        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            existingCacheFixture.FileSystem,
            existingCacheFixture.Lease,
            binding.SessionId,
            binding.SnapshotToken,
            binding.Turn,
            out var before));

        var rejected = ComposeResourcePublicationResult(
            existingCacheFixture,
            existingCacheFlow);

        Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
            existingCacheFixture.FileSystem,
            existingCacheFixture.Lease,
            binding.SessionId,
            binding.SnapshotToken,
            binding.Turn,
            out var after));
        Assert.Equal(before.ProofFingerprint, after.ProofFingerprint);
        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Plan);
        var issue = Assert.Single(rejected.Issues);
        Assert.Equal(
            "mortal_wound_treatment_publication_item_authority_changed",
            issue.Code);
    }

    [Fact]
    public void AcceptedItemSnapshot_OneUseProofBindsFinalBaselinePlannerInputsOutputsAndTransformIds()
    {
        var repositoryRoot = FindRepositoryRootForB4SourceGuard();
        var authoritySource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "BookOfEternityClient",
                "Services",
                "MortalItemAcceptedEffectSourceAuthority.cs")));
        var registrySource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "BookOfEternityClient",
                "Services",
                "AcceptedTurnAuthorityRegistry.cs")));
        var composerSource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "BookOfEternityClient",
                "Services",
                "AcceptedMechanicsWoundCommonInputComposer.cs")));
        var productionBinding = string.Join(
            Environment.NewLine,
            authoritySource,
            registrySource,
            composerSource);
        const string snapshotHeader =
            "internal sealed class MortalItemAcceptedTurnNormalizationSnapshot";
        var snapshotHeaderIndex = authoritySource.IndexOf(
            snapshotHeader,
            StringComparison.Ordinal);
        Assert.True(
            snapshotHeaderIndex >= 0,
            $"Missing production snapshot type '{snapshotHeader}'.");
        var cacheBinding = ExtractB4BracedBlockSource(
            authoritySource,
            snapshotHeaderIndex + snapshotHeader.Length,
            snapshotHeader);
        var requiredBindings = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["closed item envelope"] =
                @"(?i)\b(itemCommandEnvelope|itemEnvelope)\b",
            ["NPC-core authority"] = @"(?i)\bnpcCoreAuthority\b",
            ["NPC-trade pending before-image"] = @"(?i)\bnpcTradePending\b",
            ["training pending before-image"] = @"(?i)\btrainingPending\b",
            ["authenticated NPC-trade disposition"] =
                @"(?i)\bnpcTradeDisposition\b",
            ["item-phase after-images"] = @"(?i)\bitemPhase\w*\b",
            ["final carrier roots"] =
                @"(?i)\bfinal(Carrier|Projection)Roots\b",
            ["applied transform IDs"] = @"(?i)\bappliedTransformIds\b",
            ["final baseline fingerprint"] =
                @"(?i)\bfinalBaselineFingerprint\b"
        };
        var missingBindings = requiredBindings
            .Where(pair => !System.Text.RegularExpressions.Regex.IsMatch(
                cacheBinding,
                pair.Value))
            .Select(static pair => pair.Key)
            .ToList();
        if (!System.Text.RegularExpressions.Regex.IsMatch(
                productionBinding,
                @"\bMortalItemPublicationBaselinePlanner\s*\.\s*Project\s*\("))
        {
            missingBindings.Insert(0, "planner invocation");
        }
        Assert.True(
            missingBindings.Count == 0,
            "The one-use item authority is missing final-baseline bindings: " +
            string.Join(", ", missingBindings));

        var proof = ExtractB4MethodSource(
            authoritySource,
            "private string ComputeProofFingerprint(");
        foreach (var requiredProof in new[]
                 {
                     @"(?i)\b(itemCommandEnvelope|itemEnvelope)\b",
                     @"(?i)\bnpcCoreAuthority\b",
                     @"(?i)\bnpcTradePending\b",
                     @"(?i)\btrainingPending\b",
                     @"(?i)\bnpcTradeDisposition\b",
                     @"(?i)\bitemPhase\w*\b",
                     @"(?i)\bfinal(Carrier|Projection)Roots\b",
                     @"(?i)\bappliedTransformIds\b",
                     @"(?i)\bfinalBaselineFingerprint\b"
                 })
        {
            Assert.Matches(requiredProof, proof);
        }
        var currentAgreement = ExtractB4MethodSource(
            authoritySource,
            "private bool CurrentCacheStateAgrees(");
        Assert.Contains(
            "MatchesExactCacheState(",
            currentAgreement,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizationSnapshot",
            currentAgreement,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryTakeValidatedTreatmentPublication(",
            authoritySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "TryRearmValidatedTreatmentPublication(",
            authoritySource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GuaranteedItemConsumption_InvalidatedTerminalTakeIsQuarantineReleaseOnlyByShapeAndSource()
    {
        var terminalCacheTake = Assert.Single(
            typeof(MortalItemAcceptedTurnAuthority.Cache)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic),
            method => string.Equals(
                method.Name,
                "TryTakeInvalidatedTreatmentPublicationForTerminalRelease",
                StringComparison.Ordinal));
        var terminalCacheParameter = Assert.Single(terminalCacheTake.GetParameters());
        Assert.True(terminalCacheParameter.IsOut);
        Assert.Equal(
            typeof(MortalItemAcceptedTurnAuthority.Cache.ValidatedPublicationTakeSnapshot)
                .MakeByRefType(),
            terminalCacheParameter.ParameterType);

        var terminalFacadeTake = Assert.Single(
            typeof(AcceptedMechanicsPlanAuthority)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic),
            method => string.Equals(
                method.Name,
                "TryTakeValidatedTreatmentPublicationForTerminalRelease",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            terminalFacadeTake.GetParameters(),
            parameter => parameter.ParameterType ==
                         typeof(MortalItemAcceptedTurnNormalizationSnapshot));
        var terminalReceiptPurpose = typeof(MortalWoundTreatmentPublicationTakeReceipt)
            .GetProperty(
                "IsTerminalReleaseOnly",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(terminalReceiptPurpose);
        Assert.Equal(typeof(bool), terminalReceiptPurpose.PropertyType);

        var repositoryRoot = FindRepositoryRootForB4SourceGuard();
        var authoritySource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "BookOfEternityClient",
                "Services",
                "MortalItemAcceptedEffectSourceAuthority.cs")));
        var registrySource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "BookOfEternityClient",
                "Services",
                "AcceptedTurnAuthorityRegistry.cs")));
        var transactionSource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "BookOfEternityClient",
                "Services",
                "MortalWoundTreatmentResourcePublicationTransaction.cs")));
        var normalizerSource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(
            Path.Combine(
                repositoryRoot,
                "BookOfEternityClient",
                "Services",
                "CanonicalStateNormalizer",
                "CanonicalStateNormalizer.MortalItems.cs")));

        var terminalTake = ExtractB4MethodSource(
            authoritySource,
            "internal bool TryTakeInvalidatedTreatmentPublicationForTerminalRelease(");
        Assert.Contains("_treatmentPublicationBaselineSnapshot", terminalTake);
        Assert.Contains("RecomputesFinalPublicationBaseline(", terminalTake);
        Assert.Contains("MatchesExactCacheState(", terminalTake);
        Assert.Contains("terminalReleaseOnly: true", terminalTake);
        var rearm = ExtractB4MethodSource(
            authoritySource,
            "internal bool TryRearmValidatedTreatmentPublication(");
        Assert.Contains("snapshot.TerminalReleaseOnly", rearm);

        Assert.Contains(
            "TryTakeInvalidatedTreatmentPublicationForTerminalRelease(",
            registrySource);
        Assert.Contains("receipt.IsTerminalReleaseOnly", registrySource);
        Assert.Contains("receipt.IsTerminalReleaseOnly", transactionSource);
        Assert.Contains(
            "TryTakeValidatedTreatmentPublicationForTerminalRelease(",
            normalizerSource);
    }

    [Fact]
    public void GuaranteedItemConsumption_FinalPrepublicationBaselineIncludesEveryRootTouchingNormalizer()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true);
        var observer = new FinalBaselinePublicationObserver();
        using var fixture = AcceptedStateFixture.Create(
            scenario,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = observer.BeforeMutationAsync
            });
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var response = SeedFinalBaselineProductionInputs(fixture);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "final_prepublication_baseline");
        var before = CaptureResolverFixtureTree(fixture.Root);

        var composed = ComposeResourcePublicationResult(fixture, flow, response);

        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        Assert.DoesNotContain(composed.Issues, issue =>
            issue.Code == DeferredItemConsumptionCode);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
        var sealedPublication = ReadSealedTreatmentPublication(plan);
        Assert.Equal("Apply", sealedPublication.NpcTradeDisposition);
        AssertSealedFinalBaselineAuthorityInputs(sealedPublication, fixture);
        AssertFrozenFinalBaseline(
            sealedPublication.Baseline,
            sealedPublication.NpcTradeDisposition,
            plan,
            fixture);
        AssertResolverFixtureTreeUnchanged(fixture.Root, before);
        observer.Arm(fixture.FileSystem, sealedPublication.Baseline);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            Assert.True(observer.Matched, observer.DescribeFailure());
            AssertBaselineMatchesObservedLiveState(
                sealedPublication.Baseline,
                observer);
            publication.Compensate();
        }

        foreach (var axis in new[]
                 {
                     "npc_authority",
                     "npc_trade_pending_bytes",
                     "training_pending_bytes"
                 })
        {
            AssertFinalBaselineDriftRejectsBeforeCacheOrWrite(scenario, axis);
        }
    }

    [Fact]
    public void GuaranteedPlayerItemConsumption_NpcTradeDispositionIsSealedByNpcTailOwnership()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includePlayerItem: true,
            selectPlayerItem: true);

        SealedTreatmentPublicationProbe skip;
        using (var fixture = AcceptedStateFixture.Create(scenario))
        {
            fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
            SeedNpcTradeTailBehaviorInput(fixture);
            var flow = PersistAndRehydrateResourcePublication(
                fixture,
                scenario,
                "npc_trade_tail_disposition");
            skip = ReadSealedTreatmentPublication(
                ComposeResourcePublication(fixture, flow));
        }

        SealedTreatmentPublicationProbe apply;
        using (var fixture = AcceptedStateFixture.Create(scenario))
        {
            fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
            SeedNpcTradeTailBehaviorInput(fixture);
            var flow = PersistAndRehydrateResourcePublication(
                fixture,
                scenario,
                "npc_trade_tail_disposition");
            apply = ReadSealedTreatmentPublication(ComposeResourcePublication(
                fixture,
                flow,
                CreateNpcSkillOnlyResponse(fixture)));
        }

        Assert.Equal("SkipUntouchedTreatmentContinuation", skip.NpcTradeDisposition);
        Assert.Equal("Apply", apply.NpcTradeDisposition);
        Assert.Equal(
            ExpectedFinalBaselineTransformIds(skip.NpcTradeDisposition),
            skip.Baseline.AppliedTransformIds);
        Assert.Equal(
            ExpectedFinalBaselineTransformIds(apply.NpcTradeDisposition),
            apply.Baseline.AppliedTransformIds);
        Assert.Equal(skip.ItemEnvelopeFingerprint, apply.ItemEnvelopeFingerprint);
        Assert.NotEqual(skip.ItemAuthorityFingerprint, apply.ItemAuthorityFingerprint);
        Assert.NotEqual(skip.Baseline.Fingerprint, apply.Baseline.Fingerprint);
        Assert.True(JsonNode.DeepEquals(
            skip.Baseline.IdentityIndex,
            apply.Baseline.IdentityIndex));
        Assert.Equal(
            skip.Baseline.Roots.Keys.OrderBy(static path => path, StringComparer.Ordinal),
            apply.Baseline.Roots.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        foreach (var path in skip.Baseline.Roots.Keys.Where(path => !string.Equals(
                     path,
                     NpcCoreChangesContract.NpcCorePath,
                     StringComparison.Ordinal)))
        {
            Assert.True(
                JsonNode.DeepEquals(skip.Baseline.Roots[path], apply.Baseline.Roots[path]),
                path);
        }
        var skippedNpcRoot = Assert.IsType<JsonObject>(skip.Baseline.Roots[
            NpcCoreChangesContract.NpcCorePath]);
        var appliedNpcRoot = Assert.IsType<JsonObject>(apply.Baseline.Roots[
            NpcCoreChangesContract.NpcCorePath]);
        Assert.True(skippedNpcRoot.ContainsKey(
            NpcTradeRequestState.UpdateReceiptsProperty));
        Assert.False(appliedNpcRoot.ContainsKey(
            NpcTradeRequestState.UpdateReceiptsProperty));
        Assert.False(HasNpcTradeTailBehaviorReceipt(skippedNpcRoot));
        Assert.True(HasNpcTradeTailBehaviorReceipt(appliedNpcRoot));
        Assert.False(JsonNode.DeepEquals(skippedNpcRoot, appliedNpcRoot));
    }

    [Fact]
    public void FinalBaselinePlanner_DispatchesAndRecordsOnlyTheFrozenTransformRegistry()
    {
        var plannerType = RequireB4Type(
            "BookOfEternityClient.Services.MortalItemPublicationBaselinePlanner");
        var registryProperty = plannerType.GetProperty(
            "TransformRegistry",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(registryProperty);
        Assert.Equal(typeof(IReadOnlyList<string>), registryProperty!.PropertyType);
        Assert.Equal(
            ExpectedFinalBaselineTransformRegistryIds(),
            Assert.IsAssignableFrom<IReadOnlyList<string>>(
                registryProperty.GetValue(null)));

        var sourcePath = Path.Combine(
            FindRepositoryRootForB4SourceGuard(),
            "BookOfEternityClient",
            "Services",
            "MortalItemPublicationBaselinePlanner.cs");
        Assert.True(File.Exists(sourcePath), sourcePath);
        var project = StripB4CSharpCommentsAndLiterals(ExtractB4MethodSource(
            File.ReadAllText(sourcePath),
            "internal static MortalItemPublicationBaselineResult Project("));
        var loops = System.Text.RegularExpressions.Regex.Matches(
                project,
                @"\bforeach\s*\(\s*var\s+registration\s+in\s+TransformRegistry\s*\)")
            .Cast<System.Text.RegularExpressions.Match>()
            .ToArray();
        var loop = Assert.Single(loops);
        var loopBody = ExtractB4BracedBlockSource(
            project,
            loop.Index + loop.Length,
            "TransformRegistry foreach");
        var compactLoopBody = System.Text.RegularExpressions.Regex.Replace(
            loopBody,
            @"\s+",
            " ");
        var applyCall = "ApplyRegisteredTransform(registration,";
        var recordCall = "appliedTransformIds.Add(applied.AppliedTransformId);";
        Assert.Contains(applyCall, compactLoopBody, StringComparison.Ordinal);
        Assert.Contains(recordCall, compactLoopBody, StringComparison.Ordinal);
        Assert.True(
            compactLoopBody.IndexOf(applyCall, StringComparison.Ordinal) <
            compactLoopBody.IndexOf(recordCall, StringComparison.Ordinal),
            "The registered transform must execute before its returned applied ID is recorded.");
        Assert.Single(
            System.Text.RegularExpressions.Regex.Matches(
                    project,
                    @"\bApplyRegisteredTransform\s*\(")
                .Cast<System.Text.RegularExpressions.Match>());
        Assert.Single(
            System.Text.RegularExpressions.Regex.Matches(
                    project,
                    @"\bappliedTransformIds\s*\.\s*Add\s*\(\s*applied\s*\.\s*AppliedTransformId\s*\)")
                .Cast<System.Text.RegularExpressions.Match>());
        Assert.DoesNotContain(
            "AppliedTransformIds =",
            project,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedItemProjection_ForwardsValidatedAuthoritiesAndNeverRebuildsOrWrites()
    {
        var repositoryRoot = FindRepositoryRootForB4SourceGuard();
        var validationSource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "Validation",
            "ValidationService.MortalItemMaterialization.cs")));
        var rawValidation = ExtractB4MethodSource(
            validationSource,
            "private async Task ValidateAcceptedTurnRawMortalItemMaterializationAsync(");
        var registrationToken =
            "MortalItemAcceptedTurnAuthority.RegisterValidatedItems(";
        var registrationIndex = rawValidation.IndexOf(
            registrationToken,
            StringComparison.Ordinal);
        Assert.True(registrationIndex >= 0, "Accepted item registration call is absent.");
        var registrationCall = ExtractB4InvocationSource(
            rawValidation,
            registrationToken);
        Assert.Equal(
            10,
            CountB4TopLevelInvocationArguments(
                registrationCall,
                registrationToken.Length - 1));
        foreach (var forwardedName in new[]
                 {
                     "routeAuthorities",
                     "transferCatalog",
                     "currentProjectionRoots",
                     "backupProjectionRoots"
                 })
        {
            Assert.Contains(forwardedName, registrationCall, StringComparison.Ordinal);
        }

        foreach (var builderToken in B4ForbiddenCatalogBuilderTokens())
        {
            var matches = AllB4TokenIndexes(rawValidation, builderToken);
            Assert.Single(matches);
            Assert.True(
                matches[0] < registrationIndex,
                $"'{builderToken}' must run only while raw validation is minting the accepted authority.");
            Assert.DoesNotContain(
                builderToken,
                rawValidation[registrationIndex..],
                StringComparison.Ordinal);
        }

        var authoritySource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "MortalItemAcceptedEffectSourceAuthority.cs")));
        var authorityRegistration = ExtractB4MethodSource(
            authoritySource,
            "internal static void RegisterValidatedItems(");
        AssertB4InvocationForwards(
            authorityRegistration,
            "AcceptedTurnAuthorityRegistry.RegisterMortalItemsValidated(",
            12,
            "routeAuthorities",
            "transferCatalog",
            "currentProjectionRoots",
            "backupProjectionRoots");
        var cacheRegistration = ExtractB4MethodSource(
            authoritySource,
            "internal void Register(");
        var compactCacheRegistration = System.Text.RegularExpressions.Regex.Replace(
            cacheRegistration,
            @"\s+",
            " ");
        foreach (var retainedAssignment in new[]
                 {
                     "_routesByCreationRef = routesByCreationRef",
                     "_transfers = transfers",
                     "_currentProjectionRoots = currentProjectionRoots",
                     "_backupProjectionRoots = backupProjectionRoots"
                 })
        {
            Assert.Contains(
                retainedAssignment,
                compactCacheRegistration,
                StringComparison.Ordinal);
        }

        var registrySource = StripB4CSharpCommentsAndLiterals(File.ReadAllText(Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "AcceptedTurnAuthorityRegistry.cs")));
        AssertB4InvocationForwards(
            registrySource,
            "GetState(fileSystem, writeLease).RegisterMortalItemsValidated(",
            10,
            "routesByCreationRef",
            "transfers",
            "currentProjectionRoots",
            "backupProjectionRoots");
        var stateRegistration = ExtractB4MethodSource(
            registrySource,
            "internal void RegisterMortalItemsValidated(");
        AssertB4InvocationForwards(
            stateRegistration,
            "_mortalItems.Register(",
            10,
            "routesByCreationRef",
            "transfers",
            "currentProjectionRoots",
            "backupProjectionRoots");
        foreach (var source in new[] { authoritySource, registrySource })
        {
            foreach (var forbiddenToken in B4ForbiddenCatalogBuilderTokens()
                         .Append("MortalItemTransitionWriter"))
            {
                Assert.DoesNotContain(
                    forbiddenToken,
                    source,
                    StringComparison.Ordinal);
            }
        }

        var normalizerPath = Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer",
            "CanonicalStateNormalizer.MortalItems.cs");
        var normalizerSource = StripB4CSharpCommentsAndLiterals(
            File.ReadAllText(normalizerPath));
        var acceptedNormalization = ExtractB4MethodSource(
            normalizerSource,
            "private async Task NormalizeMortalItemsAsync(");
        Assert.Contains(
            "MortalItemCanonicalProjectionPlanner.Project(",
            acceptedNormalization,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "NormalizeMortalItemTransfersAsync(",
            acceptedNormalization,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "MortalItemAcceptedTransferCatalog.Build",
            normalizerSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "MortalItemTransitionWriter",
            normalizerSource,
            StringComparison.Ordinal);

        var projectionPath = Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "MortalItemCanonicalProjectionPlanner.cs");
        var transferPath = Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "MortalItemTransferPlanner.cs");
        Assert.True(File.Exists(projectionPath), projectionPath);
        Assert.True(File.Exists(transferPath), transferPath);
        var postRegistrationSources = new[]
        {
            acceptedNormalization,
            StripB4CSharpCommentsAndLiterals(File.ReadAllText(projectionPath)),
            StripB4CSharpCommentsAndLiterals(File.ReadAllText(transferPath))
        };
        foreach (var source in postRegistrationSources)
        {
            foreach (var forbiddenToken in B4ForbiddenCatalogBuilderTokens()
                         .Append("MortalItemTransitionWriter"))
            {
                Assert.DoesNotContain(
                    forbiddenToken,
                    source,
                    StringComparison.Ordinal);
            }
        }

        var postRegistrationIdentitySources = string.Join(
            Environment.NewLine,
            postRegistrationSources);
        AssertB4InvocationArity(
            postRegistrationIdentitySources,
            "MortalItemIdentityState.CreateRootReceipt(",
            expectedArity: 4);
        AssertB4InvocationArity(
            postRegistrationIdentitySources,
            "MortalItemIdentityState.CreateTransition(",
            expectedArity: 10);
    }

    [Fact]
    public void GuaranteedResourceQuantity_ReleaseOnlyPublishesNoMutationAndFinalizesReservation()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: Array.Empty<int>(),
            includeReusableItem: true);
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "release_only");
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        Assert.True(finalization.IsValid, DescribeIssues(finalization.Issues));
        Assert.Equal("release_only", finalization.Finalization!.Disposition);
        var resourceBytes = CaptureResourceBytes(fixture);
        var itemCarrierBytes = CaptureItemCarrierBytes(fixture);

        var plan = ComposeResourcePublication(fixture, flow);
        Assert.Empty(plan.ResourceEvents);
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            Assert.Same(plan, publication.Plan);
            AssertResourceBytesEqual(fixture, resourceBytes);
            AssertItemCarrierBytesEqual(fixture, itemCarrierBytes);
            Assert.Equal(10, ReadPlayerEnergy(fixture));
            Assert.Equal(
                "held",
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request)
                    .ResourceAuthority.ReservationDisposition);

            publication.CompleteAtFullPipelineEnd();
        }

        fixture.PrepareFreshSnapshot("release_only_history_replay");
        fixture.RestartForReplay();
        var recovered = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(fixture));
        Assert.True(recovered.IsValid, DescribeIssues(recovered.Issues));
        Assert.Empty(recovered.HeldRequests);
        Assert.Single(recovered.FinalizedRequests);
    }

    [Fact]
    public void GuaranteedResourceQuantity_DirectNormalizerRejectsBeforeTakingOrWriting()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "direct_normalizer_fence");
        var plan = ComposeResourcePublication(fixture, flow);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);

        var exception = Record.Exception(() => new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease)
            .NormalizeAcceptedMechanicsAsync(backups: null)
            .GetAwaiter()
            .GetResult());

        Assert.NotNull(exception);
        Assert.Contains("top-level accepted-turn transaction", exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        Assert.Equal(10, ReadPlayerEnergy(fixture));

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Same(plan, publication.Plan);
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        publication.Compensate();
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Theory]
    [InlineData("direct")]
    [InlineData("accumulated")]
    public void GuaranteedResourceQuantity_HeldCoordinatorFencePrecedesGenericStalePreflight(
        string entrypoint)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "early_coordinator_fence_" + entrypoint);
        var plan = ComposeResourcePublication(fixture, flow);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        WriteCanonicalPlayerEnergyAuthority(fixture.FileSystem, current: 9);

        var normalizer = new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease);
        var exception = Record.Exception(() =>
        {
            if (string.Equals(entrypoint, "direct", StringComparison.Ordinal))
            {
                _ = normalizer.NormalizeAcceptedMechanicsAsync(backups: null)
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                _ = normalizer.NormalizeAccumulatedStateWithPlanAsync(backups: null)
                    .GetAwaiter()
                    .GetResult();
            }
        });

        Assert.NotNull(exception);
        Assert.Contains(
            "top-level accepted-turn transaction",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Same(plan, PeekCachedPlan(fixture));

        RestoreResolverFixtureTree(fixture, treeBefore);
        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Same(plan, publication.Plan);
        publication.Compensate();
        AssertExactCachedPlanAndBinding(
            fixture,
            plan,
            publication.OriginalBinding);
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Fact]
    public void GuaranteedResourceQuantity_PostWriteFailureRestoresAllRootsRetainsHoldAndExactRetryCommitsOnce()
    {
        var fault = new ResourcePublicationFailureInjection();
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(
            scenario,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync
            });
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "rollback_retry");
        var plan = ComposeResourcePublication(fixture, flow);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var originalBinding,
            out var originalCached));
        Assert.Same(plan, originalCached.Plan);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);
        var commandBefore = ReadCanonicalBytes(
            fixture,
            AcceptedMechanicsPlan.WoundCommandPath);
        fault.Arm(
            WoundHistoryState.HistoryPath,
            ResourceMaterializationContract.StatePath,
            ReadCanonicalBytes(fixture, ResourceMaterializationContract.StatePath),
            fixture.FileSystem);

        var exception = Assert.Throws<CanonicalStateWriteException>(() =>
            PublishCachedResourcePlanOpen(fixture, flow, plan));

        Assert.Equal(WoundHistoryState.HistoryPath, exception.RelativePath);
        Assert.True(fault.Fired);
        Assert.True(fault.ObservedEarlierResourceWrite);
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        AssertExactCachedPlanAndBinding(fixture, plan, originalBinding);
        Assert.Equal(commandBefore, ReadCanonicalBytes(
            fixture,
            AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(2, ReadPlayerEnergy(fixture));

        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            fixture.ReadCurrentHistory(),
            fixture.ReadCurrentWound(),
            scenario.OperationKey + "_rollback_competing",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.False(competing.IsValid);
        Assert.Contains(competing.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_resource_reservation_overbooked",
            StringComparison.Ordinal));

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Same(plan, publication.Plan);
        Assert.Equal(0, ReadPlayerEnergy(fixture));
        publication.CompleteAtFullPipelineEnd();
        Assert.Single(ReadTreatmentResourceSpendTransitions(fixture));
    }

    [Fact]
    public void GuaranteedResourceQuantity_SameCoordinateMutationsFollowFinalizationOrder()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2, 3 },
            selectedResourceOrder: new[] { 1, 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "same_coordinate_order");
        using var coldFixture = CreateColdRootCopy(fixture);
        var coldRestored = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture),
            "same-coordinate cold composition"));
        var coldFlow = RehydratePersistedTreatment(
            coldFixture,
            "guaranteed",
            coldRestored);

        var plan = ComposeResourcePublication(fixture, flow);
        var coldPlan = ComposeResourcePublication(coldFixture, coldFlow);

        AssertResourcePlan(
            plan,
            expectedCurrent: 5,
            ReadResourceHistory(fixture).Transitions.Count,
            expectedSpendQuantities: new decimal[] { 3, 2 });
        Assert.Equal(new decimal[] { 10, 7 },
            plan.ResourceEvents.Select(static row => row.Before));
        Assert.Equal(new decimal[] { 7, 5 },
            plan.ResourceEvents.Select(static row => row.After));
        Assert.Equal(2, plan.ResourceEvents.Select(static row => row.OperationId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, plan.ResourceEvents.Select(static row => row.EventRef)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            plan.ResourceEvents[0].ExecutionSequence + 1,
            plan.ResourceEvents[1].ExecutionSequence);
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.EventRef),
            coldPlan.ResourceEvents.Select(static row => row.EventRef));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.OperationId),
            coldPlan.ResourceEvents.Select(static row => row.OperationId));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.SourceFingerprint),
            coldPlan.ResourceEvents.Select(static row => row.SourceFingerprint));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.ExecutionSequence),
            coldPlan.ResourceEvents.Select(static row => row.ExecutionSequence));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.Before),
            coldPlan.ResourceEvents.Select(static row => row.Before));
        Assert.Equal(
            plan.ResourceEvents.Select(static row => row.After),
            coldPlan.ResourceEvents.Select(static row => row.After));
        Assert.Equal(
            CanonicalValue(plan.StateAfterImage),
            CanonicalValue(coldPlan.StateAfterImage));
        Assert.Equal(
            CanonicalValue(plan.HistoryAfterImage),
            CanonicalValue(coldPlan.HistoryAfterImage));
    }

    [Fact]
    public void GuaranteedResourceQuantity_UnmarkedAccumulatedNormalizerRejectsBeforeTakingOrWriting()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "unmarked_accumulated_fence");
        var plan = ComposeResourcePublication(fixture, flow);
        var treeBefore = CaptureResolverFixtureTree(fixture.Root);

        var exception = Record.Exception(() => new CanonicalStateNormalizer(
                fixture.FileSystem,
                NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(fixture.Lease)
            .NormalizeAccumulatedStateWithPlanAsync(
                new Dictionary<string, string>(StringComparer.Ordinal))
            .GetAwaiter()
            .GetResult());

        Assert.NotNull(exception);
        Assert.Contains("transaction", exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
        Assert.Equal(10, ReadPlayerEnergy(fixture));

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Same(plan, publication.Plan);
        publication.Compensate();
        Assert.Same(plan, PeekCachedPlan(fixture));
        AssertResolverFixtureTreeUnchanged(fixture.Root, treeBefore);
    }

    [Fact]
    public void GuaranteedResourceQuantity_OpenTransactionTokenRejectsSwapAndReplay()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var firstFixture = AcceptedStateFixture.Create(scenario);
        using var secondFixture = AcceptedStateFixture.Create(scenario);
        firstFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        secondFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var firstFlow = PersistAndRehydrateResourcePublication(
            firstFixture,
            scenario,
            "token_first");
        var secondFlow = PersistAndRehydrateResourcePublication(
            secondFixture,
            scenario,
            "token_second");
        var firstPlan = ComposeResourcePublication(firstFixture, firstFlow);
        var secondPlan = ComposeResourcePublication(secondFixture, secondFlow);
        var firstHistoryCount = ReadResourceHistory(firstFixture).Transitions.Count;
        IReadOnlyDictionary<string, byte[]> firstPublishedTree =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, byte[]> secondPublishedTree =
            new Dictionary<string, byte[]>(StringComparer.Ordinal);

        using var firstPublication = PublishCachedResourcePlanOpen(
            firstFixture,
            firstFlow,
            firstPlan);
        using (var secondPublication = PublishCachedResourcePlanOpen(
                   secondFixture,
                   secondFlow,
                   secondPlan))
        {
            firstPublishedTree = CaptureResolverFixtureTree(firstFixture.Root);
            secondPublishedTree = CaptureResolverFixtureTree(secondFixture.Root);

            AssertResourceTransactionOperationRejected(
                firstFixture,
                secondPublication.TransactionToken,
                "complete",
                TransactionTokenMismatchCode,
                "TokenMismatch");

            AssertResolverFixtureTreeUnchanged(firstFixture.Root, firstPublishedTree);
            AssertResolverFixtureTreeUnchanged(secondFixture.Root, secondPublishedTree);
            firstPublication.CompleteAtFullPipelineEnd();
            Assert.Equal(8, ReadPlayerEnergy(firstFixture));
            Assert.Equal(
                firstHistoryCount + 1,
                ReadResourceHistory(firstFixture).Transitions.Count);

            var committedTree = CaptureResolverFixtureTree(firstFixture.Root);
            AssertResourceTransactionOperationRejected(
                firstFixture,
                firstPublication.TransactionToken,
                "complete",
                TransactionTokenReplayedCode,
                "TokenReplayed");
            AssertResolverFixtureTreeUnchanged(firstFixture.Root, committedTree);
            Assert.Equal(
                firstHistoryCount + 1,
                ReadResourceHistory(firstFixture).Transitions.Count);
        }

        Assert.Equal(10, ReadPlayerEnergy(secondFixture));
        Assert.Same(secondPlan, PeekCachedPlan(secondFixture));
    }

    [Theory]
    [InlineData("receipt_fence_invalidation")]
    [InlineData("released_hold")]
    public void GuaranteedResourceQuantity_CommitConflictRollsBackAndDoesNotExposePartialState(
        string axis)
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "commit_conflict_" + axis);
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var commandBefore = ReadCanonicalBytes(
            fixture,
            AcceptedMechanicsPlan.WoundCommandPath);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        if (string.Equals(axis, "receipt_fence_invalidation", StringComparison.Ordinal))
        {
            AcceptedMechanicsPlanAuthority.InvalidateValidated(
                fixture.FileSystem,
                fixture.Lease);
        }
        else
        {
            var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                fixture.GetAcceptedState());
            var released = acceptedState.ReleaseTreatmentResources(
                ResourceLifecycleCapability(),
                new[] { request },
                "cancelled");
            Assert.True(released.IsValid, DescribeIssues(released.Issues));
            Assert.Equal(1, released.ChangedCount);
        }

        AssertResourceTransactionOperationRejected(
            fixture,
            publication.TransactionToken,
            "complete",
            string.Equals(axis, "receipt_fence_invalidation", StringComparison.Ordinal)
                ? TransactionStaleCode
                : TransactionReservationChangedCode,
            string.Equals(axis, "receipt_fence_invalidation", StringComparison.Ordinal)
                ? "Stale"
                : "ReservationChanged");

        if (string.Equals(axis, "receipt_fence_invalidation", StringComparison.Ordinal))
        {
            publication.CompensateWithOutcome("HeldBlocked");
            Assert.Equal(commandBefore, ReadCanonicalBytes(
                fixture,
                AcceptedMechanicsPlan.WoundCommandPath));
            AssertResolverFixtureTreeUnchanged(fixture.Root, canonicalBefore);
            AssertConfirmedHeldResourceAgreement(fixture, flow.Request);
            AssertTreatmentHoldBlocksCompetingReservation(
                fixture,
                scenario,
                "invalidated_fence_hold_proof");
            AssertPublicationBlocked(fixture, flow);
        }
        else
        {
            publication.CompensateWithOutcome("CommandQuarantined");
            AssertDurableTreatmentSurfacesQuarantined(fixture, request);
            AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
                fixture,
                canonicalBefore);
        }

        Assert.Equal(2, ReadPlayerEnergy(fixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
    }

    [Fact]
    public void GuaranteedResourceQuantity_FinalCommitRejectsDriftedPublishedAgreementBeforeReservationCommit()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "final_commit_published_agreement_drift");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        Assert.Equal(8, ReadPlayerEnergy(fixture));
        WriteCanonicalPlayerEnergyAuthority(fixture.FileSystem, current: 7);

        AssertResourceTransactionOperationRejected(
            fixture,
            publication.TransactionToken,
            "complete",
            PublishedAgreementChangedCode,
            "PublishedAgreementChanged");
        AssertConfirmedHeldResourceAgreement(fixture, flow.Request);

        publication.Compensate();
        Assert.Equal(10, ReadPlayerEnergy(fixture));
        AssertExactCachedPlanAndBinding(
            fixture,
            plan,
            publication.OriginalBinding);
        AssertResolverFixtureTreeUnchanged(fixture.Root, canonicalBefore);
    }

    [Fact]
    public void GuaranteedResourceQuantity_CompetingOrdinaryAdmissionSurvivesStaleCompensationAndBlocksRestart()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        fixture.EnsureRawResourceMaterializationSnapshotCoverage();
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "competing_ordinary_admission");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var durableBefore = CaptureDurableTreatmentSurfaceBytes(fixture);

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        AcceptedMechanicsPlanAuthority.InvalidateValidated(
            fixture.FileSystem,
            fixture.Lease);
        RestoreResolverFixtureTree(fixture, canonicalBefore);
        RemoveDurableTreatmentSurfaces(fixture, durableBefore.Keys);
        var competing = AdmitCompetingCommonPlanFromLiveState(fixture);
        Assert.NotSame(plan, competing.Plan);
        RestoreDurableTreatmentSurfaceBytes(fixture, durableBefore);

        publication.CompensateWithOutcome("HeldBlocked");

        AssertDurableTreatmentSurfaceBytesEqual(fixture, durableBefore);
        AssertResolverFixtureTreeUnchanged(fixture.Root, canonicalBefore);
        AssertConfirmedHeldResourceAgreement(fixture, flow.Request);
        AssertExactCachedPlanAndBinding(
            fixture,
            competing.Plan,
            competing.Binding);
        AssertPublicationBlocked(fixture, flow);
    }

    [Fact]
    public void GuaranteedResourceQuantity_ColdConfirmedHeldCommandPublishesBeforeAnyAcceptedHistory()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var warmFixture = AcceptedStateFixture.Create(scenario);
        warmFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        _ = PersistAndRehydrateResourcePublication(
            warmFixture,
            scenario,
            "cold_confirmed_before_publication");
        using var coldFixture = CreateColdRootCopy(warmFixture);
        var catalog = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture));
        var restored = Assert.Single(AssertValidPersistedCatalog(
            catalog,
            "cold confirmed B.3 command"));
        Assert.Single(catalog.HeldRequests);
        Assert.Empty(catalog.FinalizedRequests);
        var coldFlow = RehydratePersistedTreatment(
            coldFixture,
            "guaranteed",
            restored);
        AssertConfirmedHeldResourceAgreement(coldFixture, coldFlow.Request);
        var historyBefore = ReadResourceHistory(coldFixture).Transitions.Count;

        var plan = ComposeResourcePublication(coldFixture, coldFlow);
        using var publication = PublishCachedResourcePlanOpen(
            coldFixture,
            coldFlow,
            plan);
        Assert.Equal(8, ReadPlayerEnergy(coldFixture));
        publication.CompleteAtFullPipelineEnd();

        var finalizedBytes = CaptureResourceBytes(coldFixture);
        Assert.Equal(historyBefore + 1, ReadResourceHistory(coldFixture).Transitions.Count);
        coldFixture.PrepareFreshSnapshot("cold_resource_publication_history_replay");
        using var replayFixture = CreateColdRootCopy(coldFixture);
        var replayCatalog = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(replayFixture));
        Assert.True(replayCatalog.IsValid, DescribeIssues(replayCatalog.Issues));
        Assert.Empty(replayCatalog.HeldRequests);
        Assert.Single(replayCatalog.FinalizedRequests);
        var replay = ProbePublishedTreatment(replayFixture, coldFlow.Request);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(
            replay,
            "Status")));
        AssertResourceBytesEqual(replayFixture, finalizedBytes);
        Assert.Equal(historyBefore + 1, ReadResourceHistory(replayFixture).Transitions.Count);
    }

    [Fact]
    public void GuaranteedItemConsumption_ColdConfirmedHeldCommandPublishesItemAndResourcesOnceThenAcceptedReplayIsInert()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 },
            includeReusableItem: true,
            selectReusableItem: true,
            reusableItemCount: 2);
        using var warmFixture = AcceptedStateFixture.Create(scenario);
        warmFixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        warmFixture.SetCanonicalReusableItemDurabilityForResourcePublicationTest(
            current: 8,
            maximum: 8);
        _ = PersistAndRehydrateResourcePublication(
            warmFixture,
            scenario,
            "cold_confirmed_item_before_publication");

        using var coldFixture = CreateColdRootCopy(warmFixture);
        var catalog = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture));
        var restored = Assert.Single(AssertValidPersistedCatalog(
            catalog,
            "cold confirmed B.4 item command"));
        Assert.Single(catalog.HeldRequests);
        Assert.Empty(catalog.FinalizedRequests);
        var coldFlow = RehydratePersistedTreatment(
            coldFixture,
            "guaranteed",
            restored);
        AssertConfirmedHeldResourceAgreement(coldFixture, coldFlow.Request);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            coldFlow.Request);
        Assert.Contains(request.ResourceAuthority.Claims, static claim =>
            string.Equals(claim.Kind, "resource_quantity", StringComparison.Ordinal) &&
            string.Equals(claim.AuthorityRef, "energy", StringComparison.Ordinal) &&
            claim.Quantity == 2);
        Assert.Contains(request.ResourceAuthority.Claims, static claim =>
            string.Equals(claim.Kind, "item_quantity", StringComparison.Ordinal) &&
            string.Equals(
                claim.AuthorityRef,
                "reusable_field_kit",
                StringComparison.Ordinal) &&
            claim.Quantity == 1);
        var itemTransitionsBefore = ReadIdentityTransitionCount(
            ReadItemIdentityEntry(coldFixture, "reusable_field_kit"));
        var resourceHistoryBefore = ReadResourceHistory(coldFixture);
        var energySpendsBefore = ReadTreatmentResourceSpendTransitions(
            coldFixture).Count;
        var durabilityReconfiguresBefore =
            CountReusableItemDurabilityReconfigures(coldFixture);

        var plan = ComposeResourcePublication(coldFixture, coldFlow);
        Assert.Equal(1, ReadPlanNpcItemCount(plan, "reusable_field_kit"));
        var plannedDurability = Assert.Single(
            plan.StateAfterImage["entries"]!.AsArray().OfType<JsonObject>(),
            IsReusableItemDurabilityState);
        Assert.Equal(4m, plannedDurability["current"]!.GetValue<decimal>());
        Assert.Equal(4m, plannedDurability["maximum"]!.GetValue<decimal>());

        using (var publication = PublishCachedResourcePlanOpen(
                   coldFixture,
                   coldFlow,
                   plan))
        {
            Assert.Equal(1, coldFixture.ReadNpcItemCount("reusable_field_kit"));
            Assert.Equal(8, ReadPlayerEnergy(coldFixture));
            var liveDurability = ReadReusableItemDurability(coldFixture);
            Assert.Equal(4m, liveDurability.Current);
            Assert.Equal(4m, liveDurability.Maximum);
            Assert.Equal(
                itemTransitionsBefore + 1,
                ReadIdentityTransitionCount(ReadItemIdentityEntry(
                    coldFixture,
                    "reusable_field_kit")));
            Assert.Equal(
                resourceHistoryBefore.Transitions.Count + 2,
                ReadResourceHistory(coldFixture).Transitions.Count);
            Assert.Equal(
                energySpendsBefore + 1,
                ReadTreatmentResourceSpendTransitions(coldFixture).Count);
            Assert.Equal(
                durabilityReconfiguresBefore + 1,
                CountReusableItemDurabilityReconfigures(coldFixture));
            publication.CompleteAtFullPipelineEnd();
        }

        var finalizedItemBytes = CaptureItemCarrierBytes(coldFixture);
        var finalizedResourceBytes = CaptureResourceBytes(coldFixture);
        var finalizedItemTransitions = ReadIdentityTransitionCount(
            ReadItemIdentityEntry(coldFixture, "reusable_field_kit"));
        var finalizedResourceTransitions =
            ReadResourceHistory(coldFixture).Transitions.Count;
        var finalizedEnergySpends =
            ReadTreatmentResourceSpendTransitions(coldFixture).Count;
        var finalizedDurabilityReconfigures =
            CountReusableItemDurabilityReconfigures(coldFixture);
        coldFixture.PrepareFreshSnapshot(
            "cold_item_resource_publication_history_replay");
        using var replayFixture = CreateColdRootCopy(
            coldFixture,
            carrySourceTreatmentContext: false);
        Assert.NotEqual(coldFixture.Root, replayFixture.Root);
        Assert.NotSame(coldFixture.FileSystem, replayFixture.FileSystem);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            replayFixture.FileSystem,
            replayFixture.Lease));
        Assert.False(replayFixture.HasParserProvenancedTreatmentContext());
        Assert.False(replayFixture.SharesTreatmentContextWith(coldFixture));

        var replayHistory = replayFixture.ReadCurrentHistory();
        var replayCommandRoot = ReadOptionalDurableTreatmentRoot(
            replayFixture,
            AcceptedMechanicsPlan.WoundCommandPath);
        var replayPendingRoot = ReadOptionalDurableTreatmentRoot(
            replayFixture,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        var rawReplayCatalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(
            replayCommandRoot,
            replayPendingRoot,
            replayHistory);
        Assert.True(
            rawReplayCatalog.IsValid,
            DescribeIssues(rawReplayCatalog.Issues));
        Assert.Empty(rawReplayCatalog.HeldRequests);
        var rawFinalizedRequest = Assert.Single(
            rawReplayCatalog.FinalizedRequests);
        Assert.NotSame(request, rawFinalizedRequest);

        var replayCoordinates = rawFinalizedRequest.Coordinates;
        var parsedReplayContext = MortalWoundTreatmentAuthority.ParseContext(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = replayCoordinates.Realm,
                ["targetKind"] = replayCoordinates.TargetKind,
                ["targetId"] = replayCoordinates.TargetId,
                ["providerKind"] = replayCoordinates.ProviderKind,
                ["providerId"] = replayCoordinates.ProviderId,
                ["currentLocationId"] = replayCoordinates.LocationId
            }.ToJsonString(),
            "durableAcceptedReplay.treatmentContext");
        Assert.True(
            parsedReplayContext.IsValid,
            DescribeIssues(parsedReplayContext.Issues));
        var freshReplayContext = Assert.IsType<
            MortalWoundTreatmentAuthority.Context>(parsedReplayContext.Context);
        Assert.True(freshReplayContext.HasValidParserProvenance());
        var replayAcceptedStateResult =
            MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(
                replayFixture.FileSystem,
                replayFixture.Lease,
                freshReplayContext,
                replayCoordinates.WoundId);
        Assert.True(
            replayAcceptedStateResult.IsValid,
            DescribeIssues(replayAcceptedStateResult.Issues));
        var replayAcceptedState =
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                replayAcceptedStateResult.Authority);
        Assert.NotSame(coldFlow.AcceptedState, replayAcceptedState);
        var replayCatalog = replayAcceptedState.RestorePersistedTreatmentRequests(
            replayHistory);
        Assert.True(replayCatalog.IsValid, DescribeIssues(replayCatalog.Issues));
        Assert.Empty(replayCatalog.HeldRequests);
        var finalizedRequest = Assert.Single(replayCatalog.FinalizedRequests);
        Assert.NotSame(request, finalizedRequest);
        Assert.NotSame(rawFinalizedRequest, finalizedRequest);
        Assert.Equal(
            rawFinalizedRequest.RequestFingerprint,
            finalizedRequest.RequestFingerprint);
        Assert.Equal(replayCoordinates.WoundId, replayFixture.WoundId);
        var replayWound = replayFixture.AssertCurrentWoundCoordinate(
            replayCoordinates.TargetKind,
            replayCoordinates.TargetId,
            AcceptedStateFixture.ResolveTargetCarrierPath(
                replayCoordinates.TargetKind));

        var replay = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
            finalizedRequest,
            replayHistory,
            replayWound,
            replayAcceptedState);

        Assert.Equal("ExactReplay", replay.Disposition);
        Assert.Empty(replay.Issues);
        Assert.Null(replay.Resolution);
        Assert.NotNull(replay.ReplayReceipt);
        AssertItemCarrierBytesEqual(replayFixture, finalizedItemBytes);
        AssertResourceBytesEqual(replayFixture, finalizedResourceBytes);
        Assert.Equal(1, replayFixture.ReadNpcItemCount("reusable_field_kit"));
        Assert.Equal(8, ReadPlayerEnergy(replayFixture));
        var replayDurability = ReadReusableItemDurability(replayFixture);
        Assert.Equal(4m, replayDurability.Current);
        Assert.Equal(4m, replayDurability.Maximum);
        Assert.Equal(
            finalizedItemTransitions,
            ReadIdentityTransitionCount(ReadItemIdentityEntry(
                replayFixture,
                "reusable_field_kit")));
        Assert.Equal(
            finalizedResourceTransitions,
            ReadResourceHistory(replayFixture).Transitions.Count);
        Assert.Equal(
            finalizedEnergySpends,
            ReadTreatmentResourceSpendTransitions(replayFixture).Count);
        Assert.Equal(
            finalizedDurabilityReconfigures,
            CountReusableItemDurabilityReconfigures(replayFixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            replayFixture.FileSystem,
            replayFixture.Lease));
    }

    [Fact]
    public void GuaranteedResourceQuantity_DurableCommandProofFailureQuarantinesCommandAndNeverRearms()
    {
        var fault = new DurableCommandProofFailureInjection();
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(
            scenario,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationAsync = fault.BeforeCanonicalMutationAsync,
                AfterPhysicalFilePublishedAsync = fault.AfterPhysicalFilePublishedAsync
            });
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "durable_command_proof_failure");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var durableBefore = CaptureDurableTreatmentSurfaceBytes(fixture);
        AssertDurableTreatmentSurfacesContainRequest(durableBefore, request);

        using var publication = PublishCachedResourcePlanOpen(fixture, flow, plan);
        fault.Arm(fixture.FileSystem);
        publication.CompensateWithOutcome("CommandQuarantined");

        Assert.True(fault.Fired);
        Assert.Equal(2, ReadPlayerEnergy(fixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertDurableTreatmentSurfacesQuarantined(fixture, request);
        AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
            fixture,
            canonicalBefore);
        AssertTreatmentReservationReleased(
            fixture,
            request,
            "validation_failed");
        AssertTreatmentHoldReleasedAllowsCompetingReservation(
            fixture,
            scenario,
            "durable_command_proof_terminal_release");
    }

    [Fact]
    public void GuaranteedResourceQuantity_TerminalValidationFailureRemovesDurableRequestReleasesHoldAndNeverRearms()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "terminal_release_success");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        AssertDurableTreatmentSurfacesContainRequest(
            CaptureDurableTreatmentSurfaceBytes(fixture),
            request);

        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
            publication.ReleaseTerminal("validation_failed");

        Assert.Equal(2, ReadPlayerEnergy(fixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertDurableTreatmentSurfacesQuarantined(fixture, request);
        AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
            fixture,
            canonicalBefore);
        AssertTreatmentReservationReleased(
            fixture,
            request,
            "validation_failed");
        AssertTreatmentHoldReleasedAllowsCompetingReservation(
            fixture,
            scenario,
            "terminal_release_success");
    }

    [Fact]
    public void GuaranteedResourceQuantity_RetryableRepairCompensationCanStillSettleTerminalRollback()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "repair_then_terminal_release");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);

        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            publication.CompensateForRepair();
            AssertExactCachedPlanAndBinding(
                fixture,
                plan,
                publication.OriginalBinding);
            AssertConfirmedHeldResourceAgreement(fixture, request);

            publication.ReleaseTerminal("validation_failed");
        }

        Assert.Equal(2, ReadPlayerEnergy(fixture));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertDurableTreatmentSurfacesQuarantined(fixture, request);
        AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
            fixture,
            canonicalBefore);
        AssertTreatmentReservationReleased(
            fixture,
            request,
            "validation_failed");
        AssertTreatmentHoldReleasedAllowsCompetingReservation(
            fixture,
            scenario,
            "repair_then_terminal_release");
    }

    [Fact]
    public void GuaranteedResourceQuantity_QuarantineSelectorPreservesNeighborTextContainingTargetCoordinates()
    {
        const string operationKey = "operation_t070b3_quarantine_target";
        const string attemptId = "attempt_t070b3_quarantine_target";
        const string requestFingerprint =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string neighborFingerprint =
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var targetCommand = CreateQuarantineCommandRow(
            operationKey,
            attemptId,
            requestFingerprint,
            "Target treatment scene.");
        var neighborCommand = CreateQuarantineCommandRow(
            "operation_t070b3_quarantine_neighbor",
            "attempt_t070b3_quarantine_neighbor",
            neighborFingerprint,
            operationKey);
        var command = new JsonObject
        {
            ["commands"] = new JsonArray(targetCommand, neighborCommand)
        };
        var targetPending = CreateQuarantinePendingRow(
            operationKey,
            attemptId,
            requestFingerprint);
        var neighborPending = CreateQuarantinePendingRow(
            "operation_t070b3_quarantine_neighbor",
            "attempt_t070b3_quarantine_neighbor",
            neighborFingerprint);
        neighborPending["note"] = operationKey;
        var pending = new JsonObject
        {
            ["submittedTreatmentRequests"] = new JsonArray(
                targetPending,
                neighborPending)
        };

        var result = MortalWoundTreatmentDurableSurfaceQuarantine.RemoveExactRequestRows(
            command,
            pending,
            operationKey,
            attemptId,
            requestFingerprint);

        Assert.True(result.CommandChanged);
        Assert.True(result.PendingChanged);
        var retainedCommand = Assert.IsType<JsonObject>(Assert.Single(
            command["commands"]!.AsArray()));
        Assert.Equal(
            "operation_t070b3_quarantine_neighbor",
            retainedCommand["operationKey"]!.GetValue<string>());
        Assert.Equal(operationKey, retainedCommand["finalSceneText"]!.GetValue<string>());
        var retainedPending = Assert.IsType<JsonObject>(Assert.Single(
            pending["submittedTreatmentRequests"]!.AsArray()));
        Assert.Equal(
            "operation_t070b3_quarantine_neighbor",
            retainedPending["operationKey"]!.GetValue<string>());
        Assert.Equal(operationKey, retainedPending["note"]!.GetValue<string>());
    }

    [Fact]
    public void GuaranteedResourceQuantity_TerminalLifecycleReleaseRefusalRestoresDurableRequestRetainsHoldAndRequiresRestart()
    {
        var observer = new TerminalDurableSurfaceRemovalObserver();
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(
            scenario,
            new FileSystemManagerHooks
            {
                AfterPhysicalFilePublishedAsync = observer.AfterPhysicalFilePublishedAsync
            });
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(2);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "terminal_lifecycle_release_refusal");
        var plan = ComposeResourcePublication(fixture, flow);
        var canonicalBefore = CaptureResolverFixtureTree(fixture.Root);
        var durableBefore = CaptureDurableTreatmentSurfaceBytes(fixture);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        AssertDurableTreatmentSurfacesContainRequest(
            durableBefore,
            request);

        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            observer.Arm(fixture.FileSystem, request);
            publication.ExpectTerminalReleaseFailure("rolled_back");
            Assert.True(observer.ObservedCommandRemoval);
            Assert.Equal(
                observer.PendingContainedRequest,
                observer.ObservedPendingRemoval);
            AssertDurableTreatmentSurfaceBytesEqual(fixture, durableBefore);
            AssertResolverFixtureTreeUnchanged(fixture.Root, canonicalBefore);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
                fixture.FileSystem,
                fixture.Lease));
            AssertConfirmedHeldResourceAgreement(fixture, flow.Request);
            AssertTreatmentHoldBlocksCompetingReservation(
                fixture,
                scenario,
                "terminal_release_failure_hold_proof");
            AssertPublicationBlocked(fixture, flow);
        }

        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertPublicationBlocked(fixture, flow);
    }

    [Fact]
    public void GuaranteedResourceQuantity_OpenAcceptedStateRebindKeepsConfirmedHoldThenFinalizesOnceAndColdReplays()
    {
        var scenario = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: new[] { 0 });
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "finalized_history_rebind");
        var plan = ComposeResourcePublication(fixture, flow);
        var historyBefore = ReadResourceHistory(fixture).Transitions.Count;
        using (var publication = PublishCachedResourcePlanOpen(fixture, flow, plan))
        {
            _ = fixture.GetAcceptedState();
            AssertConfirmedHeldResourceAgreement(fixture, flow.Request);
            Assert.Equal(
                historyBefore + 1,
                ReadResourceHistory(fixture).Transitions.Count);
            publication.CompleteAtFullPipelineEnd();
        }
        var finalizedBytes = CaptureResourceBytes(fixture);

        fixture.PrepareFreshSnapshot("accepted_state_rebind_history_replay");
        using var coldFixture = CreateColdRootCopy(fixture);
        var catalog = Assert.IsType<MortalWoundTreatmentPersistedRequestCatalogResult>(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture));
        Assert.True(catalog.IsValid, DescribeIssues(catalog.Issues));
        Assert.Empty(catalog.HeldRequests);
        Assert.Single(catalog.FinalizedRequests);
        var replay = ProbePublishedTreatment(coldFixture, flow.Request);

        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(
            replay,
            "Status")));
        AssertResourceBytesEqual(coldFixture, finalizedBytes);
        Assert.Equal(
            historyBefore + 1,
            ReadResourceHistory(coldFixture).Transitions.Count);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            coldFixture.FileSystem,
            coldFixture.Lease));
    }

    private static ResolverScenario CreateGuaranteedResourcePublicationScenario(
        IReadOnlyList<int> resourceQuantities,
        IReadOnlyList<int> selectedResourceOrder,
        bool includeReusableItem = false,
        bool selectReusableItem = false,
        int reusableItemCount = 1,
        bool includePlayerItem = false,
        bool selectPlayerItem = false)
    {
        var source = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        var before = source.Before.DeepClone().AsObject();
        var woundId = before["woundId"]!.GetValue<string>();
        before["consequences"] = new JsonObject
        {
            ["slotBudget"] = 2,
            ["slotsUsed"] = 1,
            ["ownedEffectSources"] = WoundContractTestData.CreateOwnedEffectSources(
                woundId,
                "mortal_world",
                ("effect_wound_test_pain", "definition_wound_test_pain", "action_control")),
            ["entries"] = new JsonArray(new JsonObject
            {
                ["slot"] = 1,
                ["profileKey"] = "action_control",
                ["effectId"] = "effect_wound_test_pain",
                ["readableSummary"] = "Резкие движения затруднены."
            })
        };
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        var requirements = route["requirements"]!.AsArray();
        foreach (var quantity in resourceQuantities)
        {
            requirements.Add(new JsonObject
            {
                ["kind"] = "resource_quantity",
                ["resourceRef"] = "energy",
                ["quantity"] = quantity,
                ["ownerRole"] = "target"
            });
        }

        int? reusableIndex = null;
        if (includeReusableItem)
        {
            reusableIndex = requirements.Count;
            requirements.Add(new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "reusable_field_kit",
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            });
            source.AcceptedState["reusableToolCount"] = reusableItemCount;
        }

        int? playerItemIndex = null;
        if (includePlayerItem)
        {
            playerItemIndex = requirements.Count;
            requirements.Add(new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "antibiotic_dose",
                ["quantity"] = 1,
                ["ownerRole"] = "target"
            });
        }

        var mutations = new JsonArray();
        foreach (var resourceOrdinal in selectedResourceOrder)
        {
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = checked(resourceOrdinal + 1)
            });
        }
        if (selectReusableItem)
        {
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = Assert.IsType<int>(reusableIndex)
            });
        }
        if (selectPlayerItem)
        {
            mutations.Add(new JsonObject
            {
                ["kind"] = "consume_requirement",
                ["scope"] = "common",
                ["milestoneOrdinal"] = null,
                ["requirementIndex"] = Assert.IsType<int>(playerItemIndex)
            });
        }
        route["resourcePolicy"]!["mutations"] = mutations;
        return source with
        {
            Before = before,
            History = CreateCurrentWoundHistory(before),
            SeedCanonicalWoundEffects = true,
            OperationKey = source.OperationKey + "_resource_publication"
        };
    }

    private static ResolverScenario CreateRepeatedItemCapacityPublicationScenario()
    {
        var source = CreateGuaranteedResourcePublicationScenario(
            resourceQuantities: new[] { 2 },
            selectedResourceOrder: Array.Empty<int>(),
            includeReusableItem: true,
            reusableItemCount: 4);
        var before = source.Before.DeepClone().AsObject();
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        var requirements = route["requirements"]!.AsArray();
        var firstItemIndex = requirements
            .Select((requirement, index) => (Requirement: requirement, Index: index))
            .Single(row => row.Requirement?["kind"]?.GetValue<string>() ==
                           "item_quantity")
            .Index;
        var resourceIndex = requirements
            .Select((requirement, index) => (Requirement: requirement, Index: index))
            .Single(row => row.Requirement?["kind"]?.GetValue<string>() ==
                           "resource_quantity")
            .Index;
        var secondItemIndex = requirements.Count;
        requirements.Add(requirements[firstItemIndex]!.DeepClone());
        route["resourcePolicy"]!["mutations"] = new JsonArray(
            CreateRequirementConsumption(firstItemIndex),
            CreateRequirementConsumption(resourceIndex),
            CreateRequirementConsumption(secondItemIndex));
        return source with
        {
            Before = before,
            History = CreateCurrentWoundHistory(before),
            OperationKey = source.OperationKey + "_repeated_item_capacity"
        };
    }

    private static JsonObject CreateRequirementConsumption(int requirementIndex) =>
        new()
        {
            ["kind"] = "consume_requirement",
            ["scope"] = "common",
            ["milestoneOrdinal"] = null,
            ["requirementIndex"] = requirementIndex
        };

    private static void AssertExactOwnerExports(
        IReadOnlyList<ResourceOwnerExport> expected,
        IReadOnlyList<ResourceOwnerExport> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var expectedOwner in expected)
        {
            var actualOwner = Assert.Single(actual, owner =>
                owner.Key == expectedOwner.Key);
            Assert.Equal(expectedOwner.Lifecycle, actualOwner.Lifecycle);
            Assert.Equal(expectedOwner.SameTurn, actualOwner.SameTurn);
            Assert.Equal(expectedOwner.OwnerRef, actualOwner.OwnerRef);
            Assert.Equal(expectedOwner.BoundNpcId, actualOwner.BoundNpcId);
            Assert.Equal(
                expectedOwner.AuthorityFingerprint,
                actualOwner.AuthorityFingerprint);
            Assert.True(expectedOwner.ResourceCapabilities.SetEquals(
                actualOwner.ResourceCapabilities));
            Assert.True(expectedOwner.RealmIndependentResourceCapabilities.SetEquals(
                actualOwner.RealmIndependentResourceCapabilities));
        }
    }

    private static TreatmentFlow PersistAndRehydrateResourcePublication(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string suffix)
    {
        var initial = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_" + suffix,
            scenario.RouteId);
        PersistTreatmentCommand(
            fixture,
            ComposeTreatmentCommand(
                initial,
                "The accepted guaranteed treatment retains its exact resource hold."));
        var restored = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(fixture),
            suffix + " persisted resource publication"));
        var rehydrated = RehydratePersistedTreatment(
            fixture,
            "guaranteed",
            restored);
        Assert.Equal(CanonicalValue(initial.Request), CanonicalValue(rehydrated.Request));
        Assert.Equal(CanonicalValue(initial.Resolution), CanonicalValue(rehydrated.Resolution));
        return rehydrated;
    }

    private static WoundAcceptedTurnPlanner.MortalWoundTreatmentPublicationResult
        ComposeResourcePublicationResult(
            AcceptedStateFixture fixture,
            TreatmentFlow flow,
            GameResponse? mechanicsResponse = null) =>
        WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(
            fixture.FileSystem,
            fixture.Lease,
            mechanicsResponse ?? new GameResponse(),
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState),
            Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request),
            Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution));

    private static JsonObject ReadItemIdentityEntry(
        AcceptedStateFixture fixture,
        string itemId)
    {
        var parsed = MortalItemIdentityState.Parse(File.ReadAllText(
            fixture.FileSystem.ResolvePath(MortalItemIdentityState.StatePath)));
        Assert.Empty(parsed.Issues);
        Assert.True(parsed.EntriesByItemId.TryGetValue(itemId, out var entry));
        return Assert.IsType<JsonObject>(entry).DeepClone().AsObject();
    }

    private static int ReadIdentityTransitionCount(JsonObject entry) =>
        Assert.IsType<JsonArray>(entry["transitions"]).Count;

    private static void AssertPlanContainsExactPartialItemIdentity(
        AcceptedMechanicsPlan plan,
        JsonObject identityBefore,
        string itemId,
        int transitionCountBefore)
    {
        Assert.True(plan.OwnerCompanionAfterImages.TryGetValue(
            MortalItemIdentityState.StatePath,
            out var indexRoot));
        var parsed = MortalItemIdentityState.Parse(indexRoot);
        Assert.Empty(parsed.Issues);
        var identityAfter = parsed.EntriesByItemId[itemId];
        Assert.Equal("active", identityAfter["state"]!.GetValue<string>());
        Assert.Equal(
            identityBefore["receiptId"]!.GetValue<string>(),
            identityAfter["receiptId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(
            identityBefore["currentCarrier"],
            identityAfter["currentCarrier"]));
        var beforeTransitions = identityBefore["transitions"]!.AsArray();
        var afterTransitions = identityAfter["transitions"]!.AsArray();
        Assert.Equal(transitionCountBefore + 1, afterTransitions.Count);
        for (var index = 0; index < transitionCountBefore; index++)
        {
            Assert.True(JsonNode.DeepEquals(
                beforeTransitions[index],
                afterTransitions[index]));
        }
        var consume = Assert.IsType<JsonObject>(afterTransitions[^1]);
        Assert.Equal("consume", consume["kind"]!.GetValue<string>());
        Assert.Equal(2, consume["quantityBefore"]!.GetValue<int>());
        Assert.Equal(1, consume["quantityAfter"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(
            identityBefore["currentCarrier"],
            consume["sourceCarrier"]));
        Assert.True(JsonNode.DeepEquals(
            identityBefore["currentCarrier"],
            consume["destinationCarrier"]));
    }

    private static GameResponse CreateAllAllowedTreatmentItemResponseFieldsPresentEmpty() =>
        new()
        {
            ActiveSkillChanges = Array.Empty<JsonElement>(),
            RemoveActiveSkills = Array.Empty<string>(),
            PassiveSkillChanges = Array.Empty<JsonElement>(),
            RemovePassiveSkills = Array.Empty<string>(),
            NPCActiveSkillChanges = Array.Empty<JsonElement>(),
            NPCPassiveSkillChanges = Array.Empty<JsonElement>(),
            UpdateInventory = Array.Empty<JsonElement>(),
            MoveInventoryItems = Array.Empty<JsonElement>(),
            RemoveInventoryItems = Array.Empty<JsonElement>(),
            NPCInventoryAdds = Array.Empty<JsonElement>(),
            NPCInventoryUpdates = Array.Empty<JsonElement>(),
            NPCInventoryRemovals = Array.Empty<JsonElement>(),
            NPCEquipmentChanges = Array.Empty<JsonElement>()
        };

    private static string CaptureGameResponsePropertyGraph(GameResponse response) =>
        string.Join(
            "\n",
            typeof(GameResponse)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property =>
                    property.CanRead && property.GetIndexParameters().Length == 0)
                .OrderBy(static property => property.Name, StringComparer.Ordinal)
                .Select(property =>
                {
                    var value = property.GetValue(response);
                    return property.Name + "=" + (value is null
                        ? "<null>"
                        : JsonSerializer.Serialize(value, property.PropertyType));
                }));

    private static void AssertGameResponseHasExactlyAllowedPresentEmpty(
        GameResponse response)
    {
        foreach (var property in typeof(GameResponse).GetProperties(
                     BindingFlags.Instance | BindingFlags.Public))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
                continue;
            var value = property.GetValue(response);
            if (!TreatmentItemAllowedResponsePropertyNames.Contains(property.Name))
            {
                Assert.Null(value);
                continue;
            }
            Assert.NotNull(value);
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable>(value).Cast<object>());
        }
    }

    private static object CreateNonNullGameResponsePropertyValue(PropertyInfo property)
    {
        var type = property.PropertyType;
        var nullable = Nullable.GetUnderlyingType(type);
        if (nullable is not null)
        {
            if (nullable == typeof(int))
                return 1;
            if (nullable == typeof(double))
                return 1d;
            if (nullable == typeof(JsonElement))
                return JsonDocument.Parse("{}").RootElement.Clone();
        }
        if (type == typeof(string))
            return "unsupported_" + property.Name;
        if (type == typeof(PlayerStatus))
            return new PlayerStatus { CurrentCondition = "unsupported" };
        if (type.IsArray)
        {
            var elementType = type.GetElementType();
            Assert.NotNull(elementType);
            return Array.CreateInstance(elementType, 0);
        }
        if (type.IsGenericType &&
            type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
        {
            return Activator.CreateInstance(type)!;
        }
        throw new InvalidOperationException(
            $"No non-null GameResponse test value is defined for {property.Name}:{type}.");
    }

    private sealed record SealedFinalBaselineProbe(
        IReadOnlyDictionary<string, JsonNode?> Roots,
        JsonObject IdentityIndex,
        IReadOnlyList<string> AppliedTransformIds,
        string Fingerprint);

    private sealed record SealedTreatmentPublicationProbe(
        object OuterAuthority,
        object ItemAuthority,
        object ItemEnvelope,
        string OuterFingerprint,
        string ItemAuthorityFingerprint,
        string ItemEnvelopeFingerprint,
        string SkillAuthorityFingerprint,
        string SkillEnvelopeFingerprint,
        string NpcTradeDisposition,
        SealedFinalBaselineProbe Baseline);

    private static SealedTreatmentPublicationProbe ReadSealedTreatmentPublication(
        AcceptedMechanicsPlan plan)
    {
        var outer = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(
            plan.TreatmentResourcePublicationAuthority);
        var itemAuthorityType = RequireB4Type(
            "BookOfEternityClient.Services.MortalWoundTreatmentItemPublicationAuthority");
        var itemEnvelopeType = RequireB4Type(
            "BookOfEternityClient.Services.MortalTreatmentItemCommandEnvelope");
        var baselineType = RequireB4Type(
            "BookOfEternityClient.Services.MortalItemPublicationBaselineResult");
        var baselineInputType = RequireB4Type(
            "BookOfEternityClient.Services.MortalItemPublicationBaselineInput");
        var itemPhaseType = RequireB4Type(
            "BookOfEternityClient.Services.MortalItemCanonicalProjectionResult");
        var dispositionType = RequireB4Type(
            "BookOfEternityClient.Services.MortalItemNpcTradeTailDisposition");
        Assert.True(dispositionType.IsEnum);
        Assert.Equal(
            new[] { "Apply", "SkipUntouchedTreatmentContinuation" },
            Enum.GetNames(dispositionType));
        RequireExactProperties(
            baselineInputType,
            ("ItemPhase", itemPhaseType),
            ("Envelope", itemEnvelopeType),
            ("NpcCoreAuthority", typeof(NpcCoreChangesContract.Authority)),
            ("NpcTradePending", typeof(CanonicalBeforeImage)),
            ("TrainingPending", typeof(CanonicalBeforeImage)),
            ("NpcTradeDisposition", dispositionType),
            ("BackupRoots", typeof(IReadOnlyDictionary<string, JsonNode?>)));
        RequireExactProperties(
            baselineType,
            ("FinalCarrierRoots", typeof(IReadOnlyDictionary<string, JsonNode?>)),
            ("IdentityIndexAfterImage", typeof(JsonObject)),
            ("AppliedTransformIds", typeof(IReadOnlyList<string>)),
            ("Issues", typeof(IReadOnlyList<ValidationIssue>)),
            ("Fingerprint", typeof(string)));

        var outerGraph = EnumerateObjectGraph(outer, maximumDepth: 16).ToArray();
        var itemAuthority = Assert.Single(
            outerGraph,
            value => value.GetType() == itemAuthorityType);
        var itemGraph = EnumerateObjectGraph(itemAuthority, maximumDepth: 12).ToArray();
        Assert.Contains(itemGraph, value => value.GetType() == itemEnvelopeType);
        var itemEnvelope = ReadExactDeclaredInternalProperty(
            itemAuthority,
            itemAuthorityType,
            "ItemEnvelope");
        Assert.Equal(itemEnvelopeType, itemEnvelope.GetType());
        Assert.Contains(itemGraph, value => value.GetType() == baselineType);
        var baselineResult = ReadExactDeclaredInternalProperty(
            itemAuthority,
            itemAuthorityType,
            "Baseline");
        Assert.Equal(baselineType, baselineResult.GetType());
        var preparedField = itemAuthorityType.GetField(
            "_preparedWoundPlan",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(preparedField);
        Assert.Equal(itemAuthorityType, preparedField.DeclaringType);
        var preparedProof = Assert.IsType<WoundPreparedAcceptedTurnPlan>(
            preparedField.GetValue(itemAuthority));
        Assert.Empty(WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(
            preparedProof));
        var skillProjection = Assert.Single(
            outerGraph,
            value =>
                string.Equals(
                    value.GetType().Name,
                    "TreatmentSkillProjectionAuthority",
                    StringComparison.Ordinal) &&
                HasReadableProperty(value, "CommandEnvelopeFingerprint"));

        Assert.Contains(outerGraph, value => ReferenceEquals(value, itemAuthority));
        Assert.Contains(outerGraph, value => ReferenceEquals(value, skillProjection));
        Assert.All(
            itemGraph.Where(value => value.GetType() == itemEnvelopeType),
            nestedEnvelope => Assert.Equal(
                ReadRequiredStringProperty(itemEnvelope, "Fingerprint"),
                ReadRequiredStringProperty(nestedEnvelope, "Fingerprint")));
        var dispositions = itemGraph
            .Where(value => value.GetType() == dispositionType)
            .Select(static value => value.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var disposition = Assert.IsType<string>(Assert.Single(dispositions));

        var roots = Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonNode?>>(
            ReadRequiredProperty(baselineResult, "FinalCarrierRoots"));
        var identity = Assert.IsType<JsonObject>(ReadRequiredProperty(
            baselineResult,
            "IdentityIndexAfterImage"));
        var appliedTransformIds = Assert.IsAssignableFrom<IReadOnlyList<string>>(
            ReadRequiredProperty(baselineResult, "AppliedTransformIds"));
        var baselineFingerprint = ReadRequiredStringProperty(
            baselineResult,
            "Fingerprint");
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable>(
            ReadRequiredProperty(baselineResult, "Issues")).Cast<object>());
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            baselineFingerprint));
        var snapshotField = itemAuthorityType.GetField(
            "_snapshot",
            BindingFlags.Instance | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly);
        Assert.NotNull(snapshotField);
        Assert.Equal(itemAuthorityType, snapshotField.DeclaringType);
        var nestedBaseline = Assert.IsType<
            MortalItemAcceptedTurnNormalizationSnapshot>(
            snapshotField.GetValue(itemAuthority))
            .CloneFinalBaseline();
        Assert.NotNull(nestedBaseline);
        Assert.Equal(baselineFingerprint, nestedBaseline.Fingerprint);
        Assert.Equal(roots.Count, nestedBaseline.FinalCarrierRoots.Count);
        Assert.All(roots, pair => Assert.True(
            nestedBaseline.FinalCarrierRoots.TryGetValue(
                pair.Key,
                out var nestedRoot) &&
            JsonNode.DeepEquals(pair.Value, nestedRoot)));
        Assert.True(JsonNode.DeepEquals(
            identity,
            nestedBaseline.IdentityIndexAfterImage));
        Assert.Equal(appliedTransformIds, nestedBaseline.AppliedTransformIds);

        return new SealedTreatmentPublicationProbe(
            outer,
            itemAuthority,
            itemEnvelope,
            ReadRequiredStringProperty(outer, "AuthorityFingerprint"),
            ReadObservedAuthorityFingerprint(itemAuthority),
            ReadRequiredStringProperty(itemEnvelope, "Fingerprint"),
            ReadObservedAuthorityFingerprint(skillProjection),
            ReadRequiredStringProperty(skillProjection, "CommandEnvelopeFingerprint"),
            disposition,
            new SealedFinalBaselineProbe(
                roots,
                identity,
                appliedTransformIds,
                baselineFingerprint));
    }

    private static string ReadObservedAuthorityFingerprint(object authority)
    {
        foreach (var name in new[] { "Fingerprint", "AuthorityFingerprint" })
        {
            var property = authority.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            if (property?.GetValue(authority) is string fingerprint)
            {
                Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
                    fingerprint));
                return fingerprint;
            }
        }
        throw new InvalidOperationException(
            "The production-created treatment item authority exposes no sealed fingerprint.");
    }

    private static object ReadExactDeclaredInternalProperty(
        object instance,
        Type declaringType,
        string propertyName)
    {
        Assert.Equal(declaringType, instance.GetType());
        var property = declaringType.GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.NotNull(property);
        Assert.Equal(declaringType, property.DeclaringType);
        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod.IsAssembly);
        var value = property.GetValue(instance);
        Assert.NotNull(value);
        return value;
    }

    private static void AssertSealedFinalBaselineAuthorityInputs(
        SealedTreatmentPublicationProbe publication,
        AcceptedStateFixture fixture)
    {
        var graph = EnumerateObjectGraph(
                publication.ItemAuthority,
                maximumDepth: 12)
            .ToArray();
        Assert.NotEmpty(graph.OfType<NpcCoreChangesContract.Authority>());
        foreach (var path in new[]
                 {
                     NpcTradeRequestState.PendingRequestPath,
                     TrainingRequestState.PendingRequestPath
                 })
        {
            var expected = ReadCanonicalBytes(fixture, path);
            Assert.Contains(
                graph.OfType<CanonicalBeforeImage>(),
                before => before.Existed &&
                          before.Bytes is { } bytes &&
                          bytes.AsSpan().SequenceEqual(expected));
        }
    }

    private static bool HasReadableProperty(object value, string propertyName) =>
        value.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        is { CanRead: true };

    private static IEnumerable<object> EnumerateObjectGraph(
        object root,
        int maximumDepth)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<(object Value, int Depth)>();
        pending.Enqueue((root, 0));
        while (pending.Count != 0)
        {
            var (value, depth) = pending.Dequeue();
            if (!seen.Add(value))
                continue;
            yield return value;
            if (depth >= maximumDepth ||
                value is string or byte[] or JsonNode or Type or MemberInfo ||
                value.GetType().IsPrimitive || value is decimal)
            {
                continue;
            }
            if (value is IEnumerable sequence)
            {
                foreach (var child in sequence.Cast<object?>()
                             .Where(static child => child is not null))
                {
                    pending.Enqueue((child!, depth + 1));
                }
            }
            foreach (var field in value.GetType().GetFields(
                         BindingFlags.Instance | BindingFlags.Public |
                         BindingFlags.NonPublic))
            {
                if (field.GetValue(value) is { } child)
                    pending.Enqueue((child, depth + 1));
            }
        }
    }

    private static void AssertTreatmentItemEnvelopeHasExactNullShape(
        object envelope,
        bool expectPresentEmpty)
    {
        var expected = new[]
        {
            "UpdateInventory",
            "MoveInventoryItems",
            "RemoveInventoryItems",
            "NPCInventoryAdds",
            "NPCInventoryUpdates",
            "NPCInventoryRemovals",
            "NPCEquipmentChanges"
        };
        var properties = envelope.GetType().GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        foreach (var name in expected)
        {
            var property = Assert.Single(properties, candidate =>
                string.Equals(candidate.Name, name, StringComparison.Ordinal));
            var value = property.GetValue(envelope);
            if (!expectPresentEmpty)
            {
                Assert.Null(value);
                continue;
            }
            Assert.NotNull(value);
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable>(value).Cast<object>());
        }
        Assert.Equal(
            expected.OrderBy(static name => name, StringComparer.Ordinal),
            properties.Where(property => expected.Contains(
                    property.Name,
                    StringComparer.Ordinal))
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            ReadRequiredStringProperty(envelope, "Fingerprint")));
    }

    private static void AssertTreatmentItemEnvelopeMatchesResponse(
        object envelope,
        GameResponse response)
    {
        var envelopeProperties = envelope.GetType().GetProperties(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var responseProperties = typeof(GameResponse).GetProperties(
            BindingFlags.Instance | BindingFlags.Public);
        foreach (var name in TreatmentItemResponsePropertyNames)
        {
            var envelopeProperty = Assert.Single(envelopeProperties, property =>
                string.Equals(property.Name, name, StringComparison.Ordinal));
            var responseProperty = Assert.Single(responseProperties, property =>
                string.Equals(property.Name, name, StringComparison.Ordinal));
            var expected = responseProperty.GetValue(response);
            var actual = envelopeProperty.GetValue(envelope);
            if (expected is null)
            {
                Assert.Null(actual);
                continue;
            }
            Assert.NotNull(actual);
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable>(expected).Cast<object>());
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable>(actual).Cast<object>());
        }
    }

    private static string ComputeExpectedTreatmentSkillEnvelopeFingerprint(
        GameResponse response)
    {
        var root = new JsonObject();
        AddSkillEnvelopeArray(root, "activeSkillChanges", response.ActiveSkillChanges);
        AddSkillEnvelopeArray(root, "removeActiveSkills", response.RemoveActiveSkills);
        AddSkillEnvelopeArray(root, "passiveSkillChanges", response.PassiveSkillChanges);
        AddSkillEnvelopeArray(root, "removePassiveSkills", response.RemovePassiveSkills);
        AddSkillEnvelopeArray(root, "NPCActiveSkillChanges", response.NPCActiveSkillChanges);
        AddSkillEnvelopeArray(root, "NPCPassiveSkillChanges", response.NPCPassiveSkillChanges);
        return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.skill_command_envelope",
            "1",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(root)
        });
    }

    private static void AddSkillEnvelopeArray<T>(
        JsonObject root,
        string propertyName,
        T[]? values)
    {
        if (values is null)
            return;
        root[propertyName] = JsonSerializer.SerializeToNode(values)!.AsArray();
    }

    private static void SetTreatmentSkillResponseFields(
        GameResponse response,
        Array? value)
    {
        Assert.Null(value);
        response.ActiveSkillChanges = null;
        response.RemoveActiveSkills = null;
        response.PassiveSkillChanges = null;
        response.RemovePassiveSkills = null;
        response.NPCActiveSkillChanges = null;
        response.NPCPassiveSkillChanges = null;
    }

    private static void SetTreatmentItemResponseFields(
        GameResponse response,
        Array? value)
    {
        Assert.Null(value);
        response.UpdateInventory = null;
        response.MoveInventoryItems = null;
        response.RemoveInventoryItems = null;
        response.NPCInventoryAdds = null;
        response.NPCInventoryUpdates = null;
        response.NPCInventoryRemovals = null;
        response.NPCEquipmentChanges = null;
    }

    private static string ReadRequiredStringProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<string>(property.GetValue(value));
    }

    private static void RequireExactProperties(
        Type type,
        params (string Name, Type Type)[] expected)
    {
        var properties = type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static property =>
                property.GetMethod is { } getter &&
                (getter.IsPublic || getter.IsAssembly))
            .ToArray();
        Assert.Equal(
            expected.Select(static value => value.Name)
                .OrderBy(static name => name, StringComparer.Ordinal),
            properties.Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
        foreach (var (name, propertyType) in expected)
        {
            var property = Assert.Single(properties, candidate =>
                string.Equals(candidate.Name, name, StringComparison.Ordinal));
            Assert.Equal(propertyType, property.PropertyType);
            Assert.True(property.CanRead);
        }
    }

    private static Type RequireB4Type(string fullName)
    {
        var type = typeof(AcceptedMechanicsPlan).Assembly.GetType(fullName);
        Assert.True(type is not null, $"The frozen B.4 type '{fullName}' is absent.");
        Assert.Equal(fullName, type!.FullName);
        return type;
    }

    private static void AssertFrozenFinalBaseline(
        SealedFinalBaselineProbe baseline,
        string npcTradeDisposition,
        AcceptedMechanicsPlan plan,
        AcceptedStateFixture fixture)
    {
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            baseline.Fingerprint));
        Assert.Equal(
            ExpectedFinalBaselineTransformIds(npcTradeDisposition),
            baseline.AppliedTransformIds);
        var requiredPaths = new[]
        {
            NpcCoreChangesContract.NpcCorePath,
            InventoryEquipmentService.ItemsPath,
            "game_state/npcs/npc_inventory.json",
            MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
            StorageTransportMoveService.CurrentLocationPath,
            MortalLocationStorageContentsState.StatePath,
            StorageTransportMoveService.VehiclesPath,
            MortalItemIdentityState.StatePath,
            "game_state/inventory/item_bonds.json",
            "game_state/inventory/item_text_updates.json",
            "game_state/inventory/recipes.json",
            "game_state/npcs/item_journals.json",
            "game_state/quests/quest_history.json"
        };
        Assert.Equal(
            requiredPaths.OrderBy(static path => path, StringComparer.Ordinal),
            baseline.Roots.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        Assert.True(JsonNode.DeepEquals(
            baseline.Roots[MortalItemIdentityState.StatePath],
            baseline.IdentityIndex));

        var npcRoot = Assert.IsType<JsonObject>(
            baseline.Roots[NpcCoreChangesContract.NpcCorePath]);
        var medic = Assert.Single(
            npcRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>(),
            actor => actor["NPCId"]?.GetValue<string>() == "field_medic_01");
        Assert.Equal("Baseline worldview", medic["worldview"]!.GetValue<string>());
        Assert.DoesNotContain(NpcCoreChangesContract.PropertyName, npcRoot.Select(
            static pair => pair.Key));
        Assert.DoesNotContain(NpcTradeRequestState.UpdateReceiptsProperty, npcRoot.Select(
            static pair => pair.Key));

        var rollbackBeforeImage = plan.BeforeImages[
            NpcCoreChangesContract.NpcCorePath];
        Assert.True(rollbackBeforeImage.Existed);
        var rollbackBytes = Assert.IsType<byte[]>(rollbackBeforeImage.Bytes);
        Assert.True(rollbackBytes.AsSpan().SequenceEqual(
            ReadCanonicalBytes(fixture, NpcCoreChangesContract.NpcCorePath)));
        var rollbackNpcRoot = JsonNode.Parse(rollbackBytes.AsSpan())!.AsObject();
        Assert.True(rollbackNpcRoot.ContainsKey(NpcCoreChangesContract.PropertyName));
        Assert.False(JsonNode.DeepEquals(rollbackNpcRoot, npcRoot));
        var receipt = Assert.Single(
            medic[NpcTradeRequestState.ReceiptsProperty]!.AsArray().OfType<JsonObject>(),
            row => row["requestId"]?.GetValue<string>() == "trade_baseline_request");
        Assert.Equal("ready", receipt["status"]!.GetValue<string>());

        var npcItems = medic["inventory"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Contains(
            npcItems,
            item => item["itemId"]?.GetValue<string>() == "antibiotic_dose");
        var equippedCreation = Assert.Single(
            npcItems,
            item => item["materializationReceipt"]?["creationRef"]?.GetValue<string>() ==
                    "new_item_baseline_npc_equipped");
        var equippedItemId = equippedCreation["itemId"]!.GetValue<string>();
        Assert.Equal(
            equippedItemId,
            medic["equippedItems"]!["mainHand"]!.GetValue<string>());

        var playerItems = Assert.IsType<JsonObject>(
            baseline.Roots[InventoryEquipmentService.ItemsPath]);
        Assert.DoesNotContain(
            playerItems["items"]!.AsArray().OfType<JsonObject>(),
            item => item["itemId"]?.GetValue<string>() == "antibiotic_dose");
        var journalCreation = Assert.Single(
            playerItems["items"]!.AsArray().OfType<JsonObject>(),
            item => item["materializationReceipt"]?["creationRef"]?.GetValue<string>() ==
                    "new_item_baseline_player_journal");
        Assert.Equal(
            "Inventory journal tail preserved",
            Assert.Single(journalCreation["journalEntries"]!.AsArray())!
                .GetValue<string>());
        Assert.False(playerItems.ContainsKey("UpdateInventory"));

        var npcCommands = Assert.IsType<JsonObject>(
            baseline.Roots["game_state/npcs/npc_inventory.json"]);
        Assert.False(npcCommands.ContainsKey("NPCInventoryAdds"));
        Assert.False(npcCommands.ContainsKey("NPCEquipmentChanges"));
        Assert.False(
            Assert.IsType<JsonObject>(baseline.Roots[
                    MortalItemAcceptedTransferCatalog.PlayerRemovalPath])
                .ContainsKey("removeInventoryItems"));

        var questHistory = Assert.IsType<JsonObject>(
            baseline.Roots["game_state/quests/quest_history.json"]);
        Assert.Contains("baseline_tail_reward", questHistory.ToJsonString(),
            StringComparison.Ordinal);

        var itemBonds = Assert.IsType<JsonObject>(
            baseline.Roots["game_state/inventory/item_bonds.json"]);
        Assert.False(itemBonds.ContainsKey("itemBondLevelChanges"));
        Assert.Contains("Baseline bond tail applied", itemBonds.ToJsonString(),
            StringComparison.Ordinal);

        var itemTexts = Assert.IsType<JsonObject>(
            baseline.Roots["game_state/inventory/item_text_updates.json"]);
        Assert.False(itemTexts.ContainsKey("updateItemTextContents"));
        Assert.Contains("Baseline text tail applied", itemTexts.ToJsonString(),
            StringComparison.Ordinal);

        var itemJournals = Assert.IsType<JsonObject>(
            baseline.Roots["game_state/npcs/item_journals.json"]);
        Assert.False(itemJournals.ContainsKey("itemJournalUpdates"));
        Assert.Contains("Baseline item-journal tail applied", itemJournals.ToJsonString(),
            StringComparison.Ordinal);

        var parsedIdentity = MortalItemIdentityState.Parse(baseline.IdentityIndex);
        Assert.Empty(parsedIdentity.Issues);
        var transferred = parsedIdentity.EntriesByItemId["antibiotic_dose"];
        var transferredTransitions = transferred["transitions"]!.AsArray();
        Assert.Equal(
            "transfer",
            transferredTransitions[^1]!["kind"]!.GetValue<string>());
        foreach (var creationRef in new[]
                 {
                     "new_item_baseline_player_journal",
                     "new_item_baseline_npc_equipped"
                 })
        {
            var created = Assert.Single(
                parsedIdentity.EntriesByItemId.Values,
                entry => entry["originCreationRefs"]!.AsArray()
                    .Any(node => node?.GetValue<string>() == creationRef));
            Assert.Equal(
                "create",
                Assert.Single(created["transitions"]!.AsArray())!["kind"]!
                    .GetValue<string>());
        }

        var baselineSkill = Assert.Single(
            medic["activeSkills"]!.AsArray().OfType<JsonObject>(),
            skill => skill["skillId"]?.GetValue<string>() ==
                     "skill_field_medicine_npc_01");
        Assert.NotEqual(
            "B.2 skill projection applied after ordinary baseline",
            baselineSkill["displayName"]!.GetValue<string>());
        var finalNpc = plan.OwnerCompanionAfterImages[
            NpcCoreChangesContract.NpcCorePath];
        var finalMedic = Assert.Single(
            finalNpc["NPCsInScene"]!.AsArray().OfType<JsonObject>(),
            actor => actor["NPCId"]?.GetValue<string>() == "field_medic_01");
        var finalSkill = Assert.Single(
            finalMedic["activeSkills"]!.AsArray().OfType<JsonObject>(),
            skill => skill["skillId"]?.GetValue<string>() ==
                     "skill_field_medicine_npc_01");
        Assert.Equal(
            "B.2 skill projection applied after ordinary baseline",
            finalSkill["displayName"]!.GetValue<string>());
    }

    private static IReadOnlyList<string> ExpectedFinalBaselineTransformIds(
        string npcTradeDisposition) =>
        new[]
        {
            "quest_history:v1",
            "npc_core:v1",
            npcTradeDisposition switch
            {
                "Apply" => "npc_trade:apply:v1",
                "SkipUntouchedTreatmentContinuation" =>
                    "npc_trade:skip_untouched_treatment_continuation:v1",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(npcTradeDisposition),
                    npcTradeDisposition,
                    null)
            },
            "inventory_items_journal:v1",
            "item_bonds:v1",
            "item_text_updates:v1",
            "npc_item_journals:v1"
        };

    private static IReadOnlyList<string>
        ExpectedFinalBaselineTransformRegistryIds() =>
        new[]
        {
            "quest_history:v1",
            "npc_core:v1",
            "npc_trade:v1",
            "inventory_items_journal:v1",
            "item_bonds:v1",
            "item_text_updates:v1",
            "npc_item_journals:v1"
        };

    private static string FindRepositoryRootForB4SourceGuard()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(
                    current.FullName,
                    "BookOfEternityClient")))
            {
                return current.FullName;
            }
        }
        throw new DirectoryNotFoundException(
            "Repository root was not found for the B.4 source guard.");
    }

    private static string ExtractB4MethodSource(
        string source,
        string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing method signature '{signature}'.");
        var openingBrace = source.IndexOf('{', start);
        Assert.True(openingBrace >= 0, $"Missing method body for '{signature}'.");
        var depth = 0;
        for (var index = openingBrace; index < source.Length; index++)
        {
            depth += source[index] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0
            };
            if (depth == 0)
                return source[start..(index + 1)];
        }
        throw new InvalidOperationException(
            $"Unterminated method body for '{signature}'.");
    }

    private static string ExtractB4BracedBlockSource(
        string source,
        int afterHeader,
        string description)
    {
        var openingBrace = source.IndexOf('{', afterHeader);
        Assert.True(openingBrace >= 0, $"Missing braced body for {description}.");
        Assert.True(
            string.IsNullOrWhiteSpace(source[afterHeader..openingBrace]),
            $"{description} must own the immediately following braced body.");
        var depth = 0;
        for (var index = openingBrace; index < source.Length; index++)
        {
            depth += source[index] switch
            {
                '{' => 1,
                '}' => -1,
                _ => 0
            };
            if (depth == 0)
                return source[openingBrace..(index + 1)];
        }
        throw new InvalidOperationException($"Unterminated braced body for {description}.");
    }

    private static IReadOnlyList<string> B4ForbiddenCatalogBuilderTokens() =>
        new[]
        {
            "MortalItemRouteAuthorityCatalog.Build",
            "MortalItemAcceptedTransferCatalog.Build"
        };

    private static IReadOnlyList<int> AllB4TokenIndexes(
        string source,
        string token)
    {
        var result = new List<int>();
        for (var offset = 0; offset < source.Length;)
        {
            var index = source.IndexOf(token, offset, StringComparison.Ordinal);
            if (index < 0)
                break;
            result.Add(index);
            offset = index + token.Length;
        }
        return result;
    }

    private static void AssertB4InvocationForwards(
        string source,
        string invocationToken,
        int expectedArity,
        params string[] forwardedNames)
    {
        var invocation = ExtractB4InvocationSource(source, invocationToken);
        Assert.Equal(
            expectedArity,
            CountB4TopLevelInvocationArguments(
                invocation,
                invocationToken.Length - 1));
        foreach (var forwardedName in forwardedNames)
            Assert.Contains(forwardedName, invocation, StringComparison.Ordinal);
    }

    private static string StripB4CSharpCommentsAndLiterals(string source)
    {
        var result = source.ToCharArray();
        const int code = 0;
        const int lineComment = 1;
        const int blockComment = 2;
        const int regularString = 3;
        const int verbatimString = 4;
        const int characterLiteral = 5;
        const int rawString = 6;
        var state = code;
        var rawQuoteCount = 0;

        static void Blank(char[] characters, int index)
        {
            if (characters[index] is not ('\r' or '\n'))
                characters[index] = ' ';
        }

        for (var index = 0; index < result.Length; index++)
        {
            var current = result[index];
            var next = index + 1 < result.Length ? result[index + 1] : '\0';
            switch (state)
            {
                case code:
                    if (current == '/' && next == '/')
                    {
                        Blank(result, index);
                        Blank(result, ++index);
                        state = lineComment;
                    }
                    else if (current == '/' && next == '*')
                    {
                        Blank(result, index);
                        Blank(result, ++index);
                        state = blockComment;
                    }
                    else if (current == '@' && next == '"')
                    {
                        Blank(result, index);
                        Blank(result, ++index);
                        state = verbatimString;
                    }
                    else if (current == '@' && next == '$' &&
                             index + 2 < result.Length && result[index + 2] == '"')
                    {
                        Blank(result, index);
                        Blank(result, ++index);
                        Blank(result, ++index);
                        state = verbatimString;
                    }
                    else if (current == '"')
                    {
                        var quoteCount = 1;
                        while (index + quoteCount < result.Length &&
                               result[index + quoteCount] == '"')
                        {
                            quoteCount++;
                        }
                        if (quoteCount >= 3)
                        {
                            rawQuoteCount = quoteCount;
                            for (var offset = 0; offset < quoteCount; offset++)
                                Blank(result, index + offset);
                            index += quoteCount - 1;
                            state = rawString;
                        }
                        else
                        {
                            Blank(result, index);
                            state = regularString;
                        }
                    }
                    else if (current == '\'')
                    {
                        Blank(result, index);
                        state = characterLiteral;
                    }
                    break;

                case lineComment:
                    Blank(result, index);
                    if (current is '\r' or '\n')
                        state = code;
                    break;

                case blockComment:
                    Blank(result, index);
                    if (current == '*' && next == '/')
                    {
                        Blank(result, ++index);
                        state = code;
                    }
                    break;

                case regularString:
                case characterLiteral:
                    Blank(result, index);
                    if (current == '\\' && next != '\0')
                    {
                        Blank(result, ++index);
                    }
                    else if ((state == regularString && current == '"') ||
                             (state == characterLiteral && current == '\''))
                    {
                        state = code;
                    }
                    break;

                case verbatimString:
                    Blank(result, index);
                    if (current == '"' && next == '"')
                    {
                        Blank(result, ++index);
                    }
                    else if (current == '"')
                    {
                        state = code;
                    }
                    break;

                case rawString:
                    Blank(result, index);
                    if (current == '"')
                    {
                        var quoteCount = 1;
                        while (index + quoteCount < result.Length &&
                               result[index + quoteCount] == '"')
                        {
                            quoteCount++;
                        }
                        if (quoteCount >= rawQuoteCount)
                        {
                            for (var offset = 1; offset < rawQuoteCount; offset++)
                                Blank(result, index + offset);
                            index += rawQuoteCount - 1;
                            state = code;
                        }
                    }
                    break;
            }
        }
        return new string(result);
    }

    private static string ExtractB4InvocationSource(
        string source,
        string invocationToken)
    {
        var start = source.IndexOf(invocationToken, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing invocation '{invocationToken}'.");
        var openingParenthesis = start + invocationToken.Length - 1;
        Assert.Equal('(', source[openingParenthesis]);
        var depth = 0;
        var inString = false;
        var inCharacter = false;
        var escaped = false;
        for (var index = openingParenthesis; index < source.Length; index++)
        {
            var character = source[index];
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if ((inString || inCharacter) && character == '\\')
            {
                escaped = true;
                continue;
            }
            if (!inCharacter && character == '"')
            {
                inString = !inString;
                continue;
            }
            if (!inString && character == '\'')
            {
                inCharacter = !inCharacter;
                continue;
            }
            if (inString || inCharacter)
                continue;
            if (character == '(')
                depth++;
            else if (character == ')' && --depth == 0)
                return source[start..(index + 1)];
        }
        throw new InvalidOperationException(
            $"Unterminated invocation '{invocationToken}'.");
    }

    private static void AssertB4InvocationArity(
        string source,
        string invocationToken,
        int expectedArity)
    {
        var invocations = new List<string>();
        for (var offset = 0; offset < source.Length;)
        {
            var index = source.IndexOf(invocationToken, offset, StringComparison.Ordinal);
            if (index < 0)
                break;
            var invocation = ExtractB4InvocationSource(source[index..], invocationToken);
            invocations.Add(invocation);
            offset = index + invocation.Length;
        }
        Assert.NotEmpty(invocations);
        Assert.All(
            invocations,
            invocation => Assert.Equal(
                expectedArity,
                CountB4TopLevelInvocationArguments(
                    invocation,
                    invocationToken.Length - 1)));
    }

    private static int CountB4TopLevelInvocationArguments(
        string invocation,
        int openingParenthesis)
    {
        Assert.InRange(openingParenthesis, 0, invocation.Length - 2);
        Assert.Equal('(', invocation[openingParenthesis]);
        var arguments = invocation[(openingParenthesis + 1)..^1];
        if (string.IsNullOrWhiteSpace(arguments))
            return 0;

        var parenthesisDepth = 0;
        var bracketDepth = 0;
        var braceDepth = 0;
        var inString = false;
        var inCharacter = false;
        var escaped = false;
        var commas = 0;
        foreach (var character in arguments)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if ((inString || inCharacter) && character == '\\')
            {
                escaped = true;
                continue;
            }
            if (!inCharacter && character == '"')
            {
                inString = !inString;
                continue;
            }
            if (!inString && character == '\'')
            {
                inCharacter = !inCharacter;
                continue;
            }
            if (inString || inCharacter)
                continue;

            switch (character)
            {
                case '(':
                    parenthesisDepth++;
                    break;
                case ')':
                    parenthesisDepth--;
                    break;
                case '[':
                    bracketDepth++;
                    break;
                case ']':
                    bracketDepth--;
                    break;
                case '{':
                    braceDepth++;
                    break;
                case '}':
                    braceDepth--;
                    break;
                case ',' when parenthesisDepth == 0 && bracketDepth == 0 && braceDepth == 0:
                    commas++;
                    break;
            }
        }
        return commas + 1;
    }

    private static void AssertBaselineMatchesObservedLiveState(
        SealedFinalBaselineProbe baseline,
        FinalBaselinePublicationObserver observer)
    {
        Assert.Equal(
            baseline.Roots.Keys.OrderBy(static path => path, StringComparer.Ordinal),
            observer.Roots.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        foreach (var path in baseline.Roots.Keys)
        {
            Assert.True(
                JsonNode.DeepEquals(baseline.Roots[path], observer.Roots[path]),
                $"Observed live prepublication root '{path}' differs from the sealed baseline.");
        }
        Assert.True(JsonNode.DeepEquals(
            baseline.IdentityIndex,
            observer.IdentityIndex));
    }

    private static GameResponse CreateNpcSkillOnlyResponse(
        AcceptedStateFixture fixture)
    {
        var npcCore = ReadCanonicalObject(
            fixture,
            NpcCoreChangesContract.NpcCorePath);
        var medic = Assert.Single(
            npcCore["NPCsInScene"]!.AsArray().OfType<JsonObject>(),
            actor => actor["NPCId"]?.GetValue<string>() == "field_medic_01");
        var skill = Assert.Single(
                medic["activeSkills"]!.AsArray().OfType<JsonObject>(),
                value => value["skillId"]?.GetValue<string>() ==
                         "skill_field_medicine_npc_01")
            .DeepClone()
            .AsObject();
        skill["displayName"] = "Disposition-only NPC tail ownership";
        return new GameResponse
        {
            ActiveSkillChanges = Array.Empty<JsonElement>(),
            RemoveActiveSkills = Array.Empty<string>(),
            PassiveSkillChanges = Array.Empty<JsonElement>(),
            RemovePassiveSkills = Array.Empty<string>(),
            NPCActiveSkillChanges = new[]
            {
                JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["npcId"] = "field_medic_01",
                    ["skillChanges"] = new JsonArray(skill)
                })
            },
            NPCPassiveSkillChanges = Array.Empty<JsonElement>()
        };
    }

    private static void SeedNpcTradeTailBehaviorInput(
        AcceptedStateFixture fixture)
    {
        var npcRoot = ReadCanonicalObject(
            fixture,
            NpcCoreChangesContract.NpcCorePath);
        npcRoot[NpcTradeRequestState.UpdateReceiptsProperty] =
            new JsonArray(new JsonObject
            {
                ["requestId"] = "trade_tail_disposition_request",
                ["npcId"] = "field_medic_01",
                ["npcName"] = "Field medic",
                ["tradeCycleId"] = "trade_tail_disposition_cycle",
                ["merchantProfile"] = "GeneralGoods",
                ["status"] = "ready",
                ["itemCount"] = 0,
                ["resolvedAtTurn"] = 42,
                ["resolvedAtUtc"] = "2026-09-02T00:02:00.0000000Z"
            });
        WriteCanonicalBytes(
            fixture,
            NpcCoreChangesContract.NpcCorePath,
            Encoding.UTF8.GetBytes(npcRoot.ToJsonString()));
    }

    private static bool HasNpcTradeTailBehaviorReceipt(JsonObject npcRoot)
    {
        var medic = Assert.Single(
            npcRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>(),
            actor => actor["NPCId"]?.GetValue<string>() == "field_medic_01");
        return medic[NpcTradeRequestState.ReceiptsProperty] is JsonArray receipts &&
               receipts.OfType<JsonObject>().Any(receipt =>
                   receipt["requestId"]?.GetValue<string>() ==
                   "trade_tail_disposition_request");
    }

    private static GameResponse SeedFinalBaselineProductionInputs(
        AcceptedStateFixture fixture)
    {
        var playerItems = ReadCanonicalObject(
            fixture,
            InventoryEquipmentService.ItemsPath);
        var transferredItem = Assert.Single(
            playerItems["items"]!.AsArray().OfType<JsonObject>(),
            item => item["itemId"]?.GetValue<string>() == "antibiotic_dose")
            .DeepClone()
            .AsObject();
        var transferredItemName = transferredItem["name"]!.GetValue<string>();
        var playerCreation = MortalItemTestFixture.CreateRawRoot(
            route: "player_acquisition",
            authorityKind: "turn_outcome",
            authorityId: "turn_42",
            sourceTurn: 42,
            creationRef: "new_item_baseline_player_journal",
            materializationId: "mat_item_baseline_player_journal");
        playerCreation["journalEntries"] = new JsonArray(
            "Inventory journal tail preserved");
        playerCreation["materialization"]!["sections"]!["readableOrSentient"] =
            new JsonObject
            {
                ["state"] = "populated",
                ["reason"] = null
            };
        playerItems["UpdateInventory"] = new JsonArray(playerCreation.DeepClone());
        WriteCanonicalBytes(
            fixture,
            InventoryEquipmentService.ItemsPath,
            Encoding.UTF8.GetBytes(playerItems.ToJsonString()));

        var questHistory = ReadCanonicalObjectOrEmpty(
            fixture,
            "game_state/quests/quest_history.json");
        if (questHistory["questRewards"] is not JsonArray questRewards)
        {
            questRewards = new JsonArray();
            questHistory["questRewards"] = questRewards;
        }
        questRewards.Add(new JsonObject
        {
            ["questId"] = "baseline_tail_reward",
            ["itemsReceived"] = new JsonArray(new JsonObject
            {
                ["itemId"] = "antibiotic_dose"
            })
        });
        WriteCanonicalBytes(
            fixture,
            "game_state/quests/quest_history.json",
            Encoding.UTF8.GetBytes(questHistory.ToJsonString()));

        var itemBonds = ReadCanonicalObjectOrEmpty(
            fixture,
            "game_state/inventory/item_bonds.json");
        itemBonds["itemBondLevelChanges"] = new JsonArray(new JsonObject
        {
            ["itemId"] = "antibiotic_dose",
            ["itemName"] = transferredItemName,
            ["newBondLevel"] = 2,
            ["changeReason"] = "Baseline bond tail applied"
        });
        WriteCanonicalBytes(
            fixture,
            "game_state/inventory/item_bonds.json",
            Encoding.UTF8.GetBytes(itemBonds.ToJsonString()));

        var itemTexts = ReadCanonicalObjectOrEmpty(
            fixture,
            "game_state/inventory/item_text_updates.json");
        itemTexts["updateItemTextContents"] = new JsonArray(new JsonObject
        {
            ["itemId"] = "antibiotic_dose",
            ["itemName"] = transferredItemName,
            ["textToAppend"] = "Baseline text tail applied"
        });
        WriteCanonicalBytes(
            fixture,
            "game_state/inventory/item_text_updates.json",
            Encoding.UTF8.GetBytes(itemTexts.ToJsonString()));

        var itemJournals = ReadCanonicalObjectOrEmpty(
            fixture,
            "game_state/npcs/item_journals.json");
        itemJournals["itemJournalUpdates"] = new JsonArray(new JsonObject
        {
            ["itemId"] = "antibiotic_dose",
            ["itemName"] = transferredItemName,
            ["entryToAppend"] = "Baseline item-journal tail applied"
        });
        WriteCanonicalBytes(
            fixture,
            "game_state/npcs/item_journals.json",
            Encoding.UTF8.GetBytes(itemJournals.ToJsonString()));

        var npcCreation = MortalItemTestFixture.CreateRawRoot(
            route: "npc_acquisition",
            authorityKind: "npc_inventory_add",
            authorityId: "npc_inventory_add:42:0:field_medic_01",
            sourceTurn: 42,
            creationRef: "new_item_baseline_npc_equipped",
            materializationId: "mat_item_baseline_npc_equipped");
        npcCreation["equipmentSlot"] = "MainHand";
        npcCreation["materialization"]!["sections"]!["equipment"] =
            new JsonObject
            {
                ["state"] = "populated",
                ["reason"] = null
            };
        var npcAdds = new JsonArray(
            new JsonObject
            {
                ["NPCId"] = "field_medic_01",
                ["NPCName"] = "Field medic",
                ["item"] = npcCreation.DeepClone(),
                ["destinationContainerId"] = null
            },
            new JsonObject
            {
                ["NPCId"] = "field_medic_01",
                ["NPCName"] = "Field medic",
                ["item"] = transferredItem.DeepClone(),
                ["destinationContainerId"] = null
            });
        var equipment = new JsonArray(new JsonObject
        {
            ["NPCId"] = "field_medic_01",
            ["NPCName"] = "Field medic",
            ["action"] = "equip",
            ["itemId"] = "new_item_baseline_npc_equipped",
            ["itemName"] = "Тестовый предмет",
            ["targetSlots"] = new JsonArray("mainHand")
        });
        WriteCanonicalBytes(
            fixture,
            MortalItemAcceptedTransferCatalog.NpcCommandsPath,
            Encoding.UTF8.GetBytes(new JsonObject
            {
                ["NPCInventoryAdds"] = npcAdds.DeepClone(),
                ["NPCEquipmentChanges"] = equipment.DeepClone()
            }.ToJsonString()));
        var removals = new JsonArray(new JsonObject
        {
            ["removedItemId"] = "antibiotic_dose",
            ["currentContentsPath"] = null
        });
        WriteCanonicalBytes(
            fixture,
            MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
            Encoding.UTF8.GetBytes(new JsonObject
            {
                ["removeInventoryItems"] = removals.DeepClone()
            }.ToJsonString()));

        var npcCore = ReadCanonicalObject(
            fixture,
            NpcCoreChangesContract.NpcCorePath);
        var medic = Assert.Single(
            npcCore["NPCsInScene"]!.AsArray().OfType<JsonObject>(),
            actor => actor["NPCId"]?.GetValue<string>() == "field_medic_01");
        var skillUpdate = Assert.Single(
                medic["activeSkills"]!.AsArray().OfType<JsonObject>(),
                skill => skill["skillId"]?.GetValue<string>() ==
                         "skill_field_medicine_npc_01")
            .DeepClone()
            .AsObject();
        skillUpdate["displayName"] =
            "B.2 skill projection applied after ordinary baseline";
        npcCore[NpcCoreChangesContract.PropertyName] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "field_medic_01",
            ["reason"] = "The accepted baseline records an ordinary profile change.",
            ["profile"] = new JsonObject
            {
                ["worldview"] = "Baseline worldview"
            }
        });
        npcCore[NpcTradeRequestState.UpdateReceiptsProperty] = new JsonArray(
            CreateBaselineTradeReceipt());
        WriteCanonicalBytes(
            fixture,
            NpcCoreChangesContract.NpcCorePath,
            Encoding.UTF8.GetBytes(npcCore.ToJsonString()));

        WriteCanonicalBytes(
            fixture,
            NpcTradeRequestState.PendingRequestPath,
            Encoding.UTF8.GetBytes(new JsonObject
            {
                ["requests"] = new JsonArray(new JsonObject
                {
                    ["requestId"] = "trade_baseline_request",
                    ["npcId"] = "field_medic_01",
                    ["npcName"] = "Field medic",
                    ["merchantProfile"] = "GeneralGoods",
                    ["tradeCycleId"] = "trade_cycle_baseline",
                    ["derivedTradeSlotCount"] = 0,
                    ["createdAtTurn"] = 42,
                    ["createdAtUtc"] = "2026-09-02T00:00:00.0000000Z",
                    ["createdAtWorldDate"] = 1,
                    ["refreshAfterWorldDate"] = 2
                })
            }.ToJsonString()));
        WriteCanonicalBytes(
            fixture,
            TrainingRequestState.PendingRequestPath,
            Encoding.UTF8.GetBytes(new JsonObject
            {
                ["requests"] = new JsonArray(new JsonObject
                {
                    ["requestId"] = "training_baseline_request",
                    ["requestKind"] = "mortal_teacher_showcase",
                    ["sourceActorId"] = "field_medic_01",
                    ["sourceActorName"] = "Field medic",
                    ["sourceActorKind"] = "npc",
                    ["realm"] = "mortal_world",
                    ["createdAtTurn"] = 42,
                    ["createdAtUtc"] = "2026-09-02T00:00:00Z",
                    ["sourceActorSnapshotHash"] = null,
                    ["reason"] = "Baseline authority coverage",
                    ["details"] = new JsonObject
                    {
                        ["dedupeKey"] = "baseline"
                    }
                })
            }.ToJsonString()));

        return new GameResponse
        {
            ActiveSkillChanges = Array.Empty<JsonElement>(),
            RemoveActiveSkills = Array.Empty<string>(),
            PassiveSkillChanges = Array.Empty<JsonElement>(),
            RemovePassiveSkills = Array.Empty<string>(),
            NPCActiveSkillChanges = new[]
            {
                JsonSerializer.SerializeToElement(new JsonObject
                {
                    ["npcId"] = "field_medic_01",
                    ["skillChanges"] = new JsonArray(skillUpdate)
                })
            },
            NPCPassiveSkillChanges = Array.Empty<JsonElement>(),
            UpdateInventory = ToResponseElements(
                Assert.IsType<JsonArray>(playerItems["UpdateInventory"])),
            MoveInventoryItems = Array.Empty<JsonElement>(),
            RemoveInventoryItems = ToResponseElements(removals),
            NPCInventoryAdds = ToResponseElements(npcAdds),
            NPCInventoryUpdates = Array.Empty<JsonElement>(),
            NPCInventoryRemovals = Array.Empty<JsonElement>(),
            NPCEquipmentChanges = ToResponseElements(equipment)
        };
    }

    private static JsonElement[] ToResponseElements(JsonArray rows) =>
        rows.Select(static row => JsonSerializer.SerializeToElement(row)).ToArray();
    private static JsonObject CreateBaselineTradeReceipt() => new()
    {
        ["requestId"] = "trade_baseline_request",
        ["npcId"] = "field_medic_01",
        ["npcName"] = "Field medic",
        ["tradeCycleId"] = "trade_cycle_baseline",
        ["merchantProfile"] = "GeneralGoods",
        ["status"] = "ready",
        ["itemCount"] = 0,
        ["resolvedAtTurn"] = 42,
        ["resolvedAtUtc"] = "2026-09-02T00:01:00.0000000Z"
    };

    private static void AssertFinalBaselineDriftRejectsBeforeCacheOrWrite(
        ResolverScenario scenario,
        string axis)
    {
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.SetCanonicalPlayerEnergyForResourcePublicationTest(10);
        var response = SeedFinalBaselineProductionInputs(fixture);
        var flow = PersistAndRehydrateResourcePublication(
            fixture,
            scenario,
            "final_baseline_drift_" + axis);
        var sealedPlan = ComposeResourcePublication(fixture, flow, response);
        var sealedPublication = ReadSealedTreatmentPublication(sealedPlan);
        AssertSealedFinalBaselineAuthorityInputs(sealedPublication, fixture);
        AssertFrozenFinalBaseline(
            sealedPublication.Baseline,
            sealedPublication.NpcTradeDisposition,
            sealedPlan,
            fixture);
        AcceptedMechanicsPlanAuthority.InvalidateValidated(
            fixture.FileSystem,
            fixture.Lease);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        MutateFinalBaselineAuthorityInput(fixture, axis);
        var beforeRejectedComposition = CaptureResolverFixtureTree(fixture.Root);

        var rejected = ComposeResourcePublicationResult(fixture, flow, response);

        Assert.False(rejected.IsValid);
        Assert.Null(rejected.Plan);
        var issue = Assert.Single(rejected.Issues);
        Assert.Equal(ItemBaselineChangedCode, issue.Code);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(
            fixture.FileSystem,
            fixture.Lease));
        AssertResolverFixtureTreeUnchanged(fixture.Root, beforeRejectedComposition);
        Assert.Equal(1, fixture.ReadNpcItemCount("reusable_field_kit"));
        Assert.Equal(10, ReadPlayerEnergy(fixture));
    }

    private static void MutateFinalBaselineAuthorityInput(
        AcceptedStateFixture fixture,
        string axis)
    {
        switch (axis)
        {
            case "npc_authority":
            {
                const string path = "game_state/misc/characteristics.json";
                var physicalPath = fixture.FileSystem.ResolvePath(path);
                var root = File.Exists(physicalPath)
                    ? ReadCanonicalObject(fixture, path)
                    : new JsonObject();
                root["baselineDriftAuthority"] = 1;
                WriteCanonicalBytes(fixture, path, Encoding.UTF8.GetBytes(root.ToJsonString()));
                break;
            }
            case "npc_trade_pending_bytes":
                AppendCanonicalWhitespace(fixture, NpcTradeRequestState.PendingRequestPath);
                break;
            case "training_pending_bytes":
                AppendCanonicalWhitespace(fixture, TrainingRequestState.PendingRequestPath);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }
    }

    private static void AppendCanonicalWhitespace(
        AcceptedStateFixture fixture,
        string path)
    {
        var bytes = ReadCanonicalBytes(fixture, path);
        WriteCanonicalBytes(
            fixture,
            path,
            bytes.Concat(new[] { (byte)' ' }).ToArray());
    }

    private static JsonObject ReadCanonicalObject(
        AcceptedStateFixture fixture,
        string path) => JsonNode.Parse(ReadCanonicalBytes(fixture, path).AsSpan())!
            .AsObject();

    private static JsonObject ReadCanonicalObjectOrEmpty(
        AcceptedStateFixture fixture,
        string path)
    {
        var physicalPath = fixture.FileSystem.ResolvePath(path);
        return File.Exists(physicalPath)
            ? ReadCanonicalObject(fixture, path)
            : new JsonObject();
    }

    private static void WriteCanonicalBytes(
        AcceptedStateFixture fixture,
        string path,
        byte[] bytes) => fixture.FileSystem.WriteFileAtomicBytesAsync(
            fixture.Lease,
            path,
            bytes).GetAwaiter().GetResult();

    private static string B4Fingerprint(params string?[] values) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\0", values)))).ToLowerInvariant();

    private static int ReadPlanNpcItemCount(AcceptedMechanicsPlan plan, string itemId)
    {
        var root = plan.OwnerCompanionAfterImages[NpcCoreChangesContract.NpcCorePath];
        return root["NPCsInScene"]!.AsArray().OfType<JsonObject>()
            .SelectMany(npc => npc["inventory"]!.AsArray().OfType<JsonObject>())
            .Single(item => item["itemId"]!.GetValue<string>() == itemId)["count"]!.GetValue<int>();
    }

    private static void WriteTreatmentProjectionRootBytes(
        AcceptedStateFixture fixture,
        string path,
        byte[] bytes)
    {
        var physicalPath = fixture.FileSystem.ResolvePath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);
        File.WriteAllBytes(physicalPath, bytes);
    }

    private static void RegisterNonExactOrdinaryItemCache(
        AcceptedStateFixture fixture,
        WoundAcceptedTurnBinding binding)
    {
        var currentRoots = MortalItemCanonicalProjectionPlanner.ProjectionRootPaths
            .ToDictionary(
                static path => path,
                static _ => (JsonNode?)null,
                StringComparer.Ordinal);
        var backupRoots = MortalItemCanonicalProjectionPlanner.ProjectionRootPaths
            .ToDictionary(
                static path => path,
                static _ => (JsonNode?)null,
                StringComparer.Ordinal);
        AcceptedTurnAuthorityRegistry.RegisterMortalItemsValidated(
            fixture.FileSystem,
            fixture.Lease,
            binding.SessionId,
            binding.SnapshotToken,
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.test.non_exact_ordinary_item_cache",
                "1",
                binding.SessionId,
                binding.SnapshotToken
            }),
            Array.Empty<MortalItemAcceptedTurnAuthority.NewCandidate>(),
            Array.Empty<MortalItemAcceptedTurnAuthority.StableCandidate>(),
            Array.Empty<string>(),
            new Dictionary<string, MortalItemRouteAuthority>(StringComparer.Ordinal),
            Array.Empty<MortalItemAcceptedTransfer>(),
            currentRoots,
            backupRoots);
        Assert.True(MortalItemAcceptedTurnAuthority.HasValidatedItems(
            fixture.FileSystem,
            fixture.Lease));
    }

    private static AcceptedMechanicsPlan ComposeResourcePublication(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        GameResponse? mechanicsResponse = null)
    {
        Assert.Contains(
            "effect_wound_test_pain",
            fixture.ReadActivePlayerEffectIds());
        var result = ComposeResourcePublicationResult(
            fixture,
            flow,
            mechanicsResponse);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        Assert.Empty(result.Issues);
        return Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
    }

    private static AcceptedMechanicsPlan PeekCachedPlan(AcceptedStateFixture fixture)
    {
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out _,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        return Assert.IsType<AcceptedMechanicsPlan>(cached.Plan);
    }

    private static void AssertExactCachedPlanAndBinding(
        AcceptedStateFixture fixture,
        AcceptedMechanicsPlan expectedPlan,
        AcceptedMechanicsPlanBinding expectedBinding)
    {
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var actualBinding,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        Assert.Same(expectedPlan, cached.Plan);
        Assert.Equal(
            AcceptedMechanicsPlanFingerprints.ComputeInput(expectedBinding),
            AcceptedMechanicsPlanFingerprints.ComputeInput(actualBinding));
    }

    private static (AcceptedMechanicsPlan Plan, AcceptedMechanicsPlanBinding Binding)
        AdmitCompetingCommonPlanFromLiveState(AcceptedStateFixture fixture)
    {
        fixture.ReleaseLeaseForExternalDistribution();
        IReadOnlyList<ValidationIssue> issues;
        try
        {
            issues = new ValidationService(
                    fixture.FileSystem,
                    NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawResourceMaterializationAsync()
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }

        Assert.False(
            issues.Any(static issue => issue.Severity == IssueSeverity.Error),
            DescribeIssues(issues));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var binding,
            out var cached));
        Assert.True(cached.Success, DescribeIssues(cached.Issues));
        return (Assert.IsType<AcceptedMechanicsPlan>(cached.Plan), binding);
    }

    private static ResourcePublicationTransactionScope PublishCachedResourcePlanOpen(
        AcceptedStateFixture fixture,
        TreatmentFlow flow,
        AcceptedMechanicsPlan expectedPlan)
    {
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(
            fixture.FileSystem,
            fixture.Lease,
            out var originalBinding,
            out var cached));
        Assert.Same(expectedPlan, cached.Plan);
        fixture.ReleaseLeaseForExternalDistribution();
        AcceptedTurnCanonicalStateRefresh.Result result;
        try
        {
            result = AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                    fixture.FileSystem,
                    new CanonicalStateNormalizer(
                        fixture.FileSystem,
                        NullLogger<CanonicalStateNormalizer>.Instance),
                    new ValidationService(
                        fixture.FileSystem,
                        NullLogger<ValidationService>.Instance),
                    new Dictionary<string, string>(StringComparer.Ordinal))
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }

        var carrier = FindOpenTreatmentTransactionCarrier(result);
        var scope = carrier.TransactionToken is null
            ? null
            : new ResourcePublicationTransactionScope(
                fixture,
                expectedPlan,
                originalBinding,
                carrier.TransactionToken);
        try
        {
            Assert.True(
                carrier.CandidateProperties.Count == 1,
                "AcceptedTurnCanonicalStateRefresh.Result must carry exactly one " +
                "internal opaque open held-treatment transaction. Found: " +
                string.Join(", ", carrier.CandidateProperties.Select(static property =>
                    property.Name + ":" + property.PropertyType.FullName)));
            Assert.False(carrier.HasMultipleNonNullTokens);
            Assert.NotNull(scope);
            var transaction = scope.TransactionToken;
            Assert.Empty(result.Issues);
            var plan = Assert.IsType<AcceptedMechanicsPlan>(result.MechanicsPlan);
            Assert.Same(expectedPlan, plan);
            Assert.Equal(
                "held",
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request)
                    .ResourceAuthority.ReservationDisposition);
            Assert.Empty(transaction.GetType().GetConstructors(
                BindingFlags.Instance | BindingFlags.Public));
            var publishedTree = CaptureResolverFixtureTree(fixture.Root);
            var probe = InvokeResourcePublicationTransactionOperation(
                fixture,
                transaction,
                "probe");
            AssertConfirmedHeldTransactionProbe(flow, probe);
            AssertResolverFixtureTreeUnchanged(fixture.Root, publishedTree);
            return scope;
        }
        catch (Exception original)
        {
            try
            {
                scope?.Dispose();
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(
                    "Open treatment publication failed and compensation also failed.",
                    original,
                    cleanup);
            }
            throw;
        }
    }

    private static OpenTreatmentTransactionCarrierProbe
        FindOpenTreatmentTransactionCarrier(object result)
    {
        var transactionProperties = result.GetType()
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(static property =>
                property.PropertyType != typeof(AcceptedMechanicsPlan) &&
                property.PropertyType != typeof(IReadOnlyList<ValidationIssue>))
            .Where(static property =>
                property.Name.Contains("transaction", StringComparison.OrdinalIgnoreCase) ||
                HasTransactionLifecycle(property.PropertyType))
            .ToArray();
        var nonNullTokens = transactionProperties
            .Select(property => property.GetValue(result))
            .Where(static value => value is not null)
            .ToArray();
        return new OpenTreatmentTransactionCarrierProbe(
            transactionProperties,
            nonNullTokens.FirstOrDefault(),
            nonNullTokens.Length > 1);
    }

    private static bool HasTransactionLifecycle(Type type)
    {
        var methods = type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return methods.Any(static method => IsCompletionMethodName(method.Name)) &&
               methods.Any(static method => IsCompensationMethodName(method.Name));
    }

    private static bool IsCompletionMethodName(string name) =>
        name.Contains("complete", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("commit", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("finalize", StringComparison.OrdinalIgnoreCase);

    private static bool IsCompensationMethodName(string name) =>
        name.Contains("compensate", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("rollback", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("abort", StringComparison.OrdinalIgnoreCase);

    private static object? InvokeResourcePublicationTransactionOperation(
        AcceptedStateFixture fixture,
        object transactionToken,
        string operation)
    {
        var expectedWord = operation switch
        {
            "probe" => "Probe",
            "complete" => "Complete",
            "compensate" => "Compensate",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };
        var tokenType = transactionToken.GetType();
        var candidates = typeof(AcceptedTurnCanonicalStateRefresh)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name.Contains(expectedWord, StringComparison.Ordinal))
            .Where(method =>
                method.Name.Contains("Transaction", StringComparison.Ordinal) ||
                method.Name.Contains("Publication", StringComparison.Ordinal))
            .Where(method =>
            {
                var parameters = method.GetParameters();
                return parameters.Length == 2 &&
                       parameters[0].ParameterType == typeof(FileSystemManager) &&
                       parameters[1].ParameterType == tokenType;
            })
            .ToArray();
        var seam = Assert.Single(candidates);
        Assert.False(seam.IsPublic);

        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            object? invoked;
            try
            {
                invoked = seam.Invoke(null, new[]
                {
                    fixture.FileSystem,
                    transactionToken
                });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            return AwaitReflectedTransactionOperation(invoked);
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }
    }

    private static object? InvokeResourcePublicationTerminalRelease(
        AcceptedStateFixture fixture,
        object transactionToken,
        string reason)
    {
        var tokenType = transactionToken.GetType();
        var candidates = typeof(AcceptedTurnCanonicalStateRefresh)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Where(method => method.Name.Contains("Terminal", StringComparison.Ordinal))
            .Where(method => method.Name.Contains("Release", StringComparison.Ordinal))
            .Where(method =>
            {
                var parameters = method.GetParameters();
                return parameters.Length == 3 &&
                       parameters[0].ParameterType == typeof(FileSystemManager) &&
                       parameters[1].ParameterType == tokenType &&
                       parameters[2].ParameterType == typeof(string);
            })
            .ToArray();
        var seam = Assert.Single(candidates);
        Assert.False(seam.IsPublic);

        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            object? invoked;
            try
            {
                invoked = seam.Invoke(null, new object?[]
                {
                    fixture.FileSystem,
                    transactionToken,
                    reason
                });
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
            return AwaitReflectedTransactionOperation(invoked);
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }
    }

    private static object? AwaitReflectedTransactionOperation(object? value)
    {
        if (value is null)
            return null;
        if (value is Task task)
        {
            task.GetAwaiter().GetResult();
            return task.GetType().GetProperty("Result")?.GetValue(task);
        }
        if (value is ValueTask valueTask)
        {
            valueTask.AsTask().GetAwaiter().GetResult();
            return null;
        }
        var asTask = value.GetType().GetMethod(
            "AsTask",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        if (asTask?.Invoke(value, null) is Task reflectedTask)
        {
            reflectedTask.GetAwaiter().GetResult();
            return reflectedTask.GetType().GetProperty("Result")?.GetValue(reflectedTask);
        }
        return value;
    }

    private static object ReadRequiredTransactionProperty(object result, string name)
    {
        var property = result.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsAssignableFrom<object>(property.GetValue(result));
    }

    private static void AssertTypedTransactionSuccess(
        object? result,
        int expectedChangedCount,
        string expectedOutcome)
    {
        var typed = Assert.IsAssignableFrom<object>(result);
        var issues = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            ReadRequiredTransactionProperty(typed, "Issues")).ToArray();
        var outcome = Convert.ToString(
            ReadRequiredTransactionProperty(typed, "Outcome"));
        Assert.True(
            Assert.IsType<bool>(ReadRequiredTransactionProperty(typed, "IsValid")),
            $"transaction outcome={outcome}; issues={DescribeIssues(issues)}");
        Assert.Empty(issues);
        Assert.Equal(expectedChangedCount, Assert.IsType<int>(
            ReadRequiredTransactionProperty(typed, "ChangedCount")));
        Assert.Equal(expectedOutcome, outcome);
    }

    private static void AssertConfirmedHeldTransactionProbe(
        TreatmentFlow flow,
        object? result)
    {
        var typed = Assert.IsAssignableFrom<object>(result);
        Assert.True(Assert.IsType<bool>(ReadRequiredTransactionProperty(
            typed,
            "IsValid")));
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            ReadRequiredTransactionProperty(typed, "Issues")));
        Assert.Equal(0, Assert.IsType<int>(ReadRequiredTransactionProperty(
            typed,
            "ChangedCount")));
        Assert.Equal("ConfirmedHeld", Convert.ToString(
            ReadRequiredTransactionProperty(typed, "State")));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var finalized = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        Assert.True(finalized.IsValid, DescribeIssues(finalized.Issues));
        Assert.Equal(request.RequestFingerprint, Convert.ToString(
            ReadRequiredTransactionProperty(typed, "RequestFingerprint")));
        Assert.Equal(finalized.Finalization!.FinalizationFingerprint, Convert.ToString(
            ReadRequiredTransactionProperty(typed, "FinalizationFingerprint")));
    }

    private static void AssertResourceTransactionOperationRejected(
        AcceptedStateFixture fixture,
        object transactionToken,
        string operation,
        string expectedCode,
        string expectedOutcome)
    {
        var result = InvokeResourcePublicationTransactionOperation(
            fixture,
            transactionToken,
            operation);
        AssertTypedTransactionFailure(result, expectedCode, expectedOutcome);
    }

    private static void AssertTypedTransactionFailure(
        object? result,
        string expectedCode,
        string expectedOutcome)
    {
        var typed = Assert.IsAssignableFrom<object>(result);
        Assert.False(Assert.IsType<bool>(ReadRequiredTransactionProperty(
            typed,
            "IsValid")));
        Assert.Equal(0, Assert.IsType<int>(ReadRequiredTransactionProperty(
            typed,
            "ChangedCount")));
        var issues = Assert.IsAssignableFrom<IEnumerable<ValidationIssue>>(
            ReadRequiredTransactionProperty(typed, "Issues")).ToArray();
        Assert.Contains(issues, issue => string.Equals(
            expectedCode,
            issue.Code,
            StringComparison.Ordinal));
        Assert.Equal(expectedOutcome, Convert.ToString(
            ReadRequiredTransactionProperty(typed, "Outcome")));
    }

    private sealed record OpenTreatmentTransactionCarrierProbe(
        IReadOnlyList<PropertyInfo> CandidateProperties,
        object? TransactionToken,
        bool HasMultipleNonNullTokens);

    private static JsonObject CreateQuarantineCommandRow(
        string operationKey,
        string attemptId,
        string requestFingerprint,
        string finalSceneText)
    {
        var request = CreateQuarantineRequestCoordinates(
            operationKey,
            attemptId,
            requestFingerprint);
        return new JsonObject
        {
            ["kind"] = "accepted_transition",
            ["transitionKind"] = "treat",
            ["commandRef"] = "command_ref_" + operationKey,
            ["operationKey"] = operationKey,
            ["authority"] = new JsonObject
            {
                ["request"] = request.DeepClone()
            },
            ["result"] = new JsonObject
            {
                ["requestAuthority"] = request.DeepClone()
            },
            ["finalSceneText"] = finalSceneText
        };
    }

    private static JsonObject CreateQuarantinePendingRow(
        string operationKey,
        string attemptId,
        string requestFingerprint) => new()
    {
        ["operationKey"] = operationKey,
        ["attemptId"] = attemptId,
        ["requestFingerprint"] = requestFingerprint,
        ["request"] = CreateQuarantineRequestCoordinates(
            operationKey,
            attemptId,
            requestFingerprint)
    };

    private static JsonObject CreateQuarantineRequestCoordinates(
        string operationKey,
        string attemptId,
        string requestFingerprint) => new()
    {
        ["coordinates"] = new JsonObject
        {
            ["operationKey"] = operationKey,
            ["attemptId"] = attemptId
        },
        ["requestFingerprint"] = requestFingerprint
    };

    private sealed class ResourcePublicationTransactionScope : IDisposable
    {
        private readonly AcceptedStateFixture _fixture;
        private bool _open = true;

        internal ResourcePublicationTransactionScope(
            AcceptedStateFixture fixture,
            AcceptedMechanicsPlan plan,
            AcceptedMechanicsPlanBinding originalBinding,
            object transactionToken)
        {
            _fixture = fixture;
            Plan = plan;
            OriginalBinding = originalBinding;
            TransactionToken = transactionToken;
        }

        internal AcceptedMechanicsPlan Plan { get; }
        internal AcceptedMechanicsPlanBinding OriginalBinding { get; }
        internal object TransactionToken { get; }

        internal void CompleteAtFullPipelineEnd()
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTransactionOperation(
                    _fixture,
                    TransactionToken,
                    "complete");
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionSuccess(result, 1, "Finalized");
        }

        internal void Compensate()
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTransactionOperation(
                    _fixture,
                    TransactionToken,
                    "compensate");
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionSuccess(result, 1, "Rearmed");
            AssertExactRearmedPlan();
        }

        internal void CompensateForRepair()
        {
            Assert.True(_open);
            var result = InvokeResourcePublicationTransactionOperation(
                _fixture,
                TransactionToken,
                "compensate");
            AssertTypedTransactionSuccess(result, 1, "Rearmed");
            AssertExactRearmedPlan();
        }

        internal void CompensateWithOutcome(string expectedOutcome)
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTransactionOperation(
                    _fixture,
                    TransactionToken,
                    "compensate");
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionSuccess(result, 1, expectedOutcome);
        }

        internal void ReleaseTerminal(string reason)
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTerminalRelease(
                    _fixture,
                    TransactionToken,
                    reason);
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionSuccess(result, 1, "Released");
        }

        internal void ExpectTerminalReleaseFailure(string reason)
        {
            Assert.True(_open);
            object? result;
            try
            {
                result = InvokeResourcePublicationTerminalRelease(
                    _fixture,
                    TransactionToken,
                    reason);
            }
            finally
            {
                _open = false;
            }
            AssertTypedTransactionFailure(
                result,
                TerminalReleaseFailureCode,
                "ReleaseFailed");
        }

        public void Dispose()
        {
            if (!_open)
                return;
            var result = InvokeResourcePublicationTransactionOperation(
                _fixture,
                TransactionToken,
                "compensate");
            _open = false;
            var outcome = Convert.ToString(ReadRequiredTransactionProperty(
                Assert.IsAssignableFrom<object>(result),
                "Outcome"));
            if (string.Equals(outcome, "HeldBlocked", StringComparison.Ordinal))
            {
                AssertTypedTransactionSuccess(result, 1, "HeldBlocked");
                return;
            }
            AssertTypedTransactionSuccess(result, 1, "Rearmed");
            AssertExactRearmedPlan();
        }

        private void AssertExactRearmedPlan()
        {
            AssertExactCachedPlanAndBinding(_fixture, Plan, OriginalBinding);
        }
    }

    private static void AssertResourcePlan(
        AcceptedMechanicsPlan plan,
        decimal expectedCurrent,
        int historyBeforeCount,
        IReadOnlyList<decimal> expectedSpendQuantities)
    {
        var definitions = ParseResourceDefinitions(plan.DefinitionAfterImage);
        var state = ResourceStateContract.ParseCanonical(
            plan.StateAfterImage.ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.True(state.IsValid, DescribeIssues(state.Issues));
        var entry = Assert.Single(state.Ledger!.Entries, IsPlayerEnergy);
        Assert.Equal(expectedCurrent, entry.Current);
        var history = ResourceHistoryState.ParseCanonical(
            plan.HistoryAfterImage.ToJsonString(),
            definitions,
            allowMissingPristine: false);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Equal(
            historyBeforeCount + expectedSpendQuantities.Count,
            history.History!.Transitions.Count);
        Assert.Equal(expectedSpendQuantities,
            plan.ResourceEvents.Select(static row => row.AppliedAmount));
        Assert.All(plan.ResourceEvents, static row =>
        {
            Assert.Equal("resource_spent", row.EventKind);
            Assert.Equal(ResourceOwnerKind.Player, row.Coordinate.OwnerKind);
            Assert.Equal("player_current", row.Coordinate.ResourceOwnerId);
            Assert.Equal("energy", row.Coordinate.ResourceKey);
            Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
                row.SourceFingerprint));
        });
        Assert.Contains(ResourceMaterializationContract.StatePath, plan.TouchedPaths);
        Assert.Contains(ResourceMaterializationContract.HistoryPath, plan.TouchedPaths);
        Assert.Contains(
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            plan.TouchedPaths);
        Assert.True(plan.BeforeImages.ContainsKey(ResourceMaterializationContract.StatePath));
        Assert.True(plan.BeforeImages.ContainsKey(ResourceMaterializationContract.HistoryPath));
    }

    private static ResourceDefinitionCatalog ParseResourceDefinitions(JsonObject root)
    {
        var result = ResourceDefinitionCatalog.ParseCanonical(
            root.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(result.IsValid, DescribeIssues(result.Issues));
        return Assert.IsType<ResourceDefinitionCatalog>(result.Catalog);
    }

    private static ResourceHistoryState ReadResourceHistory(AcceptedStateFixture fixture)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.DefinitionsPath)),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeIssues(definitions.Issues));
        var history = ResourceHistoryState.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.HistoryPath)),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        return Assert.IsType<ResourceHistoryState>(history.History);
    }

    private static decimal ReadPlayerEnergy(AcceptedStateFixture fixture)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.DefinitionsPath)),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.StatePath)),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(state.IsValid, DescribeIssues(state.Issues));
        return Assert.Single(state.Ledger!.Entries, IsPlayerEnergy).Current;
    }

    private static bool IsPlayerEnergy(ResourceStateEntry entry) =>
        entry.Coordinate.OwnerKind == ResourceOwnerKind.Player &&
        string.Equals(
            entry.Coordinate.ResourceOwnerId,
            "player_current",
            StringComparison.Ordinal) &&
        string.Equals(entry.Coordinate.ResourceKey, "energy", StringComparison.Ordinal);

    private static bool IsReusableItemDurabilityState(JsonObject entry) =>
        string.Equals(
            entry["ownerKind"]?.GetValue<string>(),
            "item",
            StringComparison.Ordinal) &&
        string.Equals(
            entry["resourceOwnerId"]?.GetValue<string>(),
            "reusable_field_kit",
            StringComparison.Ordinal) &&
        string.Equals(
            entry["resourceKey"]?.GetValue<string>(),
            "durability",
            StringComparison.Ordinal);

    private static ResourceStateEntry ReadReusableItemDurability(
        AcceptedStateFixture fixture)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.DefinitionsPath)),
            allowMissingPristine: false);
        Assert.True(definitions.IsValid, DescribeIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(
            File.ReadAllText(fixture.FileSystem.ResolvePath(
                ResourceMaterializationContract.StatePath)),
            definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(state.IsValid, DescribeIssues(state.Issues));
        return Assert.Single(state.Ledger!.Entries, static entry =>
            entry.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
            string.Equals(
                entry.Coordinate.ResourceOwnerId,
                "reusable_field_kit",
                StringComparison.Ordinal) &&
            string.Equals(
                entry.Coordinate.ResourceKey,
                "durability",
                StringComparison.Ordinal));
    }

    private static int CountReusableItemDurabilityReconfigures(
        AcceptedStateFixture fixture) => ReadResourceHistory(fixture).Transitions.Count(
        static transition =>
            transition.Operation == ResourceTransitionOperation.Reconfigure &&
            transition.Coordinate.OwnerKind == ResourceOwnerKind.Item &&
            string.Equals(
                transition.Coordinate.ResourceOwnerId,
                "reusable_field_kit",
                StringComparison.Ordinal) &&
            string.Equals(
                transition.Coordinate.ResourceKey,
                "durability",
                StringComparison.Ordinal));

    private static IReadOnlyList<ResourceTransition> ReadTreatmentResourceSpendTransitions(
        AcceptedStateFixture fixture) => ReadResourceHistory(fixture).Transitions
        .Where(static transition =>
            transition.Operation == ResourceTransitionOperation.Spend &&
            transition.Phase == ResourceMutationPhase.RegisteredSystemOutcome &&
            transition.Coordinate.OwnerKind == ResourceOwnerKind.Player &&
            string.Equals(
                transition.Coordinate.ResourceOwnerId,
                "player_current",
                StringComparison.Ordinal) &&
            string.Equals(
                transition.Coordinate.ResourceKey,
                "energy",
                StringComparison.Ordinal))
        .ToArray();

    private static void WriteCanonicalPlayerEnergyAuthority(
        FileSystemManager fileSystem,
        int current)
    {
        const string fingerprintA =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string fingerprintB =
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("energy", out var definition));
        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Player,
            "player_current",
            "energy");
        var binding = new ResourceCapacityBinding(
            definition!.CapacityPolicy.Kind,
            definition.CapacityPolicy.FormulaKey!,
            fingerprintA);
        var initialized = new ResourceStateSnapshot(
            10,
            10,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            "transition_energy_initialize",
            "operation_energy_initialize",
            "turn_1:resource:1",
            "bootstrap_materialization",
            "mortal_incarnation_1",
            ResourceMutationPhase.RegisteredSystemOutcome,
            40,
            0,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0,
            0,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            initialized,
            new ResourceSourceEvidence(
                "bootstrap_materialization",
                "mortal_incarnation_1",
                fingerprintA),
            fingerprintA,
            null,
            1);
        var transitions = new List<ResourceTransition> { initialize };
        ResourceTransition? latest = null;
        if (current != 10)
        {
            latest = new ResourceTransition(
                "transition_energy_spend",
                "operation_energy_spend",
                "turn_2:resource:1",
                "action_cost",
                "wound_fixture_energy",
                ResourceMutationPhase.DirectOutcome,
                100,
                0,
                coordinate,
                ResourceTransitionOperation.Spend,
                10 - current,
                10 - current,
                ResourceTransitionOutcome.Applied,
                null,
                initialized,
                initialized with { Current = current },
                new ResourceSourceEvidence(
                    "action_cost",
                    "wound_fixture_energy",
                    fingerprintB),
                fingerprintB,
                null,
                2);
            transitions.Add(latest);
        }
        var historyResult = ResourceHistoryState.CreateValidated(
            transitions,
            definitions);
        Assert.True(historyResult.IsValid, DescribeIssues(historyResult.Issues));
        var state = new ResourceStateLedger(new[]
        {
            new ResourceStateEntry(
                coordinate,
                current,
                10,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    1,
                    initialize.EventRef,
                    (latest ?? initialize).TransitionId,
                    (latest ?? initialize).EventRef,
                    (latest ?? initialize).Turn))
        });
        Assert.Empty(historyResult.History!.ValidateStateAgreement(state));
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.DefinitionsPath),
            definitions.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.StatePath),
            state.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.HistoryPath),
            historyResult.History.ToCanonicalJson());
        var composed = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions,
                path => Task.FromResult<string?>(File.Exists(fileSystem.ResolvePath(path))
                    ? File.ReadAllText(fileSystem.ResolvePath(path))
                    : null),
                state,
                historyResult.History,
                CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap)
            .GetAwaiter()
            .GetResult();
        Assert.True(composed.IsValid, DescribeIssues(composed.Issues));
        Assert.False(string.IsNullOrWhiteSpace(composed.CanonicalAuthorityJson));
        File.WriteAllText(
            fileSystem.ResolvePath(CanonicalResourceOwnerAuthorityComposer.AuthorityPath),
            composed.CanonicalAuthorityJson);
    }

    private static void WriteCanonicalReusableItemDurabilityAuthority(
        FileSystemManager fileSystem,
        decimal current,
        decimal maximum)
    {
        const string itemId = "reusable_field_kit";
        const string fingerprint =
            "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        Assert.Equal(maximum, current);
        var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
            File.ReadAllText(fileSystem.ResolvePath(
                ResourceMaterializationContract.DefinitionsPath)),
            allowMissingPristine: false);
        Assert.True(definitionsResult.IsValid, DescribeIssues(definitionsResult.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(
            definitionsResult.Catalog);
        Assert.True(definitions.TryResolveExact("durability", out var definition));
        Assert.Equal(ResourceCapacityKind.InstanceFixed,
            definition!.CapacityPolicy.Kind);
        var stateResult = ResourceStateContract.ParseCanonical(
            File.ReadAllText(fileSystem.ResolvePath(
                ResourceMaterializationContract.StatePath)),
            definitions,
            allowMissingPristine: false);
        Assert.True(stateResult.IsValid, DescribeIssues(stateResult.Issues));
        var historyResult = ResourceHistoryState.ParseCanonical(
            File.ReadAllText(fileSystem.ResolvePath(
                ResourceMaterializationContract.HistoryPath)),
            definitions,
            allowMissingPristine: false);
        Assert.True(historyResult.IsValid, DescribeIssues(historyResult.Issues));

        var coordinate = new ResourceCoordinate(
            "mortal_world",
            ResourceOwnerKind.Item,
            itemId,
            "durability");
        var binding = new ResourceCapacityBinding(
            ResourceCapacityKind.InstanceFixed,
            itemId,
            fingerprint);
        var initialized = new ResourceStateSnapshot(
            current,
            maximum,
            binding,
            ResourceLifecycleState.Active);
        var initialize = new ResourceTransition(
            "transition_reusable_field_kit_durability_initialize",
            "operation_reusable_field_kit_durability_initialize",
            "turn_3:resource:reusable_field_kit:durability",
            "bootstrap_materialization",
            "reusable_field_kit_durability_fixture",
            ResourceMutationPhase.RegisteredSystemOutcome,
            40,
            0,
            coordinate,
            ResourceTransitionOperation.Initialize,
            0,
            0,
            ResourceTransitionOutcome.Applied,
            ResourceCapacityDisposition.InitializeFromDefinition,
            null,
            initialized,
            new ResourceSourceEvidence(
                "bootstrap_materialization",
                "reusable_field_kit_durability_fixture",
                fingerprint),
            fingerprint,
            null,
            3);
        var history = ResourceHistoryState.CreateValidated(
            historyResult.History!.Transitions.Append(initialize),
            definitions);
        Assert.True(history.IsValid, DescribeIssues(history.Issues));
        var state = new ResourceStateLedger(
            stateResult.Ledger!.Entries.Append(new ResourceStateEntry(
                coordinate,
                current,
                maximum,
                binding,
                ResourceLifecycleState.Active,
                new ResourceChronology(
                    3,
                    initialize.EventRef,
                    initialize.TransitionId,
                    initialize.EventRef,
                    3))));
        Assert.Empty(history.History!.ValidateStateAgreement(state));
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.StatePath),
            state.ToCanonicalJson());
        File.WriteAllText(
            fileSystem.ResolvePath(ResourceMaterializationContract.HistoryPath),
            history.History.ToCanonicalJson());
        var owners = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions,
                path => Task.FromResult<string?>(
                    File.Exists(fileSystem.ResolvePath(path))
                        ? File.ReadAllText(fileSystem.ResolvePath(path))
                        : null),
                state,
                history.History,
                CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap)
            .GetAwaiter()
            .GetResult();
        Assert.True(owners.IsValid, DescribeIssues(owners.Issues));
        Assert.False(string.IsNullOrWhiteSpace(owners.CanonicalAuthorityJson));
        File.WriteAllText(
            fileSystem.ResolvePath(CanonicalResourceOwnerAuthorityComposer.AuthorityPath),
            owners.CanonicalAuthorityJson);
    }

    private sealed partial class AcceptedStateFixture
    {
        internal AcceptedStateFixture AttachColdRootWithUnprovenancedTreatmentContext(
            string root,
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease lease)
        {
            var current = Assert.IsType<MortalWoundTreatmentAuthority.Context>(
                TreatmentContext);
            var unavailable = new MortalWoundTreatmentAuthority.Context(
                current.SchemaVersion,
                current.Realm,
                current.TargetKind,
                current.TargetId,
                current.ProviderKind,
                current.ProviderId,
                current.CurrentLocationId)
            {
                SourcePath = current.SourcePath
            };
            Assert.False(unavailable.HasValidParserProvenance());
            return new AcceptedStateFixture(
                root,
                fileSystem,
                lease,
                unavailable,
                WoundId,
                TargetKind,
                TargetId,
                TargetCarrierPath);
        }

        internal bool HasParserProvenancedTreatmentContext() =>
            Assert.IsType<MortalWoundTreatmentAuthority.Context>(TreatmentContext)
                .HasValidParserProvenance();

        internal bool SharesTreatmentContextWith(AcceptedStateFixture other) =>
            ReferenceEquals(TreatmentContext, other.TreatmentContext);

        internal void SetCanonicalPlayerEnergyForResourcePublicationTest(int current)
        {
            WriteCanonicalPlayerEnergyAuthority(FileSystem, current);
            PrepareFreshSnapshot("t070b_energy_" + current);
        }

        internal void SetCanonicalReusableItemDurabilityForResourcePublicationTest(
            decimal current,
            decimal maximum)
        {
            WriteCanonicalReusableItemDurabilityAuthority(
                FileSystem,
                current,
                maximum);
            PrepareFreshSnapshot("t070b_item_durability");
        }

        internal void EnsureRawResourceMaterializationSnapshotCoverage()
        {
            var missingRoots = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                [WoundCarrierCatalog.NpcPath] = new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["entries"] = new JsonArray()
                },
                [WoundCarrierCatalog.EnemiesPath] = new JsonObject
                {
                    ["enemiesData"] = new JsonArray()
                },
                [WoundCarrierCatalog.AlliesPath] = new JsonObject
                {
                    ["alliesData"] = new JsonArray()
                },
                [WoundCarrierCatalog.AfterlifeProfilesPath] = new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["profiles"] = new JsonArray()
                },
                [MortalWoundOccurrenceState.StatePath] = new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["occurrences"] = new JsonArray()
                },
                [MortalWoundOpportunityReceiptState.StatePath] = new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["nextOrdinal"] = 1,
                    ["receipts"] = new JsonArray()
                }
            };
            foreach (var pair in missingRoots)
            {
                if (!File.Exists(FileSystem.ResolvePath(pair.Key)))
                    WriteObject(pair.Key, pair.Value);
            }
            PrepareFreshSnapshot("t070b_raw_resource_coverage");
        }
    }

    private static byte[] ReadCanonicalBytes(
        AcceptedStateFixture fixture,
        string path) => File.ReadAllBytes(fixture.FileSystem.ResolvePath(path));

    private static JsonElement? ReadOptionalDurableTreatmentRoot(
        AcceptedStateFixture fixture,
        string path)
    {
        var physicalPath = fixture.FileSystem.ResolvePath(path);
        if (!File.Exists(physicalPath))
            return null;
        using var document = JsonDocument.Parse(File.ReadAllText(physicalPath));
        return document.RootElement.Clone();
    }

    private static IReadOnlyDictionary<string, byte[]> CaptureItemCarrierBytes(
        AcceptedStateFixture fixture) => new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        [InventoryEquipmentService.ItemsPath] = ReadCanonicalBytes(
            fixture,
            InventoryEquipmentService.ItemsPath),
        ["game_state/npcs/npc_core.json"] = ReadCanonicalBytes(
            fixture,
            "game_state/npcs/npc_core.json"),
        ["game_state/npcs/npc_inventory.json"] = ReadCanonicalBytes(
            fixture,
            "game_state/npcs/npc_inventory.json"),
        ["game_state/inventory/item_identity_index.json"] = ReadCanonicalBytes(
            fixture,
            "game_state/inventory/item_identity_index.json")
    };

    private static void AssertItemCarrierBytesEqual(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, byte[]> expected)
    {
        foreach (var pair in expected)
            Assert.Equal(pair.Value, ReadCanonicalBytes(fixture, pair.Key));
    }

    private static IReadOnlyDictionary<string, CanonicalBeforeImage>
        CaptureDurableTreatmentSurfaceBytes(AcceptedStateFixture fixture) =>
        new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
        {
            [AcceptedMechanicsPlan.WoundCommandPath] = CaptureCanonicalBeforeImage(
                fixture,
                AcceptedMechanicsPlan.WoundCommandPath),
            [WoundAcceptedTurnSnapshotContract.PendingResolutionPath] =
                CaptureCanonicalBeforeImage(
                    fixture,
                    WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
        };

    private static CanonicalBeforeImage CaptureCanonicalBeforeImage(
        AcceptedStateFixture fixture,
        string path)
    {
        var fullPath = fixture.FileSystem.ResolvePath(path);
        return File.Exists(fullPath)
            ? new CanonicalBeforeImage(existed: true, File.ReadAllBytes(fullPath))
            : new CanonicalBeforeImage(existed: false, bytes: null);
    }

    private static void AssertDurableTreatmentSurfaceBytesEqual(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, CanonicalBeforeImage> expected)
    {
        foreach (var pair in expected)
        {
            var actual = CaptureCanonicalBeforeImage(fixture, pair.Key);
            Assert.Equal(pair.Value.Existed, actual.Existed);
            Assert.Equal(pair.Value.Bytes, actual.Bytes);
        }
    }

    private static void RemoveDurableTreatmentSurfaces(
        AcceptedStateFixture fixture,
        IEnumerable<string> paths)
    {
        foreach (var path in paths)
            fixture.FileSystem.DeleteFile(fixture.Lease, path);
    }

    private static void RestoreDurableTreatmentSurfaceBytes(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, CanonicalBeforeImage> expected)
    {
        foreach (var pair in expected)
        {
            if (pair.Value.Existed)
            {
                fixture.FileSystem.WriteFileAtomicBytesAsync(
                        fixture.Lease,
                        pair.Key,
                        Assert.IsType<byte[]>(pair.Value.Bytes))
                    .GetAwaiter()
                    .GetResult();
            }
            else
            {
                fixture.FileSystem.DeleteFile(fixture.Lease, pair.Key);
            }
        }
    }

    private static void AssertDurableTreatmentSurfacesContainRequest(
        IReadOnlyDictionary<string, CanonicalBeforeImage> surfaces,
        MortalWoundTreatmentAttemptRequest request)
    {
        var commandImage = surfaces[AcceptedMechanicsPlan.WoundCommandPath];
        Assert.True(commandImage.Existed);
        Assert.NotNull(commandImage.Bytes);
        var command = System.Text.Encoding.UTF8.GetString(
            commandImage.Bytes);
        Assert.Contains(request.RequestFingerprint, command, StringComparison.Ordinal);
        Assert.Contains(
            request.Coordinates.OperationKey,
            command,
            StringComparison.Ordinal);

        var pendingImage =
            surfaces[WoundAcceptedTurnSnapshotContract.PendingResolutionPath];
        if (!pendingImage.Existed)
        {
            Assert.Null(pendingImage.Bytes);
            return;
        }
        Assert.NotNull(pendingImage.Bytes);
        var pending = System.Text.Encoding.UTF8.GetString(pendingImage.Bytes);
        Assert.Equal(
            pending.Contains(request.RequestFingerprint, StringComparison.Ordinal),
            pending.Contains(request.Coordinates.OperationKey, StringComparison.Ordinal));
    }

    private static void AssertDurableTreatmentSurfacesQuarantined(
        AcceptedStateFixture fixture,
        MortalWoundTreatmentAttemptRequest request)
    {
        foreach (var relativePath in new[]
                 {
                     AcceptedMechanicsPlan.WoundCommandPath,
                     WoundAcceptedTurnSnapshotContract.PendingResolutionPath
                 })
        {
            var path = fixture.FileSystem.ResolvePath(relativePath);
            if (!File.Exists(path))
                continue;
            var text = File.ReadAllText(path);
            Assert.DoesNotContain(
                request.RequestFingerprint,
                text,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                request.Coordinates.OperationKey,
                text,
                StringComparison.Ordinal);
        }
    }

    private static void AssertTreatmentHoldBlocksCompetingReservation(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string suffix)
    {
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            fixture.ReadCurrentHistory(),
            fixture.ReadCurrentWound(),
            scenario.OperationKey + "_" + suffix,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.False(competing.IsValid);
        Assert.Null(competing.Request);
        Assert.Equal(
            "mortal_wound_treatment_resource_reservation_overbooked",
            Assert.Single(competing.Issues).Code);
    }

    private static void AssertTreatmentHoldReleasedAllowsCompetingReservation(
        AcceptedStateFixture fixture,
        ResolverScenario scenario,
        string suffix)
    {
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var competing = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(
            acceptedState,
            fixture.ReadCurrentHistory(),
            fixture.ReadCurrentWound(),
            scenario.OperationKey + "_" + suffix,
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(competing.IsValid, DescribeIssues(competing.Issues));
        Assert.Empty(competing.Issues);
        Assert.NotNull(competing.Request);
    }

    private static void AssertTreatmentReservationReleased(
        AcceptedStateFixture fixture,
        MortalWoundTreatmentAttemptRequest request,
        string reason)
    {
        var agreements = ReadTreatmentResourceAgreements(fixture);
        Assert.False(agreements.ContainsKey(request.Coordinates.OperationKey));
        var tombstone = ReadTreatmentResourceReleaseTombstone(
            fixture,
            request.Coordinates.OperationKey);
        Assert.Equal(
            request.RequestFingerprint,
            ReadRequiredProperty(tombstone, "RequestFingerprint"));
        Assert.Equal(
            ComputeTreatmentResourceAgreementFingerprint(request),
            ReadRequiredProperty(tombstone, "AgreementFingerprint"));
        Assert.Equal(reason, ReadRequiredProperty(tombstone, "Reason"));
    }

    private static object ReadTreatmentResourceReleaseTombstone(
        AcceptedStateFixture fixture,
        string operationKey)
    {
        var registry = ReadTreatmentResourceReservationRegistry(fixture);
        var released = Assert.IsAssignableFrom<System.Collections.IDictionary>(
            registry.GetType().GetField(
                    "_releasedByOperationKey",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(registry));
        Assert.True(released.Contains(operationKey));
        return Assert.IsAssignableFrom<object>(released[operationKey]);
    }

    private static string ComputeTreatmentResourceAgreementFingerprint(
        MortalWoundTreatmentAttemptRequest request)
    {
        var method = typeof(MortalWoundTreatmentResourceReservationAgreement)
            .GetMethod(
                "ComputeAgreementFingerprint",
                BindingFlags.Static | BindingFlags.NonPublic);
        return Assert.IsType<string>(method?.Invoke(
            null,
            new object[]
            {
                request.Coordinates,
                request.Mode,
                request.ResourceAuthority
            }));
    }

    private static void AssertConfirmedHeldResourceAgreement(
        AcceptedStateFixture fixture,
        object requestCandidate)
    {
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            requestCandidate);
        var getState = typeof(AcceptedTurnAuthorityRegistry).GetMethod(
            "GetState",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(FileSystemManager),
                typeof(FileSystemManager.CanonicalWriteLease)
            },
            modifiers: null);
        var state = Assert.IsAssignableFrom<object>(getState?.Invoke(
            null,
            new object[] { fixture.FileSystem, fixture.Lease }));
        var registry = Assert.IsType<MortalWoundTreatmentResourceReservationRegistry>(
            state.GetType().GetField(
                    "_treatmentResources",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(state));
        var agreements = ReadTreatmentResourceAgreements(registry);
        Assert.True(agreements.TryGetValue(
            request.Coordinates.OperationKey,
            out var agreement));
        Assert.Equal(
            MortalWoundTreatmentResourceReservationState.ConfirmedHeld,
            agreement.State);
        Assert.Equal(request.RequestFingerprint, agreement.PersistedRequestFingerprint);
        Assert.Equal(
            request.ResourceAuthority.AuthorityFingerprint,
            agreement.AuthorityFingerprint);
    }

    private static IReadOnlyDictionary<
        string,
        MortalWoundTreatmentResourceReservationAgreement>
        ReadTreatmentResourceAgreements(AcceptedStateFixture fixture)
        => ReadTreatmentResourceAgreements(
            ReadTreatmentResourceReservationRegistry(fixture));

    private static MortalWoundTreatmentResourceReservationRegistry
        ReadTreatmentResourceReservationRegistry(AcceptedStateFixture fixture)
    {
        var getState = typeof(AcceptedTurnAuthorityRegistry).GetMethod(
            "GetState",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(FileSystemManager),
                typeof(FileSystemManager.CanonicalWriteLease)
            },
            modifiers: null);
        var state = Assert.IsAssignableFrom<object>(getState?.Invoke(
            null,
            new object[] { fixture.FileSystem, fixture.Lease }));
        return Assert.IsType<MortalWoundTreatmentResourceReservationRegistry>(
            state.GetType().GetField(
                    "_treatmentResources",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(state));
    }

    private static IReadOnlyDictionary<
        string,
        MortalWoundTreatmentResourceReservationAgreement>
        ReadTreatmentResourceAgreements(
            MortalWoundTreatmentResourceReservationRegistry registry) =>
        Assert.IsAssignableFrom<IReadOnlyDictionary<
            string,
            MortalWoundTreatmentResourceReservationAgreement>>(
            registry.GetType().GetField(
                    "_byOperationKey",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(registry));

    private static void AssertPublicationBlocked(
        AcceptedStateFixture fixture,
        TreatmentFlow flow)
    {
        var rebound = new TreatmentFlow(
            fixture.GetAcceptedState(),
            flow.Request,
            flow.Resolution,
            flow.Before,
            flow.History);
        var blocked = ComposeResourcePublicationResult(fixture, rebound);
        Assert.False(blocked.IsValid);
        Assert.Null(blocked.Plan);
        Assert.Equal(PublicationBlockedCode, Assert.Single(blocked.Issues).Code);
    }

    private static void AssertCanonicalTreeEqualExceptDurableTreatmentSurfaces(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, byte[]> before)
    {
        var after = CaptureResolverFixtureTree(fixture.Root);
        var excluded = new[]
            {
                AcceptedMechanicsPlan.WoundCommandPath,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath
            }
            .Select(path => Path.GetRelativePath(
                    fixture.Root,
                    fixture.FileSystem.ResolvePath(path))
                .Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            before.Keys.Where(path => !excluded.Contains(path))
                .OrderBy(static path => path, StringComparer.Ordinal),
            after.Keys.Where(path => !excluded.Contains(path))
                .OrderBy(static path => path, StringComparer.Ordinal));
        foreach (var pair in before)
        {
            if (excluded.Contains(pair.Key))
                continue;
            Assert.True(after.TryGetValue(pair.Key, out var bytes), pair.Key);
            Assert.True(pair.Value.AsSpan().SequenceEqual(bytes), pair.Key);
        }
    }

    private static void RestoreResolverFixtureTree(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, byte[]> before)
    {
        var root = fixture.Root;
        var current = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path)
                .StartsWith(".boe_runtime", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                static path => path,
                StringComparer.Ordinal);
        foreach (var extra in current.Where(pair => !before.ContainsKey(pair.Key)))
        {
            fixture.FileSystem.DeleteFile(
                fixture.Lease,
                ResolverFixtureManagerPath(extra.Key));
        }
        foreach (var pair in before)
        {
            var path = Path.Combine(
                root,
                pair.Key.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path) &&
                File.ReadAllBytes(path).AsSpan().SequenceEqual(pair.Value))
            {
                continue;
            }
            fixture.FileSystem.WriteFileAtomicBytesAsync(
                    fixture.Lease,
                    ResolverFixtureManagerPath(pair.Key),
                    pair.Value)
                .GetAwaiter()
                .GetResult();
        }
    }

    private static string ResolverFixtureManagerPath(string capturedPath)
    {
        const string sessionPrefix = "game_session/";
        Assert.StartsWith(sessionPrefix, capturedPath, StringComparison.Ordinal);
        return capturedPath[sessionPrefix.Length..];
    }

    private static IReadOnlyDictionary<string, byte[]> CaptureResourceBytes(
        AcceptedStateFixture fixture) => new Dictionary<string, byte[]>(StringComparer.Ordinal)
    {
        [ResourceMaterializationContract.DefinitionsPath] = ReadCanonicalBytes(
            fixture,
            ResourceMaterializationContract.DefinitionsPath),
        [ResourceMaterializationContract.StatePath] = ReadCanonicalBytes(
            fixture,
            ResourceMaterializationContract.StatePath),
        [ResourceMaterializationContract.HistoryPath] = ReadCanonicalBytes(
            fixture,
            ResourceMaterializationContract.HistoryPath),
        [CanonicalResourceOwnerAuthorityComposer.AuthorityPath] = ReadCanonicalBytes(
            fixture,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath)
    };

    private static void AssertResourceBytesEqual(
        AcceptedStateFixture fixture,
        IReadOnlyDictionary<string, byte[]> expected)
    {
        foreach (var pair in expected)
            Assert.Equal(pair.Value, ReadCanonicalBytes(fixture, pair.Key));
    }

    private sealed class FinalBaselinePublicationObserver
    {
        private readonly object _gate = new();
        private FileSystemManager? _fileSystem;
        private IReadOnlyDictionary<string, JsonNode?>? _expectedRoots;
        private JsonObject? _expectedIdentity;

        internal bool Matched { get; private set; }
        internal IReadOnlyDictionary<string, JsonNode?> Roots { get; private set; } =
            new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        internal JsonObject? IdentityIndex { get; private set; }

        internal void Arm(
            FileSystemManager fileSystem,
            SealedFinalBaselineProbe baseline)
        {
            _fileSystem = fileSystem;
            _expectedRoots = baseline.Roots.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value?.DeepClone(),
                StringComparer.Ordinal);
            _expectedIdentity = baseline.IdentityIndex.DeepClone().AsObject();
            Assert.False(TryCaptureExactLiveBaseline());
        }

        internal Task BeforeMutationAsync(string _)
        {
            lock (_gate)
            {
                if (!Matched)
                    Matched = TryCaptureExactLiveBaseline();
            }
            return Task.CompletedTask;
        }

        internal string DescribeFailure() =>
            "The normalizer never exposed the exact production-sealed ordinary " +
            "carrier/index baseline before common treatment publication.";

        private bool TryCaptureExactLiveBaseline()
        {
            if (_fileSystem is null ||
                _expectedRoots is null ||
                _expectedIdentity is null)
            {
                return false;
            }
            var actual = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
            try
            {
                foreach (var pair in _expectedRoots)
                {
                    var physical = _fileSystem.ResolvePath(pair.Key);
                    if (pair.Value is null)
                    {
                        if (File.Exists(physical))
                            return false;
                        actual.Add(pair.Key, null);
                        continue;
                    }
                    if (!File.Exists(physical)) return false;
                    var root = ParseCanonicalBytes(File.ReadAllBytes(physical));
                    if (!JsonNode.DeepEquals(pair.Value, root))
                        return false;
                    actual.Add(pair.Key, root);
                }
                var identityPhysical = _fileSystem.ResolvePath(
                    MortalItemIdentityState.StatePath);
                if (!File.Exists(identityPhysical))
                    return false;
                var identity = ParseCanonicalBytes(
                        File.ReadAllBytes(identityPhysical))
                    .AsObject();
                if (!JsonNode.DeepEquals(_expectedIdentity, identity))
                    return false;
                Roots = actual;
                IdentityIndex = identity;
                return true;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or
                    JsonException or InvalidOperationException)
            {
                return false;
            }
        }

        private static JsonNode ParseCanonicalBytes(byte[] bytes)
        {
            var preamble = Encoding.UTF8.GetPreamble();
            var offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
            return JsonNode.Parse(bytes.AsSpan(offset))!;
        }
    }

    private sealed class TerminalDurableSurfaceRemovalObserver
    {
        private string? _commandPath;
        private string? _pendingPath;
        private string? _requestFingerprint;

        internal bool ObservedCommandRemoval { get; private set; }
        internal bool ObservedPendingRemoval { get; private set; }
        internal bool PendingContainedRequest { get; private set; }

        internal void Arm(
            FileSystemManager fileSystem,
            MortalWoundTreatmentAttemptRequest request)
        {
            _commandPath = fileSystem.ResolvePath(
                AcceptedMechanicsPlan.WoundCommandPath);
            _pendingPath = fileSystem.ResolvePath(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            _requestFingerprint = request.RequestFingerprint;
            PendingContainedRequest = File.Exists(_pendingPath) &&
                File.ReadAllText(_pendingPath).Contains(
                    request.RequestFingerprint,
                    StringComparison.Ordinal);
        }

        internal Task AfterPhysicalFilePublishedAsync(string path)
        {
            if (_requestFingerprint is null)
                return Task.CompletedTask;

            if (string.Equals(path, _commandPath, StringComparison.OrdinalIgnoreCase))
            {
                ObservedCommandRemoval = true;
            }
            if (string.Equals(path, _pendingPath, StringComparison.OrdinalIgnoreCase))
            {
                ObservedPendingRemoval = true;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class DurableCommandProofFailureInjection
    {
        private string? _commandPath;
        private bool _commandPublicationObserved;

        internal bool Fired { get; private set; }

        internal void Arm(FileSystemManager fileSystem)
        {
            _commandPath = fileSystem.ResolvePath(
                AcceptedMechanicsPlan.WoundCommandPath);
        }

        internal Task AfterPhysicalFilePublishedAsync(string path)
        {
            if (Fired ||
                _commandPath is null ||
                !string.Equals(path, _commandPath, StringComparison.OrdinalIgnoreCase))
            {
                return Task.CompletedTask;
            }

            _commandPublicationObserved = true;
            return Task.CompletedTask;
        }

        internal Task BeforeCanonicalMutationAsync(string path)
        {
            if (Fired || !_commandPublicationObserved || _commandPath is null)
                return Task.CompletedTask;

            Fired = true;
            File.AppendAllText(_commandPath, " ");
            return Task.CompletedTask;
        }
    }

    private sealed class ResourcePublicationFailureInjection
    {
        private string? _failurePath;
        private string? _earlierResourcePath;
        private byte[]? _earlierResourceBytes;
        private FileSystemManager? _fileSystem;

        internal bool Fired { get; private set; }
        internal bool ObservedEarlierResourceWrite { get; private set; }

        internal void Arm(
            string failurePath,
            string earlierResourcePath,
            byte[] earlierResourceBytes,
            FileSystemManager fileSystem)
        {
            _failurePath = failurePath;
            _earlierResourcePath = earlierResourcePath;
            _earlierResourceBytes = earlierResourceBytes.ToArray();
            _fileSystem = fileSystem;
        }

        internal Task BeforeCanonicalMutationAsync(string path)
        {
            if (Fired || !string.Equals(path, _failurePath, StringComparison.Ordinal))
                return Task.CompletedTask;

            Fired = true;
            var resourcePath = Assert.IsType<string>(_earlierResourcePath);
            var before = Assert.IsType<byte[]>(_earlierResourceBytes);
            var fileSystem = Assert.IsType<FileSystemManager>(_fileSystem);
            var physical = fileSystem.ResolvePath(resourcePath);
            ObservedEarlierResourceWrite = File.Exists(physical) &&
                !before.AsSpan().SequenceEqual(File.ReadAllBytes(physical));
            return Task.FromException(new IOException(
                $"Injected B.3 publication failure at '{path}'."));
        }
    }
}
