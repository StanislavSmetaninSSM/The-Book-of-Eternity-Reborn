using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerSyntheticBundlePublicationTests
{
    [Fact]
    public void SyntheticCleanup_OwnPrivateRootIsRemovedAndMissingRootRetryIsIdempotent()
    {
        var source = Stage();
        var privateRoot = Path.GetDirectoryName(source)!;
        var sentinel = Path.Combine(_root, "outside.bin"); File.WriteAllBytes(sentinel, _content);
        _fs.DeleteSyntheticRuntimeProposalStagingRoot(_root, privateRoot);
        Assert.False(Directory.Exists(privateRoot));
        _fs.DeleteSyntheticRuntimeProposalStagingRoot(_root, privateRoot);
        Assert.Equal(_content, File.ReadAllBytes(sentinel));
        Assert.True(Directory.Exists(Path.GetDirectoryName(privateRoot)));
    }

    [Theory]
    [InlineData("wrong-fixture")]
    [InlineData("area")]
    [InlineData("nested")]
    [InlineData("another-area")]
    public void SyntheticCleanup_UnownedOrMalformedRootRetainsStagingBytes(string fault)
    {
        var source = Stage();
        var privateRoot = Path.GetDirectoryName(source)!;
        var candidate = fault switch
        {
            "area" => Path.GetDirectoryName(privateRoot)!,
            "nested" => source,
            "another-area" => _fs.CreateRuntimeSaveStagingRoot(),
            _ => privateRoot
        };
        var admitted = fault == "wrong-fixture" ? Path.Combine(_root, "another-fixture") : _root;
        Assert.Throws<InvalidDataException>(() => _fs.DeleteSyntheticRuntimeProposalStagingRoot(admitted, candidate));
        AssertSourceRetained(source);
    }

    [Fact]
    public void SyntheticCleanup_LinkedTreeRetainsWholeStagingAndOutsideBytes()
    {
        var source = Stage();
        var outside = Path.Combine(_root, "outside"); Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel.bin"); File.WriteAllBytes(sentinel, _content);
        Directory.CreateSymbolicLink(Path.Combine(source, "linked"), outside);
        Assert.Throws<InvalidDataException>(() =>
            _fs.DeleteSyntheticRuntimeProposalStagingRoot(_root, Path.GetDirectoryName(source)!));
        AssertSourceRetained(source);
        Assert.Equal(_content, File.ReadAllBytes(sentinel));
    }

    [Fact]
    public async Task SyntheticCleanup_ActualStoreInvalidTreeRetainsExactStagingAndOutsideData()
    {
        var outside = Path.Combine(_root, "outside"); Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel.bin"); File.WriteAllBytes(sentinel, _content);
        string? source = null;
        _afterBoundary = () =>
        {
            // The fixture owns this unique area; production cleanup never enumerates
            // other staging roots to choose its authority.
            var privateRoot = Assert.Single(Directory.EnumerateDirectories(Path.Combine(_fs.RuntimeRootPath, "proposal-staging")));
            source = Path.Combine(privateRoot, _proposal.ProposalId);
            Directory.CreateSymbolicLink(Path.Combine(source, "linked"), outside);
            return Task.CompletedTask;
        };
        var result = await Publish(AdmittedStore());
        Assert.False(result.Published);
        Assert.NotNull(source);
        AssertSourceRetained(source!);
        Assert.Equal(_content, File.ReadAllBytes(Path.Combine(source!, "files", "binary.bin")));
        Assert.Equal(_content, File.ReadAllBytes(sentinel));
        Assert.NotNull(new DirectoryInfo(Path.Combine(source!, "linked")).LinkTarget);
    }
}
