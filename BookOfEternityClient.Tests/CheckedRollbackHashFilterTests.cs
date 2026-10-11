using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// Test-only hypothesis probe. No production reader, hook or authority API changes.
public sealed class CheckedRollbackHashFilterTests(ITestOutputHelper output)
{
    private const string DriverSha = "A0D90E47D74AFEBE8C0F968A2DDC0C87AAB1BDF863AB7A2214D19170CA8A9BC9";
    private const int ScratchLength = 65536, Rounds = 16, PairCount = 6;
    private sealed record Member(string Path, int Bytes, string SyntheticSha256);
    private sealed record Sample(double Seconds, long ManagedBytes, int Gen0, int Gen1, int Gen2);
    private sealed record Pair(int Index, string Order, Sample A, Sample B, double OptimisticWholeActionSeconds);

    private static FileSystemManager Files(string root)
    {
        Directory.CreateDirectory(Path.Combine(root, "game_session"));
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        // Explicitly off even if the invoking shell inherited profile options.
        var field = typeof(FileSystemManager).GetField("_browserAdmissionDiagnostic", BindingFlags.NonPublic | BindingFlags.Instance)!;
        field.SetValue(files, null);
        Assert.Null(field.GetValue(files));
        Assert.Null(typeof(FileSystemManager).GetField("_hooks", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(files));
        return files;
    }

    private static Func<string, TrustedLocalFileScope?, bool?, byte[]?> BindActualReader(FileSystemManager files) =>
        typeof(FileSystemManager).GetMethod("ReadOriginalBrowserBytes", BindingFlags.NonPublic | BindingFlags.Instance,
            null, [typeof(string), typeof(TrustedLocalFileScope), typeof(bool?)], null)!
            .CreateDelegate<Func<string, TrustedLocalFileScope?, bool?, byte[]?>>(files);

    private static string? ActualHash(Func<string, TrustedLocalFileScope?, bool?, byte[]?> read,
        string relative, TrustedLocalFileScope scope, Action? beforeDigest = null)
    {
        var bytes = read(relative, scope, true);
        if (bytes == null) return null;
        beforeDigest?.Invoke();
        return PendingTurnSnapshotAuthority.ComputeSha256(bytes);
    }

    private static string? PrototypeHash(FileSystemManager files, string relative, TrustedLocalFileScope scope,
        byte[] scratch, Action<FileStream>? afterLength = null, Action? beforeFinalPath = null, Action? beforeDigest = null)
    {
        if (!PendingTurnSnapshotAuthority.IsSafeRelativePath(relative)) throw BrowserOriginalMainCondition.Invalid();
        var path = scope.ValidateFile(files.ResolvePath(relative));
        if (!File.Exists(path)) return null;
        using var stream = new FileStream(scope.ValidateFile(path, false), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var length = stream.Length;
        if (length > Array.MaxLength)
            throw new IOException("Original browser file cannot be represented by the existing byte reader.");
        afterLength?.Invoke(stream); // Only out-of-interval prototype semantic controls use callbacks.
        using var sha = SHA256.Create(); // Same provider family as unchanged A; not the rejected HashData switch.
        var remaining = length;
        while (remaining > 0)
        {
            var count = (int)Math.Min(remaining, scratch.Length);
            stream.ReadExactly(scratch.AsSpan(0, count));
            sha.TransformBlock(scratch, 0, count, null, 0);
            remaining -= count;
        }
        if (stream.ReadByte() != -1) throw BrowserOriginalMainCondition.Invalid();
        beforeFinalPath?.Invoke();
        scope.ValidateFile(path, false);
        beforeDigest?.Invoke();
        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha.Hash!);
    }

    private static Sample Measure(FileSystemManager files,
        Func<string, TrustedLocalFileScope?, bool?, byte[]?> actual, Member[] members, string?[] results,
        bool prototype, int rounds)
    {
        var g0 = GC.CollectionCount(0); var g1 = GC.CollectionCount(1); var g2 = GC.CollectionCount(2);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var round = 0; round < rounds; round++)
        {
            var scope = new TrustedLocalFileScope([files.BasePath]);
            var scratch = prototype ? new byte[ScratchLength] : null;
            for (var member = 0; member < members.Length; member++)
                results[member] = prototype
                    ? PrototypeHash(files, members[member].Path, scope, scratch!)
                    : ActualHash(actual, members[member].Path, scope);
        }
        var seconds = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return new(seconds, bytes, GC.CollectionCount(0) - g0, GC.CollectionCount(1) - g1, GC.CollectionCount(2) - g2);
    }

    // Frozen SOURCE-SEQUENCE oracle only, not an actual-A mutation experiment.
    private static void ReferenceReadSequence(Stream stream, Action afterLength)
    {
        var length = stream.Length;
        if (length > Array.MaxLength)
            throw new IOException("Original browser file cannot be represented by the existing byte reader.");
        afterLength();
        var bytes = new byte[(int)length]; stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw BrowserOriginalMainCondition.Invalid();
    }

    private static void SemanticControls(string root)
    {
        var files = Files(root);
        var actual = BindActualReader(files);
        var scope = new TrustedLocalFileScope([files.BasePath]);
        var relative = "controls/member.rollback.test";
        var path = files.ResolvePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var scratch = new byte[ScratchLength];
        var aDigests = 0; var bDigests = 0;
        string? A(string name) => ActualHash(actual, name, scope, () => aDigests++);
        string? B(string name) => PrototypeHash(files, name, scope, scratch, beforeDigest: () => bDigests++);
        foreach (var length in new[] { 0, 7, 65535, 65536, 65537 })
        {
            var content = Enumerable.Range(0, length).Select(x => (byte)(x * 131 + 17)).ToArray();
            File.WriteAllBytes(path, content);
            var expected = Convert.ToHexString(SHA256.HashData(content));
            Assert.Equal(expected, A(relative)); Assert.Equal(expected, B(relative));
            Assert.Equal(content, File.ReadAllBytes(path));
        }
        // Golden controls independently generated with Python hashlib in the frozen SHA filter.
        foreach (var (content, expected) in new (byte[], string)[]
        {
            ([], "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
            ([0, 1, 127, 128, 255, 16, 32], "1804B084980780FD19D518D8BFF867E69CFACC4F14F98D4CD757ADE9ABBFAEB8"),
            ([239, 187, 191, 97, 98], "E54DD095F92262CBAF1EF453DE08896FEE09647D82BE9433CC344752E643E43D"),
            ([240, 40, 140, 188, 192, 175, 0], "B0092EB2C379028748E10D9111849ACEEFE413135CAFADFCB497DE08A96BE290")
        }
        {
            File.WriteAllBytes(path, content);
            Assert.Equal(expected, A(relative)); Assert.Equal(expected, B(relative)); Assert.Equal(content, File.ReadAllBytes(path));
        }
        aDigests = bDigests = 0;
        Assert.Null(A("controls/missing.rollback.test")); Assert.Null(B("controls/missing.rollback.test"));
        using (var sparse = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            sparse.SetLength((long)Array.MaxLength + 1);
        Assert.Equal(Assert.Throws<IOException>(() => A(relative)).Message, Assert.Throws<IOException>(() => B(relative)).Message);
        Assert.Throws<InvalidDataException>(() => A("../outside")); Assert.Throws<InvalidDataException>(() => B("../outside"));
        File.Delete(path);
        Directory.CreateDirectory(path);
        Assert.Throws<InvalidDataException>(() => A(relative)); Assert.Throws<InvalidDataException>(() => B(relative));
        Directory.Delete(path);
        var target = Path.Combine(root, "target"); File.WriteAllBytes(target, [1, 2, 3]);
        File.CreateSymbolicLink(path, target);
        Assert.Throws<InvalidDataException>(() => A(relative)); Assert.Throws<InvalidDataException>(() => B(relative));
        File.Delete(path);
        var parent = files.ResolvePath("controls/block"); File.WriteAllBytes(parent, [1]);
        Assert.Throws<InvalidDataException>(() => A("controls/block/member.rollback.test"));
        Assert.Throws<InvalidDataException>(() => B("controls/block/member.rollback.test"));
        File.Delete(parent);
        Assert.Equal(0, aDigests); Assert.Equal(0, bDigests); // No final digest produced after any refusal or absence.

        foreach (var growth in new[] { false, true })
        {
            File.WriteAllBytes(path, [1, 2, 3]); bDigests = 0;
            using var reference = new MemoryStream(); reference.Write([1, 2, 3]); reference.Position = 0;
            var oracle = Record.Exception(() => ReferenceReadSequence(reference, () => reference.SetLength(growth ? 4 : 0)));
            var observed = Record.Exception(() => PrototypeHash(files, relative, scope, scratch, afterLength: _ =>
            {
                using var writer = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                writer.SetLength(growth ? 4 : 0);
            }, beforeDigest: () => bDigests++));
            Assert.NotNull(oracle); Assert.NotNull(observed); Assert.Equal(oracle.GetType(), observed.GetType());
            Assert.Equal(0, bDigests);
        }
        File.WriteAllBytes(path, [1, 2, 3]); bDigests = 0;
        try
        {
            Assert.Throws<InvalidDataException>(() => PrototypeHash(files, relative, scope, scratch,
                beforeFinalPath: () => { File.Delete(path); File.CreateSymbolicLink(path, target); }, beforeDigest: () => bDigests++));
            Assert.Equal(0, bDigests);
        }
        finally { File.Delete(path); }
        var movingParent = Path.GetDirectoryName(path)!; var movedParent = movingParent + ".moved";
        File.WriteAllBytes(path, [1, 2, 3]); bDigests = 0;
        try
        {
            Assert.Throws<InvalidDataException>(() => PrototypeHash(files, relative, scope, scratch,
                beforeFinalPath: () => { Directory.Move(movingParent, movedParent); Directory.CreateSymbolicLink(movingParent, movedParent); },
                beforeDigest: () => bDigests++));
            Assert.Equal(0, bDigests);
        }
        finally
        {
            if (Directory.Exists(movedParent)) { Directory.Delete(movingParent); Directory.Move(movedParent, movingParent); }
        }
    }

    [Fact]
    public void FiniteCheckedPathFilterUsesPinnedRollbackLayouts()
    {
        Assert.True(OperatingSystem.IsLinux(), "This bounded filter qualifies Linux only.");
        var own = Path.Combine(Path.GetTempPath(), "boe-checked-rollback-filter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(own);
        try
        {
            SemanticControls(Path.Combine(own, "controls"));
            using var rawFile = File.OpenRead(Path.Combine(TestRepoPaths.RepoRoot,
                "specs/1553-portable-local-storage/recovery/journal-fresh-cloud-20261011/performance-current-c5-baseline/chain/gc-bd38c05c2df3/result.json.gz"));
            using var compressed = new GZipStream(rawFile, CompressionMode.Decompress);
            using var buffer = new MemoryStream(); compressed.CopyTo(buffer); var raw = buffer.ToArray();
            Assert.Equal(DriverSha, Convert.ToHexString(SHA256.HashData(raw)));
            using var driver = JsonDocument.Parse(raw);
            var turns = driver.RootElement.GetProperty("TurnDurations").EnumerateArray().ToArray(); Assert.Equal(3, turns.Length);
            long[] sourceProofs = [5341, 5917, 5521], sourceReads = [331142, 402356, 375428], sourceBytes = [10168494896, 11306759798, 10550542664];
            var reports = new List<object>(); var total = Stopwatch.StartNew(); var complete = true;
            foreach (var turn in turns)
            {
                var index = turn.GetProperty("Turn").GetInt32() - 1;
                var files = Files(Path.Combine(own, "turn-" + (index + 1)));
                var actual = BindActualReader(files);
                var members = turn.GetProperty("Inventory").GetProperty("Rollback").EnumerateArray().Select((item, member) =>
                {
                    var relative = item.GetProperty("Path").GetString()!; var length = item.GetProperty("Bytes").GetInt32();
                    Assert.True(PendingTurnSnapshotAuthority.IsSafeRelativePath(relative));
                    var content = new byte[length];
                    for (var position = 0; position < length; position++) content[position] = (byte)(position * 131 + member * 17 + index);
                    var path = files.ResolvePath(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllBytes(path, content);
                    return new Member(relative, length, Convert.ToHexString(SHA256.HashData(content)));
                }).ToArray();
                Assert.Equal(sourceReads[index], sourceProofs[index] * members.Length);
                Assert.Equal(sourceBytes[index], sourceProofs[index] * members.Sum(x => (long)x.Bytes));
                var expected = members.Select(x => x.SyntheticSha256).ToArray(); var aResults = new string?[members.Length]; var bResults = new string?[members.Length];
                var lease = files.AcquireCanonicalWriteLeaseAsync().GetAwaiter().GetResult();
                var pairs = new List<Pair>();
                try
                {
                    Measure(files, actual, members, aResults, false, 4); Measure(files, actual, members, bResults, true, 4);
                    Assert.Equal(expected, aResults); Assert.Equal(expected, bResults);
                    for (var pair = 0; pair < PairCount; pair++)
                    {
                        Sample a, b;
                        if (pair % 2 == 0) { a = Measure(files, actual, members, aResults, false, Rounds); b = Measure(files, actual, members, bResults, true, Rounds); }
                        else { b = Measure(files, actual, members, bResults, true, Rounds); a = Measure(files, actual, members, aResults, false, Rounds); }
                        Assert.Equal(expected, aResults); Assert.Equal(expected, bResults);
                        pairs.Add(new(pair, pair % 2 == 0 ? "AB" : "BA", a, b, (a.Seconds - b.Seconds) / Rounds * sourceProofs[index]));
                        if (total.Elapsed.TotalSeconds >= 20) { complete = false; break; }
                    }
                }
                finally { lease.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                foreach (var member in members)
                {
                    var bytes = File.ReadAllBytes(files.ResolvePath(member.Path)); Assert.Equal(member.Bytes, bytes.Length);
                    Assert.Equal(member.SyntheticSha256, Convert.ToHexString(SHA256.HashData(bytes)));
                }
                reports.Add(new { Turn = index + 1, ActionId = turn.GetProperty("RequestId").GetString(), Members = members,
                    SourceWholeActionProofs = sourceProofs[index], SourceWholeActionRollbackReads = sourceReads[index], SourceWholeActionRollbackBytes = sourceBytes[index],
                    ObservedHelperToAcceptanceSeconds = turn.GetProperty("HelperToAcceptanceSeconds").GetDouble(), Rounds, Pairs = pairs });
                if (!complete) break;
            }
            output.WriteLine(JsonSerializer.Serialize(new { Complete = complete, ElapsedSeconds = total.Elapsed.TotalSeconds, DriverRawSha256 = DriverSha,
                Qualification = "Test-only warmed Linux checked-path hypothesis filter; synthetic identical content at exact observed rollback layouts/lengths. Actual A reader collector/hooks off. Both arms include fresh scopes and stream/provider construction/disposal; B64KiB scratch allocated per distribution pass. Provider SHA256.Create remains in both. GC counts process-wide; allocation managed same-thread; native/CPU unmeasured. No manual GC. Four warmup passes do not prove tiered-JIT completion. Source-sequence mutation oracle is not actual-A mutation validation. Streaming changes temporal read/hash order. Signed whole-action extrapolation assumes all savings critical-path, not rigorous bound or acceptance-window attribution. PASS means correctness only; incomplete capture cannot qualify optimization.", Turns = reports }));
        }
        finally { Directory.Delete(own, recursive: true); Assert.False(Directory.Exists(own)); }
    }
}
