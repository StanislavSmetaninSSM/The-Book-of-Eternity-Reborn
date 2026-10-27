using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Qualifies small genuine source and joint planning fixtures without running the game loop or generating a world.
/// </summary>
[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed partial class JointBundleComponentTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Selects the case receiver for compact references to durable component diagnostics.
    /// </summary>
    /// <param name="output">
    /// The actual xUnit receiver for paths, status and the final fixture closing receipt.
    /// </param>
    public JointBundleComponentTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Obtains a genuine ordinary original and progression source from small canonical data and the real issuers.
    /// Rejects foreign coordinates and leases, a disposed lease, and corrupted current request or authority bytes.
    /// </summary>
    [Fact]
    public async Task OrdinaryOriginal_UsesNormalSnapshotAndProgressionFactories()
    {
        await using var fixture = new Fixture(_output, nameof(OrdinaryOriginal_UsesNormalSnapshotAndProgressionFactories));
        await fixture.InitializeAsync();
        var original = await fixture.PrepareOriginalAsync(43);
        var read = PendingTurnSnapshotReader.ReadCurrent(fixture.Files, fixture.Lease,
            ["game_state/meta/soul_state.json", ProgressionScheduleService.SchedulePath]);
        Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
        Assert.Equal(original.SessionId, read.Snapshot!.SessionId);
        Assert.Equal(original.RequestId, read.Snapshot.RequestId);
        Assert.Equal(original.SnapshotToken, read.Snapshot.SnapshotToken);
        Assert.Equal(original.Turn, read.Snapshot.TurnNumber);
        Assert.True(fixture.Files.IsCurrentSessionGeneration(fixture.Lease, original.Generation));
        await fixture.AssertOrdinaryOriginalRefusalsAsync(original);
        using var timing = fixture.Files.MeasureOperation("JointBundleFixture.CaptureProgression");
        var progression = await fixture.Runtime.Engine.CaptureJointProgressionForComponentAsync(
            fixture.Lease, original);
        Assert.True(progression.HasValidSeal);
        await progression.ValidateCurrentAsync(fixture.Files, fixture.Lease);
        Assert.False(fixture.Files.FileExists(fixture.Lease, MortalWoundOccurrenceState.StatePath));
    }

    /// <summary>
    /// Owns a fresh container, a small canonical data root and the real services used only as factory hosts.
    /// </summary>
    private sealed partial class Fixture : IAsyncDisposable
    {
        private readonly string _parent;
        private readonly string _owned;
        private readonly string _nonce;
        private readonly JointBundleDiagnosticOutput _output;
        private FileSystemManager.CanonicalWriteLease? _lease;
        private GameEngineTestRuntime? _runtime;
        private ProgressionControl? _progression;
        private bool _retained;
        private bool _disposed;
        private Exception? _disposeFailure;
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string Name, string Boundary, double Milliseconds, long SourceScopeId)> _observations = new();

        /// <summary>
        /// Registers independent diagnostics, then allocates an ordinary nonce-owned game container.
        /// </summary>
        /// <param name="output">
        /// The actual xUnit receiver for compact references and the closing receipt.
        /// </param>
        /// <param name="caseName">
        /// The actual nonempty test case name registered before allocating game files.
        /// </param>
        /// <param name="passiveTimingEvents">
        /// Whether this explicit expensive control captures every original named event through the sealed recorder.
        /// </param>
        /// <param name="gzipRecords">Whether this explicit control uses the existing lossless gzip records sink; ordinary fixtures stay plain.</param>
        /// <param name="continuingPassiveTimings">Requests the shared component transfer route; RED keeps its original after-return policy.</param>
        internal Fixture(ITestOutputHelper output, string caseName, bool passiveTimingEvents = false, bool gzipRecords = false,
            bool continuingPassiveTimings = false)
        {
            if (continuingPassiveTimings && !passiveTimingEvents)
                throw new ArgumentException("Shared event transfer requires passive original events.", nameof(continuingPassiveTimings));
            var started = Stopwatch.GetTimestamp();
            _output = new JointBundleDiagnosticOutput(caseName, output, gzipRecords: gzipRecords);
            if (passiveTimingEvents)
            {
                _passiveTimings = new OperationTimingAccumulator();
                _passiveWindow = _passiveTimings.StartWindow(captureEvents: true);
                _output.RequirePassiveTiming();
                _output.WriteLine(JsonSerializer.Serialize(new { Kind = "PassiveNamedTimingMode",
                    Mode = "sealed-passive-events-v1", ClockFrequency = Stopwatch.Frequency,
                    OriginalEvents = true, Quota = false }));
            }
            _nonce = _output.FixtureNonce;
            _parent = Path.GetFullPath(Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-bundle-component"));
            Directory.CreateDirectory(_parent);
            _owned = _output.ExpectedOwnedRoot;
            if (Directory.Exists(_owned) || File.Exists(_owned)) throw new InvalidDataException("Component destination exists.");
            Directory.CreateDirectory(_owned);
            File.WriteAllText(Path.Combine(_owned, ".fixture-owner"), _nonce);
            var game = Path.Combine(_owned, "game");
            Directory.CreateDirectory(game);
            Files = new FileSystemManager(game, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks { OperationTimingObserver = passiveTimingEvents ? null : Observe,
                    PassiveOperationTimings = _passiveTimings });
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Name = "JointBundleFixture.AllocateNamespace", Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Owned = _owned, Game = game
            }));
            // Last successful construction step; no worker can outlive an unreturned fixture.
            if (continuingPassiveTimings)
                _passiveTransfer = LifecyclePassiveTimingDiagnostics.RecordedTransfer.CreateForComponent(
                    _passiveWindow!, _output.Timings, _observations);
        }

        /// <summary>
        /// Gets the authoritative complete diagnostics receiver for this fixture.
        /// </summary>
        internal JointBundleDiagnosticOutput Output => _output;

        /// <summary>
        /// Gets the actual filesystem rooted inside this fixture's owned container.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets the real issuer host, or refuses access before it has been constructed.
        /// </summary>
        internal GameEngineTestRuntime Runtime =>
            _runtime ?? throw new InvalidOperationException("The component issuer host has not been constructed.");

        /// <summary>
        /// Gets this fixture's participating lease, or refuses access before initialization.
        /// </summary>
        internal FileSystemManager.CanonicalWriteLease Lease =>
            _lease ?? throw new InvalidOperationException("The component lease has not been acquired.");

        /// <summary>
        /// Seeds small typed data and initializes the normal schedule, issuer services and current generation.
        /// </summary>
        /// <returns>
        /// A task completing when this fixture owns its participating canonical lease.
        /// </returns>
        internal async Task InitializeAsync()
        {
            using var total = Files.MeasureOperation("JointBundleFixture.Initialize");
            try
            {
                using (Files.MeasureOperation("JointBundleFixture.SeedCanonicalData"))
                {
                    Files.EnsureDirectoryStructure();
                    MortalWoundTreatmentResolverTests.SeedMinimalJointComponentData(Files);
                }
                using (Files.MeasureOperation("JointBundleFixture.BuildProgressionControl"))
                    _progression = await new ProgressionScheduleService(Files, NullLogger<ProgressionScheduleService>.Instance)
                        .BuildControlForNextTurnAsync("Mortal World");
                using (Files.MeasureOperation("JointBundleFixture.ConstructIssuerHost"))
                    _runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(Files);
                using (Files.MeasureOperation("JointBundleFixture.AcquireLeaseAndGeneration"))
                {
                    _lease = await Files.AcquireCanonicalWriteLeaseAsync();
                    _ = Files.GetOrCreateSessionGeneration(_lease);
                }
            }
            catch (CoordinatedStatePublicationUncertainException)
            {
                _retained = true;
                throw;
            }
        }

        /// <summary>
        /// Creates an ordinary original using the existing engine producer and this fixture's actual schedule control.
        /// </summary>
        /// <param name="turn">
        /// The positive source turn to include in the request and authenticated original.
        /// </param>
        /// <returns>
        /// Data coordinates of the genuinely produced original; these coordinates grant no authority.
        /// </returns>
        internal async Task<GameEngine.JointComponentOriginal> PrepareOriginalAsync(int turn)
        {
            using var timing = Files.MeasureOperation("JointBundleFixture.PrepareOrdinaryOriginal");
            try
            {
                return await Runtime.Engine.PrepareOrdinaryOriginalForComponentAsync(Lease, new TurnRequest
                {
                    SessionId = "session_component_" + _nonce,
                    RequestId = "request_component_" + turn + "_" + _nonce,
                    TurnNumber = turn,
                    PlayerAction = "Component source preparation",
                    PreGeneratedDices1d20 = [4, 17, 1, 20],
                    ProgressionControl = _progression
                });
            }
            catch (CoordinatedStatePublicationUncertainException)
            {
                _retained = true;
                throw;
            }
        }

        /// <summary>
        /// Retains every observation for candidate slicing and immediately offers it to the uncapped persistent capture.
        /// Storage failures remain in that capture and must refuse complete fixture closing.
        /// </summary>
        /// <param name="name">
        /// The literal operation name received from the real filesystem or setup phase.
        /// </param>
        /// <param name="boundary">
        /// The actual begin or end boundary.
        /// </param>
        /// <param name="elapsed">
        /// Zero for begin, or the inclusive elapsed duration for end.
        /// </param>
        private void Observe(string name, string boundary, TimeSpan elapsed)
        {
            _observations.Enqueue((name, boundary, elapsed.TotalMilliseconds, 0));
            _output.Timings.Observe(name, boundary, elapsed);
        }

        /// <summary>
        /// Aborts an unpublished reservation and closes services and lease, then finishes diagnostics
        /// before deleting only this marked container. Failure before deletion retains the root.
        /// A late final-receipt failure fails closing but cannot retain an already deleted root.
        /// </summary>
        /// <returns>
        /// A task completing after resource closing and the owned-root closing or retention receipt.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                if (_disposeFailure is not null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(_disposeFailure).Throw();
                return;
            }
            _disposed = true;
            var failures = new List<Exception>();
            try { CloseJointReservation(); }
            catch (Exception failure) { failures.Add(failure); _retained = true; }
            try { if (_runtime is not null) await _runtime.AudioService.DisposeAsync(); }
            catch (Exception failure) { failures.Add(failure); _retained = true; }
            try { if (_lease is not null) await _lease.DisposeAsync(); }
            catch (Exception failure) { failures.Add(failure); _retained = true; }
            try { await CompletePassiveTimingsAsync(); }
            catch (Exception failure)
            {
                failures.Add(failure); _retained = true;
                _output.Timings.RecordDiagnosticFailure(failure, "passive-timing-close");
            }
            try { _output.Finish(); }
            catch (Exception failure) { failures.Add(failure); _retained = true; }
            try
            {
                if (!_retained)
                {
                    var scope = new TrustedLocalFileScope([_parent]);
                    var marker = scope.ValidateFile(Path.Combine(_owned, ".fixture-owner"), allowMissing: false);
                    if (Path.GetDirectoryName(_owned) != _parent || Path.GetFileName(_owned) != _nonce || File.ReadAllText(marker) != _nonce)
                        throw new InvalidDataException("Component closing ownership changed.");
                    scope.DeleteOwnedTree(_owned);
                }
            }
            catch (Exception failure) { failures.Add(failure); _retained = true; }
            try
            {
                _output.RecordFixtureClosing(_owned, _retained, !Directory.Exists(_owned),
                    failures.Select(failure => failure.ToString()).ToArray());
            }
            catch (Exception failure) { failures.Add(failure); _retained = true; }
            if (failures.Count != 0)
            {
                _disposeFailure = new AggregateException("Component closing failed; inspect the actual root and diagnostic receipts.", failures);
                throw _disposeFailure;
            }
            if (!_retained) Assert.False(Directory.Exists(_owned));
        }
    }
}
