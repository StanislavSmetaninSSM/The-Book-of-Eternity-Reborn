using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Copies observed applied DATA into independent component roots without issuing game authority.
/// </summary>
internal static class JointAppliedDataTemplate
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
}
