using System.Runtime.InteropServices;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Core;

// Standalone metadata I/O. Never enters FileSystemManager's canonical recovery machinery.
internal sealed class WorkerRunLedgerPersistence : IDisposable
{
    internal const int MaximumStateBytes = 4 * 1024 * 1024;
    internal static bool Supported => OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64;
    private readonly TrustedLocalFileScope _scope;
    private readonly Descriptor _owner, _journal;
    private readonly WorkerLedgerTarget _target;
    internal bool CreatedNamespace { get; }

    private WorkerRunLedgerPersistence(WorkerLedgerTarget target, TrustedLocalFileScope scope,
        Descriptor owner, Descriptor journal, bool created)
    { _target = target; _scope = scope; _owner = owner; _journal = journal; CreatedNamespace = created; }

    internal static WorkerRunLedgerPersistence Open(WorkerLedgerTarget target)
    {
        if (!Supported) throw new PlatformNotSupportedException("Worker ledger durability adapter is unavailable.");
        var scope = Scope(target);
        var kind = scope.ObserveNamespace(target.DirectoryPath);
        if (kind.BlockingFileAncestor != null) throw Invalid();
        var fresh = kind.Kind == TrustedLocalNamespaceKind.Missing;
        Descriptor? owner = null, journal = null;
        try
        {
            if (fresh)
            {
                var runtime = Path.GetDirectoryName(target.DirectoryPath)!;
                if (scope.ObserveNamespace(runtime).Kind == TrustedLocalNamespaceKind.Missing)
                    CreateDirectory(scope, runtime);
                else scope.ValidateDirectory(runtime, allowMissing: false);
                // mkdir, not CreateDirectory: only the original successful creator may bootstrap.
                CreateDirectory(scope, target.DirectoryPath);
            }
            else scope.ValidateDirectory(target.DirectoryPath, allowMissing: false);
            owner = Descriptor.OpenLock(scope.ValidateFile(Path.Combine(target.DirectoryPath, "owner.lock"), fresh), fresh);
            owner.Lock();
            journal = Descriptor.OpenLock(scope.ValidateFile(Path.Combine(target.DirectoryPath, "journal.lock"), fresh), fresh);
            if (fresh)
            {
                CreateDirectory(scope, Path.Combine(target.DirectoryPath, "retired"));
                owner.Flush(); journal.Flush(); SyncDirectory(target.DirectoryPath);
            }
            else _ = ReadSnapshot(target) ?? throw Invalid();
            var result = new WorkerRunLedgerPersistence(target, scope, owner, journal, fresh);
            result.RequireAuthority();
            return result;
        }
        catch { journal?.Dispose(); owner?.Dispose(); throw; }
    }

    internal static byte[]? ReadSnapshot(WorkerLedgerTarget target)
    {
        if (!Supported) throw new PlatformNotSupportedException("Worker ledger durability adapter is unavailable.");
        var scope = Scope(target);
        var observed = scope.ObserveNamespace(target.DirectoryPath);
        if (observed.BlockingFileAncestor != null) throw Invalid();
        if (observed.Kind == TrustedLocalNamespaceKind.Missing) return null;
        scope.ValidateDirectory(target.DirectoryPath, allowMissing: false);
        scope.ValidateFile(Path.Combine(target.DirectoryPath, "owner.lock"), allowMissing: false);
        scope.ValidateFile(Path.Combine(target.DirectoryPath, "journal.lock"), allowMissing: false);
        var retired = scope.ValidateDirectory(Path.Combine(target.DirectoryPath, "retired"), allowMissing: false);
        if (Directory.EnumerateFileSystemEntries(retired).Any()) throw Invalid(); // R1 initialization stage only.
        var names = new HashSet<string>(["owner.lock", "journal.lock", "state.json", "retired"], StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFileSystemEntries(target.DirectoryPath))
            if (!names.Remove(Path.GetFileName(path))) throw Invalid();
        if (names.Count != 0) throw Invalid();
        return ReadBounded(scope, Path.Combine(target.DirectoryPath, "state.json"), MaximumStateBytes);
    }

    internal void PublishInitial(byte[] bytes)
    {
        if (!CreatedNamespace || bytes.Length > MaximumStateBytes) throw Invalid();
        RequireAuthority(); _journal.Lock();
        try
        {
            RequireAuthority();
            var state = _scope.ValidateFile(Path.Combine(_target.DirectoryPath, "state.json"));
            if (File.Exists(state)) throw Invalid();
            var temporary = _scope.ValidateFile(Path.Combine(_target.DirectoryPath, "state-" + Guid.NewGuid().ToString("N") + ".tmp"));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(flushToDisk: true); }
            RequireAuthority();
            File.Move(temporary, state, overwrite: false);
            SyncDirectory(_target.DirectoryPath);
            RequireAuthority();
        }
        finally { _journal.Unlock(); }
    }

    private static TrustedLocalFileScope Scope(WorkerLedgerTarget target)
    {
        if (!GmWorkerRunRecordCodec.CanonicalPath(target.RootPath)) throw Invalid();
        var scope = new TrustedLocalFileScope([target.RootPath]);
        scope.ValidateDirectory(target.RootPath, allowMissing: false);
        return scope;
    }

    private void RequireAuthority()
    {
        _scope.ValidateDirectory(_target.DirectoryPath, allowMissing: false);
        _owner.RequireName(_scope.ValidateFile(Path.Combine(_target.DirectoryPath, "owner.lock"), false));
        _journal.RequireName(_scope.ValidateFile(Path.Combine(_target.DirectoryPath, "journal.lock"), false));
    }

    private static byte[] ReadBounded(TrustedLocalFileScope scope, string path, int limit)
    {
        using var stream = new FileStream(scope.ValidateFile(path, false), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length is < 1 || stream.Length > limit) throw Invalid();
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw Invalid();
        return bytes;
    }

    private static void CreateDirectory(TrustedLocalFileScope scope, string path)
    {
        scope.ValidateDirectory(path);
        if (Mkdir(path, 0x1c0) != 0) throw Invalid(); // 0700, no permission changes to existing paths.
        SyncDirectory(path);
        SyncDirectory(Path.GetDirectoryName(path)!);
    }

    private static void SyncDirectory(string path)
    {
        using var descriptor = Descriptor.OpenDirectory(path);
        descriptor.Flush();
    }

    public void Dispose() { _journal.Dispose(); _owner.Dispose(); }
    internal static IOException Invalid() => new("Worker ledger storage is unavailable or inconsistent.");

    // Linux x64 local adapter: private descriptors explicitly close across exec.
    private sealed class Descriptor : SafeHandle
    {
        private Descriptor(int descriptor) : base(new IntPtr(-1), ownsHandle: true) => SetHandle(new IntPtr(descriptor));
        public override bool IsInvalid => handle.ToInt64() < 0;
        private int Number => IsClosed || IsInvalid ? throw Invalid() : checked((int)handle);
        internal static Descriptor OpenLock(string path, bool create) => Open(path, 2 | (create ? 0x40 | 0x80 : 0));
        internal static Descriptor OpenDirectory(string path) => Open(path, 0x10000);
        private static Descriptor Open(string path, int flags)
        {
            var value = NativeOpen(path, flags | 0x80000 | 0x20000, 0x180); // CLOEXEC, NOFOLLOW, 0600.
            if (value < 0) throw Invalid();
            return new(value);
        }
        internal void Lock() { if (Flock(Number, 2 | 4) != 0) throw Invalid(); }
        internal void Unlock() { if (Flock(Number, 8) != 0) throw Invalid(); }
        internal void Flush() { if (Fsync(Number) != 0) throw Invalid(); }
        internal void RequireName(string path)
        {
            const uint mask = 0x101; // TYPE | INO.
            if (Statx(Number, "", 0x1000, mask, out var owned) != 0 ||
                Statx(-100, path, 0x100, mask, out var named) != 0 ||
                (owned.Mask & mask) != mask || (named.Mask & mask) != mask ||
                (owned.Mode & 0xf000) != 0x8000 || (named.Mode & 0xf000) != 0x8000 ||
                owned.Inode != named.Inode || owned.DeviceMajor != named.DeviceMajor || owned.DeviceMinor != named.DeviceMinor)
                throw Invalid();
        }
        protected override bool ReleaseHandle() => Close(checked((int)handle)) == 0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct FileStatus
    {
        [FieldOffset(0)] internal uint Mask;
        [FieldOffset(28)] internal ushort Mode;
        [FieldOffset(32)] internal ulong Inode;
        [FieldOffset(136)] internal uint DeviceMajor;
        [FieldOffset(140)] internal uint DeviceMinor;
    }
    [DllImport("libc", EntryPoint = "open", SetLastError = true)] private static extern int NativeOpen(string path, int flags, uint mode);
    [DllImport("libc", EntryPoint = "close")] private static extern int Close(int descriptor);
    [DllImport("libc", EntryPoint = "flock", SetLastError = true)] private static extern int Flock(int descriptor, int operation);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)] private static extern int Fsync(int descriptor);
    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)] private static extern int Mkdir(string path, uint mode);
    [DllImport("libc", EntryPoint = "statx", SetLastError = true)] private static extern int Statx(int descriptor, string path, int flags, uint mask, out FileStatus status);
}
