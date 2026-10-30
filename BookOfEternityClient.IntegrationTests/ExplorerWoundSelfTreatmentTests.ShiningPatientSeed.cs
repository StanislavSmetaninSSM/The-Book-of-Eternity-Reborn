using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    private const string ShiningPatientId = "shining_patient";
    private const string ShiningPatientHall = "hall_elyara_companion";
    private static string PatientName(string kind) => kind == "resident" ? "Мирный житель Нитей" : "Архонт Нитей";

    private static Task<HealingFixture> CreateShiningPatientFixtureAsync(string kind, FileSystemManagerHooks? hooks = null,
        Action<string>? allocated = null, bool engine = true) =>
        AfterlifeHealingPaymentLifecycleTests.CreateFactionServiceFixtureAsync(3, 14, hooks, engine: engine,
            allocatedRootForDiagnostics: allocated, playerHealingTier: 3,
            additionalBeforeResourceBootstrap: (files, lease, profiles) => SeedShiningPatientAsync(files, lease, profiles, kind));

    // Canonical initial CREATE DATA, before the one actual bootstrap. Reuse the
    // complete independent faction/helper world; the H0 patient is a separate actor.
    private static async Task SeedShiningPatientAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        JsonObject profiles, string kind)
    {
        var shining = JsonNode.Parse((await files.ReadFileAsync(lease, ShiningAbodeState.StatePath))!)!.AsObject();
        var residents = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianAbodeResidentState.StatePath))!)!.AsObject();
        var faction = Assert.Single(shining["factions"]!.AsArray()).AsObject();
        var factionId = faction["factionId"]!.GetValue<string>();
        var player = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]?.GetValue<string>() == "player_soul");
        Assert.Equal(3, player["standardArts"]!["spiritual_healing"]!.GetValue<int>());
        var wound = Assert.Single(player["activeWounds"]!.AsArray())!.DeepClone().AsObject();
        wound["owner"]!["ownerKind"] = kind; wound["owner"]!["ownerId"] = ShiningPatientId;
        wound["display"]!["visibility"] = "public";
        foreach (var definition in wound["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().OfType<JsonObject>())
            definition["allowedTargetKinds"] = new JsonArray(kind);
        var effects = player["activeEffects"]!.AsArray().OfType<JsonObject>().Select(e => e.DeepClone().AsObject()).ToArray();
        foreach (var effect in effects) { effect["target"]!["kind"] = kind; effect["target"]!["targetId"] = ShiningPatientId; }
        var patient = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(kind, ShiningPatientId, "Shining Abode", 42);
        patient["displayName"] = PatientName(kind); patient["lifeState"] = "alive"; patient["visibility"] = "public";
        patient["locationId"] = ShiningPatientHall; patient["locationName"] = "Зал сопровождения";
        patient["standardArts"]!["spiritual_healing"] = 0;
        patient["activeWounds"] = new JsonArray(wound.DeepClone());
        patient["activeEffects"] = new JsonArray(effects.Select(e => (JsonNode)e.DeepClone()).ToArray());
        player["activeWounds"] = new JsonArray(); player["activeEffects"] = new JsonArray();
        profiles["profiles"]!.AsArray().Add(patient);
        if (kind == "resident")
        {
            var row = FactionMaterializationValidationTests.BuildRouteResident(ShiningPatientId, factionId);
            row["displayName"] = PatientName(kind); row["residentRole"] = "archive_support";
            row["residentKind"] = "attendant_spirit"; row["originType"] = "native_spirit"; row["isPresent"] = false;
            row["summary"] = "Осознанный вознесённый житель; не целитель и не поставщик услуг.";
            row["guardianId"] = AfterlifeWoundTreatmentAuthority.ElyaraId; row["abodeId"] = AfterlifeWoundTreatmentAuthority.ElyaraAbodeId;
            row["mortalWorldImprint"] = new JsonObject { ["originWorldSummary"] = "Осознанный вознесённый дух.", ["futureCompanionPrompt"] = "An ascended archive spirit" };
            residents["entries"]!.AsArray().Add(row);
        }
        else
        {
            Assert.Equal("radiant_actor", kind);
            faction["factionLifecycle"]!["state"] = "active";
            faction["leadership"]!["leadershipState"] = "secure";
            faction["leadership"]!["headActorType"] = kind; faction["leadership"]!["headActorId"] = ShiningPatientId;
            shining["shiningPoliticalActors"] = new JsonArray(new JsonObject
            { ["actorId"] = ShiningPatientId, ["actorType"] = kind, ["displayName"] = PatientName(kind),
                ["summary"] = "Текущий живой глава Серебряных Нитей.", ["originFactionId"] = factionId,
                ["currentFactionId"] = factionId, ["politicalStatus"] = "head" });
        }
        GuardianAbodeResidentState.NormalizeShape(residents);
        ShiningAbodeState.NormalizeStateRoot(shining, residents);
        var parsed = WoundMaterializationContract.Parse(wound.ToJsonString(), "shining-patient.seed");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues.Select(i => i.Actual))); var w = parsed.Wound!;
        var fp = WoundIdentityState.ComputeSemanticFingerprint(w);
        var identity = WoundContractTestData.CreateIdentityEntry(w.WoundId, "shining_abode", kind, ShiningPatientId,
            WoundCarrierCatalog.AfterlifeProfilesPath, "spiritual", semanticFingerprint: fp);
        var rowHistory = WoundContractTestData.CreateTransition();
        rowHistory["woundId"] = w.WoundId; rowHistory["transitionId"] = w.LastTransition.TransitionId;
        rowHistory["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(w.WoundId);
        rowHistory["afterFingerprint"] = fp; rowHistory["sourceFingerprint"] = "sha256:" + new string('e', 64); rowHistory["attemptId"] = null;
        rowHistory["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(rowHistory["operationKey"]!.GetValue<string>(),
            rowHistory["eventRef"]!.GetValue<string>(), rowHistory["readableSummary"]!.GetValue<string>());
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects);
        foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
        {
            var effect = effects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>());
            entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
            entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundContractTestData.CreateIdentityIndex(identity).ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundContractTestData.CreateHistory(rowHistory).ToJsonString());
        await files.WriteFileAtomicAsync(lease, EffectIdentityState.StatePath, index.ToJsonString());
        await files.WriteFileAtomicAsync(lease, GuardianAbodeResidentState.StatePath, residents.ToJsonString());
        await files.WriteFileAtomicAsync(lease, ShiningAbodeState.StatePath, shining.ToJsonString());
        _ = SpiritualTreatmentParticipation.RequireShiningPatientAdmission(shining, profiles, residents,
            new("shining_abode", kind, ShiningPatientId, WoundCarrierCatalog.AfterlifeProfilesPath), SpiritualAidLocality.ForFaction(factionId, ShiningPatientHall));
    }

    [Theory]
    [InlineData("resident", false)][InlineData("resident", true)]
    [InlineData("radiant_actor", false)][InlineData("radiant_actor", true)]
    public async Task PatientMenuDoesNotDependOnUnrelatedGuardianRegistryAndReadsSelectedRootsOnce(string kind, bool malformed)
    {
        var reads = new CanonicalReadActivityAccumulator();
        await using var fixture = await CreateShiningPatientFixtureAsync(kind, new FileSystemManagerHooks { PassiveCanonicalReads = reads }, engine: false);
        // The menu is not a publication/Engine validation. Deliberately absent or
        // malformed unrelated registry must not create a fake patient dependency.
        var guardianPath = AfterlifeWoundTreatmentAuthority.AidLocalityPaths[0];
        if (malformed) await fixture.Files.WriteFileAtomicAsync(guardianPath, "{");
        else fixture.Files.DeleteFile(guardianPath);
        var paths = new[] { ShiningAbodeState.StatePath, GuardianAbodeResidentState.StatePath };
        var counts = paths.Select(reads.ReadCount).ToArray();
        var before = fixture.CaptureMutationRoots(paths.Append(guardianPath));
        var opened = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
        for (var i = 0; i < paths.Length; i++) Assert.Equal(1L, reads.ReadCount(paths[i]) - counts[i]);
        var target = Assert.Single(opened.WoundCommandState!.Targets, t => t.Owner.OwnerKind == kind && t.Owner.OwnerId == ShiningPatientId);
        Assert.Equal(PatientName(kind), target.Name); Assert.Single(target.Wounds);
        Assert.DoesNotContain(opened.WoundCommandState.Choices, c => c.Selection.Aid?.ShiningPatient is not null);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var detail = await WoundApplicationService.SelectTargetAsync(fixture.Files, lease, opened.WoundCommandState, target.Value, "/лечить");
        Assert.DoesNotContain(detail.Notifications, n => n.Severity == BookOfEternityClient.CommandProtocol.UiNotificationSeverity.Error);
        Assert.Equal(target.Owner, detail.WoundCommandState!.SelectedTarget!.Owner);
        fixture.AssertMutationRootsUnchanged(before);
    }
}
