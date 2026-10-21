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
    /// Retains the complete setup, diagnostic and ownership output for this test.
    /// </summary>
    /// <param name="output">
    /// The current test output receiver, without a diagnostic retention limit.
    /// </param>
    public JointBundleComponentTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Obtains a genuine ordinary original and progression source from small canonical data and the real issuers.
    /// Rejects foreign coordinates and leases, a disposed lease, and corrupted current request or authority bytes.
    /// </summary>
    [Fact]
    public async Task OrdinaryOriginal_UsesNormalSnapshotAndProgressionFactories()
    {
        await using var fixture = new Fixture(_output);
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
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ITestOutputHelper _output;
        private FileSystemManager.CanonicalWriteLease? _lease;
        private GameEngineTestRuntime? _runtime;
        private ProgressionControl? _progression;
        private bool _retained;
        private readonly System.Collections.Concurrent.ConcurrentQueue<(string Name, string Boundary, double Milliseconds)> _observations = new();

        /// <summary>
        /// Allocates only an ordinary nonce-owned container and its empty game namespace.
        /// </summary>
        /// <param name="output">
        /// The receiver for all boundaries and exact closing or retention evidence.
        /// </param>
        internal Fixture(ITestOutputHelper output)
        {
            var started = Stopwatch.GetTimestamp();
            _output = output;
            _parent = Path.GetFullPath(Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-bundle-component"));
            Directory.CreateDirectory(_parent);
            _owned = Path.Combine(_parent, _nonce);
            if (Directory.Exists(_owned) || File.Exists(_owned)) throw new InvalidDataException("Component destination exists.");
            Directory.CreateDirectory(_owned);
            File.WriteAllText(Path.Combine(_owned, ".fixture-owner"), _nonce);
            var game = Path.Combine(_owned, "game");
            Directory.CreateDirectory(game);
            Files = new FileSystemManager(game, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                new FileSystemManagerHooks { OperationTimingObserver = Observe });
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Name = "JointBundleFixture.AllocateNamespace", Milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                Owned = _owned, Game = game
            }));
        }

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
        /// Emits every timing boundary immediately without trimming its name, count or elapsed duration.
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
            _observations.Enqueue((name, boundary, elapsed.TotalMilliseconds));
            _output.WriteLine(JsonSerializer.Serialize(new
            {
                Name = name, Boundary = boundary, Milliseconds = elapsed.TotalMilliseconds,
                Thread = Environment.CurrentManagedThreadId
            }));
        }

        /// <summary>
        /// Aborts an unpublished joint reservation, then closes the actual services and lease before
        /// deleting only this marked ordinary container.
        /// Failure or publication uncertainty retains the container and reports its exact location.
        /// </summary>
        /// <returns>
        /// A task completing after resource closing and the owned-root closing or retention receipt.
        /// </returns>
        public async ValueTask DisposeAsync()
        {
            var failures = new List<Exception>();
            try { CloseJointReservation(); }
            catch (Exception failure) { failures.Add(failure); _retained = true; }
            try { if (_runtime is not null) await _runtime.AudioService.DisposeAsync(); }
            catch (Exception failure) { failures.Add(failure); _retained = true; }
            try { if (_lease is not null) await _lease.DisposeAsync(); }
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
            _output.WriteLine(JsonSerializer.Serialize(new { Owned = _owned, Retained = _retained, Closing = !Directory.Exists(_owned) }));
            if (failures.Count != 0) throw new AggregateException("Component closing failed; owned container retained.", failures);
            if (!_retained) Assert.False(Directory.Exists(_owned));
        }
    }
}
