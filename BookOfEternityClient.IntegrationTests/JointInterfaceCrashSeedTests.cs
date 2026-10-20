using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the seed codec with literal synthetic data, without qualifying game checkpoint state.
/// </summary>
public sealed class JointInterfaceCrashSeedTests
{
    private const string Fingerprint = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string GenerationPath = ".boe_runtime/session-generation/current.json";
    private const string IdentityPath = "game_session/identity.json";
    private const string IdentityText = "\uFEFF{ \"synthetic\": true }\r\n";
    private const string GenerationText = "{\"generation\":\"literal42\"}\n";

    /// <summary>
    /// Preserves exact bytes, empty directories and provenance through export and physical import.
    /// </summary>
    [Fact]
    public async Task ExportAndOpen_RoundTripExactNamespaceWithoutNormalizingBytes()
    {
        using var fixture = new SeedFixture();
        await JointInterfaceCrashSeed.ExportAsync(fixture.Artifact, SeedFixture.Metadata,
            Fingerprint, SeedFixture.Files(), SeedFixture.Directories());

        var seed = JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint);
        var target = Path.Combine(fixture.Root, "import");
        var metadata = await seed.MaterializeAsync(target);

        Assert.Equal(SeedFixture.Metadata, metadata);
        Assert.Equal(SeedFixture.Metadata, seed.Metadata);
        Assert.Equal(Fingerprint, seed.ContractFingerprint);
        Assert.Equal(2, seed.FileCount);
        Assert.Equal(Encoding.UTF8.GetByteCount(IdentityText) + Encoding.UTF8.GetByteCount(GenerationText), seed.PayloadBytes);
        AssertLiteralNamespace(target);
    }

    /// <summary>
    /// Makes two physically independent imports and leaves the source artifact unchanged after mutation.
    /// </summary>
    [Fact]
    public async Task Materialize_TwoCopiesDoNotShareMutableFilesOrGenerationData()
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact();
        var seed = JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint);
        var first = Path.Combine(fixture.Root, "first");
        var second = Path.Combine(fixture.Root, "second");
        await seed.MaterializeAsync(first);
        await seed.MaterializeAsync(second);

        File.WriteAllText(Path.Combine(first, IdentityPath), "changed identity");
        File.WriteAllText(Path.Combine(first, GenerationPath), "changed generation");
        Directory.Delete(Path.Combine(first, "game_session/empty"));

        AssertLiteralNamespace(second);
        AssertLiteralNamespace(Path.Combine(fixture.Artifact, "payload"));
    }

    /// <summary>
    /// Retains checked private bytes rather than rereading an artifact changed after Open.
    /// </summary>
    [Fact]
    public async Task Materialize_UsesValidatedSnapshotAfterArtifactFilesAreChangedOrRemoved()
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact();
        var seed = JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint);
        File.WriteAllText(Path.Combine(fixture.Artifact, "payload", IdentityPath), "later corruption");
        File.Delete(Path.Combine(fixture.Artifact, "payload", GenerationPath));
        File.WriteAllText(Path.Combine(fixture.Artifact, "manifest.json"), "invalid later manifest");

        var target = Path.Combine(fixture.Root, "snapshot-copy");
        await seed.MaterializeAsync(target);

        AssertLiteralNamespace(target);
        Assert.Equal("later corruption", File.ReadAllText(Path.Combine(fixture.Artifact, "payload", IdentityPath)));
    }

    /// <summary>
    /// Rejects stale, incomplete and corrupt artifacts before any import can start.
    /// </summary>
    /// <param name="corruption">
    /// The independent manifest or physical inventory violation introduced into a valid artifact.
    /// </param>
    [Theory]
    [InlineData("fingerprint")]
    [InlineData("version")]
    [InlineData("missing-file")]
    [InlineData("changed-file")]
    [InlineData("extra-file")]
    [InlineData("extra-empty-directory")]
    [InlineData("generation-digest")]
    public void Open_RejectsCorruptOrUnboundArtifact(string corruption)
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact(manifest =>
        {
            if (corruption == "fingerprint") manifest["contractFingerprint"] = new string('B', 64);
            if (corruption == "version") manifest["version"] = 2;
            if (corruption == "generation-digest") manifest["metadata"]!["generationSha256"] = new string('B', 64);
        });
        var payload = Path.Combine(fixture.Artifact, "payload");
        if (corruption == "missing-file") File.Delete(Path.Combine(payload, IdentityPath));
        if (corruption == "changed-file") File.WriteAllText(Path.Combine(payload, IdentityPath), "corrupt");
        if (corruption == "extra-file") File.WriteAllText(Path.Combine(payload, "game_session/extra.json"), "{}");
        if (corruption == "extra-empty-directory") Directory.CreateDirectory(Path.Combine(payload, "game_session/extra"));

        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint));
    }

    /// <summary>
    /// Refuses unknown or repeated JSON members instead of silently choosing one interpretation.
    /// </summary>
    /// <param name="schemaViolation">
    /// The closed-schema violation added independently to the artifact manifest.
    /// </param>
    [Theory]
    [InlineData("unknown-top-level")]
    [InlineData("duplicate-top-level")]
    [InlineData("duplicate-metadata")]
    public void Open_RejectsAmbiguousOrUnknownManifestMembers(string schemaViolation)
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact();
        var path = Path.Combine(fixture.Artifact, "manifest.json");
        var json = File.ReadAllText(path);
        json = schemaViolation switch
        {
            "unknown-top-level" => json.Insert(1, "\"unrecognized\":true,"),
            "duplicate-top-level" => json.Insert(1, "\"version\":1,"),
            _ => json.Replace("\"sessionId\":", "\"sessionId\":\"other\",\"sessionId\":", StringComparison.Ordinal)
        };
        File.WriteAllText(path, json);

        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint));
    }

    /// <summary>
    /// Prevents escaped, case-conflicting or unsupported runtime entries from entering an owned root.
    /// </summary>
    /// <param name="unsafePath">
    /// A disallowed manifest path replacing the ordinary identity file path.
    /// </param>
    [Theory]
    [InlineData("../outside.json")]
    [InlineData("game_session\\identity.json")]
    [InlineData("game_session/../outside.json")]
    [InlineData(".boe_runtime/locks/owner.json")]
    [InlineData("GAME_SESSION/identity.json")]
    public void Open_RejectsPathsOutsidePortableDurableNamespace(string unsafePath)
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact(manifest => manifest["files"]![0]!["path"] = unsafePath);

        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "outside.json")));
    }

    /// <summary>
    /// Rejects duplicate and case-conflicting file claims even if their bytes and digests agree.
    /// </summary>
    /// <param name="caseAlias">
    /// Whether the second path differs only by case rather than matching exactly.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Open_RejectsDuplicatePhysicalFileClaims(bool caseAlias)
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact(manifest =>
        {
            var files = manifest["files"]!.AsArray();
            var duplicate = files[0]!.DeepClone();
            if (caseAlias) duplicate["path"] = "game_session/IDENTITY.json";
            files.Add(duplicate);
        });

        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint));
    }

    /// <summary>
    /// Requires the source checkpoint tuple, generation binding and culture to describe source turn 42.
    /// </summary>
    /// <param name="field">
    /// The metadata contract member made invalid while preserving all physical payload bytes.
    /// </param>
    [Theory]
    [InlineData("turnNumber")]
    [InlineData("sessionId")]
    [InlineData("culture")]
    [InlineData("producerVersion")]
    public void Open_RejectsInvalidSourceProvenance(string field)
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact(manifest =>
        {
            var metadata = manifest["metadata"]!;
            if (field == "turnNumber") metadata[field] = 41;
            else metadata[field] = field == "culture" ? "en-US" : " ";
        });

        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint));
    }

    /// <summary>
    /// Refuses an already existing destination without deleting or rewriting its contents.
    /// </summary>
    [Fact]
    public async Task Materialize_RejectsExistingRootAndPreservesForeignContents()
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact();
        var seed = JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint);
        var target = Path.Combine(fixture.Root, "foreign");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "sentinel"), "keep");

        await Assert.ThrowsAnyAsync<IOException>(() => seed.MaterializeAsync(target));

        Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "sentinel")));
        Assert.Single(Directory.GetFileSystemEntries(target));
    }

    /// <summary>
    /// Refuses to create an owned import inside the artifact it would otherwise mutate.
    /// </summary>
    [Fact]
    public async Task Materialize_RejectsArtifactDescendantWithoutWritingAnything()
    {
        using var fixture = new SeedFixture();
        fixture.WriteIndependentArtifact();
        var seed = JointInterfaceCrashSeed.Open(fixture.Artifact, Fingerprint);
        var target = Path.Combine(fixture.Artifact, "nested-copy");

        await Assert.ThrowsAnyAsync<IOException>(() => seed.MaterializeAsync(target));

        Assert.False(Directory.Exists(target));
        AssertLiteralNamespace(Path.Combine(fixture.Artifact, "payload"));
    }

    /// <summary>
    /// Refuses to replace an existing artifact or remove any of its original data on failure.
    /// </summary>
    [Fact]
    public async Task Export_RejectsExistingArtifactAndPreservesIt()
    {
        using var fixture = new SeedFixture();
        Directory.CreateDirectory(fixture.Artifact);
        File.WriteAllText(Path.Combine(fixture.Artifact, "sentinel"), "keep");

        await Assert.ThrowsAnyAsync<IOException>(() => JointInterfaceCrashSeed.ExportAsync(fixture.Artifact,
            SeedFixture.Metadata, Fingerprint, SeedFixture.Files(), SeedFixture.Directories()));

        Assert.Equal("keep", File.ReadAllText(Path.Combine(fixture.Artifact, "sentinel")));
        Assert.Single(Directory.GetFileSystemEntries(fixture.Artifact));
    }

    /// <summary>
    /// Checks hand-authored expected bytes and complete relative inventory after an import.
    /// </summary>
    /// <param name="root">
    /// The physical root whose ordinary synthetic namespace must be preserved exactly.
    /// </param>
    private static void AssertLiteralNamespace(string root)
    {
        Assert.Equal(Encoding.UTF8.GetBytes(IdentityText), File.ReadAllBytes(Path.Combine(root, IdentityPath)));
        Assert.Equal(Encoding.UTF8.GetBytes(GenerationText), File.ReadAllBytes(Path.Combine(root, GenerationPath)));
        Assert.Equal(new[] { GenerationPath, IdentityPath }, Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal));
        Assert.Equal(new[] { ".boe_runtime", ".boe_runtime/session-generation", "game_session", "game_session/empty" },
            Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/')).OrderBy(path => path, StringComparer.Ordinal));
    }

    /// <summary>
    /// Owns a small literal synthetic namespace and independently writes the documented artifact schema.
    /// </summary>
    private sealed class SeedFixture : IDisposable
    {
        /// <summary>
        /// Creates a unique existing parent for artifacts and imports owned solely by this test.
        /// </summary>
        public SeedFixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "boe-seed-codec-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Artifact = Path.Combine(Root, "artifact");
        }

        /// <summary>
        /// Gets the independently owned test parent directory.
        /// </summary>
        public string Root { get; }

        /// <summary>
        /// Gets the initially nonexistent artifact destination.
        /// </summary>
        public string Artifact { get; }

        /// <summary>
        /// Gets literal source provenance bound to the small synthetic generation data.
        /// </summary>
        public static JointInterfaceCrashSeedMetadata Metadata => new("source-session", "source-request",
            "source-snapshot", 42, "source-opportunity", Digest(Encoding.UTF8.GetBytes(GenerationText)), "source-commit", "en-GB");

        /// <summary>
        /// Creates fresh byte arrays for the two literal durable files.
        /// </summary>
        /// <returns>
        /// A mutable test-owned snapshot containing identity and generation data.
        /// </returns>
        public static Dictionary<string, byte[]> Files() => new(StringComparer.Ordinal)
        {
            [IdentityPath] = Encoding.UTF8.GetBytes(IdentityText),
            [GenerationPath] = Encoding.UTF8.GetBytes(GenerationText)
        };

        /// <summary>
        /// Provides the literal complete directory inventory, including an empty game directory.
        /// </summary>
        /// <returns>
        /// Fresh portable relative directory names for the synthetic payload.
        /// </returns>
        public static string[] Directories() =>
            ["game_session", "game_session/empty", ".boe_runtime", ".boe_runtime/session-generation"];

        /// <summary>
        /// Writes real files and a manifest without using the codec exporter under test.
        /// </summary>
        /// <param name="changeManifest">
        /// An optional controlled corruption applied after independently constructing the valid manifest.
        /// </param>
        public void WriteIndependentArtifact(Action<JsonObject>? changeManifest = null)
        {
            var payload = Path.Combine(Artifact, "payload");
            foreach (var directory in Directories()) Directory.CreateDirectory(Path.Combine(payload, directory));
            foreach (var file in Files()) File.WriteAllBytes(Path.Combine(payload, file.Key), file.Value);
            var manifest = new JsonObject
            {
                ["version"] = 1,
                ["contractFingerprint"] = Fingerprint,
                ["metadata"] = JsonSerializer.SerializeToNode(Metadata, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
                ["directories"] = new JsonArray(Directories().Select(path => (JsonNode?)JsonValue.Create(path)).ToArray()),
                ["files"] = new JsonArray(Files().Select(file => (JsonNode?)new JsonObject
                {
                    ["path"] = file.Key,
                    ["length"] = file.Value.Length,
                    ["sha256"] = Digest(file.Value)
                }).ToArray())
            };
            changeManifest?.Invoke(manifest);
            File.WriteAllText(Path.Combine(Artifact, "manifest.json"), manifest.ToJsonString());
        }

        /// <summary>
        /// Removes only this fixture's unique owned parent and all files created below it.
        /// </summary>
        public void Dispose() => Directory.Delete(Root, recursive: true);

        /// <summary>
        /// Computes an independent manifest digest for literal test bytes.
        /// </summary>
        /// <param name="bytes">
        /// The exact bytes whose integrity the manifest describes.
        /// </param>
        /// <returns>
        /// The uppercase SHA256 digest of <paramref name="bytes"/>.
        /// </returns>
        private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    }
}
