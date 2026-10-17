using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// Test-only historical input transport. It never grants a production profile writer.
internal static class DarenRetirementEvidenceFixtures
{
    internal const string OriginalProductSource = "46ff637a01390a27c45a7fc36dc30e31d688e499";
    internal const string Profile = "client_profile/qte_showcase_rewards.json";
    internal const string Member = "game_state/meta/retirement-ordinary-member.bin";
    internal static readonly byte[] Before = [0xEF, 0xBB, 0xBF, 0, 0xFF, 41];
    internal static readonly byte[] After = [0xD0, 0x94, 0, 0xFF, 42];
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    internal sealed record Packet(string Kind, string ProductSource, string Producer, string OriginalRoot,
        string GenerationPath, string Generation, string EvidencePath,
        Dictionary<string, string> FilesBase64, Dictionary<string, string> Sha256);

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    internal static void Put(string root, string relative, byte[] bytes)
    {
        Assert.False(Path.IsPathRooted(relative));
        var path = Path.GetFullPath(Path.Combine(root, relative));
        Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, path, StringComparison.Ordinal);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    internal static Packet Capture(string kind, string producer, FileSystemManager files, string evidencePath)
    {
        var root = files.BasePath;
        var relativeEvidence = Path.GetRelativePath(root, evidencePath).Replace('\\', '/');
        var selected = new List<string> { Path.Combine(root, Profile), files.ResolvePath(Member), files.SessionGenerationPath };
        selected.AddRange(Directory.GetFiles(Path.GetDirectoryName(evidencePath)!, "*", SearchOption.AllDirectories));
        var images = selected.Distinct(StringComparer.Ordinal).ToDictionary(
            path => Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);
        Assert.All(images.Keys, path => Assert.DoesNotContain("..", path, StringComparison.Ordinal));
        var generation = JsonNode.Parse(images[Path.GetRelativePath(root, files.SessionGenerationPath).Replace('\\', '/')])!
            ["generationId"]!.GetValue<string>();
        return new(kind, OriginalProductSource, producer, root,
            Path.GetRelativePath(root, files.SessionGenerationPath).Replace('\\', '/'), generation,
            relativeEvidence, images.ToDictionary(p => p.Key, p => Convert.ToBase64String(p.Value)),
            images.ToDictionary(p => p.Key, p => Hash(p.Value)));
    }

    internal static void Freeze(Packet packet, ITestOutputHelper output)
    {
        var directory = Path.Combine(Path.GetTempPath(), "boe-daren-retirement-artifacts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, packet.Kind + ".json");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(packet, Json);
        File.WriteAllBytes(path, bytes);
        output.WriteLine(JsonSerializer.Serialize(new { RetirementCapture = path, Sha256 = Hash(bytes),
            packet.Kind, packet.Producer, packet.OriginalRoot, packet.Generation, Files = packet.FilesBase64.Count,
            Provenance = "AUTHENTIC_ORIGINAL_PRODUCER_BYTES; original owned root is strictly removed after this capture" }));
    }

    // Frozen captures are added only after separately reviewed/released native captures.
    // Missing data is a fixture error, never a synthetic fallback or a passing skip.
    internal static Packet Load(string kind)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.IntegrationTests", "TestData", "DarenRetirement", kind + ".json");
        Assert.True(File.Exists(path), "Reviewed original capture is required before consumer execution: " + path);
        var packet = JsonSerializer.Deserialize<Packet>(File.ReadAllBytes(path), Json)!;
        Assert.Equal(kind, packet.Kind);
        Assert.Equal(OriginalProductSource, packet.ProductSource);
        Assert.True(Guid.TryParseExact(packet.Generation, "N", out _));
        Assert.Equal(packet.FilesBase64.Keys.Order(), packet.Sha256.Keys.Order());
        foreach (var (relative, encoded) in packet.FilesBase64)
            Assert.Equal(packet.Sha256[relative], Hash(Convert.FromBase64String(encoded)));
        return packet;
    }

    internal static async Task<FileSystemManager> InstantiateAsync(Packet packet, string root, ITestOutputHelper output)
    {
        Assert.NotEqual(packet.OriginalRoot, root);
        var bootstrap = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(bootstrap, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        var targetGeneration = JsonNode.Parse(File.ReadAllBytes(bootstrap.SessionGenerationPath))!["generationId"]!.GetValue<string>();
        var transformations = new List<object>();
        foreach (var (relative, encoded) in packet.FilesBase64)
        {
            var before = Convert.FromBase64String(encoded);
            var after = before;
            var replacements = 0;
            var generationReplacements = 0;
            // Only the captured authority metadata carries absolute roots/generations.
            // Profile and backup bodies are arbitrary bytes even when named *.json.
            var metadata = relative == packet.EvidencePath || relative == packet.GenerationPath;
            Assert.False(metadata && relative == Profile);
            if (metadata)
            {
                var text = new UTF8Encoding(false, true).GetString(before);
                replacements = text.Split(packet.OriginalRoot, StringSplitOptions.None).Length - 1;
                generationReplacements = text.Split(packet.Generation, StringSplitOptions.None).Length - 1;
                after = Encoding.UTF8.GetBytes(text.Replace(packet.OriginalRoot, root, StringComparison.Ordinal)
                    .Replace(packet.Generation, targetGeneration, StringComparison.Ordinal));
            }
            Put(root, relative, after);
            if (!metadata) Assert.Equal(before, after);
            transformations.Add(new { Relative = relative, MetadataTransform = metadata,
                Before = Hash(before), After = Hash(after), RootReplacements = replacements, GenerationReplacements = generationReplacements });
        }
        output.WriteLine(JsonSerializer.Serialize(new { Provenance = "SOURCE_DERIVED_INSTANTIATION_NOT_AUTHENTIC_SAME_ROOT",
            packet.Kind, OriginalRoot = packet.OriginalRoot, InstantiatedRoot = root,
            OriginalGeneration = packet.Generation, InstantiatedGeneration = targetGeneration,
            GenerationTransform = "EXPLICIT original template generation -> actual fresh-root bootstrap generation only in EvidencePath and GenerationPath metadata; profile/backups stay raw byte-exact; original packet bytes/hashes remain unchanged",
            Transformations = transformations }));
        return new(root, NullLogger<FileSystemManager>.Instance);
    }

    internal static Dictionary<string, byte[]> Snapshot(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Where(path => !path.EndsWith(".lock", StringComparison.Ordinal))
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);

    internal static void AssertSnapshot(string root, Dictionary<string, byte[]> expected)
    {
        var actual = Snapshot(root);
        Assert.Equal(expected.Keys.Order(), actual.Keys.Order());
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }
}
