using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLivingWorldPublicationTests
{
    private const string CompletionCore = "game_state/npcs/npc_core.json";
    private const string CompletionCommands = "game_state/npcs/npc_activities.json";

    [Fact]
    public async Task OrdinaryNpcCompletion_AuthenticatedOriginalWithoutProgressionPublishesOnce()
    {
        await using var context = await PrepareCompletionAsync();
        var backups = await BackupsAsync(context);
        var original = (await context.ReadJsonAsync(CompletionCore))!.DeepClone();
        await SubmitCompletionAsync(context);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var progression = await context.Validator.PrepareNpcProgressionPublicationAsync(lease,
                await context.FileSystem.ReadFileBytesAsync(lease, CompletionCore));
            Assert.Empty(progression.Issues);
            Assert.Null(progression.Context); // This must not accidentally become completion authority.
        }
        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        await AssertCompletionAsync(context, original);
        var completedBytes = await context.FileSystem.ReadFileBytesAsync(CompletionCore);
        var repeated = await PublishAsync(context, backups);
        Assert.DoesNotContain(repeated.Issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.Equal(completedBytes, await context.FileSystem.ReadFileBytesAsync(CompletionCore));
    }

    [Fact]
    public async Task GenericNpcCompletion_SignedFilesAndBackupsDoNotGrantPublication()
    {
        await using var context = await PrepareCompletionAsync();
        var backups = await BackupsAsync(context);
        await SubmitCompletionAsync(context);
        var before = await CaptureAsync(context);
        await Assert.ThrowsAsync<InvalidDataException>(() => context.Normalizer.NormalizeAccumulatedStateAsync(backups));
        await AssertBytesAsync(context, before);
    }

    [Fact]
    public async Task OrdinaryNpcCompletion_ReachedConsumptionFailureRestoresAndRetryAppendsOnce()
    {
        var armed = false;var reached = false;
        EffectMaterializationTestContext? current = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed || reached || current is null || path != CompletionCommands) return Task.CompletedTask;
                var core = JsonNode.Parse(File.ReadAllText(current.FileSystem.ResolvePath(CompletionCore)).TrimStart('\uFEFF'))!;
                if (core["NPCsInScene"]![0]!["currentActivity"] is not null) return Task.CompletedTask;
                Assert.Equal(2, core["NPCsInScene"]![0]!["completedActivities"]!.AsArray().Count);
                reached = true;
                throw new InvalidOperationException("npc-terminal-after-core-before-consumption");
            }
        };
        await using var context = await PrepareCompletionAsync(hooks);current = context;
        var original = (await context.ReadJsonAsync(CompletionCore))!.DeepClone();
        var backups = await BackupsAsync(context);
        await SubmitCompletionAsync(context);
        var before = await CaptureAsync(context);armed = true;
        var failure = await Record.ExceptionAsync(() => PublishAsync(context, backups));armed = false;
        Assert.True(reached, "Must reach actual completed canonical core before the injected read failure.");
        Assert.NotNull(failure);
        Assert.Contains("npc-terminal-after-core-before-consumption", failure!.ToString());
        await AssertBytesAsync(context, before);
        var retry = await PublishAsync(context, backups);
        Assert.DoesNotContain(retry.Issues, issue => issue.Severity == IssueSeverity.Error);
        await AssertCompletionAsync(context, original);
    }

    private static async Task<EffectMaterializationTestContext> PrepareCompletionAsync(FileSystemManagerHooks? hooks = null)
    {
        var context = await EffectMaterializationTestContext.CreateAsync(hooks);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync()) context.FileSystem.GetOrCreateSessionGeneration(lease);
        await context.WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
        await context.WriteJsonAsync(MortalItemIdentityState.StatePath, MortalItemIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, new JsonObject { ["currentTimeInMinutes"] = 0 });
        var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor("terminal_scout");
        actor["NPCId"] = "terminal_scout";actor.Remove("initialId");actor["name"] = "Mira";
        actor["materialization"]!["materializedAtTurn"] = 40;
        actor["currentActivity"] = new JsonObject { ["activityName"] = "Survey", ["description"] = "Inspect crossing", ["activeState"] = "Active", ["totalTimeCostMinutes"] = 480, ["timeSpentMinutes"] = 240, ["currentStepNumber"] = 1, ["totalStepsInActivity"] = 2, ["linkedQuestId"] = null, ["linkedPlotOutlineNode"] = null };
        actor["completedActivities"] = new JsonArray(new JsonObject { ["activityName"] = "Earlier survey", ["completionTurn"] = 39, ["finalOutcome"] = "Success", ["narrativeSummary"] = "Earlier result." });
        actor[NpcTradeRequestState.ReceiptsProperty] = new JsonArray();
        await context.WriteJsonAsync(CompletionCore, new JsonObject { ["UpdateNPCs"] = new JsonArray(actor.DeepClone()), ["NPCsInScene"] = new JsonArray(actor) });
        var scheduler = new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance);
        var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
        { SessionId = Session, RequestId = Request, TurnNumber = 42, CurrentRealm = "Mortal World", PlayerAction = "Receive the survey result.", ProgressionControl = control, PreGeneratedDices1d20 = [7] });
        return context;
    }

    private static Task SubmitCompletionAsync(EffectMaterializationTestContext context) => context.WriteJsonAsync(CompletionCommands,
        new JsonObject { ["completeNPCActivities"] = new JsonArray(new JsonObject { ["NPCId"] = "terminal_scout", ["NPCName"] = "Mira", ["activityName"] = "Survey", ["finalState"] = "Completed", ["finalOutcome"] = "SuccessWithComplication", ["narrativeSummary"] = "The crossing is mapped, but unsafe." }) });

    private static async Task AssertCompletionAsync(EffectMaterializationTestContext context, JsonNode original)
    {
        var expected = original.DeepClone();
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            var actor = expected[section]![0]!;actor["currentActivity"] = null;
            actor["completedActivities"]!.AsArray().Add(new JsonObject { ["activityName"] = "Survey", ["completionTurn"] = 42, ["finalOutcome"] = "SuccessWithComplication", ["narrativeSummary"] = "The crossing is mapped, but unsafe." });
        }
        Assert.True(JsonNode.DeepEquals(expected, await context.ReadJsonAsync(CompletionCore)), "Only original activity/history may change, in both agreeing mirrors.");
        var commands = await context.ReadJsonAsync(CompletionCommands);
        Assert.True(commands is null || commands["completeNPCActivities"] is null || commands["completeNPCActivities"] is JsonArray { Count: 0 });
    }
}
