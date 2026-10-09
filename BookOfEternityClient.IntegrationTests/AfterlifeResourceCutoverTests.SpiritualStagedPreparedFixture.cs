using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private static readonly Lazy<Task<SpiritualStagedPreparedTemplates>> SpiritualStagedTemplates =
        new(PrepareSpiritualStagedTemplatesAsync);

    /// <summary>
    /// Stores detached session trees and comparison bytes without retaining a writable root or runtime owner.
    /// </summary>
    /// <param name="BeforeReady">
    /// Session-only corpus with lawful physical A and its original unaccepted checkpoint.
    /// </param>
    /// <param name="Accepted">
    /// Session-only corpus after genuine A commit activates B.
    /// </param>
    /// <param name="AJson">
    /// Serialized genuine A envelope, deserialized separately for each caller.
    /// </param>
    /// <param name="BJson">
    /// Serialized genuine B envelope, deserialized separately for accepted-state callers.
    /// </param>
    /// <param name="BeforeCheckpoint">
    /// Private exact pre-commit comparison bytes, copied before returning to a caller.
    /// </param>
    /// <param name="AfterCheckpoint">
    /// Private exact post-commit comparison bytes, copied before returning to a caller.
    /// </param>
    /// <param name="AcceptedAJson">
    /// Serialized lawful A image, parsed separately for each caller.
    /// </param>
    private sealed record SpiritualStagedPreparedTemplates(PreparedFixtureTree BeforeReady,
        PreparedFixtureTree Accepted, string AJson, string BJson, byte[] BeforeCheckpoint,
        byte[] AfterCheckpoint, string AcceptedAJson);

    /// <summary>
    /// Carries detached prepared-state comparisons beside one independently owned writable test context.
    /// </summary>
    /// <param name="Context">
    /// Fresh root, filesystem and validator; the caller disposes the context.
    /// </param>
    /// <param name="A">
    /// Genuine original dependent request, retained only for comparison.
    /// </param>
    /// <param name="B">
    /// Genuine accepted-A successor, or null for a pre-AReady state.
    /// </param>
    /// <param name="BeforeCheckpoint">
    /// Exact checkpoint before A acceptance.
    /// </param>
    /// <param name="AfterCheckpoint">
    /// Exact checkpoint after A acceptance, or null before acceptance.
    /// </param>
    /// <param name="AcceptedA">
    /// Detached lawful A image; changing it does not change the physical fixture.
    /// </param>
    private sealed record SpiritualStagedPreparedFixture(ResourceMaterializationTestContext Context,
        SpiritualWoundContinuationRequest A, SpiritualWoundContinuationRequest? B,
        byte[] BeforeCheckpoint, byte[]? AfterCheckpoint, JsonObject AcceptedA);

    /// <summary>
    /// Creates the selected prepared boundary with a fresh physical session generation and validation ownership.
    /// </summary>
    /// <param name="acceptedA">
    /// True selects accepted A awaiting B; false selects lawful physical A before its Ready or journal append.
    /// </param>
    /// <param name="hooks">
    /// Optional observations on this caller's independent filesystem; template preparation is unchanged.
    /// </param>
    /// <returns>
    /// Detached comparisons and an independently disposable physical context.
    /// </returns>
    private static async Task<SpiritualStagedPreparedFixture> CreatePreparedSpiritualStagedContextAsync(
        bool acceptedA, FileSystemManagerHooks? hooks = null)
    {
        var templates = await SpiritualStagedTemplates.Value;
        var context = await ResourceMaterializationTestContext.CreateAsync(hooks);
        try
        {
            (acceptedA ? templates.Accepted : templates.BeforeReady).Materialize(context.FileSystem.GameSessionPath);
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                context.FileSystem.GetOrCreateSessionGeneration(lease);
            return new(context,
                JsonSerializer.Deserialize<SpiritualWoundContinuationRequest>(templates.AJson)!,
                acceptedA ? JsonSerializer.Deserialize<SpiritualWoundContinuationRequest>(templates.BJson)! : null,
                templates.BeforeCheckpoint.ToArray(), acceptedA ? templates.AfterCheckpoint.ToArray() : null,
                JsonNode.Parse(templates.AcceptedAJson)!.AsObject());
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Prepares both reusable boundaries once through genuine signed intake, saved selection and dependent progress commit.
    /// </summary>
    /// <returns>
    /// Detached game-session bytes after all preparation leases and owners have been disposed; runtime locks are excluded.
    /// </returns>
    private static async Task<SpiritualStagedPreparedTemplates> PrepareSpiritualStagedTemplatesAsync()
    {
        await using var context = await CreateSpiritualStagedContinuationContextAsync();
        JsonObject accepted;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
            accepted = CreateSpiritualStagedCorrectionA(original);
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(accepted.ToJsonString()));
        }
        // Capture only the portable signed session corpus. Runtime locks and any private key storage remain outside it.
        var beforeReady = PreparedFixtureTree.Capture(context.FileSystem.GameSessionPath);
        var history = await CommitSpiritualStagedHistoryAAsync(context, accepted);
        var committed = PreparedFixtureTree.Capture(context.FileSystem.GameSessionPath);
        return new(beforeReady, committed, JsonSerializer.Serialize(history.A), JsonSerializer.Serialize(history.B),
            history.BeforeCheckpoint.ToArray(), history.AfterCheckpoint.ToArray(), history.AcceptedA.ToJsonString());
    }

    /// <summary>
    /// Requires one genuine preparation for reused states and proves root portability, mutable isolation and cold ownership.
    /// </summary>
    /// <returns>
    /// A task completing after moved signed snapshots reopen and mutations cannot affect another copy or a later copy.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualStagedPreparedFixture_ReusesPreparationAndPreservesColdIsolation()
    {
        var originalBefore = Volatile.Read(ref _spiritualStagedOriginalPreparationCount);
        var acceptedBefore = Volatile.Read(ref _spiritualStagedAcceptedPreparationCount);
        var first = await CreatePreparedSpiritualStagedContextAsync(acceptedA: true);
        await using var firstContext = first.Context;
        var second = await CreatePreparedSpiritualStagedContextAsync(acceptedA: true);
        await using var secondContext = second.Context;
        Assert.InRange(Volatile.Read(ref _spiritualStagedOriginalPreparationCount) - originalBefore, 0, 1);
        Assert.InRange(Volatile.Read(ref _spiritualStagedAcceptedPreparationCount) - acceptedBefore, 0, 1);
        Assert.NotEqual(firstContext.RootPath, secondContext.RootPath);
        Assert.NotSame(firstContext.FileSystem, secondContext.FileSystem);
        Assert.NotSame(firstContext.Validator, secondContext.Validator);
        Assert.NotSame(first.A.DependentDraftFields, second.A.DependentDraftFields);
        Assert.NotSame(first.B!.DependentDraftFields, second.B!.DependentDraftFields);
        string firstGeneration;
        string secondGeneration;
        await using (var lease = await firstContext.FileSystem.AcquireCanonicalWriteLeaseAsync())
            firstGeneration = Assert.IsType<string>(firstContext.FileSystem.ReadExistingSessionGeneration(lease));
        await using (var lease = await secondContext.FileSystem.AcquireCanonicalWriteLeaseAsync())
            secondGeneration = Assert.IsType<string>(secondContext.FileSystem.ReadExistingSessionGeneration(lease));
        Assert.NotEqual(firstGeneration, secondGeneration);
        Assert.Equal(JsonSerializer.Serialize(first.A), JsonSerializer.Serialize(second.A));
        Assert.Equal(JsonSerializer.Serialize(first.B), JsonSerializer.Serialize(second.B));
        Assert.Equal(first.AfterCheckpoint, second.AfterCheckpoint);
        foreach (var path in new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
                     PendingTurnSnapshotAuthority.AuthorityPath, SpiritualWoundDecisionPendingState.StatePath })
            Assert.Equal(await firstContext.FileSystem.ReadFileBytesAsync(path), await secondContext.FileSystem.ReadFileBytesAsync(path));
        var coldFs = new FileSystemManager(secondContext.RootPath, NullLogger<FileSystemManager>.Instance);
        var cold = new ValidationService(coldFs, NullLogger<ValidationService>.Instance);
        await using (var lease = await coldFs.AcquireCanonicalWriteLeaseAsync())
        {
            var projection = await cold.ReadSpiritualWoundContinuationAsync(lease);
            Assert.True(projection.Disposition == "dependent_draft",
                $"Cold prepared-state disposition: {projection.Disposition}." + Environment.NewLine +
                string.Join(Environment.NewLine, projection.Issues.Select(issue =>
                    $"{issue.Code}: {issue}; expected={issue.Expected}; actual={issue.Actual}")));
            Assert.Equal(JsonSerializer.Serialize(second.B), JsonSerializer.Serialize(projection.Request));
        }
        var secondBefore = await ReadSpiritualContinuationTransportFilesAsync(secondContext);
        await firstContext.FileSystem.WriteFileAtomicBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath, Encoding.UTF8.GetBytes("{}"));
        first.AfterCheckpoint![0] ^= 1;
        first.AcceptedA["fixtureIsolationMutation"] = true;
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(secondContext, secondBefore);
        var third = await CreatePreparedSpiritualStagedContextAsync(acceptedA: true);
        await using var thirdContext = third.Context;
        Assert.Equal(second.AfterCheckpoint, third.AfterCheckpoint);
        Assert.True(JsonNode.DeepEquals(second.AcceptedA, third.AcceptedA));

        var unaccepted = await CreatePreparedSpiritualStagedContextAsync(acceptedA: false);
        await using var unacceptedContext = unaccepted.Context;
        Assert.Null(unaccepted.AfterCheckpoint);
        Assert.Null(unaccepted.B);
        var checkpoint = (await unacceptedContext.ReadJsonAsync(SpiritualWoundCaptureCheckpointState.StatePath))!["checkpoint"]!;
        Assert.Null(checkpoint["pendingSubmission"]!["dependentDraftProgress"]);
        await using (var lease = await unacceptedContext.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var projection = await unacceptedContext.Validator.ReadSpiritualWoundContinuationAsync(lease);
            Assert.Equal(JsonSerializer.Serialize(second.A), JsonSerializer.Serialize(projection.Request));
            Assert.True(JsonNode.DeepEquals(second.AcceptedA, await unacceptedContext.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        }
        Assert.InRange(Volatile.Read(ref _spiritualStagedOriginalPreparationCount) - originalBefore, 0, 1);
        Assert.InRange(Volatile.Read(ref _spiritualStagedAcceptedPreparationCount) - acceptedBefore, 0, 1);
    }
}
