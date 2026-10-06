using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

// Nonblocking owned descriptors. A finite poll slice observes cancellation inside
// the actual operation; no blocking read is abandoned behind a cancelled waiter.
internal sealed class LinuxPtyStream : Stream
{
    private readonly SafeFileHandle _fd;
    private readonly bool _write;
    private readonly Action<string> _fault;
    private readonly object _state = new();
    private int _active;
    private bool _sealed;
    private TaskCompletionSource _idle = Completed();
    private readonly TaskCompletionSource _eof = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource Completed() { var t=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);t.SetResult();return t; }
    internal Task Settlement => ObserveSettlementAsync();
    private async Task ObserveSettlementAsync() { if(!_write)await _eof.Task; Task idle;lock(_state)idle=_idle.Task;await idle; }
    internal void SealInput() { lock(_state) _sealed=true; }
    private void Enter() { lock(_state) { if(_sealed && (_write || !_eof.Task.IsCompleted))throw new IOException("PTY operation admission closed."); if(_active++==0)_idle=new(TaskCreationOptions.RunContinuationsAsynchronously); } }
    private void Leave() { lock(_state) { if(--_active==0)_idle.TrySetResult(); } }
    private readonly SemaphoreSlim _gate = new(1);
    internal LinuxPtyStream(SafeFileHandle fd, bool write, Action<string> fault) { _fd = fd; _write = write; _fault = fault; }
    public override bool CanRead => !_write;
    public override bool CanWrite => _write;
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override Task FlushAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
    public override long Seek(long o, SeekOrigin s) => throw new NotSupportedException();
    public override void SetLength(long v) => throw new NotSupportedException();
    public override int Read(byte[] b, int o, int c) => ReadAsync(b.AsMemory(o,c)).AsTask().GetAwaiter().GetResult();
    public override void Write(byte[] b, int o, int c) => WriteAsync(b.AsMemory(o,c)).AsTask().GetAwaiter().GetResult();
    public override Task<int> ReadAsync(byte[] b,int o,int c,CancellationToken t) => ReadAsync(b.AsMemory(o,c),t).AsTask();
    public override Task WriteAsync(byte[] b,int o,int c,CancellationToken t) => WriteAsync(b.AsMemory(o,c),t).AsTask();
    public override async ValueTask<int> ReadAsync(Memory<byte> bytes, CancellationToken token = default)
    {
        if (_write) throw new NotSupportedException();
        if (bytes.Length == 0 || _eof.Task.IsCompletedSuccessfully) return 0;
        Enter();
        var heldGate=false;
        try { await _gate.WaitAsync(token); heldGate=true; return await Task.Run(() => Transfer(bytes, false, token), CancellationToken.None); }
        finally { if(heldGate)_gate.Release(); Leave(); }
    }
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken token = default)
    {
        if (!_write) throw new NotSupportedException();
        Enter();
        var heldGate=false;
        try
        {
            await _gate.WaitAsync(token); heldGate=true;
            var copy = bytes.ToArray(); var used = 0;
            while (used < copy.Length) used += await Task.Run(() => Transfer(copy.AsMemory(used), true, token), CancellationToken.None);
        }
        finally { if(heldGate)_gate.Release(); Leave(); }
    }
    private int Transfer(Memory<byte> bytes, bool write, CancellationToken token)
    {
        var held = false;
        var buffer = new byte[Math.Min(bytes.Length, 4096)];
        if (write) bytes.Span[..buffer.Length].CopyTo(buffer);
        var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            _fd.DangerousAddRef(ref held);
            var fd = _fd.DangerousGetHandle().ToInt32();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var n = write ? NativeWrite(fd, pin.AddrOfPinnedObject(), (nuint)buffer.Length) : NativeRead(fd, pin.AddrOfPinnedObject(), (nuint)buffer.Length);
                if (n >= 0) { if (write && n == 0) throw new IOException("PTY zero write."); if (!write) { if(n==0)_eof.TrySetResult(); buffer.AsSpan(0, checked((int)n)).CopyTo(bytes.Span); } return checked((int)n); }
                var e = Marshal.GetLastPInvokeError();
                if (!write && e == 5) { _eof.TrySetResult(); return 0; } // established slave hangup only; never scoped stop proof
                if (e == 4) continue;
                if (e != 11) throw new IOException("PTY I/O errno=" + e);
                var p = new PollFd { Fd = fd, Events = (short)(write ? 4 : 1) };
                if (Poll(ref p, 1, 20) < 0 && Marshal.GetLastPInvokeError() != 4) throw new IOException("PTY poll failed.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { _fault("terminal-io-fault"); throw; }
        finally { pin.Free(); if (held) _fd.DangerousRelease(); }
    }
    internal void Resize(TerminalSize size)
    {
        if (size.Columns is < 1 or > 32767 || size.Rows is < 1 or > 32767) throw new ArgumentOutOfRangeException(nameof(size));
        var w = new WinSize { Columns = (ushort)size.Columns, Rows = (ushort)size.Rows };
        if (Ioctl(_fd, 0x5414, ref w) != 0) { _fault("terminal-resize-fault"); throw new IOException("PTY resize failed."); }
    }
    protected override void Dispose(bool disposing) {
        if(disposing) { lock(_state) { if(_active!=0 || (!_write && !_eof.Task.IsCompletedSuccessfully)) throw new InvalidOperationException("PTY actual I/O/EOF is unsettled."); _sealed=true; _fd.Dispose(); _gate.Dispose(); } } base.Dispose(disposing);
    }
    [StructLayout(LayoutKind.Sequential)] private struct PollFd { internal int Fd; internal short Events, Returned; }
    [StructLayout(LayoutKind.Sequential)] private struct WinSize { internal ushort Rows, Columns, X, Y; }
    [DllImport("libc",EntryPoint="read",SetLastError=true)] private static extern long NativeRead(int fd,IntPtr data,nuint n);
    [DllImport("libc",EntryPoint="write",SetLastError=true)] private static extern long NativeWrite(int fd,IntPtr data,nuint n);
    [DllImport("libc",EntryPoint="poll",SetLastError=true)] private static extern int Poll(ref PollFd fd,nuint count,int timeout);
    [DllImport("libc",EntryPoint="ioctl",SetLastError=true)] private static extern int Ioctl(SafeFileHandle fd,uint request,ref WinSize size);
}
