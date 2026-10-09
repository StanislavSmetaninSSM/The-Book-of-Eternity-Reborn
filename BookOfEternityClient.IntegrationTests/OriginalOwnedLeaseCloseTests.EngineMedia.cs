using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("engine_generation", false)] [InlineData("engine_generation", true)]
    [InlineData("browser_generation", false)] [InlineData("browser_generation", true)]
    [InlineData("browser_image_commit", false)] [InlineData("browser_image_commit", true)]
    public async Task OriginalEngineAndBrowserMediaPublishersRetainActualUncertaintyOnClose(string mode, bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-engine-media-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commitAcquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCommitAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false; var acquisitionPauses = 0; var publicationPauses = 0; var commitAcquisitionArmed = false; var commitAcquisitionPauses = 0;
        var laterInitialOwnerClosed = 0; FileSystemManager.CanonicalWriteLease? original = null;
        string? target = null;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (armed && phase == TrustedLocalPublicationPhase.IntentPublished && publicationPauses == 0)
                    {
                        using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                        if (journal.RootElement.GetProperty("Members").EnumerateArray().Any(m => m.GetProperty("Path").GetString() == target))
                        { publicationPauses++; entered.TrySetResult(); allow.Task.GetAwaiter().GetResult(); }
                    }
                    cut.Hooks.LocalPublicationObserver!(phase, index);
                },
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                    if (armed && acquisitionPauses == 0)
                    { acquisitionPauses++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
                    else if (armed && commitAcquisitionArmed && commitAcquisitionPauses == 0)
                    { commitAcquisitionPauses++; commitAcquireEntered.TrySetResult(); await allowCommitAcquire.Task; }
                    else if (armed && mode == "browser_generation" && original != null)
                    { Assert.False(original.IsActive); laterInitialOwnerClosed++; }
                },
                BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
                AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
                SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync
            });
        cut.Attach(files); files.EnsureDirectoryStructure();
        if (mode == "browser_image_commit")
            await using (var seed = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seed);
        var imageBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9Zl1sAAAAASUVORK5CYII=");
        const string imageRelative = "images/npcs/actor.png";
        var imagePath = files.ResolvePath(imageRelative);
        if (mode == "browser_generation") { Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!); File.WriteAllBytes(imagePath, imageBytes); }
        var settings = new GameSettings { ImageProvider = "test-provider", MusicEnabled = false, SoundEnabled = false, GmBridgeAutoStart = false };
        var images = new ImageService(files, settings, new LocalizationManager(), NullLogger<ImageService>.Instance,
            new DesktopPathOpener(_ => throw new InvalidOperationException("This fixture cannot request desktop association.")));
        var media = new LocalMediaService(files); var stages = 0;
        var browser = new BrowserMediaGenerationService(images, media, settings, files, _ =>
        {
            stages++;
            Assert.Equal("browser_image_commit", mode); commitAcquisitionArmed = true;
            return Task.FromResult<StagedEntityImage?>(new(imageBytes, imageRelative));
        });
        (GameEngine Engine, AudioService Audio)? engine = mode == "engine_generation" ? CreateOriginalCloseEngine(files, settings) : null;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        if (mode != "browser_image_commit") Assert.Null(generationBefore);
        target = mode == "browser_image_commit" ? imagePath : files.SessionGenerationPath;
        cut.Select = (path, _) => path == target;
        var boundary = mode switch
        {
            "engine_generation" => "GameEngine+<CaptureCurrentSessionGenerationAsync>",
            "browser_generation" => "BrowserMediaGenerationService+<GenerateAsync>",
            _ => "BrowserMediaGenerationService+<CommitStagedImageAsync>"
        };
        var closeFailure = new IOException("Actual engine/media original owner late close.");
        var closer = new ThrowingClose(closeFailure); var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        Task? operation = null; Exception? failure = null; object? result = null; string? ownerState = null;
        try
        {
            armed = true;
            operation = mode == "engine_generation"
                ? (Task<string>)typeof(GameEngine).GetMethod("CaptureCurrentSessionGenerationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine!.Value.Engine, null)!
                : browser.GenerateAsync(new("owned local fixture", "npc", "actor"));
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            if (mode == "browser_image_commit")
            {
                Assert.Same(commitAcquireEntered.Task, await Task.WhenAny(commitAcquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
                allowCommitAcquire.TrySetResult();
            }
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, ownerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null) result = operation.GetType().GetProperty("Result")!.GetValue(operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allowCommitAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
            if (engine != null) await engine.Value.Audio.DisposeAsync();
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var generateGate = (SemaphoreSlim)typeof(BrowserMediaGenerationService).GetField("_generateGate", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        var gateAvailable = await generateGate.WaitAsync(0); if (gateAvailable) generateGate.Release();
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        var imageAfter = CleanupPublicationCut.ReadOptional(imagePath);
        output.WriteLine(JsonSerializer.Serialize(new { mode, uncertain, root, boundary, ownerState, acquisitionPauses, publicationPauses,
            stages, commitAcquisitionPauses, laterInitialOwnerClosed, result, attachments, closer.Calls, failure = failure?.ToString(),
            samePrimary = ReferenceEquals(failure, cut.OriginalUncertainty), sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure),
            lockAvailable, gateAvailable, generationBefore, generationAfter, imageBytes, imageAfter, activeAfter = original!.IsActive,
            ambientClosed = original.AmbientRegistration == null, mainClosed = original.MainAdmission == null,
            contextClosed = original.ExternalPublicationContext == null, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable); Assert.True(gateAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));
        if (mode == "browser_image_commit") { Assert.Equal(1, stages); Assert.Equal(1, commitAcquisitionPauses); Assert.Equal(generationBefore, generationAfter); }
        else { Assert.Equal(0, stages); Assert.Equal(0, commitAcquisitionPauses); }
        if (mode == "browser_generation") Assert.Equal(imageBytes, imageAfter);
        if (uncertain)
        {
            Assert.Null(result); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls);
            Assert.Same(cut.OriginalUncertainty, failure); Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
            cut.AssertReachedAndStopped(); Assert.Equal(mode == "browser_image_commit" ? 1 : 0, cut.ClosingLeases); Assert.Equal(0, laterInitialOwnerClosed);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            using var generation = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(generationAfter!));
            var actualGeneration = generation.RootElement.GetProperty("GenerationId").GetString(); Assert.False(string.IsNullOrWhiteSpace(actualGeneration));
            if (mode == "engine_generation") Assert.Equal(actualGeneration, Assert.IsType<string>(result));
            else
            {
                var actual = Assert.IsType<BrowserMediaGenerateResult>(result); Assert.True(actual.Success); Assert.Null(actual.ErrorMessage);
                var reference = media.TryCreateReference(imagePath); Assert.NotNull(reference);
                Assert.Equal(reference.MediaId, actual.MediaId); Assert.Equal(reference.Url, actual.Url); Assert.Equal(imageBytes, imageAfter);
                if (mode == "browser_generation") Assert.True(laterInitialOwnerClosed > 0);
            }
        }
    }

    private static (GameEngine Engine, AudioService Audio) CreateOriginalCloseEngine(FileSystemManager files, GameSettings settings)
    {
        var manager = new StateManager(files, settings, NullLogger<StateManager>.Instance);
        var localization = new LocalizationManager();
        var normalizer = new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance);
        var progression = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        var images = new ImageService(files, settings, localization, NullLogger<ImageService>.Instance,
            new DesktopPathOpener(_ => throw new InvalidOperationException("No desktop association in original owner fixture.")));
        var audio = new AudioService(files, settings, NullLogger<AudioService>.Instance);
        var validation = new ValidationService(files, NullLogger<ValidationService>.Instance);
        var characteristics = new CharacteristicsService(files, manager, NullLogger<CharacteristicsService>.Instance);
        var distributor = new StateDistributor(files, NullLogger<StateDistributor>.Instance);
        var scenario = new ScenarioCoreService(files, NullLogger<ScenarioCoreService>.Instance);
        var engine = new GameEngine(files, manager, new GameLoop(), normalizer, progression,
            new GameInterface(localization, settings), new ExplorerMode(manager, files, localization), localization,
            new SaveLoadService(files, manager, NullLogger<SaveLoadService>.Instance), images, validation, characteristics,
            new StoryService(files, NullLogger<StoryService>.Instance), new ActorMemoryService(files, NullLogger<ActorMemoryService>.Instance),
            audio, new ConsoleAppearanceService(settings, NullLogger<ConsoleAppearanceService>.Instance),
            new SystemModService(files, settings, NullLogger<SystemModService>.Instance),
            new SystemGuardianLibraryService(files, NullLogger<SystemGuardianLibraryService>.Instance),
            new CriticalStateHealthService(files, NullLogger<CriticalStateHealthService>.Instance),
            new WorldDirectiveService(files, NullLogger<WorldDirectiveService>.Instance), scenario,
            new AfterlifeArchiveCandidateService(files, NullLogger<AfterlifeArchiveCandidateService>.Instance),
            new AfterlifeReturnGuardService(files, NullLogger<AfterlifeReturnGuardService>.Instance),
            new RivalSoulArcService(files, NullLogger<RivalSoulArcService>.Instance),
            new GuardianCorrectionService(files, scenario, NullLogger<GuardianCorrectionService>.Instance),
            new PendingTurnStateService(files, NullLogger<PendingTurnStateService>.Instance),
            new QteSceneService(files, settings, characteristics, images, audio, distributor, validation, normalizer, manager, NullLogger<QteSceneService>.Instance),
            null!, NullLogger<GameEngine>.Instance,
            desktopPathOpener: new DesktopPathOpener(_ => throw new InvalidOperationException("No desktop association in original owner fixture.")));
        return (engine, audio);
    }
}
