using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TrustedLocalStreamPublicationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-stream-publication-" + Guid.NewGuid().ToString("N"));
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private static readonly byte[] Before = [0xEF, 0xBB, 0xBF, 0xFF];
    private static readonly byte[] After = [42, 41, 0];
    private static readonly byte[] Created = [78, 255];
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BOELP2\r\n");
    private FileSystemManager Manager(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
    private string Active => Path.Combine(_root, ".boe_runtime/trusted-local-publication-v1/active.json");
    private string Target => Path.Combine(_root, "game_session", "game_state", "core", "replace.bin");
    private string NewTarget => Path.Combine(_root, "game_session", "game_state", "core", "create.bin");
    private sealed class Cut : Exception { }

    public TrustedLocalStreamPublicationTests()
    {
        var files = Manager(); files.EnsureDirectoryStructure();
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = _generation }));
        File.WriteAllBytes(Target, Before);
    }

    [Fact]
    public async Task FileImagesBecomeSelfContainedBeforeDisposableSourcesAreRemoved()
    {
        var files = Manager(); var inputs = Path.Combine(_root, "inputs"); Directory.CreateDirectory(inputs);
        var beforeSource = Path.Combine(inputs, "before.bin"); var afterSource = Path.Combine(inputs, "after.bin");
        File.WriteAllBytes(beforeSource, Before); File.WriteAllBytes(afterSource, After);
        var scope = new TrustedLocalFileScope([_root]); var phases = new List<TrustedLocalPublicationPhase>();
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var outcome = new TrustedLocalFilePublication(files, scope).PublishImagesWithOutcome(lease,
                TrustedLocalGeneration.Existing(_generation),
                [new(Target, TrustedLocalFileImage.CaptureFile(scope, beforeSource), TrustedLocalFileImage.CaptureFile(scope, afterSource))],
                (phase, _) =>
                {
                    phases.Add(phase);
                    if (phase == TrustedLocalPublicationPhase.IntentPublished) Directory.Delete(inputs, true);
                    if (phase == TrustedLocalPublicationPhase.Committed) throw new Cut();
                });
            Assert.Equal(TrustedLocalPublicationDisposition.Committed, outcome.Disposition);
            Assert.IsType<Cut>(outcome.Failure);
        }
        Assert.Contains(TrustedLocalPublicationPhase.IntentPublished, phases);
        Assert.Equal(Magic, File.ReadAllBytes(Active)[..8]);
        Assert.Equal(After, File.ReadAllBytes(Target)); Assert.False(Directory.Exists(inputs));
        await using (var recovery = await Manager().AcquireCanonicalWriteLeaseAsync()) { }
        Assert.Equal(After, File.ReadAllBytes(Target)); Assert.False(File.Exists(Active));
    }

    [Theory]
    [InlineData((int)TrustedLocalPublicationPhase.IntentStaged, false)]
    [InlineData((int)TrustedLocalPublicationPhase.MemberStaged, false)]
    [InlineData((int)TrustedLocalPublicationPhase.MemberPublished, false)]
    [InlineData((int)TrustedLocalPublicationPhase.CommitStaged, false)]
    [InlineData((int)TrustedLocalPublicationPhase.Committed, true)]
    public async Task WarmImageCutsPreserveTheCompleteDecision(int phaseValue, bool committed)
    {
        var files = Manager(); var scope = new TrustedLocalFileScope([_root]); var reached = 0;
        var source = Path.Combine(_root, "after.bin"); File.WriteAllBytes(source, After);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var outcome = new TrustedLocalFilePublication(files, scope).PublishImagesWithOutcome(lease,
            TrustedLocalGeneration.Existing(_generation),
            [new(Target, TrustedLocalFileImage.CaptureFile(scope, Target), TrustedLocalFileImage.CaptureFile(scope, source)),
             new(NewTarget, TrustedLocalFileImage.FromBytes(null), TrustedLocalFileImage.FromBytes(Created))],
            (phase, _) => { if (phase == (TrustedLocalPublicationPhase)phaseValue) { reached++; throw new Cut(); } });
        Assert.Equal(1, reached);
        Assert.Equal(committed ? TrustedLocalPublicationDisposition.Committed : TrustedLocalPublicationDisposition.RolledBack, outcome.Disposition);
        Assert.Equal(committed ? After : Before, File.ReadAllBytes(Target));
        Assert.Equal(committed, File.Exists(NewTarget));
        if (committed) Assert.Equal(Created, File.ReadAllBytes(NewTarget));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidFramedEvidenceRecoversThroughNormalCanonicalAcquisition(bool committed)
    {
        WriteCurrentAfter(); WriteFrame(committed);
        await using (var lease = await Manager().AcquireCanonicalWriteLeaseAsync()) { }
        AssertDecision(committed); Assert.False(File.Exists(Active));
    }

    [Theory]
    [InlineData("unknown-format")]
    [InlineData("unknown-property")]
    [InlineData("duplicate-property")]
    [InlineData("duplicate-member")]
    [InlineData("negative-header")]
    [InlineData("huge-header")]
    [InlineData("truncated-header")]
    [InlineData("truncated-region")]
    [InlineData("trailing-region")]
    [InlineData("region-overlap")]
    [InlineData("region-gap")]
    [InlineData("negative-length")]
    [InlineData("region-overflow")]
    [InlineData("image-hash")]
    public async Task InvalidV2FrameRetainsAllEvidenceBeforeAnyRecoveryMutation(string corruption)
    {
        WriteCurrentAfter(); var header = Header(false, v2: true);
        switch (corruption)
        {
            case "unknown-format": header["Format"] = 3; break;
            case "unknown-property": header["Unexpected"] = true; break;
            case "duplicate-member": header["Members"]![1]!["Path"] = Target; break;
            case "region-overlap": header["Members"]![0]!["After"]!["Offset"] = 0; break;
            case "region-gap": header["Members"]![0]!["After"]!["Offset"] = 5; break;
            case "negative-length": header["Members"]![0]!["Before"]!["Length"] = -1; break;
            case "region-overflow": header["Members"]![0]!["Before"]!["Length"] = long.MaxValue; break;
            case "image-hash": header["Members"]![0]!["Before"]!["Sha256"] = new string('0', 64); break;
        }
        var json = header.ToJsonString();
        if (corruption == "duplicate-property") json = json.Replace("\"Format\":2", "\"Format\":2,\"Format\":2", StringComparison.Ordinal);
        var bytes = FrameBytes(json);
        if (corruption == "negative-header") BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8, 8), -1);
        if (corruption == "huge-header") BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8, 8), long.MaxValue);
        if (corruption == "truncated-header") bytes = bytes[..20];
        if (corruption == "truncated-region") bytes = bytes[..^1];
        if (corruption == "trailing-region") bytes = [.. bytes, 99];
        PutActive(bytes);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await Manager().AcquireCanonicalWriteLeaseAsync(); });
        Assert.Contains("v2", failure.Message, StringComparison.OrdinalIgnoreCase); // Must reach framed admission, not the old JSON-parser rejection.
        Assert.Equal(bytes, File.ReadAllBytes(Active)); AssertDecision(true);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidActiveV2OwnsDecisionDespiteUnknownTemporaryContents(bool committed)
    {
        WriteCurrentAfter(); WriteFrame(committed);
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Active)!, "intent.tmp"), [0xFF]);
        File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(Active)!, "commit.tmp"), Encoding.UTF8.GetBytes("unknown partial format"));
        await using (var lease = await Manager().AcquireCanonicalWriteLeaseAsync()) { }
        AssertDecision(committed); Assert.Empty(Directory.EnumerateFileSystemEntries(Path.GetDirectoryName(Active)!));
    }

    [Fact]
    public async Task UnknownLaterMemberPreventsEarlierRollbackAndRetainsV2Authority()
    {
        WriteCurrentAfter(); WriteFrame(false); File.WriteAllBytes(NewTarget, [99]); var evidence = File.ReadAllBytes(Active);
        var failure = await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await Manager().AcquireCanonicalWriteLeaseAsync(); });
        Assert.Contains("unknown bytes", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(After, File.ReadAllBytes(Target)); Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(NewTarget));
        Assert.Equal(evidence, File.ReadAllBytes(Active));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalV1EvidenceKeepsItsOriginalPendingAndCommittedSemantics(bool committed)
    {
        WriteCurrentAfter(); PutActive(JsonSerializer.SerializeToUtf8Bytes(Header(committed, v2: false)));
        await using (var lease = await Manager().AcquireCanonicalWriteLeaseAsync()) { }
        AssertDecision(committed); Assert.False(File.Exists(Active));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CanonicalImageAdapterUsesOneDecisionIncludingAnyFreshGeneration(bool fresh)
    {
        var committed = 0; var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
            { if (phase == TrustedLocalPublicationPhase.Committed) committed++; } });
        if (fresh) File.Delete(files.SessionGenerationPath);
        var generation = File.Exists(files.SessionGenerationPath) ? File.ReadAllBytes(files.SessionGenerationPath) : null;
        var scope = new TrustedLocalFileScope([_root]); var source = Path.Combine(_root, "after.bin"); File.WriteAllBytes(source, After);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var outcome = await files.PublishLocalImageFilesAsync(lease,
            [new("game_state/core/create.bin", TrustedLocalFileImage.FromBytes(null), TrustedLocalFileImage.CaptureFile(scope, source))]);
        Assert.Equal(TrustedLocalPublicationDisposition.Committed, outcome.Disposition); Assert.Equal(1, committed);
        Assert.Equal(After, File.ReadAllBytes(NewTarget)); Assert.Equal(Before, File.ReadAllBytes(Target));
        Assert.Equal(fresh ? 2 : 1, outcome.Publication!.Members.Count);
        if (!fresh) Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        else Assert.NotNull(files.ReadExistingSessionGeneration(lease));
    }

    private JsonObject Header(bool committed, bool v2)
    {
        long offset = 0;
        JsonObject Image(byte[]? bytes)
        {
            var result = new JsonObject { ["Exists"] = bytes != null, ["Sha256"] = bytes == null ? null : Convert.ToHexString(SHA256.HashData(bytes)) };
            if (v2)
            {
                result["Length"] = bytes?.LongLength ?? 0;
                result["Offset"] = bytes == null ? null : JsonValue.Create(offset);
                offset += bytes?.LongLength ?? 0;
            }
            else result["Bytes"] = bytes == null ? null : Convert.ToBase64String(bytes);
            return result;
        }
        return new JsonObject
        {
            ["Format"] = v2 ? 2 : 1, ["TransactionId"] = "0123456789abcdef0123456789abcdef", ["Committed"] = committed,
            ["GenerationBefore"] = new JsonObject { ["Exists"] = true, ["Id"] = _generation },
            ["GenerationAfter"] = new JsonObject { ["Exists"] = true, ["Id"] = _generation },
            ["Members"] = new JsonArray(
                new JsonObject { ["Path"] = Target, ["Before"] = Image(Before), ["After"] = Image(After) },
                new JsonObject { ["Path"] = NewTarget, ["Before"] = Image(null), ["After"] = Image(Created) })
        };
    }
    private byte[] FrameBytes(string json)
    {
        var header = Encoding.UTF8.GetBytes(json); var bytes = new byte[16 + header.Length + Before.Length + After.Length + Created.Length];
        Magic.CopyTo(bytes, 0); BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(8, 8), header.Length);
        header.CopyTo(bytes, 16); Before.CopyTo(bytes, 16 + header.Length);
        After.CopyTo(bytes, 16 + header.Length + Before.Length); Created.CopyTo(bytes, 16 + header.Length + Before.Length + After.Length);
        return bytes;
    }
    private void PutActive(byte[] bytes) { Directory.CreateDirectory(Path.GetDirectoryName(Active)!); File.WriteAllBytes(Active, bytes); }
    private void WriteFrame(bool committed) => PutActive(FrameBytes(Header(committed, v2: true).ToJsonString()));
    private void WriteCurrentAfter() { File.WriteAllBytes(Target, After); File.WriteAllBytes(NewTarget, Created); }
    private void AssertDecision(bool committed)
    {
        Assert.Equal(committed ? After : Before, File.ReadAllBytes(Target)); Assert.Equal(committed, File.Exists(NewTarget));
        if (committed) Assert.Equal(Created, File.ReadAllBytes(NewTarget));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
