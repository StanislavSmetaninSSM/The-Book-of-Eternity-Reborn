using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// One finite hypothesis filter, not a performance acceptance test or benchmark subsystem.
public sealed class ByteDigestObservedRollbackFilterTests(ITestOutputHelper output)
{
    private const string DriverRawSha256 = "A0D90E47D74AFEBE8C0F968A2DDC0C87AAB1BDF863AB7A2214D19170CA8A9BC9";
    private const int Rounds = 96, Pairs = 6;
    private sealed record Sample(double Seconds, long AllocatedBytes);
    private sealed record Pair(int Index, string Order, Sample A, Sample B, double OptimisticWholeActionSeconds);

    private static string ProposedDigest(byte[] content) => Convert.ToHexString(SHA256.HashData(content));

    private static Sample Measure(Func<byte[], string> digest, byte[][] inputs, string[] results, int rounds)
    {
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var round = 0; round < rounds; round++)
            for (var member = 0; member < inputs.Length; member++)
                results[member] = digest(inputs[member]);
        var seconds = (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
        return new(seconds, bytes);
    }

    [Fact]
    public void SameThreadFiniteFilterUsesPinnedObservedRollbackSizes()
    {
        // Digest controls are outside timing/allocation intervals. These golden
        // values were independently generated with Python hashlib SHA256.
        var controls = new (byte[] Input, string Expected)[]
        {
            ([], "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855"),
            ([0, 1, 127, 128, 255, 16, 32], "1804B084980780FD19D518D8BFF867E69CFACC4F14F98D4CD757ADE9ABBFAEB8"),
            ([239, 187, 191, 97, 98], "E54DD095F92262CBAF1EF453DE08896FEE09647D82BE9433CC344752E643E43D"),
            ([240, 40, 140, 188, 192, 175, 0], "B0092EB2C379028748E10D9111849ACEEFE413135CAFADFCB497DE08A96BE290")
        };
        foreach (var (input, expected) in controls)
        {
            var before = input.ToArray();
            Assert.Equal(expected, PendingTurnSnapshotAuthority.ComputeSha256(input));
            Assert.Equal(expected, ProposedDigest(input));
            Assert.Equal(before, input);
        }
        Assert.Throws<ArgumentNullException>(() => PendingTurnSnapshotAuthority.ComputeSha256((byte[])null!));
        Assert.Throws<ArgumentNullException>(() => ProposedDigest(null!));

        var path = Path.Combine(TestRepoPaths.RepoRoot,
            "specs/1553-portable-local-storage/recovery/journal-fresh-cloud-20261011/performance-current-c5-baseline/chain/gc-bd38c05c2df3/result.json.gz");
        using var file = File.OpenRead(path);
        using var compressed = new GZipStream(file, CompressionMode.Decompress);
        using var buffer = new MemoryStream();
        compressed.CopyTo(buffer);
        var raw = buffer.ToArray();
        Assert.Equal(DriverRawSha256, ProposedDigest(raw));
        using var driver = JsonDocument.Parse(raw);
        var turns = driver.RootElement.GetProperty("TurnDurations").EnumerateArray().ToArray();
        Assert.Equal(3, turns.Length);
        long[] sourceProofs = [5341, 5917, 5521];
        long[] sourceReads = [331142, 402356, 375428];
        long[] sourceBytes = [10168494896, 11306759798, 10550542664];
        Func<byte[], string> a = PendingTurnSnapshotAuthority.ComputeSha256, b = ProposedDigest;
        var report = new List<object>();
        var total = Stopwatch.StartNew();
        var complete = true;
        foreach (var turn in turns)
        {
            var index = turn.GetProperty("Turn").GetInt32() - 1;
            var inventory = turn.GetProperty("Inventory");
            var lengths = inventory.GetProperty("Rollback").EnumerateArray().Select(x => x.GetProperty("Bytes").GetInt32()).ToArray();
            Assert.Equal(sourceReads[index], sourceProofs[index] * lengths.Length);
            Assert.Equal(sourceBytes[index], sourceProofs[index] * lengths.Sum(x => (long)x));
            // Only lengths are observed. Content is explicitly synthetic, immutable
            // and identical between arms; no original artifact-content claim.
            var inputs = lengths.Select((length, member) =>
            {
                var bytes = new byte[length];
                for (var position = 0; position < length; position++)
                    bytes[position] = (byte)(position * 131 + member * 17 + index);
                return bytes;
            }).ToArray();
            var originals = inputs.Select(x => x.ToArray()).ToArray();
            var expected = inputs.Select(b).ToArray();
            var resultsA = new string[inputs.Length];
            var resultsB = new string[inputs.Length];
            Measure(a, inputs, resultsA, 16); Measure(b, inputs, resultsB, 16);
            var pairs = new List<Pair>();
            for (var pair = 0; pair < Pairs; pair++)
            {
                Sample measuredA, measuredB;
                if (pair % 2 == 0)
                { measuredA = Measure(a, inputs, resultsA, Rounds); measuredB = Measure(b, inputs, resultsB, Rounds); }
                else
                { measuredB = Measure(b, inputs, resultsB, Rounds); measuredA = Measure(a, inputs, resultsA, Rounds); }
                Assert.Equal(expected, resultsA); Assert.Equal(expected, resultsB);
                pairs.Add(new(pair, pair % 2 == 0 ? "AB" : "BA", measuredA, measuredB,
                    (measuredA.Seconds - measuredB.Seconds) / Rounds * sourceProofs[index]));
                if (total.Elapsed.TotalSeconds >= 20) { complete = false; break; }
            }
            for (var member = 0; member < inputs.Length; member++) Assert.Equal(originals[member], inputs[member]);
            report.Add(new
            {
                Turn = index + 1, ActionId = turn.GetProperty("RequestId").GetString(), ObservedLengths = lengths,
                SourceWholeActionProofs = sourceProofs[index], SourceWholeActionRollbackReads = sourceReads[index],
                SourceWholeActionRollbackBytes = sourceBytes[index],
                ObservedHelperToAcceptanceSeconds = turn.GetProperty("HelperToAcceptanceSeconds").GetDouble(),
                Rounds, Pairs = pairs
            });
            if (!complete) break;
        }
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Complete = complete, ElapsedSeconds = total.Elapsed.TotalSeconds, DriverRawSha256,
            Qualification = "Finite warmed same-thread byte-digest hypothesis filter, synthetic content at exact observed rollback lengths. A is unchanged product helper; B is proposed static byte SHA256 with identical uppercase hex results. No IO inside either measured arm; no GC.Collect. Counterbalanced AB/BA pairs report wall time and thread allocations. Whole-action count extrapolation is optimistic linear scope assuming every saving lies on the critical path, not a rigorous maximum or acceptance-window attribution. PASS means digest/refusal/input controls only, never latency improvement. HashData(byte[]) still allocates its digest buffer. Incomplete budget capture cannot qualify a candidate.",
            Turns = report
        }));
    }
}
