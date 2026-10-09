using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class CanonicalListingAdmissionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("mods", "missing")]
    [InlineData("mods", "literal")]
    [InlineData("mods", "leaf_link")]
    [InlineData("mods", "unknown")]
    [InlineData("profiles", "missing")]
    [InlineData("profiles", "literal")]
    [InlineData("profiles", "leaf_link")]
    [InlineData("profiles", "unknown")]
    public async Task OriginalListsAdmitRecoveryAndPreserveExactInputs(string family, string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-listing-admission-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var seedFiles = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, cut.Hooks);
        cut.Attach(seedFiles);
        seedFiles.EnsureDirectoryStructure();
        await using (var lease = await seedFiles.AcquireCanonicalWriteLeaseAsync())
            seedFiles.GetOrCreateSessionGeneration(lease);
        var relativeDirectory = family == "mods" ? SystemModService.ModsDirectory : WorldDirectiveService.ProfilesDirectory;
        var directory = seedFiles.ResolvePath(relativeDirectory);
        var name = mode == "literal" ? "ancient\\world.json" : "world.json";
        var relative = relativeDirectory + "/" + name;
        var sourcePath = Path.Combine(directory, name);
        const string sourceText = """
            {"modId":"eternity","profileId":"eternity","name":"Вечность","description":"Точный мир","worldDirectives":{"worldTitle":"Мир башен","detailedWorldDescription":"Полное описание"}}
            """;
        var sourceBytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(sourceText)).ToArray();
        var outside = Path.Combine(root, "outside");
        var outsidePath = Path.Combine(outside, name);
        if (mode == "missing") Directory.Delete(directory);
        else if (mode == "leaf_link")
        {
            Directory.CreateDirectory(outside);
            File.WriteAllBytes(outsidePath, sourceBytes);
            File.CreateSymbolicLink(sourcePath, outsidePath);
        }
        else File.WriteAllBytes(sourcePath, sourceBytes);
        var modified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        if (mode == "literal") File.SetLastWriteTimeUtc(sourcePath, modified);
        var weatherPath = seedFiles.ResolvePath("game_state/world/weather.json");
        Exception? seedFailure = null;
        if (mode == "unknown")
        {
            cut.Select = (path, _) => path == weatherPath;
            cut.Armed = true;
            seedFailure = await Record.ExceptionAsync(() => seedFiles.WriteFileAtomicBytesAsync("game_state/world/weather.json", [7, 11, 13]));
            Assert.IsType<CoordinatedStatePublicationUncertainException>(seedFailure);
            cut.AssertReachedAndStopped();
            cut.Armed = false;
        }
        var journalBefore = CleanupPublicationCut.ReadOptional(cut.JournalPath);
        var weatherBefore = CleanupPublicationCut.ReadOptional(weatherPath);
        var directoryBefore = Directory.Exists(directory);
        var admissions = 0;
        var reads = new List<string>();
        var recovery = new List<string>();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = () => { admissions++; return Task.CompletedTask; },
                BeforeCanonicalReadOpenAsync = path => { reads.Add(path); return Task.CompletedTask; },
                LocalPublicationRecoveryObserver = (phase, _) => recovery.Add(phase.ToString())
            });
        var settings = new GameSettings { EnabledSystemMods = [name] };
        List<SystemModService.SystemModDescriptor>? mods = null;
        List<SystemModService.SystemModDescriptor>? withoutContent = null;
        List<WorldDirectiveService.WorldProfileDescriptor>? profiles = null;
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (family == "mods")
            {
                var service = new SystemModService(files, settings, NullLogger<SystemModService>.Instance);
                mods = await service.GetAvailableModsAsync(includeContent: true);
                if (mode == "literal") withoutContent = await service.GetAvailableModsAsync(includeContent: false);
            }
            else profiles = await new WorldDirectiveService(files, NullLogger<WorldDirectiveService>.Instance).GetAvailableProfilesAsync();
        });
        var reportedRelative = mods?.FirstOrDefault()?.RelativePath ?? profiles?.FirstOrDefault()?.RelativePath;
        var roundTrip = reportedRelative == null ? null : CleanupPublicationCut.ReadOptional(Path.Combine(files.GameSessionPath, reportedRelative));
        var journalAfter = CleanupPublicationCut.ReadOptional(cut.JournalPath);
        var weatherAfter = CleanupPublicationCut.ReadOptional(weatherPath);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            family, mode, root, directory, directoryBefore, directoryAfter = Directory.Exists(directory),
            canonicalRootExists = Directory.Exists(files.GameSessionPath), relative, sourcePath,
            sourceBytes, sourceAfter = CleanupPublicationCut.ReadOptional(sourcePath), reportedRelative, roundTrip,
            mods, withoutContent, profiles, failure = failure?.ToString(), seedFailure = seedFailure?.ToString(),
            admissions, reads, recovery, journalBefore, journalAfter, weatherBefore, weatherAfter,
            outsidePath, outsideAfter = CleanupPublicationCut.ReadOptional(outsidePath),
            linkTarget = mode == "leaf_link" ? new FileInfo(sourcePath).LinkTarget : null,
            SeedCut = cut.Evidence()
        }));
        Assert.Equal(new[] { name }, settings.EnabledSystemMods);
        Assert.True(Directory.Exists(files.GameSessionPath));
        if (mode == "unknown")
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Null(mods); Assert.Null(profiles); Assert.Equal(1, admissions); Assert.Empty(reads);
            Assert.Equal(journalBefore, journalAfter); Assert.Equal(weatherBefore, weatherAfter);
            Assert.Equal(CleanupPublicationCut.Foreign, weatherAfter);
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
        }
        else if (mode == "leaf_link")
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Null(mods); Assert.Null(profiles); Assert.Equal(1, admissions); Assert.Empty(reads);
            Assert.Equal(outsidePath, new FileInfo(sourcePath).LinkTarget);
            Assert.Equal(sourceBytes, File.ReadAllBytes(outsidePath));
            Assert.Single(Directory.GetFileSystemEntries(outside));
        }
        else
        {
            Assert.Null(failure);
            Assert.Equal(mode == "literal" && family == "mods" ? 2 : 1, admissions);
            Assert.Null(journalAfter);
            if (mode == "missing")
            {
                Assert.False(directoryBefore); Assert.False(Directory.Exists(directory)); Assert.Empty(reads);
                Assert.Equal(0, mods?.Count ?? profiles!.Count);
            }
            else
            {
                Assert.Equal(relative, reportedRelative); Assert.Equal(sourceBytes, roundTrip);
                Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
                Assert.Equal(family == "mods" ? 2 : 1, reads.Count); Assert.All(reads, path => Assert.Equal(relative, path));
                if (family == "mods")
                {
                    var mod = Assert.Single(mods!);
                    Assert.Equal(name, mod.FileName); Assert.Equal("eternity", mod.ModId);
                    Assert.Equal("Вечность", mod.Name); Assert.Equal("Точный мир", mod.Description);
                    Assert.True(mod.Enabled); Assert.True(mod.IsJson); Assert.Equal(sourceText, mod.Content);
                    Assert.Equal(modified.ToString("o"), mod.LastModifiedUtc);
                    Assert.Equal(mod with { Content = null }, Assert.Single(withoutContent!));
                }
                else
                {
                    var profile = Assert.Single(profiles!);
                    Assert.Equal(name, profile.FileName); Assert.Equal("eternity", profile.ProfileId);
                    Assert.Equal("Вечность", profile.Name); Assert.Equal("Точный мир", profile.Description);
                    Assert.Equal("Мир башен", profile.Directives.WorldTitle);
                    Assert.Equal("Полное описание", profile.Directives.DetailedWorldDescription);
                    Assert.Equal(modified.ToString("o"), profile.LastModifiedUtc);
                }
            }
        }
    }
}
