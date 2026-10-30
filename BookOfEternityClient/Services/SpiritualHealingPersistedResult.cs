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
    private readonly AfterlifeHealingServiceQuote? _serviceQuote;
    private SpiritualHealingPersistedResult(JsonObject root, IEnumerable<SpiritualHealingPublishedStage> stages,
        AfterlifeHealingServiceQuote? serviceQuote)
    { _root = root.DeepClone().AsObject(); _stages = stages.ToImmutableArray(); _serviceQuote = serviceQuote; }
    /// <summary>Already parsed immutable quote DATA from this completely validated result, not current access.</summary>
    internal AfterlifeHealingServiceQuote? ServiceQuote => _serviceQuote;
    public override string Kind => "spiritual_healing";
    internal string Mode => _root["mode"]!.GetValue<string>();
    internal bool IsNatural => Mode == "natural";
    internal int SchemaVersion => _root["schemaVersion"]!.GetValue<int>();
    internal string ResultFingerprint => _root["resultFingerprint"]!.GetValue<string>();
    internal JsonArray? ParticipationOutcomes => _root["participationOutcomes"]?.DeepClone().AsArray();
    internal JsonObject? ShiningPatientParticipationRecord => SchemaVersion == 8
        ? (IsNatural ? _root["participationOutcomes"]?[0]?["record"] : _root["request"]?["participation"]?["record"])?.DeepClone().AsObject()
        : null;
    internal JsonObject? ResidentParticipationRecord
    {
        get
        {
            if (SchemaVersion is not (5 or 6 or 8)) return null;
            var record = (IsNatural ? _root["participationOutcomes"]?[0]?["record"]
                : _root["request"]?["participation"]?["record"]) as JsonObject;
            // Family8 also admits a radiant head, which has ordinary actor
            // memory and must never obtain protected resident-journal access.
            return record is null || SchemaVersion == 8 && (!SpiritualTreatmentParticipation.IsShiningPatientParticipation(record) ||
                SpiritualTreatmentParticipation.RecordOwner(record).OwnerKind != "resident") ? null : record.DeepClone().AsObject();
        }
    }
    internal string? AidGrantFingerprint => Mode == "aid"
        ? _root["request"]!["participation"]!["grantResultFingerprint"]!.GetValue<string>() : null;
    internal bool MatchesTurn(string sessionId, int turn) =>
        _root["binding"]!["sessionId"]!.GetValue<string>() == sessionId &&
        _root["binding"]!["turn"]!.GetValue<int>() == turn;
    internal bool MatchesBinding(string sessionId, string requestId, int turn) => MatchesTurn(sessionId, turn) &&
        _root["binding"]!["requestId"]!.GetValue<string>() == requestId;
    internal WoundMaterializationEnvelope? ReadFinalWound(string woundId) =>
        WoundAcceptedTurnData.CloneWound(_stages.LastOrDefault(s => s.After.WoundId == woundId)?.After);
    internal bool AidGrantAgrees(SpiritualHealingPersistedResult grant)
    {
        if (Mode != "aid" || !grant.IsNatural || grant.SchemaVersion != SchemaVersion) return false;
        var witness = _root["request"]!["participation"]!;
        var record = witness["record"]!;
        var selected = _stages[0].Before;
        var final = grant._stages.LastOrDefault(s => s.After.WoundId == selected.WoundId)?.After;
        return grant.ResultFingerprint == witness["grantResultFingerprint"]!.GetValue<string>() &&
            grant._root["participationOutcomes"] is JsonArray { Count: 1 } outcomes &&
            JsonNode.DeepEquals(outcomes[0]!["record"], record) &&
            JsonNode.DeepEquals(record["binding"], grant._root["binding"]) &&
            grant._root["binding"]!["sessionId"]!.GetValue<string>() == _root["binding"]!["sessionId"]!.GetValue<string>() &&
            grant._root["binding"]!["turn"]!.GetValue<int>() == _root["binding"]!["turn"]!.GetValue<int>() - 1 &&
            final is not null && final.Owner == selected.Owner &&
            WoundIdentityState.ComputeSemanticFingerprint(final) == WoundIdentityState.ComputeSemanticFingerprint(selected);
    }
    internal string ProgramReference => IsNatural ? _root["source"]!["sourceFingerprint"]!.GetValue<string>()
        : _root["request"]!["requestFingerprint"]!.GetValue<string>();
    internal string GraphResponseFingerprint => _root["source"]!["graphResponseFingerprint"]!.GetValue<string>();
    internal IReadOnlyList<SpiritualHealingPublishedStage> Stages => _stages.Select(s => s with
    { Before = WoundAcceptedTurnData.CloneWound(s.Before)!, After = WoundAcceptedTurnData.CloneWound(s.After)! }).ToArray();
    internal override JsonObject ToCanonicalJson() => _root.DeepClone().AsObject();

    internal static SpiritualHealingPersistedResult Create(AfterlifeWoundHealingPlanner.Continuation owner,
        IReadOnlyList<WoundExecutedBoundary> executed)
    {
        var binding = owner.Input.Binding;
        var cycle = owner.CycleKey;
        var participation = owner.ParticipationOutcomes;
        var request = owner.Request;
        var version = owner.Mode == "service" ? 7 : owner.Mode == "aid" ? request!["schemaVersion"]!.GetValue<int>()
            : participation is null ? 2 : SpiritualTreatmentParticipation.ResultVersion(participation[0]!["record"]!.AsObject());
        var root = new JsonObject
        {
            ["kind"] = "spiritual_healing", ["schemaVersion"] = version, ["mode"] = owner.Mode,
            ["binding"] = new JsonObject { ["sessionId"] = binding.SessionId, ["requestId"] = binding.RequestId,
                ["snapshotToken"] = binding.SnapshotToken, ["realm"] = binding.Realm, ["turn"] = binding.Turn },
            ["request"] = request, ["source"] = owner.PersistenceSource,
            ["control"] = JsonSerializer.SerializeToNode(owner.Control),
            ["scheduleBeforeJson"] = CanonicalJsonUtf8.DecodeOneOptionalBom(owner.SchedulerMutation.ScheduleBefore.Bytes!),
            ["progressionReportJson"] = CanonicalJsonUtf8.DecodeOneOptionalBom(owner.SchedulerMutation.ReportBefore.Bytes!),
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
        if (participation is not null) root["participationOutcomes"] = participation;
        if (version is 4 or 5 or 6 or 8 && owner.Mode == "aid") root["direction"] = SpiritualSelfTreatmentRequestCodec.AidDirection(version);
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
            var rootFields = new[] { "kind", "schemaVersion", "binding", "request", "source", "control", "scheduleBeforeJson", "progressionReportJson", "scheduleAfterJson", "scheduleAfterFingerprint", "stages", "resultFingerprint" };
            var version = root["schemaVersion"]!.GetValue<int>();
            var mode = Text(root, "mode");
            Closed(root, version is 3 or 4 or 5 or 6 or 8 && mode == "natural" ? rootFields.Append("mode").Append("participationOutcomes").ToArray()
                : version is 4 or 5 or 6 or 8 && mode == "aid" ? rootFields.Append("mode").Append("direction").ToArray()
                : rootFields.Append("mode").ToArray());
            if (Text(root, "kind") != "spiritual_healing" || version is not (2 or 3 or 4 or 5 or 6 or 7 or 8) || mode is not ("self" or "natural" or "aid" or "service") ||
                (version == 7) != (mode == "service") ||
                version == 2 && mode == "aid" || version is 3 or 4 or 5 or 6 or 8 && mode == "self" ||
                version is 4 or 5 or 6 or 8 && mode == "aid" && Text(root, "direction") != SpiritualSelfTreatmentRequestCodec.AidDirection(version) ||
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
            if (Text(root, "scheduleAfterFingerprint") != new CanonicalBeforeImage(true, scheduleBytes).Fingerprint ||
                !ProgressionScheduleService.SpiritualPublishedScheduleAgrees(schedule, Text(root, "scheduleBeforeJson"),
                    Text(root, "progressionReportJson"), control, Text(binding, "sessionId"), Text(binding, "requestId"),
                    binding["turn"]!.GetValue<int>(), Text(binding, "realm")))
                throw new InvalidDataException("The complete accepted schedule image differs.");
            var source = root["source"]!.AsObject();
            if (!ResourceMaterializationContract.IsAuthorityFingerprint(Text(source, "sourceFingerprint")) ||
                !ResourceMaterializationContract.IsAuthorityFingerprint(Text(source, "graphResponseFingerprint")))
                throw new InvalidDataException("Invalid spiritual source/graph-response fingerprint.");
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
            if (original.Any(w => w.Lifecycle != "active" || w.Classification.Domain != "spiritual" || w.Severity.Rank is < 1 or > 4 ||
                    w.Owner.Realm != Text(binding, "realm") || w.Owner.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath))
                throw new InvalidDataException("Foreign original spiritual recovery cohort.");
            var cycle = AfterlifeWoundTreatmentAuthority.CycleKey(Text(binding, "realm"), control.NextChaosSeaTurnOrdinal);
            string? attemptId = null;
            WoundMaterializationEnvelope? selected = null;
            SpiritualHealingResolution? resolution = null;
            AfterlifeHealingServiceQuote? completedServiceQuote = null;
            IReadOnlyDictionary<WoundOwnerCoordinate, int> tiers;
            if (mode == "natural")
            {
                Closed(source, "sourceFingerprint", "graphResponseFingerprint", "ownerTiers", "projection");
                if (root["request"] is not null || stages[0].Kind != "recover")
                    throw new InvalidDataException("Natural recovery cannot contain an active request or attempt.");
                tiers = ReadOwnerTiers(source, original);
                var members = OrderOriginal(original, null).Select(w => new SpiritualWoundRecoveryMember(w, tiers[w.Owner])).ToArray();
                if (!JsonNode.DeepEquals(source["projection"], SpiritualHealingRequestProjection.CreateNatural(
                        Text(binding, "realm"), control.NextChaosSeaTurnOrdinal, Text(source, "sourceFingerprint"), members)))
                    throw new InvalidDataException("Natural recovery projection omits or changes its original cohort/tiers/graphs.");
            }
            else
            {
                var selfFields = new[] { "healingTier", "complicationModifier", "rollMode", "sourceFingerprint", "rollContributions", "graphResponseFingerprint" };
                Closed(source, selfFields.Append("ownerTiers").ToArray());
                var tier = ValidateActiveSource(source);
                var request = root["request"]!.AsObject();
                var attempt = request["attempt"]!.AsObject();
                selected = original.Single(w => w.WoundId == Text(attempt, "woundId"));
                if (stages[0].Before.WoundId != selected.WoundId || stages[0].Kind != "treat")
                    throw new InvalidDataException("Foreign spiritual active target or stage ordering.");
                tiers = ReadOwnerTiers(source, original);
                var healer = new WoundOwnerCoordinate(Text(binding, "realm"), "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
                var context = new SpiritualSelfTreatmentStageContext(Text(binding, "sessionId"), Text(binding, "requestId"),
                    binding["turn"]!.GetValue<int>(), control);
                var selection = SpiritualSelfTreatmentRequestCodec.ReadActiveSelection(request);
                SpiritualHealingPreparedSource? servicePrepared = null;
                if (mode == "self")
                {
                    if (selected.Owner != healer || selection.Aid is not null || selection.Service is not null || tiers[selected.Owner] != tier)
                        throw new InvalidDataException("Active self target or natural owner tier differs.");
                }
                else if (mode == "service")
                {
                    var quote = selection.Service?.Quote ?? throw new InvalidDataException("Service result lacks its exact quote.");
                    completedServiceQuote = quote;
                    healer = quote.Provider;
                    servicePrepared = SpiritualSelfTreatmentRequestCodec.ReadServiceActiveSource(request["activeSource"], quote);
                    var persistedCheck = new JsonObject
                    {
                        ["healingTier"] = tier, ["complicationModifier"] = source["complicationModifier"]!.DeepClone(),
                        ["rollMode"] = source["rollMode"]!.DeepClone(), ["rollContributions"] = source["rollContributions"]!.DeepClone()
                    };
                    if (quote.Patient != selected.Owner || quote.WoundId != selected.WoundId || quote.SeverityRank != selected.Severity.Rank ||
                        quote.WoundFingerprint != WoundIdentityState.ComputeSemanticFingerprint(selected) ||
                        !SpiritualSelfTreatmentRequestCodec.IsBoundService(request, context, selection, Text(binding, "realm"), control.NextChaosSeaTurnOrdinal) ||
                        !JsonNode.DeepEquals(SpiritualSelfTreatmentRequestCodec.ActiveSourceJson(servicePrepared), persistedCheck) ||
                        Text(attempt, "sourceFingerprint") != Text(source, "sourceFingerprint") ||
                        tiers.TryGetValue(healer, out var serviceNaturalTier) && serviceNaturalTier != tier)
                        throw new InvalidDataException("Service quote, payment, real check or original wound differs.");
                }
                else
                {
                    if (selection.Aid is null) throw new InvalidDataException("Aid result lacks its explicit selected direction.");
                    healer = selection.Aid.Healer(Text(binding, "realm"));
                    var activeSource = new JsonObject
                    {
                        ["healingTier"] = tier, ["complicationModifier"] = source["complicationModifier"]!.DeepClone(),
                        ["rollMode"] = source["rollMode"]!.DeepClone(), ["rollContributions"] = source["rollContributions"]!.DeepClone()
                    };
                    if (selected.Owner != selection.Aid.Target(Text(binding, "realm")) ||
                        request["schemaVersion"]!.GetValue<int>() != version ||
                        selection.WoundId != selected.WoundId || selection.ExpectedWoundFingerprint != WoundIdentityState.ComputeSemanticFingerprint(selected) ||
                        !SpiritualSelfTreatmentRequestCodec.IsBoundAid(request, context, selection, Text(binding, "realm"), control.NextChaosSeaTurnOrdinal) ||
                        !JsonNode.DeepEquals(request["target"], SpiritualHealingRequestProjection.OwnerJson(selected.Owner)) ||
                        !JsonNode.DeepEquals(request["activeSource"], activeSource) ||
                        Text(attempt, "sourceFingerprint") != Text(source, "sourceFingerprint") ||
                        tiers.TryGetValue(healer, out var healerNaturalTier) && healerNaturalTier != tier)
                        throw new InvalidDataException("Aid healer/target/source differs from its exact retained request.");
                }
                var complicationModifier = source["complicationModifier"]!.GetValue<long>();
                var preparedSource = servicePrepared ?? new SpiritualHealingPreparedSource(selected.WoundId, selected.Owner, healer,
                    selected.Severity.Rank, tier, complicationModifier, Text(source, "rollMode"), Array.Empty<EffectRollContributionEvidence>());
                if (mode == "self" && !SpiritualSelfTreatmentRequestCodec.IsExact(request, context,
                        new(selected.WoundId, WoundIdentityState.ComputeSemanticFingerprint(selected)), Text(binding, "realm"),
                        control.NextChaosSeaTurnOrdinal, Text(source, "sourceFingerprint"), preparedSource) ||
                    Text(attempt, "cycleKey") != cycle)
                    throw new InvalidDataException("Spiritual request/draw/cycle does not rederive.");
                var modifier = selected.Complications.Where(c => c.State == "active").Aggregate(0L,
                    (total, c) => checked(total + c.TreatmentDifficultyModifier));
                if (modifier != complicationModifier) throw new InvalidDataException("Changed spiritual complication modifier.");
                resolution = SpiritualHealingResolver.ResolvePrepared(preparedSource, Text(source, "rollMode"),
                    attempt["roll"]!["values"]!.AsArray().Select(v => v!.GetValue<int>()).ToArray()).Resolution!;
                attemptId = Text(attempt, "attemptId");
            }
            var eventAuthority = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(Text(binding, "sessionId"),
                Text(binding, "requestId"), Text(binding, "snapshotToken"), binding["turn"]!.GetValue<int>());
            if (!eventAuthority.Success || eventAuthority.Events.Count != 1 || control.AfterlifeCatchupRequired ||
                schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>() != control.NextChaosSeaTurnOrdinal ||
                control.NextChaosSeaTurnOrdinal != checked(control.CurrentChaosSeaTurnOrdinal + 1))
                throw new InvalidDataException("Spiritual accepted event or single cycle differs.");
            var expected = OrderOriginal(original, selected?.WoundId).SelectMany(w => SpiritualWoundRecoveryPlanner.Build(w,
                tiers[w.Owner], attemptId, cycle, w.WoundId == selected?.WoundId ? resolution!.Calculation : null)).ToArray();
            if (expected.Length != stages.Count) throw new InvalidDataException("Missing/extra spiritual execution stage.");
            var current = original.ToDictionary(w => w.WoundId, StringComparer.Ordinal);
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i]; var e = expected[i]; var before = stage.Before; var after = stage.After;
                if (WoundMaterializationContract.SerializeCanonical(before) != WoundMaterializationContract.SerializeCanonical(current[before.WoundId]) ||
                    before.WoundId != e.WoundId || stage.EventRef != eventAuthority.Events[0].EventRef || stage.Kind != e.Kind || stage.OperationKey != e.OperationKey || stage.TransitionId != e.TransitionId ||
                    after.LastTransition.Ordinal != before.LastTransition.Ordinal + 1 || after.LastTransition.Turn != binding["turn"]!.GetValue<int>() ||
                    after.Severity.Rank != e.SeverityRank || after.Recovery.CurrentStepProgress != e.Progress ||
                    stage.AttemptId != e.AttemptId || stage.CycleKey != (e.Kind == "treat" ? cycle : e.Kind == "recover" ? e.TickKey : null))
                    throw new InvalidDataException("Spiritual stage does not match the complete recomputed program.");
                ValidateStageScope(stage, e);
                current[before.WoundId] = after;
            }
            if (version is 3 or 4 or 5 or 6 or 8 && mode == "natural")
                SpiritualTreatmentParticipation.ValidateOutcomes(root["participationOutcomes"]!.AsArray(), binding,
                    control.NextChaosSeaTurnOrdinal, stages, version);
            return new(root, stages, completedServiceQuote);
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException or KeyNotFoundException or NullReferenceException)
        {
            issues.Add(new(path, IssueSeverity.Error, "Spiritual healing history is not a complete exact result.",
                code: "spiritual_healing_history_invalid", actual: failure.Message));
            return null;
        }
    }

    private static IEnumerable<WoundMaterializationEnvelope> OrderOriginal(
        IEnumerable<WoundMaterializationEnvelope> wounds, string? selectedId) => wounds
        .OrderBy(w => w.WoundId == selectedId ? 0 : 1).ThenBy(w => w.Owner.OwnerKind, StringComparer.Ordinal)
        .ThenBy(w => w.Owner.OwnerId, StringComparer.Ordinal).ThenBy(w => w.WoundId, StringComparer.Ordinal);

    private static IReadOnlyDictionary<WoundOwnerCoordinate, int> ReadOwnerTiers(JsonObject source,
        IReadOnlyList<WoundMaterializationEnvelope> original)
    {
        var owners = original.Select(w => w.Owner).Distinct().ToHashSet();
        var rows = source["ownerTiers"] as JsonArray ?? throw new InvalidDataException("Missing natural owner tiers.");
        if (rows.Count != owners.Count) throw new InvalidDataException("Missing or extra natural owner tier.");
        var tiers = new Dictionary<WoundOwnerCoordinate, int>();
        foreach (var item in rows)
        {
            var row = item!.AsObject(); Closed(row, "owner", "healingTier");
            var value = row["owner"]!.AsObject(); Closed(value, "realm", "ownerKind", "ownerId", "carrierPath");
            var owner = new WoundOwnerCoordinate(Text(value, "realm"), Text(value, "ownerKind"), Text(value, "ownerId"), Text(value, "carrierPath"));
            var tier = row["healingTier"]!.GetValue<int>();
            if (tier is < 0 or > 5 || !owners.Contains(owner) || !tiers.TryAdd(owner, tier))
                throw new InvalidDataException("Invalid, foreign or duplicate natural owner tier.");
        }
        var canonical = new JsonArray(tiers.OrderBy(p => p.Key.OwnerKind, StringComparer.Ordinal)
            .ThenBy(p => p.Key.OwnerId, StringComparer.Ordinal).Select(p => (JsonNode)new JsonObject
            { ["owner"] = SpiritualHealingRequestProjection.OwnerJson(p.Key), ["healingTier"] = p.Value }).ToArray());
        if (!JsonNode.DeepEquals(rows, canonical)) throw new InvalidDataException("Natural owner tiers are not in canonical cohort order.");
        return tiers;
    }

    // Shared closed check DATA validation; no source or publication authority.
    internal static int ValidateActiveSource(JsonObject source)
    {
        if (source["rollContributions"] is not JsonArray contributions || contributions.Count > EffectRollContributionResolver.MaximumCapturedRows)
            throw new InvalidDataException("Missing retained effect roll diagnostics.");
        var keys = new HashSet<(string, string)>();
        var advantage = false; var disadvantage = false;
        foreach (var item in contributions)
        {
            var row = item!.AsObject(); Closed(row, "EffectId", "ComponentId", "Contribution");
            if (!ResourceMaterializationContract.IsExactIdentifier(Text(row, "EffectId")) ||
                !ResourceMaterializationContract.IsExactIdentifier(Text(row, "ComponentId")) ||
                !keys.Add((Text(row, "EffectId"), Text(row, "ComponentId"))) ||
                Text(row, "Contribution") is not ("advantage" or "disadvantage"))
                throw new InvalidDataException("Invalid/duplicate retained roll contribution coordinate.");
            advantage |= Text(row, "Contribution") == "advantage";
            disadvantage |= Text(row, "Contribution") == "disadvantage";
        }
        if (Text(source, "rollMode") != (advantage == disadvantage ? "normal" : advantage ? "advantage" : "disadvantage"))
            throw new InvalidDataException("Retained effect contributions do not derive the sealed roll mode.");
        var tier = source["healingTier"]!.GetValue<int>();
        if (tier is < 1 or > 5 || source["complicationModifier"]!.GetValue<long>() < 0)
            throw new InvalidDataException("Invalid active spiritual tier or modifier.");
        return tier;
    }

    internal static void ValidateStageScope(SpiritualHealingPublishedStage stage, SpiritualWoundStage expected)
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
