using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task BrowserOriginalAdmission_HeldFullProofUsesOneLexicalFileScope()
    {
        var timings = new List<(string Stage, TimeSpan Elapsed)>();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BrowserOriginalAdmissionTimingObserver = (stage, elapsed) => timings.Add((stage, elapsed))
            });
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true, files: files);
        await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, staged.Binding.Generation, async () =>
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            Assert.True(lease.IsActive);
            Assert.Same(files, lease.Owner);
            timings.Clear();
            var manifest = InvokeHeldOriginalFullProof(files, staged, lease);
            Assert.True(lease.IsActive);
            Assert.NotEmpty(manifest.Files);
            Assert.NotEmpty(manifest.RollbackBackups);
            var constructions = timings.Where(item => item.Stage.StartsWith("trusted-scope-construction-", StringComparison.Ordinal)).ToArray();
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
            {
                PhysicalLeaseActive = lease.IsActive, SameFileSystem = ReferenceEquals(files, lease.Owner),
                SnapshotCount = manifest.Files.Count, RollbackCount = manifest.RollbackBackups.Count,
                Constructors = constructions.Length, ConstructorSeconds = constructions.Sum(item => item.Elapsed.TotalSeconds),
                HeldConstructors = constructions.Count(item => item.Stage == "trusted-scope-construction-held"),
                UnheldConstructors = constructions.Count(item => item.Stage == "trusted-scope-construction-unheld"),
                CompletePhysicalProofs = timings.Count(item => item.Stage == "physical-verification")
            }));
            Assert.Single(timings.Where(item => item.Stage == "physical-verification"));
            Assert.All(constructions, item => Assert.Equal("trusted-scope-construction-held", item.Stage));
            Assert.Single(constructions);
            return true;
        });
    }

    [Fact]
    public async Task BrowserOriginalAdmission_SnapshotDriftRefusedAfterSuccessWithinSameHeldLease()
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        await SessionOperationContext.RunParticipatingExpectedSessionAsync(_fs, staged.Binding.Generation, async () =>
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var manifest = InvokeHeldOriginalFullProof(_fs, staged, lease);
            var path = _fs.ResolvePath(manifest.Files.First().Value);
            var original = File.ReadAllBytes(path);
            try
            {
                File.WriteAllBytes(path, original.Concat(new byte[] { (byte)' ' }).ToArray());
                Assert.True(lease.IsActive);
                var failure = Assert.Throws<TargetInvocationException>(() => InvokeHeldOriginalFullProof(_fs, staged, lease));
                Assert.IsType<System.IO.InvalidDataException>(failure.InnerException);
                Assert.True(lease.IsActive);
            }
            finally { File.WriteAllBytes(path, original); }
            Assert.Equal(staged.RequestJson, File.ReadAllText(_fs.ResolvePath("input/turn_request.json")));
            Assert.Equal(original, File.ReadAllBytes(path));
            return true;
        });
    }

    private static GameEngine.PendingTurnSnapshotManifest InvokeHeldOriginalFullProof(FileSystemManager files,
        PendingPlayerActionService.Staged staged, FileSystemManager.CanonicalWriteLease lease) =>
        (GameEngine.PendingTurnSnapshotManifest)typeof(FileSystemManager)
            .GetMethod("ValidateOriginalBrowserAdmission", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(files, [staged, true, lease])!;
}
