using System.Runtime.InteropServices;
using System.Text.Json;
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
    private readonly Action<WorkerLedgerIoStage>? _observe;
    private readonly FixtureMode _mode;
    private enum FixtureMode { LegacySynthetic, DurableSynthetic }
    private Transaction? _pending;
    private sealed record Transaction(byte[]? Before, byte[] After, WorkerRunRecord? Archive, string Temporary);
    internal bool CreatedNamespace { get; }

    private WorkerRunLedgerPersistence(WorkerLedgerTarget target, TrustedLocalFileScope scope,
        Descriptor owner, Descriptor journal, bool created, FixtureMode mode, Action<WorkerLedgerIoStage>? observe)
    { _target = target; _scope = scope; _owner = owner; _journal = journal; CreatedNamespace = created; _mode = mode; _observe = observe; }

    internal static WorkerRunLedgerPersistence Open(WorkerLedgerTarget target, Action<WorkerLedgerIoStage>? observe = null) =>
        OpenCore(target, FixtureMode.DurableSynthetic, observe);
    internal static WorkerRunLedgerPersistence OpenLegacy(WorkerLedgerTarget target) => OpenCore(target, FixtureMode.LegacySynthetic, null);

    private static WorkerRunLedgerPersistence OpenCore(WorkerLedgerTarget target, FixtureMode mode, Action<WorkerLedgerIoStage>? observe)
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
                {
                    CreateDirectory(scope, runtime);
                    observe?.Invoke(WorkerLedgerIoStage.RuntimeCreated);
                }
                else scope.ValidateDirectory(runtime, allowMissing: false);
                // mkdir, not CreateDirectory: only the original successful creator may bootstrap.
                CreateDirectory(scope, target.DirectoryPath);
                observe?.Invoke(WorkerLedgerIoStage.NamespaceCreated);
            }
            else scope.ValidateDirectory(target.DirectoryPath, allowMissing: false);
            owner = Descriptor.OpenLock(scope.ValidateFile(Path.Combine(target.DirectoryPath, "owner.lock"), fresh), fresh);
            owner.Lock();
            if (fresh) observe?.Invoke(WorkerLedgerIoStage.OwnerCreated);
            journal = Descriptor.OpenLock(scope.ValidateFile(Path.Combine(target.DirectoryPath, "journal.lock"), fresh), fresh);
            if (fresh)
            {
                observe?.Invoke(WorkerLedgerIoStage.JournalCreated);
                CreateDirectory(scope, Path.Combine(target.DirectoryPath, "retired"));
                observe?.Invoke(WorkerLedgerIoStage.RetiredDirectoryCreated);
                owner.Flush(); journal.Flush(); SyncDirectory(target.DirectoryPath);
                journal.Lock();
                try
                {
                    var path = scope.ValidateFile(Path.Combine(target.DirectoryPath, "mode.json"));
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(ModeBytes(target, mode));
                        stream.Flush(flushToDisk: true);
                    }
                    observe?.Invoke(WorkerLedgerIoStage.ModeFileFlushed);
                    SyncDirectory(target.DirectoryPath);
                    observe?.Invoke(WorkerLedgerIoStage.ModeDirectorySynced);
                }
                finally { journal.Unlock(); }
            }
            else
            {
                RequireMode(scope, target, mode);
                if (mode == FixtureMode.DurableSynthetic) _ = ReadSnapshot(target) ?? throw Invalid();
                else ValidateLegacyNamespace(scope, target);
            }
            var result = new WorkerRunLedgerPersistence(target, scope, owner, journal, fresh, mode, observe);
            result.RequireAuthority();
            return result;
        }
        catch { journal?.Dispose(); owner?.Dispose(); throw; }
    }

    internal static byte[]? ReadSnapshot(WorkerLedgerTarget target, string? allowedTemporary = null)
    {
        if (!Supported) throw new PlatformNotSupportedException("Worker ledger durability adapter is unavailable.");
        var scope = Scope(target);
        var observed = scope.ObserveNamespace(target.DirectoryPath);
        if (observed.BlockingFileAncestor != null) throw Invalid();
        if (observed.Kind == TrustedLocalNamespaceKind.Missing) return null;
        scope.ValidateDirectory(target.DirectoryPath, allowMissing: false);
        RequireMode(scope, target, FixtureMode.DurableSynthetic);
        scope.ValidateFile(Path.Combine(target.DirectoryPath, "owner.lock"), allowMissing: false);
        scope.ValidateFile(Path.Combine(target.DirectoryPath, "journal.lock"), allowMissing: false);
        var retired = scope.ValidateDirectory(Path.Combine(target.DirectoryPath, "retired"), allowMissing: false);
        var names = new HashSet<string>(["owner.lock", "journal.lock", "mode.json", "state.json", "retired"], StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFileSystemEntries(target.DirectoryPath))
            if (path != allowedTemporary && !names.Remove(Path.GetFileName(path))) throw Invalid();
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
        RequireMode(scope, target, FixtureMode.DurableSynthetic);
        return bytes;
    }

    internal void PublishInitial(byte[] bytes)
    {
        if (!CreatedNamespace || !bytes.AsSpan().SequenceEqual(GmWorkerRunLedgerCodec.Encode(GmWorkerRunLedgerCodec.Initial(_target)))) throw Conflict();
        PublishExact(null, bytes, null);
    }

    internal void PublishPrepared(byte[] expected, WorkerRunRecord record)
    {
        var next = GmWorkerRunLedgerCodec.AddPrepared(GmWorkerRunLedgerCodec.Decode(_target, expected), record);
        PublishExact(expected, GmWorkerRunLedgerCodec.Encode(next), null);
    }

    internal void VerifyExact(byte[] expected)
    {
        RequireAuthority();
        var actual = ReadSnapshot(_target);
        if (actual is null || !actual.AsSpan().SequenceEqual(expected)) throw Conflict();
        RequireAuthority();
    }

    internal void PublishTransition(byte[] expected, WorkerRunIdentity identity, WorkerRunPhase phase)
    {
        var next = GmWorkerRunLedgerCodec.Transition(GmWorkerRunLedgerCodec.Decode(_target, expected), identity, phase);
        PublishExact(expected, GmWorkerRunLedgerCodec.Encode(next),
            phase == WorkerRunPhase.AbortedBeforeLaunch ? new(2, identity, phase) : null);
    }

    // Only the original adapter can retry this frozen private plan. No new desired state is accepted.
    internal void RetryPending()
    {
        if (_pending is null) throw Conflict();
        Execute(_pending);
    }

    // Generic byte CAS is private; callers select only closed typed operations.
    private void PublishExact(byte[]? expected, byte[] bytes, WorkerRunRecord? archive)
    {
        if (_mode != FixtureMode.DurableSynthetic || _pending is not null || bytes.Length > MaximumStateBytes) throw Conflict();
        _pending = new(expected?.ToArray(), bytes.ToArray(), archive,
            Path.Combine(_target.DirectoryPath, "state-" + Guid.NewGuid().ToString("N") + ".tmp"));
        Execute(_pending);
    }

    private bool ValidateTransaction(Transaction plan)
    {
        // Any changed identity/layout/bytes loses write authority. Preserve every artifact.
        try
        {
            RequireAuthority();
            var state = _scope.ValidateFile(Path.Combine(_target.DirectoryPath, "state.json"));
            var alreadyNew = false;
            if (File.Exists(state))
            {
                var actual = ReadSnapshot(_target, plan.Temporary) ?? throw Conflict();
                alreadyNew = actual.AsSpan().SequenceEqual(plan.After);
                if (!alreadyNew && (plan.Before is null || !actual.AsSpan().SequenceEqual(plan.Before))) throw Conflict();
            }
            else
            {
                if (plan.Before is not null || !CreatedNamespace) throw Conflict();
                var names = new HashSet<string>(["owner.lock", "journal.lock", "mode.json", "retired"], StringComparer.Ordinal);
                foreach (var path in Directory.EnumerateFileSystemEntries(_target.DirectoryPath))
                    if (path != plan.Temporary && !names.Remove(Path.GetFileName(path))) throw Conflict();
                if (names.Count != 0 || Directory.EnumerateFileSystemEntries(_scope.ValidateDirectory(Path.Combine(_target.DirectoryPath, "retired"), false)).Any()) throw Conflict();
            }
            _scope.ValidateFile(plan.Temporary);
            if (File.Exists(plan.Temporary) && (alreadyNew ||
                !ReadBounded(_scope, plan.Temporary, MaximumStateBytes).AsSpan().SequenceEqual(plan.After))) throw Conflict();
            RequireAuthority();
            return alreadyNew;
        }
        catch (Exception error) when (GmWorkerRunLedger.Unavailable(error)) { throw Conflict(); }
    }

    private void Execute(Transaction plan)
    {
        _ = ValidateTransaction(plan); _journal.Lock();
        try
        {
            var alreadyNew = ValidateTransaction(plan);
            var statePath = Path.Combine(_target.DirectoryPath, "state.json");
            if (!alreadyNew)
            {
                if (plan.Archive is not null) WriteArchive(plan.Archive);
                _observe?.Invoke(WorkerLedgerIoStage.BeforeStateWrite);
                var temporary = _scope.ValidateFile(plan.Temporary);
                var exists = File.Exists(temporary);
                using (var stream = new FileStream(temporary, exists ? FileMode.Open : FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                {
                    if (!exists) stream.Write(plan.After);
                    _observe?.Invoke(WorkerLedgerIoStage.StateWritten);
                    stream.Flush(flushToDisk: true);
                    _observe?.Invoke(WorkerLedgerIoStage.StateFlushed);
                }
                if (ValidateTransaction(plan)) throw Conflict();
                File.Move(temporary, statePath, overwrite: plan.Before is not null);
                _observe?.Invoke(WorkerLedgerIoStage.StateRenamed);
            }
            else
            {
                // Rename may have succeeded without a live ACK. Re-establish durability of the exact bytes.
                using var descriptor = Descriptor.OpenFile(_scope.ValidateFile(statePath, false));
                descriptor.RequireName(statePath); descriptor.Flush();
            }
            SyncDirectory(_target.DirectoryPath);
            _observe?.Invoke(WorkerLedgerIoStage.StateDirectorySynced);
            VerifyExact(plan.After);
            _pending = null;
        }
        finally { _journal.Unlock(); }
    }

    private void WriteArchive(WorkerRunRecord record)
    {
        RequireAuthority();
        var directory = _scope.ValidateDirectory(Path.Combine(_target.DirectoryPath, "retired"), false);
        var path = _scope.ValidateFile(Path.Combine(directory, record.Identity.RunId + ".json"));
        var bytes = GmWorkerRunRecordCodec.Encode(record);
        _observe?.Invoke(WorkerLedgerIoStage.BeforeArchiveWrite);
        var exists = File.Exists(path);
        if (exists && !ReadBounded(_scope, path, 64 * 1024).AsSpan().SequenceEqual(bytes)) throw Conflict();
        using (var stream = new FileStream(path, exists ? FileMode.Open : FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            if (!exists) stream.Write(bytes);
            _observe?.Invoke(WorkerLedgerIoStage.ArchiveWritten);
            stream.Flush(flushToDisk: true);
            _observe?.Invoke(WorkerLedgerIoStage.ArchiveFlushed);
        }
        SyncDirectory(directory);
        _observe?.Invoke(WorkerLedgerIoStage.ArchiveDirectorySynced);
        RequireAuthority();
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
        RequireMode(_scope, _target, _mode);
    }

    internal void VerifyLegacy()
    {
        if (_mode != FixtureMode.LegacySynthetic) throw Invalid();
        RequireAuthority();
        ValidateLegacyNamespace(_scope, _target);
        RequireAuthority();
    }

    private static byte[] ModeBytes(WorkerLedgerTarget target, FixtureMode mode) => JsonSerializer.SerializeToUtf8Bytes(new
    { SchemaVersion = 1, RootKey = target.RootPath, Mode = mode.ToString() });

    private static void RequireMode(TrustedLocalFileScope scope, WorkerLedgerTarget target, FixtureMode mode)
    {
        // An immutable generated binding: no parser normalization, repair or mode switch.
        // Mode is cooperating exclusion, never evidence about a worker's death/result.
        if (!ReadBounded(scope, Path.Combine(target.DirectoryPath, "mode.json"), 16 * 1024)
            .AsSpan().SequenceEqual(ModeBytes(target, mode))) throw Invalid();
    }

    private static void ValidateLegacyNamespace(TrustedLocalFileScope scope, WorkerLedgerTarget target)
    {
        scope.ValidateDirectory(target.DirectoryPath, false);
        RequireMode(scope, target, FixtureMode.LegacySynthetic);
        scope.ValidateFile(Path.Combine(target.DirectoryPath, "owner.lock"), false);
        scope.ValidateFile(Path.Combine(target.DirectoryPath, "journal.lock"), false);
        var retired = scope.ValidateDirectory(Path.Combine(target.DirectoryPath, "retired"), false);
        if (Directory.EnumerateFileSystemEntries(retired).Any()) throw Invalid();
        var names = new HashSet<string>(["owner.lock", "journal.lock", "mode.json", "retired"], StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFileSystemEntries(target.DirectoryPath))
            if (!names.Remove(Path.GetFileName(path))) throw Invalid();
        if (names.Count != 0) throw Invalid();
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
    internal static WorkerLedgerConflictException Conflict() => new();
    internal static IOException Invalid() => new("Worker ledger storage is unavailable or inconsistent.");

    // Linux x64 local adapter: private descriptors explicitly close across exec.
    private sealed class Descriptor : SafeHandle
    {
        private Descriptor(int descriptor) : base(new IntPtr(-1), ownsHandle: true) => SetHandle(new IntPtr(descriptor));
        public override bool IsInvalid => handle.ToInt64() < 0;
        private int Number => IsClosed || IsInvalid ? throw Invalid() : checked((int)handle);
        internal static Descriptor OpenLock(string path, bool create) => Open(path, 2 | (create ? 0x40 | 0x80 : 0));
        internal static Descriptor OpenFile(string path) => Open(path, 2);
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

internal sealed class WorkerLedgerConflictException : IOException
{ internal WorkerLedgerConflictException() : base("Worker ledger original authority or exact evidence changed.") { } }
