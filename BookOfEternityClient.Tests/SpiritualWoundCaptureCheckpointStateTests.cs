using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualWoundCaptureCheckpointStateTests
{
    private const string DraftPath = "game_state/meta/soul_state.json";

    [Fact]
    public void Parse_AcceptsClosedEmptyRootWithoutMintingCheckpoint()
    {
        var parse = typeof(SpiritualWoundCaptureCheckpointState).GetMethod(
            "Parse", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(parse);

        var result = parse!.Invoke(null,
            ["{\"schemaVersion\":1,\"checkpoint\":null}",
             SpiritualWoundCaptureCheckpointState.StatePath, Array.Empty<string>()]);

        Assert.NotNull(result);
        Assert.True((bool)result!.GetType().GetProperty("IsValid")!.GetValue(result)!);
        Assert.NotNull(result.GetType().GetProperty("State")!.GetValue(result));
    }

    [Fact]
    public void Parse_AcceptsClosedInitialCheckpointWithExactPresentDraftBytes()
    {
        var root = InitialCheckpoint();

        var result = SpiritualWoundCaptureCheckpointState.Parse(
            root.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.NotNull(result.State);
    }

    [Fact]
    public void SerializeCanonical_DetachesOriginalInputObjectAndPreservesExactBytes()
    {
        var root = InitialCheckpoint();
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(
            root.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);
        Assert.True(parsed.IsValid);
        var serialize = typeof(SpiritualWoundCaptureCheckpointState).GetMethod(
            "SerializeCanonical", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(serialize);

        root["checkpoint"]!["originalDraftImages"]![0]!["contentBase64"] = "dGFtcGVy";
        var saved = Assert.IsType<string>(serialize!.Invoke(null, [parsed.State]));

        Assert.Contains("\"contentBase64\":\"e30=\"", saved, StringComparison.Ordinal);
        Assert.DoesNotContain("dGFtcGVy", saved, StringComparison.Ordinal);
        Assert.Equal(saved, serialize.Invoke(null, [parsed.State]));
    }

    [Fact]
    public void PlanAdvance_AcceptsOneAppendedDecisionWithoutRewritingPriorEvidence()
    {
        var initial = SpiritualWoundCaptureCheckpointState.Parse(
            InitialCheckpoint().ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);
        var advancedRoot = InitialCheckpoint();
        AddAdvance(advancedRoot);
        var advanced = SpiritualWoundCaptureCheckpointState.Parse(
            advancedRoot.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);
        Assert.True(initial.IsValid);
        Assert.True(advanced.IsValid);
        var plan = typeof(SpiritualWoundCaptureCheckpointState).GetMethod(
            "PlanAdvance", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(plan);

        var result = plan!.Invoke(null, [initial.State, advanced.State]);

        Assert.NotNull(result);
        Assert.Equal("advanced", result!.GetType().GetProperty("Disposition")!.GetValue(result));
        Assert.NotNull(result.GetType().GetProperty("After")!.GetValue(result));
    }

    [Fact]
    public void PlanAdvance_RejectsSkippedCommittedGenerationWithoutThrowing()
    {
        var initial = ParseValid(InitialCheckpoint());
        var candidateRoot = InitialCheckpoint();
        AddAdvance(candidateRoot);
        AddSecondAdvance(candidateRoot);
        var candidate = ParseValid(candidateRoot);

        var result = SpiritualWoundCaptureCheckpointState.PlanAdvance(initial, candidate);

        Assert.Equal("conflict", result.Disposition);
        Assert.Null(result.After);
    }

    [Theory]
    [InlineData("original_draft")]
    [InlineData("prior_decision")]
    [InlineData("journal_prefix")]
    public void PlanAdvance_RejectsRewrittenCommittedPrefix(string defect)
    {
        var beforeRoot = InitialCheckpoint();
        if (defect == "journal_prefix") AddInitialClock(beforeRoot, "2026-09-23T00:00:00.0000000+00:00");
        AddAdvance(beforeRoot);
        var before = ParseValid(beforeRoot);
        var candidateRoot = beforeRoot.DeepClone().AsObject();
        AddSecondAdvance(candidateRoot);
        var checkpoint = candidateRoot["checkpoint"]!.AsObject();
        switch (defect)
        {
            case "original_draft":
                var bytes = Encoding.UTF8.GetBytes("{\"changed\":true}");
                var image = checkpoint["originalDraftImages"]![0]!.AsObject();
                image["contentBase64"] = Convert.ToBase64String(bytes);
                image["contentFingerprint"] = new CanonicalBeforeImage(true, bytes).Fingerprint;
                break;
            case "prior_decision":
                checkpoint["advances"]![0]!["newDecisionFingerprints"]![0] =
                    "sha256:" + new string('9', 64);
                break;
            case "journal_prefix":
                checkpoint["allocations"]![0]!["value"] = "2026-09-23T00:00:01.0000000+00:00";
                break;
            default: throw new InvalidOperationException(defect);
        }
        Rehash(checkpoint);
        var candidate = ParseValid(candidateRoot);

        var result = SpiritualWoundCaptureCheckpointState.PlanAdvance(before, candidate);

        Assert.Equal("conflict", result.Disposition);
        Assert.Null(result.After);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parse_DistinguishesOriginallyAbsentFromPresentEmptyDraft(bool existed)
    {
        var root = InitialCheckpoint();
        var checkpoint = root["checkpoint"]!.AsObject();
        var image = checkpoint["originalDraftImages"]![0]!.AsObject();
        image["existed"] = existed;
        image["contentBase64"] = existed ? "" : null;
        image["contentFingerprint"] = new CanonicalBeforeImage(
            existed, existed ? Array.Empty<byte>() : null).Fingerprint;
        Rehash(checkpoint);

        var parsed = SpiritualWoundCaptureCheckpointState.Parse(
            root.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);

        Assert.True(parsed.IsValid);
        var serialized = SpiritualWoundCaptureCheckpointState.SerializeCanonical(parsed.State!);
        Assert.Contains(existed ? "\"contentBase64\":\"\"" : "\"contentBase64\":null",
            serialized, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("extra_root")]
    [InlineData("extra_checkpoint")]
    [InlineData("missing_image")]
    [InlineData("aliased_image")]
    [InlineData("unsorted_witnesses")]
    [InlineData("absent_with_bytes")]
    [InlineData("bad_base64")]
    [InlineData("bad_image_digest")]
    [InlineData("bad_snapshot_digest")]
    [InlineData("bad_journal_count")]
    [InlineData("bad_advance_count")]
    [InlineData("bad_checkpoint_digest")]
    public void Parse_RejectsRehashedMalformedCheckpoint(string defect)
    {
        var root = InitialCheckpoint();
        var checkpoint = root["checkpoint"]!.AsObject();
        var image = checkpoint["originalDraftImages"]![0]!.AsObject();
        switch (defect)
        {
            case "extra_root": root["unexpected"] = 1; break;
            case "extra_checkpoint": checkpoint["unexpected"] = 1; break;
            case "missing_image": checkpoint["originalDraftImages"] = new JsonArray(); break;
            case "aliased_image": image["path"] = "GAME_STATE/meta/soul_state.json"; break;
            case "unsorted_witnesses":
                var witnesses = checkpoint["physicalWitnesses"]!.AsArray();
                var first = witnesses[0]!.DeepClone();
                witnesses[0] = witnesses[1]!.DeepClone();
                witnesses[1] = first;
                break;
            case "absent_with_bytes": image["existed"] = false; break;
            case "bad_base64": image["contentBase64"] = "e30= "; break;
            case "bad_image_digest": image["contentFingerprint"] = "sha256:" + new string('0', 64); break;
            case "bad_snapshot_digest": checkpoint["originalSnapshotFingerprint"] =
                "sha256:" + new string('1', 64); break;
            case "bad_journal_count": checkpoint["initialAllocationCount"] = 1; break;
            case "bad_advance_count": checkpoint["committedAdvance"] = 1; break;
            case "bad_checkpoint_digest": checkpoint["checkpointFingerprint"] =
                "sha256:" + new string('0', 64); break;
            default: throw new InvalidOperationException(defect);
        }
        if (defect != "bad_checkpoint_digest") Rehash(checkpoint);

        var parsed = SpiritualWoundCaptureCheckpointState.Parse(
            root.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.State);
        Assert.Single(parsed.Issues);
    }

    [Fact]
    public void Parse_RejectsDuplicateJsonPropertyBeforeNodeConversion()
    {
        var duplicate = "{\"schemaVersion\":1,\"checkpoint\":null,\"checkpoint\":null}";

        var parsed = SpiritualWoundCaptureCheckpointState.Parse(
            duplicate, SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.State);
    }

    [Fact]
    public void Parse_RejectsRepeatedDecisionFingerprintWithinOneAdvance()
    {
        var root = InitialCheckpoint();
        AddAdvance(root);
        var checkpoint = root["checkpoint"]!.AsObject();
        var decisions = checkpoint["advances"]![0]!["newDecisionFingerprints"]!.AsArray();
        decisions.Add(decisions[0]!.DeepClone());
        Rehash(checkpoint);

        var parsed = SpiritualWoundCaptureCheckpointState.Parse(
            root.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.State);
    }

    [Fact]
    public void Parse_RejectsReorderedAdvanceRowsAfterDigestRepair()
    {
        var root = InitialCheckpoint();
        AddAdvance(root);
        AddSecondAdvance(root);
        var checkpoint = root["checkpoint"]!.AsObject();
        var advances = checkpoint["advances"]!.AsArray();
        var first = advances[0]!.DeepClone();
        advances[0] = advances[1]!.DeepClone();
        advances[1] = first;
        Rehash(checkpoint);

        var parsed = SpiritualWoundCaptureCheckpointState.Parse(
            root.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.State);
    }

    [Fact]
    public void Parse_RejectsAdvanceAllocationBoundaryBeyondRetainedJournal()
    {
        var root = InitialCheckpoint();
        AddAdvance(root);
        var checkpoint = root["checkpoint"]!.AsObject();
        checkpoint["advances"]![0]!["allocationCount"] = 1;
        Rehash(checkpoint);

        var parsed = SpiritualWoundCaptureCheckpointState.Parse(
            root.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);

        Assert.False(parsed.IsValid);
        Assert.Null(parsed.State);
    }

    /// <summary>
    /// Exports an exact defensive draft view and original identity from parsed checkpoint data.
    /// </summary>
    [Fact]
    public void ReadOriginalDraftInputs_ReturnsDetachedExactOriginIncludingIdentity()
    {
        var root = InitialCheckpoint();
        var checkpoint = ParseValid(root);
        var read = typeof(SpiritualWoundCaptureCheckpointState).GetMethod(
            "ReadOriginalDraftInputs", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(read);

        root["checkpoint"]!["originalDraftImages"]![0]!["contentBase64"] = "dGFtcGVy";
        var view = Assert.IsType<SpiritualOriginalDraftInputs>(read!.Invoke(checkpoint, null));
        var image = view.ReadImage(DraftPath);

        Assert.True(view.MatchesIdentity("checkpoint-session", "checkpoint-request", new string('A', 64), 4));
        Assert.Equal([DraftPath], view.PathInventory);
        Assert.True(image.Existed);
        Assert.Equal(Encoding.UTF8.GetBytes("{}"), image.Bytes);
    }

    /// <summary>
    /// Exports only the three immutable physical witness fingerprints as a read-only map.
    /// </summary>
    [Fact]
    public void ReadOriginalPhysicalWitnessFingerprints_ReturnsOnlyThreeImmutableWitnesses()
    {
        var checkpoint = ParseValid(InitialCheckpoint());
        var read = typeof(SpiritualWoundCaptureCheckpointState).GetMethod(
            "ReadOriginalPhysicalWitnessFingerprints", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(read);

        var witnesses = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(
            read!.Invoke(checkpoint, null));

        Assert.Equal(
            ["game_state/control/pending_turn_snapshot.authority.json",
             "game_state/control/pending_turn_snapshot.json",
             "input/turn_request.json"],
            witnesses.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        Assert.All(witnesses.Values,
            fingerprint => Assert.Equal(new CanonicalBeforeImage(true, Array.Empty<byte>()).Fingerprint,
                fingerprint));
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string>)witnesses).Add("ready/turn_complete.json", "forged"));
    }

    /// <summary>
    /// Keeps a missing draft image distinct from an existing empty image after export.
    /// </summary>
    /// <param name="existed">
    /// Whether the original draft path existed with empty content.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadOriginalDraftInputs_PreservesAbsenceVersusPresentEmpty(bool existed)
    {
        var root = InitialCheckpoint();
        var checkpoint = root["checkpoint"]!.AsObject();
        var image = checkpoint["originalDraftImages"]![0]!.AsObject();
        image["existed"] = existed;
        image["contentBase64"] = existed ? "" : null;
        image["contentFingerprint"] = new CanonicalBeforeImage(
            existed, existed ? Array.Empty<byte>() : null).Fingerprint;
        Rehash(checkpoint);

        var view = ParseValid(root).ReadOriginalDraftInputs();
        var restored = view.ReadImage(DraftPath);

        Assert.Equal(existed, restored.Existed);
        Assert.Equal(existed ? Array.Empty<byte>() : null, restored.Bytes);
        Assert.Equal(existed ? "" : null, view.ReadText(DraftPath));
    }

    private static JsonObject InitialCheckpoint()
    {
        var draftBytes = Encoding.UTF8.GetBytes("{}");
        var emptyWitnessFingerprint = new CanonicalBeforeImage(true, Array.Empty<byte>()).Fingerprint;
        var packetFingerprint = "sha256:" + new string('b', 64);
        var checkpoint = new JsonObject
        {
            ["sessionId"] = "checkpoint-session",
            ["requestId"] = "checkpoint-request",
            ["snapshotToken"] = new string('A', 64),
            ["turn"] = 4,
            ["realm"] = "chaos_sea",
            ["originalSnapshotFingerprint"] = "sha256:" + new string('a', 64),
            ["originalDraftImages"] = new JsonArray
            {
                new JsonObject
                {
                    ["path"] = DraftPath,
                    ["existed"] = true,
                    ["contentBase64"] = Convert.ToBase64String(draftBytes),
                    ["contentFingerprint"] = new CanonicalBeforeImage(true, draftBytes).Fingerprint
                }
            },
            ["physicalWitnesses"] = new JsonArray
            {
                Witness("game_state/control/pending_turn_snapshot.authority.json"),
                Witness("game_state/control/pending_turn_snapshot.json"),
                Witness("input/turn_request.json")
            },
            ["initialAllocationCount"] = 0,
            ["initialPendingPacketFingerprint"] = packetFingerprint,
            ["advances"] = new JsonArray(),
            ["committedAdvance"] = 0,
            ["allocations"] = new JsonArray(),
            ["expectedPendingPacketFingerprint"] = packetFingerprint,
            ["checkpointFingerprint"] = ""
        };
        checkpoint["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(
            checkpoint, "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        return new JsonObject { ["schemaVersion"] = 1, ["checkpoint"] = checkpoint };

        JsonObject Witness(string path) => new()
        {
            ["path"] = path,
            ["existed"] = true,
            ["contentFingerprint"] = emptyWitnessFingerprint
        };
    }

    private static void AddAdvance(JsonObject root)
    {
        var checkpoint = root["checkpoint"]!.AsObject();
        var prior = checkpoint["initialPendingPacketFingerprint"]!.GetValue<string>();
        var after = "sha256:" + new string('d', 64);
        var changedBytes = Encoding.UTF8.GetBytes("{\"decision\":1}");
        checkpoint["advances"]!.AsArray().Add(new JsonObject
        {
            ["ordinal"] = 1,
            ["priorPendingPacketFingerprint"] = prior,
            ["inputChanges"] = new JsonArray
            {
                new JsonObject
                {
                    ["path"] = DraftPath,
                    ["existed"] = true,
                    ["contentBase64"] = Convert.ToBase64String(changedBytes),
                    ["contentFingerprint"] = new CanonicalBeforeImage(true, changedBytes).Fingerprint
                }
            },
            ["newDecisionFingerprints"] = new JsonArray("sha256:" + new string('c', 64)),
            ["allocationCount"] = checkpoint["allocations"]!.AsArray().Count,
            ["resultPendingPacketFingerprint"] = after
        });
        checkpoint["committedAdvance"] = 1;
        checkpoint["expectedPendingPacketFingerprint"] = after;
        Rehash(checkpoint);
    }

    private static void AddSecondAdvance(JsonObject root)
    {
        var checkpoint = root["checkpoint"]!.AsObject();
        checkpoint["advances"]!.AsArray().Add(new JsonObject
        {
            ["ordinal"] = 2,
            ["priorPendingPacketFingerprint"] = "sha256:" + new string('d', 64),
            ["inputChanges"] = new JsonArray(),
            ["newDecisionFingerprints"] = new JsonArray("sha256:" + new string('e', 64)),
            ["allocationCount"] = checkpoint["allocations"]!.AsArray().Count,
            ["resultPendingPacketFingerprint"] = "sha256:" + new string('f', 64)
        });
        checkpoint["committedAdvance"] = 2;
        checkpoint["expectedPendingPacketFingerprint"] = "sha256:" + new string('f', 64);
        Rehash(checkpoint);
    }

    private static void AddInitialClock(JsonObject root, string value)
    {
        var checkpoint = root["checkpoint"]!.AsObject();
        checkpoint["allocations"]!.AsArray().Add(new JsonObject
        {
            ["ordinal"] = 0,
            ["kind"] = "utc_time",
            ["owner"] = "test-owner",
            ["coordinate"] = "first-clock",
            ["value"] = value
        });
        checkpoint["initialAllocationCount"] = 1;
        Rehash(checkpoint);
    }

    private static void Rehash(JsonObject checkpoint) =>
        checkpoint["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(
            checkpoint, "spiritual_capture_checkpoint_v1", "checkpointFingerprint");

    private static SpiritualWoundCaptureCheckpointState ParseValid(JsonObject root)
    {
        var parsed = SpiritualWoundCaptureCheckpointState.Parse(
            root.ToJsonString(), SpiritualWoundCaptureCheckpointState.StatePath, [DraftPath]);
        Assert.True(parsed.IsValid);
        return Assert.IsType<SpiritualWoundCaptureCheckpointState>(parsed.State);
    }
}
