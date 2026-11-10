using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Supplies immutable observed DATA and exact game images without issuing live proofs.
/// </summary>
internal static class JointCleanupValidatorTestData
{
    /// <summary>
    /// Binds the exact retained archive bytes without rebinding capture provenance.
    /// </summary>
    internal const string ArchiveSha256 = "A99035C7FFBB67E15D3F08240A035AE0D8DC255F2708AC9AFE587DE279870311";

    /// <summary>
    /// Locates the versioned immutable r60 cleanup-entry DATA fixture with unchanged capture provenance.
    /// </summary>
    internal static string ArchivePath => Path.Combine(TestRepoPaths.RepoRoot, "specs",
        "1536-complete-wound-materialization", "fixtures", "joint-cleanup-entry-r60-v1", "cleanup-entry.zip");

    /// <summary>
    /// Parses ordinary manifest DATA using the production serializer options and participating reader.
    /// </summary>
    /// <param name="files">
    /// The current file manager owning <paramref name="lease"/>.
    /// </param>
    /// <param name="lease">
    /// The active participating lease, checked by the actual manifest read.
    /// </param>
    /// <returns>
    /// The parsed DTO; this is neither a validated context nor deletion permission.
    /// </returns>
    internal static async Task<GameEngine.PendingTurnSnapshotManifest> ReadManifestAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease) =>
        JsonSerializer.Deserialize<GameEngine.PendingTurnSnapshotManifest>(
            await files.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)
                ?? throw new InvalidDataException("Current original manifest is absent."),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
        ?? throw new InvalidDataException("Current original manifest is null.");

    /// <summary>
    /// Hashes every game file, preserving exact relative names and byte lengths.
    /// </summary>
    /// <param name="root">
    /// Independent owned root containing the complete game-session subtree.
    /// </param>
    /// <returns>
    /// An ordinal map from exact names to length and SHA-256 observations.
    /// </returns>
    internal static Dictionary<string, string> CaptureImages(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "game_session"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(root, path), path =>
            {
                using var stream = File.OpenRead(path);
                return stream.Length + ":" + Convert.ToHexString(SHA256.HashData(stream));
            }, StringComparer.Ordinal);

    /// <summary>
    /// Captures all game-directory names, including empty directories.
    /// </summary>
    /// <param name="root">
    /// Independent owned root containing the game session.
    /// </param>
    /// <returns>
    /// Ordinally sorted relative directory names, including the game-session root.
    /// </returns>
    internal static string[] CaptureDirectories(string root) =>
        Directory.EnumerateDirectories(Path.Combine(root, "game_session"), "*", SearchOption.AllDirectories)
            .Append(Path.Combine(root, "game_session")).Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();
}
