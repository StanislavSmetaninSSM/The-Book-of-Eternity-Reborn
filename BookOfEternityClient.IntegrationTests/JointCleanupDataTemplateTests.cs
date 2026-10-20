using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks cleanup-entry transport using synthetic DATA that cannot qualify a live game original.
/// </summary>
public sealed class JointCleanupDataTemplateTests
{
    /// <summary>
    /// Distinguishes genuine cleanup provenance from the separately supported applied role.
    /// </summary>
    /// <param name="appliedRole">
    /// Whether the input deliberately uses the incompatible AppliedBeforeStory provenance.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaterializeUsesExplicitCleanupProvenance(bool appliedRole)
    {
        var owned = Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-cleanup-transport-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        using var cleanup = new CleanupOwnedFixture(owned, Console.WriteLine);
        var source = Path.Combine(owned, "source");
        Directory.CreateDirectory(Path.Combine(source, "game_session", "empty"));
        Directory.CreateDirectory(Path.Combine(source, ".boe_runtime", "session-generation"));
        File.WriteAllText(Path.Combine(source, ".boe_runtime", "session-generation", "current.json"), "{}");
        File.WriteAllText(Path.Combine(source, "game_session", "data.txt"), "exact cleanup DATA");
        var provenance = appliedRole ? new JsonObject { ["Role"] = "AppliedBeforeStory" } : new JsonObject
        {
            ["Operation"] = "JointStory.CleanupEligibleTerminalAsync", ["Cut"] = 0,
            ["Parent"] = new JsonObject { ["Cut"] = 0, ["ParentPid"] = Environment.ProcessId,
                ["Nonce"] = Guid.NewGuid().ToString("N"), ["RunId"] = Guid.NewGuid().ToString("N"),
                ["Diagnostic"] = false, ["Fault"] = null }
        };
        var archive = Path.Combine(owned, "observed.zip");
        JointCleanupDataCapture.Capture(source, archive, provenance);
        var bytes = File.ReadAllBytes(archive);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var target = Path.Combine(owned, "copy");
        if (appliedRole)
        {
            Assert.Throws<InvalidDataException>(() => JointCleanupDataTemplate.Materialize(archive, hash, target));
            Assert.False(Directory.Exists(target));
        }
        else
        {
            var manifest = JointCleanupDataTemplate.Materialize(archive, hash, target);
            Assert.False(manifest["GrantsAuthority"]!.GetValue<bool>());
            Assert.Null(manifest["Provenance"]!["Role"]); Assert.Null(manifest["Provenance"]!["Original"]);
            Assert.Equal("exact cleanup DATA", File.ReadAllText(Path.Combine(target, "game_session", "data.txt")));
            Assert.True(Directory.Exists(Path.Combine(target, "game_session", "empty")));
            File.WriteAllText(Path.Combine(target, "game_session", "data.txt"), "private mutation");
            var second = Path.Combine(owned, "second");
            JointCleanupDataTemplate.Materialize(archive, hash, second);
            Assert.Equal("exact cleanup DATA", File.ReadAllText(Path.Combine(second, "game_session", "data.txt")));
        }
        Assert.Equal(bytes, File.ReadAllBytes(archive));
        Assert.Equal("exact cleanup DATA", File.ReadAllText(Path.Combine(source, "game_session", "data.txt")));
    }
}
