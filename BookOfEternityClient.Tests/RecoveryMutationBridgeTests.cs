using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises actual directory and replacement effects while observing their root mutation envelopes.
/// </summary>
/// <param name="output">
/// Receiver for complete per-case activity observations and source-bound fixture outcomes.
/// </param>
public sealed class RecoveryMutationBridgeTests(ITestOutputHelper output)
{
    /// <summary>
    /// Observes the public directory bridge during actual directory creation and balances its exceptional exit.
    /// </summary>
    /// <param name="failAfterCreation">
    /// Whether the existing logger throws after observing the first newly created required directory.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicDirectoryBridge_TracksActualCreationAndRetiresAfterFailure(bool failAfterCreation)
    {
        using var fixture = new BridgeFixture();
        var before = fixture.CaptureQuiescent();
        var injected = new BridgeFailure();
        fixture.Logger.DirectoryCreated = relative =>
        {
            fixture.Observe("DirectoryCreated", Directory.Exists(Path.Combine(fixture.Root,
                relative.Replace('/', Path.DirectorySeparatorChar))));
            if (failAfterCreation) throw injected;
        };

        var failure = Record.Exception(fixture.Files.EnsureDirectoryStructure);

        if (failAfterCreation) Assert.Same(injected, failure);
        else Assert.Null(failure);
        Assert.NotEmpty(fixture.Observations);
        fixture.AssertObservations(expectedDepth: 1);
        Assert.All(fixture.Observations, observation => Assert.True(observation.EffectObserved));
        fixture.AssertRetired(before);
        var finalActivity = fixture.Epoch.CaptureActivity();
        output.WriteLine(JsonSerializer.Serialize(new { Kind = "RecoveryPublicDirectoryBridge", FailAfterCreation = failAfterCreation,
            Observations = fixture.Observations.Select(row => new { row.Stage, row.Active, row.CanCapture, row.EffectObserved }).ToArray(),
            Activity = new { finalActivity.Begins, finalActivity.Ends, finalActivity.Active } }));
    }

    /// <summary>
    /// Distinguishes the session replacement's outer preparation registration from its nested publication registration.
    /// </summary>
    /// <param name="failAfterMember">
    /// Whether the original publication observer interrupts the first actual member publication before commit.
    /// </param>
    /// <returns>
    /// Completion after exact commit or rollback and balanced outer and nested registrations.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SessionReplacementBridge_CoversPreparationAndNestedPublication(bool failAfterMember)
    {
        using var fixture = new BridgeFixture();
        fixture.SeedGeneration();
        const string member = "game_state/core/bridge-selected.bin";
        byte[] original = [0xFF, 0, 17];
        fixture.Put(member, original);
        var generation = File.ReadAllBytes(fixture.Files.SessionGenerationPath);
        var injected = new BridgeFailure();
        fixture.BeforeRead = path =>
        {
            if (path == member) fixture.Observe("PreCoreSelectedRead", File.Exists(fixture.PathFor(member)));
            return Task.CompletedTask;
        };
        fixture.OnPublication = (phase, _) =>
        {
            if (phase is not (TrustedLocalPublicationPhase.MemberPublished or TrustedLocalPublicationPhase.CleanupComplete)) return;
            fixture.Observe(phase.ToString(), Directory.Exists(fixture.PathFor("input")));
            if (failAfterMember && phase == TrustedLocalPublicationPhase.MemberPublished) throw injected;
        };
        await using var lifecycle = await fixture.Files.AcquireSessionLifecycleLeaseAsync();
        var before = fixture.CaptureQuiescent();

        var failure = await Record.ExceptionAsync(async () =>
        {
            await fixture.Files.ClearGameStateAsync(lifecycle);
        });

        if (failAfterMember)
        {
            Assert.Same(injected, failure);
            Assert.Equal(original, File.ReadAllBytes(fixture.PathFor(member)));
            Assert.Equal(generation, File.ReadAllBytes(fixture.Files.SessionGenerationPath));
        }
        else
        {
            Assert.Null(failure);
            Assert.False(File.Exists(fixture.PathFor(member)));
            Assert.False(generation.SequenceEqual(File.ReadAllBytes(fixture.Files.SessionGenerationPath)));
        }
        var preparation = Assert.Single(fixture.Observations, row => row.Stage == "PreCoreSelectedRead");
        Assert.Equal(1, preparation.Active);
        Assert.False(preparation.CanCapture);
        Assert.True(preparation.EffectObserved);
        var publication = fixture.Observations.Where(row => row.Stage != "PreCoreSelectedRead").ToArray();
        Assert.Contains(publication, row => row.Stage == "MemberPublished");
        Assert.All(publication, row =>
        {
            Assert.Equal(2, row.Active);
            Assert.False(row.CanCapture);
            Assert.True(row.EffectObserved);
        });
        if (!failAfterMember) Assert.Contains(publication, row => row.Stage == "CleanupComplete");
        fixture.AssertRetired(before);
        fixture.AssertNoPublicationDebt();
        var finalActivity = fixture.Epoch.CaptureActivity();
        output.WriteLine(JsonSerializer.Serialize(new { Kind = "RecoverySessionReplacementBridge", FailAfterMember = failAfterMember,
            Observations = fixture.Observations.Select(row => new { row.Stage, row.Active, row.CanCapture, row.EffectObserved }).ToArray(),
            Activity = new { finalActivity.Begins, finalActivity.Ends, finalActivity.Active } }));
    }

    /// <summary>
    /// Calls each exact load bridge under its real replacement lease and observes preparation, physical effects and cleanup.
    /// </summary>
    /// <param name="namespacePublication">
    /// Whether to build and publish a real complete namespace plan instead of the image bridge's exact file cohort.
    /// </param>
    /// <param name="failAfterDirectory">
    /// Whether to interrupt after actual directory creation, before an established commit.
    /// </param>
    /// <returns>
    /// Completion after committed bytes or restored before-images, renewed root version and balanced activity.
    /// </returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LoadReplacementBridges_CoverRealEffectsRevalidationAndCleanup(bool namespacePublication, bool failAfterDirectory)
    {
        using var fixture = new BridgeFixture();
        fixture.Files.EnsureDirectoryStructure();
        fixture.SeedGeneration();
        const string parent = "lore/bridge-created";
        const string member = parent + "/payload.bin";
        byte[] desired = [0xEF, 0xBB, 0xBF, 0, 0xFF, 23];
        var injected = new BridgeFailure();
        await using var lifecycle = await fixture.Files.AcquireSessionLifecycleLeaseAsync();
        await using var lease = await fixture.Files.AcquireSessionReplacementWriteLeaseAsync(lifecycle);
        var generation = fixture.Files.ReadLocalGenerationSnapshot(lease);
        var replacement = Guid.NewGuid().ToString("N");
        var revalidations = 0;
        var cutReached = false;
        fixture.OnPublication = (phase, _) =>
        {
            if (phase is not (TrustedLocalPublicationPhase.DirectoryCreated or TrustedLocalPublicationPhase.MemberPublished or
                TrustedLocalPublicationPhase.CleanupComplete)) return;
            fixture.Observe(phase.ToString(), Directory.Exists(fixture.PathFor(parent)));
            if (namespacePublication && failAfterDirectory && !cutReached &&
                phase == TrustedLocalPublicationPhase.DirectoryCreated && Directory.Exists(fixture.PathFor(parent)))
            {
                cutReached = true;
                throw injected;
            }
        };
        void Revalidate()
        {
            revalidations++;
            var created = Directory.Exists(fixture.PathFor(parent));
            fixture.Observe(created ? "RevalidateAfterDirectory" : "RevalidateBeforeDirectory", created);
            if (!namespacePublication && failAfterDirectory && created)
            {
                cutReached = true;
                throw injected;
            }
        }
        CanonicalLoadNamespacePlan? plan = null;
        if (namespacePublication)
        {
            // Normal load preparation has already initialized publication storage.
            // This direct bridge fixture supplies that prerequisite before measuring its envelope.
            new TrustedLocalFileScope([fixture.Root]).EnsureDirectory(
                Path.Combine(fixture.Files.RuntimeRootPath, "trusted-local-publication-v1"));
            var source = Path.Combine(fixture.Root, "selected-source.bin");
            File.WriteAllBytes(source, [41]);
            var snapshot = fixture.Files.CaptureLoadReplacementNamespace(lease, source);
            plan = fixture.Files.CreateLoadReplacementNamespacePlan(snapshot,
                new Dictionary<string, TrustedLocalFileImage>(StringComparer.Ordinal)
                { [member] = TrustedLocalFileImage.FromBytes(desired) }, preserveConfiguration: true);
        }
        Assert.False(Directory.Exists(fixture.PathFor(parent)));
        var before = fixture.CaptureQuiescent();
        TrustedLocalPublicationOutcome? outcome = null;

        var failure = await Record.ExceptionAsync(async () =>
        {
            outcome = namespacePublication
                ? await fixture.Files.PublishLoadReplacementNamespaceAsync(lease, generation, plan!, replacement, Revalidate)
                : await fixture.Files.PublishLoadReplacementImagesAsync(lease, generation,
                    [new CanonicalLocalImageChange(member, TrustedLocalFileImage.FromBytes(null), TrustedLocalFileImage.FromBytes(desired))],
                    replacement, Revalidate);
        });

        Assert.True(revalidations >= 2, JsonSerializer.Serialize(new
        {
            Revalidations = revalidations, Failure = failure?.ToString(), Outcome = outcome?.Disposition.ToString(),
            OutcomeFailure = outcome?.Failure?.ToString(), Observations = fixture.Observations.Select(row => new
            { row.Stage, row.Active, row.CanCapture, row.EffectObserved }).ToArray()
        }));
        fixture.AssertObservations(expectedDepth: 1);
        Assert.Contains(fixture.Observations, row => row.Stage == "RevalidateBeforeDirectory");
        if (failAfterDirectory)
        {
            Assert.True(cutReached);
            if (namespacePublication)
            {
                Assert.Null(failure);
                Assert.NotNull(outcome);
                Assert.Equal(TrustedLocalPublicationDisposition.RolledBack, outcome.Disposition);
                Assert.Same(injected, outcome.Failure);
                Assert.Contains(fixture.Observations, row => row.Stage == "DirectoryCreated" && row.EffectObserved);
                Assert.False(Directory.Exists(fixture.PathFor(parent)));
            }
            else
            {
                Assert.Same(injected, failure);
                Assert.Null(outcome);
                Assert.Contains(fixture.Observations, row => row.Stage == "RevalidateAfterDirectory" && row.EffectObserved);
                Assert.True(Directory.Exists(fixture.PathFor(parent)));
            }
            Assert.False(File.Exists(fixture.PathFor(member)));
            Assert.Equal(generation.Bytes, File.ReadAllBytes(fixture.Files.SessionGenerationPath));
        }
        else
        {
            Assert.Null(failure);
            Assert.NotNull(outcome);
            Assert.Equal(TrustedLocalPublicationDisposition.Committed, outcome.Disposition);
            Assert.Equal(desired, File.ReadAllBytes(fixture.PathFor(member)));
            Assert.Equal(replacement, fixture.Files.ReadLocalGenerationSnapshot(lease).Binding.Id);
            Assert.Contains(fixture.Observations, row => row.Stage == "MemberPublished" && row.EffectObserved);
            Assert.Contains(fixture.Observations, row => row.Stage == "CleanupComplete" && row.EffectObserved);
            if (namespacePublication)
                Assert.Contains(fixture.Observations, row => row.Stage == "DirectoryCreated" && row.EffectObserved);
            else
                Assert.Contains(fixture.Observations, row => row.Stage == "RevalidateAfterDirectory" && row.EffectObserved);
        }
        fixture.AssertRetired(before);
        fixture.AssertNoPublicationDebt();
        var finalActivity = fixture.Epoch.CaptureActivity();
        output.WriteLine(JsonSerializer.Serialize(new { Kind = "RecoveryLoadMutationBridge", Namespace = namespacePublication,
            FailAfterDirectory = failAfterDirectory, Revalidations = revalidations,
            Observations = fixture.Observations.Select(row => new { row.Stage, row.Active, row.CanCapture, row.EffectObserved }).ToArray(),
            Activity = new { finalActivity.Begins, finalActivity.Ends, finalActivity.Active } }));
    }

    /// <summary>
    /// Marks the exact controlled post-effect interruption without classifying it as a transient I/O retry.
    /// </summary>
    private sealed class BridgeFailure : Exception { }

    /// <summary>
    /// Owns an independent tiny file root, real manager and existing observation hooks.
    /// </summary>
    private sealed class BridgeFixture : IDisposable
    {
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly string _parent = Path.GetFullPath(Path.GetTempPath());

        /// <summary>
        /// Creates an empty fixture without canonical directory setup or invented lease authority.
        /// </summary>
        internal BridgeFixture()
        {
            Root = Path.Combine(_parent, "boe-recovery-bridges-" + _nonce);
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, ".fixture-owner"), _nonce);
            Files = new FileSystemManager(Root, Logger, PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path => BeforeRead?.Invoke(path) ?? Task.CompletedTask,
                LocalPublicationObserver = (phase, index) => OnPublication?.Invoke(phase, index)
            });
        }

        /// <summary>
        /// Gets the exact nonce-owned fixture root.
        /// </summary>
        internal string Root { get; }

        /// <summary>
        /// Gets the actual manager used by every tested bridge.
        /// </summary>
        internal FileSystemManager Files { get; }

        /// <summary>
        /// Gets the root-owned mutation tracker without creating a substitute version source.
        /// </summary>
        internal CanonicalValidationEpoch Epoch => Files.CanonicalRootAuthorityIdentity.ValidationEpoch;

        /// <summary>
        /// Gets the existing logger observation used after required-directory creation.
        /// </summary>
        internal DirectoryCreationLogger Logger { get; } = new();

        /// <summary>
        /// Gets or sets the existing pre-read callback; null performs no probe.
        /// </summary>
        internal Func<string, Task>? BeforeRead { get; set; }

        /// <summary>
        /// Gets or sets the existing publication callback; null performs no probe.
        /// </summary>
        internal Action<TrustedLocalPublicationPhase, int>? OnPublication { get; set; }

        /// <summary>
        /// Gets every real reached observation without sampling or resetting prior observations.
        /// </summary>
        internal List<(string Stage, int Active, bool CanCapture, bool EffectObserved)> Observations { get; } = [];

        /// <summary>
        /// Resolves a fixture-owned canonical relative name without invoking a mutation or a fresh admission.
        /// </summary>
        /// <param name="relative">
        /// Exact constant relative name used by this fixture.
        /// </param>
        /// <returns>
        /// The absolute name inside the fixture's game session.
        /// </returns>
        internal string PathFor(string relative) => Path.Combine(Files.GameSessionPath,
            relative.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>
        /// Seeds exact bytes before the measured bridge phase without granting source or lease authority.
        /// </summary>
        /// <param name="relative">
        /// Constant canonical fixture member to create.
        /// </param>
        /// <param name="bytes">
        /// Exact independent bytes to persist, including an empty array for an empty file.
        /// </param>
        internal void Put(string relative, byte[] bytes)
        {
            var path = PathFor(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// Seeds a current-schema generation record before acquiring the real lifecycle and replacement leases.
        /// </summary>
        internal void SeedGeneration()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Files.SessionGenerationPath)!);
            File.WriteAllBytes(Files.SessionGenerationPath,
                JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
        }

        /// <summary>
        /// Records a reached body boundary and observes quiescence without changing root state.
        /// </summary>
        /// <param name="stage">
        /// Exact callback or physical publication phase that was reached.
        /// </param>
        /// <param name="effectObserved">
        /// Whether the scenario-specific real directory or file effect is currently present.
        /// </param>
        internal void Observe(string stage, bool effectObserved)
        {
            var activity = Epoch.CaptureActivity();
            var canCapture = Epoch.TryCapture(out var version);
            Assert.Null(version);
            Observations.Add((stage, activity.Active, canCapture, effectObserved));
        }

        /// <summary>
        /// Captures the idle version and activity after setup or existing lease acquisition has settled.
        /// </summary>
        /// <returns>
        /// The exact initial version and diagnostic totals used to check balanced bridge retirement.
        /// </returns>
        internal (object Version, long Begins, long Ends) CaptureQuiescent()
        {
            var activity = Epoch.CaptureActivity();
            Assert.Equal(0, activity.Active);
            Assert.True(Epoch.TryCapture(out var version));
            return (Assert.IsType<object>(version), activity.Begins, activity.Ends);
        }

        /// <summary>
        /// Requires every recorded body boundary to refuse root capture at the intended exact active depth.
        /// </summary>
        /// <param name="expectedDepth">
        /// Exact active mutation count expected while these bridges perform their work.
        /// </param>
        internal void AssertObservations(int expectedDepth)
        {
            Assert.NotEmpty(Observations);
            Assert.All(Observations, row =>
            {
                Assert.Equal(expectedDepth, row.Active);
                Assert.False(row.CanCapture);
            });
        }

        /// <summary>
        /// Requires complete registration retirement and a different eligible version after bridge work settles.
        /// </summary>
        /// <param name="before">
        /// Idle version and activity captured before invoking the measured bridge.
        /// </param>
        internal void AssertRetired((object Version, long Begins, long Ends) before)
        {
            var after = CaptureQuiescent();
            Assert.NotSame(before.Version, after.Version);
            Assert.True(after.Begins > before.Begins);
            Assert.Equal(after.Begins - before.Begins, after.Ends - before.Ends);
        }

        /// <summary>
        /// Checks that completed publication or compensation left no active journal or member scratch.
        /// </summary>
        internal void AssertNoPublicationDebt()
        {
            Assert.False(File.Exists(Path.Combine(Files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
            Assert.Empty(Directory.EnumerateFiles(Root, ".boe-local-*", SearchOption.AllDirectories));
        }

        /// <summary>
        /// Deletes only this fixture's exact nonce-owned root after its lease owners have settled.
        /// </summary>
        public void Dispose()
        {
            Logger.DirectoryCreated = null;
            BeforeRead = null;
            OnPublication = null;
            Assert.Equal(_nonce, File.ReadAllText(Path.Combine(Root, ".fixture-owner")));
            Assert.Equal("boe-recovery-bridges-" + _nonce, Path.GetFileName(Root));
            Assert.Equal(Path.TrimEndingDirectorySeparator(_parent), Path.GetDirectoryName(Root));
            new TrustedLocalFileScope([_parent]).DeleteOwnedTree(Root);
            Assert.False(Directory.Exists(Root));
        }
    }

    /// <summary>
    /// Observes the existing required-directory log message after actual creation, without adding a production seam.
    /// </summary>
    private sealed class DirectoryCreationLogger : ILogger<FileSystemManager>
    {
        /// <summary>
        /// Gets or sets the receiver of the original logged relative directory; null disables observation.
        /// </summary>
        internal Action<string>? DirectoryCreated { get; set; }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (DirectoryCreated is null || logLevel != LogLevel.Debug ||
                !formatter(state, exception).StartsWith("Создана директория:", StringComparison.Ordinal) ||
                state is not IEnumerable<KeyValuePair<string, object?>> properties) return;
            var path = properties.Single(row => row.Key == "Path").Value as string
                ?? throw new InvalidDataException("The existing directory log omitted its original path.");
            DirectoryCreated(path);
        }
    }
}
