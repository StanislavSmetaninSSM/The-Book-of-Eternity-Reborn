using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_CurrentCaptureStopPrecedesPreparationAndExpiresActualOwner()
    {
        using var probe = new JointCaptureCutProbe(this, stop: true, observe: true);
        await RunJointCompletionControlAsync(nonzeroProgression: false, useLiveCaller: true,
            currentCaptureProbe: probe);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuaranteedTreatment_CurrentCaptureObservationPreservesOrdinaryCompletion(bool observe)
    {
        using var probe = new JointCaptureCutProbe(this, stop: false, observe: observe);
        await RunJointCompletionControlAsync(nonzeroProgression: false, useLiveCaller: true,
            currentCaptureProbe: probe);
        probe.AssertTransparency();
    }

    // Fixture observations only: no owner invocation, issued capability, new
    // production counter, source seed, or NI result. Name matching compiles on
    // the pre-seam product so its real old route can witness the missing cut.
    private sealed class JointCaptureCutProbe(GameEngineTurnLifecycleTests fixture, bool stop, bool observe) : IDisposable
    {
        private const string CheckpointName = "MortalJointCustodyCaptureCompletedBeforePreparation";
        private readonly IOException _sentinel = new("CUT01: actual completed current custody capture stop");
        private readonly HashSet<(object Attempt, string Stage)> _retained = [];
        private readonly HashSet<object> _reservations = new(ReferenceEqualityComparer.Instance);
        private readonly HashSet<object> _takes = new(ReferenceEqualityComparer.Instance);
        private readonly List<string> _postCutMutations = [];
        private readonly List<string> _trace = [];
        private GameEngine? _engine;
        private GameEngine.MortalJointPublicationAttempt? _attemptAtCut;
        private FileSystemManager.CanonicalWriteLease? _leaseAtCut;
        private GameEngineSessionFinalizationHooks? _priorHooks;
        private Func<string, Task>? _priorRead;
        private Action<string>? _priorMutation;
        private Dictionary<string, byte[]?>? _atCut;
        private RegistryView? _registryAtCut;
        private int _turnAtCut, _callbacks, _preparationEntryReads, _commonPublications, _postCutPublications;
        private bool _cutReached;
        internal bool StopRequested => stop;

        internal void Attach(GameEngine engine)
        {
            Assert.Null(_engine);
            _engine = engine;
            _priorHooks = GetPrivateFieldValue<GameEngineSessionFinalizationHooks?>(engine, "_sessionFinalizationHooks");
            _priorRead = fixture._consoleCanonicalReadObserver;
            _priorMutation = fixture._consoleMutationObserver;
            fixture._consoleCanonicalReadObserver = async path =>
            {
                if (_priorRead is not null) await _priorRead(path);
                var attempt = ObserveActualState();
                if (attempt is not { Live: true } || path != WoundAcceptedTurnSnapshotContract.PendingResolutionPath) return;
                var stages = GetPrivateField<IDictionary>(attempt, "_stages");
                if (attempt.ExportDiagnostic()["phase"]!.GetValue<string>() == "PreparationStarted" &&
                    stages.Count == 0 && attempt.Continuation is null)
                {
                    // This PRESENT installed-wave read is ReconstructAsync88,
                    // reached from preparation146. Absent command reads are not
                    // assumed to invoke the initial-validation observer.
                    _preparationEntryReads++;
                    _trace.Add("actual-preparation-present-pending-read");
                }
            };
            fixture._consoleMutationObserver = path =>
            {
                _priorMutation?.Invoke(path);
                ObserveActualState();
                if (_cutReached) _postCutMutations.Add(path);
            };
            if (observe)
                engine.ConfigureSessionFinalizationHooksForTesting(new GameEngineSessionFinalizationHooks
                {
                    AtCheckpointAsync = async checkpoint =>
                    {
                        if (_priorHooks?.AtCheckpointAsync is { } previous) await previous(checkpoint);
                        if (checkpoint.ToString() == CheckpointName) AtCompletedCapture();
                    }
                });
        }

        private GameEngine.MortalJointPublicationAttempt? ObserveActualState()
        {
            if (_engine is null) return null;
            var attempt = GetPrivateFieldValue<GameEngine.MortalJointPublicationAttempt?>(_engine, "_mortalJointPublicationAttempt");
            if (attempt is null) return null;
            foreach (var stage in GetPrivateField<IDictionary>(attempt, "_stages").Keys.Cast<object>())
                _retained.Add((attempt, stage.ToString()!));
            if (attempt.Reservation is { } reservation) _reservations.Add(reservation);
            var registry = ReadExistingRegistry();
            if (registry.OpenTake is { } take) _takes.Add(take);
            return attempt;
        }

        private void AtCompletedCapture()
        {
            _callbacks++;
            _trace.Add("actual-current-capture-callback");
            var attempt = Assert.IsType<GameEngine.MortalJointPublicationAttempt>(ObserveActualState());
            var lease = GetPrivateField<FileSystemManager.CanonicalWriteLease>(attempt, "_lease");
            Assert.True(attempt.Live); Assert.True(lease.IsActive);
            Assert.False(attempt.Delegated);
            Assert.Null(attempt.Continuation); Assert.Null(attempt.Reservation);
            Assert.Empty(GetPrivateField<IDictionary>(attempt, "_stages"));
            var diagnostic = attempt.ExportDiagnostic();
            Assert.Equal("PreparationStarted", diagnostic["phase"]!.GetValue<string>());
            Assert.False(diagnostic["captureCanOpen"]!.GetValue<bool>());
            Assert.False(diagnostic["fullSemanticAdmission"]!.GetValue<bool>());
            Assert.False(diagnostic["hasS"]!.GetValue<bool>());
            Assert.False(diagnostic["hasB"]!.GetValue<bool>());
            Assert.False(diagnostic["hasM3"]!.GetValue<bool>());
            _attemptAtCut = attempt; _leaseAtCut = lease;
            _registryAtCut = ReadExistingRegistry();
            _atCut = PhysicalImages();
            _turnAtCut = GetPrivateField<GameLoop>(_engine!, "_gameLoop").TurnNumber;
            _cutReached = true;
            fixture.WritePhysicalInjuryDiagnostic("cut01_actual_capture_before_preparation", new
            {
                Callback = _callbacks, Stage = diagnostic, LeaseActive = lease.IsActive,
                Generation = attempt.Generation, Turn = _turnAtCut, Counters = Counters(),
                Images = RawImages(_atCut), Meaning = "Current custody capture completed; never NI completeness or authority"
            });
            if (stop) throw _sentinel;
        }

        internal void ObservePublication(TrustedLocalPublicationPhase phase, int index)
        {
            var attempt = ObserveActualState();
            if (_cutReached) _postCutPublications++;
            if (phase != TrustedLocalPublicationPhase.Committed || attempt is null) return;
            var journalPath = Path.Combine(fixture._fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(journalPath));
            if (journal.RootElement.GetProperty("Members").EnumerateArray().Any(member =>
                    member.GetProperty("Path").GetString() == fixture._fs.ResolvePath(MortalQteDecisionPublication.StatePath)))
            {
                _commonPublications++;
                _trace.Add("actual-joint-marker-common-publication");
            }
        }

        internal void AssertStopped(GameEngine actualEngine)
        {
            Assert.Same(_engine, actualEngine);
            var after = PhysicalImages();
            var attempt = ObserveActualState();
            var registry = ReadExistingRegistry();
            var refresh = GetPrivateFieldValue<object?>(actualEngine, "_mortalJointRepairCanonicalRefresh");
            var disposition = refresh?.GetType().GetProperty("Disposition")?.GetValue(refresh)?.ToString();
            var issues = refresh?.GetType().GetProperty("Issues")?.GetValue(refresh) as IReadOnlyList<ValidationIssue>;
            fixture.WritePhysicalInjuryDiagnostic("cut01_actual_stop_outcome", new
            {
                Callback = _callbacks, Counters = Counters(), Trace = _trace,
                ActualAttempt = attempt?.ExportDiagnostic(), ActualDisposition = disposition,
                Issues = issues?.Select(i => new { i.Code, i.FilePath, i.Message }),
                CutImages = _atCut is null ? null : RawImages(_atCut), AfterImages = RawImages(after),
                ActualTurn = GetPrivateField<GameLoop>(actualEngine, "_gameLoop").TurnNumber,
                Scope = "Original actual route outcome, even when missing-cut baseline completes common"
            });
            // FIRST intended old-product failure, after genuine shared Stage43,
            // original/repair/held-request/corrected-reply prerequisites. No enum
            // existence, NotNull or setup failure substitutes for this semantic cut.
            Assert.Equal(1, _callbacks);
            Assert.Same(_attemptAtCut, attempt);
            Assert.False(attempt!.Live); Assert.False(_leaseAtCut!.IsActive);
            Assert.True(attempt.CloseRecorded); Assert.False(attempt.Delegated);
            Assert.Null(attempt.Continuation); Assert.Null(attempt.Reservation);
            Assert.Empty(GetPrivateField<IDictionary>(attempt, "_stages"));
            Assert.Equal("UnavailableNotReserved", attempt.ExportDiagnostic()["closeDisposition"]!.GetValue<string>());
            var close = GetPrivateField<object>(attempt, "_close");
            Assert.Null(GetPrivateFieldValue<object?>(close, "_receipt"));
            Assert.Equal("PublicationUncertain", disposition);
            Assert.Contains(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(issues), i => i.Message.Contains(_sentinel.Message, StringComparison.Ordinal));
            Assert.False(GetPrivateFieldValue<bool>(actualEngine, "_inGame"));
            Assert.Equal(_turnAtCut, GetPrivateField<GameLoop>(actualEngine, "_gameLoop").TurnNumber);
            Assert.Equal(0, _preparationEntryReads);
            Assert.Empty(_retained); Assert.Empty(_reservations); Assert.Empty(_takes);
            Assert.Equal(0, _commonPublications); Assert.Equal(0, _postCutPublications); Assert.Empty(_postCutMutations);
            Assert.NotNull(_registryAtCut);
            Assert.Same(_registryAtCut!.State, registry.State);
            Assert.Null(registry.Reservation); Assert.Null(registry.OpenTake);
            Assert.Same(_registryAtCut.Reservation, registry.Reservation);
            Assert.Same(_registryAtCut.OpenTake, registry.OpenTake);
            Assert.Same(_registryAtCut.Common, registry.Common);
            Assert.Equal(_atCut!.Keys.OrderBy(p => p, StringComparer.Ordinal), after.Keys.OrderBy(p => p, StringComparer.Ordinal));
            foreach (var (path, bytes) in _atCut) Assert.Equal(bytes, after[path]);
        }

        internal void AssertTransparency()
        {
            Assert.False(stop);
            Assert.Equal(observe ? 1 : 0, _callbacks);
            Assert.True(_preparationEntryReads > 0);
            Assert.Contains(_retained, row => row.Stage == "Preparation");
            Assert.Contains(_retained, row => row.Stage == "Common");
            Assert.True(_commonPublications > 0);
            fixture.WritePhysicalInjuryDiagnostic("cut01_actual_ordinary_transparency", new
            { AdditionalObserver = observe, Callback = _callbacks, Counters = Counters(), Trace = _trace,
                Meaning = "Existing full completion+inert-repeat assertions returned; not a globally null essential fixture hook or NI proof" });
        }

        private object Counters() => new
        {
            PreparationPresentWaveEntryReads = _preparationEntryReads,
            RetainedPreparation = _retained.Count(x => x.Stage == "Preparation"),
            RetainedPrepared = _retained.Count(x => x.Stage == "Prepared"),
            RetainedFinal = _retained.Count(x => x.Stage == "Final"),
            RetainedPreflight = _retained.Count(x => x.Stage == "Preflight"),
            RetainedEffects = _retained.Count(x => x.Stage == "Effects"),
            RetainedCommon = _retained.Count(x => x.Stage == "Common"),
            ReservationObjectsObserved = _reservations.Count, OpenTakeObjectsObserved = _takes.Count,
            JointMarkerCommonPublications = _commonPublications,
            PostCutCanonicalMutations = _postCutMutations.ToArray(), PostCutLocalPublicationEvents = _postCutPublications,
            Meaning = "Actual read/retained-state/publication observations; not fabricated per-method invocation totals"
        };

        private RegistryView ReadExistingRegistry()
        {
            var rootSlots = typeof(AcceptedTurnAuthorityRegistry).GetField("RootSlots", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            object?[] arguments = [fixture._fs.CanonicalRootAuthorityIdentity, null];
            var present = (bool)rootSlots.GetType().GetMethod("TryGetValue")!.Invoke(rootSlots, arguments)!;
            Assert.True(present); // Existing genuine source/confirmed claim already owns this slot.
            var state = GetPrivateField<object>(arguments[1]!, "_state");
            return new(state, GetPrivateFieldValue<object?>(state, "_mortalWoundTreatmentPublicationReservation"),
                GetPrivateFieldValue<object?>(state, "_openJointRepairPublicationReceipt"),
                GetPrivateFieldValue<object?>(state, "_validatedJointRepairCommonAuthority"));
        }
        private sealed record RegistryView(object State, object? Reservation, object? OpenTake, object? Common);

        private Dictionary<string, byte[]?> PhysicalImages()
        {
            var files = Directory.EnumerateFiles(fixture._fs.GameSessionPath, "*", SearchOption.AllDirectories)
                .OrderBy(p => p, StringComparer.Ordinal).ToDictionary(
                    p => Path.GetRelativePath(fixture._fs.GameSessionPath, p).Replace('\\', '/'),
                    p => (byte[]?)File.ReadAllBytes(p), StringComparer.Ordinal);
            files.Add("$sessionGeneration", File.ReadAllBytes(fixture._fs.SessionGenerationPath));
            return files;
        }
        private static object RawImages(IReadOnlyDictionary<string, byte[]?> images) =>
            images.ToDictionary(p => p.Key, p => p.Value is null ? null : Convert.ToBase64String(p.Value), StringComparer.Ordinal);

        public void Dispose()
        {
            if (_engine is null) return;
            fixture._consoleCanonicalReadObserver = _priorRead;
            fixture._consoleMutationObserver = _priorMutation;
            _engine.ConfigureSessionFinalizationHooksForTesting(_priorHooks);
        }
    }
}
