using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks Linux DATA selection and transport without issuing a game capability.
/// </summary>
public sealed class JointAppliedDataSourceTests
{
    /// <summary>
    /// Requires an explicit current Linux source and refuses invalid input before creating a copy.
    /// </summary>
    /// <param name="fault">
    /// The malformed record or transport condition, or none for independent exact copies.
    /// </param>
    [Theory]
    [InlineData("none")]
    [InlineData("missing")]
    [InlineData("relative")]
    [InlineData("platform")]
    [InlineData("generator")]
    [InlineData("build")]
    [InlineData("archive-hash")]
    [InlineData("inner-provenance")]
    public void LinuxSourceRequiresCurrentProvenanceBeforeCopy(string fault)
    {
        var owned = Path.Combine(Path.GetTempPath(), "boe-linux-applied-source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        using var cleanup = new CleanupOwnedFixture(owned, Console.WriteLine);
        var source = Path.Combine(owned, "source");
        Directory.CreateDirectory(Path.Combine(source, "game_session"));
        Directory.CreateDirectory(Path.Combine(source, ".boe_runtime", "session-generation"));
        File.WriteAllText(Path.Combine(source, ".boe_runtime", "session-generation", "current.json"), "{}");
        File.WriteAllText(Path.Combine(source, "game_session", "data.txt"), "independent DATA only");
        var archive = Path.Combine(owned, "observed.zip");
        JointCleanupDataCapture.Capture(source, archive, new JsonObject
        {
            ["Role"] = "AppliedBeforeStory", ["Platform"] = "Linux",
            ["Generator"] = fault == "inner-provenance" ? "foreign" : JointAppliedDataTemplate.LinuxGenerator
        });
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)));
        var gameHash = JointAppliedDataTemplate.CurrentGameAssemblySha256();
        var record = new JsonObject
        {
            ["SchemaVersion"] = 1, ["Platform"] = fault == "platform" ? "Windows" : "Linux",
            ["Generator"] = fault == "generator" ? "foreign" : JointAppliedDataTemplate.LinuxGenerator,
            ["GameAssemblySHA256"] = fault == "build" ? new string('0', 64) : gameHash,
            ["Archive"] = archive, ["ArchiveSHA256"] = fault == "archive-hash" ? new string('0', 64) : hash
        };
        var recordPath = Path.Combine(owned, "source-record.json");
        File.WriteAllText(recordPath, record.ToJsonString());
        var requested = fault == "missing" ? null : fault == "relative" ? "relative.json" : recordPath;
        var target = Path.Combine(owned, "copy");
        if (fault != "none")
        {
            Assert.Throws<InvalidDataException>(() =>
                JointAppliedDataTemplate.Materialize(JointAppliedDataTemplate.ResolveLinux(requested, gameHash), target));
            Assert.False(Directory.Exists(target));
        }
        else
        {
            var input = JointAppliedDataTemplate.ResolveLinux(requested, gameHash);
            var manifest = JointAppliedDataTemplate.Materialize(input, target);
            Assert.False(manifest["GrantsAuthority"]!.GetValue<bool>());
            Assert.Equal(hash, input.ArchiveSha256);
            File.WriteAllText(Path.Combine(target, "game_session", "data.txt"), "private mutation");
            var second = Path.Combine(owned, "second");
            JointAppliedDataTemplate.Materialize(input, second);
            Assert.Equal("independent DATA only", File.ReadAllText(Path.Combine(second, "game_session", "data.txt")));
        }
        Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))));
        Assert.Equal("independent DATA only", File.ReadAllText(Path.Combine(source, "game_session", "data.txt")));
    }
}
