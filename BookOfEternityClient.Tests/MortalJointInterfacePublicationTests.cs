using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalJointInterfacePublicationTests
{
    [Fact]
    public void PublishedRecordPreservesExactDiagnosticsWithoutClaimingHistoricalReturn()
    {
        var issues = new[]
        {
            new ValidationIssue("output/interface_updates.json", IssueSeverity.Warning, "warning", null,
                actor: "GM", section: "InterfaceUpdates", expected: null, actual: "", repairHint: "hint",
                category: IssueCategory.ProtocolViolation, repairTargetFiles: ["output/interface_updates.json"]),
            new ValidationIssue("output/interface_updates.json", IssueSeverity.Warning, "warning", null,
                actor: "GM", section: "InterfaceUpdates", expected: null, actual: "", repairHint: "hint",
                category: IssueCategory.ProtocolViolation, repairTargetFiles: ["output/interface_updates.json"])
        };
        var prepared = Prepared(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("{}\r\n")).ToArray(), issues);
        var published = prepared.AsPublished();
        var reread = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(published.ToJson()));
        Assert.Equal("Published", reread.State);
        Assert.Equal(prepared.ResultFingerprint, reread.ResultFingerprint);
        Assert.NotEqual(prepared.Fingerprint, reread.Fingerprint);
        Assert.Equal(prepared.BeforeBytes, reread.AfterBytes);
        Assert.Equal(2, reread.Issues.Count);
        foreach (var issue in reread.Issues)
        {
            Assert.Null(issue.Code); Assert.Null(issue.Expected); Assert.Equal("", issue.Actual);
            Assert.Equal("GM", issue.Actor); Assert.Equal("hint", issue.RepairHint);
            Assert.Equal(IssueSeverity.Warning, issue.Severity);
            Assert.Equal(IssueCategory.ProtocolViolation, issue.Category);
            Assert.Equal(new[] { "output/interface_updates.json" }, issue.RepairTargetFiles);
        }
        Assert.DoesNotContain("Returned", reread.ToJson().ToJsonString(), StringComparison.Ordinal);
        Assert.DoesNotContain("publicReturn", reread.ToJson().ToJsonString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("extra")]
    [InlineData("image")]
    [InlineData("targets-blank")]
    [InlineData("targets-duplicate")]
    [InlineData("issue-null-targets")]
    public void StrictReaderRefusesMalformedOrSilentlyCanonicalizedData(string corruption)
    {
        var root = Prepared(Encoding.UTF8.GetBytes("{}"), [new ValidationIssue("i", IssueSeverity.Warning, "m")]).ToJson();
        switch (corruption)
        {
            case "version": root["version"] = 2; break;
            case "extra": root["extra"] = true; break;
            case "image": root["beforeImage"]!["base64"] = "e30=\n"; break;
            case "targets-blank": root["issues"]![0]!["repairTargetFiles"] = new JsonArray(""); break;
            case "targets-duplicate": root["issues"]![0]!["repairTargetFiles"] = new JsonArray("i", "i"); break;
            case "issue-null-targets": root["issues"]![0]!["repairTargetFiles"] = null; break;
        }
        ResealData(root);
        var failure = Assert.Throws<InvalidDataException>(() => MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(root)));
        Assert.Equal(corruption switch
        {
            "version" => "joint_interface_result_version_state",
            "extra" => "joint_interface_closed_shape",
            "image" => "joint_interface_image_digest",
            "targets-blank" => "joint_interface_required_string",
            "targets-duplicate" => "joint_interface_issue_duplicate_target",
            _ => "joint_interface_issue_targets"
        }, failure.Message);
    }

    [Fact]
    public void ObjectCannotClaimTimestampPolicyWasNotReachedEvenWithConsistentDataDigests()
    {
        var root = Prepared(Encoding.UTF8.GetBytes("{}"), []).ToJson();
        root["timestampCheck"]!["timestamp"] = "NotReached"; ResealData(root);
        Assert.Equal("joint_interface_result_timestamp_shape", Assert.Throws<InvalidDataException>(() =>
            MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(root))).Message);
    }

    [Theory]
    [InlineData("yesterday")]
    [InlineData("2026-10-18T12:00:00+00:00")]
    [InlineData("2026-10-18T12:00:00.0000000+02:00")]
    public void FrozenFallbackRejectsNoncanonicalOrNonUtcDataWithoutReevaluatingInterfacePolicy(string sample)
    {
        var root = Prepared(Encoding.UTF8.GetBytes("{}"), []).ToJson();
        root["afterImage"] = new JsonObject { ["present"] = true, ["base64"] = "e30g",
            ["sha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("{} "))) };
        root["normalization"] = "Changed";
        root["fallback"] = new JsonObject { ["required"] = true, ["sampled"] = true, ["value"] = sample };
        ResealData(root);
        Assert.Equal("joint_interface_result_fallback_utc_encoding", Assert.Throws<InvalidDataException>(() =>
            MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(root))).Message);
    }

    [Fact]
    public void StrictReaderRejectsDuplicatePropertiesBeforeJsonNodeCanonicalization()
    {
        var json = Prepared(null, []).ToJson().ToJsonString();
        using var doc = JsonDocument.Parse(json.Insert(1, "\"version\":1,"));
        Assert.Throws<InvalidDataException>(() => MortalJointInterfacePublication.Read(doc.RootElement));
    }

    [Fact]
    public void InterfaceAboveCheckpointBlobLimitIsPreservedWithoutTruncation()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"_large\":\"" + new string('x', 2 * 1024 * 1024 + 1) + "\"}");
        var record = Prepared(bytes, []);
        var decoded = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(record.ToJson()));
        Assert.Equal(bytes, decoded.BeforeBytes);
        Assert.Equal(bytes, decoded.AfterBytes);
        var exposed = decoded.AfterBytes!; exposed[0] = 0;
        Assert.Equal(bytes, decoded.AfterBytes);
    }

    [Fact]
    public void ErrorPreparedIsFrozenAndCannotBecomePublished()
    {
        var record = Prepared(Encoding.UTF8.GetBytes("{}"), [new ValidationIssue("i", IssueSeverity.Error, "refuse")]);
        Assert.Equal("Prepared", record.State);
        Assert.True(record.HasErrors);
        Assert.Throws<InvalidDataException>(() => record.AsPublished());
        Assert.Equal(Encoding.UTF8.GetBytes("{}"), record.BeforeBytes);
    }

    [Fact]
    public void AbsenceIsDistinctFromPresentEmptyAndReturnedCopiesAreDefensive()
    {
        var absent = Prepared(null, []);
        var empty = Prepared([], []);
        Assert.NotEqual(absent.ResultFingerprint, empty.ResultFingerprint);
        Assert.Null(absent.BeforeBytes); Assert.Empty(empty.BeforeBytes!);
        var json = absent.ToJson(); json["binding"]!["requestId"] = "foreign";
        Assert.Equal("request", absent.ToJson()["binding"]!["requestId"]!.GetValue<string>());
    }

    private static MortalJointInterfacePublication.Record Prepared(byte[]? bytes, IReadOnlyList<ValidationIssue> issues) =>
        MortalJointInterfacePublication.CreatePrepared(new JsonObject
        {
            ["sessionGeneration"] = "generation", ["sessionId"] = "session", ["requestId"] = "request",
            ["snapshotToken"] = "snapshot", ["turnNumber"] = 43, ["realm"] = "mortal_world",
            ["stageOrdinal"] = 1, ["readyOrdinal"] = 2, ["iterationOrdinal"] = 3,
            ["checkpointFingerprint"] = Fingerprint("checkpoint"), ["operationKey"] = "operation",
            ["attemptId"] = "attempt", ["requestFingerprint"] = Fingerprint("request")
        }, new JsonObject { ["graph"] = "complete-test-data-not-engine-authority" },
        Fingerprint("source"), new MortalJointInterfacePublication.ComputedResult(bytes, bytes,
            bytes is null ? "Absent" : "NoWrite", false, null, bytes is null ? "Absent" : "Object",
            bytes is null ? "NotReached" : "ParseAccepted", issues));

    private static string Fingerprint(string text) => WoundAcceptedTurnFingerprintWriter.Compute([text]);

    // Independently implement the documented DATA digest composition, so strict
    // shape/enum/target negatives cannot pass only because an old digest broke.
    // These hashes do not construct a live issuer, Ready, continuation or take.
    private static void ResealData(JsonObject root)
    {
        var immutable = root.DeepClone().AsObject();
        immutable.Remove("state"); immutable.Remove("resultFingerprint"); immutable.Remove("fingerprint");
        root["resultFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "book_of_eternity.mortal_joint_interface.computed-result-v1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(immutable) });
        var record = root.DeepClone().AsObject(); record.Remove("fingerprint");
        root["fingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[]
        { "book_of_eternity.mortal_joint_interface.record-v1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(record) });
    }
}
