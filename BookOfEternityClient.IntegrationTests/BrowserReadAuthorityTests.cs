using System.Net;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmRuntime;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserReadAuthorityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-read-authority-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("/api/session")]
    [InlineData("/api/game-screen")]
    [InlineData("/api/lifecycle/dashboard")]
    [InlineData("/api/lifecycle/validate")]
    public async Task StoppingAuthorityCannotBecomeSuccessfulRead(string route)
    {
        Directory.CreateDirectory(_root);
        var assets = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend/public");
        await using var app = LocalWebUiHost.Build([], new(_root, "http://127.0.0.1:0", assets));
        await app.StartAsync();
        var files = app.Services.GetRequiredService<FileSystemManager>();
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        // Negative-only refusal fixture. This is never evidence of a live owner.
        var identity = new GmSessionRunIdentity(files.BasePath, Guid.NewGuid().ToString("N"),
            Guid.NewGuid().ToString("N"), 1,
            OperatingSystem.IsWindows() ? GmSessionRunBackend.WindowsJob : GmSessionRunBackend.LinuxSupervisor,
            Guid.NewGuid().ToString("N"), "negative-only-no-owner");
        var record = GmSessionRunRecordCodec.Encode(new(1, identity, GmSessionRunDisposition.Stopping, null));
        var recordPath = Path.Combine(files.RuntimeRootPath, "gm-runs/main.json");
        Directory.CreateDirectory(Path.GetDirectoryName(recordPath)!);
        File.WriteAllBytes(recordPath, record);
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(30) };
            using var response = await SendAsync(http, route);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(record, File.ReadAllBytes(recordPath));
            Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        }
        finally
        {
            File.Delete(recordPath);
            await app.StopAsync();
        }
    }

    [Theory]
    [InlineData("/api/session")]
    [InlineData("/api/game-screen")]
    [InlineData("/api/lifecycle/dashboard")]
    [InlineData("/api/lifecycle/validate")]
    public async Task FinalizationFailureCannotBecomeSuccessfulRead(string route)
    {
        var armed = 0;
        var closes = 0;
        var hooks = new FileSystemManagerHooks
        {
            SessionOperationClosingAsync = () =>
            {
                if (Volatile.Read(ref armed) == 1)
                {
                    Interlocked.Increment(ref closes);
                    throw new IOException("controlled original operation finalization failure");
                }
                return Task.CompletedTask;
            }
        };
        Directory.CreateDirectory(_root);
        var assets = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend/public");
        await using var app = LocalWebUiHost.Build([], new(_root, "http://127.0.0.1:0", assets), hooks);
        await app.StartAsync();
        var files = app.Services.GetRequiredService<FileSystemManager>();
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json",
            """{"soulName":"Owned read fixture","currentRealm":"Chaos Sea","currentIncarnation":0}""");
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        try
        {
            Volatile.Write(ref armed, 1);
            using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(45) };
            using var response = await SendAsync(http, route);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.True(Volatile.Read(ref closes) > 0, "The real operation must reach its finalization boundary.");
            Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        }
        finally
        {
            Volatile.Write(ref armed, 0);
            await app.StopAsync();
        }
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient http, string route) =>
        route.EndsWith("/validate", StringComparison.Ordinal) ? http.PostAsync(route, null) : http.GetAsync(route);

    public void Dispose()
    {
        var path = Path.GetFullPath(_root);
        var prefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "boe-read-authority-");
        if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected fixture root.");
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
