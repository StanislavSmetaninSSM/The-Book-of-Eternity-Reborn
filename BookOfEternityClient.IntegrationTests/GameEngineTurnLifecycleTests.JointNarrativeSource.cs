using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task GuaranteedTreatment_LiveNarrativeSourceDriftRefusesBeforePreparation() =>
        RunLiveNarrativeSourceControlAsync(changeSource: true);

    [Fact]
    public Task GuaranteedTreatment_UnchangedLiveNarrativeSourceCompletesJointOnce() =>
        RunLiveNarrativeSourceControlAsync(changeSource: false);

    [Fact]
    public Task GuaranteedTreatment_LiveNarrativeDependencyDriftPreventsFirstInterfacePreparation() =>
        RunLiveNarrativeSourceControlAsync(changeSource: true, duringNarrative: true);

    [Fact]
    public Task GuaranteedTreatment_LiveNarrativePreparedColdResumePreservesFrozenI() =>
        RunJointInterfaceActualProcessLossAsync(0);

    [Fact]
    public Task GuaranteedTreatment_LiveNarrativePublishedColdResumePreservesFrozenI() =>
        RunJointInterfaceActualProcessLossAsync(2);

    /// <summary>
    /// Runs a fresh genuine Stage/Ready owner with named preparation observations, without sharing mutable fixture state.
    /// </summary>
    /// <param name="changeSource">Whether to change the consumed achievements bytes at the existing capture checkpoint.</param>
    /// <param name="duringNarrative">Whether a later actual N dependency read triggers the change before the first I preparation.</param>
    /// <returns>Completion after held or original full completion assertions and owned cleanup.</returns>
    private async Task RunLiveNarrativeSourceControlAsync(bool changeSource, bool duringNarrative = false)
    {
        using var probe = new JointNarrativeSourceProbe(changeSource, duringNarrative);
        using var fixture = new GameEngineTurnLifecycleTests(_directGachaOutput, adoptedRoot: null,
            passiveOperationTimings: probe.Timings);
        await fixture.RunJointCompletionControlAsync(false, useLiveCaller: true, narrativeSourceProbe: probe);
    }

    // Observations and one public-source intervention only. No fabricated N/I
    // result, original, Ready, lease, registry access or capability is introduced.
    private sealed class JointNarrativeSourceProbe(bool changeSource, bool duringNarrative) : IDisposable
    {
        private const string SourcePath = "game_state/meta/achievements.json";
        private const string PreparationOperation = "GameEngine.CreateJointRepairPreparationAsync";
        private readonly List<object> _reads = [];
        private readonly List<string> _postCutMutations = [];
        private OperationTimingAccumulator.Window? _window;
        private GameEngineTurnLifecycleTests? _fixture;
        private GameEngineTestRuntime? _runtime;
        private Func<string, Task>? _priorRead;
        private Func<string, Task>? _priorReturnedRead;
        private Action<string>? _priorMutation;
        private Dictionary<string, byte[]?>? _atCut;
        private byte[]? _sourceRead, _narrativeSourceRead, _sourceAtCut;
        private bool _armed, _cut;
        private int _sourceReads, _sourceReturns, _narrativeSourceReads, _narrativeSourceReturns,
            _freshSourceReads, _callbacks, _captureCallbacks, _turnAtCut;
        internal OperationTimingAccumulator Timings { get; } = new();
        private long PreparationEntries => _window?.Capture().Rows.SingleOrDefault(row => row.Name == PreparationOperation)?.Started ?? 0;
        internal bool ExpectsHeld => changeSource;

        /// <summary>Attaches to the existing actual runtime and ordinary physical read/mutation callbacks.</summary>
        /// <param name="fixture">The independent genuine lifecycle fixture.</param>
        /// <param name="runtime">Its actual constructed engine and loop.</param>
        internal void Attach(GameEngineTurnLifecycleTests fixture, GameEngineTestRuntime runtime)
        {
            Assert.Null(_fixture);
            _fixture = fixture; _runtime = runtime;
            _priorRead = fixture._consoleCanonicalReadObserver;
            _priorReturnedRead = fixture._consoleReturnedCanonicalReadObserver;
            _priorMutation = fixture._consoleMutationObserver;
            fixture._consoleCanonicalReadObserver = async path =>
            {
                if (_priorRead is not null) await _priorRead(path);
                if (!_armed || _cut) return;
                // The callback is inside the actual ordinary source read. Never
                // invoke a validator or synthesize a post-read to manufacture N.
                byte[]? raw = path == SourcePath ? File.ReadAllBytes(fixture._fs.ResolvePath(path)) : null;
                var operations = path == SourcePath ? _window?.Capture() : null;
                var inNarrative = IsOperationActive(operations, "NarrativePolicy.Evaluate");
                var inFreshRecheck = IsOperationActive(operations, "NarrativePolicy.Recheck");
                _reads.Add(new { Path = path, SourceRaw = Raw(raw), InNarrative = inNarrative, InFreshRecheck = inFreshRecheck });
                if (path != SourcePath) return;
                _sourceReads++; _sourceRead = raw;
                if (inNarrative) { _narrativeSourceReads++; _narrativeSourceRead = raw; }
                if (inFreshRecheck) _freshSourceReads++;
            };
            fixture._consoleReturnedCanonicalReadObserver = async path =>
            {
                if (_priorReturnedRead is not null) await _priorReturnedRead(path);
                if (!_armed || _cut) return;
                var inNarrative = (path == SourcePath || (duringNarrative && path == "game_state/quests/regular_quests.json")) &&
                    IsOperationActive(_window?.Capture(), "NarrativePolicy.Evaluate");
                if (path == SourcePath)
                {
                    _sourceReturns++;
                    if (inNarrative) _narrativeSourceReturns++;
                    _reads.Add(new { Path = path, Returned = true, InNarrative = inNarrative });
                }
                if (duringNarrative && path == "game_state/quests/regular_quests.json" && _narrativeSourceReturns > 0 &&
                    inNarrative)
                    InterveneAtConsumedSource();
            };
            fixture._consoleMutationObserver = path =>
            {
                _priorMutation?.Invoke(path);
                if (_cut) _postCutMutations.Add(path);
            };
        }

        /// <summary>Arms only after the genuine corrected public response has been written and read back.</summary>
        internal void ObserveCorrectedSubmissionReadback()
        {
            Assert.False(_armed); _armed = true;
            Assert.False(_cut); Assert.Equal(0, _sourceReads);
            _window = Timings.StartWindow();
        }

        /// <summary>Changes one real N dependency after N returned and Capturing completed, before preparation.</summary>
        /// <param name="checkpoint">The real engine checkpoint after its prior fixture observer.</param>
        internal void ObserveCheckpoint(SessionFinalizationCheckpoint checkpoint)
        {
            if (checkpoint != SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation) return;
            _captureCallbacks++;
            if (duringNarrative) return;
            InterveneAtConsumedSource();
        }

        private void InterveneAtConsumedSource()
        {
            Assert.True(_armed); Assert.False(_cut); _callbacks++;
            Assert.True(_narrativeSourceReads > 0, "Corrected pre-intake N must actually read achievements before the capture checkpoint.");
            Assert.Equal(_narrativeSourceReads, _narrativeSourceReturns);
            // Initial physical hooks also observe explicit leased fresh readers;
            // those readers deliberately have no public-return hook. Preserve
            // both totals, while pairing only reads inside the actual N scope.
            if (duringNarrative) Assert.Equal(0, _freshSourceReads);
            else Assert.True(_freshSourceReads >= 2,
                "The pre-I and Capturing fresh N checks must both physically read the source before this cut.");
            var fixture = _fixture!;
            var path = fixture._fs.ResolvePath(SourcePath);
            var before = File.ReadAllBytes(path);
            Assert.Equal(_sourceRead, before);
            Assert.Equal(_narrativeSourceRead, before);
            // JSON whitespace changes exact consumed bytes while preserving all
            // ordinary achievement semantics and the genuinely signed original.
            if (changeSource) File.WriteAllBytes(path, before.Concat(Encoding.UTF8.GetBytes("\n ")).ToArray());
            _sourceAtCut = File.ReadAllBytes(path);
            Assert.Equal(changeSource, !before.AsSpan().SequenceEqual(_sourceAtCut));
            _turnAtCut = _runtime!.GameLoop.TurnNumber;
            _atCut = PhysicalImages();
            _cut = true;
            fixture.WritePhysicalInjuryDiagnostic("live_n_actual_dependency_cut", new
            {
                changeSource, duringNarrative, Callback = _callbacks, CaptureCallbacks = _captureCallbacks,
                ActualCorrectedSourceReads = _sourceReads, ActualCorrectedSourceReturns = _sourceReturns,
                ActualNarrativeSourceReads = _narrativeSourceReads, ActualNarrativeSourceReturns = _narrativeSourceReturns,
                FreshRecheckSourceReads = _freshSourceReads,
                SourcePath, Before = Raw(before), After = Raw(_sourceAtCut), Reads = _reads,
                Runtime = _runtime.Engine.CaptureMortalJointRuntimeObservation(), Turn = _turnAtCut,
                Images = RawImages(_atCut)
            });
        }

        /// <summary>Asserts causal refusal before preparation and exact physical effect preservation.</summary>
        /// <param name="engine">The real engine after its ordinary owner returned.</param>
        /// <param name="diagnostics">Complete existing logger diagnostics.</param>
        internal async Task AssertHeldAsync(GameEngine engine, string diagnostics)
        {
            Assert.Same(_runtime!.Engine, engine);
            var after = PhysicalImages();
            var runtime = engine.CaptureMortalJointRuntimeObservation();
            if (!_cut)
            {
                _fixture!.WritePhysicalInjuryDiagnostic("live_n_missing_dependency_cut", new
                {
                    changeSource, duringNarrative, _armed, _callbacks, _captureCallbacks,
                    _sourceReads, _sourceReturns, _narrativeSourceReads, _narrativeSourceReturns, _freshSourceReads,
                    Runtime = runtime, EngineDiagnostics = diagnostics, Reads = _reads, Operations = _window?.Capture()
                });
                Assert.True(_cut, "The actual dependency cut was not reached; see live_n_missing_dependency_cut and the original engine diagnostics.");
            }
            _fixture!.WritePhysicalInjuryDiagnostic("live_n_actual_source_refusal", new
            {
                changeSource, _callbacks, _sourceReads, _sourceReturns, PreparationEntries, PostCutMutations = _postCutMutations,
                _narrativeSourceReads, _narrativeSourceReturns, _freshSourceReads,
                Runtime = runtime, EngineDiagnostics = diagnostics, Operations = _window!.Capture(),
                CutImages = RawImages(_atCut!), AfterImages = RawImages(after),
                TurnAtCut = _turnAtCut, TurnAfter = _runtime.GameLoop.TurnNumber
            });
            // FIRST intended baseline failure: the omitted consumed dependency
            // reaches real preparation. Compile/setup/missing API are never RED.
            Assert.Equal(0, PreparationEntries);
            Assert.Equal(1, _callbacks); Assert.True(_sourceReads > 0);
            Assert.True(runtime.CheckpointPresent); Assert.True(runtime.CorrectionReady);
            if (duringNarrative)
            {
                Assert.Equal(0, _captureCallbacks);
                Assert.Equal(0, Starts(_window!.Capture(), "MortalJointInterface.PrepareResult"));
                Assert.False(runtime.CanonicalRefreshPresent);
                Assert.True(runtime.PreparationHeld);
            }
            else
            {
                Assert.Equal(1, _captureCallbacks);
                Assert.True(runtime.CanonicalRefreshPresent);
                Assert.Equal("PublicationUncertain", runtime.CanonicalDisposition);
                Assert.NotEmpty(runtime.Issues!);
            }
            Assert.Equal(_turnAtCut, _runtime.GameLoop.TurnNumber);
            Assert.Empty(_postCutMutations);
            Assert.Equal(_atCut!.Keys, after.Keys);
            foreach (var (path, raw) in _atCut) Assert.Equal(raw, after[path]);
            Assert.Equal(_sourceAtCut, after[SourcePath]);
            Assert.Equal("KnownLiveNHold", runtime.NarrativeContribution);
            var beforeRepeat = _window!.Capture();
            var mutationCount = _postCutMutations.Count;
            var repeated = await engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync();
            var repeatedImages = PhysicalImages();
            var afterRepeat = _window.Capture();
            var repeatedRuntime = engine.CaptureMortalJointRuntimeObservation();
            _fixture.WritePhysicalInjuryDiagnostic("live_n_known_hold_same_engine_repeat", new
            {
                Repeated = repeated, Runtime = repeatedRuntime, BeforeCounters = beforeRepeat,
                AfterCounters = afterRepeat, BeforeImages = RawImages(after), AfterImages = RawImages(repeatedImages),
                PostCutMutations = _postCutMutations, TurnAfter = _runtime.GameLoop.TurnNumber
            });
            Assert.Equal("KnownLiveNHold", repeatedRuntime.NarrativeContribution);
            Assert.True(repeatedRuntime.PreparationHeld);
            Assert.Equal(mutationCount, _postCutMutations.Count);
            Assert.Equal(_turnAtCut, _runtime.GameLoop.TurnNumber);
            Assert.Equal(after.Keys, repeatedImages.Keys);
            foreach (var (path, raw) in after) Assert.Equal(raw, repeatedImages[path]);
            foreach (var name in new[] { "NarrativePolicy.Capture", "NarrativePolicy.Evaluate",
                         "NarrativePolicy.EvaluateInterface", "MortalJointInterface.PrepareResult", PreparationOperation })
                Assert.Equal(Starts(beforeRepeat, name), Starts(afterRepeat, name));
        }

        private int _closedPrefixRefusals;

        /// <summary>Uses the existing genuine closed-capture checkpoint; no new owner or callback is introduced.</summary>
        internal async Task AssertPrefixPhaseAtCheckpointAsync(SessionFinalizationCheckpoint checkpoint)
        {
            if (changeSource || checkpoint != SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation) return;
            Assert.Equal(0, _closedPrefixRefusals);
            await AssertActualPrefixPhaseRefusalAsync(expired: false);
            _closedPrefixRefusals++;
        }

        private async Task AssertActualPrefixPhaseRefusalAsync(bool expired)
        {
            var engine = _runtime!.Engine;
            var beforeResult = Assert.IsType<MortalJointDebugPrefixObservation>(engine.CaptureMortalJointReasoningPrefixObservation());
            var beforeRuntime = engine.CaptureMortalJointRuntimeObservation();
            var beforeOperations = _window!.Capture();
            // The checkpoint cut was captured immediately before this awaited
            // check. Expiry is checked after the full existing inert-repeat proof.
            var beforeImages = expired ? PhysicalImages() : Assert.IsType<Dictionary<string, byte[]?>>(_atCut);
            var beforeTurn = _runtime.GameLoop.TurnNumber;
            var mutationCount = _postCutMutations.Count;
            Assert.Equal("NeedsActorDependencies", beforeResult.Disposition);
            Assert.Equal(1, beforeResult.Evaluations); Assert.Equal(1, beforeResult.TimestampCalls);
            Assert.Equal(expired ? 2 : 1, beforeResult.SourceChecks);
            Assert.Equal(expired, beforeResult.AttemptExpired);
            Assert.Equal(expired, beforeResult.DataDisposed);
            if (!expired) Assert.Equal("PreparationStarted", beforeResult.Phase);
            var failure = await Record.ExceptionAsync(() => engine.ReconsumeCurrentMortalJointReasoningPrefixAsync());
            var afterResult = engine.CaptureMortalJointReasoningPrefixObservation();
            var afterRuntime = engine.CaptureMortalJointRuntimeObservation();
            var afterOperations = _window.Capture();
            var afterImages = PhysicalImages();
            _fixture!.WritePhysicalInjuryDiagnostic("dfr_actual_consumer_phase_refusal", new
            {
                Expired = expired, Failure = failure?.ToString(), BeforeResult = beforeResult, AfterResult = afterResult,
                BeforeRuntime = beforeRuntime, AfterRuntime = afterRuntime,
                BeforeCounters = beforeOperations, AfterCounters = afterOperations,
                BeforeImages = RawImages(beforeImages), AfterImages = RawImages(afterImages),
                BeforeTurn = beforeTurn, AfterTurn = _runtime.GameLoop.TurnNumber,
                MutationCountBefore = mutationCount, MutationCountAfter = _postCutMutations.Count
            });
            var refused = Assert.IsType<InvalidDataException>(failure);
            Assert.Contains(expired ? "current joint attempt owner" : "debug_prefix_attempt_phase_unavailable", refused.Message);
            Assert.Equal(JsonSerializer.Serialize(beforeResult), JsonSerializer.Serialize(afterResult));
            Assert.Equal(JsonSerializer.Serialize(beforeRuntime), JsonSerializer.Serialize(afterRuntime));
            foreach (var name in new[] { "ReasoningPrefix.Capture", "ReasoningPrefix.Evaluate", "ReasoningPrefix.Recheck" })
            {
                Assert.Equal(Starts(beforeOperations, name), Starts(afterOperations, name));
                Assert.Equal(Completes(beforeOperations, name), Completes(afterOperations, name));
            }
            Assert.Equal(beforeTurn, _runtime.GameLoop.TurnNumber);
            Assert.Equal(mutationCount, _postCutMutations.Count);
            Assert.Equal(beforeImages.Keys, afterImages.Keys);
            foreach (var (path, raw) in beforeImages) Assert.Equal(raw, afterImages[path]);
        }

        private static long Completes(OperationTimingAccumulator.Snapshot snapshot, string name) =>
            snapshot.Rows.SingleOrDefault(row => row.Name == name)?.Completed ?? 0;

        /// <summary>Reads only the existing sealed scalar timing window; it grants no source or publication authority.</summary>
        /// <param name="snapshot">One detached observation reused for the current callback's phase checks.</param>
        /// <param name="name">The existing named operation whose current invocation is observed.</param>
        /// <returns>Whether that named operation has started and has not returned.</returns>
        private static bool IsOperationActive(OperationTimingAccumulator.Snapshot? snapshot, string name) =>
            snapshot?.Rows.SingleOrDefault(row => row.Name == name) is { } operation &&
            operation.Started > operation.Completed;

        private static long Starts(OperationTimingAccumulator.Snapshot snapshot, string name) =>
            snapshot.Rows.SingleOrDefault(row => row.Name == name)?.Started ?? 0;

        /// <summary>Checks the unchanged source contrast after all original full completion and inert-repeat assertions.</summary>
        internal async Task AssertCompletedAsync()
        {
            Assert.False(changeSource); Assert.Equal(1, _callbacks); Assert.True(_sourceReads > 0);
            Assert.True(_narrativeSourceReads > 0); Assert.Equal(_narrativeSourceReads, _narrativeSourceReturns);
            Assert.Equal(1, PreparationEntries);
            var currentSource = File.ReadAllBytes(_fixture!._fs.ResolvePath(SourcePath));
            Assert.Equal(_sourceRead, currentSource);
            Assert.Equal(_narrativeSourceRead, currentSource);
            var prefix = Assert.IsType<MortalJointDebugPrefixObservation>(_runtime!.Engine.CaptureMortalJointReasoningPrefixObservation());
            Assert.Equal("NeedsActorDependencies", prefix.Disposition);
            Assert.Equal(1, prefix.Evaluations); Assert.Equal(2, prefix.SourceChecks);
            Assert.Equal(1, prefix.TimestampCalls); Assert.Null(prefix.Fault);
            Assert.False(prefix.HistoricalCurrentOnly);
            Assert.True(prefix.DataDisposed); Assert.True(prefix.AttemptExpired);
            Assert.Equal(1, Starts(_window!.Capture(), "ReasoningPrefix.Capture"));
            Assert.Equal(1, _closedPrefixRefusals);
            await AssertActualPrefixPhaseRefusalAsync(expired: true);
            _fixture.WritePhysicalInjuryDiagnostic("dfr_unchanged_actual_completion", new { Prefix = prefix, Operations = _window.Capture() });
            _fixture.WritePhysicalInjuryDiagnostic("live_n_unchanged_full_completion", new
            { _callbacks, _sourceReads, _sourceReturns, PreparationEntries, Operations = _window!.Capture(), Reads = _reads,
                _narrativeSourceReads, _narrativeSourceReturns, _freshSourceReads,
                PostCutMutations = _postCutMutations, Meaning = "Original complete joint outcome and inert repeat returned." });
        }

        private Dictionary<string, byte[]?> PhysicalImages()
        {
            var files = Directory.EnumerateFiles(_fixture!._fs.GameSessionPath, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal).ToDictionary(
                    path => Path.GetRelativePath(_fixture._fs.GameSessionPath, path).Replace('\\', '/'),
                    path => (byte[]?)File.ReadAllBytes(path), StringComparer.Ordinal);
            files.Add("$sessionGeneration", File.ReadAllBytes(_fixture._fs.SessionGenerationPath));
            return files;
        }
        private static string? Raw(byte[]? bytes) => bytes is null ? null : Convert.ToBase64String(bytes);
        private static object RawImages(Dictionary<string, byte[]?> images) =>
            images.ToDictionary(pair => pair.Key, pair => Raw(pair.Value), StringComparer.Ordinal);

        /// <summary>Restores only the callbacks owned by this independent probe.</summary>
        public void Dispose()
        {
            _window?.Dispose();
            if (_fixture is null) return;
            _fixture._consoleCanonicalReadObserver = _priorRead;
            _fixture._consoleReturnedCanonicalReadObserver = _priorReturnedRead;
            _fixture._consoleMutationObserver = _priorMutation;
        }
    }
}
