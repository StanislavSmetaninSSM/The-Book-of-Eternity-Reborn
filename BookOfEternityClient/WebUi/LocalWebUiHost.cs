using System.Net;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.WebUi;

public sealed record LocalWebUiHostOptions(string BasePath, string Url);

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
        builder.Services.AddSingleton<LocalWebUiSessionStatusService>();
        builder.Services.AddSingleton<ExplorerWebCommandService>();

        var app = builder.Build();
        app.Services.GetRequiredService<FileSystemManager>().EnsureDirectoryStructure();

        app.MapGet("/", () => Results.Content(BuildShellHtml(), "text/html; charset=utf-8"));
        app.MapGet("/api/health", (LocalWebUiSessionStatusService status) => status.BuildStatus());
        app.MapGet("/api/session", (LocalWebUiSessionStatusService status) => status.BuildStatus());
        app.MapPost("/api/explorer/command", async (ExplorerWebCommandRequest request, ExplorerWebCommandService commandService) =>
            await commandService.ExecuteAsync(request));

        return app;
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

    private static string BuildShellHtml() =>
        """
        <!doctype html>
        <html lang="ru">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>The Book of Eternity: Reborn - Local Web UI</title>
          <style>
            :root {
              color-scheme: dark;
              --bg: #101418;
              --panel: #182129;
              --text: #edf2e8;
              --muted: #a7b0a6;
              --accent: #d9b45f;
            }
            body {
              margin: 0;
              min-height: 100vh;
              display: grid;
              place-items: center;
              background:
                radial-gradient(circle at 20% 10%, rgba(217, 180, 95, .18), transparent 26rem),
                linear-gradient(135deg, #101418, #1c2425 55%, #151515);
              color: var(--text);
              font: 16px/1.5 Georgia, "Times New Roman", serif;
            }
            main {
              width: min(46rem, calc(100vw - 2rem));
              padding: 2rem;
              border: 1px solid rgba(217, 180, 95, .35);
              border-radius: 1.25rem;
              background: color-mix(in srgb, var(--panel) 92%, transparent);
              box-shadow: 0 2rem 5rem rgba(0, 0, 0, .35);
            }
            h1 { margin: 0 0 .75rem; color: var(--accent); font-size: clamp(2rem, 6vw, 4rem); line-height: .95; }
            p { color: var(--muted); max-width: 38rem; }
            code { color: var(--accent); }
          </style>
        </head>
        <body>
          <main>
            <h1>The Book of Eternity</h1>
            <p>Локальная браузерная оболочка запущена. Сейчас это безопасный каркас: он читает текущую папку сессии и отдаёт служебный статус, не заменяя консольный режим.</p>
            <p>Проверка состояния доступна по <code>/api/health</code>. Командный API и полноценный интерфейс будут подключены следующими задачами.</p>
          </main>
        </body>
        </html>
        """;
}
