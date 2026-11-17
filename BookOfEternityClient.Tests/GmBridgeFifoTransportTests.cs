using System.Text;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmBridgeFifoTransportTests
{
    [Fact]
    public async Task OriginalPair_ExchangesBothDirections_AndPeerCloseIsEof()
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-fifo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var accept = listener.AcceptAsync(deadline.Token);
            await using var client = await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint, deadline.Token);
            await using var server = await accept;
            var request = Encoding.UTF8.GetBytes("Привет, ведущий!\n");
            await client.WriteAsync(request, deadline.Token);
            var input = new byte[request.Length];
            await server.ReadExactlyAsync(input, deadline.Token);
            Assert.Equal(request, input);
            await server.WriteAsync("reply\n"u8.ToArray(), deadline.Token);
            var reply = new byte[6];
            await client.ReadExactlyAsync(reply, deadline.Token);
            Assert.Equal("reply\n", Encoding.UTF8.GetString(reply));
            await server.DisposeAsync();
            Assert.Equal(0, await client.ReadAsync(new byte[1], deadline.Token));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task CancelledPendingRead_SettlesWithoutConsumingLaterBytes()
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-fifo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var accept = listener.AcceptAsync(deadline.Token);
            await using var client = await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint, deadline.Token);
            await using var server = await accept;
            using var canceled = new CancellationTokenSource();
            var read = client.ReadAsync(new byte[4], canceled.Token).AsTask();
            canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read);
            await server.WriteAsync("next"u8.ToArray(), deadline.Token);
            var bytes = new byte[4];
            await client.ReadExactlyAsync(bytes, deadline.Token);
            Assert.Equal("next", Encoding.UTF8.GetString(bytes));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
