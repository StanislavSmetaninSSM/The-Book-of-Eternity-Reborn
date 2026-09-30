using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TrustedLocalFilePublicationTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(Path.GetTempPath(), "boe-publication-" + Guid.NewGuid().ToString("N"));
    private readonly string _root;
    private readonly FileSystemManager _files;
    private readonly string _generation = Guid.NewGuid().ToString("N");
    private string Journal => Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1");
    private string Active => Path.Combine(Journal, "active.json");

    public TrustedLocalFilePublicationTests()
    {
        _root = Path.Combine(_sandbox, "root");
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        SetGeneration(_generation);
    }

    private TrustedLocalFilePublication Publisher(params string[] exact) => new(_files, new TrustedLocalFileScope([_root], exact));
    private string Member(string name) => Path.Combine(_root, name);
    private void SetGeneration(string id)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_files.SessionGenerationPath)!);
        File.WriteAllBytes(_files.SessionGenerationPath, GenerationBytes(id));
    }
    private static byte[] GenerationBytes(string id) => JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, GenerationId = id });
    private static Action<TrustedLocalPublicationPhase, int> Crash(TrustedLocalPublicationPhase phase, int member = -1) =>
        (actual, index) => { if (actual == phase && (member < 0 || member == index)) throw new Interrupted(); };
    private sealed class Interrupted : Exception { }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void AssertImage(string path, byte[]? bytes)
    {
        Assert.Equal(bytes != null, File.Exists(path));
        if (bytes != null) Assert.Equal(bytes, File.ReadAllBytes(path));
    }
    private void AssertClean()
    {
        Assert.False(File.Exists(Active));
        Assert.Empty(Directory.EnumerateFiles(_root, ".boe-local-*", SearchOption.AllDirectories));
        if (Directory.Exists(Journal)) Assert.Empty(Directory.EnumerateFileSystemEntries(Journal));
    }

    public static IEnumerable<object?[]> Images()
    {
        yield return [null, new byte[] { 0xEF, 0xBB, 0xBF, 123, 125 }];
        yield return [Array.Empty<byte>(), new byte[] { 0xFF, 0, 0xFE, 17 }];
        yield return [new byte[] { 0xFF, 0, 0xFE, 17 }, Array.Empty<byte>()];
        yield return [new byte[] { 0xEF, 0xBB, 0xBF, 123, 125 }, null];
        yield return [null, null];
        yield return [new byte[] { 1, 2 }, new byte[] { 1, 2 }];
    }

    [Theory, MemberData(nameof(Images))]
    public async Task Publish_PreservesExactBytesAbsenceAndReturnsLogicalResult(byte[]? before, byte[]? after)
    {
        var path = Member("state.bin");
        if (before != null) File.WriteAllBytes(path, before);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var result = Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(path, before, after)]);
        AssertImage(path, after);
        Assert.Equal(TrustedLocalGeneration.Existing(_generation), result.Generation);
        Assert.True(Guid.TryParseExact(result.TransactionId, "N", out _));
        var member = Assert.Single(result.Members);
        Assert.Equal(path, member.Path);
        Assert.Equal(after != null, member.Exists);
        Assert.Equal(after == null ? null : Hash(after), member.Sha256);
        AssertClean();
    }

    [Theory]
    [InlineData((int)TrustedLocalPublicationPhase.IntentStaged)]
    [InlineData((int)TrustedLocalPublicationPhase.IntentPublished)]
    [InlineData((int)TrustedLocalPublicationPhase.MemberStaged)]
    [InlineData((int)TrustedLocalPublicationPhase.MemberPublished)]
    [InlineData((int)TrustedLocalPublicationPhase.CommitStaged)]
    [InlineData((int)TrustedLocalPublicationPhase.Committed)]
    [InlineData((int)TrustedLocalPublicationPhase.CleanupMember)]
    [InlineData((int)TrustedLocalPublicationPhase.CleanupComplete)]
    public async Task Recover_EveryPublishCutRestoresWholeSetOrKeepsCommitted(int phaseValue)
    {
        var phase = (TrustedLocalPublicationPhase)phaseValue;
        var replace = Member("config.json");
        var create = Member("game_settings.json");
        var delete = Member("deleted.bin");
        byte[] before = [0xEF, 0xBB, 0xBF, 0xFF, 0];
        File.WriteAllBytes(replace, before);
        File.WriteAllBytes(delete, []);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation),
            [new(replace, before, []), new(create, null, [0xFF]), new(delete, [], null)], Crash(phase)));
        Publisher().Recover(lease);
        var committed = phase is TrustedLocalPublicationPhase.Committed or TrustedLocalPublicationPhase.CleanupMember or TrustedLocalPublicationPhase.CleanupComplete;
        AssertImage(replace, committed ? [] : before);
        AssertImage(create, committed ? [0xFF] : null);
        AssertImage(delete, committed ? null : []);
        Publisher().Recover(lease);
        AssertClean();
    }

    [Theory]
    [InlineData((int)TrustedLocalPublicationPhase.RollbackStaged)]
    [InlineData((int)TrustedLocalPublicationPhase.MemberRestored)]
    public async Task Recover_RestartsAfterInterruptedRollback(int phaseValue)
    {
        var phase = (TrustedLocalPublicationPhase)phaseValue;
        var path = Member("state");
        File.WriteAllBytes(path, [0xFF]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation),
            [new(path, [0xFF], [1])], Crash(TrustedLocalPublicationPhase.MemberPublished)));
        Assert.Throws<Interrupted>(() => Publisher().Recover(lease, Crash(phase)));
        Publisher().Recover(lease);
        AssertImage(path, [0xFF]);
        AssertClean();
    }

    [Fact]
    public async Task Recover_PreflightsEveryMemberBeforeRollbackAndRetainsUnknownContent()
    {
        var a = Member("a"); var b = Member("b");
        File.WriteAllBytes(a, [1]); File.WriteAllBytes(b, [2]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation),
            [new(a, [1], [11]), new(b, [2], [22])], Crash(TrustedLocalPublicationPhase.MemberPublished, 1)));
        var evidence = File.ReadAllBytes(Active);
        File.WriteAllBytes(b, [99]);
        Assert.Throws<InvalidDataException>(() => Publisher().Recover(lease));
        AssertImage(a, [11]); AssertImage(b, [99]);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
    }

    [Fact]
    public async Task Publish_RejectsExpectedContentAndStaleGenerationBeforeAnyMutation()
    {
        var path = Member("state"); File.WriteAllBytes(path, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(path, [2], [3])]));
        Assert.Throws<InvalidDataException>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(Guid.NewGuid().ToString("N")), [new(path, [1], [3])]));
        AssertImage(path, [1]); AssertClean();
    }

    [Fact]
    public async Task Recover_RejectsChangedGenerationWithoutTouchingMembersOrEvidence()
    {
        var path = Member("state"); File.WriteAllBytes(path, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(path, [1], [2])], Crash(TrustedLocalPublicationPhase.MemberPublished)));
        var evidence = File.ReadAllBytes(Active);
        SetGeneration(Guid.NewGuid().ToString("N"));
        Assert.Throws<InvalidDataException>(() => Publisher().Recover(lease));
        AssertImage(path, [2]); Assert.Equal(evidence, File.ReadAllBytes(Active));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_BootstrapGenerationIsAnExplicitJournalMember(bool committed)
    {
        File.Delete(_files.SessionGenerationPath);
        var config = Member("config.json");
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Absent,
            [new(config, null, [1]), new(_files.SessionGenerationPath, null, GenerationBytes(_generation))],
            Crash(committed ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished, committed ? -1 : 1)));
        Publisher().Recover(lease);
        AssertImage(config, committed ? [1] : null);
        AssertImage(_files.SessionGenerationPath, committed ? GenerationBytes(_generation) : null);
        AssertClean();
    }

    [Fact]
    public async Task Publish_RequiresItsOwnActiveCanonicalLease()
    {
        var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        await lease.DisposeAsync();
        Assert.Throws<InvalidOperationException>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(Member("a"), null, [1])]));
        Assert.Throws<InvalidOperationException>(() => Publisher().Recover(lease));
        var otherRoot = Path.Combine(_sandbox, "other"); Directory.CreateDirectory(otherRoot);
        var other = new FileSystemManager(otherRoot, NullLogger<FileSystemManager>.Instance);
        await using var otherLease = await other.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidOperationException>(() => Publisher().Recover(otherLease));
        AssertClean();
    }

    [Fact]
    public async Task Publish_RecoversPendingWorkBeforeStartingNextTransaction()
    {
        var path = Member("state"); File.WriteAllBytes(path, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(path, [1], [2])], Crash(TrustedLocalPublicationPhase.MemberPublished)));
        Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(path, [1], [3])]);
        AssertImage(path, [3]); AssertClean();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Publish_HardLinkedDestinationNeverChangesOutsideBytes(bool delete)
    {
        var outside = Path.Combine(_sandbox, "outside"); var path = Member("state");
        File.WriteAllBytes(outside, [0xFE, 0]);
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(path, outside, IntPtr.Zero));
        else Assert.Equal(0, Link(outside, path));
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(path, [0xFE, 0], delete ? null : [1])]);
        AssertImage(outside, [0xFE, 0]); AssertImage(path, delete ? null : [1]); AssertClean();
    }

    [Fact]
    public async Task Publish_ExactExternalGrantUsesOwnedSiblingStageWithoutGrantingOtherFiles()
    {
        var outside = Path.Combine(_sandbox, "external.json"); File.WriteAllBytes(outside, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Publisher(outside).Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(outside, [1], [2])]);
        AssertImage(outside, [2]);
        Assert.Throws<InvalidDataException>(() => Publisher(outside).Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(Path.Combine(_sandbox, "ungranted"), null, [2])]));
        AssertClean();
    }

    [Fact]
    public async Task Publish_RejectsDuplicateAndReservedMembers()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(Member("a"), null, [1]), new(Member("a"), null, [2])]));
        Assert.Throws<InvalidDataException>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(Active, null, [1])]));
        Assert.Throws<InvalidDataException>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(_files.CanonicalWriteLockPath, null, [1])]));
        AssertClean();
    }

    [Fact]
    public async Task Recover_RetainsInvalidJournalAndLeavesOldFormatSeparate()
    {
        var old = Path.Combine(_files.PhysicalPublicationTransactionsRootPath, "old.json");
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(old)!); File.WriteAllText(old, "old evidence");
        Directory.CreateDirectory(Journal); File.WriteAllText(Active, "{\"Format\":1,\"Format\":1}");
        Assert.Throws<InvalidDataException>(() => Publisher().Recover(lease));
        Assert.Equal("{\"Format\":1,\"Format\":1}", File.ReadAllText(Active));
        Assert.Equal("old evidence", File.ReadAllText(old));
    }

    [Fact]
    public async Task Recover_CommittedUnknownMemberBlocksCleanupWithoutRollback()
    {
        var path = Member("state"); File.WriteAllBytes(path, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(path, [1], [2])], Crash(TrustedLocalPublicationPhase.Committed)));
        File.WriteAllBytes(path, [99]);
        Assert.Throws<InvalidDataException>(() => Publisher().Recover(lease));
        AssertImage(path, [99]); Assert.True(File.Exists(Active));
    }

    [Theory]
    [InlineData("MemberPublished")]
    [InlineData("Committed")]
    [InlineData("CleanupMember")]
    [InlineData("RollbackStaged")]
    public async Task Recover_AbruptColdProcessExitAndSeparateRecoveryProcess(string phase)
    {
        File.WriteAllBytes(Member("replace"), [0xEF, 0xBB, 0xBF, 0xFF]);
        File.WriteAllBytes(Member("delete"), []);
        Assert.Equal(73, await RunHost("publish", phase));
        Assert.True(File.Exists(Active));
        Assert.Equal(0, await RunHost("recover", phase));
        var committed = phase is "Committed" or "CleanupMember";
        AssertImage(Member("replace"), committed ? [] : [0xEF, 0xBB, 0xBF, 0xFF]);
        AssertImage(Member("create"), committed ? [0xFE, 0] : null);
        AssertImage(Member("delete"), committed ? null : []);
        AssertClean();
    }

    [Fact]
    public async Task Publish_SecondParticipatingProcessCannotAcquireHeldCanonicalLease()
    {
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(74, await RunHost("contend", "unused"));
        AssertClean();
    }

    private async Task<int> RunHost(string mode, string phase)
    {
        var assembly = typeof(TrustedLocalFilePublicationTests).Assembly.Location;
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(PortableStorageCrashHost.Program).Assembly.Location,
                     _root, _generation, mode, phase }) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Crash fixture did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout + await stderr;
            Assert.True(process.ExitCode is 0 or 73 or 74, output);
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
        }
    }

    [Theory]
    [InlineData("format")]
    [InlineData("duplicate-member")]
    [InlineData("hash")]
    [InlineData("missing-decision")]
    [InlineData("outside-path")]
    public async Task Recover_PreflightsInvalidJournalWithoutRestoringAnyMember(string corruption)
    {
        var a = Member("a"); var b = Member("b");
        File.WriteAllBytes(a, [1]); File.WriteAllBytes(b, [2]);
        var outside = Path.Combine(_sandbox, "outside"); File.WriteAllBytes(outside, [90]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation),
            [new(a, [1], [11]), new(b, [2], [22])], Crash(TrustedLocalPublicationPhase.MemberPublished, 1)));
        var json = JsonNode.Parse(File.ReadAllText(Active))!;
        switch (corruption)
        {
            case "format": json["Format"] = 9; break;
            case "duplicate-member": json["Members"]!.AsArray().Add(json["Members"]![0]!.DeepClone()); break;
            case "hash": json["Members"]![1]!["Before"]!["Sha256"] = new string('0', 64); break;
            case "missing-decision": json.AsObject().Remove("Committed"); break;
            case "outside-path": json["Members"]![1]!["Path"] = outside; break;
        }
        var evidence = Encoding.UTF8.GetBytes(json.ToJsonString()); File.WriteAllBytes(Active, evidence);
        Assert.Throws<InvalidDataException>(() => Publisher().Recover(lease));
        AssertImage(a, [11]); AssertImage(b, [22]); AssertImage(outside, [90]);
        Assert.Equal(evidence, File.ReadAllBytes(Active));
    }

    [Fact]
    public async Task Recover_LinkInLaterMemberPreservesEarlierMemberAndOutsideSentinel()
    {
        var a = Member("a"); var b = Member("b");
        File.WriteAllBytes(a, [1]); File.WriteAllBytes(b, [2]);
        var outside = Path.Combine(_sandbox, "outside"); File.WriteAllBytes(outside, [90]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation),
            [new(a, [1], [11]), new(b, [2], [22])], Crash(TrustedLocalPublicationPhase.MemberPublished, 1)));
        File.Delete(b); File.CreateSymbolicLink(b, outside);
        var evidence = File.ReadAllBytes(Active);
        Assert.Throws<InvalidDataException>(() => Publisher().Recover(lease));
        AssertImage(a, [11]); AssertImage(outside, [90]); Assert.Equal(evidence, File.ReadAllBytes(Active));
    }

    [Fact]
    public async Task Recover_PartiallyWrittenPrivateScratchIsNotCanonicalAuthority()
    {
        var path = Member("state"); File.WriteAllBytes(path, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation),
            [new(path, [1], [2, 3, 4])], Crash(TrustedLocalPublicationPhase.MemberStaged)));
        var stage = Assert.Single(Directory.EnumerateFiles(_root, ".boe-local-*.stage"));
        File.WriteAllBytes(stage, [2]);
        Publisher().Recover(lease);
        AssertImage(path, [1]); AssertClean();
    }

    public static IEnumerable<object[]> GenerationEncodings()
    {
        foreach (var encoding in new[] { "utf8-bom", "utf16-le", "utf16-be", "utf32-le", "utf32-be" })
            foreach (var operation in new[] { "bootstrap", "transition", "rollback" })
                yield return [encoding, operation];
    }

    [Theory, MemberData(nameof(GenerationEncodings))]
    public async Task Publish_GenerationEncodingPreservesExactBootstrapTransitionAndRollbackBytes(string encodingName, string operation)
    {
        Encoding encoding = encodingName switch
        {
            "utf8-bom" => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            "utf16-le" => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
            "utf16-be" => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
            "utf32-le" => new UTF32Encoding(bigEndian: false, byteOrderMark: true),
            "utf32-be" => new UTF32Encoding(bigEndian: true, byteOrderMark: true),
            _ => throw new ArgumentOutOfRangeException(nameof(encodingName))
        };
        byte[] Encode(string id) => encoding.GetPreamble().Concat(encoding.GetBytes(Encoding.UTF8.GetString(GenerationBytes(id)))).ToArray();
        var bootstrap = operation == "bootstrap";
        var committed = operation != "rollback";
        var before = bootstrap ? null : Encode(_generation);
        var afterId = Guid.NewGuid().ToString("N");
        var after = Encode(afterId);
        if (bootstrap) File.Delete(_files.SessionGenerationPath);
        else File.WriteAllBytes(_files.SessionGenerationPath, before!);
        var config = Member("config.json");
        if (!bootstrap) File.WriteAllBytes(config, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        // Existing generation reading establishes that this is a valid current
        // schema image, independently of the publisher's image decoder.
        Assert.Equal(bootstrap ? null : _generation, _files.ReadExistingSessionGeneration(lease));
        Assert.Throws<Interrupted>(() => Publisher().Publish(lease,
            bootstrap ? TrustedLocalGeneration.Absent : TrustedLocalGeneration.Existing(_generation),
            [new(config, bootstrap ? null : [1], [2]), new(_files.SessionGenerationPath, before, after)],
            Crash(committed ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished, committed ? -1 : 1)));
        Publisher().Recover(lease);
        AssertImage(_files.SessionGenerationPath, committed ? after : before);
        AssertImage(config, committed ? [2] : bootstrap ? null : [1]);
        Assert.Equal(committed ? afterId : _generation, _files.ReadExistingSessionGeneration(lease));
        AssertClean();
    }

    [Theory]
    [InlineData(@"\\?\C:\game\.boe_runtime\session-generation\current.json", @"C:\game\.boe_runtime\session-generation\current.json")]
    [InlineData(@"\\?\C:\game\.boe_runtime\trusted-local-publication-v1", @"C:\game\.boe_runtime\trusted-local-publication-v1")]
    [InlineData(@"\\?\C:\game\.boe_runtime\locks\canonical-write.lock", @"C:\game\.boe_runtime\locks\canonical-write.lock")]
    [InlineData(@"\\?\C:\game\.boe_runtime\locks\session-lifecycle.lock", @"C:\game\.boe_runtime\locks\session-lifecycle.lock")]
    [InlineData(@"\\?\UNC\server\share\game\.boe_runtime\session-generation\current.json", @"\\server\share\game\.boe_runtime\session-generation\current.json")]
    [InlineData(@"\\?\UNC\server\share\game\.boe_runtime\trusted-local-publication-v1", @"\\server\share\game\.boe_runtime\trusted-local-publication-v1")]
    [InlineData(@"\\?\UNC\server\share\game\.boe_runtime\locks\canonical-write.lock", @"\\server\share\game\.boe_runtime\locks\canonical-write.lock")]
    [InlineData(@"\\?\UNC\server\share\game\.boe_runtime\locks\session-lifecycle.lock", @"\\server\share\game\.boe_runtime\locks\session-lifecycle.lock")]
    public void AuthorityPathNormalization_MatchesScopeForExtendedWindowsComparisons(string raw, string expected)
    {
        var member = TrustedLocalFileScope.NormalizeWindowsPathSpelling(raw);
        var authority = TrustedLocalFilePublication.NormalizeAuthorityPath(raw, windows: true);
        Assert.Equal(expected, authority);
        Assert.Equal(member, authority);
        Assert.Equal(expected, TrustedLocalFilePublication.NormalizeAuthorityPath(expected, windows: true));
        Assert.Equal(raw, TrustedLocalFilePublication.NormalizeAuthorityPath(raw, windows: false));
    }

    [Fact]
    public async Task Publish_PreservesTheExistingGenerationReadersCurrentSchemaContract()
    {
        var before = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"generationId\":\"" + _generation + "\",\"extension\":true}");
        File.WriteAllBytes(_files.SessionGenerationPath, before);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(_generation, _files.ReadExistingSessionGeneration(lease));
        var after = GenerationBytes(_generation);
        Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation), [new(_files.SessionGenerationPath, before, after)]);
        AssertImage(_files.SessionGenerationPath, after); AssertClean();
    }

    [Fact]
    public async Task Publish_RejectsDuplicateGenerationMemberAsInvalidData()
    {
        var bytes = GenerationBytes(_generation);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => Publisher().Publish(lease, TrustedLocalGeneration.Existing(_generation),
            [new(_files.SessionGenerationPath, bytes, bytes), new(_files.SessionGenerationPath, bytes, bytes)]));
        AssertImage(_files.SessionGenerationPath, bytes); AssertClean();
    }

    [Theory]
    [InlineData((int)TrustedLocalPublicationPhase.MemberPublished, (int)TrustedLocalPublicationDisposition.RolledBack)]
    [InlineData((int)TrustedLocalPublicationPhase.Committed, (int)TrustedLocalPublicationDisposition.Committed)]
    [InlineData((int)TrustedLocalPublicationPhase.CleanupMember, (int)TrustedLocalPublicationDisposition.Committed)]
    public async Task PublishWithOutcome_SeparatesRollbackFromDurableCommitCleanupFailure(int phase, int expected)
    {
        var path = Member("outcome"); File.WriteAllBytes(path, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var result = Publisher().PublishWithOutcome(lease, TrustedLocalGeneration.Existing(_generation),
            [new(path, [1], [2])], Crash((TrustedLocalPublicationPhase)phase));
        var committed = expected == (int)TrustedLocalPublicationDisposition.Committed;
        Assert.Equal((TrustedLocalPublicationDisposition)expected, result.Disposition);
        Assert.IsType<Interrupted>(result.Failure);
        Assert.Equal(committed, result.Publication != null);
        AssertImage(path, committed ? [2] : [1]);
        Publisher().Recover(lease);
        AssertImage(path, committed ? [2] : [1]); AssertClean();
    }

    [Fact]
    public async Task PublishWithOutcome_UnknownRollbackContentIsUncertainAndRetainsEvidence()
    {
        var path = Member("outcome"); File.WriteAllBytes(path, [1]);
        await using var lease = await _files.AcquireCanonicalWriteLeaseAsync();
        var result = Publisher().PublishWithOutcome(lease, TrustedLocalGeneration.Existing(_generation),
            [new(path, [1], [2])], (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
                File.WriteAllBytes(path, [99]); throw new Interrupted();
            });
        Assert.Equal(TrustedLocalPublicationDisposition.Uncertain, result.Disposition);
        Assert.Null(result.Publication); Assert.NotNull(result.Failure);
        AssertImage(path, [99]); Assert.True(File.Exists(Active));
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)] private static extern int Link(string oldPath, string newPath);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLink(string path, string existing, IntPtr security);
    public void Dispose() { if (Directory.Exists(_sandbox)) Directory.Delete(_sandbox, true); }
}
