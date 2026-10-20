using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Loading owned seed bytes must not initialize a new item identity or generation over them.
    /// </summary>
    [Fact]
    public void JointInterfaceSeed_AdoptedRootPreservesIdentityAndGenerationBytes()
    {
        var root = Path.Combine(_rootPath, "owned-seed-copy");
        var identityPath = Path.Combine(root, "game_session", MortalItemIdentityState.StatePath.Replace('/', Path.DirectorySeparatorChar));
        var generationPath = Path.Combine(root, ".boe_runtime", "session-generation", "current.json");
        var identity = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString() + "\r\n")).ToArray();
        var generation = File.ReadAllBytes(Path.Combine(_fs.RuntimeRootPath, "session-generation", "current.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(identityPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(generationPath)!);
        File.WriteAllBytes(identityPath, identity);
        File.WriteAllBytes(generationPath, generation);
        try
        {
            using var adopted = new GameEngineTurnLifecycleTests(_directGachaOutput, root);
            Assert.Equal(Path.GetFullPath(root), Path.GetFullPath(adopted._rootPath));
            Assert.Equal(identity, File.ReadAllBytes(identityPath));
            Assert.Equal(generation, File.ReadAllBytes(generationPath));
        }
        finally
        {
            new CleanupOwnedFixture(root, line => _directGachaOutput?.WriteLine(line)).Dispose();
        }
        Assert.False(Directory.Exists(root));
    }

    /// <summary>
    /// A missing adopted directory must fail before any world or runtime path is created.
    /// </summary>
    [Fact]
    public void JointInterfaceSeed_AdoptionRefusesMissingRootWithoutCreatingIt()
    {
        var root = Path.Combine(_rootPath, "missing-seed-copy");
        Assert.Throws<DirectoryNotFoundException>(() => new GameEngineTurnLifecycleTests(_directGachaOutput, root));
        Assert.False(Directory.Exists(root));
    }

    /// <summary>
    /// Ordinary lifecycle fixtures continue to initialize the canonical empty item identity.
    /// </summary>
    [Fact]
    public void JointInterfaceSeed_DefaultFixtureStillInitializesIdentity()
    {
        var path = _fs.ResolvePath(MortalItemIdentityState.StatePath);
        Assert.True(File.Exists(path));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal(1, root["schemaVersion"]!.GetValue<int>());
    }

    /// <summary>
    /// Game inputs invalidate a seed while documentation and generated build files do not.
    /// </summary>
    /// <param name="relativePath">
    /// The single physical input changed after the first fingerprint.
    /// </param>
    /// <param name="changesContract">
    /// Whether this input belongs to the declared generator contract closure.
    /// </param>
    [Theory]
    [InlineData("BookOfEternityClient/Game.cs", true)]
    [InlineData("FileSystemExample/game_session/world.json", true)]
    [InlineData("Directory.Build.props", true)]
    [InlineData("docs/note.md", false)]
    [InlineData("BookOfEternityClient/obj/Generated.cs", false)]
    public void JointInterfaceSeed_FingerprintTracksInputsInsteadOfDocumentation(string relativePath, bool changesContract)
    {
        var root = Path.Combine(_rootPath, "fingerprint-inputs");
        WriteSeedFingerprintInputs(root);
        var before = JointInterfaceSeedContractFingerprint.Compute(root);
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "changed-input");
        var after = JointInterfaceSeedContractFingerprint.Compute(root);
        Assert.Equal(changesContract, before != after);
    }

    /// <summary>
    /// Moving identical source inputs to another checkout does not invalidate an immutable seed.
    /// </summary>
    [Fact]
    public void JointInterfaceSeed_FingerprintDoesNotBindAbsoluteCheckoutRoot()
    {
        var first = Path.Combine(_rootPath, "fingerprint-first");
        var second = Path.Combine(_rootPath, "fingerprint-second");
        WriteSeedFingerprintInputs(first);
        WriteSeedFingerprintInputs(second);
        Assert.Equal(JointInterfaceSeedContractFingerprint.Compute(first), JointInterfaceSeedContractFingerprint.Compute(second));
    }

    /// <summary>
    /// Writes a small declared source closure without creating a runnable game or fake authority.
    /// </summary>
    /// <param name="root">
    /// The fresh owned test directory for literal source and asset inputs.
    /// </param>
    private static void WriteSeedFingerprintInputs(string root)
    {
        var inputs = new Dictionary<string, string>
        {
            ["BookOfEternityClient/Game.cs"] = "class LiteralGame {}",
            ["BookOfEternityClient/Game.csproj"] = "<Project />",
            ["BookOfEternityClient.IntegrationTests/Generator.cs"] = "class LiteralGenerator {}",
            ["BookOfEternityClient.TestSupport/Host.cs"] = "class LiteralHost {}",
            ["FileSystemExample/game_session/world.json"] = "{\"literal\":true}",
            ["Directory.Build.props"] = "<Project />"
        };
        foreach (var (relative, content) in inputs)
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
    }
}
