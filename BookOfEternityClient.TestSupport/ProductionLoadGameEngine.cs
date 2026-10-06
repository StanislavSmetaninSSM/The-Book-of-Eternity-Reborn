using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
namespace BookOfEternityClient.Tests;
internal static class ProductionLoadGameEngine
{
    internal static GameEngine Create(FileSystemManager fs,GameSettings settings,SaveLoadService? suppliedSave=null)
    {
        IConsoleInputSource inputSource=SystemConsoleInputSource.Instance;
        var stateManager = new StateManager(fs, settings, NullLogger<StateManager>.Instance);
        var localization = new LocalizationManager { CurrentLanguage = "ru" };
        var gameLoop = new GameLoop();
        var normalizer = new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var progressionSchedule = new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance);
        var gameInterface = new GameInterface(localization, settings);
        var clipboardService = new TestClipboardService();
        var explorer = new ExplorerMode(stateManager, fs, localization, clipboardService: clipboardService, console: new TestExplorerConsole());
        var saveLoad = suppliedSave ?? new SaveLoadService(fs, stateManager, NullLogger<SaveLoadService>.Instance);
        var imageService = new ImageService(fs, settings, localization, NullLogger<ImageService>.Instance);
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        var characteristicsService = new CharacteristicsService(fs, stateManager, NullLogger<CharacteristicsService>.Instance);
        var storyService = new StoryService(fs, NullLogger<StoryService>.Instance);
        var actorMemoryService = new ActorMemoryService(fs, NullLogger<ActorMemoryService>.Instance);
        var audioService = new AudioService(fs, settings, NullLogger<AudioService>.Instance);
        var consoleAppearance = new ConsoleAppearanceService(settings, NullLogger<ConsoleAppearanceService>.Instance);
        var systemModService = new SystemModService(fs, settings, NullLogger<SystemModService>.Instance);
        var systemGuardianLibraryService = new SystemGuardianLibraryService(fs, NullLogger<SystemGuardianLibraryService>.Instance);
        var criticalStateHealth = new CriticalStateHealthService(fs, NullLogger<CriticalStateHealthService>.Instance);
        var worldDirectiveService = new WorldDirectiveService(fs, NullLogger<WorldDirectiveService>.Instance);
        var scenarioCoreService = new ScenarioCoreService(fs, NullLogger<ScenarioCoreService>.Instance);
        var afterlifeArchiveCandidateService = new AfterlifeArchiveCandidateService(fs, NullLogger<AfterlifeArchiveCandidateService>.Instance);
        var afterlifeReturnGuardService = new AfterlifeReturnGuardService(fs, NullLogger<AfterlifeReturnGuardService>.Instance);
        var rivalSoulArcService = new RivalSoulArcService(fs, NullLogger<RivalSoulArcService>.Instance);
        var guardianCorrectionService = new GuardianCorrectionService(fs, scenarioCoreService, NullLogger<GuardianCorrectionService>.Instance);
        var pendingTurnState = new PendingTurnStateService(fs, NullLogger<PendingTurnStateService>.Instance);
        var stateDistributor = new StateDistributor(fs, NullLogger<StateDistributor>.Instance);
        var qteSceneService = new QteSceneService(
            fs,
            settings,
            characteristicsService,
            imageService,
            audioService,
            stateDistributor,
            validator,
            normalizer,
            stateManager,
            NullLogger<QteSceneService>.Instance);

        var engine = new GameEngine(
            fs,
            stateManager,
            gameLoop,
            normalizer,
            progressionSchedule,
            gameInterface,
            explorer,
            localization,
            saveLoad,
            imageService,
            validator,
            characteristicsService,
            storyService,
            actorMemoryService,
            audioService,
            consoleAppearance,
            systemModService,
            systemGuardianLibraryService,
            criticalStateHealth,
            worldDirectiveService,
            scenarioCoreService,
            afterlifeArchiveCandidateService,
            afterlifeReturnGuardService,
            rivalSoulArcService,
            guardianCorrectionService,
            pendingTurnState,
            qteSceneService,
            clipboardService,
            NullLogger<GameEngine>.Instance,
            inputSource);
        return engine;
    }
}
