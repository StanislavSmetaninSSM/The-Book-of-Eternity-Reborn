using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Existing publisher prerequisites; this is not the future dedicated-helper batch or client-reader proof.</summary>
public sealed class GmRelayPublicationFoundationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingPublisher_CutBeforeOrAfterCommitPreservesOneWholeDecision(bool committed)
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-relay-publication-" + Guid.NewGuid().ToString("N"));
        using var cleanup = new GmRelayLongSessionTests.OwnedRoot(root, output.WriteLine);
        var armed = false;
        var reached = 0;
        var phases = new List<string>();
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) =>
            {
                if (!armed) return;
                phases.Add(phase + ":" + index);
                if (phase == (committed ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished) &&
                    (committed || index == 1)) { reached++; throw new ControlledPublicationCut(); }
            }});
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            files.BootstrapLocalStorage(lease, null, JsonSerializer.SerializeToUtf8Bytes(new GameSettings()));
        string[] members = ["output/relay_member_a.json", "output/relay_member_b.json", "game_state/control/gm_relay_apply_receipt.json", "ready/turn_complete.json"];
        byte[] before = "{\"controlled\":\"before\"}"u8.ToArray();
        byte[] after = "{\"controlled\":\"after-not-game-acceptance\"}"u8.ToArray();
        foreach (var path in members.Take(2)) await files.WriteFileAtomicBytesAsync(path, before);
        TrustedLocalPublicationOutcome outcome;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            armed = true;
            try { outcome = await files.PublishLocalFilesAsync(lease, members.Select((path, index) => new CanonicalLocalFileChange(path, index < 2 ? before : null, after)).ToArray()); }
            finally { armed = false; }
        }
        Assert.Equal(1, reached);
        Assert.Equal(committed ? TrustedLocalPublicationDisposition.Committed : TrustedLocalPublicationDisposition.RolledBack, outcome.Disposition);
        var exactAtDecision = members.Select((path, index) => new
        {
            Path = path, Exists = File.Exists(files.ResolvePath(path)),
            Matches = File.Exists(files.ResolvePath(path)) && File.ReadAllBytes(files.ResolvePath(path)).AsSpan().SequenceEqual(committed ? after : before)
        }).ToArray();
        foreach (var row in exactAtDecision.Select((value, index) => (value, index)))
        {
            Assert.Equal(committed || row.index < 2, row.value.Exists);
            if (row.value.Exists) Assert.True(row.value.Matches);
        }
        // A fresh real admission settles retained cleanup debt; no hand-authored journal.
        await using (var recovery = await files.AcquireCanonicalWriteLeaseAsync()) { }
        Assert.False(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        output.WriteLine(JsonSerializer.Serialize(new { committed, reached, phases, ExactAtDecision = exactAtDecision, Scope = "EXISTING_REAL_PUBLISHER_NOT_HELPER_BATCH_OR_ACCEPTANCE" }));
    }

    [Theory]
    [InlineData("game_state/control/gm_prompt_delivery.json", false)]
    [InlineData("game_state/control/gm_relay_apply_receipt.json", false)]
    [InlineData("game_state/control/gm_prompt_delivery.json.neighbor", true)]
    [InlineData("game_state/control/gm_relay_apply_receipt.json.neighbor", true)]
    public void OperationalDraftExclusion_IsExactAndPreservesOrdinarySuffixNeighbors(string path, bool expected)
    {
        Assert.Equal(expected, SpiritualOriginalDraftInputs.IsDraftPath(path));
    }

    private sealed class ControlledPublicationCut : Exception { }
}
