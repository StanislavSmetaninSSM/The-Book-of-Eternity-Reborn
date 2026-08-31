using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void ColdClaimRecovery_RestoresDiceAndFateBeforeAnyExactRetry()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var original = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_cold_claim",
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            original,
            "The durable request must reclaim its dice and Fate evidence first.");
        PersistTreatmentCommand(fixture, command);

        using var coldFixture = CreateColdRootCopy(fixture);
        var restored = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture),
            "cold claim recovery"));

        var competing = ResolveCurrentTreatment(
            coldFixture,
            "procedure",
            scenario.OperationKey + "_competing_before_retry",
            scenario.RouteId);
        Assert.Equal(
            new[] { 1 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(competing.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Equal(
            "effect_fate_shield_newer",
            ReadPreparedFateEffectId(competing.Request));

        var rehydrated = RehydratePersistedTreatment(
            coldFixture,
            "procedure",
            restored);
        Assert.Equal(
            CanonicalValue(original.Resolution),
            CanonicalValue(rehydrated.Resolution));
    }

    [Fact]
    public void ColdClaimRecovery_PreservesExactGapInsteadOfRepackingPersistedClaim()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);
        _ = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_cancelled_earlier",
            scenario.RouteId);
        var persistedLater = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_persisted_later",
            scenario.RouteId);
        Assert.Equal(
            new[] { 1 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(persistedLater.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Equal(
            "effect_fate_shield_newer",
            ReadPreparedFateEffectId(persistedLater.Request));
        var command = ComposeTreatmentCommand(
            persistedLater,
            "Only the later durable request survives the restart.");
        PersistTreatmentCommand(fixture, command);

        using var coldFixture = CreateColdRootCopy(fixture);
        var restored = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture),
            "gapped cold claim recovery"));
        Assert.Equal(
            CanonicalValue(persistedLater.Request),
            CanonicalValue(restored));

        var replacement = ResolveCurrentTreatment(
            coldFixture,
            "procedure",
            scenario.OperationKey + "_fills_released_gap",
            scenario.RouteId);
        Assert.Equal(
            new[] { 0 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(replacement.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Equal(
            "effect_fate_shield_older",
            ReadPreparedFateEffectId(replacement.Request));

        var rehydrated = RehydratePersistedTreatment(
            coldFixture,
            "procedure",
            restored);
        Assert.Equal(
            CanonicalValue(persistedLater.Resolution),
            CanonicalValue(rehydrated.Resolution));
    }

    [Fact]
    public void ColdClaimRecovery_ExactSecondRestoreDoesNotEraseNewLiveClaims()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 1, 17);
        scenario.AcceptedState["sterileThreadCount"] = 3;
        using var fixture = AcceptedStateFixture.Create(scenario);
        var persisted = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_persisted_once",
            scenario.RouteId);
        PersistTreatmentCommand(
            fixture,
            ComposeTreatmentCommand(
                persisted,
                "The durable procedure survives one cold recovery phase."));

        using var coldFixture = CreateColdRootCopy(fixture);
        _ = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture),
            "initial one-shot claim recovery"));

        var liveAfterRecovery = ResolveCurrentTreatment(
            coldFixture,
            "procedure",
            scenario.OperationKey + "_live_after_recovery",
            scenario.RouteId);
        Assert.Equal(
            new[] { 1 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(liveAfterRecovery.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Equal(
            "effect_fate_shield_newer",
            ReadPreparedFateEffectId(liveAfterRecovery.Request));

        _ = Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(coldFixture),
            "idempotent second claim recovery"));

        var nextLive = ResolveCurrentTreatment(
            coldFixture,
            "procedure",
            scenario.OperationKey + "_next_live_after_second_restore",
            scenario.RouteId);
        Assert.Equal(
            new[] { 2 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(nextLive.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Null(ReadPropertyAllowingNull(
            ReadRequiredProperty(nextLive.Request, "ModeAuthority"),
            "PreparedCriticalReaction"));
    }

    [Fact]
    public void ColdClaimRecovery_AcceptedStateIngressRestoresBeforeFirstNewRequest()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var persisted = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_durable_before_restart",
            scenario.RouteId);
        PersistTreatmentCommand(
            fixture,
            ComposeTreatmentCommand(
                persisted,
                "Accepted-state ingress must recover this request before new work."));

        using var coldFixture = CreateColdRootCopy(fixture);
        var firstNewRequest = ResolveCurrentTreatment(
            coldFixture,
            "procedure",
            scenario.OperationKey + "_first_new_after_restart",
            scenario.RouteId);

        Assert.Equal(
            new[] { 1 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(firstNewRequest.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Equal(
            "effect_fate_shield_newer",
            ReadPreparedFateEffectId(firstNewRequest.Request));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("newer")]
    public void ColdClaimRecovery_SourceZeroRequiresTheProducerSelectedOldestFate(
        string preparedReaction)
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_cold_fate_provenance_" + preparedReaction,
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "The repair wave carries a fully resealed but producer-impossible Fate claim.");
        var pending = ComposeTreatmentRepairPendingRoot(
            command,
            CreateTreatmentRepairPackets(command.Binding, scenario.Before));
        var request = FindSerializedTreatmentRequest(pending);
        var authority = ReadJsonObject(request, "ModeAuthority");
        Assert.Equal(
            new[] { 0 },
            Assert.IsType<JsonArray>(authority[
                    FindJsonPropertyName(authority, "SourceIndices")])
                .Select(static value => value!.GetValue<int>()));

        if (string.Equals(preparedReaction, "missing", StringComparison.Ordinal))
        {
            authority[FindJsonPropertyName(authority, "PreparedCriticalReaction")] = null;
        }
        else
        {
            var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
                flow.AcceptedState);
            var newer = acceptedState.EffectMechanics.FateShieldReactionCandidates[1];
            var prepared = ReadJsonObject(authority, "PreparedCriticalReaction");
            prepared[FindJsonPropertyName(prepared, "EffectId")] = newer.EffectId;
            prepared[FindJsonPropertyName(prepared, "TriggerId")] = newer.TriggerId;
            prepared[FindJsonPropertyName(prepared, "AcceptedEffectFingerprint")] =
                newer.AcceptedEffectFingerprint;
            ResealDetachedPreparedReaction(authority);
        }
        ResealDetachedProcedureAuthorityAndRequest(request);

        var submitted = Assert.IsType<JsonArray>(pending[
            FindJsonPropertyName(pending, "SubmittedTreatmentRequests")]);
        var submittedRow = Assert.IsType<JsonObject>(Assert.Single(submitted));
        submittedRow[FindJsonPropertyName(submittedRow, "RequestFingerprint")] =
            ReadJsonString(request, "RequestFingerprint");
        File.WriteAllText(
            fixture.FileSystem.ResolvePath(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath),
            pending.ToJsonString());

        using var coldFixture = CreateColdRootCopy(fixture);
        var rejected = coldFixture.ExportCurrent();

        AssertInvalidTypedResult(
            rejected,
            "Authority",
            "source-zero producer-impossible Fate claim " + preparedReaction);
        Assert.Contains(
            AsObjects(ReadRequiredProperty(rejected, "Issues"))
                .Select(Assert.IsType<ValidationIssue>),
            static issue => string.Equals(
                issue.Code,
                "mortal_wound_treatment_critical_reaction_restore_mismatch",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ColdClaimRecovery_AdvantageGapPairAllowsOnlyOneHistoricalFateClaim()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 1, 1, 17);
        scenario.AcceptedState["sterileThreadCount"] = 3;
        using var fixture = AcceptedStateFixture.Create(scenario);
        fixture.ReplacePlayerProcedureRollEffectsAndFate(
            "cold_advantage_gap_provenance",
            "advantage");
        var transient = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_transient_advantage_claim",
            scenario.RouteId);
        Assert.Equal(
            new[] { 0, 1 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(transient.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Equal("effect_fate_shield_older", ReadPreparedFateEffectId(
            transient.Request));

        var persisted = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_persisted_advantage_claim",
            scenario.RouteId);
        Assert.Equal(
            new[] { 2, 3 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(persisted.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Equal("effect_fate_shield_newer", ReadPreparedFateEffectId(
            persisted.Request));

        var command = ComposeTreatmentCommand(
            persisted,
            "One disappeared advantage pair can explain only one prior Fate claim.");
        var pending = ComposeTreatmentRepairPendingRoot(
            command,
            CreateTreatmentRepairPackets(command.Binding, scenario.Before));
        var request = FindSerializedTreatmentRequest(pending);
        var authority = ReadJsonObject(request, "ModeAuthority");
        authority[FindJsonPropertyName(authority, "PreparedCriticalReaction")] = null;
        ResealDetachedProcedureAuthorityAndRequest(request);
        var submitted = Assert.IsType<JsonArray>(pending[
            FindJsonPropertyName(pending, "SubmittedTreatmentRequests")]);
        var submittedRow = Assert.IsType<JsonObject>(Assert.Single(submitted));
        submittedRow[FindJsonPropertyName(submittedRow, "RequestFingerprint")] =
            ReadJsonString(request, "RequestFingerprint");
        File.WriteAllText(
            fixture.FileSystem.ResolvePath(
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath),
            pending.ToJsonString());

        using var coldFixture = CreateColdRootCopy(fixture);
        var rejected = coldFixture.ExportCurrent();

        AssertInvalidTypedResult(
            rejected,
            "Authority",
            "advantage gap pair overcounted as two historical Fate claims");
        Assert.Contains(
            AsObjects(ReadRequiredProperty(rejected, "Issues"))
                .Select(Assert.IsType<ValidationIssue>),
            static issue => string.Equals(
                issue.Code,
                "mortal_wound_treatment_critical_reaction_restore_mismatch",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ColdClaimRecovery_LowerGapCannotBeReusedForTwoFateSkips()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 1, 17);
        scenario.AcceptedState["sterileThreadCount"] = 4;
        using var fixture = AcceptedStateFixture.Create(scenario);
        _ = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_transient_lower_claim",
            scenario.RouteId);
        var persistedNewer = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_persisted_newer",
            scenario.RouteId);
        var persistedWithoutReaction = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_persisted_without_reaction",
            scenario.RouteId);
        Assert.Equal(
            new[] { 1 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(persistedNewer.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Equal(
            "effect_fate_shield_newer",
            ReadPreparedFateEffectId(persistedNewer.Request));
        Assert.Equal(
            new[] { 2 },
            ReadIntSequence(ReadRequiredProperty(
                ReadRequiredProperty(persistedWithoutReaction.Request, "ModeAuthority"),
                "SourceIndices")));
        Assert.Null(ReadPropertyAllowingNull(
            ReadRequiredProperty(persistedWithoutReaction.Request, "ModeAuthority"),
            "PreparedCriticalReaction"));

        var newerRequest = Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(persistedNewer.Request));
        var forgedRequest = Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(persistedWithoutReaction.Request));
        var forgedAuthority = ReadJsonObject(forgedRequest, "ModeAuthority");
        var prepared = ReadJsonObject(
                ReadJsonObject(newerRequest, "ModeAuthority"),
                "PreparedCriticalReaction")
            .DeepClone()
            .AsObject();
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            persistedNewer.AcceptedState);
        var older = acceptedState.EffectMechanics.FateShieldReactionCandidates[0];
        prepared[FindJsonPropertyName(prepared, "EffectId")] = older.EffectId;
        prepared[FindJsonPropertyName(prepared, "TriggerId")] = older.TriggerId;
        prepared[FindJsonPropertyName(prepared, "AcceptedEffectFingerprint")] =
            older.AcceptedEffectFingerprint;
        forgedAuthority[FindJsonPropertyName(
            forgedAuthority,
            "PreparedCriticalReaction")] = prepared;
        ResealDetachedPreparedReaction(forgedAuthority);
        ResealDetachedProcedureAuthorityAndRequest(forgedRequest);
        var parseIssues = new List<ValidationIssue>();
        Assert.True(
            MortalWoundTreatmentCommandCodec.TryParseRequest(
                forgedRequest,
                "request",
                parseIssues,
                out var parsedForgedRequest),
            DescribeIssues(parseIssues));
        var forged = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            parsedForgedRequest);
        var persistedNewerModel = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            persistedNewer.Request);
        var newer = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            persistedNewerModel.ModeAuthority);
        var forgedProcedure = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            forged.ModeAuthority);
        var recovery = new MortalWoundCriticalReactionReservationRegistry();
        var historicallyClaimedEffectIds =
            new HashSet<string>(StringComparer.Ordinal);
        var first = recovery.RestoreExact(
            persistedNewerModel.Coordinates,
            acceptedState.EffectMechanics.FateShieldReactionCandidates,
            newer.PreparedCriticalReaction,
            maximumHistoricallyClaimedOlderCandidates: 1,
            historicallyClaimedEffectIds: historicallyClaimedEffectIds);
        Assert.True(first.IsValid, DescribeIssues(first.Issues));

        var reused = recovery.RestoreExact(
            forged.Coordinates,
            acceptedState.EffectMechanics.FateShieldReactionCandidates,
            forgedProcedure.PreparedCriticalReaction,
            maximumHistoricallyClaimedOlderCandidates: 1,
            historicallyClaimedEffectIds: historicallyClaimedEffectIds);

        Assert.False(
            reused.IsValid,
            "One historical lower d20 gap was incorrectly reused for two Fate skips.");
        Assert.Contains(reused.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_critical_reaction_restore_mismatch",
            StringComparison.Ordinal));
    }

    [Fact]
    public void ColdClaimRecovery_PreservesAcceptedOrderWhenReleasedLowerSpanIsReused()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        scenario.AcceptedState["sterileThreadCount"] = 4;
        using var fixture = AcceptedStateFixture.Create(scenario);

        var transient = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_transient_source_zero",
            scenario.RouteId);
        var acceptedEarlier = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_accepted_earlier_source_one",
            scenario.RouteId);
        var acceptedState = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(
            acceptedEarlier.AcceptedState);
        var transientRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            transient.Request);
        var transientProcedure = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            transientRequest.ModeAuthority);
        Assert.True(transientProcedure.ReleaseProvisionalReservations(acceptedState));

        var acceptedLater = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_accepted_later_source_zero",
            scenario.RouteId);
        var earlierRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            acceptedEarlier.Request);
        var laterRequest = Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            acceptedLater.Request);
        Assert.Equal(
            new[] { 1 },
            Assert.IsType<MortalWoundProcedureCheckAuthority>(
                earlierRequest.ModeAuthority).SourceIndices);
        Assert.Equal(
            "effect_fate_shield_newer",
            ReadPreparedFateEffectId(earlierRequest));
        Assert.Equal(
            new[] { 0 },
            Assert.IsType<MortalWoundProcedureCheckAuthority>(
                laterRequest.ModeAuthority).SourceIndices);
        Assert.Equal(
            "effect_fate_shield_older",
            ReadPreparedFateEffectId(laterRequest));

        var reconstructed =
            new MortalWoundProcedureClaimRecoveryCoordinator().Restore(
                acceptedState,
                new[] { earlierRequest, laterRequest });
        Assert.True(reconstructed.IsValid, DescribeIssues(reconstructed.Issues));
        Assert.Equal(
            new[]
            {
                earlierRequest.Coordinates.OperationKey,
                laterRequest.Coordinates.OperationKey
            },
            reconstructed.Requests.Select(static request =>
                request.Coordinates.OperationKey));

        var invalidEarlier = WithInvalidCurrentContextCoordinates(earlierRequest);
        var invalidLater = WithInvalidCurrentContextCoordinates(laterRequest);
        var rejected =
            new MortalWoundProcedureClaimRecoveryCoordinator().Restore(
                acceptedState,
                new[] { invalidEarlier, invalidLater });

        Assert.False(rejected.IsValid);
        var issue = Assert.Single(rejected.Issues);
        Assert.Equal(
            "mortal_wound_treatment_procedure_claim_recovery_stale",
            issue.Code);
        Assert.Equal(
            earlierRequest.Coordinates.OperationKey,
            issue.Actual);
    }

    private static MortalWoundTreatmentAttemptRequest
        WithInvalidCurrentContextCoordinates(
            MortalWoundTreatmentAttemptRequest source)
    {
        var coordinates = MutateAndResealCoordinates(
            source.Coordinates,
            "context_fingerprint");
        var requestFingerprint = MortalWoundTreatmentAttemptRequest.ComputeFingerprint(
            source.Mode,
            coordinates,
            source.MilestoneOrdinal,
            source.RouteSourceWoundFingerprint,
            source.ModeAuthority,
            source.RequirementAuthority,
            source.ResourceAuthority);
        return Assert.IsType<MortalWoundTreatmentAttemptRequest>(
            MortalWoundTreatmentAttemptRequest.RestoreDetached(
                source.Mode,
                coordinates,
                source.MilestoneOrdinal,
                source.RouteSourceWound,
                source.RouteSourceWoundFingerprint,
                source.ModeAuthority,
                source.RequirementAuthority,
                source.ResourceAuthority,
                requestFingerprint));
    }

    private static object RestoreCurrentPersistedTreatmentCatalog(
        AcceptedStateFixture fixture)
    {
        var acceptedState = fixture.GetAcceptedState();
        return InvokeInstance(
            ExactInstanceMethod(
                acceptedState.GetType(),
                "RestorePersistedTreatmentRequests",
                1),
            acceptedState,
            new object?[] { fixture.ReadCurrentHistory() });
    }
}
