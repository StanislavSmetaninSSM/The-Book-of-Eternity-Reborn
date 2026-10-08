using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Consumes immutable, independently source-derived v1 evidence through normal lease acquisition.</summary>
public sealed class TrustedLocalOriginalV1FixtureTests(ITestOutputHelper output)
{
    private const string Placeholder = "/__boe_v1_fixture__/root";
    private const string BeforeId = "11111111111111111111111111111111";
    private const string AfterId = "22222222222222222222222222222222";
    private const string TransactionId = "33333333333333333333333333333333";
    private const string FixtureDirectory = "specs/1553-portable-local-storage/recovery/fixtures/original-v1-source-derived";
    private sealed record ExpectedMember(string RelativePath, byte[]? Before, byte[]? After);

    [Theory]
    [InlineData(false, "pending.json", 2066, "13fb4c68e26b87107501478a4f302a17e16fa5f25019de3bcac14d528e7167a1")]
    [InlineData(true, "committed.json", 2065, "da0c9fd29ed3d23d8903d42b88691d4d83bc156c1142061595dbdc2bd602ba02")]
    public async Task NormalLeaseRecoversIndependentOriginalV1Decision(
        bool committed, string fixtureName, int fixtureLength, string fixtureSha256)
    {
        // The oracle is explicit known data, never the current production Header/image helper.
        ExpectedMember[] expected =
        [
            new("replace.bin", [0xEF, 0xBB, 0xBF, 0x7B, 0x7D], [0xFF, 0, 0xFE, 0x11]),
            new("created-empty.bin", null, []),
            new("deleted.bin", [1, 2, 3], null),
            new(".boe_runtime/session-generation/current.json",
                [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(
                    "{\"schemaVersion\":1,\"generationId\":\"" + BeforeId + "\",\"extension\":true}")],
                [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(
                    "{\"SchemaVersion\":1,\"GenerationId\":\"" + AfterId + "\"}")])
        ];
        var sourcePath = Path.Combine(TestRepoPaths.RepoRoot, FixtureDirectory, fixtureName);
        var immutableBytes = File.ReadAllBytes(sourcePath);
        Assert.Equal(fixtureLength, immutableBytes.Length);
        Assert.Equal(fixtureSha256, Hash(immutableBytes).ToLowerInvariant());
        var journal = JsonNode.Parse(immutableBytes)!.AsObject();
        Assert.Equal(1, journal["Format"]!.GetValue<int>());
        Assert.Equal(TransactionId, journal["TransactionId"]!.GetValue<string>());
        Assert.Equal(committed, journal["Committed"]!.GetValue<bool>());
        Assert.True(journal["GenerationBefore"]!["Exists"]!.GetValue<bool>());
        Assert.True(journal["GenerationAfter"]!["Exists"]!.GetValue<bool>());
        Assert.Equal(BeforeId, journal["GenerationBefore"]!["Id"]!.GetValue<string>());
        Assert.Equal(AfterId, journal["GenerationAfter"]!["Id"]!.GetValue<string>());
        var members = journal["Members"]!.AsArray();
        Assert.Equal(4, members.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(Placeholder + "/" + expected[i].RelativePath, members[i]!["Path"]!.GetValue<string>());
            AssertFixtureImage(members[i]!["Before"]!, expected[i].Before);
            AssertFixtureImage(members[i]!["After"]!, expected[i].After);
        }

        var sandbox = Path.Combine(Path.GetTempPath(), "boe-original-v1-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(sandbox, "root");
        var insideSentinel = Path.Combine(root, "unrelated-sentinel.bin");
        var outsideSentinel = Path.Combine(sandbox, "outside-sentinel.bin");
        byte[] sentinelBytes = [0x80, 0, 0xEF, 0xBB, 0xBF, 0x27];
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllBytes(insideSentinel, sentinelBytes);
            File.WriteAllBytes(outsideSentinel, sentinelBytes);
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            string MemberPath(ExpectedMember member) => Path.Combine(root, member.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            for (var i = 0; i < expected.Length; i++)
            {
                var path = MemberPath(expected[i]);
                // Only Members.Path is rebound. Payloads, hashes, identifiers and order stay immutable.
                members[i]!["Path"] = path;
                if (expected[i].After is { } bytes)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllBytes(path, bytes);
                }
                AssertImage(path, expected[i].After);
            }
            var materializedBytes = Encoding.UTF8.GetBytes(journal.ToJsonString());
            var rebound = JsonNode.Parse(materializedBytes)!;
            for (var i = 0; i < expected.Length; i++)
                rebound["Members"]![i]!["Path"] = Placeholder + "/" + expected[i].RelativePath;
            Assert.Equal(JsonNode.Parse(immutableBytes)!.ToJsonString(), rebound.ToJsonString());

            var journalRoot = Path.Combine(root, ".boe_runtime", "trusted-local-publication-v1");
            var active = Path.Combine(journalRoot, "active.json");
            Directory.CreateDirectory(journalRoot);
            File.WriteAllBytes(active, materializedBytes);
            Assert.Equal(materializedBytes, File.ReadAllBytes(active));
            Assert.Single(Directory.EnumerateFileSystemEntries(journalRoot));
            Assert.Empty(Directory.EnumerateFiles(root, ".boe-local-*", SearchOption.AllDirectories));
            output.WriteLine("Fixture {0}: immutable SHA256 {1}; materialized SHA256 {2}; four After members seeded.",
                fixtureName, fixtureSha256, Hash(materializedBytes));

            // Normal production entrypoint performs recovery in this same process. No hook or direct Recover call.
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.Equal(committed ? AfterId : BeforeId, files.ReadExistingSessionGeneration(lease));
                foreach (var member in expected)
                    AssertImage(MemberPath(member), committed ? member.After : member.Before);
                Assert.False(File.Exists(active));
                if (Directory.Exists(journalRoot)) Assert.Empty(Directory.EnumerateFileSystemEntries(journalRoot));
                Assert.Empty(Directory.EnumerateFiles(root, ".boe-local-*", SearchOption.AllDirectories));
                Assert.Equal(sentinelBytes, File.ReadAllBytes(insideSentinel));
                Assert.Equal(sentinelBytes, File.ReadAllBytes(outsideSentinel));
                var expectedFiles = expected.Where(member => (committed ? member.After : member.Before) != null)
                    .Select(MemberPath).Append(insideSentinel).Append(files.CanonicalWriteLockPath).OrderBy(path => path).ToArray();
                Assert.Equal(expectedFiles, Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path).ToArray());
            }
            Assert.Equal(immutableBytes, File.ReadAllBytes(sourcePath));
            output.WriteLine("Normal lease recovered {0}; exact four-member decision, generation, two sentinels and journal/sibling cleanup verified.",
                committed ? "committed After" : "pending Before");
        }
        finally
        {
            if (Directory.Exists(sandbox)) Directory.Delete(sandbox, recursive: true);
            Assert.False(Directory.Exists(sandbox));
            output.WriteLine("Owned fixture cleanup verified: zero sandbox remainders.");
        }
    }

    private static void AssertFixtureImage(JsonNode image, byte[]? expected)
    {
        Assert.Equal(expected != null, image["Exists"]!.GetValue<bool>());
        if (expected == null)
        {
            Assert.Null(image["Bytes"]);
            Assert.Null(image["Sha256"]);
        }
        else
        {
            Assert.Equal(expected, Convert.FromBase64String(image["Bytes"]!.GetValue<string>()));
            Assert.Equal(Hash(expected), image["Sha256"]!.GetValue<string>());
        }
    }

    private static void AssertImage(string path, byte[]? expected)
    {
        Assert.False(Directory.Exists(path));
        Assert.Equal(expected != null, File.Exists(path));
        if (expected != null) Assert.Equal(expected, File.ReadAllBytes(path));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
