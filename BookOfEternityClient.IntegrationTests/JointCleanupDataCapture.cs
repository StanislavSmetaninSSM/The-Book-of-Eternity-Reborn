using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exports observed terminal diagnostic data without importing or issuing game authority.
/// </summary>
internal static class JointCleanupDataCapture
{
    /// <summary>
    /// Writes exact game-session and generation data to a new archive outside the source root.
    /// </summary>
    /// <param name="root">
    /// The owned test root containing game_session and inert generation data.
    /// </param>
    /// <param name="archivePath">
    /// A nonexistent archive file beneath an existing directory outside <paramref name="root"/>.
    /// </param>
    /// <param name="provenance">
    /// Detached generating-boundary metadata, serialized as data rather than validated authority.
    /// </param>
    internal static void Capture(string root, string archivePath, JsonObject provenance)
    {
        root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        archivePath = Path.GetFullPath(archivePath);
        var relativeOutput = Path.GetRelativePath(root, archivePath);
        if (!Path.IsPathFullyQualified(relativeOutput) && relativeOutput != ".." &&
            !relativeOutput.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Diagnostic output must be outside the observed root.");
        if (File.Exists(archivePath) || Directory.Exists(archivePath))
            throw new IOException("Diagnostic output already exists.");
        var scope = new TrustedLocalFileScope([root]);
        var session = scope.ValidateDirectory(Path.Combine(root, "game_session"), allowMissing: false);
        var directories = new List<string> { "game_session", ".boe_runtime", ".boe_runtime/session-generation" };
        var paths = new List<string> { ".boe_runtime/session-generation/current.json" };
        var pending = new Queue<string>(); pending.Enqueue(session);
        while (pending.TryDequeue(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory).OrderBy(value => value, StringComparer.Ordinal))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("An observed data member must be ordinary: " + path);
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    directories.Add(relative);
                    pending.Enqueue(scope.ValidateDirectory(path, allowMissing: false));
                }
                else paths.Add(relative);
            }
        }
        var images = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in paths)
            images.Add(path, File.ReadAllBytes(scope.ValidateFile(Path.Combine(root, path), allowMissing: false)));
        var probes = new[] { "ready/turn_complete.json", "ready/turn_error.json", "input/turn_request.json",
            "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json" };
        var absent = probes.Select(path => "game_session/" + path).Where(path => !images.ContainsKey(path)).ToArray();
        var builds = new[] { typeof(GameEngine).Assembly, typeof(JointCleanupDataCapture).Assembly,
            typeof(PendingTurnSnapshotTestAuthority).Assembly }.Distinct().Select(assembly => new
        {
            Name = assembly.GetName().Name,
            Version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(assembly.Location)))
        }).ToArray();
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            Kind = "joint_cleanup_observed_data_v1", GrantsAuthority = false,
            Consistency = "Observed cleanup-entry data, not a closed/quiescent game capability; fresh production validation required for replay.",
            CapturedUtc = DateTime.UtcNow, PID = Environment.ProcessId, Provenance = provenance, Builds = builds,
            Directories = directories.OrderBy(value => value, StringComparer.Ordinal).ToArray(), Absent = absent,
            Files = images.Select(pair => new { Path = pair.Key, Length = pair.Value.LongLength,
                Sha256 = Convert.ToHexString(SHA256.HashData(pair.Value)) }).ToArray(),
            Excluded = "Runtime coordination, locks, workers and publication state; only inert generation bytes included."
        }, new JsonSerializerOptions { WriteIndented = true });
        var outputScope = new TrustedLocalFileScope([Path.GetDirectoryName(archivePath)!]);
        outputScope.ValidateFile(archivePath, allowMissing: true);
        using var output = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (var pair in images)
        {
            using var entry = archive.CreateEntry("payload/" + pair.Key, CompressionLevel.Fastest).Open();
            entry.Write(pair.Value);
        }
        using var metadata = archive.CreateEntry("manifest.json", CompressionLevel.Fastest).Open();
        metadata.Write(manifest);
    }
}
