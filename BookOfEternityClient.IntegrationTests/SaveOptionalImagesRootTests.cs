using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class SaveOptionalImagesRootTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OriginalSaveSkipsLinkedOptionalImagesRootAndPreservesLiteralHostNames(bool rootLink)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-save-images-root-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        var reads = new List<string>(); var publications = new List<string>();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = path => { reads.Add(path); return Task.CompletedTask; },
                LocalPublicationObserver = (phase, _) => publications.Add(phase.ToString())
            });
        var state = PortableSaveFixture.Seed(files);
        // Replace only the fixture's seeded generation with the actual cold publisher before exercising save.
        File.Delete(files.SessionGenerationPath);
        await using (var generationLease = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(generationLease);
        var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
        var images = files.ResolvePath("images");
        if (Directory.Exists(images)) Directory.Delete(images, recursive: true);
        var outside = Path.Combine(root, "outside-optional-images"); Directory.CreateDirectory(outside);
        var actualRoot = rootLink ? outside : images;
        Directory.CreateDirectory(actualRoot);
        var selected = Path.Combine(actualRoot, "npcs", "portrait.png"); Directory.CreateDirectory(Path.GetDirectoryName(selected)!);
        var selectedBytes = new byte[] { 31, 41, 59 }; File.WriteAllBytes(selected, selectedBytes);
        var exactUpper = Path.Combine(actualRoot, "Scenes", "literal.png"); Directory.CreateDirectory(Path.GetDirectoryName(exactUpper)!); File.WriteAllBytes(exactUpper, [26, 53]);
        var ephemeral = Path.Combine(actualRoot, "scenes", "ephemeral.png"); Directory.CreateDirectory(Path.GetDirectoryName(ephemeral)!); File.WriteAllBytes(ephemeral, [58, 97]);
        if (rootLink) Directory.CreateSymbolicLink(images, outside);
        Dictionary<string, byte[]> ReadOutsideFiles() => Directory.EnumerateFiles(outside, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var outsideBefore = ReadOutsideFiles();
        var logger = new PortableSaveFixture.CaptureLogger(); var service = new SaveLoadService(files, state, logger);
        reads.Clear(); publications.Clear(); SaveCreationResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await service.CreateSaveAsync("Original optional images", "literal optional root", turnNumber: 4));
        Dictionary<string, byte[]> entries = new(StringComparer.Ordinal); JsonElement? manifest = null;
        if (result?.Committed == true)
        {
            using var archive = ZipFile.OpenRead(files.ResolvePath(result.DestinationRelativePath!));
            foreach (var entry in archive.Entries)
            {
                using var stream = entry.Open(); using var buffer = new MemoryStream(); stream.CopyTo(buffer); entries.Add(entry.FullName, buffer.ToArray());
            }
            using var document = JsonDocument.Parse(entries["save_manifest.json"]); manifest = document.RootElement.Clone();
        }
        output.WriteLine(JsonSerializer.Serialize(new { rootLink, root, images, outside, selected, selectedBytes, outsideAfter = File.ReadAllBytes(selected),
            actualLinkTarget = new DirectoryInfo(images).LinkTarget, failure = failure?.ToString(), disposition = result?.Disposition.ToString(),
            followUp = result?.NeedsFollowUp, blocked = result?.ContinuationBlocked, resultFailure = result?.Failure?.ToString(),
            outsideBefore, outsideAfterFiles = ReadOutsideFiles(), errors = logger.Errors.Select(error => error.ToString()), result?.DestinationRelativePath, reads, publications, entries, manifest,
            generationBefore, generationAfter = File.ReadAllBytes(files.SessionGenerationPath) }));
        Assert.Null(failure); Assert.NotNull(result); Assert.True(result.Committed); Assert.False(result.NeedsFollowUp); Assert.False(result.ContinuationBlocked); Assert.Empty(logger.Errors);
        Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath)); Assert.Equal(selectedBytes, File.ReadAllBytes(selected));
        Assert.Contains("Committed", publications);
        var outsideAfterFiles = ReadOutsideFiles();
        Assert.Equal(outsideBefore.Keys.Order(StringComparer.Ordinal), outsideAfterFiles.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in outsideBefore) Assert.Equal(pair.Value, outsideAfterFiles[pair.Key]);
        Assert.DoesNotContain(reads, path => path.StartsWith("images/scenes/", StringComparison.Ordinal));
        Assert.Contains("game_state/meta/soul_state.json", entries.Keys); Assert.Contains("save_metadata.json", entries.Keys);
        Assert.DoesNotContain("images/scenes/ephemeral.png", entries.Keys);
        if (rootLink)
        {
            Assert.Equal(outside, new DirectoryInfo(images).LinkTarget); Assert.DoesNotContain(entries.Keys, path => path.StartsWith("images/", StringComparison.Ordinal));
            Assert.DoesNotContain(reads, path => path.StartsWith("images/", StringComparison.Ordinal));
        }
        else
        {
            Assert.Equal(selectedBytes, entries["images/npcs/portrait.png"]); Assert.Equal(new byte[] { 26, 53 }, entries["images/Scenes/literal.png"]);
            Assert.Contains("images/npcs/portrait.png", reads); Assert.Contains("images/Scenes/literal.png", reads);
        }
        Assert.Equal(entries.Keys.Where(path => path != "save_manifest.json").Order(StringComparer.Ordinal),
            manifest!.Value.GetProperty("entries").EnumerateArray().Select(entry => entry.GetProperty("path").GetString()!).Order(StringComparer.Ordinal));
        foreach (var entry in manifest.Value.GetProperty("entries").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!; Assert.Equal(entries[path].Length, entry.GetProperty("length").GetInt64());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(entries[path])).ToLowerInvariant(), entry.GetProperty("sha256").GetString()!.ToLowerInvariant());
        }
        using (var lockProbe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) Assert.True(lockProbe.CanWrite);
        Assert.Empty(Directory.EnumerateFiles(files.RuntimeRootPath, "*.zip", SearchOption.AllDirectories));
    }
}
