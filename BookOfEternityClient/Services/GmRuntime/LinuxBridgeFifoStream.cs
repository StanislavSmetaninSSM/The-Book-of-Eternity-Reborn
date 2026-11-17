using System.Buffers;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

// Nonblocking syscalls only. No abandoned blocking Task.Run or dummy writer masks EOF.
internal sealed class LinuxBridgeFifoStream(SafeFileHandle reader, SafeFileHandle writer, Action? beforeHandlesClose = null) : Stream
{
    private readonly SemaphoreSlim _readGate = new(1), _writeGate = new(1);
    private readonly CancellationTokenSource _stop = new();
    private readonly object _lifetime = new();
    private readonly TaskCompletionSource _idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _operations;
    private bool _disposed;
    public override bool CanRead => !_disposed;
    public override bool CanWrite => !_disposed;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { ObjectDisposedException.ThrowIf(_disposed, this); }
    public override Task FlushAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); Flush(); return Task.CompletedTask; }
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long length) => throw new NotSupportedException();
    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    public override void Write(byte[] buffer, int offset, int count) => WriteAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => ReadAsync(buffer.AsMemory(offset, count), token).AsTask();
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) => WriteAsync(buffer.AsMemory(offset, count), token).AsTask();
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => ReadCore(buffer, false, token);
    internal ValueTask<int> ReadStartupAsync(Memory<byte> buffer, CancellationToken token) => ReadCore(buffer, true, token);

    private async ValueTask<int> ReadCore(Memory<byte> output, bool startup, CancellationToken token)
    {
        Begin();
        try
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
            await _readGate.WaitAsync(cancel.Token).ConfigureAwait(false);
            try
            {
                if (output.IsEmpty) return 0;
                var bytes = ArrayPool<byte>.Shared.Rent(Math.Min(output.Length, 65536));
                try
                {
                    while (true)
                    {
                        cancel.Token.ThrowIfCancellationRequested();
                        var count = LinuxBridgeFifoNative.Read(reader, bytes, Math.Min(output.Length, bytes.Length), out var error);
                        if (count > 0) { bytes.AsMemory(0, count).CopyTo(output); return count; }
                        if (count == 0 && !startup) return 0;
                        if (count < 0 && error != 4 && error != 11) throw new LinuxBridgeFifoNative.NativeFailure("read", error);
                        await Task.Delay(5, cancel.Token).ConfigureAwait(false);
                    }
                }
                finally { ArrayPool<byte>.Shared.Return(bytes); }
            }
            finally { _readGate.Release(); }
        }
        finally { End(); }
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token = default)
    {
        Begin();
        try
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token, _stop.Token);
            await _writeGate.WaitAsync(cancel.Token).ConfigureAwait(false);
            try
            {
                var bytes = ArrayPool<byte>.Shared.Rent(Math.Min(Math.Max(input.Length, 1), 65536));
                try
                {
                    var offset = 0;
                    while (offset < input.Length)
                    {
                        cancel.Token.ThrowIfCancellationRequested();
                        var size = Math.Min(bytes.Length, input.Length - offset);
                        input.Slice(offset, size).CopyTo(bytes);
                        var count = LinuxBridgeFifoNative.Write(writer, bytes, size, out var error);
                        if (count > 0) { offset += count; continue; }
                        if (count < 0 && error != 4 && error != 11) throw new LinuxBridgeFifoNative.NativeFailure("write", error);
                        await Task.Delay(5, cancel.Token).ConfigureAwait(false);
                    }
                }
                finally { ArrayPool<byte>.Shared.Return(bytes); }
            }
            finally { _writeGate.Release(); }
        }
        finally { End(); }
    }

    private void Begin()
    {
        lock (_lifetime) { ObjectDisposedException.ThrowIf(_disposed, this); _operations++; }
    }
    private void End()
    {
        lock (_lifetime) { _operations--; if (_disposed && _operations == 0) _idle.TrySetResult(); }
    }
    protected override void Dispose(bool disposing)
    {
        lock (_lifetime)
        {
            if (_disposed) return;
            _disposed = true;
            if (_operations == 0) _idle.TrySetResult();
        }
        // Cancellation callbacks may complete operations; never invoke under lifetime lock.
        _stop.Cancel();
        beforeHandlesClose?.Invoke();
        reader.Dispose(); writer.Dispose();
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        Dispose();
        await _idle.Task.ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
