using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the explicit five-cut selection with synthetic artifacts and independent closed wire metadata.
/// </summary>
public sealed class JointInterfaceFiveCutSeedInputTests
{
    /// <summary>
    /// Binds, round-trips and imports the same source42 shape for every explicit ordinary Windows cut.
    /// </summary>
    /// <param name="cut">
    /// The original cut from zero through four; no cut-specific authority is stored in the artifact.
    /// </param>
    /// <returns>
    /// Completion after a checked import retains the exact synthetic payload and leaves its source immutable.
    /// </returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ExplicitFiveUsageBindsAndImportsEveryOrdinaryWindowsCut(int cut)
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        var repository = CreateSyntheticRepository(fixture.Root);
        await JointInterfaceCrashSeed.ExportAsync(fixture.Artifact, JointInterfaceCrashSeedTests.SeedFixture.Metadata,
            JointInterfaceSeedContractFingerprint.Compute(repository), JointInterfaceCrashSeedTests.SeedFixture.Files(),
            JointInterfaceCrashSeedTests.SeedFixture.Directories());
        var manifestPath = Path.Combine(fixture.Artifact, "manifest.json");
        var manifestBefore = File.ReadAllBytes(manifestPath);
        var mode = new JointInterfaceCrashMode(cut, false, null);

        // Mutation caught: retaining the legacy-only usage gate prevents a checked five-cut import.
        var bound = JointInterfaceSeedCrashInput.Bind(fixture.Artifact, repository, true, mode,
            JointInterfaceSeedCrashInput.OrdinaryWindowsFiveCutsUsage);
        var serialized = JsonSerializer.SerializeToNode(bound)!.AsObject();
        Assert.Equal(new[] { "ArtifactPath", "ManifestSha256", "Usage" }, serialized.Select(row => row.Key).Order(StringComparer.Ordinal));
        Assert.Equal("ordinary-windows-five-cuts-v1", serialized["Usage"]!.GetValue<string>());
        var selected = JointInterfaceSeedCrashInput.Read(new JsonObject { ["Source42Seed"] = serialized }, true, mode);
        Assert.Equal(bound, selected);
        var destination = Path.Combine(fixture.Root, "import");
        Assert.Equal(JointInterfaceCrashSeedTests.SeedFixture.Metadata,
            await selected!.Open(repository).MaterializeAsync(destination));
        Assert.Equal(Encoding.UTF8.GetBytes("\uFEFF{ \"synthetic\": true }\r\n"),
            File.ReadAllBytes(Path.Combine(destination, "game_session", "identity.json")));
        Assert.Equal(Encoding.UTF8.GetBytes("{\"generation\":\"literal42\"}\n"),
            File.ReadAllBytes(Path.Combine(destination, ".boe_runtime", "session-generation", "current.json")));
        Assert.True(Directory.Exists(Path.Combine(destination, "game_session", "empty")));
        Assert.Equal(manifestBefore, File.ReadAllBytes(manifestPath));
    }

    /// <summary>
    /// Preserves the exact legacy wire shape and its cut1 refusal when usage is absent.
    /// </summary>
    [Fact]
    public void AbsentUsageKeepsTwoFieldWireAndLegacyCutZeroOnly()
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        var value = new JointInterfaceSeedCrashInput(fixture.Artifact, new string('A', 64));
        var serialized = JsonSerializer.SerializeToNode(value)!.AsObject();
        Assert.Equal(new[] { "ArtifactPath", "ManifestSha256" }, serialized.Select(row => row.Key).Order(StringComparer.Ordinal));
        var parent = new JsonObject { ["Source42Seed"] = serialized };
        Assert.Equal(value, JointInterfaceSeedCrashInput.Read(parent, true, new(0, false, null)));
        var error = Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Read(parent, true, new(1, false, null)));
        Assert.Equal("Source42Seed is permitted only for ordinary Windows cut0.", error.Message);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Rejects forbidden explicit usage combinations before artifact IO or namespace creation.
    /// </summary>
    /// <param name="windows">
    /// Whether the selected platform is Windows.
    /// </param>
    /// <param name="cut">
    /// The requested cut, including negative and too-large values.
    /// </param>
    /// <param name="diagnostic">
    /// Whether diagnostic mode is requested.
    /// </param>
    /// <param name="fault">
    /// An integrity fault, or <see langword="null"/> for no fault.
    /// </param>
    /// <param name="acquisition">
    /// Whether the incompatible fixture-acquisition mode is requested.
    /// </param>
    [Theory]
    [InlineData(false, 0, false, null, false)]
    [InlineData(false, 4, false, null, false)]
    [InlineData(true, -1, false, null, false)]
    [InlineData(true, 5, false, null, false)]
    [InlineData(true, 0, true, null, false)]
    [InlineData(true, 4, true, null, false)]
    [InlineData(true, 0, false, "Version", false)]
    [InlineData(true, 2, false, "CurrentInterface", false)]
    [InlineData(true, 0, false, null, true)]
    [InlineData(true, 4, false, null, true)]
    public void ExplicitFiveUsageRejectsUnsupportedModesBeforeArtifactIo(
        bool windows, int cut, bool diagnostic, string? fault, bool acquisition)
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        var mode = new JointInterfaceCrashMode(cut, diagnostic, fault);
        var parent = Parent(fixture.Artifact);
        if (acquisition)
            parent["FixtureAcquisitionOnly"] = true;
        var bindError = Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Bind(fixture.Artifact,
            Path.Combine(fixture.Root, "nonexistent-repository"), windows, mode,
            JointInterfaceSeedCrashInput.OrdinaryWindowsFiveCutsUsage, acquisition));
        var readError = Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Read(parent, windows, mode));
        Assert.DoesNotContain("manifest", bindError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manifest", readError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Rejects every malformed or unknown explicit usage instead of falling back to legacy behavior.
    /// </summary>
    /// <param name="violation">
    /// The malformed usage node or closed-object violation to introduce.
    /// </param>
    [Theory]
    [InlineData("null")]
    [InlineData("number")]
    [InlineData("object")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("wrong-case")]
    [InlineData("extra-field")]
    public void ExplicitUsageRejectsMalformedOrUnknownClosedMetadata(string violation)
    {
        using var fixture = new JointInterfaceCrashSeedTests.SeedFixture();
        var parent = Parent(fixture.Artifact);
        var selection = parent["Source42Seed"]!.AsObject();
        selection["Usage"] = violation switch
        {
            "null" => null,
            "number" => JsonValue.Create(42),
            "object" => new JsonObject(),
            "empty" => JsonValue.Create(""),
            "unknown" => JsonValue.Create("future-five-cuts"),
            "wrong-case" => JsonValue.Create("ORDINARY-WINDOWS-FIVE-CUTS-V1"),
            _ => JsonValue.Create("ordinary-windows-five-cuts-v1")
        };
        if (violation == "extra-field")
            selection["PreparedStage43"] = true;
        Assert.Throws<InvalidDataException>(() => JointInterfaceSeedCrashInput.Read(parent, true, new(0, false, null)));
        Assert.Empty(Directory.GetFileSystemEntries(fixture.Root));
    }

    /// <summary>
    /// Independently constructs the explicit parent wire object without opening an artifact.
    /// </summary>
    /// <param name="artifact">
    /// The ordinary absolute artifact spelling; the directory may be absent.
    /// </param>
    /// <returns>
    /// Fresh closed metadata using the literal accepted usage spelling and an uppercase digest.
    /// </returns>
    private static JsonObject Parent(string artifact) => new()
    {
        ["Source42Seed"] = new JsonObject
        {
            ["ArtifactPath"] = artifact,
            ["ManifestSha256"] = new string('A', 64),
            ["Usage"] = "ordinary-windows-five-cuts-v1"
        }
    };

    /// <summary>
    /// Creates a tiny ordinary source closure for codec checks without constructing a game world.
    /// </summary>
    /// <param name="root">
    /// The existing GUID-owned synthetic fixture root.
    /// </param>
    /// <returns>
    /// The synthetic repository containing all required source and asset directories.
    /// </returns>
    private static string CreateSyntheticRepository(string root)
    {
        var repository = Path.Combine(root, "repository");
        foreach (var relative in new[] { "BookOfEternityClient", "BookOfEternityClient.IntegrationTests",
                     "BookOfEternityClient.TestSupport", "FileSystemExample/game_session" })
            Directory.CreateDirectory(Path.Combine(repository, relative));
        File.WriteAllText(Path.Combine(repository, "BookOfEternityClient", "Source.cs"), "class SyntheticSource { }");
        File.WriteAllText(Path.Combine(repository, "FileSystemExample", "game_session", "identity.json"), "{\"synthetic\":true}");
        return repository;
    }
}
