using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;

// ═══════════════════════════════════════════════════
// 📖 The Book of Eternity: Reborn - C# Client
// ═══════════════════════════════════════════════════

Console.OutputEncoding = System.Text.Encoding.UTF8;
// On Windows, use Unicode (UTF-16) for input to force ReadConsoleW (native Unicode API).
// UTF-8 InputEncoding causes .NET to use ReadFile which corrupts non-ASCII characters.
if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
    Console.InputEncoding = System.Text.Encoding.Unicode;
else
    Console.InputEncoding = System.Text.Encoding.UTF8;

static string ResolveDefaultBasePath()
{
    var processDir = Path.GetDirectoryName(Environment.ProcessPath) ?? Environment.CurrentDirectory;

    // Development/default layout:
    // BookOfEternityClient\bin\Debug\net8.0\BookOfEternityClient.exe
    // We want BookOfEternityClient\game_session, not bin\...\game_session.
    var dir = new DirectoryInfo(processDir);
    while (dir != null)
    {
        var csprojPath = Path.Combine(dir.FullName, "BookOfEternityClient.csproj");
        if (File.Exists(csprojPath))
            return dir.FullName;

        dir = dir.Parent;
    }

    return processDir;
}

// Determine base path: prefer the project root containing BookOfEternityClient.csproj.
var startupOptions = ClientStartupOptions.Parse(args, ResolveDefaultBasePath());
var basePath = startupOptions.BasePath;

if (startupOptions.WebMode)
{
    await using var webApp = LocalWebUiHost.Build(
        args,
        new LocalWebUiHostOptions(basePath, startupOptions.WebUrl));
    await webApp.RunAsync();
    return;
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddDebug();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices((context, services) =>
    {
        // Configuration
        services.AddSingleton(new GameSettings());

        // Core
        services.AddSingleton(sp =>
            new FileSystemManager(basePath, sp.GetRequiredService<ILogger<FileSystemManager>>()));
        services.AddSingleton(sp =>
            new StateManager(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<GameSettings>(),
                sp.GetRequiredService<ILogger<StateManager>>()));
        services.AddSingleton<GameLoop>();

        // UI
        services.AddSingleton<LocalizationManager>();
        services.AddSingleton(sp =>
            new GameInterface(
                sp.GetRequiredService<LocalizationManager>(),
                sp.GetRequiredService<GameSettings>()));

        // Services
        services.AddSingleton(sp =>
            new SaveLoadService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<StateManager>(),
                sp.GetRequiredService<ILogger<SaveLoadService>>()));
        services.AddSingleton(sp =>
            new ImageService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<GameSettings>(),
                sp.GetRequiredService<LocalizationManager>(),
                sp.GetRequiredService<ILogger<ImageService>>()));
        services.AddSingleton(sp =>
            new StateDistributor(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<StateDistributor>>()));
        services.AddSingleton(sp =>
            new ValidationService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<ValidationService>>()));
        services.AddSingleton(sp =>
            new CriticalStateHealthService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<CriticalStateHealthService>>()));
        services.AddSingleton(sp =>
            new CharacteristicsService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<StateManager>(),
                sp.GetRequiredService<ILogger<CharacteristicsService>>()));
        services.AddSingleton(sp =>
            new StoryService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<StoryService>>()));
        services.AddSingleton(sp =>
            new ActorMemoryService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<ActorMemoryService>>()));
        services.AddSingleton(sp =>
            new AudioService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<GameSettings>(),
                sp.GetRequiredService<ILogger<AudioService>>()));
        services.AddSingleton(sp =>
            new ConsoleAppearanceService(
                sp.GetRequiredService<GameSettings>(),
                sp.GetRequiredService<ILogger<ConsoleAppearanceService>>()));
        services.AddSingleton(sp =>
            new SystemModService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<GameSettings>(),
                sp.GetRequiredService<ILogger<SystemModService>>()));
        services.AddSingleton(sp =>
            new WorldDirectiveService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<WorldDirectiveService>>()));
        services.AddSingleton(sp =>
            new ScenarioCoreService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<ScenarioCoreService>>()));
        services.AddSingleton(sp =>
            new AfterlifeArchiveCandidateService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<AfterlifeArchiveCandidateService>>()));
        services.AddSingleton(sp =>
            new AfterlifeArchiveConsultationService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<AfterlifeArchiveConsultationService>>()));
        services.AddSingleton(sp =>
            new AfterlifeArchiveProjectFuelService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<AfterlifeArchiveProjectFuelService>>()));
        services.AddSingleton(sp =>
            new SystemGuardianLibraryService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<SystemGuardianLibraryService>>()));
        services.AddSingleton(sp =>
            new AfterlifeReturnGuardService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<AfterlifeReturnGuardService>>()));
        services.AddSingleton(sp =>
            new RivalSoulArcService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<RivalSoulArcService>>()));
        services.AddSingleton(sp =>
            new GuardianCorrectionService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ScenarioCoreService>(),
                sp.GetRequiredService<ILogger<GuardianCorrectionService>>()));
        services.AddSingleton(sp =>
            new SoulIdentityService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<SoulIdentityService>>()));
        services.AddSingleton<IClipboardService, SystemClipboardService>();
        services.AddSingleton(sp =>
            new CanonicalStateNormalizer(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<CanonicalStateNormalizer>>()));
        services.AddSingleton(sp =>
            new ProgressionScheduleService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<ProgressionScheduleService>>()));
        services.AddSingleton(sp =>
            new PendingTurnStateService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<PendingTurnStateService>>()));
        services.AddSingleton(sp =>
            new GuardianTradeService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<GuardianTradeService>>()));
        services.AddSingleton(sp =>
            new NpcTradeService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<ILogger<NpcTradeService>>()));
        services.AddSingleton(sp =>
            new QteSceneService(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<GameSettings>(),
                sp.GetRequiredService<CharacteristicsService>(),
                sp.GetRequiredService<ImageService>(),
                sp.GetRequiredService<AudioService>(),
                sp.GetRequiredService<StateDistributor>(),
                sp.GetRequiredService<ValidationService>(),
                sp.GetRequiredService<CanonicalStateNormalizer>(),
                sp.GetRequiredService<StateManager>(),
                sp.GetRequiredService<ILogger<QteSceneService>>()));
        services.AddSingleton(sp =>
            new ExplorerMode(
                sp.GetRequiredService<StateManager>(),
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<LocalizationManager>(),
                sp.GetRequiredService<ValidationService>(),
                sp.GetRequiredService<CharacteristicsService>(),
                sp.GetRequiredService<StoryService>(),
                sp.GetRequiredService<ImageService>(),
                sp.GetRequiredService<PendingTurnStateService>(),
                sp.GetRequiredService<GuardianTradeService>(),
                sp.GetRequiredService<NpcTradeService>(),
                sp.GetRequiredService<SystemModService>(),
                sp.GetRequiredService<SystemGuardianLibraryService>(),
                sp.GetRequiredService<WorldDirectiveService>(),
                sp.GetRequiredService<ScenarioCoreService>(),
                sp.GetRequiredService<AfterlifeArchiveCandidateService>(),
                sp.GetRequiredService<AfterlifeArchiveConsultationService>(),
                sp.GetRequiredService<AfterlifeArchiveProjectFuelService>(),
                sp.GetRequiredService<GuardianCorrectionService>(),
                sp.GetRequiredService<SoulIdentityService>(),
                sp.GetRequiredService<IClipboardService>()));

        // Engine
        services.AddSingleton<GameEngine>();
    })
    .Build();

// Run the game
var engine = host.Services.GetRequiredService<GameEngine>();
await engine.RunAsync();
