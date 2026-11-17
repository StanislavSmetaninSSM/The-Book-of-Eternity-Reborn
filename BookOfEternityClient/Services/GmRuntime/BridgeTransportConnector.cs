using System.IO.Pipes;

namespace BookOfEternityClient.Services.GmRuntime;

/// <summary>Transport coordinates, never application admission or process authority.</summary>
public sealed record BridgeTransportEndpoint(int Version, string Kind, string Address, string? TransportRun);

public static class BridgeTransportConnector
{
    public static async Task<Stream> ConnectAsync(BridgeTransportEndpoint endpoint, CancellationToken token = default)
    {
        Validate(endpoint);
        token.ThrowIfCancellationRequested();
        if (endpoint.Kind == "linux-fifo-v1")
            return await LinuxBridgeFifoClient.ConnectAsync(new(endpoint.Address, endpoint.TransportRun!), token).ConfigureAwait(false);
        var pipe = new NamedPipeClientStream(".", endpoint.Address, PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            await pipe.ConnectAsync(deadline.Token).ConfigureAwait(false);
            return pipe;
        }
        catch { pipe.Dispose(); throw; }
    }

    internal static void Validate(BridgeTransportEndpoint endpoint)
    {
        if (endpoint == null || endpoint.Version != 1 || string.IsNullOrWhiteSpace(endpoint.Address) ||
            endpoint.Address.Length > 4096 || endpoint.Address.Contains('\0'))
            throw new InvalidDataException("Invalid bridge transport endpoint.");
        if (endpoint.Kind == "linux-fifo-v1")
        {
            if (!OperatingSystem.IsLinux() || !Path.IsPathFullyQualified(endpoint.Address) ||
                endpoint.Address != Path.GetFullPath(endpoint.Address) || endpoint.TransportRun == null ||
                !LinuxBridgeFifoProtocol.Nonce(endpoint.TransportRun) ||
                Path.GetFileName(endpoint.Address) != "boe-bridge-" + endpoint.TransportRun)
                throw new InvalidDataException("Invalid Linux FIFO endpoint.");
        }
        else if (endpoint.Kind == "named-pipe-v1")
        {
            // Linux absolute socket paths preserve endpoint identity across TMPDIR changes.
            var absoluteLinux = OperatingSystem.IsLinux() && Path.IsPathFullyQualified(endpoint.Address) &&
                endpoint.Address == Path.GetFullPath(endpoint.Address);
            var simpleName = endpoint.Address.Length <= 128 && endpoint.Address.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
            if (endpoint.TransportRun != null || (!simpleName && !absoluteLinux))
                throw new InvalidDataException("Invalid named-pipe endpoint.");
        }
        else throw new InvalidDataException("Unsupported bridge transport endpoint.");
    }
}
