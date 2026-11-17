namespace BookOfEternityGMBridge;
internal static class BridgePeerDispatcher
{
    internal static Task RunAsync(BridgeTransportListener listener, Func<Stream,CancellationToken,Task> handler,
        CancellationToken token, int maximumPeers=48) => throw new IOException("Dispatcher not implemented.");
}
