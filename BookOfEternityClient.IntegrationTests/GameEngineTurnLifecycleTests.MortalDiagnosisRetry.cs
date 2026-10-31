using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(20, false)]
    [InlineData(1, true)]
    public async Task MortalDiagnosisOuterRollbackRetainsExactAttemptBeforeCompetingClaim(int natural, bool shield)
    {
        GameEngineTestRuntime? runtime = null;
        GameEngine.RollbackSnapshot? backup = null;
        Dictionary<string, byte[]>? original = null;
        TurnRequest? turn = null;
        try
        {
            await using var fixture = await MortalWoundTreatmentResolverTests.DiagnosisOwnershipFixture.CreateAsync(
                natural, signedInput: true, duration: 30, seedFate: shield, originalProducer: async (files, request) =>
                {
                    turn = request;
                    // Legitimate initial defaults precede the one original backup;
                    // no accepted-turn baseline is recaptured or repaired afterward.
                    foreach (var pair in new Dictionary<string, JsonObject>
                    {
                        [AfterlifeEntityProfileState.StatePath] = AfterlifeEntityProfileState.CreateDefaultRoot(),
                        [AfterlifeSpiritualConflictState.StatePath] = AfterlifeSpiritualConflictState.CreateDefaultRoot(),
                        [AfterlifeChronicleState.StatePath] = AfterlifeChronicleState.CreateDefaultRoot(),
                        [AfterlifeGlobalFlagState.StatePath] = AfterlifeGlobalFlagState.CreateDefaultRoot()
                    })
                        if (await files.ReadFileBytesAsync(pair.Key) is null) await files.WriteFileAtomicAsync(pair.Key, pair.Value.ToJsonString());
                    var command = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
                    files.DeleteFile(AcceptedMechanicsPlan.WoundCommandPath);
                    runtime = CreateGameEngineRuntime(files, configureSettings: settings =>
                    { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
                    runtime.GameLoop.SetSession(request.SessionId, request.TurnNumber - 1);
                    original = CaptureDiagnosisRollbackCanonical(files);
                    backup = await runtime.Engine.CaptureOriginalRollbackAsync("mortal-diagnosis-exact-retry");
                    await runtime.Explorer.StagePendingLocalTurnRollbackSnapshotAsync(AcceptedMechanicsPlan.WoundCommandPath);
                    await files.WriteFileAtomicBytesAsync(AcceptedMechanicsPlan.WoundCommandPath, command);
                    runtime.Explorer.MarkExistingPendingLocalTurnValidationSnapshotFiles(AcceptedMechanicsPlan.WoundCommandPath);
                    runtime.Engine.OverlayExplorerLocalRollbackSnapshot(backup, runtime.Explorer.ConsumePendingLocalTurnRollbackSnapshot());
                    await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath,
                        JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                    await runtime.Engine.CaptureOriginalValidationSnapshotAsync(request, backup, "mortal-diagnosis-exact-retry");
                });
            Assert.NotNull(runtime); Assert.NotNull(backup); Assert.NotNull(original); Assert.NotNull(turn);
            var fs = fixture.Files;
            var frame = new SpiritualSelfTreatmentStageContext(turn.SessionId, turn.RequestId, turn.TurnNumber, turn.ProgressionControl!);
            var admitted = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fs, fixture.Lease, frame);
            var before = admitted.Request;
            var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(before.ModeAuthority);
            var terminal = MortalWoundDiagnosisOutcome.Resolve(admitted.Owner,
                new(admitted.Owner.History, Array.Empty<ValidationIssue>()), before);
            var generation = fs.GetOrCreateSessionGeneration(fixture.Lease);
            var held = fixture.CaptureJson(); // Actual common-publication BEFORE image.
            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
            Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
            Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
            var composed = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fs, fixture.Lease,
                new GameResponse(), admitted.Owner, before);
            Assert.True(composed.IsValid, JsonSerializer.Serialize(composed.Issues));
            await fixture.ReleaseLeaseAsync();
            var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(fs,
                new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance), validator,
                new Dictionary<string, string>(StringComparer.Ordinal));
            var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(published.TreatmentResourcePublicationTransaction);
            try
            {
                Assert.DoesNotContain(published.Issues, issue => issue.Severity == IssueSeverity.Error);
                var transient = WoundHistoryState.Parse(await fs.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
                Assert.True(transient.IsValid, JsonSerializer.Serialize(transient.Issues));
                var result = Assert.IsType<MortalWoundDiagnosisPersistedResult>(Assert.Single(transient.State!.Transitions,
                    row => row.TransitionResult is MortalWoundDiagnosisPersistedResult).TransitionResult);
                Assert.Equal(before.RequestFingerprint, result.Request.RequestFingerprint);
                if (shield)
                {
                    var identity = EffectIdentityState.Parse(JsonSerializer.Deserialize<JsonElement>(
                        Assert.IsType<string>(await fs.ReadFileAsync(EffectIdentityState.StatePath))), EffectIdentityState.StatePath);
                    Assert.Empty(identity.Issues);
                    Assert.Equal("expired", Assert.Single(identity.State!.Entries, row => row.EffectId == "effect_fate_shield_older").State);
                    Assert.Equal("active", Assert.Single(identity.State.Entries, row => row.EffectId == "effect_fate_shield_newer").State);
                }
                var restored = await transaction.CompensateAsync(fs);
                Assert.True(restored.IsValid, JsonSerializer.Serialize(restored.Issues));
                Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Rearmed, restored.Outcome);
                fixture.AssertJson(held);
                await runtime.Engine.RestoreOriginalForRepairAsync(backup, generation);
                AssertDiagnosisRollbackCanonical(original, fs); // ORIGINAL image, independently captured before backup/staging.
                await fixture.AcquireLeaseAsync();
                Assert.Null(await fs.ReadFileBytesAsync(fixture.Lease, AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Null(await fs.ReadFileBytesAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
                Assert.True(AcceptedTurnAuthorityRegistry.HasMortalDiagnosisOuterRollbackCheckpoint(fs, fixture.Lease));
                var wrong = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(frame.Progression))!;
                wrong.NextChaosSeaTurnOrdinal++;
                await Assert.ThrowsAsync<InvalidDataException>(() => AcceptedTurnAuthorityRegistry.ResumeMortalDiagnosisAfterOuterRollbackAsync(
                    fs, fixture.Lease, frame with { Progression = wrong }));
                AssertDiagnosisRollbackCanonical(original, fs);
                await AcceptedTurnAuthorityRegistry.ResumeMortalDiagnosisAfterOuterRollbackAsync(fs, fixture.Lease, frame);
                var retry = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fs, fixture.Lease, frame, requireRetained: true);
                var history = new WoundHistoryParseResult(retry.Owner.History, Array.Empty<ValidationIssue>());
                var competing = MortalWoundTreatmentPlanner.PrepareProcedureRequest(retry.Owner, history, retry.Owner.CurrentWound,
                    "competing_after_actual_outer_rollback", "inspect_explicit_method", fixture.EventRef);
                Assert.True(competing.IsValid, JsonSerializer.Serialize(competing.Issues));
                var competitor = Assert.IsType<MortalWoundProcedureCheckAuthority>(competing.Request!.ModeAuthority);
                Assert.Equal(new[] { 1 }, competitor.SourceIndices);
                var retained = Assert.IsType<MortalWoundProcedureCheckAuthority>(retry.Request.ModeAuthority);
                Assert.Equal(before.RequestFingerprint, retry.Request.RequestFingerprint);
                Assert.Equal(check.SourceIndices, retained.SourceIndices); Assert.Equal(check.SourceRolls, retained.SourceRolls);
                Assert.Equal(before.ResourceAuthority.ReservationId, retry.Request.ResourceAuthority.ReservationId);
                Assert.Equal(before.ResourceAuthority.AuthorityFingerprint, retry.Request.ResourceAuthority.AuthorityFingerprint);
                Assert.Equal(check.PreparedCriticalReaction?.PreparedReactionFingerprint, retained.PreparedCriticalReaction?.PreparedReactionFingerprint);
                Assert.Equal(terminal.Resolution.ResultFingerprint,
                    MortalWoundDiagnosisOutcome.Resolve(retry.Owner, history, retry.Request).Resolution.ResultFingerprint);
                Assert.True(competing.Request.RollbackNewProvisionalClaims(retry.Owner));
                await fixture.ReleaseLeaseAsync();
                var cancelled = await transaction.ReleaseTerminalAsync(fs, "cancelled");
                Assert.True(cancelled.IsValid, JsonSerializer.Serialize(cancelled.Issues));
                Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Released, cancelled.Outcome);
                await fixture.AcquireLeaseAsync();
                Assert.False(AcceptedTurnAuthorityRegistry.HasMortalDiagnosisOuterRollbackCheckpoint(fs, fixture.Lease));
                await Assert.ThrowsAsync<InvalidDataException>(() => AcceptedTurnAuthorityRegistry.ResumeMortalDiagnosisAfterOuterRollbackAsync(fs, fixture.Lease, frame));
            }
            finally { await ((IAsyncDisposable)transaction).DisposeAsync(); }
        }
        finally { if (runtime is not null) await runtime.AudioService.DisposeAsync(); }
    }

    private static Dictionary<string, byte[]> CaptureDiagnosisRollbackCanonical(FileSystemManager files) =>
        GameEngine.EnumerateOriginalBrowserRollbackTrackedFiles(
                FileSystemManager.EnumerateFilesWithoutFollowingReparsePoints(files.GameSessionPath, "*")
                    .Select(path => Path.GetRelativePath(files.GameSessionPath, path).Replace(Path.DirectorySeparatorChar, '/')).ToArray())
            .ToDictionary(path => path, path => File.ReadAllBytes(files.ResolvePath(path)), StringComparer.Ordinal);

    private static void AssertDiagnosisRollbackCanonical(IReadOnlyDictionary<string, byte[]> expected, FileSystemManager files)
    {
        var actual = CaptureDiagnosisRollbackCanonical(files);
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in expected) Assert.Equal(pair.Value, actual[pair.Key]);
    }
}
