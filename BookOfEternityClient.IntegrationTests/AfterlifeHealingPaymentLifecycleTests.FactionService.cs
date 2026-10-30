using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeHealingPaymentLifecycleTests
{
    internal const string FactionHealerId = "resident_public_healer_0";
    internal const string FactionHealerName = "Целитель Серебряных Нитей";

    /// <summary>Seeds the existing complete faction/resident fixtures before the sole actual resource bootstrap.</summary>
    /// <param name="rank">Patient wound rank.</param>
    /// <param name="die">Direct stage entropy dependency; real Engine journeys retain their actual draws.</param>
    /// <param name="hooks">Optional physical source/publication observation dependencies.</param>
    /// <param name="engine">Includes the existing ordinary Engine lore prerequisites.</param>
    /// <param name="providers">Independent exact public providers for real source-count controls.</param>
    /// <returns>An owned per-test fixture, never a publication or source authority substitute.</returns>
    internal static Task<HealingFixture> CreateFactionServiceFixtureAsync(int rank, int die,
        FileSystemManagerHooks? hooks = null, bool engine = false, int providers = 1) =>
        CreateServiceFixtureAsync(rank, die, hooks, engine, realm: "Shining Abode",
            additionalBeforeResourceBootstrap: async (files, profiles) =>
            {
                var inputs = ShiningFactionHealingServiceTestData.Create(providers);
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                var shining = JsonNode.Parse((await files.ReadFileAsync(lease, ShiningAbodeState.StatePath))!)!.AsObject();
                var faction = FactionMaterializationValidationTests.BuildCompleteNativeShiningFaction();
                var factionId = faction["factionId"]!.GetValue<string>();
                faction["hallId"] = "hall_elyara_companion";
                faction["charter"]!["factionName"] = "Серебряные Нити";
                faction["factionLifecycle"]!["state"] = "leaderless";
                faction["leadership"]!["leadershipState"] = "vacant";
                faction["leadership"]!["headActorType"] = null; faction["leadership"]!["headActorId"] = null;
                faction["materialization"]!["capabilities"]!["hasResidentAffiliations"] = true;
                faction["materialization"]!["capabilities"]!["canTrade"] = false;
                faction["materialization"]!["sections"]!["residentAffiliations"] = new JsonObject { ["state"] = "populated" };
                var residents = new JsonObject(); GuardianAbodeResidentState.NormalizeShape(residents);
                for (var index = 0; index < providers; index++)
                {
                    var id = "resident_public_healer_" + index;
                    var resident = FactionMaterializationValidationTests.BuildRouteResident(id, factionId);
                    resident["displayName"] = index == 0 ? FactionHealerName : "Целитель " + (index + 1);
                    resident["residentRole"] = "healing_support";
                    resident["guardianId"] = AfterlifeWoundTreatmentAuthority.ElyaraId;
                    resident["abodeId"] = AfterlifeWoundTreatmentAuthority.ElyaraAbodeId;
                    residents["entries"]!.AsArray().Add(resident);
                    var actor = inputs.Profiles["profiles"]![index]!.DeepClone().AsObject();
                    actor["displayName"] = resident["displayName"]!.DeepClone(); actor["locationId"] = "hall_elyara_companion";
                    actor["healingServiceProfile"]!["locationRef"] = "hall_elyara_companion";
                    profiles["profiles"]!.AsArray().Add(actor);
                }
                GuardianAbodeResidentState.NormalizeShape(residents);
                shining["factions"] = new JsonArray(faction);
                ShiningAbodeState.NormalizeStateRoot(shining, residents);
                Assert.True(ShiningAbodeState.IsFactionLeaderless(faction));
                Assert.False(ShiningAbodeState.FactionHasAvailableTrade(faction, ShiningAbodeState.ComputeFactionStrength(faction, residents, 1)));
                await files.WriteFileAtomicAsync(lease, GuardianAbodeResidentState.StatePath, residents.ToJsonString());
                await files.WriteFileAtomicAsync(lease, ShiningAbodeState.StatePath, shining.ToJsonString());
                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            });

    [Theory]
    [InlineData(1, "NoImprovement")] [InlineData(5, "RecoveryPoint")] [InlineData(20, "ReduceTwo")]
    public Task FactionServiceChargesActualFailurePartialAndSuccessOnceAndReplays(int die, string band) =>
        RunAcceptedServiceAsync(die, band, "Shining Abode", faction: true);

    [Theory]
    [InlineData("role")] [InlineData("membership")] [InlineData("availability")] [InlineData("funds")]
    public async Task FactionConfirmationRejectsCurrentLossWithoutRequestPaymentOrWorldCycle(string change)
    {
        await using var fixture = await CreateFactionServiceFixtureAsync(2, 20);
        var files = fixture.Files;
        var state = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
        var choice = Assert.Single(state.Choices, c => c.Available && c.Selection.Service?.Quote.Provider.OwnerKind == "resident");
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var path = change == "funds" ? AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath : change == "availability"
            ? WoundCarrierCatalog.AfterlifeProfilesPath : GuardianAbodeResidentState.StatePath;
        var root = JsonNode.Parse((await files.ReadFileAsync(lease, path))!)!.AsObject();
        if (change == "funds") root["inkFeathers"]!["current"] = 50;
        else if (change == "availability") root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]!.GetValue<string>() == FactionHealerId)
            ["healingServiceProfile"]!["availability"] = "unavailable";
        else root["entries"]![0]![change == "role" ? "residentRole" : "shiningFactionId"] = change == "role" ? "archive_support" : "faction_foreign";
        await files.WriteFileAtomicAsync(lease, path, root.ToJsonString());
        var before = fixture.CaptureMutationRoots([AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath,
            GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath]);
        await Assert.ThrowsAsync<InvalidDataException>(() => WoundApplicationService.ConfirmAsync(files, lease, state, choice.Value));
        fixture.AssertMutationRootsUnchanged(before);
        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Null(await files.ReadFileBytesAsync(lease, PendingPlayerActionService.PendingPath));
        Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
    }

    [Fact]
    public async Task FactionStageRechecksActualRosterAfterControlBeforeAnyDraw()
    {
        FileSystemManager? files = null; var armed = false; var changes = 0; var draws = 0;
        await using var fixture = await CreateFactionServiceFixtureAsync(2, 20, new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed || path != ProgressionScheduleService.SchedulePath || !File.Exists(files!.ResolvePath(path))) return Task.CompletedTask;
                var schedule = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(File.ReadAllBytes(files.ResolvePath(path))))!;
                if (schedule["pendingChaosSeaCycles"]!.GetValue<int>() != 1) return Task.CompletedTask;
                armed = false; changes++;
                var rosterPath = files.ResolvePath(GuardianAbodeResidentState.StatePath);
                var roster = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(File.ReadAllBytes(rosterPath)))!;
                roster["entries"]![0]!["residentRole"] = "archive_support";
                File.WriteAllText(rosterPath, roster.ToJsonString());
                return Task.CompletedTask;
            }
        });
        files = fixture.Files;
        var state = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
        var choice = Assert.Single(state.Choices, c => c.Available && c.Selection.Service?.Quote.Provider.OwnerKind == "resident");
        var selected = choice.Selection with { Service = choice.Selection.Service! with { PlayerConfirmed = true } };
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        armed = true;
        var stage = await AfterlifeWoundTreatmentAuthority.StagePlayerServiceTurnAsync(files, lease,
            new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance),
            "session_spiritual_healing", "request_spiritual_healing", 43, selected, _ => { draws++; return [20]; });
        Assert.Equal(1, changes); Assert.False(stage.Stage.IsStaged); Assert.Equal(0, draws);
        Assert.Null(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(500, JsonNode.Parse((await files.ReadFileAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
        output.WriteLine("Actual roster changed after prepared control: zero draws/requests/payments/accepted cycles. Outer Engine still owns prepared-control rollback.");
    }

    [Fact]
    public async Task FactionMenuOpensTheSameSourcesForOneOrTwentyActualProviders()
    {
        Dictionary<string, int>? baseline = null;
        foreach (var count in new[] { 1, 20 })
        {
            var armed = false; var reads = new Dictionary<string, int>(StringComparer.Ordinal);
            await using var fixture = await CreateFactionServiceFixtureAsync(2, 20, providers: count, hooks: new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path => { if (armed) reads[path] = reads.GetValueOrDefault(path) + 1; return Task.CompletedTask; }
            });
            var before = fixture.CaptureMutationRoots([GuardianAbodeResidentState.StatePath, ShiningAbodeState.StatePath,
                AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath]);
            armed = true;
            var state = (await WoundApplicationService.OpenAsync(fixture.Files, "/лечить", treatment: true)).WoundCommandState!;
            armed = false;
            Assert.Equal(count, state.Choices.Count(c => c.Available && c.Selection.Service?.Quote.Provider.OwnerKind == "resident"));
            Assert.Equal(1, reads.GetValueOrDefault(GuardianAbodeResidentState.StatePath));
            Assert.True(reads.GetValueOrDefault(WoundCarrierCatalog.AfterlifeProfilesPath) > 0);
            if (baseline is null) baseline = new(reads, StringComparer.Ordinal);
            else Assert.Equal(baseline.OrderBy(p => p.Key), reads.OrderBy(p => p.Key));
            fixture.AssertMutationRootsUnchanged(before);
            output.WriteLine(JsonSerializer.Serialize(new { providers = count, actualSourceOpens = reads }));
        }
    }
}
