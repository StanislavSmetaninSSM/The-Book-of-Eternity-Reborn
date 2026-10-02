using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the image adapter's actual pre-intent generation and captured-source admission boundaries.
/// </summary>
public sealed class TrustedLocalImageAdapterAdmissionTests
{
    /// <summary>
    /// Retains the destination and complete library when generation or candidate input changes before intent.
    /// </summary>
    /// <param name="drift">
    /// Selects a changed generation, same-length changed candidate bytes, or a removed candidate source.
    /// </param>
    [Theory]
    [InlineData("generation")]
    [InlineData("candidate-bytes")]
    [InlineData("candidate-missing")]
    public async Task PreIntentDriftCannotPublishCapturedArchive(string drift)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-image-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var generationId = Guid.NewGuid().ToString("N");
            var generation = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId, extra = "preserved" });
            var candidate = Path.Combine(root, "candidate.bin");
            File.WriteAllBytes(candidate, [10, 0, 255]);
            const string target = "saves/manual_saves/new.zip";
            var phases = new List<TrustedLocalPublicationPhase>();
            var reached = 0;
            FileSystemManager? files = null;
            files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    AfterCanonicalMutationBoundaryValidatedAsync = path =>
                    {
                        Assert.Equal(target, path);
                        reached++;
                        if (drift == "generation")
                        {
                            generation = JsonSerializer.SerializeToUtf8Bytes(new
                            { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N"), extra = "changed externally" });
                            File.WriteAllBytes(files!.SessionGenerationPath, generation);
                        }
                        else if (drift == "candidate-bytes") File.WriteAllBytes(candidate, [11, 0, 255]);
                        else File.Delete(candidate);
                        return Task.CompletedTask;
                    },
                    LocalPublicationObserver = (phase, _) => phases.Add(phase)
                });
            files.EnsureDirectoryStructure();
            Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
            File.WriteAllBytes(files.SessionGenerationPath, generation);
            var library = new Dictionary<string, byte[]>();
            foreach (var folder in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            {
                var path = $"saves/{folder}/sentinel.zip";
                library.Add(path, [9, 0, 255]);
                File.WriteAllBytes(files.ResolvePath(path), library[path]);
            }
            var outside = Path.Combine(root, "outside-sentinel.bin");
            File.WriteAllBytes(outside, [92, 0, 255]);
            var image = TrustedLocalFileImage.CaptureFile(new TrustedLocalFileScope([root]), candidate);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                CanonicalLocalImageChange[] changes = [new(target, TrustedLocalFileImage.FromBytes(null), image)];
                if (drift == "generation")
                {
                    var error = await Assert.ThrowsAsync<InvalidDataException>(() => files.PublishLocalImageFilesAsync(lease, changes));
                    Assert.Equal("The session generation changed during image preparation.", error.Message);
                }
                else
                {
                    var outcome = await files.PublishLocalImageFilesAsync(lease, changes);
                    // No validated prepared decision exists yet: the shared outcome remains conservatively Uncertain.
                    Assert.Equal(TrustedLocalPublicationDisposition.Uncertain, outcome.Disposition);
                    Assert.Null(outcome.Publication);
                    if (drift == "candidate-bytes") Assert.IsType<InvalidDataException>(outcome.Failure);
                    else Assert.IsType<FileNotFoundException>(outcome.Failure);
                }
                Assert.Equal(1, reached);
                Assert.Empty(phases);
                Assert.False(File.Exists(files.ResolvePath(target)));
                Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
                Assert.Equal(new byte[] { 92, 0, 255 }, File.ReadAllBytes(outside));
                Assert.Equal(library.Keys.Order(StringComparer.Ordinal),
                    Directory.GetFiles(files.ResolvePath("saves"), "*", SearchOption.AllDirectories)
                        .Select(path => "saves/" + Path.GetRelativePath(files.ResolvePath("saves"), path).Replace('\\', '/'))
                        .Order(StringComparer.Ordinal));
                foreach (var member in library) Assert.Equal(member.Value, File.ReadAllBytes(files.ResolvePath(member.Key)));
                var journal = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1");
                if (Directory.Exists(journal)) Assert.Empty(Directory.EnumerateFileSystemEntries(journal));
                Assert.Empty(Directory.GetFiles(root, ".boe-local-*", SearchOption.AllDirectories));
                if (drift == "candidate-missing") Assert.False(File.Exists(candidate));
                else Assert.Equal(drift == "candidate-bytes" ? new byte[] { 11, 0, 255 } : [10, 0, 255], File.ReadAllBytes(candidate));
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
