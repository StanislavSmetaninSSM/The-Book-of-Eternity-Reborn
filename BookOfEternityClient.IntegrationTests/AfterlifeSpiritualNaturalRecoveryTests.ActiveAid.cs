using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Fact]
    public async Task ActiveAidRequiresTheCommittedGrantAndRetainsOneAttemptThroughPublicationAndReplay()
    {
        await using var fixture = await HealingFixture.CreateAsync(3, 3, 14, stageRequest: false,
            beforeResourceBootstrap: SeedAidGuardianAsync);
        var selection = await CommitAidGrantAsync(fixture);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(1, original.Recovery.CurrentStepProgress); // Actual grant-turn natural tick changed the selected FP.
        var files = fixture.Files;
        var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        // A retained record on another visible profile is not the current scope.
        // The closed DATA shape is valid; this is a menu filter control, not a
        // claimed second accepted grant producer.
        var menuProfiles = (await files.ReadFileBytesAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!;
        var menuRoot = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(menuProfiles))!.AsObject();
        var foreign = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guardian_previous_aid", "Chaos Sea", 42);
        foreign[SpiritualTreatmentParticipation.RecordProperty] = menuRoot["profiles"]!.AsArray().OfType<JsonObject>()
            .Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AidGuardianId)[SpiritualTreatmentParticipation.RecordProperty]!.DeepClone();
        foreign[SpiritualTreatmentParticipation.RecordProperty]!["targetId"] = "guardian_previous_aid";
        SpiritualTreatmentParticipation.ValidateRecord(foreign[SpiritualTreatmentParticipation.RecordProperty]!.AsObject());
        menuRoot["profiles"]!.AsArray().Insert(0, foreign);
        await files.WriteFileAtomicAsync(WoundCarrierCatalog.AfterlifeProfilesPath, menuRoot.ToJsonString());
        var menu = await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true);
        Assert.Equal(selection, Assert.Single(menu.WoundCommandState!.Choices, c => c.Available).Selection);
        await files.WriteFileAtomicBytesAsync(WoundCarrierCatalog.AfterlifeProfilesPath, menuProfiles);
        var draws = 0;
        int[] Draw(int count) { draws++; Assert.Equal(1, count); return [14]; }
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var before = fixture.CaptureMutationRoots([LiveTurnPreparationService.TurnRequestPath]);
            foreach (var (session, turn) in new[] { ("foreign_session", 44), ("session_spiritual_healing", 43), ("session_spiritual_healing", 45) })
            {
                var denied = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                    session, "request_aid", turn, selection, Draw);
                Assert.False(denied.Stage.IsStaged);
                Assert.Null(denied.Control);
                Assert.Equal(0, draws);
                fixture.AssertMutationRootsUnchanged(before);
            }
            // A present competing request is not treated as the normal cleaned absence.
            await files.WriteFileAtomicAsync(lease, LiveTurnPreparationService.TurnRequestPath,
                new JsonObject { ["sessionId"] = "foreign_session", ["turnNumber"] = 43 }.ToJsonString());
            var competing = fixture.CaptureMutationRoots([LiveTurnPreparationService.TurnRequestPath]);
            Assert.False((await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw)).Stage.IsStaged);
            Assert.Equal(0, draws);
            fixture.AssertMutationRootsUnchanged(competing);
            files.DeleteFile(lease, LiveTurnPreparationService.TurnRequestPath);
            var profileBytes = (await files.ReadFileBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!;
            foreach (var refusal in new[] { "missing", "revoked", "incapable", "copied_content" })
            {
                var root = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(profileBytes))!.AsObject();
                var target = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AidGuardianId);
                switch (refusal)
                {
                    case "missing": target.Remove(SpiritualTreatmentParticipation.RecordProperty); break;
                    case "revoked": target[SpiritualTreatmentParticipation.RecordProperty]!["status"] = "revoked"; break;
                    case "incapable": target[SpiritualTreatmentParticipation.RecordProperty]!["capable"] = false; break;
                    default: target[SpiritualTreatmentParticipation.RecordProperty]!["readableEvidence"] = "Иное содержание при том же старом tuple."; break;
                }
                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, root.ToJsonString());
                var unchanged = fixture.CaptureMutationRoots();
                Assert.False((await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                    "session_spiritual_healing", "request_aid", 44, selection, Draw)).Stage.IsStaged);
                Assert.Equal(0, draws);
                fixture.AssertMutationRootsUnchanged(unchanged);
                await files.WriteFileAtomicBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profileBytes);
            }
        }
        BookOfEternityClient.Models.ProgressionControl control;
        JsonObject request;
        JsonObject projection;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var staged = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw);
            Assert.True(staged.Stage.IsStaged, staged.Stage.IssueCode);
            control = Assert.IsType<BookOfEternityClient.Models.ProgressionControl>(staged.Control);
            Assert.Equal(1, draws);
            projection = Assert.IsType<JsonObject>(staged.Stage.GmContext);
            request = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
            Assert.Equal("aid", request["mode"]!.GetValue<string>());
            Assert.Equal(3, request["activeSource"]!["healingTier"]!.GetValue<int>());
            var nullRecord = request.DeepClone().AsObject();
            nullRecord["participation"]!["record"]!["capable"] = null;
            Assert.False(SpiritualSelfTreatmentRequestCodec.IsBoundAid(nullRecord,
                new("session_spiritual_healing", "request_aid", 44, control), selection, "chaos_sea", control.NextChaosSeaTurnOrdinal));
            var retained = fixture.CaptureMutationRoots();
            var repeated = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw);
            Assert.False(repeated.Stage.IsStaged);
            Assert.Null(repeated.Control);
            Assert.Equal(1, draws);
            fixture.AssertMutationRootsUnchanged(retained); // No pending deletion or second control/draw.
        }
        await new LiveTurnPreparationService(files).PrepareAsync(new()
        {
            SessionId = "session_spiritual_healing", RequestId = "request_aid", TurnNumber = 44,
            CurrentRealm = "Chaos Sea", PlayerAction = WoundApplicationService.AidActionText, ProgressionControl = control
        });
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            await WriteAidReportAsync(files, lease, control);
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            var missingGraphs = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.Contains(missingGraphs, i => i.Actual?.Contains("GM-authored spiritual destination graphs are required", StringComparison.Ordinal) == true);
            Assert.True(JsonNode.DeepEquals(request, JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)));
            var manifest = await fixture.ReadManifestAsync(lease);
            await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", new JsonObject
            {
                ["dialogueOptions"] = new JsonArray(), ["timestamp"] = DateTime.UtcNow.ToString("o"),
                ["spiritualHealingGraphs"] = AuthorNaturalGraphs(original, projection["destinationGraphs"]!.AsArray())
            }.ToJsonString());
            var localPath = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
            var locality = (await files.ReadFileBytesAsync(lease, localPath))!;
            var moved = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(locality))!.AsObject();
            moved["chaosSeaNavigation"]!["currentAbodeId"] = "different_abode";
            await files.WriteFileAtomicAsync(lease, localPath, moved.ToJsonString());
            var before = fixture.CaptureMutationRoots([localPath]);
            Assert.Contains(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease), i => i.Severity == IssueSeverity.Error);
            fixture.AssertMutationRootsUnchanged(before);
            await files.WriteFileAtomicBytesAsync(lease, localPath, locality);
            var consentBefore = (await files.ReadFileBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!;
            var revoked = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(consentBefore))!.AsObject();
            revoked["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AidGuardianId)
                [SpiritualTreatmentParticipation.RecordProperty]!["status"] = "revoked";
            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, revoked.ToJsonString());
            before = fixture.CaptureMutationRoots();
            Assert.Contains(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease), i => i.Severity == IssueSeverity.Error);
            fixture.AssertMutationRootsUnchanged(before);
            Assert.True(JsonNode.DeepEquals(request, JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)));
            Assert.Equal(1, draws); // Post-draw revocation refuses without reroll or stale prepared fallback.
            await files.WriteFileAtomicBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, consentBefore);
            var issues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.True(!issues.Any(i => i.Severity == IssueSeverity.Error), string.Join("\n", issues.Select(i => i.Code + ":" + i.Actual)));
            Assert.NotNull(await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
            Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(control,
                manifest.SessionId, manifest.RequestId, 44, p => files.ReadFileBytesAsync(lease, p), manifest.ManifestPayloadHash, request));
        }
        var final = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(2, final.Severity.Rank);
        Assert.Equal(2, final.Recovery.CurrentStepProgress); // III1 → activeH3 II1 → naturalH0 II2.
        var history = await fixture.ReadHistoryAsync();
        var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
        Assert.Equal("aid", result.Mode);
        Assert.Equal(3, result.SchemaVersion);
        Assert.True(JsonNode.DeepEquals(request, result.ToCanonicalJson()["request"]));
        var missingGrant = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history))!.AsObject();
        var grantRow = missingGrant["transitions"]!.AsArray().OfType<JsonObject>()
            .Single(r => r["transitionResult"]?["mode"]?.GetValue<string>() == "natural");
        grantRow.Remove("transitionResult"); // Retain every row, chronology/FP and the complete accepted aid result.
        var standaloneIssues = new List<ValidationIssue>();
        Assert.NotNull(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(result.ToCanonicalJson()),
            "standalone.aid", standaloneIssues));
        Assert.Empty(standaloneIssues);
        var missing = WoundHistoryState.Parse(missingGrant.ToJsonString(), "missing.grant.history");
        Assert.False(missing.IsValid);
        Assert.Contains(missing.Issues, i => i.Code == "spiritual_aid_grant_history_invalid");
        var forged = result.ToCanonicalJson();
        forged["source"]!["healingTier"] = 4;
        forged["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(forged);
        var parserIssues = new List<ValidationIssue>();
        Assert.Null(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(forged), "changed.activeH", parserIssues));
        Assert.Contains(parserIssues, i => i.Actual?.Contains("exact retained request", StringComparison.Ordinal) == true);
        var settled = fixture.CaptureMutationRoots();
        Assert.DoesNotContain(await new ValidationService(files, NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
        fixture.AssertMutationRootsUnchanged(settled);
        fixture.AssertResourcesUnchanged();
        Assert.Equal(1, draws);
    }

    private static async Task<SpiritualSelfTreatmentSelection> CommitAidGrantAsync(HealingFixture fixture)
    {
        await StageAsync(fixture);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AidGuardianId)
                [SpiritualTreatmentParticipation.ChangeProperty] = AidGrantProposal();
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        }
        await PublishAsync(fixture);
        var after = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        await using var cleanup = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        // Direct publisher fixture models the cleaned protocol absence; actual
        // Engine cleanup is separately required by the real guided caller tests.
        fixture.Files.DeleteFile(cleanup, LiveTurnPreparationService.TurnRequestPath);
        var root = JsonNode.Parse((await fixture.Files.ReadFileAsync(cleanup, WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
        var record = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AidGuardianId)
            [SpiritualTreatmentParticipation.RecordProperty]!.AsObject();
        return new(after.WoundId, WoundIdentityState.ComputeSemanticFingerprint(after))
            { Aid = new("player_soul", AidGuardianId, SpiritualTreatmentParticipation.Fingerprint(record)) };
    }

    internal static JsonObject AidGrantProposal() => new()
    {
        ["schemaVersion"] = 1, ["operation"] = "grant", ["healerId"] = "player_soul", ["woundId"] = "wound_spiritual_session",
        ["conscious"] = true, ["capable"] = true, ["readableEvidence"] = "Хранитель понимает предложение и прямо разрешает душе лечить эту рану."
    };

    private static Task WriteAidReportAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        BookOfEternityClient.Models.ProgressionControl c) => files.WriteFileAtomicAsync(lease, ProgressionScheduleService.ReportPath,
        new JsonObject { ["progressionProcessingReport"] = new JsonObject
        {
            ["sessionId"] = "session_spiritual_healing", ["requestId"] = "request_aid", ["turnNumber"] = 44,
            ["worldCyclesProcessed"] = 0, ["factionCyclesProcessed"] = 0,
            ["chaosSeaCyclesProcessed"] = 1, ["guardianProjectCyclesProcessed"] = 1, ["residentAgencyCyclesProcessed"] = 1,
            ["newLastChaosSeaSimulationOrdinal"] = c.NextChaosSeaTurnOrdinal,
            ["newLastGuardianProjectCycleOrdinal"] = c.NextGuardianProjectCycleOrdinal,
            ["newLastResidentAgencyCycleOrdinal"] = c.NextResidentAgencyCycleOrdinal
        }}.ToJsonString());
}
