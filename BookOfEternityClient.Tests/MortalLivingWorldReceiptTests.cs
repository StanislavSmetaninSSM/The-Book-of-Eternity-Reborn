using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Detached receipt declaration DATA; no source authentication, effects, lease or publication acceptance.</summary>
public sealed class MortalLivingWorldReceiptTests
{
    [Fact]
    public void IdenticalInputProducesDeterministicReceiptAndHistoryBytes()
    {
        var first = Build(); var second = Build();
        Assert.Equal(first.HistoryJson, second.HistoryJson);
        Assert.Equal(first.HistoryBytes, second.HistoryBytes);
        Assert.Equal(first.Receipt.ToJsonString(), second.Receipt.ToJsonString());
        Assert.Equal(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(first.HistoryJson)), first.HistoryBytes);
        Assert.Equal(2, JsonNode.Parse(first.HistoryJson)!["schemaVersion"]!.GetValue<int>());
        Assert.Equal(2, first.Receipt["receiptRevision"]!.GetValue<int>());
        Assert.False(first.Receipt.ContainsKey("evidenceFingerprint"));
        Assert.StartsWith("sha256:", Fingerprint(first, "declarationFingerprint"));
        Assert.StartsWith("sha256:", Fingerprint(first, "receiptFingerprint"));
    }

    [Fact]
    public void ObjectPropertyOrderDoesNotChangeDeclarationOrHistoryBytes()
    {
        var report = Report();
        var reordered = new JsonObject(report.Reverse().Select(pair => KeyValuePair.Create(pair.Key, pair.Value?.DeepClone())));
        var first = MortalLivingWorldReceipt.Build("snapshot", Context(), report, null);
        var second = MortalLivingWorldReceipt.Build("snapshot", Context(), reordered, null);
        Assert.Equal(first.HistoryBytes, second.HistoryBytes);
        Assert.Equal(Fingerprint(first, "declarationFingerprint"), Fingerprint(second, "declarationFingerprint"));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("session")]
    [InlineData("request")]
    [InlineData("turn")]
    [InlineData("report")]
    [InlineData("window")]
    public void SourceCompleteReportAndNumericWindowAreBoundToDeclaration(string change)
    {
        var baseline = Build(); var context = Context(); var report = Report(); var token = "snapshot";
        switch (change)
        {
            case "snapshot": token = "snapshot_other"; break;
            case "session": context = context with { SessionId = "other_session" }; report["sessionId"] = context.SessionId; break;
            case "request": context = context with { RequestId = "other_request" }; report["requestId"] = context.RequestId; break;
            case "turn": context = context with { Turn = 8 }; report["turn"] = context.Turn; break;
            case "report": report["audit"]![0]!["reason"] = "A different exact assessment reason."; break;
            case "window": context = context with { ResultMinute = 241 }; report["toMinute"] = 241; break;
        }
        var changed = MortalLivingWorldReceipt.Build(token, context, report, null);
        Assert.NotEqual(Fingerprint(baseline, "declarationFingerprint"), Fingerprint(changed, "declarationFingerprint"));
        Assert.NotEqual(Fingerprint(baseline, "receiptFingerprint"), Fingerprint(changed, "receiptFingerprint"));
        Assert.Equal(report["fromMinute"]!.ToJsonString(), changed.Receipt["fromMinute"]!.ToJsonString());
        Assert.Equal(report["toMinute"]!.ToJsonString(), changed.Receipt["toMinute"]!.ToJsonString());
        Assert.True(JsonNode.DeepEquals(report, changed.Receipt["report"]));
    }

    [Fact]
    public void AppendPreservesExactPriorReceiptAndRejectsDuplicateHistorySources()
    {
        var first = Build(); var prior = JsonNode.Parse(first.HistoryJson)!.AsObject();
        var originalReceipt = prior["entries"]![0]!.ToJsonString();
        var nextContext = Context() with { RequestId = "request_2", Turn = 8 };
        var nextReport = Report(); nextReport["requestId"] = nextContext.RequestId; nextReport["turn"] = nextContext.Turn;
        var next = MortalLivingWorldReceipt.Build("snapshot_2", nextContext, nextReport, prior);
        var history = JsonNode.Parse(next.HistoryJson)!.AsObject();
        Assert.Equal(2, history["entries"]!.AsArray().Count);
        Assert.Equal(originalReceipt, history["entries"]![0]!.ToJsonString());
        Assert.True(JsonNode.DeepEquals(next.Receipt, history["entries"]![1]));
        history["entries"]!.AsArray().Add(history["entries"]![0]!.DeepClone());
        Assert.Throws<InvalidDataException>(() => MortalLivingWorldReceipt.ValidateHistory(history));
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("tuple")]
    public void AnExistingSourceCannotAuthorizeAnotherAppend(string duplicate)
    {
        var prior = JsonNode.Parse(Build().HistoryJson)!.AsObject();
        var context = Context(); var report = Report(); var token = "snapshot";
        if (duplicate == "snapshot")
        { context = context with { RequestId = "request_other", Turn = 8 }; report["requestId"] = context.RequestId; report["turn"] = context.Turn; }
        else token = "different_snapshot";
        Assert.Throws<InvalidDataException>(() => MortalLivingWorldReceipt.Build(token, context, report, prior));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("entryUnknownField")]
    [InlineData("source")]
    [InlineData("report")]
    [InlineData("window")]
    [InlineData("declaration")]
    [InlineData("receipt")]
    public void TamperedHistoryIsRejected(string change)
    {
        var history = JsonNode.Parse(Build().HistoryJson)!.AsObject(); var entry = history["entries"]![0]!.AsObject();
        switch (change)
        {
            case "schemaVersion": history["schemaVersion"] = 1; break;
            case "entryUnknownField": entry["authority"] = true; break;
            case "source": entry["requestId"] = "forged"; break;
            case "report": entry["report"]!["audit"]![0]!["reason"] = "Changed after acceptance"; break;
            case "window": entry["toMinute"] = 241; break;
            case "declaration": entry["declarationFingerprint"] = "sha256:forged"; break;
            case "receipt": entry["receiptFingerprint"] = "sha256:forged"; break;
        }
        Assert.Throws<InvalidDataException>(() => MortalLivingWorldReceipt.ValidateHistory(history));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" snapshot")]
    [InlineData("snapshot ")]
    public void SourceTokenMustBeExactAndNonempty(string token) =>
        Assert.Throws<InvalidDataException>(() => MortalLivingWorldReceipt.Build(token, Context(), Report(), null));

    [Theory]
    [InlineData("requestId")]
    [InlineData("fromMinute")]
    [InlineData("toMinute")]
    [InlineData("audit")]
    public void ReportMustMatchExactAssessedSourceAndWindow(string field)
    {
        var report = Report();
        report[field] = field switch
        { "requestId" => JsonValue.Create("foreign"), "audit" => new JsonArray(), _ => JsonValue.Create(999L) };
        Assert.Throws<InvalidDataException>(() => MortalLivingWorldReceipt.Build("snapshot", Context(), report, null));
    }

    [Fact]
    public void BuilderAndHistoryValidationDoNotMutateOrRetainCallerData()
    {
        var prior = JsonNode.Parse(Build().HistoryJson)!.AsObject(); var before = prior.ToJsonString();
        var context = Context() with { RequestId = "request_2", Turn = 8 }; var report = Report();
        report["requestId"] = context.RequestId; report["turn"] = context.Turn;
        var reportBefore = report.ToJsonString();
        var copy = MortalLivingWorldReceipt.ValidateHistory(prior);
        var built = MortalLivingWorldReceipt.Build("snapshot_2", context, report, prior);
        Assert.Equal(before, prior.ToJsonString()); Assert.Equal(reportBefore, report.ToJsonString());
        copy["entries"]!.AsArray().Clear(); built.Receipt["report"]!["audit"]![0]!["reason"] = "Caller modifies detached result";
        Assert.Equal(before, prior.ToJsonString()); Assert.Equal(reportBefore, report.ToJsonString());
        Assert.DoesNotContain("Caller modifies detached result", Encoding.UTF8.GetString(built.HistoryBytes));
    }

    private static string Fingerprint(MortalLivingWorldReceiptData data, string field) => data.Receipt[field]!.GetValue<string>();
    private static MortalLivingWorldReceiptData Build() => MortalLivingWorldReceipt.Build("snapshot", Context(), Report(), null);
    private static MortalLivingWorldAssessmentContext Context() => new("session", "request", 7, 0, 240, 0, 0, 240, 1440);
    private static JsonObject Report() => new()
    {
        ["schemaVersion"] = 1, ["sessionId"] = "session", ["requestId"] = "request", ["turn"] = 7,
        ["fromMinute"] = 0L, ["toMinute"] = 240L, ["resolution"] = "noSignificantChange",
        ["audit"] = new JsonArray(new[] { "npcs", "factions", "locations", "projects", "world" }.Select(scope =>
            (JsonNode)new JsonObject { ["scope"] = scope, ["reason"] = "Current bounded scope reviewed; no autonomous change." }).ToArray()),
        ["events"] = new JsonArray()
    };
}
