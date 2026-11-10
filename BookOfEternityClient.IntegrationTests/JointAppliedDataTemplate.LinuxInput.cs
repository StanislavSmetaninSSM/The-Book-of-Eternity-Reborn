using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Retains exact immutable DATA coordinates without transporting any live owner or capability.
/// </summary>
/// <param name="ArchivePath">
/// Absolute source archive path.
/// </param>
/// <param name="ArchiveSha256">
/// Exact uppercase archive digest, checked again before any destination write.
/// </param>
/// <param name="LinuxGameAssemblySha256">
/// Current game digest required by a Linux-generated manifest, or null for the existing Windows input.
/// </param>
internal sealed record JointAppliedDataSource(string ArchivePath, string ArchiveSha256, string? LinuxGameAssemblySha256);

internal static partial class JointAppliedDataTemplate
{
    /// <summary>
    /// Identifies only the genuine minimal current Linux publisher used for qualification preparation.
    /// </summary>
    internal const string LinuxGenerator = "linux-minimal-applied-v1";

    /// <summary>
    /// Resolves an explicit Linux input or the existing Windows observed input without cross-platform fallback.
    /// </summary>
    /// <returns>
    /// Immutable DATA coordinates whose manifest is still independently checked by materialization.
    /// </returns>
    internal static JointAppliedDataSource ResolveCurrent()
    {
        if (OperatingSystem.IsLinux())
            return ResolveLinux(Environment.GetEnvironmentVariable("BOE_TEST_LINUX_APPLIED_RECORD"), CurrentGameAssemblySha256());
        if (!OperatingSystem.IsWindows()) throw new InvalidDataException("Applied DATA preparation supports Linux and Windows.");
        var directory = Path.Combine(TestRepoPaths.RepoRoot, "specs", "1536-complete-wound-materialization", "fixtures", "joint-applied-data");
        var record = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "source-record.json")))!.AsObject();
        return new(Path.Combine(directory, record["Archive"]!.GetValue<string>()), record["ArchiveSHA256"]!.GetValue<string>(), null);
    }

    /// <summary>
    /// Freshly binds a Linux input record to the current game binary before selecting its archive.
    /// </summary>
    /// <param name="recordPath">
    /// Required absolute external record path; unset or relative configuration never selects Windows DATA.
    /// </param>
    /// <param name="expectedGameAssemblySha256">
    /// Exact current game digest; this evidence pin is not a game permission.
    /// </param>
    /// <returns>
    /// Exact external source coordinates, or a refusal before any destination exists.
    /// </returns>
    internal static JointAppliedDataSource ResolveLinux(string? recordPath, string expectedGameAssemblySha256)
    {
        if (string.IsNullOrWhiteSpace(recordPath) || !Path.IsPathFullyQualified(recordPath))
            throw new InvalidDataException("Linux applied DATA requires BOE_TEST_LINUX_APPLIED_RECORD with an absolute external record.");
        RequireExternalPath(recordPath);
        recordPath = new TrustedLocalFileScope([Path.GetDirectoryName(recordPath)!]).ValidateFile(recordPath, allowMissing: false);
        JsonObject record;
        try { record = JsonNode.Parse(File.ReadAllText(recordPath))?.AsObject() ?? throw new InvalidDataException("Missing Linux input record."); }
        catch (JsonException error) { throw new InvalidDataException("Malformed Linux input record.", error); }
        if (record.Count != 6 || record["SchemaVersion"]?.GetValue<int>() != 1 ||
            record["Platform"]?.GetValue<string>() != "Linux" || record["Generator"]?.GetValue<string>() != LinuxGenerator ||
            record["GameAssemblySHA256"]?.GetValue<string>() != expectedGameAssemblySha256 || !IsDigest(expectedGameAssemblySha256))
            throw new InvalidDataException("Linux applied DATA has foreign platform, generator or game build.");
        var archive = record["Archive"]?.GetValue<string>();
        var hash = record["ArchiveSHA256"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(archive) || !Path.IsPathFullyQualified(archive) || !IsDigest(hash))
            throw new InvalidDataException("Linux applied DATA has invalid archive coordinates.");
        RequireExternalPath(archive);
        return new(Path.GetFullPath(archive), hash!, expectedGameAssemblySha256);
    }

    /// <summary>
    /// Computes a fresh input provenance pin without adding a global path cache.
    /// </summary>
    /// <returns>
    /// Exact uppercase SHA-256 of the loaded game assembly file.
    /// </returns>
    internal static string CurrentGameAssemblySha256() =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(GameEngine).Assembly.Location)));

    /// <summary>
    /// Keeps generated records and archives outside the source checkout.
    /// </summary>
    /// <param name="path">
    /// Absolute record or archive coordinate to validate without writing it.
    /// </param>
    internal static void RequireExternalPath(string path)
    {
        var relative = Path.GetRelativePath(TestRepoPaths.RepoRoot, Path.GetFullPath(path));
        if (!Path.IsPathFullyQualified(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Generated applied DATA must remain outside checkout.");
    }

    /// <summary>
    /// Recognizes the exact digest spelling used by the existing capture transport.
    /// </summary>
    /// <param name="value">
    /// Candidate uppercase SHA-256 text.
    /// </param>
    /// <returns>
    /// Whether the value contains exactly 64 uppercase hexadecimal digits.
    /// </returns>
    private static bool IsDigest(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
}
