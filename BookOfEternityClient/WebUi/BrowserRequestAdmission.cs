using Microsoft.AspNetCore.Http;

namespace BookOfEternityClient.WebUi;

/// <summary>
/// Queues this host's state handlers before they enter the finite physical owner
/// guard. This is scheduling only; every handler retains its existing authority checks.
/// </summary>
internal sealed class BrowserRequestAdmission : IEndpointFilter
{
    // No WaitHandle is created. Keep this managed-only gate alive with in-flight
    // handlers, which may unwind after an aborted host shutdown disposes DI.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        await _gate.WaitAsync(context.HttpContext.RequestAborted);
        try
        {
            context.HttpContext.RequestAborted.ThrowIfCancellationRequested();
            return await next(context);
        }
        finally { _gate.Release(); }
    }
}
