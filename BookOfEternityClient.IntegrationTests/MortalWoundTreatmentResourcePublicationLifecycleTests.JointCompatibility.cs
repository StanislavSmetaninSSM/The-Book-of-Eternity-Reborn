using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("absent")]
    [InlineData("private_v1")]
    public async Task JointNullFrameHold_ComponentAbsentAndGenuineV1RemainUnclaimedAndByteExact(string scenario)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        GameEngine engine;
        PendingTurnSnapshotReadAuthority original;
        if (scenario == "private_v1")
        {
            // Existing genuine QTE42→actual Stage43→normal common publisher.
            // No marker/receipt, original tuple or accepted proof is authored.
            var published = await PreparePublishedMortalCompletionProofAsync(materialize: false);
            engine = published.Engine; original = published.Snapshot;
            var genuine = await InspectMortalCompletionProofAsync(original);
            Assert.Equal(MortalQteDecisionPublication.CurrentPublicationState.VerifiedCompleted, genuine.State);
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var marker = ParseJsonObjectBytes(Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath)));
            Assert.Equal(1, marker["schemaVersion"]!.GetValue<int>());
            Assert.False(marker.ContainsKey("jointTreatment")); Assert.False(marker.ContainsKey("progression"));
        }
        else
        {
            Assert.Equal("absent", scenario);
            var ordinary = await PrepareMortalDecisionComponentAsync(materialize: false, offered: false);
            engine = ordinary.Engine; original = ordinary.Snapshot;
            Assert.Empty(MortalQteWoundOpportunityProjection.Project(original));
            // Remove the helper's unsolicited diagnostic draft before this
            // ordinary/no-offer component baseline; never remove authority.
            _fs.DeleteFile(MortalWoundOpportunityAdapter.DecisionPath);
            Assert.False(_fs.FileExists(MortalQteDecisionPublication.StatePath));
        }
        // Capture all GameSession file names/bytes and physical G only.
        // RuntimeRoot contents/names and empty directories are not captured;
        // runtime assertions below cover only turn and inGame scalars.
        Dictionary<string, byte[]?> before;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            before = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
        var turn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber;
        var inGame = GetPrivateFieldValue<bool>(engine, "_inGame");
        bool? held = null;
        var failure = await Record.ExceptionAsync(async () =>
            held = await InvokePrivateAsync<bool>(engine, "TryHoldMortalJointWithoutValidatedOriginalAsync"));
        Dictionary<string, byte[]?> after;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            after = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
        WritePhysicalInjuryDiagnostic("fr063_joint_nullframe_nonapplicable_component", new
        {
            scenario, ComponentOnly = true, ActualMethod = "TryHoldMortalJointWithoutValidatedOriginalAsync",
            original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber,
            Failure = failure?.ToString(), held, Before = before, After = after,
            TurnBefore = turn, TurnAfter = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber,
            InGameBefore = inGame, InGameAfter = GetPrivateFieldValue<bool>(engine, "_inGame")
        });
        Assert.Null(failure); Assert.False(held);
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var image in before) Assert.Equal(image.Value, after[image.Key]);
        Assert.Equal(turn, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        Assert.Equal(inGame, GetPrivateFieldValue<bool>(engine, "_inGame"));
        // False only proves this private preservation guard did not claim the
        // component. It grants no publication/replay/native completion authority.
    }
}
