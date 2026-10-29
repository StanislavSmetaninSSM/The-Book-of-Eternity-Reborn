using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ElyaraCurrentLocationNavigationTests
{
    [Theory]
    [InlineData(1)] [InlineData(20)]
    public async Task VisitReadsEachActualSourceOnceWithoutPerActorReadsOrMutation(int population)
    {
        var armed = false; var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20,
            new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = path =>
            { if (armed) reads[path] = reads.GetValueOrDefault(path) + 1; return Task.CompletedTask; } }, remoteElyara: true);
        var files = fixture.Files;
        // All source paths exist so the hook counts actual opens, including empty roots.
        await files.WriteFileAtomicAsync(ShiningAbodeState.StatePath, ShiningAbodeState.CreateDefaultState().ToJsonString());
        await files.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, new JsonObject().ToJsonString());
        var profiles = JsonNode.Parse((await files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        for (var index = 1; index < population; index++)
        {
            var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guardian_unrelated_" + index, "Chaos Sea", 42);
            actor["activeWounds"] = new JsonArray(); actor["activeEffects"] = new JsonArray();
            profiles["profiles"]!.AsArray().Add(actor);
        }
        await files.WriteFileAtomicAsync(WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        _ = files.GetOrCreateSessionGeneration(lease);
        var before = SnapshotCanonical(files);
        armed = true; var source = await ElyaraVisitService.ReadAsync(files, lease); armed = false;
        Assert.Equal(ElyaraVisitKind.ChaosTravel, source.Plan.Kind);
        Assert.Equal(ElyaraVisitService.SourcePaths.OrderBy(p => p), reads.Keys.OrderBy(p => p));
        Assert.All(reads.Values, count => Assert.Equal(1, count));
        AssertCanonicalUnchanged(files, before);
        output.WriteLine($"population={population}; actual source opens=5, one per root; no per-actor IO/cache or canonical mutation.");
    }

    [Theory]
    [InlineData("dead")] [InlineData("relocated")] [InlineData("discovery")]
    [InlineData("player_moved")] [InlineData("realm")] [InlineData("unavailable")]
    [InlineData("duplicate")]
    public async Task ConfirmAndStageRejectFreshChangesWithoutPublishingOrDrawing(string change)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20, remoteElyara: true);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var state = (await ElyaraVisitService.ReadAsync(files, lease)).Plan.State!;
        var path = change is "discovery" or "player_moved" ? ElyaraVisitService.SourcePaths[2]
            : change == "realm" ? ElyaraVisitService.SourcePaths[0] : ElyaraVisitService.SourcePaths[1];
        var root = JsonNode.Parse((await files.ReadFileAsync(lease, path))!)!.AsObject();
        if (change == "realm") root["currentRealm"] = "Shining Abode";
        else if (change == "discovery") root["chaosSeaNavigation"]!["discoveredAbodes"] = new JsonArray("abode_visit_source");
        else if (change == "player_moved") root["chaosSeaNavigation"]!["currentAbodeId"] = AfterlifeWoundTreatmentAuthority.ElyaraAbodeId;
        else
        {
            var actor = root["profiles"]!.AsArray().OfType<JsonObject>().Single(AfterlifeHealingServiceContract.IsElyaraProfile);
            if (change == "dead") actor["lifeState"] = "dead";
            if (change == "unavailable") actor["healingServiceProfile"]!["availability"] = "unavailable";
            if (change == "duplicate") root["profiles"]!.AsArray().Add(actor.DeepClone());
            if (change == "relocated") { actor["locationId"] = "unproved_place"; actor["healingServiceProfile"]!["locationRef"] = "unproved_place"; }
        }
        await files.WriteFileAtomicAsync(lease, path, root.ToJsonString());
        var before = SnapshotCanonical(files);
        await Assert.ThrowsAsync<InvalidDataException>(() => ElyaraVisitService.ConfirmAsync(files, lease, state));
        await Assert.ThrowsAsync<InvalidDataException>(() => ElyaraVisitService.RequireCurrentAsync(files, lease, state.Intent));
        AssertCanonicalUnchanged(files, before);
        foreach (var transient in new[] { PendingPlayerActionService.PendingPath, AcceptedMechanicsPlan.WoundCommandPath,
            LiveTurnPreparationService.TurnRequestPath, ProgressionScheduleService.SchedulePath })
            Assert.Null(await files.ReadFileBytesAsync(lease, transient));
        Assert.Equal(500, JsonNode.Parse((await files.ReadFileAsync(lease, ElyaraVisitService.SourcePaths[0]))!)!["inkFeathers"]!["current"]!.GetValue<int>());
    }

    [Fact]
    public async Task PhysicalClosingFenceRejectsReencodingAndSpecificQueueCannotLoseItsMarker()
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20, remoteElyara: true);
        var files = fixture.Files; await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var source = await ElyaraVisitService.ReadAsync(files, lease);
        var state = source.Plan.State!;
        var action = await ElyaraVisitService.ConfirmAsync(files, lease, state);
        var queued = PendingPlayerActionService.PrepareQueued(files, lease, action, ElyaraVisitService.BrowserSource, DateTime.UtcNow.ToString("o"));
        Assert.Equal(action, queued["playerAction"]!.GetValue<string>());
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            _ = PendingPlayerActionService.PrepareQueued(files, lease, action[(action.IndexOf('\n') + 1)..],
                ElyaraVisitService.BrowserSource, DateTime.UtcNow.ToString("o"));
            await Task.CompletedTask;
        });
        var path = ElyaraVisitService.SourcePaths[0];
        var root = JsonNode.Parse((await files.ReadFileAsync(lease, path))!)!;
        await files.WriteFileAtomicAsync(lease, path, root.ToJsonString(new() { WriteIndented = true }));
        // Comparison DATA survives formatting, but physical closure still refuses changed bytes.
        Assert.Equal(state.Intent, (await ElyaraVisitService.ReadAsync(files, lease)).Plan.State!.Intent);
        await Assert.ThrowsAsync<InvalidDataException>(() => ElyaraVisitService.RequireImagesCurrentAsync(files, lease, source));
    }

    internal static async Task SeedRemoteSourceAsync(FileSystemManager files, JsonObject profiles)
    {
        var source = PlayerGuardianFoundationValidationTests.CreateGuardian("guardian_visit_source", "Тихий Хранитель", "abode_visit_source");
        source["abode"]!["isDiscovered"] = true;
        var path = AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths[0];
        var guardians = JsonNode.Parse((await files.ReadFileAsync(path))!)!.AsObject();
        guardians["guardians"]![0]!["abode"]!["isDiscovered"] = true;
        guardians["guardians"]!.AsArray().Add(source);
        guardians["activeGuardian"] = source.DeepClone();
        guardians["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = "guardian_visit_source",
            ["currentAbodeId"] = "abode_visit_source", ["discoveredAbodes"] = new JsonArray("abode_visit_source", AfterlifeWoundTreatmentAuthority.ElyaraAbodeId) };
        var actor = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guardian_visit_source", "Chaos Sea", 42);
        actor["displayName"] = "Тихий Хранитель"; actor["locationId"] = "abode_visit_source";
        actor["activeWounds"] = new JsonArray(); actor["activeEffects"] = new JsonArray();
        profiles["profiles"]!.AsArray().Add(actor);
        await files.WriteFileAtomicAsync(path, guardians.ToJsonString());
        await files.WriteFileAtomicAsync(WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
    }
}
