using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T064-A RED contract for the pure pending Mortal-occurrence state.  This deliberately
/// names no filesystem, lease, writer, pending-snapshot, or adapter surface: those are
/// T064/T070 orchestration concerns, not parser authority.
/// </summary>
public sealed class MortalWoundOccurrenceStateTests
{
    private const string RootPath = "game_state/control/pending_mortal_wound_occurrences.json";
    private const string RowPath = RootPath + ".occurrences[0]";

    public static TheoryData<string> AdapterKinds => new()
    {
        "formal", "qte", "combat", "trap", "check", "hazard", "narrative"
    };

    public static TheoryData<string, string, string> MortalOwners => new()
    {
        { "player", "player_current", "game_state/player/wounds.json" },
        { "npc", "npc_field_medic_01", "game_state/npcs/npc_wounds.json" },
        { "combatant", "combatant_01", "game_state/combat/enemies.json" },
        { "combatant_member", "combatant_member_01", "game_state/combat/allies.json" }
    };

    [Fact]
    public void PureApi_ContainsOnlyParserSerializerFingerprintAndPureAppendSurfaces()
    {
        var type = RequiredType();
        AssertStatic(type, "Parse", 2, "MortalWoundOccurrenceParseResult");
        AssertStatic(type, "SerializeCanonical", 1, typeof(string).Name);
        AssertStatic(type, "ComputeOccurrenceFingerprint", 1, typeof(string).Name);
        var append = ExactStatic(type, "PlanAppend", 3, "MortalWoundOccurrenceAppendPlanResult");
        Assert.Equal("MortalWoundOccurrenceState", append.GetParameters()[0].ParameterType.Name);
        Assert.Equal("MortalWoundOccurrenceCandidateBatch", append.GetParameters()[1].ParameterType.Name);
        Assert.Equal("MortalWoundOpportunityReceiptState", append.GetParameters()[2].ParameterType.Name);

        Assert.DoesNotContain(
            type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            constructor => constructor.IsPublic || constructor.IsAssembly || constructor.IsFamilyOrAssembly);

        Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance), method =>
            method.Name.Contains("File", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Lease", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Write", StringComparison.OrdinalIgnoreCase) ||
            method.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains("FileSystemManager", StringComparison.Ordinal) ||
                parameter.ParameterType.Name.Contains("CanonicalWriteLease", StringComparison.Ordinal)));
    }

    [Fact]
    public void CandidateBatch_IsClosedSemanticInputAndCannotSupplyDerivedOccurrenceAuthority()
    {
        var assembly = typeof(WoundMaterializationContract).Assembly;
        var batch = assembly.GetType("BookOfEternityClient.Services.MortalWoundOccurrenceCandidateBatch", false, false)
            ?? throw new Xunit.Sdk.XunitException("T064-A requires a typed occurrence candidate batch.");
        Assert.Equal(new[] { "Candidates" }, batch.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name).OrderBy(name => name));

        var candidate = assembly.GetType("BookOfEternityClient.Services.MortalWoundOccurrenceCandidate", false, false)
            ?? throw new Xunit.Sdk.XunitException("T064-A requires a typed source-result candidate.");
        Assert.Equal(
            new[]
            {
                "AcceptedEventOrdinal", "AcceptedEvents", "AdapterKind", "Domain", "GuaranteedTrigger", "HardMaximumSeverityRank",
                "MinimumSeverityRank",
                "Outcome", "Owner", "ProducerCandidateCount", "ProducerCandidateOrdinal", "ProducerOperationKey",
                "ProfileKey", "SafeContext", "Source", "SourceRequestId", "SourceResultFingerprint", "SourceSessionId",
                "SourceSnapshotToken", "SourceTurn", "WorseningTarget"
            },
            candidate.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).OrderBy(name => name));
        Assert.DoesNotContain(candidate.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "OccurrenceId" or "OpportunityRef" or "AcceptedEventsFingerprint" or
            "CandidateFingerprint" or "OccurrenceFingerprint" or "GuaranteedTriggerAuthorityFingerprint");
        Assert.Equal("WoundGuaranteedTriggerEvidence", candidate.GetProperty("GuaranteedTrigger")!.PropertyType.Name);
    }

    [Fact]
    public void Parse_RootAndEveryNestedObjectAreClosedCompleteAndRejectDuplicateRawProperties()
    {
        foreach (var json in new string?[] { null, string.Empty, " ", "[]", "null", "{" })
            AssertInvalid(Parse(json), RootPath, "mortal_wound_occurrence_invalid_root");

        foreach (var field in new[] { "schemaVersion", "occurrences" })
        {
            var root = Root();
            root.Remove(field);
            AssertInvalid(Parse(root), RootPath + "." + field, "mortal_wound_occurrence_missing_field");
        }

        var unknownRoot = Root();
        unknownRoot["legacyOccurrences"] = new JsonArray();
        AssertInvalid(Parse(unknownRoot), RootPath + ".legacyOccurrences", "mortal_wound_occurrence_unknown_field");

        foreach (var field in RequiredRowFields)
        {
            var row = Row();
            row.Remove(field);
            AssertInvalid(Parse(Root(row)), RowPath + "." + field, "mortal_wound_occurrence_missing_field");
        }

        foreach (var nested in new[] { "owner", "source", "outcome", "safeContext", "guaranteedTrigger" })
        {
            var row = Row();
            row[nested] = new JsonObject { ["forged"] = true };
            AssertInvalid(Parse(Root(row)), RowPath + "." + nested, "mortal_wound_occurrence_missing_field");
        }

        var baseline = Root(Row());
        var occurrenceId = baseline["occurrences"]![0]!["occurrenceId"]!.GetValue<string>();
        var duplicate = baseline.ToJsonString().Replace(
            $"\"occurrenceId\":\"{occurrenceId}\"",
            $"\"occurrenceId\":\"{occurrenceId}\",\"occurrenceId\":\"forged\"",
            StringComparison.Ordinal);
        AssertInvalid(Parse(duplicate), RowPath + ".occurrenceId", "mortal_wound_occurrence_duplicate_property");
    }

    [Fact]
    public void Parse_RejectsWrongSchemaAndScalarTypesBeforeAnySemanticAuthorityIsCreated()
    {
        foreach (var schema in new JsonNode?[] { 0, 2, "1", null })
        {
            var root = Root();
            root["schemaVersion"] = schema?.DeepClone();
            AssertInvalid(Parse(root), RootPath + ".schemaVersion", "mortal_wound_occurrence_invalid_field");
        }

        var decimalSchema = Root().ToJsonString().Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":1.0",
            StringComparison.Ordinal);
        AssertInvalid(Parse(decimalSchema), RootPath + ".schemaVersion", "mortal_wound_occurrence_invalid_field");

        var nonArray = Root();
        nonArray["occurrences"] = new JsonObject();
        AssertInvalid(Parse(nonArray), RootPath + ".occurrences", "mortal_wound_occurrence_invalid_field");

        var nonObject = Root();
        nonObject["occurrences"] = new JsonArray("forged");
        AssertInvalid(Parse(nonObject), RowPath, "mortal_wound_occurrence_invalid_field");

        foreach (var field in new[] { "sourceTurn", "producerCandidateOrdinal", "producerCandidateCount", "acceptedEventOrdinal", "hardMaximumSeverityRank" })
        {
            var row = Row();
            row[field] = "1";
            AssertInvalid(Parse(Root(row)), RowPath + "." + field, "mortal_wound_occurrence_invalid_field");
        }
    }

    [Fact]
    public void Parse_EachNestedOccurrenceObjectIsClosedCompleteAndDuplicateSafe()
    {
        var required = new Dictionary<string, string[]>
        {
            ["acceptedEvents[0]"] = ["eventRef", "kind", "authorityId", "semanticFingerprint"],
            ["owner"] = ["realm", "ownerKind", "ownerId", "carrierPath"],
            ["source"] = ["kind", "sourceId", "state"],
            ["outcome"] = ["kind", "maximumSeverityRank", "readableCause"],
            ["safeContext"] = ["target", "cause", "allowedLocationKinds"],
            ["guaranteedTrigger"] = ["triggerId", "sourceKind", "sourceId", "sourceState", "realm", "domain", "owner", "requiredSeverityRank", "materializedAtTurn", "sourceContractFingerprint", "authorityFingerprint"],
            ["worseningTarget"] = ["woundId", "causeKind"]
        };

        foreach (var (nestedPath, fields) in required)
        {
            foreach (var field in fields)
            {
                var row = Row(worseningTarget: new JsonObject { ["woundId"] = "wound_active", ["causeKind"] = "retrauma" }, minimumSeverityRank: 2);
                RemoveNested(row, nestedPath, field);
                AssertInvalid(Parse(Root(row)), RowPath + "." + nestedPath + "." + field,
                    "mortal_wound_occurrence_missing_field");
            }

            var unknown = Row(worseningTarget: new JsonObject { ["woundId"] = "wound_active", ["causeKind"] = "retrauma" }, minimumSeverityRank: 2);
            NestedObject(unknown, nestedPath)["forged"] = true;
            AssertInvalid(Parse(Root(unknown)), RowPath + "." + nestedPath + ".forged",
                "mortal_wound_occurrence_unknown_field");
        }

        var raw = Root(Row(
            worseningTarget: new JsonObject { ["woundId"] = "wound_active", ["causeKind"] = "retrauma" },
            minimumSeverityRank: 2)).ToJsonString(new System.Text.Json.JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
        foreach (var (token, path) in new[]
                 {
                     ("\"eventRef\":\"event_41_0\"", ".acceptedEvents[0].eventRef"),
                     ("\"ownerKind\":\"player\"", ".owner.ownerKind"),
                     ("\"sourceId\":\"source_41\"", ".source.sourceId"),
                     ("\"readableCause\":\"Подтвержденное повреждение.\"", ".outcome.readableCause"),
                     ("\"target\":\"рука\"", ".safeContext.target"),
                     ("\"triggerId\":\"guarantee_trigger_41\"", ".guaranteedTrigger.triggerId"),
                     ("\"causeKind\":\"retrauma\"", ".worseningTarget.causeKind")
                 })
        {
            var duplicate = raw.Replace(token, token + "," + token, StringComparison.Ordinal);
            AssertInvalid(Parse(duplicate), RowPath + path, "mortal_wound_occurrence_duplicate_property");
        }
    }

    [Theory]
    [MemberData(nameof(AdapterKinds))]
    public void Parse_AcceptsExactlyTheSevenRegisteredMortalAdapterKinds(string adapterKind)
    {
        var result = Parse(Root(Row(adapterKind: adapterKind)));

        AssertValid(result);
        Assert.Equal(adapterKind, Required(Required(result, "State"), "Occurrences", 0, "AdapterKind"));
    }

    [Fact]
    public void Parse_RejectsAdapterKindDomainOutcomeAndSeverityOutsideTheClosedMortalEnvelope()
    {
        foreach (var mutation in new Action<JsonObject>[]
                 {
                     row => row["adapterKind"] = "item",
                     row => row["domain"] = "spiritual",
                     row => row["outcome"]!["kind"] = "harmless",
                     row => row["outcome"]!["maximumSeverityRank"] = 0,
                     row => row["outcome"]!["maximumSeverityRank"] = 5,
                     row => row["hardMaximumSeverityRank"] = 0,
                     row => row["hardMaximumSeverityRank"] = 5
                 })
        {
            var row = Row();
            mutation(row);
            AssertInvalid(Parse(Root(row)), RowPath, "mortal_wound_occurrence_invalid_field");
        }
    }

    [Fact]
    public void Parse_RejectsResealedMalformedSemanticCoordinatesFingerprintsAndCandidateCount()
    {
        foreach (var (mutation, path) in new (Action<JsonObject> Mutation, string Path)[]
                 {
                      (row => row["sourceSessionId"] = " source_session_41", ".sourceSessionId"),
                      (row => row["sourceRequestId"] = "source_request_41\u0001", ".sourceRequestId"),
                      (row => row["sourceSnapshotToken"] = " source_snapshot_41", ".sourceSnapshotToken"),
                     (row => row["producerOperationKey"] = " source_batch_formal", ".producerOperationKey"),
                     (row => row["producerCandidateCount"] = 33, ".producerCandidateCount"),
                     (row => row["profileKey"] = " mortal_formal_injury_v1", ".profileKey"),
                     (row => row["source"]!["kind"] = " formal_retrauma", ".source.kind"),
                     (row => row["source"]!["sourceId"] = "source_41\u0001", ".source.sourceId"),
                     (row => row["source"]!["state"] = " active", ".source.state"),
                     (row => row["acceptedEvents"]![0]!["eventRef"] = " event_41_0", ".acceptedEvents[0].eventRef"),
                     (row => row["acceptedEvents"]![0]!["kind"] = " formal_retrauma", ".acceptedEvents[0].kind"),
                     (row => row["acceptedEvents"]![0]!["authorityId"] = " event_authority_41_0", ".acceptedEvents[0].authorityId"),
                     (row => row["acceptedEvents"]![0]!["semanticFingerprint"] = "sha256:" + new string('A', 64), ".acceptedEvents[0].semanticFingerprint"),
                     (row => row["sourceResultFingerprint"] = "source-result-not-a-fingerprint", ".sourceResultFingerprint")
                 })
        {
            var row = Row();
            mutation(row);
            RecomputeOccurrenceFingerprints(row);
            AssertInvalidAny(Parse(Root(row)), RowPath + path);
        }

        foreach (var (mutation, path) in new (Action<JsonObject> Mutation, string Path)[]
                 {
                     (row => row["guaranteedTrigger"]!["triggerId"] = " guarantee_trigger_41", ".guaranteedTrigger.triggerId"),
                     (row => row["guaranteedTrigger"]!["sourceContractFingerprint"] = "not-a-fingerprint", ".guaranteedTrigger.sourceContractFingerprint")
                 })
        {
            var row = Row(minimumSeverityRank: 2);
            mutation(row);
            ResealGuaranteedTrigger(row);
            RecomputeOccurrenceFingerprints(row);
            AssertInvalidAny(Parse(Root(row)), RowPath + path);
        }
    }

    [Fact]
    public void Parse_RequiresExplicitOrdinaryNullPairOrOneCompleteSealedGuaranteedTrigger()
    {
        AssertValid(Parse(Root(Row())));

        foreach (var (mutate, path) in new (Action<JsonObject> Mutate, string Path)[]
                 {
                     (row => row.Remove("minimumSeverityRank"), ".minimumSeverityRank"),
                     (row => row.Remove("guaranteedTrigger"), ".guaranteedTrigger"),
                     (row => row["minimumSeverityRank"] = 2, ".guaranteedTrigger"),
                     (row => row["guaranteedTrigger"] = GuaranteedTrigger(row, 2), ".minimumSeverityRank")
                 })
        {
            var ordinary = Row();
            mutate(ordinary);
            RecomputeOccurrenceFingerprints(ordinary);
            AssertInvalidAny(Parse(Root(ordinary)), RowPath + path);
        }

        foreach (var invalidRank in new[] { 0, 5 })
        {
            var invalid = Row(minimumSeverityRank: 2);
            invalid["minimumSeverityRank"] = invalidRank;
            invalid["guaranteedTrigger"]!["requiredSeverityRank"] = invalidRank;
            ResealGuaranteedTrigger(invalid);
            RecomputeOccurrenceFingerprints(invalid);
            AssertInvalidAny(Parse(Root(invalid)), RowPath + ".minimumSeverityRank");
        }

        var guaranteed = Row(minimumSeverityRank: 2);
        AssertValid(Parse(Root(guaranteed)));
        Assert.NotEqual(Row()["candidateFingerprint"]!.GetValue<string>(), guaranteed["candidateFingerprint"]!.GetValue<string>());
        Assert.NotEqual(Row()["occurrenceFingerprint"]!.GetValue<string>(), guaranteed["occurrenceFingerprint"]!.GetValue<string>());

        foreach (var field in new[]
                 {
                     "triggerId", "sourceKind", "sourceId", "sourceState", "realm", "domain", "owner",
                     "requiredSeverityRank", "materializedAtTurn", "sourceContractFingerprint", "authorityFingerprint"
                 })
        {
            var incomplete = Row(minimumSeverityRank: 2);
            incomplete["guaranteedTrigger"]!.AsObject().Remove(field);
            AssertInvalidAny(Parse(Root(incomplete)), RowPath + ".guaranteedTrigger." + field);
        }

        foreach (var ownerField in new[] { "realm", "ownerKind", "ownerId", "carrierPath" })
        {
            var incompleteOwner = Row(minimumSeverityRank: 2);
            incompleteOwner["guaranteedTrigger"]!["owner"]!.AsObject().Remove(ownerField);
            AssertInvalidAny(Parse(Root(incompleteOwner)), RowPath + ".guaranteedTrigger.owner." + ownerField);
        }

        var unknown = Row(minimumSeverityRank: 2);
        unknown["guaranteedTrigger"]!["forged"] = true;
        RecomputeOccurrenceFingerprints(unknown);
        AssertInvalidAny(Parse(Root(unknown)), RowPath + ".guaranteedTrigger.forged");

        var unknownOwner = Row(minimumSeverityRank: 2);
        unknownOwner["guaranteedTrigger"]!["owner"]!["forged"] = true;
        RecomputeOccurrenceFingerprints(unknownOwner);
        AssertInvalidAny(Parse(Root(unknownOwner)), RowPath + ".guaranteedTrigger.owner.forged");

        var recursive = Root(Row(minimumSeverityRank: 2)).ToJsonString();
        const string ownerKind = "\"ownerKind\":\"player\"";
        var ownerOffset = recursive.LastIndexOf(ownerKind, StringComparison.Ordinal);
        var duplicateOwner = recursive[..ownerOffset] + ownerKind + "," + ownerKind + recursive[(ownerOffset + ownerKind.Length)..];
        AssertInvalidAny(Parse(duplicateOwner), RowPath + ".guaranteedTrigger.owner.ownerKind");
    }

    [Fact]
    public void Parse_GuaranteedTriggerMustExactlyAgreeWithOccurrenceAndItsExistingAuthoritySeal()
    {
        foreach (var mutate in new Action<JsonObject>[]
                 {
                     row => row["guaranteedTrigger"]!["sourceKind"] = "other_source",
                     row => row["guaranteedTrigger"]!["sourceId"] = "other_source_id",
                     row => row["guaranteedTrigger"]!["sourceState"] = "accepted",
                     row => row["guaranteedTrigger"]!["realm"] = "chaos_sea",
                     row => row["guaranteedTrigger"]!["domain"] = "spiritual",
                     row => row["guaranteedTrigger"]!["owner"]!["ownerId"] = "other_owner",
                     row => row["guaranteedTrigger"]!["requiredSeverityRank"] = 1,
                     row => row["minimumSeverityRank"] = 3,
                     row => row["guaranteedTrigger"]!["requiredSeverityRank"] = 3,
                     row =>
                     {
                         row["minimumSeverityRank"] = 3;
                         row["guaranteedTrigger"]!["requiredSeverityRank"] = 3;
                     },
                     row => row["hardMaximumSeverityRank"] = 1,
                     row => row["guaranteedTrigger"]!["materializedAtTurn"] = 0,
                     row => row["guaranteedTrigger"]!["materializedAtTurn"] = 41
                 })
        {
            var row = Row(minimumSeverityRank: 2);
            mutate(row);
            ResealGuaranteedTrigger(row);
            RecomputeOccurrenceFingerprints(row);
            AssertInvalidAny(Parse(Root(row)), RowPath + ".guaranteedTrigger");
        }

        var forgedAuthority = Row(minimumSeverityRank: 2);
        forgedAuthority["guaranteedTrigger"]!["authorityFingerprint"] = ExternalFingerprint("forged-guarantee-authority");
        RecomputeOccurrenceFingerprints(forgedAuthority);
        AssertInvalidAny(Parse(Root(forgedAuthority)), RowPath + ".guaranteedTrigger.authorityFingerprint");

        var changedOccurrenceSourceState = Row(minimumSeverityRank: 2);
        changedOccurrenceSourceState["source"]!["state"] = "retired";
        RecomputeOccurrenceFingerprints(changedOccurrenceSourceState);
        AssertInvalidAny(Parse(Root(changedOccurrenceSourceState)), RowPath + ".guaranteedTrigger");
    }

    [Theory]
    [MemberData(nameof(MortalOwners))]
    public void Parse_AcceptsOnlyTheRegisteredMortalOwnerCarrierMappings(
        string ownerKind,
        string ownerId,
        string carrierPath)
    {
        var result = Parse(Root(Row(ownerKind: ownerKind, ownerId: ownerId, carrierPath: carrierPath)));

        AssertValid(result);
        Assert.Equal(ownerKind, Required(Required(result, "State"), "Occurrences", 0, "Owner", "OwnerKind"));

        var invalid = Row(ownerKind: ownerKind, ownerId: ownerId, carrierPath: carrierPath);
        invalid["owner"]!["carrierPath"] = "game_state/player/wounds.json#forged";
        AssertInvalid(Parse(Root(invalid)), RowPath + ".owner.carrierPath", "mortal_wound_occurrence_invalid_owner_mapping");
    }

    [Fact]
    public void Parse_EnforcesThirtyTwoRowsOneToOneHundredSixtyEventsAndTheSelectedEventOrdinal()
    {
        AssertValid(Parse(Root(Enumerable.Range(0, 32).Select(index =>
            Row(producerOperationKey: $"source_batch_{index}")).ToArray())));

        AssertInvalid(Parse(Root(Enumerable.Range(0, 33).Select(index =>
            Row(producerOperationKey: $"source_batch_{index}")).ToArray())), RootPath + ".occurrences", "mortal_wound_occurrence_limit_exceeded");

        var emptyEvents = Row();
        emptyEvents["acceptedEvents"] = new JsonArray();
        AssertInvalid(Parse(Root(emptyEvents)), RowPath + ".acceptedEvents", "mortal_wound_occurrence_invalid_event_set");

        var tooManyEvents = Row();
        tooManyEvents["acceptedEvents"] = new JsonArray(Enumerable.Range(0, 161)
            .Select(index => (JsonNode)new JsonObject
            {
                ["eventRef"] = $"event_{index}", ["kind"] = "formal_retrauma",
                ["authorityId"] = $"authority_{index}", ["semanticFingerprint"] = ExternalFingerprint($"event:{index}")
            }).ToArray());
        AssertInvalid(Parse(Root(tooManyEvents)), RowPath + ".acceptedEvents", "mortal_wound_occurrence_limit_exceeded");

        var selectedOutsideSet = Row();
        selectedOutsideSet["acceptedEventOrdinal"] = 1;
        AssertInvalid(Parse(Root(selectedOutsideSet)), RowPath + ".acceptedEventOrdinal", "mortal_wound_occurrence_invalid_event_ordinal");
    }

    [Fact]
    public void Parse_CreateRequiresAbsentWorseningTargetAndWorsenRequiresOneCompleteExactTarget()
    {
        AssertValid(Parse(Root(Row(worseningTarget: null))));

        var explicitNull = Row(worseningTarget: null);
        explicitNull["worseningTarget"] = null;
        AssertInvalid(Parse(Root(explicitNull)), RowPath + ".worseningTarget", "mortal_wound_occurrence_create_target_mismatch");

        foreach (var target in new JsonNode?[]
                 {
                     new JsonObject(),
                     new JsonObject { ["woundId"] = "wound_active" },
                     new JsonObject { ["woundId"] = "wound_active", ["causeKind"] = "unknown" }
                 })
        {
            var row = Row(worseningTarget: new JsonObject
            {
                ["woundId"] = "wound_active",
                ["causeKind"] = "retrauma"
            });
            row["worseningTarget"] = target?.DeepClone();
            AssertInvalid(Parse(Root(row)), RowPath + ".worseningTarget", "mortal_wound_occurrence_invalid_worsening_target");
        }

        foreach (var cause in new[] { "deterioration", "retrauma" })
            AssertValid(Parse(Root(Row(worseningTarget: new JsonObject { ["woundId"] = "wound_active", ["causeKind"] = cause }))));
    }

    [Fact]
    public void Parse_SafeContextUsesOneToFiveUniqueClosedLocationKindsAndBoundedTrimmedReadableText()
    {
        foreach (var kind in new[] { "anatomical", "systemic", "mental", "spiritual_axis", "other" })
        {
            var valid = Row();
            valid["safeContext"]!["allowedLocationKinds"] = new JsonArray(kind);
            RecomputeOccurrenceFingerprints(valid);
            AssertValid(Parse(Root(valid)));
        }

        foreach (var locations in new[]
                 {
                     new JsonArray(), new JsonArray("anatomical", "anatomical"), new JsonArray("anatomical", "other", "systemic", "mental", "spiritual_axis", "extra"),
                     new JsonArray("invented")
                 })
        {
            var row = Row();
            row["safeContext"]!["allowedLocationKinds"] = locations;
            AssertInvalid(Parse(Root(row)), RowPath + ".safeContext.allowedLocationKinds", "mortal_wound_occurrence_invalid_safe_context");
        }

        foreach (var field in new[] { "target", "cause" })
        {
            foreach (var value in new[] { "", " ", " x", "x ", "bad\u0001", new string('x', 2001) })
            {
                var row = Row();
                row["safeContext"]![field] = value;
                AssertInvalid(Parse(Root(row)), RowPath + ".safeContext." + field, "mortal_wound_occurrence_invalid_readable_text");
            }
        }

        foreach (var value in new[] { "", " ", " x", "x ", "bad\u0001", new string('x', 2001) })
        {
            var row = Row();
            row["outcome"]!["readableCause"] = value;
            AssertInvalid(Parse(Root(row)), RowPath + ".outcome.readableCause", "mortal_wound_occurrence_invalid_readable_text");
        }
    }

    [Fact]
    public void Parse_BatchRequiresPersistedCanonicalOrderAndAgreesOnEveryBatchScopedAuthorityField()
    {
        var first = Row(producerCandidateOrdinal: 0, producerCandidateCount: 2);
        first["acceptedEvents"]!.AsArray().Add(new JsonObject
        {
            ["eventRef"] = "event_41_1", ["kind"] = "formal_retrauma",
            ["authorityId"] = "event_authority_41_1", ["semanticFingerprint"] = ExternalFingerprint("event:formal_retrauma:1")
        });
        RecomputeOccurrenceFingerprints(first);
        var second = first.DeepClone().AsObject();
        second["producerCandidateOrdinal"] = 1;
        second["acceptedEventOrdinal"] = 1;
        RecomputeOccurrenceFingerprints(second);
        AssertInvalidAny(Parse(Root(second, first)), RootPath + ".occurrences[0].producerCandidateOrdinal");

        foreach (var mutate in new Action<JsonObject>[]
                 {
                     row => row["producerCandidateOrdinal"] = 2,
                     row => row["producerCandidateCount"] = 3,
                     row => row["sourceSessionId"] = "source_session_other",
                     row => row["sourceRequestId"] = "source_request_other",
                     row => row["sourceTurn"] = 99,
                     row => row["sourceResultFingerprint"] = ExternalFingerprint("changed-source-result")
                 })
        {
            var changed = second.DeepClone().AsObject();
            mutate(changed);
            RecomputeOccurrenceFingerprints(changed);
            AssertInvalid(Parse(Root(first, changed)), RootPath + ".occurrences[1]", "mortal_wound_occurrence_batch_agreement_mismatch");
        }

        var differentCompleteEventSet = second.DeepClone().AsObject();
        differentCompleteEventSet["acceptedEvents"]![1]!["eventRef"] = "event_41_forged";
        RecomputeOccurrenceFingerprints(differentCompleteEventSet);
        AssertInvalid(Parse(Root(first, differentCompleteEventSet)), RootPath + ".occurrences[1].acceptedEvents",
            "mortal_wound_occurrence_batch_agreement_mismatch");
    }

    [Fact]
    public void Parse_RejectsExactAndConfusableIdentityCollisionsAndForgedFingerprints()
    {
        var same = Row(producerOperationKey: "source_batch_other");
        same["occurrenceId"] = Row()["occurrenceId"]!.DeepClone();
        same["occurrenceFingerprint"] = ComputeOccurrenceFingerprint(same);
        AssertInvalidAny(Parse(Root(Row(), same)), RootPath + ".occurrences[1].occurrenceId");

        var first = Row(producerOperationKey: "source_batch_o");
        var confusable = Row(producerOperationKey: "source_batch_other");
        confusable["occurrenceId"] = first["occurrenceId"]!.GetValue<string>().Replace("occurrence", "оccurrence", StringComparison.Ordinal);
        confusable["occurrenceFingerprint"] = ComputeOccurrenceFingerprint(confusable);
        AssertInvalidAny(Parse(Root(first, confusable)), RootPath + ".occurrences[1].occurrenceId");

        var opportunityLatin = Row(producerOperationKey: "source_batch_opportunity_1");
        var opportunityCyrillic = Row(producerOperationKey: "source_batch_opportunity_2");
        opportunityCyrillic["opportunityRef"] = opportunityLatin["opportunityRef"]!.GetValue<string>().Replace("mortal_wound", "mortal_wоund", StringComparison.Ordinal);
        opportunityCyrillic["occurrenceFingerprint"] = ComputeOccurrenceFingerprint(opportunityCyrillic);
        AssertInvalidAny(Parse(Root(opportunityLatin, opportunityCyrillic)), RootPath + ".occurrences[1].opportunityRef");

        var coordinateLatin = Row(producerOperationKey: "producer_batch_o");
        var coordinateCyrillic = Row(producerOperationKey: "producer_batch_о");
        AssertInvalidAny(Parse(Root(coordinateLatin, coordinateCyrillic)), RootPath + ".occurrences[1].producerOperationKey");

        var partialLatin = Row(
            producerOperationKey: "producer_partial_o",
            producerCandidateOrdinal: 0,
            producerCandidateCount: 2);
        var partialCyrillic = Row(
            producerOperationKey: "producer_partial_о",
            producerCandidateOrdinal: 1,
            producerCandidateCount: 2);
        AssertInvalidAny(
            Parse(Root(partialLatin, partialCyrillic)),
            RootPath + ".occurrences[1].producerOperationKey");

        foreach (var (field, code) in new[]
                 {
                     ("acceptedEventsFingerprint", "mortal_wound_occurrence_accepted_events_fingerprint_mismatch"),
                     ("candidateFingerprint", "mortal_wound_occurrence_candidate_fingerprint_mismatch"),
                     ("occurrenceFingerprint", "mortal_wound_occurrence_fingerprint_mismatch")
                 })
        {
            var forged = Row();
            forged[field] = ExternalFingerprint("forged-" + field);
            AssertInvalid(Parse(Root(forged)), RowPath + "." + field, code);
        }
    }

    [Fact]
    public void PlanAppend_RequiresACompleteBatchAndCoalescesOnlyAnExactSourceRetryAcrossConsumedReceipts()
    {
        var type = RequiredType();
        var method = ExactStatic(type, "PlanAppend", 3);
        Assert.Equal("MortalWoundOccurrenceAppendPlanResult", method.ReturnType.Name);
        Assert.Equal("MortalWoundOccurrenceState", method.GetParameters()[0].ParameterType.Name);
        Assert.Equal("MortalWoundOccurrenceCandidateBatch", method.GetParameters()[1].ParameterType.Name);
        Assert.Equal("MortalWoundOpportunityReceiptState", method.GetParameters()[2].ParameterType.Name);
        Assert.DoesNotContain(method.GetParameters(), parameter =>
            parameter.ParameterType == typeof(JsonNode) || parameter.ParameterType == typeof(JsonObject) ||
            parameter.ParameterType.Name.Contains("FileSystemManager", StringComparison.Ordinal));

        // The typed request must contain the whole 0..count-1 batch.  A result exposes
        // only immutable disposition/issues/after-state; canonical IDs and fingerprints
        // are recomputed inside the state helper rather than supplied by the caller.
        AssertClosedResult(method.ReturnType, "Disposition", "Issues", "State", "BatchFingerprint");
    }

    [Fact]
    public void PlanAppend_CanonicallyAppendsReversedCandidatesAndClassifiesExactAndChangedSourceRetries()
    {
        var before = MortalWoundOccurrenceState.Parse("{\"schemaVersion\":1,\"occurrences\":[]}", RootPath).State!;
        var consumed = MortalWoundOpportunityReceiptState.Parse("{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}",
            "game_state/wounds/wound_opportunity_receipts.json").State!;
        var first = Candidate(0, 2);
        var second = Candidate(1, 2);
        var reversed = new MortalWoundOccurrenceCandidateBatch(new[] { second, first });

        var appended = MortalWoundOccurrenceState.PlanAppend(before, reversed, consumed);
        Assert.Equal("appended", appended.Disposition);
        Assert.Empty(appended.Issues);
        Assert.Equal(new[] { 0, 1 }, appended.State!.Occurrences.Select(value => value.ProducerCandidateOrdinal));
        Assert.Equal(ComputeBatchFingerprint(appended.State.Occurrences), appended.BatchFingerprint);

        var replay = MortalWoundOccurrenceState.PlanAppend(appended.State, reversed, consumed);
        Assert.Equal("exact_replay", replay.Disposition);
        Assert.Empty(replay.Issues);
        Assert.Equal(appended.BatchFingerprint, replay.BatchFingerprint);
        Assert.Equal(
            MortalWoundOccurrenceState.SerializeCanonical(appended.State),
            MortalWoundOccurrenceState.SerializeCanonical(replay.State!));

        var consumedOccurrence = appended.State.Occurrences[0];
        var decisionFingerprint = ExternalFingerprint("decision:none:split_consumption");
        var receiptDraft = new MortalWoundOpportunityReceiptDraft(
            new MortalWoundOpportunityDecisionBinding("session_42", "request_42", "snapshot_42", 42),
            consumedOccurrence.OccurrenceId,
            ExternalFingerprint("opportunity:" + consumedOccurrence.OccurrenceId),
            "none",
            decisionFingerprint,
            "wound_operation_" + decisionFingerprint["sha256:".Length..],
            null,
            null);
        var emptyHistory = WoundHistoryState.Parse(WoundContractTestData.CreateHistory().ToJsonString(), WoundHistoryState.HistoryPath).State!;
        var receiptAppend = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            appended.State,
            consumed,
            receiptDraft,
            emptyHistory);
        Assert.Equal("appended", receiptAppend.Disposition);
        var splitReplay = MortalWoundOccurrenceState.PlanAppend(
            receiptAppend.OccurrenceState!,
            reversed,
            receiptAppend.ReceiptState!);
        Assert.Equal("exact_replay", splitReplay.Disposition);
        Assert.Equal(
            MortalWoundOccurrenceState.SerializeCanonical(receiptAppend.OccurrenceState!),
            MortalWoundOccurrenceState.SerializeCanonical(splitReplay.State!));

        var duplicateAcrossRoots = MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
            appended.State,
            receiptAppend.ReceiptState!);
        Assert.NotEmpty(duplicateAcrossRoots);

        var changedSourceResult = ExternalFingerprint("changed-source-retry");
        var changed = new MortalWoundOccurrenceCandidateBatch(new[]
        {
            first with { SourceResultFingerprint = changedSourceResult },
            second with { SourceResultFingerprint = changedSourceResult }
        });
        var conflict = MortalWoundOccurrenceState.PlanAppend(appended.State, changed, consumed);
        Assert.Equal("conflict", conflict.Disposition);
        Assert.Null(conflict.State);
        Assert.NotEmpty(conflict.Issues);

        var incomplete = new MortalWoundOccurrenceCandidateBatch(new[] { first });
        var incompleteResult = MortalWoundOccurrenceState.PlanAppend(before, incomplete, consumed);
        Assert.Equal("conflict", incompleteResult.Disposition);
        Assert.Null(incompleteResult.State);
    }

    [Fact]
    public void PlanAppend_RejectsAResealedColdReplayReceiptWhenAnyRetainedOccurrenceCoordinateChanged()
    {
        var emptyPending = MortalWoundOccurrenceState.Parse(
            "{\"schemaVersion\":1,\"occurrences\":[]}",
            RootPath).State!;
        var emptyReceipts = MortalWoundOpportunityReceiptState.Parse(
            "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}",
            "game_state/wounds/wound_opportunity_receipts.json").State!;
        var first = Candidate(0, 2);
        var second = Candidate(1, 2);
        var reversed = new MortalWoundOccurrenceCandidateBatch(new[] { second, first });
        var appended = MortalWoundOccurrenceState.PlanAppend(emptyPending, reversed, emptyReceipts);
        Assert.Equal("appended", appended.Disposition);

        var occurrence = appended.State!.Occurrences[0];
        var decisionFingerprint = ExternalFingerprint("decision:none:cold-replay-tamper");
        var receiptDraft = new MortalWoundOpportunityReceiptDraft(
            new MortalWoundOpportunityDecisionBinding("session_42", "request_42", "snapshot_42", 42),
            occurrence.OccurrenceId,
            ExternalFingerprint("opportunity:" + occurrence.OccurrenceId),
            "none",
            decisionFingerprint,
            "wound_operation_" + decisionFingerprint["sha256:".Length..],
            null,
            null);
        var consumed = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            appended.State,
            emptyReceipts,
            receiptDraft,
            WoundHistoryState.Parse(
                WoundContractTestData.CreateHistory().ToJsonString(),
                WoundHistoryState.HistoryPath).State!);
        Assert.Equal("appended", consumed.Disposition);

        var mutations = new (Action<JsonObject> Mutate, bool ResealIdentity)[]
        {
            (row => row["eventRef"] = "event_changed", false),
            (ChangeConsumedEventEvidence, false),
            (row => row["sourceSessionId"] = "source_session_changed", false),
            (row => row["sourceRequestId"] = "source_request_changed", false),
            (row => row["sourceTurn"] = 99, false),
            (row => row["producerOperationKey"] = "source_batch_changed", false),
            (row => row["producerCandidateOrdinal"] = 1, false),
            (row => row["producerCandidateCount"] = 3, false),
            (row => row["sourceResultFingerprint"] = ExternalFingerprint("source-result:changed"), false),
            (row => row["candidateFingerprint"] = ExternalFingerprint("candidate:changed"), true)
        };

        foreach (var (mutate, resealIdentity) in mutations)
        {
            var changedReceipts = MortalWoundOpportunityReceiptStateTests.MutateCanonicalReceiptStateForTest(
                consumed.ReceiptState!,
                mutate,
                resealIdentity);

            var replay = MortalWoundOccurrenceState.PlanAppend(
                consumed.OccurrenceState!,
                reversed,
                changedReceipts);

            Assert.Equal("conflict", replay.Disposition);
            Assert.Null(replay.State);
            Assert.NotEmpty(replay.Issues);
        }
    }

    [Fact]
    public void SerializeCanonical_RoundTripsDetachedStateAndUsesNoCallerOwnedJson()
    {
        var result = Parse(Root(Row()));
        AssertValid(result);
        var state = Required(result, "State");
        var serialized = (string)ExactStatic(RequiredType(), "SerializeCanonical", 1).Invoke(null, new[] { state })!;
        var reparsed = Parse(serialized);
        AssertValid(reparsed);
        Assert.Equal(serialized, (string)ExactStatic(RequiredType(), "SerializeCanonical", 1).Invoke(null, new[] { Required(reparsed, "State") })!);
        Assert.NotSame(state, Required(reparsed, "State"));
    }

    private static readonly string[] RequiredRowFields =
    {
        "occurrenceId", "opportunityRef", "sourceSessionId", "sourceRequestId", "sourceSnapshotToken", "sourceTurn",
        "producerOperationKey", "producerCandidateOrdinal", "producerCandidateCount", "adapterKind",
        "acceptedEventOrdinal", "acceptedEvents", "acceptedEventsFingerprint", "owner", "domain",
        "profileKey", "source", "outcome", "hardMaximumSeverityRank", "minimumSeverityRank",
        "guaranteedTrigger", "safeContext",
        "sourceResultFingerprint", "candidateFingerprint", "occurrenceFingerprint"
    };

    private static void ChangeConsumedEventEvidence(JsonObject row)
    {
        var selection = row["consumedEventSelection"]!.AsObject();
        selection["readableCause"] = "Измененная проверяемая причина ранения.";
        row["eventSemanticFingerprint"] =
            WoundOpportunityEventEvidenceFingerprint.Compute(
                new WoundOpportunityEventEvidence(
                    selection["adapterKind"]!.GetValue<string>(),
                    selection["authorityKind"]!.GetValue<string>(),
                    selection["authorityId"]!.GetValue<string>(),
                    selection["outcomeKind"]!.GetValue<string>(),
                    selection["maximumSeverityRank"]!.GetValue<int>(),
                    selection["readableCause"]!.GetValue<string>()));
    }

    private static object Parse(JsonObject root) => Parse(root.ToJsonString());
    private static object Parse(string? json) => Invoke(ExactStatic(RequiredType(), "Parse", 2), json!, RootPath);

    private static JsonObject NestedObject(JsonObject row, string path) => path switch
    {
        "acceptedEvents[0]" => row["acceptedEvents"]!.AsArray()[0]!.AsObject(),
        _ => row[path]!.AsObject()
    };

    private static void RemoveNested(JsonObject row, string path, string field) =>
        NestedObject(row, path).Remove(field);

    private static JsonObject Root(params JsonObject[] rows) => new()
    {
        ["schemaVersion"] = 1,
        ["occurrences"] = new JsonArray(rows.Select(row => (JsonNode)row.DeepClone()).ToArray())
    };

    internal static JsonObject CreateCanonicalOccurrenceRoot(bool guaranteed = false, bool worsening = false) =>
        Root(Row(
            worseningTarget: worsening
                ? new JsonObject { ["woundId"] = "wound_active", ["causeKind"] = "retrauma" }
                : null,
            minimumSeverityRank: guaranteed ? 2 : null));

    internal static JsonObject CreateCanonicalOccurrenceBatchRoot()
    {
        var first = Row(producerCandidateOrdinal: 0, producerCandidateCount: 2);
        first["acceptedEvents"]!.AsArray().Add(new JsonObject
        {
            ["eventRef"] = "event_41_1",
            ["kind"] = "formal_retrauma",
            ["authorityId"] = "event_authority_41_1",
            ["semanticFingerprint"] = WoundOpportunityEventEvidenceFingerprint.Compute(
                new WoundOpportunityEventEvidence(
                    "formal",
                    "formal_retrauma",
                    "event_authority_41_1",
                    "harmful",
                    2,
                    "Подтвержденное повреждение."))
        });
        RecomputeOccurrenceFingerprints(first);
        var second = first.DeepClone().AsObject();
        second["producerCandidateOrdinal"] = 1;
        second["acceptedEventOrdinal"] = 1;
        RecomputeOccurrenceFingerprints(second);
        return Root(first, second);
    }

    private static MortalWoundOccurrenceCandidate Candidate(int ordinal, int count) => new(
        SourceSessionId: "source_session_41",
        SourceRequestId: "source_request_41",
        SourceSnapshotToken: "source_snapshot_41",
        SourceTurn: 41,
        ProducerOperationKey: "source_batch_append_41",
        ProducerCandidateOrdinal: ordinal,
        ProducerCandidateCount: count,
        AdapterKind: "formal",
        AcceptedEventOrdinal: ordinal,
        AcceptedEvents:
        [
            new WoundAcceptedEventAuthority(
                "event_41_0",
                "formal_retrauma",
                "event_authority_41_0",
                WoundOpportunityEventEvidenceFingerprint.Compute(new WoundOpportunityEventEvidence(
                    "formal", "formal_retrauma", "event_authority_41_0", "harmful", 2, "Подтвержденное повреждение."))),
            new WoundAcceptedEventAuthority(
                "event_41_1",
                "formal_retrauma",
                "event_authority_41_1",
                WoundOpportunityEventEvidenceFingerprint.Compute(new WoundOpportunityEventEvidence(
                    "formal", "formal_retrauma", "event_authority_41_1", "harmful", 2, "Подтвержденное повреждение.")))
        ],
        Owner: new WoundOwnerCoordinate("mortal_world", "player", "player_current", "game_state/player/wounds.json"),
        Domain: "physical",
        ProfileKey: "mortal_formal_injury_v1",
        Source: new MortalWoundOccurrenceSource("formal_retrauma", "source_41", "active"),
        Outcome: new MortalWoundOccurrenceOutcome("harmful", 2, "Подтвержденное повреждение."),
        HardMaximumSeverityRank: 4,
        MinimumSeverityRank: null,
        GuaranteedTrigger: null,
        SafeContext: new WoundOpportunitySafeContext("рука", "повторная травма", ["anatomical"]),
        WorseningTarget: null,
        SourceResultFingerprint: ExternalFingerprint("source-result:source_batch_append_41"));

    private static string ComputeBatchFingerprint(IEnumerable<MortalWoundOccurrence> occurrences)
    {
        var ordered = occurrences.OrderBy(value => value.ProducerCandidateOrdinal).ToArray();
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound.occurrence_candidate_batch", "1",
            ordered[0].ProducerOperationKey,
            ordered.Length.ToString(CultureInfo.InvariantCulture)
        };
        foreach (var occurrence in ordered)
        {
            fields.Add(occurrence.ProducerCandidateOrdinal.ToString(CultureInfo.InvariantCulture));
            fields.Add(occurrence.CandidateFingerprint);
        }
        return Hash(fields);
    }

    private static JsonObject Row(
        string adapterKind = "formal", string producerOperationKey = "source_batch_formal",
        int producerCandidateOrdinal = 0, int producerCandidateCount = 1,
        string ownerKind = "player", string ownerId = "player_current",
        string carrierPath = "game_state/player/wounds.json", JsonObject? worseningTarget = null,
        int? minimumSeverityRank = null)
    {
        var sourceKind = adapterKind == "formal" ? "formal_retrauma" : adapterKind;
        var eventSemanticFingerprint = WoundOpportunityEventEvidenceFingerprint.Compute(
            new WoundOpportunityEventEvidence(
                adapterKind,
                sourceKind,
                "event_authority_41_0",
                "harmful",
                2,
                "Подтвержденное повреждение."));
        var sourceResultFingerprint = ExternalFingerprint("source-result:" + producerOperationKey);
        var acceptedEventsFingerprint = Hash(
            "book_of_eternity.wound.accepted_event_set", "1", "1", "0", "event_41_0",
            sourceKind, "event_authority_41_0", eventSemanticFingerprint);
        var row = new JsonObject
        {
        ["occurrenceId"] = string.Empty, ["opportunityRef"] = string.Empty,
        ["sourceSessionId"] = "source_session_41", ["sourceRequestId"] = "source_request_41",
        ["sourceSnapshotToken"] = "source_snapshot_41", ["sourceTurn"] = 41,
        ["producerOperationKey"] = producerOperationKey, ["producerCandidateOrdinal"] = producerCandidateOrdinal,
        ["producerCandidateCount"] = producerCandidateCount, ["adapterKind"] = adapterKind,
        ["acceptedEventOrdinal"] = 0,
        ["acceptedEvents"] = new JsonArray(new JsonObject { ["eventRef"] = "event_41_0", ["kind"] = sourceKind, ["authorityId"] = "event_authority_41_0", ["semanticFingerprint"] = eventSemanticFingerprint }),
        ["acceptedEventsFingerprint"] = acceptedEventsFingerprint,
        ["owner"] = new JsonObject { ["realm"] = "mortal_world", ["ownerKind"] = ownerKind, ["ownerId"] = ownerId, ["carrierPath"] = carrierPath },
        ["domain"] = "physical", ["profileKey"] = $"mortal_{adapterKind}_injury_v1",
        ["source"] = new JsonObject { ["kind"] = sourceKind, ["sourceId"] = "source_41", ["state"] = "active" },
        ["outcome"] = new JsonObject { ["kind"] = "harmful", ["maximumSeverityRank"] = 2, ["readableCause"] = "Подтвержденное повреждение." },
        ["hardMaximumSeverityRank"] = 4,
        ["minimumSeverityRank"] = minimumSeverityRank,
        ["guaranteedTrigger"] = null,
        ["safeContext"] = new JsonObject { ["target"] = "рука", ["cause"] = "повторная травма", ["allowedLocationKinds"] = new JsonArray("anatomical") },
            ["sourceResultFingerprint"] = sourceResultFingerprint, ["candidateFingerprint"] = string.Empty, ["occurrenceFingerprint"] = string.Empty
        };
        if (worseningTarget is not null)
            row["worseningTarget"] = worseningTarget.DeepClone();
        if (minimumSeverityRank is not null)
            row["guaranteedTrigger"] = GuaranteedTrigger(row, minimumSeverityRank.Value);
        RecomputeOccurrenceFingerprints(row);
        return row;
    }

    private static void AssertValid(object result)
    {
        Assert.True(Assert.IsType<bool>(Required(result, "IsValid")), Describe(result));
        Assert.NotNull(Required(result, "State"));
        Assert.Empty(Values(result, "Issues"));
    }

    private static void AssertInvalid(object result, string path, string code)
    {
        Assert.False(Assert.IsType<bool>(Required(result, "IsValid")));
        Assert.Null(PropertyValue(result, "State"));
        Assert.Contains(Values(result, "Issues"), issue =>
            Equals(Required(issue!, "Code"), code) && Equals(Required(issue!, "FilePath"), path));
    }

    private static void AssertInvalidAny(object result, string path)
    {
        Assert.False(Assert.IsType<bool>(Required(result, "IsValid")));
        Assert.Null(PropertyValue(result, "State"));
        Assert.Contains(Values(result, "Issues"), issue => Equals(Required(issue!, "FilePath"), path));
    }

    private static Type RequiredType() => typeof(WoundMaterializationContract).Assembly.GetType(
        "BookOfEternityClient.Services.MortalWoundOccurrenceState", false, false)
        ?? throw new Xunit.Sdk.XunitException("T064-A requires pure MortalWoundOccurrenceState.");

    private static MethodInfo ExactStatic(Type type, string name, int arity, string? resultName = null)
    {
        var method = Assert.Single(
            type.GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate => candidate.Name == name && candidate.GetParameters().Length == arity);
        if (resultName is not null) Assert.Equal(resultName, method.ReturnType.Name);
        return method;
    }

    private static object Invoke(MethodInfo method, params object?[] arguments) =>
        method.Invoke(null, arguments) ?? throw new Xunit.Sdk.XunitException($"{method.Name} returned null.");

    private static object Required(object instance, string property) =>
        instance.GetType().GetProperty(property)?.GetValue(instance)
        ?? throw new Xunit.Sdk.XunitException($"{instance.GetType().Name}.{property} is required.");

    private static object? PropertyValue(object instance, string property)
    {
        var info = instance.GetType().GetProperty(property)
            ?? throw new Xunit.Sdk.XunitException($"{instance.GetType().Name}.{property} is required.");
        return info.GetValue(instance);
    }

    private static object Required(object instance, string collection, int index, params string[] chain)
    {
        object value = ((IEnumerable)Required(instance, collection)).Cast<object>().ElementAt(index);
        foreach (var property in chain) value = Required(value, property);
        return value;
    }

    private static IReadOnlyList<object?> Values(object instance, string property) =>
        ((IEnumerable)Required(instance, property)).Cast<object?>().ToArray();

    private static string Describe(object result) => string.Join(Environment.NewLine, Values(result, "Issues").Select(issue =>
        $"{Required(issue!, "Code")} {Required(issue!, "FilePath")}"));

    private static void AssertStatic(Type type, string name, int arity, string returnTypeName) =>
        Assert.Equal(returnTypeName, ExactStatic(type, name, arity).ReturnType.Name);

    private static void AssertClosedResult(Type type, params string[] properties) =>
        Assert.Equal(properties.OrderBy(value => value), type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name).OrderBy(value => value));

    private static string ComputeCandidateFingerprint(JsonObject row)
    {
        var owner = row["owner"]!.AsObject();
        var source = row["source"]!.AsObject();
        var outcome = row["outcome"]!.AsObject();
        var safeContext = row["safeContext"]!.AsObject();
        var locations = safeContext["allowedLocationKinds"]!.AsArray();
        var target = row["worseningTarget"] as JsonObject;
        var fields = new List<string?>
        {
            "book_of_eternity.mortal_wound.occurrence_candidate", "1",
            Value(row, "sourceSessionId"), Value(row, "sourceRequestId"), Value(row, "sourceSnapshotToken"),
            Number(row, "sourceTurn"),
            Value(row, "producerOperationKey"), Number(row, "producerCandidateOrdinal"), Number(row, "producerCandidateCount"),
            Value(row, "adapterKind"), Number(row, "acceptedEventOrdinal"), Value(row, "acceptedEventsFingerprint"),
            Value(owner, "realm"), Value(owner, "ownerKind"), Value(owner, "ownerId"), Value(owner, "carrierPath"),
            Value(row, "domain"), Value(row, "profileKey"),
            Value(source, "kind"), Value(source, "sourceId"), Value(source, "state"),
            Value(outcome, "kind"), Number(outcome, "maximumSeverityRank"), Value(outcome, "readableCause"),
            Number(row, "hardMaximumSeverityRank"), NullableNumber(row, "minimumSeverityRank"),
            ComputeGuaranteedAuthorityFingerprint(row["guaranteedTrigger"] as JsonObject),
            Value(safeContext, "target"), Value(safeContext, "cause"),
            locations.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < locations.Count; index++)
        {
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(locations[index]!.GetValue<string>());
        }
        fields.Add(target is null ? "create" : "worsen");
        fields.Add(target is null ? null : Value(target, "woundId"));
        fields.Add(target is null ? null : Value(target, "causeKind"));
        fields.Add(Value(row, "sourceResultFingerprint"));
        return Hash(fields);
    }

    private static void RecomputeOccurrenceFingerprints(JsonObject row)
    {
        row["acceptedEventsFingerprint"] = ComputeAcceptedEventsFingerprint(row);
        var candidateFingerprint = ComputeCandidateFingerprint(row);
        row["candidateFingerprint"] = candidateFingerprint;
        var candidateHex = candidateFingerprint["sha256:".Length..];
        row["occurrenceId"] = "mortal_wound_occurrence_" + candidateHex;
        row["opportunityRef"] = "mortal_wound_" + candidateHex;
        row["occurrenceFingerprint"] = ComputeOccurrenceFingerprint(row);
    }

    private static string ComputeAcceptedEventsFingerprint(JsonObject row)
    {
        var events = row["acceptedEvents"]!.AsArray();
        var fields = new List<string?>
        {
            "book_of_eternity.wound.accepted_event_set", "1",
            events.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < events.Count; index++)
        {
            var item = events[index]!.AsObject();
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(Value(item, "eventRef"));
            fields.Add(Value(item, "kind"));
            fields.Add(Value(item, "authorityId"));
            fields.Add(Value(item, "semanticFingerprint"));
        }
        return Hash(fields);
    }

    private static string ComputeOccurrenceFingerprint(JsonObject row) => Hash(
        "book_of_eternity.mortal_wound.occurrence", "1",
        Value(row, "occurrenceId"), Value(row, "opportunityRef"), Value(row, "candidateFingerprint"));

    private static JsonObject GuaranteedTrigger(JsonObject occurrence, int requiredSeverityRank)
    {
        var owner = occurrence["owner"]!.AsObject();
        var source = occurrence["source"]!.AsObject();
        var trigger = new JsonObject
        {
            ["triggerId"] = "guarantee_trigger_41",
            ["sourceKind"] = Value(source, "kind"),
            ["sourceId"] = Value(source, "sourceId"),
            ["sourceState"] = "active",
            ["realm"] = Value(owner, "realm"),
            ["domain"] = Value(occurrence, "domain"),
            ["owner"] = owner.DeepClone(),
            ["requiredSeverityRank"] = requiredSeverityRank,
            ["materializedAtTurn"] = occurrence["sourceTurn"]!.GetValue<int>() - 1,
            ["sourceContractFingerprint"] = ExternalFingerprint("guarantee-source-contract"),
            ["authorityFingerprint"] = string.Empty
        };
        trigger["authorityFingerprint"] = ComputeGuaranteedAuthorityFingerprint(trigger);
        return trigger;
    }

    private static string? ComputeGuaranteedAuthorityFingerprint(JsonObject? trigger)
    {
        if (trigger is null) return null;
        var owner = trigger["owner"]!.AsObject();
        return Hash(
            "book_of_eternity.wound.guaranteed_trigger_authority", "1",
            Value(trigger, "triggerId"), Value(trigger, "sourceKind"), Value(trigger, "sourceId"),
            Value(trigger, "sourceState"), Value(trigger, "realm"), Value(trigger, "domain"),
            Value(owner, "realm"), Value(owner, "ownerKind"), Value(owner, "ownerId"), Value(owner, "carrierPath"),
            Number(trigger, "requiredSeverityRank"), Number(trigger, "materializedAtTurn"),
            Value(trigger, "sourceContractFingerprint"));
    }

    private static void ResealGuaranteedTrigger(JsonObject occurrence)
    {
        var trigger = occurrence["guaranteedTrigger"] as JsonObject;
        if (trigger is not null)
            trigger["authorityFingerprint"] = ComputeGuaranteedAuthorityFingerprint(trigger);
    }

    private static string ExternalFingerprint(string label) => Hash("test.mortal_wound.external", "1", label);

    private static string Hash(params string?[] fields) => Hash((IEnumerable<string?>)fields);

    private static string Hash(IEnumerable<string?> fields)
    {
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            if (field is null)
            {
                builder.Append("-1:");
                continue;
            }
            builder.Append(Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(field);
        }
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string Value(JsonObject node, string field) => node[field]!.GetValue<string>();
    private static string Number(JsonObject node, string field) => node[field]!.GetValue<int>().ToString(CultureInfo.InvariantCulture);
    private static string? NullableNumber(JsonObject node, string field) => node[field] is null
        ? null
        : Number(node, field);
}
