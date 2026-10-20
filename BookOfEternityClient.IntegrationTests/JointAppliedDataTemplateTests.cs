using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Tests DATA transport only; synthetic bytes never qualify a game original or capability.
/// </summary>
public sealed class JointAppliedDataTemplateTests
{
    /// <summary>
    /// Copies exact DATA or refuses damaged/hash/namespace/absence/role/overwrite input before target creation.
    /// </summary>
    /// <param name="fault">
    /// The deliberately malformed transport condition, or none for exact independent copies.
    /// </param>
    [Theory]
    [InlineData("none")]
    [InlineData("archive-hash")]
    [InlineData("member-hash")]
    [InlineData("traversal")]
    [InlineData("runtime")]
    [InlineData("existing")]
    [InlineData("absence")]
    [InlineData("duplicate")]
    [InlineData("role")]
    [InlineData("directory-parent")]
    [InlineData("absent-alias")]
    public void Materialize_RequiresExactOrdinaryDataBeforeWriting(string fault)
    {
        var owned = Path.Combine(Path.GetTempPath(), "boe-applied-data-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        using var cleanup = new CleanupOwnedFixture(owned, Console.WriteLine);
        var source = Path.Combine(owned, "source");
        Directory.CreateDirectory(Path.Combine(source, "game_session", "empty"));
        Directory.CreateDirectory(Path.Combine(source, ".boe_runtime", "session-generation"));
        File.WriteAllText(Path.Combine(source, ".boe_runtime", "session-generation", "current.json"), "{}");
        File.WriteAllText(Path.Combine(source, "game_session", "data.txt"), "exact source bytes");
        var archive = Path.Combine(owned, "observed.zip");
        JointCleanupDataCapture.Capture(source, archive, new JsonObject { ["Role"] = "AppliedBeforeStory" });
        if (fault is not ("none" or "archive-hash" or "existing"))
        {
            using var zip = ZipFile.Open(archive, ZipArchiveMode.Update);
            JsonObject manifest;
            using (var reader = new StreamReader(zip.GetEntry("manifest.json")!.Open()))
                manifest = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            if (fault == "member-hash")
            {
                zip.GetEntry("payload/game_session/data.txt")!.Delete();
                using var writer = new StreamWriter(zip.CreateEntry("payload/game_session/data.txt").Open());
                writer.Write("changed bytes");
            }
            if (fault == "traversal") manifest["Directories"]!.AsArray().Add("game_session/../escape");
            if (fault == "directory-parent") manifest["Directories"]!.AsArray().Add("game_session/unlisted/deep");
            if (fault == "absence") manifest["Absent"]!.AsArray().Add("game_session/data.txt");
            if (fault == "absent-alias") manifest["Absent"]!.AsArray().Add("game_session/data.txt.");
            if (fault == "role") manifest["Provenance"]!["Role"] = "PreparedOriginal";
            if (fault == "runtime")
            {
                const string name = ".boe_runtime/locks/live.lock";
                using (var stream = zip.CreateEntry("payload/" + name).Open()) stream.WriteByte(42);
                manifest["Files"]!.AsArray().Add(new JsonObject { ["Path"] = name, ["Length"] = 1,
                    ["Sha256"] = Convert.ToHexString(SHA256.HashData(new byte[] { 42 })) });
            }
            if (fault == "duplicate")
            {
                using var writer = new StreamWriter(zip.CreateEntry("payload/game_session/data.txt").Open());
                writer.Write("duplicate");
            }
            zip.GetEntry("manifest.json")!.Delete();
            using var metadata = new StreamWriter(zip.CreateEntry("manifest.json").Open());
            metadata.Write(manifest.ToJsonString());
        }
        var bytes = File.ReadAllBytes(archive);
        var hash = fault == "archive-hash" ? new string('0', 64) : Convert.ToHexString(SHA256.HashData(bytes));
        var target = Path.Combine(owned, "copy");
        if (fault == "existing") { Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "keep.txt"), "keep"); }
        // A trailing dot is a distinct ordinary POSIX name; Windows aliases must refuse before creating a root.
        if (fault == "none" || (fault == "absent-alias" && !OperatingSystem.IsWindows()))
        {
            var manifest = JointAppliedDataTemplate.Materialize(archive, hash, target);
            Assert.False(manifest["GrantsAuthority"]!.GetValue<bool>());
            Assert.Equal("exact source bytes", File.ReadAllText(Path.Combine(target, "game_session", "data.txt")));
            Assert.True(Directory.Exists(Path.Combine(target, "game_session", "empty")));
            File.WriteAllText(Path.Combine(target, "game_session", "data.txt"), "private mutation");
            var second = Path.Combine(owned, "second");
            JointAppliedDataTemplate.Materialize(archive, hash, second);
            Assert.Equal("exact source bytes", File.ReadAllText(Path.Combine(second, "game_session", "data.txt")));
        }
        else
        {
            if (fault == "existing") Assert.Throws<IOException>(() => JointAppliedDataTemplate.Materialize(archive, hash, target));
            else Assert.Throws<InvalidDataException>(() => JointAppliedDataTemplate.Materialize(archive, hash, target));
            if (fault == "existing") Assert.Equal("keep", File.ReadAllText(Path.Combine(target, "keep.txt")));
            else Assert.False(Directory.Exists(target));
        }
        Assert.Equal(bytes, File.ReadAllBytes(archive));
        Assert.Equal("exact source bytes", File.ReadAllText(Path.Combine(source, "game_session", "data.txt")));
    }
}
