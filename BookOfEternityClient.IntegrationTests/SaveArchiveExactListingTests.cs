using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class SaveArchiveExactListingTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("listing", "literal")]
    [InlineData("listing", "leaf_link")]
    [InlineData("retention", "literal")]
    [InlineData("retention", "leaf_link")]
    public async Task OriginalArchiveConsumersPreserveExactSelectedNamesAndIgnoreUnselectedLinks(string consumer, string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-archive-exact-list-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        var publications = new List<string>();
        var reads = new List<string>();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                AfterCanonicalReadInitialValidationAsync = path => { reads.Add(path); return Task.CompletedTask; },
                LocalPublicationObserver = (phase, _) => publications.Add(phase.ToString())
            });
        var state = PortableSaveFixture.Seed(files); state.Settings.MaxAutosaves = 0;
        var saveDir = consumer == "listing" ? "saves/manual_saves" : "saves/autosaves";
        var directory = files.ResolvePath(saveDir);
        var name = mode == "literal" ? "old\\archive.zip" : "old.zip";
        var relative = saveDir + "/" + name;
        var selected = Path.Combine(directory, name);
        var outside = Path.Combine(root, "outside"); Directory.CreateDirectory(outside);
        var outsideZip = Path.Combine(outside, "external.zip");
        WriteArchive(mode == "leaf_link" ? outsideZip : selected, "Точная запись", "2026-01-02T00:00:00Z");
        if (mode == "leaf_link") File.CreateSymbolicLink(selected, outsideZip);
        var sourceBytes = File.ReadAllBytes(selected);
        var ignoredUpper = Path.Combine(directory, "ignored.ZIP");
        File.WriteAllBytes(ignoredUpper, [17, 19]);
        var ignoredFile = Path.Combine(directory, "ignored.txt");
        var outsideText = Path.Combine(outside, "external.txt"); File.WriteAllBytes(outsideText, [23, 29]);
        File.CreateSymbolicLink(ignoredFile, outsideText);
        var ignoredDirectory = Path.Combine(directory, "ignored-nested");
        Directory.CreateSymbolicLink(ignoredDirectory, outside);
        if (consumer == "listing") WriteArchive(Path.Combine(directory, "newer.zip"), "Новая запись", "2026-01-03T00:00:00Z");
        var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
        var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance);
        List<SaveInfo>? saves = null; SaveCreationResult? result = null;
        byte[]? committedNewBytes = null;
        if (consumer == "retention")
            service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance,
                new SaveLoadServiceHooks { BeforeAutosaveRetentionAsync = _ =>
                {
                    var actual = Directory.GetFiles(directory, "*.zip").Single(path => path != selected);
                    committedNewBytes = File.ReadAllBytes(actual); return Task.CompletedTask;
                }});
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (consumer == "listing") saves = await service.GetAvailableSavesAsync(saveDir);
            else result = await service.CreateAutosaveAsync(11);
        });
        var selectedExists = File.Exists(selected);
        var selectedAfter = selectedExists ? File.ReadAllBytes(selected) : null;
        output.WriteLine(JsonSerializer.Serialize(new { consumer, mode, root, relative, sourceBytes, selectedExists, selectedAfter,
            failure = failure?.ToString(), saves, disposition = result?.Disposition.ToString(), destination = result?.DestinationRelativePath,
            followUp = result?.NeedsFollowUp, blocked = result?.ContinuationBlocked, resultFailure = result?.Failure?.ToString(), committedNewBytes,
            reads, publications, topLevel = Directory.GetFileSystemEntries(directory), generationBefore,
            generationAfter = File.ReadAllBytes(files.SessionGenerationPath), outsideZip,
            outsideAfter = mode == "leaf_link" ? File.ReadAllBytes(outsideZip) : null }));
        Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Equal(new byte[] { 17, 19 }, File.ReadAllBytes(ignoredUpper));
        Assert.Equal(outsideText, new FileInfo(ignoredFile).LinkTarget);
        Assert.Equal(new byte[] { 23, 29 }, File.ReadAllBytes(outsideText));
        Assert.Equal(outside, new DirectoryInfo(ignoredDirectory).LinkTarget);
        if (consumer == "listing")
        {
            if (mode == "literal")
            {
                Assert.Null(failure); Assert.NotNull(saves); Assert.Equal(2, saves.Count);
                Assert.Equal("Новая запись", saves[0].Metadata!.SaveName); Assert.Equal("Точная запись", saves[1].Metadata!.SaveName);
                Assert.Equal(selected, saves[1].FileName); Assert.Equal(sourceBytes.LongLength, saves[1].FileSize);
                Assert.Contains(relative, reads); Assert.Equal(sourceBytes, selectedAfter);
            }
            else
            {
                Assert.IsType<InvalidDataException>(failure); Assert.Null(saves); Assert.Empty(reads);
                Assert.Equal(outsideZip, new FileInfo(selected).LinkTarget); Assert.Equal(sourceBytes, File.ReadAllBytes(outsideZip));
            }
        }
        else
        {
            Assert.Null(failure); Assert.NotNull(result); Assert.True(result.Committed); Assert.NotNull(committedNewBytes);
            Assert.Contains("Committed", publications);
            if (mode == "literal")
            {
                Assert.False(result.NeedsFollowUp); Assert.Null(result.Failure); Assert.False(selectedExists);
                Assert.Empty(Directory.GetFiles(directory, "*.zip"));
            }
            else
            {
                Assert.True(result.NeedsFollowUp); Assert.NotNull(result.Failure); Assert.False(result.ContinuationBlocked);
                Assert.Equal(outsideZip, new FileInfo(selected).LinkTarget); Assert.Equal(sourceBytes, File.ReadAllBytes(outsideZip));
                Assert.Equal(2, Directory.GetFiles(directory, "*.zip").Length);
                Assert.Equal(committedNewBytes, File.ReadAllBytes(files.ResolvePath(result.DestinationRelativePath!)));
            }
        }
    }

    private static void WriteArchive(string path, string name, string timestamp)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        using var stream = archive.CreateEntry("save_metadata.json").Open();
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { saveName = name, timestamp, turnNumber = 7 }));
        stream.Write(bytes);
    }
}
