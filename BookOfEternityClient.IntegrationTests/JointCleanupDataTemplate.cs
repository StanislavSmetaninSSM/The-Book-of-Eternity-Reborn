using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Imports exact cleanup-entry DATA without restoring a historical owner or issuing a proof.
/// </summary>
internal static class JointCleanupDataTemplate
{
    /// <summary>
    /// Verifies complete cleanup-entry provenance and ordinary payloads before creating a new copy.
    /// </summary>
    /// <param name="archivePath">
    /// Immutable observed ZIP outside the new root.
    /// </param>
    /// <param name="expectedSha256">
    /// Exact expected archive SHA256; mismatches are refused without rebinding.
    /// </param>
    /// <param name="targetRoot">
    /// Nonexistent independent test root beneath an ordinary owned parent.
    /// </param>
    /// <returns>
    /// Detached verified transport manifest; live original and proofs still require real validators.
    /// </returns>
    internal static JsonObject Materialize(string archivePath, string expectedSha256, string targetRoot) =>
        JointObservedDataTemplate.Materialize(archivePath, expectedSha256, targetRoot, JointObservedDataKind.CleanupEntry);
}
