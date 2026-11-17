using System.Text;
using System.Threading.Channels;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

internal sealed record LinuxBridgeFifoEndpoint(string DirectoryPath, string RunId, int ProtocolVersion = 1);

internal sealed class LinuxBridgeFifoListener : IAsyncDisposable
{
    private readonly SafeFileHandle _parent, _run;
    private readonly LinuxBridgeFifoNative.Identity _runIdentity, _ticketIdentity;
    private readonly string _name;
    private readonly LinuxBridgeFifoStream _tickets;
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _slots = new(48);
    private readonly Channel<Stream> _ready = Channel.CreateBounded<Stream>(48);
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private readonly List<Task> _pending = [];
    private readonly Task _pump;
    private readonly Action? _beforeReadyEnqueue;
    private readonly object _disposeGate = new();
    private Task? _dispose;
    internal LinuxBridgeFifoEndpoint Endpoint { get; }

    private LinuxBridgeFifoListener(SafeFileHandle parent, SafeFileHandle run, string name,
        LinuxBridgeFifoEndpoint endpoint, LinuxBridgeFifoNative.Identity ticketIdentity,
        LinuxBridgeFifoStream tickets, Action? beforeReadyEnqueue)
    {
        _beforeReadyEnqueue = beforeReadyEnqueue;
        _parent = parent; _run = run; _name = name; Endpoint = endpoint;
        _runIdentity = LinuxBridgeFifoNative.Stat(run); _ticketIdentity = ticketIdentity; _tickets = tickets;
        _pump = PumpAsync();
    }

    internal static LinuxBridgeFifoListener Create(string parentDirectory, Action? afterTicketCreated = null, Action? beforeReadyEnqueue = null)
    {
        var parent = LinuxBridgeFifoNative.OpenDirectory(parentDirectory);
        var runId = Guid.NewGuid().ToString("N"); var name = "boe-bridge-" + runId;
        SafeFileHandle? run = null, read = null, write = null;
        try
        {
            run = LinuxBridgeFifoNative.CreateDirectory(parent, name);
            var ticketIdentity = LinuxBridgeFifoNative.CreateFifo(run, "tickets");
            afterTicketCreated?.Invoke();
            // Only rendezvous has a lifetime RDWR anchor. Each peer is unidirectional.
            read = LinuxBridgeFifoNative.OpenFifo(run, "tickets", LinuxBridgeFifoNative.ReadWrite);
            write = LinuxBridgeFifoNative.OpenFifo(run, "tickets", LinuxBridgeFifoNative.WriteOnly);
            var tickets = new LinuxBridgeFifoStream(read, write); read = write = null;
            return new(parent, run, name, new(Path.Combine(parentDirectory, name), runId), ticketIdentity, tickets, beforeReadyEnqueue);
        }
        catch { read?.Dispose(); write?.Dispose(); run?.Dispose(); parent.Dispose(); throw; }
    }

    internal async Task<Stream> AcceptAsync(CancellationToken token = default)
    {
        var stream = await _ready.Reader.ReadAsync(token).ConfigureAwait(false);
        _slots.Release(); return stream;
    }

    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                var line = await LinuxBridgeFifoProtocol.LineAsync(_tickets, false, _stop.Token).ConfigureAwait(false);
                var parts = line.Split(' ');
                if (parts.Length != 4 || parts[0] != "TICKET" || parts[1] != "1" || parts[2] != Endpoint.RunId || !LinuxBridgeFifoProtocol.Nonce(parts[3])) continue;
                var nonce = parts[3];
                if (_seen.Contains(nonce)) continue;
                if (_seen.Count >= 65536) throw new IOException("FIFO lifetime ticket capacity exhausted; restart listener explicitly.");
                _seen.Add(nonce);
                await _slots.WaitAsync(_stop.Token).ConfigureAwait(false);
                _pending.RemoveAll(task => task.IsCompletedSuccessfully);
                _pending.Add(AdmitAsync(nonce));
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) { _ready.Writer.TryComplete(ex); _stop.Cancel(); }
        finally { _ready.Writer.TryComplete(); }
    }

    private async Task AdmitAsync(string nonce)
    {
        LinuxBridgeFifoStream? stream = null;
        SafeFileHandle? peer = null, read = null, write = null;
        LinuxBridgeFifoNative.Identity? peerId = null, c2s = null, s2c = null;
        var name = "peer-" + nonce;
        var delivered = false;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            peer = LinuxBridgeFifoNative.CreateDirectory(_run, name); peerId = LinuxBridgeFifoNative.Stat(peer);
            c2s = LinuxBridgeFifoNative.CreateFifo(peer, "c2s"); s2c = LinuxBridgeFifoNative.CreateFifo(peer, "s2c");
            read = LinuxBridgeFifoNative.OpenFifo(peer, "c2s", LinuxBridgeFifoNative.ReadOnly);
            write = await LinuxBridgeFifoProtocol.OpenWriterAsync(peer, "s2c", timeout.Token).ConfigureAwait(false);
            stream = new(read, write); read = write = null;
            await LinuxBridgeFifoProtocol.SendAsync(stream, $"HELLO 1 {Endpoint.RunId} {nonce}", timeout.Token).ConfigureAwait(false);
            var ack = await LinuxBridgeFifoProtocol.LineAsync(stream, true, timeout.Token).ConfigureAwait(false);
            if (ack != $"ACK 1 {Endpoint.RunId} {nonce}") throw new IOException("FIFO acknowledgement mismatch.");
            LinuxBridgeFifoNative.RemoveFifo(peer, "c2s", c2s); c2s = null;
            LinuxBridgeFifoNative.RemoveFifo(peer, "s2c", s2c); s2c = null;
            await LinuxBridgeFifoProtocol.SendAsync(stream, $"READY 1 {Endpoint.RunId} {nonce}", timeout.Token).ConfigureAwait(false);
            _beforeReadyEnqueue?.Invoke();
            await _ready.Writer.WriteAsync(stream, timeout.Token).ConfigureAwait(false);
            delivered = true; stream = null;
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException) { /* Refusal never grants a connection. */ }
        finally
        {
            if (stream != null) await stream.DisposeAsync().ConfigureAwait(false);
            read?.Dispose(); write?.Dispose();
            try
            {
                if (peer != null)
                {
                    if (c2s != null) LinuxBridgeFifoNative.RemoveFifo(peer, "c2s", c2s);
                    if (s2c != null) LinuxBridgeFifoNative.RemoveFifo(peer, "s2c", s2c);
                    if (peerId != null) LinuxBridgeFifoNative.RemoveDirectory(_run, name, peerId);
                }
            }
            catch (IOException ex) { _ready.Writer.TryComplete(ex); _stop.Cancel(); }
            finally { peer?.Dispose(); if (!delivered) _slots.Release(); }
        }
    }

    public ValueTask DisposeAsync()
    {
        lock (_disposeGate) return new(_dispose ??= DisposeCoreAsync());
    }
    private async Task DisposeCoreAsync()
    {
        _stop.Cancel();
        await _tickets.DisposeAsync().ConfigureAwait(false);
        await _pump.ConfigureAwait(false);
        await Task.WhenAll(_pending).ConfigureAwait(false);
        while (_ready.Reader.TryRead(out var stream)) await stream.DisposeAsync().ConfigureAwait(false);
        try
        {
            LinuxBridgeFifoNative.RemoveFifo(_run, "tickets", _ticketIdentity);
            LinuxBridgeFifoNative.RemoveDirectory(_parent, _name, _runIdentity);
        }
        finally { _run.Dispose(); _parent.Dispose(); }
    }
}

internal static class LinuxBridgeFifoClient
{
    internal static async Task<Stream> ConnectAsync(LinuxBridgeFifoEndpoint endpoint, CancellationToken token = default)
    {
        if (endpoint.ProtocolVersion != 1 || !LinuxBridgeFifoProtocol.Nonce(endpoint.RunId) ||
            Path.GetFileName(endpoint.DirectoryPath) != "boe-bridge-" + endpoint.RunId)
            throw new IOException("Invalid FIFO endpoint.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var parent = LinuxBridgeFifoNative.OpenDirectory(Path.GetDirectoryName(endpoint.DirectoryPath)!);
        using var run = LinuxBridgeFifoNative.OpenPrivateDirectory(parent, Path.GetFileName(endpoint.DirectoryPath));
        var nonce = Guid.NewGuid().ToString("N");
        using (var tickets = LinuxBridgeFifoNative.OpenFifo(run, "tickets", LinuxBridgeFifoNative.WriteOnly))
        {
            var bytes = Encoding.ASCII.GetBytes($"TICKET 1 {endpoint.RunId} {nonce}\n");
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                var count = LinuxBridgeFifoNative.Write(tickets, bytes, bytes.Length, out var error);
                if (count == bytes.Length) break;
                if (count >= 0 || (error != 4 && error != 11)) throw new IOException("Atomic FIFO ticket write failed.");
                await Task.Delay(5, timeout.Token).ConfigureAwait(false);
            }
        }
        SafeFileHandle? peer = null, read = null, write = null;
        LinuxBridgeFifoStream? stream = null;
        try
        {
            while (peer == null)
            {
                timeout.Token.ThrowIfCancellationRequested();
                try { peer = LinuxBridgeFifoNative.OpenPrivateDirectory(run, "peer-" + nonce); }
                catch (IOException ex) when (LinuxBridgeFifoNative.Missing(ex)) { await Task.Delay(5, timeout.Token).ConfigureAwait(false); }
            }
            while (read == null)
            {
                timeout.Token.ThrowIfCancellationRequested();
                try { read = LinuxBridgeFifoNative.OpenFifo(peer, "s2c", LinuxBridgeFifoNative.ReadOnly); }
                catch (IOException ex) when (LinuxBridgeFifoNative.Missing(ex)) { await Task.Delay(5, timeout.Token).ConfigureAwait(false); }
            }
            write = await LinuxBridgeFifoProtocol.OpenWriterAsync(peer, "c2s", timeout.Token).ConfigureAwait(false);
            stream = new(read, write); read = write = null;
            var hello = await LinuxBridgeFifoProtocol.LineAsync(stream, true, timeout.Token).ConfigureAwait(false);
            if (hello != $"HELLO 1 {endpoint.RunId} {nonce}") throw new IOException("FIFO hello mismatch.");
            await LinuxBridgeFifoProtocol.SendAsync(stream, $"ACK 1 {endpoint.RunId} {nonce}", timeout.Token).ConfigureAwait(false);
            var ready = await LinuxBridgeFifoProtocol.LineAsync(stream, false, timeout.Token).ConfigureAwait(false);
            if (ready != $"READY 1 {endpoint.RunId} {nonce}") throw new IOException("FIFO ready mismatch.");
            var result = stream; stream = null; return result;
        }
        finally
        {
            if (stream != null) await stream.DisposeAsync().ConfigureAwait(false);
            read?.Dispose(); write?.Dispose(); peer?.Dispose();
        }
    }
}

internal static class LinuxBridgeFifoProtocol
{
    internal static bool Nonce(string value) => value.Length == 32 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static async Task<SafeFileHandle> OpenWriterAsync(SafeFileHandle directory, string name, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try { return LinuxBridgeFifoNative.OpenFifo(directory, name, LinuxBridgeFifoNative.WriteOnly); }
            catch (IOException ex) when (LinuxBridgeFifoNative.NoReader(ex) || LinuxBridgeFifoNative.Missing(ex))
            { await Task.Delay(5, token).ConfigureAwait(false); }
        }
    }
    internal static Task SendAsync(Stream stream, string line, CancellationToken token) => stream.WriteAsync(Encoding.ASCII.GetBytes(line + "\n"), token).AsTask();
    internal static async Task<string> LineAsync(LinuxBridgeFifoStream stream, bool startup, CancellationToken token)
    {
        var bytes = new byte[512]; var one = new byte[1]; var count = 0;
        while (count < bytes.Length)
        {
            var n = startup ? await stream.ReadStartupAsync(one, token).ConfigureAwait(false) : await stream.ReadAsync(one, token).ConfigureAwait(false);
            if (n == 0) throw new IOException("FIFO handshake channel closed.");
            if (one[0] == 10) return Encoding.ASCII.GetString(bytes, 0, count);
            if (one[0] is < 32 or > 126) throw new IOException("FIFO ticket is not bounded ASCII.");
            bytes[count++] = one[0];
        }
        throw new IOException("FIFO handshake line exceeds bound.");
    }
}
