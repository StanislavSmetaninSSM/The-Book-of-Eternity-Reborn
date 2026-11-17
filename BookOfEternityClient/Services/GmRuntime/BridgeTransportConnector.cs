using System.IO.Pipes;

namespace BookOfEternityClient.Services.GmRuntime;

/// <summary>Transport coordinates, never application admission or process authority.</summary>
public sealed record BridgeTransportEndpoint(int Version, string Kind, string Address, string? TransportRun);

public static class BridgeTransportConnector
{
    public static Task<Stream> ConnectAsync(BridgeTransportEndpoint endpoint, CancellationToken token = default)
        => Task.FromException<Stream>(new IOException("Bridge transport connector not implemented."));
}
