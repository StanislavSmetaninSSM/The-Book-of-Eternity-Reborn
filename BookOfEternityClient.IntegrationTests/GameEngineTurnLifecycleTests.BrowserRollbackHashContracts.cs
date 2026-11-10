using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BrowserOriginalAuthority_HashCallbackPreservesOriginalException(bool invalidOperation)
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        var manifest = GameEngine.ValidateDetachedBrowserBinding(staged);
        Exception original = invalidOperation ? new InvalidOperationException("original hash refusal") : new IOException("original native read failure");
        var byteCalls = 0; var hashCalls = 0;
        var observed = Assert.ThrowsAny<Exception>(() => GameEngine.ValidateOriginalBrowserAuthority(manifest, staged.AuthorityJson,
            _ => { byteCalls++; throw new Exception("exact callback must not use byte reader"); },
            _ => { hashCalls++; throw original; }));
        Assert.Same(original, observed); Assert.Equal(1, hashCalls); Assert.Equal(0, byteCalls);
    }

    [Theory]
    [InlineData("bytes")]
    [InlineData("missing")]
    [InlineData("leaf-link")]
    [InlineData("parent-link")]
    public async Task BrowserOriginalAdmission_RollbackDriftRefusedAfterSuccessWithinSameHeldLease(string drift)
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        await SessionOperationContext.RunParticipatingExpectedSessionAsync(_fs, staged.Binding.Generation, async () =>
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var manifest = InvokeHeldOriginalFullProof(_fs, staged, lease);
            var path = _fs.ResolvePath(manifest.RollbackBackups.First().Value);
            var original = File.ReadAllBytes(path);
            var parent = Path.GetDirectoryName(path)!; var moved = parent + ".rollback-test-owned";
            var target = Path.Combine(_rootPath, "rollback-test-target"); File.WriteAllBytes(target, original);
            try
            {
                if (drift == "bytes") File.WriteAllBytes(path, original.Concat(new byte[] { (byte)' ' }).ToArray());
                else if (drift == "missing") File.Delete(path);
                else if (drift == "leaf-link") { File.Delete(path); File.CreateSymbolicLink(path, target); }
                else { Directory.Move(parent, moved); Directory.CreateSymbolicLink(parent, moved); }
                Assert.True(lease.IsActive);
                var failure = Assert.Throws<TargetInvocationException>(() => InvokeHeldOriginalFullProof(_fs, staged, lease));
                Assert.IsType<InvalidDataException>(failure.InnerException); Assert.True(lease.IsActive);
            }
            finally
            {
                if (drift == "parent-link") { Directory.Delete(parent); Directory.Move(moved, parent); }
                else { if (File.Exists(path) || drift == "leaf-link") File.Delete(path); File.WriteAllBytes(path, original); }
                File.Delete(target);
            }
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(staged.RequestJson, File.ReadAllText(_fs.ResolvePath("input/turn_request.json")));
            Assert.Equal(manifest.ManifestPayloadHash, InvokeHeldOriginalFullProof(_fs, staged, lease).ManifestPayloadHash);
            return true;
        });
    }
}
