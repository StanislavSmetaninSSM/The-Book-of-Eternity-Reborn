using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    // Initial world preparation for the rollback journey only. Snapshot creation
    // normally commits these defaults before taking its effective rollback image.
    internal static async Task SeedDiagnosisRollbackBeforeBootstrapAsync(FileSystemManager files, JsonObject profiles)
    {
        await AfterlifeSpiritualNaturalRecoveryTests.SeedDiagnosisBeforeBootstrapAsync(files, profiles);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var profileBytes = await files.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        Assert.NotNull(profileBytes);
        foreach (var pair in DiagnosisSnapshotDefaults())
            if (await files.ReadFileBytesAsync(lease, pair.Key) is null)
                await files.WriteFileAtomicAsync(lease, pair.Key, pair.Value.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        Assert.Equal(profileBytes, await files.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
    }

    [Fact]
    public async Task DiagnosisSnapshotPrefixCreatesOnlyMissingDefaultsAndPreservesPreparedRollbackImages()
    {
        // Independent worlds: initializing the first one cannot accidentally
        // satisfy the prepared-world no-op oracle in the second one.
        foreach (var prepared in new[] { false, true })
        {
            await using var fixture = await HealingFixture.CreateAsync(2, 0, 20, stageRequest: false,
                beforeResourceBootstrap: prepared ? SeedDiagnosisRollbackBeforeBootstrapAsync :
                    AfterlifeSpiritualNaturalRecoveryTests.SeedDiagnosisBeforeBootstrapAsync);
            var files = fixture.Files;
            var defaults = DiagnosisSnapshotDefaults();
            var paths = defaults.Keys.Append(AfterlifeEntityProfileState.StatePath).ToArray();
            var before = fixture.CaptureMutationRoots(paths);
            var profileBytes = Assert.IsType<byte[]>(before[AfterlifeEntityProfileState.StatePath]);
            foreach (var pair in defaults)
            {
                if (!prepared) Assert.Null(before[pair.Key]);
                else Assert.True(JsonNode.DeepEquals(pair.Value,
                    JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(Assert.IsType<byte[]>(before[pair.Key])))));
            }
            Assert.NotEmpty(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(profileBytes))!["profiles"]!.AsArray());
            var control = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                .BuildControlForNextTurnAsync();
            var request = new TurnRequest
            {
                SessionId = "session_diagnosis_initialization", RequestId = "request_diagnosis_initialization",
                TurnNumber = 43, PlayerAction = WoundApplicationService.DiagnosisActionText,
                Timestamp = DateTime.UtcNow.ToString("O"), ProgressionControl = control
            };
            var runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(files, configureSettings: settings =>
            { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; });
            try
            {
                runtime.GameLoop.SetSession(request.SessionId, 42);
                var backup = await runtime.Engine.CaptureOriginalRollbackAsync(request.RequestId);
                await files.WriteFileAtomicAsync(LiveTurnPreparationService.TurnRequestPath,
                    JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await runtime.Engine.CaptureOriginalValidationSnapshotAsync(request, backup, "diagnosis-initialization");
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                var manifest = await fixture.ReadManifestAsync(lease);
                Assert.True(PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(
                    (await files.ReadFileAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath))!, out var authority));
                Assert.NotNull(authority);
                Assert.Equal(PendingTurnSnapshotAuthority.ExactSnapshotHashMode, authority.SnapshotHashMode);
                Assert.Equal(manifest.ManifestPayloadHash, authority.ManifestPayloadHash);
                foreach (var path in paths)
                {
                    var actual = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, path));
                    if (path == AfterlifeEntityProfileState.StatePath) Assert.Equal(profileBytes, actual);
                    else Assert.True(JsonNode.DeepEquals(defaults[path], JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(actual))));
                    if (prepared) Assert.Equal(before[path], actual);
                    Assert.Contains(path, manifest.RollbackBaselineFiles);
                    Assert.Equal(actual, await files.ReadFileBytesAsync(lease, manifest.Files[path]));
                    Assert.Equal(manifest.Files[path], authority.Files[path]);
                    Assert.Equal(actual, await files.ReadFileBytesAsync(lease, manifest.RollbackBackups[path]));
                    Assert.Equal(manifest.RollbackBackups[path], backup.BackupFiles[path]);
                    Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(actual), backup.BackupHashes[path], ignoreCase: true);
                    Assert.Equal(backup.BackupHashes[path], authority.RollbackBackupHashes[path], ignoreCase: true);
                    Assert.Equal(PendingTurnSnapshotAuthority.ComputeSha256(actual), authority.SnapshotFileHashes[path], ignoreCase: true);
                    if (!prepared && defaults.ContainsKey(path))
                        Assert.EndsWith(".initialized", manifest.RollbackBackups[path]);
                }
            }
            finally { await runtime.AudioService.DisposeAsync(); }
        }
    }

    private static Dictionary<string, JsonObject> DiagnosisSnapshotDefaults() => new(StringComparer.Ordinal)
    {
        [AfterlifeSpiritualConflictState.StatePath] = AfterlifeSpiritualConflictState.CreateDefaultRoot(),
        [AfterlifeChronicleState.StatePath] = AfterlifeChronicleState.CreateDefaultRoot(),
        [AfterlifeGlobalFlagState.StatePath] = AfterlifeGlobalFlagState.CreateDefaultRoot()
    };
}
