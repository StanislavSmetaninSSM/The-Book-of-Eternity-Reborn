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

    [Fact]
    public void NativeDirectoryPostCreateFailure_RemovesOwnedEntry()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-fifo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var parent = LinuxBridgeFifoNative.OpenDirectory(root);
            Assert.Throws<IOException>(() => LinuxBridgeFifoNative.CreateDirectory(parent, "owned", () => throw new IOException("causal post-create cut")));
            Assert.False(Directory.Exists(Path.Combine(root, "owned")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void NativeFifoPostCreateFailure_RemovesOwnedEntry()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-fifo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var parent = LinuxBridgeFifoNative.OpenDirectory(root);
            Assert.Throws<IOException>(() => LinuxBridgeFifoNative.CreateFifo(parent, "owned", () => throw new IOException("causal post-create cut")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ListenerPostTicketFailure_RemovesOwnedRun()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-fifo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<IOException>(() => LinuxBridgeFifoListener.Create(root, () => throw new IOException("causal post-ticket cut")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task ConcurrentDisposals_WaitForActualHandleClose()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var read = File.OpenHandle("/dev/null", FileMode.Open, FileAccess.Read);
        var write = File.OpenHandle("/dev/null", FileMode.Open, FileAccess.Write);
        var stream = new LinuxBridgeFifoStream(read, write, () => { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); });
        Task? first = null, second = null;
        try
        {
            first = Task.Run(async () => await stream.DisposeAsync());
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            second = stream.DisposeAsync().AsTask();
            Assert.False(second.IsCompleted);
        }
        finally
        {
            release.Set();
            if (first != null) await first;
            if (second != null) await second;
            await stream.DisposeAsync();
        }
        Assert.True(read.IsClosed);
        Assert.True(write.IsClosed);
    }

    [Fact]
    public async Task InjectedPendingAdmissionFault_DisposalStillDrainsQueueAndRemovesRun()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-fifo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var calls = 0;
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = LinuxBridgeFifoListener.Create(root, beforeReadyEnqueue: () =>
        {
            if (Interlocked.Increment(ref calls) == 2)
            {
                failed.TrySetResult();
                throw new System.Threading.Channels.ChannelClosedException();
            }
        });
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await using var first = await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint, deadline.Token);
            await using var second = await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint, deadline.Token);
            await failed.Task.WaitAsync(deadline.Token);
            await Assert.ThrowsAsync<System.Threading.Channels.ChannelClosedException>(() => listener.DisposeAsync().AsTask());
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
            Assert.Equal(0, await first.ReadAsync(new byte[1], deadline.Token));
        }
        finally
        {
            // Initial RED may leave a queued connection; reclaim that known test stream.
            using var rescue = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            try { await using var queued = await listener.AcceptAsync(rescue.Token); }
            catch (Exception ex) when (ex is OperationCanceledException or System.Threading.Channels.ChannelClosedException) { }
            try { await listener.DisposeAsync(); }
            catch (System.Threading.Channels.ChannelClosedException) { }
            Directory.Delete(root, recursive: true);
        }
    }
}
