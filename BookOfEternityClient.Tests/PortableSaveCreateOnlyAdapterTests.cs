using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Ordinary adapter collision coverage; ZIP validity alone is not game-save validity.</summary>
public sealed class PortableSaveCreateOnlyAdapterTests(ITestOutputHelper output)
{
    [Fact]
    public async Task SecondCreateOfIdenticalZipRejectsWithoutChangingArchiveGenerationOrLibrary()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-create-only-adapter-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            var generationId = Guid.NewGuid().ToString("N");
            var generationBytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId });
            Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
            File.WriteAllBytes(files.SessionGenerationPath, generationBytes);

            var libraryRoot = files.ResolvePath("saves");
            Directory.CreateDirectory(libraryRoot);
            var library = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var name in new[] { "manual-existing.zip", "autosave-existing.zip", "checkpoint-existing.zip" })
            {
                var path = Path.Combine(libraryRoot, name);
                WriteClosedZip(path, "sentinel.txt", Encoding.UTF8.GetBytes(name));
                library.Add(name, File.ReadAllBytes(path));
            }

            var inputRoot = Path.Combine(root, "candidate-input");
            Directory.CreateDirectory(inputRoot);
            var candidatePath = Path.Combine(inputRoot, "candidate.zip");
            byte[] payload = Encoding.UTF8.GetBytes("tiny synthetic adapter payload\n");
            WriteClosedZip(candidatePath, "payload.txt", payload);
            var candidateBytes = File.ReadAllBytes(candidatePath);
            var candidateHash = Hash(candidateBytes);
            AssertValidZip(candidatePath, "payload.txt", payload);
            var image = TrustedLocalFileImage.CaptureFile(new TrustedLocalFileScope([inputRoot]), candidatePath);
            Assert.True(image.IsFileBacked);
            Assert.Null(image.Bytes);
            Assert.Equal(candidateBytes.LongLength, image.Length);
            Assert.Equal(candidateHash, image.Sha256);

            const string targetName = "manual-create-only.zip";
            const string relativePath = "saves/" + targetName;
            var destination = files.ResolvePath(relativePath);
            Assert.False(File.Exists(destination));
            CanonicalLocalImageChange[] changes = [new(relativePath, TrustedLocalFileImage.FromBytes(null), image)];
            Assert.False(changes[0].Before.Exists);

            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.Equal(generationId, files.ReadExistingSessionGeneration(lease));
                var first = await files.PublishLocalImageFilesAsync(lease, changes);
                Assert.Equal(TrustedLocalPublicationDisposition.Committed, first.Disposition);
                Assert.Null(first.Failure);
                Assert.NotNull(first.Publication);
                Assert.Single(first.Publication.Members);
                Assert.Equal(TrustedLocalGeneration.Existing(generationId), first.Publication.Generation);
                library.Add(targetName, candidateBytes);
                AssertPreservedState();
                AssertValidZip(destination, "payload.txt", payload);

                // The same absent-before declaration remains a create, even when After is byte-identical.
                // Removing adapter absent-before validation would turn this into a false success or
                // a different outcome; the explicit rejection assertion must catch that regression.
                var rejection = await Assert.ThrowsAsync<InvalidDataException>(() =>
                    files.PublishLocalImageFilesAsync(lease, changes));
                Assert.Equal("An image member changed before publication.", rejection.Message);
                AssertPreservedState();
                output.WriteLine("First create: Committed, one member, no failure. Second identical create: explicit InvalidDataException at absent-before validation.");
                output.WriteLine("Archive/candidate SHA256: " + candidateHash + "; bytes: " + candidateBytes.Length);
                output.WriteLine("Generation bytes/identity, three library sentinels and complete library membership unchanged; publication journal and sibling scratch empty.");

                void AssertPreservedState()
                {
                    Assert.Equal(candidateBytes, File.ReadAllBytes(destination));
                    Assert.Equal(candidateHash, Hash(File.ReadAllBytes(destination)));
                    Assert.Equal(candidateBytes, File.ReadAllBytes(candidatePath));
                    Assert.Equal(candidateHash, Hash(File.ReadAllBytes(candidatePath)));
                    Assert.Equal(candidateBytes.LongLength, image.Length);
                    Assert.Equal(candidateHash, image.Sha256);
                    Assert.Equal(generationBytes, File.ReadAllBytes(files.SessionGenerationPath));
                    Assert.Equal(generationId, files.ReadExistingSessionGeneration(lease));
                    Assert.Equal(library.Keys.Order(StringComparer.Ordinal),
                        Directory.EnumerateFiles(libraryRoot, "*", SearchOption.AllDirectories)
                            .Select(path => Path.GetRelativePath(libraryRoot, path)).Order(StringComparer.Ordinal));
                    foreach (var (name, bytes) in library)
                        Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(libraryRoot, name)));
                    var journalRoot = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1");
                    if (Directory.Exists(journalRoot))
                        Assert.Empty(Directory.EnumerateFileSystemEntries(journalRoot));
                    Assert.Empty(Directory.EnumerateFileSystemEntries(root, ".boe-local-*", SearchOption.AllDirectories));
                }
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            Assert.False(Directory.Exists(root));
            output.WriteLine("Owned fixture cleanup complete; no fixture directory remains.");
        }
    }

    private static void WriteClosedZip(string path, string entryName, byte[] payload)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression).Open();
            entry.Write(payload);
        }
        stream.Flush(flushToDisk: true);
    }

    private static void AssertValidZip(string path, string entryName, byte[] payload)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = Assert.Single(archive.Entries);
        Assert.Equal(entryName, entry.FullName);
        Assert.Equal(payload.LongLength, entry.Length);
        using var source = entry.Open();
        using var contents = new MemoryStream();
        source.CopyTo(contents);
        Assert.Equal(payload, contents.ToArray());
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
