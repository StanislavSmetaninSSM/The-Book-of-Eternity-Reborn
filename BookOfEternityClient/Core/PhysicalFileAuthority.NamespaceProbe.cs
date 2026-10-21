namespace BookOfEternityClient.Core;

/// <summary>
/// Identifies the actual Windows open performed by a scoped namespace probe.
/// </summary>
internal enum PhysicalNamespaceOpenKind
{
    /// <summary>
    /// A non-following entry metadata open.
    /// </summary>
    NamespaceEntry,
    /// <summary>
    /// The original stable directory authority open.
    /// </summary>
    DirectoryAuthority,
    /// <summary>
    /// A combined non-following directory classification and authority open.
    /// </summary>
    CombinedDirectory
}

/// <summary>
/// Reports the arguments of one actual Windows namespace open attempt.
/// </summary>
/// <param name="Path">
/// The normalized absolute path passed to the native open.
/// </param>
/// <param name="DesiredAccess">
/// The native requested access mask.
/// </param>
/// <param name="ShareMode">
/// The native sharing mask.
/// </param>
/// <param name="Flags">
/// The native open flags.
/// </param>
/// <param name="Kind">
/// The responsibility of this open in the real traversal.
/// </param>
internal readonly record struct PhysicalNamespaceOpenAttempt(
    string Path, uint DesiredAccess, uint ShareMode, uint Flags, PhysicalNamespaceOpenKind Kind);

/// <summary>
/// Supplies per-invocation observations and controlled boundaries without replacing native results.
/// </summary>
internal sealed class PhysicalNamespaceProbeHooks
{
    /// <summary>
    /// Receives every actual Windows open attempt before the native call; observer failures are isolated.
    /// </summary>
    internal Action<PhysicalNamespaceOpenAttempt>? ObserveNativeOpen { get; init; }

    /// <summary>
    /// Runs inside the owned handle cleanup region before directory validation; failures propagate.
    /// </summary>
    internal Action<string>? BeforeDirectoryHandleValidation { get; init; }

    /// <summary>
    /// Runs before the final leaf probe while the validated direct parent remains retained.
    /// </summary>
    internal Action<string>? BeforeLeafProbe { get; init; }
}

internal static partial class PhysicalFileAuthority
{
    /// <summary>
    /// Classifies a storage-evidence entry through a fresh physical traversal.
    /// </summary>
    /// <param name="rootPath">
    /// Existing physical root; the target must be a strict descendant.
    /// </param>
    /// <param name="expectedPath">
    /// Absolute entry to classify without following its final component.
    /// </param>
    /// <param name="authorityName">
    /// Description included in existing authority refusals.
    /// </param>
    /// <param name="hooks">
    /// Optional per-invocation observations and controlled test boundaries; null preserves ordinary operation.
    /// </param>
    /// <returns>
    /// The observed final entry kind, or Missing after a missing validated ancestor.
    /// Invalid ancestors and authority failures retain their exceptions.
    /// </returns>
    internal static NamespaceEntryKind ProbeStorageEvidenceNamespaceEntryFromRoot(
        string rootPath,
        string expectedPath,
        string authorityName,
        PhysicalNamespaceProbeHooks? hooks = null) =>
        ProbeNamespaceEntryFromRootCore(rootPath, expectedPath, authorityName, hooks);

    /// <summary>
    /// Performs the original per-call root and ancestor traversal without retaining results across calls.
    /// </summary>
    /// <param name="rootPath">
    /// Existing physical root; the target must be a strict descendant.
    /// </param>
    /// <param name="expectedPath">
    /// Absolute entry to classify without following its final component.
    /// </param>
    /// <param name="authorityName">
    /// Description included in existing authority refusals.
    /// </param>
    /// <param name="hooks">
    /// Optional per-invocation observations and controlled test boundaries; null preserves ordinary operation.
    /// </param>
    /// <returns>
    /// The observed final entry kind, or Missing after a missing validated ancestor.
    /// Invalid ancestors and authority failures retain their exceptions.
    /// </returns>
    private static NamespaceEntryKind ProbeNamespaceEntryFromRootCore(
        string rootPath,
        string expectedPath,
        string authorityName,
        PhysicalNamespaceProbeHooks? hooks)
    {
        var root = Path.GetFullPath(rootPath).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var expected = Path.GetFullPath(expectedPath);
        if (!IsSameOrDescendant(expected, root) ||
            PathsEqual(expected, root))
        {
            throw new InvalidDataException(
                $"{authorityName} entry is outside its physical root.");
        }

        var segments = Path.GetRelativePath(root, expected).Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 ||
            segments.Any(segment => segment is "." or ".."))
        {
            throw new InvalidDataException(
                $"{authorityName} namespace path is invalid.");
        }

        StableDirectory? current = null;
        try
        {
            var rootParentPath = Path.GetDirectoryName(root);
            if (rootParentPath == null)
            {
                throw new InvalidDataException(
                    $"{authorityName} physical root has no parent directory.");
            }

            using (var rootParent = OpenStableDirectory(
                       rootParentPath,
                       authorityName + " root parent", hooks: hooks))
            {
                var rootKind = ProbeNamespaceEntry(
                    rootParent,
                    root,
                    authorityName + " root", hooks);
                if (rootKind == NamespaceEntryKind.Missing)
                    return NamespaceEntryKind.Missing;
                if (rootKind != NamespaceEntryKind.Directory)
                {
                    throw new InvalidDataException(
                        $"{authorityName} root is not a physical directory.");
                }

                try
                {
                    current = OpenStableDirectory(root, authorityName, hooks: hooks);
                }
                catch (DirectoryNotFoundException)
                {
                    rootKind = ProbeNamespaceEntry(
                        rootParent,
                        root,
                        authorityName + " root", hooks);
                    if (rootKind == NamespaceEntryKind.Missing)
                        return NamespaceEntryKind.Missing;
                    throw;
                }
            }

            for (var index = 0; index < segments.Length - 1; index++)
            {
                var childPath = Path.Combine(current.FullPath, segments[index]);
                var childKind = ProbeNamespaceEntry(
                    current,
                    childPath,
                    authorityName + " parent", hooks: hooks);
                if (childKind == NamespaceEntryKind.Missing)
                    return NamespaceEntryKind.Missing;
                if (childKind != NamespaceEntryKind.Directory)
                {
                    throw new InvalidDataException(
                        $"{authorityName} parent is not a physical directory.");
                }

                var child = OpenStableDirectory(
                    childPath,
                    authorityName + " parent", hooks: hooks);
                current.Dispose();
                current = child;
            }

            hooks?.BeforeLeafProbe?.Invoke(expected);
            return ProbeNamespaceEntry(
                current,
                expected,
                authorityName, hooks);
        }
        finally
        {
            current?.Dispose();
        }
    }

    /// <summary>
    /// Reports the actual native arguments without allowing diagnostic failure to replace authority behavior.
    /// </summary>
    /// <param name="hooks">
    /// Optional observation receiver scoped to this invocation.
    /// </param>
    /// <param name="path">
    /// Normalized absolute native target.
    /// </param>
    /// <param name="access">
    /// Native requested access mask.
    /// </param>
    /// <param name="share">
    /// Native sharing mask.
    /// </param>
    /// <param name="flags">
    /// Native open flags.
    /// </param>
    /// <param name="kind">
    /// Responsibility of the actual open.
    /// </param>
    private static void ObserveNamespaceNativeOpen(
        PhysicalNamespaceProbeHooks? hooks, string path, uint access, uint share, uint flags,
        PhysicalNamespaceOpenKind kind)
    {
        try { hooks?.ObserveNativeOpen?.Invoke(new(path, access, share, flags, kind)); }
        catch { /* Passive diagnostics never replace the physical operation. */ }
    }
}
