using System.Globalization;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T067-B detached requirement-authority regressions. Every negative row starts from a
/// production-created request and recomputes every enclosing witness/binding/scope/bundle,
/// resource, and request seal. The detached validator must therefore reject semantics,
/// rather than relying on an unchanged outer hash to detect the mutation.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void DetachedModeAuthority_SkillScopedRollTamperedRollSkillIdRejectsAfterReseal()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.ReplacePlayerProcedureSkillScopedRollEffects(
            "t171_detached_roll_skill",
            new ProcedureSkillScopedRollEffectSeed(
                "advantage", "skill", "skill_field_medicine_01"));
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_t171_roll_skill",
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        AssertDetachedRequirementAccepted(request);
        var authority = ReadJsonObject(request, "ModeAuthority");
        authority[FindJsonPropertyName(authority, "RollSkillId")] =
            "skill_patient_observation_01";

        ResealDetachedProcedureAuthorityAndRequestWithRollSkillId(request);
        AssertDetachedModeAuthorityRejected(request);
    }

    [Fact]
    public void DetachedRequirementAuthority_ProductionKindCompleteRequestRemainsAccepted()
    {
        var scenario = ConfigureAllRequirementKinds(
            CreateScenario(
                "procedure_advantage_uses_two_contiguous_dice",
                "procedure"));
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_detached_kind_complete_control",
            scenario.RouteId);

        AssertDetachedRequirementAccepted(
            SerializeDetachedRequirementRequest(flow));
    }

    [Theory]
    [InlineData("resolved_row_seal")]
    [InlineData("inactive_success")]
    [InlineData("foreign_actor_role")]
    public void DetachedRequirementAuthority_ResealedSuccessMustMatchT060AndAuthoredRequirement(
        string axis)
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_detached_success_" + axis,
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        AssertDetachedRequirementAccepted(request);
        var binding = Assert.IsType<JsonObject>(Assert.Single(
            ReadDetachedBindings(ReadDetachedScope(request, "common"))));
        var row = ReadJsonObject(binding, "ResolvedRequirement");
        var witness = ReadJsonObject(binding, "SuccessWitness");
        var evidence = ReadJsonObject(witness, "Evidence");

        switch (axis)
        {
            case "resolved_row_seal":
                row[FindJsonPropertyName(row, "AuthorityFingerprint")] =
                    "sha256:" + new string('a', 64);
                break;
            case "inactive_success":
                evidence[FindJsonPropertyName(evidence, "CapabilityActive")] = false;
                witness[FindJsonPropertyName(witness, "WitnessFingerprint")] =
                    ComputeDetachedSuccessWitnessFingerprint(row, witness, evidence);
                break;
            case "foreign_actor_role":
                evidence[FindJsonPropertyName(evidence, "ActorRole")] = "target";
                witness[FindJsonPropertyName(witness, "WitnessFingerprint")] =
                    ComputeDetachedSuccessWitnessFingerprint(row, witness, evidence);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        ResealDetachedRequirementRequest(request);
        AssertDetachedRequirementRejected(request);
    }

    [Theory]
    [InlineData("unknown_loss")]
    [InlineData("reserved_without_observation")]
    [InlineData("foreign_authored_reference")]
    public void DetachedRequirementAuthority_ResealedAbsentFailureUsesClosedAuthoredTopology(
        string axis)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_detached_absent_" + axis,
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        PromoteDetachedCourseRequestToSecondMilestone(request);
        AssertDetachedRequirementAccepted(request);
        var milestone = ReadDetachedScope(request, "course_milestone");
        ReadDetachedBindings(milestone).Clear();
        var failure = CreateDetachedAbsentFailure(
            "course_milestone",
            0,
            "item_quantity",
            "antibiotic_dose");
        ReadDetachedFailures(milestone).Add(failure);
        milestone[FindJsonPropertyName(milestone, "Status")] = "Unsatisfied";
        var bundle = ReadJsonObject(request, "RequirementAuthority");
        bundle[FindJsonPropertyName(bundle, "CourseRequirementStatus")] = "Unsatisfied";
        bundle[FindJsonPropertyName(bundle, "InterruptionReason")] =
            "requirements_unsatisfied";
        MakeDetachedResourceNotRequired(request);

        switch (axis)
        {
            case "unknown_loss":
                failure[FindJsonPropertyName(failure, "LossReason")] = "unknown_loss";
                break;
            case "reserved_without_observation":
                failure[FindJsonPropertyName(failure, "LossReason")] = "reserved";
                break;
            case "foreign_authored_reference":
                failure[FindJsonPropertyName(failure, "AuthorityRef")] =
                    "foreign_antibiotic_dose";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        failure[FindJsonPropertyName(failure, "WitnessFingerprint")] =
            ComputeDetachedFailureWitnessFingerprint(failure);
        ResealDetachedRequirementRequest(request);
        AssertDetachedRequirementRejected(request);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(17)]
    public void DetachedRequirementAuthority_ResealedScopeMustHaveExactBoundedRouteCardinality(
        int rowCount)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_detached_cardinality_" +
            rowCount.ToString(CultureInfo.InvariantCulture),
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        PromoteDetachedCourseRequestToSecondMilestone(request);
        AssertDetachedRequirementAccepted(request);
        var milestone = ReadDetachedScope(request, "course_milestone");
        ReadDetachedBindings(milestone).Clear();
        var failures = ReadDetachedFailures(milestone);
        failures.Add(CreateDetachedAbsentFailure(
            "course_milestone",
            0,
            "item_quantity",
            "antibiotic_dose"));
        for (var index = 1; index < rowCount; index++)
        {
            failures.Add(CreateDetachedAbsentFailure(
                "course_milestone",
                index,
                "item_quantity",
                "foreign_antibiotic_dose_" + index.ToString(CultureInfo.InvariantCulture)));
        }
        milestone[FindJsonPropertyName(milestone, "Status")] = "Unsatisfied";
        var bundle = ReadJsonObject(request, "RequirementAuthority");
        bundle[FindJsonPropertyName(bundle, "CourseRequirementStatus")] = "Unsatisfied";
        bundle[FindJsonPropertyName(bundle, "InterruptionReason")] =
            "requirements_unsatisfied";
        MakeDetachedResourceNotRequired(request);

        ResealDetachedRequirementRequest(request);
        AssertDetachedRequirementRejected(request);
    }

    [Theory]
    [InlineData("noncanonical_loss_reason")]
    [InlineData("forged_cumulative_quantity")]
    public void DetachedRequirementAuthority_ObservedFailureMustBeCanonicalAndLedgerReachable(
        string axis)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_detached_observed_" + axis,
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        PromoteDetachedCourseRequestToSecondMilestone(request);
        AssertDetachedRequirementAccepted(request);
        var milestone = ReadDetachedScope(request, "course_milestone");
        var bindings = ReadDetachedBindings(milestone);
        var binding = Assert.IsType<JsonObject>(Assert.Single(bindings));
        var row = ReadJsonObject(binding, "ResolvedRequirement");
        var witness = ReadJsonObject(binding, "SuccessWitness");
        var evidence = ReadJsonObject(witness, "Evidence").DeepClone().AsObject();

        var lossReason = "quantity_insufficient";
        switch (axis)
        {
            case "noncanonical_loss_reason":
                evidence[FindJsonPropertyName(evidence, "Count")] = 0;
                evidence[FindJsonPropertyName(evidence, "AvailableCount")] = 0;
                evidence[FindJsonPropertyName(evidence, "Lifecycle")] = "retired";
                break;
            case "forged_cumulative_quantity":
                evidence[FindJsonPropertyName(evidence, "Count")] = 1;
                evidence[FindJsonPropertyName(evidence, "AvailableCount")] = 1;
                evidence[FindJsonPropertyName(evidence, "CumulativeRequestedQuantity")] = 2L;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        var failure = CreateDetachedObservedItemFailure(
            "course_milestone",
            row,
            witness,
            evidence,
            lossReason);
        bindings.Clear();
        ReadDetachedFailures(milestone).Add(failure);
        milestone[FindJsonPropertyName(milestone, "Status")] = "Unsatisfied";
        var bundle = ReadJsonObject(request, "RequirementAuthority");
        bundle[FindJsonPropertyName(bundle, "CourseRequirementStatus")] = "Unsatisfied";
        bundle[FindJsonPropertyName(bundle, "InterruptionReason")] =
            "requirements_unsatisfied";
        MakeDetachedResourceNotRequired(request);

        ResealDetachedRequirementRequest(request);
        AssertDetachedRequirementRejected(request);
    }

    [Fact]
    public void DetachedRequirementAuthority_FirstCourseUnsatisfiedCannotBeResealedAsContinuation()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_detached_first_unsatisfied",
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        AssertDetachedRequirementAccepted(request);
        var milestone = ReadDetachedScope(request, "course_milestone");
        ReadDetachedBindings(milestone).Clear();
        ReadDetachedFailures(milestone).Add(CreateDetachedAbsentFailure(
            "course_milestone",
            0,
            "item_quantity",
            "antibiotic_dose"));
        milestone[FindJsonPropertyName(milestone, "Status")] = "Unsatisfied";
        var bundle = ReadJsonObject(request, "RequirementAuthority");
        bundle[FindJsonPropertyName(bundle, "CourseRequirementStatus")] = "Unsatisfied";
        bundle[FindJsonPropertyName(bundle, "InterruptionReason")] = null;
        MakeDetachedResourceNotRequired(request);

        ResealDetachedRequirementRequest(request);
        AssertDetachedRequirementRejected(request);
    }

    [Theory]
    [InlineData("foreign_accepted_state")]
    [InlineData("foreign_coordinates")]
    [InlineData("invalid_course_id")]
    [InlineData("foreign_starting_owner")]
    public void DetachedCourseStartAuthority_ResealedNestedBindingsMustMatchRequest(
        string axis)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_detached_course_start_" + axis,
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        AssertDetachedRequirementAccepted(request);
        var mode = ReadJsonObject(request, "ModeAuthority");
        var start = ReadJsonObject(mode, "CourseStartAuthority");

        switch (axis)
        {
            case "foreign_accepted_state":
                start[FindJsonPropertyName(start, "AcceptedStateFingerprint")] =
                    "sha256:" + new string('a', 64);
                break;
            case "foreign_coordinates":
                start[FindJsonPropertyName(start, "CoordinatesFingerprint")] =
                    "sha256:" + new string('b', 64);
                break;
            case "invalid_course_id":
                start[FindJsonPropertyName(start, "CourseId")] = " invalid_course_id";
                mode[FindJsonPropertyName(mode, "CourseId")] = " invalid_course_id";
                break;
            case "foreign_starting_owner":
                var owner = ReadJsonObject(
                    ReadJsonObject(start, "StartingWound"),
                    "Owner");
                owner[FindJsonPropertyName(owner, "OwnerId")] = "foreign_player";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        ResealDetachedCourseStartGraph(request);
        AssertDetachedCourseAuthorityRejected(request);
    }

    [Theory]
    [InlineData("source_path")]
    [InlineData("contribution_identifier")]
    [InlineData("derived_roll_mode")]
    [InlineData("non_contiguous_dice")]
    [InlineData("tie_selects_right")]
    [InlineData("roll_actor")]
    [InlineData("modifier")]
    [InlineData("difficulty")]
    [InlineData("reaction_without_critical_one")]
    public void DetachedModeAuthority_ResealedProcedureForgeryMustMatchRouteSemantics(
        string axis)
    {
        var scenario = CreateScenario(
            "procedure_advantage_uses_two_contiguous_dice",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_detached_procedure_" + axis,
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        AssertDetachedRequirementAccepted(request);
        var authority = ReadJsonObject(request, "ModeAuthority");
        var contributions = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "RollContributions")]);
        var sourceIndices = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceIndices")]);
        var sourceRolls = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceRolls")]);

        switch (axis)
        {
            case "source_path":
                authority[FindJsonPropertyName(authority, "SourcePath")] =
                    "input/foreign_turn_request.json";
                break;
            case "contribution_identifier":
                var invalidContribution = Assert.IsType<JsonObject>(
                    Assert.Single(contributions));
                invalidContribution[FindJsonPropertyName(
                    invalidContribution,
                    "EffectId")] = " effect_roll_advantage";
                break;
            case "derived_roll_mode":
                Assert.IsType<JsonObject>(Assert.Single(contributions))[
                    FindJsonPropertyName(
                        Assert.IsType<JsonObject>(contributions[0]),
                        "Contribution")] = "disadvantage";
                break;
            case "non_contiguous_dice":
                sourceIndices[1] = 2;
                authority[FindJsonPropertyName(authority, "SelectedSourceIndex")] = 2;
                authority[FindJsonPropertyName(authority, "NaturalRoll")] =
                    sourceRolls[1]!.GetValue<int>();
                break;
            case "tie_selects_right":
                sourceRolls[0] = 10;
                sourceRolls[1] = 10;
                authority[FindJsonPropertyName(authority, "SelectedSourceIndex")] =
                    sourceIndices[1]!.GetValue<int>();
                authority[FindJsonPropertyName(authority, "NaturalRoll")] = 10;
                break;
            case "roll_actor":
                authority[FindJsonPropertyName(authority, "RollActorId")] =
                    "foreign_roll_actor";
                break;
            case "modifier":
                authority[FindJsonPropertyName(authority, "Modifier")] = 99;
                break;
            case "difficulty":
                authority[FindJsonPropertyName(
                    authority,
                    "ComplicationDifficultyModifier")] = 1;
                authority[FindJsonPropertyName(authority, "EffectiveDifficulty")] = 16;
                break;
            case "reaction_without_critical_one":
                authority[FindJsonPropertyName(authority, "PreparedCriticalReaction")] =
                    new JsonObject
                    {
                        ["effectId"] = "effect_forged_fate_shield",
                        ["triggerId"] = "trigger_forged_critical_failure",
                        ["acceptedEffectFingerprint"] =
                            "sha256:" + new string('a', 64),
                        ["preparedReactionFingerprint"] =
                            "sha256:" + new string('b', 64)
                    };
                ResealDetachedPreparedReaction(authority);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        ResealDetachedProcedureAuthorityAndRequest(request);
        AssertDetachedModeAuthorityRejected(request);
    }

    [Fact]
    public void DetachedModeAuthority_ResealedCriticalReactionRequiresAcceptedFingerprintForm()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_detached_reaction_fingerprint",
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        AssertDetachedRequirementAccepted(request);
        var authority = ReadJsonObject(request, "ModeAuthority");
        var reaction = ReadJsonObject(authority, "PreparedCriticalReaction");
        reaction[FindJsonPropertyName(reaction, "AcceptedEffectFingerprint")] =
            "forged_accepted_effect";

        ResealDetachedPreparedReaction(authority);
        ResealDetachedProcedureAuthorityAndRequest(request);
        AssertDetachedModeAuthorityRejected(request);
    }

    [Fact]
    public void DetachedModeAuthority_FactoryProducedSeventeenRollContributionsRoundTrip()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.ReplacePlayerProcedureRollEffects(
            "seventeen_distinct_roll_modifiers",
            Enumerable.Repeat("advantage", 17).ToArray());
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_seventeen_roll_modifiers",
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        var authority = ReadJsonObject(request, "ModeAuthority");
        Assert.Equal(
            17,
            Assert.IsType<JsonArray>(authority[
                FindJsonPropertyName(authority, "RollContributions")]).Count);

        AssertDetachedRequirementAccepted(request);
    }

    [Theory]
    [InlineData("snapshot_binding")]
    [InlineData("capability_binding")]
    [InlineData("owner_binding")]
    [InlineData("source_path")]
    [InlineData("owner_kind")]
    [InlineData("skill_kind")]
    [InlineData("wound_domain")]
    [InlineData("severity_envelope")]
    [InlineData("closed_complication_kind")]
    [InlineData("sorted_complication_kinds")]
    [InlineData("numeric_limits")]
    [InlineData("empty_limits")]
    [InlineData("source_fingerprint")]
    [InlineData("outcome_coverage")]
    public void DetachedModeAuthority_ResealedGuaranteedForgeryMustMatchRouteSemantics(
        string axis)
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_detached_guaranteed_" + axis,
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        AssertDetachedRequirementAccepted(request);
        var proof = ReadJsonObject(request, "ModeAuthority");
        var limits = ReadJsonObject(proof, "OperationLimits");
        var removableKinds = Assert.IsType<JsonArray>(limits[
            FindJsonPropertyName(limits, "RemovableComplicationKinds")]);

        switch (axis)
        {
            case "snapshot_binding":
                proof[FindJsonPropertyName(proof, "SnapshotToken")] =
                    "foreign_snapshot_token";
                break;
            case "capability_binding":
                proof[FindJsonPropertyName(proof, "CapabilityRef")] =
                    "foreign_healing_capability";
                break;
            case "owner_binding":
                proof[FindJsonPropertyName(proof, "OwnerId")] =
                    "foreign_provider";
                break;
            case "source_path":
                proof[FindJsonPropertyName(proof, "SourcePath")] =
                    "game_state/foreign/skills.json";
                break;
            case "owner_kind":
                proof[FindJsonPropertyName(proof, "OwnerKind")] = "combatant";
                break;
            case "skill_kind":
                proof[FindJsonPropertyName(proof, "SkillKind")] = "ritual";
                break;
            case "wound_domain":
                proof[FindJsonPropertyName(proof, "WoundDomain")] = "spiritual";
                break;
            case "severity_envelope":
                proof[FindJsonPropertyName(proof, "MaximumSeverityRank")] = 5;
                break;
            case "closed_complication_kind":
                removableKinds.Add("curse");
                break;
            case "sorted_complication_kinds":
                removableKinds.Add("pain");
                removableKinds.Add("bleeding");
                break;
            case "numeric_limits":
                limits[FindJsonPropertyName(
                    limits,
                    "MaximumSeverityReductionSteps")] = 4;
                break;
            case "empty_limits":
                SetDetachedCapabilityLimitsToEmpty(limits, removableKinds);
                break;
            case "source_fingerprint":
                break;
            case "outcome_coverage":
                limits[FindJsonPropertyName(limits, "MayStabilize")] = false;
                limits[FindJsonPropertyName(limits, "MaximumRecoveryPoints")] = 1;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        ResealDetachedCapabilityProofAndRequest(request);
        if (string.Equals(axis, "source_fingerprint", StringComparison.Ordinal))
        {
            proof[FindJsonPropertyName(proof, "SourceSemanticFingerprint")] =
                "sha256:" + new string('c', 64);
            ResealDetachedCapabilityProofUsingCurrentSourceAndRequest(request);
        }
        AssertDetachedModeAuthorityRejected(request);
    }

    [Fact]
    public void DetachedModeAuthority_ResealedSourceCapabilityMayBeAbsentFromLocation()
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_detached_source_absent",
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        var binding = Assert.IsType<JsonObject>(Assert.Single(
            ReadDetachedBindings(ReadDetachedScope(request, "common"))));
        var row = ReadJsonObject(binding, "ResolvedRequirement");
        var witness = ReadJsonObject(binding, "SuccessWitness");
        var evidence = ReadJsonObject(witness, "Evidence");
        evidence[FindJsonPropertyName(evidence, "ActorPresent")] = false;
        witness[FindJsonPropertyName(witness, "WitnessFingerprint")] =
            ComputeDetachedSuccessWitnessFingerprint(row, witness, evidence);

        ResealDetachedRequirementRequest(request);
        AssertDetachedRequirementAccepted(request);
    }

    private static void ResealDetachedPreparedReaction(JsonObject authority)
    {
        var reaction = ReadJsonObject(authority, "PreparedCriticalReaction");
        reaction[FindJsonPropertyName(reaction, "PreparedReactionFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.prepared_critical_reaction",
                "1",
                ReadJsonString(reaction, "EffectId"),
                ReadJsonString(reaction, "TriggerId"),
                ReadJsonString(reaction, "AcceptedEffectFingerprint"),
                ReadJsonString(authority, "CoordinatesFingerprint"),
                ReadJsonString(authority, "AcceptedStateFingerprint")
            });
    }

    private static void ResealDetachedProcedureAuthorityAndRequestWithRollSkillId(
        JsonObject request)
    {
        var authority = ReadJsonObject(request, "ModeAuthority");
        var contributions = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "RollContributions")]);
        var sourceIndices = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceIndices")]);
        var sourceRolls = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceRolls")]);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.procedure_check_authority",
            "1",
            ReadJsonString(authority, "SourcePath"),
            ReadJsonString(authority, "RollMode"),
            ReadJsonString(authority, "RollActorKind"),
            ReadJsonString(authority, "RollActorId"),
            ReadOptionalJsonString(authority, "RollSkillId"),
            DetachedNumber(contributions.Count)
        };
        for (var index = 0; index < contributions.Count; index++)
        {
            var contribution = Assert.IsType<JsonObject>(contributions[index]);
            fields.Add(DetachedNumber(index));
            fields.Add(ReadJsonString(contribution, "EffectId"));
            fields.Add(ReadJsonString(contribution, "ComponentId"));
            fields.Add(ReadJsonString(contribution, "Contribution"));
        }
        fields.Add(DetachedNumber(sourceIndices.Count));
        for (var index = 0; index < sourceIndices.Count; index++)
        {
            fields.Add(DetachedNumber(index));
            fields.Add(DetachedNumber(sourceIndices[index]!.GetValue<int>()));
            fields.Add(DetachedNumber(sourceRolls[index]!.GetValue<int>()));
        }
        foreach (var property in new[]
                 {
                     "SelectedSourceIndex", "NaturalRoll", "Modifier",
                     "ComplicationDifficultyModifier", "EffectiveDifficulty"
                 })
        {
            fields.Add(DetachedNumber(authority[
                FindJsonPropertyName(authority, property)]!.GetValue<int>()));
        }
        fields.Add(ReadJsonString(authority, "RequirementAuthorityFingerprint"));
        fields.Add(ReadJsonString(authority, "CoordinatesFingerprint"));
        fields.Add(ReadJsonString(authority, "AcceptedStateFingerprint"));
        fields.Add(authority[FindJsonPropertyName(authority, "PreparedCriticalReaction")]
            is JsonObject prepared
            ? ReadJsonString(prepared, "PreparedReactionFingerprint")
            : null);
        authority[FindJsonPropertyName(authority, "AuthorityFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(fields);
        ResealDetachedRequestOnly(request);
    }

    private static void SetDetachedCapabilityLimitsToEmpty(
        JsonObject limits,
        JsonArray removableKinds)
    {
        limits[FindJsonPropertyName(limits, "MayStabilize")] = false;
        limits[FindJsonPropertyName(limits, "MaximumRecoveryPoints")] = 0;
        limits[FindJsonPropertyName(limits, "MaximumSeverityReductionSteps")] = 0;
        removableKinds.Clear();
        limits[FindJsonPropertyName(limits, "MayHealAtSeverityI")] = false;
        limits[FindJsonPropertyName(limits, "MaximumCosmeticHealLegacies")] = 0;
        limits[FindJsonPropertyName(
            limits,
            "MaximumMechanicalEffectHealLegacies")] = 0;
    }

    private static void ResealDetachedCapabilityProofUsingCurrentSourceAndRequest(
        JsonObject request)
    {
        var proof = ReadJsonObject(request, "ModeAuthority");
        var limits = ReadJsonObject(proof, "OperationLimits");
        var removableKinds = Assert.IsType<JsonArray>(limits[
            FindJsonPropertyName(limits, "RemovableComplicationKinds")]);
        proof[FindJsonPropertyName(proof, "ProofFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.capability_proof",
                "1",
                ReadJsonString(proof, "SnapshotToken"),
                ReadJsonString(proof, "SourcePath"),
                ReadJsonString(proof, "OwnerKind"),
                ReadJsonString(proof, "OwnerId"),
                ReadJsonString(proof, "SkillKind"),
                ReadJsonString(proof, "SkillId"),
                ReadJsonString(proof, "CapabilityRef"),
                ReadJsonString(proof, "WoundDomain"),
                DetachedNumber(ReadDetachedInt32(proof, "MinimumSeverityRank")),
                DetachedNumber(ReadDetachedInt32(proof, "MaximumSeverityRank")),
                DetachedBoolean(ReadDetachedBoolean(limits, "MayStabilize")),
                DetachedNumber(ReadDetachedInt32(limits, "MaximumRecoveryPoints")),
                DetachedNumber(ReadDetachedInt32(
                    limits,
                    "MaximumSeverityReductionSteps")),
                DetachedNumber(removableKinds.Count),
                string.Join(",", removableKinds.Select(
                    static value => value!.GetValue<string>())),
                DetachedBoolean(ReadDetachedBoolean(limits, "MayHealAtSeverityI")),
                DetachedNumber(ReadDetachedInt32(
                    limits,
                    "MaximumCosmeticHealLegacies")),
                DetachedNumber(ReadDetachedInt32(
                    limits,
                    "MaximumMechanicalEffectHealLegacies")),
                ReadJsonString(proof, "ContextFingerprint"),
                ReadJsonString(proof, "AcceptedStateFingerprint"),
                ReadJsonString(proof, "CoordinatesFingerprint"),
                ReadJsonString(proof, "SourceSemanticFingerprint")
            });
        ResealDetachedRequestOnly(request);
    }

    private static void AssertDetachedModeAuthorityRejected(JsonObject request)
    {
        var issues = new List<ValidationIssue>();
        var accepted = MortalWoundTreatmentCommandCodec.TryParseRequest(
            request,
            "request",
            issues,
            out _);
        Assert.False(accepted, DescribeIssues(issues));
        Assert.Contains(issues, static issue =>
            string.Equals(
                issue.Code,
                "mortal_wound_treatment_persisted_request_seal_mismatch",
                StringComparison.Ordinal) &&
            Convert.ToString(issue.Actual)?.Contains(
                "mode_authority",
                StringComparison.Ordinal) == true);
    }

    private static void PromoteDetachedCourseRequestToSecondMilestone(
        JsonObject request)
    {
        const int ordinal = 2;
        request[FindJsonPropertyName(request, "MilestoneOrdinal")] = ordinal;
        var mode = ReadJsonObject(request, "ModeAuthority");
        var start = ReadJsonObject(mode, "CourseStartAuthority");
        var startingWound = ReadJsonObject(start, "StartingWound");
        var originalDue = mode[FindJsonPropertyName(mode, "DueAtGameTimeMinutes")]!
            .GetValue<long>();
        var originalDeadline = mode[
            FindJsonPropertyName(mode, "DeadlineAtGameTimeMinutes")]!.GetValue<long>();
        var due = start[FindJsonPropertyName(start, "StartedAtGameTimeMinutes")]!
            .GetValue<long>() + 480L;
        var deadline = checked(due + originalDeadline - originalDue);
        var gameTime = ReadJsonObject(mode, "GameTimeAuthority");
        gameTime[FindJsonPropertyName(gameTime, "CurrentTimeInMinutes")] = due;
        gameTime[FindJsonPropertyName(gameTime, "AuthorityFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.game_time_authority",
                "1",
                "world_time.currentTimeInMinutes",
                "game_state/world/world_time.json",
                DetachedNumber(due),
                ReadJsonString(gameTime, "CoordinatesFingerprint"),
                ReadJsonString(gameTime, "AcceptedStateFingerprint")
            });
        var courseCoordinate = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.course_coordinates",
            "1",
            ReadJsonString(mode, "CourseId"),
            DetachedNumber(ordinal),
            ReadJsonString(startingWound, "WoundId"),
            ReadJsonString(start, "StartingWoundFingerprint"),
            ReadJsonString(start, "RouteId"),
            ReadJsonString(start, "RouteFingerprint"),
            ReadJsonString(start, "AuthorityFingerprint")
        });
        mode[FindJsonPropertyName(mode, "MilestoneOrdinal")] = ordinal;
        mode[FindJsonPropertyName(mode, "DueAtGameTimeMinutes")] = due;
        mode[FindJsonPropertyName(mode, "DeadlineAtGameTimeMinutes")] = deadline;
        mode[FindJsonPropertyName(mode, "WindowDisposition")] = "ready";
        mode[FindJsonPropertyName(mode, "CourseCoordinateFingerprint")] =
            courseCoordinate;
        mode[FindJsonPropertyName(mode, "AuthorityFingerprint")] =
            ComputeSerializedCourseAuthorityFingerprint(mode);

        var bundle = ReadJsonObject(request, "RequirementAuthority");
        bundle[FindJsonPropertyName(bundle, "CourseMilestoneOrdinal")] = ordinal;
        bundle[FindJsonPropertyName(bundle, "CourseCoordinateFingerprint")] =
            courseCoordinate;
        var milestone = ReadDetachedScope(request, "course_milestone");
        milestone[FindJsonPropertyName(milestone, "CourseMilestoneOrdinal")] = ordinal;

        var resource = ReadJsonObject(request, "ResourceAuthority");
        resource[FindJsonPropertyName(resource, "CourseMilestoneOrdinal")] = ordinal;
        resource[FindJsonPropertyName(resource, "CourseCoordinateFingerprint")] =
            courseCoordinate;
        ResealDetachedRequirementRequest(request);
    }

    private static void ResealDetachedCourseStartGraph(JsonObject request)
    {
        var mode = ReadJsonObject(request, "ModeAuthority");
        var start = ReadJsonObject(mode, "CourseStartAuthority");
        var startingWound = ReadJsonObject(start, "StartingWound");
        var parsed = WoundMaterializationContract.Parse(
            startingWound.ToJsonString(),
            "detachedCourseStart.startingWound");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var typedWound = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var startingFingerprint = WoundIdentityState.ComputeSemanticFingerprint(
            typedWound);
        start[FindJsonPropertyName(start, "StartingWoundFingerprint")] =
            startingFingerprint;
        start[FindJsonPropertyName(start, "AuthorityFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.course_start_authority",
                "1",
                ReadJsonString(start, "CourseId"),
                ReadJsonString(start, "RouteId"),
                ReadJsonString(start, "RouteFingerprint"),
                WoundMaterializationContract.SerializeCanonical(typedWound),
                startingFingerprint,
                DetachedNumber(ReadDetachedInt64(start, "StartedAtGameTimeMinutes")),
                ReadJsonString(start, "AcceptedStateFingerprint"),
                ReadJsonString(start, "CoordinatesFingerprint")
            });
        var courseCoordinate = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.course_coordinates",
            "1",
            ReadJsonString(mode, "CourseId"),
            DetachedNumber(ReadDetachedInt32(mode, "MilestoneOrdinal")),
            ReadJsonString(startingWound, "WoundId"),
            startingFingerprint,
            ReadJsonString(start, "RouteId"),
            ReadJsonString(start, "RouteFingerprint"),
            ReadJsonString(start, "AuthorityFingerprint")
        });
        mode[FindJsonPropertyName(mode, "CourseCoordinateFingerprint")] =
            courseCoordinate;
        mode[FindJsonPropertyName(mode, "AuthorityFingerprint")] =
            ComputeSerializedCourseAuthorityFingerprint(mode);

        var bundle = ReadJsonObject(request, "RequirementAuthority");
        bundle[FindJsonPropertyName(bundle, "CourseId")] =
            ReadJsonString(mode, "CourseId");
        bundle[FindJsonPropertyName(bundle, "CourseCoordinateFingerprint")] =
            courseCoordinate;
        var resource = ReadJsonObject(request, "ResourceAuthority");
        resource[FindJsonPropertyName(resource, "CourseId")] =
            ReadJsonString(mode, "CourseId");
        resource[FindJsonPropertyName(resource, "CourseCoordinateFingerprint")] =
            courseCoordinate;
        ResealDetachedRequirementRequest(request);
    }

    private static JsonObject SerializeDetachedRequirementRequest(TreatmentFlow flow) =>
        Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(flow.Request));

    private static JsonObject ReadDetachedScope(JsonObject request, string scope)
    {
        var bundle = ReadJsonObject(request, "RequirementAuthority");
        var scopes = Assert.IsType<JsonArray>(bundle[
            FindJsonPropertyName(bundle, "Scopes")]);
        return Assert.Single(scopes.OfType<JsonObject>(), candidate =>
            string.Equals(ReadJsonString(candidate, "Scope"), scope,
                StringComparison.Ordinal));
    }

    private static JsonArray ReadDetachedBindings(JsonObject scope) =>
        Assert.IsType<JsonArray>(scope[FindJsonPropertyName(scope, "Bindings")]);

    private static JsonArray ReadDetachedFailures(JsonObject scope) =>
        Assert.IsType<JsonArray>(scope[FindJsonPropertyName(scope, "FailureWitnesses")]);

    private static JsonObject CreateDetachedAbsentFailure(
        string scope,
        int requirementIndex,
        string kind,
        string authorityRef)
    {
        var failure = new JsonObject
        {
            ["scope"] = scope,
            ["requirementIndex"] = requirementIndex,
            ["kind"] = kind,
            ["authorityRef"] = authorityRef,
            ["lossReason"] = "authority_absent",
            ["observation"] = null,
            ["witnessFingerprint"] = string.Empty
        };
        failure["witnessFingerprint"] =
            ComputeDetachedFailureWitnessFingerprint(failure);
        return failure;
    }

    private static JsonObject CreateDetachedObservedItemFailure(
        string scope,
        JsonObject row,
        JsonObject successWitness,
        JsonObject evidence,
        string lossReason)
    {
        var observation = new JsonObject
        {
            ["snapshotToken"] = ReadJsonString(successWitness, "SnapshotToken"),
            ["realm"] = ReadJsonString(row, "Realm"),
            ["ownerKind"] = ReadOptionalJsonString(row, "OwnerKind"),
            ["ownerId"] = ReadOptionalJsonString(row, "OwnerId"),
            ["providerKind"] = ReadOptionalJsonString(row, "ProviderKind"),
            ["providerId"] = ReadOptionalJsonString(row, "ProviderId"),
            ["targetKind"] = ReadOptionalJsonString(row, "TargetKind"),
            ["targetId"] = ReadOptionalJsonString(row, "TargetId"),
            ["locationId"] = ReadOptionalJsonString(row, "LocationId"),
            ["evidence"] = evidence,
            ["observationFingerprint"] = string.Empty
        };
        observation["observationFingerprint"] =
            ComputeDetachedItemObservationFingerprint(
                observation,
                evidence,
                ReadJsonString(row, "AuthorityRef"));
        var failure = new JsonObject
        {
            ["scope"] = scope,
            ["requirementIndex"] = ReadDetachedInt32(row, "RequirementIndex"),
            ["kind"] = ReadJsonString(row, "Kind"),
            ["authorityRef"] = ReadJsonString(row, "AuthorityRef"),
            ["lossReason"] = lossReason,
            ["observation"] = observation,
            ["witnessFingerprint"] = string.Empty
        };
        failure["witnessFingerprint"] =
            ComputeDetachedFailureWitnessFingerprint(failure);
        return failure;
    }

    private static string ComputeDetachedSuccessWitnessFingerprint(
        JsonObject row,
        JsonObject witness,
        JsonObject evidence)
    {
        Assert.Equal("source_capability", ReadJsonString(row, "Kind"));
        var mechanicalFields = DetachedSourceCapabilityMechanicalFields(row, evidence);
        var evidenceFields = DetachedSourceCapabilityEvidenceFields(evidence);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_success_witness",
            "1",
            ReadJsonString(witness, "Scope"),
            DetachedNumber(ReadDetachedInt32(row, "RequirementIndex")),
            ReadJsonString(row, "Kind"),
            ReadJsonString(row, "AuthorityRef"),
            ReadJsonString(witness, "SnapshotToken"),
            ReadJsonString(row, "Realm"),
            ReadOptionalJsonString(row, "OwnerKind"),
            ReadOptionalJsonString(row, "OwnerId"),
            ReadOptionalJsonString(row, "ProviderKind"),
            ReadOptionalJsonString(row, "ProviderId"),
            ReadOptionalJsonString(row, "TargetKind"),
            ReadOptionalJsonString(row, "TargetId"),
            ReadOptionalJsonString(row, "LocationId"),
            ReadJsonString(evidence, "Kind"),
            DetachedNumber(mechanicalFields.Count)
        };
        fields.AddRange(mechanicalFields);
        fields.Add(DetachedNumber(evidenceFields.Count));
        fields.AddRange(evidenceFields);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static IReadOnlyList<string?> DetachedSourceCapabilityMechanicalFields(
        JsonObject row,
        JsonObject evidence) => new string?[]
    {
        ReadOptionalJsonString(row, "OwnerKind"),
        ReadOptionalJsonString(row, "OwnerId"),
        ReadJsonString(row, "Realm"),
        ReadJsonString(evidence, "ActorCurrentLocationId"),
        ReadJsonString(evidence, "ActorLifecycle"),
        DetachedBoolean(ReadDetachedBoolean(evidence, "ActorActive")),
        DetachedBoolean(ReadDetachedBoolean(evidence, "ActorReachable")),
        ReadJsonString(row, "AuthorityRef"),
        ReadJsonString(evidence, "CapabilityLifecycle"),
        DetachedBoolean(ReadDetachedBoolean(evidence, "CapabilityActive"))
    };

    private static IReadOnlyList<string?> DetachedSourceCapabilityEvidenceFields(
        JsonObject evidence) => new string?[]
    {
        ReadJsonString(evidence, "Kind"),
        ReadJsonString(evidence, "ActorRole"),
        ReadJsonString(evidence, "ActorCurrentLocationId"),
        ReadJsonString(evidence, "RequiredLocationId"),
        ReadJsonString(evidence, "ActorLifecycle"),
        DetachedBoolean(ReadDetachedBoolean(evidence, "ActorActive")),
        DetachedBoolean(ReadDetachedBoolean(evidence, "ActorReachable")),
        DetachedBoolean(ReadDetachedBoolean(evidence, "ActorPresent")),
        ReadJsonString(evidence, "CapabilityLifecycle"),
        DetachedBoolean(ReadDetachedBoolean(evidence, "CapabilityActive"))
    };

    private static string ComputeDetachedItemObservationFingerprint(
        JsonObject observation,
        JsonObject evidence,
        string authorityRef)
    {
        var mechanicalFields = new string?[]
        {
            authorityRef,
            ReadJsonString(observation, "Realm"),
            ReadOptionalJsonString(observation, "OwnerKind"),
            ReadOptionalJsonString(observation, "OwnerId"),
            DetachedNumber(ReadDetachedInt32(evidence, "Count")),
            DetachedNumber(ReadDetachedInt32(evidence, "AvailableCount")),
            ReadJsonString(evidence, "ReservationState"),
            ReadJsonString(evidence, "Lifecycle"),
            DetachedBoolean(ReadDetachedBoolean(evidence, "Active")),
            DetachedNumber(ReadDetachedInt64(evidence, "CumulativeRequestedQuantity"))
        };
        var evidenceFields = new string?[]
        {
            ReadJsonString(evidence, "Kind"),
            ReadJsonString(evidence, "OwnerRole"),
            DetachedNumber(ReadDetachedInt32(evidence, "RequestedQuantity")),
            DetachedNumber(ReadDetachedInt32(evidence, "Count")),
            DetachedNumber(ReadDetachedInt32(evidence, "AvailableCount")),
            ReadJsonString(evidence, "ReservationState"),
            ReadJsonString(evidence, "Lifecycle"),
            DetachedBoolean(ReadDetachedBoolean(evidence, "Active")),
            DetachedNumber(ReadDetachedInt64(evidence, "CumulativeRequestedQuantity"))
        };
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_failure_observation",
            "1",
            ReadJsonString(observation, "SnapshotToken"),
            ReadJsonString(observation, "Realm"),
            ReadOptionalJsonString(observation, "OwnerKind"),
            ReadOptionalJsonString(observation, "OwnerId"),
            ReadOptionalJsonString(observation, "ProviderKind"),
            ReadOptionalJsonString(observation, "ProviderId"),
            ReadOptionalJsonString(observation, "TargetKind"),
            ReadOptionalJsonString(observation, "TargetId"),
            ReadOptionalJsonString(observation, "LocationId"),
            ReadJsonString(evidence, "Kind"),
            DetachedNumber(mechanicalFields.Length)
        };
        fields.AddRange(mechanicalFields);
        fields.Add(DetachedNumber(evidenceFields.Length));
        fields.AddRange(evidenceFields);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string ComputeDetachedFailureWitnessFingerprint(JsonObject failure) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.requirement_failure_witness",
            "1",
            ReadJsonString(failure, "Scope"),
            DetachedNumber(ReadDetachedInt32(failure, "RequirementIndex")),
            ReadJsonString(failure, "Kind"),
            ReadJsonString(failure, "AuthorityRef"),
            ReadJsonString(failure, "LossReason"),
            failure[FindJsonPropertyName(failure, "Observation")] is JsonObject observation
                ? ReadJsonString(observation, "ObservationFingerprint")
                : null
        });

    private static void ResealDetachedRequirementRequest(JsonObject request)
    {
        var bundle = ReadJsonObject(request, "RequirementAuthority");
        var scopes = Assert.IsType<JsonArray>(bundle[
            FindJsonPropertyName(bundle, "Scopes")]);
        foreach (var scope in scopes.OfType<JsonObject>())
        {
            var bindings = ReadDetachedBindings(scope);
            foreach (var binding in bindings.OfType<JsonObject>())
            {
                var row = ReadJsonObject(binding, "ResolvedRequirement");
                var witness = ReadJsonObject(binding, "SuccessWitness");
                binding[FindJsonPropertyName(binding, "BindingFingerprint")] =
                    WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                    {
                        "book_of_eternity.mortal_wound_treatment.requirement_binding",
                        "1",
                        ReadJsonString(witness, "Scope"),
                        DetachedNumber(ReadDetachedInt32(row, "RequirementIndex")),
                        ReadJsonString(row, "AuthorityFingerprint"),
                        ReadJsonString(witness, "WitnessFingerprint")
                    });
            }

            var failures = ReadDetachedFailures(scope);
            foreach (var failure in failures.OfType<JsonObject>())
            {
                failure[FindJsonPropertyName(failure, "WitnessFingerprint")] =
                    ComputeDetachedFailureWitnessFingerprint(failure);
            }
            var scopeFields = new List<string?>
            {
                "book_of_eternity.mortal_wound_treatment.requirement_scope",
                "1",
                ReadJsonString(scope, "Scope"),
                ReadOptionalJsonInt32(scope, "CourseMilestoneOrdinal") is { } scopeOrdinal
                    ? DetachedNumber(scopeOrdinal)
                    : null,
                ReadJsonString(scope, "Status"),
                DetachedNumber(bindings.Count)
            };
            scopeFields.AddRange(bindings.OfType<JsonObject>().Select(binding =>
                ReadJsonString(binding, "BindingFingerprint")));
            scopeFields.Add(DetachedNumber(failures.Count));
            scopeFields.AddRange(failures.OfType<JsonObject>().Select(failure =>
                ReadJsonString(failure, "WitnessFingerprint")));
            scope[FindJsonPropertyName(scope, "AuthorityFingerprint")] =
                WoundAcceptedTurnFingerprintWriter.Compute(scopeFields);
        }

        var bundleFields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.requirement_bundle",
            "1",
            ReadJsonString(bundle, "Mode"),
            ReadJsonString(bundle, "ContextFingerprint"),
            ReadJsonString(bundle, "AcceptedStateFingerprint"),
            ReadJsonString(bundle, "RouteFingerprint"),
            ReadOptionalJsonString(bundle, "CourseId"),
            ReadOptionalJsonInt32(bundle, "CourseMilestoneOrdinal") is { } bundleOrdinal
                ? DetachedNumber(bundleOrdinal)
                : null,
            ReadOptionalJsonString(bundle, "CourseCoordinateFingerprint"),
            ReadOptionalJsonString(bundle, "CourseRequirementStatus"),
            ReadOptionalJsonString(bundle, "InterruptionReason"),
            DetachedNumber(scopes.Count)
        };
        bundleFields.AddRange(scopes.OfType<JsonObject>().Select(scope =>
            ReadJsonString(scope, "AuthorityFingerprint")));
        var bundleFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(bundleFields);
        bundle[FindJsonPropertyName(bundle, "AuthorityFingerprint")] = bundleFingerprint;

        var resource = ReadJsonObject(request, "ResourceAuthority");
        resource[FindJsonPropertyName(resource, "RequirementAuthorityFingerprint")] =
            bundleFingerprint;
        ResealSerializedResourceAndRequest(
            request,
            ComputeSerializedPolicyFingerprint(ReadJsonObject(resource, "Policy")));
    }

    private static void MakeDetachedResourceNotRequired(JsonObject request)
    {
        var resource = ReadJsonObject(request, "ResourceAuthority");
        Assert.IsType<JsonArray>(resource[
            FindJsonPropertyName(resource, "Claims")]).Clear();
        resource[FindJsonPropertyName(resource, "ReservationDisposition")] =
            "not_required";
        resource[FindJsonPropertyName(resource, "ReservationId")] = null;
    }

    private static void AssertDetachedRequirementRejected(JsonObject request)
    {
        var issues = new List<ValidationIssue>();
        var accepted = MortalWoundTreatmentCommandCodec.TryParseRequest(
            request,
            "request",
            issues,
            out _);
        Assert.False(accepted, DescribeIssues(issues));
        Assert.True(issues.Any(static issue =>
            string.Equals(
                issue.Code,
                "mortal_wound_treatment_persisted_request_seal_mismatch",
                StringComparison.Ordinal) &&
            Convert.ToString(issue.Actual)?.Contains(
                "requirement_authority",
                StringComparison.Ordinal) == true),
            DescribeIssues(issues));
    }

    private static void AssertDetachedCourseAuthorityRejected(JsonObject request)
    {
        var issues = new List<ValidationIssue>();
        var accepted = MortalWoundTreatmentCommandCodec.TryParseRequest(
            request,
            "request",
            issues,
            out _);
        Assert.False(accepted, DescribeIssues(issues));
        Assert.Contains(issues, static issue =>
            string.Equals(
                issue.Code,
                "mortal_wound_treatment_persisted_request_seal_mismatch",
                StringComparison.Ordinal) &&
            Convert.ToString(issue.Actual)?.Contains(
                "mode_authority.course_start_binding",
                StringComparison.Ordinal) == true);
    }

    private static void AssertDetachedRequirementAccepted(JsonObject request)
    {
        var issues = new List<ValidationIssue>();
        var accepted = MortalWoundTreatmentCommandCodec.TryParseRequest(
            request,
            "request",
            issues,
            out var parsed);
        Assert.True(accepted, DescribeIssues(issues));
        Assert.NotNull(parsed);
    }

    private static int ReadDetachedInt32(JsonObject value, string propertyName) =>
        value[FindJsonPropertyName(value, propertyName)]!.GetValue<int>();

    private static long ReadDetachedInt64(JsonObject value, string propertyName) =>
        value[FindJsonPropertyName(value, propertyName)]!.GetValue<long>();

    private static bool ReadDetachedBoolean(JsonObject value, string propertyName) =>
        value[FindJsonPropertyName(value, propertyName)]!.GetValue<bool>();

    private static string DetachedNumber(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string DetachedNumber(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string DetachedBoolean(bool value) => value ? "true" : "false";
}
