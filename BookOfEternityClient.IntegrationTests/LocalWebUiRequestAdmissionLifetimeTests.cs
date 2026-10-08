using BookOfEternityClient.WebUi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalWebUiRequestAdmissionLifetimeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-http-lifetime-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CancelledFilterInvocationNeverEntersOrReleasesOriginalPermit()
    {
        await using var app = Build();
        var admission = app.Services.GetRequiredService<BrowserRequestAdmission>();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var original = admission.InvokeAsync(Context(), async _ => { await release.Task; return "original"; }).AsTask();
        using var cancellation = new CancellationTokenSource();
        var cancelledEntries = 0;
        var cancelled = admission.InvokeAsync(Context(cancellation.Token), _ =>
        { Interlocked.Increment(ref cancelledEntries); return ValueTask.FromResult<object?>("must not enter"); }).AsTask();
        Task<object?>? next = null;
        try
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
            Assert.Equal(0, cancelledEntries);
            next = admission.InvokeAsync(Context(), _ => ValueTask.FromResult<object?>("next")).AsTask();
            Assert.False(next.IsCompleted); // cancelled wait cannot release the original invocation's permit
        }
        finally { release.TrySetResult(); await original; }
        Assert.Equal("next", await next!);
    }

    [Fact]
    public async Task AbortedHostShutdownDoesNotDisposePermitBeforeHandlerReturns()
    {
        var app = Build();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var admission = app.Services.GetRequiredService<BrowserRequestAdmission>();
        app.MapGet("/owned-test-hold", async () =>
        { entered.TrySetResult(); await release.Task; return "owned result"; })
            .AddEndpointFilter(async (context, next) =>
            {
                try { var result = await next(context); completed.TrySetResult(null); return result; }
                catch (Exception error) { completed.TrySetResult(error); throw; }
            })
            .AddEndpointFilter(admission);
        var disposed = false;
        Task<HttpResponseMessage>? request = null;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        try
        {
            await app.StartAsync();
            request = client.GetAsync(app.Urls.Single() + "/owned-test-hold");
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            try { await app.StopAsync(stop.Token); }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            await app.DisposeAsync();
            disposed = true;
            release.TrySetResult();
            Assert.Null(await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            release.TrySetResult();
            if (request != null)
                try { using var response = await request; }
                catch (HttpRequestException) { }
                catch (OperationCanceledException) { }
            if (!disposed) await app.DisposeAsync();
        }
    }

    private WebApplication Build()
    {
        Directory.CreateDirectory(_root);
        var assets = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.WebFrontend", "public");
        return LocalWebUiHost.Build([], new(_root, "http://127.0.0.1:0", assets));
    }

    private static EndpointFilterInvocationContext Context(CancellationToken token = default) =>
        new DefaultEndpointFilterInvocationContext(new DefaultHttpContext { RequestAborted = token });

    public void Dispose()
    {
        var path = Path.GetFullPath(_root);
        if (!path.StartsWith(Path.Combine(Path.GetFullPath(Path.GetTempPath()), "boe-http-lifetime-"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unexpected owned fixture root.");
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }
}
