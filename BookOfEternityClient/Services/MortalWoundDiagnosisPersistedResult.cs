using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundDiagnosisPersistedRecovery(string OriginalSourcePath,
    WoundMaterializationEnvelope Original, WoundMaterializationEnvelope Working, long ConsumedRecovery,
    long ConsumedDeterioration, MortalWoundRecoveryResolution Resolution, ImmutableArray<MortalWoundRecoveryPublishedStage> Stages);

// Closed historical/comparison DATA. This class cannot mint an accepted source,
// reservation, execution receipt or publication permit.
internal sealed class MortalWoundDiagnosisPersistedResult : WoundTransitionResult
{
    private static readonly JsonSerializerOptions Options = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };
    private readonly JsonObject _root;
    private readonly WoundMaterializationEnvelope _original;
    private readonly WoundMaterializationEnvelope _knowledge;
    private MortalWoundDiagnosisPersistedResult(JsonObject root, WoundAcceptedTurnBinding binding,
        MortalWoundRecoverySourceEvidence source, MortalWoundTreatmentResolution resolution,
        WoundMaterializationEnvelope original, WoundMaterializationEnvelope knowledge,
        ImmutableArray<MortalWoundDiagnosisPersistedRecovery> recoveries)
    {
        _root = root.DeepClone().AsObject(); Binding = binding;
        Source = source with { AcceptedD20EventValues = source.AcceptedD20EventValues.ToImmutableArray() };
        Resolution = resolution; _original = original; _knowledge = knowledge; Recoveries = recoveries;
    }
    public override string Kind => "mortal_diagnosis";
    internal string ResultFingerprint => Text(_root, "resultFingerprint");
    internal string WorkingFingerprint => Text(_root, "workingFingerprint");
    internal WoundAcceptedTurnBinding Binding { get; }
    internal MortalWoundRecoverySourceEvidence Source { get; }
    internal MortalWoundTreatmentResolution Resolution { get; }
    internal MortalWoundTreatmentAttemptRequest Request => Resolution.RequestAuthority;
    internal WoundMaterializationEnvelope Original => WoundAcceptedTurnData.CloneWound(_original)!;
    internal WoundMaterializationEnvelope KnowledgeAfter => WoundAcceptedTurnData.CloneWound(_knowledge)!;
    internal ImmutableArray<MortalWoundDiagnosisPersistedRecovery> Recoveries { get; }
    internal override JsonObject ToCanonicalJson() => _root.DeepClone().AsObject();

    internal static MortalWoundDiagnosisPersistedResult CreateComparisonEvidence(MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundDiagnosisOutcome outcome, MortalWoundDiagnosisWorkingProjection working,
        ProgressionScheduleService.FrozenMortalCalculation progression,
        IReadOnlyDictionary<string, IReadOnlyList<MortalWoundRecoveryPublishedStage>> stages)
    {
        working.RequireCurrent(owner);
        outcome.RequireCurrent(owner, new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>()));
        if (!stages.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(working.Members.Select(value => value.Original.WoundId)))
            throw new InvalidDataException("Diagnosis recovery evidence must cover the complete admitted original cohort exactly.");
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(outcome.Resolution);
        if (!finalization.IsValid || finalization.Finalization is null) throw new InvalidDataException("Diagnosis resource finalization does not reconstruct.");
        var carriers = owner.ReadDiagnosisOriginalCarriers();
        var root = new JsonObject
        {
            ["kind"] = "mortal_diagnosis", ["schemaVersion"] = 1, ["workingFingerprint"] = working.Fingerprint,
            ["binding"] = MortalWoundRecoveryPersistedResult.BindingJson(owner.Binding),
            ["source"] = JsonSerializer.SerializeToNode(working.OriginalSource, Options),
            ["input"] = owner.ReadDiagnosisOriginalInput(),
            ["request"] = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(outcome.Request),
            ["original"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(outcome.Request.RouteSourceWound)),
            ["knowledgeAfter"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(outcome.KnowledgeAfter)),
            ["terminal"] = CreateTerminalProjection(outcome.Resolution),
            ["resourceFinalization"] = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(finalization.Finalization),
            ["originalCarriers"] = new JsonObject
            {
                [WoundCarrierCatalog.PlayerPath] = carriers.PlayerWounds, [WoundCarrierCatalog.NpcPath] = carriers.NpcWounds,
                [WoundCarrierCatalog.EnemiesPath] = carriers.EnemyCombatants, [WoundCarrierCatalog.AlliesPath] = carriers.AllyCombatants
            },
            ["clock"] = new JsonObject
            {
                ["originalMinute"] = working.OriginalMinute, ["resultingMinute"] = working.ResultingMinute,
                ["originalBase64"] = Convert.ToBase64String(working.ReadOriginalBytes(EffectAcceptedTurnInputComposer.WorldTimePath)),
                ["after"] = working.ClockAfter,
                ["scheduleBeforeBase64"] = Base64(progression.ScheduleBefore.Bytes), ["scheduleAfterBase64"] = Base64(progression.ScheduleAfter.Bytes),
                ["reportBeforeBase64"] = Base64(progression.ReportBefore.Bytes), ["reportAfterBase64"] = Base64(progression.ReportAfter.Bytes),
                ["progressionFingerprint"] = progression.MutationFingerprint
            },
            ["cohort"] = new JsonArray(working.Members.Select(value => (JsonNode?)new JsonObject
            {
                ["woundId"] = value.Original.WoundId, ["sourcePath"] = value.OriginalSourcePath,
                ["consumedRecovery"] = value.ConsumedRecovery, ["consumedDeterioration"] = value.ConsumedDeterioration,
                ["resolution"] = MortalWoundRecoveryPersistedResult.ResolutionJson(value.Resolution),
                ["stages"] = new JsonArray(stages[value.Original.WoundId].Select(stage => (JsonNode?)MortalWoundRecoveryPersistedResult.StageJson(stage)).ToArray())
            }).ToArray())
        };
        root["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(root);
        var issues = new List<ValidationIssue>();
        return Parse(JsonSerializer.SerializeToElement(root), "diagnosis.result", issues) ??
            throw new InvalidDataException(string.Join(";", issues.Select(value => value.Code + ": " + value.Actual)));
    }

    internal static MortalWoundDiagnosisPersistedResult? Parse(JsonElement element, string path, ICollection<ValidationIssue> issues)
    {
        try
        {
            if (!MortalWoundTreatmentCommandCodec.HasNoDuplicateProperties(element, path, issues))
                throw new InvalidDataException("Duplicate diagnosis result fields.");
            var root = JsonNode.Parse(element.GetRawText())?.AsObject() ?? throw new InvalidDataException("Diagnosis result is not an object.");
            Closed(root, "kind", "schemaVersion", "binding", "source", "input", "request", "original", "knowledgeAfter", "terminal",
                "resourceFinalization", "originalCarriers", "clock", "cohort", "workingFingerprint", "resultFingerprint");
            if (Text(root, "kind") != "mortal_diagnosis" || Integer(root, "schemaVersion") != 1 ||
                Text(root, "resultFingerprint") != WoundHistoryState.ComputeTransitionResultFingerprint(root))
                throw new InvalidDataException("Diagnosis result header or complete fingerprint differs.");
            var start = issues.Count;
            var binding = MortalWoundRecoveryPersistedResult.ParseBinding(JsonSerializer.SerializeToElement(Object(root, "binding")), path + ".binding", issues);
            var sourceNode = Object(root, "source");
            var source = sourceNode.Deserialize<MortalWoundRecoverySourceEvidence>(Options) ?? throw new InvalidDataException("Missing diagnosis original source.");
            if (!JsonNode.DeepEquals(sourceNode, JsonSerializer.SerializeToNode(source, Options)))
                throw new InvalidDataException("Original source is not its exact closed typed representation.");
            var original = Wound(Object(root, "original"), path + ".original");
            MortalWoundRecoveryPersistedResult.ValidateSource(source, binding, original);
            if (!MortalWoundTreatmentCommandCodec.TryParseRequest(Object(root, "request"), path + ".request", issues, out var request) || request is null)
                throw new InvalidDataException("The complete retained diagnosis request does not reconstruct.");
            var coordinates = request.Coordinates;
            if (request.Mode != "diagnosis" || !WoundResponseInputComposer.MatchesAcceptedBinding(binding, coordinates) ||
                coordinates.SessionGeneration != source.SessionGeneration || coordinates.ContextFingerprint != source.ContextFingerprint ||
                coordinates.AcceptedStateFingerprint != source.AcceptedStateFingerprint || coordinates.ExpectedBeforeFingerprint != source.WoundFingerprint ||
                WoundMaterializationContract.SerializeCanonical(original) != WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound))
                throw new InvalidDataException("Diagnosis request differs from its original source or binding.");
            if (request.ModeAuthority is not MortalWoundProcedureCheckAuthority check || check.SourceIndices.Count != check.SourceRolls.Count ||
                check.SourceIndices.Where((index, offset) => index < 0 || index >= source.AcceptedD20EventValues.Count ||
                    source.AcceptedD20EventValues[index] != check.SourceRolls[offset]).Any())
                throw new InvalidDataException("Diagnosis procedure indices/values differ from the retained accepted d20 pool.");
            var input = Object(root, "input");
            MortalWoundDiagnosisInputCodec.RequireRequestMatches(input, request);
            var resolution = MortalWoundDiagnosisOutcome.ReconstructResolution(request);
            if (!JsonNode.DeepEquals(Object(root, "terminal"), CreateTerminalProjection(resolution))) throw new InvalidDataException("Terminal diagnosis band/check/result was changed.");
            var knowledge = Wound(Object(root, "knowledgeAfter"), path + ".knowledgeAfter");
            var expectedKnowledge = MortalWoundDiagnosisOutcome.ReconstructKnowledge(resolution);
            if (WoundMaterializationContract.SerializeCanonical(knowledge) != WoundMaterializationContract.SerializeCanonical(expectedKnowledge))
                throw new InvalidDataException("Diagnosis did not preserve its complete declared knowledge-only result.");
            var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            if (!finalization.IsValid || finalization.Finalization is null || !JsonNode.DeepEquals(root["resourceFinalization"],
                WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(finalization.Finalization)))
                throw new InvalidDataException("Diagnosis resource consumption/refund does not reconstruct.");
            var clock = Object(root, "clock");
            Closed(clock, "originalMinute", "resultingMinute", "originalBase64", "after", "scheduleBeforeBase64", "scheduleAfterBase64",
                "reportBeforeBase64", "reportAfterBase64", "progressionFingerprint");
            var method = MortalWoundProcedureSubject.FromDetached(original, "diagnosis", coordinates.MemberId).Diagnosis!;
            var future = checked(source.CurrentGameMinute + method.Check.DurationMinutes);
            if (Integer(clock, "originalMinute") != source.CurrentGameMinute || Integer(clock, "resultingMinute") != future)
                throw new InvalidDataException("Diagnosis clock differs from original time plus the declared method duration.");
            var originalClock = Bytes(clock, "originalBase64") ?? throw new InvalidDataException("Missing original clock bytes.");
            var clockAfter = JsonNode.Parse(BookOfEternityClient.Core.FileSystemManager.DecodeFileBytes(originalClock))!.AsObject();
            if (clockAfter["currentTimeInMinutes"] is not JsonValue originalMinute || !originalMinute.TryGetValue<long>(out var retainedMinute) ||
                retainedMinute != source.CurrentGameMinute)
                throw new InvalidDataException("Diagnosis original clock differs from its retained source minute.");
            clockAfter["currentTimeInMinutes"] = future;
            if (!JsonNode.DeepEquals(clockAfter, Object(clock, "after"))) throw new InvalidDataException("Diagnosis changed unrelated clock fields or charged time twice.");
            var scheduleBefore = Bytes(clock, "scheduleBeforeBase64") ?? throw new InvalidDataException("Missing original schedule bytes.");
            var scheduleAfter = Bytes(clock, "scheduleAfterBase64") ?? throw new InvalidDataException("Missing resulting schedule bytes.");
            var preparedAt = JsonNode.Parse(BookOfEternityClient.Core.FileSystemManager.DecodeFileBytes(scheduleAfter))?["lastUpdatedUtc"]?.GetValue<string>()
                ?? throw new InvalidDataException("Missing actual progression preparation timestamp.");
            var progression = ProgressionScheduleService.ReconstructDiagnosisOutcomeData(Object(input, "control"), scheduleBefore, originalClock,
                Bytes(clock, "reportBeforeBase64"), binding.SessionId, binding.RequestId, binding.Turn, method.Check.DurationMinutes, preparedAt);
            if (!EqualBytes(progression.ScheduleAfter.Bytes, scheduleAfter) || !EqualBytes(progression.ReportAfter.Bytes, Bytes(clock, "reportAfterBase64")) ||
                progression.MutationFingerprint != Text(clock, "progressionFingerprint"))
                throw new InvalidDataException("Diagnosis schedule/report images differ from ordinary progression at the verified future clock.");
            var carriers = Object(root, "originalCarriers");
            Closed(carriers, WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath);
            JsonObject? Carrier(string name) => carriers[name] is null ? null : carriers[name]!.AsObject();
            var catalog = WoundCarrierCatalog.Build(new(Carrier(WoundCarrierCatalog.PlayerPath), Carrier(WoundCarrierCatalog.NpcPath),
                Carrier(WoundCarrierCatalog.EnemiesPath), Carrier(WoundCarrierCatalog.AlliesPath), null));
            if (catalog.Issues.Count != 0) throw new InvalidDataException("Diagnosis original carrier cohort is invalid.");
            var originals = catalog.Occurrences.Where(value => value.Wound.Lifecycle == "active" && value.Wound.Owner.Realm == "mortal_world" &&
                value.Wound.Classification.Domain == "physical").ToArray();
            if (root["cohort"] is not JsonArray cohort || cohort.Count != originals.Length || cohort.Count == 0 ||
                originals.Count(value => value.Wound.WoundId == original.WoundId && WoundMaterializationContract.SerializeCanonical(value.Wound) ==
                    WoundMaterializationContract.SerializeCanonical(original)) != 1)
                throw new InvalidDataException("Diagnosis omitted or replaced an original physical cohort member.");
            var recoveries = ImmutableArray.CreateBuilder<MortalWoundDiagnosisPersistedRecovery>();
            var allocated = new HashSet<string>(StringComparer.Ordinal) { knowledge.LastTransition.TransitionId };
            var operations = new HashSet<string>(StringComparer.Ordinal) { coordinates.OperationKey };
            for (var index = 0; index < originals.Length; index++)
            {
                var entry = cohort[index] as JsonObject ?? throw new InvalidDataException("Invalid diagnosis cohort row.");
                Closed(entry, "woundId", "sourcePath", "consumedRecovery", "consumedDeterioration", "resolution", "stages");
                var member = originals[index];
                if (Text(entry, "woundId") != member.Wound.WoundId || Text(entry, "sourcePath") != member.JsonPath)
                    throw new InvalidDataException("Diagnosis cohort order/identity/source differs from its original catalog.");
                var working = member.Wound.WoundId == original.WoundId ? knowledge : member.Wound;
                var consumedRecovery = Integer(entry, "consumedRecovery"); var consumedDeterioration = Integer(entry, "consumedDeterioration");
                if (consumedRecovery < 0 || consumedDeterioration < 0) throw new InvalidDataException("Negative original cadence consumption.");
                var calculated = MortalWoundRecoveryPlanner.ReconstructAtMinute(source, binding, working, future, consumedRecovery, consumedDeterioration);
                if (calculated.Resolution is null || !JsonNode.DeepEquals(entry["resolution"], MortalWoundRecoveryPersistedResult.ResolutionJson(calculated.Resolution)))
                    throw new InvalidDataException("Diagnosis ordinary recovery calculation was altered.");
                var stageNode = entry["stages"] as JsonArray ?? throw new InvalidDataException("Diagnosis stages must be an exact array.");
                IReadOnlyList<MortalWoundRecoveryPublishedStage> stages = stageNode.Count == 0
                    ? Array.Empty<MortalWoundRecoveryPublishedStage>()
                    : MortalWoundRecoveryPersistedResult.ParseStages(JsonSerializer.SerializeToElement(stageNode), path + $".cohort[{index}].stages", issues);
                MortalWoundRecoveryPersistedResult.ValidateDiagnosisWorkingStages(binding, working, calculated.Resolution, stages);
                foreach (var stage in stages)
                    if (!allocated.Add(stage.TransitionId) || !operations.Add(stage.OperationKey)) throw new InvalidDataException("Diagnosis recovery repeated an allocated transition/operation.");
                recoveries.Add(new(member.JsonPath, member.Wound, working, consumedRecovery, consumedDeterioration, calculated.Resolution, stages.ToImmutableArray()));
            }
            var originalSchedule = JsonNode.Parse(BookOfEternityClient.Core.FileSystemManager.DecodeFileBytes(scheduleBefore))!.AsObject();
            var workingFingerprint = MortalWoundDiagnosisWorkingProjection.ComputeComparisonFingerprint(request, resolution,
                source.AcceptedStateFingerprint, source.CurrentGameMinute, future, Object(input, "control"), originalSchedule, Object(clock, "after"),
                recoveries.Select(value => new MortalWoundDiagnosisRecoveryMember(value.OriginalSourcePath, value.Original, value.Working,
                    value.ConsumedRecovery, value.ConsumedDeterioration, value.Resolution)));
            if (Text(root, "workingFingerprint") != workingFingerprint) throw new InvalidDataException("Diagnosis working projection fingerprint differs from complete retained inputs.");
            if (issues.Count != start) throw new InvalidDataException("Closed diagnosis comparison subgraph was rejected.");
            return new(root, binding, source, resolution, original, knowledge, recoveries.ToImmutable());
        }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or ArgumentException or JsonException or
            OverflowException or FormatException or KeyNotFoundException)
        {
            issues.Add(new(path, IssueSeverity.Error, "Mortal diagnosis result cannot be reconstructed.",
                code: "mortal_wound_diagnosis_result_invalid", actor: "Client", section: "wound_materialization", actual: failure.Message));
            return null;
        }
    }

    internal static JsonObject CreateTerminalProjection(MortalWoundTreatmentResolution resolution) => new()
    {
        ["category"] = resolution.ResultCategory, ["selectedOutcomeIndex"] = resolution.SelectedOutcomeIndex,
        ["modeEvidence"] = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(resolution.ModeEvidence),
        ["resolutionFingerprint"] = resolution.ResolutionAuthorityFingerprint, ["resultFingerprint"] = resolution.ResultFingerprint,
        ["consumptionTrigger"] = resolution.ConsumptionTrigger
    };
    private static WoundMaterializationEnvelope Wound(JsonObject value, string path)
    {
        var parsed = WoundMaterializationContract.Parse(value.ToJsonString(), path);
        return parsed.IsValid && parsed.Wound is not null ? parsed.Wound : throw new InvalidDataException("Invalid canonical diagnosis wound: " + path);
    }
    private static void Closed(JsonObject root, params string[] fields)
    { if (root.Count != fields.Length || fields.Any(field => !root.ContainsKey(field))) throw new InvalidDataException("Missing or unknown diagnosis result fields."); }
    private static JsonObject Object(JsonObject root, string field) => root[field] as JsonObject ?? throw new InvalidDataException("Expected diagnosis object: " + field);
    private static string Text(JsonObject root, string field) => root[field] is JsonValue value && value.TryGetValue<string>(out var text)
        ? text : throw new InvalidDataException("Expected diagnosis string: " + field);
    private static long Integer(JsonObject root, string field) => root[field] is JsonValue value && value.TryGetValue<long>(out var number)
        ? number : throw new InvalidDataException("Expected exact diagnosis integer: " + field);
    private static string? Base64(byte[]? bytes) => bytes is null ? null : Convert.ToBase64String(bytes);
    private static byte[]? Bytes(JsonObject root, string field) => root[field] is null ? null : Convert.FromBase64String(Text(root, field));
    private static bool EqualBytes(byte[]? before, byte[]? after) => before is null ? after is null : after is not null && before.AsSpan().SequenceEqual(after);
}
