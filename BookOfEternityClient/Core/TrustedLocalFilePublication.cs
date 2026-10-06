using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Core;

// Null bytes mean absence, including when the desired operation is delete.
internal sealed record TrustedLocalFileChange(string Path, byte[]? Before, byte[]? After);
internal sealed record TrustedLocalGeneration(
    [property: JsonRequired] bool Exists, [property: JsonRequired] string? Id)
{
    internal static TrustedLocalGeneration Absent => new(false, null);
    internal static TrustedLocalGeneration Existing(string id) => new(true, id);
}
internal sealed record TrustedLocalPublishedMember(string Path, bool Exists, string? Sha256);
internal sealed record TrustedLocalPublicationResult(string TransactionId, TrustedLocalGeneration Generation,
    IReadOnlyList<TrustedLocalPublishedMember> Members);
internal enum TrustedLocalPublicationDisposition { Committed, RolledBack, Uncertain }
internal sealed record TrustedLocalPublicationOutcome(TrustedLocalPublicationDisposition Disposition,
    TrustedLocalPublicationResult? Publication, Exception? Failure);

internal enum TrustedLocalPublicationPhase
{
    IntentStaged, IntentPublished, MemberStaged, MemberPublished, CommitStaged, Committed,
    RollbackStaged, MemberRestored, CleanupMember, CleanupComplete,
    /// <summary>
    /// Forward reconciliation removed a declared ordinary empty directory.
    /// </summary>
    DirectoryRemoved,
    /// <summary>
    /// Forward reconciliation created a declared ordinary directory after intent publication.
    /// </summary>
    DirectoryCreated,
    /// <summary>
    /// Rollback removed a declared ordinary empty directory absent from the before namespace.
    /// </summary>
    RollbackDirectoryRemoved,
    /// <summary>
    /// Rollback recreated a declared ordinary directory present in the before namespace.
    /// </summary>
    RollbackDirectoryCreated
}

/// <summary>
/// Identifies a narrow metadata transport observation made by the owned frame codec.
/// </summary>
internal enum TrustedLocalFrameMetadataObservationKind
{
    /// <summary>
    /// The opening header or one complete member has been flushed to the output stream.
    /// </summary>
    WriterFlushed,
    /// <summary>
    /// An actual incomplete token has required a larger carry buffer.
    /// </summary>
    ReaderBufferGrown,
    /// <summary>
    /// One complete JSON token has been decoded.
    /// </summary>
    ReaderTokenCompleted,
    /// <summary>
    /// Oversized token carry storage has been released after token completion.
    /// </summary>
    ReaderBufferReleased
}

/// <summary>
/// Carries metadata encoding or token-buffer measurements without exposing authority paths or payload bytes.
/// </summary>
/// <param name="Kind">
/// The actual codec action being observed.
/// </param>
/// <param name="MemberIndex">
/// The flushed member index, or minus one for the header opening or a reader observation.
/// </param>
/// <param name="BytesPendingBeforeFlush">
/// The writer's pending encoded bytes before flushing; zero for reader observations.
/// </param>
/// <param name="BytesPendingAfterFlush">
/// The writer's pending encoded bytes after flushing; zero for reader observations.
/// </param>
/// <param name="DestinationPosition">
/// The output stream position after flushing; zero for reader observations.
/// </param>
/// <param name="BufferCapacity">
/// The reader's current token-buffer capacity; zero for writer observations.
/// </param>
/// <param name="EncodedTokenBytes">
/// The actual retained or completed encoded token length; zero for writer observations.
/// </param>
internal readonly record struct TrustedLocalFrameMetadataObservation(
    TrustedLocalFrameMetadataObservationKind Kind, int MemberIndex,
    long BytesPendingBeforeFlush, long BytesPendingAfterFlush, long DestinationPosition,
    int BufferCapacity, long EncodedTokenBytes);

/// <summary>
/// One process-crash journal for exact-byte single/member-set publication. Requires
/// an existing canonical lease; it is not protection against concurrent owner edits.
/// File flushes do not establish power-loss durability of directory renames.
/// </summary>
internal sealed partial class TrustedLocalFilePublication
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    // Match the existing current-schema generation reader; journal documents
    // remain strict, version-separated authority.
    private static readonly JsonSerializerOptions GenerationReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly StringComparer MemberComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly FileSystemManager _files;
    private readonly TrustedLocalFileScope _scope;
    private readonly TrustedLocalFileScope _journalScope;
    private readonly string _journalRoot;
    private readonly string _generationPath;
    private readonly string _canonicalWriteLockPath;
    private readonly string _sessionLifecycleLockPath;
    private readonly Action<TrustedLocalFrameMetadataObservation>? _metadataObserver;
    private string Active => Path.Combine(_journalRoot, "active.json");
    private string IntentStage => Path.Combine(_journalRoot, "intent.tmp");
    private string CommitStage => Path.Combine(_journalRoot, "commit.tmp");

    // Pure spelling seam permits the Windows policy to be verified on Linux.
    internal static string NormalizeAuthorityPath(string path, bool windows) =>
        windows ? TrustedLocalFileScope.NormalizeWindowsPathSpelling(path) : path;

    /// <summary>
    /// Creates publication and recovery authority for the supplied local scope and runtime journal.
    /// </summary>
    /// <param name="files">
    /// The owner of the active canonical lease and runtime generation paths.
    /// </param>
    /// <param name="scope">
    /// The explicit local member scope to validate before publication or recovery.
    /// </param>
    /// <param name="metadataObserver">
    /// An optional owned diagnostic callback for metadata transport measurements; <see langword="null"/> disables observations.
    /// </param>
    internal TrustedLocalFilePublication(FileSystemManager files, TrustedLocalFileScope scope,
        Action<TrustedLocalFrameMetadataObservation>? metadataObserver = null)
    {
        _files = files;
        _scope = scope;
        _metadataObserver = metadataObserver;
        var windows = OperatingSystem.IsWindows();
        _journalRoot = NormalizeAuthorityPath(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1"), windows);
        _generationPath = NormalizeAuthorityPath(files.SessionGenerationPath, windows);
        _canonicalWriteLockPath = NormalizeAuthorityPath(files.CanonicalWriteLockPath, windows);
        _sessionLifecycleLockPath = NormalizeAuthorityPath(files.SessionLifecycleLockPath, windows);
        _journalScope = new TrustedLocalFileScope([files.RuntimeRootPath]);
    }

    internal TrustedLocalPublicationOutcome PublishWithOutcome(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalFileChange> changes,
        Action<TrustedLocalPublicationPhase, int>? observer = null)
    {
        _files.EnsureWorkerPurposePublication(lease, generation, changes);
        return PublishWithOutcome(lease, attempt => PublishCore(lease, generation, changes, observer, attempt));
    }

    private TrustedLocalPublicationOutcome PublishWithOutcome(FileSystemManager.CanonicalWriteLease lease,
        Func<PublicationAttempt, TrustedLocalPublicationResult> publish)
    {
        _files.EnsureCanonicalWriteLeaseActive(lease);
        lease.EnsureNoPendingLocalDecision(); // Refuse before the catch/recovery wrapper can undo its caller.
        var attempt = new PublicationAttempt();
        try
        {
            var result = publish(attempt);
            return new(TrustedLocalPublicationDisposition.Committed, result, null);
        }
        catch (Exception failure)
        {
            // The durable decision precedes every cleanup callback. Never undo
            // committed runtime state merely because cleanup still needs retry.
            if (attempt.Committed != null)
                return new(TrustedLocalPublicationDisposition.Committed, attempt.Committed, failure);
            try
            {
                Recover(lease);
                if (attempt.Prepared != null && ReadGeneration(lease) == attempt.Prepared.GenerationBefore &&
                    attempt.Prepared.Members.All(member => Matches(member.Path, member.Before)))
                    return new(TrustedLocalPublicationDisposition.RolledBack, null, failure);
                if (attempt.NamespacePrepared != null &&
                    NamespaceMatchesExact(lease, attempt.NamespacePrepared, after: false))
                    return new(TrustedLocalPublicationDisposition.RolledBack, null, failure);
            }
            catch (Exception recoveryFailure)
            {
                failure = new AggregateException(failure, recoveryFailure);
            }
            return new(TrustedLocalPublicationDisposition.Uncertain, null, failure);
        }
    }

    private sealed class PublicationAttempt
    {
        internal Journal? Prepared { get; set; }
        internal bool IntentPublished { get; set; }
        /// <summary>
        /// Retains completely prepared v3 evidence for exact before-namespace outcome confirmation.
        /// </summary>
        internal NamespaceJournal? NamespacePrepared { get; set; }
        internal TrustedLocalPublicationResult? Committed { get; set; }
    }

    internal TrustedLocalPublicationResult Publish(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalFileChange> changes,
        Action<TrustedLocalPublicationPhase, int>? observer = null)
    {
        lease.EnsureNoPendingLocalDecision();
        return PublishCore(lease, generation, changes, observer, attempt: null);
    }

    private TrustedLocalPublicationResult PublishCore(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalFileChange> changes,
        Action<TrustedLocalPublicationPhase, int>? observer, PublicationAttempt? attempt)
    {
        _files.EnsureWorkerPurposePublication(lease, generation, changes);
        BeginPublication(lease, generation, changes.Count);
        var members = changes.Select(change => new Member
        {
            Path = ValidateMemberPath(change.Path),
            Before = TrustedLocalFileImage.FromBytes(change.Before),
            After = TrustedLocalFileImage.FromBytes(change.After)
        }).ToArray();
        return PublishMembers(lease, generation, members, 1, observer, attempt);
    }

    private void BeginPublication(FileSystemManager.CanonicalWriteLease lease, TrustedLocalGeneration generation, int changeCount)
    {
        _files.EnsureCanonicalWriteLeaseActive(lease);
        Recover(lease);
        ValidateGeneration(generation);
        if (ReadGeneration(lease) != generation)
            throw Conflict("The expected session generation is not current.");
        if (changeCount == 0)
            throw Conflict("A publication must declare at least one member.");
    }

    private TrustedLocalPublicationResult PublishMembers(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, Member[] members, int format,
        Action<TrustedLocalPublicationPhase, int>? observer, PublicationAttempt? attempt)
    {
        var journal = ApplyMembers(lease, generation, members, format, observer, attempt);
        return CommitMembers(lease, journal, observer, attempt);
    }

    private Journal ApplyMembers(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, Member[] members, int format,
        Action<TrustedLocalPublicationPhase, int>? observer, PublicationAttempt? attempt)
    {
        if (members.Select(member => member.Path).Distinct(MemberComparer).Count() != members.Length)
            throw Conflict("A publication contains duplicate members.");
        var generationMember = members.SingleOrDefault(member => MemberComparer.Equals(member.Path, _generationPath));
        var afterGeneration = generationMember == null ? generation : ParseGeneration(generationMember.After);
        if (generationMember != null && ParseGeneration(generationMember.Before) != generation)
            throw Conflict("The generation member does not match its binding.");
        _files.EnsureMainRecoveryGeneration(lease,generation,afterGeneration);
        if (!afterGeneration.Exists)
            throw Conflict("A publication must retain or establish a session generation.");
        var journal = new Journal
        {
            Format = format, TransactionId = Guid.NewGuid().ToString("N"), Committed = false,
            GenerationBefore = generation, GenerationAfter = afterGeneration, Members = members
        };
        ValidateJournal(journal);
        if (attempt != null) attempt.Prepared = journal;
        foreach (var member in members)
            if (!Matches(member.Path, member.Before))
                throw Conflict("A publication member does not match its expected before image.");
        var scratch = ScratchScope(journal);
        foreach (var path in ScratchPaths(journal))
            if (File.Exists(scratch.ValidateFile(path)))
                throw Conflict("A publication scratch name already exists.");

        journal = WriteJournal(IntentStage, journal);
        Observe(lease, observer, TrustedLocalPublicationPhase.IntentStaged);
        File.Move(_journalScope.ValidateFile(IntentStage, false), _journalScope.ValidateFile(Active), overwrite: false);
        if (attempt != null) attempt.IntentPublished = true;
        journal = RebindJournal(journal, Active);
        members = journal.Members;
        if (attempt != null) attempt.Prepared = journal;
        Observe(lease, observer, TrustedLocalPublicationPhase.IntentPublished);
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.After.Exists)
            {
                WriteNew(scratch, ScratchPath(journal, index, rollback: false), member.After);
                Observe(lease, observer, TrustedLocalPublicationPhase.MemberStaged, index);
            }
            // Detect accidental changes before replacing a name. The canonical
            // lease serializes participating writers; hostile races are excluded.
            if (!Matches(member.Path, member.Before))
                throw Conflict("A member changed before its publication.");
            if (member.After.Exists)
                File.Move(scratch.ValidateFile(ScratchPath(journal, index, false), false),
                    _scope.ValidateFile(member.Path), overwrite: member.Before.Exists);
            else
                _scope.DeleteOwnedFile(member.Path);
            Observe(lease, observer, TrustedLocalPublicationPhase.MemberPublished, index);
        }
        return journal;
    }

    private TrustedLocalPublicationResult CommitMembers(FileSystemManager.CanonicalWriteLease lease,
        Journal journal, Action<TrustedLocalPublicationPhase, int>? observer, PublicationAttempt? attempt)
    {
        Preflight(lease, journal, requireAfter: true);
        journal = journal with { Committed = true };
        journal = WriteJournal(CommitStage, journal);
        Observe(lease, observer, TrustedLocalPublicationPhase.CommitStaged);
        File.Move(_journalScope.ValidateFile(CommitStage, false), _journalScope.ValidateFile(Active, false), overwrite: true);
        journal = RebindJournal(journal, Active);
        var result = new TrustedLocalPublicationResult(journal.TransactionId, journal.GenerationAfter,
            journal.Members.Select(member => new TrustedLocalPublishedMember(member.Path, member.After.Exists, member.After.Sha256)).ToArray());
        if (attempt != null) attempt.Committed = result;
        Observe(lease, observer, TrustedLocalPublicationPhase.Committed);
        Cleanup(lease, journal, observer);
        return result;
    }

    internal void ValidateMainRecoveryGeneration(FileSystemManager.CanonicalWriteLease lease)
    {
        if(!Directory.Exists(_journalRoot))return;
        ValidateJournalDirectory();
        if(!File.Exists(_journalScope.ValidateFile(Active))) {
            if(Directory.EnumerateFileSystemEntries(_journalRoot).Any())throw GmSessionRunPersistence.Invalid();return;
        }
        if(HasNamespaceJournalMagic(Active)) {
            var j=ReadNamespaceJournal(Active);_files.EnsureMainRecoveryGeneration(lease,j.GenerationBefore,j.GenerationAfter);
        } else {
            var j=ReadJournal(Active);_files.EnsureMainRecoveryGeneration(lease,j.GenerationBefore,j.GenerationAfter);
        }
    }

    internal void Recover(FileSystemManager.CanonicalWriteLease lease,
        Action<TrustedLocalPublicationPhase, int>? observer = null)
    {
        _files.EnsureWorkerRecoveryAdmission(lease);
        lease.EnsureNoPendingLocalDecision();
        RecoverCore(lease, observer);
    }

    private void RecoverCore(FileSystemManager.CanonicalWriteLease lease,
        Action<TrustedLocalPublicationPhase, int>? observer = null)
    {
        _files.EnsureCanonicalWriteLeaseActive(lease);
        _journalScope.EnsureDirectory(_journalRoot);
        ValidateJournalDirectory();
        if (!File.Exists(_journalScope.ValidateFile(Active)))
        {
            if (File.Exists(_journalScope.ValidateFile(CommitStage)))
                throw Conflict("A commit stage without its active journal is unresolved evidence.");
            // This private scratch file precedes the intent publication. Even a
            // partially written intent cannot have authorized a member mutation.
            _journalScope.DeleteOwnedFile(IntentStage);
            return;
        }

        if (HasNamespaceJournalMagic(Active))
        {
            RecoverNamespace(lease, ReadNamespaceJournal(Active), observer);
            return;
        }
        var journal = ReadJournal(Active);
        Preflight(lease, journal, requireAfter: journal.Committed);
        if (!journal.Committed)
        {
            var scratch = ScratchScope(journal);
            for (var index = 0; index < journal.Members.Length; index++)
            {
                var member = journal.Members[index];
                if (!Matches(member.Path, member.Before))
                {
                    if (member.Before.Exists)
                    {
                        var stage = ScratchPath(journal, index, rollback: true);
                        scratch.DeleteOwnedFile(stage);
                        WriteNew(scratch, stage, member.Before);
                        Observe(lease, observer, TrustedLocalPublicationPhase.RollbackStaged, index);
                        File.Move(scratch.ValidateFile(stage, false), _scope.ValidateFile(member.Path), overwrite: member.After.Exists);
                    }
                    else
                        _scope.DeleteOwnedFile(member.Path);
                }
                Observe(lease, observer, TrustedLocalPublicationPhase.MemberRestored, index);
            }
        }
        Cleanup(lease, journal, observer);
    }

    private void Preflight(FileSystemManager.CanonicalWriteLease lease, Journal journal, bool requireAfter)
    {
        _files.EnsureCanonicalWriteLeaseActive(lease);
        ValidateJournalDirectory();
        var actualGeneration = ReadGeneration(lease);
        if (actualGeneration != journal.GenerationAfter && (requireAfter || actualGeneration != journal.GenerationBefore))
            throw Conflict("The journal belongs to a different session generation.");
        // All canonical members and scratch path types are checked before the
        // first rollback or cleanup. No earlier member is restored on conflict.
        foreach (var member in journal.Members)
            if (!Matches(member.Path, member.After) && (requireAfter || !Matches(member.Path, member.Before)))
                throw Conflict("A publication member contains unknown bytes; evidence retained.");
        var scratch = ScratchScope(journal);
        foreach (var path in ScratchPaths(journal)) scratch.ValidateFile(path);
    }

    private void Cleanup(FileSystemManager.CanonicalWriteLease lease, Journal journal,
        Action<TrustedLocalPublicationPhase, int>? observer)
    {
        _files.EnsureCanonicalWriteLeaseActive(lease);
        // Scratch contents are not authority: a crash may have interrupted any
        // write. Names are derived from the validated intent and never followed.
        var scratch = ScratchScope(journal);
        foreach (var path in ScratchPaths(journal)) scratch.ValidateFile(path);
        for (var index = 0; index < journal.Members.Length; index++)
        {
            scratch.DeleteOwnedFile(ScratchPath(journal, index, false));
            scratch.DeleteOwnedFile(ScratchPath(journal, index, true));
            Observe(lease, observer, TrustedLocalPublicationPhase.CleanupMember, index);
        }
        _journalScope.DeleteOwnedFile(IntentStage);
        _journalScope.DeleteOwnedFile(CommitStage);
        // The pending/committed decision and all before images survive until all
        // other cleanup is done. Repeating cleanup can never turn commit into undo.
        _journalScope.DeleteOwnedFile(Active);
        Observe(lease, observer, TrustedLocalPublicationPhase.CleanupComplete);
    }

    private void ValidateJournal(Journal journal)
    {
        if (journal.Format is not (1 or 2) || !ValidId(journal.TransactionId) || journal.Members is not { Length: > 0 })
            throw Conflict("Unknown or invalid publication format; evidence retained.");
        ValidateGeneration(journal.GenerationBefore);
        ValidateGeneration(journal.GenerationAfter);
        if (!journal.GenerationAfter.Exists) throw Conflict("A journal cannot remove session generation.");
        var paths = new HashSet<string>(MemberComparer);
        foreach (var member in journal.Members)
        {
            if (member == null || !paths.Add(ValidateMemberPath(member.Path)) ||
                !string.Equals(member.Path, _scope.ValidateFile(member.Path), StringComparison.Ordinal))
                throw Conflict("Invalid or duplicate publication member.");
            ValidateImage(member.Before);
            ValidateImage(member.After);
        }
        var generationMember = journal.Members.SingleOrDefault(member => MemberComparer.Equals(member.Path, _generationPath));
        if (generationMember == null)
        {
            if (journal.GenerationBefore != journal.GenerationAfter)
                throw Conflict("A generation transition must be a declared member.");
        }
        else if (ParseGeneration(generationMember.Before) != journal.GenerationBefore ||
                 ParseGeneration(generationMember.After) != journal.GenerationAfter)
            throw Conflict("The generation images do not match the journal binding.");
        foreach (var path in ScratchPaths(journal))
            if (!paths.Add(path)) throw Conflict("Publication member/scratch names overlap.");
    }

    private string ValidateMemberPath(string path)
    {
        var normalized = _scope.ValidateFile(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (normalized.Equals(_journalRoot, comparison) || normalized.StartsWith(_journalRoot + Path.DirectorySeparatorChar, comparison) ||
            _journalRoot.StartsWith(normalized + Path.DirectorySeparatorChar, comparison) ||
            normalized.Equals(_canonicalWriteLockPath, comparison) || normalized.Equals(_sessionLifecycleLockPath, comparison))
            throw Conflict("A publication cannot mutate its journal or writer ownership paths.");
        return normalized;
    }

    private void ValidateJournalDirectory()
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(_journalRoot))
        {
            _journalScope.ValidateFile(path, allowMissing: false);
            if (path != Active && path != IntentStage && path != CommitStage)
                throw Conflict("Unrecognized publication evidence; retained without mutation.");
        }
    }

    private TrustedLocalGeneration ReadGeneration(FileSystemManager.CanonicalWriteLease lease)
    {
        var id = _files.ReadExistingSessionGeneration(lease);
        return id == null ? TrustedLocalGeneration.Absent : TrustedLocalGeneration.Existing(id);
    }

    private static TrustedLocalGeneration ParseGeneration(TrustedLocalFileImage image)
    {
        if (!image.Exists) return TrustedLocalGeneration.Absent;
        try
        {
            // Decode exactly like FileSystemManager.ReadRuntimeText. The image
            // remains byte-exact in the journal; only its generation semantics
            // are read through the existing BOM-aware text contract.
            using var stream = image.OpenRead();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var value = StrictJsonAuthority.Deserialize<GenerationDocument>(reader.ReadToEnd(), GenerationReadOptions, "Publication generation");
            if (value == null || value.SchemaVersion != 1 || !ValidId(value.GenerationId))
                throw Conflict("The declared generation image is invalid.");
            return TrustedLocalGeneration.Existing(value.GenerationId);
        }
        catch (JsonException ex) { throw new InvalidDataException("The declared generation image is invalid.", ex); }
    }

    private static void ValidateGeneration(TrustedLocalGeneration generation)
    {
        if (generation == null || (generation.Exists ? !ValidId(generation.Id) : generation.Id != null))
            throw Conflict("The generation binding must explicitly describe a valid ID or absence.");
    }

    private static bool ValidId(string? value) => Guid.TryParseExact(value, "N", out var id) && value == id.ToString("N");
    private static void ValidateImage(TrustedLocalFileImage image)
    {
        if (image == null) throw Conflict("A publication image is null; evidence retained.");
        image.Validate();
    }
    private bool Matches(string path, TrustedLocalFileImage image) => image.MatchesFile(_scope, path);
    private static string ScratchPath(Journal journal, int index, bool rollback) => Path.Combine(
        Path.GetDirectoryName(journal.Members[index].Path)!, $".boe-local-{journal.TransactionId}-{index}.{(rollback ? "undo" : "stage")}");
    private static IEnumerable<string> ScratchPaths(Journal journal) => Enumerable.Range(0, journal.Members.Length)
        .SelectMany(index => new[] { ScratchPath(journal, index, false), ScratchPath(journal, index, true) });
    private static TrustedLocalFileScope ScratchScope(Journal journal) => new([], ScratchPaths(journal));
    private static void WriteNew(TrustedLocalFileScope scope, string path, byte[] bytes)
    {
        using var stream = new FileStream(scope.ValidateFile(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }
    private static void WriteNew(TrustedLocalFileScope scope, string path, TrustedLocalFileImage image)
    {
        using var stream = new FileStream(scope.ValidateFile(path), FileMode.CreateNew, FileAccess.Write, FileShare.None);
        image.CopyTo(stream);
        stream.Flush(flushToDisk: true);
    }

    private void Observe(FileSystemManager.CanonicalWriteLease lease, Action<TrustedLocalPublicationPhase, int>? observer,
        TrustedLocalPublicationPhase phase, int index = -1)
    {
        observer?.Invoke(phase, index);
        _files.EnsureCanonicalWriteLeaseActive(lease);
    }
    private static InvalidDataException Conflict(string message) => new(message);

    private sealed record GenerationDocument(int SchemaVersion, string GenerationId);
    private sealed record Journal
    {
        public required int Format { get; init; }
        public required string TransactionId { get; init; }
        public required bool Committed { get; init; }
        public required TrustedLocalGeneration GenerationBefore { get; init; }
        public required TrustedLocalGeneration GenerationAfter { get; init; }
        public required Member[] Members { get; init; }
    }
    private sealed record Member
    {
        public required string Path { get; init; }
        public required TrustedLocalFileImage Before { get; init; }
        public required TrustedLocalFileImage After { get; init; }
    }
}
