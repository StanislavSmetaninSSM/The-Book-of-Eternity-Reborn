namespace BookOfEternityClient.Services.GmRuntime;

// #1589 one-shot external CLR boundary for Windows PowerShell Desktop.
// RED stub: no transport, admission, state access, replay or background work.
internal static class GmBridgeRequestControl
{
    internal sealed record Request(string RequestId, string StatusJson, string RequestJson, int TimeoutMilliseconds);
    internal static Request ParseRequest(string json) => throw new InvalidDataException("Bridge request helper is not implemented.");
    internal static Task<int> RunAsync(Stream input, Stream output, CancellationToken token)
        => Task.FromResult(2);
    internal static Task<int?> TryRunAsync(string[] args)
        => Task.FromResult<int?>(args.Contains("--gm-bridge-request", StringComparer.Ordinal) ? 2 : null);
}
