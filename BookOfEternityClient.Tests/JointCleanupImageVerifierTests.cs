using System.Text.Json;
using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks the shared cleanup image leaf without creating a joint gameplay session.
/// </summary>
/// <param name="output">
/// The test sink retaining every produced component timing record.
/// </param>
public sealed class JointCleanupImageVerifierTests(ITestOutputHelper output)
{
    /// <summary>
    /// Reads every member and retained image once, preserving exact absence and order.
    /// </summary>
    /// <param name="after">
    /// Whether committed after-images or original before-images are expected.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Images_ReadExactBeforeAndAfterCohort(bool after)
    {
        var members = new[] { new CanonicalLocalFileChange("member", [1], null) };
        var retained = new Dictionary<string, byte[]?> { ["retained"] = [4], ["absent"] = null };
        var reads = new List<string>();
        await JointCleanupImageVerifier.RequireImagesAsync(members, retained, after, async path =>
        {
            reads.Add(path); await Task.Yield();
            return path == "member" ? (after ? null : new byte[] { 1 }) : retained[path];
        });
        Assert.Equal(new[] { "member", "retained", "absent" }, reads);
    }

    /// <summary>
    /// Refuses exact-byte or absence drift and does not continue to later entries.
    /// </summary>
    /// <param name="target">
    /// The member, retained image or expected absence to change.
    /// </param>
    /// <param name="after">
    /// Whether the expected member is its committed absent after-image.
    /// </param>
    [Theory]
    [InlineData("member", false)]
    [InlineData("member", true)]
    [InlineData("retained", false)]
    [InlineData("retained", true)]
    [InlineData("absent", false)]
    [InlineData("absent", true)]
    public async Task Images_RefuseChangedBytesAndUnexpectedPresence(string target, bool after)
    {
        var reads = new List<string>();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => JointCleanupImageVerifier.RequireImagesAsync(
            [new("member", [1], null)], new Dictionary<string, byte[]?> { ["retained"] = [4], ["absent"] = null }, after,
            path =>
            {
                reads.Add(path);
                byte[]? actual = path == target ? [] : path == "member" ? (after ? null : [1]) : path == "retained" ? [4] : null;
                return Task.FromResult(actual);
            }));
        Assert.Equal((target == "member" ? "joint_cleanup_member_mismatch: " : "joint_cleanup_retained_union_changed: ") + target, error.Message);
        Assert.Equal(target, reads[^1]);
        Assert.Equal(target == "member" ? 1 : target == "retained" ? 2 : 3, reads.Count);
    }

    /// <summary>
    /// Propagates a suspended read failure unchanged before attempting another image.
    /// </summary>
    [Fact]
    public async Task Images_PropagateReadFailure()
    {
        var failure = new IOException("reader failed");
        var reads = 0;
        var error = await Assert.ThrowsAsync<IOException>(() => JointCleanupImageVerifier.RequireImagesAsync([new("member", [1], null)],
            new Dictionary<string, byte[]?> { ["retained"] = [4] }, false,
            async _ => { reads++; await Task.Yield(); throw failure; }));
        Assert.Same(failure, error); Assert.Equal(1, reads);
    }

    /// <summary>
    /// Uses the real publisher callback and complete physical after-readback on a small owned cohort.
    /// </summary>
    /// <param name="drift">
    /// Whether a retained byte changes before prepared-namespace validation.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Images_RealPublisherRevalidatesPreparedNamespace(bool drift)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-cleanup-images-" + Guid.NewGuid().ToString("N"));
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        try
        {
            files.EnsureDirectoryStructure();
            const string member = "game_state/core/cleanup_member.bin";
            const string kept = "game_state/core/cleanup_retained.bin";
            const string absent = "game_state/core/cleanup_absent.bin";
            await files.WriteFileAtomicBytesAsync(member, new byte[] { 1 });
            await files.WriteFileAtomicBytesAsync(kept, new byte[] { 4 });
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var members = new[] { new CanonicalLocalFileChange(member, [1], null) };
            var retained = new Dictionary<string, byte[]?> { [kept] = [4], [absent] = null };
            var reads = 0;
            Task<byte[]?> Read(string path) { reads++; return files.ReadFileBytesAsync(lease, path); }
            var clock = Stopwatch.StartNew();
            await JointCleanupImageVerifier.RequireImagesAsync(members, retained, false, Read);
            Assert.Equal(3, reads);
            if (drift) File.WriteAllBytes(files.ResolvePath(kept), [9]);
            var callback = 0;
            async Task Validate() { callback++; await JointCleanupImageVerifier.RequireImagesAsync(members, retained, false, Read); }
            if (drift)
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => files.PublishLocalFilesAsync(lease, members, validatePreparedNamespace: Validate));
                Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(files.ResolvePath(member)));
                Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(files.ResolvePath(kept)));
            }
            else
            {
                var result = await files.PublishLocalFilesAsync(lease, members, validatePreparedNamespace: Validate);
                Assert.Equal(TrustedLocalPublicationDisposition.Committed, result.Disposition);
                files.RequireCommittedLocalPublication(result);
                await JointCleanupImageVerifier.RequireImagesAsync(members, retained, true, Read);
                Assert.Equal(9, reads);
                Assert.False(File.Exists(files.ResolvePath(member)));
                Assert.Equal(new byte[] { 4 }, File.ReadAllBytes(files.ResolvePath(kept)));
                Assert.False(File.Exists(files.ResolvePath(absent)));
            }
            Assert.Equal(1, callback);
            output.WriteLine(JsonSerializer.Serialize(new { Operation="CleanupImages.RealPublisherAndReadback", Drift=drift, Reads=reads, Milliseconds=clock.Elapsed.TotalMilliseconds }));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            output.WriteLine(JsonSerializer.Serialize(new { CleanupOwnedRoot=root, Absent=!Directory.Exists(root) }));
        }
    }

}
