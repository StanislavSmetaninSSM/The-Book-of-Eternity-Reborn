using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundAcceptedTurnPlanCacheTests
{
    [Theory]
    [InlineData("event")]
    [InlineData("target")]
    [InlineData("roll")]
    [InlineData("snapshot")]
    [InlineData("generation")]
    public void RepairWave_ChangedSemanticAuthorityInvalidatesEveryPendingPacket(
        string changedAuthority)
    {
        var cache = new WoundAcceptedTurnPlanCache();
        var authority = CreateAuthority();
        var packet = CreatePacket();
        Assert.True(cache.TryRegisterRepairWave(authority, new[] { packet }));
        var changed = changedAuthority switch
        {
            "event" => authority with { EventFingerprint = Fingerprint('b') },
            "target" => authority with { TargetFingerprint = Fingerprint('d') },
            "roll" => authority with { RollFingerprint = Fingerprint('b') },
            "snapshot" => authority with { SnapshotToken = "snapshot_repair_changed" },
            "generation" => authority with { Generation = "generation_repair_changed" },
            _ => throw new ArgumentOutOfRangeException(nameof(changedAuthority))
        };

        Assert.False(cache.TryTakeRepairPacket(
            changed,
            packet.CreateReceipt(),
            out _));
        Assert.False(cache.HasRepairWave);
        Assert.False(cache.TryTakeRepairPacket(
            authority,
            packet.CreateReceipt(),
            out _));
    }

    [Theory]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("snapshot")]
    [InlineData("candidate")]
    [InlineData("semantic")]
    public void RepairWave_RequiresTheExactPacketReceipt(string changedField)
    {
        var cache = new WoundAcceptedTurnPlanCache();
        var authority = CreateAuthority();
        var packet = CreatePacket();
        Assert.True(cache.TryRegisterRepairWave(authority, new[] { packet }));
        var exact = packet.CreateReceipt();
        var changed = changedField switch
        {
            "session" => exact with { SessionId = "session_repair_changed" },
            "request" => exact with { RequestId = "request_repair_changed" },
            "snapshot" => exact with { SnapshotToken = "snapshot_repair_changed" },
            "candidate" => exact with { CandidateRef = "candidate_repair_changed" },
            "semantic" => exact with { SemanticFingerprint = Fingerprint('c') },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };

        Assert.False(cache.TryTakeRepairPacket(authority, changed, out _));
        Assert.False(cache.HasRepairWave);

        var replacement = CreatePacket();
        Assert.True(cache.TryRegisterRepairWave(authority, new[] { replacement }));
        Assert.True(cache.TryTakeRepairPacket(
            authority,
            replacement.CreateReceipt(),
            out var taken));
        Assert.Equal(replacement.CandidateRef, taken.CandidateRef);
    }

    [Fact]
    public void RepairWave_ExactReceiptConsumesOneCandidateExactlyOnce()
    {
        var cache = new WoundAcceptedTurnPlanCache();
        var authority = CreateAuthority();
        var first = CreatePacket("candidate_repair_001", Fingerprint('a'));
        var second = CreatePacket("candidate_repair_002", Fingerprint('b'));
        Assert.True(cache.TryRegisterRepairWave(authority, new[] { first, second }));

        Assert.True(cache.TryTakeRepairPacket(
            authority,
            first.CreateReceipt(),
            out var taken));
        Assert.Equal(first.CandidateRef, taken.CandidateRef);
        Assert.True(cache.HasRepairWave);
        Assert.False(cache.TryTakeRepairPacket(
            authority,
            first.CreateReceipt(),
            out _));

        Assert.True(cache.TryTakeRepairPacket(
            authority,
            second.CreateReceipt(),
            out var secondTaken));
        Assert.Equal(second.CandidateRef, secondTaken.CandidateRef);
        Assert.False(cache.HasRepairWave);
        Assert.False(cache.TryTakeRepairPacket(
            authority,
            second.CreateReceipt(),
            out _));
    }

    [Fact]
    public void RepairWave_InvalidateAllRevokesOutstandingReceiptsAndPreparedAuthorityTogether()
    {
        var cache = new WoundAcceptedTurnPlanCache();
        var authority = CreateAuthority();
        var packet = CreatePacket();
        Assert.True(cache.TryRegisterRepairWave(authority, new[] { packet }));

        cache.InvalidateAll();

        Assert.False(cache.HasRepairWave);
        Assert.False(cache.TryTakeRepairPacket(
            authority,
            packet.CreateReceipt(),
            out _));
        Assert.False(cache.HasPrepared);
        Assert.False(cache.HasFinal);
    }

    [Fact]
    public void RepairWave_RegisteringNewGenerationRevokesEveryReceiptFromThePriorWave()
    {
        var cache = new WoundAcceptedTurnPlanCache();
        var oldAuthority = CreateAuthority();
        var oldPacket = CreatePacket("candidate_repair_old", Fingerprint('a'));
        Assert.True(cache.TryRegisterRepairWave(oldAuthority, new[] { oldPacket }));

        var newAuthority = oldAuthority with
        {
            RequestId = "request_repair_new",
            SnapshotToken = "snapshot_repair_new",
            Generation = "generation_repair_new",
            EventFingerprint = Fingerprint('d'),
            TargetFingerprint = Fingerprint('e'),
            RollFingerprint = Fingerprint('f')
        };
        var newPacket = CreatePacket(
            "candidate_repair_new",
            Fingerprint('d'),
            newAuthority.SessionId,
            newAuthority.RequestId,
            newAuthority.SnapshotToken);
        Assert.True(cache.TryRegisterRepairWave(newAuthority, new[] { newPacket }));

        Assert.False(cache.TryTakeRepairPacket(
            oldAuthority,
            oldPacket.CreateReceipt(),
            out _));
        Assert.False(cache.HasRepairWave);

        Assert.True(cache.TryRegisterRepairWave(newAuthority, new[] { newPacket }));
        Assert.True(cache.TryTakeRepairPacket(
            newAuthority,
            newPacket.CreateReceipt(),
            out var taken));
        Assert.Equal("candidate_repair_new", taken.CandidateRef);
    }

    private static WoundRepairPacketAuthority CreateAuthority() => new(
        "session_repair_cache",
        "request_repair_cache",
        "snapshot_repair_cache",
        "generation_repair_cache",
        Fingerprint('a'),
        Fingerprint('b'),
        Fingerprint('c'));

    private static WoundRepairPacket CreatePacket(
        string candidateRef = "candidate_repair_001",
        string? semanticFingerprint = null,
        string sessionId = "session_repair_cache",
        string requestId = "request_repair_cache",
        string snapshotToken = "snapshot_repair_cache")
    {
        var issue = new ValidationIssue(
            "woundDecisions[0].proposal.severity",
            IssueSeverity.Error,
            "Severity exceeds the sealed opportunity.",
            code: "wound_severity_above_opportunity",
            section: "wound_materialization",
            expected: "validator-internal range",
            actual: "III");
        var candidate = new WoundRepairCandidateInput(
            "repair_wound",
            candidateRef,
            semanticFingerprint ?? Fingerprint('a'),
            "opportunity_repair_cache_001",
            new JsonObject
            {
                ["event"] = "осколок после обвала",
                ["target"] = "игрок",
                ["realm"] = "Смертный мир"
            },
            new[] { "none", "materialize" },
            "I",
            "II",
            new JsonObject
            {
                ["opportunityRef"] = "opportunity_repair_cache_001",
                ["decision"] = "materialize",
                ["woundRef"] = "local_wound_ref_repair_cache_001",
                ["proposal"] = new JsonObject
                {
                    ["classification"] = new JsonObject(),
                    ["display"] = new JsonObject
                    {
                        ["acquisitionNarration"] = "Осколок рассекает предплечье."
                    },
                    ["severity"] = "III",
                    ["complications"] = new JsonArray(),
                    ["consequenceDefinitions"] = new JsonArray(),
                    ["treatment"] = new JsonObject(),
                    ["recovery"] = new JsonObject()
                }
            },
            new[] { issue });
        return Assert.Single(WoundRepairPacketBuilder.Build(new WoundRepairBuildRequest(
            sessionId,
            requestId,
            snapshotToken,
            new[] { candidate })));
    }

    private static string Fingerprint(char value) =>
        "sha256:" + new string(value, 64);
}
