using System.Security.Cryptography;
using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TrustedLocalFileImageTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-file-image-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Exact = [0xEF, 0xBB, 0xBF, 0xFF, 0, 0xFE, 17];
    private TrustedLocalFileScope Scope() { Directory.CreateDirectory(_root); return new([_root]); }
    private string Source => Path.Combine(_root, "source.bin");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureFilePreservesEmptyOrBinaryContentWithoutMaterializingBytes(bool empty)
    {
        var scope = Scope(); byte[] bytes = empty ? [] : Exact;
        File.WriteAllBytes(Source, bytes);
        var image = TrustedLocalFileImage.CaptureFile(scope, Source);
        Assert.True(image.Exists); Assert.True(image.IsFileBacked); Assert.Null(image.Bytes);
        Assert.Equal(bytes.LongLength, image.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), image.Sha256);
        using var destination = new MemoryStream(); image.CopyTo(destination);
        Assert.Equal(bytes, destination.ToArray());
        Assert.Equal(bytes, File.ReadAllBytes(Source));
    }

    [Fact]
    public void ByteImageOwnsAnIndependentSnapshotAndAbsenceDiffersFromEmpty()
    {
        var input = Exact.ToArray(); var image = TrustedLocalFileImage.FromBytes(input);
        input[0] = 42;
        using var destination = new MemoryStream(); image.CopyTo(destination);
        Assert.Equal(Exact, destination.ToArray()); Assert.False(image.IsFileBacked);
        var absent = TrustedLocalFileImage.FromBytes(null); var empty = TrustedLocalFileImage.FromBytes([]);
        Assert.False(absent.Exists); Assert.Null(absent.Bytes); Assert.Null(absent.Sha256); Assert.Equal(0, absent.Length);
        Assert.True(empty.Exists); Assert.NotNull(empty.Bytes); Assert.Equal(0, empty.Length);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())), empty.Sha256);
        Assert.Throws<InvalidOperationException>(() => absent.CopyTo(destination));
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("directory")]
    [InlineData("outside")]
    [InlineData("link")]
    public void CaptureRejectsUnapprovedMissingLinkedOrWrongTypeSource(string kind)
    {
        var scope = Scope(); var path = Source;
        if (kind == "directory") Directory.CreateDirectory(path);
        if (kind == "outside") path = Path.Combine(Path.GetTempPath(), "boe-image-outside-" + Guid.NewGuid().ToString("N"));
        if (kind == "link")
        {
            var target = Path.Combine(_root, "target.bin"); File.WriteAllBytes(target, Exact);
            File.CreateSymbolicLink(path, target);
        }
        if (kind == "absent") Assert.Throws<FileNotFoundException>(() => TrustedLocalFileImage.CaptureFile(scope, path));
        else Assert.Throws<InvalidDataException>(() => TrustedLocalFileImage.CaptureFile(scope, path));
    }

    [Theory]
    [InlineData("same-length")]
    [InlineData("shorter")]
    [InlineData("longer")]
    [InlineData("deleted")]
    public void FileCopyRejectsDriftAfterCapture(string drift)
    {
        var scope = Scope(); File.WriteAllBytes(Source, Exact);
        var image = TrustedLocalFileImage.CaptureFile(scope, Source);
        if (drift == "deleted") File.Delete(Source);
        else File.WriteAllBytes(Source, drift switch
        {
            "same-length" => Enumerable.Repeat((byte)42, Exact.Length).ToArray(),
            "shorter" => [1], _ => [.. Exact, 42]
        });
        using var destination = new MemoryStream();
        if (drift == "deleted") Assert.Throws<FileNotFoundException>(() => image.CopyTo(destination));
        else Assert.Throws<InvalidDataException>(() => image.CopyTo(destination));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
