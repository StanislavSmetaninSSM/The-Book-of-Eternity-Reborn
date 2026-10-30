using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

/// <summary>Closed historical DATA for one diagnosis plus exactly one ordinary natural suffix.</summary>
internal sealed class SpiritualDiagnosisPersistedResult : WoundTransitionResult
{
    private readonly JsonObject _root;
    private readonly SpiritualDiagnosisProgram _program;
    private SpiritualDiagnosisPersistedResult(JsonObject root, SpiritualDiagnosisProgram program)
    { _root = root.DeepClone().AsObject(); _program = program; }
    public override string Kind => "spiritual_diagnosis";
    internal (string SessionId, string RequestId, int Turn) BindingIdentity =>
        (_root["binding"]!["sessionId"]!.GetValue<string>(), _root["binding"]!["requestId"]!.GetValue<string>(), _root["binding"]!["turn"]!.GetValue<int>());
    internal string ResultFingerprint => Text(_root, "resultFingerprint");
    internal WoundMaterializationEnvelope Original => _program.KnowledgeBefore;
    internal WoundMaterializationEnvelope KnowledgeAfter => _program.KnowledgeAfter;
    internal string NaturalResultFingerprint => Text(_root["naturalSuffix"]!.AsObject(), "resultFingerprint");
    internal SpiritualDiagnosisProgram Program => _program;
    internal JsonObject Request => _root["request"]!.DeepClone().AsObject();
    internal JsonObject Binding => _root["binding"]!.DeepClone().AsObject();
    internal bool MatchesBinding(string sessionId, string requestId, int turn) =>
        Text(_root["binding"]!.AsObject(), "sessionId") == sessionId &&
        Text(_root["binding"]!.AsObject(), "requestId") == requestId && _root["binding"]!["turn"]!.GetValue<int>() == turn;
    internal SpiritualHealingPublishedStage DiagnosisStage => new(Original, KnowledgeAfter, Text(_root, "operationKey"),
        Text(_root, "eventRef"), null, _root["request"]!["diagnosis"]!["attemptId"]!.GetValue<string>());
    internal override JsonObject ToCanonicalJson() => _root.DeepClone().AsObject();

    internal static SpiritualDiagnosisPersistedResult Create(AfterlifeWoundHealingPlanner.Continuation owner,
        IReadOnlyList<WoundExecutedBoundary> executed, SpiritualHealingPersistedResult natural)
    {
        var data = owner.DiagnosisSource ?? throw new InvalidDataException("Diagnosis result requires its actual continuation.");
        if (executed.Count != data.Program.Stages.Count || executed[0].Kind != "diagnose" ||
            WoundMaterializationContract.SerializeCanonical(executed[0].After) != WoundMaterializationContract.SerializeCanonical(data.Program.KnowledgeAfter))
            throw new InvalidDataException("Diagnosis result lost its actual executed knowledge boundary.");
        var binding = owner.Input.Binding;
        var admission = data.Candidate.Admission;
        var root = new JsonObject
        {
            ["kind"] = "spiritual_diagnosis", ["schemaVersion"] = 1,
            ["binding"] = new JsonObject { ["sessionId"] = binding.SessionId, ["requestId"] = binding.RequestId,
                ["snapshotToken"] = binding.SnapshotToken, ["realm"] = binding.Realm, ["turn"] = binding.Turn },
            ["request"] = data.Request.DeepClone(), ["original"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(admission.Wound)),
            ["knowledgeAfter"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(executed[0].After)),
            ["operationKey"] = executed[0].OperationKey, ["eventRef"] = executed[0].EventRef,
            ["admission"] = new JsonObject { ["healingTier"] = admission.HealingTier, ["pathFingerprint"] = admission.PathFingerprint,
                ["requirementFingerprint"] = admission.RequirementFingerprint, ["sourceFingerprint"] = admission.SourceFingerprint,
                ["requirementBindings"] = admission.RequirementBindings },
            ["originalCohort"] = new JsonArray(data.Candidate.Members.Select(m => (JsonNode)new JsonObject
            { ["wound"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(m.Wound)), ["healingTier"] = m.HealingTier }).ToArray()),
            ["originalSources"] = new JsonArray(data.OriginalSources.Select(s => (JsonNode)new JsonObject
            { ["path"] = s.Path, ["fingerprint"] = s.Fingerprint }).ToArray()),
            ["sourceFingerprint"] = data.SourceFingerprint, ["graphResponseFingerprint"] = owner.GraphResponseFingerprint,
            ["control"] = JsonSerializer.SerializeToNode(owner.Control),
            ["scheduleBeforeJson"] = CanonicalJsonUtf8.DecodeOneOptionalBom(owner.SchedulerMutation.ScheduleBefore.Bytes!),
            ["progressionReportJson"] = CanonicalJsonUtf8.DecodeOneOptionalBom(owner.SchedulerMutation.ReportBefore.Bytes!),
            ["scheduleAfterJson"] = owner.SchedulerMutation.ScheduleAfterJson,
            ["scheduleAfterFingerprint"] = owner.SchedulerMutation.ScheduleAfter.Fingerprint,
            ["naturalSuffix"] = new JsonObject { ["primaryTransitionId"] = natural.Stages[0].TransitionId,
                ["resultFingerprint"] = natural.ResultFingerprint,
                ["transitionIds"] = new JsonArray(natural.Stages.Select(s => (JsonNode)s.TransitionId).ToArray()) }
        };
        root["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(root);
        var issues = new List<ValidationIssue>();
        var result = Parse(JsonSerializer.SerializeToElement(root), "spiritualDiagnosis.result", issues)
            ?? throw new InvalidDataException("Diagnosis result rejected: " + string.Join(";", issues.Select(i => i.Actual)));
        result.RequireNatural(natural);
        return result;
    }

    internal static SpiritualDiagnosisPersistedResult? Parse(JsonElement element, string path, ICollection<ValidationIssue> issues)
    {
        try
        {
            var root = StrictJsonAuthority.Deserialize<JsonObject>(element.GetRawText(), new JsonSerializerOptions(), "spiritual diagnosis result")!;
            AfterlifeWoundTreatmentAuthority.MaterializeProperties(root);
            Closed(root, "kind", "schemaVersion", "binding", "request", "original", "knowledgeAfter", "operationKey", "eventRef",
                "admission", "originalCohort", "originalSources", "sourceFingerprint", "graphResponseFingerprint", "control",
                "scheduleBeforeJson", "progressionReportJson", "scheduleAfterJson", "scheduleAfterFingerprint", "naturalSuffix", "resultFingerprint");
            if (Text(root, "kind") != "spiritual_diagnosis" || root["schemaVersion"]!.GetValue<int>() != 1 ||
                Text(root, "resultFingerprint") != WoundHistoryState.ComputeTransitionResultFingerprint(root))
                throw new InvalidDataException("Diagnosis kind/version/result fingerprint differs.");
            var binding = root["binding"]!.AsObject(); Closed(binding, "sessionId", "requestId", "snapshotToken", "realm", "turn");
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" })
                if (!ResourceMaterializationContract.IsExactIdentifier(Text(binding, field))) throw new InvalidDataException("Invalid diagnosis binding.");
            var realm = Text(binding, "realm");
            if (realm is not ("chaos_sea" or "shining_abode") || binding["turn"]!.GetValue<int>() < 1) throw new InvalidDataException("Invalid diagnosis realm/turn.");
            var control = root["control"]!.Deserialize<ProgressionControl>()!;
            if (WoundAcceptedTurnFingerprintWriter.CanonicalJson(root["control"]) !=
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(control)))
                throw new InvalidDataException("Diagnosis control is not its complete exact shape.");
            var context = new SpiritualSelfTreatmentStageContext(Text(binding, "sessionId"), Text(binding, "requestId"), binding["turn"]!.GetValue<int>(), control);
            var request = root["request"]!.AsObject();
            var selection = SpiritualDiagnosisRequestCodec.ReadSelection(request);
            var original = ReadWound(root["original"], path + ".original");
            var after = ReadWound(root["knowledgeAfter"], path + ".knowledgeAfter");
            var admitted = root["admission"]!.AsObject();
            Closed(admitted, "healingTier", "pathFingerprint", "requirementFingerprint", "sourceFingerprint", "requirementBindings");
            var admission = new SpiritualDiagnosisAdmission(selection, original, admitted["healingTier"]!.GetValue<int>(),
                Text(admitted, "pathFingerprint"), Text(admitted, "requirementFingerprint"), Text(admitted, "sourceFingerprint"))
            { RequirementBindings = admitted["requirementBindings"]!.AsArray() };
            if (original.Owner.Realm != realm || !SpiritualDiagnosisRequestCodec.IsExact(request, context, selection, admission))
                throw new InvalidDataException("Diagnosis original/admission/retained request differs.");
            var cohort = root["originalCohort"]!.AsArray().Select((node, index) =>
            {
                var row = node!.AsObject(); Closed(row, "wound", "healingTier");
                return new SpiritualWoundRecoveryMember(ReadWound(row["wound"], path + ".originalCohort[" + index + "]"), row["healingTier"]!.GetValue<int>());
            }).ToArray();
            var program = SpiritualDiagnosisProgram.Build(context, request, admission, cohort);
            var sourceRows = root["originalSources"]!.AsArray();
            var expectedPaths = AfterlifeWoundTreatmentAuthority.DiagnosisSourcePaths(realm);
            if (sourceRows.Count != expectedPaths.Count) throw new InvalidDataException("Diagnosis original dependency set differs.");
            var stamps = sourceRows.Select((node, index) =>
            {
                var row = node!.AsObject(); Closed(row, "path", "fingerprint");
                if (Text(row, "path") != expectedPaths[index] || !ResourceMaterializationContract.IsAuthorityFingerprint(Text(row, "fingerprint")))
                    throw new InvalidDataException("Foreign diagnosis original source/order/fingerprint.");
                return new AfterlifeWoundTreatmentAuthority.DiagnosisSourceStamp(Text(row, "path"), Text(row, "fingerprint"));
            }).ToArray();
            if (Text(root, "sourceFingerprint") != AfterlifeWoundTreatmentAuthority.ComputeDiagnosisSourceFingerprint(context, stamps) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(Text(root, "graphResponseFingerprint")) ||
                WoundMaterializationContract.SerializeCanonical(after) != WoundMaterializationContract.SerializeCanonical(program.KnowledgeAfter) ||
                Text(root, "operationKey") != program.Stages[0].OperationKey)
                throw new InvalidDataException("Diagnosis source, knowledge or operation differs.");
            var authority = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(context.SessionId, context.RequestId,
                Text(binding, "snapshotToken"), context.Turn);
            if (!authority.Success || authority.Events.Count != 1 || Text(root, "eventRef") != authority.Events[0].EventRef)
                throw new InvalidDataException("Diagnosis lost its exact accepted event.");
            var scheduleJson = Text(root, "scheduleAfterJson");
            var schedule = StrictJsonAuthority.Deserialize<JsonObject>(scheduleJson, new JsonSerializerOptions(), "diagnosis schedule")!;
            var scheduleBytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(scheduleJson)).ToArray();
            if (Text(root, "scheduleAfterFingerprint") != new CanonicalBeforeImage(true, scheduleBytes).Fingerprint ||
                !ProgressionScheduleService.SpiritualPublishedScheduleAgrees(schedule, Text(root, "scheduleBeforeJson"),
                    Text(root, "progressionReportJson"), control, context.SessionId, context.RequestId, context.Turn, realm))
                throw new InvalidDataException("Diagnosis did not settle exactly its admitted safe cycle.");
            var suffix = root["naturalSuffix"]!.AsObject(); Closed(suffix, "primaryTransitionId", "resultFingerprint", "transitionIds");
            if (!ResourceMaterializationContract.IsAuthorityFingerprint(Text(suffix, "resultFingerprint")) ||
                Text(suffix, "primaryTransitionId") != program.NaturalStages[0].TransitionId ||
                !suffix["transitionIds"]!.AsArray().Select(n => n!.GetValue<string>()).SequenceEqual(program.NaturalStages.Select(s => s.TransitionId), StringComparer.Ordinal))
                throw new InvalidDataException("Diagnosis requires exactly its complete nonempty ordered natural suffix.");
            return new(root, program);
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or
            OverflowException or KeyNotFoundException or NullReferenceException or FormatException)
        {
            issues.Add(new(path, IssueSeverity.Error, "Spiritual diagnosis history is not one complete exact result.",
                code: "spiritual_diagnosis_history_invalid", actual: failure.Message));
            return null;
        }
    }

    internal void RequireNatural(SpiritualHealingPersistedResult natural)
    {
        var root = natural.ToCanonicalJson();
        if (!natural.IsNatural || natural.SchemaVersion != 2 || natural.ResultFingerprint != NaturalResultFingerprint ||
            natural.ProgramReference != _program.NaturalProgramReference || !JsonNode.DeepEquals(root["binding"], _root["binding"]) ||
            !JsonNode.DeepEquals(root["control"], _root["control"]) ||
            new[] { "scheduleBeforeJson", "progressionReportJson", "scheduleAfterJson", "scheduleAfterFingerprint" }.Any(field => Text(root, field) != Text(_root, field)) ||
            root["source"]?["graphResponseFingerprint"]?.GetValue<string>() != Text(_root, "graphResponseFingerprint") ||
            !natural.Stages.Select(s => s.TransitionId).SequenceEqual(_program.NaturalStages.Select(s => s.TransitionId), StringComparer.Ordinal))
            throw new InvalidDataException("Diagnosis natural result differs from its exact linked cycle/program.");
        var expectedTiers = new JsonArray(_program.NaturalMembers.GroupBy(m => m.Wound.Owner).Select(g => (JsonNode)new JsonObject
        { ["owner"] = SpiritualHealingRequestProjection.OwnerJson(g.Key), ["healingTier"] = g.First().HealingTier }).ToArray());
        if (!JsonNode.DeepEquals(root["source"]!["ownerTiers"], expectedTiers))
            throw new InvalidDataException("Diagnosis suffix changed an original owner's admitted natural tier.");
        for (var index = 0; index < natural.Stages.Count; index++)
            SpiritualHealingPersistedResult.ValidateStageScope(natural.Stages[index], _program.NaturalStages[index]);
        var starts = natural.Stages.GroupBy(s => s.Before.WoundId).ToDictionary(g => g.Key, g => g.First().Before, StringComparer.Ordinal);
        if (starts.Count != _program.NaturalMembers.Count || _program.NaturalMembers.Any(m =>
            !starts.TryGetValue(m.Wound.WoundId, out var wound) || WoundMaterializationContract.SerializeCanonical(wound) != WoundMaterializationContract.SerializeCanonical(m.Wound)))
            throw new InvalidDataException("Diagnosis suffix omits or changes its diagnosed whole-cohort start images.");
    }

    private static WoundMaterializationEnvelope ReadWound(JsonNode? node, string path)
    {
        var parsed = WoundMaterializationContract.Parse(node?.ToJsonString(), path);
        return parsed.IsValid ? parsed.Wound! : throw new InvalidDataException("Noncanonical diagnosis wound image.");
    }
    private static string Text(JsonObject root, string field) => root[field]!.GetValue<string>();
    private static void Closed(JsonObject root, params string[] fields)
    {
        if (root.Count != fields.Length || !fields.All(root.ContainsKey)) throw new InvalidDataException("Diagnosis result has missing or unknown fields.");
    }
}
