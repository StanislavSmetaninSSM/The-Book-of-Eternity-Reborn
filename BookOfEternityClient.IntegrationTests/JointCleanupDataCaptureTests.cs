using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises immutable terminal diagnostic export using independent small physical roots.
/// </summary>
public sealed class JointCleanupDataCaptureTests
{
    /// <summary>
    /// Preserves exact bytes, empty directories, explicit absence and detached provenance without runtime coordination.
    /// </summary>
    [Fact]
    public void Capture_ExactDataInventoryAndAbsence_ExcludesRuntimeAuthority()
    {
        using var fixture = new CaptureFixture();
        JointCleanupDataCapture.Capture(fixture.Source, fixture.Archive, new JsonObject { ["Cut"] = 0, ["Nonce"] = "detached" });
        using var archive = ZipFile.OpenRead(fixture.Archive);
        using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());
        var manifest = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        Assert.Equal("joint_cleanup_observed_data_v1", manifest["Kind"]!.GetValue<string>());
        Assert.Equal("detached", manifest["Provenance"]!["Nonce"]!.GetValue<string>());
        Assert.False(manifest["GrantsAuthority"]!.GetValue<bool>());
        Assert.Contains("game_session/empty", manifest["Directories"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.Contains("game_session/ready/turn_error.json", manifest["Absent"]!.AsArray().Select(node => node!.GetValue<string>()));
        Assert.DoesNotContain("game_session/ready/turn_complete.json", manifest["Absent"]!.AsArray().Select(node => node!.GetValue<string>()));
        var files = manifest["Files"]!.AsArray();
        Assert.Equal(4, files.Count);
        Assert.Equal(5, archive.Entries.Count);
        foreach (var row in files)
        {
            var path = row!["Path"]!.GetValue<string>();
            var source = File.ReadAllBytes(Path.Combine(fixture.Source, path));
            using var stream = archive.GetEntry("payload/" + path)!.Open();
            using var copy = new MemoryStream(); stream.CopyTo(copy);
            Assert.Equal(source, copy.ToArray());
            Assert.Equal(Convert.ToHexString(SHA256.HashData(source)), row["Sha256"]!.GetValue<string>());
            Assert.Equal(source.LongLength, row["Length"]!.GetValue<long>());
        }
        Assert.Equal(new byte[] { 0, 255, 13, 10 }, File.ReadAllBytes(Path.Combine(fixture.Source, "game_session/binary.dat")));
        Assert.True(File.Exists(Path.Combine(fixture.Source, ".boe_runtime/locks/live.lock")));
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("locks/") || entry.FullName.Contains("active.json"));
    }

    /// <summary>
    /// Refuses an existing destination and preserves its original bytes.
    /// </summary>
    [Fact]
    public void Capture_ExistingOutput_IsNotOverwritten()
    {
        using var fixture = new CaptureFixture();
        File.WriteAllText(fixture.Archive, "keep");
        Assert.Throws<IOException>(() => JointCleanupDataCapture.Capture(fixture.Source, fixture.Archive, new()));
        Assert.Equal("keep", File.ReadAllText(fixture.Archive));
    }

    /// <summary>
    /// Refuses recursive output inside the captured source namespace before creating it.
    /// </summary>
    [Fact]
    public void Capture_OutputInsideSource_IsRefused()
    {
        using var fixture = new CaptureFixture();
        var output = Path.Combine(fixture.Source, "nested.zip");
        Assert.Throws<InvalidDataException>(() => JointCleanupDataCapture.Capture(fixture.Source, output, new()));
        Assert.False(File.Exists(output));
    }

    /// <summary>
    /// Missing generation data cannot produce a seemingly complete diagnostic image.
    /// </summary>
    [Fact]
    public void Capture_MissingGeneration_IsRefusedBeforeOutput()
    {
        using var fixture = new CaptureFixture();
        File.Delete(Path.Combine(fixture.Source, ".boe_runtime/session-generation/current.json"));
        Assert.Throws<FileNotFoundException>(() => JointCleanupDataCapture.Capture(fixture.Source, fixture.Archive, new()));
        Assert.False(File.Exists(fixture.Archive));
    }

    /// <summary>
    /// Refuses a linked source member without following or copying its target.
    /// </summary>
    [Fact]
    public void Capture_LinkedSource_IsRefusedBeforeOutput()
    {
        using var fixture = new CaptureFixture();
        var target = Path.Combine(Path.GetDirectoryName(fixture.Source)!, "target.txt");
        File.WriteAllText(target, "owned target");
        var link = Path.Combine(fixture.Source, "game_session/linked.txt");
        File.CreateSymbolicLink(link, target);
        try
        {
            Assert.Throws<InvalidDataException>(() => JointCleanupDataCapture.Capture(fixture.Source, fixture.Archive, new()));
            Assert.False(File.Exists(fixture.Archive));
            Assert.Equal("owned target", File.ReadAllText(target));
        }
        finally { File.Delete(link); }
    }

    /// <summary>
    /// Reports absence according to physical file and directory visibility, including case aliases.
    /// </summary>
    /// <param name="directory">
    /// <see langword="true"/> creates a directory at the probe; otherwise creates a differently cased file.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Capture_ProbePresence_UsesPhysicalNamespace(bool directory)
    {
        using var fixture = new CaptureFixture();
        var probe = Path.Combine(fixture.Source, "game_session/ready/turn_error.json");
        if (directory) Directory.CreateDirectory(probe);
        else File.WriteAllText(Path.Combine(Path.GetDirectoryName(probe)!, "Turn_Error.json"), "observed malformed signal");
        var physicallyPresent = File.Exists(probe) || Directory.Exists(probe);
        JointCleanupDataCapture.Capture(fixture.Source, fixture.Archive, new());
        using var archive = ZipFile.OpenRead(fixture.Archive);
        using var reader = new StreamReader(archive.GetEntry("manifest.json")!.Open());
        var manifest = JsonNode.Parse(reader.ReadToEnd())!;
        var absent = manifest["Absent"]!.AsArray().Select(node => node!.GetValue<string>()).ToArray();
        if (physicallyPresent) Assert.DoesNotContain("game_session/ready/turn_error.json", absent);
        else Assert.Contains("game_session/ready/turn_error.json", absent);
    }

    /// <summary>
    /// Owns all mutable data for one test, including output, and removes only that root.
    /// </summary>
    private sealed class CaptureFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-cleanup-data-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Gets the owned observed-data source root.
        /// </summary>
        internal string Source => Path.Combine(_root, "source");

        /// <summary>
        /// Gets the new archive path outside the source root.
        /// </summary>
        internal string Archive => Path.Combine(_root, "observed.zip");

        /// <summary>
        /// Creates binary, text, directory and excluded coordination examples.
        /// </summary>
        internal CaptureFixture()
        {
            Directory.CreateDirectory(Path.Combine(Source, "game_session/empty"));
            Directory.CreateDirectory(Path.Combine(Source, "game_session/ready"));
            Directory.CreateDirectory(Path.Combine(Source, ".boe_runtime/session-generation"));
            Directory.CreateDirectory(Path.Combine(Source, ".boe_runtime/locks"));
            Directory.CreateDirectory(Path.Combine(Source, ".boe_runtime/trusted-local-publication-v1"));
            File.WriteAllBytes(Path.Combine(Source, "game_session/binary.dat"), [0, 255, 13, 10]);
            File.WriteAllText(Path.Combine(Source, "game_session/ready/turn_complete.json"), "{}");
            File.WriteAllBytes(Path.Combine(Source, "game_session/empty.dat"), []);
            File.WriteAllText(Path.Combine(Source, ".boe_runtime/session-generation/current.json"), "{\"data\":true}");
            File.WriteAllText(Path.Combine(Source, ".boe_runtime/locks/live.lock"), "never copy");
            File.WriteAllText(Path.Combine(Source, ".boe_runtime/trusted-local-publication-v1/active.json"), "never copy");
        }

        /// <inheritdoc/>
        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}
