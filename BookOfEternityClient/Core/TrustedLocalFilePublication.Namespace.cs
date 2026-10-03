using System.Security.Cryptography;

namespace BookOfEternityClient.Core;

internal sealed partial class TrustedLocalFilePublication
{
    /// <summary>
    /// Publishes one complete namespace and generation through the existing intent/commit decision.
    /// </summary>
    /// <param name="lease">
    /// The active canonical lease held by the replacement coordinator.
    /// </param>
    /// <param name="generation">
    /// The expected current generation, including explicit absence.
    /// </param>
    /// <param name="plan">
    /// The complete before/after inventory and immutable library/source boundaries.
    /// </param>
    /// <param name="observer">
    /// An optional owned interruption callback; null disables callbacks.
    /// </param>
    /// <returns>
    /// The established decision, retaining failures after commit as follow-up debt.
    /// </returns>
    internal TrustedLocalPublicationOutcome PublishNamespaceWithOutcome(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, TrustedLocalNamespacePlan plan,
        Action<TrustedLocalPublicationPhase, int>? observer = null) =>
        PublishWithOutcome(lease, attempt => PublishNamespaceCore(lease, generation, plan, observer, attempt));

    /// <summary>
    /// Validates admission before the coordinator enters the typed publication attempt.
    /// </summary>
    /// <param name="lease">
    /// The current replacement lease.
    /// </param>
    /// <param name="generation">
    /// The expected current generation.
    /// </param>
    /// <param name="plan">
    /// The complete candidate namespace.
    /// </param>
    internal void ValidateNamespaceBeforePublication(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, TrustedLocalNamespacePlan plan)
    {
        var journal = CreateNamespaceJournal(generation, plan);
        ValidateNamespaceJournal(journal);
        PreflightNamespace(lease, journal, exactAfter: false, exactBefore: true);
    }

    /// <summary>
    /// Binds the exact generation member and a new transaction to immutable namespace descriptors.
    /// </summary>
    /// <param name="generation">
    /// The before-generation binding.
    /// </param>
    /// <param name="plan">
    /// The complete candidate inventory.
    /// </param>
    /// <returns>
    /// Pending in-memory evidence; no filesystem mutation is performed.
    /// </returns>
    private NamespaceJournal CreateNamespaceJournal(TrustedLocalGeneration generation, TrustedLocalNamespacePlan plan)
    {
        var members = plan.Changes.ToArray();
        var member = members.SingleOrDefault(change => MemberComparer.Equals(change.Path, _generationPath))
            ?? throw Conflict("Namespace replacement must declare its exact generation file.");
        var after = member.After.Kind == TrustedLocalNamespaceKind.File
            ? ParseGeneration(member.After.FileImage!) : throw Conflict("Namespace replacement must establish a generation.");
        return new NamespaceJournal
        {
            Format = 3, TransactionId = Guid.NewGuid().ToString("N"), Committed = false,
            GenerationBefore = generation, GenerationAfter = after, NamespaceRoot = plan.RootPath,
            Members = members, Boundaries = plan.Boundaries.ToArray()
        };
    }

    /// <summary>
    /// Carries out the one namespace publication after complete before-state admission.
    /// </summary>
    /// <param name="lease">
    /// The active canonical lease.
    /// </param>
    /// <param name="generation">
    /// The expected before generation.
    /// </param>
    /// <param name="plan">
    /// The exact namespace plan.
    /// </param>
    /// <param name="observer">
    /// Optional owned callbacks at actual publication cuts.
    /// </param>
    /// <param name="attempt">
    /// The existing typed attempt, retaining prepared and committed decisions.
    /// </param>
    /// <returns>
    /// The committed generation and member report.
    /// </returns>
    private TrustedLocalPublicationResult PublishNamespaceCore(FileSystemManager.CanonicalWriteLease lease,
        TrustedLocalGeneration generation, TrustedLocalNamespacePlan plan,
        Action<TrustedLocalPublicationPhase, int>? observer, PublicationAttempt attempt)
    {
        BeginPublication(lease, generation, plan.Changes.Count);
        var journal = CreateNamespaceJournal(generation, plan);
        ValidateNamespaceJournal(journal);
        PreflightNamespace(lease, journal, exactAfter: false, exactBefore: true);
        attempt.NamespacePrepared = journal;
        var scratch = NamespaceScratchScope(journal);
        foreach (var path in NamespaceScratchPaths(journal))
            if (File.Exists(scratch.ValidateFile(path))) throw Conflict("A namespace scratch name already exists.");
        journal = WriteNamespaceJournal(IntentStage, journal);
        Observe(lease, observer, TrustedLocalPublicationPhase.IntentStaged);
        File.Move(_journalScope.ValidateFile(IntentStage, false), _journalScope.ValidateFile(Active), overwrite: false);
        journal = RebindNamespaceJournal(journal, Active);
        attempt.NamespacePrepared = journal;
        Observe(lease, observer, TrustedLocalPublicationPhase.IntentPublished);
        PreflightNamespace(lease, journal, exactAfter: false, exactBefore: true);
        ReconcileNamespace(lease, journal, after: true, observer);
        PreflightNamespace(lease, journal, exactAfter: true, exactBefore: false);
        journal = WriteNamespaceJournal(CommitStage, journal with { Committed = true });
        Observe(lease, observer, TrustedLocalPublicationPhase.CommitStaged);
        // A callback cannot smuggle a changed target into the durable decision.
        PreflightNamespace(lease, journal, exactAfter: true, exactBefore: false);
        File.Move(_journalScope.ValidateFile(CommitStage, false), _journalScope.ValidateFile(Active, false), overwrite: true);
        journal = RebindNamespaceJournal(journal, Active);
        var result = new TrustedLocalPublicationResult(journal.TransactionId, journal.GenerationAfter,
            journal.Members.Select(member => new TrustedLocalPublishedMember(member.Path,
                member.After.Kind != TrustedLocalNamespaceKind.Missing, member.After.FileImage?.Sha256)).ToArray());
        attempt.Committed = result;
        Observe(lease, observer, TrustedLocalPublicationPhase.Committed);
        CleanupNamespace(lease, journal, observer);
        return result;
    }

    /// <summary>
    /// Validates all decoded paths, shapes, image bytes and immutable boundary identities before recovery.
    /// </summary>
    /// <param name="journal">
    /// The complete v3 evidence whose sources reopen its closed self-contained frame.
    /// </param>
    private void ValidateNamespaceJournal(NamespaceJournal journal)
    {
        var root = NormalizeAuthorityPath(_files.GameSessionPath, OperatingSystem.IsWindows());
        if (journal.Format != 3 || !ValidId(journal.TransactionId) || journal.Members is not { Length: > 0 } ||
            journal.Boundaries == null || journal.NamespaceRoot != root || _scope.ValidateNamespacePath(root) != root)
            throw Conflict("The namespace journal identity is invalid.");
        ValidateGeneration(journal.GenerationBefore);
        ValidateGeneration(journal.GenerationAfter);
        if (!journal.GenerationAfter.Exists) throw Conflict("Namespace replacement cannot remove generation.");
        var nodes = NamespaceNodes(journal);
        if (!nodes.TryGetValue(root, out var rootNode) || rootNode.Before.Kind != TrustedLocalNamespaceKind.Directory ||
            rootNode.After.Kind != TrustedLocalNamespaceKind.Directory)
            throw Conflict("Namespace replacement must retain the exact session root directory.");
        var generation = nodes.GetValueOrDefault(_generationPath)
            ?? throw Conflict("Namespace replacement lacks its generation member.");
        if (generation.Before.Kind is not (TrustedLocalNamespaceKind.Missing or TrustedLocalNamespaceKind.File) ||
            generation.After.Kind != TrustedLocalNamespaceKind.File ||
            ParseGeneration(generation.Before.FileImage ?? TrustedLocalFileImage.FromBytes(null)) != journal.GenerationBefore ||
            ParseGeneration(generation.After.FileImage!) != journal.GenerationAfter)
            throw Conflict("Namespace generation images do not match the decision.");
        foreach (var required in _files.LoadReplacementRequiredDirectoryPaths())
            if (!nodes.TryGetValue(required, out var requiredNode) || requiredNode.After.Kind != TrustedLocalNamespaceKind.Directory)
                throw Conflict("Namespace replacement must retain required directories.");
        var library = NormalizeAuthorityPath(_files.ResolvePath("saves"), OperatingSystem.IsWindows());
        var boundaries = new Dictionary<string, TrustedLocalNamespaceBoundary>(MemberComparer);
        foreach (var boundary in journal.Boundaries)
        {
            if (boundary == null || boundary.Path != _scope.ValidateNamespacePath(boundary.Path) ||
                !NamespaceWithin(boundary.Path, root) || !boundaries.TryAdd(boundary.Path, boundary) ||
                !Enum.IsDefined(boundary.Kind) || boundary.Length < 0)
                throw Conflict("Invalid namespace boundary.");
            if (boundary.Kind == TrustedLocalNamespaceKind.File)
            {
                if (!ValidNamespaceHash(boundary.Sha256)) throw Conflict("Invalid boundary file digest.");
            }
            else if (boundary.Length != 0 || boundary.Sha256 != null)
                throw Conflict("A non-file boundary contains file metadata.");
        }
        if (!boundaries.TryGetValue(library, out var libraryBoundary) ||
            libraryBoundary.Kind is not (TrustedLocalNamespaceKind.Directory or TrustedLocalNamespaceKind.Missing) ||
            boundaries.Count > 2 || boundaries.Values.Any(boundary =>
                !MemberComparer.Equals(boundary.Path, library) && boundary.Kind != TrustedLocalNamespaceKind.File))
            throw Conflict("Namespace boundaries must be the canonical library and optional exact selected source.");
        foreach (var member in journal.Members)
        {
            foreach (var boundary in boundaries.Values)
                if (MemberComparer.Equals(member.Path, boundary.Path) || NamespaceWithin(member.Path, boundary.Path) ||
                    (NamespaceWithin(boundary.Path, member.Path) &&
                     (member.Before.Kind != TrustedLocalNamespaceKind.Directory || member.After.Kind != TrustedLocalNamespaceKind.Directory)))
                    throw Conflict("Namespace member conflicts with a protected boundary or its ancestors.");
            if (MemberComparer.Equals(member.Path, _generationPath) || MemberComparer.Equals(member.Path, root)) continue;
            if (!NamespaceWithin(member.Path, root)) throw Conflict("Namespace member is outside the exact session root.");
            var parent = Path.GetDirectoryName(member.Path)!;
            if (!nodes.TryGetValue(parent, out var parentNode) ||
                (member.Before.Kind != TrustedLocalNamespaceKind.Missing && parentNode.Before.Kind != TrustedLocalNamespaceKind.Directory) ||
                (member.After.Kind != TrustedLocalNamespaceKind.Missing && parentNode.After.Kind != TrustedLocalNamespaceKind.Directory))
                throw Conflict("Namespace member lacks an explicit compatible parent.");
        }
        foreach (var boundary in boundaries.Values)
        {
            if (!MemberComparer.Equals(boundary.Path, library) && NamespaceWithin(boundary.Path, library))
            {
                if (libraryBoundary.Kind != TrustedLocalNamespaceKind.Directory)
                    throw Conflict("A selected source cannot exist within a missing library.");
                continue;
            }
            if (!nodes.TryGetValue(Path.GetDirectoryName(boundary.Path)!, out var parent) ||
                parent.Before.Kind != TrustedLocalNamespaceKind.Directory || parent.After.Kind != TrustedLocalNamespaceKind.Directory)
                throw Conflict("A protected boundary lacks its immutable covered ancestor.");
        }
        var occupied = nodes.Keys.Concat(boundaries.Keys).ToHashSet(MemberComparer);
        foreach (var path in NamespaceScratchPaths(journal))
        {
            if (!occupied.Add(path) || boundaries.Keys.Any(boundary =>
                MemberComparer.Equals(path, boundary) || NamespaceWithin(path, boundary)))
                throw Conflict("Namespace scratch conflicts with recorded authority.");
            _scope.ValidateNamespacePath(path);
        }
    }

    /// <summary>
    /// Builds one native-comparison index while validating normalized member identities and exact images.
    /// </summary>
    /// <param name="journal">
    /// The namespace evidence to index.
    /// </param>
    /// <returns>
    /// Unique admitted nodes; no current namespace is changed.
    /// </returns>
    private Dictionary<string, TrustedLocalNamespaceChange> NamespaceNodes(NamespaceJournal journal)
    {
        var nodes = new Dictionary<string, TrustedLocalNamespaceChange>(MemberComparer);
        foreach (var member in journal.Members)
        {
            if (member == null || member.Path != _scope.ValidateNamespacePath(member.Path) || !nodes.TryAdd(member.Path, member))
                throw Conflict("Invalid or duplicate namespace member.");
            ValidateNamespaceImage(member.Before);
            ValidateNamespaceImage(member.After);
        }
        return nodes;
    }

    /// <summary>
    /// Validates the exact payload contract of a namespace image.
    /// </summary>
    /// <param name="image">
    /// The declared missing, directory or regular-file image.
    /// </param>
    private static void ValidateNamespaceImage(TrustedLocalNamespaceImage image)
    {
        if (image == null || !Enum.IsDefined(image.Kind)) throw Conflict("Invalid namespace image kind.");
        if (image.Kind == TrustedLocalNamespaceKind.File)
        {
            if (image.FileImage is not { Exists: true }) throw Conflict("A file namespace image lacks exact bytes.");
            image.FileImage.Validate();
        }
        else if (image.FileImage != null) throw Conflict("A non-file namespace image contains bytes.");
    }

    /// <summary>
    /// Tests containment with the native name comparison, retaining component boundaries.
    /// </summary>
    /// <param name="path">
    /// The normalized candidate name.
    /// </param>
    /// <param name="root">
    /// The normalized containing directory.
    /// </param>
    /// <returns>
    /// True only for a descendant, never for the directory itself.
    /// </returns>
    private static bool NamespaceWithin(string path, string root) => path.StartsWith(
        root + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>
    /// Checks the canonical SHA-256 spelling used by exact file-image transport.
    /// </summary>
    /// <param name="hash">
    /// The possibly absent encoded digest.
    /// </param>
    /// <returns>
    /// True only for exactly 64 uppercase hexadecimal characters.
    /// </returns>
    private static bool ValidNamespaceHash(string? hash) => hash is { Length: 64 } && hash.All(value =>
        value is >= '0' and <= '9' or >= 'A' and <= 'F');

    /// <summary>
    /// Admits every current node, protected boundary and direct child before the first recovery mutation.
    /// </summary>
    /// <param name="lease">
    /// The current canonical lease.
    /// </param>
    /// <param name="journal">
    /// Completely decoded and validated namespace evidence.
    /// </param>
    /// <param name="exactAfter">
    /// Requires the complete after-state when true.
    /// </param>
    /// <param name="exactBefore">
    /// Requires the complete before-state when true; otherwise pending states may be mixed.
    /// </param>
    private void PreflightNamespace(FileSystemManager.CanonicalWriteLease lease, NamespaceJournal journal,
        bool exactAfter, bool exactBefore)
    {
        _files.EnsureCanonicalWriteLeaseActive(lease);
        ValidateJournalDirectory();
        var generation = ReadGeneration(lease);
        if (generation != (exactBefore ? journal.GenerationBefore : journal.GenerationAfter) &&
            (exactAfter || exactBefore || generation != journal.GenerationBefore))
            throw Conflict("Namespace evidence belongs to another generation.");
        var nodes = journal.Members.ToDictionary(member => member.Path, MemberComparer);
        var boundaries = journal.Boundaries.ToDictionary(boundary => boundary.Path, MemberComparer);
        var scratchPaths = NamespaceScratchPaths(journal).ToHashSet(MemberComparer);
        var scratch = NamespaceScratchScope(journal);
        foreach (var path in scratchPaths) scratch.ValidateFile(path);
        foreach (var boundary in journal.Boundaries)
            if (!MatchesNamespaceBoundary(boundary)) throw Conflict("A protected namespace boundary changed.");
        foreach (var member in journal.Members.OrderBy(member => NamespaceDepth(member.Path)))
        {
            var actual = _scope.ObserveNamespace(member.Path);
            if (actual.BlockingFileAncestor != null)
            {
                if (!nodes.TryGetValue(actual.BlockingFileAncestor, out var blocker) ||
                    !NodeMatchesAllowed(blocker, _scope.ObserveNamespace(blocker.Path), exactAfter, exactBefore))
                    throw Conflict("An undeclared file blocks a namespace descendant.");
            }
            if (!NodeMatchesAllowed(member, actual, exactAfter, exactBefore))
                throw Conflict("A namespace member has unknown kind or bytes; evidence retained.");
            if (actual.Kind != TrustedLocalNamespaceKind.Directory) continue;
            foreach (var entry in Directory.EnumerateFileSystemEntries(_scope.ValidateDirectory(member.Path, false)))
            {
                var path = _scope.ValidateNamespacePath(entry);
                if (!nodes.ContainsKey(path) && !boundaries.ContainsKey(path) && !scratchPaths.Contains(path))
                    throw Conflict("An unknown namespace child would be lost during recovery; evidence retained.");
            }
        }
    }

    /// <summary>
    /// Matches an observed node against an exact target or the admissible pending states.
    /// </summary>
    /// <param name="member">
    /// The admitted node's before and after descriptors.
    /// </param>
    /// <param name="actual">
    /// Its non-following observation; any file blocker has been admitted by the caller.
    /// </param>
    /// <param name="exactAfter">
    /// Selects exact after-state matching.
    /// </param>
    /// <param name="exactBefore">
    /// Selects exact before-state matching.
    /// </param>
    /// <returns>
    /// True for an exact target or an allowed pending snapshot/conversion gap.
    /// </returns>
    private bool NodeMatchesAllowed(TrustedLocalNamespaceChange member, TrustedLocalNamespaceObservation actual,
        bool exactAfter, bool exactBefore)
    {
        if (exactAfter) return NamespaceImageMatches(member.Path, member.After, actual);
        if (exactBefore) return NamespaceImageMatches(member.Path, member.Before, actual);
        return NamespaceImageMatches(member.Path, member.Before, actual) ||
            NamespaceImageMatches(member.Path, member.After, actual) ||
            (actual.Kind == TrustedLocalNamespaceKind.Missing &&
             ((member.Before.Kind == TrustedLocalNamespaceKind.File && member.After.Kind == TrustedLocalNamespaceKind.Directory) ||
              (member.Before.Kind == TrustedLocalNamespaceKind.Directory && member.After.Kind == TrustedLocalNamespaceKind.File)));
    }

    /// <summary>
    /// Matches exact regular bytes or an explicit ordinary missing/directory kind.
    /// </summary>
    /// <param name="path">
    /// The admitted normalized node path.
    /// </param>
    /// <param name="image">
    /// The desired exact image.
    /// </param>
    /// <param name="actual">
    /// The already observed non-following kind.
    /// </param>
    /// <returns>
    /// True only when kind and, for a file, length/digest match.
    /// </returns>
    private bool NamespaceImageMatches(string path, TrustedLocalNamespaceImage image, TrustedLocalNamespaceObservation actual) =>
        actual.Kind == image.Kind && (image.Kind != TrustedLocalNamespaceKind.File || image.FileImage!.MatchesFile(_scope, path));

    /// <summary>
    /// Validates a protected library/source without enumerating the library or obtaining mutation authority.
    /// </summary>
    /// <param name="boundary">
    /// The exact immutable boundary descriptor.
    /// </param>
    /// <returns>
    /// True when its ordinary kind and optional exact file content remain admitted.
    /// </returns>
    private bool MatchesNamespaceBoundary(TrustedLocalNamespaceBoundary boundary)
    {
        var actual = _scope.ObserveNamespace(boundary.Path);
        if (actual.BlockingFileAncestor != null || actual.Kind != boundary.Kind) return false;
        if (boundary.Kind != TrustedLocalNamespaceKind.File) return true;
        using var stream = new FileStream(_scope.ValidateFile(boundary.Path, false), FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete, TrustedLocalFileImage.CopyBufferSize, FileOptions.SequentialScan);
        if (stream.Length != boundary.Length) return false;
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        _scope.ValidateFile(boundary.Path, false);
        return stream.Position == boundary.Length && stream.Length == boundary.Length && hash == boundary.Sha256;
    }

    /// <summary>
    /// Confirms exact complete rollback state before returning a typed rolled-back outcome.
    /// </summary>
    /// <param name="lease">
    /// The active canonical lease.
    /// </param>
    /// <param name="journal">
    /// The prepared namespace evidence retained by the attempt.
    /// </param>
    /// <param name="after">
    /// Selects the after target when true, otherwise the complete before target.
    /// </param>
    /// <returns>
    /// True after exact namespace/generation confirmation; conflicts throw and establish no safe outcome.
    /// </returns>
    private bool NamespaceMatchesExact(FileSystemManager.CanonicalWriteLease lease, NamespaceJournal journal, bool after)
    {
        PreflightNamespace(lease, journal, exactAfter: after, exactBefore: !after);
        return true;
    }

    /// <summary>
    /// Recovers pending namespace evidence or retries only cleanup for an established commit.
    /// </summary>
    /// <param name="lease">
    /// The current canonical lease.
    /// </param>
    /// <param name="journal">
    /// Fully validated v3 evidence read from active authority.
    /// </param>
    /// <param name="observer">
    /// Optional owned recovery interruption callbacks.
    /// </param>
    private void RecoverNamespace(FileSystemManager.CanonicalWriteLease lease, NamespaceJournal journal,
        Action<TrustedLocalPublicationPhase, int>? observer)
    {
        PreflightNamespace(lease, journal, exactAfter: journal.Committed, exactBefore: false);
        if (!journal.Committed)
        {
            ReconcileNamespace(lease, journal, after: false, observer);
            PreflightNamespace(lease, journal, exactAfter: false, exactBefore: true);
        }
        CleanupNamespace(lease, journal, observer);
    }

    /// <summary>
    /// Counts native path components for deterministic parent/child reconciliation ordering.
    /// </summary>
    /// <param name="path">
    /// The normalized absolute name.
    /// </param>
    /// <returns>
    /// Its directory-separator count.
    /// </returns>
    private static int NamespaceDepth(string path) => path.Count(value => value == Path.DirectorySeparatorChar);

    /// <summary>
    /// Derives exact transaction-owned names beneath an ancestor that remains an ordinary directory in both snapshots.
    /// </summary>
    /// <param name="journal">
    /// Validated namespace descriptors and transaction identity.
    /// </param>
    /// <param name="index">
    /// The stable file-bearing node index.
    /// </param>
    /// <param name="rollback">
    /// Selects undo rather than forward stage.
    /// </param>
    /// <param name="nodes">
    /// The one admitted node index reused for all names in this operation.
    /// </param>
    /// <returns>
    /// An exact reserved scratch name outside convertible or protected subtrees.
    /// </returns>
    private string NamespaceScratchPath(NamespaceJournal journal, int index, bool rollback,
        IReadOnlyDictionary<string, TrustedLocalNamespaceChange> nodes)
    {
        var path = journal.Members[index].Path;
        var parent = Path.GetDirectoryName(path)!;
        if (!MemberComparer.Equals(path, _generationPath))
        {
            while (!nodes.TryGetValue(parent, out var anchor) || anchor.Before.Kind != TrustedLocalNamespaceKind.Directory ||
                   anchor.After.Kind != TrustedLocalNamespaceKind.Directory)
                parent = Path.GetDirectoryName(parent) ?? throw Conflict("A namespace file lacks a stable scratch anchor.");
        }
        _scope.ValidateDirectory(parent, false);
        return Path.Combine(parent, $".boe-local-{journal.TransactionId}-{index}.{(rollback ? "undo" : "stage")}");
    }

    /// <summary>
    /// Enumerates only file-bearing nodes' exact forward and rollback scratch names.
    /// </summary>
    /// <param name="journal">
    /// The admitted namespace inventory.
    /// </param>
    /// <returns>
    /// Exact names derived from the transaction and stable node indices.
    /// </returns>
    private IEnumerable<string> NamespaceScratchPaths(NamespaceJournal journal)
    {
        var nodes = journal.Members.ToDictionary(member => member.Path, MemberComparer);
        for (var index = 0; index < journal.Members.Length; index++)
            if (journal.Members[index].Before.Kind == TrustedLocalNamespaceKind.File || journal.Members[index].After.Kind == TrustedLocalNamespaceKind.File)
            {
                yield return NamespaceScratchPath(journal, index, false, nodes);
                yield return NamespaceScratchPath(journal, index, true, nodes);
            }
    }

    /// <summary>
    /// Constrains scratch cleanup to the exact names declared by this transaction.
    /// </summary>
    /// <param name="journal">
    /// The validated namespace evidence.
    /// </param>
    /// <returns>
    /// An exact-file-only grant, with stable existing parents checked.
    /// </returns>
    private TrustedLocalFileScope NamespaceScratchScope(NamespaceJournal journal) => new([], NamespaceScratchPaths(journal));

    /// <summary>
    /// Reconciles known ordinary nodes nonrecursively, then publishes the generation file last.
    /// </summary>
    /// <param name="lease">
    /// The active canonical lease.
    /// </param>
    /// <param name="journal">
    /// The complete admitted namespace and its self-contained images.
    /// </param>
    /// <param name="after">
    /// Selects forward publication when true and exact rollback when false.
    /// </param>
    /// <param name="observer">
    /// Optional actual-operation interruption callbacks.
    /// </param>
    private void ReconcileNamespace(FileSystemManager.CanonicalWriteLease lease, NamespaceJournal journal, bool after,
        Action<TrustedLocalPublicationPhase, int>? observer)
    {
        var nodes = journal.Members.ToDictionary(member => member.Path, MemberComparer);
        var indexed = journal.Members.Select((member, index) => (Member: member, Index: index)).ToArray();
        var scratch = NamespaceScratchScope(journal);
        TrustedLocalNamespaceImage Target(TrustedLocalNamespaceChange member) => after ? member.After : member.Before;
        TrustedLocalNamespaceObservation Recheck(TrustedLocalNamespaceChange member)
        {
            var actual = _scope.ObserveNamespace(member.Path);
            if (actual.BlockingFileAncestor != null && (!nodes.TryGetValue(actual.BlockingFileAncestor, out var blocker) ||
                !NodeMatchesAllowed(blocker, _scope.ObserveNamespace(blocker.Path), false, false)))
                throw Conflict("An unknown blocker appeared during namespace reconciliation.");
            if (!NodeMatchesAllowed(member, actual, false, false))
                throw Conflict("A namespace member changed during reconciliation.");
            return actual;
        }
        foreach (var (member, index) in indexed)
        {
            if (MemberComparer.Equals(member.Path, _generationPath) || Target(member).Kind == TrustedLocalNamespaceKind.File) continue;
            if (Recheck(member).Kind != TrustedLocalNamespaceKind.File) continue;
            _scope.DeleteOwnedFile(member.Path);
            Observe(lease, observer, after ? TrustedLocalPublicationPhase.MemberPublished : TrustedLocalPublicationPhase.MemberRestored, index);
        }
        foreach (var (member, index) in indexed.OrderByDescending(value => NamespaceDepth(value.Member.Path)))
        {
            if (Target(member).Kind == TrustedLocalNamespaceKind.Directory || MemberComparer.Equals(member.Path, _generationPath)) continue;
            if (Recheck(member).Kind != TrustedLocalNamespaceKind.Directory) continue;
            var path = _scope.ValidateDirectory(member.Path, false);
            if (Directory.EnumerateFileSystemEntries(path).Any()) throw Conflict("A convertible directory is not empty.");
            Directory.Delete(path, recursive: false);
            Observe(lease, observer, after ? TrustedLocalPublicationPhase.DirectoryRemoved : TrustedLocalPublicationPhase.RollbackDirectoryRemoved, index);
        }
        foreach (var (member, index) in indexed.OrderBy(value => NamespaceDepth(value.Member.Path)))
        {
            if (Target(member).Kind != TrustedLocalNamespaceKind.Directory || Recheck(member).Kind == TrustedLocalNamespaceKind.Directory) continue;
            _scope.EnsureDirectory(member.Path);
            Observe(lease, observer, after ? TrustedLocalPublicationPhase.DirectoryCreated : TrustedLocalPublicationPhase.RollbackDirectoryCreated, index);
        }
        foreach (var (member, index) in indexed.OrderBy(value => MemberComparer.Equals(value.Member.Path, _generationPath) ? 1 : 0))
        {
            var target = Target(member);
            if (target.Kind != TrustedLocalNamespaceKind.File) continue;
            var actual = Recheck(member);
            if (!after && NamespaceImageMatches(member.Path, target, actual))
            {
                Observe(lease, observer, TrustedLocalPublicationPhase.MemberRestored, index);
                continue;
            }
            var stage = NamespaceScratchPath(journal, index, rollback: !after, nodes);
            scratch.DeleteOwnedFile(stage);
            WriteNew(scratch, stage, target.FileImage!);
            Observe(lease, observer, after ? TrustedLocalPublicationPhase.MemberStaged : TrustedLocalPublicationPhase.RollbackStaged, index);
            actual = Recheck(member);
            if (actual.Kind == TrustedLocalNamespaceKind.Directory) throw Conflict("A target file still occupies a directory.");
            File.Move(scratch.ValidateFile(stage, false), _scope.ValidateFile(member.Path),
                overwrite: actual.Kind == TrustedLocalNamespaceKind.File);
            Observe(lease, observer, after ? TrustedLocalPublicationPhase.MemberPublished : TrustedLocalPublicationPhase.MemberRestored, index);
        }
    }

    /// <summary>
    /// Retains active evidence until all other exact owned cleanup and target revalidation succeeds.
    /// </summary>
    /// <param name="lease">
    /// The active canonical lease.
    /// </param>
    /// <param name="journal">
    /// The established committed or fully restored pending decision.
    /// </param>
    /// <param name="observer">
    /// Optional owned cleanup interruption callbacks.
    /// </param>
    private void CleanupNamespace(FileSystemManager.CanonicalWriteLease lease, NamespaceJournal journal,
        Action<TrustedLocalPublicationPhase, int>? observer)
    {
        PreflightNamespace(lease, journal, exactAfter: journal.Committed, exactBefore: !journal.Committed);
        var nodes = journal.Members.ToDictionary(member => member.Path, MemberComparer);
        var scratch = NamespaceScratchScope(journal);
        for (var index = 0; index < journal.Members.Length; index++)
        {
            var member = journal.Members[index];
            if (member.Before.Kind != TrustedLocalNamespaceKind.File && member.After.Kind != TrustedLocalNamespaceKind.File) continue;
            scratch.DeleteOwnedFile(NamespaceScratchPath(journal, index, false, nodes));
            scratch.DeleteOwnedFile(NamespaceScratchPath(journal, index, true, nodes));
            Observe(lease, observer, TrustedLocalPublicationPhase.CleanupMember, index);
        }
        _journalScope.DeleteOwnedFile(IntentStage);
        _journalScope.DeleteOwnedFile(CommitStage);
        PreflightNamespace(lease, journal, exactAfter: journal.Committed, exactBefore: !journal.Committed);
        _journalScope.DeleteOwnedFile(Active);
        Observe(lease, observer, TrustedLocalPublicationPhase.CleanupComplete);
    }
}
