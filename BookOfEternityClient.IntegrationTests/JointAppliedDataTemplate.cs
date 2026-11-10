using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Copies observed applied DATA into independent component roots without issuing game authority.
/// </summary>
internal static partial class JointAppliedDataTemplate
{
    /// <summary>
    /// Verifies a complete observed archive before copying ordinary DATA into a new root.
    /// </summary>
    /// <param name="archivePath">
    /// Existing immutable observed-DATA ZIP, outside <paramref name="targetRoot"/>.
    /// </param>
    /// <param name="expectedSha256">
    /// The exact uppercase SHA-256 from the detached source record; no rebinding is performed.
    /// </param>
    /// <param name="targetRoot">
    /// Nonexistent per-test root beneath an existing ordinary owned directory.
    /// </param>
    /// <returns>
    /// The verified detached manifest. Current game authority must be obtained from real production validators.
    /// </returns>
    internal static JsonObject Materialize(string archivePath, string expectedSha256, string targetRoot) =>
        JointObservedDataTemplate.Materialize(archivePath, expectedSha256, targetRoot, JointObservedDataKind.AppliedBeforeStory);

    /// <summary>
    /// Copies explicitly selected platform DATA after its complete provenance and transport checks.
    /// </summary>
    /// <param name="input">
    /// Exact immutable input coordinates; Linux sources also retain the current game assembly digest.
    /// </param>
    /// <param name="targetRoot">
    /// Nonexistent independent destination beneath an owned ordinary parent.
    /// </param>
    /// <returns>
    /// Detached manifest DATA; every consumer must obtain fresh production capabilities.
    /// </returns>
    internal static JsonObject Materialize(JointAppliedDataSource input, string targetRoot) =>
        JointObservedDataTemplate.Materialize(input.ArchivePath, input.ArchiveSha256, targetRoot,
            input.LinuxGameAssemblySha256 is null ? JointObservedDataKind.AppliedBeforeStory : JointObservedDataKind.LinuxAppliedBeforeStory,
            input.LinuxGameAssemblySha256);
}
