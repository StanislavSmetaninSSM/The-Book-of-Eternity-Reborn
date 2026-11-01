using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("{invalid-stale-mortal-clock")]
    public async Task OrdinaryAfterlifeAdmissionDoesNotParseOrPublishStaleMortalClock(string clock)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            captureOriginalSnapshot: async original =>
            {
                // Match the existing standalone original-intake fixture: the
                // real dispatcher needs the signed schedule even to rule out
                // a natural cycle. Complete it before this fixture's only original.
                var control = await new ProgressionScheduleService(original.FileSystem,
                    NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
                var schedule = Assert.IsType<byte[]>(await original.FileSystem.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath));
                await original.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
                    preGeneratedDices1d20: [15, 5, 12, 8], progressionControl: control);
                await using var lease = await original.FileSystem.AcquireCanonicalWriteLeaseAsync();
                var read = PendingTurnSnapshotReader.ReadCurrent(original.FileSystem, lease,
                    [ProgressionScheduleService.SchedulePath, AfterlifeSpiritualConflictState.StatePath]);
                Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
                Assert.Equal(schedule, read.Snapshot!.ReadRequiredBytes(ProgressionScheduleService.SchedulePath));
                using var conflict = JsonDocument.Parse(read.Snapshot.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath));
                Assert.Equal(JsonValueKind.Object, conflict.RootElement.GetProperty("activeConflict").ValueKind);
            });
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactJsonAsync(EffectAcceptedTurnInputComposer.WorldTimePath, clock);
        var before = await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath);
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoConflictFrameErrors(issues);
        Assert.NotNull(await PeekPlanAsync(context));
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath));
    }
}
