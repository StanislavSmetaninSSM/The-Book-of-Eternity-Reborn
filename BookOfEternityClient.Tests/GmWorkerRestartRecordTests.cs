using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRestartRecordTests
{
    [Theory]
    [InlineData("Prepared")]
    [InlineData("LaunchIntent")]
    [InlineData("ReleaseIntent")]
    [InlineData("Released")]
    [InlineData("StopValidated")]
    [InlineData("PublicationIntent")]
    [InlineData("Published")]
    [InlineData("CleanupPending")]
    [InlineData("Uncertain")]
    public void CurrentProgress_ColdObservationCannotRestoreLiveAuthority(string phase)
    {
        var bytes = Bytes(Record(phase));
        var observation = GmWorkerRunRecordCodec.Observe(bytes);
        Assert.Equal(WorkerRunObservationKind.Uncertain, observation.Kind);
        Assert.NotNull(observation.Record);
        Assert.Equal(phase, observation.Record.Phase.ToString());
        Assert.Equal(bytes, GmWorkerRunRecordCodec.Encode(observation.Record));
    }

    [Theory]
    [InlineData("Retired", false)]
    [InlineData("Retired", true)]
    [InlineData("AbortedBeforeLaunch", false)]
    [InlineData("AbortedBeforeLaunch", true)]
    public void TerminalSyntax_RetainsFrozenCleanupFactsWithoutCreatingExecution(string phase, bool auditRequired)
    {
        var payload = Record(phase);
        payload["Progress"] = Progress(phase, auditRequired);
        var bytes = Bytes(payload);
        var observation = GmWorkerRunRecordCodec.Observe(bytes);
        Assert.Equal(WorkerRunObservationKind.Quiescent, observation.Kind);
        Assert.NotNull(observation.Record);
        Assert.Equal(bytes, GmWorkerRunRecordCodec.Encode(observation.Record));
    }

    [Theory]
    [InlineData("old-schema")]
    [InlineData("missing-progress")]
    [InlineData("unknown-progress")]
    [InlineData("duplicate-progress")]
    [InlineData("prepared-has-publication")]
    [InlineData("intent-committed")]
    [InlineData("published-uncommitted")]
    [InlineData("published-no-publication")]
    [InlineData("retired-uncommitted")]
    [InlineData("retired-no-cleanup")]
    [InlineData("early-cleanup")]
    [InlineData("abort-has-publication")]
    [InlineData("wrong-proposal-digest")]
    [InlineData("wrong-content-digest")]
    [InlineData("empty-proposal")]
    [InlineData("unknown-publication")]
    [InlineData("numeric-committed")]
    [InlineData("required-audit-missing-id")]
    [InlineData("required-audit-missing-digest")]
    [InlineData("unrequired-audit-has-id")]
    [InlineData("unrequired-audit-has-digest")]
    [InlineData("unknown-cleanup")]
    public void InconsistentProgress_IsBlocked(string mutation)
    {
        var payload = Record("Retired");
        var progress = payload["Progress"]!.AsObject();
        var publication = progress["Publication"]!.AsObject();
        var cleanup = progress["Cleanup"]!.AsObject();
        byte[]? raw = null;
        switch (mutation)
        {
            case "old-schema": payload["SchemaVersion"] = 1; break;
            case "missing-progress": payload.Remove("Progress"); break;
            case "unknown-progress": progress["ProviderPayload"] = "not-authority"; break;
            case "duplicate-progress": raw = Encoding.UTF8.GetBytes(payload.ToJsonString().Replace("\"Progress\":", "\"Progress\":null,\"Progress\":")); break;
            case "prepared-has-publication": payload["Phase"] = "Prepared"; progress["Cleanup"] = null; break;
            case "intent-committed": payload["Phase"] = "PublicationIntent"; progress["Cleanup"] = null; break;
            case "published-uncommitted": payload["Phase"] = "Published"; progress["Cleanup"] = null; publication["Committed"] = false; break;
            case "published-no-publication": payload["Phase"] = "Published"; progress["Cleanup"] = null; progress["Publication"] = null; break;
            case "retired-uncommitted": publication["Committed"] = false; break;
            case "retired-no-cleanup": progress["Cleanup"] = null; break;
            case "early-cleanup": payload["Phase"] = "Published"; break;
            case "abort-has-publication": payload["Phase"] = "AbortedBeforeLaunch"; break;
            case "wrong-proposal-digest": publication["ProposalSha256"] = new string('Z', 64); break;
            case "wrong-content-digest": publication["ContentSha256"] = "short"; break;
            case "empty-proposal": publication["ProposalId"] = ""; break;
            case "unknown-publication": publication["LivePermit"] = true; break;
            case "numeric-committed": publication["Committed"] = 1; break;
            case "required-audit-missing-id": cleanup["RequiredAudit"] = true; cleanup["AuditSha256"] = new string('d', 64); break;
            case "required-audit-missing-digest": cleanup["RequiredAudit"] = true; cleanup["AuditEventId"] = "worker_audit_fixture"; break;
            case "unrequired-audit-has-id": cleanup["AuditEventId"] = "worker_audit_fixture"; break;
            case "unrequired-audit-has-digest": cleanup["AuditSha256"] = new string('d', 64); break;
            case "unknown-cleanup": cleanup["Trusted"] = true; break;
            default: throw new InvalidOperationException("Unknown mutation.");
        }
        var observed = GmWorkerRunRecordCodec.Observe(raw ?? Bytes(payload));
        Assert.Equal(WorkerRunObservationKind.Blocked, observed.Kind);
        Assert.Null(observed.Record);
    }

    private static JsonObject Record(string phase) => new()
    {
        ["SchemaVersion"] = 2,
        ["Identity"] = JsonSerializer.SerializeToNode(GmWorkerRunRecordTests.Identity(),
            new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } }),
        ["Phase"] = phase,
        ["Progress"] = Progress(phase)
    };

    private static JsonObject? Progress(string phase, bool audit = false)
    {
        if (phase is "Prepared" or "LaunchIntent" or "ReleaseIntent" or "Released" or "StopValidated") return null;
        return new()
        {
            ["Publication"] = phase == "AbortedBeforeLaunch" ? null : new JsonObject
            { ["ProposalId"] = "proposal_fixture", ["ProposalSha256"] = new string('b', 64),
                ["ContentSha256"] = new string('c', 64), ["Committed"] = phase != "PublicationIntent" },
            ["Cleanup"] = phase is "Retired" or "AbortedBeforeLaunch" ? new JsonObject
            { ["RequiredAudit"] = audit, ["AuditEventId"] = audit ? "worker_audit_fixture" : null,
                ["AuditSha256"] = audit ? new string('d', 64) : null } : null
        };
    }
    private static byte[] Bytes(JsonObject payload) => Encoding.UTF8.GetBytes(payload.ToJsonString());
}
