using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PriorPendingObservationShapeRemainsUnrelatedDataButCannotIssueDiagnosisAuthority(bool combatShape)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, exportDiagnosis: false,
            originalProducer: async (files, request) =>
            {
                await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
                {
                    SessionId = request.SessionId, RequestId = request.RequestId, TurnNumber = request.TurnNumber,
                    CurrentRealm = "Mortal World", ProgressionControl = request.ProgressionControl,
                    PlayerAction = request.PlayerAction, PreGeneratedDices1d20 = request.PreGeneratedDices1d20
                });
                var manifest = JsonNode.Parse(Assert.IsType<string>(await files.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!.AsObject();
                var paths = combatShape ? PendingTurnSnapshotPathPresenceV1.BeforeDiagnosisCombatLogicalPaths
                    : PendingTurnSnapshotPathPresenceV1.BeforeDiagnosisLogicalPaths;
                var members = manifest["files"]!.AsObject();
                manifest["originalPathPresenceV1"] = new JsonObject(paths.Select(path =>
                    new KeyValuePair<string, JsonNode?>(path, JsonValue.Create(members.ContainsKey(path)))));
                // Existing deliberate signed-DATA negative pattern, not a valid
                // diagnosis producer, repair packet or accepted game receipt.
                manifest["manifestPayloadHash"] = string.Empty;
                manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
                await files.WriteFileAtomicAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath, manifest.ToJsonString());
                await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(files);
            });
        var before = fixture.CaptureJson();
        var unrelated = PendingTurnSnapshotReader.ReadObservedCurrent(fixture.Files, fixture.Lease,
            new PendingTurnSnapshotPathSelection(["game_state/meta/soul_state.json"], [WoundAcceptedTurnSnapshotContract.PendingResolutionPath]));
        Assert.True(unrelated.Success, JsonSerializer.Serialize(unrelated.Issues));
        Assert.Null(unrelated.Evidence!.ReadPendingResolutionOriginal());
        var input = MortalWoundDiagnosisInputCodec.Read(JsonNode.Parse(Assert.IsType<string>(await fixture.Files.ReadFileAsync(
            fixture.Lease, AcceptedMechanicsPlan.WoundCommandPath)))!.AsObject());
        // Export must refuse before any usable diagnosis owner exists. No
        // Prepare/Reserve is called and no detached DATA reactivates claims.
        var diagnosis = MortalWoundTreatmentAcceptedStateAuthority.ExportDiagnosisCurrent(fixture.Files, fixture.Lease,
            input.Context, input.Selection.WoundId);
        Assert.False(diagnosis.IsValid); Assert.Null(diagnosis.Authority);
        Assert.Contains(diagnosis.Issues, issue => issue.Code == "mortal_diagnosis_original_pending_unobserved" &&
            issue.FilePath == WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        Assert.Null(await fixture.Files.ReadFileBytesAsync(fixture.Lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
        fixture.AssertJson(before);
    }
}
