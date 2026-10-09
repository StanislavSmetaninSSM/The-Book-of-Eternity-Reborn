using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class ImageDirectoryAdmissionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("constructor_missing")]
    [InlineData("gallery_unknown_recovery")]
    [InlineData("gallery_committed_debt")]
    [InlineData("gallery_symlink")]
    public async Task OriginalGalleryAdmitsStorageBeforeDirectoryOrAssociation(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-gallery-admission-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var seedCut = new CleanupPublicationCut();
        var debtArmed = false;
        var debtCuts = 0;
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                seedCut.Hooks.LocalPublicationObserver!(phase, index);
                if (debtArmed && phase == TrustedLocalPublicationPhase.Committed)
                {
                    debtCuts++;
                    throw new InvalidOperationException("Known committed gallery preparation cleanup debt.");
                }
            }
        };
        var seedFiles = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        seedCut.Attach(seedFiles);
        var constructor = mode == "constructor_missing";
        var weatherPath = Path.Combine(seedFiles.GameSessionPath, "game_state", "world", "weather.json");
        var imageRoot = Path.Combine(seedFiles.GameSessionPath, "images");
        var galleryPath = Path.Combine(imageRoot, "npcs");
        byte[] published = [7, 11, 13];
        Exception? seedFailure = null;
        if (!constructor)
        {
            seedFiles.EnsureDirectoryStructure();
            await using (var lease = await seedFiles.AcquireCanonicalWriteLeaseAsync()) seedFiles.GetOrCreateSessionGeneration(lease);
        }
        if (mode == "gallery_unknown_recovery")
        {
            seedCut.Select = (path, _) => path == weatherPath;
            seedCut.Armed = true;
            seedFailure = await Record.ExceptionAsync(() => seedFiles.WriteFileAtomicBytesAsync("game_state/world/weather.json", published));
            Assert.IsType<CoordinatedStatePublicationUncertainException>(seedFailure);
            seedCut.AssertReachedAndStopped();
            seedCut.Armed = false;
        }
        if (mode == "gallery_committed_debt")
        {
            debtArmed = true;
            await seedFiles.WriteFileAtomicBytesAsync("game_state/world/weather.json", published);
            debtArmed = false;
            Assert.Equal(1, debtCuts);
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(seedCut.JournalPath));
            Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
            Assert.Equal(published, File.ReadAllBytes(weatherPath));
        }
        var journalBefore = CleanupPublicationCut.ReadOptional(seedCut.JournalPath);
        var weatherBefore = CleanupPublicationCut.ReadOptional(weatherPath);
        var canonicalRootBefore = Directory.Exists(seedFiles.GameSessionPath);
        var imageRootBefore = Directory.Exists(imageRoot);
        var admissions = 0;
        var recovered = new List<string>();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = () => { admissions++; return Task.CompletedTask; },
                LocalPublicationRecoveryObserver = (phase, _) => recovered.Add(phase.ToString())
            });
        var requests = new List<string>();
        bool? journalAbsentAtAssociation = null;
        bool? priorBytesCorrectAtAssociation = null;
        bool? directoryReadyAtAssociation = null;
        bool? releasedBeforeAssociation = null;
        var admissionsAtAssociation = 0;
        void RecordAssociation(ProcessStartInfo request)
        {
            requests.Add(request.FileName);
            Assert.True(request.UseShellExecute);
            Assert.Empty(request.ArgumentList);
            Assert.Equal("", request.Arguments);
            admissionsAtAssociation = admissions;
            journalAbsentAtAssociation = !File.Exists(seedCut.JournalPath);
            priorBytesCorrectAtAssociation = published.SequenceEqual(File.ReadAllBytes(weatherPath));
            directoryReadyAtAssociation = Directory.Exists(galleryPath);
            // Never let the release probe repair missing gallery recovery itself.
            if (journalAbsentAtAssociation != true || priorBytesCorrectAtAssociation != true || directoryReadyAtAssociation != true)
                return;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var lease = files.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token).GetAwaiter().GetResult();
            lease.DisposeAsync().AsTask().GetAwaiter().GetResult();
            releasedBeforeAssociation = true;
        }
        var outside = Path.Combine(root, "outside");
        var sentinel = Path.Combine(outside, "sentinel.bin");
        var previousConsole = AnsiConsole.Console;
        using var consoleText = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(consoleText)
        });
        try
        {
            DesktopOpenResult? result = null;
            var failure = Record.Exception(() =>
            {
                var service = new ImageService(files, new GameSettings(), new LocalizationManager(),
                    NullLogger<ImageService>.Instance, new DesktopPathOpener(RecordAssociation));
                if (constructor) return;
                if (mode == "gallery_symlink")
                {
                    Directory.CreateDirectory(outside);
                    File.WriteAllBytes(sentinel, [17, 19]);
                    Directory.CreateSymbolicLink(galleryPath, outside);
                }
                result = service.OpenImagesFolder("npc");
            });
            var journalAfter = CleanupPublicationCut.ReadOptional(seedCut.JournalPath);
            var weatherAfter = CleanupPublicationCut.ReadOptional(weatherPath);
            var linkTarget = mode == "gallery_symlink" ? new DirectoryInfo(galleryPath).LinkTarget : null;
            output.WriteLine(JsonSerializer.Serialize(new
            {
                mode, failure = failure?.ToString(), seedFailure = seedFailure?.ToString(),
                status = result?.Status.ToString(), resultPath = result?.Path, resultError = result?.Error?.ToString(),
                canonicalRootBefore, canonicalRootAfter = Directory.Exists(files.GameSessionPath),
                imageRootBefore, imageRootAfter = Directory.Exists(imageRoot), galleryPath,
                galleryDirectoryExists = Directory.Exists(galleryPath), journalBefore, journalAfter, weatherBefore, weatherAfter,
                debtCuts, admissions, recovered, requests, admissionsAtAssociation, journalAbsentAtAssociation,
                priorBytesCorrectAtAssociation, directoryReadyAtAssociation, releasedBeforeAssociation,
                linkTarget, sentinel = CleanupPublicationCut.ReadOptional(sentinel), outside,
                console = consoleText.ToString(), SeedCut = seedCut.Evidence()
            }));
            Assert.Null(failure);
            if (constructor)
            {
                Assert.False(canonicalRootBefore); Assert.False(imageRootBefore);
                Assert.False(Directory.Exists(files.GameSessionPath)); Assert.False(Directory.Exists(imageRoot));
                Assert.Null(result); Assert.Equal(0, admissions); Assert.Empty(requests);
            }
            else if (mode == "gallery_unknown_recovery")
            {
                Assert.Equal(DesktopOpenStatus.Failed, result!.Status);
                Assert.IsType<InvalidDataException>(result.Error);
                Assert.Equal(1, admissions); Assert.Empty(requests); Assert.False(Directory.Exists(galleryPath));
                Assert.Equal(journalBefore, journalAfter); Assert.Equal(weatherBefore, weatherAfter);
                Assert.Equal(CleanupPublicationCut.Foreign, weatherAfter); Assert.Contains(galleryPath, consoleText.ToString());
            }
            else if (mode == "gallery_committed_debt")
            {
                Assert.Equal(DesktopOpenStatus.Requested, result!.Status);
                Assert.Equal(galleryPath, Assert.Single(requests));
                Assert.True(journalAbsentAtAssociation); Assert.True(priorBytesCorrectAtAssociation);
                Assert.True(directoryReadyAtAssociation); Assert.True(releasedBeforeAssociation);
                Assert.Equal(1, admissionsAtAssociation); Assert.Equal(2, admissions); Assert.NotEmpty(recovered);
                Assert.Null(journalAfter); Assert.Equal(published, weatherAfter);
            }
            else
            {
                Assert.Equal(DesktopOpenStatus.Failed, result!.Status); Assert.Empty(requests);
                Assert.Equal(outside, linkTarget); Assert.Equal(new byte[] { 17, 19 }, File.ReadAllBytes(sentinel));
                Assert.Single(Directory.GetFileSystemEntries(outside)); Assert.Contains(galleryPath, consoleText.ToString());
            }
        }
        finally { AnsiConsole.Console = previousConsole; }
    }
}
