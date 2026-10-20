using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Counts the first real resource validation while retaining both QTE source barriers.
    /// </summary>
    /// <returns>
    /// Completion after the genuine owner closes before common publication and its copy is removed.
    /// </returns>
    [Fact]
    public Task JointValidation_ResourceChecksBaselineExactlyTwiceInsideQte()
        => RunJointValidationControlAsync("count");

    /// <summary>
    /// Requires the QTE entry barrier to refuse changed current narrative bytes.
    /// </summary>
    /// <returns>
    /// Completion after actual entry refusal without common publication and owned cleanup.
    /// </returns>
    [Fact]
    public Task JointValidation_QteEntryRefusesChangedBeforeImage()
        => RunJointValidationControlAsync("entry-bytes");

    /// <summary>
    /// Requires the QTE exit barrier to refuse a formerly absent guarded file created during a read.
    /// </summary>
    /// <returns>
    /// Completion after actual exit refusal without common publication and owned cleanup.
    /// </returns>
    [Fact]
    public Task JointValidation_QteExitRefusesChangedAbsence()
        => RunJointValidationControlAsync("exit-absence");

    /// <summary>
    /// Imports current genuine source data into a distinct fixture and selects one component control.
    /// </summary>
    /// <param name="scenario">
    /// The count, entry-byte drift or exit-absence drift control; no fallback source generation is allowed.
    /// </param>
    /// <returns>
    /// Completion after the selected real-owner component assertions and verified root cleanup.
    /// </returns>
    private async Task RunJointValidationControlAsync(string scenario)
    {
        Assert.True(OperatingSystem.IsWindows());
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        var root = Path.Combine(Path.GetTempPath(), "boe-joint-validation-" + Guid.NewGuid().ToString("N"));
        try
        {
            var artifact = Path.Combine(TestRepoPaths.RepoRoot, "specs", "1536-complete-wound-materialization",
                "fixtures", "joint-interface-source42", "artifact");
            var seed = JointInterfaceCrashSeed.Open(artifact,
                JointInterfaceSeedContractFingerprint.Compute(TestRepoPaths.RepoRoot));
            var metadata = await seed.MaterializeAsync(root);
            using var fixture = new GameEngineTurnLifecycleTests(_directGachaOutput, root);
            using var owner = new CleanupOwnedFixture(root, line => _directGachaOutput?.WriteLine(line));
            using var interfaces = new JointInterfaceLifecycleProbe(fixture);
            using var probe = new JointValidationDedupProbe(fixture, scenario);
            await fixture.RunJointCompletionControlAsync(false, useLiveCaller: true,
                interfaceLifecycleProbe: interfaces, sourceSeed: metadata, validationDedupProbe: probe);
        }
        finally
        {
            new CleanupOwnedFixture(root, line => _directGachaOutput?.WriteLine(line)).Dispose();
            CultureInfo.CurrentCulture = previousCulture;
        }
        Assert.False(Directory.Exists(root));
    }

    /// <summary>
    /// Observes one actual resource call and injects owned test drift without receiving an authority object.
    /// </summary>
    private sealed class JointValidationDedupProbe : IDisposable
    {
        private readonly GameEngineTurnLifecycleTests _fixture;
        private readonly string _scenario;
        private readonly JointOperationTimings _timings = new();
        private readonly Stack<string> _stack = new();
        private readonly List<string> _mutations = [];
        private readonly IOException _stop = new("DEDUP component stop after first resource validation, before common publication");
        private FileSystemManagerHooks? _hooks;
        private Action<string, string, TimeSpan>? _priorTiming;
        private Func<string, Task>? _priorRead;
        private Func<string, Task>? _priorReturnedRead;
        private Action<string>? _priorMutation;
        private GameEngine? _engine;
        private GameEngine.MortalJointPublicationAttempt? _attempt;
        private FileSystemManager.CanonicalWriteLease? _lease;
        private Dictionary<string, byte[]>? _before;
        private Exception? _observerFailure;
        private string? _injectedPath, _triggerRead;
        private byte[]? _injectedBytes;
        private int _baselines, _qteBaselines, _targetCalls, _commonEntries;
        private bool _targetActive, _targetEnded, _qteActive, _armExit, _stopThrown, _comparedProjection;

        /// <summary>
        /// Selects a component assertion for an otherwise ordinary genuine fixture.
        /// </summary>
        /// <param name="fixture">
        /// The independent current-source fixture whose existing hooks will be restored on disposal.
        /// </param>
        /// <param name="scenario">
        /// The closed count, entry-byte or exit-absence control name.
        /// </param>
        internal JointValidationDedupProbe(GameEngineTurnLifecycleTests fixture, string scenario)
        {
            Assert.Contains(scenario, new[] { "count", "entry-bytes", "exit-absence" });
            _fixture = fixture;
            _scenario = scenario;
        }

        /// <summary>
        /// Installs observation and existing read fault callbacks on the actual fixture owner.
        /// </summary>
        /// <param name="engine">
        /// The real engine that will perform natural Stage43 and joint repair.
        /// </param>
        internal void Attach(GameEngine engine)
        {
            Assert.Null(_engine);
            _engine = engine;
            _hooks = GetPrivateField<FileSystemManagerHooks>(_fixture._fs, "_hooks");
            _priorTiming = _hooks.OperationTimingObserver;
            TimingProperty().SetValue(_hooks, (Action<string, string, TimeSpan>)ObserveOperation);
            _priorRead = _fixture._consoleCanonicalReadObserver;
            _priorReturnedRead = _fixture._consoleReturnedCanonicalReadObserver;
            _priorMutation = _fixture._consoleMutationObserver;
            _fixture._consoleCanonicalReadObserver = async path =>
            {
                if (_priorRead is not null) await _priorRead(path);
                if (_scenario == "count" && _targetEnded && !_stopThrown)
                {
                    _stopThrown = true;
                    throw _stop;
                }
            };
            _fixture._consoleReturnedCanonicalReadObserver = async path =>
            {
                if (_priorReturnedRead is not null) await _priorReturnedRead(path);
                if (_scenario == "exit-absence" && _armExit && _qteActive && _injectedPath is null)
                {
                    _triggerRead = path;
                    Inject(TrainingRequestState.PendingRequestPath, Encoding.UTF8.GetBytes("{}"));
                }
            };
            _fixture._consoleMutationObserver = path =>
            {
                _priorMutation?.Invoke(path);
                if (_targetActive || _targetEnded) _mutations.Add(path);
            };
        }

        /// <summary>
        /// Retains every timing record and tracks nesting within the first actual resource validation.
        /// </summary>
        /// <param name="name">
        /// The existing literal operation name.
        /// </param>
        /// <param name="boundary">
        /// The existing begin or end boundary; an end alone does not establish success.
        /// </param>
        /// <param name="elapsed">
        /// The inclusive elapsed time provided by the production observer.
        /// </param>
        private void ObserveOperation(string name, string boundary, TimeSpan elapsed)
        {
            _priorTiming?.Invoke(name, boundary, elapsed);
            _timings.Observe(name, boundary, elapsed);
            try
            {
                if (boundary == "begin" && name == "JointCommon.CreateAuthority") _commonEntries++;
                if (boundary == "begin" && name == "JointResources.ValidateCurrentAsync")
                {
                    _targetCalls++;
                    if (_targetCalls == 1)
                    {
                        _targetActive = true;
                        _attempt = GetPrivateField<GameEngine.MortalJointPublicationAttempt>(_engine!, "_mortalJointPublicationAttempt");
                        _lease = GetPrivateField<FileSystemManager.CanonicalWriteLease>(_attempt, "_lease");
                        Assert.True(_attempt.Live);
                        Assert.True(_lease.IsActive);
                        Assert.NotNull(_attempt.Continuation);
                        Assert.NotNull(_attempt.Reservation);
                        _before = PhysicalImages();
                        Assert.False(_before.ContainsKey(TrainingRequestState.PendingRequestPath));
                    }
                }
                if (!_targetActive) return;
                if (boundary == "begin")
                {
                    _stack.Push(name);
                    if (name == "JointResources.RecomputeQteAsync")
                    {
                        _qteActive = true;
                        if (_scenario == "entry-bytes")
                        {
                            const string path = "output/narrative_response.json";
                            Inject(path, _before![path].Concat(new byte[] { 32 }).ToArray());
                        }
                    }
                    if (name == "JointBaseline.ValidateCurrentAsync")
                    {
                        _baselines++;
                        if (_qteActive) _qteBaselines++;
                    }
                    if (name == "JointResources.CompareCompanionProjection") _comparedProjection = true;
                }
                else
                {
                    Assert.Equal(name, _stack.Pop());
                    if (name == "JointBaseline.ValidateCurrentAsync" && _qteActive && _qteBaselines == 1)
                        _armExit = true;
                    if (name == "JointResources.RecomputeQteAsync") _qteActive = false;
                    if (name == "JointResources.ValidateCurrentAsync")
                    {
                        Assert.Empty(_stack);
                        _targetActive = false;
                        _targetEnded = true;
                    }
                }
            }
            catch (Exception error) { _observerFailure ??= error; }
        }

        /// <summary>
        /// Changes one physically owned current input while retaining its drift through refusal.
        /// </summary>
        /// <param name="path">
        /// The exact fixture-relative guarded file to alter or create.
        /// </param>
        /// <param name="bytes">
        /// The owned replacement bytes; the probe does not restore them to resurrect an authority.
        /// </param>
        private void Inject(string path, byte[] bytes)
        {
            Assert.Null(_injectedPath);
            File.WriteAllBytes(_fixture._fs.ResolvePath(path), bytes);
            _injectedPath = path;
            _injectedBytes = bytes.ToArray();
        }

        /// <summary>
        /// Verifies the actual stopped or refused owner without running ordinary completion assertions.
        /// </summary>
        /// <param name="engine">
        /// The same engine observed at the first resource call.
        /// </param>
        /// <param name="failure">
        /// Any exception escaping the ordinary caller, which must be absent after its held classification.
        /// </param>
        internal void AssertStopped(GameEngine engine, Exception? failure)
        {
            Assert.Same(_engine, engine);
            var attempt = GetPrivateField<GameEngine.MortalJointPublicationAttempt>(engine, "_mortalJointPublicationAttempt");
            var stages = GetPrivateField<IDictionary>(attempt, "_stages");
            var refresh = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCanonicalRefresh");
            var issues = refresh?.GetType().GetProperty("Issues")?.GetValue(refresh) as IReadOnlyList<ValidationIssue>;
            _fixture.WritePhysicalInjuryDiagnostic("joint_validation_dedup_component", new
            {
                Scenario = _scenario, TotalBaselines = _baselines, QteBaselines = _qteBaselines,
                TargetCalls = _targetCalls, TargetEnded = _targetEnded, ProjectionCompared = _comparedProjection,
                CommonEntries = _commonEntries, StopThrown = _stopThrown, InjectedPath = _injectedPath,
                TriggerRead = _triggerRead, ObserverFailure = _observerFailure?.ToString(),
                EscapedFailure = failure?.ToString(), ActualOwner = attempt.ExportDiagnostic(),
                Issues = issues?.Select(issue => new { issue.Code, issue.Message }),
                Meaning = "Real resource-validation component only; no common publication or gameplay PASS"
            });
            Assert.Null(_observerFailure);
            Assert.Null(failure);
            Assert.Same(_attempt, attempt);
            Assert.True(_targetEnded);
            Assert.Equal(1, _targetCalls);
            Assert.False(attempt.Live);
            Assert.False(_lease!.IsActive);
            Assert.True(attempt.CloseRecorded);
            Assert.False(attempt.Delegated);
            Assert.DoesNotContain(stages.Keys.Cast<object>(), stage => stage.ToString() is "Common" or "Preflight");
            Assert.Equal(0, _commonEntries);
            Assert.Empty(_mutations);
            Assert.Equal("PublicationUncertain", refresh?.GetType().GetProperty("Disposition")?.GetValue(refresh)?.ToString());
            Assert.NotNull(issues);
            if (_scenario == "count")
            {
                Assert.True(_stopThrown);
                Assert.True(_comparedProjection);
                Assert.Contains(issues!, issue => issue.Message.Contains(_stop.Message, StringComparison.Ordinal));
                Assert.Equal(2, _qteBaselines);
            }
            else
            {
                Assert.False(_stopThrown);
                Assert.False(_comparedProjection);
                Assert.NotNull(_injectedPath);
                Assert.Contains(issues!, issue => issue.Message.Contains("changed", StringComparison.OrdinalIgnoreCase));
                Assert.Equal(_scenario == "entry-bytes" ? 1 : 2, _qteBaselines);
                if (_scenario == "exit-absence") Assert.NotNull(_triggerRead);
            }
            var after = PhysicalImages();
            var expected = _before!.ToDictionary(row => row.Key, row => row.Value, StringComparer.Ordinal);
            if (_injectedPath is not null) expected[_injectedPath] = _injectedBytes!;
            Assert.Equal(expected.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
            foreach (var (path, bytes) in expected) Assert.Equal(bytes, after[path]);
            // The causal count assertion follows genuine owner/refusal and byte-preservation evidence.
            if (_scenario == "count") Assert.Equal(2, _baselines);
        }

        /// <summary>
        /// Captures current owned physical bytes without minting validation evidence.
        /// </summary>
        /// <returns>
        /// All game-session files and exact generation bytes for the no-publication assertion.
        /// </returns>
        private Dictionary<string, byte[]> PhysicalImages()
        {
            var images = Directory.EnumerateFiles(_fixture._fs.GameSessionPath, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal).ToDictionary(
                    path => Path.GetRelativePath(_fixture._fs.GameSessionPath, path).Replace('\\', '/'),
                    File.ReadAllBytes, StringComparer.Ordinal);
            images.Add("$sessionGeneration", File.ReadAllBytes(_fixture._fs.SessionGenerationPath));
            return images;
        }

        /// <summary>
        /// Locates the existing timing property without adding a production seam.
        /// </summary>
        /// <returns>
        /// The existing init property used only to install and restore this fixture's observer.
        /// </returns>
        private static PropertyInfo TimingProperty() => typeof(FileSystemManagerHooks).GetProperty(
            nameof(FileSystemManagerHooks.OperationTimingObserver), BindingFlags.Instance | BindingFlags.NonPublic)!;

        /// <summary>
        /// Restores this fixture's prior callbacks after its current owner has closed.
        /// </summary>
        public void Dispose()
        {
            if (_hooks is null) return;
            TimingProperty().SetValue(_hooks, _priorTiming);
            _fixture._consoleCanonicalReadObserver = _priorRead;
            _fixture._consoleReturnedCanonicalReadObserver = _priorReturnedRead;
            _fixture._consoleMutationObserver = _priorMutation;
        }
    }
}
