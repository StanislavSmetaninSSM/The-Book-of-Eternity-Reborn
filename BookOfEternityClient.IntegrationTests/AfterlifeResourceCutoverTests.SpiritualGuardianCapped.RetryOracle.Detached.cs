using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Reads both pinned immutable actual normal envelopes and runs detached probes without creating any live owner or new game fixture.</summary>
    [Fact]
    public void SpiritualGuardianCapped_ReadsActualRetryEnvelopesAndRejectsDetachedCorruptions()
    {
        var root = Environment.GetEnvironmentVariable("BOE_GUARDIAN_CAPPED_RETRY_ENVELOPE_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(root), "Set BOE_GUARDIAN_CAPPED_RETRY_ENVELOPE_ROOT to the external directory holding the two immutable Task5 full envelopes.");
        Assert.True(Path.IsPathFullyQualified(root!), "The external envelope root must be an absolute path.");
        foreach (var (rank, expectedHash) in new[]
        {
            (3, "9f4e38958f44cd840d0a777d9817fa6f3d5d62a4e667134ae5bbe96ea0940b51"),
            (4, "0f2ec9a4f07b953707d370d8aae22bebe1b79e2ed9e768591c82defca246880c")
        })
        {
            var path = Path.Combine(root!, "task5-r1-rank" + rank + "-complete-envelope.json");
            Assert.True(File.Exists(path), "Missing immutable actual capped envelope: " + path);
            var bytes = File.ReadAllBytes(path); var observedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            Assert.Equal(expectedHash, observedHash); var json = new UTF8Encoding(false, true).GetString(bytes);
            var fixture = ReadGuardianCappedRetryFixture(new GuardianCappedRetryDocument(json).Root.AsObject()); Assert.Equal(rank, fixture.Rank);
            var readerStarted = Stopwatch.GetTimestamp(); var first = ReadGuardianCappedRetryAttempt(fixture, json);
            var readerElapsed = Stopwatch.GetElapsedTime(readerStarted); var probesStarted = Stopwatch.GetTimestamp();
            var probes = AssertGuardianCappedRetryProbes(first, _storageOutput.WriteLine); var probesElapsed = Stopwatch.GetElapsedTime(probesStarted);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_retry_named_sections", rank, readerMilliseconds = readerElapsed.TotalMilliseconds, probesMilliseconds = probesElapsed.TotalMilliseconds, probes }));
            WriteGuardianCappedRetryMap("guardian_capped_actual_envelope_detached_gate", fixture.Name, first, probes);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_capped_actual_envelope_input_pin", rank, path, bytes = bytes.Length, observedHash,
                baselineEnvelopes = 1, probes, liveFixtures = 0, liveOwners = 0, lateHooks = 0 }));
        }
    }
}
