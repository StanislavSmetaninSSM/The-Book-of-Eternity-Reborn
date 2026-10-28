using BookOfEternityClient.Core;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

// Strict comparison DATA for this combat family. It has no scheduler/safe attempt
// fields and cannot issue source, draft, resource or publication authority.
internal sealed class SpiritualCombatHealingPersistedResult : WoundTransitionResult
{
    private readonly JsonObject _root;
    private readonly ImmutableArray<SpiritualHealingPublishedStage> _stages;
    private SpiritualCombatHealingPersistedResult(JsonObject root, IEnumerable<SpiritualHealingPublishedStage> stages)
    { _root = root.DeepClone().AsObject(); _stages = stages.ToImmutableArray(); }
    public override string Kind => "spiritual_combat_healing";
    internal string ResultFingerprint => Text(_root, "resultFingerprint");
    internal string SourceFingerprint => Text(_root["source"]!.AsObject(), "sourceFingerprint");
    internal JsonObject Source => _root["source"]!.DeepClone().AsObject();
    internal ResourceTransition Spend => _root["resourceTransition"]!.Deserialize<ResourceTransition>()!;
    internal IReadOnlyList<SpiritualHealingPublishedStage> Stages => _stages.Select(s => s with
        { Before = WoundAcceptedTurnData.CloneWound(s.Before)!, After = WoundAcceptedTurnData.CloneWound(s.After)! }).ToArray();
    internal override JsonObject ToCanonicalJson() => _root.DeepClone().AsObject();

    internal static SpiritualCombatHealingPersistedResult Create(
        ValidationService.SpiritualOriginalTurnCapture.CombatHealingSelection selection,
        IReadOnlyList<WoundExecutedBoundary> executed)
    {
        var root = new JsonObject
        {
            ["kind"] = "spiritual_combat_healing", ["schemaVersion"] = 1, ["source"] = selection.Witness,
            ["graphResponseFingerprint"] = selection.GraphResponseFingerprint,
            ["resourceTransition"] = JsonSerializer.SerializeToNode(selection.ReadActualSpend()),
            ["stages"] = new JsonArray(executed.Select(s => (JsonNode)new JsonObject
            {
                ["before"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(s.Before)),
                ["after"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(s.After)),
                ["operationKey"] = s.OperationKey, ["eventRef"] = s.EventRef,
                ["attemptId"] = s.After.LastTransition.Kind == "treat" ? s.After.Care.LastAttemptId : null
            }).ToArray())
        };
        root["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(root);
        return Read(root);
    }

    internal static SpiritualCombatHealingPersistedResult? Parse(JsonElement element, string path, ICollection<ValidationIssue> issues)
    {
        try
        {
            return Read(StrictJsonAuthority.Deserialize<JsonObject>(element.GetRawText(), new JsonSerializerOptions(), path)
                ?? throw new InvalidDataException("Combat result must be an object."));
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or InvalidOperationException or ArgumentException or OverflowException or NullReferenceException or KeyNotFoundException)
        {
            issues.Add(new(path, IssueSeverity.Error, "Invalid spiritual combat healing result.",
                code: "spiritual_combat_healing_result_invalid", actual: error.Message));
            return null;
        }
    }

    internal sealed record SourceData(SpiritualCombatHealingIntent Intent, WoundMaterializationEnvelope Before,
        SpiritualHealingPreparedSource Source, SpiritualHealingCalculation Calculation,
        string SourceId, string Fingerprint, string EventRef, string ExchangeId, string Disposition, int Turn);

    // Used by the same receipt ledger for resolved AND blocked sources. A valid
    // parsed witness is DATA; B still requires its actual original/capture owner.
    internal static SourceData ReadSource(JsonObject root)
    {
        Closed(root, "schemaVersion kind sourceId eventRef sessionId requestId snapshotToken turn realm conflictId exchangeId exchangeOrdinal turnExchangeOrdinal intent beforeWound healingSource rollMode diceClaim calculation disposition exchange exchangeFingerprint sourceFingerprint");
        var unsealed = root.DeepClone().AsObject(); unsealed.Remove("sourceFingerprint");
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "book_of_eternity.spiritual_combat_healing.witness", "1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(unsealed) });
        if (Number(root, "schemaVersion") != 1 || Text(root, "kind") != "spiritual_combat_healing_source" ||
            Text(root, "sourceFingerprint") != fingerprint || Text(root, "realm") != "chaos_sea" || Number(root, "turnExchangeOrdinal") != 0)
            throw new InvalidDataException("Combat source kind/schema/fingerprint differs.");
        foreach (var name in new[] { "sessionId", "requestId", "snapshotToken", "sourceId", "eventRef", "conflictId", "exchangeId" })
            if (!ResourceMaterializationContract.IsExactIdentifier(Text(root, name))) throw new InvalidDataException("Invalid combat source coordinate.");
        var intent = SpiritualCombatHealingIntent.Parse(root["intent"]);
        var turn = Number(root, "turn");
        if (!intent.Matches(Text(root, "sessionId"), Text(root, "requestId"), turn) || Text(root, "conflictId") != intent.ConflictId ||
            Number(root, "exchangeOrdinal") != intent.NextExchangeOrdinal)
            throw new InvalidDataException("Combat source differs from its exact original intent.");
        var coordinate = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            { "book_of_eternity.spiritual_combat_healing.source", "1", Text(root, "snapshotToken"), intent.Fingerprint, Text(root, "exchangeId") })[7..];
        var accepted = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(Text(root, "sessionId"), Text(root, "requestId"), Text(root, "snapshotToken"), turn);
        if (!accepted.Success || Text(root, "sourceId") != "spiritual_combat_healing_source_" + coordinate ||
            Text(root, "eventRef") != accepted.Events.Single().EventRef)
            throw new InvalidDataException("Combat source/event identity differs.");
        var parsedBefore = WoundMaterializationContract.Parse(root["beforeWound"]!.ToJsonString(), "combat.source.before");
        if (!parsedBefore.IsValid || parsedBefore.Wound is not { Lifecycle: "active" } before || before.WoundId != intent.WoundId ||
            before.Owner != new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) ||
            WoundIdentityState.ComputeSemanticFingerprint(before) != intent.WoundFingerprint)
            throw new InvalidDataException("Combat source wound differs from selection.");
        var source = root["healingSource"]!.Deserialize<SpiritualHealingPreparedSource>()
            ?? throw new InvalidDataException("Missing combat healing source.");
        Closed(root["healingSource"]!.AsObject(), "WoundId Target Healer WoundSeverityRank HealingTier ComplicationModifier RollMode RollContributions");
        if (source.WoundId != before.WoundId || source.Target != before.Owner || source.Healer != before.Owner ||
            source.WoundSeverityRank != before.Severity.Rank || source.HealingTier != intent.HealingTier ||
            source.ComplicationModifier != before.Complications.Where(c => c.State == "active").Sum(c => (long)c.TreatmentDifficultyModifier))
            throw new InvalidDataException("Combat source art/target/complication audit differs.");
        foreach (var field in new[] { "Target", "Healer" })
            Closed(root["healingSource"]![field]!.AsObject(), "Realm OwnerKind OwnerId CarrierPath");
        if (source.RollContributions is null) throw new InvalidDataException("Missing combat effect-roll evidence.");
        var contributions = root["healingSource"]!["RollContributions"]!.AsArray();
        var components = new HashSet<(string Effect, string Component)>();
        foreach (var node in contributions)
        {
            var row = node!.AsObject(); Closed(row, "EffectId ComponentId Contribution");
            if (!ResourceMaterializationContract.IsExactIdentifier(Text(row, "EffectId")) ||
                !ResourceMaterializationContract.IsExactIdentifier(Text(row, "ComponentId")) ||
                !components.Add((Text(row, "EffectId"), Text(row, "ComponentId"))) ||
                Text(row, "Contribution") is not ("advantage" or "disadvantage"))
                throw new InvalidDataException("Invalid combat effect-roll evidence.");
        }
        var advantage = source.RollContributions.Any(c => c.Contribution == "advantage");
        var disadvantage = source.RollContributions.Any(c => c.Contribution == "disadvantage");
        if (source.RollMode != (advantage == disadvantage ? "normal" : advantage ? "advantage" : "disadvantage"))
            throw new InvalidDataException("Combat roll mode differs from the admitted effect evidence.");
        var exchange = root["exchange"]!.AsObject();
        if (Text(root, "exchangeFingerprint") != WoundAcceptedTurnFingerprintWriter.Compute(new[] { WoundAcceptedTurnFingerprintWriter.CanonicalJson(exchange) }) ||
            Text(exchange, "exchangeId") != Text(root, "exchangeId") || !string.Equals(Text(exchange, "operationType"), AfterlifeActionCostRules.SpiritualHealingOperation, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Combat exchange differs from source.");
        var outcome = Text(exchange, "outcome").ToLowerInvariant();
        var disposition = outcome is "success" or "partial_success" ? "resolved"
            : outcome is "blocked" or "countered" or "setback" or "no_effect" ? "blocked"
            : throw new InvalidDataException("Unknown combat action outcome.");
        if (Text(root, "disposition") != disposition) throw new InvalidDataException("Combat disposition differs from exchange.");
        var claim = root["diceClaim"]!.AsArray();
        var indices = new HashSet<int>(); var rolls = new List<int>();
        foreach (var item in claim)
        {
            var row = item!.AsObject(); Closed(row, "sourceIndex value selection");
            if (Number(row, "sourceIndex") < 0 || !indices.Add(Number(row, "sourceIndex"))) throw new InvalidDataException("Duplicate combat die claim.");
            rolls.Add(Number(row, "value"));
        }
        var audit = exchange["diceAudit"]!.AsObject();
        var actualDice = audit["diceUsed"]!.AsArray().OfType<JsonObject>().Where(row =>
            new[] { "player", "playerSide", "soul" }.Contains(Text(row, "side"), StringComparer.OrdinalIgnoreCase)).Select(row => (JsonNode)new JsonObject
            { ["sourceIndex"] = row["sourceIndex"]?.DeepClone(), ["value"] = row["value"]?.DeepClone(), ["selection"] = row["selection"]?.DeepClone() }).ToArray();
        var mode = (audit["rollMode"]?["player"]?["effectiveMode"]?.GetValue<string>() ?? "normal").ToLowerInvariant();
        var resolution = SpiritualHealingResolver.ResolvePrepared(source, Text(root, "rollMode"), rolls);
        if (!JsonNode.DeepEquals(new JsonArray(actualDice), claim) || mode != Text(root, "rollMode") || !resolution.IsValid ||
            resolution.Resolution is not { } resolved || !resolved.Calculation.HasSufficientTier ||
            !JsonNode.DeepEquals(root["calculation"], JsonSerializer.SerializeToNode(resolved.Calculation)))
            throw new InvalidDataException("Combat calculation differs from admitted player dice/source.");
        var explicitSelection = claim.OfType<JsonObject>().Any(row => !string.IsNullOrWhiteSpace(row["selection"]?.GetValue<string>()));
        var selected = claim.OfType<JsonObject>().Where(row =>
            string.Equals(row["selection"]?.GetValue<string>(), "selected", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (explicitSelection && claim.OfType<JsonObject>().Any(row =>
                !new[] { "selected", "discarded" }.Contains(row["selection"]?.GetValue<string>(), StringComparer.OrdinalIgnoreCase)) ||
            !explicitSelection && claim.Count != 1 || explicitSelection && selected.Length != 1)
            throw new InvalidDataException("Combat audit must select exactly one die with the admitted closed marker vocabulary.");
        var chosen = explicitSelection ? selected[0] : claim[0]!.AsObject();
        if (Number(chosen, "value") != resolved.Calculation.Input.NaturalRoll || claim.Count > 1 &&
            claim.OfType<JsonObject>().Where(row => !ReferenceEquals(row, chosen)).Any(row =>
                !string.Equals(row["selection"]?.GetValue<string>(), "discarded", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Combat audit differs from the die selected by its actual roll mode.");
        return new(intent, before, source, resolved.Calculation, Text(root, "sourceId"), fingerprint,
            Text(root, "eventRef"), Text(root, "exchangeId"), disposition, turn);
    }

    internal static void ValidateSpend(JsonObject sourceRoot, ResourceTransition spend)
    {
        var source = ReadSource(sourceRoot);
        ValidateSpend(source, sourceRoot, spend);
    }

    private static void ValidateSpend(SourceData source, JsonObject sourceRoot, ResourceTransition spend)
    {
        var audit = sourceRoot["exchange"]!["actionCostAudit"]!["player"]!.AsObject();
        var amount = Number(audit, "effectiveCost");
        if (Number(audit, "baseCost") != 5 || Number(audit, "minCost") != 2 || Number(audit, "artTier") != source.Source.HealingTier || amount < 2 ||
            spend.OriginKind != "afterlife_cost" || spend.OriginId != source.ExchangeId || spend.Operation != ResourceTransitionOperation.Spend ||
            spend.Coordinate != new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points") ||
            spend.Turn != source.Turn || spend.RequestedAmount != amount || spend.AppliedAmount != amount ||
            spend.BeforeState is not { State: ResourceLifecycleState.Active } before || spend.AfterState is not { State: ResourceLifecycleState.Active } after ||
            before.Current != Number(audit, "before") || after.Current != Number(audit, "after") || before.Current - after.Current != amount ||
            before.Maximum != after.Maximum || before.CapacityBinding != after.CapacityBinding)
            throw new InvalidDataException("Combat result lacks its exact typed paid OD transition.");
    }

    private static SpiritualCombatHealingPersistedResult Read(JsonObject root)
    {
        AfterlifeWoundTreatmentAuthority.MaterializeProperties(root);
        Closed(root, "kind schemaVersion source graphResponseFingerprint resourceTransition stages resultFingerprint");
        if (Text(root, "kind") != "spiritual_combat_healing" || Number(root, "schemaVersion") != 1 ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Text(root, "graphResponseFingerprint")) ||
            Text(root, "resultFingerprint") != WoundHistoryState.ComputeTransitionResultFingerprint(root))
            throw new InvalidDataException("Combat result kind/schema/fingerprint differs.");
        var sourceRoot = root["source"]!.AsObject(); var source = ReadSource(sourceRoot);
        var spend = root["resourceTransition"]!.Deserialize<ResourceTransition>() ?? throw new InvalidDataException("Missing combat OD result.");
        if (!JsonNode.DeepEquals(root["resourceTransition"], JsonSerializer.SerializeToNode(spend)))
            throw new InvalidDataException("Combat paid transition shape differs.");
        ValidateSpend(source, sourceRoot, spend);
        if (source.Disposition != "resolved") throw new InvalidDataException("Blocked combat healing cannot have wound stages.");
        var program = SpiritualWoundRecoveryPlanner.BuildActiveOnly(source.Before, source.SourceId + "_check", source.SourceId, source.Calculation);
        var rows = root["stages"]!.AsArray();
        if (rows.Count != program.Count) throw new InvalidDataException("Combat result has missing or extra stages.");
        var stages = new List<SpiritualHealingPublishedStage>(); var current = source.Before;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index]!.AsObject(); Closed(row, "before after operationKey eventRef attemptId");
            var first = WoundMaterializationContract.Parse(row["before"]!.ToJsonString(), "combat.stage.before");
            var last = WoundMaterializationContract.Parse(row["after"]!.ToJsonString(), "combat.stage.after");
            var expected = program[index];
            if (!first.IsValid || !last.IsValid || WoundMaterializationContract.SerializeCanonical(first.Wound!) !=
                    WoundMaterializationContract.SerializeCanonical(current)) throw new InvalidDataException("Combat stage before-chain differs.");
            var after = last.Wound!;
            if (Text(row, "operationKey") != expected.OperationKey || Text(row, "eventRef") != source.EventRef ||
                after.LastTransition.TransitionId != expected.TransitionId || after.LastTransition.Kind != expected.Kind || after.LastTransition.Turn != source.Turn ||
                after.LastTransition.Ordinal != current.LastTransition.Ordinal + 1 || after.Severity.Rank != expected.SeverityRank ||
                after.Recovery.CurrentStepProgress != expected.Progress || after.Recovery.LastTickKey != current.Recovery.LastTickKey ||
                (expected.Kind == "heal") != (after.Lifecycle == "healed") || (row["attemptId"]?.GetValue<string>()) != expected.AttemptId)
                throw new InvalidDataException("Combat stage scalar/no-natural program differs.");
            var stage = new SpiritualHealingPublishedStage(first.Wound!, after, expected.OperationKey, source.EventRef, null, expected.AttemptId);
            SpiritualHealingPersistedResult.ValidateStageScope(stage, expected);
            stages.Add(stage); current = after;
        }
        return new(root, stages);
    }

    private static string Text(JsonObject root, string field) => root[field] is JsonValue v && v.TryGetValue<string>(out var s)
        ? s ?? throw new InvalidDataException("Null combat string.") : throw new InvalidDataException("Missing combat string: " + field);
    private static int Number(JsonObject root, string field) => root[field] is JsonValue v && v.TryGetValue<int>(out var n)
        ? n : throw new InvalidDataException("Missing combat integer: " + field);
    private static void Closed(JsonObject root, string fields)
    {
        var allowed = fields.Split(' ');
        if (root.Count != allowed.Length || allowed.Any(f => !root.ContainsKey(f))) throw new InvalidDataException("Combat shape is not closed.");
    }
}
