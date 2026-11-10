using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_ActualReturnedReadCannotHideChangedWriterPreimage()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "ReadRace");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
    }

    [Fact]
    public async Task GuaranteedTreatment_PublishedInterfaceCannotExcuseOtherImmutableReadyDrift()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "ReadyDrift");
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
    }

    private sealed partial class JointInterfaceLifecycleProbe
    {
        private Func<string, Task>? _priorReturnedRead;
        private int _raceMutations;
        private byte[]? _raceBefore, _raceAfter, _racePending;
        private DateTime? _raceMtime;
        private bool _originalIAbsentObserved;
        private const string ReadyDriftPath = "game_state/player/experience.json";

        internal void ObserveActualOriginalMetadata(PendingTurnSnapshotReadAuthority original)
        {
            if (scenario != "ReadRace") return;
            Assert.DoesNotContain(InterfacePath, original.DeclaredOriginalLogicalPaths);
            _originalIAbsentObserved = true;
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_original_omits_interface_snapshot", new
            { original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber,
                InterfaceSnapshotDeclared = false, DeclaredNames = original.DeclaredOriginalLogicalPaths });
        }

        private void AttachReturnedReadRace()
        {
            if (scenario != "ReadRace") return;
            _priorReturnedRead = fixture._consoleReturnedCanonicalReadObserver;
            fixture._consoleReturnedCanonicalReadObserver = async path =>
            {
                if (_priorReturnedRead is { } prior) await prior(path);
                if (path != InterfacePath || !_configured || _raceMutations != 0 ||
                    !GetPrivateFieldValue<bool>(_engine!, "_mortalJointInterfaceFreshConsumed")) return;
                Assert.True(_originalIAbsentObserved);
                // Original I was physically removed BEFORE Stage43: freshness
                // returned null before any current public-I read. This is the
                // actual prepare adapter's public read, AFTER its leased raw
                // before-image. N's earlier prose reads occur before FreshConsumed.
                var ready = GetPrivateFieldValue<object?>(_engine!, "_mortalJointInterfaceReady"); Assert.NotNull(ready);
                Assert.True(GetPrivateFieldValue<bool>(ready, "DeleteReturned"));
                Assert.True(GetPrivateFieldValue<bool>(ready, "ReleaseReturned"));
                _racePending = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
                Assert.False(ParseJsonObjectBytes(Assert.IsType<byte[]>(_racePending)).ContainsKey(MortalJointInterfacePublication.PendingField));
                _raceBefore = ReadPhysical(InterfacePath); Assert.Equal(_submitted, _raceBefore);
                _raceAfter = Assert.IsType<byte[]>(_raceBefore).Concat(new byte[] { 32 }).ToArray();
                File.WriteAllBytes(fixture._fs.ResolvePath(InterfacePath), _raceAfter); // trusted-local physical fault, no reentrant writer/capability
                _raceMtime = Mtime(); _raceMutations++;
                fixture.WritePhysicalInjuryDiagnostic("iobs_actual_returned_read_physical_preimage_fault", new
                { path, Before = Raw(_raceBefore), After = Raw(_raceAfter), Mtime = _raceMtime, Pending = Raw(_racePending),
                    FreshConsumed = true, ReadyDeleteReturned = true, ReadyReleaseReturned = true,
                    Meaning = "Actual public read result already exists; eventual projected bytes must not hide this physical preimage change" });
            };
        }

        private void ObservePublishedReadyDrift(JsonObject record)
        {
            if (scenario != "ReadyDrift" || _raceMutations != 0 || record["state"]!.GetValue<string>() != "Published") return;
            _raceBefore = ReadPhysical(ReadyDriftPath); Assert.NotNull(_raceBefore);
            Assert.Equal(_mechanicalBefore[ReadyDriftPath], _raceBefore);
            _raceAfter = _raceBefore.Concat(new byte[] { 32 }).ToArray();
            _racePending = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            File.WriteAllBytes(fixture._fs.ResolvePath(ReadyDriftPath), _raceAfter); _raceMutations++;
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_published_ready_noninterface_drift", new
            { Path = ReadyDriftPath, Before = Raw(_raceBefore), After = Raw(_raceAfter), Record = record,
                Meaning = "Real Published I cohort is positive reach; only I's exact owned transition may differ from immutable Ready, not this semantically identical whitespace fault" });
        }

        private void AssertSourceRaceHeld(GameEngine engine, string diagnostics)
        {
            Assert.Equal(1, _raceMutations); Assert.Equal(0, _captureCallbacks);
            Assert.Empty(_publications.Where(row => row.MarkerCommitted));
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            Assert.True(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfaceFreshConsumed"));
            Assert.Equal(_racePending, ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            if (scenario == "ReadRace")
            {
                Assert.Equal(_raceAfter, ReadPhysical(InterfacePath)); Assert.Equal(_raceMtime, Mtime());
                Assert.Empty(_resultCommits); Assert.Empty(_publications);
                Assert.Null(GetPrivateFieldValue<object?>(engine, "_mortalJointInterfaceData"));
                Assert.True(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfacePreparationHeld"));
                Assert.Contains("joint_interface_consumed_read_preimage_changed", diagnostics);
            }
            else
            {
                Assert.Equal(_raceAfter, ReadPhysical(ReadyDriftPath));
                var result = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(_resultCommits.Last()));
                Assert.Equal("Published", result.State); Assert.Equal(result.AfterBytes, ReadPhysical(InterfacePath));
                var ready = GetPrivateFieldValue<object?>(engine, "_mortalJointInterfaceReady"); Assert.NotNull(ready);
                var source = GetPrivateFieldValue<object?>(ready, "Source"); Assert.NotNull(source);
                var images = Assert.IsType<Dictionary<string, byte[]?>>(source.GetType().GetProperty("Current")!.GetValue(source));
                Assert.Equal(_raceBefore, images[ReadyDriftPath]); // no Ready source rewrite/reseed
                var attempt = GetPrivateFieldValue<GameEngine.MortalJointPublicationAttempt?>(engine, "_mortalJointPublicationAttempt");
                Assert.NotNull(attempt); Assert.False(attempt.Live); Assert.False(attempt.Delegated);
                Assert.Null(attempt.Continuation); Assert.Null(attempt.Reservation);
                Assert.Empty(GetPrivateField<IDictionary>(attempt, "_stages"));
                Assert.True(attempt.ExportDiagnostic()["liveCapabilitiesExpired"]!.GetValue<bool>());
                Assert.Contains("joint_interface_ready_submitted_source_changed", diagnostics);
            }
            foreach (var (path, before) in _mechanicalBefore)
                Assert.Equal(scenario == "ReadyDrift" && path == ReadyDriftPath ? _raceAfter : before, ReadPhysical(path));
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_source_race_held_no_repair", new
            { scenario, Mutations = _raceMutations, Foreign = Raw(_raceAfter), Pending = Raw(ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath)),
                Interface = Raw(ReadPhysical(InterfacePath)), ResultCommits = _resultCommits, Trace = _publications, Diagnostics = diagnostics });
        }

        private void DisposeReturnedReadRace()
        { if (scenario == "ReadRace") fixture._consoleReturnedCanonicalReadObserver = _priorReturnedRead; }
    }
}
