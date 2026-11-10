using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks explicit crash seed selection using small synthetic durable data without creating game authority.
/// </summary>
public sealed class JointInterfaceSeedCrashInputTests
{
    /// <summary>
    /// Preserves the legacy path only when the opt-in property is absent.
    /// </summary>
    [Fact]
    public void Read_AbsentSeedSelectionPreservesLegacyMode()
    {
        Assert.Null(JointInterfaceSeedCrashInput.Read(new JsonObject(), false, new JointInterfaceCrashMode(4, false, null)));
    }

    /// <summary>
    /// Rejects every malformed explicit selection rather than treating it as an absent opt-in.
    /// </summary>
    /// <param name="violation">
    /// The invalid shape or field value supplied in the otherwise closed selection object.
    /// </param>
    [Theory]
    [InlineData("null")]
    [InlineData("string")]
    [InlineData("empty")]
    [InlineData("missing-field")]
    [InlineData("extra-field")]
    [InlineData("relative-path")]
    [InlineData("lowercase-digest")]
    public void Read_RejectsMalformedPresentSelectionWithoutLegacyFallback(string violation)
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        var selection = SelectionObject(fixture.Artifact);
        if (violation == "missing-field") selection.Remove("ManifestSha256");
        if (violation == "extra-field") selection["manifestSha256"] = new string('A', 64);
        if (violation == "relative-path") selection["ArtifactPath"] = "relative/artifact";
        if (violation == "lowercase-digest") selection["ManifestSha256"] = new string('a', 64);
        var parent = new JsonObject
        {
            ["Source42Seed"] = violation switch
            {
                "null" => null,
                "string" => JsonValue.Create("not an object"),
                "empty" => new JsonObject(),
                _ => selection
            }
        };

        Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Read(parent, true, new JointInterfaceCrashMode(0, false, null)));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Rejects forbidden modes before any repository or artifact file access can occur.
    /// </summary>
    /// <param name="windows">
    /// Whether the requested consumer runs on Windows.
    /// </param>
    /// <param name="cut">
    /// The requested crash cut index.
    /// </param>
    /// <param name="diagnostic">
    /// Whether the existing diagnostic mode was requested.
    /// </param>
    /// <param name="fault">
    /// The integrity fault requested with the selection, or <see langword="null"/> for no fault.
    /// </param>
    [Theory]
    [InlineData(false, 0, false, null)]
    [InlineData(true, 1, false, null)]
    [InlineData(true, 0, true, null)]
    [InlineData(true, 0, false, "Version")]
    public void BindAndRead_RejectForbiddenModesBeforeFileIo(bool windows, int cut, bool diagnostic, string? fault)
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        var missingRepository = Path.Combine(fixture.Root, "missing-repository");
        var mode = new JointInterfaceCrashMode(cut, diagnostic, fault);
        var parent = new JsonObject { ["Source42Seed"] = SelectionObject(fixture.Artifact) };

        Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Bind(fixture.Artifact, missingRepository, windows, mode));
        Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Read(parent, windows, mode));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Binds exact manifest bytes through parent metadata and imports an independent checked namespace.
    /// </summary>
    [Fact]
    public async Task BindReadAndOpen_ImportExactSelectedArtifactBytes()
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        await JointInterfaceCrashSeed.ExportAsync(fixture.Artifact, JointInterfaceCrashSeedTests.SeedFixture.Metadata,
            JointInterfaceSeedContractFingerprint.Compute(TestRepoPaths.RepoRoot),
            JointInterfaceCrashSeedTests.SeedFixture.Files(), JointInterfaceCrashSeedTests.SeedFixture.Directories());
        var mode = new JointInterfaceCrashMode(0, false, null);
        var bound = JointInterfaceSeedCrashInput.Bind(fixture.Artifact, TestRepoPaths.RepoRoot, true, mode);
        var parent = new JsonObject { ["Source42Seed"] = JsonSerializer.SerializeToNode(bound) };

        var selected = JointInterfaceSeedCrashInput.Read(parent, true, mode);
        Assert.Equal(bound, selected);
        Assert.Equal(fixture.Artifact, bound.ArtifactPath);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture.Artifact, "manifest.json")))), bound.ManifestSha256);
        var imported = Path.Combine(fixture.Root, "import");
        var metadata = await selected!.Open(TestRepoPaths.RepoRoot).MaterializeAsync(imported);

        Assert.Equal(JointInterfaceCrashSeedTests.SeedFixture.Metadata, metadata);
        Assert.Equal(Encoding.UTF8.GetBytes("\uFEFF{ \"synthetic\": true }\r\n"), File.ReadAllBytes(Path.Combine(imported, "game_session/identity.json")));
        Assert.Equal(Encoding.UTF8.GetBytes("{\"generation\":\"literal42\"}\n"), File.ReadAllBytes(Path.Combine(imported, ".boe_runtime/session-generation/current.json")));
        Assert.True(Directory.Exists(Path.Combine(imported, "game_session/empty")));
    }

    /// <summary>
    /// Rejects a manifest changed after binding even when the rewritten artifact is still valid for the codec.
    /// </summary>
    [Fact]
    public async Task Open_RejectsChangedManifestEvenWhenRewrittenArtifactIsValid()
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        var fingerprint = JointInterfaceSeedContractFingerprint.Compute(TestRepoPaths.RepoRoot);
        await JointInterfaceCrashSeed.ExportAsync(fixture.Artifact, JointInterfaceCrashSeedTests.SeedFixture.Metadata,
            fingerprint, JointInterfaceCrashSeedTests.SeedFixture.Files(), JointInterfaceCrashSeedTests.SeedFixture.Directories());
        var bound = JointInterfaceSeedCrashInput.Bind(fixture.Artifact, TestRepoPaths.RepoRoot, true, new JointInterfaceCrashMode(0, false, null));
        var manifestPath = Path.Combine(fixture.Artifact, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        manifest["metadata"]!["producerVersion"] = "another-valid-producer";
        File.WriteAllText(manifestPath, manifest.ToJsonString());
        Assert.Equal("another-valid-producer", JointInterfaceCrashSeed.Open(fixture.Artifact, fingerprint).Metadata.ProducerVersion);

        Assert.Throws<InvalidDataException>(() => bound.Open(TestRepoPaths.RepoRoot));
        Assert.Equal(new[] { fixture.Artifact }, Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Refuses an absent artifact without creating a producer fixture or invoking a replacement generator.
    /// </summary>
    [Fact]
    public void Bind_MissingArtifactFailsWithoutCreatingAnyRoot()
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();

        Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Bind(fixture.Artifact,
            TestRepoPaths.RepoRoot, true, new JointInterfaceCrashMode(0, false, null)));

        Assert.Empty(Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Refuses a physically valid artifact produced for different source contracts without modifying its manifest.
    /// </summary>
    [Fact]
    public async Task Bind_RejectsStaleContractWithoutRebindingArtifact()
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        await JointInterfaceCrashSeed.ExportAsync(fixture.Artifact, JointInterfaceCrashSeedTests.SeedFixture.Metadata,
            new string('0', 64), JointInterfaceCrashSeedTests.SeedFixture.Files(), JointInterfaceCrashSeedTests.SeedFixture.Directories());
        var manifestPath = Path.Combine(fixture.Artifact, "manifest.json");
        var before = File.ReadAllBytes(manifestPath);

        Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Bind(fixture.Artifact,
            TestRepoPaths.RepoRoot, true, new JointInterfaceCrashMode(0, false, null)));

        Assert.Equal(before, File.ReadAllBytes(manifestPath));
        Assert.Equal(new[] { fixture.Artifact }, Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Creates the documented Pascal-case parent selection independently of the input parser.
    /// </summary>
    /// <param name="artifactPath">
    /// The absolute artifact directory represented by this metadata-only object.
    /// </param>
    /// <returns>
    /// A fresh closed selection object with a syntactically valid uppercase manifest digest.
    /// </returns>
    private static JsonObject SelectionObject(string artifactPath) => new()
    {
        ["ArtifactPath"] = artifactPath,
        ["ManifestSha256"] = new string('A', 64)
    };
}
