using System.IO.Pipes;
using System.Text.Json;

namespace BookOfEternityClient.Services.GmRuntime;

/// <summary>Transport coordinates, never application admission or process authority.</summary>
public sealed record BridgeTransportEndpoint(int Version, string Kind, string Address, string? TransportRun);

public static class BridgeTransportConnector
{
    private static readonly JsonSerializerOptions EndpointJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // Only immutable transport coordinates are decoded here. They cannot grant an
    // application pin, adopt a process, or replace the original operation handshake.
    public static BridgeTransportEndpoint DecodeEndpoint(string json)
    {
        try { using var doc = JsonDocument.Parse(json); return DecodeEndpoint(doc.RootElement); }
        catch (JsonException ex) { throw new InvalidDataException("Malformed bridge endpoint JSON.", ex); }
    }

    private static BridgeTransportEndpoint DecodeEndpoint(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Bridge endpoint must be an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
            if (!names.Add(property.Name) || property.Name is not ("version" or "kind" or "address" or "transportRun"))
                throw new InvalidDataException("Ambiguous or unknown bridge endpoint field.");
        if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) ||
            !root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Incomplete bridge endpoint.");
        string? run = null;
        if (root.TryGetProperty("transportRun", out var runValue))
        {
            if (runValue.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
                throw new InvalidDataException("Invalid bridge transport run.");
            if (runValue.ValueKind == JsonValueKind.String) run = runValue.GetString();
        }
        var endpoint = new BridgeTransportEndpoint(number, kind.GetString()!, address.GetString()!, run);
        Validate(endpoint);
        return endpoint;
    }

    public static BridgeTransportEndpoint DecodeStatus(string json)
    {
        try { using var doc = JsonDocument.Parse(json); return DecodeStatus(doc.RootElement); }
        catch (JsonException ex) { throw new InvalidDataException("Malformed bridge status JSON.", ex); }
    }

    public static BridgeTransportEndpoint DecodeStatus(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Bridge status must be an object.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
            if (!names.Add(property.Name) ||
                (string.Equals(property.Name, "transportEndpoint", StringComparison.OrdinalIgnoreCase) && property.Name != "transportEndpoint"))
                throw new InvalidDataException("Ambiguous bridge status fields.");
        if (!root.TryGetProperty("transportEndpoint", out var endpoint))
            throw new InvalidDataException("Versioned bridge transport endpoint is absent; no legacy fallback.");
        return DecodeEndpoint(endpoint);
    }

    public static string EncodeEndpoint(BridgeTransportEndpoint endpoint)
    {
        Validate(endpoint);
        return JsonSerializer.Serialize(endpoint, EndpointJson);
    }

    // The shipped PowerShell launcher calls this one asynchronous operation. It
    // owns and joins every I/O, instead of abandoning timed-out Wait tasks.
    public static async Task<string> RequestAsync(string statusJson, string requestJson, int timeoutMilliseconds, CancellationToken token = default)
    {
        if (timeoutMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
        var endpoint = DecodeStatus(statusJson);
        using var request = JsonDocument.Parse(requestJson);
        if (request.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Bridge request must be an object.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeoutMilliseconds);
        await using var stream = await ConnectAsync(endpoint, deadline.Token).ConfigureAwait(false);
        await MainOperationReader.WriteAsync(stream, request.RootElement, deadline.Token, int.MaxValue).ConfigureAwait(false);
        var response = await new MainOperationReader(stream).ReadAsync<JsonElement?>(deadline.Token, int.MaxValue).ConfigureAwait(false);
        return response?.GetRawText() ?? throw new IOException("Bridge returned an empty response.");
    }

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

    // Pure name grammar, independent of the executing OS; no connection or authority.
    internal static bool IsSimpleNamedPipeName(string name)
        => name.Length <= 128 && name.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

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
            var simpleName = IsSimpleNamedPipeName(endpoint.Address);
            if (endpoint.TransportRun != null || (OperatingSystem.IsLinux() ? !absoluteLinux : !simpleName))
                throw new InvalidDataException("Invalid named-pipe endpoint.");
        }
        else throw new InvalidDataException("Unsupported bridge transport endpoint.");
    }
}
