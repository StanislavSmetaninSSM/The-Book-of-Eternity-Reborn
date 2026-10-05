using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmSessionRunRecordTests
{
    internal static GmSessionRunIdentity Identity => new("/game/世界", new string('1', 32),
        new string('2', 32), 1, GmSessionRunBackend.LinuxSupervisor, new string('3', 32), "boot-1");
    internal static GmSessionRunRecord Record(string state = "Prepared") => new(1, Identity,
        Enum.Parse<GmSessionRunDisposition>(state), null);
    internal static GmSessionRunStopEvidence Stop(GmSessionRunIdentity? identity = null,
        string kind = "OwnedScopeEmpty") => new(identity ?? Identity,
            Enum.Parse<GmSessionRunStopKind>(kind), kind == "OwnedScopeEmpty" ? "boot-1" : "boot-2");
    internal static GmSessionRunRecord Stopped => Record("Stopped") with { StopEvidence = Stop() };

    [Theory]
    [InlineData("Prepared")]
    [InlineData("Running")]
    [InlineData("Stopping")]
    [InlineData("Uncertain")]
    [InlineData("Stopped")]
    public void Codec_RoundTripsEveryDisposition(string state)
    {
        var record = state == "Stopped" ? Stopped : Record(state);
        Assert.Equal(record, GmSessionRunRecordCodec.Decode(GmSessionRunRecordCodec.Encode(record)));
    }

    [Theory]
    [InlineData("OwnedScopeEmpty")]
    [InlineData("VerifiedHostReboot")]
    public void Codec_RoundTripsBothRetainedStopEvidenceKinds(string kind)
    {
        var record = Stopped with { StopEvidence = Stop(kind: kind) };
        Assert.Equal(record, GmSessionRunRecordCodec.Decode(GmSessionRunRecordCodec.Encode(record)));
    }

    [Fact]
    public void Codec_AcceptsExistingAllZeroGeneration()
    {
        var record = Record() with { Identity = Identity with { GenerationId = new string('0', 32) } };
        Assert.Equal(record, GmSessionRunRecordCodec.Decode(GmSessionRunRecordCodec.Encode(record)));
    }

    [Theory]
    [InlineData("unknown-root")]
    [InlineData("duplicate-root")]
    [InlineData("escaped-duplicate")]
    [InlineData("missing-root")]
    [InlineData("missing-nullable")]
    [InlineData("unknown-identity")]
    [InlineData("duplicate-identity")]
    [InlineData("null-identity")]
    [InlineData("wrong-identity")]
    [InlineData("unknown-stop")]
    [InlineData("duplicate-stop")]
    [InlineData("missing-stop")]
    [InlineData("unknown-stop-identity")]
    [InlineData("duplicate-stop-identity")]
    [InlineData("wrong-primitive")]
    [InlineData("numeric-state")]
    [InlineData("wrong-case-state")]
    [InlineData("unknown-state")]
    [InlineData("wrong-schema")]
    [InlineData("trailing-document")]
    [InlineData("comment")]
    [InlineData("trailing-comma")]
    [InlineData("escaped-lone-surrogate")]
    public void Codec_RejectsInvalidDocumentWithoutPayloadDiagnostics(string mutation)
    {
        var json = Encoding.UTF8.GetString(GmSessionRunRecordCodec.Encode(Stopped));
        json = mutation switch
        {
            "unknown-root" => json.Insert(1, "\"SECRET_PAYLOAD\":0,"),
            "duplicate-root" => json.Insert(1, "\"SchemaVersion\":1,"),
            "escaped-duplicate" => json.Insert(1, "\"Schema\\u0056ersion\":1,"),
            "missing-root" => json.Replace("\"SchemaVersion\":1,", ""),
            "missing-nullable" => Encoding.UTF8.GetString(GmSessionRunRecordCodec.Encode(Record())).Replace(",\"StopEvidence\":null", ""),
            "unknown-identity" => json.Replace("\"Identity\":{", "\"Identity\":{\"SECRET_PAYLOAD\":0,"),
            "duplicate-identity" => json.Replace("\"Identity\":{", "\"Identity\":{\"Epoch\":1,"),
            "null-identity" => Edit(json, "Identity", null),
            "wrong-identity" => Edit(json, "Identity", JsonValue.Create("SECRET_PAYLOAD")),
            "unknown-stop" => json.Replace("\"StopEvidence\":{", "\"StopEvidence\":{\"SECRET_PAYLOAD\":0,"),
            "duplicate-stop" => json.Replace("\"StopEvidence\":{", "\"StopEvidence\":{\"Kind\":\"OwnedScopeEmpty\","),
            "missing-stop" => json.Replace("\"ObservedBootId\":\"boot-1\"", "\"Other\":\"boot-1\""),
            "unknown-stop-identity" => EditStopIdentity(json, "SECRET_PAYLOAD", JsonValue.Create(0)),
            "duplicate-stop-identity" => json.Replace("\"StopEvidence\":{\"Identity\":{", "\"StopEvidence\":{\"Identity\":{\"Epoch\":1,"),
            "wrong-primitive" => json.Replace("\"Epoch\":1", "\"Epoch\":\"SECRET_PAYLOAD\""),
            "numeric-state" => Edit(json, "Disposition", JsonValue.Create(5)),
            "wrong-case-state" => Edit(json, "Disposition", JsonValue.Create("stopped")),
            "unknown-state" => Edit(json, "Disposition", JsonValue.Create("SECRET_PAYLOAD")),
            "wrong-schema" => Edit(json, "SchemaVersion", JsonValue.Create(2)),
            "trailing-document" => json + "{}",
            "comment" => "/*SECRET_PAYLOAD*/" + json,
            "trailing-comma" => json.Insert(json.Length - 1, ","),
            "escaped-lone-surrogate" => json.Replace("boot-1", "\\uD800"),
            _ => throw new InvalidOperationException()
        };
        var ex = Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Decode(Encoding.UTF8.GetBytes(json)));
        Assert.Equal("Persistent GM run record is invalid.", ex.Message);
        Assert.Null(ex.InnerException);
    }

    [Theory]
    [InlineData("RunId", "00000000000000000000000000000000")]
    [InlineData("HostInstanceId", "00000000000000000000000000000000")]
    [InlineData("RunId", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("GenerationId", "22222222-2222-2222-2222-222222222222")]
    [InlineData("RootKey", "")]
    [InlineData("RootKey", " ")]
    [InlineData("RootKey", "bad\nroot")]
    [InlineData("BootId", "")]
    [InlineData("BootId", "bad boot")]
    [InlineData("BootId", "世界")]
    [InlineData("Backend", "Unknown")]
    public void Codec_RejectsInvalidIdentityFields(string property, string value)
    {
        var node = JsonNode.Parse(GmSessionRunRecordCodec.Encode(Record()))!;
        node["Identity"]![property] = value;
        Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Codec_RejectsNonpositiveEpoch(long epoch)
    {
        var node = JsonNode.Parse(GmSessionRunRecordCodec.Encode(Record()))!;
        node["Identity"]!["Epoch"] = epoch;
        Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }

    [Fact]
    public void Codec_EnforcesEncodedDocumentBoundary()
    {
        var bytes = GmSessionRunRecordCodec.Encode(Record());
        var exact = Enumerable.Repeat((byte)' ', GmSessionRunRecordCodec.MaximumBytes).ToArray();
        bytes.CopyTo(exact, 0);
        Assert.Equal(Record(), GmSessionRunRecordCodec.Decode(exact));
        Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Decode(exact.Concat(new[] { (byte)' ' }).ToArray()));
    }

    [Fact]
    public void Codec_EnforcesUtf8RootAndBootBoundsOnEncodingAndDecoding()
    {
        var boundary = Record() with { Identity = Identity with { RootKey = new string('я', 2048), BootId = new string('b', 128) } };
        Assert.Equal(boundary, GmSessionRunRecordCodec.Decode(GmSessionRunRecordCodec.Encode(boundary)));
        foreach (var invalid in new[] {
            boundary with { Identity = boundary.Identity with { RootKey = boundary.Identity.RootKey + "a" } },
            boundary with { Identity = boundary.Identity with { BootId = new string('b', 129) } },
            Record() with { Identity = Identity with { RootKey = "bad\uD800" } } })
            Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Encode(invalid));
        var node = JsonNode.Parse(GmSessionRunRecordCodec.Encode(boundary))!;
        node["Identity"]!["RootKey"] = boundary.Identity.RootKey + "a";
        Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Decode(Encoding.UTF8.GetBytes(node.ToJsonString())));
    }

    [Fact]
    public void Codec_RejectsInvalidUtf8BomEmptyAndTruncation()
    {
        var bytes = GmSessionRunRecordCodec.Encode(Record());
        foreach (var invalid in new[] { Array.Empty<byte>(), new byte[] { 0xff }, new byte[] { 0xef, 0xbb, 0xbf }.Concat(bytes).ToArray(), bytes[..^1] })
            Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Decode(invalid));
    }

    [Theory]
    [InlineData("Prepared")]
    [InlineData("Running")]
    [InlineData("Stopping")]
    [InlineData("Uncertain")]
    public void Codec_RejectsStopEvidenceOnNonterminalState(string state) =>
        Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Encode(Record(state) with { StopEvidence = Stop() }));

    [Fact]
    public void Codec_RejectsStoppedWithoutEvidence() =>
        Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Encode(Record("Stopped")));

    [Theory]
    [InlineData("RootKey")]
    [InlineData("RunId")]
    [InlineData("GenerationId")]
    [InlineData("Epoch")]
    [InlineData("Backend")]
    [InlineData("HostInstanceId")]
    [InlineData("BootId")]
    public void Transition_RejectsEveryStopIdentityMismatchAndRetainsOriginal(string field)
    {
        var original = Record("Uncertain");
        var before = GmSessionRunRecordCodec.Encode(original);
        var evidence = Stop(Mismatch(Identity, field));
        Assert.Throws<InvalidDataException>(() => GmSessionRunTransitions.ConfirmStopped(original, evidence));
        Assert.Equal(before, GmSessionRunRecordCodec.Encode(original));
        Assert.Throws<InvalidDataException>(() => GmSessionRunRecordCodec.Encode(Stopped with { StopEvidence = evidence }));
    }

    [Theory]
    [InlineData("OwnedScopeEmpty", "boot-2")]
    [InlineData("VerifiedHostReboot", "boot-1")]
    [InlineData("Unspecified", "boot-2")]
    [InlineData("VerifiedHostReboot", "")]
    public void Transition_RejectsInvalidStopObservation(string kind, string boot)
    {
        var evidence = new GmSessionRunStopEvidence(Identity, Enum.Parse<GmSessionRunStopKind>(kind), boot);
        Assert.Throws<InvalidDataException>(() => GmSessionRunTransitions.ConfirmStopped(Record("Uncertain"), evidence));
    }

    [Theory]
    [InlineData("OwnedScopeEmpty")]
    [InlineData("VerifiedHostReboot")]
    public void Transition_ConfirmedStopRetainsEvidenceAndIsIdempotent(string kind)
    {
        var evidence = Stop(kind: kind);
        var stopped = GmSessionRunTransitions.ConfirmStopped(Record("Uncertain"), evidence);
        Assert.Equal(GmSessionRunDisposition.Stopped, stopped.Disposition);
        Assert.Equal(evidence, stopped.StopEvidence);
        Assert.Equal(stopped, GmSessionRunTransitions.ConfirmStopped(stopped, evidence));
        Assert.Throws<InvalidOperationException>(() => GmSessionRunTransitions.MarkRunning(stopped, Identity));
        Assert.Throws<InvalidOperationException>(() => GmSessionRunTransitions.MarkStopping(stopped, Identity));
        Assert.Throws<InvalidOperationException>(() => GmSessionRunTransitions.MarkUncertain(stopped));
        Assert.Throws<InvalidOperationException>(() => GmSessionRunTransitions.ConfirmStopped(stopped,
            Stop(kind: kind == "OwnedScopeEmpty" ? "VerifiedHostReboot" : "OwnedScopeEmpty")));
    }

    [Fact]
    public void Transition_RequiresMatchingOwnerAndLegalProgression()
    {
        Assert.Throws<InvalidDataException>(() => GmSessionRunTransitions.MarkRunning(Record(), Mismatch(Identity, "Epoch")));
        var running = GmSessionRunTransitions.MarkRunning(Record(), Identity);
        Assert.Equal(GmSessionRunDisposition.Running, running.Disposition);
        Assert.Throws<InvalidOperationException>(() => GmSessionRunTransitions.MarkRunning(running, Identity));
        Assert.Throws<InvalidDataException>(() => GmSessionRunTransitions.MarkStopping(running, Mismatch(Identity, "RunId")));
        Assert.Equal(GmSessionRunDisposition.Stopping, GmSessionRunTransitions.MarkStopping(running, Identity).Disposition);
        Assert.Equal(GmSessionRunDisposition.Stopping, GmSessionRunTransitions.MarkStopping(Record(), Identity).Disposition);
        Assert.Throws<InvalidOperationException>(() => GmSessionRunTransitions.MarkRunning(Record("Uncertain"), Identity));
        Assert.Throws<InvalidOperationException>(() => GmSessionRunTransitions.MarkStopping(Record("Uncertain"), Identity));
        Assert.Equal(GmSessionRunDisposition.Uncertain, GmSessionRunTransitions.MarkUncertain(running).Disposition);
    }

    [Theory]
    [InlineData("Prepared")]
    [InlineData("Running")]
    [InlineData("Stopping")]
    [InlineData("Uncertain")]
    [InlineData("Stopped")]
    public void OrdinaryFile_ColdInterpretationNeverReleasesNonterminalAndPreservesBytes(string state)
    {
        var directory = Path.Combine(Path.GetTempPath(), "gm-run-record-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "main.json");
            var bytes = GmSessionRunRecordCodec.Encode(state == "Stopped" ? Stopped : Record(state));
            File.WriteAllBytes(path, bytes);
            var cold = GmSessionRunTransitions.InterpretCold(GmSessionRunRecordCodec.Decode(File.ReadAllBytes(path)));
            Assert.Equal(state == "Stopped" ? GmSessionRunDisposition.Stopped : GmSessionRunDisposition.Uncertain, cold.Disposition);
            var decision = GmSessionRunAdmission.Evaluate(GmSessionRunObservation.Valid(cold),
                new(Identity.RootKey, Identity.Backend, Identity.GenerationId), GmSessionRunOperation.QuiescentMutation);
            Assert.Equal(state == "Stopped", decision == GmSessionRunAdmissionDecision.SlotConditionSatisfied);
            Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void OrdinaryFile_UnreadableRecordRemainsEvidenceAndNeverBecomesMissing()
    {
        var directory = Path.Combine(Path.GetTempPath(), "gm-run-invalid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "main.json");
            File.WriteAllBytes(path, new byte[] { (byte)'{' });
            var observed = GmSessionRunRecordCodec.Observe(File.ReadAllBytes(path));
            Assert.Equal(GmSessionRunObservationKind.Unreadable, observed.Kind);
            Assert.NotEqual(GmSessionRunAdmissionDecision.SlotConditionSatisfied, GmSessionRunAdmission.Evaluate(observed,
                new(Identity.RootKey, Identity.Backend, Identity.GenerationId), GmSessionRunOperation.StartRun, Identity));
            Assert.Equal(new byte[] { (byte)'{' }, File.ReadAllBytes(path));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    internal static GmSessionRunIdentity Mismatch(GmSessionRunIdentity id, string field) => field switch
    {
        "RootKey" => id with { RootKey = id.RootKey + "other" },
        "RunId" => id with { RunId = new string('4', 32) },
        "GenerationId" => id with { GenerationId = new string('5', 32) },
        "Epoch" => id with { Epoch = id.Epoch + 1 },
        "Backend" => id with { Backend = GmSessionRunBackend.WindowsJob },
        "HostInstanceId" => id with { HostInstanceId = new string('6', 32) },
        "BootId" => id with { BootId = "boot-2" },
        _ => throw new InvalidOperationException()
    };

    private static string Edit(string json, string property, JsonNode? value)
    {
        var node = JsonNode.Parse(json)!;
        node[property] = value;
        return node.ToJsonString();
    }

    private static string EditStopIdentity(string json, string property, JsonNode? value)
    {
        var node = JsonNode.Parse(json)!;
        node["StopEvidence"]!["Identity"]![property] = value;
        return node.ToJsonString();
    }
}
