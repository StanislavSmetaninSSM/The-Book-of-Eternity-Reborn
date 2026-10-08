using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services;

internal static class LinuxClipboardReaderIdentity
{
    internal static bool Available()
    {
        try
        {
            var fd = Open(Environment.ProcessId, 0);
            if (fd < 0) return false;
            using var self = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
            return Signal(self, 0, IntPtr.Zero, 0) == 0;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException) { return false; }
    }

    internal static SafeFileHandle? Capture(Process original)
    {
        SafeFileHandle? descriptor = null;
        try
        {
            var fd = Open(original.Id, 0);
            if (fd < 0) return null;
            descriptor = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
            // .NET8.0.31: new self HasExited creates a Holder through the child-table
            // lock held across waitpid+exit caching (including reapAll). Bare self
            // construction does not. Then the retained original child's cache
            // rejects any descriptor opened after its PID was recycled.
            using var self = Process.GetCurrentProcess();
            _ = self.HasExited;
            if (original.HasExited) { descriptor.Dispose(); return null; }
            return descriptor;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException or InvalidOperationException)
        { descriptor?.Dispose(); return null; }
    }

    internal static void Stop(SafeFileHandle descriptor)
    {
        // No numeric PID or process-tree fallback. A concurrent exit makes this
        // identity unaddressable (ESRCH), never redirects a signal to a reused PID.
        try { _ = Signal(descriptor, 9, IntPtr.Zero, 0); }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException) { }
    }

    [DllImport("libc", EntryPoint = "pidfd_open", SetLastError = true)]
    private static extern int Open(int pid, uint flags);
    [DllImport("libc", EntryPoint = "pidfd_send_signal", SetLastError = true)]
    private static extern int Signal(SafeFileHandle descriptor, int signal, IntPtr info, uint flags);
}
