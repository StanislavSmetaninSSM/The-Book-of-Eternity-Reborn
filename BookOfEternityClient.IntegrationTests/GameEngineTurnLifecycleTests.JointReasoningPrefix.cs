using System.Globalization;
using System.Text;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_EmptyReasoningPrefixRefusesBeforeCommonPreparation()
    {
        using var probe = new JointReasoningPrefixProbe();
        using var fixture = new GameEngineTurnLifecycleTests(_directGachaOutput, adoptedRoot: null,
            passiveOperationTimings: probe.Timings);
        await fixture.RunJointCompletionControlAsync(false, useLiveCaller: true, reasoningPrefixProbe: probe);
    }

    // This authors only an ordinary corrected public response. Its original,
    // Ready, N/I invocation and Capturing owner all come from the real driver.
    private sealed class JointReasoningPrefixProbe : IDisposable
    {
        private const string DebugPath = "output/debug_logs.json";
        private const string PreparationOperation = "GameEngine.CreateJointRepairPreparationAsync";
        private const string CaptureOperation = "GameEngine.CaptureCurrentHotCustodyAsync";
        private readonly List<object> _reads = [];
        private readonly List<string> _postCutMutations = [];
        private OperationTimingAccumulator.Window? _window;
        private GameEngineTurnLifecycleTests? _fixture;
        private GameEngineTestRuntime? _runtime;
        private Func<string, Task>? _priorRead, _priorReturnedRead;
        private Action<string>? _priorMutation;
        private byte[]? _submitted, _narrativeRead;
        private bool _observedDebugChanged;
        private Dictionary<string, byte[]?>? _atCut;
        private MortalJointRuntimeObservation? _runtimeAtCut;
        private OperationTimingAccumulator.Snapshot? _operationsAtCut;
        private int _narrativeReads, _narrativeReturns, _turnAtCut;

        internal OperationTimingAccumulator Timings { get; } = new();

        /// <summary>Changes thoughts only in the real complete corrected submission, before public readback and N.</summary>
        internal void ConfigureCorrectedOutputs(Dictionary<string, byte[]> outputs)
        {
            Assert.Null(_submitted);
            var debug = ParseJsonObjectBytes(outputs[DebugPath]);
            var timestamp = debug["timestamp"]!.GetValue<string>();
            Assert.True(DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out _), "The existing authored debug timestamp must be valid.");
            debug["gm_thoughts_markdown"] = string.Empty;
            _submitted = Encoding.UTF8.GetBytes(debug.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            outputs[DebugPath] = _submitted.ToArray();
        }

        /// <summary>Arms after the existing physical public-response readback, before the correlated repair Ready.</summary>
        internal void ObserveCorrectedSubmissionReadback()
        {
            Assert.Null(_window);
            Assert.NotNull(_submitted);
            Assert.Equal(_submitted, File.ReadAllBytes(_fixture!._fs.ResolvePath(DebugPath)));
            _window = Timings.StartWindow();
        }

        /// <summary>Observes existing physical read callbacks and sealed named counters without invoking any owner.</summary>
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
                if (_window is null || path != DebugPath) return;
                var operations = _window.Capture();
                var raw = File.ReadAllBytes(fixture._fs.ResolvePath(path));
                _observedDebugChanged |= _submitted is null || !_submitted.AsSpan().SequenceEqual(raw);
                var inNarrative = Active(operations, "NarrativePolicy.Evaluate");
                var inCapture = Active(operations, CaptureOperation);
                var inRecheck = Active(operations, "NarrativePolicy.Recheck");
                _reads.Add(new { Path = path, Raw = Raw(raw), InNarrative = inNarrative,
                    InCapture = inCapture, InRecheck = inRecheck });
                if (inNarrative) { _narrativeReads++; _narrativeRead = raw; }
                // The actual N fresh check is inside Capturing after I consumption.
                // Freeze here so an earlier prefix refusal need not reach the later
                // completed-capture checkpoint. No source intervention occurs here.
                if (_atCut is null && inCapture && inRecheck)
                {
                    _runtimeAtCut = runtime.Engine.CaptureMortalJointRuntimeObservation();
                    _operationsAtCut = operations;
                    _turnAtCut = runtime.GameLoop.TurnNumber;
                    _atCut = PhysicalImages();
                    fixture.WritePhysicalInjuryDiagnostic("dfr_actual_n_i_capture_cut", new
                    {
                        Submitted = Raw(_submitted), Reads = _reads.ToArray(), Runtime = _runtimeAtCut,
                        Operations = operations, Turn = _turnAtCut, Images = RawImages(_atCut)
                    });
                }
            };
            fixture._consoleReturnedCanonicalReadObserver = async path =>
            {
                if (_priorReturnedRead is not null) await _priorReturnedRead(path);
                if (_window is null || path != DebugPath) return;
                var inNarrative = Active(_window.Capture(), "NarrativePolicy.Evaluate");
                if (inNarrative) _narrativeReturns++;
                _reads.Add(new { Path = path, Returned = true, InNarrative = inNarrative });
            };
            fixture._consoleMutationObserver = path =>
            {
                _priorMutation?.Invoke(path);
                if (_atCut is not null) _postCutMutations.Add(path);
            };
        }

        /// <summary>Requires the genuine temporal prerequisites before the sole intended old-product failure.</summary>
        internal void AssertRefusedBeforePreparation(GameEngine engine, string diagnostics)
        {
            Assert.Same(_runtime!.Engine, engine);
            var runtime = engine.CaptureMortalJointRuntimeObservation();
            var operations = _window?.Capture();
            var after = PhysicalImages();
            var preparationEntries = Starts(operations, PreparationOperation);
            _fixture!.WritePhysicalInjuryDiagnostic("dfr_actual_prefix_temporal_outcome", new
            {
                Submitted = Raw(_submitted), NarrativeRead = Raw(_narrativeRead), _observedDebugChanged,
                _narrativeReads, _narrativeReturns,
                CutReached = _atCut is not null, RuntimeAtCut = _runtimeAtCut,
                OperationsAtCut = _operationsAtCut, Runtime = runtime, EngineDiagnostics = diagnostics,
                PreparationEntries = preparationEntries, Operations = operations, Reads = _reads,
                PostCutMutations = _postCutMutations,
                CutImages = _atCut is null ? null : RawImages(_atCut), AfterImages = RawImages(after),
                TurnAtCut = _turnAtCut, TurnAfter = _runtime.GameLoop.TurnNumber
            });
            Assert.NotNull(_window);
            Assert.NotNull(_atCut);
            Assert.NotNull(_runtimeAtCut);
            Assert.Equal(1, _narrativeReads);
            Assert.Equal(_narrativeReads, _narrativeReturns);
            Assert.Equal(_submitted, _narrativeRead);
            Assert.False(_observedDebugChanged);
            Assert.Equal(1, Starts(_operationsAtCut, "NarrativePolicy.Evaluate"));
            Assert.Equal(1, Completed(_operationsAtCut, "NarrativePolicy.Evaluate"));
            Assert.Equal(1, Starts(_operationsAtCut, "MortalJointInterface.PrepareResult"));
            Assert.Equal(1, Completed(_operationsAtCut, "MortalJointInterface.PrepareResult"));
            Assert.Equal(1, Starts(_operationsAtCut, CaptureOperation));
            Assert.Equal(0, Completed(_operationsAtCut, CaptureOperation));
            Assert.Equal(0, Starts(_operationsAtCut, PreparationOperation));
            Assert.True(_runtimeAtCut.CheckpointPresent);
            Assert.True(_runtimeAtCut.CorrectionReady);
            Assert.True(_runtimeAtCut.FreshConsumed);
            Assert.Equal("MatchedLiveN", _runtimeAtCut.NarrativeContribution);
            Assert.Equal(42, _turnAtCut);
            Assert.Equal(_submitted, _atCut[DebugPath]);
            Assert.Equal(_submitted, after[DebugPath]);
            Assert.NotNull(runtime.Issues);
            Assert.Single(runtime.Issues, issue => issue.Code == "missing_gm_thoughts" && issue.FilePath == DebugPath);
            Assert.NotEqual("KnownLiveNHold", runtime.NarrativeContribution);
            if (preparationEntries > 0)
            {
                // Baseline must reach genuine late Full with exactly the authored
                // prefix error; unrelated admission/setup failures are not causal RED.
                Assert.True(Starts(operations, "JointCompletion.ValidateReasoningAsync") > 0);
                Assert.True(Completed(operations, "JointCompletion.ValidateReasoningAsync") > 0);
            }
            // FIRST intended causal baseline failure: old code prepared before
            // late Full diagnosed the unchanged, N-compatible empty debug prefix.
            Assert.Equal(0, preparationEntries);
            Assert.True(runtime.CheckpointPresent);
            Assert.True(runtime.CorrectionReady);
            Assert.Equal(_turnAtCut, _runtime.GameLoop.TurnNumber);
            Assert.Empty(_postCutMutations);
            Assert.Equal(_atCut.Keys, after.Keys);
            foreach (var (path, raw) in _atCut) Assert.Equal(raw, after[path]);
        }

        private static bool Active(OperationTimingAccumulator.Snapshot snapshot, string name) =>
            Starts(snapshot, name) > Completed(snapshot, name);
        private static long Starts(OperationTimingAccumulator.Snapshot? snapshot, string name) =>
            snapshot?.Rows.SingleOrDefault(row => row.Name == name)?.Started ?? 0;
        private static long Completed(OperationTimingAccumulator.Snapshot? snapshot, string name) =>
            snapshot?.Rows.SingleOrDefault(row => row.Name == name)?.Completed ?? 0;

        private Dictionary<string, byte[]?> PhysicalImages()
        {
            var fixture = _fixture!;
            var images = Directory.EnumerateFiles(fixture._fs.GameSessionPath, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal).ToDictionary(
                    path => Path.GetRelativePath(fixture._fs.GameSessionPath, path).Replace('\\', '/'),
                    path => (byte[]?)File.ReadAllBytes(path), StringComparer.Ordinal);
            images.Add("$sessionGeneration", File.ReadAllBytes(fixture._fs.SessionGenerationPath));
            return images;
        }
        private static string? Raw(byte[]? bytes) => bytes is null ? null : Convert.ToBase64String(bytes);
        private static object RawImages(Dictionary<string, byte[]?> images) =>
            images.ToDictionary(pair => pair.Key, pair => Raw(pair.Value), StringComparer.Ordinal);

        /// <summary>Restores only this probe's callbacks and closes its passive window.</summary>
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
