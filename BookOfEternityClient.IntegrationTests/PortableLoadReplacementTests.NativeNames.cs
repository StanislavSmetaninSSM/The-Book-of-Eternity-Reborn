using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableLoadReplacementTests
{
    /// <summary>
    /// Preserves simultaneous arbitrary case-distinct payload names on Linux and refuses their native Windows collision.
    /// </summary>
    /// <param name="rollback">
    /// Whether to interrupt actual member publication and require the exact original namespace and generation.
    /// </param>
    /// <returns>
    /// Completion after the native decision, both independent payloads and protected source/library bytes are verified.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeCaseDistinctPayloadsKeepExactCommittedOrRolledBackDecision(bool rollback)
    {
        var source = await PrepareCurrentArchiveAsync();
        byte[] upper = [1, 0, 255, 2];
        byte[] lower = [3, 0, 254, 4];
        AppendManifestedPayload(source, "lore/Pair.bin", upper);
        AppendManifestedPayload(source, "lore/pair.bin", lower);
        var protectedFiles = SnapshotLibraryAndSource(source);
        Put("lore/Pair.bin", [71, 72]);
        if (OperatingSystem.IsLinux()) Put("lore/pair.bin", [81, 82]);
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);
        var published = 0;
        if (rollback) _fault = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished ||
                ReadNamespaceMemberRelativePath(index) != "lore/pair.bin") return;
            Assert.Equal(upper, File.ReadAllBytes(_files.ResolvePath("lore/Pair.bin")));
            Assert.Equal(lower, File.ReadAllBytes(_files.ResolvePath("lore/pair.bin")));
            published++;
            throw new InvalidOperationException("native case-pair publication cut");
        };

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
            Assert.Contains("duplicate", Assert.IsType<InvalidDataException>(result.Failure).Message);
            Assert.Equal(0, _prepared);
            Assert.Empty(_phases);
        }
        else
        {
            Assert.True(OperatingSystem.IsLinux(), "This owner requires native Linux or Windows.");
            Assert.Equal(rollback ? LoadReplacementDisposition.RolledBack : LoadReplacementDisposition.Committed, result.Disposition);
            Assert.Equal(1, _prepared);
            Assert.Equal(rollback, result.NeedsFollowUp);
            if (rollback)
            {
                Assert.Equal(1, published);
                Assert.Contains("native case-pair publication cut", result.Failure!.ToString());
            }
            Assert.False(result.ContinuationBlocked);
            Assert.False(string.IsNullOrWhiteSpace(result.EstablishedGeneration));
            if (!rollback)
            {
                Assert.Equal(upper, File.ReadAllBytes(_files.ResolvePath("lore/Pair.bin")));
                Assert.Equal(lower, File.ReadAllBytes(_files.ResolvePath("lore/pair.bin")));
                Assert.NotEqual(generation, File.ReadAllBytes(_files.SessionGenerationPath));
                Assert.Single(_phases, value => value.Phase == TrustedLocalPublicationPhase.Committed);
            }
        }
        if (OperatingSystem.IsWindows() || rollback)
        {
            Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
            AssertPreserved(before);
            Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
            Assert.DoesNotContain(_phases, value => value.Phase == TrustedLocalPublicationPhase.Committed);
        }
        AssertPreserved(protectedFiles);
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Rejects duplicate originals and fixed-state aliases before publication, preserving the admitted source and live state.
    /// </summary>
    /// <param name="kind">
    /// An exact duplicate, colliding fixed alias, or deliberately mismatched original manifest digest.
    /// </param>
    /// <returns>
    /// Completion after prepublication refusal and exact live/source/library/generation preservation.
    /// </returns>
    [Theory]
    [InlineData("exact-duplicate")]
    [InlineData("fixed-alias")]
    [InlineData("case-pair-hash")]
    [InlineData("ambiguous-manifest-alias")]
    [InlineData("double-claimed-entry")]
    [InlineData("duplicate-manifest")]
    public async Task NativeOriginalInventoryRejectsDuplicateFixedAliasAndWrongHash(string kind)
    {
        var source = await PrepareCurrentArchiveAsync();
        if (kind == "duplicate-manifest")
        {
            using var archive = ZipFile.Open(source, ZipArchiveMode.Update);
            var original = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("save_manifest.json"));
            using var bytes = new MemoryStream();
            using (var input = original.Open()) input.CopyTo(bytes);
            using var output = archive.CreateEntry("SAVE_MANIFEST.JSON", CompressionLevel.NoCompression).Open();
            output.Write(bytes.ToArray());
        }
        else if (kind == "fixed-alias")
            AppendManifestedPayload(source, SoulPath.ToUpperInvariant(), _loadedSoul);
        else if (kind == "double-claimed-entry")
        {
            AppendManifestedPayload(source, "lore/alpha.bin", [1, 0, 255, 2]);
            AppendManifestedPayload(source, "lore/beta.bin", [1, 0, 255, 2]);
        }
        else
        {
            AppendManifestedPayload(source, "lore/Pair.bin", [1, 0, 255, 2]);
            AppendManifestedPayload(source, kind == "exact-duplicate" ? "lore/Pair.bin" : "lore/pair.bin",
                [3, 0, 254, 4], invalidHash: kind == "case-pair-hash");
        }
        if (kind is "ambiguous-manifest-alias" or "double-claimed-entry")
        {
            using var archive = ZipFile.Open(source, ZipArchiveMode.Update);
            var entry = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("save_manifest.json"));
            JsonObject manifest;
            using (var input = entry.Open()) manifest = Assert.IsType<JsonObject>(JsonNode.Parse(input));
            entry.Delete();
            var members = Assert.IsType<JsonArray>(manifest["entries"]);
            var original = kind == "double-claimed-entry" ? "lore/beta.bin" : "lore/Pair.bin";
            var alias = kind == "double-claimed-entry" ? "lore/ALPHA.bin" : "lore/PAIR.bin";
            Assert.Single(members, member => member!["path"]!.GetValue<string>() == original)!["path"] = alias;
            using var output = archive.CreateEntry("save_manifest.json", CompressionLevel.NoCompression).Open();
            output.Write(Encoding.UTF8.GetBytes(manifest.ToJsonString()));
        }
        var before = Snapshot(_files.GameSessionPath);
        var generation = File.ReadAllBytes(_files.SessionGenerationPath);

        var result = await _service.LoadGameWithOutcomeAsync(source);

        Report(result);
        Assert.Equal(LoadReplacementDisposition.NotLoaded, result.Disposition);
        Assert.IsType<InvalidDataException>(result.Failure);
        if (kind == "case-pair-hash" && OperatingSystem.IsLinux())
            Assert.Contains("SHA-256", Assert.IsType<InvalidDataException>(result.Failure).Message);
        Assert.Equal(0, _prepared);
        Assert.Equal(0, _lifecycleOpen);
        Assert.Empty(_phases);
        Assert.Equal(before.Keys.Order(), Snapshot(_files.GameSessionPath).Keys.Order());
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(_files.SessionGenerationPath));
        AssertOwnedScratchEmpty();
    }

    /// <summary>
    /// Extends a current-producer archive with an exact independently hashed payload and manifest entry.
    /// </summary>
    /// <param name="source">
    /// The owned current-producer archive with a complete original manifest.
    /// </param>
    /// <param name="path">
    /// The exact case-sensitive ZIP entry spelling, including deliberate collision scenarios.
    /// </param>
    /// <param name="bytes">
    /// Independent payload bytes.
    /// </param>
    /// <param name="invalidHash">
    /// Whether to deliberately substitute a valid-shaped but false digest for the negative admission scenario.
    /// </param>
    private static void AppendManifestedPayload(string source, string path, byte[] bytes, bool invalidHash = false)
    {
        using var archive = ZipFile.Open(source, ZipArchiveMode.Update);
        var original = Assert.IsType<ZipArchiveEntry>(archive.GetEntry("save_manifest.json"));
        JsonObject manifest;
        using (var input = original.Open()) manifest = Assert.IsType<JsonObject>(JsonNode.Parse(input));
        original.Delete();
        var entries = Assert.IsType<JsonArray>(manifest["entries"]);
        entries.Add(new JsonObject
        {
            ["path"] = path,
            ["length"] = bytes.Length,
            ["sha256"] = invalidHash ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes))
        });
        using (var output = archive.CreateEntry(path, CompressionLevel.NoCompression).Open()) output.Write(bytes);
        using var manifestOutput = archive.CreateEntry("save_manifest.json", CompressionLevel.NoCompression).Open();
        manifestOutput.Write(Encoding.UTF8.GetBytes(manifest.ToJsonString()));
    }
}
