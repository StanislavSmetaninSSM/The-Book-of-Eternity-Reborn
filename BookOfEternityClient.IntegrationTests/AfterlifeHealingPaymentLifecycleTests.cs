using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

/// <summary>Actual stage/original/common C4/History, with deterministic retained draws; not a live GM or fresh process.</summary>
public sealed partial class AfterlifeHealingPaymentLifecycleTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("funds")][InlineData("dead")][InlineData("price")]
    public async Task ConfirmationRejectsFreshLossWithoutRequestDrawOrPayment(string change)
    {
        await using var fixture = await CreateServiceFixtureAsync(1, 20);
        var files = fixture.Files;
        var state = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
        state = state.ForTarget(state.Targets.Single(t => t.Owner.OwnerKind == "player_soul"));
        var selected = Assert.Single(state.Choices, c => c.Available && c.Selection.Service is not null);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        if (change == "funds")
        {
            var soul = JsonNode.Parse((await files.ReadFileAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!;
            soul["inkFeathers"]!["current"] = 24;
            await files.WriteFileAtomicAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath, soul.ToJsonString());
        }
        else
        {
            var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
            var actor = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(AfterlifeHealingServiceContract.IsElyaraProfile);
            if (change == "dead") actor["lifeState"] = "dead";
            else actor["healingServiceProfile"]!["priceMultiplierPercent"] = 101;
            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        }
        var before = fixture.CaptureMutationRoots([AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath]);
        await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.ConfirmAsync(files, lease, state, selected.Value));
        fixture.AssertMutationRootsUnchanged(before);
        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Null(await files.ReadFileBytesAsync(lease, PendingPlayerActionService.PendingPath));
    }

    [Fact]
    public async Task SourceChangeAfterControlRefusesBeforeAnyTreatmentDrawOrPayment()
    {
        FileSystemManager? files = null; var armed = false; var changed = 0; var draws = 0;
        await using var fixture = await CreateServiceFixtureAsync(3, 20, new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed || path != ProgressionScheduleService.SchedulePath || !File.Exists(files!.ResolvePath(path))) return Task.CompletedTask;
                var current = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(File.ReadAllBytes(files.ResolvePath(path))))!;
                if (current["pendingChaosSeaCycles"]!.GetValue<int>() != 1) return Task.CompletedTask;
                armed = false; changed++;
                var profilePath = files.ResolvePath(WoundCarrierCatalog.AfterlifeProfilesPath);
                var profiles = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(File.ReadAllBytes(profilePath)))!;
                profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(AfterlifeHealingServiceContract.IsElyaraProfile)["lifeState"] = "dead";
                File.WriteAllText(profilePath, profiles.ToJsonString());
                return Task.CompletedTask;
            }
        });
        files = fixture.Files;
        var state = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
        var choice = Assert.Single(state.Choices, c => c.Available && c.Selection.Service is not null);
        var selected = choice.Selection with { Service = choice.Selection.Service! with { PlayerConfirmed = true } };
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        armed = true;
        var result = await AfterlifeWoundTreatmentAuthority.StagePlayerServiceTurnAsync(files, lease,
            new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance),
            "session_spiritual_healing", "request_spiritual_healing", 43, selected, _ => { draws++; return [20]; });
        Assert.Equal(1, changed); Assert.False(result.Stage.IsStaged); Assert.Equal(0, draws);
        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(500, JsonNode.Parse((await files.ReadFileAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.Equal(0, JsonNode.Parse((await files.ReadFileAsync(lease, ProgressionScheduleService.SchedulePath))!)!["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
        Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
        output.WriteLine("Foreign life change after control: zero treatment draws/requests/fees/accepted cycles. Caller still owns prepared-control rollback; this direct stage does not claim an outer Engine rollback.");
    }

    [Fact]
    public async Task OpeningServiceReusesParsedReadSetWithoutAdditionalSourceReads()
    {
        var armed = false; var reads = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var fixture = await CreateServiceFixtureAsync(3, 20, new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (armed) reads[path] = reads.GetValueOrDefault(path) + 1;
                return Task.CompletedTask;
            }
        });
        // This counter observes actual opens, not the fresh absence path. Supply
        // the ordinary empty roster before either compared action so every
        // asserted locality input exists and no setup write enters the sample.
        var residents = new JsonObject();
        GuardianAbodeResidentState.NormalizeShape(residents);
        await fixture.Files.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, residents.ToJsonString());
        Dictionary<string, int>? baseline = null;
        for (var action = 0; action < 2; action++)
        {
            // Same existing read/menu path first with unavailable service, then
            // available service. Do not pretend its existing fresh mechanics
            // reads disappear or weaken them to manufacture a one-read result.
            var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
            profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(AfterlifeHealingServiceContract.IsElyaraProfile)
                ["healingServiceProfile"]!["availability"] = action == 0 ? "unavailable" : "available";
            await fixture.Files.WriteFileAtomicAsync(WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            var before = fixture.CaptureMutationRoots([AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath,
                GuardianAbodeResidentState.StatePath]);
            reads.Clear(); armed = true;
            var opened = await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true);
            armed = false;
            Assert.Equal(action == 1, opened.WoundCommandState!.Choices.Any(c => c.Available && c.Selection.Service is not null));
            foreach (var path in new[] { WoundCarrierCatalog.AfterlifeProfilesPath, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath,
                AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths[0], GuardianAbodeResidentState.StatePath })
                Assert.True(reads.GetValueOrDefault(path) > 0, "Each action requires fresh source reads: " + path);
            Assert.Equal(0, reads.GetValueOrDefault(ProgressionScheduleService.SchedulePath));
            if (baseline is null) baseline = new(reads, StringComparer.Ordinal);
            else Assert.Equal(baseline.OrderBy(p => p.Key), reads.OrderBy(p => p.Key));
            fixture.AssertMutationRootsUnchanged(before);
            output.WriteLine(JsonSerializer.Serialize(new { available = action == 1, actualSourceReads = reads }));
        }
        output.WriteLine("Service projection adds zero source reads over the existing same read/menu path. Both actions read fresh; no cross-action cache or gameplay mutation, no speedup claim.");
    }

    [Theory]
    [InlineData(1, "NoImprovement")]
    [InlineData(2, "RecoveryPoint")]
    [InlineData(20, "ReduceTwo")]
    public Task AcceptedSuccessPartialAndFailedAttemptChargeOnceAndReplayWithoutWrites(int die, string band) =>
        RunAcceptedServiceAsync(die, band, "Chaos Sea");

    private async Task RunAcceptedServiceAsync(int die, string band, string realm, bool faction = false,
        string? progression = null, int? expectedProjectedTier = null)
    {
        var rank = faction ? 2 : 3; var tier = faction ? 3 : 5; var price = faction ? 51 : 100;
        await using var fixture = faction ? await CreateFactionServiceFixtureAsync(rank, die, progression: progression)
            : await CreateServiceFixtureAsync(rank, die, realm: realm);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var (stage, request) = await StageAsync(fixture, die, proveRetry: true, realm: realm, faction: faction);
        var graphs = AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(original, stage.Stage.GmContext!["destinationGraphs"]!.AsArray());
        await fixture.Files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
            { ["spiritualHealingGraphs"] = graphs, ["dialogueOptions"] = new JsonArray(), ["timestamp"] = DateTime.UtcNow.ToString("o") }.ToJsonString());
        LiveTurnPendingSnapshotManifest manifest;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>((await fixture.Files.ReadFileAsync(lease,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var plan = await fixture.PublishAsync();
        Assert.True(plan.OwnerCompanionAfterImages.ContainsKey(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath));
        var wallet = JsonNode.Parse((await fixture.Files.ReadFileAsync(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!["inkFeathers"]!;
        Assert.Equal(500 - price, wallet["current"]!.GetValue<int>()); Assert.Equal(900, wallet["total"]!.GetValue<int>());
        var history = await fixture.ReadHistoryAsync();
        var paid = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions, t => t.AttemptId is not null).TransitionResult);
        Assert.Equal("service", paid.Mode); Assert.Equal(7, paid.SchemaVersion);
        Assert.True(JsonNode.DeepEquals(request, paid.ToCanonicalJson()["request"]));
        if (expectedProjectedTier is { } projectedTier)
        {
            var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
            var healer = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]!.GetValue<string>() == FactionHealerId);
            Assert.Equal(projectedTier, healer["standardArts"]!["spiritual_healing"]!.GetValue<int>());
            Assert.Equal(progression == "disabled" ? 34 : progression == "enabled" ? 0 : 6, healer["currencies"]!["inkFeathers"]!.GetValue<int>());
            Assert.Equal(progression == "disabled" ? 0 : 1, healer["currencies"]!["lightSparks"]!.GetValue<int>());
            var entries = healer["progressionLedger"]!.AsArray().OfType<JsonObject>().ToArray();
            Assert.Equal(progression == "disabled" ? 0 : 1, entries.Length);
            if (entries.Length == 1)
            {
                Assert.Equal("client_auto_strategy", entries[0]["source"]!.GetValue<string>());
                Assert.Equal("shining:1", entries[0]["cycleKey"]!.GetValue<string>());
                Assert.Equal(6, entries[0]["income"]!["inkFeathers"]!.GetValue<int>());
                Assert.Equal(1, entries[0]["income"]!["lightSparks"]!.GetValue<int>());
                Assert.Equal(progression == "enabled" ? 40 : 0, entries[0]["spending"]!["inkFeathers"]!.GetValue<int>());
                Assert.Equal(0, entries[0]["spending"]!["lightSparks"]!.GetValue<int>());
                Assert.Equal("shining:1", healer["progressionStrategy"]!["lastAutoProgressionCycleKey"]!.GetValue<string>());
                Assert.Equal(progression == "enabled" ? new[] { "spiritual_healing:3->4" } : Array.Empty<string>(),
                    entries[0]["upgrades"]!.AsArray().Select(n => n!.GetValue<string>()));
            }
            Assert.Equal(3, paid.ServiceQuote!.HealingTier);
            Assert.Equal(51, paid.ServiceQuote.Amount);
        }
        var calculation = SpiritualHealingOutcomeMath.Calculate(new(tier, rank, die, 0, 0))!;
        Assert.Equal(band, calculation.ResultBand.ToString());
        var expected = SpiritualWoundRecoveryPlanner.Build(original, 0, request["attempt"]!["attemptId"]!.GetValue<string>(),
            request["attempt"]!["cycleKey"]!.GetValue<string>(), calculation);
        Assert.Equal(expected.Select(s => (s.Kind, s.SeverityRank, s.Progress)),
            paid.Stages.Select(s => (s.Kind, s.After.Severity.Rank, s.After.Recovery.CurrentStepProgress)));
        await fixture.AssertOneCycleAndRetainedAttemptAsync(); fixture.AssertResourcesUnchanged();
        var settled = fixture.CaptureMutationRoots([AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath]);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            for (var retry = 0; retry < 2; retry++)
                Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(stage.Control!,
                    "session_spiritual_healing", "request_spiritual_healing", 43, p => fixture.Files.ReadFileBytesAsync(lease, p),
                    manifest.ManifestPayloadHash, request));
        fixture.AssertMutationRootsUnchanged(settled);
        if (die == 1)
        {
            // Genuine result remains historical truth, but an externally
            // restored pre-fee wallet cannot prove CURRENT complete settlement.
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            var path = AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath;
            var soul = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, path))!)!.AsObject();
            soul["inkFeathers"] = request["payment"]!["walletBefore"]!.DeepClone();
            await fixture.Files.WriteFileAtomicAsync(lease, path, soul.ToJsonString());
            var changed = fixture.CaptureMutationRoots([path]);
            var refusal = await Assert.ThrowsAsync<InvalidDataException>(() => ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(stage.Control!,
                "session_spiritual_healing", "request_spiritual_healing", 43, p => fixture.Files.ReadFileBytesAsync(lease, p), manifest.ManifestPayloadHash, request));
            Assert.Contains("current wallet", refusal.Message);
            Assert.True(WoundHistoryState.Parse((await fixture.Files.ReadFileAsync(lease, WoundHistoryState.HistoryPath))!, WoundHistoryState.HistoryPath).IsValid);
            fixture.AssertMutationRootsUnchanged(changed);
        }
        output.WriteLine($"Service {band}: d{die}, fee{price} once, wallet500→{500 - price},total900; one retained draw/cycle, strict result7; two complete replay reads write nothing.");
    }

    [Fact]
    public async Task ActualCommonPublicationCutRestoresWalletAndEveryStagedParticipant()
    {
        FileSystemManager? files = null; var armed = false; var cuts = 0;
        await using var fixture = await CreateServiceFixtureAsync(3, 20, new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (!armed || phase != TrustedLocalPublicationPhase.MemberPublished) return;
                var path = files!.ResolvePath(AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath);
                if (JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(File.ReadAllBytes(path)))!["inkFeathers"]!["current"]!.GetValue<int>() != 400) return;
                armed = false; cuts++; throw new InvalidOperationException("Service C4 cut after the actual wallet publication.");
            }
        });
        files = fixture.Files;
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var (stage, _) = await StageAsync(fixture, 20);
        await files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
            { ["spiritualHealingGraphs"] = AfterlifeSpiritualNaturalRecoveryTests.AuthorNaturalGraphs(original,
                stage.Stage.GmContext!["destinationGraphs"]!.AsArray()) }.ToJsonString());
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync()) await fixture.WriteReportAsync(lease);
        var before = fixture.CaptureMutationRoots([AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath]);
        armed = true;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.PublishAsync());
        Assert.Equal(1, cuts); fixture.AssertMutationRootsUnchanged(before); fixture.AssertResourcesUnchanged();
        Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
        output.WriteLine("Actual wallet C4 cut: exact Soul/wound/effect/identity/History/schedule/report/request restored, retained die preserved, no fee/result accepted.");
    }

    [Fact]
    public async Task CompetingGmFeatherChangeRejectsWithoutPaymentOrHistory()
    {
        await using var fixture = await CreateServiceFixtureAsync(3, 1);
        await StageAsync(fixture, 1);
        await fixture.Files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
        {
            ["spiritualHealingGraphs"] = new JsonArray(),
            ["metaStateUpdates"] = new JsonObject { ["inkFeatherChanges"] = new JsonObject { ["spend"] = 100 } }
        }.ToJsonString());
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        var before = fixture.CaptureMutationRoots([AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath]);
        var issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
        Assert.Contains(issues, i => i.Code == "spiritual_self_treatment_admission_invalid" &&
            i.Actual?.Contains("competing GM currency", StringComparison.Ordinal) == true);
        fixture.AssertMutationRootsUnchanged(before);
    }

    /// <summary>Uses the existing whole resource bootstrap; optional extra authored input runs before that boundary only.</summary>
    internal static Task<HealingFixture> CreateServiceFixtureAsync(int rank, int die, FileSystemManagerHooks? hooks = null,
        bool engine = false, string realm = "Chaos Sea", bool remoteElyara = false,
        Func<FileSystemManager, JsonObject, Task>? additionalBeforeResourceBootstrap = null) =>
        HealingFixture.CreateAsync(rank, 0, die, stageRequest: false, hooks: hooks, beforeResourceBootstrap: async (files, profiles) =>
            {
                await SeedServiceAsync(files, profiles, realm);
                if (remoteElyara) await ElyaraCurrentLocationNavigationTests.SeedRemoteSourceAsync(files, profiles);
                if (additionalBeforeResourceBootstrap is not null) await additionalBeforeResourceBootstrap(files, profiles);
                if (engine) await ExplorerWoundSelfTreatmentTests.SeedAidJourneyLoreAsync(files);
            },
            realm: realm, configureSoul: soul => soul["inkFeathers"] = new JsonObject { ["current"] = 500, ["total"] = 900 });

    internal static async Task SeedServiceAsync(FileSystemManager files, JsonObject profiles, string realm = "Chaos Sea")
    {
        var library = new SystemGuardianLibraryService(files, NullLogger<SystemGuardianLibraryService>.Instance);
        var preset = new SystemGuardianLibraryService.SystemGuardianPresetDescriptor
        {
            PresetId = "elyara", DisplayName = "Элиара Последней Раны", DefaultNameVariant = "Элиара Последней Раны",
            Domain = "Healing", AbodeName = "Лазарет Незаживающего Света", LibraryKind = "built_in"
        };
        var actor = library.BuildAfterlifeEntityProfileRootForFreshNewGame(preset, "Душа", 42,
            DateTimeOffset.Parse("2026-10-29T00:00:00Z"))["profiles"]![0]!.DeepClone().AsObject();
        actor["activeWounds"] = new JsonArray(); actor["activeEffects"] = new JsonArray();
        if (realm == "Shining Abode")
        {
            actor["realm"] = realm; actor["locationId"] = "hall_elyara_companion"; actor["locationName"] = "Зал сопровождения";
            actor["healingServiceProfile"]!["realm"] = "shining_abode";
            actor["healingServiceProfile"]!["locationRef"] = "hall_elyara_companion";
        }
        profiles["profiles"]!.AsArray().Add(actor);
        var guardian = PlayerGuardianFoundationValidationTests.CreateGuardian(AfterlifeWoundTreatmentAuthority.ElyaraId,
            "Элиара Последней Раны", AfterlifeWoundTreatmentAuthority.ElyaraAbodeId);
        guardian["abode"]!["name"] = "Лазарет Незаживающего Света";
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        if (realm == "Shining Abode")
        {
            var shining = ShiningAbodeState.CreateDefaultState();
            shining["currentHallId"] = "hall_elyara_companion";
            shining["halls"] = new JsonArray(new JsonObject { ["hallId"] = "hall_elyara_companion", ["hallName"] = "Зал сопровождения",
                ["description"] = "Тихий зал для сопровождения души.", ["serviceTags"] = new JsonArray() });
            ShiningAbodeState.NormalizeStateRoot(shining, residentRoot: null); // Complete current DATA before the sole bootstrap.
            await files.WriteFileAtomicAsync(lease, ShiningAbodeState.StatePath, shining.ToJsonString());
        }
        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await files.WriteFileAtomicAsync(lease, AfterlifeWoundTreatmentAuthority.ServiceLocalityPaths[0], new JsonObject
        {
            ["guardians"] = new JsonArray(guardian), ["activeGuardian"] = guardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = AfterlifeWoundTreatmentAuthority.ElyaraId,
                ["currentAbodeId"] = AfterlifeWoundTreatmentAuthority.ElyaraAbodeId,
                ["discoveredAbodes"] = new JsonArray(AfterlifeWoundTreatmentAuthority.ElyaraAbodeId) }
        }.ToJsonString());
    }

    internal static async Task<(AfterlifeWoundTreatmentAuthority.ServiceTurnStageResult Stage, JsonObject Request)> StageAsync(
        HealingFixture fixture, int die, bool proveRetry = false, string realm = "Chaos Sea", bool faction = false)
    {
        var files = fixture.Files;
        var offered = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
        var choice = Assert.Single(offered.Choices, c => c.Selection.Service is not null && c.Available &&
            (!faction || c.Selection.Service.Quote.Provider.OwnerKind == "resident"));
        var selected = choice.Selection with { Service = choice.Selection.Service! with { PlayerConfirmed = true } };
        var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        AfterlifeWoundTreatmentAuthority.ServiceTurnStageResult stage;
        JsonObject request; var draws = 0;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            stage = await AfterlifeWoundTreatmentAuthority.StagePlayerServiceTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_spiritual_healing", 43, selected, _ => { draws++; return [die]; });
            Assert.True(stage.Stage.IsStaged, stage.Stage.IssueCode);
            var bytes = (await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!;
            request = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes))!.AsObject();
            if (proveRetry)
            {
                var schedule = await files.ReadFileBytesAsync(lease, ProgressionScheduleService.SchedulePath);
                var retry = await AfterlifeWoundTreatmentAuthority.StagePlayerServiceTurnAsync(files, lease, scheduler,
                    "session_spiritual_healing", "request_spiritual_healing", 43, selected, _ => { draws++; return [20]; });
                Assert.True(retry.Stage.IsStaged, retry.Stage.IssueCode); Assert.True(retry.Stage.AlreadyStaged);
                Assert.Equal(bytes, await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Equal(schedule, await files.ReadFileBytesAsync(lease, ProgressionScheduleService.SchedulePath));
            }
            Assert.Equal(1, draws);
            Assert.Equal(500, JsonNode.Parse((await files.ReadFileAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!["inkFeathers"]!["current"]!.GetValue<int>());
        }
        await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "session_spiritual_healing", RequestId = "request_spiritual_healing", TurnNumber = 43,
            CurrentRealm = realm, PlayerAction = WoundApplicationService.ActionFor(selected),
            ProgressionControl = stage.Control, PreGeneratedDices1d20 = [7, 11, 16]
        });
        return (stage, request);
    }
}
