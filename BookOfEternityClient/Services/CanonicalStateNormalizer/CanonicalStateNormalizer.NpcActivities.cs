using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private const string NpcActivitiesPath = "game_state/npcs/npc_activities.json";
    private sealed record NpcActivityUpdateInput(string Raw, JsonObject Sidecar,
        JsonObject OriginalCore, IReadOnlyDictionary<string, JsonObject> Updates);

    // Operation-local DATA only. The existing normalizer lease/accepted owner still
    // governs every write; no plan or publication permission is cached here.
    private async Task<NpcActivityUpdateInput?> PrepareNpcActivityUpdatesAsync(
        IReadOnlyDictionary<string, string>? backups, MortalItemAcceptedTurnNormalizationMode itemMode,
        AcceptedMechanicsPlan? plan, NpcCoreChangesContract.ProgressionSourceContext? source, string? realm,
        MortalWoundTreatmentPublicationTakeReceipt? treatment)
    {
        var raw = await ReadCanonicalFileAsync(NpcActivitiesPath);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var sidecar = JsonNode.Parse(raw) as JsonObject
            ?? throw new InvalidDataException("NPC activity commands require an object.");
        if (!sidecar.ContainsKey("NPCActivityUpdates")) return null;
        if (sidecar["NPCActivityUpdates"] is not JsonArray commands)
            throw new InvalidDataException("NPCActivityUpdates must be an array.");
        if (commands.Count == 0) return null;
        if (!RealmSemantics.IsMortalRealm(realm) ||
            itemMode is MortalItemAcceptedTurnNormalizationMode.ClientOwnedBootstrap ||
            source is not null && source.Realm != "mortal_world" ||
            _npcProgressionSource?.SkillInput is not null || treatment is not null ||
            itemMode is MortalItemAcceptedTurnNormalizationMode.Validated { Snapshot.HasFinalPublicationBaseline: true })
            throw new InvalidDataException("NPC activity updates require ordinary Mortal publication without specialized NPC candidates.");
        if (sidecar["completeNPCActivities"] is not null && sidecar["completeNPCActivities"] is not JsonArray { Count: 0 })
            throw new InvalidDataException("NPC activity updates cannot compose with terminal completion in this publication path.");
        if (plan is not null)
        {
            if (plan.AwaitsPendingResolution || plan.TreatmentResourcePublicationAuthority is not null ||
                plan.JointRepairCommonAuthority is not null || plan.SpiritualHealingContinuation is not null ||
                plan.DirectWoundPublicationAuthority is not null ||
                plan.HasWoundStageBundle && !plan.HasOrdinaryOriginalTurnWoundStages ||
                plan.LiveWoundProofFingerprint is not null)
                throw new InvalidDataException("NPC activity updates cannot enter pending or specialized publication.");
            foreach (var path in new[] { NpcCoreChangesContract.NpcCorePath, NpcActivitiesPath })
                if (plan.HasWholeCarrierAfterImage(path) || plan.PendingAfterImages.ContainsKey(path) ||
                    plan.ConsumedPaths.Contains(path, StringComparer.Ordinal) ||
                    plan.OwnerTransitions.Any(transition => transition.Path == path))
                    throw new InvalidDataException("NPC activity updates overlap a common owner output before composition is supported.");
        }
        var original = await ReadBackupObjectAsync(NpcCoreChangesContract.NpcCorePath, backups)
            ?? throw new InvalidDataException("NPC activity updates require the actual original NPC core.");
        var current = await ReadNodeAsync(NpcCoreChangesContract.NpcCorePath) as JsonObject
            ?? throw new InvalidDataException("NPC activity updates require current NPC core.");
        var updates = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in commands)
        {
            if (node is not JsonObject command || command["NPCId"] is not JsonValue idValue ||
                !idValue.TryGetValue<string>(out var id) || string.IsNullOrWhiteSpace(id) || id != id.Trim() ||
                command["activityUpdate"] is not JsonObject patch || patch.Count == 0 || !updates.TryAdd(id, patch))
                throw new InvalidDataException("NPC activity updates require unique exact NPC IDs and nonempty patches.");
            var actors = ExactActivityActors(original, id);
            if (actors[0]["currentActivity"] is not JsonObject)
                throw new InvalidDataException("NPC activity updates cannot create an activity or infer an actor.");
            if (ExactActivityActors(current, id).Any(actor =>
                    !JsonNode.DeepEquals(actor["currentActivity"], actors[0]["currentActivity"])))
                throw new InvalidDataException("NPC activity changed outside its original command input.");
            foreach (var field in patch)
                RequireNpcActivityField(field.Key, field.Value);
        }
        return new(raw, sidecar, original, updates);
    }

    private async Task NormalizeNpcActivityUpdatesAsync(NpcActivityUpdateInput? input)
    {
        if (input is null) return;
        if (await ReadCanonicalFileAsync(NpcActivitiesPath) != input.Raw)
            throw new InvalidDataException("NPC activity commands changed after preparation.");
        var current = await ReadNodeAsync(NpcCoreChangesContract.NpcCorePath) as JsonObject
            ?? throw new InvalidDataException("NPC activity publication lost current NPC core.");
        var result = current.DeepClone().AsObject();
        foreach (var (id, patch) in input.Updates)
        {
            var originalActivity = ExactActivityActors(input.OriginalCore, id)[0]["currentActivity"]!;
            foreach (var actor in ExactActivityActors(result, id))
            {
                if (!JsonNode.DeepEquals(actor["currentActivity"], originalActivity))
                    throw new InvalidDataException("NPC activity changed outside its original command input.");
                var activity = actor["currentActivity"]!.AsObject();
                foreach (var field in patch) activity[field.Key] = field.Value?.DeepClone();
            }
        }
        // Validate all targets and fresh commands before the first activity write.
        if (await ReadCanonicalFileAsync(NpcActivitiesPath) != input.Raw)
            throw new InvalidDataException("NPC activity commands changed before publication.");
        await WriteIfChangedAsync(NpcCoreChangesContract.NpcCorePath, current, result);
        if (await ReadCanonicalFileAsync(NpcActivitiesPath) != input.Raw)
            throw new InvalidDataException("NPC activity commands changed before consumption.");
        var consumed = input.Sidecar.DeepClone().AsObject();
        consumed.Remove("NPCActivityUpdates");
        await WriteIfChangedAsync(NpcActivitiesPath, input.Sidecar, consumed);
    }

    private static List<JsonObject> ExactActivityActors(JsonObject root, string id)
    {
        var actors = new List<JsonObject>();
        foreach (var section in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            if (root[section] is not JsonArray array) continue;
            var matches = new List<JsonObject>();
            foreach (var actor in array.OfType<JsonObject>())
            {
                var resemblesTarget = actor.Any(property =>
                    (property.Key.Equals("NPCId", StringComparison.OrdinalIgnoreCase) ||
                     property.Key.Equals("id", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value is JsonValue alias && alias.TryGetValue<string>(out var candidate) &&
                    string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase));
                if (!resemblesTarget) continue;
                if (!GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actor, out var actorId) || actorId != id)
                    throw new InvalidDataException("NPC activity identity has conflicting or inexact aliases.");
                matches.Add(actor);
            }
            if (matches.Count > 1) throw new InvalidDataException("NPC activity identity is ambiguous.");
            actors.AddRange(matches);
        }
        if (actors.Count == 0 || actors.Any(actor => !JsonNode.DeepEquals(actor, actors[0])))
            throw new InvalidDataException("NPC activity requires exact agreeing canonical actor copies.");
        return actors;
    }

    private static void RequireNpcActivityField(string field, JsonNode? value)
    {
        if (field is "totalTimeCostMinutes" or "timeSpentMinutes" or "currentStepNumber" or "totalStepsInActivity")
        {
            if (value is JsonValue number && number.TryGetValue<int>(out _)) return;
        }
        else if (field is "linkedQuestId" or "linkedPlotOutlineNode")
        {
            if (value is null || value is JsonValue optional && optional.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)) return;
        }
        else if (field is "activityName" or "description" or "activeState")
        {
            if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) &&
                (field != "activeState" || !text.Equals("Completed", StringComparison.OrdinalIgnoreCase) && !text.Equals("Abandoned", StringComparison.OrdinalIgnoreCase))) return;
        }
        throw new InvalidDataException("Unsupported or malformed nonterminal NPC activity field: " + field);
    }
}
