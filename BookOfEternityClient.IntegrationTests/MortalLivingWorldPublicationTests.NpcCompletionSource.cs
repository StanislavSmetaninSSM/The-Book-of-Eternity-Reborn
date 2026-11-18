using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLivingWorldPublicationTests
{
    [Theory]
    [InlineData("foreign-operation")]
    [InlineData("disposed-lease")]
    [InlineData("replacement-lease")]
    [InlineData("changed-request")]
    public async Task NpcCompletionSource_RealIssuedCaptureCannotMoveOrOutliveOperation(string mutation)
    {
        await using var context = await PrepareCompletionAsync();
        await SubmitCompletionAsync(context);
        var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        try
        {
            var source = await MortalNpcActivityCompletionSource.CaptureOrdinaryAsync(context.FileSystem, lease);
            Assert.NotNull(source);
            await source!.RequireCurrentAsync(context.FileSystem, lease); // Genuine positive premise.
            if (mutation == "foreign-operation")
            {
                await using var foreign = await PrepareCompletionAsync();
                await using var otherLease = await foreign.FileSystem.AcquireCanonicalWriteLeaseAsync();
                await Assert.ThrowsAsync<InvalidDataException>(() => source.RequireCurrentAsync(foreign.FileSystem, otherLease));
            }
            else if (mutation == "changed-request")
            {
                var request = (await context.ReadJsonAsync(LiveTurnPreparationService.TurnRequestPath))!;
                request["requestId"] = "new-request-not-issued";
                await context.FileSystem.WriteFileAtomicAsync(lease, LiveTurnPreparationService.TurnRequestPath, request.ToJsonString());
                await Assert.ThrowsAsync<InvalidDataException>(() => source.RequireCurrentAsync(context.FileSystem, lease));
            }
            else
            {
                await lease.DisposeAsync();
                if (mutation == "replacement-lease")
                {
                    await using var replacement = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                    await Assert.ThrowsAsync<InvalidDataException>(() => source.RequireCurrentAsync(context.FileSystem, replacement));
                }
                else await Assert.ThrowsAnyAsync<InvalidOperationException>(() => source.RequireCurrentAsync(context.FileSystem, lease));
            }
        }
        finally { await lease.DisposeAsync(); }
    }

    [Fact]
    public async Task NpcCompletionSource_SignedNonMortalSoulCannotBorrowRequestRealm()
    {
        await using var context = await PrepareCompletionAsync(originalSoulRealm: "Chaos Sea");
        await context.WriteJsonAsync("game_state/meta/soul_state.json", new JsonObject { ["currentRealm"] = "Mortal World" });
        await SubmitCompletionAsync(context);
        var before = await CaptureAsync(context);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            await Assert.ThrowsAsync<InvalidDataException>(() => MortalNpcActivityCompletionSource.CaptureOrdinaryAsync(context.FileSystem, lease));
        await AssertBytesAsync(context, before);
    }

    [Fact]
    public async Task NpcCompletionSource_QteRouteCannotBorrowOrdinaryIssuedCapture()
    {
        await using var context = await PrepareCompletionAsync();
        var backups = await BackupsAsync(context);
        await SubmitCompletionAsync(context);
        var before = await CaptureAsync(context);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var ordinary = await context.Normalizer.BindTo(lease).WithOrdinaryNpcCompletionSourceAsync();
            var refusal = await Assert.ThrowsAsync<InvalidDataException>(() => ordinary.ForQteNormalization().NormalizeAccumulatedStateWithPlanAsync(backups));
            Assert.Equal("Terminal NPC activity requires its issued ordinary original operation.", refusal.Message);
        }
        await AssertBytesAsync(context, before);
    }

    [Fact]
    public async Task OrdinaryNpcCompletion_AfterConsumptionRequestChangeRefusesAndRestoresOwnedBytes()
    {
        var armed = false;
        var reached = false;
        byte[]? replacedRequest = null;
        EffectMaterializationTestContext? current = null;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (!armed || reached || current is null || path != CompletionCore) return Task.CompletedTask;
                var core = JsonNode.Parse(File.ReadAllText(current.FileSystem.ResolvePath(CompletionCore)).TrimStart('\uFEFF'))!;
                var sidecar = JsonNode.Parse(File.ReadAllText(current.FileSystem.ResolvePath(CompletionCommands)).TrimStart('\uFEFF'))!;
                if (core["NPCsInScene"]![0]!["currentActivity"] is not null || sidecar["completeNPCActivities"] is not null)
                    return Task.CompletedTask;
                Assert.Equal(2, core["NPCsInScene"]![0]!["completedActivities"]!.AsArray().Count);
                reached = true;
                var requestPath = current.FileSystem.ResolvePath(LiveTurnPreparationService.TurnRequestPath);
                var request = JsonNode.Parse(File.ReadAllText(requestPath).TrimStart('\uFEFF'))!;
                request["requestId"] = "changed-after-terminal-consumption";
                replacedRequest = System.Text.Encoding.UTF8.GetBytes(request.ToJsonString());
                File.WriteAllBytes(requestPath, replacedRequest);
                return Task.CompletedTask;
            }
        };
        await using var context = await PrepareCompletionAsync(hooks);
        current = context;
        var backups = await BackupsAsync(context);
        await SubmitCompletionAsync(context);
        var before = await CaptureAsync(context);
        armed = true;
        var failure = await Record.ExceptionAsync(() => PublishAsync(context, backups));
        armed = false;
        Assert.True(reached, "Must mutate request only after actual core completion and command consumption.");
        var refused = Assert.IsType<InvalidDataException>(failure);
        // A genuine signed Mortal turn has a living-world source even without a report.
        // Its earlier fence proves outer refusal/rollback here, not isolated terminal closing-fence coverage.
        Assert.Equal("Living-world authenticated original source changed.", refused.Message);
        // External request mutation is not ours to overwrite. All normalizer-owned bytes roll back.
        before[LiveTurnPreparationService.TurnRequestPath] = replacedRequest;
        await AssertBytesAsync(context, before);
    }

    [Fact]
    public async Task OrdinaryNpcCompletion_PreservesUnrelatedCurrentActorData()
    {
        await using var context = await PrepareCompletionAsync();
        var backups = await BackupsAsync(context);
        var current = (await context.ReadJsonAsync(CompletionCore))!;
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
            current[section]![0]!["description"] = "Current accepted description, unrelated to the activity.";
        await context.WriteJsonAsync(CompletionCore, current);
        await SubmitCompletionAsync(context);
        var result = await PublishAsync(context, backups);
        Assert.DoesNotContain(result.Issues, issue => issue.Severity == IssueSeverity.Error);
        await AssertCompletionAsync(context, current);
    }
}
