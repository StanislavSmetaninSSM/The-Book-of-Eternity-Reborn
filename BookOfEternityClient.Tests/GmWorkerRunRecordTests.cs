using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerRunRecordTests
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
    public void ColdNonterminalRecord_RetainsExactIdentityAndRemainsUncertain(string phase)
    {
        var record = CurrentRecord(Identity(), Enum.Parse<WorkerRunPhase>(phase));
        var observation = GmWorkerRunRecordCodec.Observe(Bytes(record));
        Assert.Equal(WorkerRunObservationKind.Uncertain, observation.Kind);
        Assert.Equal(record, observation.Record);
    }

    [Fact]
    public void PrelaunchAbort_RetainsExactIdentityWithoutLiveAuthority()
    {
        var record = new WorkerRunRecord(2, Identity(), WorkerRunPhase.AbortedBeforeLaunch);
        var observation = GmWorkerRunRecordCodec.Observe(Bytes(record));
        Assert.Equal(WorkerRunObservationKind.Quiescent, observation.Kind);
        Assert.Equal(record, observation.Record);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("oversize")]
    [InlineData("invalid-utf8")]
    [InlineData("null")]
    [InlineData("array")]
    [InlineData("schema")]
    [InlineData("missing-phase")]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("duplicate-identity")]
    [InlineData("numeric-phase")]
    [InlineData("unknown-phase")]
    [InlineData("retirement-without-cleanup-facts")]
    [InlineData("missing-identity-field")]
    [InlineData("unknown-identity-field")]
    [InlineData("relative-root")]
    [InlineData("noncanonical-root")]
    [InlineData("control-root")]
    [InlineData("oversize-root")]
    [InlineData("invalid-run")]
    [InlineData("empty-generation")]
    [InlineData("invalid-host")]
    [InlineData("zero-epoch")]
    [InlineData("negative-epoch")]
    [InlineData("overflow-epoch")]
    [InlineData("string-epoch")]
    [InlineData("numeric-backend")]
    [InlineData("unknown-backend")]
    [InlineData("mismatched-scope")]
    [InlineData("control-worker")]
    [InlineData("empty-task")]
    [InlineData("oversize-task")]
    [InlineData("invalid-digest")]
    [InlineData("relative-workspace")]
    public void InvalidRecord_IsBlockedWithoutMissingOrPayloadDiagnostic(string mutation)
    {
        var payload = JsonNode.Parse(Bytes(new(2, Identity(), WorkerRunPhase.Prepared)))!.AsObject();
        var identity = payload["Identity"]!.AsObject();
        byte[]? raw = null;
        switch (mutation)
        {
            case "empty": raw = []; break;
            case "oversize": raw = new byte[GmWorkerRunRecordCodec.MaximumBytes + 1]; break;
            case "invalid-utf8": raw = [0xff]; break;
            case "null": raw = "null"u8.ToArray(); break;
            case "array": raw = "[]"u8.ToArray(); break;
            case "schema": payload["SchemaVersion"] = 1; break;
            case "missing-phase": payload.Remove("Phase"); break;
            case "unknown": payload["secret-payload"] = "do-not-echo"; break;
            case "duplicate": raw = Encoding.UTF8.GetBytes(payload.ToJsonString().Replace("\"SchemaVersion\":2", "\"SchemaVersion\":2,\"SchemaVersion\":2")); break;
            case "duplicate-identity": raw = Encoding.UTF8.GetBytes(payload.ToJsonString().Replace("\"Epoch\":1", "\"Epoch\":1,\"Epoch\":1")); break;
            case "numeric-phase": payload["Phase"] = 0; break;
            case "unknown-phase": payload["Phase"] = "prepared"; break;
            case "retirement-without-cleanup-facts": payload["Phase"] = "Retired"; break;
            case "missing-identity-field": identity.Remove("WorkerId"); break;
            case "unknown-identity-field": identity["extra"] = true; break;
            case "relative-root": identity["RootKey"] = "relative"; break;
            case "noncanonical-root": identity["RootKey"] = Identity().RootKey + Path.DirectorySeparatorChar + "."; break;
            case "control-root": identity["RootKey"] = Identity().RootKey + "\n"; break;
            case "oversize-root": identity["RootKey"] = Path.GetPathRoot(Identity().RootKey) + new string('r', 4097); break;
            case "invalid-run": identity["RunId"] = "secret-invalid-run"; break;
            case "empty-generation": identity["GenerationId"] = ""; break;
            case "invalid-host": identity["HostInstanceId"] = new string('0', 32); break;
            case "zero-epoch": identity["Epoch"] = 0; break;
            case "negative-epoch": identity["Epoch"] = -1; break;
            case "overflow-epoch": raw = Encoding.UTF8.GetBytes(payload.ToJsonString().Replace("\"Epoch\":1", "\"Epoch\":9223372036854775808")); break;
            case "string-epoch": identity["Epoch"] = "1"; break;
            case "numeric-backend": identity["Backend"] = 2; break;
            case "unknown-backend": identity["Backend"] = "unknown"; break;
            case "mismatched-scope": identity["Scope"] = "WindowsJob"; break;
            case "control-worker": identity["WorkerId"] = "worker\n"; break;
            case "empty-task": identity["TaskId"] = ""; break;
            case "oversize-task": identity["TaskId"] = new string('t', 1025); break;
            case "invalid-digest": identity["TaskSha256"] = new string('Z', 64); break;
            case "relative-workspace": identity["WorkspacePath"] = "relative"; break;
            default: throw new InvalidOperationException("Unknown fixture mutation.");
        }
        var observation = GmWorkerRunRecordCodec.Observe(raw ?? Encoding.UTF8.GetBytes(payload.ToJsonString()));
        Assert.Equal(WorkerRunObservationKind.Blocked, observation.Kind);
        Assert.Null(observation.Record);
    }

    internal static WorkerRunRecord CurrentRecord(WorkerRunIdentity identity, WorkerRunPhase phase) => new(2, identity, phase,
        phase is WorkerRunPhase.PublicationIntent or WorkerRunPhase.Published
            ? new(new("proposal_fixture", new string('b', 64), new string('c', 64), phase == WorkerRunPhase.Published), null) : null);

    internal static WorkerRunIdentity Identity() => new(
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ledger-record-root")), 1,
        "11111111111111111111111111111111", "22222222222222222222222222222222",
        "worker", "task", new string('a', 64), WorkerRunBackend.LinuxNativeLineage,
        WorkerRunScope.OrdinarySamePidNamespace, "33333333333333333333333333333333",
        Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ledger-record-workspace")));

    internal static byte[] Bytes(WorkerRunRecord record) => JsonSerializer.SerializeToUtf8Bytes(record,
        new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } });
}
