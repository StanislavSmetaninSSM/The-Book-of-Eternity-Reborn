using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLivingWorldPublicationTests
{
    private const string Projects = "game_state/factions/faction_projects.json";
    private const string Flags = "game_state/world/world_flags.json";

    [Fact]
    public async Task OrdinaryLifecycle_ProjectBirthUsesExistingFactionProjectReducer()
    {
        await using var context = await PrepareLifecycleAsync(false);
        var backups = await BackupsAsync(context);
        Assert.Null(await context.ReadJsonAsync(Projects));
        var project = LifecycleProject();
        await SubmitLifecycleProjectBirthAsync(context, project, LifecycleReport(true, null, project));
        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var published = await context.ReadJsonAsync(Projects);
        Assert.True(JsonNode.DeepEquals(project, Assert.Single(published!["activeProjects"]!.AsArray())));
        Assert.Empty(published["completedProjects"]!.AsArray());
        Assert.Null((await context.ReadJsonAsync(Factions))!["factionDataChanges"]);
        Assert.Single((await context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
    }

    [Fact]
    public async Task OrdinaryLifecycle_ProjectCompletionUsesExistingCompletionCommand()
    {
        await using var context = await PrepareLifecycleAsync(true);
        var backups = await BackupsAsync(context);
        var before = LifecycleProject(); var after = LifecycleProject();
        after.Remove("activeState"); after["finalState"] = "Completed"; after["completionTurn"] = 42;
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 240 });
        await context.WriteJsonAsync(Projects, new JsonObject { ["completeFactionProjects"] = new JsonArray(new JsonObject
        { ["factionId"] = "council", ["factionName"] = "Council", ["projectId"] = "bridge", ["projectName"] = "Bridge", ["finalState"] = "Completed", ["completionTurn"] = 42 }) });
        await context.WriteJsonAsync(MortalLivingWorldSource.ReportPath, LifecycleReport(true, before, after));
        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var published = await context.ReadJsonAsync(Projects);
        Assert.Empty(published!["activeProjects"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(after, Assert.Single(published["completedProjects"]!.AsArray())));
        Assert.Null(published["completeFactionProjects"]);
        Assert.Single((await context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
    }

    [Fact]
    public async Task OrdinaryLifecycle_FalseProjectBirthEvidenceRestoresReducerInputAndReceiptAbsence()
    {
        await using var context = await PrepareLifecycleAsync(false);
        var backups = await BackupsAsync(context);
        var project = LifecycleProject(); var falseAfter = LifecycleProject(); falseAfter["currentStep"] = 3;
        await SubmitLifecycleProjectBirthAsync(context, project, LifecycleReport(true, null, falseAfter));
        var before = await CaptureAsync(context);
        var result = await PublishAsync(context, backups);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_lifecycle_invalid");
        await AssertBytesAsync(context, before);
        Assert.Null(await context.ReadJsonAsync(Projects));
        Assert.Null(await context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath));
    }

    [Fact]
    public async Task OrdinaryLifecycle_FirstWorldFlagUsesExistingCanonicalSourceRoute()
    {
        // Flags have no separate normalizer reducer: this is the existing canonical submission route.
        await using var context = await PrepareLifecycleAsync(false);
        var backups = await BackupsAsync(context);
        Assert.Null(await context.ReadJsonAsync(Flags));
        var flag = new JsonObject { ["flagId"] = "bridge_open", ["displayName"] = "Bridge open", ["description"] = "Crossing is available", ["value"] = true };
        await context.WriteJsonAsync(Flags, new JsonObject { ["worldStateFlags"] = new JsonArray(flag.DeepClone()) });
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 240 });
        await context.WriteJsonAsync(MortalLivingWorldSource.ReportPath, LifecycleReport(false, null, flag));
        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.True(JsonNode.DeepEquals(flag, Assert.Single((await context.ReadJsonAsync(Flags))!["worldStateFlags"]!.AsArray())));
        Assert.Single((await context.ReadJsonAsync(MortalLivingWorldSource.HistoryPath))!["entries"]!.AsArray());
    }

    private static async Task<EffectMaterializationTestContext> PrepareLifecycleAsync(bool existingProject)
    {
        var context = await EffectMaterializationTestContext.CreateAsync();
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            context.FileSystem.GetOrCreateSessionGeneration(lease);
        await context.WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 0 });
        await context.WriteJsonAsync(Factions, new JsonObject { ["factions"] = new JsonArray(new JsonObject { ["factionId"] = "council", ["name"] = "Council", ["currentAgenda"] = "wait" }) });
        if (existingProject) await context.WriteJsonAsync(Projects, new JsonObject { ["activeProjects"] = new JsonArray(LifecycleProject()), ["completedProjects"] = new JsonArray() });
        var scheduler = new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
        var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
        { SessionId = Session, RequestId = Request, TurnNumber = 42, CurrentRealm = "Mortal World", PlayerAction = "Wait for world activity.", ProgressionControl = control, PreGeneratedDices1d20 = [7] });
        return context;
    }
    private static JsonObject LifecycleProject() => new()
    { ["factionId"] = "council", ["factionName"] = "Council", ["projectId"] = "bridge", ["projectName"] = "Bridge", ["activeState"] = "Active", ["description"] = "Build bridge",
      ["totalResourceCost"] = new JsonArray(), ["resourcesSpent"] = new JsonArray(), ["totalTimeCostMinutes"] = 240, ["timeSpentMinutes"] = 0, ["totalSteps"] = 4, ["currentStep"] = 0 };
    private static async Task SubmitLifecycleProjectBirthAsync(EffectMaterializationTestContext context, JsonObject project, JsonObject report)
    {
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 240 });
        var faction = new JsonObject { ["factionId"] = "council", ["name"] = "Council", ["currentAgenda"] = "wait", ["activeProjects"] = new JsonArray(project.DeepClone()), ["completedProjects"] = new JsonArray() };
        await context.WriteJsonAsync(Factions, new JsonObject { ["factionDataChanges"] = new JsonArray(faction) });
        await context.WriteJsonAsync(MortalLivingWorldSource.ReportPath, report);
    }
    private static JsonObject LifecycleReport(bool project, JsonObject? before, JsonObject after)
    {
        var report = Report(changes: true); var ev = report["events"]![0]!;
        JsonObject Target() => project ? new() { ["kind"] = "factionProject", ["id"] = "bridge", ["ownerId"] = "council" }
            : new() { ["kind"] = "worldFlag", ["id"] = "bridge_open" };
        ev["targets"] = new JsonArray(Target());
        var effect = ev["effects"]![0]!; effect["target"] = Target(); effect["operation"] = before is null ? "create" : "complete"; effect["field"] = "entity";
        effect["before"] = before is null ? new JsonObject { ["present"] = false } : new JsonObject { ["present"] = true, ["value"] = before.DeepClone() };
        effect["after"] = new JsonObject { ["present"] = true, ["value"] = after.DeepClone() };
        return report;
    }
}
