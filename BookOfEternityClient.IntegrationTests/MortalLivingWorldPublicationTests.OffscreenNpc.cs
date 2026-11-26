using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLivingWorldPublicationTests
{
    private static readonly GameStateValidationSelection OffscreenNpcFinalSelection = new(
        GameStateValidationPhase.AcceptedTurnActorMaterializationCompleteness |
        GameStateValidationPhase.NpcStateFiles);

    [Fact]
    public async Task OrdinaryPublication_ReducedOffscreenNpcLocationAndActivityRetainHistoricalCarriers()
    {
        await using var context = await PrepareOffscreenNpcOrdinaryAsync();
        var backups = await BackupsAsync(context);
        var original = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        var raw = original.DeepClone().AsObject();
        raw["NPCCoreChanges"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = "npc_journey_courier",
            ["reason"] = "The courier followed the already accepted footpath.",
            ["location"] = new JsonObject { ["currentLocationId"] = "loc_journey_forest", ["initialLocationId"] = null }
        });
        await context.WriteJsonAsync(CompletionCore, raw);
        await context.WriteJsonAsync(CompletionCommands, new JsonObject
        {
            ["NPCActivityUpdates"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_journey_captain",
                ["NPCName"] = "Captain Irina",
                ["activityUpdate"] = new JsonObject { ["timeSpentMinutes"] = 60, ["currentStepNumber"] = 1 }
            })
        });
        var clock = (await context.ReadJsonAsync(MortalLivingWorldSource.WorldTimePath))!.AsObject();
        clock["currentTimeInMinutes"] = 540;
        clock["timeOfDay"] = "09:00";
        await context.WriteJsonAsync(MortalLivingWorldSource.WorldTimePath, clock);
        Assert.DoesNotContain(await context.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync(),
            issue => issue.Severity == IssueSeverity.Error);

        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        var settled = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        Assert.Empty(settled["NPCsInScene"]!.AsArray());
        Assert.Null(settled["NPCCoreChanges"]);
        var expected = original.DeepClone();
        expected["UpdateNPCs"]![0]!["currentActivity"]!["timeSpentMinutes"] = 60;
        expected["UpdateNPCs"]![0]!["currentActivity"]!["currentStepNumber"] = 1;
        expected["UpdateNPCs"]![1]!["currentLocationId"] = "loc_journey_forest";
        Assert.True(JsonNode.DeepEquals(expected, settled), "Both reducers must reach the exact canonical image; no fixture after-image is installed.");
        Assert.Null((await context.ReadJsonAsync(CompletionCommands))?["NPCActivityUpdates"]);

        var issues = await context.Validator.ValidateGameStateAsync(OffscreenNpcFinalSelection);
        Assert.DoesNotContain(issues, issue => IsHistoricalOffscreenResendIssue(issue.Code));
    }

    [Theory]
    [InlineData("raw-location")]
    [InlineData("raw-envelope")]
    [InlineData("raw-inventory")]
    public async Task OrdinaryPublication_AuthoredOffscreenNpcRowsAreNotCertifiedAsSettled(string mutation)
    {
        await using var context = await PrepareOffscreenNpcOrdinaryAsync();
        var raw = (await context.ReadJsonAsync(CompletionCore))!.AsObject();
        var actor = raw["UpdateNPCs"]![0]!;
        switch (mutation)
        {
            case "raw-location": actor["currentLocationId"] = "loc_journey_forest"; break;
            case "raw-envelope": actor["materialization"]!["materializationId"] = "mat_replacement"; break;
            case "raw-inventory": actor["inventory"]!.AsArray().Add(new JsonObject { ["itemId"] = "unauthorized_item" }); break;
        }
        await context.WriteJsonAsync(CompletionCore, raw);
        var issues = await context.Validator.ValidateGameStateAsync(OffscreenNpcFinalSelection);
        Assert.Contains(issues, issue => IsHistoricalOffscreenResendIssue(issue.Code));
    }

    private static bool IsHistoricalOffscreenResendIssue(string? code) => code is
        "actor_materialization_historical_envelope_changed" or
        "actor_materialization_existing_resend_forbidden" or
        "npc_existing_inventory_resend_forbidden";

    private static async Task<EffectMaterializationTestContext> PrepareOffscreenNpcOrdinaryAsync()
    {
        var context = await EffectMaterializationTestContext.CreateAsync();
        try
        {
            await MortalLiveSeedBuilder.PrepareAsync(context.FileSystem, TestRepoPaths.BaseSessionRoot,
                MortalLiveSeedBuilder.SeedOffscreenActorsAsync, stateOnly: true);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                context.FileSystem.GetOrCreateSessionGeneration(lease);
            var scheduler = new ProgressionScheduleService(context.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance);
            var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
            await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = Session, RequestId = Request, TurnNumber = 42, CurrentRealm = "Mortal World",
                PlayerAction = "Wait in the private chambers while other people act.",
                ProgressionControl = control, PreGeneratedDices1d20 = [7]
            });
            return context;
        }
        catch { await context.DisposeAsync(); throw; }
    }
}
