using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Source-derived private-stage boundaries, not authentic old-producer captures.</summary>
public sealed class DarenRetirementPreIntentTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CompleteRetiredIntent_RefusesBeforePrivateScratchDiscard(int format)
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = await SeedAsync(root);
        var intent = SeedIntent(files, format, retired: true, corruptImage: false);
        await InspectAsync(root, intent, expectRetained: true);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public async Task OrdinaryOrTruncatedPrivateIntent_KeepsScratchDiscardPolicy(int format, bool truncated)
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = await SeedAsync(root);
        var intent = SeedIntent(files, format, retired: false, corruptImage: false);
        if (truncated)
        {
            var bytes = File.ReadAllBytes(intent);
            File.WriteAllBytes(intent, bytes[..(bytes.Length / 2)]);
            output.WriteLine("PrivateScratchMutation=TRUNCATED_ORDINARY_FRAME; SHA256=" +
                DarenRetirementEvidenceFixtures.Hash(File.ReadAllBytes(intent)));
        }
        await InspectAsync(root, intent, expectRetained: false);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RetiredPathWithInvalidLaterImage_IsMalformedScratchNotRecognizedEvidence(int format)
    {
        var root = NewRoot();
        using var cleanup = new CleanupOwnedFixture(root, output.WriteLine);
        var files = await SeedAsync(root);
        var intent = SeedIntent(files, format, retired: true, corruptImage: true);
        await InspectAsync(root, intent, expectRetained: false);
    }

    private static async Task<FileSystemManager> SeedAsync(string root)
    {
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        DarenRetirementEvidenceFixtures.Put(root, DarenRetirementEvidenceFixtures.Profile, DarenRetirementEvidenceFixtures.After);
        await files.WriteFileAtomicBytesAsync(DarenRetirementEvidenceFixtures.Member, DarenRetirementEvidenceFixtures.After);
        Assert.Equal(DarenRetirementEvidenceFixtures.After, File.ReadAllBytes(Path.Combine(root, DarenRetirementEvidenceFixtures.Profile)));
        Assert.Equal(DarenRetirementEvidenceFixtures.After, File.ReadAllBytes(files.ResolvePath(DarenRetirementEvidenceFixtures.Member)));
        return files;
    }

    private string SeedIntent(FileSystemManager files, int format, bool retired, bool corruptImage)
    {
        var generation = DarenRetirementEvidenceFixtures.ReadCurrentGeneration(File.ReadAllBytes(files.SessionGenerationPath));
        JsonObject Image(byte[] bytes) => new()
        {
            ["Exists"] = true, ["Bytes"] = Convert.ToBase64String(bytes),
            ["Sha256"] = DarenRetirementEvidenceFixtures.Hash(bytes)
        };
        JsonObject Member(string path) => new()
        {
            ["Path"] = path, ["Before"] = Image(DarenRetirementEvidenceFixtures.Before),
            ["After"] = Image(DarenRetirementEvidenceFixtures.After)
        };
        var members = new JsonArray(Member(retired ? Path.Combine(files.BasePath, DarenRetirementEvidenceFixtures.Profile) :
            files.ResolvePath(DarenRetirementEvidenceFixtures.Member)));
        var document = new JsonObject
        {
            ["Format"] = 1, ["TransactionId"] = Guid.NewGuid().ToString("N"), ["Committed"] = false,
            ["GenerationBefore"] = new JsonObject { ["Exists"] = true, ["Id"] = generation },
            ["GenerationAfter"] = new JsonObject { ["Exists"] = true, ["Id"] = generation }, ["Members"] = members
        };
        Assert.Equal(generation, document["GenerationBefore"]!["Id"]!.GetValue<string>());
        Assert.Equal(generation, document["GenerationAfter"]!["Id"]!.GetValue<string>());
        Assert.Equal(DarenRetirementEvidenceFixtures.Before,
            Convert.FromBase64String(members[0]!["Before"]!["Bytes"]!.GetValue<string>()));
        Assert.Equal(DarenRetirementEvidenceFixtures.After,
            Convert.FromBase64String(members[0]!["After"]!["Bytes"]!.GetValue<string>()));
        if (corruptImage) members[0]!["After"]!["Sha256"] = new string('0', 64);
        var bytes = format == 1 ? JsonSerializer.SerializeToUtf8Bytes(document) : EncodeSourceDerivedFrame(document);
        var directory = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1");
        Directory.CreateDirectory(directory);
        Assert.Empty(Directory.EnumerateFileSystemEntries(directory));
        var intent = Path.Combine(directory, "intent.tmp");
        File.WriteAllBytes(intent, bytes);
        Assert.Equal(bytes, File.ReadAllBytes(intent));
        Assert.Single(Directory.EnumerateFileSystemEntries(directory));
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Provenance = "SOURCE_DERIVED_FULL_FORMAT" + format + "_INTENT_NOT_AUTHENTIC_CAPTURE",
            FormatSource = "TrustedLocalFilePublication Journal/Member/FrameMetadata; exact current owned root and generation, opaque images",
            Generation = generation, Retired = retired, InvalidLaterImageHash = corruptImage,
            Intent = intent, Bytes = bytes.Length, Sha256 = DarenRetirementEvidenceFixtures.Hash(bytes)
        }));
        return intent;
    }

    // Independent wire construction follows the existing v2 field/offset contract;
    // no production writer or retired profile producer is reintroduced by this fixture.
    private static byte[] EncodeSourceDerivedFrame(JsonObject document)
    {
        var metadata = document.DeepClone().AsObject();
        metadata["Format"] = 2;
        using var payload = new MemoryStream();
        foreach (var member in metadata["Members"]!.AsArray())
        foreach (var name in new[] { "Before", "After" })
        {
            var image = member![name]!;
            var bytes = Convert.FromBase64String(image["Bytes"]!.GetValue<string>());
            member[name] = new JsonObject
            {
                ["Exists"] = true, ["Length"] = bytes.Length,
                ["Sha256"] = image["Sha256"]!.GetValue<string>(), ["Offset"] = payload.Position
            };
            payload.Write(bytes);
        }
        var header = JsonSerializer.SerializeToUtf8Bytes(metadata);
        using var frame = new MemoryStream();
        frame.Write(Encoding.ASCII.GetBytes("BOELP2\r\n"));
        Span<byte> length = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(length, header.Length);
        frame.Write(length);
        frame.Write(header);
        frame.Write(payload.ToArray());
        var result = frame.ToArray();
        Assert.Equal(header.Length, BinaryPrimitives.ReadInt64LittleEndian(result.AsSpan(8, 8)));
        Assert.Equal(header, result.AsSpan(16, header.Length).ToArray());
        Assert.Equal(payload.ToArray(), result.AsSpan(16 + header.Length).ToArray());
        return result;
    }

    private async Task InspectAsync(string root, string intent, bool expectRetained)
    {
        var before = DarenRetirementEvidenceFixtures.Snapshot(root);
        var expected = before.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        if (!expectRetained) Assert.True(expected.Remove(Path.GetRelativePath(root, intent)));
        var callbacks = new List<string>();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path => { callbacks.Add("mutation:" + path); return Task.CompletedTask; },
                LocalPublicationRecoveryObserver = (phase, index) => callbacks.Add("recovery:" + phase + ":" + index)
            });
        Exception? failure = null;
        try { await using var lease = await files.AcquireCanonicalWriteLeaseAsync(); }
        catch (Exception error) { failure = error; }
        var after = DarenRetirementEvidenceFixtures.Snapshot(root);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            PrivateIntentConsumer = root, ExpectRetained = expectRetained, Failure = failure?.ToString(), Callbacks = callbacks,
            Before = before.ToDictionary(pair => pair.Key, pair => DarenRetirementEvidenceFixtures.Hash(pair.Value)),
            After = after.ToDictionary(pair => pair.Key, pair => DarenRetirementEvidenceFixtures.Hash(pair.Value))
        }));
        if (expectRetained)
        {
            Assert.IsType<InvalidDataException>(failure);
            Assert.Contains("Retired Daren", failure.Message, StringComparison.Ordinal);
        }
        else Assert.Null(failure);
        Assert.Empty(callbacks);
        DarenRetirementEvidenceFixtures.AssertSnapshot(root, expected);
        Assert.Equal(expectRetained, File.Exists(intent));
    }

    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "boe-daren-retirement-storage-" + Guid.NewGuid().ToString("N"));
}
