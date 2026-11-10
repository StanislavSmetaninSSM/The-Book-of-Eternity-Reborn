using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises closed fixture ZIP transport using tiny independently authored manifests, entries and empty directories.
/// </summary>
public sealed class JointInterfaceSeedPackageTests
{
    /// <summary>
    /// Requires exact manifest and payload bytes plus explicit empty-directory preservation through fresh hydration.
    /// </summary>
    [Fact]
    public void HydratePreservesExactBytesAndExplicitEmptyDirectoriesWithoutChangingPackage()
    {
        using var fixture = new PackageFixture();
        fixture.WritePackage();
        var archiveBefore = File.ReadAllBytes(fixture.Archive);
        var bindingBefore = File.ReadAllBytes(Path.Combine(fixture.Package, "package.json"));

        // Mutation caught: an unimplemented carrier cannot preserve the empty directory lost by raw Git transport.
        JointInterfaceSeedPackage.Hydrate(fixture.Package, fixture.Destination);

        Assert.Equal(File.ReadAllBytes(Path.Combine(fixture.SourceArtifact, "manifest.json")),
            File.ReadAllBytes(Path.Combine(fixture.Destination, "manifest.json")));
        foreach (var file in JointInterfaceCrashSeedTests.SeedFixture.Files())
            Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(fixture.Destination, "payload", file.Key)));
        Assert.True(Directory.Exists(Path.Combine(fixture.Destination, "payload", "game_session", "empty")));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(fixture.Destination, "payload", "game_session", "empty")));
        Assert.Equal(JointInterfaceCrashSeedTests.SeedFixture.Metadata,
            JointInterfaceCrashSeed.Open(fixture.Destination, new string('A', 64)).Metadata);
        Assert.Equal(archiveBefore, File.ReadAllBytes(fixture.Archive));
        Assert.Equal(bindingBefore, File.ReadAllBytes(Path.Combine(fixture.Package, "package.json")));
        Assert.Equal(PackageFixture.Sentinel, File.ReadAllBytes(fixture.Outside));
    }

    /// <summary>
    /// Rejects malformed metadata, digests, entry kinds, spellings and incomplete inventory without hydrating a namespace.
    /// </summary>
    /// <param name="violation">
    /// The single independently authored closed-package violation.
    /// </param>
    [Theory]
    [InlineData("binding-extra")]
    [InlineData("binding-missing")]
    [InlineData("binding-null")]
    [InlineData("binding-version")]
    [InlineData("binding-lowercase-digest")]
    [InlineData("archive-digest")]
    [InlineData("manifest-digest")]
    [InlineData("archive-traversal")]
    [InlineData("archive-rooted")]
    [InlineData("archive-backslash")]
    [InlineData("archive-duplicate")]
    [InlineData("archive-case-alias")]
    [InlineData("archive-symlink")]
    [InlineData("archive-directory-data")]
    [InlineData("archive-missing-empty-directory")]
    [InlineData("archive-missing-file")]
    [InlineData("archive-extra-file")]
    [InlineData("archive-extra-directory")]
    [InlineData("manifest-extra-field")]
    public void MalformedPackageIsRefusedWithoutHydratingOrChangingSource(string violation)
    {
        using var fixture = new PackageFixture();
        fixture.WritePackage(violation);
        var archiveBefore = File.ReadAllBytes(fixture.Archive);
        var bindingBefore = File.ReadAllBytes(Path.Combine(fixture.Package, "package.json"));

        Assert.Throws<InvalidDataException>(() => JointInterfaceSeedPackage.Hydrate(fixture.Package, fixture.Destination));

        Assert.False(Directory.Exists(fixture.Destination));
        Assert.False(File.Exists(fixture.Destination));
        Assert.Equal(archiveBefore, File.ReadAllBytes(fixture.Archive));
        Assert.Equal(bindingBefore, File.ReadAllBytes(Path.Combine(fixture.Package, "package.json")));
        Assert.Equal(PackageFixture.Sentinel, File.ReadAllBytes(fixture.Outside));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "escaped.bin")));
    }

    /// <summary>
    /// Refuses a preexisting destination and preserves its foreign sentinel rather than treating it as owned hydration.
    /// </summary>
    [Fact]
    public void HydrateRefusesExistingDestinationWithoutChangingForeignContents()
    {
        using var fixture = new PackageFixture();
        fixture.WritePackage();
        Directory.CreateDirectory(fixture.Destination);
        var foreign = Path.Combine(fixture.Destination, "foreign.bin");
        File.WriteAllBytes(foreign, PackageFixture.Sentinel);
        Assert.Throws<InvalidDataException>(() => JointInterfaceSeedPackage.Hydrate(fixture.Package, fixture.Destination));
        Assert.Equal(PackageFixture.Sentinel, File.ReadAllBytes(foreign));
        Assert.Equal(new[] { foreign }, Directory.GetFileSystemEntries(fixture.Destination));
    }

    /// <summary>
    /// Retains the original codec's missing-directory refusal instead of silently repairing a raw artifact.
    /// </summary>
    [Fact]
    public void OriginalRawCodecStillRejectsADeclaredDirectoryMissingAfterTransport()
    {
        using var fixture = new PackageFixture();
        Directory.Delete(Path.Combine(fixture.SourceArtifact, "payload", "game_session", "empty"));
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashSeed.Open(fixture.SourceArtifact, new string('A', 64)));
        Assert.False(Directory.Exists(Path.Combine(fixture.SourceArtifact, "payload", "game_session", "empty")));
    }

    /// <summary>
    /// Owns one synthetic raw artifact and independently writes the explicit ZIP/binding transport format.
    /// </summary>
    private sealed class PackageFixture : IDisposable
    {
        private readonly JointInterfaceCrashSeedTests.SeedFixture _source = new();

        /// <summary>
        /// Gets the exact outside bytes that transport validation must preserve.
        /// </summary>
        internal static byte[] Sentinel => [0, 0xFE, 43, 0xFF];

        /// <summary>
        /// Gets this fixture's unique owned parent.
        /// </summary>
        internal string Root => _source.Root;

        /// <summary>
        /// Gets the independently authored ordinary raw artifact.
        /// </summary>
        internal string SourceArtifact => _source.Artifact;

        /// <summary>
        /// Gets the separate directory containing package.json and source42.zip.
        /// </summary>
        internal string Package => Path.Combine(Root, "package");

        /// <summary>
        /// Gets the explicitly named transport archive.
        /// </summary>
        internal string Archive => Path.Combine(Package, "source42.zip");

        /// <summary>
        /// Gets the initially absent hydration destination.
        /// </summary>
        internal string Destination => Path.Combine(Root, "hydrated-artifact");

        /// <summary>
        /// Gets the outside sentinel path within this fixture's owned parent.
        /// </summary>
        internal string Outside => Path.Combine(Root, "outside.bin");

        /// <summary>
        /// Creates only literal synthetic codec data and the outside sentinel.
        /// </summary>
        internal PackageFixture()
        {
            _source.WriteIndependentArtifact();
            File.WriteAllBytes(Outside, Sentinel);
        }

        /// <summary>
        /// Writes one package with exact directory entries and an optional single controlled corruption.
        /// </summary>
        /// <param name="violation">
        /// The corruption name, or <see langword="null"/> for a valid closed carrier.
        /// </param>
        internal void WritePackage(string? violation = null)
        {
            Directory.CreateDirectory(Package);
            var manifest = File.ReadAllBytes(Path.Combine(SourceArtifact, "manifest.json"));
            if (violation == "manifest-extra-field")
            {
                var changed = JsonNode.Parse(manifest)!.AsObject();
                changed["unrecognized"] = true;
                manifest = Encoding.UTF8.GetBytes(changed.ToJsonString());
            }
            var entries = new List<(string Name, byte[] Bytes, int Attributes)>
            {
                ("manifest.json", manifest, unchecked((int)0x80000000)),
                ("payload/", [], 0x40000010)
            };
            entries.AddRange(JointInterfaceCrashSeedTests.SeedFixture.Directories()
                .Select(directory => ("payload/" + directory + "/", Array.Empty<byte>(), 0x40000010)));
            entries.AddRange(JointInterfaceCrashSeedTests.SeedFixture.Files()
                .Select(file => ("payload/" + file.Key, file.Value, unchecked((int)0x80000000))));
            switch (violation)
            {
                case "archive-traversal": entries.Add(("../escaped.bin", Sentinel, 0)); break;
                case "archive-rooted": entries.Add(("/escaped.bin", Sentinel, 0)); break;
                case "archive-backslash": entries.Add(("payload\\escaped.bin", Sentinel, 0)); break;
                case "archive-duplicate": entries.Add(entries[0]); break;
                case "archive-case-alias": entries.Add(("MANIFEST.JSON", manifest, 0)); break;
                case "archive-symlink": entries.Add(("payload/link", Encoding.UTF8.GetBytes("../outside.bin"), unchecked((int)0xA0000000))); break;
                case "archive-directory-data": entries[1] = ("payload/", Sentinel, 0x40000010); break;
                case "archive-missing-empty-directory": entries.RemoveAll(entry => entry.Name == "payload/game_session/empty/"); break;
                case "archive-missing-file": entries.RemoveAll(entry => entry.Name == "payload/game_session/identity.json"); break;
                case "archive-extra-file": entries.Add(("payload/game_session/extra.bin", Sentinel, 0)); break;
                case "archive-extra-directory": entries.Add(("payload/game_session/extra/", [], 0x40000010)); break;
            }
            using (var output = File.Create(Archive))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
                foreach (var entry in entries)
                {
                    var created = zip.CreateEntry(entry.Name);
                    created.ExternalAttributes = entry.Attributes;
                    using var stream = created.Open();
                    stream.Write(entry.Bytes);
                }
            var binding = new JsonObject
            {
                ["SchemaVersion"] = 1,
                ["ArchiveSha256"] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Archive))),
                ["ManifestSha256"] = Convert.ToHexString(SHA256.HashData(manifest))
            };
            if (violation == "binding-extra") binding["Extra"] = true;
            if (violation == "binding-missing") binding.Remove("ManifestSha256");
            if (violation == "binding-version") binding["SchemaVersion"] = 2;
            if (violation == "binding-lowercase-digest") binding["ArchiveSha256"] = new string('a', 64);
            if (violation == "archive-digest") binding["ArchiveSha256"] = new string('0', 64);
            if (violation == "manifest-digest") binding["ManifestSha256"] = new string('0', 64);
            File.WriteAllText(Path.Combine(Package, "package.json"), violation == "binding-null" ? "null" : binding.ToJsonString());
        }

        /// <summary>
        /// Removes only this instance's fresh GUID-owned synthetic fixture root.
        /// </summary>
        public void Dispose() => _source.Dispose();
    }
}
