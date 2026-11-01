using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData("procedure", false)]
    [InlineData("course", false)]
    [InlineData("guaranteed", false)]
    public Task LegalTerminalHealReachesActualTreatmentPublication(string mode, bool removeThenHeal) =>
        AssertLegalTerminalPublicationAsync(mode, removeThenHeal);

    [Fact]
    public Task IndependentTerminalLegacyUsesActualTreatmentPublication() =>
        AssertLegalTerminalPublicationAsync("guaranteed", removeThenHeal: true);

    private async Task AssertLegalTerminalPublicationAsync(string mode, bool removeThenHeal)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, physicalMode: mode,
            preparePhysicalWound: wound => PrepareTerminalPhysicalWound(wound, mode, removeThenHeal));
        var files = fixture.Files;
        Assert.True(fixture.Authority.HasCurrentAdmissionAuthority());
        var before = fixture.Authority.CurrentWound;
        Assert.Equal("active", before.Lifecycle); Assert.Equal(1, before.Severity.Rank);
        Assert.NotEmpty(before.Consequences.OwnedEffectSources.RootBindings);
        var parsed = WoundMaterializationContract.Parse(WoundMaterializationContract.SerializeCanonical(before), "terminal.initial");
        Assert.True(parsed.IsValid, DescribeIssues(parsed.Issues));
        var input = fixture.Authority.ReadGuidedTreatmentOriginalInput();
        var frame = MortalWoundGuidedTreatmentInputCodec.Read(input).Frame;
        var admitted = await WoundApplicationService.ResolveMortalTreatmentInputAsync(files, fixture.Lease, frame);
        var request = admitted.Request;
        Assert.Equal(mode, request.Mode); Assert.Equal("treatment", request.Coordinates.Purpose);
        Assert.Equal(mode == "course" ? 1 : (int?)null, request.MilestoneOrdinal);
        Assert.True(MortalWoundTreatmentDetachedSealValidator.IsValid(request));
        if (request.ModeAuthority is MortalWoundProcedureCheckAuthority check)
        {
            Assert.Equal(new[] { 20 }, check.SourceRolls);
            Assert.True(admitted.Owner.HasLiveProcedureReservationAgreement(check));
        }
        var history = new WoundHistoryParseResult(admitted.Owner.History, []);
        var reduced = mode switch
        {
            "procedure" => MortalWoundTreatmentPlanner.CreateProcedureAttempt(request, history, admitted.Owner.CurrentWound, admitted.Owner),
            "course" => MortalWoundTreatmentPlanner.CreateCourseMilestoneAttempt(request, history, admitted.Owner.CurrentWound, admitted.Owner),
            _ => MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, admitted.Owner.CurrentWound, admitted.Owner)
        };
        Assert.Equal("Resolved", reduced.Disposition);
        var resolution = Assert.IsType<MortalWoundTreatmentResolution>(reduced.Resolution);
        Assert.Equal("AcceptedTerminal", resolution.AttemptDisposition); Assert.Equal("success", resolution.ResultCategory);
        Assert.IsType<MortalWoundHealOperation>(resolution.DeclaredResult[^1]);
        Assert.IsType<MortalWoundHealOutcomeIntent>(resolution.OutcomeIntents[^1]);
        Assert.Equal(removeThenHeal ? 2 : 1, resolution.DeclaredResult.Count);
        if (removeThenHeal)
        {
            Assert.IsType<MortalWoundRemoveComplicationOperation>(resolution.DeclaredResult[0]);
            Assert.IsType<MortalWoundRemoveComplicationOutcomeIntent>(resolution.OutcomeIntents[0]);
        }
        if (mode == "course") Assert.Equal("completed", resolution.CourseDisposition);
        var simulation = MortalWoundTreatmentWorkingWoundSimulator.Simulate(before, new[] { resolution.DeclaredResult.ToImmutableArray() });
        Assert.True(simulation.IsApplicable); Assert.True(simulation.Improved);
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var held = fixture.CaptureJson();
        if (removeThenHeal) AssertTerminalLegacyPreparationPrerequisite(fixture, admitted.Owner, request, resolution, held);
        AssertTerminalPersistedResultRoundTrip(fixture, admitted.Owner, request, resolution, held);
        var observedExceptions = new ConcurrentQueue<string>();
        var invocation = new AsyncLocal<object?>(); var marker = new object();
        void Observe(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (ReferenceEquals(invocation.Value, marker) && args.Exception is InvalidOperationException)
                observedExceptions.Enqueue(args.Exception.ToString());
        }
        WoundAcceptedTurnPlanner.MortalWoundTreatmentPublicationResult composed;
        var previous = invocation.Value;
        if (removeThenHeal) AppDomain.CurrentDomain.FirstChanceException += Observe;
        try
        {
            if (removeThenHeal) invocation.Value = marker;
            composed = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(files, fixture.Lease,
                new GameResponse(), admitted.Owner, request, resolution);
        }
        finally
        {
            if (removeThenHeal) AppDomain.CurrentDomain.FirstChanceException -= Observe;
            invocation.Value = previous;
        }
        foreach (var exception in observedExceptions) output.WriteLine("REMOVE_PUBLICATION_FIRST_CHANCE: " + exception);
        fixture.AssertJson(held);
        Assert.True(composed.IsValid,
            "TERMINAL_HEAL_CAUSAL: the actual publication rejected a legal, retained terminal heal after native mode/source/claim premises. " +
            DescribeIssues(composed.Issues) + Environment.NewLine + string.Join(Environment.NewLine, observedExceptions));
        AssertTerminalPublicationStages(composed.Plan!, removeThenHeal ? "remove" : "simple");
    }

    private static void AssertTerminalPersistedResultRoundTrip(DiagnosisOwnershipFixture fixture,
        MortalWoundTreatmentAcceptedStateAuthority owner, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution, Dictionary<string, byte[]> captured)
    {
        var receipt = MortalWoundTreatmentReceipt.Create(resolution);
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        Assert.True(finalization.IsValid, DescribeIssues(finalization.Issues));
        var claims = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
            fixture.Files, fixture.Lease, request, finalization.Finalization!);
        Assert.True(claims.IsValid, DescribeIssues(claims.Issues));
        Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, claims.State);
        var serialized = WoundResponseInputComposer.ComposeMortalWoundTreatmentPersistedResult(request, receipt);
        // The canonical route writer is independent of the persistence serializer.
        // Compare the entire ordered payload, including definitions and parameters.
        var canonical = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound))!.AsObject();
        var route = Assert.Single(canonical["treatment"]!["routes"]!.AsArray().OfType<JsonObject>(),
            row => row["routeId"]!.GetValue<string>() == request.Coordinates.RouteId);
        Assert.False(receipt.Interruption);
        var outcomes = route["outcomes"]!.AsArray();
        var expected = request.Mode == "course"
            ? Assert.Single(outcomes.OfType<JsonObject>(), row => row["ordinal"]!.GetValue<int>() == request.MilestoneOrdinal)["result"]
            : outcomes[Assert.IsType<int>(receipt.SelectedOutcomeIndex)]!["result"];
        Assert.True(JsonNode.DeepEquals(expected, serialized["declaredResult"]));
        var persisted = MortalWoundTreatmentPersistedResult.Create(resolution);
        Assert.True(JsonNode.DeepEquals(serialized, persisted.ToCanonicalJson()));
        var issues = new List<ValidationIssue>();
        var reparsed = MortalWoundTreatmentPersistedResult.Parse(JsonSerializer.SerializeToElement(serialized), "codec.result", issues);
        Assert.Empty(issues); Assert.NotNull(reparsed);
        Assert.True(JsonNode.DeepEquals(serialized, reparsed.ToCanonicalJson()));
        Assert.Equal(request.RequestFingerprint, reparsed.Request.RequestFingerprint);
        Assert.Equal(receipt.ReceiptFingerprint, reparsed.Receipt.ReceiptFingerprint);
        Assert.True(JsonNode.DeepEquals(serialized["requestAuthority"], reparsed.SerializeRequest()));
        var heal = serialized["declaredResult"]!.AsArray()[^1]!.AsObject();
        var legacies = heal["legacies"]!.AsArray();
        if (legacies.Count > 0)
        {
            Assert.Equal(new[] { "cosmetic", "mechanical_effect" }, legacies.Select(row => row!["kind"]!.GetValue<string>()));
            void Refuse(string label, Action<JsonArray> fault, bool semantic = false)
            {
                var changed = serialized.DeepClone().AsObject();
                fault(changed["declaredResult"]!.AsArray()[^1]!["legacies"]!.AsArray());
                var rejectedIssues = new List<ValidationIssue>();
                var rejected = MortalWoundTreatmentPersistedResult.Parse(JsonSerializer.SerializeToElement(changed), "codec.result", rejectedIssues);
                Assert.True(rejected is null, label); Assert.NotEmpty(rejectedIssues);
                if (semantic) Assert.Contains(rejectedIssues, issue => issue.Code == "mortal_wound_treatment_persisted_result_semantics_mismatch");
            }
            Refuse("missing legacy reference", rows => rows[0]!.AsObject().Remove("localLegacyRef"));
            Refuse("missing legacy kind", rows => rows[0]!.AsObject().Remove("kind"));
            Refuse("extra legacy field", rows => rows[0]!["foreign"] = true);
            Refuse("unknown legacy kind", rows => rows[0]!["kind"] = "foreign");
            Refuse("cosmetic cannot carry mechanical payload", rows => rows[0]!["effectDraft"] = rows[1]!["effectDraft"]!.DeepClone());
            Refuse("missing mechanical draft", rows => rows[1]!.AsObject().Remove("effectDraft"));
            Refuse("extra mechanical field", rows => rows[1]!["foreign"] = true);
            Refuse("changed full parameters", rows => rows[1]!["effectDraft"]!["applications"]![0]!["parameters"]!["foreign"] = 7, semantic: true);
            Refuse("changed full definition", rows => rows[1]!["effectDraft"]!["definitions"]![0]!["definition"]!["display"]!["description"] = "foreign", semantic: true);
            Refuse("changed legacy order", rows =>
            {
                var first = rows[0]!.DeepClone(); var second = rows[1]!.DeepClone(); rows[0] = second; rows[1] = first;
            }, semantic: true);
            var typedHeal = Assert.IsType<MortalWoundHealOperation>(receipt.DeclaredResult[^1]);
            var cosmetic = Assert.IsType<MortalWoundCosmeticLegacyDraft>(typedHeal.Legacies[0]);
            Assert.Throws<JsonException>(() => WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(
                new MortalWoundHealOperation(typedHeal.Legacies.SetItem(0, cosmetic with { Kind = "mechanical_effect" }))));
        }
        else Assert.Empty(Assert.IsType<MortalWoundHealOperation>(reparsed.Receipt.DeclaredResult[^1]).Legacies);
        Assert.Same(request, owner.ReadHeldGuidedTreatmentRequest(request.Coordinates));
        var after = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
            fixture.Files, fixture.Lease, request, finalization.Finalization!);
        Assert.True(after.IsValid, DescribeIssues(after.Issues)); Assert.Equal(claims.State, after.State);
        fixture.AssertJson(captured);
    }

    private static void AssertTerminalLegacyPreparationPrerequisite(DiagnosisOwnershipFixture fixture,
        MortalWoundTreatmentAcceptedStateAuthority owner, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution, Dictionary<string, byte[]> captured)
    {
        var files = fixture.Files;
        Assert.True(owner.HasCurrentAdmissionAuthority());
        Assert.Same(request, owner.ReadHeldGuidedTreatmentRequest(request.Coordinates));
        var requestBefore = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)!.ToJsonString();
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        Assert.True(finalization.IsValid, DescribeIssues(finalization.Issues));
        var claimBefore = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
            files, fixture.Lease, request, finalization.Finalization!);
        Assert.True(claimBefore.IsValid, DescribeIssues(claimBefore.Issues));
        Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, claimBefore.State);
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(CanonicalJsonUtf8.DecodeOneOptionalBom(
            captured[files.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)]),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        Assert.Equal(owner.Binding.SessionId, manifest.SessionId); Assert.Equal(owner.Binding.RequestId, manifest.RequestId);
        Assert.Equal(owner.Binding.Turn, manifest.TurnNumber); Assert.Equal(owner.Binding.SnapshotToken, manifest.ManifestPayloadHash);
        Assert.Equal(owner.Binding.SnapshotToken, PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest,
            LiveTurnPreparationService.ManifestHashJsonOptions, value => value.ManifestPayloadHash,
            (value, hash) => value.ManifestPayloadHash = hash));
        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath, EffectIdentityState.StatePath, EffectAcceptedTurnInputComposer.WorldTimePath
        };
        var paths = new[]
        {
            WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
            WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
            WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath, EffectCarrierCatalog.EnemiesPath,
            EffectCarrierCatalog.AlliesPath, EffectCarrierCatalog.AfterlifeProfilesPath, EffectCarrierCatalog.SpiritualConflictPath,
            EffectIdentityState.StatePath, EffectAcceptedTurnInputComposer.WorldTimePath
        }.Distinct(StringComparer.Ordinal).ToArray();
        // One-shot parsed fixture input DATA from the actual frozen originals.
        // Decode overlapping wound/effect carriers once; the map owns no authority.
        var original = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (!manifest.Files.TryGetValue(path, out var snapshotPath))
            {
                Assert.DoesNotContain(path, required); Assert.False(manifest.SnapshotFileHashes.ContainsKey(path), path);
                if (manifest.OriginalPathPresenceV1?.TryGetValue(path, out var present) == true) Assert.False(present, path);
                Assert.False(captured.ContainsKey(files.ResolvePath(path)), path);
                original.Add(path, null); continue;
            }
            var bytes = owner.ReadDiagnosisOriginalBytes(path);
            Assert.Equal(bytes, captured[files.ResolvePath(snapshotPath)]);
            Assert.Equal(bytes, captured[files.ResolvePath(path)]);
            original.Add(path, JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject());
        }
        JsonObject? Read(string path, bool mustExist = false)
        {
            var root = original[path];
            if (mustExist) Assert.NotNull(root);
            return root;
        }
        var carriers = new WoundCarrierCatalogInput(Read(WoundCarrierCatalog.PlayerPath, true), Read(WoundCarrierCatalog.NpcPath),
            Read(WoundCarrierCatalog.EnemiesPath), Read(WoundCarrierCatalog.AlliesPath), Read(WoundCarrierCatalog.AfterlifeProfilesPath));
        var woundIdentity = Read(WoundIdentityState.StatePath, true)!; var woundHistory = Read(WoundHistoryState.HistoryPath, true)!;
        var effects = new EffectCarrierCatalogInput(Read(EffectCarrierCatalog.PlayerPath, true), Read(EffectCarrierCatalog.NpcPath),
            Read(EffectCarrierCatalog.EnemiesPath), Read(EffectCarrierCatalog.AlliesPath), Read(EffectCarrierCatalog.AfterlifeProfilesPath),
            Read(EffectCarrierCatalog.SpiritualConflictPath));
        var effectIdentity = Read(EffectIdentityState.StatePath, true)!;
        var catalog = WoundCarrierCatalog.Build(carriers); Assert.Empty(catalog.Issues);
        var identities = WoundIdentityState.Parse(woundIdentity.ToJsonString(), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(woundHistory.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(identities.IsValid, DescribeIssues(identities.Issues)); Assert.True(history.IsValid, DescribeIssues(history.Issues));
        Assert.Empty(history.State!.ValidateAgreement(identities.State!, catalog));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound),
            WoundMaterializationContract.SerializeCanonical(Assert.Single(catalog.Occurrences, row => row.WoundId == request.Coordinates.WoundId).Wound));
        var effectCatalog = EffectCarrierCatalog.Build(effects); Assert.Empty(effectCatalog.Issues);
        var effectIndex = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(effectIdentity), EffectIdentityState.StatePath);
        Assert.Empty(effectIndex.Issues); var agreement = new List<ValidationIssue>();
        ValidationService.ValidateEffectCarrierIndexAgreement(effectCatalog, effectIndex.State!, agreement); Assert.Empty(agreement);
        Assert.Equal(owner.CurrentGameMinute, Read(EffectAcceptedTurnInputComposer.WorldTimePath, true)!["currentTimeInMinutes"]!.GetValue<long>());
        var input = new WoundAcceptedTurnInput(owner.Binding, [], [], carriers, woundIdentity, woundHistory, effects, effectIdentity);
        var outcome = MortalWoundTreatmentOutcomePublicationPlanner.Prepare(owner, request, resolution, owner.CurrentGameMinute);
        Assert.True(outcome.IsValid, DescribeIssues(outcome.Issues));
        var preparation = outcome.Preparation!;
        var materialization = MortalWoundTreatmentSeverityRematerializationPlanner.Prepare(input, preparation,
            request.RequestFingerprint, resolution.ResolutionAuthorityFingerprint, resolution.ResultFingerprint,
            request.Coordinates.AttemptId, request.Coordinates.OperationKey, request.Coordinates.ExpectedBeforeFingerprint);
        Assert.True(materialization.IsValid, DescribeIssues(materialization.Issues));
        var prefix = materialization.Batch is null ? Array.Empty<WoundEffectOperationBatch>() : new[] { materialization.Batch };
        var legacy = MortalWoundHealLegacyPlanner.Prepare(request, resolution, owner.Binding,
            WoundAcceptedTurnFingerprints.ComputeInput(input), owner.CurrentGameMinute, prefix);
        var declared = Assert.IsType<MortalWoundHealOperation>(resolution.DeclaredResult[^1]);
        Assert.Equal(new[] { "cosmetic", "mechanical_effect" }, declared.Legacies.Select(row => row.Kind));
        Assert.Equal(declared.Legacies.Select(row => row.LocalLegacyRef), legacy.LegacyDraftBindings.Select(row => row.LocalLegacyRef));
        var mechanical = Assert.IsType<MortalWoundMechanicalEffectLegacyDraft>(declared.Legacies[1]);
        Assert.Single(mechanical.EffectDraft.Definitions); var application = Assert.Single(mechanical.EffectDraft.Applications);
        Assert.Equal("{}", application.Parameters.GetRawText());
        var batch = Assert.Single(legacy.EffectOperationBatches); var rootApplication = Assert.Single(batch.RootApplications);
        Assert.Equal("wound_legacy", batch.SourceExport.Kind); Assert.False(batch.SourceExport.Materializable);
        Assert.Equal(legacy.LegacyDraftBindings[1].LegacyId, batch.PreparedWoundId);
        Assert.Equal(mechanical.LocalLegacyRef + "/" + application.ApplicationRef, rootApplication.ApplicationRef);
        Assert.Equal(batch.SourceExport.Owner, request.RouteSourceWound.Owner);
        Assert.True(WoundEffectCarrierAdapter.TryCreateTargetKey(request.RouteSourceWound.Owner, out var target));
        Assert.Equal(target, rootApplication.ExpectedTargetKey); Assert.Equal(batch.PreparedWoundId, rootApplication.ExpectedSourceKey.SourceId);
        var definition = Assert.Single(batch.SourceExport.Definitions);
        Assert.Equal("action_control", definition.Definition["components"]![0]!["profile"]!.GetValue<string>());
        Assert.Equal("source_bound", definition.Definition["lifetime"]!["mode"]!.GetValue<string>());
        Assert.True(WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(request.RouteSourceWound.Owner, target,
            definition.Definition, out var coordinate)); Assert.Equal(coordinate, rootApplication.ExpectedCarrierCoordinate);
        MortalWoundHealLegacyPlanner.RequireExactBatches(legacy, legacy.EffectOperationBatches);
        MortalWoundHealLegacyPlanner.RequireDisjointCoordinates(prefix.Concat(legacy.EffectOperationBatches).ToArray(), preparation.ReadTerminalHealIntent());
        Assert.Same(request, owner.ReadHeldGuidedTreatmentRequest(request.Coordinates));
        Assert.Equal(requestBefore, WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)!.ToJsonString());
        var claimAfter = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(
            files, fixture.Lease, request, finalization.Finalization!);
        Assert.True(claimAfter.IsValid, DescribeIssues(claimAfter.Issues)); Assert.Equal(claimBefore.State, claimAfter.State);
        fixture.AssertJson(captured);
    }

    private static void PrepareTerminalPhysicalWound(JsonObject wound, string mode, bool removeThenHeal)
    {
        if (removeThenHeal)
        {
            // Reuse only the existing pure initial DATA builder, never its old reflection resolver.
            var routeId = wound["treatment"]!["routes"]![0]!["routeId"]!.DeepClone();
            var knownRouteIds = wound["treatment"]!["knownRouteIds"]!.DeepClone();
            var existing = CreateOutcomeIntentScenario(GuaranteedHealCase()).Before.DeepClone().AsObject();
            existing["treatment"]!["routes"]![0]!["routeId"] = routeId;
            existing["treatment"]!["knownRouteIds"] = knownRouteIds;
            wound.Clear(); foreach (var pair in existing) wound[pair.Key] = pair.Value?.DeepClone();
            return;
        }
        wound["severity"]!["value"] = "I"; wound["severity"]!["rank"] = 1;
        wound["severity"]!["maximumAtCreation"] = "I";
        wound["consequences"]!["slotBudget"] = 1; wound["consequences"]!["slotsUsed"] = 1;
        var route = wound["treatment"]!["routes"]![0]!.AsObject();
        var heal = new JsonArray(new JsonObject { ["kind"] = "heal", ["legacies"] = new JsonArray() });
        if (mode == "course")
        {
            // A one-milestone course is canonical (1–32); the full UI course remains three milestones.
            route["outcomes"] = new JsonArray(CourseMilestone(1, 0, "completed", heal));
            route["resourcePolicy"]!["mutations"] = new JsonArray(CourseMutation(1));
        }
        else route["outcomes"]![0]!["result"] = heal;
    }
}
