using System.Net;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace BookOfEternityClient.WebUi;

public sealed record LocalWebUiHostOptions(string BasePath, string Url, string? FrontendAssetsPath = null);

public static class LocalWebUiHost
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static WebApplication Build(string[] args, LocalWebUiHostOptions options)
    {
        if (!IsLocalUrl(options.Url))
            throw new InvalidOperationException("Local Web UI can only bind to localhost/loopback URLs.");

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = options.BasePath
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.WebHost.UseUrls(options.Url);

        builder.Services.Configure<JsonOptions>(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = WebJsonOptions.PropertyNamingPolicy;
            json.SerializerOptions.WriteIndented = WebJsonOptions.WriteIndented;
        });

        builder.Services.AddSingleton(sp =>
            new FileSystemManager(options.BasePath, sp.GetRequiredService<ILogger<FileSystemManager>>()));
        builder.Services.AddSingleton(new GameSettings());
        builder.Services.AddSingleton(sp =>
            new StateManager(
                sp.GetRequiredService<FileSystemManager>(),
                sp.GetRequiredService<GameSettings>(),
                sp.GetRequiredService<ILogger<StateManager>>()));
        builder.Services.AddSingleton<LocalizationManager>();
        builder.Services.AddSingleton<ValidationService>();
        builder.Services.AddSingleton<CharacteristicsService>();
        builder.Services.AddSingleton<ImageService>();
        builder.Services.AddSingleton<BrowserMediaGenerationService>();
        builder.Services.AddSingleton<LocalMediaService>();
        builder.Services.AddSingleton<AudioService>();
        builder.Services.AddSingleton<BrowserAudioService>();
        builder.Services.AddSingleton<BrowserClientSettingsService>();
        builder.Services.AddSingleton<SaveLoadService>();
        builder.Services.AddSingleton<StateDistributor>();
        builder.Services.AddSingleton<CanonicalStateNormalizer>();
        builder.Services.AddSingleton<ScenarioCoreService>();
        builder.Services.AddSingleton<QteSceneService>();
        builder.Services.AddSingleton<QteWebInteractionService>();
        builder.Services.AddSingleton<LocalUiSessionLockService>();
        builder.Services.AddSingleton<BrowserLocalWriteCoordinator>();
        builder.Services.AddSingleton<BrowserMortalWorldWriteService>();
        builder.Services.AddSingleton<LocalWebUiSessionStatusService>();
        builder.Services.AddSingleton<BrowserGameScreenService>();
        builder.Services.AddSingleton<BrowserLifecycleDashboardService>();
        builder.Services.AddSingleton<LocalWebUiMainMenuService>();
        builder.Services.AddSingleton<ExplorerWebPromptSessionService>();
        builder.Services.AddSingleton<ExplorerWebCommandService>();
        builder.Services.AddSingleton<BrowserPlayerActionService>();

        var app = builder.Build();
        var frontendAssets = LocalWebUiFrontendAssets.Resolve(options.FrontendAssetsPath);
        app.Services.GetRequiredService<FileSystemManager>().EnsureDirectoryStructure();

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(frontendAssets.RootPath),
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers[HeaderNames.CacheControl] = "no-store";
            }
        });

        app.MapGet("/", () => ServeFrontendIndex(frontendAssets));
        app.MapGet("/api/main-menu", async (LocalWebUiMainMenuService menu) => await menu.BuildAsync());
        app.MapPost("/api/saves/load", async (BrowserLoadSaveRequest request, LocalWebUiMainMenuService menu) =>
        {
            var result = await menu.LoadSaveAsync(request);
            return result.Success
                ? Results.Json(result, WebJsonOptions)
                : Results.BadRequest(new { result.Error, result.LoadedSaveId, result.Menu });
        });
        app.MapGet("/assets/map-viewer.css", () => Results.Content(LocalMapViewerAssets.StyleSheet, "text/css; charset=utf-8"));
        app.MapGet("/assets/map-viewer.js", () => Results.Content(LocalMapViewerAssets.Script, "application/javascript; charset=utf-8"));
        app.MapGet("/api/health", async (LocalWebUiSessionStatusService status) => await status.BuildStatusAsync());
        app.MapGet("/api/session", async (LocalWebUiSessionStatusService status) => await status.BuildStatusAsync());
        app.MapGet("/api/game-screen", async (BrowserGameScreenService gameScreen) =>
        {
            try
            {
                return Results.Json(await gameScreen.BuildAsync(), WebJsonOptions);
            }
            catch (BrowserNoActiveSessionException ex)
            {
                return Results.Json(new { error = ex.Message }, WebJsonOptions, statusCode: StatusCodes.Status404NotFound);
            }
        });
        app.MapGet("/api/client/settings", async (BrowserClientSettingsService settings) => await settings.BuildAsync());
        app.MapPost("/api/client/settings", async (BrowserClientSettingsUpdateRequest request, BrowserClientSettingsService settings) =>
        {
            var result = await settings.UpdateAsync(request);
            return result.Success
                ? Results.Json(result.Settings, WebJsonOptions)
                : Results.Conflict(new { error = result.Message });
        });
        app.MapGet("/api/audio/settings", async (BrowserAudioService audio) => await audio.BuildSettingsAsync());
        app.MapPost("/api/audio/settings", async (BrowserAudioSettingsUpdateRequest request, BrowserAudioService audio) => await audio.UpdateSettingsAsync(request));
        app.MapGet("/api/audio/assets/{assetId}", (string assetId, BrowserAudioService audio) => audio.ServeAsset(assetId));
        app.MapGet("/api/lifecycle/dashboard", async (BrowserLifecycleDashboardService lifecycle) =>
            await lifecycle.BuildDashboardAsync());
        app.MapPost("/api/lifecycle/validate", async (BrowserLifecycleDashboardService lifecycle) =>
            await lifecycle.BuildValidationAsync());
        app.MapGet("/api/explorer/command-coverage", () => BrowserCommandCoverageService.Build());
        app.MapPost("/api/explorer/command", async (ExplorerWebCommandRequest request, ExplorerWebCommandService commandService) =>
            await commandService.ExecuteAsync(request));
        app.MapGet("/api/explorer/prompt-sessions/{sessionId}", (string sessionId, ExplorerWebCommandService commandService) =>
            commandService.GetPromptSession(sessionId));
        app.MapPost("/api/explorer/prompt-sessions/submit", async (ExplorerPromptSessionSubmitRequest request, ExplorerWebCommandService commandService) =>
            await commandService.SubmitPromptSessionAsync(request));
        app.MapPost("/api/explorer/prompt-sessions/cancel", async (ExplorerPromptSessionCancelRequest request, ExplorerWebCommandService commandService) =>
            await commandService.CancelPromptSessionAsync(request));
        app.MapPost("/api/explorer/player-action", async (BrowserPlayerActionRequest request, BrowserPlayerActionService playerAction) =>
            await playerAction.SubmitAsync(request));
        app.MapGet("/api/media/{mediaId}", (string mediaId, LocalMediaService media) =>
        {
            if (!media.TryResolveMediaId(mediaId, out var file, out var error) || file == null)
            {
                var statusCode = error.Contains("не найден", StringComparison.OrdinalIgnoreCase)
                    ? StatusCodes.Status404NotFound
                    : StatusCodes.Status400BadRequest;
                return Results.Json(new { error }, statusCode: statusCode);
            }

            return Results.File(
                file.FullPath,
                file.ContentType,
                fileDownloadName: null,
                enableRangeProcessing: true);
        });
        app.MapPost("/api/media/generate", async (BrowserMediaGenerateRequest request, BrowserMediaGenerationService gen) =>
            Results.Json(await gen.GenerateAsync(request), WebJsonOptions));
        app.MapGet("/api/qte/state", async (QteWebInteractionService qte) =>
            await qte.BuildStateAsync());
        app.MapPost("/api/qte/offer", async (QteWebOfferDecisionRequest request, QteWebInteractionService qte) =>
            await qte.ResolveOfferDecisionAsync(request));
        app.MapPost("/api/qte/action", async (QteWebActionRequest request, QteWebInteractionService qte) =>
            await qte.ResolveActionAsync(request));
        app.MapGet("/api/qte/practice", async (QteWebInteractionService qte) =>
            await qte.BuildPracticeStateAsync());
        app.MapPost("/api/qte/practice/start", async (QtePracticeStartRequest request, QteWebInteractionService qte) =>
            await qte.StartPracticeAttemptAsync(request));
        app.MapPost("/api/qte/practice/action", async (QtePracticeActionRequest request, QteWebInteractionService qte) =>
            await qte.ResolvePracticeActionAsync(request));
        app.MapPost("/api/qte/practice/retry", async (QteWebInteractionService qte) =>
            await qte.RetryPracticeAttemptAsync());
        app.MapPost("/api/qte/practice/exit", async (QteWebInteractionService qte) =>
            await qte.ExitPracticeAttemptAsync());

        app.MapFallback((HttpContext context) =>
            IsFrontendFallbackRequest(context.Request)
                ? ServeFrontendIndex(frontendAssets)
                : Results.NotFound());

        return app;
    }

    private static IResult ServeFrontendIndex(LocalWebUiFrontendAssets frontendAssets) =>
        Results.File(frontendAssets.IndexPath, "text/html; charset=utf-8");

    private static bool IsFrontendFallbackRequest(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
            return false;

        var path = request.Path;
        return !path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
               !path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        return IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }
}
