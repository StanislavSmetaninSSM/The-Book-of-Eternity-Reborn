using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Generates one genuine source checkpoint and qualifies its immutable transport into two fresh roots.
    /// </summary>
    [Fact]
    public async Task JointInterfaceSeedGenerator_ActualSource42ExportsAndLoadsTwoIndependentCopies()
    {
        Assert.True(OperatingSystem.IsWindows());
        var artifactParent = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-interface-seeds", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifactParent);
        var artifactPath = Path.Combine(artifactParent, "artifact");
        var copies = new[] { Path.Combine(artifactParent, "copy-one"), Path.Combine(artifactParent, "copy-two") };
        var clock = Stopwatch.StartNew();
        var timings = new Dictionary<string, double>();
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        AgentConsoleLiveInputSource? input = null;
        QteStoryRun? source = null;
        var audioClosed = false;
        var qualified = false;
        var copyCleanup = false;
        var sourceCleanup = false;
        string? failure = null;
        JointInterfaceCrashSeed? seed = null;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-GB");
            var start = clock.Elapsed.TotalSeconds;
            source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2,
                retainLiveInput: retained => input = retained,
                configureBeforePreparation: ConfigureJointCompletionWorldBeforeOriginalAsync);
            timings["source42Seconds"] = clock.Elapsed.TotalSeconds - start;
            Assert.Null(source.Failure);
            Assert.True(source.ActionObserved);
            Assert.NotNull(source.Source);
            Assert.Equal(42, source.Source.TurnNumber);
            await AssertGenuinePendingQteSourceAsync(source);
            var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);
            await AssertJointPriorLifecycleInactiveBaselineAsync(source);
            input?.Dispose();
            input = null;
            var audio = source.Runtime!.AudioService;
            await audio.DisposeAsync();
            audioClosed = true;
            Assert.NotEqual(AudioOutcome.CleanupUncertain, audio.Status.Outcome);

            start = clock.Elapsed.TotalSeconds;
            var snapshot = await CaptureClosedJointSeedAsync(_fs);
            var fingerprint = JointInterfaceSeedContractFingerprint.Compute(TestRepoPaths.RepoRoot);
            var metadata = new JointInterfaceCrashSeedMetadata(source.Source.SessionId, source.Source.RequestId,
                source.SnapshotToken!, 42, occurrence.OpportunityRef,
                Convert.ToHexString(SHA256.HashData(snapshot.Files[".boe_runtime/session-generation/current.json"])),
                typeof(GameEngineTurnLifecycleTests).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
                CultureInfo.CurrentCulture.Name);
            await JointInterfaceCrashSeed.ExportAsync(artifactPath, metadata, fingerprint, snapshot.Files, snapshot.Directories);
            seed = JointInterfaceCrashSeed.Open(artifactPath, fingerprint);
            timings["captureExportOpenSeconds"] = clock.Elapsed.TotalSeconds - start;

            for (var index = 0; index < copies.Length; index++)
            {
                start = clock.Elapsed.TotalSeconds;
                Assert.Equal(metadata, await seed.MaterializeAsync(copies[index]));
                timings[$"copy{index + 1}MaterializeSeconds"] = clock.Elapsed.TotalSeconds - start;
                AssertJointDurableNamespaceEqual(snapshot, await CaptureJointDurableNamespaceAsync(copies[index]));
                start = clock.Elapsed.TotalSeconds;
                using var adopted = new GameEngineTurnLifecycleTests(_directGachaOutput, copies[index]);
                await AssertImportedJointSeedWorldAsync(adopted._fs, metadata);
                AssertJointDurableNamespaceEqual(snapshot, await CaptureClosedJointSeedAsync(adopted._fs));
                timings[$"copy{index + 1}ReadOnlyValidationSeconds"] = clock.Elapsed.TotalSeconds - start;
                // Dispose removes this owned copy. The second independent copy is created from the
                // same private seed snapshot after the first copy and all its lease owners are gone.
            }
            Assert.All(copies, root => Assert.False(Directory.Exists(root)));
            AssertJointDurableNamespaceEqual(snapshot, await CaptureClosedJointSeedAsync(_fs));
            var reopened = JointInterfaceCrashSeed.Open(artifactPath, fingerprint);
            Assert.Equal(seed.FileCount, reopened.FileCount);
            Assert.Equal(seed.PayloadBytes, reopened.PayloadBytes);
            timings["totalBeforeCleanupSeconds"] = clock.Elapsed.TotalSeconds;
            qualified = true;
        }
        catch (Exception error)
        {
            failure = error.ToString();
            throw;
        }
        finally
        {
            input?.Dispose();
            if (!audioClosed && source?.Runtime is { } sourceRuntime)
                await sourceRuntime.AudioService.DisposeAsync();
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
            foreach (var root in copies)
                new CleanupOwnedFixture(root, line => _directGachaOutput?.WriteLine(line)).Dispose();
            new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line)).Dispose();
            copyCleanup = copies.All(root => !Directory.Exists(root) && !File.Exists(root));
            sourceCleanup = !Directory.Exists(_rootPath) && !File.Exists(_rootPath);
            var manifest = Path.Combine(artifactPath, "manifest.json");
            var result = new
            {
                SchemaVersion = 1, Qualified = qualified && copyCleanup && sourceCleanup, Failure = failure,
                ArtifactPath = artifactPath, ManifestSha256 = File.Exists(manifest) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifest))) : null,
                seed?.ContractFingerprint, seed?.FileCount, seed?.PayloadBytes, seed?.Metadata,
                Timings = timings, TotalSeconds = clock.Elapsed.TotalSeconds, CopyCleanup = copyCleanup, SourceCleanup = sourceCleanup,
                ProductionValidation = "fresh FileSystemManager and local lease per independent root; read-only inactive-tail/resource/occurrence predicates",
                Scope = "source42 preparation/loading only; no Stage43, crash or current-owner qualification"
            };
            await File.WriteAllTextAsync(Path.Combine(artifactParent, "result.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            _directGachaOutput?.WriteLine("JOINT_SEED_RESULT " + Path.Combine(artifactParent, "result.json"));
        }
        Assert.True(copyCleanup);
        Assert.True(sourceCleanup);
    }

    /// <summary>
    /// Captures stable durable bytes while proving the local operation has no outstanding publication or worker ownership.
    /// </summary>
    /// <param name="files">
    /// The source or newly adopted filesystem whose ordinary operation must already have returned.
    /// </param>
    /// <returns>
    /// The exact stable game session and generation namespace without coordination files.
    /// </returns>
    private static async Task<JointDurableNamespace> CaptureClosedJointSeedAsync(FileSystemManager files)
    {
        files.RequireLoadIpcOutsideFileScopes();
        var runtime = ReadQuiescentJointRuntimeNames(files);
        JointDurableNamespace snapshot;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            lease.EnsureNoPendingLocalDecision();
            Assert.Null(lease.MutationIntentRecorder);
            Assert.Null(lease.ExternalPublicationContext);
            Assert.Null(lease.BrowserLocalAccess);
            Assert.Null(lease.WorkerPurpose);
            Assert.False(lease.IsLegacyStorageRecovery);
            Assert.False(lease.MainAdmission?.WasRemote ?? false);
            Assert.False(lease.MainAdmission?.OwnsRemote ?? false);
            files.RequireAbsentMainWorkerInventory();
            snapshot = await CaptureJointDurableNamespaceAsync(files.BasePath);
            lease.EnsureNoPendingLocalDecision();
        }
        files.RequireLoadIpcOutsideFileScopes();
        AssertJointDurableNamespaceEqual(snapshot, await CaptureJointDurableNamespaceAsync(files.BasePath));
        var after = ReadQuiescentJointRuntimeNames(files);
        // Acquiring the first local lease may create inert lock names on a fresh copy.
        // Every observed runtime member must satisfy the same quiescent allowlist.
        Assert.Contains("session-generation/current.json", runtime.Files);
        Assert.Contains("session-generation/current.json", after.Files);
        return snapshot;
    }

    /// <summary>
    /// Validates copied game data through a fresh local lease without issuing acknowledgments or repairing state.
    /// </summary>
    /// <param name="files">
    /// The independently materialized filesystem, with no source engine or lease attached.
    /// </param>
    /// <param name="metadata">
    /// Detached source tuple and occurrence provenance against which copied records are checked.
    /// </param>
    /// <returns>
    /// The parsed physical occurrence after the production predicates and source invariants pass.
    /// </returns>
    private static async Task<MortalWoundOccurrence> AssertImportedJointSeedWorldAsync(FileSystemManager files, JointInterfaceCrashSeedMetadata metadata)
    {
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await GameEngine.ValidateCurrentMortalJointInactiveTailAsync(files, lease, 43);
        Assert.Null(await files.ReadFileBytesAsync(lease, "input/turn_request.json"));
        Assert.Null(await files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        Assert.Equal(metadata.GenerationSha256, Convert.ToHexString(SHA256.HashData(files.ReadLocalGenerationSnapshot(lease).Bytes)));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var history = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(state.IsValid, DescribeValidationIssues(state.Issues));
        Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
        Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
        var damage = Assert.Single(history.History.Transitions, row => row.EventRef == "turn_42:qte_terminal:1:resource:1");
        Assert.Equal(ResourceTransitionOutcome.Applied, damage.Outcome);
        Assert.Equal(5m, damage.AppliedAmount);
        Assert.Equal(10m, damage.BeforeState!.Current);
        Assert.Equal(5m, damage.AfterState!.Current);
        Assert.Equal(5m, Assert.Single(state.Ledger!.Entries, entry => entry.Coordinate.ResourceKey == "health").Current);
        var occurrences = MortalWoundOccurrenceState.Parse(await files.ReadFileAsync(lease, MortalWoundOccurrenceState.StatePath), MortalWoundOccurrenceState.StatePath);
        Assert.True(occurrences.IsValid, DescribeValidationIssues(occurrences.Issues));
        var occurrence = Assert.Single(occurrences.State!.Occurrences);
        Assert.Equal(metadata.SessionId, occurrence.SourceSessionId);
        Assert.Equal(metadata.RequestId, occurrence.SourceRequestId);
        Assert.Equal(metadata.SnapshotToken, occurrence.SourceSnapshotToken);
        Assert.Equal(42, occurrence.SourceTurn);
        Assert.Equal(metadata.OpportunityRef, occurrence.OpportunityRef);
        Assert.Equal("qte", occurrence.AdapterKind);
        Assert.Equal("physical", occurrence.Domain);
        Assert.Equal("mortal_qte_terminal_health_damage_v1", occurrence.ProfileKey);
        Assert.Equal(2, occurrence.Outcome.MaximumSeverityRank);
        Assert.Equal("turn_42:qte_terminal:1:resource:1", occurrence.AcceptedEvents[occurrence.AcceptedEventOrdinal].EventRef);
        Assert.Equal(0, occurrence.ProducerCandidateOrdinal);
        Assert.Equal(1, occurrence.ProducerCandidateCount);
        var prior = JsonNode.Parse((await files.ReadFileAsync(lease, QteDeferredEffectContinuation.StatePath))!)!;
        Assert.Equal("terminal", prior["state"]!.GetValue<string>());
        Assert.Equal(metadata.SessionId, prior["clientStoryAppend"]!["source"]!["sessionId"]!.GetValue<string>());
        Assert.Equal(metadata.RequestId, prior["clientStoryAppend"]!["source"]!["requestId"]!.GetValue<string>());
        Assert.Equal(42, prior["clientStoryAppend"]!["source"]!["turnNumber"]!.GetValue<int>());
        var terminal = Assert.Single(JsonNode.Parse((await files.ReadFileAsync(lease, QteSceneService.QteHistoryPath))!)!.AsArray())!;
        Assert.Equal(42, terminal["acceptedAtTurn"]!.GetValue<int>());
        Assert.Null(JsonNode.Parse((await files.ReadFileAsync(lease, QteSceneService.QteRuntimePath))!)["activeScene"]);
        lease.EnsureNoPendingLocalDecision();
        return occurrence;
    }
}
