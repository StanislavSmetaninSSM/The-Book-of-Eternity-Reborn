using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Services.GmRuntime;

// One original control request across the Desktop CLR boundary; never retries.
internal static class GmBridgeRequestControl
{
    internal sealed record Request(string RequestId, string StatusJson, string RequestJson, int TimeoutMilliseconds);
    internal static Request ParseRequest(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid control envelope.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!names.Add(property.Name) || property.Name is not ("requestId" or "statusJson" or "requestJson" or "timeoutMilliseconds"))
                    throw new InvalidDataException("Invalid control envelope fields.");
            if (names.Count != 4) throw new InvalidDataException("Missing control envelope fields.");
            var id = root.GetProperty("requestId").GetString();
            if (!Guid.TryParseExact(id, "N", out var guid) || guid.ToString("N") != id)
                throw new InvalidDataException("Invalid control correlation.");
            var status = root.GetProperty("statusJson").GetString();
            var request = root.GetProperty("requestJson").GetString();
            if (status is null || request is null || !root.GetProperty("timeoutMilliseconds").TryGetInt32(out var timeout) || timeout <= 0)
                throw new InvalidDataException("Invalid control payload or deadline.");
            return new Request(id!, status, request, timeout);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { throw new InvalidDataException("Invalid control envelope.", ex); }
    }

    internal static async Task<int> RunAsync(Stream input, Stream output, CancellationToken token)
    {
        Request request;
        try
        {
            using var reader = new StreamReader(input, new UTF8Encoding(false, true), false, 1024, leaveOpen: true);
            var line = await reader.ReadLineAsync(token);
            if (line is null) return 2;
            // Strict UTF-8 decoding above; remove one UTF-8 preamble only.
            // Do not enable StreamReader UTF-16/32 autodetection.
            if (line.Length > 0 && line[0] == '\uFEFF') line = line[1..];
            request = ParseRequest(line);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or DecoderFallbackException or OperationCanceledException)
        { return 2; }
        object reply;
        int exit;
        try
        {
            var response = await BridgeTransportConnector.RequestAsync(request.StatusJson, request.RequestJson, request.TimeoutMilliseconds, token);
            reply = new { requestId = request.RequestId, ok = true, response };
            exit = 0;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            reply = new { requestId = request.RequestId, ok = false, errorCode = "bridge_request_failed_or_unknown" };
            exit = 2;
        }
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(reply) + "\n");
        await output.WriteAsync(bytes, token);
        await output.FlushAsync(token);
        return exit;
    }

    internal static async Task<int?> TryRunAsync(string[] args)
    {
        if (!args.Contains("--gm-bridge-request", StringComparer.Ordinal)) return null;
        if (args.Length != 1) return 2;
        return await RunAsync(Console.OpenStandardInput(), Console.OpenStandardOutput(), CancellationToken.None);
    }
}
