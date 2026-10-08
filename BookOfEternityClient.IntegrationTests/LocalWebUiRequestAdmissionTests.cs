using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.WebUi;
using Microsoft.AspNetCore.Builder;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalWebUiRequestAdmissionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-http-admission-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ParallelStateRequestsWaitOutsidePhysicalGuardBudget()
    {
        await WithHeldSessionAsync(async (client, release, contention) =>
        {
            var queued = client.GetAsync("/api/audio/settings");
            try
            {
                // The unchanged physical guard exhausts 200 attempts * 50ms.
                // An ordinary sibling request must wait before entering that boundary.
                await Task.Delay(TimeSpan.FromSeconds(11.5));
            }
            finally { release.TrySetResult(); }
            using var response = await queued;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var audio = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.NotNull(audio["musicEnabled"]);
            Assert.Equal(0, contention());
        });
    }

    [Fact]
    public async Task CancelledQueuedRequestLeavesNextRequestUsable()
    {
        await WithHeldSessionAsync(async (client, release, _) =>
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.GetAsync("/api/audio/settings", cancellation.Token));
            release.TrySetResult();
            using var next = await client.GetAsync("/api/session");
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        });
    }

    [Theory]
    [InlineData("/API/SAVES/LOAD-COMPLETE/")]
    [InlineData("/api/saves/load-cancel")]
    public async Task LoadCompletionAndCancellationBypassWaitingStateRequests(string route)
    {
        await WithHeldSessionAsync(async (client, release, _) =>
        {
            var queued = client.GetAsync("/api/audio/settings");
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var response = await client.PostAsJsonAsync(route,
                    new { operationId = "unknown-owned-test-operation", establishedGeneration = "unknown", refreshConfirmed = true }, timeout.Token);
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
                Assert.False(body["success"]!.GetValue<bool>());
            }
            finally { release.TrySetResult(); }
            using var resumed = await queued;
            Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        });
    }

    [Theory]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/api/explorer/command-coverage", HttpStatusCode.OK)]
    [InlineData("/api/audio/assets/absent-owned-asset", HttpStatusCode.NotFound)]
    public async Task StaticAndMetadataDeliveryBypassStateRequests(string route, HttpStatusCode expected)
    {
        await WithHeldSessionAsync(async (client, _, _) =>
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var response = await client.GetAsync(route, timeout.Token);
            Assert.Equal(expected, response.StatusCode);
        });
    }

    [Fact]
    public async Task HandlerFailureRemainsFailureAndReleasesAdmission()
    {
        var armed = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalWriteLockOpenAsync = () =>
            {
                if (Interlocked.Exchange(ref armed, 0) == 1) throw new IOException("owned diagnostic failure");
                return Task.CompletedTask;
            }
        };
        await WithHostAsync(hooks, async client =>
        {
            Volatile.Write(ref armed, 1);
            using var failed = await client.GetAsync("/api/session");
            Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
            using var next = await client.GetAsync("/api/session");
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        });
    }

    [Fact]
    public async Task IndependentlyHeldPhysicalGuardStillRefusesHttpRequest()
    {
        await WithHostAsync(null, async client =>
        {
            var path = Path.Combine(_root, ".boe_runtime", "locks", "gm-main-owner.lock");
            using (var originalGuard = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                using var refused = await client.GetAsync("/api/session");
                Assert.Equal(HttpStatusCode.InternalServerError, refused.StatusCode);
            }
            using var next = await client.GetAsync("/api/session");
            Assert.Equal(HttpStatusCode.OK, next.StatusCode);
        });
    }

    private async Task WithHeldSessionAsync(Func<HttpClient, TaskCompletionSource, Func<int>, Task> action)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = 0;
        var contentions = 0;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                if (Interlocked.Exchange(ref armed, 0) != 1) return;
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            },
            MainOwnerLockContendedAsync = () => { Interlocked.Increment(ref contentions); return Task.CompletedTask; }
        };
        await WithHostAsync(hooks, async client =>
        {
            Volatile.Write(ref armed, 1);
            var first = client.GetAsync("/api/session");
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                await action(client, release, () => Volatile.Read(ref contentions));
            }
            finally
            {
                release.TrySetResult();
                using var response = await first;
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        });
    }

    private async Task WithHostAsync(FileSystemManagerHooks? hooks, Func<HttpClient, Task> action)
    {
        Directory.CreateDirectory(_root);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var assets = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "public");
        await using var app = LocalWebUiHost.Build([], new(_root, url, assets), hooks);
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(40) };
            await action(client);
        }
        finally
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await app.StopAsync(timeout.Token);
        }
    }

    public void Dispose()
    {
        var path = Path.GetFullPath(_root);
        var expectedPrefix = Path.Combine(Path.GetFullPath(Path.GetTempPath()), "boe-http-admission-");
        if (!path.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected owned fixture root.");
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
