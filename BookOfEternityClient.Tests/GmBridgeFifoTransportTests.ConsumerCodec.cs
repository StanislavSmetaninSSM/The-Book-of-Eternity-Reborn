using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmBridgeFifoTransportTests
{
    private static readonly BridgeTransportEndpoint CodecEndpoint = new(1, "linux-fifo-v1",
        "/tmp/boe-bridge-0123456789abcdef0123456789abcdef", "0123456789abcdef0123456789abcdef");

    [Fact]
    public void PublicEndpointCodec_ExactDescriptorRoundTripsWithoutPipeNameFallback()
    {
        var json = BridgeTransportConnector.EncodeEndpoint(CodecEndpoint);
        Assert.Equal(CodecEndpoint, BridgeTransportConnector.DecodeEndpoint(json));
        Assert.Equal(CodecEndpoint, BridgeTransportConnector.DecodeStatus("{\"state\":\"Running\",\"pipeName\":\"diagnostic-only\",\"transportEndpoint\":" + json + "}"));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"pipeName\":\"old-only\"}")]
    [InlineData("{\"transportEndpoint\":null}")]
    [InlineData("{\"transportEndpoint\":{},\"transportEndpoint\":{}}")]
    [InlineData("{\"TransportEndpoint\":{}}")]
    public void PublicEndpointCodec_MissingMalformedOrDuplicateStatusRefuses(string status)
        => Assert.Throws<InvalidDataException>(() => BridgeTransportConnector.DecodeStatus(status));

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"version\":1,\"version\":1,\"kind\":\"named-pipe-v1\",\"address\":\"/tmp/a\",\"transportRun\":null}")]
    [InlineData("{\"version\":1,\"kind\":\"named-pipe-v1\",\"address\":\"/tmp/a\",\"transportRun\":null,\"fallback\":true}")]
    [InlineData("{\"Version\":1,\"kind\":\"named-pipe-v1\",\"address\":\"/tmp/a\",\"transportRun\":null}")]
    public void PublicEndpointCodec_ClosedExactSchemaRefusesAmbiguity(string endpoint)
        => Assert.Throws<InvalidDataException>(() => BridgeTransportConnector.DecodeEndpoint(endpoint));

    [Fact]
    public async Task PublicRequestHelper_UsesOriginalConnectionAndReturnsActualResponse()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-request", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var accept = listener.AcceptAsync(timeout.Token);
            var descriptor = new BridgeTransportEndpoint(1, "linux-fifo-v1", listener.Endpoint.DirectoryPath, listener.Endpoint.RunId);
            var status = "{\"transportEndpoint\":" + BridgeTransportConnector.EncodeEndpoint(descriptor) + "}";
            var reply = BridgeTransportConnector.RequestAsync(status, "{\n\"command\":\"diagnostics\"\n}", 3000, timeout.Token);
            await using var server = await accept;
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true);
            using var request = JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
            Assert.Equal("diagnostics", request.RootElement.GetProperty("command").GetString());
            await server.WriteAsync("{\"ok\":true,\"actual\":73}\n"u8.ToArray(), timeout.Token);
            Assert.Equal("{\"ok\":true,\"actual\":73}", await reply);
            Assert.Null(await reader.ReadLineAsync(timeout.Token));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public void PublicEndpointCodec_DuplicateOtherwiseValidDescriptorIsRefused()
    {
        var json = BridgeTransportConnector.EncodeEndpoint(CodecEndpoint);
        Assert.Throws<InvalidDataException>(() => BridgeTransportConnector.DecodeStatus(
            "{\"transportEndpoint\":" + json + ",\"transportEndpoint\":" + json + "}"));
    }

    [Fact]
    public async Task PublicRequestHelper_CancelledReadCompletesAndClosesOriginalStream()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-request", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var cancelRequest = new CancellationTokenSource();
            var accept = listener.AcceptAsync(timeout.Token);
            var descriptor = new BridgeTransportEndpoint(1, "linux-fifo-v1", listener.Endpoint.DirectoryPath, listener.Endpoint.RunId);
            var status = "{\"transportEndpoint\":" + BridgeTransportConnector.EncodeEndpoint(descriptor) + "}";
            var reply = BridgeTransportConnector.RequestAsync(status, "{\"command\":\"diagnostics\"}", 5000, cancelRequest.Token);
            await using var server = await accept;
            using var reader = new StreamReader(server, Encoding.UTF8, false, 1024, true);
            Assert.NotNull(await reader.ReadLineAsync(timeout.Token));
            cancelRequest.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reply);
            Assert.Null(await reader.ReadLineAsync(timeout.Token));
        }
        finally { Directory.Delete(root, true); }
    }

}
