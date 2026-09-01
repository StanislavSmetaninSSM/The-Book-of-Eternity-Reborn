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

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    public void TreatmentCommandClassifier_DoesNotCaptureOtherAcceptedTransitionFamilies(
        string transitionKind)
    {
        var row = JsonSerializer.SerializeToElement(new
        {
            kind = "accepted_transition",
            transitionKind
        });

        Assert.False(MortalWoundTreatmentCommandCodec.IsTreatmentCommand(row));
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
            RestoreCurrentPersistedTreatmentCatalog(coldFixture),
            "cold command reconstruction and live-claim recovery"));
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
                RestoreCurrentPersistedTreatmentCatalog(publicationFixture),
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
    public void TreatmentCommand_CommandRefIsRecomputedFromTheCompletePersistedCommand()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_command_ref_tamper",
                scenario.RouteId),
            "The deterministic command identity cannot be replaced.");
        var tampered = command.Root.DeepClone().AsObject();
        var row = Assert.IsType<JsonObject>(Assert.Single(
            tampered["commands"]!.AsArray()));
        row[FindJsonPropertyName(row, "CommandRef")] = "foreign_command_ref_1536";

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(tampered));
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_persisted_command_ref_mismatch",
            StringComparison.Ordinal));
    }

    [Fact]
    public void TreatmentCommand_ComposerRejectsForeignAcceptedTurnBinding()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_foreign_composer_binding",
            scenario.RouteId);
        var binding = Assert.IsType<WoundAcceptedTurnBinding>(
            ReadAcceptedStateMember(flow.AcceptedState, "Binding"));
        var foreign = binding with { SessionId = "foreign_session_1536" };

        Assert.Throws<InvalidOperationException>(() =>
            WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(
                foreign,
                Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution),
                "A foreign accepted root must not produce durable treatment bytes."));
    }

    [Theory]
    [InlineData("procedure_roll")]
    [InlineData("requirement_evidence")]
    [InlineData("resource_claim")]
    public void TreatmentCommand_NestedAuthorityTamperCannotCrossDetachedSealValidation(
        string axis)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                "procedure",
                scenario.OperationKey + "_nested_tamper_" + axis,
                scenario.RouteId),
            "Every nested authority remains sealed after persistence.");
        var tampered = command.Root.DeepClone().AsObject();
        var copies = FindSerializedTreatmentRequests(tampered);
        Assert.Equal(2, copies.Length);
        foreach (var request in copies)
        {
            switch (axis)
            {
                case "procedure_roll":
                {
                    var modeAuthority = ReadJsonObject(request, "ModeAuthority");
                    var name = FindJsonPropertyName(modeAuthority, "NaturalRoll");
                    modeAuthority[name] = modeAuthority[name]!.GetValue<int>() == 20 ? 19 : 20;
                    break;
                }
                case "requirement_evidence":
                {
                    var requirement = ReadJsonObject(request, "RequirementAuthority");
                    var scopes = Assert.IsType<JsonArray>(requirement[
                        FindJsonPropertyName(requirement, "Scopes")]);
                    var scope = Assert.IsType<JsonObject>(Assert.Single(scopes));
                    var bindings = Assert.IsType<JsonArray>(scope[
                        FindJsonPropertyName(scope, "Bindings")]);
                    var binding = bindings.OfType<JsonObject>().First(candidate =>
                        HasJsonProperty(
                            ReadJsonObject(
                                ReadJsonObject(candidate, "SuccessWitness"),
                                "Evidence"),
                            "Count"));
                    var witness = ReadJsonObject(binding, "SuccessWitness");
                    var evidence = ReadJsonObject(witness, "Evidence");
                    var countName = FindJsonPropertyName(evidence, "Count");
                    evidence[countName] = evidence[countName]!.GetValue<int>() + 1;
                    break;
                }
                case "resource_claim":
                {
                    var resource = ReadJsonObject(request, "ResourceAuthority");
                    var claims = Assert.IsType<JsonArray>(resource[
                        FindJsonPropertyName(resource, "Claims")]);
                    var claim = Assert.IsType<JsonObject>(Assert.Single(claims));
                    var quantityName = FindJsonPropertyName(claim, "Quantity");
                    claim[quantityName] = claim[quantityName]!.GetValue<int>() + 1;
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
            }
        }

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(tampered));
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_persisted_request_seal_mismatch",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("command", "sessionId")]
    [InlineData("command", "requestId")]
    [InlineData("command", "snapshotToken")]
    [InlineData("pending", "sessionId")]
    [InlineData("pending", "requestId")]
    [InlineData("pending", "snapshotToken")]
    public void PersistedTreatment_RootBindingMustMatchEverySealedRequest(
        string origin,
        string field)
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_foreign_root_" + origin + "_" + field,
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "The submitted treatment remains bound to one accepted turn root.");
        var pending = ComposeTreatmentRepairPendingRoot(
            command,
            CreateTreatmentRepairPackets(command.Binding, scenario.Before));
        var root = string.Equals(origin, "command", StringComparison.Ordinal)
            ? command.Root.DeepClone().AsObject()
            : pending.DeepClone().AsObject();
        root[field] = "foreign_" + field.ToLowerInvariant() + "_1536";

        if (string.Equals(origin, "command", StringComparison.Ordinal))
        {
            var parsed = WoundResponseInputComposer.ParseCommandRoot(
                JsonSerializer.SerializeToElement(root));
            Assert.False(parsed.Success);
            Assert.Contains(parsed.Issues, static issue => string.Equals(
                issue.Code,
                "mortal_wound_treatment_persisted_root_binding_mismatch",
                StringComparison.Ordinal));
        }

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(
                string.Equals(origin, "command", StringComparison.Ordinal) ? root : null,
                string.Equals(origin, "pending", StringComparison.Ordinal) ? root : null,
                flow.History),
            origin + " " + field + " binding mismatch");
    }

    [Theory]
    [InlineData("procedure", "procedure_normal_uses_lowest_free_die")]
    [InlineData("course", "course_first_milestone_is_ready_at_inclusive_due_time")]
    [InlineData("guaranteed", "guaranteed_current_capability_proof_stabilizes")]
    public void TreatmentCommand_ModeEvidencePayloadIsRecomputedRatherThanTrustingItsSeal(
        string mode,
        string scenarioName)
    {
        var scenario = CreateScenario(scenarioName, mode);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                mode,
                scenario.OperationKey + "_mode_evidence_tamper_" + mode,
                scenario.RouteId),
            "The persisted mode evidence remains derived from the sealed request.");
        var tampered = command.Root.DeepClone().AsObject();
        var evidence = ReadJsonObject(
            ReadCommandTreatmentResult(tampered),
            "ModeEvidence");
        switch (mode)
        {
            case "procedure":
                evidence[FindJsonPropertyName(evidence, "RollActorId")] =
                    "foreign_procedure_actor";
                break;
            case "course":
            {
                var property = FindJsonPropertyName(
                    evidence,
                    "ResolvedAtGameTimeMinutes");
                evidence[property] = evidence[property]!.GetValue<long>() + 1;
                break;
            }
            case "guaranteed":
                evidence[FindJsonPropertyName(evidence, "ActorRole")] = "target";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(tampered));
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_persisted_mode_evidence_mismatch",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("course_game_time")]
    [InlineData("requirement_evidence")]
    public void TreatmentCommand_NullNestedAuthorityFailsClosedWithoutThrowing(
        string axis)
    {
        var mode = string.Equals(axis, "course_game_time", StringComparison.Ordinal)
            ? "course"
            : "procedure";
        var scenarioName = mode == "course"
            ? "course_first_milestone_is_ready_at_inclusive_due_time"
            : "procedure_normal_uses_lowest_free_die";
        var scenario = CreateScenario(scenarioName, mode);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                mode,
                scenario.OperationKey + "_null_nested_" + axis,
                scenario.RouteId),
            "Malformed nullable authority input must fail closed.");
        var tampered = command.Root.DeepClone().AsObject();
        foreach (var request in FindSerializedTreatmentRequests(tampered))
        {
            if (string.Equals(axis, "course_game_time", StringComparison.Ordinal))
            {
                var authority = ReadJsonObject(request, "ModeAuthority");
                authority[FindJsonPropertyName(authority, "GameTimeAuthority")] = null;
                continue;
            }

            var requirement = ReadJsonObject(request, "RequirementAuthority");
            var scopes = Assert.IsType<JsonArray>(requirement[
                FindJsonPropertyName(requirement, "Scopes")]);
            var scope = Assert.IsType<JsonObject>(Assert.Single(scopes));
            var bindings = Assert.IsType<JsonArray>(scope[
                FindJsonPropertyName(scope, "Bindings")]);
            var binding = bindings.OfType<JsonObject>().First(candidate =>
                HasJsonProperty(candidate, "SuccessWitness"));
            var witness = ReadJsonObject(binding, "SuccessWitness");
            witness[FindJsonPropertyName(witness, "Evidence")] = null;
        }

        WoundResponseCommandParsingResult? parsed = null;
        var exception = Record.Exception(() => parsed =
            WoundResponseInputComposer.ParseCommandRoot(
                JsonSerializer.SerializeToElement(tampered)));
        Assert.Null(exception);
        Assert.NotNull(parsed);
        Assert.False(parsed.Success);
        Assert.NotEmpty(parsed.Issues);
    }

    [Fact]
    public void TreatmentCommand_IgnoredComputedAuthorityFieldCannotSurviveTypedRoundTrip()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var command = ComposeTreatmentCommand(
            ResolveCurrentTreatment(
                fixture,
                "course",
                scenario.OperationKey + "_computed_clock_tamper",
                scenario.RouteId),
            "Every serialized authority field is checked by typed reconstruction.");
        var tampered = command.Root.DeepClone().AsObject();
        foreach (var request in FindSerializedTreatmentRequests(tampered))
        {
            var authority = ReadJsonObject(request, "ModeAuthority");
            var gameTime = ReadJsonObject(authority, "GameTimeAuthority");
            gameTime[FindJsonPropertyName(gameTime, "ClockKind")] =
                "foreign_clock_kind";
        }

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(tampered));
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_persisted_request_roundtrip_mismatch",
            StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("not_required_hides_quantity", "procedure",
        "procedure_normal_uses_lowest_free_die")]
    [InlineData("missing_current_claim", "course",
        "course_first_milestone_is_ready_at_inclusive_due_time")]
    public void TreatmentCommand_ResealedResourceGraphMustMatchRequirementBindings(
        string axis,
        string mode,
        string scenarioName)
    {
        var scenario = CreateScenario(scenarioName, mode);
        if (string.Equals(axis, "missing_current_claim", StringComparison.Ordinal))
        {
            var route = scenario.Before["treatment"]!["routes"]![0]!.AsObject();
            route["requirements"]!.AsArray().Add(new JsonObject
            {
                ["kind"] = "item_quantity",
                ["itemRef"] = "sterile_thread",
                ["quantity"] = 1,
                ["ownerRole"] = "provider"
            });
            route["resourcePolicy"]!["mutations"]!.AsArray().Insert(
                0,
                new JsonObject
                {
                    ["kind"] = "consume_requirement",
                    ["scope"] = "common",
                    ["milestoneOrdinal"] = null,
                    ["requirementIndex"] = 1
                });
        }
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            mode,
            scenario.OperationKey + "_resource_agreement_" + axis,
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "Every held quantity remains linked to its exact success witness.");
        var policy = ReadRequiredProperty(
            ReadRequiredProperty(flow.Request, "ResourceAuthority"),
            "Policy");
        var policyFingerprint = Assert.IsType<string>(Invoke(
            ExactStaticMethod(
                typeof(MortalWoundTreatmentResourceComposer),
                "ComputePolicyFingerprint",
                1),
            new[] { policy }));
        var tampered = command.Root.DeepClone().AsObject();
        foreach (var request in FindSerializedTreatmentRequests(tampered))
        {
            var resource = ReadJsonObject(request, "ResourceAuthority");
            var claims = Assert.IsType<JsonArray>(resource[
                FindJsonPropertyName(resource, "Claims")]);
            if (string.Equals(
                    axis,
                    "not_required_hides_quantity",
                    StringComparison.Ordinal))
            {
                Assert.NotEmpty(claims);
                claims.Clear();
                resource[FindJsonPropertyName(resource, "ReservationDisposition")] =
                    "not_required";
                resource[FindJsonPropertyName(resource, "ReservationId")] = null;
            }
            else
            {
                Assert.True(claims.Count >= 2);
                claims.RemoveAt(claims.Count - 1);
            }
            ResealSerializedResourceAndRequest(request, policyFingerprint);
        }

        var parsed = WoundResponseInputComposer.ParseCommandRoot(
            JsonSerializer.SerializeToElement(tampered));
        Assert.False(parsed.Success);
        Assert.Contains(parsed.Issues, static issue =>
            string.Equals(
                issue.Code,
                "mortal_wound_treatment_persisted_request_seal_mismatch",
                StringComparison.Ordinal) &&
            Convert.ToString(issue.Actual)?.Contains(
                "resource_requirement_agreement",
                StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("missing_future_milestone")]
    [InlineData("future_requirement_is_not_quantity")]
    public void PersistedCourseRequest_ResourcePolicySelectorsMustMatchTheSealedStartingRoute(
        string axis)
    {
        var scenario = CreateFirstCourseResourceScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_future_selector_" + axis,
            scenario.RouteId);
        var request = Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(flow.Request));
        var resource = ReadJsonObject(request, "ResourceAuthority");
        var policy = ReadJsonObject(resource, "Policy");
        var mutations = Assert.IsType<JsonArray>(policy[
            FindJsonPropertyName(policy, "Mutations")]);
        var future = Assert.Single(
            mutations.OfType<JsonObject>(),
            mutation => ReadOptionalJsonInt32(mutation, "MilestoneOrdinal") == 2);
        if (string.Equals(axis, "missing_future_milestone", StringComparison.Ordinal))
        {
            future[FindJsonPropertyName(future, "MilestoneOrdinal")] = 4;
        }
        else
        {
            future[FindJsonPropertyName(future, "RequirementIndex")] = 1;
        }
        ResealSerializedResourceAndRequest(
            request,
            ComputeSerializedPolicyFingerprint(policy));

        var issues = new List<ValidationIssue>();
        var parsed = MortalWoundTreatmentCommandCodec.TryParseRequest(
            request,
            "request",
            issues,
            out _);

        Assert.False(parsed);
        Assert.Contains(issues, static issue =>
            string.Equals(
                issue.Code,
                "mortal_wound_treatment_persisted_request_seal_mismatch",
                StringComparison.Ordinal) &&
            Convert.ToString(issue.Actual)?.Contains(
                "resource_requirement_agreement",
                StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("missing_future_mutation")]
    [InlineData("missing_consume_trigger")]
    public void PersistedCourseRequest_ResourcePolicyMustExactlyMatchTheSealedStartingRoute(
        string axis)
    {
        var scenario = CreateFirstCourseResourceScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_policy_exact_" + axis,
            scenario.RouteId);
        var request = Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(flow.Request));
        var resource = ReadJsonObject(request, "ResourceAuthority");
        var policy = ReadJsonObject(resource, "Policy");
        if (string.Equals(axis, "missing_future_mutation", StringComparison.Ordinal))
        {
            var mutations = Assert.IsType<JsonArray>(policy[
                FindJsonPropertyName(policy, "Mutations")]);
            var futureIndex = mutations.Select((value, index) => (value, index))
                .Single(pair => ReadOptionalJsonInt32(
                    Assert.IsType<JsonObject>(pair.value),
                    "MilestoneOrdinal") == 2)
                .index;
            mutations.RemoveAt(futureIndex);
        }
        else
        {
            Assert.IsType<JsonArray>(policy[
                FindJsonPropertyName(policy, "ConsumeOn")]).Clear();
        }
        ResealSerializedResourceAndRequest(
            request,
            ComputeSerializedPolicyFingerprint(policy));

        var issues = new List<ValidationIssue>();
        var parsed = MortalWoundTreatmentCommandCodec.TryParseRequest(
            request,
            "request",
            issues,
            out _);

        Assert.False(parsed);
        Assert.Contains(issues, static issue =>
            string.Equals(
                issue.Code,
                "mortal_wound_treatment_persisted_request_seal_mismatch",
                StringComparison.Ordinal) &&
            Convert.ToString(issue.Actual)?.Contains(
                "resource_requirement_agreement",
                StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("due_minute")]
    [InlineData("deadline_minute")]
    [InlineData("window_disposition")]
    public void PersistedCourseRequest_WindowMustBeRecomputedFromTheSealedStartingRoute(
        string axis)
    {
        var scenario = CreateFirstCourseResourceScenario();
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_course_window_" + axis,
            scenario.RouteId);
        var request = Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(flow.Request));
        var modeAuthority = ReadJsonObject(request, "ModeAuthority");
        switch (axis)
        {
            case "due_minute":
                modeAuthority[FindJsonPropertyName(
                    modeAuthority,
                    "DueAtGameTimeMinutes")] =
                    modeAuthority[FindJsonPropertyName(
                        modeAuthority,
                        "DueAtGameTimeMinutes")]!.GetValue<long>() + 1;
                break;
            case "deadline_minute":
                modeAuthority[FindJsonPropertyName(
                    modeAuthority,
                    "DeadlineAtGameTimeMinutes")] =
                    modeAuthority[FindJsonPropertyName(
                        modeAuthority,
                        "DeadlineAtGameTimeMinutes")]!.GetValue<long>() + 1;
                break;
            default:
                modeAuthority[FindJsonPropertyName(
                    modeAuthority,
                    "WindowDisposition")] = "foreign_window";
                break;
        }
        modeAuthority[FindJsonPropertyName(modeAuthority, "AuthorityFingerprint")] =
            ComputeSerializedCourseAuthorityFingerprint(modeAuthority);
        var policy = ReadJsonObject(
            ReadJsonObject(request, "ResourceAuthority"),
            "Policy");
        ResealSerializedResourceAndRequest(
            request,
            ComputeSerializedPolicyFingerprint(policy));

        var issues = new List<ValidationIssue>();
        var parsed = MortalWoundTreatmentCommandCodec.TryParseRequest(
            request,
            "request",
            issues,
            out _);

        Assert.False(parsed);
        Assert.Contains(issues, static issue =>
            string.Equals(
                issue.Code,
                "mortal_wound_treatment_persisted_request_seal_mismatch",
                StringComparison.Ordinal) &&
            Convert.ToString(issue.Actual)?.Contains(
                "mode_authority.course_window",
                StringComparison.Ordinal) == true);
    }

    [Fact]
    public void DetachedRequirementScope_RejectsNestedWitnessesFromAnotherScope()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_foreign_nested_scope",
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var common = Assert.Single(request.RequirementAuthority.Scopes);
        var foreignSuccess = MortalWoundTreatmentRequirementScopeAuthority.Create(
            "course_milestone",
            1,
            "Satisfied",
            common.Bindings,
            Array.Empty<MortalWoundTreatmentRequirementFailureWitness>());
        var foreignFailure = MortalWoundTreatmentRequirementScopeAuthority.Create(
            "course_milestone",
            1,
            "Unsatisfied",
            Array.Empty<MortalWoundTreatmentRequirementBinding>(),
            new[]
            {
                MortalWoundTreatmentRequirementFailureWitness.CreateAbsent(
                    "common",
                    0,
                    "item_quantity",
                    "foreign_missing_item")
            });
        var validate = ExactStaticMethod(
            typeof(MortalWoundTreatmentDetachedSealValidator),
            "HasValidRequirementScope",
            1);

        Assert.False(Assert.IsType<bool>(Invoke(validate, new object[]
        {
            foreignSuccess
        })));
        Assert.False(Assert.IsType<bool>(Invoke(validate, new object[]
        {
            foreignFailure
        })));
    }

    [Fact]
    public void GuaranteedSelfTreatment_PersistedEvidenceKeepsTheAuthoredTargetRole()
    {
        var scenario = CreateScenario(
            "guaranteed_current_capability_proof_stabilizes",
            "guaranteed");
        var before = WoundContractTestData.CreateActiveWound(
            woundId: scenario.Before["woundId"]!.GetValue<string>(),
            ownerKind: "npc",
            ownerId: "field_medic_01",
            carrierPath: WoundCarrierCatalog.NpcPath);
        before["treatment"] = scenario.Before["treatment"]!.DeepClone();
        var route = before["treatment"]!["routes"]![0]!.AsObject();
        route["requirements"]![0]!["actorRole"] = "target";
        route["resolution"]!["actorRole"] = "target";
        var accepted = scenario.AcceptedState.DeepClone().AsObject();
        accepted["targetKind"] = "npc";
        accepted["targetId"] = "field_medic_01";
        scenario = scenario with
        {
            Before = before,
            AcceptedState = accepted,
            OperationKey = scenario.OperationKey + "_self_target"
        };
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "guaranteed",
            scenario.OperationKey,
            scenario.RouteId);

        var command = ComposeTreatmentCommand(
            flow,
            "The healer applies the target-owned guarantee to their own wound.");

        Assert.True(command.Parsed.Success, DescribeIssues(command.Parsed.Issues));
    }

    [Fact]
    public void PersistedProcedureEvidence_RecomputesTheExactCriticalReactionIntent()
    {
        var scenario = CreateScenario(
            "procedure_player_natural_one_reserves_oldest_fate_shield",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_forged_reaction_intent",
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var authority = Assert.IsType<MortalWoundProcedureCheckAuthority>(
            request.ModeAuthority);
        var evidence = Assert.IsType<MortalWoundProcedureModeEvidence>(
            resolution.ModeEvidence);
        var source = Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(evidence));
        const string forgedReaction =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        source[FindJsonPropertyName(source, "ReactionFingerprint")] = forgedReaction;
        source[FindJsonPropertyName(source, "AcceptedRollFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.procedure_evidence",
                "1",
                authority.AuthorityFingerprint,
                evidence.Total.ToString(System.Globalization.CultureInfo.InvariantCulture),
                evidence.BaseDifficulty.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                evidence.Margin.ToString(System.Globalization.CultureInfo.InvariantCulture),
                evidence.OriginalOutcome,
                evidence.ResolvedOutcome,
                evidence.SelectedBandId,
                evidence.SelectedOutcomeIndex.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                forgedReaction
            });
        var issues = new List<ValidationIssue>();
        var arguments = new object?[]
        {
            source,
            request,
            resolution.SelectedOutcomeIndex,
            "result.modeEvidence",
            issues,
            null,
            null,
            null
        };
        var validate = ExactStaticMethod(
            typeof(MortalWoundTreatmentCommandCodec),
            "TryValidateModeEvidence",
            arguments.Length);

        var valid = Assert.IsType<bool>(validate.Invoke(null, arguments));

        Assert.False(valid);
        Assert.Contains(issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_persisted_mode_evidence_mismatch",
            StringComparison.Ordinal));
    }

    [Fact]
    public void PersistedCourseEvidence_DerivesDispositionFromTheSealedMilestone()
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_forged_course_disposition",
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var authority = Assert.IsType<MortalWoundCourseModeAuthority>(
            request.ModeAuthority);
        var evidence = Assert.IsType<MortalWoundCourseModeEvidence>(
            resolution.ModeEvidence);
        Assert.Equal("active", evidence.CourseDisposition);
        var source = Assert.IsType<JsonObject>(WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(evidence));
        const string forgedDisposition = "completed";
        source[FindJsonPropertyName(source, "CourseDisposition")] = forgedDisposition;
        source[FindJsonPropertyName(source, "ClockEvidenceFingerprint")] =
            WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.course_evidence",
                "1",
                authority.AuthorityFingerprint,
                authority.CourseId,
                authority.MilestoneOrdinal.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                authority.CourseStartAuthority.StartedAtGameTimeMinutes.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                authority.GameTimeAuthority.CurrentTimeInMinutes.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                authority.WindowDisposition,
                forgedDisposition
            });
        var issues = new List<ValidationIssue>();
        var arguments = new object?[]
        {
            source,
            request,
            resolution.SelectedOutcomeIndex,
            "result.modeEvidence",
            issues,
            null,
            null,
            null
        };
        var validate = ExactStaticMethod(
            typeof(MortalWoundTreatmentCommandCodec),
            "TryValidateModeEvidence",
            arguments.Length);

        var valid = Assert.IsType<bool>(validate.Invoke(null, arguments));

        Assert.False(valid);
        Assert.Contains(issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_persisted_mode_evidence_mismatch",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("result_category")]
    [InlineData("selected_outcome")]
    [InlineData("interruption")]
    [InlineData("consumption_trigger")]
    [InlineData("course_disposition")]
    [InlineData("route_fingerprint")]
    [InlineData("route_completion")]
    public void PersistedCourseResult_DerivesAllActionableFieldsFromTheSealedRequest(
        string axis)
    {
        var scenario = CreateScenario(
            "course_first_milestone_is_ready_at_inclusive_due_time",
            "course");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "course",
            scenario.OperationKey + "_forged_course_result_" + axis,
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var resultCategory = resolution.ResultCategory;
        var selectedOutcomeIndex = resolution.SelectedOutcomeIndex;
        var interruption = resolution.Interruption;
        var consumptionTrigger = resolution.ConsumptionTrigger;
        var courseDisposition = resolution.CourseDisposition;
        var routeFingerprint = resolution.RouteFingerprint;
        var routeCompletion = resolution.RouteCompletion;
        switch (axis)
        {
            case "result_category":
                resultCategory = "failed_attempt";
                break;
            case "selected_outcome":
                selectedOutcomeIndex = null;
                break;
            case "interruption":
                interruption = true;
                break;
            case "consumption_trigger":
                consumptionTrigger = "none";
                break;
            case "course_disposition":
                courseDisposition = "completed";
                break;
            case "route_fingerprint":
                routeFingerprint =
                    "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                break;
            case "route_completion":
                routeCompletion = "AppendOnce";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }
        var validate = ExactStaticMethod(
            typeof(MortalWoundTreatmentCommandCodec),
            "HasMatchingResultSemantics",
            8);
        var originalValid = Assert.IsType<bool>(Invoke(validate, new object?[]
        {
            request,
            resolution.ResultCategory,
            resolution.SelectedOutcomeIndex,
            resolution.Interruption,
            resolution.ConsumptionTrigger,
            resolution.CourseDisposition,
            resolution.RouteFingerprint,
            resolution.RouteCompletion
        }));
        var forgedValid = Assert.IsType<bool>(Invoke(validate, new object?[]
        {
            request,
            resultCategory,
            selectedOutcomeIndex,
            interruption,
            consumptionTrigger,
            courseDisposition,
            routeFingerprint,
            routeCompletion
        }));

        Assert.True(originalValid);
        Assert.False(forgedValid);
    }

    [Theory]
    [InlineData("procedure", "procedure_normal_uses_lowest_free_die")]
    [InlineData("course", "course_first_milestone_is_ready_at_inclusive_due_time")]
    [InlineData("guaranteed", "guaranteed_current_capability_proof_stabilizes")]
    public void PersistedRequest_CarriesTheCanonicalRouteSourceForDetachedResultVerification(
        string mode,
        string scenarioName)
    {
        var scenario = CreateScenario(scenarioName, mode);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            mode,
            scenario.OperationKey + "_route_source_" + mode,
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var routeSourceWound = Assert.IsType<WoundMaterializationEnvelope>(
            ReadRequiredProperty(request, "RouteSourceWound"));
        var routeSourceFingerprint = Assert.IsType<string>(
            ReadRequiredProperty(request, "RouteSourceWoundFingerprint"));

        Assert.Equal(request.Coordinates.WoundId, routeSourceWound.WoundId);
        Assert.Equal(
            request.Coordinates.ExpectedBeforeFingerprint,
            routeSourceFingerprint);
        Assert.Equal(
            routeSourceFingerprint,
            WoundIdentityState.ComputeSemanticFingerprint(routeSourceWound));
        Assert.Equal(
            request.RequirementAuthority.RouteFingerprint,
            MortalWoundTreatmentRouteFingerprint.Compute(
                routeSourceWound,
                request.Coordinates.RouteId));
    }

    [Theory]
    [InlineData("procedure", "procedure_normal_uses_lowest_free_die")]
    [InlineData("course", "course_first_milestone_is_ready_at_inclusive_due_time")]
    [InlineData("guaranteed", "guaranteed_current_capability_proof_stabilizes")]
    public void PersistedResult_DeclaredOperationsMustExactlyMatchTheSealedRouteSelection(
        string mode,
        string scenarioName)
    {
        var scenario = CreateScenario(scenarioName, mode);
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            mode,
            scenario.OperationKey + "_declared_result_" + mode,
            scenario.RouteId);
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(flow.Request);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(flow.Resolution);
        var forged = resolution.DeclaredResult
            .Concat(new MortalWoundTreatmentOperation[]
            {
                new MortalWoundNoImprovementOperation()
            })
            .ToArray();
        var validate = ExactStaticMethod(
            typeof(MortalWoundTreatmentCommandCodec),
            "HasMatchingDeclaredResult",
            2);

        Assert.True(Assert.IsType<bool>(Invoke(validate, new object[]
        {
            request,
            resolution.DeclaredResult
        })));
        Assert.False(Assert.IsType<bool>(Invoke(validate, new object[]
        {
            request,
            forged
        })));
    }

    [Fact]
    public void PersistedPending_RejectsSubmittedTreatmentWithoutARepairWave()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_forged_empty_repair_wave",
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "No repair wave may retain a private treatment request.");
        var pending = Assert.IsType<JsonObject>(Invoke(
            ExactStaticMethod(typeof(WoundRepairPacketBuilder), "ComposePendingRoot", 3),
            new object?[]
            {
                command.Binding,
                Array.Empty<WoundRepairPacket>(),
                command.Parsed
            }));
        var request = ReadCommandTreatmentRequest(command.Root);
        var coordinates = ReadJsonObject(request, "Coordinates");
        pending["submittedTreatmentRequests"] = new JsonArray(new JsonObject
        {
            ["operationKey"] = ReadJsonString(coordinates, "OperationKey"),
            ["attemptId"] = ReadJsonString(coordinates, "AttemptId"),
            ["requestFingerprint"] = ReadJsonString(request, "RequestFingerprint"),
            ["request"] = request.DeepClone()
        });

        AssertInvalidPersistedCatalog(
            ParsePersistedRequestCatalog(null, pending, flow.History),
            "submitted treatment without repair wave");
    }

    [Fact]
    public void ResourceFinalization_PersistedCommandConfirmsAndRollbackReleaseAllowsExactRetry()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        scenario.AcceptedState["sterileThreadCount"] = 2;
        using var fixture = AcceptedStateFixture.Create(scenario);

        var persisted = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_confirmed_persistence",
            scenario.RouteId);
        var persistedCommand = ComposeTreatmentCommand(
            persisted,
            "The complete command confirms its treatment resource hold.");
        var persistedRequests = ParseTreatmentRequests(persistedCommand);

        var modified = PersistTreatmentCommand(fixture, persistedCommand);

        Assert.Contains(AcceptedMechanicsPlan.WoundCommandPath, modified);
        var repeatedConfirmation = ConfirmTreatmentResources(
            fixture,
            persistedRequests);
        Assert.True(repeatedConfirmation.IsValid,
            DescribeIssues(repeatedConfirmation.Issues));
        Assert.Equal(0, repeatedConfirmation.ChangedCount);

        var rolledBack = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_rolled_back_persistence",
            scenario.RouteId);
        var rolledBackCommand = ComposeTreatmentCommand(
            rolledBack,
            "This command is restored to its exact before-image.");

        FailTreatmentCommandAfterPhysicalWrite(fixture, rolledBackCommand);

        var exactRetry = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_rolled_back_persistence",
            scenario.RouteId);
        Assert.Equal(CanonicalValue(rolledBack.Request), CanonicalValue(exactRetry.Request));
        Assert.Equal(
            CanonicalValue(rolledBack.Resolution),
            CanonicalValue(exactRetry.Resolution));
    }

    [Fact]
    public async Task ResourceFinalization_RollbackFailureRetainsHoldAndSurfacesAggregateFailure()
    {
        var scenario = CreateScenario(
            "procedure_normal_uses_lowest_free_die",
            "procedure");
        scenario.AcceptedState["sterileThreadCount"] = 1;
        using var fixture = AcceptedStateFixture.Create(scenario);
        var flow = ResolveCurrentTreatment(
            fixture,
            "procedure",
            scenario.OperationKey + "_rollback_failure",
            scenario.RouteId);
        var command = ComposeTreatmentCommand(
            flow,
            "A failed physical rollback must retain the exact resource hold.");
        var hooks = new StateDistributorHooks
        {
            AfterFileMutationAppliedAsync = path => string.Equals(
                    path,
                    AcceptedMechanicsPlan.WoundCommandPath,
                    StringComparison.Ordinal)
                ? Task.FromException(new IOException(
                    "Injected failure after the accepted treatment command write."))
                : Task.CompletedTask,
            BeforeFileMutationRollback = path =>
            {
                if (string.Equals(
                        path,
                        AcceptedMechanicsPlan.WoundCommandPath,
                        StringComparison.Ordinal))
                {
                    throw new IOException(
                        "Injected failure before treatment command rollback.");
                }
            }
        };

        fixture.ReleaseLeaseForExternalDistribution();
        AggregateException failure;
        try
        {
            failure = await Assert.ThrowsAsync<AggregateException>(() =>
                    new StateDistributor(
                            fixture.FileSystem,
                            NullLogger<StateDistributor>.Instance,
                            hooks)
                        .DistributeAsync(
                            new GameResponse { Response = command.FinalSceneText },
                            command.Recomposed));
        }
        finally
        {
            fixture.ReacquireLeaseAfterExternalDistribution();
        }

        Assert.Contains(failure.InnerExceptions, exception => exception.Message.Contains(
            "after the accepted treatment command write",
            StringComparison.Ordinal));
        Assert.Contains(failure.InnerExceptions, exception => exception.Message.Contains(
            "before treatment command rollback",
            StringComparison.Ordinal));
        Assert.True(File.Exists(fixture.FileSystem.ResolvePath(
            AcceptedMechanicsPlan.WoundCommandPath)));

        var acceptedState = fixture.GetAcceptedState();
        var competing = MortalWoundTreatmentPlanner.PrepareProcedureRequest(
            Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(acceptedState),
            fixture.ReadCurrentHistory(),
            fixture.ReadCurrentWound(),
            scenario.OperationKey + "_rollback_failure_competing",
            scenario.RouteId,
            fixture.AcceptedEventRef(acceptedState));
        Assert.False(competing.IsValid);
        Assert.Contains(competing.Issues, static issue => string.Equals(
            issue.Code,
            "mortal_wound_treatment_resource_reservation_overbooked",
            StringComparison.Ordinal));
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

    private static IReadOnlyList<MortalWoundTreatmentAttemptRequest>
        ParseTreatmentRequests(TreatmentPersistenceCommand command)
    {
        var issues = new List<ValidationIssue>();
        var requests = command.Parsed.TreatmentCommands.Select(draft =>
        {
            Assert.True(MortalWoundTreatmentCommandCodec.TryParseRequest(
                draft.Request,
                AcceptedMechanicsPlan.WoundCommandPath + ".authority.request",
                issues,
                out var request));
            return Assert.IsType<MortalWoundTreatmentAttemptRequest>(request);
        }).ToArray();
        Assert.Empty(issues);
        return requests;
    }

    private static MortalWoundTreatmentResourceLifecycleResult ConfirmTreatmentResources(
        AcceptedStateFixture fixture,
        IReadOnlyList<MortalWoundTreatmentAttemptRequest> requests)
    {
        return MortalWoundTreatmentResourceComposer
            .ConfirmPersistedTreatmentResources(
                fixture.FileSystem,
                fixture.Lease,
                requests);
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
        Assert.True(
            string.Equals(
                "Resolved",
                Convert.ToString(ReadRequiredProperty(result, "Disposition")),
                StringComparison.Ordinal),
            DescribeIssues(AsObjects(ReadRequiredProperty(result, "Issues"))
                .Select(Assert.IsType<ValidationIssue>)));
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
        return Assert.Single(AssertValidPersistedCatalog(
            RestoreCurrentPersistedTreatmentCatalog(fixture),
            boundary));
    }

    private static void CopyDirectoryTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories)
                 .Where(path => IsColdRootCopyPath(source, path)))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }
        foreach (var file in Directory.EnumerateFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories)
                 .Where(path => IsColdRootCopyPath(source, path)))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    private static bool IsColdRootCopyPath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return !relative.StartsWith(".boe_runtime", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(relative, ".boe_runtime", StringComparison.OrdinalIgnoreCase) ||
               relative.StartsWith(
                   ".boe_runtime/session-generation",
                   StringComparison.OrdinalIgnoreCase);
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
        var expectedNode = WoundResponseInputComposer
            .SerializeMortalWoundTreatmentValue(expected);
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
        var issues = AsObjects(ReadRequiredProperty(result, "Issues"))
            .Select(Assert.IsType<ValidationIssue>)
            .ToArray();
        Assert.True(
            Assert.IsType<bool>(ReadRequiredProperty(result, "IsValid")),
            boundary + " was rejected by the persisted-request catalog: " +
            DescribeIssues(issues));
        Assert.Empty(issues);
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

    private static string? ReadOptionalJsonString(
        JsonObject value,
        string propertyName)
    {
        var node = value[FindJsonPropertyName(value, propertyName)];
        return node is null ? null : node.GetValue<string>();
    }

    private static int? ReadOptionalJsonInt32(
        JsonObject value,
        string propertyName)
    {
        var node = value[FindJsonPropertyName(value, propertyName)];
        return node is null ? null : node.GetValue<int>();
    }

    private static void ResealSerializedResourceAndRequest(
        JsonObject request,
        string policyFingerprint)
    {
        var resource = ReadJsonObject(request, "ResourceAuthority");
        var claims = Assert.IsType<JsonArray>(resource[
            FindJsonPropertyName(resource, "Claims")]);
        var disposition = ReadJsonString(resource, "ReservationDisposition");
        string ComputeResourceFingerprint(string? reservationId)
        {
            var fields = new List<string?>
            {
                "book_of_eternity.mortal_wound_treatment.resource_reservation_authority",
                "1",
                disposition,
                reservationId,
                ReadJsonString(resource, "CoordinatesFingerprint"),
                ReadJsonString(resource, "AcceptedStateFingerprint"),
                ReadJsonString(resource, "RouteFingerprint"),
                ReadOptionalJsonString(resource, "CourseId"),
                ReadOptionalJsonInt32(resource, "CourseMilestoneOrdinal")
                    ?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ReadOptionalJsonString(resource, "CourseCoordinateFingerprint"),
                ReadJsonString(resource, "RequirementAuthorityFingerprint"),
                policyFingerprint,
                claims.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            fields.AddRange(claims.OfType<JsonObject>().Select(claim =>
                ReadJsonString(claim, "ClaimFingerprint")));
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }

        string? reservationId = null;
        if (string.Equals(disposition, "held", StringComparison.Ordinal))
        {
            var semantic = ComputeResourceFingerprint(null);
            reservationId = "wound_treatment_resource_reservation_" +
                            semantic["sha256:".Length..];
        }
        resource[FindJsonPropertyName(resource, "ReservationId")] = reservationId;
        var resourceFingerprint = ComputeResourceFingerprint(reservationId);
        resource[FindJsonPropertyName(resource, "AuthorityFingerprint")] =
            resourceFingerprint;

        var mode = ReadJsonString(request, "Mode");
        var coordinates = ReadJsonObject(request, "Coordinates");
        var modeAuthority = ReadJsonObject(request, "ModeAuthority");
        var requirement = ReadJsonObject(request, "RequirementAuthority");
        var modeFingerprint = string.Equals(mode, "guaranteed", StringComparison.Ordinal)
            ? ReadJsonString(modeAuthority, "ProofFingerprint")
            : ReadJsonString(modeAuthority, "AuthorityFingerprint");
        var requestFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.attempt_request",
                "1",
                mode,
                ReadJsonString(coordinates, "CoordinatesFingerprint"),
                ReadOptionalJsonInt32(request, "MilestoneOrdinal")
                    ?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ReadJsonString(request, "RouteSourceWoundFingerprint"),
                modeFingerprint,
                ReadJsonString(requirement, "AuthorityFingerprint"),
                resourceFingerprint
            });
        request[FindJsonPropertyName(request, "RequestFingerprint")] =
            requestFingerprint;
    }

    private static string ComputeSerializedPolicyFingerprint(JsonObject policy)
    {
        var consumeOn = Assert.IsType<JsonArray>(policy[
            FindJsonPropertyName(policy, "ConsumeOn")]);
        var refundOn = Assert.IsType<JsonArray>(policy[
            FindJsonPropertyName(policy, "RefundOn")]);
        var mutations = Assert.IsType<JsonArray>(policy[
            FindJsonPropertyName(policy, "Mutations")]);
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound_treatment.resource_policy",
            "1",
            policy[FindJsonPropertyName(policy, "ReserveBeforeResolution")]!
                .GetValue<bool>()
                ? "true"
                : "false",
            consumeOn.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        fields.AddRange(consumeOn.Select(static value => value!.GetValue<string>()));
        fields.Add(refundOn.Count.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        fields.AddRange(refundOn.Select(static value => value!.GetValue<string>()));
        fields.Add(mutations.Count.ToString(
            System.Globalization.CultureInfo.InvariantCulture));
        foreach (var mutation in mutations.OfType<JsonObject>())
        {
            fields.Add(ReadJsonString(mutation, "Kind"));
            fields.Add(ReadJsonString(mutation, "Scope"));
            fields.Add(ReadOptionalJsonInt32(mutation, "MilestoneOrdinal")
                ?.ToString(System.Globalization.CultureInfo.InvariantCulture));
            fields.Add(ReadOptionalJsonInt32(mutation, "RequirementIndex")
                ?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string ComputeSerializedCourseAuthorityFingerprint(
        JsonObject authority)
    {
        var gameTime = ReadJsonObject(authority, "GameTimeAuthority");
        var start = ReadJsonObject(authority, "CourseStartAuthority");
        return WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.course_mode_authority",
            "1",
            ReadJsonString(gameTime, "AuthorityFingerprint"),
            ReadJsonString(authority, "CourseId"),
            ReadOptionalJsonInt32(authority, "MilestoneOrdinal")
                ?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            authority[FindJsonPropertyName(authority, "DueAtGameTimeMinutes")]!
                .GetValue<long>()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            authority[FindJsonPropertyName(authority, "DeadlineAtGameTimeMinutes")]!
                .GetValue<long>()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            ReadJsonString(authority, "WindowDisposition"),
            ReadJsonString(start, "AuthorityFingerprint"),
            ReadJsonString(authority, "CourseCoordinateFingerprint"),
            ReadJsonString(authority, "CoordinatesFingerprint"),
            ReadJsonString(authority, "AcceptedStateFingerprint")
        });
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
