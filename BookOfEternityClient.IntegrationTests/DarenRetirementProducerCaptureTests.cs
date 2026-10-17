using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// Temporary pre-removal capture owner. Archive this source and retire its selectors
// once the authentic packets are frozen; do not retain removed production APIs for it.
public sealed class DarenRetirementProducerCaptureTests(ITestOutputHelper output)
{
    [Fact]
    public async Task OriginalCurrentBrowserProducer_CapturesPendingExternalMember()
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-daren-retirement-capture-" + Guid.NewGuid().ToString("N"));
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        DarenRetirementEvidenceFixtures.Put(root, DarenRetirementEvidenceFixtures.Profile, DarenRetirementEvidenceFixtures.Before);
        await files.WriteFileAtomicBytesAsync(DarenRetirementEvidenceFixtures.Member, DarenRetirementEvidenceFixtures.Before);
        string manifest;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var transaction = await ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync(files, lease,
                [DarenRetirementEvidenceFixtures.Member], "retirement_capture", rollbackExternalFileIds: ["daren_reward_profile"]);
            lease.ExternalPublicationContext = transaction.LocalTransaction;
            try
            {
                Assert.Equal(7, transaction.SchemaVersion);
                await files.WriteFileAtomicBytesAsync(lease, DarenRetirementEvidenceFixtures.Member, DarenRetirementEvidenceFixtures.After);
                await new DarenRewardProfileFileStore(files).WriteExactBytesAtomicAsync(lease, DarenRetirementEvidenceFixtures.After);
                manifest = files.ResolvePath(transaction.ManifestPath);
                Assert.True(File.Exists(manifest));
                using var raw = JsonDocument.Parse(File.ReadAllBytes(manifest));
                Assert.Equal("daren_reward_profile", raw.RootElement.GetProperty("externalEntries")[0].GetProperty("fileId").GetString());
                Assert.Equal(DarenRetirementEvidenceFixtures.After, File.ReadAllBytes(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile)));
            }
            finally { lease.ExternalPublicationContext = null; transaction.LocalTransaction!.Access.Dispose(); }
        }
        DarenRetirementEvidenceFixtures.Freeze(DarenRetirementEvidenceFixtures.Capture("browser7",
            "ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync + declared actual DarenRewardProfileFileStore; staged transaction not resolved", files, manifest), output);
    }

    [Fact]
    public async Task OriginalStandaloneProfileProducer_CapturesCommittedCleanupDebt()
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-daren-retirement-capture-" + Guid.NewGuid().ToString("N"));
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var armed = false;
        var cut = false;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (armed && phase == TrustedLocalPublicationPhase.Committed)
                    {
                        armed = false; cut = true;
                        throw new IOException("owned retirement capture: committed cleanup debt");
                    }
                }
            });
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        DarenRetirementEvidenceFixtures.Put(root, DarenRetirementEvidenceFixtures.Profile, DarenRetirementEvidenceFixtures.Before);
        await files.WriteFileAtomicBytesAsync(DarenRetirementEvidenceFixtures.Member, DarenRetirementEvidenceFixtures.Before);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            armed = true;
            await new DarenRewardProfileFileStore(files).WriteExactBytesAtomicAsync(lease, DarenRetirementEvidenceFixtures.After);
        }
        var journal = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        Assert.True(cut); Assert.True(File.Exists(journal));
        using var raw = JsonDocument.Parse(File.ReadAllBytes(journal));
        Assert.True(raw.RootElement.GetProperty("Committed").GetBoolean());
        Assert.Equal(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile), raw.RootElement.GetProperty("Members")[0].GetProperty("Path").GetString());
        Assert.Equal(DarenRetirementEvidenceFixtures.After, File.ReadAllBytes(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile)));
        DarenRetirementEvidenceFixtures.Freeze(DarenRetirementEvidenceFixtures.Capture("standalone1",
            "DarenRewardProfileFileStore.WriteExactBytesAtomicAsync / original standalone publisher; actual Committed observer cleanup cut", files, journal), output);
    }
}
