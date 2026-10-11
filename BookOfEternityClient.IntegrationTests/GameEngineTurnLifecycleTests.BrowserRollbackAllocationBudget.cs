using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    // Causal allocation contract on the existing complete physical proof, not
    // a new reader seam or an elapsed-time acceptance claim.
    [Fact]
    public async Task BrowserOriginalAdmission_RollbackPayloadGrowthDoesNotAllocateWholeContent()
    {
        const string logical = "game_state/core/rollback_allocation_probe.json";
        const int smallBytes = 22, largeBytes = 693766, proofsPerSample = 4, samples = 3;
        const long allowedAllocationGrowth = 128 * 1024;

        async Task<(FileSystemManager Files, PendingPlayerActionService.Staged Staged, GameEngine.PendingTurnSnapshotManifest Manifest)> Prepare(string label, int length)
        {
            var own = Path.Combine(_rootPath, "rollback-allocation-" + label);
            Directory.CreateDirectory(own);
            var files = new FileSystemManager(own, NullLogger<FileSystemManager>.Instance);
            // Counter scope must exclude optional observer/fixture-hook work.
            var collector = typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.Instance | BindingFlags.NonPublic)!;
            collector.SetValue(files, null); Assert.Null(collector.GetValue(files));
            Assert.Null(typeof(FileSystemManager).GetField("_hooks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(files));
            files.EnsureDirectoryStructure();
            const string prefix = "{\"probe\":true}";
            var content = Encoding.UTF8.GetBytes(prefix + new string(' ', length - prefix.Length));
            Assert.Equal(length, content.Length);
            await files.WriteFileAtomicBytesAsync(logical, content);
            var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true, files: files);
            var manifest = GameEngine.ValidateDetachedBrowserBinding(staged);
            Assert.True(manifest.Files.ContainsKey(logical)); Assert.True(manifest.RollbackBackups.ContainsKey(logical));
            Assert.Equal(content, File.ReadAllBytes(files.ResolvePath(manifest.Files[logical])));
            Assert.Equal(content, File.ReadAllBytes(files.ResolvePath(manifest.RollbackBackups[logical])));
            return (files, staged, manifest);
        }

        var small = await Prepare("small", smallBytes);
        var large = await Prepare("large", largeBytes);
        Assert.Equal(small.Manifest.Files.Keys.Order(), large.Manifest.Files.Keys.Order());
        Assert.Equal(small.Manifest.RollbackBackups.Keys.Order(), large.Manifest.RollbackBackups.Keys.Order());
        Assert.Equal(small.Manifest.RollbackBaselineFiles.Order(), large.Manifest.RollbackBaselineFiles.Order());
        // The only canonical content difference permitted here is probe padding.
        foreach (var key in small.Manifest.Files.Keys.Where(key => key != logical))
            Assert.Equal(File.ReadAllBytes(small.Files.ResolvePath(small.Manifest.Files[key])),
                File.ReadAllBytes(large.Files.ResolvePath(large.Manifest.Files[key])));
        foreach (var key in small.Manifest.RollbackBackups.Keys.Where(key => key != logical))
            Assert.Equal(File.ReadAllBytes(small.Files.ResolvePath(small.Manifest.RollbackBackups[key])),
                File.ReadAllBytes(large.Files.ResolvePath(large.Manifest.RollbackBackups[key])));

        async Task<long[]> Measure(FileSystemManager files, PendingPlayerActionService.Staged staged)
        {
            return await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, staged.Binding.Generation, async () =>
            {
                await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                Assert.True(lease.IsActive); Assert.Same(files, lease.Owner);
                var before = Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
                    .ToDictionary(path => Path.GetRelativePath(files.GameSessionPath, path), File.ReadAllBytes, StringComparer.Ordinal);
                for (var warm = 0; warm < 8; warm++) InvokeHeldOriginalFullProof(files, staged, lease);
                var allocated = new long[samples];
                // No await, manual GC, output or assertions inside counter intervals.
                for (var sample = 0; sample < samples; sample++)
                {
                    var starting = GC.GetAllocatedBytesForCurrentThread();
                    for (var proof = 0; proof < proofsPerSample; proof++) InvokeHeldOriginalFullProof(files, staged, lease);
                    allocated[sample] = (GC.GetAllocatedBytesForCurrentThread() - starting) / proofsPerSample;
                }
                Assert.True(lease.IsActive);
                var after = Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
                    .ToDictionary(path => Path.GetRelativePath(files.GameSessionPath, path), File.ReadAllBytes, StringComparer.Ordinal);
                Assert.Equal(before.Keys.Order(), after.Keys.Order());
                foreach (var key in before.Keys) Assert.Equal(before[key], after[key]);
                return allocated;
            });
        }

        var smallAllocations = await Measure(small.Files, small.Staged);
        var largeAllocations = await Measure(large.Files, large.Staged);
        var smallMedian = smallAllocations.Order().ElementAt(1);
        var largeMedian = largeAllocations.Order().ElementAt(1);
        var growth = largeMedian - smallMedian;
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            Qualification = "Warmed same-thread managed allocation slope on the actual complete held physical proof. Two independently owned genuine signed fixtures. Same logical inventory; only probe padding grows. No native/CPU/elapsed-time claim; no manual GC or observers.",
            SmallPayloadBytes = smallBytes, LargePayloadBytes = largeBytes, PayloadGrowthBytes = largeBytes - smallBytes,
            SnapshotCount = small.Manifest.Files.Count, RollbackCount = small.Manifest.RollbackBackups.Count,
            ProofsPerSample = proofsPerSample, Samples = samples,
            SmallAllocationsPerProof = smallAllocations, LargeAllocationsPerProof = largeAllocations,
            MedianAllocationGrowth = growth, AllowedAllocationGrowth = allowedAllocationGrowth
        }));
        Assert.True(growth <= allowedAllocationGrowth,
            $"Whole-content rollback allocation remains: payload grows {largeBytes - smallBytes}B, median full-proof managed allocation grows {growth}B (budget {allowedAllocationGrowth}B).");
    }
}
