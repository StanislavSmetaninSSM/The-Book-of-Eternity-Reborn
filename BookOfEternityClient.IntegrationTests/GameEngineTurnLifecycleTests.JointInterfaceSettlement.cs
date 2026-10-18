using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_CommonMemberCompensationRetainsPublishedInterfaceWithoutReplay()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "CommonFault");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuaranteedTreatment_ErrorPreparedOnlyWholeTerminalOwnerCanRemoveOrCompensate(bool fault)
    {
        using var probe = new JointInterfaceLifecycleProbe(this, fault ? "TerminalFault" : "Terminal");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
    }

    private sealed partial class JointInterfaceLifecycleProbe
    {
        private int _settlementFaults;
        private bool _terminalArmed;
        private readonly List<object> _settlementTrace = [];
        private CanonicalLocalFileChange[]? _terminalMembers;
        private readonly IOException _settlementFailure = new("L01 actual whole-owner member publication fault.");

        private void ObserveSettlementPublication(TrustedLocalPublicationPhase phase, int index)
        {
            if (!_configured || (scenario != "CommonFault" && !_terminalArmed) ||
                phase is not (TrustedLocalPublicationPhase.MemberPublished or TrustedLocalPublicationPhase.Committed)) return;
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Path.Combine(
                fixture._fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
            var members = journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
            var marker = members.FirstOrDefault(row => row.GetProperty("Path").GetString() == fixture._fs.ResolvePath(MortalQteDecisionPublication.StatePath));
            var pending = members.FirstOrDefault(row => row.GetProperty("Path").GetString() == fixture._fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            var isCommon = scenario == "CommonFault" && marker.ValueKind != JsonValueKind.Undefined && Image(marker, "After") is not null;
            var isTerminal = _terminalArmed && pending.ValueKind != JsonValueKind.Undefined && Image(pending, "After") is null;
            if (!isCommon && !isTerminal) return;
            if (isTerminal && _terminalMembers is null)
                _terminalMembers = members.Select(row => new CanonicalLocalFileChange(
                    Path.GetRelativePath(fixture._fs.GameSessionPath, row.GetProperty("Path").GetString()!).Replace('\\', '/'),
                    Image(row, "Before"), Image(row, "After"))).ToArray();
            _settlementTrace.Add(new { Phase = phase.ToString(), Index = index, Journal = journal.RootElement.Clone(), IsCommon = isCommon, IsTerminal = isTerminal });
            if (phase != TrustedLocalPublicationPhase.MemberPublished || _settlementFaults != 0 ||
                !(isCommon || scenario == "TerminalFault")) return;
            // A real member has been applied in the actual owning cohort. Let
            // its existing rollback/compensation settle; this is not a crash.
            _settlementFaults++;
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_owning_member_fault", new
            { scenario, Phase = phase.ToString(), Index = index, Journal = journal.RootElement.Clone(), Error = _settlementFailure.Message });
            throw _settlementFailure;
        }

        private async Task AssertCompensatedCommonAsync(GameEngine engine)
        {
            Assert.Equal(1, _settlementFaults); Assert.Equal(1, _captureCallbacks);
            var refresh = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCanonicalRefresh"); Assert.NotNull(refresh);
            Assert.Equal(MortalJointRepairPublicationDisposition.RetryableHeld,
                refresh.GetType().GetProperty("Disposition")!.GetValue(refresh));
            var record = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(
                ParseJsonObjectBytes(Assert.IsType<byte[]>(ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)))[MortalJointInterfacePublication.PendingField]));
            Assert.Equal("Published", record.State); Assert.False(record.HasErrors);
            Assert.True(JsonNode.DeepEquals(_resultCommits[1], record.ToJson()));
            Assert.Equal(record.AfterBytes, ReadPhysical(InterfacePath));
            Assert.Single(_publications.Where(row => row.InterfaceChanged));
            Assert.Empty(_publications.Where(row => row.MarkerCommitted));
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            foreach (var (path, bytes) in _mechanicalBefore) Assert.Equal(bytes, ReadPhysical(path));
            // RetryableHeld may lawfully resume through the actual current
            // owner. This case asserts direct restore retention, not that a
            // later owning retry is forbidden.
            await AssertActualRetainingOwnersAsync(engine, invokeWrapper: false);
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_common_compensation_retained", new
            { Disposition = "RetryableHeld", Result = record.ToJson(), Interface = Raw(ReadPhysical(InterfacePath)),
                Trace = _settlementTrace, Meaning = "Actual common member failure compensated by its owning taken transaction; prior I is retained, not normalized or generically restored" });
        }

        private async Task AssertWholeTerminalAsync(GameEngine engine)
        {
            var stage = GetPrivateFieldValue<object?>(engine, "_hotOrdinaryOriginal"); Assert.NotNull(stage);
            var preparation = GetPrivateFieldValue<object?>(stage, "Preparation"); Assert.NotNull(preparation);
            var backup = preparation.GetType().GetProperty("Backup")!.GetValue(preparation); Assert.NotNull(backup);
            var checkpoint = GetPrivateFieldValue<MortalJointRepairCheckpoint?>(engine, "_mortalJointRepairCheckpoint"); Assert.NotNull(checkpoint);
            var before = CaptureCanonicalFiles();
            object? settled = null;
            _terminalArmed = true;
            try
            {
                await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture._fs, async () =>
                {
                    settled = await InvokePrivateTaskResultAsync(engine, "TerminateMortalJointRepairAsync", backup, checkpoint, "validation_failed");
                    return true;
                });
            }
            finally { _terminalArmed = false; }
            Assert.NotNull(settled); Assert.NotNull(_terminalMembers); Assert.NotEmpty(_terminalMembers);
            var disposition = Assert.IsType<MortalJointRepairPublicationDisposition>(settled.GetType().GetProperty("Disposition")!.GetValue(settled));
            var after = CaptureCanonicalFiles();
            if (scenario == "TerminalFault")
            {
                Assert.Equal(1, _settlementFaults); Assert.Equal(MortalJointRepairPublicationDisposition.PublicationUncertain, disposition);
                Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
                foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
                Assert.NotNull(ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            }
            else
            {
                Assert.Equal(0, _settlementFaults); Assert.Equal(MortalJointRepairPublicationDisposition.TerminalReleased, disposition);
                foreach (var member in _terminalMembers) Assert.Equal(member.After, ReadPhysical(member.RelativePath));
                foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                    MortalWoundOpportunityAdapter.DecisionPath }) Assert.Null(ReadPhysical(path));
                var changed = _terminalMembers.Select(row => fixture._fs.ResolvePath(row.RelativePath)).ToHashSet(StringComparer.Ordinal);
                Assert.Equal(before.Keys.Except(changed).Order(StringComparer.Ordinal), after.Keys.Except(changed).Order(StringComparer.Ordinal));
                foreach (var (path, bytes) in before.Where(row => !changed.Contains(row.Key))) Assert.Equal(bytes, after[path]);
            }
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_whole_terminal_settlement", new
            { scenario, Disposition = disposition.ToString(), Faults = _settlementFaults, Members = _terminalMembers, Trace = _settlementTrace,
                Before = before.ToDictionary(row => row.Key, row => Raw(row.Value)), After = after.ToDictionary(row => row.Key, row => Raw(row.Value)),
                Meaning = "Real current confirmed claim and complete Stage backup; no record-only deletion, fallback, manufactured release token or projector retry" });
        }
    }
}
