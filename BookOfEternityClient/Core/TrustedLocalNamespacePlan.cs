namespace BookOfEternityClient.Core;

/// <summary>
/// Identifies the admitted kind of one local namespace name.
/// </summary>
internal enum TrustedLocalNamespaceKind
{
    /// <summary>
    /// No entry is present at the name.
    /// </summary>
    Missing,
    /// <summary>
    /// An ordinary directory is present at the name.
    /// </summary>
    Directory,
    /// <summary>
    /// An ordinary exact-byte file is present at the name.
    /// </summary>
    File
}

/// <summary>
/// Describes a namespace state whose file bytes are owned only by a present file image.
/// </summary>
/// <param name="Kind">
/// The admitted namespace kind; missing and directory states require no file image.
/// </param>
/// <param name="FileImage">
/// A present exact-byte image for a file state, including an empty file; otherwise <see langword="null"/>.
/// </param>
internal sealed record TrustedLocalNamespaceImage(TrustedLocalNamespaceKind Kind, TrustedLocalFileImage? FileImage);

/// <summary>
/// Records the complete before and after states of one normalized absolute namespace member.
/// </summary>
/// <param name="Path">
/// The normalized absolute member path admitted by the complete namespace validator.
/// </param>
/// <param name="Before">
/// The exact namespace state required for rollback.
/// </param>
/// <param name="After">
/// The exact namespace state required after commit.
/// </param>
internal sealed record TrustedLocalNamespaceChange(string Path, TrustedLocalNamespaceImage Before, TrustedLocalNamespaceImage After);

/// <summary>
/// Records one immutable admitted library or selected-source boundary without granting mutation authority.
/// </summary>
/// <param name="Path">
/// The exact normalized absolute protected boundary path.
/// </param>
/// <param name="Kind">
/// Its required directory, missing or file kind.
/// </param>
/// <param name="Length">
/// The exact nonnegative file length, or zero for directory and missing boundaries.
/// </param>
/// <param name="Sha256">
/// The exact file hash, or <see langword="null"/> for directory and missing boundaries.
/// </param>
internal sealed record TrustedLocalNamespaceBoundary(string Path, TrustedLocalNamespaceKind Kind, long Length, string? Sha256);

/// <summary>
/// Supplies a complete local namespace member set and its immutable admitted boundaries.
/// </summary>
/// <param name="RootPath">
/// The exact normalized game-session directory, which remains a directory.
/// </param>
/// <param name="Changes">
/// The complete unique member inventory, including the manager-owned exact runtime generation member.
/// </param>
/// <param name="Boundaries">
/// The exact canonical library and any admitted in-session selected-source boundary.
/// </param>
internal sealed record TrustedLocalNamespacePlan(string RootPath, IReadOnlyList<TrustedLocalNamespaceChange> Changes,
    IReadOnlyList<TrustedLocalNamespaceBoundary> Boundaries);

/// <summary>
/// Supplies the before and after states of one canonical session-relative load member.
/// </summary>
/// <param name="RelativePath">
/// The canonical session-relative member name resolved and admitted by the manager.
/// </param>
/// <param name="Before">
/// The exact prior namespace state.
/// </param>
/// <param name="After">
/// The exact prepared replacement namespace state.
/// </param>
internal sealed record CanonicalLoadNamespaceChange(string RelativePath, TrustedLocalNamespaceImage Before, TrustedLocalNamespaceImage After);

/// <summary>
/// Captures complete session-relative namespace nodes and manager-admitted immutable boundaries.
/// </summary>
/// <param name="Nodes">
/// The complete unique session-relative node inventory with exact namespace images.
/// </param>
/// <param name="Boundaries">
/// The exact absolute canonical library and any admitted in-session selected-source boundary.
/// </param>
internal sealed record CanonicalLoadNamespaceSnapshot(IReadOnlyDictionary<string, TrustedLocalNamespaceImage> Nodes,
    IReadOnlyList<TrustedLocalNamespaceBoundary> Boundaries);

/// <summary>
/// Supplies a complete prepared load namespace without granting arbitrary runtime or external path mutation.
/// </summary>
/// <param name="Changes">
/// The complete unique session-relative change inventory; the manager appends exact generation authority.
/// </param>
/// <param name="Boundaries">
/// The exact absolute boundaries admitted by the manager's canonical snapshot.
/// </param>
internal sealed record CanonicalLoadNamespacePlan(IReadOnlyList<CanonicalLoadNamespaceChange> Changes,
    IReadOnlyList<TrustedLocalNamespaceBoundary> Boundaries);
