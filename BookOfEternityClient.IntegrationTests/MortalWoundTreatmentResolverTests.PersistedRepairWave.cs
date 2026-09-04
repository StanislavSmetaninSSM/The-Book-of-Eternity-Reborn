using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public void PersistedRepairWaveValidator_AcceptsCanonicalBuilderProjection()
    {
        var wave = CreatePersistedRepairWave();

        Assert.True(IsValidPersistedRepairWave(wave));
    }

    [Fact]
    public void PersistedRepairWaveValidator_RequiresOneToSixtyFourPairedRows()
    {
        var wave = CreatePersistedRepairWave();
        var noPackets = wave with { Packets = new JsonArray() };
        var noReceipts = wave with { Receipts = new JsonArray() };

        Assert.False(IsValidPersistedRepairWave(noPackets));
        Assert.False(IsValidPersistedRepairWave(noReceipts));
        Assert.False(IsValidPersistedRepairWave(new PersistedRepairWave(
            new JsonArray(),
            new JsonArray(),
            wave.SessionId,
            wave.RequestId,
            wave.SnapshotToken)));

        var packetTemplate = Assert.IsType<JsonObject>(wave.Packets[0]);
        var receiptTemplate = Assert.IsType<JsonObject>(wave.Receipts[0]);
        var packets = new JsonArray();
        var receipts = new JsonArray();
        for (var index = 0; index < 65; index++)
        {
            var candidateRef = $"candidate_persisted_limit_{index:D3}";
            var packet = packetTemplate.DeepClone().AsObject();
            var receipt = receiptTemplate.DeepClone().AsObject();
            packet["candidateRef"] = candidateRef;
            receipt["candidateRef"] = candidateRef;
            packets.Add(packet);
            receipts.Add(receipt);
        }

        Assert.False(IsValidPersistedRepairWave(wave with
        {
            Packets = packets,
            Receipts = receipts
        }));
    }

    [Theory]
    [InlineData("packet_unknown")]
    [InlineData("packet_missing")]
    [InlineData("receipt_unknown")]
    [InlineData("receipt_missing")]
    public void PersistedRepairWaveValidator_RequiresClosedPacketAndReceiptRows(
        string axis)
    {
        var wave = CreatePersistedRepairWave();
        var packet = Assert.IsType<JsonObject>(wave.Packets[0]);
        var receipt = Assert.IsType<JsonObject>(wave.Receipts[0]);
        switch (axis)
        {
            case "packet_unknown":
                packet["unexpected"] = true;
                break;
            case "packet_missing":
                Assert.True(packet.Remove("kind"));
                break;
            case "receipt_unknown":
                receipt["unexpected"] = true;
                break;
            case "receipt_missing":
                Assert.True(receipt.Remove("semanticFingerprint"));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        Assert.False(IsValidPersistedRepairWave(wave));
    }

    [Fact]
    public void PersistedRepairWaveValidator_RejectsRecursiveDuplicateProperties()
    {
        var wave = CreatePersistedRepairWave();
        var packetJson = wave.Packets[0]!.ToJsonString();
        const string safeContextPrefix = "\"safeContext\":{";
        Assert.Contains(safeContextPrefix, packetJson, StringComparison.Ordinal);
        packetJson = packetJson.Replace(
            safeContextPrefix,
            safeContextPrefix + "\"event\":\"duplicate\",",
            StringComparison.Ordinal);
        using var packetDocument = JsonDocument.Parse("[" + packetJson + "]");

        Assert.False(WoundRepairPacketBuilder.IsValidPersistedRepairWave(
            packetDocument.RootElement,
            JsonSerializer.SerializeToElement(wave.Receipts),
            wave.SessionId,
            wave.RequestId,
            wave.SnapshotToken));
    }

    [Theory]
    [InlineData("packet_session")]
    [InlineData("packet_request")]
    [InlineData("packet_snapshot")]
    [InlineData("receipt_session")]
    [InlineData("receipt_request")]
    [InlineData("receipt_snapshot")]
    [InlineData("receipt_candidate")]
    [InlineData("receipt_semantic")]
    public void PersistedRepairWaveValidator_RequiresRootAndPositionalAgreement(
        string axis)
    {
        var wave = CreatePersistedRepairWave();
        var packet = Assert.IsType<JsonObject>(wave.Packets[0]);
        var receipt = Assert.IsType<JsonObject>(wave.Receipts[0]);
        switch (axis)
        {
            case "packet_session":
                packet["sessionId"] = "foreign_session";
                break;
            case "packet_request":
                packet["requestId"] = "foreign_request";
                break;
            case "packet_snapshot":
                packet["snapshotToken"] = "foreign_snapshot";
                break;
            case "receipt_session":
                receipt["sessionId"] = "foreign_session";
                break;
            case "receipt_request":
                receipt["requestId"] = "foreign_request";
                break;
            case "receipt_snapshot":
                receipt["snapshotToken"] = "foreign_snapshot";
                break;
            case "receipt_candidate":
                receipt["candidateRef"] = "foreign_candidate";
                break;
            case "receipt_semantic":
                receipt["semanticFingerprint"] = RepairWaveFingerprint('f');
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        Assert.False(IsValidPersistedRepairWave(wave));
    }

    [Fact]
    public void PersistedRepairWaveValidator_RequiresExactPositionalReceiptOrder()
    {
        var wave = CreatePersistedRepairWave(
            CreatePersistedRepairPacket(
                "candidate_persisted_order_A",
                RepairWaveFingerprint('a')),
            CreatePersistedRepairPacket(
                "candidate_persisted_order_B",
                RepairWaveFingerprint('b')));
        var first = wave.Receipts[0]!.DeepClone();
        var second = wave.Receipts[1]!.DeepClone();
        wave.Receipts[0] = second;
        wave.Receipts[1] = first;

        Assert.False(IsValidPersistedRepairWave(wave));
    }

    [Fact]
    public void PersistedRepairWaveValidator_RejectsExactAndConfusableCandidateRefs()
    {
        var exact = CreatePersistedRepairWave(
            CreatePersistedRepairPacket(
                "candidate_persisted_duplicate",
                RepairWaveFingerprint('a')),
            CreatePersistedRepairPacket(
                "candidate_persisted_duplicate",
                RepairWaveFingerprint('b')));
        var confusable = CreatePersistedRepairWave(
            CreatePersistedRepairPacket(
                "candidate_persisted_A",
                RepairWaveFingerprint('a')),
            CreatePersistedRepairPacket(
                "candidate_persisted_А",
                RepairWaveFingerprint('b')));

        Assert.False(IsValidPersistedRepairWave(exact));
        Assert.False(IsValidPersistedRepairWave(confusable));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("unknown_field")]
    [InlineData("missing_field")]
    [InlineData("invalid_path")]
    [InlineData("invalid_code")]
    [InlineData("invalid_expected")]
    [InlineData("oversized_actual")]
    public void PersistedRepairWaveValidator_RequiresCanonicalIssueProjection(
        string axis)
    {
        var wave = CreatePersistedRepairWave();
        var packet = Assert.IsType<JsonObject>(wave.Packets[0]);
        var issues = Assert.IsType<JsonArray>(packet["issues"]);
        var issue = Assert.IsType<JsonObject>(issues[0]);
        switch (axis)
        {
            case "empty":
                issues.Clear();
                break;
            case "unknown_field":
                issue["unexpected"] = true;
                break;
            case "missing_field":
                Assert.True(issue.Remove("actual"));
                break;
            case "invalid_path":
                issue["path"] = "proposal.ownerId";
                break;
            case "invalid_code":
                issue["code"] = "foreign_issue";
                break;
            case "invalid_expected":
                issue["expected"] = "accept anything";
                break;
            case "oversized_actual":
                issue["actual"] = new string('x', 513);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        Assert.False(IsValidPersistedRepairWave(wave));
    }

    [Theory]
    [InlineData("unknown_field")]
    [InlineData("missing_field")]
    [InlineData("non_string")]
    [InlineData("oversized")]
    public void PersistedRepairWaveValidator_RequiresClosedReadableSafeContext(
        string axis)
    {
        var wave = CreatePersistedRepairWave();
        var context = Assert.IsType<JsonObject>(
            Assert.IsType<JsonObject>(wave.Packets[0])["safeContext"]);
        switch (axis)
        {
            case "unknown_field":
                context["ownerId"] = "private_owner";
                break;
            case "missing_field":
                Assert.True(context.Remove("realm"));
                break;
            case "non_string":
                context["target"] = 17;
                break;
            case "oversized":
                context["event"] = new string('x', 2_049);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        Assert.False(IsValidPersistedRepairWave(wave));
    }

    [Theory]
    [InlineData("shape_unknown")]
    [InlineData("response_changed")]
    [InlineData("decision_count")]
    [InlineData("proposal_base")]
    [InlineData("correct_only_mismatch")]
    public void PersistedRepairWaveValidator_RequiresCanonicalResponseShape(
        string axis)
    {
        var wave = CreatePersistedRepairWave();
        var packet = Assert.IsType<JsonObject>(wave.Packets[0]);
        var shape = Assert.IsType<JsonObject>(packet["requiredResponseShape"]);
        var decisions = Assert.IsType<JsonArray>(shape["woundDecisions"]);
        var decision = Assert.IsType<JsonObject>(decisions[0]);
        var proposal = Assert.IsType<JsonObject>(decision["proposal"]);
        switch (axis)
        {
            case "shape_unknown":
                shape["unexpected"] = true;
                break;
            case "response_changed":
                shape["response"] = "free-form response";
                break;
            case "decision_count":
                decisions.Add(decision.DeepClone());
                break;
            case "proposal_base":
                proposal["base"] = "foreignProposal";
                break;
            case "correct_only_mismatch":
                proposal["correctOnly"] = new JsonArray("proposal.display.name");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        Assert.False(IsValidPersistedRepairWave(wave));
    }

    [Theory]
    [InlineData("non_object")]
    [InlineData("unknown_root_field")]
    [InlineData("sensitive_nested_field")]
    public void PersistedRepairWaveValidator_RequiresSanitizedPreservedProposal(
        string axis)
    {
        var wave = CreatePersistedRepairWave();
        var packet = Assert.IsType<JsonObject>(wave.Packets[0]);
        var proposal = Assert.IsType<JsonObject>(packet["preservedProposal"]);
        switch (axis)
        {
            case "non_object":
                packet["preservedProposal"] = "proposal";
                break;
            case "unknown_root_field":
                proposal["owner"] = new JsonObject();
                break;
            case "sensitive_nested_field":
                proposal["display"]!["ownerId"] = "private_owner";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
        }

        Assert.False(IsValidPersistedRepairWave(wave));
    }

    private static bool IsValidPersistedRepairWave(PersistedRepairWave wave) =>
        WoundRepairPacketBuilder.IsValidPersistedRepairWave(
            JsonSerializer.SerializeToElement(wave.Packets),
            JsonSerializer.SerializeToElement(wave.Receipts),
            wave.SessionId,
            wave.RequestId,
            wave.SnapshotToken);

    private static PersistedRepairWave CreatePersistedRepairWave(
        params WoundRepairPacket[] packets)
    {
        if (packets.Length == 0)
            packets = new[] { CreatePersistedRepairPacket() };
        var packetArray = new JsonArray(packets
            .Select(static packet => (JsonNode?)packet.ToJsonObject())
            .ToArray());
        var receiptArray = new JsonArray(packets
            .Select(static packet => packet.CreateReceipt())
            .Select(static receipt => (JsonNode?)new JsonObject
            {
                ["sessionId"] = receipt.SessionId,
                ["requestId"] = receipt.RequestId,
                ["snapshotToken"] = receipt.SnapshotToken,
                ["candidateRef"] = receipt.CandidateRef,
                ["semanticFingerprint"] = receipt.SemanticFingerprint
            })
            .ToArray());
        return new PersistedRepairWave(
            packetArray,
            receiptArray,
            packets[0].SessionId,
            packets[0].RequestId,
            packets[0].SnapshotToken);
    }

    private static WoundRepairPacket CreatePersistedRepairPacket(
        string candidateRef = "candidate_persisted_repair_001",
        string? semanticFingerprint = null)
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
            semanticFingerprint ?? RepairWaveFingerprint('a'),
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
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(new WoundRepairBuildRequest(
            "session_persisted_repair",
            "request_persisted_repair",
            "snapshot_persisted_repair",
            new[] { candidate })));
        Assert.Equal("wound_materialization_repair", packet.Kind);
        return packet;
    }

    private static string RepairWaveFingerprint(char value) =>
        "sha256:" + new string(value, 64);

    private sealed record PersistedRepairWave(
        JsonArray Packets,
        JsonArray Receipts,
        string SessionId,
        string RequestId,
        string SnapshotToken);
}
