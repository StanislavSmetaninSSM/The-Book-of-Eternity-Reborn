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
        var afterEncoding = await ElyaraVisitService.ReadAsync(files, lease);
        await files.WriteFileAtomicAsync(lease, AfterlifeSpiritualConflictState.StatePath,
            AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());
        Assert.Equal(state.Intent, (await ElyaraVisitService.ReadAsync(files, lease)).Plan.State!.Intent);
        await Assert.ThrowsAsync<InvalidDataException>(() => ElyaraVisitService.RequireImagesCurrentAsync(files, lease, afterEncoding));
    }

    [Theory]
    [InlineData("valid")] [InlineData("dead")] [InlineData("forged_original")]
    public async Task ActualAuthenticatedOriginalValidatorCannotAcceptCurrentImageAsPermission(string change)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20, remoteElyara: true);
        var files = fixture.Files; string action; BookOfEternityClient.Models.ProgressionControl control;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var state = (await ElyaraVisitService.ReadAsync(files, lease)).Plan.State!;
            action = await ElyaraVisitService.ConfirmAsync(files, lease, state);
            if (change == "forged_original")
            {
                // Current afterimages will look reachable, but the actual authenticated
                // pre-turn source lacks the original discovery offered by this Action.
                var root = JsonNode.Parse((await files.ReadFileAsync(lease, ElyaraVisitService.SourcePaths[2]))!)!;
                root["chaosSeaNavigation"]!["discoveredAbodes"] = new JsonArray("abode_visit_source");
                await files.WriteFileAtomicAsync(lease, ElyaraVisitService.SourcePaths[2], root.ToJsonString());
            }
            control = await new ProgressionScheduleService(files, Microsoft.Extensions.Logging.Abstractions.NullLogger<ProgressionScheduleService>.Instance)
                .BuildControlForNextTurnAsync(lease, activeTurnRealm: null);
        }
        await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "session_elyara_original", RequestId = "request_elyara_original", TurnNumber = 43,
            CurrentRealm = "Chaos Sea", PlayerAction = action, ProgressionControl = control, PreGeneratedDices1d20 = [7, 11, 16]
        });
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var root = JsonNode.Parse((await files.ReadFileAsync(lease, ElyaraVisitService.SourcePaths[2]))!)!;
            root["activeGuardian"] = root["guardians"]![0]!.DeepClone();
            root["chaosSeaNavigation"]!["currentAbodeId"] = AfterlifeWoundTreatmentAuthority.ElyaraAbodeId;
            root["chaosSeaNavigation"]!["currentGuardianId"] = AfterlifeWoundTreatmentAuthority.ElyaraId;
            root["chaosSeaNavigation"]!["discoveredAbodes"] = new JsonArray("abode_visit_source", AfterlifeWoundTreatmentAuthority.ElyaraAbodeId);
            await files.WriteFileAtomicAsync(lease, ElyaraVisitService.SourcePaths[2], root.ToJsonString());
            if (change == "dead")
            {
                var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
                profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(AfterlifeHealingServiceContract.IsElyaraProfile)["lifeState"] = "dead";
                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            }
        }
        var before = SnapshotCanonical(files); var issues = new List<ValidationIssue>();
        await new ValidationService(files, Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance)
            .ValidateElyaraVisitOutcomeAsync(action, issues);
        if (change == "valid") Assert.Empty(issues);
        else Assert.Contains(issues, i => i.Code == "elyara_visit_original_or_arrival_invalid");
        AssertCanonicalUnchanged(files, before);
        Assert.Null(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Theory]
    [InlineData("hall")] [InlineData("package")] [InlineData("dead")] [InlineData("sealed")]
    public async Task ShiningLocalServiceConfirmationKeepsFreshHallLifeAndPackageGuards(string change)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20, realm: "Shining Abode");
        var files = fixture.Files;
        var state = (await ElyaraVisitService.OpenAsync(files)).WoundCommandState!;
        Assert.False(state.IsTargetSelection); // Current Shining route is direct self choice, no foreign target phase.
        var choice = Assert.Single(state.Choices, c => c.Available && c.Selection.Service is not null);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var path = change == "dead" ? WoundCarrierCatalog.AfterlifeProfilesPath : ShiningAbodeState.StatePath;
        var root = JsonNode.Parse((await files.ReadFileAsync(lease, path))!)!;
        if (change == "hall") root["currentHallId"] = "hall_elsewhere";
        if (change == "package") root["preparedIncarnationPackage"] = new JsonObject();
        if (change == "sealed") root["availability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension;
        if (change == "dead") root["profiles"]!.AsArray().OfType<JsonObject>().Single(AfterlifeHealingServiceContract.IsElyaraProfile)["lifeState"] = "dead";
        await files.WriteFileAtomicAsync(lease, path, root.ToJsonString());
        var before = SnapshotCanonical(files);
        await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.ConfirmAsync(files, lease, state, choice.Value));
        AssertCanonicalUnchanged(files, before);
        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
    }

    [Fact]
    public async Task ExistingShiningScopeResolverRetainsItsSourceOrderAndCurrentMembership()
    {
        var armed = false; var opened = new List<string>();
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateServiceFixtureAsync(1, 20,
            new FileSystemManagerHooks { BeforeCanonicalReadOpenAsync = path =>
            { if (armed) opened.Add(path); return Task.CompletedTask; } }, realm: "Shining Abode");
        var files = fixture.Files;
        var residents = new JsonObject { ["entries"] = new JsonArray(new JsonObject
            { ["residentId"] = "resident_here", ["hallId"] = "hall_elyara_companion", ["isPresent"] = true }) };
        await files.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residents.ToJsonString());
        armed = true;
        var scope = await new LocalInteractionScopeService(files).ResolveAsync("Shining Abode");
        armed = false;
        Assert.True(scope.IsResolved); Assert.Equal("hall_elyara_companion", scope.LocationId);
        Assert.Equal(new[] { ShiningAbodeState.StatePath, GuardianAbodeResidentState.StatePath }, opened);
        Assert.Equal(2, scope.AuthoritySnapshots.Count);
        Assert.Contains("resident_here", scope.LocalActorIds);
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
