using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Refuses to report successful rollback without authority or when an authenticated original backup has disappeared.
    /// </summary>
    /// <param name="missingBackup">
    /// Removes one genuine backup when <see langword="true"/>; otherwise supplies no rollback authority.
    /// </param>
    /// <returns>
    /// A task completing after a <see langword="false"/> result and preserved input, snapshot and remaining original evidence are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedRollbackResult_FailurePreservesOriginalEvidence(bool missingBackup)
    {
        var fixture = await CreateSpiritualEntryGuardOriginalAsync(absentConflict: false);
        await using var context = fixture.Context;
        object? rollback = null;
        if (missingBackup)
        {
            var method = typeof(GameEngine).GetMethod("BuildValidatedRollbackSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;
            rollback = method.Invoke(fixture.Engine, [fixture.Snapshot]);
            Assert.NotNull(rollback);
            var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"));
            var backups = Assert.IsType<JsonObject>(manifest["rollbackBackups"]);
            Assert.NotEmpty(backups);
            context.FileSystem.DeleteFile(backups.First().Value!.GetValue<string>());
        }
        var expected = await ReadSpiritualEntryGuardFilesAsync(context);

        var result = await InvokePrivateTaskResultAsync(fixture.Engine, "RollbackRejectedAcceptedTurnAsync",
            new object?[] { rollback, string.Empty });

        Assert.False(Assert.IsType<bool>(result));
        if (missingBackup)
        {
            const string diagnosticPath = "game_state/control/validation_diagnostic_failure_report.json";
            var diagnostic = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(diagnosticPath));
            expected[Path.Combine("game_state", "control", "validation_diagnostic_failure_report.json")] = diagnostic;
        }
        await AssertSpiritualEntryGuardFilesAsync(context, expected);
        Assert.True(context.FileSystem.FileExists("input/turn_request.json"));
        Assert.True(context.FileSystem.FileExists("game_state/control/pending_turn_snapshot.json"));
        Assert.True(context.FileSystem.FileExists("game_state/control/pending_turn_snapshot.authority.json"));
    }
}
