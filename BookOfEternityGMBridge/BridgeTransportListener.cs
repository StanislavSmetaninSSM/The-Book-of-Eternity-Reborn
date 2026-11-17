using BookOfEternityClient.Services.GmRuntime;
namespace BookOfEternityGMBridge;
internal abstract class BridgeTransportListener : IAsyncDisposable
{
    internal abstract BridgeTransportEndpoint Endpoint { get; }
    internal abstract Task<Exception> Failure { get; }
    internal abstract Task<Stream> AcceptAsync(CancellationToken token);
    public abstract ValueTask DisposeAsync();
    internal static BridgeTransportListener CreateFifo(string parent, Action? beforeReady = null) => throw new IOException("FIFO adapter not implemented.");
    internal static BridgeTransportListener Create(string pipeName, string parent,
        Action? socketProbe = null, Func<BridgeTransportEndpoint, BridgeTransportListener>? namedFactory = null)
        => throw new IOException("Listener adapter not implemented.");
}
