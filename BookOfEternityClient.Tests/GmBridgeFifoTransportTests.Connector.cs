using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmBridgeFifoTransportTests
{
    [Fact]
    public async Task PublicConnector_UsesOriginalFifoStreamAndEof()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-connector", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var accepting = listener.AcceptAsync(timeout.Token);
            await using var client = await BridgeTransportConnector.ConnectAsync(
                new(1, "linux-fifo-v1", listener.Endpoint.DirectoryPath, listener.Endpoint.RunId), timeout.Token);
            await using var server = await accepting;
            await client.WriteAsync("original"u8.ToArray(), timeout.Token);
            var bytes = new byte[8];
            await server.ReadExactlyAsync(bytes, timeout.Token);
            Assert.Equal("original"u8.ToArray(), bytes);
            await server.DisposeAsync();
            Assert.Equal(0, await client.ReadAsync(new byte[1], timeout.Token));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(2, "linux-fifo-v1", "/tmp/boe-bridge-00000000000000000000000000000000", "00000000000000000000000000000000")]
    [InlineData(1, "unknown", "pipe", null)]
    [InlineData(1, "named-pipe-v1", "../pipe", null)]
    [InlineData(1, "named-pipe-v1", "pipe", "unexpected-run")]
    [InlineData(1, "linux-fifo-v1", "relative", "00000000000000000000000000000000")]
    [InlineData(1, "linux-fifo-v1", "/tmp/boe-bridge-00000000000000000000000000000000", "11111111111111111111111111111111")]
    public async Task PublicConnector_InvalidEndpointRefusesBeforeConnect(int version, string kind, string address, string? run)
        => await Assert.ThrowsAsync<InvalidDataException>(() => BridgeTransportConnector.ConnectAsync(new(version, kind, address, run)));

    [Fact]
    public async Task PublicConnector_CancelledTokenNeverCreatesPeer()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-connector", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BridgeTransportConnector.ConnectAsync(
                new(1, "linux-fifo-v1", listener.Endpoint.DirectoryPath, listener.Endpoint.RunId), new CancellationToken(true)));
            Assert.Empty(Directory.GetDirectories(listener.Endpoint.DirectoryPath));
        }
        finally { Directory.Delete(root, true); }
    }
    [Fact]
    public async Task PublicConnector_LinuxRelativeNamedEndpointCannotDependOnTmpdir()
    {
        Assert.True(OperatingSystem.IsLinux());
        await Assert.ThrowsAsync<InvalidDataException>(() => BridgeTransportConnector.ConnectAsync(
            new(1, "named-pipe-v1", "ambiguous-relative-name", null), new CancellationToken(true)));
    }

    [Fact]
    public async Task PublicConnector_StaleRunDoesNotAdmitToFreshListener()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-connector", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            BridgeTransportEndpoint stale;
            await using (var old = LinuxBridgeFifoListener.Create(root))
                stale = new(1, "linux-fifo-v1", old.Endpoint.DirectoryPath, old.Endpoint.RunId);
            await using var fresh = LinuxBridgeFifoListener.Create(root);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<IOException>(() => BridgeTransportConnector.ConnectAsync(stale, timeout.Token));
            Assert.Empty(Directory.GetDirectories(fresh.Endpoint.DirectoryPath));
            var accepting = fresh.AcceptAsync(timeout.Token);
            await using var client = await BridgeTransportConnector.ConnectAsync(
                new(1, "linux-fifo-v1", fresh.Endpoint.DirectoryPath, fresh.Endpoint.RunId), timeout.Token);
            await using var server = await accepting;
            await client.WriteAsync(new byte[] { 73 }, timeout.Token);
            var actual = new byte[1];
            await server.ReadExactlyAsync(actual, timeout.Token);
            Assert.Equal(73, actual[0]);
        }
        finally { Directory.Delete(root, true); }
    }

}
