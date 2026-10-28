using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    internal async Task<bool> IsPublishedSpiritualCombatAsync(ProgressionControl control, bool requirePublished = false)
    {
        if (!IsAfterlifeRealm(control.CurrentRealm) || control.AfterlifeCatchupRequired ||
            new[] { control.ChaosSeaCyclesExpectedThisTurn, control.GuardianProjectCyclesExpectedThisTurn,
                control.ResidentAgencyCyclesExpectedThisTurn, control.ShiningAbodeCyclesExpectedThisTurn,
                control.ShiningFactionCyclesExpectedThisTurn, control.ShiningTradeCyclesExpectedThisTurn,
                control.WorldCyclesAlreadyPendingBeforeTurn, control.FactionCyclesAlreadyPendingBeforeTurn }.Any(v => v != 0)) return false;
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var bytes = await _fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.TurnRequestPath);
        if (bytes is null) return false;
        var request = StrictJsonAuthority.Deserialize<TurnRequest>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes), JsonOpts, "combat current request")!;
        var selected = request.SpiritualHealing?["mode"]?.GetValue<string>() == "combat_self";
        if (!selected) return false;
        var published = await IsPublishedSpiritualCombatCoreAsync(control, request.SessionId, request.RequestId, request.TurnNumber,
                path => _fs.ReadFileBytesAsync(lease, path), expectedProjection: request.SpiritualHealing, allowUnpublished: !requirePublished);
        if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(control), JsonSerializer.SerializeToNode(request.ProgressionControl)))
            throw new InvalidDataException("Combat current control differs from its selected request.");
        if (!published && !requirePublished && await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath) is { } pending)
        {
            var intent = SpiritualCombatHealingIntent.Parse(StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(pending), JsonOpts, "pending combat intent"));
            if (intent.IsBound(new(request.SessionId, request.RequestId, request.TurnNumber, control), intent.Selection) &&
                request.SpiritualHealing?["intentFingerprint"]?.GetValue<string>() == intent.Fingerprint) return false;
        }
        if (!published) throw new InvalidDataException("The current combat intent lacks its exact accepted no-cycle result.");
        return true;
    }

    internal sealed record PublishedCombatHealingData(SpiritualCombatHealingPersistedResult.SourceData Source,
        SpiritualCombatHealingPersistedResult? Result, JsonObject Outcome);

    internal static async Task<bool> IsPublishedSpiritualCombatCoreAsync(ProgressionControl control,
        string sessionId, string requestId, int turn, Func<string, Task<byte[]?>> read,
        string? expectedSnapshotToken = null, JsonObject? expectedIntent = null, JsonObject? expectedProjection = null, bool allowUnpublished = false) =>
        await ReadPublishedSpiritualCombatCoreAsync(control, sessionId, requestId, turn, read,
            expectedSnapshotToken, expectedIntent, expectedProjection, allowUnpublished) is not null;

    internal static async Task<PublishedCombatHealingData?> ReadPublishedSpiritualCombatCoreAsync(ProgressionControl control,
        string sessionId, string requestId, int turn, Func<string, Task<byte[]?>> read,
        string? expectedSnapshotToken = null, JsonObject? expectedIntent = null, JsonObject? expectedProjection = null, bool allowUnpublished = false)
    {
        if ((expectedSnapshotToken is null) != (expectedIntent is null))
            throw new InvalidDataException("Combat replay original binding is incomplete.");
        async Task<string?> Text(string path)
        {
            var bytes = await read(path); return bytes is null ? null : CanonicalJsonUtf8.DecodeOneOptionalBom(bytes);
        }
        var receiptJson = await Text(SpiritualWoundOpportunityReceiptState.StatePath);
        var history = WoundHistoryState.Parse(await Text(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        if (!history.IsValid || history.State is null) throw new InvalidDataException("Combat History is not canonical.");
        SpiritualWoundOpportunityReceiptState? ledger = null;
        if (receiptJson is not null)
        {
            var parsed = SpiritualWoundOpportunityReceiptState.Parse(receiptJson, SpiritualWoundOpportunityReceiptState.StatePath);
            if (!parsed.IsValid || parsed.State is null) throw new InvalidDataException("Combat receipt is not canonical.");
            ledger = parsed.State;
        }
        if (ledger?.HasCombatHealing != true && history.State.Transitions.Any(t => t.TransitionResult is SpiritualCombatHealingPersistedResult))
            throw new InvalidDataException("Combat History lost its mandatory durable healing family.");
        var entry = ledger?.ReadCombatOutcome(sessionId, requestId, turn);
        if (entry is null)
        {
            if (history.State.Transitions.Select(t => t.TransitionResult).OfType<SpiritualCombatHealingPersistedResult>().Any(r =>
                    r.Source["sessionId"]!.GetValue<string>() == sessionId && r.Source["requestId"]!.GetValue<string>() == requestId &&
                    r.Source["turn"]!.GetValue<int>() == turn) || expectedIntent is not null || !allowUnpublished && expectedProjection is not null)
                throw new InvalidDataException("Combat original has no mandatory accepted healing outcome.");
            return null;
        }
        var witness = entry["witness"]!.AsObject();
        var source = SpiritualCombatHealingPersistedResult.ReadSource(witness); var intent = source.Intent;
        if (expectedIntent is not null && (!JsonNode.DeepEquals(expectedIntent, intent.Root) ||
                witness["snapshotToken"]!.GetValue<string>() != expectedSnapshotToken) ||
            !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(control), intent.Root["progressionControl"]))
            throw new InvalidDataException("Combat result differs from its actual original/control.");
        if (expectedProjection is not null && !JsonNode.DeepEquals(expectedProjection, intent.CreateGmProjection(source.Before, source.Source)))
            throw new InvalidDataException("Combat result differs from its current selected projection.");
        foreach (var path in new[] { ReportPath, AcceptedMechanicsPlan.WoundCommandPath, ResourceMaterializationContract.CommandPath,
            EffectAcceptedTurnPlan.CommandPath, SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath })
            if (await read(path) is not null) throw new InvalidDataException("Combat replay has a replaced consumed input: " + path);
        var images = intent.Root["sourceImages"]!.AsArray();
        foreach (var path in new[] { SchedulePath, EffectAcceptedTurnInputComposer.WorldTimePath })
        {
            var bytes = await read(path);
            var expected = images.Single(i => i!["path"]!.GetValue<string>() == path)!["fingerprint"]!.GetValue<string>();
            if (new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint != expected)
                throw new InvalidDataException("Combat replay changed its exact scheduler/world image: " + path);
        }
        var soul = StrictJsonAuthority.Deserialize<JsonObject>(await Text("game_state/meta/soul_state.json") ?? "null", JsonOpts, "combat current soul");
        if (!AfterlifeEntityProfileState.TryNormalizeEffectRealm(soul?["currentRealm"]?.GetValue<string>(), out var realm) || realm != "chaos_sea")
            throw new InvalidDataException("Combat replay changed its realm.");
        var roots = new Dictionary<string, JsonObject?>(StringComparer.Ordinal);
        foreach (var path in WoundAcceptedTurnSnapshotContract.CarrierPaths)
        {
            var text = await Text(path); roots[path] = text is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(text, JsonOpts, path);
        }
        var catalog = WoundCarrierCatalog.Build(new(roots[WoundCarrierCatalog.PlayerPath], roots[WoundCarrierCatalog.NpcPath],
            roots[WoundCarrierCatalog.EnemiesPath], roots[WoundCarrierCatalog.AlliesPath], roots[WoundCarrierCatalog.AfterlifeProfilesPath]));
        var identities = WoundIdentityState.Parse(await Text(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        if (catalog.Issues.Count != 0 || !identities.IsValid || !history.IsValid || history.State!.ValidateAgreement(identities.State!, catalog).Count != 0)
            throw new InvalidDataException("Combat replay lost its wound/identity/History agreement.");
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await Text(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        var state = definitions.Catalog is null ? null : ResourceStateContract.ParseCanonical(await Text(ResourceMaterializationContract.StatePath), definitions.Catalog, allowMissingPristine: false);
        var resources = definitions.Catalog is null ? null : ResourceHistoryState.ParseCanonical(await Text(ResourceMaterializationContract.HistoryPath), definitions.Catalog, allowMissingPristine: false);
        if (definitions.Catalog is null || definitions.Issues.Count != 0 || state?.Ledger is null || state.Issues.Count != 0 ||
            resources?.History is null || resources.Issues.Count != 0 || resources.History.ValidateStateAgreement(state.Ledger).Count != 0)
            throw new InvalidDataException("Combat replay lost canonical resource agreement.");
        ledger!.RequireCombatHistoryResourceAgreement(history.State!, resources.History);
        if (history.State!.Transitions.Any(t => t.Turn == turn && t.EventRef == source.EventRef && t.Kind == "recover"))
            throw new InvalidDataException("Combat cannot contain a natural recovery tick.");
        var result = history.State.Transitions.Select(t => t.TransitionResult).OfType<SpiritualCombatHealingPersistedResult>()
            .SingleOrDefault(r => r.SourceFingerprint == source.Fingerprint);
        if (source.Disposition == "resolved")
        {
            if (result is null) throw new InvalidDataException("Combat result lost its wound stages.");
            var final = result.Stages[^1];
            if (history.State.Transitions.Last(t => t.WoundId == source.Before.WoundId).TransitionId != final.TransitionId)
                throw new InvalidDataException("Combat result no longer describes the current selected wound.");
        }
        else
        {
            if (!catalog.TryResolveOne(source.Before.WoundId, out var current) || current.Wound.Recovery.LastTickKey != source.Before.Recovery.LastTickKey)
                throw new InvalidDataException("Blocked combat changed natural tick or lost its original wound.");
            var receipt = StrictJsonAuthority.Deserialize<JsonObject>(receiptJson!, JsonOpts, "combat receipt")!;
            var incoming = receipt["sources"]!.AsArray().OfType<JsonObject>().Where(row =>
                row["instanceId"]!.GetValue<string>() == entry["instanceId"]!.GetValue<string>() &&
                row["witness"]!["conflictId"]!.GetValue<string>() == intent.ConflictId &&
                row["witness"]!["exchangeId"]!.GetValue<string>() == source.ExchangeId &&
                row["witness"]!["affectedSide"]!.GetValue<string>() == "player" &&
                row["witness"]!["turnEvidence"]!["sessionId"]!.GetValue<string>() == sessionId &&
                row["witness"]!["turnEvidence"]!["requestId"]!.GetValue<string>() == requestId &&
                row["witness"]!["turnEvidence"]!["snapshotToken"]!.GetValue<string>() == witness["snapshotToken"]!.GetValue<string>() &&
                row["witness"]!["turnEvidence"]!["turn"]!.GetValue<int>() == turn).ToArray();
            var changes = history.State.Transitions.Where(t => t.WoundId == source.Before.WoundId &&
                t.WoundTransitionOrdinal > source.Before.LastTransition.Ordinal).ToArray();
            var materialized = receipt["decisions"]!.AsArray().OfType<JsonObject>().Where(d =>
                d["decision"]?.GetValue<string>() == "materialize" && d["woundId"]?.GetValue<string>() == source.Before.WoundId &&
                incoming.Any(row => row["witness"]!["sourceId"]!.GetValue<string>() == d["sourceId"]?.GetValue<string>())).ToArray();
            if (changes.Length != materialized.Length || changes.Length > 1)
                throw new InvalidDataException("Blocked combat lost its exact selected-wound counterharm bijection.");
            if (changes.Length == 0 && WoundMaterializationContract.SerializeCanonical(current.Wound) != WoundMaterializationContract.SerializeCanonical(source.Before))
                throw new InvalidDataException("Blocked combat fabricated wound progress.");
            foreach (var change in changes)
            {
                var decision = materialized.Single(d => d["transitionId"]?.GetValue<string>() == change.TransitionId);
                var harm = incoming.Single(row => row["witness"]!["sourceId"]!.GetValue<string>() == decision["sourceId"]!.GetValue<string>())["witness"]!;
                var eventRef = "spiritual_wound_event_" + WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                { "book_of_eternity.spiritual_wound_source_event", "1", sessionId, requestId,
                    witness["snapshotToken"]!.GetValue<string>(), intent.ConflictId, source.ExchangeId, "player" })[7..];
                if (change.Kind != "worsen" || change.Turn != turn || change.EventRef != eventRef ||
                    change.SourceFingerprint != harm["sourceFingerprint"]!.GetValue<string>() ||
                    change.SourceFingerprint != decision["sourceFingerprint"]!.GetValue<string>() ||
                    change.BeforeFingerprint != WoundIdentityState.ComputeSemanticFingerprint(source.Before) ||
                    change.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(current.Wound) ||
                    change.TransitionId != current.Wound.LastTransition.TransitionId || current.Wound.Recovery.CurrentStepProgress != 0)
                    throw new InvalidDataException("Blocked combat wound change lacks its exact independently receipted counterharm.");
            }
        }
        return new(source, result, entry["outcome"]!.DeepClone().AsObject());
    }
}
