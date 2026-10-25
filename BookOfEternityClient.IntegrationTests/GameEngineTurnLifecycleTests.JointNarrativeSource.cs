using System.Text;
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

    /// <summary>
    /// Runs a fresh genuine Stage/Ready owner with named preparation observations, without sharing mutable fixture state.
    /// </summary>
    /// <param name="changeSource">Whether to change the consumed achievements bytes at the existing capture checkpoint.</param>
    /// <returns>Completion after held or original full completion assertions and owned cleanup.</returns>
    private async Task RunLiveNarrativeSourceControlAsync(bool changeSource)
    {
        using var probe = new JointNarrativeSourceProbe(changeSource);
        using var fixture = new GameEngineTurnLifecycleTests(_directGachaOutput, adoptedRoot: null,
            passiveOperationTimings: probe.Timings);
        await fixture.RunJointCompletionControlAsync(false, useLiveCaller: true, narrativeSourceProbe: probe);
    }

    // Observations and one public-source intervention only. No fabricated N/I
    // result, original, Ready, lease, registry access or capability is introduced.
    private sealed class JointNarrativeSourceProbe(bool changeSource) : IDisposable
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
        private byte[]? _sourceRead, _sourceAtCut;
        private bool _armed, _cut;
        private int _sourceReads, _sourceReturns, _callbacks, _turnAtCut;
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
                _reads.Add(new { Path = path, SourceRaw = Raw(raw) });
                if (path != SourcePath) return;
                _sourceReads++; _sourceRead = raw;
            };
            fixture._consoleReturnedCanonicalReadObserver = async path =>
            {
                if (_priorReturnedRead is not null) await _priorReturnedRead(path);
                if (!_armed || _cut || path != SourcePath) return;
                _sourceReturns++;
                _reads.Add(new { Path = path, Returned = true });
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
        }

        /// <summary>Changes one real N dependency after N returned and Capturing completed, before preparation.</summary>
        /// <param name="checkpoint">The real engine checkpoint after its prior fixture observer.</param>
        internal void ObserveCheckpoint(SessionFinalizationCheckpoint checkpoint)
        {
            if (checkpoint != SessionFinalizationCheckpoint.MortalJointCustodyCaptureCompletedBeforePreparation) return;
            Assert.True(_armed); Assert.False(_cut); _callbacks++;
            Assert.True(_sourceReads > 0, "Corrected pre-intake N must actually read achievements before the capture checkpoint.");
            Assert.Equal(_sourceReads, _sourceReturns);
            var fixture = _fixture!;
            var path = fixture._fs.ResolvePath(SourcePath);
            var before = File.ReadAllBytes(path);
            Assert.Equal(_sourceRead, before);
            // JSON whitespace changes exact consumed bytes while preserving all
            // ordinary achievement semantics and the genuinely signed original.
            if (changeSource) File.WriteAllBytes(path, before.Concat(Encoding.UTF8.GetBytes("\n ")).ToArray());
            _sourceAtCut = File.ReadAllBytes(path);
            Assert.Equal(changeSource, !before.AsSpan().SequenceEqual(_sourceAtCut));
            _turnAtCut = _runtime!.GameLoop.TurnNumber;
            _atCut = PhysicalImages();
            _cut = true;
            _window = Timings.StartWindow();
            fixture.WritePhysicalInjuryDiagnostic("live_n_actual_dependency_cut", new
            {
                changeSource, Callback = _callbacks, ActualCorrectedSourceReads = _sourceReads, ActualCorrectedSourceReturns = _sourceReturns,
                SourcePath, Before = Raw(before), After = Raw(_sourceAtCut), Reads = _reads,
                Runtime = _runtime.Engine.CaptureMortalJointRuntimeObservation(), Turn = _turnAtCut,
                Images = RawImages(_atCut)
            });
        }

        /// <summary>Asserts causal refusal before preparation and exact physical effect preservation.</summary>
        /// <param name="engine">The real engine after its ordinary owner returned.</param>
        /// <param name="diagnostics">Complete existing logger diagnostics.</param>
        internal void AssertHeld(GameEngine engine, string diagnostics)
        {
            Assert.Same(_runtime!.Engine, engine);
            var after = PhysicalImages();
            var runtime = engine.CaptureMortalJointRuntimeObservation();
            _fixture!.WritePhysicalInjuryDiagnostic("live_n_actual_source_refusal", new
            {
                changeSource, _callbacks, _sourceReads, _sourceReturns, PreparationEntries, PostCutMutations = _postCutMutations,
                Runtime = runtime, EngineDiagnostics = diagnostics, Operations = _window!.Capture(),
                CutImages = RawImages(_atCut!), AfterImages = RawImages(after),
                TurnAtCut = _turnAtCut, TurnAfter = _runtime.GameLoop.TurnNumber
            });
            // FIRST intended baseline failure: the omitted consumed dependency
            // reaches real preparation. Compile/setup/missing API are never RED.
            Assert.Equal(0, PreparationEntries);
            Assert.Equal(1, _callbacks); Assert.True(_sourceReads > 0);
            Assert.True(runtime.CheckpointPresent); Assert.True(runtime.CorrectionReady);
            Assert.True(runtime.CanonicalRefreshPresent);
            Assert.Equal("PublicationUncertain", runtime.CanonicalDisposition);
            Assert.NotEmpty(runtime.Issues!);
            Assert.Equal(_turnAtCut, _runtime.GameLoop.TurnNumber);
            Assert.Empty(_postCutMutations);
            Assert.Equal(_atCut!.Keys, after.Keys);
            foreach (var (path, raw) in _atCut) Assert.Equal(raw, after[path]);
            Assert.Equal(_sourceAtCut, after[SourcePath]);
        }

        /// <summary>Checks the unchanged source contrast after all original full completion and inert-repeat assertions.</summary>
        internal void AssertCompleted()
        {
            Assert.False(changeSource); Assert.Equal(1, _callbacks); Assert.True(_sourceReads > 0);
            Assert.Equal(1, PreparationEntries);
            Assert.Equal(_sourceRead, File.ReadAllBytes(_fixture!._fs.ResolvePath(SourcePath)));
            _fixture.WritePhysicalInjuryDiagnostic("live_n_unchanged_full_completion", new
            { _callbacks, _sourceReads, _sourceReturns, PreparationEntries, Operations = _window!.Capture(), Reads = _reads,
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
