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

    [Fact]
    public async Task ConcurrentPeers_LargePayloadsRemainSeparate_AndPathsAreRemoved()
    {
        await InPrivateRoot(async root =>
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var server = Task.Run(async () =>
            {
                var replies = new List<Task>();
                for (var i = 0; i < 4; i++)
                {
                    var peer = await listener.AcceptAsync(deadline.Token);
                    replies.Add(Echo(peer));
                }
                await Task.WhenAll(replies);
            });
            async Task Echo(Stream peer)
            {
                await using (peer)
                {
                    var bytes = new byte[131072];
                    await peer.ReadExactlyAsync(bytes, deadline.Token);
                    await peer.WriteAsync(bytes, deadline.Token);
                }
            }
            await Task.WhenAll(Enumerable.Range(1, 4).Select(async id =>
            {
                await using var client = await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint, deadline.Token);
                var sent = Enumerable.Repeat((byte)id, 131072).ToArray();
                await client.WriteAsync(sent, deadline.Token);
                var received = new byte[sent.Length];
                await client.ReadExactlyAsync(received, deadline.Token);
                Assert.Equal(sent, received);
            }));
            await server;
            await listener.DisposeAsync();
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        });
    }

    [Fact]
    public async Task Disposal_JoinsPendingReadBlockedWriteAndWriteGateWaiter()
    {
        await InPrivateRoot(async root =>
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var accept = listener.AcceptAsync(deadline.Token);
            await using var client = await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint, deadline.Token);
            await using var server = await accept;
            var writer = client.WriteAsync(new byte[1048576], deadline.Token).AsTask();
            Assert.Equal(1, await server.ReadAsync(new byte[1], deadline.Token));
            Assert.False(writer.IsCompleted);
            var queuedWriter = client.WriteAsync(new byte[1], deadline.Token).AsTask();
            var reader = client.ReadAsync(new byte[1], deadline.Token).AsTask();
            await client.DisposeAsync();
            Assert.True(reader.IsCompleted);
            Assert.True(writer.IsCompleted);
            Assert.True(queuedWriter.IsCompleted);
            foreach (var operation in new Task[] { reader, writer, queuedWriter })
            {
                var error = await Record.ExceptionAsync(() => operation);
                Assert.True(error is OperationCanceledException or ObjectDisposedException);
            }
            await server.DisposeAsync();
            await listener.DisposeAsync();
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        });
    }

    [Fact]
    public async Task BrokenReader_ReportsEpipeWithoutTerminatingCurrentTestHost()
    {
        await InPrivateRoot(async root =>
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var accept = listener.AcceptAsync(deadline.Token);
            await using var client = await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint, deadline.Token);
            await using var server = await accept;
            await server.DisposeAsync();
            var error = await Assert.ThrowsAsync<LinuxBridgeFifoNative.NativeFailure>(() => client.WriteAsync(new byte[1], deadline.Token).AsTask());
            Assert.Equal(32, error.ErrorNumber);
            await client.DisposeAsync();
            await listener.DisposeAsync();
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        });
    }

    [Fact]
    public async Task WrongRunSymlinkAndStaleEndpoint_AreRefused()
    {
        await InPrivateRoot(async root =>
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            await Assert.ThrowsAnyAsync<IOException>(() => LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint with { RunId = new string('0', 32) }));
            var otherRun = Guid.NewGuid().ToString("N");
            var alias = Path.Combine(root, "boe-bridge-" + otherRun);
            Directory.CreateSymbolicLink(alias, listener.Endpoint.DirectoryPath);
            try { await Assert.ThrowsAnyAsync<IOException>(() => LinuxBridgeFifoClient.ConnectAsync(new(alias, otherRun))); }
            finally { Directory.Delete(alias); }
            await listener.DisposeAsync();
            await Assert.ThrowsAnyAsync<IOException>(() => LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint));
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        });
    }

    [Fact]
    public async Task LostClientBeforeHello_CleansPeer_AndReplayCannotRecreateIt()
    {
        await InPrivateRoot(async root =>
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var nonce = Guid.NewGuid().ToString("N");
            var peerPath = Path.Combine(listener.Endpoint.DirectoryPath, "peer-" + nonce);
            SendTicket(listener.Endpoint, nonce);
            await Until(() => Directory.Exists(peerPath), deadline.Token);
            await Until(() => !Directory.Exists(peerPath), deadline.Token);
            SendTicket(listener.Endpoint, nonce);
            // Following valid ticket provides an ordering witness that replay was consumed.
            var accept = listener.AcceptAsync(deadline.Token);
            await using var client = await LinuxBridgeFifoClient.ConnectAsync(listener.Endpoint, deadline.Token);
            await using var server = await accept;
            Assert.False(Directory.Exists(peerPath));
            await client.DisposeAsync(); await server.DisposeAsync();
            await listener.DisposeAsync();
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        });
    }

    [Fact]
    public async Task PendingHandshakeCapacity_IsBounded_AndShutdownCleansAllAdmittedPeers()
    {
        await InPrivateRoot(async root =>
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            for (var i = 0; i < 49; i++) SendTicket(listener.Endpoint, Guid.NewGuid().ToString("N"));
            await Until(() => Directory.EnumerateDirectories(listener.Endpoint.DirectoryPath).Count() == 48, deadline.Token);
            Assert.Equal(48, Directory.EnumerateDirectories(listener.Endpoint.DirectoryPath).Count());
            await listener.DisposeAsync();
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        });
    }

    [Fact]
    public async Task MalformedTicket_FailsClosed_AndDisposalRemovesRun()
    {
        await InPrivateRoot(async root =>
        {
            await using var listener = LinuxBridgeFifoListener.Create(root);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using (var run = LinuxBridgeFifoNative.OpenDirectory(listener.Endpoint.DirectoryPath))
            using (var ticket = LinuxBridgeFifoNative.OpenFifo(run, "tickets", LinuxBridgeFifoNative.WriteOnly))
            {
                Assert.Equal(2, LinuxBridgeFifoNative.Write(ticket, [0, 10], 2, out _));
            }
            await Assert.ThrowsAsync<System.Threading.Channels.ChannelClosedException>(() => listener.AcceptAsync(deadline.Token));
            await listener.DisposeAsync();
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        });
    }

    private static void SendTicket(LinuxBridgeFifoEndpoint endpoint, string nonce)
    {
        using var run = LinuxBridgeFifoNative.OpenDirectory(endpoint.DirectoryPath);
        using var ticket = LinuxBridgeFifoNative.OpenFifo(run, "tickets", LinuxBridgeFifoNative.WriteOnly);
        var bytes = Encoding.ASCII.GetBytes($"TICKET 1 {endpoint.RunId} {nonce}\n");
        Assert.Equal(bytes.Length, LinuxBridgeFifoNative.Write(ticket, bytes, bytes.Length, out _));
    }
    private static async Task Until(Func<bool> condition, CancellationToken token)
    {
        while (!condition()) await Task.Delay(5, token);
    }
    private static async Task InPrivateRoot(Func<string, Task> body)
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-fifo", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { await body(root); }
        finally { Directory.Delete(root, recursive: true); }
    }
}
