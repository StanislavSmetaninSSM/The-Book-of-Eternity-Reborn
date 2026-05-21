using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using BookOfEternityClient.WebUi;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalWebUiHostTests : IDisposable
{
    private readonly string _rootPath;

    public LocalWebUiHostTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-local-web-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void Build_RejectsNonLoopbackUrls()
    {
        var options = new LocalWebUiHostOptions(_rootPath, "http://0.0.0.0:8787");

        var ex = Assert.Throws<InvalidOperationException>(() => LocalWebUiHost.Build(Array.Empty<string>(), options));

        Assert.Contains("localhost", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsLocalSessionStatus()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var json = await client.GetStringAsync("/api/health");
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("ok", root["status"]!.GetValue<string>());
        Assert.True(root["localOnly"]!.GetValue<bool>());
        Assert.Equal(_rootPath, root["basePath"]!.GetValue<string>());
        Assert.Equal(Path.Combine(_rootPath, "game_session"), root["gameSessionPath"]!.GetValue<string>());
    }

    [Fact]
    public async Task RootEndpoint_ReturnsBrowserShellHtml()
    {
        var url = "http://127.0.0.1:" + GetFreeLoopbackPort();
        await using var app = LocalWebUiHost.Build(Array.Empty<string>(), new LocalWebUiHostOptions(_rootPath, url));
        await app.StartAsync();

        using var client = new HttpClient { BaseAddress = new Uri(url) };
        var html = await client.GetStringAsync("/");

        Assert.Contains("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("The Book of Eternity", html, StringComparison.Ordinal);
        Assert.Contains("/api/health", html, StringComparison.Ordinal);
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

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
