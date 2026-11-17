namespace BookOfEternityClient.Services.GmRuntime;

// Refusing API skeleton for the first causal transport tests; not wired into bridge.
internal sealed record LinuxBridgeFifoEndpoint(string DirectoryPath, string RunId, int ProtocolVersion = 1);

internal sealed class LinuxBridgeFifoListener : IAsyncDisposable
{
    internal LinuxBridgeFifoEndpoint Endpoint => throw new NotSupportedException("FIFO transport not implemented.");
    internal static LinuxBridgeFifoListener Create(string parentDirectory) => throw new NotSupportedException("FIFO transport not implemented.");
    internal Task<Stream> AcceptAsync(CancellationToken token = default) => throw new NotSupportedException("FIFO transport not implemented.");
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal static class LinuxBridgeFifoClient
{
    internal static Task<Stream> ConnectAsync(LinuxBridgeFifoEndpoint endpoint, CancellationToken token = default) =>
        throw new NotSupportedException("FIFO transport not implemented.");
}
