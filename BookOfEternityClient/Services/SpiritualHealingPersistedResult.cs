using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record SpiritualHealingPublishedStage(WoundMaterializationEnvelope Before,
    WoundMaterializationEnvelope After, string OperationKey, string EventRef, string? CycleKey, string? AttemptId)
{
    internal string TransitionId => After.LastTransition.TransitionId;
    internal string Kind => After.LastTransition.Kind;
    internal bool Terminal => Kind == "heal";
}

/// <summary>Strict replay DATA for one completed treatment and its single ordered recovery cycle.</summary>
internal sealed class SpiritualHealingPersistedResult : WoundTransitionResult
{
    private readonly JsonObject _root;
    private readonly ImmutableArray<SpiritualHealingPublishedStage> _stages;
    private SpiritualHealingPersistedResult(JsonObject root, IEnumerable<SpiritualHealingPublishedStage> stages)
    { _root = root.DeepClone().AsObject(); _stages = stages.ToImmutableArray(); }
    public override string Kind => "spiritual_healing";
    internal IReadOnlyList<SpiritualHealingPublishedStage> Stages => _stages.Select(s => s with
    { Before = WoundAcceptedTurnData.CloneWound(s.Before)!, After = WoundAcceptedTurnData.CloneWound(s.After)! }).ToArray();
    internal override JsonObject ToCanonicalJson() => _root.DeepClone().AsObject();

    internal static SpiritualHealingPersistedResult Create(AfterlifeWoundHealingPlanner.Continuation owner,
        IReadOnlyList<WoundExecutedBoundary> executed)
    {
        var binding = owner.Input.Binding;
        var cycle = owner.Request["attempt"]!["cycleKey"]!.GetValue<string>();
        var root = new JsonObject
        {
            ["kind"] = "spiritual_healing", ["schemaVersion"] = 1,
            ["binding"] = new JsonObject { ["sessionId"] = binding.SessionId, ["requestId"] = binding.RequestId,
                ["snapshotToken"] = binding.SnapshotToken, ["realm"] = binding.Realm, ["turn"] = binding.Turn },
            ["request"] = owner.Request, ["source"] = owner.PersistenceSource,
            ["control"] = JsonSerializer.SerializeToNode(owner.Control),
            ["scheduleAfterJson"] = owner.SchedulerMutation.ScheduleAfterJson,
            ["scheduleAfterFingerprint"] = owner.SchedulerMutation.ScheduleAfter.Fingerprint,
            ["stages"] = new JsonArray(executed.Select(s => (JsonNode)new JsonObject
            {
                ["before"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(s.Before)),
                ["after"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(s.After)),
                ["operationKey"] = s.OperationKey, ["eventRef"] = s.EventRef,
                ["cycleKey"] = s.Kind == "treat" ? cycle : s.Kind == "recover" ? s.After.Recovery.LastTickKey : null,
                ["attemptId"] = s.Kind == "treat" ? s.After.Care.LastAttemptId : null
            }).ToArray())
        };
        root["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(root);
        var issues = new List<ValidationIssue>();
        return Parse(JsonSerializer.SerializeToElement(root), "spiritualHealing.result", issues)
            ?? throw new InvalidDataException("Spiritual persisted result rejected: " + string.Join(";", issues.Select(i => i.Actual)));
    }

    internal static SpiritualHealingPersistedResult? Parse(JsonElement element, string path,
        ICollection<ValidationIssue> issues)
    {
        try
        {
            var root = StrictJsonAuthority.Deserialize<JsonObject>(element.GetRawText(), new JsonSerializerOptions(), "spiritual healing result")
                ?? throw new InvalidDataException("Result must be an object.");
            AfterlifeWoundTreatmentAuthority.MaterializeProperties(root);
            Closed(root, "kind", "schemaVersion", "binding", "request", "source", "control", "scheduleAfterJson", "scheduleAfterFingerprint", "stages", "resultFingerprint");
            if (Text(root, "kind") != "spiritual_healing" || root["schemaVersion"]!.GetValue<int>() != 1 ||
                Text(root, "resultFingerprint") != WoundHistoryState.ComputeTransitionResultFingerprint(root))
                throw new InvalidDataException("Spiritual result kind/schema/fingerprint differs.");
            var binding = root["binding"]!.AsObject();
            Closed(binding, "sessionId", "requestId", "snapshotToken", "realm", "turn");
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken" })
                if (!ResourceMaterializationContract.IsExactIdentifier(Text(binding, field))) throw new InvalidDataException("Invalid spiritual binding.");
            if (Text(binding, "realm") is not ("chaos_sea" or "shining_abode") || binding["turn"]!.GetValue<int>() < 1)
                throw new InvalidDataException("Invalid spiritual realm/turn.");
            var control = root["control"]!.Deserialize<BookOfEternityClient.Models.ProgressionControl>()
                ?? throw new InvalidDataException("Missing accepted cycle control.");
            if (WoundAcceptedTurnFingerprintWriter.CanonicalJson(root["control"]!) !=
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(JsonSerializer.SerializeToNode(control)!))
                throw new InvalidDataException("Accepted spiritual control must have its complete exact closed shape.");
            var scheduleJson = Text(root, "scheduleAfterJson");
            var schedule = StrictJsonAuthority.Deserialize<JsonObject>(scheduleJson, new JsonSerializerOptions(), "spiritual schedule result")!;
            var scheduleBytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(scheduleJson)).ToArray();
            if (Text(root, "scheduleAfterFingerprint") != new CanonicalBeforeImage(true, scheduleBytes).Fingerprint)
                throw new InvalidDataException("The complete accepted schedule image differs.");
            var source = root["source"]!.AsObject();
            Closed(source, "healingTier", "complicationModifier", "rollMode", "sourceFingerprint", "rollContributions", "graphResponseFingerprint");
            if (source["rollContributions"] is not JsonArray || string.IsNullOrWhiteSpace(Text(source, "graphResponseFingerprint")))
                throw new InvalidDataException("Missing retained effect/graph-response source diagnostics.");
            var tier = source["healingTier"]!.GetValue<int>();
            var complicationModifier = source["complicationModifier"]!.GetValue<long>();
            if (tier is < 1 or > 5 || complicationModifier < 0 || !ResourceMaterializationContract.IsAuthorityFingerprint(Text(source, "sourceFingerprint")))
                throw new InvalidDataException("Invalid spiritual source diagnostics.");
            var request = root["request"]!.AsObject();
            var attempt = request["attempt"]!.AsObject();
            var rows = root["stages"]!.AsArray();
            if (rows.Count is < 1 or > 2048) throw new InvalidDataException("Unbounded spiritual stage cohort.");
            var stages = new List<SpiritualHealingPublishedStage>();
            foreach (var node in rows)
            {
                var row = node!.AsObject(); Closed(row, "before", "after", "operationKey", "eventRef", "cycleKey", "attemptId");
                var before = WoundMaterializationContract.Parse(row["before"]!.ToJsonString(), path + ".before");
                var after = WoundMaterializationContract.Parse(row["after"]!.ToJsonString(), path + ".after");
                if (!before.IsValid || !after.IsValid) throw new InvalidDataException("Noncanonical spiritual stage wound.");
                stages.Add(new(before.Wound!, after.Wound!, Text(row, "operationKey"), Text(row, "eventRef"),
                    row["cycleKey"]?.GetValue<string>(), row["attemptId"]?.GetValue<string>()));
            }
            var original = stages.GroupBy(s => s.Before.WoundId).Select(g => g.First().Before).ToArray();
            var selected = original.Single(w => w.WoundId == Text(attempt, "woundId"));
            if (selected.Owner.Realm != Text(binding, "realm") || selected.Owner.OwnerKind != "player_soul" || selected.Owner.OwnerId != "player_soul" ||
                original.Any(w => w.Owner != selected.Owner) || stages[0].Before.WoundId != selected.WoundId || stages[0].Kind != "treat")
                throw new InvalidDataException("Foreign spiritual self target or stage ordering.");
            var preparedSource = new SpiritualHealingPreparedSource(selected.WoundId, selected.Owner, selected.Owner,
                selected.Severity.Rank, tier, complicationModifier, Text(source, "rollMode"), Array.Empty<EffectRollContributionEvidence>());
            if (!SpiritualSelfTreatmentRequestCodec.IsExact(request,
                    new(Text(binding, "sessionId"), Text(binding, "requestId"), binding["turn"]!.GetValue<int>(), control),
                    new(selected.WoundId, WoundIdentityState.ComputeSemanticFingerprint(selected)), Text(binding, "realm"),
                    attempt["cycleOrdinal"]!.GetValue<int>(), Text(source, "sourceFingerprint"), preparedSource) ||
                schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>() != attempt["cycleOrdinal"]!.GetValue<int>())
                throw new InvalidDataException("Spiritual request/draw/cycle does not rederive.");
            var modifier = selected.Complications.Where(c => c.State == "active").Aggregate(0L,
                (total, c) => checked(total + c.TreatmentDifficultyModifier));
            if (modifier != complicationModifier) throw new InvalidDataException("Changed spiritual complication modifier.");
            var resolution = SpiritualHealingResolver.ResolvePrepared(preparedSource, Text(source, "rollMode"),
                attempt["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray()).Resolution!;
            var eventAuthority = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(Text(binding, "sessionId"),
                Text(binding, "requestId"), Text(binding, "snapshotToken"), binding["turn"]!.GetValue<int>());
            if (!eventAuthority.Success || eventAuthority.Events.Count != 1 || control.AfterlifeCatchupRequired ||
                control.NextChaosSeaTurnOrdinal != attempt["cycleOrdinal"]!.GetValue<int>() ||
                control.NextChaosSeaTurnOrdinal != checked(control.CurrentChaosSeaTurnOrdinal + 1))
                throw new InvalidDataException("Spiritual accepted event or single cycle differs.");
            var expected = original.SelectMany(w => SpiritualWoundRecoveryPlanner.Build(w, tier, Text(attempt, "attemptId"),
                Text(attempt, "cycleKey"), w.WoundId == selected.WoundId ? resolution.Calculation : null)).ToArray();
            if (expected.Length != stages.Count) throw new InvalidDataException("Missing/extra spiritual execution stage.");
            var current = original.ToDictionary(w => w.WoundId, StringComparer.Ordinal);
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i]; var e = expected[i]; var before = stage.Before; var after = stage.After;
                if (WoundMaterializationContract.SerializeCanonical(before) != WoundMaterializationContract.SerializeCanonical(current[before.WoundId]) ||
                    before.WoundId != e.WoundId || stage.EventRef != eventAuthority.Events[0].EventRef || stage.Kind != e.Kind || stage.OperationKey != e.OperationKey || stage.TransitionId != e.TransitionId ||
                    after.LastTransition.Ordinal != before.LastTransition.Ordinal + 1 || after.LastTransition.Turn != binding["turn"]!.GetValue<int>() ||
                    after.Severity.Rank != e.SeverityRank || after.Recovery.CurrentStepProgress != e.Progress ||
                    stage.AttemptId != e.AttemptId || stage.CycleKey != (e.Kind == "treat" ? Text(attempt, "cycleKey") : e.Kind == "recover" ? e.TickKey : null))
                    throw new InvalidDataException("Spiritual stage does not match the complete recomputed program.");
                ValidateStageScope(stage, e);
                current[before.WoundId] = after;
            }
            return new(root, stages);
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException or KeyNotFoundException or NullReferenceException)
        {
            issues.Add(new(path, IssueSeverity.Error, "Spiritual healing history is not a complete exact result.",
                code: "spiritual_healing_history_invalid", actual: failure.Message));
            return null;
        }
    }

    private static void ValidateStageScope(SpiritualHealingPublishedStage stage, SpiritualWoundStage expected)
    {
        var before = stage.Before; var after = stage.After;
        var expectedCare = expected.Kind == "treat" ? before.Care with { LastAttemptId = expected.AttemptId } : expected.Kind == "heal"
            ? before.Care with { State = "healed", ActiveCourseId = null } : before.Care;
        var expectedRecovery = expected.Kind == "heal" ? before.Recovery : before.Recovery with
        { CurrentStepProgress = expected.Progress, CurrentStepThreshold = 2 * expected.SeverityRank,
            LastTickKey = expected.Kind == "recover" ? expected.TickKey : before.Recovery.LastTickKey };
        if (JsonSerializer.Serialize(after.Care) != JsonSerializer.Serialize(expectedCare) ||
            JsonSerializer.Serialize(after.Recovery) != JsonSerializer.Serialize(expectedRecovery) ||
            after.Lifecycle != (expected.Kind == "heal" ? "healed" : "active") ||
            before.Complications.Count != after.Complications.Count || before.Complications.Where((c, i) =>
                c with { OwnedEffectIds = after.Complications[i].OwnedEffectIds } != after.Complications[i]).Any())
            throw new InvalidDataException("Spiritual stage changed unrelated care/recovery/complication fields.");
        var restored = after with { Lifecycle = before.Lifecycle, Severity = before.Severity, Care = before.Care,
            Recovery = before.Recovery, Consequences = before.Consequences, Complications = before.Complications, LastTransition = before.LastTransition };
        if (WoundMaterializationContract.SerializeCanonical(restored) != WoundMaterializationContract.SerializeCanonical(before) ||
            before.Severity.Rank == after.Severity.Rank && WoundMaterializationContract.SerializeCanonical(after with
                { Lifecycle = before.Lifecycle, Care = before.Care, Recovery = before.Recovery, LastTransition = before.LastTransition }) !=
                WoundMaterializationContract.SerializeCanonical(before))
            throw new InvalidDataException("Spiritual stage changed an unrequested graph or wound field.");
    }

    private static string Text(JsonObject obj, string name) => obj[name]?.GetValue<string>() ?? throw new InvalidDataException("Missing " + name);
    private static void Closed(JsonObject obj, params string[] fields)
    { if (obj.Count != fields.Length || fields.Any(f => !obj.ContainsKey(f))) throw new InvalidDataException("Unknown/missing spiritual result field."); }
}
