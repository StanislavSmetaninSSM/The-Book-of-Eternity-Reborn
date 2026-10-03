using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises v2 metadata through publication and normal recovery, using independently encoded frames.
/// </summary>
public sealed class TrustedLocalFrameMetadataTests : IDisposable
{
    private const string Generation = "11111111111111111111111111111111";
    private const string Transaction = "22222222222222222222222222222222";
    private const int LargeMemberCount = 4096;
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-frame-metadata-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Before = [0xEF, 0xBB, 0xBF, 0xFF, 0];
    private static readonly byte[] After = [0xFE, 42, 0];
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BOELP2\r\n");
    private string Target => Path.Combine(_root, "game_session", "game_state", "core", "metadata-target.bin");
    private string Active => Path.Combine(_root, ".boe_runtime", "trusted-local-publication-v1", "active.json");
    private FileSystemManager Manager() => new(_root, NullLogger<FileSystemManager>.Instance);
    private sealed class IntentCut : Exception { }

    /// <summary>
    /// Creates an isolated current-generation session for every test instance.
    /// </summary>
    public TrustedLocalFrameMetadataTests()
    {
        var files = Manager();
        files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new
        { schemaVersion = 1, generationId = Generation }));
        File.WriteAllBytes(Target, Before);
    }

    /// <summary>
    /// Requires the real v2 producer to finish a header larger than the former aggregate budget.
    /// </summary>
    /// <returns>
    /// A task that completes after the intent cut and exact rollback assertions.
    /// </returns>
    [Fact]
    public async Task LargeMetadataWriterReachesCompleteIntentWithoutAggregateCap()
    {
        // Mutation caught: retaining the 1 MiB writer cap prevents the intent callback.
        var files = Manager();
        var scope = new TrustedLocalFileScope([_root]);
        var changes = new List<TrustedLocalImageChange>
        {
            new(Target, TrustedLocalFileImage.CaptureFile(scope, Target), TrustedLocalFileImage.FromBytes(After))
        };
        for (var index = 1; index < LargeMemberCount; index++)
            changes.Add(new(AbsentPath(index), TrustedLocalFileImage.FromBytes(null), TrustedLocalFileImage.FromBytes(null)));
        var reached = false;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var outcome = new TrustedLocalFilePublication(files, scope).PublishImagesWithOutcome(lease,
            TrustedLocalGeneration.Existing(Generation), changes, (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.IntentStaged) return;
                reached = true;
                using var input = File.OpenRead(Path.Combine(Path.GetDirectoryName(Active)!, "intent.tmp"));
                Span<byte> prefix = stackalloc byte[16];
                input.ReadExactly(prefix);
                Assert.Equal(Magic, prefix[..8].ToArray());
                var length = BinaryPrimitives.ReadInt64LittleEndian(prefix[8..]);
                Assert.True(length > 1024 * 1024, "The fixture must cross the original aggregate cap.");
                Assert.Equal(16 + length + Before.Length + After.Length, input.Length);
                var metadata = new byte[checked((int)length)];
                input.ReadExactly(metadata);
                using var document = JsonDocument.Parse(metadata);
                Assert.Equal(LargeMemberCount, document.RootElement.GetProperty("Members").GetArrayLength());
                throw new IntentCut();
            });
        Assert.True(reached, outcome.Failure?.ToString());
        Assert.Equal(TrustedLocalPublicationDisposition.RolledBack, outcome.Disposition);
        Assert.IsType<IntentCut>(outcome.Failure);
        Assert.Equal(Before, File.ReadAllBytes(Target));
        Assert.False(File.Exists(Active));
    }

    /// <summary>
    /// Requires every member to reach the real output with no pending encoding and no aggregate retention.
    /// </summary>
    /// <returns>
    /// A task that completes after comparing two real intents with the same largest member encoding.
    /// </returns>
    [Fact]
    public async Task WriterFlushesEachMemberWithoutWholeHeaderRetention()
    {
        // Mutation caught: delaying Flush until the array closes retains all encoded members.
        var files = Manager();
        var scope = new TrustedLocalFileScope([_root]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var smaller = ObserveWriterIntent(files, scope, lease, 100);
        var larger = ObserveWriterIntent(files, scope, lease, 1000);
        Assert.Equal(smaller.Max(value => value.BytesPendingBeforeFlush),
            larger.Max(value => value.BytesPendingBeforeFlush));
        Assert.True(larger[^1].DestinationPosition > smaller[^1].DestinationPosition);
        Assert.Equal(Before, File.ReadAllBytes(Target));
    }

    /// <summary>
    /// Proves actual oversized-token growth and release separately from the existing path authority refusal.
    /// </summary>
    /// <returns>
    /// A task that completes after transport observations and preserved recovery evidence are checked.
    /// </returns>
    [Fact]
    public async Task LongEscapedTokenReleasesCarryBeforeLaterPathRefusal()
    {
        // This token is valid JSON transport, deliberately relative and therefore not a supported authority path.
        // Mutation caught: allocating the whole header or keeping oversized carry after the long token completes.
        var files = Manager();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var header = Header(false);
        header["Members"]![1]!["Path"] = new string('\u0416', 12000);
        var json = header.ToJsonString();
        Assert.Contains("\\u0416", json, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new string('\u0416', 12000))) > 65536);
        var evidence = Frame(json);
        PutActive(evidence);
        File.WriteAllBytes(Target, After);
        var observations = new List<TrustedLocalFrameMetadataObservation>();
        var publication = new TrustedLocalFilePublication(files, new TrustedLocalFileScope([_root]), observations.Add);
        Assert.Throws<InvalidDataException>(() => publication.Recover(lease));

        Assert.Contains(observations, value => value.Kind == TrustedLocalFrameMetadataObservationKind.ReaderBufferGrown &&
            value.BufferCapacity > 65536 && value.EncodedTokenBytes > 0);
        Assert.All(observations.Where(value => value.Kind == TrustedLocalFrameMetadataObservationKind.ReaderBufferGrown), value =>
        {
            Assert.True(value.EncodedTokenBytes >= 65536);
            Assert.InRange(value.BufferCapacity, value.EncodedTokenBytes + 1, value.EncodedTokenBytes * 2);
        });
        var completed = observations.FindIndex(value => value.Kind == TrustedLocalFrameMetadataObservationKind.ReaderTokenCompleted &&
            value.EncodedTokenBytes > 65536);
        Assert.True(completed >= 0, "The actual oversized escaped token must be decoded before path admission refuses it.");
        var released = observations.FindIndex(completed + 1, value =>
            value.Kind == TrustedLocalFrameMetadataObservationKind.ReaderBufferReleased && value.BufferCapacity == 65536);
        Assert.True(released > completed, "Oversized carry must be released after decoding its token.");
        Assert.Contains(observations.Skip(released + 1), value =>
            value.Kind == TrustedLocalFrameMetadataObservationKind.ReaderTokenCompleted &&
            value.BufferCapacity == 65536 && value.EncodedTokenBytes < 65536);
        Assert.Equal(After, File.ReadAllBytes(Target));
        Assert.Equal(evidence, File.ReadAllBytes(Active));
        using var generation = JsonDocument.Parse(File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Equal(Generation, generation.RootElement.GetProperty("generationId").GetString());
    }

    /// <summary>
    /// Recovers an independently encoded large metadata frame without imposing a total header limit.
    /// </summary>
    /// <param name="committed">
    /// Selects committed after-state cleanup or pending before-state rollback.
    /// </param>
    /// <returns>
    /// A task that completes after normal lease recovery and exact decision assertions.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LargeMetadataRoundTripsWithoutAggregateCap(bool committed)
    {
        // Mutation caught: retaining the reader cap rejects physically complete supported metadata.
        var header = Header(committed);
        var members = header["Members"]!.AsArray();
        for (var index = 2; index < LargeMemberCount; index++)
            members.Add(new JsonObject { ["Path"] = AbsentPath(index), ["Before"] = AbsentImage(), ["After"] = AbsentImage() });
        var json = header.ToJsonString();
        Assert.True(Encoding.UTF8.GetByteCount(json) > 1024 * 1024);
        PutActive(Frame(json));
        File.WriteAllBytes(Target, After);
        File.WriteAllBytes(AbsentPath(1), []);
        var files = Manager();
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(Generation, files.ReadExistingSessionGeneration(lease));
        Assert.Equal(committed ? After : Before, File.ReadAllBytes(Target));
        Assert.Equal(committed, File.Exists(AbsentPath(1)));
        Assert.False(File.Exists(Active));
        Assert.Empty(Directory.EnumerateFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
    }

    /// <summary>
    /// Accepts decoded names and arbitrary property order while carrying split tokens across the read boundary.
    /// </summary>
    /// <param name="padding">
    /// Inserts JSON whitespace so the escaped member token crosses the 64 KiB input boundary.
    /// </param>
    /// <returns>
    /// A task that completes after recovery has restored the exact before image.
    /// </returns>
    [Theory]
    [InlineData(0)]
    [InlineData(65470)]
    public async Task ReorderedAndEscapedFieldsKeepExistingMeaning(int padding)
    {
        var path = JsonSerializer.Serialize(Target);
        var json = "{\"Members\":[" + new string(' ', padding) +
            "{\"After\":{\"Offset\":5,\"Sha256\":" + JsonSerializer.Serialize(Hash(After)) +
            ",\"Length\":3,\"Exists\":true},\"Pa\\u0074h\":" + path +
            ",\"Before\":{\"Offset\":0,\"Length\":5,\"Exists\":true,\"Sha256\":" +
            JsonSerializer.Serialize(Hash(Before)) + "}}],\"GenerationAfter\":{\"Id\":\"" + Generation +
            "\",\"Exists\":true},\"Committed\":false,\"GenerationBefore\":{\"Id\":\"" + Generation +
            "\",\"Exists\":true},\"TransactionId\":\"" + Transaction + "\",\"For\\u006dat\":2}";
        PutActive(Frame(json));
        File.WriteAllBytes(Target, After);
        await using (var lease = await Manager().AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(Before, File.ReadAllBytes(Target));
        Assert.False(File.Exists(Active));
    }

    /// <summary>
    /// Keeps the original independently encoded small-frame pending and committed decisions.
    /// </summary>
    /// <param name="committed">
    /// Selects the established before or after decision.
    /// </param>
    /// <returns>
    /// A task that completes after checking the original small-frame recovery decision.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentSmallV2FrameKeepsExactDecision(bool committed)
    {
        var bytes = Frame(Header(committed).ToJsonString());
        Assert.InRange(bytes.Length, 17, 1024 * 1024);
        PutActive(bytes);
        File.WriteAllBytes(Target, After);
        File.WriteAllBytes(AbsentPath(1), []);
        await using (var lease = await Manager().AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(committed ? After : Before, File.ReadAllBytes(Target));
        Assert.Equal(committed, File.Exists(AbsentPath(1)));
        if (committed) Assert.Empty(File.ReadAllBytes(AbsentPath(1)));
        Assert.False(File.Exists(Active));
    }

    /// <summary>
    /// Rejects late schema or region damage before restoring an earlier valid member.
    /// </summary>
    /// <param name="corruption">
    /// Identifies the independently applied invalid metadata or payload mutation.
    /// </param>
    /// <returns>
    /// A task that completes after refusal, preserved earlier bytes and retained evidence are checked.
    /// </returns>
    [Theory]
    [InlineData("root-duplicate-escaped")]
    [InlineData("member-duplicate-escaped")]
    [InlineData("image-duplicate-escaped")]
    [InlineData("generation-duplicate-escaped")]
    [InlineData("root-unknown")]
    [InlineData("member-unknown")]
    [InlineData("image-unknown")]
    [InlineData("generation-unknown")]
    [InlineData("member-missing-path")]
    [InlineData("member-null-before")]
    [InlineData("image-missing-offset")]
    [InlineData("image-wrong-length-type")]
    [InlineData("generation-missing-id")]
    [InlineData("generation-null")]
    [InlineData("root-wrong-committed-type")]
    [InlineData("root-null-members")]
    [InlineData("extra-root")]
    [InlineData("token-truncated")]
    [InlineData("declared-length-overflow")]
    [InlineData("metadata-crosses-payload")]
    [InlineData("negative-region")]
    [InlineData("region-overflow")]
    [InlineData("region-gap")]
    [InlineData("region-overlap")]
    [InlineData("trailing-region")]
    [InlineData("late-hash")]
    public async Task StrictMetadataRejectsLateInvalidMemberBeforeRecoveryMutation(string corruption)
    {
        var header = Header(false);
        var late = header["Members"]![1]!.AsObject();
        var image = late["After"]!.AsObject();
        switch (corruption)
        {
            case "root-unknown": header["Unexpected"] = true; break;
            case "member-unknown": late["Unexpected"] = true; break;
            case "image-unknown": image["Unexpected"] = true; break;
            case "generation-unknown": header["GenerationAfter"]!["Unexpected"] = true; break;
            case "member-missing-path": late.Remove("Path"); break;
            case "member-null-before": late["Before"] = null; break;
            case "image-missing-offset": image.Remove("Offset"); break;
            case "image-wrong-length-type": image["Length"] = "0"; break;
            case "generation-missing-id": header["GenerationAfter"]!.AsObject().Remove("Id"); break;
            case "generation-null": header["GenerationAfter"] = null; break;
            case "root-wrong-committed-type": header["Committed"] = "false"; break;
            case "root-null-members": header["Members"] = null; break;
            case "negative-region": image["Length"] = -1; break;
            case "region-overflow": image["Length"] = long.MaxValue; break;
            case "region-gap": image["Offset"] = 9; break;
            case "region-overlap": image["Offset"] = 7; break;
            case "late-hash": image["Sha256"] = new string('0', 64); break;
        }
        var json = header.ToJsonString();
        json = corruption switch
        {
            "root-duplicate-escaped" => json.Insert(json.Length - 1, ",\"For\\u006dat\":2"),
            "member-duplicate-escaped" => json.Replace(JsonSerializer.Serialize(AbsentPath(1)), JsonSerializer.Serialize(AbsentPath(1)) + ",\"Pa\\u0074h\":" + JsonSerializer.Serialize(AbsentPath(1)), StringComparison.Ordinal),
            "image-duplicate-escaped" => json.Replace("\"Offset\":8", "\"Offset\":8,\"Off\\u0073et\":8", StringComparison.Ordinal),
            "generation-duplicate-escaped" => json.Replace("\"Id\":\"" + Generation + "\"", "\"Id\":\"" + Generation + "\",\"I\\u0064\":\"" + Generation + "\"", StringComparison.Ordinal),
            "extra-root" => json + " {}",
            "token-truncated" => json[..^3],
            _ => json
        };
        var bytes = Frame(json);
        if (corruption == "declared-length-overflow") BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8, 8), long.MaxValue);
        if (corruption == "metadata-crosses-payload") BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8, 8), Encoding.UTF8.GetByteCount(json) - 1);
        if (corruption == "trailing-region") bytes = [.. bytes, 99];
        PutActive(bytes);
        File.WriteAllBytes(Target, After);
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await Manager().AcquireCanonicalWriteLeaseAsync(); });
        Assert.Equal(After, File.ReadAllBytes(Target));
        Assert.False(File.Exists(AbsentPath(1)));
        Assert.Equal(bytes, File.ReadAllBytes(Active));
        using var generation = JsonDocument.Parse(File.ReadAllBytes(Manager().SessionGenerationPath));
        Assert.Equal(Generation, generation.RootElement.GetProperty("generationId").GetString());
    }

    /// <summary>
    /// Requires every root, generation, member and image field while enforcing its exact value type and null contract.
    /// </summary>
    /// <returns>
    /// A task that completes after every isolated mutation has retained all earlier bytes and authority evidence.
    /// </returns>
    [Fact]
    public async Task EveryMetadataFieldRejectsOmissionWrongTypeAndForbiddenNull()
    {
        var fields = new (string Shape, string Field)[]
        {
            ("root", "Format"), ("root", "TransactionId"), ("root", "Committed"),
            ("root", "GenerationBefore"), ("root", "GenerationAfter"), ("root", "Members"),
            ("generation-before", "Exists"), ("generation-before", "Id"),
            ("generation-after", "Exists"), ("generation-after", "Id"),
            ("member", "Path"), ("member", "Before"), ("member", "After"),
            ("absent-image", "Exists"), ("absent-image", "Length"),
            ("absent-image", "Sha256"), ("absent-image", "Offset"),
            ("present-image", "Exists"), ("present-image", "Length"),
            ("present-image", "Sha256"), ("present-image", "Offset")
        };
        var files = Manager();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var publication = new TrustedLocalFilePublication(files, new TrustedLocalFileScope([_root]));
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        foreach (var (shape, field) in fields)
        foreach (var mutation in new[] { "missing", "wrong-type", "null" })
        {
            if (mutation == "null" && shape == "absent-image" && (field is "Sha256" or "Offset")) continue;
            var header = Header(false);
            var late = header["Members"]![1]!.AsObject();
            var target = shape switch
            {
                "root" => header,
                "generation-before" => header["GenerationBefore"]!.AsObject(),
                "generation-after" => header["GenerationAfter"]!.AsObject(),
                "member" => late,
                "absent-image" => late["Before"]!.AsObject(),
                "present-image" => late["After"]!.AsObject(),
                _ => throw new InvalidOperationException("Unknown independent fixture shape.")
            };
            if (mutation == "missing") target.Remove(field);
            else if (mutation == "null") target[field] = null;
            else target[field] = field switch
            {
                "Format" or "Length" or "Offset" or "Exists" or "Committed" => JsonValue.Create("wrong-type"),
                "TransactionId" or "Path" or "Id" or "Sha256" => JsonValue.Create(0),
                "Members" => new JsonObject(),
                _ => new JsonArray()
            };
            var evidence = Frame(header.ToJsonString());
            File.WriteAllBytes(Target, After);
            PutActive(evidence);
            var failure = Record.Exception(() => publication.Recover(lease));
            Assert.True(failure is InvalidDataException, $"{shape}.{field}/{mutation}: {failure}");
            Assert.Equal(After, File.ReadAllBytes(Target));
            Assert.False(File.Exists(AbsentPath(1)));
            Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
            Assert.Equal(evidence, File.ReadAllBytes(Active));
        }
    }

    /// <summary>
    /// Decodes actual multi-byte UTF-8 split across the base I/O boundary without changing exact path meaning.
    /// </summary>
    /// <param name="splitText">
    /// The two-byte or four-byte Unicode sequence whose first encoded byte ends the first buffer.
    /// </param>
    /// <returns>
    /// A task that completes after normal recovery restores the exact Unicode-named member.
    /// </returns>
    [Theory]
    [InlineData("Ж")]
    [InlineData("🐾")]
    public async Task SplitUtf8PathTokenKeepsExactMemberMeaning(string splitText)
    {
        var unicodePath = Path.Combine(Path.GetDirectoryName(Target)!, "Ж🐾.bin");
        File.WriteAllBytes(unicodePath, After);
        var header = Header(false);
        header["Members"]![0]!["Path"] = unicodePath;
        var json = header.ToJsonString(new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        json = json.Replace("\\u0416", "Ж", StringComparison.OrdinalIgnoreCase)
            .Replace("\\uD83D\\uDC3E", "🐾", StringComparison.OrdinalIgnoreCase);
        var splitAt = json.IndexOf(splitText, StringComparison.Ordinal);
        Assert.True(splitAt > 0);
        var padding = 65535 - Encoding.UTF8.GetByteCount(json.AsSpan(0, splitAt));
        Assert.True(padding > 0);
        json = json.Insert(1, new string(' ', padding));
        Assert.Equal(65535, Encoding.UTF8.GetByteCount(json.AsSpan(0, json.IndexOf(splitText, StringComparison.Ordinal))));
        PutActive(Frame(json));
        await using (var lease = await Manager().AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(Before, File.ReadAllBytes(unicodePath));
        Assert.Equal(Before, File.ReadAllBytes(Target));
        Assert.False(File.Exists(Active));
    }

    /// <summary>
    /// Refuses malformed JSON transport and unsupported numeric spellings before any recovery mutation.
    /// </summary>
    /// <returns>
    /// A task that completes after each independently corrupted frame preserves earlier bytes and exact evidence.
    /// </returns>
    [Fact]
    public async Task InvalidMetadataSyntaxAndNumericTokensRetainExactEvidence()
    {
        var files = Manager();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var publication = new TrustedLocalFilePublication(files, new TrustedLocalFileScope([_root]));
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        foreach (var mutation in new[] { "format-fraction", "length-fraction", "offset-fraction", "number-overflow",
            "comment", "trailing-comma", "root-null", "root-array", "wrong-property-case", "invalid-utf8" })
        {
            var header = Header(false);
            var lateImage = header["Members"]![1]!["After"]!.AsObject();
            if (mutation == "length-fraction") lateImage["Length"] = JsonNode.Parse("0.5");
            if (mutation == "offset-fraction") lateImage["Offset"] = JsonNode.Parse("8.0");
            var json = header.ToJsonString();
            json = mutation switch
            {
                "format-fraction" => json.Replace("\"Format\":2", "\"Format\":2.0", StringComparison.Ordinal),
                "number-overflow" => json.Replace("\"Offset\":8", "\"Offset\":9223372036854775808", StringComparison.Ordinal),
                "comment" => json.Insert(json.Length - 1, "/* unsupported */"),
                "trailing-comma" => json.Insert(json.Length - 1, ","),
                "root-null" => "null",
                "root-array" => "[]",
                "wrong-property-case" => json.Replace("\"Offset\":8", "\"offset\":8", StringComparison.Ordinal),
                _ => json
            };
            var evidence = Frame(json);
            if (mutation == "invalid-utf8")
            {
                var pathStart = json.LastIndexOf("\"Path\":", StringComparison.Ordinal);
                var character = json.IndexOf('x', pathStart);
                Assert.True(character > pathStart);
                evidence[16 + Encoding.UTF8.GetByteCount(json.AsSpan(0, character))] = 0xFF;
            }
            PutActive(evidence);
            File.WriteAllBytes(Target, After);
            var failure = Record.Exception(() => publication.Recover(lease));
            Assert.True(failure is InvalidDataException, $"{mutation}: {failure}");
            Assert.Equal(After, File.ReadAllBytes(Target));
            Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
            Assert.Equal(evidence, File.ReadAllBytes(Active));
        }
    }

    /// <summary>
    /// Builds the supported v2 schema independently from production private descriptor types.
    /// </summary>
    /// <param name="committed">
    /// The authority decision encoded in the frame.
    /// </param>
    /// <returns>
    /// A header with one replacement and one zero-length creation in exact payload order.
    /// </returns>
    private JsonObject Header(bool committed) => new()
    {
        ["Format"] = 2, ["TransactionId"] = Transaction, ["Committed"] = committed,
        ["GenerationBefore"] = new JsonObject { ["Exists"] = true, ["Id"] = Generation },
        ["GenerationAfter"] = new JsonObject { ["Exists"] = true, ["Id"] = Generation },
        ["Members"] = new JsonArray(
            new JsonObject { ["Path"] = Target, ["Before"] = PresentImage(Before, 0), ["After"] = PresentImage(After, 5) },
            new JsonObject { ["Path"] = AbsentPath(1), ["Before"] = AbsentImage(), ["After"] = PresentImage([], 8) })
    };

    /// <summary>
    /// Captures per-member writer observations from one actual publication stopped at its complete private intent.
    /// </summary>
    /// <param name="files">
    /// The fixture manager owning the active lease.
    /// </param>
    /// <param name="scope">
    /// The fixture's explicit local member scope.
    /// </param>
    /// <param name="lease">
    /// The active canonical write lease.
    /// </param>
    /// <param name="count">
    /// The complete member count, including the replacement followed by absent members.
    /// </param>
    /// <returns>
    /// The ordered complete-member flush measurements after exact frame and rollback checks.
    /// </returns>
    private List<TrustedLocalFrameMetadataObservation> ObserveWriterIntent(FileSystemManager files,
        TrustedLocalFileScope scope, FileSystemManager.CanonicalWriteLease lease, int count)
    {
        var changes = new List<TrustedLocalImageChange>
        { new(Target, TrustedLocalFileImage.CaptureFile(scope, Target), TrustedLocalFileImage.FromBytes(After)) };
        for (var index = 1; index < count; index++)
            changes.Add(new(AbsentPath(index), TrustedLocalFileImage.FromBytes(null), TrustedLocalFileImage.FromBytes(null)));
        var observations = new List<TrustedLocalFrameMetadataObservation>();
        var publication = new TrustedLocalFilePublication(files, scope, observations.Add);
        var outcome = publication.PublishImagesWithOutcome(lease, TrustedLocalGeneration.Existing(Generation), changes,
            (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.IntentStaged) return;
                var frame = File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Active)!, "intent.tmp"));
                var length = BinaryPrimitives.ReadInt64LittleEndian(frame.AsSpan(8, 8));
                Assert.Equal(16 + length + Before.Length + After.Length, frame.LongLength);
                using var metadata = JsonDocument.Parse(frame.AsMemory(16, checked((int)length)));
                Assert.Equal(count, metadata.RootElement.GetProperty("Members").GetArrayLength());
                Assert.Equal(Before, frame.AsSpan(checked(16 + (int)length), Before.Length).ToArray());
                Assert.Equal(After, frame.AsSpan(checked(16 + (int)length + Before.Length)).ToArray());
                throw new IntentCut();
            });
        Assert.Equal(TrustedLocalPublicationDisposition.RolledBack, outcome.Disposition);
        Assert.IsType<IntentCut>(outcome.Failure);
        var flushed = observations.Where(value => value.Kind == TrustedLocalFrameMetadataObservationKind.WriterFlushed).ToList();
        Assert.Equal(count + 1, flushed.Count);
        Assert.Equal(-1, flushed[0].MemberIndex);
        Assert.True(flushed[0].BytesPendingBeforeFlush > 0);
        Assert.Equal(0, flushed[0].BytesPendingAfterFlush);
        Assert.True(flushed[0].DestinationPosition > 16);
        long previous = flushed[0].DestinationPosition;
        for (var index = 1; index < flushed.Count; index++)
        {
            var value = flushed[index];
            Assert.Equal(index - 1, value.MemberIndex);
            Assert.True(value.BytesPendingBeforeFlush > 0);
            Assert.Equal(0, value.BytesPendingAfterFlush);
            Assert.True(value.DestinationPosition > previous);
            previous = value.DestinationPosition;
        }
        Assert.False(File.Exists(Active));
        return flushed.Skip(1).ToList();
    }

    /// <summary>
    /// Encodes an absent image with every required nullable field.
    /// </summary>
    /// <returns>
    /// The supported absent descriptor.
    /// </returns>
    private static JsonObject AbsentImage() => new() { ["Exists"] = false, ["Length"] = 0, ["Sha256"] = null, ["Offset"] = null };

    /// <summary>
    /// Encodes known fixture bytes with a separately supplied exact payload offset.
    /// </summary>
    /// <param name="bytes">
    /// The present image bytes, including a valid empty image.
    /// </param>
    /// <param name="offset">
    /// The independently specified contiguous payload offset.
    /// </param>
    /// <returns>
    /// The supported present descriptor.
    /// </returns>
    private static JsonObject PresentImage(byte[] bytes, long offset) => new()
    { ["Exists"] = true, ["Length"] = bytes.LongLength, ["Sha256"] = Hash(bytes), ["Offset"] = offset };

    /// <summary>
    /// Provides a unique long filename beneath an existing fixture directory.
    /// </summary>
    /// <param name="index">
    /// The unique descriptor index.
    /// </param>
    /// <returns>
    /// An absolute initially absent member path.
    /// </returns>
    private string AbsentPath(int index) => Path.Combine(Path.GetDirectoryName(Target)!, new string('x', 170) + index.ToString("D5") + ".bin");

    /// <summary>
    /// Builds exact v2 prefix, JSON region and fixed replacement payload without the production writer.
    /// </summary>
    /// <param name="json">
    /// The independently authored metadata, including deliberate invalid text.
    /// </param>
    /// <returns>
    /// The complete physical fixture frame.
    /// </returns>
    private static byte[] Frame(string json)
    {
        var metadata = Encoding.UTF8.GetBytes(json);
        var bytes = new byte[16 + metadata.Length + Before.Length + After.Length];
        Magic.CopyTo(bytes, 0);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8, 8), metadata.LongLength);
        metadata.CopyTo(bytes, 16);
        Before.CopyTo(bytes, 16 + metadata.Length);
        After.CopyTo(bytes, 16 + metadata.Length + Before.Length);
        return bytes;
    }

    /// <summary>
    /// Publishes fixture authority directly into its independently owned journal directory.
    /// </summary>
    /// <param name="bytes">
    /// The exact valid or corrupt frame bytes to retain for recovery.
    /// </param>
    private void PutActive(byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Active)!);
        File.WriteAllBytes(Active, bytes);
    }

    /// <summary>
    /// Computes the expected hash from known fixture bytes.
    /// </summary>
    /// <param name="bytes">
    /// The exact fixture image.
    /// </param>
    /// <returns>
    /// Its uppercase SHA-256 hexadecimal representation.
    /// </returns>
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    /// <summary>
    /// Removes only this test instance's independently owned temporary root.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
