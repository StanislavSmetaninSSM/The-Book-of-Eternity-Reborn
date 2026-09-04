using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentSeverityReductionPlannerTests
{
    [Fact]
    public void Prepare_StabilizeThenReducePreservesDeclaredOrdinalOrder()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 1);
        var firstResolution = CreateSyntheticResolution("s,r1");
        var secondResolution = CreateSyntheticResolution("r1,s");
        var first = CreateSyntheticPreparation(firstResolution, projected);
        var second = CreateSyntheticPreparation(secondResolution, projected);

        Assert.True(first.AgreesWith(firstResolution));
        Assert.True(second.AgreesWith(secondResolution));
        Assert.NotEqual(first.Fingerprint, second.Fingerprint);
        Assert.Equal(1, first.SeverityReduction!.Steps);
        Assert.Equal("II", first.ProvisionalAfter.Severity.Value);
        Assert.NotSame(first.Before, first.Before);
        Assert.NotSame(first.ProvisionalAfter, first.ProvisionalAfter);
        Assert.NotSame(first.SeverityReduction, first.SeverityReduction);
    }

    [Fact]
    public void Prepare_TwoOneStepReductionsAggregateToOneAtomicDestination()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 2);
        var resolution = CreateSyntheticResolution("r1,r1");
        var preparation = CreateSyntheticPreparation(resolution, projected);

        Assert.True(preparation.AgreesWith(resolution));
        Assert.Equal(2, preparation.SeverityReduction!.Steps);
        Assert.Equal("I", preparation.ProvisionalAfter.Severity.Value);
        Assert.Equal(1, preparation.ProvisionalAfter.Severity.Rank);
    }

    [Theory]
    [InlineData("s,r1,r1")]
    [InlineData("r1,s,r1")]
    [InlineData("r1,r1,s")]
    public void Prepare_StabilizationPlacementIsBoundInOrderedPreparation(string shape)
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 2);
        var resolution = CreateSyntheticResolution(shape);
        var preparation = CreateSyntheticPreparation(resolution, projected);

        Assert.True(preparation.AgreesWith(resolution));
        Assert.False(string.IsNullOrWhiteSpace(preparation.Fingerprint));
        Assert.Equal("I", preparation.ProvisionalAfter.Severity.Value);
    }

    [Theory]
    [InlineData("steps")]
    [InlineData("ordinal")]
    [InlineData("intent_fingerprint")]
    [InlineData("route_completion")]
    public void Prepare_SealedMismatchIsRejected(string mutation)
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var projected = AssertValidProjection(before, 1);
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(resolution, projected);
        var original = Assert.IsType<MortalWoundReduceSeverityOutcomeIntent>(
            Assert.Single(resolution.OutcomeIntents));
        var changedIntent = mutation switch
        {
            "steps" => MortalWoundReduceSeverityOutcomeIntent.Create(
                0,
                original.DeclaredOperationFingerprint,
                original.IntentFingerprint,
                2),
            "ordinal" => MortalWoundReduceSeverityOutcomeIntent.Create(
                1,
                original.DeclaredOperationFingerprint,
                original.IntentFingerprint,
                1),
            "intent_fingerprint" => MortalWoundReduceSeverityOutcomeIntent.Create(
                0,
                original.DeclaredOperationFingerprint,
                original.IntentFingerprint + "_changed",
                1),
            _ => original
        };
        var changed = CreateSyntheticResolution(
            new MortalWoundTreatmentOperation[] { new MortalWoundReduceSeverityOperation(1) },
            new MortalWoundTreatmentOutcomeIntent[] { changedIntent },
            mutation == "route_completion" ? "None" : "AppendOnce");

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            changed,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.False(result.IsValid);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_outcome_preparation_mismatch");
    }

    [Fact]
    public void Prepare_UnchangedResultFinalizesOnlyFromExactProvisionalAfter()
    {
        var before = Parse(WoundContractTestData.CreateActiveWound());
        var resolution = CreateSyntheticResolution("n", routeCompletion: "None");
        var preparation = CreateSyntheticPreparation(
            resolution,
            severityReduction: null,
            provisionalAfter: before);

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.True(result.IsValid, Describe(result.Issues));
        Assert.Equal(
            WoundMaterializationContract.SerializeCanonical(preparation.ProvisionalAfter),
            WoundMaterializationContract.SerializeCanonical(result.After!));
        Assert.Equal(before.Severity.Rank, result.DeclaredOutcome!.ResultingSeverityRank);
        Assert.False(string.IsNullOrWhiteSpace(result.Fingerprint));
    }

    [Fact]
    public void FinalizeReduction_WithoutAuthenticatedBatchFailsClosed()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            AssertValidProjection(before, 1));
        var arbitrary = new Dictionary<string, EffectAcceptedApplicationResult>(
            StringComparer.Ordinal)
        {
            ["application_arbitrary"] = null!
        };

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            null,
            arbitrary);

        Assert.False(result.IsValid);
        Assert.Null(result.After);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_publication_slice_unsupported");
    }

    [Fact]
    public void FinalizeReduction_NullProjectionCannotBypassFailClosedBoundary()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            severityReduction: null,
            provisionalAfter: before);

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.False(result.IsValid);
        Assert.Null(result.After);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_outcome_preparation_mismatch");
    }

    [Fact]
    public void PrepareReductionBatch_CompleteCurrentTeardownAndGenerationLinksAreSealed()
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            AssertValidProjection(before, 1));
        var input = CreateRematerializationInput(preparation);

        var (batch, authority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);

        Assert.Equal(2, batch.RootApplications.Count);
        Assert.Equal(2, batch.TerminalOperations.Count);
        Assert.DoesNotContain(batch.RootLineageAuthority, static row =>
            row.EffectId is not null);
        Assert.Equal(
            preparation.SeverityReduction!.Roots
                .Select(static root => root.PriorEffectId),
            batch.RootApplications.Select(ReadPriorRootEffectId));
        Assert.All(batch.RootApplications, static root =>
            Assert.Null(root.SourceSelector.SourceId));
        Assert.All(batch.RootApplications, root =>
            Assert.Equal(batch.LocalWoundRef, root.SourceSelector.SourceRef));
        Assert.Equal("treat", batch.TransitionAuthority.TransitionKind);
        AssertPrivateAuthoritySeals(authority);
    }

    [Fact]
    public void ReductionEffectHandoff_TreatBatchRequiresPrivateAuthority()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            AssertValidProjection(before, 1));
        var input = CreateRematerializationInput(preparation);
        var (batch, _) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var baseline = WoundAcceptedTurnPlannerCore.CreateBaselineAuthority(
            WoundAcceptedTurnFingerprints.ComputeInput(input),
            input);
        var provisional = new WoundPreparedAcceptedTurnPlan(
            input.Binding,
            WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding),
            WoundAcceptedTurnFingerprints.ComputeInput(input),
            string.Empty,
            new[] { before.WoundId },
            new[] { preparation.TransitionId },
            new[] { preparation.ProvisionalAfter },
            new[] { batch },
            baseline);
        var forged = new WoundPreparedAcceptedTurnPlan(
            input.Binding,
            provisional.BindingFingerprint,
            provisional.InputFingerprint,
            WoundAcceptedTurnFingerprints.ComputePreparation(provisional),
            provisional.AllocatedWoundIds,
            provisional.AllocatedTransitionIds,
            provisional.PreparedWounds,
            provisional.EffectOperationBatches,
            provisional.BaselineAuthority);

        var issues = WoundAcceptedTurnPlannerCore.ValidatePreparedAuthority(forged);

        Assert.Contains(issues, static issue =>
            issue.Code == "wound_plan_prepared_seal_mismatch");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("reordered")]
    [InlineData("extra")]
    public void PrepareReductionBatch_ChangedTerminalTopologyCannotBorrowPrivateSeal(
        string mutation)
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            AssertValidProjection(before, 1));
        var input = CreateRematerializationInput(preparation);
        var (batch, authority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var terminals = mutation switch
        {
            "missing" => batch.TerminalOperations.Skip(1).ToArray(),
            "reordered" => batch.TerminalOperations.Reverse().ToArray(),
            "extra" => batch.TerminalOperations
                .Append(batch.TerminalOperations[0])
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var provisional = new WoundEffectOperationBatch(
            batch.LocalWoundRef,
            batch.PreparedWoundId,
            batch.SourceExport,
            batch.RootApplications,
            terminals,
            batch.RootLineageAuthority,
            string.Empty,
            batch.TransitionAuthority);
        var resealed = new WoundEffectOperationBatch(
            provisional.LocalWoundRef,
            provisional.PreparedWoundId,
            provisional.SourceExport,
            provisional.RootApplications,
            provisional.TerminalOperations,
            provisional.RootLineageAuthority,
            WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional),
            provisional.TransitionAuthority);

        Assert.False(InvokeRematerializationAgrees(
            input,
            preparation,
            resolution,
            resealed,
            authority));
    }

    [Theory]
    [InlineData("projection")]
    [InlineData("source_export")]
    public void PrepareReductionBatch_ChangedProjectionOrSourceExportCannotBorrowPrivateSeal(
        string mutation)
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            AssertValidProjection(before, 1));
        var input = CreateRematerializationInput(preparation);
        var (batch, authority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var changedPreparation = mutation == "projection"
            ? CreateSyntheticPreparation(
                resolution,
                new MortalWoundTreatmentSeverityReductionProjection(
                    preparation.SeverityReduction!.Before,
                    preparation.SeverityReduction.ProvisionalAfter,
                    preparation.SeverityReduction.Steps,
                    preparation.SeverityReduction.Roots,
                    preparation.SeverityReduction.Fingerprint + "_changed"))
            : preparation;
        var changedExport = mutation == "source_export"
            ? new WoundEffectSourceExport(
                batch.SourceExport.SchemaVersion,
                batch.SourceExport.Kind,
                batch.SourceExport.SourceId,
                batch.SourceExport.SourceRef,
                batch.SourceExport.State,
                batch.SourceExport.Materializable,
                batch.SourceExport.Realm,
                batch.SourceExport.Owner,
                batch.SourceExport.CausalEventRef,
                batch.SourceExport.EventSemanticFingerprint,
                batch.SourceExport.OpportunityId,
                batch.SourceExport.OpportunityAuthorityFingerprint,
                batch.SourceExport.Definitions.Reverse().ToArray())
            : batch.SourceExport;
        var changedBatch = mutation == "source_export"
            ? new WoundEffectOperationBatch(
                batch.LocalWoundRef,
                batch.PreparedWoundId,
                changedExport,
                batch.RootApplications,
                batch.TerminalOperations,
                batch.RootLineageAuthority,
                batch.SourceExportFingerprint,
                batch.TransitionAuthority)
            : batch;

        Assert.False(InvokeRematerializationAgrees(
            input,
            changedPreparation,
            resolution,
            changedBatch,
            authority));
    }

    [Fact]
    public void PrepareReductionBatch_AuthenticProjectionFingerprintCannotSealChangedRoots()
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var resolution = CreateSyntheticResolution("r1");
        var projection = AssertValidProjection(before, 1);
        var root = Assert.Single(projection.Roots);
        var changedProjection = new MortalWoundTreatmentSeverityReductionProjection(
            projection.Before,
            projection.ProvisionalAfter,
            projection.Steps,
            new[]
            {
                new MortalWoundTreatmentRematerializationRoot(
                    root.PriorEffectId + "_forged",
                    root.DefinitionKey,
                    root.OwnershipDomain,
                    root.Slots)
            },
            projection.Fingerprint);
        var preparation = CreateSyntheticPreparation(
            resolution,
            changedProjection);
        var input = CreateRematerializationInput(preparation);

        var (batch, authority, issues) = InvokeRawRematerializationPrepare(
            input,
            preparation,
            resolution);

        Assert.Null(batch);
        Assert.Null(authority);
        Assert.Contains(issues, static issue =>
            issue.Code == "wound_plan_treatment_rematerialization_invalid");
    }

    [Theory]
    [InlineData("missing_root")]
    [InlineData("reordered_roots")]
    [InlineData("changed_lineage")]
    public void PrepareReductionBatch_ChangedRootOrLineageCannotBorrowPrivateSeal(
        string mutation)
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            AssertValidProjection(before, 1));
        var input = CreateRematerializationInput(preparation);
        var (batch, authority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        var roots = mutation switch
        {
            "missing_root" => batch.RootApplications.Skip(1).ToArray(),
            "reordered_roots" => batch.RootApplications.Reverse().ToArray(),
            _ => batch.RootApplications.ToArray()
        };
        var lineage = mutation == "changed_lineage"
            ? batch.RootLineageAuthority.Select((row, index) => index == 0
                ? row with { DefinitionKey = row.DefinitionKey + "_changed" }
                : row).ToArray()
            : batch.RootLineageAuthority.ToArray();
        var provisional = new WoundEffectOperationBatch(
            batch.LocalWoundRef,
            batch.PreparedWoundId,
            batch.SourceExport,
            roots,
            batch.TerminalOperations,
            lineage,
            string.Empty,
            batch.TransitionAuthority);
        var resealed = new WoundEffectOperationBatch(
            provisional.LocalWoundRef,
            provisional.PreparedWoundId,
            provisional.SourceExport,
            provisional.RootApplications,
            provisional.TerminalOperations,
            provisional.RootLineageAuthority,
            WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional),
            provisional.TransitionAuthority);

        Assert.False(InvokeRematerializationAgrees(
            input,
            preparation,
            resolution,
            resealed,
            authority));
    }

    [Fact]
    public void PrepareReductionBatch_IsDeterministicDetachedAndRecomputesAuthorityDag()
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier"));
        var resolution = CreateSyntheticResolution("r1");
        var preparation = CreateSyntheticPreparation(
            resolution,
            AssertValidProjection(before, 1));
        var input = CreateRematerializationInput(preparation);

        var (firstBatch, firstAuthority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);
        firstBatch.SourceExport.Definitions[0].Definition["display"]!["name"] =
            "detached mutation";
        var (secondBatch, secondAuthority) = InvokeRematerializationPrepare(
            input,
            preparation,
            resolution);

        Assert.Equal(
            ReadAuthorityString(firstAuthority, "AuthoritySeal"),
            ReadAuthorityString(secondAuthority, "AuthoritySeal"));
        Assert.Equal(
            ReadAuthorityString(secondAuthority, "SourceExportFingerprint"),
            WoundAcceptedTurnFingerprints.ComputeSourceExport(secondBatch));
        Assert.Equal(
            ReadAuthorityString(secondAuthority, "BatchTopologyFingerprint"),
            MortalWoundTreatmentSeverityRematerializationPlanner
                .ComputeBatchTopologyFingerprint(secondBatch));
        var expectedSeed = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_wound_treatment.rematerialization_seed",
            "1",
            ReadAuthorityString(secondAuthority, "PreparedInputFingerprint"),
            ReadAuthorityString(secondAuthority, "RequestFingerprint"),
            ReadAuthorityString(
                secondAuthority,
                "ResolutionAuthorityFingerprint"),
            ReadAuthorityString(secondAuthority, "ResultFingerprint"),
            ReadAuthorityString(secondAuthority, "AttemptId"),
            ReadAuthorityString(secondAuthority, "OperationKey"),
            ReadAuthorityString(secondAuthority, "TransitionId"),
            ReadAuthorityString(secondAuthority, "ExpectedBeforeFingerprint"),
            ReadAuthorityString(secondAuthority, "ProjectionFingerprint")
        });
        Assert.Equal(
            expectedSeed,
            ReadAuthorityString(secondAuthority, "AuthoritySeedFingerprint"));
        var expectedSeal = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_wound_treatment.rematerialization_authority",
            "1",
            ReadAuthorityString(secondAuthority, "AuthoritySeedFingerprint"),
            ReadAuthorityString(secondAuthority, "BatchTopologyFingerprint")
        });
        Assert.Equal(
            expectedSeal,
            ReadAuthorityString(secondAuthority, "AuthoritySeal"));
        Assert.Equal(
            secondBatch.RootApplications.Count,
            secondBatch.RootApplications.Select(static value =>
                    MortalLocationIdentityState.BuildConfusableKey(
                        value.ApplicationRef))
                .Distinct(StringComparer.Ordinal)
                .Count());
    }

    [Theory]
    [InlineData("projection_steps")]
    [InlineData("projection_fingerprint")]
    [InlineData("projection_after")]
    [InlineData("non_reduction_projection")]
    public void Prepare_InconsistentProjectionSealCannotFinalize(string mutation)
    {
        var before = Parse(CreateRankThreeWound("action_control"));
        var resolution = CreateSyntheticResolution(
            mutation == "non_reduction_projection" ? "n" : "r1",
            mutation == "non_reduction_projection" ? "None" : "AppendOnce");
        var valid = AssertValidProjection(
            before,
            mutation == "projection_steps" ? 2 : 1);
        var projection = mutation switch
        {
            "projection_fingerprint" =>
                new MortalWoundTreatmentSeverityReductionProjection(
                    valid.Before,
                    valid.ProvisionalAfter,
                    valid.Steps,
                    valid.Roots,
                    valid.Fingerprint + "_changed"),
            "projection_after" =>
                new MortalWoundTreatmentSeverityReductionProjection(
                    valid.Before,
                    before,
                    valid.Steps,
                    valid.Roots,
                    valid.Fingerprint),
            _ => valid
        };
        var preparation = CreateSyntheticPreparation(resolution, projection);

        var result = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            preparation,
            resolution,
            null,
            new Dictionary<string, EffectAcceptedApplicationResult>(
                StringComparer.Ordinal));

        Assert.False(result.IsValid);
        Assert.Null(result.After);
        Assert.Null(result.DeclaredOutcome);
        Assert.Contains(result.Issues, static issue =>
            issue.Code == "mortal_wound_treatment_outcome_preparation_mismatch");
    }

    [Theory]
    [InlineData(1, "II", 2)]
    [InlineData(2, "I", 1)]
    public void Project_ValidReductionPreservesSemanticGraphAndChangesNoRuntimeIdentity(
        int steps,
        string expectedValue,
        int expectedRank)
    {
        var source = CreateRankThreeWound(steps == 1
            ? new[] { "action_control", "resistance_modifier" }
            : new[] { "action_control" });
        if (steps == 1)
        {
            source["complications"] = new JsonArray(new JsonObject
            {
                ["complicationId"] = "complication_destination_restriction",
                ["kind"] = "impairment",
                ["state"] = "active",
                ["displayName"] = "Restricted movement",
                ["treatmentDifficultyModifier"] = 1,
                ["ownedEffectIds"] = new JsonArray("effect_destination_2"),
                ["visibility"] = "known_to_player"
            });
        }
        var before = Parse(source);
        var beforeJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();

        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            steps,
            "turn_43:treatment_projection");

        Assert.True(result.IsValid, Describe(result.Issues));
        var projection = Assert.IsType<MortalWoundTreatmentSeverityReductionProjection>(
            result.Projection);
        var after = projection.ProvisionalAfter;
        var afterJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(after))!.AsObject();
        Assert.Equal(expectedValue, after.Severity.Value);
        Assert.Equal(expectedRank, after.Severity.Rank);
        Assert.Equal("III", after.Severity.MaximumAtCreation);
        Assert.Equal("turn_43:treatment_projection", after.Severity.LastChangeEventRef);
        Assert.Equal(expectedRank, after.Consequences.SlotBudget);
        Assert.Equal(steps, projection.Steps);
        Assert.False(string.IsNullOrWhiteSpace(projection.Fingerprint));
        foreach (var member in new[]
                 {
                     "origin", "classification", "display", "care", "complications",
                     "treatment", "recovery", "relations", "lastTransition"
                 })
        {
            Assert.True(
                JsonNode.DeepEquals(beforeJson[member], afterJson[member]),
                $"Projection changed wound.{member}.");
        }
        foreach (var member in new[] { "slotsUsed", "ownedEffectSources", "entries" })
        {
            Assert.True(
                JsonNode.DeepEquals(
                    beforeJson["consequences"]![member],
                    afterJson["consequences"]![member]),
                $"Projection changed wound.consequences.{member}.");
        }

        var oldEffectIds = before.Consequences.OwnedEffectSources.RootBindings
            .Select(static binding => binding.EffectId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            oldEffectIds,
            after.Consequences.OwnedEffectSources.RootBindings
                .Select(static binding => binding.EffectId)
                .Order(StringComparer.Ordinal));
        Assert.Equal(oldEffectIds, projection.Roots.Select(static root => root.PriorEffectId));
        Assert.DoesNotContain(
            projection.Roots,
            static root => root.PriorEffectId.Contains("replacement", StringComparison.Ordinal));
        Assert.NotSame(projection.Before, projection.Before);
        Assert.NotSame(projection.ProvisionalAfter, projection.ProvisionalAfter);
        Assert.NotSame(projection.Roots, projection.Roots);
        var rootsReadback = Assert.IsType<MortalWoundTreatmentRematerializationRoot[]>(
            projection.Roots);
        rootsReadback[0] = null!;
        Assert.NotNull(projection.Roots[0]);
        if (steps == 1)
        {
            Assert.Equal("base_wound", projection.Roots[0].OwnershipDomain.Kind);
            Assert.Equal("complication", projection.Roots[1].OwnershipDomain.Kind);
            Assert.Equal(
                "complication_destination_restriction",
                projection.Roots[1].OwnershipDomain.ComplicationId);
        }
    }

    [Fact]
    public void Project_DestinationEnvelopeRejectsExcessSlotsWithoutPruning()
    {
        var before = Parse(CreateRankThreeWound(
            "action_control",
            "resistance_modifier",
            "periodic_damage"));
        var canonicalBefore = WoundMaterializationContract.SerializeCanonical(before);

        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            1,
            "turn_43:slot_overflow");

        Assert.False(result.IsValid);
        Assert.Null(result.Projection);
        Assert.NotEmpty(result.Issues);
        Assert.Equal(canonicalBefore, WoundMaterializationContract.SerializeCanonical(before));
        Assert.Equal(3, before.Consequences.SlotsUsed);
        Assert.Equal(3, before.Consequences.Entries.Count);
    }

    [Fact]
    public void Project_DestinationEnvelopeRejectsOverpoweredComponentWithoutWeakening()
    {
        var source = CreateRankThreeWound("action_control");
        source["consequences"]!["ownedEffectSources"]!["definitions"]![0]!
            ["components"]![0]!["payload"]!["operation"] = "forbid";
        var before = Parse(source);
        var canonicalBefore = WoundMaterializationContract.SerializeCanonical(before);

        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            1,
            "turn_43:power_overflow");

        Assert.False(result.IsValid);
        Assert.Null(result.Projection);
        Assert.Contains(
            result.Issues,
            static issue => string.Equals(
                issue.Code,
                "wound_consequence_action_forbid_invalid",
                StringComparison.Ordinal));
        Assert.Equal(canonicalBefore, WoundMaterializationContract.SerializeCanonical(before));
    }

    [Fact]
    public void Project_DestinationEnvelopeRejectsReactionExpansionPowerWithoutWeakening()
    {
        var source = CreateRankFourReactionWound();
        var before = Parse(source);
        var canonicalBefore = WoundMaterializationContract.SerializeCanonical(before);

        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            1,
            "turn_43:reaction_power_overflow");

        Assert.False(result.IsValid);
        Assert.Null(result.Projection);
        Assert.Contains(result.Issues, static issue =>
            string.Equals(
                issue.Code,
                "wound_consequence_magnitude_exceeded",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.FilePath,
                "mortalWoundTreatment.severityReductionProjection.before.consequences.ownedEffectSources.definitions[0].components[0].payload.value",
                StringComparison.Ordinal));
        Assert.Equal(canonicalBefore, WoundMaterializationContract.SerializeCanonical(before));
        var definitions = before.Consequences.OwnedEffectSources.Definitions;
        Assert.Equal(2, definitions.Count);
        Assert.Contains(definitions, static definition =>
            definition.GetProperty("definitionKey").GetString() ==
            "definition_destination_leaf");
    }

    private static JsonObject CreateRankThreeWound(params string[] profiles)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        wound["consequences"]!["slotBudget"] = 3;
        wound["consequences"]!["slotsUsed"] = profiles.Length;
        var roots = profiles.Select((profile, index) => (
            EffectId: $"effect_destination_{index + 1}",
            DefinitionKey: $"definition_destination_{index + 1}",
            Profile: profile)).ToArray();
        wound["consequences"]!["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSourcesForTarget(
                wound["woundId"]!.GetValue<string>(),
                "mortal_world",
                "player",
                roots);
        wound["consequences"]!["entries"] = new JsonArray(
            profiles.Select((profile, index) => (JsonNode)new JsonObject
            {
                ["slot"] = index + 1,
                ["profileKey"] = profile,
                ["effectId"] = roots[index].EffectId,
                ["readableSummary"] = $"Preserved destination slot {index + 1}."
            }).ToArray());
        return wound;
    }

    private static MortalWoundTreatmentSeverityReductionProjection AssertValidProjection(
        WoundMaterializationEnvelope before,
        int steps)
    {
        var result = MortalWoundTreatmentSeverityReductionPlanner.Project(
            before,
            steps,
            "turn_43:treatment_projection");
        Assert.True(result.IsValid, Describe(result.Issues));
        return Assert.IsType<MortalWoundTreatmentSeverityReductionProjection>(
            result.Projection);
    }

    private static MortalWoundTreatmentOutcomePreparation CreateSyntheticPreparation(
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentSeverityReductionProjection? severityReduction,
        WoundMaterializationEnvelope? provisionalAfter = null)
    {
        var before = severityReduction?.Before ?? provisionalAfter ??
            throw new InvalidOperationException("A synthetic preparation needs a wound.");
        var after = provisionalAfter ?? severityReduction!.ProvisionalAfter;
        const string transitionId = "wound_transition_t070_b6_direct";
        const long currentMinute = 1_260;
        var fingerprint =
            MortalWoundTreatmentOutcomePublicationPlanner.ComputePreparationFingerprint(
                before,
                after,
                resolution,
                transitionId,
                currentMinute,
                severityReduction?.Fingerprint);
        return new MortalWoundTreatmentOutcomePreparation(
            before,
            after,
            transitionId,
            severityReduction,
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.OutcomeIntents,
            resolution.RouteCompletion,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            currentMinute,
            fingerprint);
    }

    private static MortalWoundTreatmentResolution CreateSyntheticResolution(
        string shape,
        string routeCompletion = "AppendOnce")
    {
        var operations = shape.Split(',').Select(static token => token switch
        {
            "n" => (MortalWoundTreatmentOperation)new MortalWoundNoImprovementOperation(),
            "s" => new MortalWoundStabilizeOperation(),
            "r1" => new MortalWoundReduceSeverityOperation(1),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), token, null)
        }).ToArray();
        var intents = operations.Select((operation, ordinal) =>
        {
            var declared = MortalWoundTreatmentOutcomeIntentComposer.DeclaredFingerprint(
                ordinal, operation);
            var intent = $"sha256:intent_{ordinal}_{operation.Kind}";
            return operation switch
            {
                MortalWoundNoImprovementOperation =>
                    (MortalWoundTreatmentOutcomeIntent)
                    MortalWoundNoImprovementOutcomeIntent.Create(
                        ordinal, declared, intent),
                MortalWoundStabilizeOperation => MortalWoundStabilizeOutcomeIntent.Create(
                    ordinal, declared, intent),
                MortalWoundReduceSeverityOperation reduction =>
                    MortalWoundReduceSeverityOutcomeIntent.Create(
                        ordinal, declared, intent, reduction.Steps),
                _ => throw new InvalidOperationException()
            };
        }).ToArray();
        return CreateSyntheticResolution(operations, intents, routeCompletion);
    }

    private static MortalWoundTreatmentResolution CreateSyntheticResolution(
        IReadOnlyList<MortalWoundTreatmentOperation> operations,
        IReadOnlyList<MortalWoundTreatmentOutcomeIntent> intents,
        string routeCompletion)
    {
        var constructor = typeof(MortalWoundTreatmentResolution)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return Assert.IsType<MortalWoundTreatmentResolution>(constructor.Invoke(new object?[]
        {
            "procedure",
            null,
            "AcceptedTerminal",
            routeCompletion == "AppendOnce" ? "success" : "failed_attempt",
            0,
            false,
            operations,
            intents,
            null,
            "never",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "sha256:route",
            "sha256:resolution",
            "sha256:request",
            "sha256:result",
            routeCompletion
        }));
    }

    private static JsonObject CreateRankFourReactionWound()
    {
        var wound = CreateRankThreeWound("event_reaction", "characteristic_modifier");
        wound["severity"]!["value"] = "IV";
        wound["severity"]!["rank"] = 4;
        wound["severity"]!["maximumAtCreation"] = "IV";
        wound["consequences"]!["slotBudget"] = 4;
        var sources = wound["consequences"]!["ownedEffectSources"]!.AsObject();
        var root = WoundContractTestData.CreateApplyDefinitionRoot(
            wound["woundId"]!.GetValue<string>(),
            "mortal_world",
            "definition_destination_reaction",
            "definition_destination_leaf");
        var leaf = WoundContractTestData.CreateOwnedEffectDefinition(
            wound["woundId"]!.GetValue<string>(),
            "mortal_world",
            "definition_destination_leaf",
            "characteristic_modifier");
        leaf["components"]![0]!["payload"]!["operation"] = "flat";
        leaf["components"]![0]!["payload"]!["value"] = 4;
        sources["definitions"] = new JsonArray(root, leaf);
        sources["rootBindings"] = new JsonArray(
            WoundContractTestData.CreateRootBinding(
                "effect_destination_reaction",
                "definition_destination_reaction"));
        wound["consequences"]!["slotsUsed"] = 2;
        wound["consequences"]!["entries"] = new JsonArray(
            new JsonObject
            {
                ["slot"] = 1,
                ["profileKey"] = "event_reaction",
                ["effectId"] = "effect_destination_reaction",
                ["readableSummary"] = "The wound reacts to renewed harm."
            },
            new JsonObject
            {
                ["slot"] = 2,
                ["profileKey"] = "characteristic_modifier",
                ["effectId"] = "effect_destination_reaction",
                ["readableSummary"] = "The reaction imposes a severe penalty."
            });
        return wound;
    }

    private static WoundMaterializationEnvelope Parse(JsonObject wound)
    {
        var parsed = WoundMaterializationContract.Parse(
            wound.ToJsonString(),
            "projectionTest.wound");
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        return Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) => string.Join(
        Environment.NewLine,
        issues.Select(static issue =>
            $"{issue.Code}@{issue.FilePath}: {issue.Expected}; actual={issue.Actual}"));

    private static WoundAcceptedTurnInput CreateRematerializationInput(
        MortalWoundTreatmentOutcomePreparation preparation)
    {
        var before = preparation.Before;
        var eventRef = preparation.ProvisionalAfter.Severity.LastChangeEventRef;
        var acceptedEvent = new WoundAcceptedEventAuthority(
            eventRef,
            "accepted_turn",
            "authority_t070_b6_treatment",
            "sha256:" + new string('e', 64));
        var events = new[] { acceptedEvent };
        var binding = new WoundAcceptedTurnBinding(
            "session_t070_b6",
            "request_t070_b6",
            "snapshot_t070_b6",
            before.Owner.Realm,
            43,
            events,
            WoundAcceptedEventSetFingerprint.Compute(events));
        var woundNode = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();
        var woundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(before);
        var woundIdentity = WoundContractTestData.CreateIdentityEntry(
            before.WoundId,
            before.Owner.Realm,
            before.Owner.OwnerKind,
            before.Owner.OwnerId,
            before.Owner.CarrierPath,
            before.Classification.Domain,
            "active",
            before.Origin.CreatedAtTurn,
            before.Origin.EventRef,
            before.LastTransition.Ordinal,
            semanticFingerprint: woundFingerprint);
        var historyTransition = WoundContractTestData.CreateTransition();
        historyTransition["transitionId"] = before.LastTransition.TransitionId;
        historyTransition["woundId"] = before.WoundId;
        historyTransition["woundTransitionOrdinal"] = before.LastTransition.Ordinal;
        historyTransition["turn"] = before.LastTransition.Turn;
        historyTransition["kind"] = before.LastTransition.Kind;
        historyTransition["eventRef"] = before.Origin.EventRef;
        historyTransition["afterFingerprint"] = woundFingerprint;
        var definitions = before.Consequences.OwnedEffectSources.Definitions
            .Select(static value => JsonNode.Parse(value.GetRawText())!.AsObject())
            .ToDictionary(
                static value => value["definitionKey"]!.GetValue<string>(),
                StringComparer.Ordinal);
        var effects = before.Consequences.OwnedEffectSources.RootBindings
            .Select((bindingValue, index) => CreateRematerializationEffect(
                before,
                bindingValue,
                definitions[bindingValue.DefinitionKey],
                index))
            .ToArray();
        var effectIdentity = EffectMaterializationTestFixture.CreateIdentityIndex(
            effects);
        foreach (var entry in effectIdentity["entries"]!.AsArray()
                     .OfType<JsonObject>())
        {
            var effect = effects.Single(value => string.Equals(
                value["effectId"]!.GetValue<string>(),
                entry["effectId"]!.GetValue<string>(),
                StringComparison.Ordinal));
            entry["transitions"]![0]!["transitionId"] =
                effect["chronology"]!["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] =
                effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        return new WoundAcceptedTurnInput(
            binding,
            Array.Empty<WoundOpportunityAuthority>(),
            Array.Empty<WoundAcceptedTransitionDraft>(),
            new WoundCarrierCatalogInput(
                WoundContractTestData.CreatePlayerCarrier(woundNode),
                null,
                null,
                null,
                null),
            WoundContractTestData.CreateIdentityIndex(woundIdentity),
            WoundContractTestData.CreateHistory(historyTransition),
            new EffectCarrierCatalogInput(
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["activeEffects"] = new JsonArray(effects
                        .Select(static value => (JsonNode)value.DeepClone())
                        .ToArray())
                },
                null,
                null,
                null,
                null,
                null),
            effectIdentity);
    }

    private static JsonObject CreateRematerializationEffect(
        WoundMaterializationEnvelope wound,
        WoundRootEffectBinding binding,
        JsonObject definition,
        int index)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["effectId"] = binding.EffectId;
        effect["realm"] = wound.Owner.Realm;
        effect["target"] = new JsonObject
        {
            ["kind"] = "player",
            ["targetId"] = wound.Owner.OwnerId
        };
        effect["source"] = new JsonObject
        {
            ["kind"] = "wound",
            ["sourceId"] = wound.WoundId,
            ["definitionKey"] = binding.DefinitionKey
        };
        effect["display"] = definition["display"]!.DeepClone();
        effect["components"] = definition["components"]!.DeepClone();
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "source_bound",
            ["linkKind"] = "wound",
            ["targetId"] = wound.WoundId,
            ["activePredicate"] = "active",
            ["onSourceLoss"] = "expire",
            ["displayText"] = "While the wound remains active"
        };
        var stacking = definition["stacking"]!.DeepClone().AsObject();
        stacking.Remove("atMaximum");
        stacking["currentStacks"] = 1;
        effect["stacking"] = stacking;
        effect["triggers"] = definition["triggers"]!.DeepClone();
        effect["removal"] = definition["removal"]!.DeepClone();
        effect["links"] = definition["links"]!.DeepClone();
        effect["chronology"] = new JsonObject
        {
            ["createdAtTurn"] = 42,
            ["createdEventRef"] = $"turn_42:t070_b6:create:{index}",
            ["causalEventRef"] = beforeEventRef(wound),
            ["lastTransitionId"] = $"effect_transition_t070_b6_create_{index}",
            ["lastTransitionTurn"] = 42
        };
        return effect;

        static string beforeEventRef(WoundMaterializationEnvelope value) =>
            value.Origin.EventRef;
    }

    private static (WoundEffectOperationBatch Batch, object Authority)
        InvokeRematerializationPrepare(
            WoundAcceptedTurnInput input,
            MortalWoundTreatmentOutcomePreparation preparation,
            MortalWoundTreatmentResolution resolution)
    {
        var carrierIssues = EffectCarrierCatalog.Build(
            input.PreTurnEffectCarriers!).Issues;
        Assert.True(carrierIssues.Count == 0, Describe(carrierIssues));
        using var identityDocument = JsonDocument.Parse(
            input.PreTurnEffectIdentityIndex!.ToJsonString());
        var identityParse = EffectIdentityState.Parse(
            identityDocument.RootElement,
            EffectIdentityState.StatePath);
        Assert.True(identityParse.Issues.Count == 0,
            Describe(identityParse.Issues));
        var (rawBatch, authority, issues) = InvokeRawRematerializationPrepare(
            input,
            preparation,
            resolution);
        Assert.True(issues.Count == 0, Describe(issues));
        var batch = Assert.IsType<WoundEffectOperationBatch>(rawBatch);
        Assert.NotNull(authority);
        return (batch, authority!);
    }

    private static (object? Batch, object? Authority,
        IReadOnlyList<ValidationIssue> Issues) InvokeRawRematerializationPrepare(
            WoundAcceptedTurnInput input,
            MortalWoundTreatmentOutcomePreparation preparation,
            MortalWoundTreatmentResolution resolution)
    {
        var type = typeof(WoundAcceptedTurnPlanner).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentSeverityRematerializationPlanner");
        Assert.True(type is not null,
            "The private treatment rematerialization planner must exist.");
        var method = type!.GetMethod(
            "Prepare",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.True(method is not null,
            "The private treatment rematerialization planner must expose Prepare.");
        var result = method!.Invoke(null, new object?[]
        {
            input,
            preparation,
            resolution.RequestFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint,
            "wound_treatment_attempt_t070_b6",
            "operation_t070_b6",
            WoundIdentityState.ComputeSemanticFingerprint(preparation.Before)
        });
        Assert.NotNull(result);
        var resultType = result!.GetType();
        var issues = Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
            resultType.GetProperty("Issues", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(result));
        var batch = resultType.GetProperty(
                "Batch",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(result);
        var authority = resultType.GetProperty(
                "Authority",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(result);
        return (batch, authority, issues);
    }

    private static bool InvokeRematerializationAgrees(
        WoundAcceptedTurnInput input,
        MortalWoundTreatmentOutcomePreparation preparation,
        MortalWoundTreatmentResolution resolution,
        WoundEffectOperationBatch batch,
        object authority)
    {
        var type = authority.GetType().Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentSeverityRematerializationPlanner");
        Assert.NotNull(type);
        var method = type!.GetMethod(
            "Agrees",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<bool>(method!.Invoke(null, new object?[]
        {
            input,
            preparation,
            resolution.RequestFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint,
            "wound_treatment_attempt_t070_b6",
            "operation_t070_b6",
            WoundIdentityState.ComputeSemanticFingerprint(preparation.Before),
            batch,
            authority
        }));
    }

    private static void AssertPrivateAuthoritySeals(object authority)
    {
        var type = authority.GetType();
        foreach (var propertyName in new[]
                 {
                     "AuthoritySeedFingerprint",
                     "SourceExportFingerprint",
                     "BatchTopologyFingerprint",
                     "AuthoritySeal"
                 })
        {
            var value = Assert.IsType<string>(type.GetProperty(propertyName)!
                .GetValue(authority));
            Assert.StartsWith("sha256:", value);
        }
    }

    private static string ReadAuthorityString(
        object authority,
        string propertyName) => Assert.IsType<string>(authority.GetType()
        .GetProperty(propertyName)!
        .GetValue(authority));

    private static string? ReadPriorRootEffectId(
        WoundRootEffectApplication application)
    {
        var property = typeof(WoundRootEffectApplication).GetProperty(
            "PriorRootEffectId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<string>(property!.GetValue(application));
    }
}
