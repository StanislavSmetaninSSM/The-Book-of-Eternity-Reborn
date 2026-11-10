using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_GenuineRetainedVersionOneCompletesWithoutProtocolUpgrade()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "Legacy");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        probe.AssertGenuineLegacyCompletion();
    }

    [Fact]
    public async Task GuaranteedTreatment_GenuineRetainedVersionOneCommittedColdKeepsLegacyConsumer()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "LegacyCold");
        await RunJointCompletionControlAsync(false, useLiveCaller: false, pauseAppliedForCold: true, interfaceLifecycleProbe: probe);
        probe.AssertGenuineLegacyCompletion();
    }

    [Fact]
    public async Task GuaranteedTreatment_NewOrdinaryOriginalUsesPriorMarkerSourceInsteadOfCachedData()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "NewOrdinary");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        probe.AssertNewOrdinaryWasObserved();
    }

    private sealed partial class JointInterfaceLifecycleProbe
    {
        internal bool UsesGenuineLegacyProducer => scenario is "Legacy" or "LegacyCold";
        private byte[]? _legacyProducedPending;
        private bool _newOrdinaryObserved;

        internal async Task InstallGenuineLegacyWaveAsync(GameEngine engine, PendingTurnSnapshotReadAuthority original)
        {
            if (!UsesGenuineLegacyProducer) return;
            Assert.Null(GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCheckpoint"));
            var validator = GetPrivateField<ValidationService>(engine, "_validator");
            var narrative = await validator.ValidateAcceptedTurnNarrativePayloadAsync(); // real ordinary owner OUTSIDE installing lease
            Assert.DoesNotContain(narrative, issue => issue.Severity == IssueSeverity.Error);
            await using (var lease = await fixture._fs.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.Null(await fixture._fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
                var captured = await MortalJointRepairCheckpoint.CaptureAsync(fixture._fs, lease,
                    original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
                Assert.True(captured.IsValid, DescribeValidationIssues(captured.Issues));
                var capture = Assert.IsType<MortalJointRepairCheckpoint.RejectedCapture>(captured.Capture);
                var checkpoint = Assert.IsType<MortalJointRepairCheckpoint>(captured.Checkpoint);
                Assert.False(checkpoint.UsesInterfacePublication);
                Assert.Equal(1, checkpoint.ToJsonObject()["schemaVersion"]!.GetValue<int>());
                _legacyProducedPending = JsonSerializer.SerializeToUtf8Bytes(capture.Pending);
                fixture._fs.RequireCommittedLocalPublication(await fixture._fs.PublishLocalFilesAsync(lease,
                    [new(WoundAcceptedTurnSnapshotContract.PendingResolutionPath, null, _legacyProducedPending)],
                    validatePreparedNamespace: async () =>
                    {
                        capture.EnsureCurrent(fixture._fs, lease);
                        Assert.Null(await fixture._fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
                        var fresh = await MortalJointRepairCheckpoint.CaptureAsync(fixture._fs, lease,
                            original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
                        Assert.True(fresh.IsValid, DescribeValidationIssues(fresh.Issues));
                        Assert.True(JsonNode.DeepEquals(checkpoint.ToJsonObject(), fresh.Checkpoint!.ToJsonObject()));
                        Assert.Equal(capture.CommandBytes, fresh.Capture!.CommandBytes);
                    }));
                Assert.Equal(_legacyProducedPending, await fixture._fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
                var reconstructed = await MortalJointRepairCheckpoint.ReconstructAsync(fixture._fs, lease,
                    original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
                Assert.True(reconstructed.IsValid, DescribeValidationIssues(reconstructed.Issues));
                Assert.True(JsonNode.DeepEquals(checkpoint.ToJsonObject(), reconstructed.Checkpoint!.ToJsonObject()));
                fixture.WritePhysicalInjuryDiagnostic("iobs_actual_legacy_factory_installed_readback", new
                { Original = new { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber },
                    Checkpoint = checkpoint.ToJsonObject(), Pending = Raw(_legacyProducedPending), CaptureIssues = capture.RejectedIssues,
                    Meaning = "Actual existing CaptureAsync v1 producer and complete pending graph; no v2 edit or private authority field seeded" });
            } // installation's real owning release before engine entry/selection
            Assert.Null(GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCheckpoint"));
        }

        internal void AssertGenuineLegacyCompletion()
        {
            Assert.NotNull(_legacyProducedPending); Assert.True(_completionObserved);
            var pending = ParseJsonObjectBytes(_legacyProducedPending);
            Assert.Equal(1, pending["jointRepairCheckpoint"]!["schemaVersion"]!.GetValue<int>());
            Assert.False(pending.ContainsKey(MortalJointInterfacePublication.PendingField));
            Assert.Equal(1, _firstMarker!["outputBinding"]!["version"]!.GetValue<int>());
            Assert.False(_firstMarker.ContainsKey("niPublication"));
            Assert.False(_firstMarker["outputBinding"]!.AsObject().ContainsKey("publishedInterfaceResult"));
            Assert.Empty(_resultCommits);
            Assert.Null(GetPrivateFieldValue<string?>(_engine!, "_mortalJointInterfaceFreshCheckpoint"));
            Assert.False(GetPrivateFieldValue<bool>(_engine!, "_mortalJointInterfaceFreshConsumed"));
            Assert.Equal(_submitted, _finalInterface);
            if (scenario == "Legacy")
            {
                var prefix = Assert.IsType<MortalJointDebugPrefixObservation>(_engine!.CaptureMortalJointReasoningPrefixObservation());
                Assert.Equal("UnavailableLegacyProtocol", prefix.Disposition);
                Assert.Equal(0, prefix.Evaluations); Assert.Equal(0, prefix.SourceChecks);
                Assert.Equal(0, prefix.TimestampCalls); Assert.Empty(prefix.Sources); Assert.Null(prefix.Fault);
                Assert.True(prefix.DataDisposed); Assert.True(prefix.AttemptExpired);
                fixture.WritePhysicalInjuryDiagnostic("dfr_genuine_legacy_protocol_not_upgraded", prefix);
            }
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_legacy_completion", new
            { scenario, Version = 1, Pending = Raw(_legacyProducedPending), Common = _firstMarker,
                Submitted = Raw(_submitted), Final = Raw(_finalInterface), Trace = _publications,
                Scope = "Full existing genuine engine completion or actual cold child already asserted by shared owner; no new child3 history claim" });
        }

        internal async Task ObserveNewOrdinaryBeforeCleanupAsync(GameEngine engine)
        {
            if (scenario != "NewOrdinary") return;
            Assert.True(_completionObserved); Assert.Equal(43, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            var priorMarker = ReadPhysical(MortalQteDecisionPublication.StatePath); Assert.NotNull(priorMarker);
            Assert.Equal(3, ParseJsonObjectBytes(priorMarker)["outputBinding"]!["version"]!.GetValue<int>());
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture._fs, async () =>
            {
                Assert.NotNull(await InvokePrivateTaskNullableResultAsync(engine, "StagePlayerTurnAsync", "Продолжить разговор.", null, null));
                await using (var lease = await fixture._fs.AcquireCanonicalWriteLeaseAsync())
                {
                    var read = PendingTurnSnapshotReader.ReadCurrent(fixture._fs, lease,
                        new PendingTurnSnapshotPathSelection(["game_state/meta/soul_state.json"], [MortalQteDecisionPublication.StatePath]));
                    Assert.True(read.Success, DescribeValidationIssues(read.Issues));
                    Assert.Equal(44, read.Snapshot!.TurnNumber);
                    Assert.Equal(priorMarker, read.Snapshot.ReadRequiredBytes(MortalQteDecisionPublication.StatePath));
                    Assert.Equal(priorMarker, await fixture._fs.ReadFileBytesAsync(lease, MortalQteDecisionPublication.StatePath));
                    var publicI = new JsonObject { ["dialogueOptions"] = new JsonArray(new JsonObject { ["text"] = TwoTags }) };
                    await fixture._fs.WriteFileAtomicBytesAsync(lease, InterfacePath, Encoding.UTF8.GetBytes(publicI.ToJsonString()));
                }
                var authored = ReadPhysical(InterfacePath);
                Assert.NotNull(GetPrivateFieldValue<object?>(engine, "_mortalJointInterfaceData")); // old cache deliberately remains
                var returned = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine, "ReadMortalJointInterfaceAtGenericSlotAsync");
                Assert.DoesNotContain(returned, issue => issue.Severity == IssueSeverity.Error);
                var actual = ParseJsonObjectBytes(Assert.IsType<byte[]>(ReadPhysical(InterfacePath)));
                var option = Assert.IsType<JsonObject>(Assert.Single(actual["dialogueOptions"]!.AsArray()));
                fixture.WritePhysicalInjuryDiagnostic("iobs_actual_new_ordinary_after_joint", new
                { OriginalTurn = 44, PriorMarker = Raw(priorMarker), PublicBefore = Raw(authored), PublicAfter = Raw(ReadPhysical(InterfacePath)), Returned = returned });
                Assert.Equal(OneTag, option["text"]!.GetValue<string>()); Assert.Equal(TwoTags, option["inputValue"]!.GetValue<string>());
                Assert.False(string.IsNullOrWhiteSpace(actual["timestamp"]?.GetValue<string>()));
                Assert.Equal(priorMarker, ReadPhysical(MortalQteDecisionPublication.StatePath));
                _newOrdinaryObserved = true;
                return true;
            });
        }

        internal void AssertNewOrdinaryWasObserved() => Assert.True(_newOrdinaryObserved);
    }
}
