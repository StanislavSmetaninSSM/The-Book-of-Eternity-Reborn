using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks actual constructor wiring and read-only existing registry observations without preparing a game world.
/// </summary>
public sealed class JointInterfaceTypedRuntimeTests
{
    /// <summary>
    /// Consumes the supplied QTE key through the constructed service while leaving engine input untouched.
    /// </summary>
    /// <returns>
    /// Completion after the real audio service is disposed; no source turn or QTE scene is created.
    /// </returns>
    [Fact]
    public async Task FactoryRoutesExplicitQteInputToActualServiceWithoutReplacingEngineInput()
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var engineInput = new KeyInput(ConsoleKey.A);
        var qteInput = new KeyInput(ConsoleKey.Escape);
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files,
            inputSource: engineInput, qteInputSource: qteInput);
        try
        {
            // Mutation caught: ignoring the QTE constructor argument leaves the actual service on system input.
            // This assertion precedes input access, making the legacy RED safe in a redirected test host.
            Assert.True(runtime.QteSceneService.UsesInputSource(qteInput), "The actual QTE service did not retain its explicit input.");
            Assert.True(runtime.QteSceneService.TryReadImmediateKey(out var key));
            Assert.Equal(ConsoleKey.Escape, key.Key);
            qteInput.AssertCompleted();
            Assert.True(engineInput.KeyAvailable);
            Assert.Equal(ConsoleKey.A, engineInput.ReadKey().Key);
            engineInput.AssertCompleted();
            Assert.False(runtime.QteSceneService.TryReadImmediateKey(out _));
        }
        finally
        {
            await runtime.AudioService.DisposeAsync();
        }
    }

    /// <summary>
    /// Retains the original system QTE input when the new factory argument is absent.
    /// </summary>
    /// <returns>
    /// Completion after checking the retained constructor dependency without reading the real console.
    /// </returns>
    [Fact]
    public async Task FactoryDefaultQteInputPreservesLegacySystemSource()
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(
            new FileSystemManager(root, NullLogger<FileSystemManager>.Instance), inputSource: new KeyInput(ConsoleKey.A));
        try
        {
            Assert.True(runtime.QteSceneService.UsesInputSource(SystemConsoleInputSource.Instance));
            Assert.False(runtime.QteSceneService.UsesInputSource(null));
        }
        finally
        {
            await runtime.AudioService.DisposeAsync();
        }
    }

    /// <summary>
    /// Distinguishes a real existing registry state from absence without creating or consuming authority in the observation.
    /// </summary>
    /// <returns>
    /// Completion after repeated observations preserve the exact physical generation bytes.
    /// </returns>
    [Fact]
    public async Task ExistingRegistryObservationDistinguishesAdmissionFromAbsenceWithoutMutation()
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var identity = files.CanonicalRootAuthorityIdentity;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(files.ReadExistingSessionGeneration(lease));
        var absent = AcceptedTurnAuthorityRegistry.ObserveExistingJointPublication(identity);
        Assert.False(absent.RootSlotPresent);
        Assert.False(absent.AuthorityStatePresent);
        Assert.False(absent.TreatmentReservationPresent);
        Assert.False(absent.OpenJointRepairReceiptPresent);
        Assert.Null(files.ReadExistingSessionGeneration(lease));

        // This original production admission creates the real state under its existing generation rules.
        Assert.False(AcceptedTurnAuthorityRegistry.HasCommonValidated(files, lease));
        Assert.NotNull(files.ReadExistingSessionGeneration(lease));
        var before = File.ReadAllBytes(files.SessionGenerationPath);
        var observed = AcceptedTurnAuthorityRegistry.ObserveExistingJointPublication(identity);
        Assert.True(observed.RootSlotPresent, "An actually admitted registry slot cannot be reported as absent.");
        Assert.True(observed.AuthorityStatePresent);
        Assert.False(observed.TreatmentReservationPresent);
        Assert.False(observed.OpenJointRepairReceiptPresent);
        Assert.Equal(observed, AcceptedTurnAuthorityRegistry.ObserveExistingJointPublication(identity));
        Assert.Equal(before, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.False(AcceptedTurnAuthorityRegistry.HasCommonValidated(files, lease));
    }

    /// <summary>
    /// Chooses a fresh GUID-owned root in this repository's dedicated typed-runtime fixture directory.
    /// </summary>
    /// <returns>
    /// An absolute root spelling; only the selected fixture creates it.
    /// </returns>
    private static string NewRoot() => Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-typed-runtime-owned",
        Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Supplies one actual queued key and refuses unrelated line reads.
    /// </summary>
    private sealed class KeyInput : IConsoleInputSource
    {
        private readonly Queue<ConsoleKeyInfo> _keys = new();

        /// <summary>
        /// Queues one key for the constructor-wiring control.
        /// </summary>
        /// <param name="key">
        /// The exact physical key consumed by the selected real service.
        /// </param>
        internal KeyInput(ConsoleKey key) => _keys.Enqueue(new ConsoleKeyInfo('\0', key, false, false, false));

        /// <summary>
        /// Identifies this finite queue as scripted input.
        /// </summary>
        public bool IsScripted => true;

        /// <summary>
        /// Reports whether the sole queued key remains available without waiting.
        /// </summary>
        public bool KeyAvailable => _keys.Count != 0;

        /// <summary>
        /// Consumes the next queued key; exhaustion remains an error.
        /// </summary>
        /// <param name="intercept">
        /// The standard echo flag, defaulting to <see langword="true"/>; this in-memory input never echoes.
        /// </param>
        /// <returns>
        /// The exact key supplied to this queue's constructor.
        /// </returns>
        public ConsoleKeyInfo ReadKey(bool intercept = true) => _keys.Dequeue();

        /// <summary>
        /// Refuses line input because this constructor control supplies only a finite key queue.
        /// </summary>
        /// <returns>
        /// No value; line reads always throw <see cref="InvalidOperationException"/>.
        /// </returns>
        public string? ReadLine() => throw new InvalidOperationException("This constructor control supplies keys only.");

        /// <summary>
        /// Asserts that the selected real service consumed this queue's complete input.
        /// </summary>
        public void AssertCompleted() => Assert.Empty(_keys);
    }
}
