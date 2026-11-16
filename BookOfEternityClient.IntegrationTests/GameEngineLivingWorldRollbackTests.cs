using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Real engine original/rollback owners and their repair obligation collector; not the interactive repair loop.</summary>
public sealed partial class GameEngineLivingWorldRollbackTests
{
    private const string Session = "living-world-engine-rollback";
    private const string Request = "living-world-engine-request";
    private const string Factions = "game_state/factions/faction_core.json";

    [Fact]
    public async Task RepairObligations_RequirePendingGmReportButNeverClientPublishedHistory()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SubmitAsync();
        var pending = await fixture.ObligationsAsync();
        Assert.Contains(pending, row => row.Path == MortalLivingWorldSource.ReportPath && row.RejectedExists);
        Assert.DoesNotContain(pending, row => row.Path == MortalLivingWorldSource.HistoryPath);

        await fixture.PublishSuccessfullyAsync();
        Assert.Single((await fixture.Context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
        Assert.Null(await fixture.Files.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));
        var published = await fixture.ObligationsAsync();

        Assert.DoesNotContain(published, row => row.Path == MortalLivingWorldSource.HistoryPath);
        Assert.DoesNotContain(published, row => row.Path == MortalLivingWorldSource.ReportPath);
        Assert.Contains(published, row => row.Path == Factions);
    }

    [Fact]
    public async Task OuterRepairRollback_AfterPublicationRestoresExactOriginalAndRequiresFreshReportBeforeRetry()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SubmitAsync();
        await fixture.PublishSuccessfullyAsync();
        Assert.Single((await fixture.Context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
        Assert.Null(await fixture.Files.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));
        Assert.Equal("defend", (await fixture.Context.ReadJsonAsync(Factions))!["factions"]![0]!["currentAgenda"]!.GetValue<string>());

        // The engine-owned outer compensation boundary after a later turn failure,
        // not the normalizer's local before-images and not a fabricated receipt.
        await fixture.Runtime.Engine.RestoreOriginalForRepairAsync(fixture.Rollback, fixture.Generation);
        fixture.AssertExactOriginal();
        Assert.Null(await fixture.Files.ReadFileBytesAsync(MortalLivingWorldSource.HistoryPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));

        await fixture.SubmitAsync(includeReport: false);
        var rejected = await fixture.PublishAsync();
        Assert.Contains(rejected.Issues, row => row.Code == "mortal_living_world_report_required");
        Assert.Null(await fixture.Files.ReadFileBytesAsync(MortalLivingWorldSource.HistoryPath));
        await fixture.SubmitAsync();
        await fixture.PublishSuccessfullyAsync();
        Assert.Single((await fixture.Context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
        Assert.Null(await fixture.Files.ReadFileBytesAsync(MortalLivingWorldSource.ReportPath));
    }

    [Fact]
    public async Task SuccessfulSameOriginalRepeat_IsRefusedWithoutDuplicateOrMutation()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SubmitAsync();
        await fixture.PublishSuccessfullyAsync();
        var accepted = Capture(fixture.Files);

        var repeated = await fixture.PublishAsync();

        Assert.Contains(repeated.Issues, row => row.Code == "mortal_living_world_history_mutation");
        AssertExact(accepted, fixture.Files);
        Assert.Single((await fixture.Context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
        // This is a negative repeated-publication contract. It does not claim
        // the engine's in-place critical-health repair reentry is supported.
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal required EffectMaterializationTestContext Context { get; init; }
        internal required GameEngineTestRuntime Runtime { get; init; }
        internal required GameEngine.RollbackSnapshot Rollback { get; init; }
        internal required Dictionary<string, string> Backups { get; init; }
        internal required Dictionary<string, byte[]> Original { get; init; }
        internal required string Generation { get; init; }
        internal required ProgressionControl Progression { get; init; }
        internal FileSystemManager Files => Context.FileSystem;

        internal static async Task<Fixture> CreateAsync(FileSystemManagerHooks? hooks = null, IConsoleInputSource? input = null, bool criticalRepair = false)
        {
            var context = await EffectMaterializationTestContext.CreateAsync(hooks);
            var files = context.FileSystem;
            string generation;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                generation = files.GetOrCreateSessionGeneration(lease);
            await context.WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
            await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
            await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 0 });
            await context.WriteJsonAsync(Factions, new JsonObject { ["factions"] = new JsonArray(Faction("wait")) });
            foreach (var pair in new Dictionary<string, JsonObject>
            {
                [AfterlifeEntityProfileState.StatePath] = AfterlifeEntityProfileState.CreateDefaultRoot(),
                [AfterlifeSpiritualConflictState.StatePath] = AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                [AfterlifeChronicleState.StatePath] = AfterlifeChronicleState.CreateDefaultRoot(),
                [AfterlifeGlobalFlagState.StatePath] = AfterlifeGlobalFlagState.CreateDefaultRoot()
            })
                if (await files.ReadFileBytesAsync(pair.Key) is null) await context.WriteJsonAsync(pair.Key, pair.Value);
            if (criticalRepair) await context.WriteJsonAsync("lore/codex_entries.json", new JsonObject());
            var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
            var request = new TurnRequest
            {
                SessionId = Session, RequestId = Request, TurnNumber = 42, PlayerAction = "Wait for world activity.",
                PreGeneratedDices1d20 = [7], ProgressionControl = await scheduler.BuildControlForNextTurnAsync("Mortal World")
            };
            var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, inputSource: input, configureSettings: settings =>
            { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
            runtime.GameLoop.SetSession(Session, 41);
            var original = Capture(files);
            var rollback = await runtime.Engine.CaptureOriginalRollbackAsync("living-world-outer-repair");
            await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath,
                JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            var backups = await runtime.Engine.CaptureOriginalValidationSnapshotAsync(request, rollback, "living-world-outer-repair");
            return new Fixture { Context = context, Runtime = runtime, Rollback = rollback, Backups = backups,
                Original = original, Generation = generation, Progression = request.ProgressionControl! };
        }

        internal async Task SubmitAsync(bool includeReport = true)
        {
            await Context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 240 });
            await Context.WriteJsonAsync(Factions, new JsonObject { ["factionDataChanges"] = new JsonArray(Faction("defend")) });
            if (!includeReport) return;
            var audit = new JsonArray();
            foreach (var scope in new[] { "npcs", "factions", "locations", "projects", "world" })
                audit.Add(new JsonObject { ["scope"] = scope, ["reason"] = "Reviewed relevant state; no significant autonomous change." });
            await Context.WriteJsonAsync(MortalLivingWorldSource.ReportPath, new JsonObject
            {
                ["schemaVersion"] = 1, ["sessionId"] = Session, ["requestId"] = Request, ["turn"] = 42,
                ["fromMinute"] = 0, ["toMinute"] = 240, ["resolution"] = "noSignificantChange",
                ["audit"] = audit, ["events"] = new JsonArray()
            });
        }

        internal Task<AcceptedTurnCanonicalStateRefresh.Result> PublishAsync() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(Files, Context.Normalizer, Context.Validator, Backups);
        internal async Task PublishSuccessfullyAsync() =>
            Assert.DoesNotContain((await PublishAsync()).Issues, row => row.Severity == IssueSeverity.Error);
        internal Task<IReadOnlyList<GameEngine.RepairResubmissionPathObligation>> ObligationsAsync() =>
            Runtime.Engine.CaptureChangedRollbackTrackedPathsForRepairSessionAsync(Rollback, Generation);
        internal void AssertExactOriginal() => AssertExact(Original, Files);
        public async ValueTask DisposeAsync()
        {
            await Runtime.AudioService.DisposeAsync();
            await Context.DisposeAsync();
        }
    }

    private static JsonObject Faction(string agenda) => new() { ["factionId"] = "council", ["currentAgenda"] = agenda };
    private static Dictionary<string, byte[]> Capture(FileSystemManager files) =>
        GameEngine.EnumerateOriginalBrowserRollbackTrackedFiles(
            FileSystemManager.EnumerateFilesWithoutFollowingReparsePoints(files.GameSessionPath, "*")
                .Select(path => Path.GetRelativePath(files.GameSessionPath, path).Replace(Path.DirectorySeparatorChar, '/')).ToArray())
            .ToDictionary(path => path, path => File.ReadAllBytes(files.ResolvePath(path)), StringComparer.Ordinal);
    private static void AssertExact(IReadOnlyDictionary<string, byte[]> expected, FileSystemManager files)
    {
        var actual = Capture(files);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
