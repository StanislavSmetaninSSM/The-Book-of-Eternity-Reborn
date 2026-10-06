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

    public static WebApplication Build(string[] args, LocalWebUiHostOptions options) => Build(args, options, hooks: null);

    /// <summary>Builds the same host with controlled filesystem boundaries for isolated contract tests.</summary>
    internal static WebApplication Build(string[] args, LocalWebUiHostOptions options, FileSystemManagerHooks? hooks)
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
            new FileSystemManager(options.BasePath, sp.GetRequiredService<ILogger<FileSystemManager>>(),
                PhysicalLoadTransactionOperations.Instance, hooks));
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
        builder.Services.AddSingleton<BrowserLoadStateService>();
        builder.Services.AddSingleton<ExplorerWebPromptSessionService>();
        builder.Services.AddSingleton<ExplorerWebCommandService>();
        builder.Services.AddSingleton<BrowserPlayerActionService>();

        var frontendAssets = LocalWebUiFrontendAssets.Resolve(options.FrontendAssetsPath);
        var app = builder.Build();
        try
        {
            app.Services.GetRequiredService<StateManager>().BootstrapLocalStorageAsync().GetAwaiter().GetResult();
        }
        catch
        {
            app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            throw;
        }

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
        app.MapPost("/api/saves/create", async (BrowserCreateSaveRequest request, LocalWebUiMainMenuService menu) =>
        {
            var result = await menu.CreateManualSaveAsync(request);
            return CreateSaveResponse(result);
        });
        app.MapPost("/api/saves/load", async (BrowserLoadSaveRequest request, LocalWebUiMainMenuService menu, BrowserLoadStateService state, HttpContext context) =>
        {
            var result = await menu.LoadSaveAsync(request, state.BuildAsync,context.RequestAborted);
            return LoadSaveResponse(result);
        });
        app.MapPost("/api/saves/load-complete", async (BrowserLoadCompletionRequest request,LocalWebUiMainMenuService menu)=>
            LoadSaveResponse(await menu.CompleteLoadAsync(request)));
        app.MapPost("/api/saves/load-cancel", async (BrowserLoadCompletionRequest request,LocalWebUiMainMenuService menu)=>
            LoadSaveResponse(await menu.CancelLoadAsync(request)));
        app.MapPost("/api/saves/load-state", async (BrowserLoadStateRequest request, BrowserLoadStateService state) =>
        {
            try { return Results.Json(await state.BuildAsync(request), WebJsonOptions); }
            catch (Exception)
            {
                return Results.Json(new { error = "Обновление текущего состояния книги не подтверждено. Продолжение остановлено." },
                    WebJsonOptions, statusCode: StatusCodes.Status409Conflict);
            }
        });
        // The unified map viewer is a single self-contained bundle (React + MapAtlas
        // + inlined CSS). It is the SAME renderer used by the standalone
        // map_viewer.html and the Vite React client, so all three surfaces stay
        // in lockstep. The local web UI shell calls window.BookOfEternityMap.mount.
        app.MapGet("/assets/map-viewer.js", () => Results.Content(LocalMapViewerAssets.Bundle, "application/javascript; charset=utf-8"));
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
        app.MapGet("/api/client/settings", async Task<IResult> (BrowserClientSettingsService settings) =>
        {
            try { return Results.Json(await settings.BuildAsync(), WebJsonOptions); }
            catch (InvalidDataException)
            {
                return Results.Conflict(new { error = "Не удалось безопасно прочитать настройки. Сохранённое состояние требует проверки.", persistenceStatus = "blocked" });
            }
        });
        app.MapPost("/api/client/settings", async (BrowserClientSettingsUpdateRequest request, BrowserClientSettingsService settings) =>
        {
            var result = await settings.UpdateAsync(request);
            return result.Success
                ? Results.Json(result.Settings, WebJsonOptions)
                : Results.Conflict(new { error = result.Message, persistenceStatus = result.Disposition.ToString().ToLowerInvariant() });
        });
        app.MapGet("/api/audio/settings", async Task<IResult> (BrowserAudioService audio) =>
        {
            try { return Results.Json(await audio.BuildSettingsAsync(), WebJsonOptions); }
            catch (InvalidDataException)
            {
                return Results.Conflict(new { error = "Не удалось безопасно прочитать настройки. Сохранённое состояние требует проверки.", persistenceStatus = "blocked" });
            }
        });
        app.MapPost("/api/audio/settings", async Task<IResult> (BrowserAudioSettingsUpdateRequest request, BrowserAudioService audio) =>
        {
            try { return Results.Json(await audio.UpdateSettingsAsync(request), WebJsonOptions); }
            catch (BrowserSettingsWriteException ex)
            {
                return Results.Conflict(new { error = ex.Message, persistenceStatus = ex.Disposition.ToString().ToLowerInvariant() });
            }
        });
        app.MapGet("/api/audio/assets/{assetId}", (string assetId, BrowserAudioService audio) => audio.ServeAsset(assetId));
        app.MapGet("/api/lifecycle/dashboard", async (BrowserLifecycleDashboardService lifecycle) =>
            await lifecycle.BuildDashboardAsync());
        app.MapPost("/api/lifecycle/validate", async (BrowserLifecycleDashboardService lifecycle) =>
            await lifecycle.BuildValidationAsync());
        app.MapGet("/api/explorer/command-coverage", () => BrowserCommandCoverageService.Build());
        app.MapPost("/api/explorer/command", async (ExplorerWebCommandRequest request, ExplorerWebCommandService commandService) =>
            await commandService.ExecuteAsync(request));
        app.MapGet("/api/explorer/prompt-sessions/{sessionId}", async (string sessionId, ExplorerWebCommandService commandService) =>
            await commandService.GetPromptSessionAsync(sessionId));
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
        app.MapPost("/api/qte/practice/retry", async (QteInteractionRequest request, QteWebInteractionService qte) =>
            await qte.RetryPracticeAttemptAsync(request.InteractionToken));
        app.MapPost("/api/qte/practice/exit", async (QteInteractionRequest request, QteWebInteractionService qte) =>
            await qte.ExitPracticeAttemptAsync(request.InteractionToken));
        app.MapGet("/api/qte/daren", async (QteWebInteractionService qte) =>
            await qte.BuildDarenShowcaseStateAsync());
        app.MapPost("/api/qte/daren/start", async (QteInteractionRequest request, QteWebInteractionService qte) =>
            await qte.StartDarenShowcaseAsync(request.InteractionToken));
        app.MapPost("/api/qte/daren/action", async (DarenShowcaseActionRequest request, QteWebInteractionService qte) =>
            await qte.ResolveDarenShowcaseActionAsync(request));
        app.MapPost("/api/qte/daren/retry", async (QteInteractionRequest request, QteWebInteractionService qte) =>
            await qte.RetryDarenShowcaseAsync(request.InteractionToken));
        app.MapPost("/api/qte/daren/exit", async (QteInteractionRequest request, QteWebInteractionService qte) =>
            await qte.ExitDarenShowcaseAsync(request.InteractionToken));

        app.MapFallback((HttpContext context) =>
            IsFrontendFallbackRequest(context.Request)
                ? ServeFrontendIndex(frontendAssets)
                : Results.NotFound());

        return app;
    }

    /// <summary>
    /// Serializes the complete save decision for both successful and rejected browser requests.
    /// </summary>
    /// <param name="result">
    /// The retained save outcome, including committed follow-up and unresolved uncertainty.
    /// </param>
    /// <returns>
    /// A JSON response preserving disposition, exact identity and continuation fields at every status.
    /// </returns>
    internal static IResult CreateSaveResponse(BrowserCreateSaveResultDto result) =>
        Results.Json(result, WebJsonOptions, statusCode: result.ContinuationBlocked
            ? StatusCodes.Status409Conflict
            : result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);

    /// <summary>Preserves the complete typed replacement result on successful and unsuccessful HTTP responses.</summary>
    internal static IResult LoadSaveResponse(BrowserLoadSaveResultDto result) =>
        Results.Json(result, WebJsonOptions, statusCode: result.ContinuationBlocked
            ? StatusCodes.Status409Conflict
            : result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);

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
