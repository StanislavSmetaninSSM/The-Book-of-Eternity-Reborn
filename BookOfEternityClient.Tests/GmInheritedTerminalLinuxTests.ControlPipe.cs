using System.IO.Pipes;
using System.Runtime.InteropServices;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmInheritedTerminalLinuxTests
{
    [Fact]
    public async Task ActualFullPipe_CancelledByteAndAbortDoNotAppendOrMaskEof()
    {
        Assert.True(OperatingSystem.IsLinux());
        using var reader = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None);
        var descriptor = DuplicatePipeWriter(reader.ClientSafePipeHandle, 1030, 3);
        Assert.True(descriptor >= 0);
        using var writerHandle = new SafePipeHandle((IntPtr)descriptor, true);
        using var writer = new AnonymousPipeClientStream(PipeDirection.Out, writerHandle);
        reader.DisposeLocalCopyOfClientHandle();
        using var control = new InheritedTerminalControl(writer);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var filler = Enumerable.Repeat((byte)'x', writer.OutBufferSize).ToArray();
        await control.WriteAsync(filler, lifetime.Token);
        using var cancellation = new CancellationTokenSource();
        var pending = control.WriteAsync(new byte[] { (byte)'A' }, cancellation.Token).AsTask();
        try
        {
            await Task.Delay(50, lifetime.Token);
            Assert.False(pending.IsCompleted, "Full pipe must actually block the new byte before cancellation.");
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(2)));
            control.Abort();
            control.Abort(); // Idempotent, no retry/flush.
            using var observed = new MemoryStream();
            await reader.CopyToAsync(observed, lifetime.Token);
            Assert.Equal(filler, observed.ToArray());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => control.WriteAsync(new byte[] { (byte)'A' }, lifetime.Token).AsTask());
        }
        finally
        {
            cancellation.Cancel();
            control.Abort();
            reader.Dispose();
            try { await pending.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
        }
    }

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int DuplicatePipeWriter(SafePipeHandle handle, int command, int minimum);
}
