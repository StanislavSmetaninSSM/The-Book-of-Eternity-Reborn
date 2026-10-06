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
        var names = new HashSet<string>(["owner.lock", "journal.lock", "state.json", "retired"], StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFileSystemEntries(target.DirectoryPath))
            if (!names.Remove(Path.GetFileName(path))) throw Invalid();
        if (names.Count != 0) throw Invalid();
        var statePath = Path.Combine(target.DirectoryPath, "state.json");
        var bytes = ReadBounded(scope, statePath, MaximumStateBytes);
        var state = GmWorkerRunLedgerCodec.Decode(target, bytes);
        var required = state.Retired.ToDictionary(item => item.RunId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFileSystemEntries(retired))
        {
            if (seen.Count >= GmWorkerRunLedgerCodec.MaximumRetiredEntries) throw Invalid();
            var name = Path.GetFileName(path);
            if (!name.EndsWith(".json", StringComparison.Ordinal)) throw Invalid();
            var runId = name[..^5];
            if (!GmWorkerRunRecordCodec.Id(runId) || !seen.Add(runId)) throw Invalid();
            var archive = ReadBounded(scope, path, 64 * 1024);
            var record = GmWorkerRunRecordCodec.Decode(archive);
            if (record.Phase != WorkerRunPhase.AbortedBeforeLaunch || record.Identity.RunId != runId ||
                !GmWorkerRunLedgerCodec.RootMatches(record.Identity.RootKey, target.RootPath)) throw Invalid();
            if (required.Remove(runId, out var reference))
            {
                if (reference.Epoch != record.Identity.Epoch || reference.TaskKeySha256 != GmWorkerRunLedgerCodec.TaskKey(record.Identity) ||
                    reference.RecordSha256 != GmWorkerRunLedgerCodec.Hash(archive)) throw Invalid();
            }
            else
            {
                // A candidate alone never removes its still-active reservation.
                var active = state.Entries.SingleOrDefault(item => item.Identity == record.Identity);
                if (active?.Phase != WorkerRunPhase.Prepared ||
                    !archive.AsSpan().SequenceEqual(GmWorkerRunRecordCodec.Encode(active with { Phase = WorkerRunPhase.AbortedBeforeLaunch }))) throw Invalid();
            }
        }
        if (required.Count != 0 || !ReadBounded(scope, statePath, MaximumStateBytes).AsSpan().SequenceEqual(bytes)) throw Invalid();
        return bytes;
    }

    internal void PublishInitial(byte[] bytes)
    {
        if (!CreatedNamespace) throw Invalid();
        PublishExact(null, bytes);
    }

    internal void PublishPrepared(byte[] expected, WorkerRunRecord record)
    {
        var before = GmWorkerRunLedgerCodec.Decode(_target, expected);
        var next = GmWorkerRunLedgerCodec.AddPrepared(before, record);
        PublishExact(expected, GmWorkerRunLedgerCodec.Encode(next));
    }

    internal void VerifyExact(byte[] expected)
    {
        RequireAuthority();
        var actual = ReadSnapshot(_target);
        if (actual is null || !actual.AsSpan().SequenceEqual(expected)) throw Invalid();
        RequireAuthority();
    }

    internal void PublishTransition(byte[] expected, WorkerRunIdentity identity, WorkerRunPhase phase)
    {
        var before = GmWorkerRunLedgerCodec.Decode(_target, expected);
        var next = GmWorkerRunLedgerCodec.Transition(before, identity, phase);
        VerifyExact(expected);
        if (phase == WorkerRunPhase.AbortedBeforeLaunch)
        {
            _journal.Lock();
            try
            {
                VerifyExact(expected);
                var directory = _scope.ValidateDirectory(Path.Combine(_target.DirectoryPath, "retired"), false);
                var path = _scope.ValidateFile(Path.Combine(directory, identity.RunId + ".json"));
                var bytes = GmWorkerRunRecordCodec.Encode(new(1, identity, phase));
                if (File.Exists(path))
                {
                    if (!ReadBounded(_scope, path, 64 * 1024).AsSpan().SequenceEqual(bytes)) throw Invalid();
                }
                else
                {
                    using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    stream.Write(bytes); stream.Flush(flushToDisk: true);
                }
                SyncDirectory(directory);
                RequireAuthority();
            }
            finally { _journal.Unlock(); }
        }
        PublishExact(expected, GmWorkerRunLedgerCodec.Encode(next));
    }

    // Generic byte CAS is private; callers select only closed typed operations.
    private void PublishExact(byte[]? expected, byte[] bytes)
    {
        if (bytes.Length > MaximumStateBytes) throw Invalid();
        RequireAuthority(); _journal.Lock();
        try
        {
            RequireAuthority();
            var state = _scope.ValidateFile(Path.Combine(_target.DirectoryPath, "state.json"));
            if (expected is null ? File.Exists(state) :
                !ReadBounded(_scope, state, MaximumStateBytes).AsSpan().SequenceEqual(expected)) throw Invalid();
            var temporary = _scope.ValidateFile(Path.Combine(_target.DirectoryPath, "state-" + Guid.NewGuid().ToString("N") + ".tmp"));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(flushToDisk: true); }
            RequireAuthority();
            if (expected is null ? File.Exists(state) :
                !ReadBounded(_scope, state, MaximumStateBytes).AsSpan().SequenceEqual(expected)) throw Invalid();
            File.Move(temporary, state, overwrite: expected is not null);
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
        protected override bool ReleaseHandle() => WorkerRunLedgerPersistence.Close(checked((int)handle)) == 0;
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
