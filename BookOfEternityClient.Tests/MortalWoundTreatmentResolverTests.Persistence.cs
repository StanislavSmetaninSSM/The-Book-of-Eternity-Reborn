using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T061 persistence boundary. Valid command and pending authority is produced only by
/// production composers. Serialized output is reordered only for byte-semantic equality
/// or replaced from another production composition for negative parser rows; the test
/// never constructs a typed treatment request, requirement bundle, resource authority,
/// reservation, die claim, Fate claim, or authority fingerprint.
/// </summary>
public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void PersistenceSurface_KeepsCompositionPureAndCatalogParsingDetachedFromFileWrites()
    {
        var command = ExactStaticMethod(
            typeof(WoundResponseInputComposer),
            "ComposeMortalWoundTreatmentCommandRoot",
            3);
        Assert.Equal(typeof(WoundAcceptedTurnBinding), command.GetParameters()[0].ParameterType);
        Assert.Equal(
            "BookOfEternityClient.Services.MortalWoundTreatmentResolution",
            command.GetParameters()[1].ParameterType.FullName);
        Assert.Equal(typeof(string), command.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(JsonObject), command.ReturnType);
        Assert.DoesNotContain(command.GetParameters(), static parameter =>
            parameter.ParameterType.Name is "FileSystemManager" or "CanonicalWriteLease");

        var pending = ExactStaticMethod(
            typeof(WoundRepairPacketBuilder),
            "ComposePendingRoot",
            3);
        Assert.Equal(typeof(WoundAcceptedTurnBinding), pending.GetParameters()[0].ParameterType);
        Assert.True(typeof(IEnumerable<WoundRepairPacket>).IsAssignableFrom(
            pending.GetParameters()[1].ParameterType));
        Assert.Equal(typeof(WoundResponseCommandParsingResult), pending.GetParameters()[2].ParameterType);
        Assert.Equal(typeof(JsonObject), pending.ReturnType);
        Assert.DoesNotContain(pending.GetParameters(), static parameter =>
            parameter.ParameterType.Name is "FileSystemManager" or "CanonicalWriteLease");

        var catalog = RequirePersistedRequestCatalog();
        var parse = ExactStaticMethod(catalog, "Parse", 3);
        Assert.Equal(typeof(JsonElement?), parse.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(JsonElement?), parse.GetParameters()[1].ParameterType);
        Assert.Equal(typeof(WoundHistoryParseResult), parse.GetParameters()[2].ParameterType);
        Assert.Equal(
            new[] { "IsValid", "Issues", "Requests" }.OrderBy(static name => name),
            parse.ReturnType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.GetIndexParameters().Length == 0)
                .Select(static property => property.Name)
                .OrderBy(static name => name));
    }

    [Fact]
    public void PersistedCommand_ColdRestartReconstructsExactRetryAndKeepsDiceFateAndResourceHeld()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var operationKey = scenario.OperationKey + "_persisted_restart";
        var first = ResolveCurrentTreatment(
            fixture,
            "procedure",
            operationKey,
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            first,
            "The first treatment request is held for accepted transition processing.");

        var modified = PersistTreatmentCommand(fixture, command);
        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, modified);
        var commandPath = fixture.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath);
        var persistedBytes = File.ReadAllBytes(commandPath);
        var persistedRoot = JsonNode.Parse(File.ReadAllText(commandPath))!.AsObject();
        Assert.True(JsonNode.DeepEquals(command.Root, persistedRoot));
        var reparsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(persistedRoot));
        Assert.True(reparsed.Success, DescribeIssues(reparsed.Issues));

        using var coldFixture = CreateColdRootCopy(fixture);
        Assert.NotSame(
            fixture.FileSystem.CanonicalRootAuthorityIdentity,
            coldFixture.FileSystem.CanonicalRootAuthorityIdentity);
        var coldCommandPath = coldFixture.FileSystem.ResolvePath(
            AcceptedMechanicsPlan.WoundCommandPath);
        Assert.Equal(persistedBytes, File.ReadAllBytes(coldCommandPath));
        persistedRoot = JsonNode.Parse(File.ReadAllText(coldCommandPath))!.AsObject();
        var restored = Assert.Single(AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(persistedRoot, null, coldFixture.ReadCurrentHistory()),
            "cold command reconstruction"));
        Assert.NotSame(first.Request, restored);
        Assert.Equal(CanonicalValue(first.Request), CanonicalValue(restored));
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(first.Request, "RequirementAuthority")),
            CanonicalValue(ReadRequiredProperty(restored, "RequirementAuthority")));
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(first.Request, "ResourceAuthority")),
            CanonicalValue(ReadRequiredProperty(restored, "ResourceAuthority")));

        var rehydrated = RehydratePersistedTreatment(
            coldFixture,
            "procedure",
            restored);
        Assert.NotSame(first.Resolution, rehydrated.Resolution);
        Assert.Equal(
            CanonicalValue(first.Resolution),
            CanonicalValue(rehydrated.Resolution));
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(first.Resolution, "OutcomeIntents")),
            CanonicalValue(ReadRequiredProperty(rehydrated.Resolution, "OutcomeIntents")));
        Assert.Equal(
            CanonicalValue(ReadPropertyAllowingNull(first.Resolution, "CriticalReactionIntent")),
            CanonicalValue(ReadPropertyAllowingNull(
                rehydrated.Resolution,
                "CriticalReactionIntent")));

        using (var publicationFixture = CreateColdRootCopy(fixture))
        {
            var publicationCommandPath = publicationFixture.FileSystem.ResolvePath(
                AcceptedMechanicsPlan.WoundCommandPath);
            Assert.Equal(persistedBytes, File.ReadAllBytes(publicationCommandPath));
            var publicationRoot = JsonNode.Parse(File.ReadAllText(
                publicationCommandPath))!.AsObject();
            var publicationRequest = Assert.Single(AssertValidPersistedCatalog(
                ParsePersistedRequestCatalog(
                    publicationRoot,
                    null,
                    publicationFixture.ReadCurrentHistory()),
                "cold T070 publication reconstruction"));
            var publicationFlow = RehydratePersistedTreatment(
                publicationFixture,
                "procedure",
                publicationRequest);
            Assert.Equal(
                CanonicalValue(first.Resolution),
                CanonicalValue(publicationFlow.Resolution));

            ComposeAndPublishTreatment(publicationFixture, publicationFlow);

            Assert.Equal(1, publicationFixture.ReadNpcItemCount("sterile_thread"));
            Assert.Equal(
                new[] { "effect_fate_shield_newer" },
                publicationFixture.ReadActivePlayerEffectIds());
            publicationFixture.AssertItemIdentityIndexValid();
            var publishedHistory = JsonNode.Parse(File.ReadAllText(
                publicationFixture.FileSystem.ResolvePath(
                    WoundHistoryState.HistoryPath)))!.AsObject();
            Assert.Single(publishedHistory["transitions"]!.AsArray(), row =>
                string.Equals(
                    row!["kind"]!.GetValue<string>(),
                    "treat",
                    StringComparison.Ordinal));
        }

        var exactRetry = ResolveCurrentTreatment(
            coldFixture,
            "procedure",
            operationKey,
            scenario.RouteId);
        Assert.Equal(CanonicalValue(first.Request), CanonicalValue(exactRetry.Request));
        Assert.Equal(CanonicalValue(first.Resolution), CanonicalValue(exactRetry.Resolution));

        var fresh = ResolveCurrentTreatment(
            coldFixture,
            "procedure",
            scenario.OperationKey + "_fresh_after_restart",
            scenario.RouteId);
        Assert.Equal(new[] { 1 }, ReadIntSequence(ReadRequiredProperty(
            ReadRequiredProperty(fresh.Request, "ModeAuthority"),
            "SourceIndices")));
        Assert.Equal("effect_fate_shield_newer", ReadPreparedFateEffectId(fresh.Request));
        Assert.Equal("effect_fate_shield_older", ReadPreparedFateEffectId(first.Request));

        var firstResource = ReadRequiredProperty(first.Request, "ResourceAuthority");
        var freshResource = ReadRequiredProperty(fresh.Request, "ResourceAuthority");
        Assert.Equal("held", Convert.ToString(ReadRequiredProperty(firstResource, "ReservationDisposition")));
        Assert.Equal("held", Convert.ToString(ReadRequiredProperty(freshResource, "ReservationDisposition")));
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(firstResource, "ReservationId")),
            Convert.ToString(ReadRequiredProperty(freshResource, "ReservationId")));
        var firstClaim = Assert.Single(AsObjects(ReadRequiredProperty(firstResource, "Claims")));
        var freshClaim = Assert.Single(AsObjects(ReadRequiredProperty(freshResource, "Claims")));
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(firstClaim, "ClaimFingerprint")),
            Convert.ToString(ReadRequiredProperty(freshClaim, "ClaimFingerprint")));

        var acceptedState = coldFixture.GetAcceptedState();
        var exhausted = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), "PrepareProcedureRequest", 6),
            new object?[]
            {
                acceptedState,
                coldFixture.ReadCurrentHistory(),
                coldFixture.ReadCurrentWound(),
                scenario.OperationKey + "_third_resource_claim",
                scenario.RouteId,
                coldFixture.AcceptedEventRef(acceptedState)
            });
        AssertInvalidTypedResult(
            exhausted,
            "Request",
            "persisted plus fresh resource claims exhaust the two-item fixture");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(exhausted, "Issues")));
    }

    [Theory]
    [InlineData("course", "course_first_milestone_is_ready_at_inclusive_due_time")]
    [InlineData("guaranteed", "guaranteed_current_capability_proof_stabilizes")]
    public void PersistedCommand_ColdRestartExecutesEveryNonProcedureReducerAndPublishesThroughT070(
        string mode,
        string scenarioName)
    {
        var scenario = CreateScenario(scenarioName, mode);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            mode,
            scenario.OperationKey + "_persisted_" + mode,
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            $"The persisted {mode} request is restored by its ordinary mode reducer.");

        var modified = PersistTreatmentCommand(fixture, command);
        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, modified);
        var durableCommandBytes = File.ReadAllBytes(fixture.FileSystem.ResolvePath(
            AcceptedMechanicsPlan.WoundCommandPath));

        using (var reducerFixture = CreateColdRootCopy(fixture))
        {
            Assert.NotSame(
                fixture.FileSystem.CanonicalRootAuthorityIdentity,
                reducerFixture.FileSystem.CanonicalRootAuthorityIdentity);
            var restoredRequest = ReadSinglePersistedTreatmentRequest(
                reducerFixture,
                durableCommandBytes,
                $"cold {mode} reducer reconstruction");
            Assert.NotSame(flow.Request, restoredRequest);
            Assert.Equal(CanonicalValue(flow.Request), CanonicalValue(restoredRequest));

            var rehydrated = RehydratePersistedTreatment(
                reducerFixture,
                mode,
                restoredRequest);
            Assert.NotSame(flow.Resolution, rehydrated.Resolution);
            Assert.Equal(
                CanonicalValue(flow.Resolution),
                CanonicalValue(rehydrated.Resolution));
            Assert.Equal(
                CanonicalValue(ReadRequiredProperty(flow.Resolution, "OutcomeIntents")),
                CanonicalValue(ReadRequiredProperty(rehydrated.Resolution, "OutcomeIntents")));
            Assert.Equal(
                CanonicalValue(ReadPropertyAllowingNull(flow.Resolution, "CriticalReactionIntent")),
                CanonicalValue(ReadPropertyAllowingNull(
                    rehydrated.Resolution,
                    "CriticalReactionIntent")));
        }

        using var publicationFixture = CreateColdRootCopy(fixture);
        var publicationRequest = ReadSinglePersistedTreatmentRequest(
            publicationFixture,
            durableCommandBytes,
            $"cold {mode} T070 reconstruction");
        var publicationFlow = RehydratePersistedTreatment(
            publicationFixture,
            mode,
            publicationRequest);
        ComposeAndPublishTreatment(publicationFixture, publicationFlow);

        var publishedWound = publicationFixture.ReadCurrentWound();
        if (string.Equals(mode, "course", StringComparison.Ordinal))
        {
            Assert.False(string.IsNullOrWhiteSpace(publishedWound.Care.ActiveCourseId));
            Assert.Equal(7, publicationFixture.ReadPlayerItemCount("antibiotic_dose"));
            publicationFixture.AssertItemIdentityIndexValid();
        }
        else
        {
            Assert.Equal("stabilized", publishedWound.Care.State);
            Assert.Null(publishedWound.Care.ActiveCourseId);
            Assert.Equal(8, publicationFixture.ReadPlayerItemCount("antibiotic_dose"));
        }
        Assert.Equal(2, publicationFixture.ReadNpcItemCount("sterile_thread"));
        Assert.Empty(publicationFixture.ReadActivePlayerEffectIds());
        var persistedHistory = publicationFixture.ReadCurrentHistory();
        var treatmentTransition = Assert.Single(
            persistedHistory.State!.Transitions,
            static transition => string.Equals(
                transition.Kind,
                "treat",
                StringComparison.Ordinal));
        var publicationCoordinates = ReadRequiredProperty(
            publicationRequest,
            "Coordinates");
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(
                publicationCoordinates,
                "OperationKey")),
            treatmentTransition.OperationKey);
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(
                publicationCoordinates,
                "AttemptId")),
            treatmentTransition.AttemptId);
        Assert.False(treatmentTransition.Terminal);

        publicationFixture.RestartForReplay();
        var exactReplay = ProbePublishedTreatment(publicationFixture, publicationRequest);
        Assert.Equal("ExactReplay", Convert.ToString(ReadRequiredProperty(
            exactReplay,
            "Status")));
        var receipt = ReadRequiredProperty(exactReplay, "Receipt");
        AssertClosedTreatmentReceipt(
            receipt,
            publicationRequest,
            publicationFlow.Resolution);
        AssertReceiptOwnsNoActionableOutcomeSurface(receipt.GetType());
    }

    [Fact]
    public void TreatmentCommand_PostSealResultTamperCannotParseOrRecompose()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_sealed_result",
                scenario.RouteId),
            "The sealed successful treatment is submitted.");

        var foreignScenario = CreateScenario(
            "procedure_disadvantage_uses_two_contiguous_dice",
            "procedure");
        using var foreignFixture = AcceptedStateFixture.Create(foreignScenario);
        var foreignCommand = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                foreignFixture,
                "procedure",
                foreignScenario.OperationKey + "_foreign_result",
                foreignScenario.RouteId),
            "A distinct failed treatment supplies a production result semantic.");

        var tampered = command.Root.DeepClone().AsObject();
        var result = ReadCommandTreatmentResult(tampered);
        var foreignResult = ReadCommandTreatmentResult(foreignCommand.Root);
        var declaredResultName = FindJsonPropertyName(result, "DeclaredResult");
        var foreignDeclaredResult = foreignResult[
            FindJsonPropertyName(foreignResult, "DeclaredResult")]!;
        Assert.False(JsonNode.DeepEquals(
            result[declaredResultName],
            foreignDeclaredResult));
        result[declaredResultName] = foreignDeclaredResult.DeepClone();

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(tampered));
        if (!parsed.Success)
        {
            Assert.NotEmpty(parsed.Issues);
            return;
        }

        var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(
            command.Binding,
            parsed,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.False(
            recomposed.Success,
            "A post-seal replacement of the treatment result semantic crossed both parsers.");
        Assert.NotEmpty(recomposed.Issues);
    }

    [Fact]
    public void FailedCommandPersistence_RollsBackBytesReleasesAllClaimsAndAllowsExactRetry()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        scenario.AcceptedState["sterileThreadCount"] = 1;
        using var fixture = AcceptedStateFixture.Create(scenario);
        var operationKey = scenario.OperationKey + "_failed_persistence";
        var first = ResolveCurrentTreatment(
            fixture,
            "procedure",
            operationKey,
            scenario.RouteId);
        var firstCommand = ComposeTreatmentCommand(
            first,
            "This command write is forced through a genuine rollback.");

        FailTreatmentCommandAfterPhysicalWrite(fixture, firstCommand);
        AssertNoDurableSubmittedTreatment(fixture);

        var reuseProbe = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_released_claim_probe",
            scenario.RouteId);
        AssertUsesSameReleasedDiceFateAndResource(first, reuseProbe);
        var probeCommand = ComposeTreatmentCommand(
            reuseProbe,
            "The released-input probe is rolled back as well.");
        FailTreatmentCommandAfterPhysicalWrite(fixture, probeCommand);
        AssertNoDurableSubmittedTreatment(fixture);

        var exactRetry = ResolveCurrentTreatment(
            fixture,
            "procedure",
            operationKey,
            scenario.RouteId);
        Assert.Equal(CanonicalValue(first.Request), CanonicalValue(exactRetry.Request));
        Assert.Equal(CanonicalValue(first.Resolution), CanonicalValue(exactRetry.Resolution));
        var repeatedExactRetry = ResolveCurrentTreatment(
            fixture,
            "procedure",
            operationKey,
            scenario.RouteId);
        Assert.Equal(CanonicalValue(exactRetry.Request), CanonicalValue(repeatedExactRetry.Request));
        Assert.Equal(
            CanonicalValue(exactRetry.Resolution),
            CanonicalValue(repeatedExactRetry.Resolution));

        var acceptedState = fixture.GetAcceptedState();
        var competing = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), "PrepareProcedureRequest", 6),
            new object?[]
            {
                acceptedState,
                fixture.ReadCurrentHistory(),
                fixture.ReadCurrentWound(),
                scenario.OperationKey + "_rollback_single_claim_probe",
                scenario.RouteId,
                fixture.AcceptedEventRef(acceptedState)
            });
        AssertInvalidTypedResult(
            competing,
            "Request",
            "two exact retries retain one logical claim against the one-item fixture");
        AssertNoDurableSubmittedTreatment(fixture);
    }

    [Fact]
    public void CommandAndRepairPending_ByteDifferentSemanticCopiesCoalesceToOneCompleteRequest()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(1, 1, 17);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_command_pending_exact",
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "A bounded repair wave retains the exact submitted treatment authority.");
        var pending = ComposeTreatmentRepairPendingRoot(
            command,
            CreateTreatmentRepairPackets(command.Binding, scenario.Before));

        var commandRequest = ReadCommandTreatmentRequest(command.Root);
        var pendingRequest = FindSerializedTreatmentRequest(pending);
        Assert.True(JsonNode.DeepEquals(commandRequest, pendingRequest));
        ReverseJsonObjectProperties(pendingRequest);
        Assert.True(JsonNode.DeepEquals(commandRequest, pendingRequest));
        Assert.NotEqual(commandRequest.ToJsonString(), pendingRequest.ToJsonString());

        var requests = AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(command.Root, pending, flow.History),
            "byte-semantic command/pending coalescing");
        var restored = Assert.Single(requests);
        Assert.Equal(CanonicalValue(flow.Request), CanonicalValue(restored));
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(flow.Request, "RequirementAuthority")),
            CanonicalValue(ReadRequiredProperty(restored, "RequirementAuthority")));
        Assert.Equal(
            CanonicalValue(ReadRequiredProperty(flow.Request, "ResourceAuthority")),
            CanonicalValue(ReadRequiredProperty(restored, "ResourceAuthority")));
    }

    [Fact]
    public void RepairPending_DoesNotCopySubmittedTreatmentWithoutANonEmptyRepairWave()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_no_repair_wave",
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "No bounded wound repair wave is present.");

        var pending = Assert.IsType<JsonObject>(Invoke(
            ExactStaticMethod(typeof(WoundRepairPacketBuilder), "ComposePendingRoot", 3),
            new object?[]
            {
                command.Binding,
                Array.Empty<WoundRepairPacket>(),
                command.Parsed
            }));

        Assert.DoesNotContain(EnumerateJsonObjects(pending), candidate =>
            HasJsonProperty(candidate.Value, "submittedTreatmentRequests"));
        Assert.DoesNotContain(
            ReadJsonString(ReadCommandTreatmentRequest(command.Root), "RequestFingerprint"),
            pending.ToJsonString(),
            StringComparison.Ordinal);
        Assert.Empty(FindSerializedTreatmentRequests(pending));
    }

    [Theory]
    [InlineData("request")]
    [InlineData("requirement_bundle")]
    [InlineData("resource_authority")]
    [InlineData("outer_coordinate")]
    public void PersistedCatalog_RejectsEveryCommandPendingDivergence(string axis)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_persisted_divergence_" + axis,
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "The submitted treatment enters a bounded repair wave.");
        var pending = ComposeTreatmentRepairPendingRoot(
            command,
            CreateTreatmentRepairPackets(command.Binding, scenario.Before));
        Assert.Single(AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(command.Root, pending, flow.History),
            axis + " baseline"));

        var foreignScenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        foreignScenario.AcceptedState["sterileThreadCount"] = 3;
        using var foreignFixture = AcceptedStateFixture.Create(foreignScenario);
        var foreignFlow = ResolveCurrentTreatment(
            foreignFixture,
            "procedure",
            foreignScenario.OperationKey + "_persisted_foreign_" + axis,
            foreignScenario.RouteId);
        var foreignCommand = ComposeTreatmentCommand(
            foreignFlow,
            "A distinct production request supplies the divergent authority.");
        Assert.Single(AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(foreignCommand.Root, null, foreignFlow.History),
            axis + " production divergence source"));

        MutatePendingTreatmentAuthority(pending, foreignCommand.Root, axis);

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(command.Root, pending, flow.History),
            axis + " divergence");
    }

    [Fact]
    public void PersistedCatalog_RejectsDifferentOperationsSharingOneNonNullCourseMilestone()
    {
        var firstScenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        var secondScenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var firstFixture = AcceptedStateFixture.Create(firstScenario);
        using var secondFixture = AcceptedStateFixture.Create(secondScenario);
        const string startSuffix = "_persisted_course_start";
        ComposeAndPublishTreatment(firstFixture, ResolveCurrentTreatment(
            firstFixture,
            "course",
            firstScenario.OperationKey + startSuffix,
            firstScenario.RouteId));
        ComposeAndPublishTreatment(secondFixture, ResolveCurrentTreatment(
            secondFixture,
            "course",
            secondScenario.OperationKey + startSuffix,
            secondScenario.RouteId));
        firstFixture.PrepareNextTurn(43, 480, "persisted_course_collision");
        secondFixture.PrepareNextTurn(43, 480, "persisted_course_collision");

        var first = ResolveCurrentTreatment(
            firstFixture,
            "course",
            firstScenario.OperationKey + "_persisted_course_first",
            firstScenario.RouteId);
        var second = ResolveCurrentTreatment(
            secondFixture,
            "course",
            secondScenario.OperationKey + "_persisted_course_second",
            secondScenario.RouteId);
        var firstCoordinates = ReadRequiredProperty(first.Request, "Coordinates");
        var secondCoordinates = ReadRequiredProperty(second.Request, "Coordinates");
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(firstCoordinates, "OperationKey")),
            Convert.ToString(ReadRequiredProperty(secondCoordinates, "OperationKey")));
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(firstCoordinates, "AttemptId")),
            Convert.ToString(ReadRequiredProperty(secondCoordinates, "AttemptId")));
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(first.Request, "RequestFingerprint")),
            Convert.ToString(ReadRequiredProperty(second.Request, "RequestFingerprint")));
        var firstMode = ReadRequiredProperty(first.Request, "ModeAuthority");
        var secondMode = ReadRequiredProperty(second.Request, "ModeAuthority");
        var firstCourseId = Convert.ToString(ReadRequiredProperty(firstMode, "CourseId"));
        var secondCourseId = Convert.ToString(ReadRequiredProperty(secondMode, "CourseId"));
        Assert.False(string.IsNullOrWhiteSpace(firstCourseId));
        Assert.Equal(
            firstCourseId,
            secondCourseId);
        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(first.Request, "MilestoneOrdinal")));
        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(second.Request, "MilestoneOrdinal")));
        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(firstMode, "MilestoneOrdinal")));
        Assert.Equal(2, Convert.ToInt32(ReadRequiredProperty(secondMode, "MilestoneOrdinal")));

        var firstCommand = ComposeTreatmentCommand(
            first,
            "The first course milestone request is submitted.");
        var secondCommand = ComposeTreatmentCommand(
            second,
            "A distinct operation submits the same durable course milestone.");
        Assert.Equal(firstCommand.Binding.SessionId, secondCommand.Binding.SessionId);
        Assert.Equal(firstCommand.Binding.RequestId, secondCommand.Binding.RequestId);
        Assert.Equal(firstCommand.Binding.SnapshotToken, secondCommand.Binding.SnapshotToken);
        var secondPending = ComposeTreatmentRepairPendingRoot(
            secondCommand,
            CreateTreatmentRepairPackets(
                secondCommand.Binding,
                secondScenario.Before));

        Assert.Single(AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(firstCommand.Root, null, first.History),
            "first course command alone"));
        Assert.Single(AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(null, secondPending, second.History),
            "second course pending request alone"));
        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(firstCommand.Root, secondPending, first.History),
            "durable course milestone uniqueness");
    }

    private static TreatmentPersistenceCommand ComposeTreatmentCommand(
        TreatmentFlow flow,
        string finalSceneText)
    {
        var binding = Assert.IsType<WoundAcceptedTurnBinding>(
            ReadAcceptedStateMember(flow.AcceptedState, "Binding"));
        var composer = ExactStaticMethod(
            typeof(WoundResponseInputComposer),
            "ComposeMortalWoundTreatmentCommandRoot",
            3);
        Assert.Equal(flow.Resolution.GetType(), composer.GetParameters()[1].ParameterType);
        var composed = Assert.IsType<JsonObject>(Invoke(
            composer,
            new object?[] { binding, flow.Resolution, finalSceneText }));
        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(composed));
        Assert.True(parsed.Success, DescribeIssues(parsed.Issues));
        var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(
            binding,
            parsed,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(recomposed.Success, DescribeIssues(recomposed.Issues));
        Assert.True(JsonNode.DeepEquals(composed, recomposed.CommandRoot));
        Assert.Equal(binding.SessionId, ReadJsonString(composed, "SessionId"));
        Assert.Equal(binding.RequestId, ReadJsonString(composed, "RequestId"));
        Assert.Equal(binding.SnapshotToken, ReadJsonString(composed, "SnapshotToken"));
        var commandRow = Assert.IsType<JsonObject>(Assert.Single(
            composed["commands"]!.AsArray()));
        Assert.Equal(
            new[]
            {
                "authority", "commandRef", "finalSceneText", "kind", "operationKey",
                "result", "transitionKind"
            },
            commandRow.Select(static pair => pair.Key)
                .OrderBy(static name => name, StringComparer.Ordinal));
        Assert.Equal("accepted_transition", ReadJsonString(commandRow, "Kind"));
        Assert.Equal("treat", ReadJsonString(commandRow, "TransitionKind"));
        Assert.Equal(finalSceneText, ReadJsonString(commandRow, "FinalSceneText"));
        Assert.False(string.IsNullOrWhiteSpace(ReadJsonString(commandRow, "CommandRef")));
        var authority = ReadJsonObject(commandRow, "Authority");
        var result = ReadJsonObject(commandRow, "Result");
        Assert.NotEmpty(authority);
        Assert.NotEmpty(result);
        var serializedRequest = ReadJsonObject(authority, "Request");
        var resultRequest = ReadJsonObject(result, "RequestAuthority");
        Assert.True(JsonNode.DeepEquals(serializedRequest, resultRequest));
        var serializedCopies = FindSerializedTreatmentRequests(composed);
        Assert.Equal(2, serializedCopies.Length);
        Assert.All(serializedCopies, copy =>
            Assert.True(JsonNode.DeepEquals(serializedRequest, copy)));
        AssertSerializedTypedValue(flow.Request, serializedRequest);
        AssertTreatmentCommandResult(flow, result);
        var coordinates = ReadRequiredProperty(flow.Request, "Coordinates");
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(coordinates, "OperationKey")),
            ReadJsonString(commandRow, "OperationKey"));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(flow.Request, "RequestFingerprint")),
            ReadJsonString(serializedRequest, "RequestFingerprint"));
        Assert.NotEmpty(ReadJsonObject(serializedRequest, "RequirementAuthority"));
        Assert.NotEmpty(ReadJsonObject(serializedRequest, "ResourceAuthority"));
        return new TreatmentPersistenceCommand(
            binding,
            composed,
            parsed,
            recomposed,
            finalSceneText);
    }

    private static IReadOnlyList<string> PersistTreatmentCommand(
        AcceptedStateFixture fixture,
        TreatmentPersistenceCommand command)
    {
        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            return new StateDistributor(
                    fixture.FileSystem,
                    NullLogger<StateDistributor>.Instance)
                .DistributeAsync(
                    new GameResponse { Response = command.FinalSceneText },
                    command.Recomposed)
                .GetAwaiter()
                .GetResult();
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }
    }

    private static void FailTreatmentCommandAfterPhysicalWrite(
        AcceptedStateFixture fixture,
        TreatmentPersistenceCommand command)
    {
        var baselines = new[]
        {
            CaptureFileBaseline(fixture, AcceptedMechanicsPlan.WoundCommandPath),
            CaptureFileBaseline(fixture, WoundHistoryState.HistoryPath),
            CaptureFileBaseline(fixture, WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
        };
        var commandWriteObserved = false;
        var hooks = new StateDistributorHooks
        {
            AfterFileMutationAppliedAsync = path =>
            {
                if (!string.Equals(
                        path,
                        AcceptedMechanicsPlan.WoundCommandPath,
                        StringComparison.Ordinal))
                {
                    return Task.CompletedTask;
                }

                commandWriteObserved = true;
                var written = JsonNode.Parse(File.ReadAllText(
                    fixture.FileSystem.ResolvePath(path)))!.AsObject();
                Assert.True(JsonNode.DeepEquals(command.Root, written));
                return Task.FromException(new IOException(
                    "T061 injected failure after the accepted treatment command write."));
            }
        };

        fixture.ReleaseLeaseForExternalDistribution();
        try
        {
            var failure = Assert.ThrowsAsync<IOException>(() =>
                    new StateDistributor(
                            fixture.FileSystem,
                            NullLogger<StateDistributor>.Instance,
                            hooks)
                        .DistributeAsync(
                            new GameResponse { Response = command.FinalSceneText },
                            command.Recomposed))
                .GetAwaiter()
                .GetResult();
            Assert.Contains("after the accepted treatment command write", failure.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }

        Assert.True(commandWriteObserved);
        Assert.All(baselines, baseline => AssertFileMatchesBaseline(fixture, baseline));
    }

    private static PersistedFileBaseline CaptureFileBaseline(
        AcceptedStateFixture fixture,
        string relativePath)
    {
        var path = fixture.FileSystem.ResolvePath(relativePath);
        return File.Exists(path)
            ? new PersistedFileBaseline(relativePath, true, File.ReadAllBytes(path))
            : new PersistedFileBaseline(relativePath, false, Array.Empty<byte>());
    }

    private static void AssertFileMatchesBaseline(
        AcceptedStateFixture fixture,
        PersistedFileBaseline baseline)
    {
        var path = fixture.FileSystem.ResolvePath(baseline.RelativePath);
        Assert.Equal(baseline.Existed, File.Exists(path));
        if (baseline.Existed)
            Assert.Equal(baseline.Bytes, File.ReadAllBytes(path));
    }

    private static void AssertNoDurableSubmittedTreatment(AcceptedStateFixture fixture)
    {
        Assert.False(File.Exists(fixture.FileSystem.ResolvePath(
            AcceptedMechanicsPlan.WoundCommandPath)));
        Assert.False(File.Exists(fixture.FileSystem.ResolvePath(
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath)));
        Assert.Empty(AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(null, null, fixture.ReadCurrentHistory()),
            "post-rollback durable request catalog"));
    }

    private static void AssertUsesSameReleasedDiceFateAndResource(
        TreatmentFlow original,
        TreatmentFlow probe)
    {
        var originalMode = ReadRequiredProperty(original.Request, "ModeAuthority");
        var probeMode = ReadRequiredProperty(probe.Request, "ModeAuthority");
        Assert.Equal(
            ReadIntSequence(ReadRequiredProperty(originalMode, "SourceIndices")),
            ReadIntSequence(ReadRequiredProperty(probeMode, "SourceIndices")));
        Assert.Equal(
            ReadIntSequence(ReadRequiredProperty(originalMode, "SourceRolls")),
            ReadIntSequence(ReadRequiredProperty(probeMode, "SourceRolls")));
        Assert.Equal(ReadPreparedFateEffectId(original.Request), ReadPreparedFateEffectId(probe.Request));

        var originalResource = ReadRequiredProperty(original.Request, "ResourceAuthority");
        var probeResource = ReadRequiredProperty(probe.Request, "ResourceAuthority");
        Assert.Equal("held", Convert.ToString(ReadRequiredProperty(probeResource, "ReservationDisposition")));
        Assert.NotEqual(
            Convert.ToString(ReadRequiredProperty(originalResource, "ReservationId")),
            Convert.ToString(ReadRequiredProperty(probeResource, "ReservationId")));
        var originalClaim = Assert.Single(AsObjects(ReadRequiredProperty(originalResource, "Claims")));
        var probeClaim = Assert.Single(AsObjects(ReadRequiredProperty(probeResource, "Claims")));
        foreach (var property in new[]
                 {
                     "Scope", "RequirementIndex", "Kind", "AuthorityRef", "Realm",
                     "OwnerKind", "OwnerId", "Quantity"
                 })
        {
            Assert.Equal(
                CanonicalValue(ReadRequiredProperty(originalClaim, property)),
                CanonicalValue(ReadRequiredProperty(probeClaim, property)));
        }
    }

    private static JsonObject ComposeTreatmentRepairPendingRoot(
        TreatmentPersistenceCommand command,
        IReadOnlyList<WoundRepairPacket> packets)
    {
        var pending = Assert.IsType<JsonObject>(Invoke(
            ExactStaticMethod(typeof(WoundRepairPacketBuilder), "ComposePendingRoot", 3),
            new object?[] { command.Binding, packets, command.Parsed }));
        var requestFingerprint = ReadJsonString(
            ReadCommandTreatmentRequest(command.Root),
            "RequestFingerprint");
        var commandRequest = ReadCommandTreatmentRequest(command.Root);
        var requirementFingerprint = ReadJsonString(
            ReadJsonObject(commandRequest, "RequirementAuthority"),
            "AuthorityFingerprint");
        var resourceFingerprint = ReadJsonString(
            ReadJsonObject(commandRequest, "ResourceAuthority"),
            "AuthorityFingerprint");
        var coordinates = ReadJsonObject(commandRequest, "Coordinates");
        var operationKey = ReadJsonString(coordinates, "OperationKey");
        var attemptId = ReadJsonString(coordinates, "AttemptId");
        var publicPacket = Assert.Single(packets).ToJsonObject().ToJsonString();
        Assert.DoesNotContain(requestFingerprint, publicPacket, StringComparison.Ordinal);
        Assert.DoesNotContain(requirementFingerprint, publicPacket, StringComparison.Ordinal);
        Assert.DoesNotContain(resourceFingerprint, publicPacket, StringComparison.Ordinal);
        Assert.DoesNotContain(operationKey, publicPacket, StringComparison.Ordinal);
        Assert.DoesNotContain(attemptId, publicPacket, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "submittedTreatmentRequests",
            publicPacket,
            StringComparison.OrdinalIgnoreCase);
        var privatePending = pending.ToJsonString();
        Assert.Contains(requestFingerprint, privatePending, StringComparison.Ordinal);
        Assert.Equal(command.Binding.SessionId, ReadJsonString(pending, "SessionId"));
        Assert.Equal(command.Binding.RequestId, ReadJsonString(pending, "RequestId"));
        Assert.Equal(command.Binding.SnapshotToken, ReadJsonString(pending, "SnapshotToken"));
        var submittedName = FindJsonPropertyName(pending, "SubmittedTreatmentRequests");
        var submitted = Assert.IsType<JsonArray>(pending[submittedName]);
        var submittedRow = Assert.IsType<JsonObject>(Assert.Single(submitted));
        var pendingRequest = FindSerializedTreatmentRequest(pending);
        Assert.True(JsonNode.DeepEquals(commandRequest, pendingRequest));
        Assert.Equal(
            operationKey,
            ReadJsonString(submittedRow, "OperationKey"));
        Assert.Equal(
            attemptId,
            ReadJsonString(submittedRow, "AttemptId"));
        Assert.Equal(
            requestFingerprint,
            ReadJsonString(submittedRow, "RequestFingerprint"));
        var packet = Assert.Single(packets);
        Assert.Contains(packet.CandidateRef, privatePending, StringComparison.Ordinal);
        Assert.Contains("field clinic", privatePending, StringComparison.Ordinal);
        var receipt = packet.CreateReceipt();
        Assert.Single(EnumerateJsonObjects(pending).Select(static value => value.Value), candidate =>
            candidate.Count == 5 &&
            JsonStringEquals(candidate, "SessionId", receipt.SessionId) &&
            JsonStringEquals(candidate, "RequestId", receipt.RequestId) &&
            JsonStringEquals(candidate, "SnapshotToken", receipt.SnapshotToken) &&
            JsonStringEquals(candidate, "CandidateRef", receipt.CandidateRef) &&
            JsonStringEquals(candidate, "SemanticFingerprint", receipt.SemanticFingerprint));
        return pending;
    }

    private static IReadOnlyList<WoundRepairPacket> CreateTreatmentRepairPackets(
        WoundAcceptedTurnBinding binding,
        JsonObject canonicalWound)
    {
        var (repairBinding, opportunity) = CreateTreatmentRepairOpportunity(binding);
        var proposal = CreateTreatmentRepairProposal(canonicalWound);
        var treatment = ReadJsonObject(proposal, "Treatment");
        var route = Assert.IsType<JsonObject>(Assert.Single(
            treatment[FindJsonPropertyName(treatment, "Routes")]!.AsArray()));
        Assert.True(route.Remove(FindJsonPropertyName(route, "DisplayName")));
        var finalSceneText = ReadJsonString(
            ReadJsonObject(proposal, "Display"),
            "AcquisitionNarration");
        var rejectedDecision = new JsonObject
        {
            ["opportunityRef"] = opportunity.PublicRef,
            ["decision"] = "materialize",
            ["woundRef"] = "local_t061_persisted_repair_wound",
            ["proposal"] = proposal
        };
        var composition = WoundResponseInputComposer.Compose(
            repairBinding,
            new[] { opportunity },
            new[] { JsonSerializer.SerializeToElement(rejectedDecision) },
            finalSceneText,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.False(composition.Success);
        Assert.NotEmpty(composition.Issues);
        Assert.Contains(composition.Issues, static issue =>
            issue.WoundRepairContext is not null);

        var packets = WoundRepairPacketBuilder.Build(composition.Issues);
        var packet = Assert.Single(packets);
        Assert.Equal(binding.SessionId, packet.SessionId);
        Assert.Equal(binding.RequestId, packet.RequestId);
        Assert.Equal(binding.SnapshotToken, packet.SnapshotToken);
        Assert.True(packet.MatchesOpportunity(opportunity));
        Assert.DoesNotContain(
            opportunity.AuthorityFingerprint,
            packet.ToJsonObject().ToJsonString(),
            StringComparison.Ordinal);
        return packets;
    }

    private static (
        WoundAcceptedTurnBinding Binding,
        WoundOpportunityAuthority Opportunity) CreateTreatmentRepairOpportunity(
        WoundAcceptedTurnBinding submittedBinding)
    {
        var evidence = new WoundOpportunityEventEvidence(
            "narrative",
            "narrative_injury",
            "event_authority_t061_persisted_repair_001",
            "harmful",
            4,
            "Field treatment in the field clinic requires one bounded correction.");
        var acceptedEvent = new WoundAcceptedEventAuthority(
            "event_t061_persisted_repair_001",
            "narrative_injury",
            "event_authority_t061_persisted_repair_001",
            WoundOpportunityEventEvidenceFingerprint.Compute(evidence));
        var acceptedEvents = new[] { acceptedEvent };
        var repairBinding = new WoundAcceptedTurnBinding(
            submittedBinding.SessionId,
            submittedBinding.RequestId,
            submittedBinding.SnapshotToken,
            submittedBinding.Realm,
            submittedBinding.Turn,
            acceptedEvents,
            WoundAcceptedEventSetFingerprint.Compute(acceptedEvents));
        var result = WoundOpportunityAuthority.Compose(new WoundOpportunityBuildRequest(
            repairBinding,
            "opportunity_internal_t061_persisted_repair_001",
            "opportunity_t061_persisted_repair_001",
            acceptedEvent.EventRef,
            new WoundOwnerCoordinate(
                submittedBinding.Realm,
                "player",
                "player_current",
                WoundCarrierCatalog.PlayerPath),
            "physical",
            "mortal_narrative_injury_v1",
            "combat_action",
            "combat_action_t061_persisted_repair_001",
            "active",
            evidence,
            HardMaximumSeverityRank: 4,
            GuaranteedTrigger: null,
            new WoundOpportunitySafeContext(
                "patient in the field clinic",
                "field treatment requires one bounded correction",
                new[] { "anatomical", "systemic", "other" })));
        Assert.True(result.Success, DescribeIssues(result.Issues));
        return (
            repairBinding,
            Assert.IsType<WoundOpportunityAuthority>(result.Opportunity));
    }

    private static JsonObject CreateTreatmentRepairProposal(JsonObject canonicalWound)
    {
        var classification = ReadJsonObject(canonicalWound, "Classification")
            .DeepClone()
            .AsObject();
        Assert.True(classification.Remove(FindJsonPropertyName(classification, "Domain")));
        return new JsonObject
        {
            ["classification"] = classification,
            ["display"] = ReadJsonObject(canonicalWound, "Display").DeepClone(),
            ["severity"] = ReadJsonString(
                ReadJsonObject(canonicalWound, "Severity"),
                "Value"),
            ["complications"] = new JsonArray(),
            ["consequenceDefinitions"] = new JsonArray(),
            ["treatment"] = ReadJsonObject(canonicalWound, "Treatment").DeepClone(),
            ["recovery"] = ReadJsonObject(canonicalWound, "Recovery").DeepClone()
        };
    }

    private static AcceptedStateFixture CreateColdRootCopy(AcceptedStateFixture source)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "boe-t061-cold-" + Guid.NewGuid().ToString("N"));
        FileSystemManager.CanonicalWriteLease? lease = null;
        try
        {
            CopyDirectoryTree(source.Root, root);
            var fileSystem = new FileSystemManager(
                root,
                NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            lease = fileSystem.AcquireCanonicalWriteLeaseAsync()
                .GetAwaiter()
                .GetResult();
            return source.AttachColdRoot(root, fileSystem, lease);
        }
        catch
        {
            lease?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            throw;
        }
    }

    private static TreatmentFlow RehydratePersistedTreatment(
        AcceptedStateFixture fixture,
        string mode,
        object restoredRequest)
    {
        var history = fixture.ReadCurrentHistory();
        var before = fixture.ReadCurrentWound();
        var acceptedState = fixture.GetAcceptedState();
        var result = Invoke(
            ExactStaticMethod(RequireOutcomeResolver(), mode switch
            {
                "procedure" => "CreateProcedureAttempt",
                "course" => "CreateCourseMilestoneAttempt",
                "guaranteed" => "CreateGuaranteedAttempt",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            }, 4),
            new object?[] { restoredRequest, history, before, acceptedState });
        Assert.Equal(
            "Resolved",
            Convert.ToString(ReadRequiredProperty(result, "Disposition")));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        return new TreatmentFlow(
            acceptedState,
            restoredRequest,
            ReadRequiredProperty(result, "Resolution"),
            before,
            history);
    }

    private static object ReadSinglePersistedTreatmentRequest(
        AcceptedStateFixture fixture,
        byte[] expectedCommandBytes,
        string boundary)
    {
        var commandPath = fixture.FileSystem.ResolvePath(
            AcceptedMechanicsPlan.WoundCommandPath);
        Assert.Equal(expectedCommandBytes, File.ReadAllBytes(commandPath));
        var commandRoot = JsonNode.Parse(File.ReadAllText(commandPath))!.AsObject();
        return Assert.Single(AssertValidPersistedCatalog(
            ParsePersistedRequestCatalog(
                commandRoot,
                null,
                fixture.ReadCurrentHistory()),
            boundary));
    }

    private static void CopyDirectoryTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }
        foreach (var file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static void AssertTreatmentCommandResult(
        TreatmentFlow flow,
        JsonObject result)
    {
        Assert.Equal(
            new[]
            {
                "attemptDisposition", "consumptionTrigger", "declaredResult", "interruption",
                "kind", "mode", "modeEvidence", "receiptFingerprint", "requestAuthority",
                "resolutionAuthorityFingerprint", "resultCategory", "resultFingerprint",
                "routeCompletion", "routeFingerprint", "routeId", "selectedOutcomeIndex"
            },
            result.Select(static pair => pair.Key)
                .OrderBy(static name => name, StringComparer.Ordinal));
        Assert.Equal("treat", ReadJsonString(result, "Kind"));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(flow.Resolution, "Mode")),
            ReadJsonString(result, "Mode"));
        Assert.Equal("accepted_terminal", ReadJsonString(result, "AttemptDisposition"));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(
                ReadRequiredProperty(flow.Request, "Coordinates"),
                "RouteId")),
            ReadJsonString(result, "RouteId"));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(flow.Resolution, "RouteFingerprint")),
            ReadJsonString(result, "RouteFingerprint"));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(flow.Resolution, "ResultCategory")),
            ReadJsonString(result, "ResultCategory"));
        AssertSerializedTypedValue(
            ReadPropertyAllowingNull(flow.Resolution, "SelectedOutcomeIndex"),
            result[FindJsonPropertyName(result, "SelectedOutcomeIndex")]);
        Assert.Equal(
            Assert.IsType<bool>(ReadRequiredProperty(flow.Resolution, "Interruption")),
            result[FindJsonPropertyName(result, "Interruption")]!.GetValue<bool>());
        AssertSerializedTypedValue(
            ReadRequiredProperty(flow.Resolution, "DeclaredResult"),
            result[FindJsonPropertyName(result, "DeclaredResult")]);
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(flow.Resolution, "ConsumptionTrigger")),
            ReadJsonString(result, "ConsumptionTrigger"));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(
                flow.Resolution,
                "ResolutionAuthorityFingerprint")),
            ReadJsonString(result, "ResolutionAuthorityFingerprint"));
        AssertSerializedTypedValue(
            ReadRequiredProperty(flow.Resolution, "ModeEvidence"),
            result[FindJsonPropertyName(result, "ModeEvidence")]);
        Assert.Equal(
            ToContractToken(Convert.ToString(ReadRequiredProperty(
                flow.Resolution,
                "RouteCompletion"))!),
            ReadJsonString(result, "RouteCompletion"));
        Assert.Equal(
            Convert.ToString(ReadRequiredProperty(flow.Resolution, "ResultFingerprint")),
            ReadJsonString(result, "ResultFingerprint"));
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            ReadJsonString(result, "ReceiptFingerprint")));
    }

    private static void AssertSerializedTypedValue(object? expected, JsonNode? actual)
    {
        var expectedNode = expected is null
            ? null
            : JsonSerializer.SerializeToNode(expected, expected.GetType());
        Assert.True(JsonNode.DeepEquals(
            NormalizeJsonPropertyNames(expectedNode),
            NormalizeJsonPropertyNames(actual)));
    }

    private static JsonNode? NormalizeJsonPropertyNames(JsonNode? node)
    {
        if (node is JsonObject sourceObject)
        {
            var normalized = new JsonObject();
            foreach (var pair in sourceObject.OrderBy(
                         static pair => pair.Key,
                         StringComparer.OrdinalIgnoreCase))
            {
                normalized.Add(
                    pair.Key.ToLowerInvariant(),
                    NormalizeJsonPropertyNames(pair.Value));
            }
            return normalized;
        }
        if (node is JsonArray sourceArray)
        {
            return new JsonArray(sourceArray
                .Select(NormalizeJsonPropertyNames)
                .ToArray());
        }
        return node?.DeepClone();
    }

    private static string ToContractToken(string value)
    {
        var characters = new List<char>(value.Length + 4);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index != 0)
                characters.Add('_');
            characters.Add(char.ToLowerInvariant(character));
        }
        return new string(characters.ToArray());
    }

    private static object ParsePersistedRequestCatalog(
        JsonObject? command,
        JsonObject? pending,
        WoundHistoryParseResult history)
    {
        object? commandArgument = command is null
            ? null
            : JsonSerializer.SerializeToElement(command);
        object? pendingArgument = pending is null
            ? null
            : JsonSerializer.SerializeToElement(pending);
        return Invoke(
            ExactStaticMethod(RequirePersistedRequestCatalog(), "Parse", 3),
            new[] { commandArgument, pendingArgument, (object?)history });
    }

    private static Type RequirePersistedRequestCatalog()
    {
        var type = typeof(WoundMaterializationContract).Assembly.GetType(
            "BookOfEternityClient.Services.MortalWoundTreatmentPersistedRequestCatalog",
            throwOnError: false,
            ignoreCase: false);
        Assert.NotNull(type);
        return type!;
    }

    private static object[] AssertValidPersistedCatalog(object result, string boundary)
    {
        AssertClosedProperties(result, new[] { "IsValid", "Issues", "Requests" });
        Assert.True(
            Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            boundary + " was rejected by the persisted-request catalog.");
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Issues")));
        return AsObjects(ReadRequiredProperty(result, "Requests"));
    }

    private static void AssertInvalidPersistedCatalog(object result, string boundary)
    {
        AssertClosedProperties(result, new[] { "IsValid", "Issues", "Requests" });
        Assert.False(
            Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            boundary + " unexpectedly crossed the persisted-request catalog.");
        Assert.NotEmpty(AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>));
        Assert.Empty(AsObjects(ReadRequiredProperty(result, "Requests")));
    }

    private static JsonObject FindSerializedTreatmentRequest(JsonObject root)
    {
        return Assert.Single(FindSerializedTreatmentRequests(root));
    }

    private static JsonObject ReadCommandTreatmentRequest(JsonObject root)
    {
        var command = Assert.IsType<JsonObject>(Assert.Single(
            root[FindJsonPropertyName(root, "Commands")]!.AsArray()));
        return ReadJsonObject(ReadJsonObject(command, "Authority"), "Request");
    }

    private static JsonObject ReadCommandTreatmentResult(JsonObject root)
    {
        var command = Assert.IsType<JsonObject>(Assert.Single(
            root[FindJsonPropertyName(root, "Commands")]!.AsArray()));
        return ReadJsonObject(command, "Result");
    }

    private static JsonObject[] FindSerializedTreatmentRequests(JsonObject root) =>
        EnumerateJsonObjects(root)
            .Select(static value => value.Value)
            .Where(static candidate =>
                HasJsonProperty(candidate, "Coordinates") &&
                HasJsonProperty(candidate, "ModeAuthority") &&
                HasJsonProperty(candidate, "RequirementAuthority") &&
                HasJsonProperty(candidate, "ResourceAuthority") &&
                HasJsonProperty(candidate, "RequestFingerprint"))
            .ToArray();

    private static void MutatePendingTreatmentAuthority(
        JsonObject pending,
        JsonObject foreignCommand,
        string axis)
    {
        var request = FindSerializedTreatmentRequest(pending);
        var foreignRequest = ReadCommandTreatmentRequest(foreignCommand);
        Assert.False(JsonNode.DeepEquals(request, foreignRequest));
        var requirements = ReadJsonObject(request, "RequirementAuthority");
        var resource = ReadJsonObject(request, "ResourceAuthority");
        var foreignRequirements = ReadJsonObject(foreignRequest, "RequirementAuthority");
        var foreignResource = ReadJsonObject(foreignRequest, "ResourceAuthority");
        switch (axis)
        {
            case "request":
                ReplaceJsonObject(request, foreignRequest);
                break;
            case "requirement_bundle":
                Assert.False(JsonNode.DeepEquals(requirements, foreignRequirements));
                ReplaceJsonObject(
                    requirements,
                    foreignRequirements);
                break;
            case "resource_authority":
                Assert.False(JsonNode.DeepEquals(resource, foreignResource));
                ReplaceJsonObject(
                    resource,
                    foreignResource);
                break;
            case "outer_coordinate":
            {
                var outer = EnumerateJsonObjects(pending)
                    .Where(candidate =>
                        !IsWithin(candidate.Value, request) &&
                        HasJsonProperty(candidate.Value, "OperationKey"))
                    .OrderBy(static candidate => candidate.Depth)
                    .Select(static candidate => candidate.Value)
                    .FirstOrDefault();
                Assert.NotNull(outer);
                var foreignOperationKey = ReadJsonString(
                    ReadJsonObject(foreignRequest, "Coordinates"),
                    "OperationKey");
                Assert.NotEqual(
                    ReadJsonString(outer!, "OperationKey"),
                    foreignOperationKey);
                SetJsonProperty(outer!, "OperationKey", foreignOperationKey);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }
    }

    private static void ReplaceJsonObject(JsonObject target, JsonObject source)
    {
        var detached = source
            .Select(static pair => new KeyValuePair<string, JsonNode?>(
                pair.Key,
                pair.Value?.DeepClone()))
            .ToArray();
        target.Clear();
        foreach (var pair in detached)
            target.Add(pair.Key, pair.Value);
    }

    private static void ReverseJsonObjectProperties(JsonObject value)
    {
        var properties = value
            .Select(static pair => new KeyValuePair<string, JsonNode?>(
                pair.Key,
                pair.Value?.DeepClone()))
            .Reverse()
            .ToArray();
        Assert.True(properties.Length > 1);
        value.Clear();
        foreach (var property in properties)
            value.Add(property.Key, property.Value);
    }

    private static IEnumerable<(JsonObject Value, int Depth)> EnumerateJsonObjects(
        JsonNode? node,
        int depth = 0)
    {
        if (node is JsonObject currentObject)
        {
            yield return (currentObject, depth);
            foreach (var child in currentObject.Select(static pair => pair.Value))
            foreach (var descendant in EnumerateJsonObjects(child, depth + 1))
                yield return descendant;
        }
        else if (node is JsonArray currentArray)
        {
            foreach (var child in currentArray)
            foreach (var descendant in EnumerateJsonObjects(child, depth + 1))
                yield return descendant;
        }
    }

    private static bool IsWithin(JsonNode node, JsonNode ancestor)
    {
        for (JsonNode? current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
        }

        return false;
    }

    private static bool HasJsonProperty(JsonObject value, string propertyName) =>
        value.Any(pair => string.Equals(
            pair.Key,
            propertyName,
            StringComparison.OrdinalIgnoreCase));

    private static bool JsonStringEquals(
        JsonObject value,
        string propertyName,
        string expected)
    {
        var names = value
            .Select(static pair => pair.Key)
            .Where(name => string.Equals(
                name,
                propertyName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return names.Length == 1 &&
               value[names[0]] is JsonValue scalar &&
               scalar.TryGetValue<string>(out var actual) &&
               string.Equals(actual, expected, StringComparison.Ordinal);
    }

    private static JsonObject ReadJsonObject(JsonObject value, string propertyName)
    {
        var name = FindJsonPropertyName(value, propertyName);
        return Assert.IsType<JsonObject>(value[name]);
    }

    private static string ReadJsonString(JsonObject value, string propertyName)
    {
        var name = FindJsonPropertyName(value, propertyName);
        return value[name]!.GetValue<string>();
    }

    private static void SetJsonProperty(
        JsonObject value,
        string propertyName,
        string replacement)
    {
        var name = FindJsonPropertyName(value, propertyName);
        value[name] = replacement;
    }

    private static string FindJsonPropertyName(JsonObject value, string propertyName) =>
        Assert.Single(
            value.Select(static pair => pair.Key),
            name => string.Equals(
                name,
                propertyName,
                StringComparison.OrdinalIgnoreCase));

    private static string ReadPreparedFateEffectId(object request) =>
        Convert.ToString(ReadRequiredProperty(
            ReadRequiredProperty(
                ReadRequiredProperty(request, "ModeAuthority"),
                "PreparedCriticalReaction"),
            "EffectId"))!;

    private sealed record TreatmentPersistenceCommand(
        WoundAcceptedTurnBinding Binding,
        JsonObject Root,
        WoundResponseCommandParsingResult Parsed,
        WoundResponseInputCompositionResult Recomposed,
        string FinalSceneText);

    private sealed record PersistedFileBaseline(
        string RelativePath,
        bool Existed,
        byte[] Bytes);
}
