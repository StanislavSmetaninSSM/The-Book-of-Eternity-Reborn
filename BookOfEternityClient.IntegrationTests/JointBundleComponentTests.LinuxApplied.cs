using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>
    /// Prepares current Linux applied DATA through genuine publishers and exports it after all live owners close.
    /// </summary>
    /// <returns>
    /// Completion after canonical/progression publication, fresh applied validation, capture and normal fixture cleanup.
    /// </returns>
    [Fact]
    public async Task LinuxAppliedData_PublishesCurrentMinimalTemplate()
    {
        Assert.True(OperatingSystem.IsLinux(), "This preparation requires native Linux execution.");
        var recordPath = Environment.GetEnvironmentVariable("BOE_TEST_LINUX_APPLIED_RECORD")
            ?? throw new InvalidDataException("Set an absolute external Linux applied record path.");
        Assert.False(string.IsNullOrWhiteSpace(recordPath));
        Assert.True(Path.IsPathFullyQualified(recordPath));
        JointAppliedDataTemplate.RequireExternalPath(recordPath);
        JointAppliedDataTemplate.RequireExternalPath(TestDiagnosticPaths.Root);
        var outputScope = new TrustedLocalFileScope([TestDiagnosticPaths.Root]);
        outputScope.ValidateFile(recordPath, allowMissing: true);
        Assert.False(File.Exists(recordPath) || Directory.Exists(recordPath));
        outputScope.EnsureDirectory(Path.GetDirectoryName(recordPath)!);
        var archive = Path.Combine(Path.GetDirectoryName(recordPath)!, "applied-" + Guid.NewGuid().ToString("N") + ".zip");
        await using (var fixture = new Fixture(_output, nameof(LinuxAppliedData_PublishesCurrentMinimalTemplate)))
        {
            await fixture.InitializeAsync();
            await fixture.PublishLinuxAppliedDataAsync(archive);
        }
        // A failed publisher, capture or fixture close never publishes a selectable success record.
        var record = new JsonObject
        {
            ["SchemaVersion"] = 1, ["Platform"] = "Linux", ["Generator"] = JointAppliedDataTemplate.LinuxGenerator,
            ["GameAssemblySHA256"] = JointAppliedDataTemplate.CurrentGameAssemblySha256(),
            ["Archive"] = archive, ["ArchiveSHA256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)))
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(record);
        using (var stream = new FileStream(recordPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            stream.Write(bytes);
            stream.Flush(flushToDisk: true);
        }
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "LinuxAppliedDataPrepared", Record = recordPath,
            Archive = archive, ArchiveSHA256 = record["ArchiveSHA256"]!.GetValue<string>(), GrantsAuthority = false,
            SourceLeaseClosed = true, FixtureCleanupCompleted = true }));
    }

    private sealed partial class Fixture
    {
        /// <summary>
        /// Prevents ordinary abort or root deletion when an entered publisher has no definite outcome.
        /// </summary>
        private bool _jointPublicationOutcomeUncertain;

        /// <summary>
        /// Runs the normal publishers on independently issued minimal data, then captures only inert DATA.
        /// </summary>
        /// <param name="archivePath">
        /// Nonexistent external archive selected before the genuine preparation begins.
        /// </param>
        /// <returns>
        /// Completion after real applied validation and source, reservation, service and lease closing.
        /// </returns>
        internal async Task PublishLinuxAppliedDataAsync(string archivePath)
        {
            var bound = await PrepareBoundJointAsync(async () =>
            {
                using var source = Files.BeginSourceDataOperationIfNeeded();
                Assert.NotNull(source);
                using var recovery = Files.BeginRecoveryValidation(Lease);
                var mutationInFlight = false;
                try
                {
                    using var total = Files.MeasureOperation("LinuxApplied.PrepareAndPublish");
                    var common = await WoundAcceptedTurnPlanner.CreateJointRepairCommonPlanFromBoundForComponentAsync(Files, Lease,
                        _jointBound ?? throw new InvalidOperationException("A genuine current bound input is required."));
                    Assert.True(common.Success, JsonSerializer.Serialize(common.Issues));
                    var plan = Assert.IsType<AcceptedMechanicsPlan>(common.Plan);
                    var normalizer = new CanonicalStateNormalizer(Files, NullLogger<CanonicalStateNormalizer>.Instance).BindTo(Lease);
                    var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                        (await Files.ReadFileAsync(Lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
                        LiveTurnPreparationService.ManifestJsonOptions);
                    Assert.NotNull(manifest);
                    var preflight = await normalizer.PrevalidateJointRepairPublicationTransactionAsync();
                    Assert.NotNull(preflight);
                    Assert.Equal(plan.InputFingerprint, preflight.Validated.Plan.InputFingerprint);
                    Assert.Equal(plan.PreparedPlanFingerprint, preflight.Validated.Plan.PreparedPlanFingerprint);
                    mutationInFlight = true;
                    var published = await normalizer.NormalizeAndValidateJointRepairAsync(preflight, Runtime.ValidationService, manifest.RollbackBackups);
                    Assert.NotEqual(MortalJointRepairPublicationDisposition.PublicationUncertain, published.Disposition);
                    mutationInFlight = false;
                    Assert.Equal(MortalJointRepairPublicationDisposition.Committed, published.Disposition);
                    Assert.Empty(published.Issues);
                    var original = _jointOriginal ?? throw new InvalidOperationException("The issued original is required.");
                    var phase = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(Files, Lease,
                        original.SessionId, original.RequestId, original.SnapshotToken, original.Turn);
                    Assert.Equal("unapplied", phase.Phase);
                    mutationInFlight = true;
                    var applied = await MortalQteDecisionPublication.CommitJointProgressionPhaseAsync(Files, Lease, phase);
                    Assert.NotEqual(MortalQteDecisionPublication.JointProgressionPublicationDisposition.PublicationUncertain, applied.Disposition);
                    mutationInFlight = false;
                    Assert.Equal(MortalQteDecisionPublication.JointProgressionPublicationDisposition.Applied, applied.Disposition);
                    Assert.NotNull(applied.AppliedCapability);
                    Assert.NotNull(applied.CompletedWitness);
                    Assert.Empty(applied.Issues);
                    var fresh = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(Files, Lease,
                        original.SessionId, original.RequestId, original.SnapshotToken, original.Turn);
                    Assert.Equal("applied", fresh.Phase);
                    await source.CompleteAsync();
                }
                catch (Exception failure) when (mutationInFlight)
                {
                    _jointPublicationOutcomeUncertain = true;
                    _retained = true;
                    throw failure as CoordinatedStatePublicationUncertainException ?? new CoordinatedStatePublicationUncertainException(failure);
                }
            }, reuseStableData: true);
            Assert.True(bound.Success, JsonSerializer.Serialize(bound.Issues));
            var issued = _jointOriginal ?? throw new InvalidOperationException("The actual original coordinates are required.");
            var provenance = new JsonObject
            {
                ["Role"] = "AppliedBeforeStory", ["Platform"] = "Linux", ["Generator"] = JointAppliedDataTemplate.LinuxGenerator,
                ["Operation"] = "CanonicalCommittedAndProgressionApplied", ["RecoveryQualified"] = false,
                ["Original"] = new JsonObject { ["SessionId"] = issued.SessionId, ["RequestId"] = issued.RequestId,
                    ["SnapshotToken"] = issued.SnapshotToken, ["TurnNumber"] = issued.Turn }
            };
            CloseJointReservation();
            await Runtime.AudioService.DisposeAsync();
            _runtime = null;
            await Lease.DisposeAsync();
            _lease = null;
            JointCleanupDataCapture.Capture(Files.BasePath, archivePath, provenance);
        }
    }
}
