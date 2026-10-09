using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class ImageSourceAdmissionTests(ITestOutputHelper output)
{
    // Raw directory/file selection before admission fails the Unknown/debt/link
    // rows. Reacquiring instead of borrowing the browser's original lease fails
    // its positive existing-image control. No desktop process or provider runs.
    [Theory]
    [InlineData("lookup_unknown")]
    [InlineData("export_unknown")]
    [InlineData("scene_unknown")]
    [InlineData("lookup_debt")]
    [InlineData("export_debt")]
    [InlineData("scene_debt")]
    [InlineData("canonical_prompt_unknown")]
    [InlineData("external_prompt")]
    [InlineData("lookup_link")]
    [InlineData("scene_output_link")]
    [InlineData("browser_existing")]
    public async Task OriginalImageSourcesAdmitBeforeLookupExportOrScene(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-image-source-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var seedCut = new CleanupPublicationCut();
        var debtArmed = false;
        var debtCuts = 0;
        var seedFiles = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    seedCut.Hooks.LocalPublicationObserver!(phase, index);
                    if (debtArmed && phase == TrustedLocalPublicationPhase.Committed)
                    { debtCuts++; throw new InvalidOperationException("Known committed image-source cleanup debt."); }
                }
            });
        seedCut.Attach(seedFiles);
        seedFiles.EnsureDirectoryStructure();
        await using (var lease = await seedFiles.AcquireCanonicalWriteLeaseAsync()) seedFiles.GetOrCreateSessionGeneration(lease);
        var originalGeneration = File.ReadAllBytes(seedFiles.SessionGenerationPath);
        const string relative = "images/npcs/actor__img_20261009_010101001.png";
        var imagePath = seedFiles.ResolvePath(relative);
        var outputDirectory = seedFiles.ResolvePath("output");
        var outputImage = Path.Combine(outputDirectory, "scene.png");
        var weatherPath = seedFiles.ResolvePath("game_state/world/weather.json");
        var outside = Path.Combine(root, "outside");
        var outsideImage = Path.Combine(outside, "outside.png");
        var exportPath = Path.Combine(root, "exports", "result.png");
        byte[] imageBytes = [0, 7, 11, 13, 255];
        byte[] published = [17, 19, 23];
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        Directory.CreateDirectory(outside);
        File.WriteAllBytes(imagePath, imageBytes);
        File.WriteAllBytes(outsideImage, imageBytes);
        if (mode == "lookup_link")
        { File.Delete(imagePath); File.CreateSymbolicLink(imagePath, outsideImage); }
        if (mode == "scene_output_link")
        {
            if (Directory.Exists(outputDirectory)) Directory.Delete(outputDirectory, recursive: false);
            Directory.CreateSymbolicLink(outputDirectory, outside);
        }
        else
        { Directory.CreateDirectory(outputDirectory); File.WriteAllBytes(outputImage, imageBytes); }
        // Original lookup/scene selection is top-level only. Ignored descendants
        // must not become authority dependencies merely because admission is added.
        var ignoredImage = Path.Combine(Path.GetDirectoryName(imagePath)!, "ignored", "link.png");
        var ignoredOutput = Path.Combine(outputDirectory, "ignored", "link.png");
        if (IsDebtMode(mode) || mode == "browser_existing")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ignoredImage)!);
            File.CreateSymbolicLink(ignoredImage, outsideImage);
            Directory.CreateDirectory(Path.GetDirectoryName(ignoredOutput)!);
            File.CreateSymbolicLink(ignoredOutput, outsideImage);
        }
        File.SetLastWriteTimeUtc(mode == "scene_output_link" ? outsideImage : outputImage, DateTime.UtcNow);
        Exception? seedFailure = null;
        var unknown = mode.EndsWith("_unknown", StringComparison.Ordinal) || mode == "external_prompt";
        var debt = mode.EndsWith("_debt", StringComparison.Ordinal);
        if (unknown)
        {
            seedCut.Select = (path, _) => path == weatherPath; seedCut.Armed = true;
            seedFailure = await Record.ExceptionAsync(() => seedFiles.WriteFileAtomicBytesAsync("game_state/world/weather.json", published));
            Assert.IsType<CoordinatedStatePublicationUncertainException>(seedFailure);
            seedCut.AssertReachedAndStopped(); seedCut.Armed = false;
        }
        if (debt)
        {
            debtArmed = true;
            await seedFiles.WriteFileAtomicBytesAsync("game_state/world/weather.json", published);
            debtArmed = false; Assert.Equal(1, debtCuts);
            using var metadata = CleanupPublicationCut.Metadata(File.ReadAllBytes(seedCut.JournalPath));
            Assert.True(metadata.RootElement.GetProperty("Committed").GetBoolean());
        }
        var journalBefore = CleanupPublicationCut.ReadOptional(seedCut.JournalPath);
        var weatherBefore = CleanupPublicationCut.ReadOptional(weatherPath);
        var admissions = 0;
        var ordinaryAdmissions = 0;
        var closingAdmissions = 0;
        var closing = false;
        var sourceReads = new List<string>();
        var recoveryBeforeSourceRead = new List<bool>();
        var recovered = new List<string>();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = () =>
                {
                    admissions++;
                    if (closing) { closingAdmissions++; closing = false; } else ordinaryAdmissions++;
                    return Task.CompletedTask;
                },
                SessionOperationClosingAsync = () => { Assert.False(closing); closing = true; return Task.CompletedTask; },
                BeforeCanonicalReadOpenAsync = path =>
                {
                    sourceReads.Add(path);
                    recoveryBeforeSourceRead.Add(!File.Exists(seedCut.JournalPath) && published.SequenceEqual(File.ReadAllBytes(weatherPath)));
                    return Task.CompletedTask;
                },
                LocalPublicationRecoveryObserver = (phase, _) => recovered.Add(phase.ToString())
            });
        var requests = new List<string>();
        bool? recoveredAtAssociation = null;
        bool? releasedBeforeAssociation = null;
        var admissionsAtAssociation = 0;
        void RecordAssociation(ProcessStartInfo request)
        {
            requests.Add(request.FileName); Assert.True(request.UseShellExecute); Assert.Empty(request.ArgumentList);
            admissionsAtAssociation = admissions;
            recoveredAtAssociation = !File.Exists(seedCut.JournalPath) && published.SequenceEqual(File.ReadAllBytes(weatherPath));
            if (!debt || recoveredAtAssociation != true) return;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var probe = files.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token).GetAwaiter().GetResult();
            probe.DisposeAsync().AsTask().GetAwaiter().GetResult(); releasedBeforeAssociation = true;
        }
        var settings = new GameSettings { GenerateSceneImages = true, ShowImagesInConsole = false, ImageProvider = "none" };
        var service = new ImageService(files, settings, new LocalizationManager(),
            NullLogger<ImageService>.Instance, new DesktopPathOpener(RecordAssociation));
        var previousConsole = AnsiConsole.Console;
        using var consoleText = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(consoleText)
        });
        string? selected = null;
        ImageExportResult? exported = null;
        BrowserMediaGenerateResult? browser = null;
        var stages = 0;
        Exception? failure;
        try
        {
            failure = await Record.ExceptionAsync(async () =>
            {
                if (mode.StartsWith("lookup_", StringComparison.Ordinal)) selected = service.GetEntityImagePath("npc", "actor");
                else if (mode.StartsWith("export_", StringComparison.Ordinal)) exported = service.ExportEntityImage("npc", "actor", exportPath);
                else if (mode == "browser_existing")
                {
                    settings.ImageProvider = "test-provider";
                    var browserService = new BrowserMediaGenerationService(service, new LocalMediaService(files), settings, files,
                        _ => { stages++; throw new InvalidOperationException("Existing image must not stage provider bytes."); });
                    browser = await browserService.GenerateAsync(new("ignored prompt", "npc", "actor"));
                }
                else await service.ProcessSceneImagePrompt(mode == "canonical_prompt_unknown" ? imagePath : mode == "external_prompt" ? outsideImage : "scene prompt");
            });
        }
        finally { AnsiConsole.Console = previousConsole; }
        var journalAfter = CleanupPublicationCut.ReadOptional(seedCut.JournalPath);
        var weatherAfter = CleanupPublicationCut.ReadOptional(weatherPath);
        var exportedBytes = CleanupPublicationCut.ReadOptional(exportPath);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            mode, root, relative, imagePath, outputImage, outsideImage, exportPath, imageBytes,
            failure = failure?.ToString(), seedFailure = seedFailure?.ToString(), selected, exported, exportedBytes, browser, stages,
            admissions, ordinaryAdmissions, closingAdmissions, closing, sourceReads, recoveryBeforeSourceRead, recovered, requests, admissionsAtAssociation,
            recoveredAtAssociation, releasedBeforeAssociation, debtCuts, journalBefore, journalAfter, weatherBefore, weatherAfter,
            originalGeneration, generationAfter = File.ReadAllBytes(files.SessionGenerationPath),
            imageAfter = File.ReadAllBytes(imagePath), outsideAfter = File.ReadAllBytes(outsideImage),
            linkTarget = mode == "lookup_link" ? new FileInfo(imagePath).LinkTarget : mode == "scene_output_link" ? new DirectoryInfo(outputDirectory).LinkTarget : null,
            ignoredImageLink = File.Exists(ignoredImage) ? new FileInfo(ignoredImage).LinkTarget : null,
            ignoredOutputLink = File.Exists(ignoredOutput) ? new FileInfo(ignoredOutput).LinkTarget : null,
            console = consoleText.ToString(), SeedCut = seedCut.Evidence()
        }));
        Assert.Equal(originalGeneration, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Equal(imageBytes, File.ReadAllBytes(imagePath)); Assert.Equal(imageBytes, File.ReadAllBytes(outsideImage));
        Assert.False(closing);
        if (IsDebtMode(mode) || mode == "browser_existing")
        {
            Assert.Equal(outsideImage, new FileInfo(ignoredImage).LinkTarget);
            Assert.Equal(outsideImage, new FileInfo(ignoredOutput).LinkTarget);
        }
        if (unknown && mode != "external_prompt")
        {
            Assert.IsType<InvalidDataException>(failure); Assert.Equal(1, admissions);
            Assert.Empty(sourceReads); Assert.Empty(recovered); Assert.Empty(requests); Assert.Null(exportedBytes);
            Assert.Equal(journalBefore, journalAfter); Assert.Equal(weatherBefore, weatherAfter);
            Assert.Equal(CleanupPublicationCut.Foreign, weatherAfter);
        }
        else if (mode.EndsWith("_link", StringComparison.Ordinal))
        {
            Assert.IsType<InvalidDataException>(failure); Assert.Equal(1, admissions); Assert.Empty(sourceReads); Assert.Empty(requests);
            Assert.Equal(mode == "lookup_link" ? outsideImage : outside,
                mode == "lookup_link" ? new FileInfo(imagePath).LinkTarget : new DirectoryInfo(outputDirectory).LinkTarget);
            Assert.Single(Directory.GetFiles(outside));
        }
        else
        {
            Assert.Null(failure);
            if (mode == "external_prompt")
            {
                Assert.Equal(outsideImage, Assert.Single(requests)); Assert.Equal(0, admissions); Assert.Empty(sourceReads);
                Assert.Empty(recovered); Assert.Equal(journalBefore, journalAfter); Assert.Equal(weatherBefore, weatherAfter);
            }
            else if (mode == "browser_existing")
            {
                Assert.True(browser!.Success, browser.ErrorMessage); Assert.NotNull(browser.MediaId); Assert.StartsWith("/api/media/", browser.Url);
                Assert.Equal(0, stages); Assert.Equal(3, admissions); Assert.Equal(2, ordinaryAdmissions);
                Assert.Equal(1, closingAdmissions); Assert.Empty(requests);
            }
            else
            {
                Assert.Null(journalAfter); Assert.Equal(published, weatherAfter); Assert.NotEmpty(recovered);
                if (mode == "lookup_debt") { Assert.Equal(imagePath, selected); Assert.Equal(1, admissions); }
                else if (mode == "export_debt")
                {
                    Assert.True(exported!.Success, exported.ErrorMessage); Assert.Equal(imagePath, exported.SourcePath);
                    Assert.Equal(imageBytes, exportedBytes); Assert.Equal(new[] { relative }, sourceReads);
                    Assert.True(Assert.Single(recoveryBeforeSourceRead)); Assert.Equal(1, admissions);
                }
                else
                {
                    Assert.Equal(outputImage, Assert.Single(requests)); Assert.True(recoveredAtAssociation);
                    Assert.True(releasedBeforeAssociation); Assert.Equal(1, admissionsAtAssociation); Assert.Equal(2, admissions);
                }
            }
        }
    }

    private static bool IsDebtMode(string mode) => mode.EndsWith("_debt", StringComparison.Ordinal);
}
