using System.Runtime.InteropServices;

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
    /// Root path resolved to an absolute physical anchor; it may be missing, and the target must be a strict descendant.
    /// </param>
    /// <param name="expectedPath">
    /// Target path resolved to an absolute entry whose final component is not followed.
    /// </param>
    /// <param name="authorityName">
    /// Description included in existing authority refusals.
    /// </param>
    /// <param name="hooks">
    /// Optional per-invocation observations and controlled test boundaries; <see langword="null"/> preserves ordinary operation.
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
        ProbeNamespaceEntryFromRootCore(rootPath, expectedPath, authorityName, hooks, combineDirectoryOpens: true);

    /// <summary>
    /// Performs the original per-call root and ancestor traversal without retaining results across calls.
    /// </summary>
    /// <param name="rootPath">
    /// Root path resolved to an absolute physical anchor; it may be missing, and the target must be a strict descendant.
    /// </param>
    /// <param name="expectedPath">
    /// Target path resolved to an absolute entry whose final component is not followed.
    /// </param>
    /// <param name="authorityName">
    /// Description included in existing authority refusals.
    /// </param>
    /// <param name="hooks">
    /// Optional per-invocation observations and controlled test boundaries; <see langword="null"/> preserves ordinary operation.
    /// </param>
    /// <param name="combineDirectoryOpens">
    /// True enables the Windows storage-specific successful-directory combination; false retains original traversal.
    /// Non-Windows traversal always uses the original sequence.
    /// </param>
    /// <returns>
    /// The observed final entry kind, or Missing after a missing validated ancestor.
    /// Invalid ancestors and authority failures retain their exceptions.
    /// </returns>
    private static NamespaceEntryKind ProbeNamespaceEntryFromRootCore(
        string rootPath,
        string expectedPath,
        string authorityName,
        PhysicalNamespaceProbeHooks? hooks,
        bool combineDirectoryOpens)
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
                if (combineDirectoryOpens && OperatingSystem.IsWindows())
                {
                    current = OpenStorageProbeDirectory(rootParent, root, authorityName, rootEntry: true, hooks);
                    if (current == null) return NamespaceEntryKind.Missing;
                }
                else
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
            }

            for (var index = 0; index < segments.Length - 1; index++)
            {
                var childPath = Path.Combine(current.FullPath, segments[index]);
                StableDirectory? child;
                if (combineDirectoryOpens && OperatingSystem.IsWindows())
                {
                    child = OpenStorageProbeDirectory(current, childPath, authorityName, rootEntry: false, hooks);
                    if (child == null) return NamespaceEntryKind.Missing;
                }
                else
                {
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

                    child = OpenStableDirectory(
                        childPath,
                        authorityName + " parent", hooks: hooks);
                }
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
    /// Opens and validates a successful Windows traversal directory on one fresh non-following handle.
    /// </summary>
    /// <param name="parent">
    /// Retained direct parent, kept alive until the child has passed validation.
    /// </param>
    /// <param name="expectedPath">
    /// Absolute child directory path to classify and retain.
    /// </param>
    /// <param name="authorityName">
    /// Original traversal description used for matching refusal messages.
    /// </param>
    /// <param name="rootEntry">
    /// True for the root component; false for an intermediate parent component.
    /// </param>
    /// <param name="hooks">
    /// Per-invocation observations and controlled validation boundaries, or <see langword="null"/>.
    /// </param>
    /// <returns>
    /// Owned stable directory, or <see langword="null"/> after a native missing-entry result.
    /// Nonmissing open errors use the original classification and opening sequence.
    /// </returns>
    private static StableDirectory? OpenStorageProbeDirectory(
        StableDirectory parent, string expectedPath, string authorityName, bool rootEntry,
        PhysicalNamespaceProbeHooks? hooks)
    {
        var entryName = authorityName + (rootEntry ? " root" : " parent");
        EnsureDirectChild(parent, expectedPath, entryName);
        var normalizedPath = Path.GetFullPath(expectedPath);
        var access = FileReadAttributes | FileListDirectory | SynchronizeAccess;
        var share = FileShareRead | FileShareWrite;
        var flags = FileFlagOpenReparsePoint | FileFlagBackupSemantics;
        ObserveNamespaceNativeOpen(hooks, normalizedPath, access, share, flags,
            PhysicalNamespaceOpenKind.CombinedDirectory);
        var handle = CreateFile(ToWindowsExtendedPath(normalizedPath), access, share, IntPtr.Zero,
            OpenExisting, flags, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is ErrorFileNotFound or ErrorPathNotFound) return null;
            // Listing access is also file-data access. Preserve the original
            // metadata-first refusal when combined access fails on a bad parent.
            return OpenStorageProbeDirectoryWithOriginalSequence(parent, normalizedPath, authorityName, rootEntry, hooks);
        }

        try
        {
            hooks?.BeforeDirectoryHandleValidation?.Invoke(normalizedPath);
            EnsureHandleMatchesExpectedPath(handle, normalizedPath, entryName, FileNameOpened);
            var attributeTag = GetAttributeTag(handle, entryName);
            if ((attributeTag.FileAttributes & FileAttributes.ReparsePoint) != 0 ||
                (attributeTag.FileAttributes & FileAttributes.Directory) == 0)
                throw new InvalidDataException(entryName + " is not a physical directory.");
            // Opened-name and normalized-name checks have distinct contracts;
            // both remain on this same newly opened authority.
            EnsureHandleMatchesExpectedPath(handle, normalizedPath, rootEntry ? authorityName : entryName);
            return new StableDirectory(normalizedPath, handle);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Retains the original metadata-first directory sequence after a combined native open fails.
    /// </summary>
    /// <param name="parent">
    /// Retained direct parent, kept alive until the child has passed validation.
    /// </param>
    /// <param name="expectedPath">
    /// Absolute child directory path to classify and retain.
    /// </param>
    /// <param name="authorityName">
    /// Original traversal description used for matching refusal messages.
    /// </param>
    /// <param name="rootEntry">
    /// True for the root component; false for an intermediate parent component.
    /// </param>
    /// <param name="hooks">
    /// Per-invocation observations and controlled validation boundaries, or <see langword="null"/>.
    /// </param>
    /// <returns>
    /// Owned original directory authority, or <see langword="null"/> after the original missing-entry observation.
    /// </returns>
    private static StableDirectory? OpenStorageProbeDirectoryWithOriginalSequence(
        StableDirectory parent, string expectedPath, string authorityName, bool rootEntry,
        PhysicalNamespaceProbeHooks? hooks)
    {
        var entryName = authorityName + (rootEntry ? " root" : " parent");
        var kind = ProbeNamespaceEntry(parent, expectedPath, entryName, hooks);
        if (kind == NamespaceEntryKind.Missing) return null;
        if (kind != NamespaceEntryKind.Directory)
            throw new InvalidDataException(entryName + " is not a physical directory.");
        try
        {
            return OpenStableDirectory(expectedPath, rootEntry ? authorityName : entryName, hooks: hooks);
        }
        catch (DirectoryNotFoundException) when (rootEntry)
        {
            kind = ProbeNamespaceEntry(parent, expectedPath, entryName, hooks);
            if (kind == NamespaceEntryKind.Missing) return null;
            throw;
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
