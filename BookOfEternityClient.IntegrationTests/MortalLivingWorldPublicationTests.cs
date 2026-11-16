using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Real ordinary canonical owner, genuine live original, independent per-test filesystem.</summary>
public sealed class MortalLivingWorldPublicationTests
{
    private const string Factions = "game_state/factions/faction_core.json";
    private const string Session = "living-publication";
    private const string Request = "living-publication-request";

    [Fact]
    public async Task OrdinaryPublication_MissingAssessmentRefusesBeforeReducerMutation()
    {
        await using var context = await PrepareAsync();
        var backups = await BackupsAsync(context);
        await SubmitAsync(context, report: null);
        var before = await CaptureAsync(context);

        var result = await PublishAsync(context, backups);

        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_report_required");
        await AssertBytesAsync(context, before);
    }

    [Fact]
    public async Task OrdinaryPublication_NoChangePublishesPrivateSourceBoundReceiptAndConsumesReport()
    {
        await using var context = await PrepareAsync();
        var backups = await BackupsAsync(context);
        await SubmitAsync(context, Report());

        var result = await PublishAsync(context, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var history = Assert.IsType<JsonObject>(await context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath));
        Assert.Equal(1, history["schemaVersion"]!.GetValue<int>());
        var entry = Assert.Single(history["entries"]!.AsArray());
        Assert.Equal(Session, entry!["sessionId"]!.GetValue<string>());
        Assert.Equal(Request, entry["requestId"]!.GetValue<string>());
        Assert.Equal(42, entry["turn"]!.GetValue<int>());
        Assert.Equal("noSignificantChange", entry["report"]!["resolution"]!.GetValue<string>());
        Assert.Equal((await context.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!["manifestPayloadHash"]!.GetValue<string>(),
            entry["snapshotToken"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(MortalLivingWorldSource.ReportPath));
        Assert.Null(await context.ReadJsonAsync("game_state/world/world_events.json"));
        Assert.Equal("defend", (await context.ReadJsonAsync(Factions))!["factions"]![0]!["currentAgenda"]!.GetValue<string>());
        Assert.Null((await context.ReadJsonAsync(Factions))!["factionDataChanges"]);
    }

    [Fact]
    public async Task OrdinaryPublication_DeclaredEffectUsesActualReducedFactionNotPendingCommand()
    {
        await using var context = await PrepareAsync();
        var backups = await BackupsAsync(context);
        await SubmitAsync(context, Report(changes: true));

        var result = await PublishAsync(context, backups);

        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal("defend", (await context.ReadJsonAsync(Factions))!["factions"]![0]!["currentAgenda"]!.GetValue<string>());
        var entry = Assert.Single((await context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
        Assert.Equal("council-response", entry!["report"]!["events"]![0]!["eventId"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(MortalLivingWorldSource.ReportPath));
    }

    [Fact]
    public async Task OrdinaryPublication_FalseFinalEffectRestoresReducerInputAndReceiptAbsence()
    {
        await using var context = await PrepareAsync();
        var backups = await BackupsAsync(context);
        var report = Report(changes: true);
        report["events"]![0]!["effects"]![0]!["after"]!["value"] = "not the canonical result";
        await SubmitAsync(context, report);
        var before = await CaptureAsync(context);

        var result = await PublishAsync(context, backups);

        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_effect_mismatch");
        await AssertBytesAsync(context, before);
    }

    [Fact]
    public async Task OrdinaryPublication_ReachedLateFailureRestoresExactBytesAndRetryAppendsOnce()
    {
        var armed = false;
        var reached = false;
        EffectMaterializationTestContext? current = null;
        const string failureMarker = "living-world reached published-history late read";
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed || reached || path != MortalLivingWorldSource.HistoryPath || current is null ||
                    !File.Exists(current.FileSystem.ResolvePath(MortalLivingWorldSource.HistoryPath)) ||
                    File.Exists(current.FileSystem.ResolvePath(MortalLivingWorldSource.ReportPath))) return Task.CompletedTask;
                var history = JsonNode.Parse(File.ReadAllText(current.FileSystem.ResolvePath(MortalLivingWorldSource.HistoryPath)))!;
                Assert.Single(history["entries"]!.AsArray());
                var factions = JsonNode.Parse(File.ReadAllText(current.FileSystem.ResolvePath(Factions)))!;
                Assert.Equal("defend", factions["factions"]![0]!["currentAgenda"]!.GetValue<string>());
                Assert.Null(factions["factionDataChanges"]);
                reached = true;
                throw new InvalidOperationException(failureMarker);
            }
        };
        await using var context = await PrepareAsync(hooks);
        current = context;
        var backups = await BackupsAsync(context);
        await SubmitAsync(context, Report(changes: true));
        var before = await CaptureAsync(context);

        armed = true;
        var failure = await Record.ExceptionAsync(() => PublishAsync(context, backups));
        armed = false;

        Assert.True(reached, "The real owner must publish history, consume the report and reduce the effect before the injected failure.");
        Assert.NotNull(failure);
        Assert.Contains(failureMarker, failure!.ToString(), StringComparison.Ordinal);
        await AssertBytesAsync(context, before);
        var retry = await PublishAsync(context, backups);
        Assert.DoesNotContain(retry.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Single((await context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
    }

    [Fact]
    public async Task OrdinaryPublication_TamperedClientHistoryRefusesWithoutMutation()
    {
        await using var context = await PrepareAsync();
        var backups = await BackupsAsync(context);
        await SubmitAsync(context, Report());
        await context.WriteJsonAsync(MortalLivingWorldSource.HistoryPath,
            new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() });
        var before = await CaptureAsync(context);

        var result = await PublishAsync(context, backups);

        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_history_mutation");
        await AssertBytesAsync(context, before);
    }

    [Fact]
    public async Task OrdinaryPublication_ClockChangingAtReducerBoundaryCannotUseOpeningTimeAssessment()
    {
        var armed = false;
        var reached = false;
        EffectMaterializationTestContext? current = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = path =>
            {
                if (!armed || reached || path != Factions) return Task.CompletedTask;
                reached = true;
                File.WriteAllText(current!.FileSystem.ResolvePath(MortalLivingWorldSource.WorldTimePath),
                    "{\"currentTimeInMinutes\":480}");
                return Task.CompletedTask;
            }
        };
        await using var context = await PrepareAsync(hooks);
        current = context;
        var backups = await BackupsAsync(context);
        await SubmitAsync(context, Report(changes: true));
        var before = await CaptureAsync(context);

        armed = true;
        var result = await PublishAsync(context, backups);
        armed = false;

        Assert.True(reached, "The actual faction reducer must be reached before evaluating settled time.");
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_window_invalid");
        await AssertBytesAsync(context, before);
    }

    private static async Task<EffectMaterializationTestContext> PrepareAsync(FileSystemManagerHooks? hooks = null)
    {
        var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            context.FileSystem.GetOrCreateSessionGeneration(lease);
        await context.WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 0 });
        await context.WriteJsonAsync(Factions, new JsonObject { ["factions"] = new JsonArray(Faction("wait")) });
        var scheduler = new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
        var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = Session, RequestId = Request, TurnNumber = 42, CurrentRealm = "Mortal World",
            PlayerAction = "Wait for world activity.", ProgressionControl = control, PreGeneratedDices1d20 = [7]
        });
        return context;
    }

    private static async Task SubmitAsync(EffectMaterializationTestContext context, JsonObject? report)
    {
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 240 });
        await context.WriteJsonAsync(Factions, new JsonObject { ["factionDataChanges"] = new JsonArray(Faction("defend")) });
        if (report is not null) await context.WriteJsonAsync(MortalLivingWorldSource.ReportPath, report);
    }

    private static JsonObject Faction(string agenda) => new() { ["factionId"] = "council", ["currentAgenda"] = agenda };

    private static JsonObject Report(bool changes = false)
    {
        var audit = new JsonArray();
        foreach (var scope in new[] { "npcs", "factions", "locations", "projects", "world" })
            audit.Add(new JsonObject { ["scope"] = scope, ["reason"] = "Reviewed current relevant state; no other significant autonomous change." });
        var events = new JsonArray();
        if (changes) events.Add(new JsonObject
        {
            ["eventId"] = "council-response", ["fromMinute"] = 0, ["toMinute"] = 240,
            ["actors"] = new JsonArray(Reference()), ["targets"] = new JsonArray(Reference()),
            ["cause"] = new JsonObject { ["summary"] = "Council responds to elapsed time.", ["references"] = new JsonArray(Reference()) },
            ["effects"] = new JsonArray(new JsonObject
            {
                ["effectId"] = "agenda", ["target"] = Reference(), ["operation"] = "update", ["field"] = "currentAgenda",
                ["before"] = new JsonObject { ["present"] = true, ["value"] = "wait" },
                ["after"] = new JsonObject { ["present"] = true, ["value"] = "defend" }
            }),
            ["consequences"] = new JsonArray(new JsonObject { ["summary"] = "Council pursues defense.", ["effectIds"] = new JsonArray("agenda") }),
            ["knowledge"] = new JsonObject { ["player"] = "hidden", ["newsReferences"] = new JsonArray() }
        });
        return new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = Session, ["requestId"] = Request, ["turn"] = 42,
            ["fromMinute"] = 0, ["toMinute"] = 240, ["resolution"] = changes ? "changes" : "noSignificantChange",
            ["audit"] = audit, ["events"] = events
        };
        static JsonObject Reference() => new() { ["kind"] = "faction", ["id"] = "council" };
    }

    private static async Task<IReadOnlyDictionary<string, string>> BackupsAsync(EffectMaterializationTestContext context) =>
        (await context.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!["files"]!.AsObject()
            .ToDictionary(pair => pair.Key, pair => pair.Value!.GetValue<string>(), StringComparer.Ordinal);

    private static Task<AcceptedTurnCanonicalStateRefresh.Result> PublishAsync(EffectMaterializationTestContext context,
        IReadOnlyDictionary<string, string> backups) => AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
        context.FileSystem, context.Normalizer, context.Validator, backups);

    private static async Task<Dictionary<string, byte[]?>> CaptureAsync(EffectMaterializationTestContext context)
    {
        var paths = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(MortalLivingWorldSource.ObservedPaths)
            .Concat([LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath, "input/turn_request.json"])
            .Distinct(StringComparer.Ordinal);
        var values = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths) values.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
        return values;
    }

    private static async Task AssertBytesAsync(EffectMaterializationTestContext context, IReadOnlyDictionary<string, byte[]?> expected)
    {
        foreach (var pair in expected) Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(pair.Key));
    }
}
