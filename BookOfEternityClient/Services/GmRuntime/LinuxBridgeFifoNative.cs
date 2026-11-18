using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternityClient.Services.GmRuntime;

// Trusted same-UID local transport. Anchoring prevents accidental parent replacement;
// it does not turn stat/unlink into atomic defense against a hostile same-UID process.
internal static class LinuxBridgeFifoNative
{
    internal const int ReadOnly = 0, WriteOnly = 1, ReadWrite = 2;
    private const int NoFollow = 0x20000, DirectoryFlag = 0x10000, CloseOnExec = 0x80000, NonBlock = 0x800;
    internal sealed record Identity(ulong Device, ulong Inode, uint Uid, ushort Mode, uint Links);

    internal static void RequirePlatform()
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Bridge FIFO ABI is qualified only on Linux x64.");
    }

    internal static SafeFileHandle OpenDirectory(string path)
    {
        RequirePlatform();
        if (!Path.IsPathFullyQualified(path) || path.IndexOf('\0') >= 0)
            throw new IOException("Absolute bridge parent path required.");
        var current = OpenAt(null, "/", ReadOnly | DirectoryFlag);
        try
        {
            foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                Name(part);
                var next = OpenAt(current, part, ReadOnly | DirectoryFlag);
                current.Dispose(); current = next;
            }
            return current;
        }
        catch { current.Dispose(); throw; }
    }

    internal static SafeFileHandle OpenPrivateDirectory(SafeFileHandle parent, string name)
    {
        Name(name); var fd = OpenAt(parent, name, ReadOnly | DirectoryFlag);
        try { RequirePrivate(fd, 0x4000, 0x1c0); return fd; }
        catch { fd.Dispose(); throw; }
    }

    internal static SafeFileHandle CreateDirectory(SafeFileHandle parent, string name, Action? afterCreation = null)
    {
        Name(name);
        if (mkdirat(parent, name, 0x1c0) != 0) throw Error("mkdirat");
        Identity? original = null;
        try
        {
            original = StatAt(parent, name, 0x100);
            afterCreation?.Invoke();
            return OpenPrivateDirectory(parent, name);
        }
        catch (Exception failure)
        {
            try { RemoveCreated(parent, name, original, directory: true); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    internal static Identity CreateFifo(SafeFileHandle directory, string name, Action? afterCreation = null)
    {
        Name(name);
        if (mkfifoat(directory, name, 0x180) != 0) throw Error("mkfifoat");
        Identity? original = null;
        try
        {
            original = StatAt(directory, name, 0x100);
            afterCreation?.Invoke();
            using var fd = OpenFifo(directory, name, ReadOnly);
            return Stat(fd);
        }
        catch (Exception failure)
        {
            try { RemoveCreated(directory, name, original, directory: false); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    // Shared local singleton primitive. No PID, process registry or publication authority.
    internal static SafeFileHandle OpenOrCreateGuardDirectory(SafeFileHandle parent, string name)
    {
        Name(name);
        if (mkdirat(parent, name, 0x1c0) != 0 && Marshal.GetLastPInvokeError() != 17)
            throw Error("mkdirat guard parent");
        return OpenAt(parent, name, ReadOnly | DirectoryFlag);
    }

    internal static SafeFileHandle OpenGuard(SafeFileHandle parent, string name, out bool created)
    {
        Name(name);
        var fd = openatCreate(parent, name, ReadWrite | NoFollow | CloseOnExec | NonBlock | 0x40 | 0x80, 0x180);
        created = fd >= 0;
        if (fd < 0 && Marshal.GetLastPInvokeError() == 17)
            fd = openat(parent, name, ReadWrite | NoFollow | CloseOnExec | NonBlock);
        if (fd < 0) throw Error("openat guard");
        var handle = new SafeFileHandle((IntPtr)fd, true);
        try
        {
            RequirePrivate(handle, 0x8000, 0x180);
            if (Stat(handle).Links != 1) throw new IOException("Linked guard refused.");
            if (flock(handle, 2 | 4) != 0) throw Error("flock guard");
            RequireNamedIdentity(parent, name, handle);
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    internal static void RequireNamedIdentity(SafeFileHandle parent, string name, SafeFileHandle held)
    {
        Name(name);
        if (StatAt(parent, name, 0x100) != Stat(held)) throw new IOException("Guard namespace identity changed.");
    }

    internal static SafeFileHandle OpenFifo(SafeFileHandle directory, string name, int mode)
    {
        Name(name); var fd = OpenAt(directory, name, mode);
        try { RequirePrivate(fd, 0x1000, 0x180); if (Stat(fd).Links != 1) throw new IOException("Linked FIFO refused."); return fd; }
        catch { fd.Dispose(); throw; }
    }

    internal static bool Missing(IOException error) => error is NativeFailure { ErrorNumber: 2 };
    internal static bool NoReader(IOException error) => error is NativeFailure { ErrorNumber: 6 };

    internal static void RemoveFifo(SafeFileHandle directory, string name, Identity original) =>
        RemoveCreated(directory, name, original, directory: false);

    internal static void RemoveDirectory(SafeFileHandle parent, string name, Identity original) =>
        RemoveCreated(parent, name, original, directory: true);

    private static void RemoveCreated(SafeFileHandle parent, string name, Identity? original, bool directory)
    {
        // statx-at needs no new FD, so cleanup still works after descriptor exhaustion.
        Name(name);
        if (original == null) throw new IOException("Created entry ownership could not be captured; cleanup remains uncertain.");
        Identity now;
        try { now = StatAt(parent, name, 0x100); }
        catch (IOException ex) when (Missing(ex)) { return; }
        var type = directory ? 0x4000 : 0x1000;
        if ((original.Mode & 0xf000) != type || original.Uid != geteuid() ||
            now.Device != original.Device || now.Inode != original.Inode || now.Uid != original.Uid || now.Mode != original.Mode ||
            (!directory && (now.Links != 1 || original.Links != 1)))
            throw new IOException("Changed owned entry retained during cleanup.");
        if (unlinkat(parent, name, directory ? 0x200 : 0) != 0 && Marshal.GetLastPInvokeError() != 2) throw Error("unlinkat owned entry");
    }

    internal static Identity Stat(SafeFileHandle fd) => StatAt(fd, "", 0x1000);
    private static Identity StatAt(SafeFileHandle fd, string name, int flags)
    {
        const uint mask = 0x10f;
        if (statx(fd, name, flags, mask, out var value) != 0) throw Error("statx");
        if ((value.Mask & mask) != mask) throw new IOException("Incomplete FIFO identity metadata.");
        return new(((ulong)value.DeviceMajor << 32) | value.DeviceMinor, value.Inode, value.Uid, value.Mode, value.Links);
    }

    private static void RequirePrivate(SafeFileHandle fd, ushort type, ushort permissions)
    {
        var id = Stat(fd);
        if ((id.Mode & 0xf000) != type || (id.Mode & 0xfff) != permissions || id.Uid != geteuid())
            throw new IOException("Bridge entry type, owner or private mode mismatch.");
    }

    private static SafeFileHandle OpenAt(SafeFileHandle? parent, string name, int flags)
    {
        int result;
        if (parent == null) result = open(name, flags | NoFollow | CloseOnExec | NonBlock);
        else result = openat(parent, name, flags | NoFollow | CloseOnExec | NonBlock);
        if (result < 0) throw Error("openat");
        return new SafeFileHandle((IntPtr)result, true);
    }

    internal static void Name(string name)
    {
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.Contains('/') || name.Contains('\0'))
            throw new IOException("Relative bridge basename required.");
    }

    internal static int Read(SafeFileHandle fd, byte[] buffer, int count, out int error)
    {
        var n = read(fd, buffer, (nuint)count); error = n < 0 ? Marshal.GetLastPInvokeError() : 0; return checked((int)n);
    }

    internal static int Write(SafeFileHandle fd, byte[] buffer, int count, out int error)
    {
        // CoreCLR8 initializes SIGPIPE=SIG_IGN. Observe, never mutate it. This
        // trusted-runtime prerequisite excludes concurrent foreign handler changes.
        if (sigaction(13, IntPtr.Zero, out var action) != 0 || action.Handler != (IntPtr)1)
            throw new IOException("Bridge FIFO requires the existing CoreCLR SIGPIPE-ignore disposition.");
        var n = write(fd, buffer, (nuint)count); error = n < 0 ? Marshal.GetLastPInvokeError() : 0; return checked((int)n);
    }

    internal sealed class NativeFailure(string operation, int number) : IOException($"Bridge FIFO {operation} failed (errno {number}).")
    { internal int ErrorNumber { get; } = number; }
    private static IOException Error(string operation) => new NativeFailure(operation, Marshal.GetLastPInvokeError());

    [StructLayout(LayoutKind.Explicit, Size = 256)] private struct Statx
    {
        [FieldOffset(0)] public uint Mask;
        [FieldOffset(16)] public uint Links;
        [FieldOffset(20)] public uint Uid;
        [FieldOffset(28)] public ushort Mode;
        [FieldOffset(32)] public ulong Inode;
        [FieldOffset(136)] public uint DeviceMajor;
        [FieldOffset(140)] public uint DeviceMinor;
    }
    [StructLayout(LayoutKind.Explicit, Size = 152)] private struct SignalAction { [FieldOffset(0)] public IntPtr Handler; }
    [DllImport("libc", SetLastError = true)] private static extern int open(string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int openat(SafeFileHandle directory, string path, int flags);
    [DllImport("libc", EntryPoint = "openat", SetLastError = true)] private static extern int openatCreate(SafeFileHandle directory, string path, int flags, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int flock(SafeFileHandle descriptor, int operation);
    [DllImport("libc", SetLastError = true)] private static extern int mkdirat(SafeFileHandle directory, string path, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int mkfifoat(SafeFileHandle directory, string path, uint mode);
    [DllImport("libc", SetLastError = true)] private static extern int unlinkat(SafeFileHandle directory, string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern int statx(SafeFileHandle directory, string path, int flags, uint mask, out Statx value);
    [DllImport("libc")] private static extern uint geteuid();
    [DllImport("libc", SetLastError = true)] private static extern nint read(SafeFileHandle fd, byte[] buffer, nuint length);
    [DllImport("libc", SetLastError = true)] private static extern nint write(SafeFileHandle fd, byte[] buffer, nuint length);
    [DllImport("libc", SetLastError = true)] private static extern int sigaction(int signal, IntPtr action, out SignalAction previous);
}
