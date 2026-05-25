using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalWebUiBuiltFrontendSmokeTests : IDisposable
{
    private readonly string _rootPath;

    public LocalWebUiBuiltFrontendSmokeTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-built-web-ui-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    [Trait("Category", "BrowserWebUiBuiltFrontend")]
    [Trait("Category", "BrowserWebUiSmoke")]
    public async Task BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics()
    {
        var frontendDist = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "dist");
        var indexPath = Path.Combine(frontendDist, "index.html");
        Assert.True(
            File.Exists(indexPath),
            $"Missing built browser frontend at {indexPath}. Run `npm run verify --prefix BookOfEternityClient.WebFrontend` before the built-frontend smoke test.");

        WriteSessionFile("game_state/meta/soul_state.json", """
        {
          "soulName": "CI-душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 5,
          "inkFeathers": { "current": 3 }
        }
        """);
        WriteSessionFile("game_state/world/current_location.json", """
        {
          "name": "Проверочный тракт"
        }
        """);
        WriteSessionFile("output/narrative_response.json", """
        {
          "response": "Сборка браузера открывает локальную книгу без сети."
        }
        """);

        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(
            Array.Empty<string>(),
            new LocalWebUiHostOptions(_rootPath, url, frontendDist));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var root = await CaptureAsync(client, "/");
        var gameRoute = await CaptureAsync(client, "/game");
        var menuResponse = await CaptureAsync(client, "/api/main-menu");
        var sessionResponse = await CaptureAsync(client, "/api/session");
        var screenResponse = await CaptureAsync(client, "/api/game-screen");
        var missingApi = await CaptureAsync(client, "/api/not-real");
        var missingAsset = await CaptureAsync(client, "/assets/not-real.js");
        var assetPaths = ExtractAssetPaths(root.Body);
        var assetResponses = new List<SmokeResponse>();
        foreach (var assetPath in assetPaths)
            assetResponses.Add(await CaptureAsync(client, assetPath));

        var artifactRoot = PrepareArtifactDirectory();
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "root.html"), root.Body);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "game-route.html"), gameRoute.Body);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "main-menu.json"), menuResponse.Body);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "session.json"), sessionResponse.Body);
        await File.WriteAllTextAsync(Path.Combine(artifactRoot, "game-screen.json"), screenResponse.Body);
        await File.WriteAllTextAsync(
            Path.Combine(artifactRoot, "network.json"),
            JsonSerializer.Serialize(
                new
                {
                    baseUrl = url,
                    frontendDist,
                    requests = new[]
                    {
                        root.ToArtifact(),
                        gameRoute.ToArtifact(),
                        menuResponse.ToArtifact(),
                        sessionResponse.ToArtifact(),
                        screenResponse.ToArtifact(),
                        missingApi.ToArtifact(),
                        missingAsset.ToArtifact()
                    }.Concat(assetResponses.Select(response => response.ToArtifact()))
                },
                new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Equal(HttpStatusCode.OK, gameRoute.StatusCode);
        Assert.Equal(HttpStatusCode.OK, menuResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, screenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingApi.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missingAsset.StatusCode);

        Assert.Contains("<div id=\"root\"></div>", root.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/assets/", root.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The Book of Eternity: Reborn", root.Body, StringComparison.Ordinal);
        Assert.Equal(root.Body, gameRoute.Body);
        Assert.NotEmpty(assetPaths);
        Assert.All(assetResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Contains(assetResponses, response => response.Path.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
            response.ContentType?.Contains("javascript", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(assetResponses, response => response.Path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) &&
            response.ContentType?.Contains("text/css", StringComparison.OrdinalIgnoreCase) == true);

        var menu = JsonNode.Parse(menuResponse.Body)!.AsObject();
        var session = JsonNode.Parse(sessionResponse.Body)!.AsObject();
        var screen = JsonNode.Parse(screenResponse.Body)!.AsObject();
        var appSource = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "src", "App.tsx"));

        Assert.True(session["localOnly"]!.GetValue<bool>());
        Assert.Equal("CI-душа", menu["session"]!["soulName"]!.GetValue<string>());
        Assert.Equal("CI-душа", screen["soul"]!["name"]!.GetValue<string>());
        Assert.Equal("Проверочный тракт", screen["world"]!["location"]!.GetValue<string>());
        Assert.Contains("локальную книгу", screen["narrative"]!["text"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Главная", appSource, StringComparison.Ordinal);
        Assert.Contains("Игра", appSource, StringComparison.Ordinal);
        Assert.Contains("Расширенный режим", appSource, StringComparison.Ordinal);
        Assert.Contains("ActionMenu", appSource, StringComparison.Ordinal);
        Assert.Contains("Персонаж / Душа", appSource, StringComparison.Ordinal);
        Assert.Contains("Подготовить форму", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("setAdvancedEnabled(true)", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("action.advancedCommand}", appSource, StringComparison.Ordinal);
    }

    private static async Task<SmokeResponse> CaptureAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        return new SmokeResponse(path, response.StatusCode, response.Content.Headers.ContentType?.ToString(), body);
    }

    private static string[] ExtractAssetPaths(string html) =>
        Regex.Matches(html, "(?:src|href)=\"(?<path>/assets/[^\"]+)\"", RegexOptions.IgnoreCase)
            .Select(match => WebUtility.HtmlDecode(match.Groups["path"].Value))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private sealed record SmokeResponse(string Path, HttpStatusCode StatusCode, string? ContentType, string Body)
    {
        public SmokeRequestArtifact ToArtifact() => new(Path, (int)StatusCode, ContentType, Body.Length);
    }

    private sealed record SmokeRequestArtifact(string Path, int Status, string? ContentType, int BodyLength);

    private static string PrepareArtifactDirectory()
    {
        var artifactRoot = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "browser-smoke");
        Directory.CreateDirectory(artifactRoot);
        return artifactRoot;
    }

    private static int GetFreeLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

    private void WriteSessionFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_rootPath, "game_session", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
