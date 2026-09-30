using System.Security.Cryptography;
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
internal enum TrustedLocalPublicationPhase
{
    IntentStaged, IntentPublished, MemberStaged, MemberPublished, CommitStaged, Committed,
    RollbackStaged, MemberRestored, CleanupMember, CleanupComplete
}

/// <summary>
/// One process-crash journal for exact-byte single/member-set publication. Requires
/// an existing canonical lease; it is not protection against concurrent owner edits.
/// File flushes do not establish power-loss durability of directory renames.
/// </summary>
internal sealed class TrustedLocalFilePublication
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly StringComparer MemberComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly FileSystemManager _files;
    private readonly TrustedLocalFileScope _scope;
    private readonly TrustedLocalFileScope _journalScope;
    private readonly string _journalRoot;
    private string Active => Path.Combine(_journalRoot, "active.json");
    private string IntentStage => Path.Combine(_journalRoot, "intent.tmp");
    private string CommitStage => Path.Combine(_journalRoot, "commit.tmp");

    internal TrustedLocalFilePublication(FileSystemManager files, TrustedLocalFileScope scope)
    {
        _files = files;
        _scope = scope;
        _journalRoot = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1");
        _journalScope = new TrustedLocalFileScope([files.RuntimeRootPath]);
    }

    internal TrustedLocalPublicationResult Publish(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, IReadOnlyList<TrustedLocalFileChange> changes,
        Action<TrustedLocalPublicationPhase, int>? observer = null)
    {
        _files.EnsureCanonicalWriteLeaseActive(lease);
        Recover(lease);
        ValidateGeneration(generation);
        if (ReadGeneration(lease) != generation)
            throw Conflict("The expected session generation is not current.");
        if (changes.Count == 0)
            throw Conflict("A publication must declare at least one member.");

        var members = changes.Select(change => new Member
        {
            Path = ValidateMemberPath(change.Path),
            Before = Image.FromBytes(change.Before),
            After = Image.FromBytes(change.After)
        }).ToArray();
        var generationMember = members.SingleOrDefault(member => MemberComparer.Equals(member.Path, _files.SessionGenerationPath));
        var afterGeneration = generationMember == null ? generation : ParseGeneration(generationMember.After.Bytes);
        if (generationMember != null && ParseGeneration(generationMember.Before.Bytes) != generation)
            throw Conflict("The generation member does not match its binding.");
        if (!afterGeneration.Exists)
            throw Conflict("A publication must retain or establish a session generation.");
        var journal = new Journal
        {
            Format = 1, TransactionId = Guid.NewGuid().ToString("N"), Committed = false,
            GenerationBefore = generation, GenerationAfter = afterGeneration, Members = members
        };
        ValidateJournal(journal);
        foreach (var member in members)
            if (!Matches(member.Path, member.Before))
                throw Conflict("A publication member does not match its expected before image.");
        var scratch = ScratchScope(journal);
        foreach (var path in ScratchPaths(journal))
            if (File.Exists(scratch.ValidateFile(path)))
                throw Conflict("A publication scratch name already exists.");

        WriteNew(_journalScope, IntentStage, JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions));
        Observe(lease, observer, TrustedLocalPublicationPhase.IntentStaged);
        File.Move(_journalScope.ValidateFile(IntentStage, false), _journalScope.ValidateFile(Active), overwrite: false);
        Observe(lease, observer, TrustedLocalPublicationPhase.IntentPublished);
        for (var index = 0; index < members.Length; index++)
        {
            var member = members[index];
            if (member.After.Exists)
            {
                WriteNew(scratch, ScratchPath(journal, index, rollback: false), member.After.Bytes!);
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
        Preflight(lease, journal, requireAfter: true);
        journal = journal with { Committed = true };
        WriteNew(_journalScope, CommitStage, JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions));
        Observe(lease, observer, TrustedLocalPublicationPhase.CommitStaged);
        File.Move(_journalScope.ValidateFile(CommitStage, false), _journalScope.ValidateFile(Active, false), overwrite: true);
        Observe(lease, observer, TrustedLocalPublicationPhase.Committed);
        Cleanup(lease, journal, observer);
        return new(journal.TransactionId, afterGeneration,
            members.Select(member => new TrustedLocalPublishedMember(member.Path, member.After.Exists, member.After.Sha256)).ToArray());
    }

    internal void Recover(FileSystemManager.CanonicalWriteLease lease,
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

        Journal journal;
        try
        {
            journal = StrictJsonAuthority.Deserialize<Journal>(File.ReadAllBytes(Active), JsonOptions, "Trusted-local publication")
                ?? throw Conflict("The publication journal is null.");
        }
        catch (JsonException ex) { throw new InvalidDataException("The publication journal is invalid; evidence retained.", ex); }
        ValidateJournal(journal);
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
                        WriteNew(scratch, stage, member.Before.Bytes!);
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
        if (journal.Format != 1 || !ValidId(journal.TransactionId) || journal.Members is not { Length: > 0 })
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
        var generationMember = journal.Members.SingleOrDefault(member => MemberComparer.Equals(member.Path, _files.SessionGenerationPath));
        if (generationMember == null)
        {
            if (journal.GenerationBefore != journal.GenerationAfter)
                throw Conflict("A generation transition must be a declared member.");
        }
        else if (ParseGeneration(generationMember.Before.Bytes) != journal.GenerationBefore ||
                 ParseGeneration(generationMember.After.Bytes) != journal.GenerationAfter)
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
            normalized.Equals(_files.CanonicalWriteLockPath, comparison) || normalized.Equals(_files.SessionLifecycleLockPath, comparison))
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

    private static TrustedLocalGeneration ParseGeneration(byte[]? bytes)
    {
        if (bytes == null) return TrustedLocalGeneration.Absent;
        try
        {
            var value = StrictJsonAuthority.Deserialize<GenerationDocument>(bytes, JsonOptions, "Publication generation");
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
    private static void ValidateImage(Image image)
    {
        if (image == null || image.Exists != (image.Bytes != null) ||
            image.Sha256 != (image.Bytes == null ? null : Hash(image.Bytes)))
            throw Conflict("A publication image is invalid; evidence retained.");
    }
    private bool Matches(string path, Image image)
    {
        var exists = File.Exists(_scope.ValidateFile(path));
        return exists == image.Exists && (!exists || Hash(File.ReadAllBytes(path)) == image.Sha256);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
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
        public required Image Before { get; init; }
        public required Image After { get; init; }
    }
    private sealed record Image
    {
        public required bool Exists { get; init; }
        public required byte[]? Bytes { get; init; }
        public required string? Sha256 { get; init; }
        internal static Image FromBytes(byte[]? bytes) => new()
        {
            Exists = bytes != null, Bytes = bytes?.ToArray(), Sha256 = bytes == null ? null : Hash(bytes)
        };
    }
}
