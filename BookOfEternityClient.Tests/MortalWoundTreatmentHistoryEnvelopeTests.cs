using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the closed physical History DATA codec without an active owner or publication.
/// The terminal fixture is detached INPUT; the lexical metadata below is explicitly synthetic.
/// </summary>
public sealed class MortalWoundTreatmentHistoryEnvelopeTests
{
    private const string AlteredFingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SyntheticSource = "game_state/character/wounds/synthetic_history_input.json";
    private const string PolicyInputOriginalBinding = "sha256:2ddbf927bcbc9f50568a1eeae669cf9a4021544941f7cd696c1e6be85c6a72f0";
    private const string PolicyInnerReceipt = "sha256:3eb6ef0f17cc6e57f13c54f679865959ab90f73a0015a747f2e75863682fda49";
    private const string InnerReceipt = "sha256:e0409f96e85beb650ba55a120b9028737c9b802ee3719d0e955782e319f96b4a";

    /// <summary>A closed version3 envelope survives strict parsing and preserves the entire inner receipt.</summary>
    [Fact]
    public void Parse_Version3DetachedInput_RoundTripsExactTerminalAndReceipt()
    {
        var envelope = DetachedEnvelope();
        var original = envelope.ToJsonString();
        var result = Parse(envelope, out var issues);

        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.Equal(InnerReceipt, result.Receipt.ReceiptFingerprint);
        Assert.Equal(original, result.ToCanonicalJson().ToJsonString());
        Assert.Equal(envelope["terminal"]!.ToJsonString(), result.ToCanonicalJson()["terminal"]!.ToJsonString());
        Assert.False(result.HasGuidedPublicationWitness);
        Assert.Equal(envelope["terminal"]!["requestAuthority"]!.ToJsonString(), result.SerializeRequest().ToJsonString());

        var copy = result.ToCanonicalJson();
        copy["publicationMinute"] = 1;
        copy["terminal"]!["receiptFingerprint"] = "changed returned copy";
        Assert.Equal(original, result.ToCanonicalJson().ToJsonString());
        Assert.Equal(InnerReceipt, result.Receipt.ReceiptFingerprint);
    }

    /// <summary>The unversioned request/receipt remains raw command DATA and is never silently resealed.</summary>
    [Fact]
    public void Parse_RawTerminalInput_PreservesOriginalBytesWithoutHistoryMetadata()
    {
        var terminal = TerminalInput();
        var result = Parse(terminal, out var issues);

        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.Equal(terminal.ToJsonString(), result.ToCanonicalJson().ToJsonString());
        Assert.False(result.ToCanonicalJson().ContainsKey("schemaVersion"));
        Assert.False(result.ToCanonicalJson().ContainsKey("bindingFingerprint"));
        Assert.Equal(InnerReceipt, result.Receipt.ReceiptFingerprint);
    }

    /// <summary>Unknown versions never fall back to either known version or raw terminal parsing.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void Parse_UnknownVersion_RejectsWithoutFallback(int version)
    {
        var envelope = DetachedEnvelope();
        envelope["schemaVersion"] = version;
        Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Required History fields cannot be defaulted even after recomputing the envelope seal.</summary>
    [Theory]
    [InlineData("kind")]
    [InlineData("schemaVersion")]
    [InlineData("terminal")]
    [InlineData("bindingFingerprint")]
    [InlineData("sourcePath")]
    [InlineData("publicationMinute")]
    [InlineData("outcomePreparationFingerprint")]
    [InlineData("resultFingerprint")]
    public void Parse_MissingField_Rejects(string field)
    {
        var envelope = DetachedEnvelope();
        envelope.Remove(field);
        if (field != "resultFingerprint") Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Additional fields cannot enlarge the closed History contract.</summary>
    [Theory]
    [InlineData("currentMinute")]
    [InlineData("originalScheduleBase64")]
    [InlineData("publicationInputs")]
    [InlineData("unrecognized")]
    public void Parse_ExtraField_Rejects(string field)
    {
        var envelope = DetachedEnvelope();
        envelope[field] = 1260;
        Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Duplicate properties remain invalid even when their values agree.</summary>
    [Theory]
    [InlineData("kind")]
    [InlineData("schemaVersion")]
    [InlineData("terminal")]
    [InlineData("bindingFingerprint")]
    [InlineData("sourcePath")]
    [InlineData("publicationMinute")]
    [InlineData("outcomePreparationFingerprint")]
    [InlineData("resultFingerprint")]
    public void Parse_DuplicateField_Rejects(string field)
    {
        var envelope = DetachedEnvelope();
        var text = envelope.ToJsonString();
        using var document = JsonDocument.Parse(text[..^1] + ",\"" + field + "\":" + envelope[field]!.ToJsonString() + "}");
        var issues = new List<ValidationIssue>();
        var result = MortalWoundTreatmentPersistedResult.Parse(document.RootElement, "historyResult", issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
    }

    /// <summary>The recursive duplicate guard covers the unchanged nested terminal too.</summary>
    [Fact]
    public void Parse_NestedDuplicateField_Rejects()
    {
        var envelope = DetachedEnvelope();
        var terminal = envelope["terminal"]!.ToJsonString();
        var duplicate = terminal[..^1] + ",\"kind\":\"treat\"}";
        using var document = JsonDocument.Parse(envelope.ToJsonString().Replace(
            "\"terminal\":" + terminal, "\"terminal\":" + duplicate, StringComparison.Ordinal));
        var issues = new List<ValidationIssue>();
        Assert.Null(MortalWoundTreatmentPersistedResult.Parse(document.RootElement, "historyResult", issues));
        Assert.NotEmpty(issues);
    }

    /// <summary>Field types, minute range and lexical metadata are checked instead of coerced.</summary>
    [Theory]
    [InlineData("kind", "7")]
    [InlineData("kind", "\"heal\"")]
    [InlineData("schemaVersion", "\"3\"")]
    [InlineData("schemaVersion", "3.5")]
    [InlineData("schemaVersion", "null")]
    [InlineData("terminal", "[]")]
    [InlineData("terminal", "null")]
    [InlineData("bindingFingerprint", "7")]
    [InlineData("bindingFingerprint", "\"not-a-fingerprint\"")]
    [InlineData("bindingFingerprint", "null")]
    [InlineData("sourcePath", "7")]
    [InlineData("sourcePath", "\"\"")]
    [InlineData("sourcePath", "\"   \"")]
    [InlineData("sourcePath", "null")]
    [InlineData("publicationMinute", "\"1260\"")]
    [InlineData("publicationMinute", "-1")]
    [InlineData("publicationMinute", "1.5")]
    [InlineData("publicationMinute", "9223372036854775808")]
    [InlineData("publicationMinute", "null")]
    [InlineData("outcomePreparationFingerprint", "7")]
    [InlineData("outcomePreparationFingerprint", "null")]
    [InlineData("outcomePreparationFingerprint", "\"\"")]
    [InlineData("outcomePreparationFingerprint", "\"not-a-fingerprint\"")]
    [InlineData("resultFingerprint", "7")]
    [InlineData("resultFingerprint", "null")]
    public void Parse_WrongTypeOrRange_Rejects(string field, string json)
    {
        var envelope = DetachedEnvelope();
        envelope[field] = JsonNode.Parse(json);
        if (field != "resultFingerprint") Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Each historical metadata value is inside the envelope seal.</summary>
    [Theory]
    [InlineData("bindingFingerprint", "\"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"")]
    [InlineData("sourcePath", "\"game_state/character/wounds/changed.json\"")]
    [InlineData("publicationMinute", "1261")]
    [InlineData("resultFingerprint", "\"sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb\"")]
    public void Parse_AlteredSealedEnvelope_Rejects(string field, string json)
    {
        var envelope = DetachedEnvelope();
        envelope[field] = JsonNode.Parse(json);
        AssertRejected(envelope);
    }

    /// <summary>An outer seal never repairs altered inner receipt bytes.</summary>
    [Fact]
    public void Parse_AlteredInnerReceiptWithNewOuterSeal_Rejects()
    {
        var envelope = DetachedEnvelope();
        envelope["terminal"]!["receiptFingerprint"] = AlteredFingerprint;
        Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>A versioned wrapper cannot be nested as the unchanged raw terminal.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Parse_VersionedNestedTerminal_Rejects(int version)
    {
        var envelope = DetachedEnvelope();
        envelope["terminal"]!["schemaVersion"] = version;
        Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Adding a version and seal to old raw bytes cannot invent the required History metadata.</summary>
    [Fact]
    public void Parse_RawTerminalWithVersionAndReseal_Rejects()
    {
        var terminal = TerminalInput();
        terminal["schemaVersion"] = 3;
        Reseal(terminal);
        AssertRejected(terminal);
    }

    /// <summary>Valid boundary minutes are retained exactly without an inferred historical clock.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(long.MaxValue)]
    public void Parse_BoundaryMinute_RetainsExactValue(long minute)
    {
        var envelope = DetachedEnvelope(minute);
        var result = Parse(envelope, out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.Equal(minute, result.ReconstructHistoricalPublication().CurrentMinute);
    }

    /// <summary>Version2 dispatch and complete request serialization remain unchanged.</summary>
    [Fact]
    public void Parse_GuidedVersion2DetachedInput_PreservesRequestAndWitness()
    {
        var envelope = GuidedDetachedEnvelope();
        var result = Parse(envelope, out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.True(result.HasGuidedPublicationWitness);
        Assert.Equal(envelope.ToJsonString(), result.ToCanonicalJson().ToJsonString());
        Assert.Equal(envelope["terminal"]!["requestAuthority"]!.ToJsonString(), result.SerializeRequest().ToJsonString());
        Assert.Equal(InnerReceipt, result.Receipt.ReceiptFingerprint);
    }

    /// <summary>Malformed guided minutes produce the existing structured refusal without changing the input.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("\"1260\"")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    [InlineData("true")]
    [InlineData("[]")]
    [InlineData("{}")]
    public void Parse_GuidedMalformedMinute_ReportsStructuredRefusal(string json)
    {
        var envelope = GuidedDetachedEnvelope();
        Assert.NotNull(Parse(envelope, out var baselineIssues));
        Assert.Empty(baselineIssues);
        var terminal = envelope["terminal"]!.ToJsonString();
        envelope["currentMinute"] = JsonNode.Parse(json);
        Reseal(envelope);
        var before = envelope.ToJsonString();

        var result = Parse(envelope, out var issues);

        Assert.Null(result);
        var issue = Assert.Single(issues);
        Assert.Equal("mortal_wound_guided_publication_result_invalid", issue.Code);
        Assert.Equal("historyResult", issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal("The complete guided treatment publication DATA is invalid.", issue.Message);
        Assert.Equal("Client", issue.Actor);
        Assert.Equal("wound_materialization", issue.Section);
        Assert.Equal("one closed version2 witness and independently reconstructed terminal publication", issue.Expected);
        Assert.False(string.IsNullOrWhiteSpace(issue.Actual));
        Assert.Equal(before, envelope.ToJsonString());
        Assert.Equal(terminal, envelope["terminal"]!.ToJsonString());
    }

    /// <summary>The existing guided integer minute contract preserves zero, ordinary and maximum Int64 values.</summary>
    [Theory]
    [InlineData(0L)]
    [InlineData(1260L)]
    [InlineData(long.MaxValue)]
    public void Parse_GuidedValidMinute_RetainsExactValueAndWitness(long minute)
    {
        var envelope = GuidedDetachedEnvelope();
        envelope["currentMinute"] = minute;
        Reseal(envelope);
        var before = envelope.ToJsonString();

        var result = Parse(envelope, out var issues);

        Assert.Empty(issues);
        Assert.NotNull(result);
        Assert.True(result.HasGuidedPublicationWitness);
        Assert.Equal(before, result.ToCanonicalJson().ToJsonString());
        Assert.Equal(minute, result.ReconstructGuidedPublication().CurrentMinute);
        Assert.Equal(envelope["terminal"]!["requestAuthority"]!.ToJsonString(), result.SerializeRequest().ToJsonString());
        Assert.Equal(InnerReceipt, result.Receipt.ReceiptFingerprint);
    }

    /// <summary>The full guided witness gate still rejects malformed inputs under a fresh outer seal.</summary>
    [Fact]
    public void Parse_GuidedInvalidPublicationInputsWithNewOuterSeal_Rejects()
    {
        var envelope = GuidedDetachedEnvelope();
        envelope["publicationInputs"]!["unexpected"] = true;
        Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Resealing the outer wrapper cannot repair a changed nested request fingerprint.</summary>
    [Fact]
    public void Parse_ChangedInnerRequestWithNewOuterSeal_Rejects()
    {
        var envelope = DetachedEnvelope();
        envelope["terminal"]!["requestAuthority"]!["requestFingerprint"] = AlteredFingerprint;
        Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Historical comparison uses only frozen DATA and never enters payment finalization.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ReconstructHistoricalPublication_DoesNotInvokeResourcePipeline(int version)
    {
        var envelope = version == 2 ? GuidedDetachedEnvelope() : DetachedEnvelope();
        var result = Parse(envelope, out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        using var scope = FileSystemManager.EnterOperationTimingContext((name, boundary, _) =>
        {
            if (name == "TreatmentResources.Finalize" && boundary == "begin")
                throw new InvalidOperationException("Historical comparison must not enter resource finalization.");
        });
        var comparison = result.ReconstructHistoricalPublication();
        Assert.Equal(envelope["bindingFingerprint"]!.GetValue<string>(), comparison.BindingFingerprint);
        Assert.Equal(SyntheticSource, comparison.SourcePath);
        Assert.Equal(1260, comparison.CurrentMinute);
        Assert.Equal(InnerReceipt, MortalWoundTreatmentReceipt.Create(comparison.Resolution).ReceiptFingerprint);
        Assert.True(comparison.Preparation.PreparesTerminalHeal);
        Assert.NotSame(result.Request, comparison.Resolution.RequestAuthority);
    }

    /// <summary>A retained wrapper exports independent request/receipt graphs and mutable JSON copies.</summary>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void DetachedCopy_IsolatesCompanionsAndReturnedJson(int version)
    {
        var result = Parse(version == 2 ? GuidedDetachedEnvelope() : DetachedEnvelope(), out var issues);
        Assert.Empty(issues);
        Assert.NotNull(result);
        var before = result.ToCanonicalJson().ToJsonString();
        var copy = result.DetachedCopy();
        Assert.NotSame(result.Request, copy.Request);
        Assert.NotSame(result.Receipt, copy.Receipt);
        Assert.NotSame(result.Request.RouteSourceWound, copy.Request.RouteSourceWound);
        var requestJson = copy.SerializeRequest();
        requestJson["requestFingerprint"] = AlteredFingerprint;
        var wrapperJson = copy.ToCanonicalJson();
        wrapperJson["terminal"]!["receiptFingerprint"] = AlteredFingerprint;
        wrapperJson["sourcePath"] = "changed external copy";
        Assert.Equal(before, result.ToCanonicalJson().ToJsonString());
        Assert.Equal(before, copy.ToCanonicalJson().ToJsonString());
        Assert.Equal(InnerReceipt, result.Receipt.ReceiptFingerprint);
        Assert.Equal(InnerReceipt, copy.Receipt.ReceiptFingerprint);
        Assert.Equal(result.SerializeRequest().ToJsonString(), copy.SerializeRequest().ToJsonString());
    }

    /// <summary>A detached policy INPUT survives the closed eight-field codec without acquiring authority.</summary>
    [Fact]
    public void Parse_PolicyVersion3LexicalInput_RoundTripsRawReceiptAndPreparation()
    {
        var terminal = PolicyTerminalInput();
        var originalInner = terminal.ToJsonString();
        var envelope = LexicalEnvelope(terminal, SyntheticSource, 1260);
        Assert.Equal(8, envelope.Count);
        // The original seven-field capture independently records this default
        // binding. Agreement is DATA evidence, not an owner or publication grant.
        Assert.Equal(PolicyInputOriginalBinding, envelope["bindingFingerprint"]!.GetValue<string>());
        var parsed = Parse(envelope, out var issues);
        Assert.Empty(issues);
        Assert.NotNull(parsed);
        Assert.Equal(envelope.ToJsonString(), parsed.ToCanonicalJson().ToJsonString());
        Assert.Equal(originalInner, parsed.ToCanonicalJson()["terminal"]!.ToJsonString());
        Assert.Equal(PolicyInnerReceipt, parsed.Receipt.ReceiptFingerprint);
        Assert.IsType<MortalWoundApplyDeteriorationOperation>(parsed.Receipt.DeclaredResult[0]);
        var comparison = parsed.ReconstructHistoricalPublication();
        Assert.Equal(envelope["outcomePreparationFingerprint"]!.GetValue<string>(), comparison.Preparation.Fingerprint);
        Assert.Equal(DeriveDefaultBinding(comparison.Resolution.Coordinates), comparison.BindingFingerprint);
        Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(comparison.Resolution.OutcomeIntents[0]);
    }

    /// <summary>A changed well-formed preparation comparator is not repaired by resealing only the outer wrapper.</summary>
    [Fact]
    public void Parse_ChangedPreparationComparatorWithNewOuterSeal_Rejects()
    {
        var envelope = DetachedEnvelope();
        Assert.NotEqual(AlteredFingerprint, envelope["outcomePreparationFingerprint"]!.GetValue<string>());
        envelope["outcomePreparationFingerprint"] = AlteredFingerprint;
        Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Policy binding/source and exact minute enter the comparator while inner receipt bytes stay fixed.</summary>
    [Theory]
    [InlineData("bindingFingerprint")]
    [InlineData("sourcePath")]
    [InlineData("publicationMinute")]
    public void Parse_ResealedPolicyMetadataWithUnchangedInnerAndPreparation_Rejects(string field)
    {
        var envelope = LexicalEnvelope(PolicyTerminalInput(), SyntheticSource, 1260);
        var originalInner = envelope["terminal"]!.ToJsonString();
        var originalPreparation = envelope["outcomePreparationFingerprint"]!.GetValue<string>();
        if (field == "bindingFingerprint") envelope[field] = AlteredFingerprint;
        else if (field == "sourcePath") envelope[field] = SyntheticSource + ".foreign_lexical_source";
        else envelope[field] = 1261L;
        var (request, receipt) = DecodeTerminalInput(envelope["terminal"]!.AsObject());
        var changedPreparation = RecomposeLexicalPreparation(request, receipt,
            envelope["bindingFingerprint"]!.GetValue<string>(), envelope["sourcePath"]!.GetValue<string>(),
            envelope["publicationMinute"]!.GetValue<long>());
        Assert.NotEqual(originalPreparation, changedPreparation.Fingerprint);
        Reseal(envelope);
        Assert.Equal(originalInner, envelope["terminal"]!.ToJsonString());
        Assert.Equal(originalPreparation, envelope["outcomePreparationFingerprint"]!.GetValue<string>());
        AssertRejected(envelope);
    }

    /// <summary>A default binding mismatch is refused even when the no-policy preparation does not depend on it.</summary>
    [Fact]
    public void Parse_ResealedForeignDefaultBindingWithoutPolicy_Rejects()
    {
        var envelope = DetachedEnvelope();
        var (request, receipt) = DecodeTerminalInput(envelope["terminal"]!.AsObject());
        var expected = DeriveDefaultBinding(request.Coordinates);
        Assert.Equal(expected, envelope["bindingFingerprint"]!.GetValue<string>());
        Assert.NotEqual(expected, AlteredFingerprint);
        var changedPreparation = RecomposeLexicalPreparation(request, receipt, AlteredFingerprint,
            SyntheticSource, envelope["publicationMinute"]!.GetValue<long>());
        Assert.Equal(envelope["outcomePreparationFingerprint"]!.GetValue<string>(), changedPreparation.Fingerprint);
        envelope["bindingFingerprint"] = AlteredFingerprint;
        Reseal(envelope);
        AssertRejected(envelope);
    }

    /// <summary>Builds only synthetic lexical version2 witness DATA, never a publication authority.</summary>
    private static JsonObject GuidedDetachedEnvelope()
    {
        var envelope = DetachedEnvelope();
        envelope["schemaVersion"] = 2;
        envelope.Remove("publicationMinute");
        envelope.Remove("outcomePreparationFingerprint");
        envelope["currentMinute"] = 1260;
        envelope["originalScheduleBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("{}"));
        envelope["publicationInputs"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["skillCommands"] = new JsonObject(),
            ["itemCommands"] = new JsonObject
            {
                ["UpdateInventory"] = null, ["moveInventoryItems"] = null, ["removeInventoryItems"] = null,
                ["NPCInventoryAdds"] = null, ["NPCInventoryUpdates"] = null, ["NPCInventoryRemovals"] = null,
                ["NPCEquipmentChanges"] = null
            }
        };
        Reseal(envelope);
        return envelope;
    }

    /// <summary>Loads an independent mutable copy of the strict detached terminal INPUT.</summary>
    private static JsonObject TerminalInput() => JsonNode.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "Fixtures", "treatment-semantic-terminal-v1.json")))!["Result"]!.DeepClone().AsObject();

    /// <summary>Constructs lexical parser input only; it is not evidence of an actual publication.</summary>
    private static JsonObject DetachedEnvelope(long minute = 1260) =>
        LexicalEnvelope(TerminalInput(), SyntheticSource, minute);

    /// <summary>Loads only raw adverse INPUT, never an old History envelope to backfill or reseal as genuine.</summary>
    private static JsonObject PolicyTerminalInput()
    {
        var input = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Fixtures", "treatment-history-adverse-input-v1.json")))!.AsObject();
        Assert.Equal("detached_raw_treatment_input_only", input["FixtureKind"]!.GetValue<string>());
        return input["Result"]!.DeepClone().AsObject();
    }

    /// <summary>Builds detached lexical comparison DATA through the raw codec and pure typed calculators only.</summary>
    private static JsonObject LexicalEnvelope(JsonObject terminal, string source, long minute)
    {
        var (request, receipt) = DecodeTerminalInput(terminal);
        var binding = DeriveDefaultBinding(request.Coordinates);
        var preparation = RecomposeLexicalPreparation(request, receipt, binding, source, minute);
        var envelope = new JsonObject
        {
            ["kind"] = "treat", ["schemaVersion"] = 3, ["terminal"] = terminal,
            ["bindingFingerprint"] = binding, ["sourcePath"] = source,
            ["publicationMinute"] = minute, ["outcomePreparationFingerprint"] = preparation.Fingerprint
        };
        Reseal(envelope);
        return envelope;
    }

    /// <summary>Strictly decodes raw request/receipt INPUT without invoking the version3 parser under test.</summary>
    private static (MortalWoundTreatmentAttemptRequest Request, MortalWoundTreatmentReceipt Receipt)
        DecodeTerminalInput(JsonObject terminal)
    {
        var issues = new List<ValidationIssue>();
        Assert.True(MortalWoundTreatmentCommandCodec.TryParseHistoryResult(JsonSerializer.SerializeToElement(terminal),
            "lexicalHistory.rawInput", issues, out var request, out var receipt, out _), JsonSerializer.Serialize(issues));
        Assert.Empty(issues);
        Assert.NotNull(request); Assert.NotNull(receipt);
        return (request, receipt);
    }

    /// <summary>Derives the deterministic default event-vector binding as DATA, never an accepted-state owner.</summary>
    private static string DeriveDefaultBinding(MortalWoundTreatmentAttemptCoordinates coordinates)
    {
        var events = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(coordinates.SessionId,
            coordinates.RequestId, coordinates.SnapshotToken, coordinates.Turn);
        Assert.True(events.Success, JsonSerializer.Serialize(events.Issues));
        Assert.Empty(events.Issues);
        var accepted = Assert.Single(events.Events);
        Assert.Equal(coordinates.EventRef, accepted.EventRef);
        Assert.Equal(coordinates.EventKind, accepted.Kind);
        Assert.Equal(coordinates.EventAuthorityId, accepted.AuthorityId);
        Assert.Equal(coordinates.EventSemanticFingerprint, accepted.SemanticFingerprint);
        return MortalWoundTreatmentAcceptedStateAuthority.HashBinding(new WoundAcceptedTurnBinding(
            coordinates.SessionId, coordinates.RequestId, coordinates.SnapshotToken, coordinates.Realm,
            coordinates.Turn, events.Events, events.EventsFingerprint));
    }

    /// <summary>Computes a lexical preparation comparator without Parse(version3), live authority or publication.</summary>
    private static MortalWoundTreatmentOutcomePreparation RecomposeLexicalPreparation(
        MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentReceipt receipt,
        string binding, string source, long minute)
    {
        Assert.True(MortalWoundTreatmentOutcomeIntentComposer.TryComposeDetached(request, receipt.DeclaredResult,
            binding, source, out var intents, out var issues), JsonSerializer.Serialize(issues));
        Assert.Empty(issues);
        var critical = request.ModeAuthority is MortalWoundProcedureCheckAuthority { PreparedCriticalReaction: { } prepared }
            ? MortalWoundCriticalReactionIntent.Create(request, prepared) : null;
        var resolution = MortalWoundTreatmentResolution.Create(request.Mode, request.Coordinates,
            receipt.AttemptDisposition, receipt.ResultCategory, receipt.SelectedOutcomeIndex, receipt.Interruption,
            receipt.DeclaredResult, intents, critical, receipt.ConsumptionTrigger, receipt.CourseId,
            receipt.CourseMilestoneOrdinal, receipt.CourseDisposition, request, receipt.ModeEvidence,
            receipt.RouteFingerprint, receipt.RouteCompletion);
        Assert.Equal(receipt.ReceiptFingerprint, MortalWoundTreatmentReceipt.Create(resolution).ReceiptFingerprint);
        var outcome = MortalWoundTreatmentOutcomePublicationPlanner.RecomposeDetached(request, resolution, minute);
        Assert.True(outcome.IsValid, JsonSerializer.Serialize(outcome.Issues));
        return Assert.IsType<MortalWoundTreatmentOutcomePreparation>(outcome.Preparation);
    }

    /// <summary>Seals adversarial lexical input without granting publication authority.</summary>
    private static void Reseal(JsonObject input) =>
        input["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(input);

    /// <summary>Exercises the production parser directly through its existing typed API.</summary>
    private static MortalWoundTreatmentPersistedResult? Parse(JsonObject input, out List<ValidationIssue> issues)
    {
        issues = [];
        return MortalWoundTreatmentPersistedResult.Parse(JsonSerializer.SerializeToElement(input), "historyResult", issues);
    }

    /// <summary>Requires refusal rather than successful defaulting or an unhandled exception.</summary>
    private static void AssertRejected(JsonObject input)
    {
        var result = Parse(input, out var issues);
        Assert.Null(result);
        Assert.NotEmpty(issues);
    }
}
