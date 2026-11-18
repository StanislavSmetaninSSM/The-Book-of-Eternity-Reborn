using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLivingWorldPublicationTests
{
    // These calls enter the real ordinary outer owner. Mutation is test input before
    // admission, never a synthetic publication source or a replacement owner grant.
    [Theory]
    [InlineData("missing-original")]
    [InlineData("stale-request")]
    [InlineData("foreign-mismatched-bundle")]
    [InlineData("wrong-id")]
    [InlineData("wrong-name")]
    [InlineData("mirror-drift")]
    [InlineData("history-drift")]
    [InlineData("duplicate-target")]
    [InlineData("new-actor")]
    [InlineData("forged-turn")]
    public async Task OrdinaryNpcCompletion_InvalidInputRefusesWithoutMutation(string mutation)
    {
        await using var context = await PrepareCompletionAsync();
        var backups = await BackupsAsync(context);
        await SubmitCompletionAsync(context);
        var commands = (await context.ReadJsonAsync(CompletionCommands))!;
        var command = commands["completeNPCActivities"]![0]!;
        switch (mutation)
        {
            case "missing-original":
                File.Delete(context.FileSystem.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
                break;
            case "stale-request":
                var request = (await context.ReadJsonAsync(LiveTurnPreparationService.TurnRequestPath))!;
                request["requestId"] = "different-current-request";
                await context.WriteJsonAsync(LiveTurnPreparationService.TurnRequestPath, request);
                break;
            case "foreign-mismatched-bundle":
                await using (var donor = await PrepareCompletionAsync())
                {
                    // Genuine donor bundle, but its snapshot files are not copied here.
                    // This proves mismatched-bundle refusal, not isolated cross-root binding.
                    await context.WriteJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                        (await donor.ReadJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!);
                    await context.WriteJsonAsync(PendingTurnSnapshotAuthority.AuthorityPath,
                        (await donor.ReadJsonAsync(PendingTurnSnapshotAuthority.AuthorityPath))!);
                }
                break;
            case "wrong-id": command["NPCId"] = "not-the-original-actor"; break;
            case "wrong-name": command["NPCName"] = "Not Mira"; break;
            case "duplicate-target": commands["completeNPCActivities"]!.AsArray().Add(command.DeepClone()); break;
            case "forged-turn": command["completionTurn"] = 999; break;
            case "mirror-drift":
            case "history-drift":
            case "new-actor":
                var core = (await context.ReadJsonAsync(CompletionCore))!;
                if (mutation == "mirror-drift") core["UpdateNPCs"]![0]!["currentActivity"]!["timeSpentMinutes"] = 241;
                else if (mutation == "history-drift")
                    foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
                        core[section]![0]!["completedActivities"]![0]!["narrativeSummary"] = "Forged original history.";
                else
                {
                    foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
                    {
                        var added = core[section]![0]!.DeepClone();
                        added["NPCId"] = "new-this-turn";
                        added["materialization"]!["materializedAtTurn"] = 42;
                        added["materialization"]!["actorId"] = "new-this-turn";
                        added["materialization"]!["materializationId"] = "mat_new-this-turn_turn_42";
                        core[section]!.AsArray().Add(added);
                    }
                    command["NPCId"] = "new-this-turn";
                }
                await context.WriteJsonAsync(CompletionCore, core);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        await context.WriteJsonAsync(CompletionCommands, commands);
        var before = await CaptureAsync(context);
        await AssertCompletionRefusedAsync(context, backups);
        await AssertBytesAsync(context, before);
    }

    [Fact]
    public async Task OrdinaryNpcCompletion_ReintroducedCommandCannotAppendAgain()
    {
        await using var context = await PrepareCompletionAsync();
        var backups = await BackupsAsync(context);
        var original = (await context.ReadJsonAsync(CompletionCore))!.DeepClone();
        await SubmitCompletionAsync(context);
        var first = await PublishAsync(context, backups);
        Assert.DoesNotContain(first.Issues, issue => issue.Severity == IssueSeverity.Error);
        await AssertCompletionAsync(context, original); // Required real first publication premise.
        await SubmitCompletionAsync(context);
        var before = await CaptureAsync(context);
        await AssertCompletionRefusedAsync(context, backups);
        await AssertBytesAsync(context, before);
    }

    private static async Task AssertCompletionRefusedAsync(EffectMaterializationTestContext context,
        IReadOnlyDictionary<string, string> backups)
    {
        AcceptedTurnCanonicalStateRefresh.Result? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await PublishAsync(context, backups));
        if (failure is not null) Assert.IsType<InvalidDataException>(failure);
        else Assert.Contains(result!.Issues, issue => issue.Severity == IssueSeverity.Error);
    }
}
