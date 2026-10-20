using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Selects one exact immutable source42 artifact without transporting game authority or live owners.
/// </summary>
/// <param name="ArtifactPath">
/// The absolute ordinary artifact directory selected by the parent.
/// </param>
/// <param name="ManifestSha256">
/// The uppercase SHA256 digest of the exact selected manifest bytes.
/// </param>
internal sealed record JointInterfaceSeedCrashInput(string ArtifactPath, string ManifestSha256)
{
    /// <summary>
    /// Validates an ordinary Windows cut0 artifact and binds its exact manifest to the current source contracts.
    /// </summary>
    /// <param name="artifactPath">
    /// The absolute existing artifact directory; missing or invalid artifacts are refused without generation.
    /// </param>
    /// <param name="repoRoot">
    /// The repository whose current broad source fingerprint must match the artifact.
    /// </param>
    /// <param name="windows">
    /// Whether the requested crash consumer runs on Windows.
    /// </param>
    /// <param name="mode">
    /// The ordinary cut0 mode, with diagnostics disabled and no integrity fault.
    /// </param>
    /// <returns>
    /// A detached selection containing the validated artifact path and exact manifest digest.
    /// </returns>
    internal static JointInterfaceSeedCrashInput Bind(string artifactPath, string repoRoot, bool windows,
        JointInterfaceCrashMode mode) => throw new NotSupportedException();

    /// <summary>
    /// Reads an optional closed seed selection and refuses malformed or forbidden explicit opt-ins.
    /// </summary>
    /// <param name="parent">
    /// The parent metadata object whose optional Source42Seed property contains the exact selection.
    /// </param>
    /// <param name="windows">
    /// Whether the requested crash consumer runs on Windows.
    /// </param>
    /// <param name="mode">
    /// The crash mode, which must be ordinary Windows cut0 when a selection is present.
    /// </param>
    /// <returns>
    /// The validated detached selection, or <see langword="null"/> only when Source42Seed is absent.
    /// </returns>
    internal static JointInterfaceSeedCrashInput? Read(JsonObject parent, bool windows, JointInterfaceCrashMode mode) =>
        throw new NotSupportedException();

    /// <summary>
    /// Opens the selected artifact only if its exact manifest and current broad source contracts still match.
    /// </summary>
    /// <param name="repoRoot">
    /// The repository whose producing and consuming source closure must match the selected artifact.
    /// </param>
    /// <returns>
    /// A checked immutable codec snapshot ready for independent materialization into a new owned root.
    /// </returns>
    internal JointInterfaceCrashSeed Open(string repoRoot) => throw new NotSupportedException();
}
