using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T067-B cold-reducer regressions. These requests are internally consistent and have
/// every enclosing detached seal recomputed, but disagree with the fresh accepted-state
/// authority supplied to the reducer.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void FreshAuthority_RejectsResealedProcedureDieOutsideAcceptedPoolEvidence()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_fresh_die",
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        var authority = ReadJsonObject(request, "ModeAuthority");
        var sourceRolls = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceRolls")]);
        var forgedRoll = sourceRolls[0]!.GetValue<int>() == 20
            ? 19
            : sourceRolls[0]!.GetValue<int>() + 1;
        sourceRolls[0] = forgedRoll;
        authority[FindJsonPropertyName(authority, "NaturalRoll")] = forgedRoll;

        ResealDetachedProcedureAuthorityAndRequest(request);
        var parsed = ParseFreshAuthorityRequest(request);

        AssertFreshAuthorityRejected(MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            parsed,
            flow.History,
            flow.Before,
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState)));
    }

    [Fact]
    public void FreshAuthority_RejectsResealedProcedureContributionAbsentFromAcceptedEffects()
    {
        var scenario = CreateScenario(
            "procedure_advantage_uses_two_contiguous_dice",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_fresh_effect",
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        var authority = ReadJsonObject(request, "ModeAuthority");
        var contribution = Assert.IsType<JsonObject>(Assert.Single(
            Assert.IsType<JsonArray>(authority[
                FindJsonPropertyName(authority, "RollContributions")])))!;
        contribution[FindJsonPropertyName(contribution, "Contribution")] =
            "disadvantage";
        authority[FindJsonPropertyName(authority, "RollMode")] = "disadvantage";
        var sourceIndices = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceIndices")]);
        var sourceRolls = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceRolls")]);
        authority[FindJsonPropertyName(authority, "SelectedSourceIndex")] =
            sourceIndices[0]!.GetValue<int>();
        authority[FindJsonPropertyName(authority, "NaturalRoll")] =
            sourceRolls[0]!.GetValue<int>();

        ResealDetachedProcedureAuthorityAndRequest(request);
        var parsed = ParseFreshAuthorityRequest(request);

        AssertFreshAuthorityRejected(MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            parsed,
            flow.History,
            flow.Before,
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState)));
    }

    [Fact]
    public void FreshAuthority_RejectsResealedNewerFateShieldInsteadOfOldestCandidate()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_fresh_fate_order",
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var candidates = acceptedState.EffectMechanics.FateShieldReactionCandidates;
        Assert.True(candidates.Count >= 2);
        var newer = candidates[1];
        var request = SerializeDetachedRequirementRequest(flow);
        var authority = ReadJsonObject(request, "ModeAuthority");
        var prepared = ReadJsonObject(authority, "PreparedCriticalReaction");
        prepared[FindJsonPropertyName(prepared, "EffectId")] = newer.EffectId;
        prepared[FindJsonPropertyName(prepared, "TriggerId")] = newer.TriggerId;
        prepared[FindJsonPropertyName(prepared, "AcceptedEffectFingerprint")] =
            newer.AcceptedEffectFingerprint;
        prepared[FindJsonPropertyName(prepared, "PreparedReactionFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.prepared_critical_reaction",
                "1",
                newer.EffectId,
                newer.TriggerId,
                newer.AcceptedEffectFingerprint,
                ReadJsonString(authority, "CoordinatesFingerprint"),
                ReadJsonString(authority, "AcceptedStateFingerprint")
            });

        ResealDetachedProcedureAuthorityAndRequest(request);
        var parsed = ParseFreshAuthorityRequest(request);

        AssertFreshAuthorityRejected(MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            parsed,
            flow.History,
            flow.Before,
            acceptedState));
    }

    [Fact]
    public void FreshAuthority_RejectsResealedGuaranteedProofAbsentFromCanonicalCatalog()
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey + "_fresh_capability",
            scenario.RouteId);
        var request = SerializeDetachedRequirementRequest(flow);
        var proof = ReadJsonObject(request, "ModeAuthority");
        var limits = ReadJsonObject(proof, "OperationLimits");
        limits[FindJsonPropertyName(limits, "MaximumRecoveryPoints")] = 3;

        ResealDetachedCapabilityProofAndRequest(request);
        var parsed = ParseFreshAuthorityRequest(request);

        AssertFreshAuthorityRejected(MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(
            parsed,
            flow.History,
            flow.Before,
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(flow.AcceptedState)));
    }

    [Fact]
    public void FreshAuthority_RejectsResealedRequirementWitnessAbsentFromAcceptedProjection()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_fresh_requirement",
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            flow.AcceptedState);
        var request = SerializeDetachedRequirementRequest(flow);
        var common = ReadDetachedScope(request, "common");
        var binding = Assert.Single(ReadDetachedBindings(common).OfType<JsonObject>(),
            candidate => string.Equals(
                ReadJsonString(ReadJsonObject(candidate, "ResolvedRequirement"), "Kind"),
                "item_quantity",
                StringComparison.Ordinal));
        var row = ReadJsonObject(binding, "ResolvedRequirement");
        var witness = ReadJsonObject(binding, "SuccessWitness");
        var evidence = ReadJsonObject(witness, "Evidence");
        const int forgedCount = 3;
        evidence[FindJsonPropertyName(evidence, "Count")] = forgedCount;
        evidence[FindJsonPropertyName(evidence, "AvailableCount")] = forgedCount;
        var typedRow = JsonSerializer.Deserialize<MortalWoundResolvedRequirement>(
            row.ToJsonString(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(typedRow);
        var route = Assert.Single(flow.Before.Treatment.Routes, candidate =>
            string.Equals(candidate.RouteId, scenario.RouteId, StringComparison.Ordinal));
        row[FindJsonPropertyName(row, "AuthorityFingerprint")] =
            MortalWoundTreatmentAuthority.RecomputeResolvedRequirementFingerprint(
                typedRow!,
                acceptedState.RequirementContext,
                route.Requirements[typedRow!.RequirementIndex],
                new string?[]
                {
                    ReadJsonString(row, "AuthorityRef"),
                    ReadJsonString(row, "Realm"),
                    ReadOptionalJsonString(row, "OwnerKind"),
                    ReadOptionalJsonString(row, "OwnerId"),
                    DetachedNumber(forgedCount),
                    DetachedNumber(forgedCount),
                    ReadJsonString(evidence, "ReservationState"),
                    ReadJsonString(evidence, "Lifecycle"),
                    DetachedBoolean(ReadDetachedBoolean(evidence, "Active")),
                    DetachedNumber(ReadDetachedInt64(
                        evidence,
                        "CumulativeRequestedQuantity"))
                });
        witness[FindJsonPropertyName(witness, "WitnessFingerprint")] =
            ComputeDetachedItemSuccessWitnessFingerprint(row, witness, evidence);

        var resource = ReadJsonObject(request, "ResourceAuthority");
        var claim = Assert.Single(
            Assert.IsType<JsonArray>(resource[FindJsonPropertyName(resource, "Claims")])
                .OfType<JsonObject>(),
            candidate => string.Equals(
                ReadJsonString(candidate, "AuthorityRef"),
                ReadJsonString(row, "AuthorityRef"),
                StringComparison.Ordinal));
        var witnessFingerprint = ReadJsonString(witness, "WitnessFingerprint");
        claim[FindJsonPropertyName(claim, "SuccessWitnessFingerprint")] =
            witnessFingerprint;
        claim[FindJsonPropertyName(claim, "ClaimFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.resource_claim",
                "1",
                ReadJsonString(claim, "Scope"),
                DetachedNumber(ReadDetachedInt32(claim, "RequirementIndex")),
                ReadJsonString(claim, "Kind"),
                ReadJsonString(claim, "AuthorityRef"),
                ReadJsonString(claim, "Realm"),
                ReadJsonString(claim, "OwnerKind"),
                ReadJsonString(claim, "OwnerId"),
                DetachedNumber(ReadDetachedInt32(claim, "Quantity")),
                witnessFingerprint
            });
        ResealDetachedRequirementRequest(request);
        var bundle = ReadJsonObject(request, "RequirementAuthority");
        var procedure = ReadJsonObject(request, "ModeAuthority");
        procedure[FindJsonPropertyName(
            procedure,
            "RequirementAuthorityFingerprint")] =
            ReadJsonString(bundle, "AuthorityFingerprint");
        ResealDetachedProcedureAuthorityAndRequest(request);
        var parsed = ParseFreshAuthorityRequest(request);

        AssertFreshAuthorityRejected(MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            parsed,
            flow.History,
            flow.Before,
            acceptedState));
    }

    [Fact]
    public void FreshAuthority_RejectsCurrentProcedureWithInapplicableSuccessBand()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        var safeRoute = scenario.Before["treatment"]!["routes"]![0]!
            .DeepClone().AsObject();
        safeRoute["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 });
        safeRoute["outcomes"]![1]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "add_recovery", ["points"] = 1 });
        var badRoute = safeRoute.DeepClone().AsObject();
        var badRouteId = scenario.RouteId + "_inapplicable";
        badRoute["routeId"] = badRouteId;
        badRoute["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "stabilize" });
        scenario.Before["care"]!["state"] = "stabilized";
        scenario.Before["care"]!["stabilizedAtTurn"] = 41;
        scenario.Before["recovery"]!["blockers"] = new JsonArray();
        scenario.Before["treatment"]!["routes"] = new JsonArray(
            badRoute,
            safeRoute);
        scenario.Before["treatment"]!["knownRouteIds"] = new JsonArray(
            badRouteId,
            scenario.RouteId);

        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var coordinatesResult = MortalWoundTreatmentPlanner.CreateAttemptCoordinates(
            acceptedState,
            before,
            scenario.OperationKey + "_inapplicable",
            badRouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(coordinatesResult.IsValid, DescribeIssues(coordinatesResult.Issues));
        var coordinates = Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(
            coordinatesResult.Coordinates);
        var route = Assert.IsType<MortalWoundProcedureRouteDefinition>(Assert.Single(
            acceptedState.TreatmentDefinition.Routes,
            candidate => string.Equals(
                candidate.RouteId,
                badRouteId,
                StringComparison.Ordinal)));
        var requirementResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForProcedure(
                acceptedState,
                coordinates,
                before);
        Assert.True(requirementResult.IsValid, DescribeIssues(requirementResult.Issues));
        var requirement = Assert.IsType<MortalWoundTreatmentRequirementAuthorityBundle>(
            requirementResult.Authority);
        var procedureResult = MortalWoundProcedureCheckAuthority.Create(
            coordinates,
            route,
            before,
            requirement,
            acceptedState);
        Assert.True(procedureResult.IsValid, DescribeIssues(procedureResult.Issues));
        var procedure = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            procedureResult.Authority);
        var resourceResult = MortalWoundTreatmentResourceComposer.PrepareProcedure(
            acceptedState,
            coordinates,
            before,
            requirement,
            procedure);
        Assert.True(resourceResult.IsValid, DescribeIssues(resourceResult.Issues));
        var resource = Assert.IsType<MortalWoundTreatmentResourceReservationAuthority>(
            resourceResult.Authority);
        var sealedResult = MortalWoundTreatmentPlanner.SealProcedureRequest(
            coordinates,
            procedure,
            requirement,
            resource,
            before);
        Assert.True(sealedResult.IsValid, DescribeIssues(sealedResult.Issues));
        var serialized = Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(sealedResult.Request));
        var detached = ParseFreshAuthorityRequest(serialized);
        Assert.True(MortalWoundTreatmentResourceComposer.RollbackNew(resourceResult));
        Assert.True(procedure.RollbackNewProvisionalReservations(acceptedState));

        AssertFreshAuthorityRejected(MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            detached,
            history,
            before,
            acceptedState));

        var fallback = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            acceptedState,
            history,
            before,
            scenario.OperationKey + "_after_inapplicable",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(fallback.IsValid, DescribeIssues(fallback.Issues));
        var fallbackRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            fallback.Request);
        Assert.Equal(
            new[] { 0 },
            Assert.IsType<MortalWoundProcedureCheckAuthority>(
                fallbackRequest.ModeAuthority).SourceIndices);
    }

    [Fact]
    public void FreshAuthority_RejectsReleasedProcedureClaimAndReusesFreedDie()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            fixture.GetAcceptedState());
        var before = fixture.ReadCurrentWound();
        var history = fixture.ReadCurrentHistory();
        var coordinatesResult = MortalWoundTreatmentPlanner.CreateAttemptCoordinates(
            acceptedState,
            before,
            scenario.OperationKey + "_released_claim",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(coordinatesResult.IsValid, DescribeIssues(coordinatesResult.Issues));
        var coordinates = Assert.IsType<MortalWoundTreatmentAttemptCoordinates>(
            coordinatesResult.Coordinates);
        var route = Assert.IsType<MortalWoundProcedureRouteDefinition>(Assert.Single(
            acceptedState.TreatmentDefinition.Routes));
        var requirementResult =
            MortalWoundTreatmentRequirementAuthorityBundle.CreateForProcedure(
                acceptedState,
                coordinates,
                before);
        Assert.True(requirementResult.IsValid, DescribeIssues(requirementResult.Issues));
        var requirement = Assert.IsType<MortalWoundTreatmentRequirementAuthorityBundle>(
            requirementResult.Authority);
        var procedureResult = MortalWoundProcedureCheckAuthority.Create(
            coordinates,
            route,
            before,
            requirement,
            acceptedState);
        Assert.True(procedureResult.IsValid, DescribeIssues(procedureResult.Issues));
        var procedure = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            procedureResult.Authority);
        var resourceResult = MortalWoundTreatmentResourceComposer.PrepareProcedure(
            acceptedState,
            coordinates,
            before,
            requirement,
            procedure);
        Assert.True(resourceResult.IsValid, DescribeIssues(resourceResult.Issues));
        var resource = Assert.IsType<MortalWoundTreatmentResourceReservationAuthority>(
            resourceResult.Authority);
        var sealedResult = MortalWoundTreatmentPlanner.SealProcedureRequest(
            coordinates,
            procedure,
            requirement,
            resource,
            before);
        Assert.True(sealedResult.IsValid, DescribeIssues(sealedResult.Issues));
        var releasedRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            sealedResult.Request);
        Assert.True(MortalWoundTreatmentResourceComposer.RollbackNew(resourceResult));
        Assert.True(procedure.ReleaseProvisionalReservations(acceptedState));

        AssertFreshAuthorityRejected(MortalWoundTreatmentPlanner.CreateProcedureAttempt(
            releasedRequest,
            history,
            before,
            acceptedState));

        var replacement = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            acceptedState,
            history,
            before,
            scenario.OperationKey + "_replacement",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.True(replacement.IsValid, DescribeIssues(replacement.Issues));
        Assert.Equal(
            new[] { 0 },
            Assert.IsType<MortalWoundProcedureCheckAuthority>(
                Assert.IsType<MortalWoundTreatmentAttemptRequest>(
                    replacement.Request).ModeAuthority).SourceIndices);
    }

    private static MortalWoundTreatmentAttemptRequest ParseFreshAuthorityRequest(
        JsonObject request)
    {
        var issues = new List<ValidationIssue>();
        Assert.True(
            MortalWoundTreatmentCommandCodec.TryParseRequest(
                request,
                "request",
                issues,
                out var parsed),
            DescribeIssues(issues));
        return Assert.IsType<MortalWoundTreatmentAttemptRequest>(parsed);
    }

    private static void AssertFreshAuthorityRejected(
        MortalWoundTreatmentResolutionResult result)
    {
        Assert.Equal("Rejected", result.Disposition);
        Assert.Null(result.Resolution);
        Assert.Contains(result.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_resolution_fresh_authority_mismatch",
            StringComparison.Ordinal));
    }

    private static void ResealDetachedProcedureAuthorityAndRequest(JsonObject request)
    {
        var authority = ReadJsonObject(request, "ModeAuthority");
        var contributions = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "RollContributions")]);
        var sourceIndices = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceIndices")]);
        var sourceRolls = Assert.IsType<JsonArray>(authority[
            FindJsonPropertyName(authority, "SourceRolls")]);
        Assert.Equal(sourceIndices.Count, sourceRolls.Count);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.procedure_check_authority",
            "1",
            ReadJsonString(authority, "SourcePath"),
            ReadJsonString(authority, "RollMode"),
            ReadJsonString(authority, "RollActorKind"),
            ReadJsonString(authority, "RollActorId"),
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
        fields.Add(authority[
            FindJsonPropertyName(authority, "PreparedCriticalReaction")] is JsonObject prepared
            ? ReadJsonString(prepared, "PreparedReactionFingerprint")
            : null);
        authority[FindJsonPropertyName(authority, "AuthorityFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(fields);
        ResealDetachedRequestOnly(request);
    }

    private static string ComputeDetachedItemSuccessWitnessFingerprint(
        JsonObject row,
        JsonObject witness,
        JsonObject evidence)
    {
        var mechanicalFields = new string?[]
        {
            ReadJsonString(row, "AuthorityRef"),
            ReadJsonString(row, "Realm"),
            ReadOptionalJsonString(row, "OwnerKind"),
            ReadOptionalJsonString(row, "OwnerId"),
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
            DetachedNumber(mechanicalFields.Length)
        };
        fields.AddRange(mechanicalFields);
        fields.Add(DetachedNumber(evidenceFields.Length));
        fields.AddRange(evidenceFields);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void ResealDetachedCapabilityProofAndRequest(JsonObject request)
    {
        var proof = ReadJsonObject(request, "ModeAuthority");
        var limits = ReadJsonObject(proof, "OperationLimits");
        var removableKinds = Assert.IsType<JsonArray>(limits[
            FindJsonPropertyName(limits, "RemovableComplicationKinds")]);
        var joinedKinds = string.Join(",", removableKinds.Select(
            static value => value!.GetValue<string>()));
        var sourceFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.capability_source",
            "1",
            ReadJsonString(proof, "SourcePath"),
            ReadJsonString(proof, "OwnerKind"),
            ReadJsonString(proof, "OwnerId"),
            ReadJsonString(proof, "SkillKind"),
            ReadJsonString(proof, "SkillId"),
            "1",
            ReadJsonString(proof, "CapabilityRef"),
            ReadJsonString(proof, "WoundDomain"),
            DetachedNumber(proof[FindJsonPropertyName(proof, "MinimumSeverityRank")]!
                .GetValue<int>()),
            DetachedNumber(proof[FindJsonPropertyName(proof, "MaximumSeverityRank")]!
                .GetValue<int>()),
            "active",
            "true",
            DetachedBoolean(limits[FindJsonPropertyName(limits, "MayStabilize")]!
                .GetValue<bool>()),
            DetachedNumber(limits[FindJsonPropertyName(limits, "MaximumRecoveryPoints")]!
                .GetValue<int>()),
            DetachedNumber(limits[
                FindJsonPropertyName(limits, "MaximumSeverityReductionSteps")]!
                .GetValue<int>()),
            DetachedNumber(removableKinds.Count),
            joinedKinds,
            DetachedBoolean(limits[FindJsonPropertyName(limits, "MayHealAtSeverityI")]!
                .GetValue<bool>()),
            DetachedNumber(limits[
                FindJsonPropertyName(limits, "MaximumCosmeticHealLegacies")]!
                .GetValue<int>()),
            DetachedNumber(limits[
                FindJsonPropertyName(limits, "MaximumMechanicalEffectHealLegacies")]!
                .GetValue<int>())
        });
        proof[FindJsonPropertyName(proof, "SourceSemanticFingerprint")] =
            sourceFingerprint;
        var proofFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
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
            DetachedNumber(proof[FindJsonPropertyName(proof, "MinimumSeverityRank")]!
                .GetValue<int>()),
            DetachedNumber(proof[FindJsonPropertyName(proof, "MaximumSeverityRank")]!
                .GetValue<int>()),
            DetachedBoolean(limits[FindJsonPropertyName(limits, "MayStabilize")]!
                .GetValue<bool>()),
            DetachedNumber(limits[FindJsonPropertyName(limits, "MaximumRecoveryPoints")]!
                .GetValue<int>()),
            DetachedNumber(limits[
                FindJsonPropertyName(limits, "MaximumSeverityReductionSteps")]!
                .GetValue<int>()),
            DetachedNumber(removableKinds.Count),
            joinedKinds,
            DetachedBoolean(limits[FindJsonPropertyName(limits, "MayHealAtSeverityI")]!
                .GetValue<bool>()),
            DetachedNumber(limits[
                FindJsonPropertyName(limits, "MaximumCosmeticHealLegacies")]!
                .GetValue<int>()),
            DetachedNumber(limits[
                FindJsonPropertyName(limits, "MaximumMechanicalEffectHealLegacies")]!
                .GetValue<int>()),
            ReadJsonString(proof, "ContextFingerprint"),
            ReadJsonString(proof, "AcceptedStateFingerprint"),
            ReadJsonString(proof, "CoordinatesFingerprint"),
            sourceFingerprint
        });
        proof[FindJsonPropertyName(proof, "ProofFingerprint")] = proofFingerprint;
        ResealDetachedRequestOnly(request);
    }

    private static void ResealDetachedRequestOnly(JsonObject request)
    {
        var resource = ReadJsonObject(request, "ResourceAuthority");
        ResealSerializedResourceAndRequest(
            request,
            ComputeSerializedPolicyFingerprint(ReadJsonObject(resource, "Policy")));
    }
}
