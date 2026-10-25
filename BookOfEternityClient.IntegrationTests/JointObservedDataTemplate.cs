using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Copies explicitly selected observed DATA into independent roots without issuing game authority.
/// </summary>
internal static class JointObservedDataTemplate
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
    /// <param name="kind">
    /// The explicitly requested provenance kind; no live game authority is imported.
    /// </param>
    /// <param name="expectedLinuxGameAssemblySha256">
    /// Current game pin required only by the explicit Linux applied provenance kind.
    /// </param>
    /// <returns>
    /// The verified detached manifest. Current game authority must be obtained from real production validators.
    /// </returns>
    internal static JsonObject Materialize(string archivePath, string expectedSha256, string targetRoot, JointObservedDataKind kind,
        string? expectedLinuxGameAssemblySha256 = null)
    {
        targetRoot = Path.GetFullPath(targetRoot);
        if (File.Exists(targetRoot) || Directory.Exists(targetRoot)) throw new IOException("Component root already exists.");
        var parentScope = new TrustedLocalFileScope([Path.GetDirectoryName(targetRoot)!]);
        parentScope.ValidateDirectory(targetRoot, allowMissing: true);
        var archiveScope = new TrustedLocalFileScope([Path.GetDirectoryName(Path.GetFullPath(archivePath))!]);
        archiveScope.ValidateFile(archivePath, allowMissing: false);
        Require(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archivePath))) == expectedSha256);
        using var archive = ZipFile.OpenRead(archivePath);
        Require(archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.Ordinal).Count() == archive.Entries.Count);
        var metadata = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Missing observed DATA manifest.");
        JsonObject manifest;
        using (var reader = new StreamReader(metadata.Open())) manifest = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        Require(manifest["Kind"]!.GetValue<string>() == "joint_cleanup_observed_data_v1" &&
            !manifest["GrantsAuthority"]!.GetValue<bool>() && HasExpectedProvenance(manifest, kind, expectedLinuxGameAssemblySha256));
        var directories = manifest["Directories"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        var directoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in directories)
        {
            Require(IsOrdinaryRelative(path) && directoryNames.Add(path) &&
                (path == "game_session" || path.StartsWith("game_session/", StringComparison.Ordinal) ||
                    path is ".boe_runtime" or ".boe_runtime/session-generation"));
            parentScope.ValidateDirectory(Path.Combine(targetRoot, path), allowMissing: true);
        }
        Require(directoryNames.Contains("game_session") && directoryNames.Contains(".boe_runtime/session-generation"));
        foreach (var path in directories.Where(path => path.Contains('/')))
            Require(directoryNames.Contains(path[..path.LastIndexOf('/')]));
        var images = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var files = manifest["Files"]!.AsArray();
        Require(files.Count + 1 == archive.Entries.Count);
        foreach (var file in files)
        {
            var path = file!["Path"]!.GetValue<string>();
            Require(IsOrdinaryRelative(path) && !directoryNames.Contains(path) &&
                (path.StartsWith("game_session/", StringComparison.Ordinal) || path == ".boe_runtime/session-generation/current.json"));
            Require(directoryNames.Contains(path[..path.LastIndexOf('/')]));
            parentScope.ValidateFile(Path.Combine(targetRoot, path), allowMissing: true);
            using var stream = (archive.GetEntry("payload/" + path) ?? throw new InvalidDataException("Missing DATA member.")).Open();
            using var bytes = new MemoryStream(); stream.CopyTo(bytes);
            var image = bytes.ToArray();
            Require(image.LongLength == file["Length"]!.GetValue<long>() &&
                Convert.ToHexString(SHA256.HashData(image)) == file["Sha256"]!.GetValue<string>() && images.TryAdd(path, image));
        }
        Require(images.ContainsKey(".boe_runtime/session-generation/current.json"));
        var absent = manifest["Absent"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        foreach (var path in absent)
        {
            Require(IsOrdinaryRelative(path) && path.StartsWith("game_session/", StringComparison.Ordinal) &&
                !images.ContainsKey(path) && !directoryNames.Contains(path));
            parentScope.ValidateNamespacePath(Path.Combine(targetRoot, path));
        }
        // No target write precedes verification of the complete archive and its declared namespace.
        parentScope.EnsureDirectory(targetRoot);
        var scope = new TrustedLocalFileScope([targetRoot]);
        foreach (var path in directories.OrderBy(path => path.Length).ThenBy(path => path, StringComparer.Ordinal))
            scope.EnsureDirectory(Path.Combine(targetRoot, path));
        foreach (var pair in images)
        {
            var path = scope.ValidateFile(Path.Combine(targetRoot, pair.Key), allowMissing: true);
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) output.Write(pair.Value);
            Require(File.ReadAllBytes(path).AsSpan().SequenceEqual(pair.Value));
        }
        foreach (var path in absent)
            Require(!File.Exists(Path.Combine(targetRoot, path)) && !Directory.Exists(Path.Combine(targetRoot, path)));
        return manifest;
    }

    /// <summary>
    /// Requires the selected capture provenance without treating parent metadata as live authority.
    /// </summary>
    /// <param name="manifest">
    /// Detached observed-DATA manifest parsed before any target write.
    /// </param>
    /// <param name="kind">
    /// The exact supported applied-role or ordinary cleanup-entry provenance contract.
    /// </param>
    /// <param name="expectedLinuxGameAssemblySha256">
    /// Exact expected game digest for the Linux-generated applied kind.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only when all selected provenance fields match; otherwise <see langword="false"/>.
    /// </returns>
    private static bool HasExpectedProvenance(JsonObject manifest, JointObservedDataKind kind, string? expectedLinuxGameAssemblySha256)
    {
        if (manifest["Provenance"] is not JsonObject provenance) return false;
        if (kind == JointObservedDataKind.AppliedBeforeStory)
            return provenance["Role"]?.GetValue<string>() == "AppliedBeforeStory";
        if (kind == JointObservedDataKind.LinuxAppliedBeforeStory)
        {
            var builds = manifest["Builds"]?.AsArray().OfType<JsonObject>()
                .Where(row => row["Name"]?.GetValue<string>() == "BookOfEternityClient").ToArray() ?? [];
            return provenance["Role"]?.GetValue<string>() == "AppliedBeforeStory" &&
                provenance["Platform"]?.GetValue<string>() == "Linux" &&
                provenance["Generator"]?.GetValue<string>() == JointAppliedDataTemplate.LinuxGenerator &&
                expectedLinuxGameAssemblySha256 is not null && builds.Length == 1 &&
                builds[0]["Sha256"]?.GetValue<string>() == expectedLinuxGameAssemblySha256;
        }
        if (kind != JointObservedDataKind.CleanupEntry || provenance.ContainsKey("Role") || provenance.ContainsKey("Original")) return false;
        return provenance["Operation"] is JsonValue operation && operation.TryGetValue<string>(out var name) &&
            name == "JointStory.CleanupEligibleTerminalAsync" &&
            provenance["Cut"] is JsonValue cutValue && cutValue.TryGetValue<int>(out var cut) && cut is >= 0 and <= 4 &&
            provenance["Parent"] is JsonObject parent &&
            parent["Cut"] is JsonValue parentCut && parentCut.TryGetValue<int>(out var ownedCut) && ownedCut == cut &&
            parent["ParentPid"] is JsonValue pidValue && pidValue.TryGetValue<int>(out var pid) && pid > 0 &&
            parent["Nonce"] is JsonValue nonce && nonce.TryGetValue<string>(out var nonceText) && Guid.TryParseExact(nonceText, "N", out _) &&
            parent["RunId"] is JsonValue run && run.TryGetValue<string>(out var runText) && Guid.TryParseExact(runText, "N", out _) &&
            parent["Diagnostic"] is JsonValue diagnostic && diagnostic.TryGetValue<bool>(out var observed) && !observed &&
            parent.TryGetPropertyValue("Fault", out var fault) && fault is null;
    }

    /// <summary>
    /// Rejects relative paths with traversal, aliases or platform-specific alternate spelling.
    /// </summary>
    /// <param name="path">
    /// Exact forward-slash member name from the archive manifest.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for an ordinary relative spelling; otherwise <see langword="false"/>.
    /// </returns>
    private static bool IsOrdinaryRelative(string path) => path == path.Trim() && !path.Contains('\\') &&
        PendingTurnSnapshotAuthority.IsSafeRelativePath(path);

    /// <summary>
    /// Refuses incomplete or inconsistent observed DATA before it can be used as a template.
    /// </summary>
    /// <param name="condition">
    /// The required transport condition; <see langword="false"/> throws <see cref="InvalidDataException"/>.
    /// </param>
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException("Observed DATA archive does not match its exact transport contract.");
    }
}

/// <summary>
/// Selects the explicit provenance contract of immutable observed DATA.
/// </summary>
internal enum JointObservedDataKind
{
    /// <summary>
    /// Requires the existing applied-before-story capture role.
    /// </summary>
    AppliedBeforeStory,
    /// <summary>
    /// Requires the current Linux generator and exact current game-build evidence pin.
    /// </summary>
    LinuxAppliedBeforeStory,
    /// <summary>
    /// Requires the genuine cleanup-entry operation and parent metadata.
    /// </summary>
    CleanupEntry
}
