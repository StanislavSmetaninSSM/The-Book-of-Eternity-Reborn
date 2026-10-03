using System.Text.Json;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private static readonly TrustedLocalNamespaceImage LoadNamespaceMissing = new(TrustedLocalNamespaceKind.Missing, null);
    private static readonly TrustedLocalNamespaceImage LoadNamespaceDirectory = new(TrustedLocalNamespaceKind.Directory, null);

    /// <summary>
    /// Resolves an exact load inventory name without discarding native payload spelling.
    /// </summary>
    /// <param name="relative">
    /// The session-relative name, or empty for the immutable session root.
    /// </param>
    /// <returns>
    /// The normalized confined absolute path; no current file/directory shape is assumed.
    /// </returns>
    private string ResolveLoadNamespacePath(string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(component => component is "." or "..") ||
            (relative.Length > 0 && relative.Split('/').Any(string.IsNullOrEmpty)))
            throw new InvalidDataException("Invalid relative load namespace name.");
        var path = Path.Combine(GameSessionPath, relative.Replace('/', Path.DirectorySeparatorChar));
        return new TrustedLocalFileScope([BasePath]).ValidateNamespacePath(path);
    }

    /// <summary>
    /// Returns required covered directories, excluding all descendants of the opaque save library.
    /// </summary>
    /// <returns>
    /// Exact absolute required directory paths for namespace admission and plan construction.
    /// </returns>
    internal IReadOnlyList<string> LoadReplacementRequiredDirectoryPaths() => RequiredDirectories
        .Where(path => !path.StartsWith("game_session/saves/", StringComparison.Ordinal))
        .Select(path => ResolveLoadNamespacePath(path["game_session/".Length..])).ToArray();

    /// <summary>
    /// Captures every ordinary file and directory while keeping the library opaque and selected source immutable.
    /// </summary>
    /// <param name="lease">
    /// The active ordinary session-replacement lease.
    /// </param>
    /// <param name="selectedSource">
    /// The prepared exact source path. Only an in-session source becomes a recovery boundary.
    /// </param>
    /// <returns>
    /// One complete session-relative snapshot and exact protected boundaries.
    /// </returns>
    internal CanonicalLoadNamespaceSnapshot CaptureLoadReplacementNamespace(CanonicalWriteLease lease, string selectedSource)
    {
        EnsureValidSessionReplacementLease(lease);
        EnsureNoLegacyStorageEvidence();
        var scope = new TrustedLocalFileScope([BasePath]);
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var root = scope.ValidateDirectory(GameSessionPath, false);
        var library = ResolveLoadNamespacePath("saves");
        var libraryKind = scope.ObserveNamespace(library);
        if (libraryKind.BlockingFileAncestor != null || libraryKind.Kind == TrustedLocalNamespaceKind.File)
            throw new InvalidDataException("The canonical save library must be an ordinary directory or absent.");
        var boundaries = new List<TrustedLocalNamespaceBoundary> { new(library, libraryKind.Kind, 0, null) };
        var source = TrustedLocalFilePublication.NormalizeAuthorityPath(selectedSource, OperatingSystem.IsWindows());
        if (source.StartsWith(root + Path.DirectorySeparatorChar, comparison))
        {
            var image = TrustedLocalFileImage.CaptureFile(scope, source);
            boundaries.Add(new(source, TrustedLocalNamespaceKind.File, image.Length, image.Sha256));
        }
        var protectedNames = boundaries.Select(boundary => boundary.Path).ToHashSet(comparer);
        var nodes = new Dictionary<string, TrustedLocalNamespaceImage>(comparer) { [""] = LoadNamespaceDirectory };
        var directories = new Queue<string>();
        directories.Enqueue(root);
        while (directories.TryDequeue(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(scope.ValidateDirectory(directory, false)))
            {
                var path = scope.ValidateNamespacePath(entry);
                if (protectedNames.Contains(path)) continue;
                var actual = scope.ObserveNamespace(path);
                var relative = GetLocalRelativePath(root, path, OperatingSystem.IsWindows());
                if (actual.BlockingFileAncestor != null || actual.Kind == TrustedLocalNamespaceKind.Missing)
                    throw new InvalidDataException("The live namespace changed during capture.");
                if (actual.Kind == TrustedLocalNamespaceKind.Directory)
                {
                    nodes.Add(relative, LoadNamespaceDirectory);
                    directories.Enqueue(path);
                }
                else nodes.Add(relative, new(TrustedLocalNamespaceKind.File, TrustedLocalFileImage.CaptureFile(scope, path)));
            }
        }
        return new(nodes, boundaries);
    }

    /// <summary>
    /// Builds one complete replacement plan, retaining harmless directories and exact unimported configuration.
    /// </summary>
    /// <param name="snapshot">
    /// The captured original nodes and manager-admitted boundaries.
    /// </param>
    /// <param name="incoming">
    /// The originally validated prepared payload images keyed by exact relative name.
    /// </param>
    /// <param name="preserveConfiguration">
    /// Retains exact live configuration bytes or absence when the archive has no configuration.
    /// </param>
    /// <returns>
    /// Complete changes including declared incoming/required directories; generation is appended only during publication.
    /// </returns>
    internal CanonicalLoadNamespacePlan CreateLoadReplacementNamespacePlan(CanonicalLoadNamespaceSnapshot snapshot,
        IReadOnlyDictionary<string, TrustedLocalFileImage> incoming, bool preserveConfiguration)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var after = snapshot.Nodes.ToDictionary(pair => pair.Key,
            pair => pair.Value.Kind == TrustedLocalNamespaceKind.Directory ? LoadNamespaceDirectory : LoadNamespaceMissing, comparer);
        if (preserveConfiguration) after["config.json"] = snapshot.Nodes.GetValueOrDefault("config.json") ?? LoadNamespaceMissing;
        foreach (var (relative, image) in incoming)
        {
            ResolveLoadNamespacePath(relative);
            after[relative] = new(TrustedLocalNamespaceKind.File, image);
            var parent = Path.GetDirectoryName(relative.Replace('/', Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(parent))
            {
                var name = parent.Replace(Path.DirectorySeparatorChar, '/');
                if (incoming.ContainsKey(name)) throw new InvalidDataException("Incoming file and directory paths conflict.");
                after[name] = LoadNamespaceDirectory;
                parent = Path.GetDirectoryName(parent);
            }
        }
        var required = LoadReplacementRequiredDirectoryPaths().Select(path => GetLocalRelativePath(GameSessionPath, path,
            OperatingSystem.IsWindows())).ToHashSet(comparer);
        foreach (var relative in required)
        {
            if (after.TryGetValue(relative, out var image) && image.Kind == TrustedLocalNamespaceKind.File)
                throw new InvalidDataException("Incoming file occupies a required directory.");
            after[relative] = LoadNamespaceDirectory;
            var parent = Path.GetDirectoryName(relative.Replace('/', Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(parent))
            {
                var name = parent.Replace(Path.DirectorySeparatorChar, '/');
                if (incoming.ContainsKey(name)) throw new InvalidDataException("Incoming file blocks a required directory.");
                after[name] = LoadNamespaceDirectory;
                parent = Path.GetDirectoryName(parent);
            }
        }
        foreach (var relative in after.Keys.ToArray())
        {
            var parent = Path.GetDirectoryName(relative.Replace('/', Path.DirectorySeparatorChar));
            while (!string.IsNullOrEmpty(parent))
            {
                var name = parent.Replace(Path.DirectorySeparatorChar, '/');
                if (incoming.ContainsKey(name))
                {
                    if (required.Contains(relative)) throw new InvalidDataException("Incoming file blocks a required directory.");
                    after[relative] = LoadNamespaceMissing;
                    break;
                }
                parent = Path.GetDirectoryName(parent);
            }
        }
        var changes = snapshot.Nodes.Keys.Union(after.Keys, comparer).OrderBy(path => path, StringComparer.Ordinal)
            .Select(relative => new CanonicalLoadNamespaceChange(relative,
                snapshot.Nodes.GetValueOrDefault(relative) ?? LoadNamespaceMissing,
                after.GetValueOrDefault(relative) ?? LoadNamespaceMissing)).ToArray();
        return new(changes, snapshot.Boundaries);
    }

    /// <summary>
    /// Revalidates the complete original namespace and publishes its replacement without pre-intent live directory creation.
    /// </summary>
    /// <param name="lease">
    /// The active ordinary replacement lease.
    /// </param>
    /// <param name="expectedGeneration">
    /// Exact prior generation binding and bytes.
    /// </param>
    /// <param name="plan">
    /// The complete prepared session-relative namespace and immutable boundaries.
    /// </param>
    /// <param name="replacementGeneration">
    /// The canonical new generation ID to establish in the same decision.
    /// </param>
    /// <param name="validatePreparedNamespace">
    /// Revalidates the selected source, private preparation and caller-owned evidence; must not mutate the session.
    /// </param>
    /// <param name="cancellationToken">
    /// Cancels before an established decision; the default token does not request cancellation.
    /// </param>
    /// <returns>
    /// The established publication decision, preserving uncertainty and follow-up failures.
    /// </returns>
    internal async Task<TrustedLocalPublicationOutcome> PublishLoadReplacementNamespaceAsync(CanonicalWriteLease lease,
        LocalSessionGenerationSnapshot expectedGeneration, CanonicalLoadNamespacePlan plan, string replacementGeneration,
        Action validatePreparedNamespace, CancellationToken cancellationToken = default)
    {
        if (SessionOperationContext.TryGetExpectedGeneration(BasePath, out _))
            throw new InvalidOperationException("Load cannot run inside a generation-bound session operation.");
        EnsureValidSessionReplacementLease(lease);
        if (lease.MutationIntentRecorder != null || lease.IsLegacyStorageRecovery)
            throw new InvalidOperationException("Ordinary load cannot use a legacy transaction lease.");
        if (!Guid.TryParseExact(replacementGeneration, "N", out var parsed) || parsed.ToString("N") != replacementGeneration)
            throw new InvalidDataException("The replacement generation is invalid.");
        var members = plan.Changes.Select(change => new TrustedLocalNamespaceChange(ResolveLoadNamespacePath(change.RelativePath),
            change.Before, change.After)).ToList();
        members.Add(new(SessionGenerationPath,
            expectedGeneration.Bytes == null ? LoadNamespaceMissing : new(TrustedLocalNamespaceKind.File, TrustedLocalFileImage.FromBytes(expectedGeneration.Bytes)),
            new(TrustedLocalNamespaceKind.File, TrustedLocalFileImage.FromBytes(
                JsonSerializer.SerializeToUtf8Bytes(new SessionGenerationDocument(1, replacementGeneration))))));
        var namespacePlan = new TrustedLocalNamespacePlan(GameSessionPath, members, plan.Boundaries);
        var publisher = new TrustedLocalFilePublication(this, new TrustedLocalFileScope([BasePath]));
        void Revalidate()
        {
            ResolveBackupPublicationRecovery(lease);
            var generation = ReadLocalGenerationSnapshot(lease);
            if (generation.Binding != expectedGeneration.Binding || !ExactBytesEqual(generation.Bytes, expectedGeneration.Bytes))
                throw new InvalidDataException("The generation changed during namespace preparation.");
            validatePreparedNamespace();
            publisher.ValidateNamespaceBeforePublication(lease, expectedGeneration.Binding, namespacePlan);
        }
        var registrations = new List<InProcessMutationRegistration>();
        try
        {
            Revalidate();
            foreach (var member in members) registrations.Add(new InProcessMutationRegistration(member.Path));
            for (var index = 0; index < plan.Changes.Count; index++)
            {
                var change = plan.Changes[index];
                await InvokeBeforeCanonicalMutationBoundaryAsync(change.RelativePath);
                if (ResolveLoadNamespacePath(change.RelativePath) != members[index].Path)
                    throw new InvalidDataException("Canonical namespace identity changed before mutation.");
                await InvokeAfterCanonicalMutationBoundaryValidatedAsync(change.RelativePath);
            }
            for (var attempt = 0; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Revalidate();
                var outcome = publisher.PublishNamespaceWithOutcome(lease, expectedGeneration.Binding, namespacePlan, _hooks?.LocalPublicationObserver);
                if (outcome.Disposition == TrustedLocalPublicationDisposition.Committed)
                    CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
                if (outcome.Disposition != TrustedLocalPublicationDisposition.RolledBack || outcome.Failure is InvalidDataException ||
                    outcome.Failure == null || !IsTransientFileAccessException(outcome.Failure) || attempt >= TransientFileAccessRetryCount - 1)
                    return outcome;
                await Task.Delay(TransientFileAccessRetryDelay, cancellationToken);
            }
        }
        finally { foreach (var registration in registrations) registration.Dispose(); }
    }
}
