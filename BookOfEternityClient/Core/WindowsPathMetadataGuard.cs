namespace BookOfEternityClient.Core;

/// <summary>
/// Identifies the directory-only root or a directory-first descendant component.
/// </summary>
internal enum PathMetadataRole
{
    /// <summary>
    /// Selects the authority root, whose presence is queried only as a directory.
    /// </summary>
    Root,

    /// <summary>
    /// Selects a descendant component, queried as a directory before considering a file.
    /// </summary>
    Segment
}

/// <summary>
/// Identifies an actual managed metadata API attempt, rather than a native syscall count.
/// </summary>
internal enum PathMetadataQueryKind
{
    /// <summary>
    /// Records an attempt to call <see cref="Directory.Exists"/>.
    /// </summary>
    DirectoryExists,

    /// <summary>
    /// Records an attempt to call <see cref="File.Exists"/>.
    /// </summary>
    FileExists,

    /// <summary>
    /// Records an attempt to call the original <see cref="FileSystemManager.IsReparsePoint"/> classifier.
    /// </summary>
    IsReparsePoint,

    /// <summary>
    /// Records a fresh directory observation using one <see cref="FileSystemInfo.Refresh"/> call.
    /// </summary>
    DirectoryRefresh,

    /// <summary>
    /// Records a fresh file observation using one <see cref="FileSystemInfo.Refresh"/> call.
    /// </summary>
    FileRefresh
}

/// <summary>
/// Describes one invocation-scoped managed metadata attempt without carrying storage authority.
/// </summary>
/// <param name="Path">
/// The component's existing spelling supplied to the managed API.
/// </param>
/// <param name="Role">
/// The root or descendant role selected by the walker.
/// </param>
/// <param name="Kind">
/// The managed API attempted; a refresh may issue more than one native request.
/// </param>
internal readonly record struct PathMetadataQuery(string Path, PathMetadataRole Role, PathMetadataQueryKind Kind);

/// <summary>
/// Holds presence and optional cached attributes for one component decision only.
/// </summary>
/// <param name="Exists">
/// Whether the requested directory or file kind was observed; <see langword="false"/> suppresses attribute inspection.
/// </param>
/// <param name="CachedAttributes">
/// Attributes from that observation, or <see langword="null"/> when a positive observation needs the original reparse query.
/// These values confer no authority for later calls or operations.
/// </param>
internal readonly record struct PathMetadataObservation(bool Exists, FileAttributes? CachedAttributes);

/// <summary>
/// Supplies per-component metadata and the original fallback decisions to the internal Windows walker.
/// </summary>
internal interface IWindowsPathMetadataProvider
{
    /// <summary>
    /// Observes directory presence with optional attributes for this component decision.
    /// </summary>
    /// <param name="path">
    /// The required root or descendant component spelling.
    /// </param>
    /// <param name="role">
    /// The directory-only root or directory-first segment role.
    /// </param>
    /// <returns>
    /// Fresh directory presence and optional attributes; <see langword="false"/> never requires attributes.
    /// </returns>
    PathMetadataObservation ObserveDirectory(string path, PathMetadataRole role);

    /// <summary>
    /// Observes file presence after a negative directory observation at a descendant component.
    /// </summary>
    /// <param name="path">
    /// The required descendant component spelling.
    /// </param>
    /// <param name="role">
    /// The segment role; roots are never queried as files by the walker.
    /// </param>
    /// <returns>
    /// Fresh file presence and optional attributes; <see langword="false"/> never requires attributes.
    /// </returns>
    PathMetadataObservation ObserveFile(string path, PathMetadataRole role);

    /// <summary>
    /// Executes the original directory-presence decision after an expected new-observation failure.
    /// </summary>
    /// <param name="path">
    /// The original required component spelling.
    /// </param>
    /// <param name="role">
    /// The root or segment role retained for diagnostics.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the original directory API reports presence; otherwise <see langword="false"/>, including its suppressed errors.
    /// </returns>
    bool LegacyDirectoryExists(string path, PathMetadataRole role);

    /// <summary>
    /// Executes the original file-presence decision only after a negative legacy directory decision.
    /// </summary>
    /// <param name="path">
    /// The original required descendant spelling.
    /// </param>
    /// <param name="role">
    /// The segment role retained for diagnostics.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the original file API reports presence; otherwise <see langword="false"/>, including its suppressed errors.
    /// </returns>
    bool LegacyFileExists(string path, PathMetadataRole role);

    /// <summary>
    /// Executes the original final reparse query, preserving its result and any thrown exception.
    /// </summary>
    /// <param name="path">
    /// The positive component spelling, without replacement or normalization.
    /// </param>
    /// <param name="role">
    /// The root or segment role retained for diagnostics.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the original query observes a reparse point; otherwise <see langword="false"/>.
    /// </returns>
    bool IsReparsePoint(string path, PathMetadataRole role);
}

/// <summary>
/// Preserves the original root and descendant walk using component-local metadata DATA.
/// </summary>
internal static class WindowsPathMetadataGuard
{
    /// <summary>
    /// Checks the real Windows filesystem without retaining observations beyond this invocation.
    /// </summary>
    /// <param name="authorityRoot">
    /// The already normalized authority root; only directory presence is queried at this role.
    /// </param>
    /// <param name="fullPath">
    /// The already normalized and contained root or descendant path.
    /// </param>
    /// <param name="authorityName">
    /// The existing authority label used in original reparse-refusal errors.
    /// </param>
    /// <param name="observer">
    /// An optional passive managed-query observer. Its failures do not affect filesystem decisions.
    /// </param>
    internal static void Ensure(
        string authorityRoot, string fullPath, string authorityName, Action<PathMetadataQuery>? observer) =>
        Ensure(authorityRoot, fullPath, authorityName, new WindowsPathMetadataProvider(observer));

    /// <summary>
    /// Checks the original root and segment decisions through a typed internal metadata provider.
    /// Expected new-observation failures use the original component decision outside their catch.
    /// </summary>
    /// <param name="authorityRoot">
    /// The already normalized authority root; an absent directory does not stop descendant walking.
    /// </param>
    /// <param name="fullPath">
    /// The already normalized and contained candidate; equality with the root stops after root handling.
    /// </param>
    /// <param name="authorityName">
    /// The unchanged authority label in the root and path reparse-refusal messages.
    /// </param>
    /// <param name="provider">
    /// The invocation's metadata dependency. Detached tests may supply DATA, never manager authority.
    /// </param>
    internal static void Ensure(
        string authorityRoot, string fullPath, string authorityName, IWindowsPathMetadataProvider provider)
    {
        var current = authorityRoot;
        if (TryObserve(current, PathMetadataRole.Root, provider, out var rootObservation)
            ? IsObservedReparse(current, PathMetadataRole.Root, rootObservation, provider)
            : provider.LegacyDirectoryExists(current, PathMetadataRole.Root) &&
              provider.IsReparsePoint(current, PathMetadataRole.Root))
        {
            throw new InvalidDataException($"{authorityName} root cannot be a reparse point.");
        }

        var relative = Path.GetRelativePath(authorityRoot, fullPath);
        if (relative == ".")
            return;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            bool reparse;
            if (TryObserve(current, PathMetadataRole.Segment, provider, out var observation))
            {
                if (!observation.Exists)
                    break;
                reparse = IsObservedReparse(current, PathMetadataRole.Segment, observation, provider);
            }
            else
            {
                if (!provider.LegacyDirectoryExists(current, PathMetadataRole.Segment) &&
                    !provider.LegacyFileExists(current, PathMetadataRole.Segment))
                    break;
                reparse = provider.IsReparsePoint(current, PathMetadataRole.Segment);
            }
            if (reparse)
                throw new InvalidDataException($"{authorityName} path traverses reparse point '{current}'.");
        }
    }

    /// <summary>
    /// Observes one component in directory-first order, catching only expected new-observation failures.
    /// </summary>
    /// <param name="path">
    /// The original component spelling.
    /// </param>
    /// <param name="role">
    /// The root role excludes a file observation; a segment permits one after directory absence.
    /// </param>
    /// <param name="provider">
    /// The observation dependency for this invocation.
    /// </param>
    /// <param name="observation">
    /// The component's presence and optional attributes, or the default value when fallback is required.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when observation completed, including absence; <see langword="false"/> when the original decision must run.
    /// </returns>
    private static bool TryObserve(string path, PathMetadataRole role, IWindowsPathMetadataProvider provider,
        out PathMetadataObservation observation)
    {
        try
        {
            observation = provider.ObserveDirectory(path, role);
            if (observation.Exists)
            {
                if (observation.CachedAttributes is { } attributes && !attributes.HasFlag(FileAttributes.Directory))
                    observation = observation with { CachedAttributes = null };
                return true;
            }
            if (role == PathMetadataRole.Root)
                return true;
            observation = provider.ObserveFile(path, role);
            if (observation.Exists && observation.CachedAttributes is { } fileAttributes &&
                fileAttributes.HasFlag(FileAttributes.Directory))
                observation = observation with { CachedAttributes = null };
            return true;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException)
        {
            observation = default;
            return false;
        }
    }

    /// <summary>
    /// Omits only the redundant final query for positive ordinary attributes of the observed kind.
    /// </summary>
    /// <param name="path">
    /// The original component spelling supplied to any required original query.
    /// </param>
    /// <param name="role">
    /// The current root or descendant role.
    /// </param>
    /// <param name="observation">
    /// This component's DATA; <see langword="false"/> does not inspect cached attributes.
    /// </param>
    /// <param name="provider">
    /// The original reparse-query dependency whose errors must propagate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when the original query reports a reparse point; otherwise <see langword="false"/>.
    /// </returns>
    private static bool IsObservedReparse(string path, PathMetadataRole role,
        PathMetadataObservation observation, IWindowsPathMetadataProvider provider) =>
        observation.Exists &&
        (observation.CachedAttributes is not { } attributes || attributes.HasFlag(FileAttributes.ReparsePoint)) &&
        provider.IsReparsePoint(path, role);
}

/// <summary>
/// Observes fresh component-local Windows metadata and retains the original APIs for fallback decisions.
/// Passive diagnostics report managed attempts without retaining physical observations between calls.
/// </summary>
internal sealed class WindowsPathMetadataProvider : IWindowsPathMetadataProvider
{
    private readonly Action<PathMetadataQuery>? _observer;

    /// <summary>
    /// Binds passive diagnostics to one guard invocation without sharing physical observations.
    /// </summary>
    /// <param name="observer">
    /// An optional callback before each managed API attempt; exceptions are ignored.
    /// </param>
    internal WindowsPathMetadataProvider(Action<PathMetadataQuery>? observer) => _observer = observer;

    /// <inheritdoc />
    public PathMetadataObservation ObserveDirectory(string path, PathMetadataRole role)
    {
        var directory = new DirectoryInfo(path);
        ObserveAttempt(path, role, PathMetadataQueryKind.DirectoryRefresh);
        directory.Refresh();
        var exists = directory.Exists;
        return new(exists, exists ? directory.Attributes : null);
    }

    /// <inheritdoc />
    public PathMetadataObservation ObserveFile(string path, PathMetadataRole role)
    {
        if (Path.EndsInDirectorySeparator(path))
            return new(LegacyFileExists(path, role), null);

        var file = new FileInfo(path);
        ObserveAttempt(path, role, PathMetadataQueryKind.FileRefresh);
        file.Refresh();
        var exists = file.Exists;
        return new(exists, exists ? file.Attributes : null);
    }

    /// <inheritdoc />
    public bool LegacyDirectoryExists(string path, PathMetadataRole role)
    {
        ObserveAttempt(path, role, PathMetadataQueryKind.DirectoryExists);
        return Directory.Exists(path);
    }

    /// <inheritdoc />
    public bool LegacyFileExists(string path, PathMetadataRole role)
    {
        ObserveAttempt(path, role, PathMetadataQueryKind.FileExists);
        return File.Exists(path);
    }

    /// <inheritdoc />
    public bool IsReparsePoint(string path, PathMetadataRole role)
    {
        ObserveAttempt(path, role, PathMetadataQueryKind.IsReparsePoint);
        return FileSystemManager.IsReparsePoint(path);
    }

    /// <summary>
    /// Reports a managed API attempt while isolating all diagnostic callback failures.
    /// </summary>
    /// <param name="path">
    /// The original path supplied to the impending API call.
    /// </param>
    /// <param name="role">
    /// The root or segment role of that attempt.
    /// </param>
    /// <param name="kind">
    /// The actual managed API about to execute, rather than its possible native requests.
    /// </param>
    private void ObserveAttempt(string path, PathMetadataRole role, PathMetadataQueryKind kind)
    {
        try
        {
            _observer?.Invoke(new(path, role, kind));
        }
        catch
        {
            // Diagnostics cannot change the filesystem decision or its failure.
        }
    }
}
