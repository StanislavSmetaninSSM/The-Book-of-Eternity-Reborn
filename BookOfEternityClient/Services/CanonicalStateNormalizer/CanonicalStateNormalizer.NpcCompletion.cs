using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private sealed record NpcCompletionInput(string Raw, JsonObject Sidecar, JsonObject Original,
        IReadOnlyDictionary<string, JsonObject> Commands, MortalNpcActivityCompletionSource Source);

    internal async Task<CanonicalStateNormalizer> WithOrdinaryNpcCompletionSourceAsync()
    {
        if (_writeLease is null) throw new InvalidDataException("Terminal NPC publication needs its ordinary held owner.");
        var source = await MortalNpcActivityCompletionSource.CaptureOrdinaryAsync(_fs, _writeLease);
        return new(_fs, _logger, _writeLease, _normalizationWriteAuthority, _guardianPowerJournalMode,
            _spiritualLocalityNormalizationOwner, _npcProgressionSource, source);
    }

    internal Task RequireNpcCompletionSourceCurrentAsync() => _npcCompletionSource is null
        ? Task.CompletedTask : _npcCompletionSource.RequireCurrentAsync(_fs,
            _writeLease ?? throw new InvalidDataException("Terminal NPC publication lost its held lease."));

    private async Task<NpcCompletionInput?> PrepareNpcActivityCompletionsAsync(
        MortalItemAcceptedTurnNormalizationMode mode, AcceptedMechanicsPlan? plan,
        MortalWoundTreatmentPublicationTakeReceipt? treatment)
    {
        var raw = await ReadCanonicalFileAsync(NpcActivitiesPath);
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var sidecar = JsonNode.Parse(raw) as JsonObject ?? throw new InvalidDataException("NPC commands require an object.");
        if (sidecar["completeNPCActivities"] is null or JsonArray { Count: 0 }) return null;
        if (sidecar["completeNPCActivities"] is not JsonArray commands)
            throw new InvalidDataException("Terminal NPC commands must be an array.");
        if (_npcCompletionSource is null || _writeLease is null ||
            _guardianPowerJournalMode != GuardianPowerJournalNormalizationMode.RepairTurnLevelAuthority ||
            _spiritualLocalityNormalizationOwner is not null || treatment is not null ||
            _npcProgressionSource?.SkillInput is not null ||
            mode is MortalItemAcceptedTurnNormalizationMode.ClientOwnedBootstrap ||
            mode is MortalItemAcceptedTurnNormalizationMode.Validated { Snapshot.HasFinalPublicationBaseline: true })
            throw new InvalidDataException("Terminal NPC activity requires its issued ordinary original operation.");
        if (sidecar["NPCActivityUpdates"] is not null && sidecar["NPCActivityUpdates"] is not JsonArray { Count: 0 })
            throw new InvalidDataException("NPC activity updates cannot compose with terminal completion in this publication path.");
        if (plan is not null && (plan.AwaitsPendingResolution || plan.TreatmentResourcePublicationAuthority is not null ||
            plan.JointRepairCommonAuthority is not null || plan.SpiritualHealingContinuation is not null ||
            plan.DirectWoundPublicationAuthority is not null || plan.LiveWoundProofFingerprint is not null ||
            plan.HasWoundStageBundle && !plan.HasOrdinaryOriginalTurnWoundStages))
            throw new InvalidDataException("Terminal NPC activity does not support specialized publication.");
        foreach (var path in new[] { NpcCoreChangesContract.NpcCorePath, NpcActivitiesPath })
            if (plan is not null && (plan.HasWholeCarrierAfterImage(path) || plan.PendingAfterImages.ContainsKey(path) ||
                plan.ConsumedPaths.Contains(path, StringComparer.Ordinal) || plan.OwnerTransitions.Any(t => t.Path == path)))
                throw new InvalidDataException("Terminal NPC activity overlaps another canonical owner.");
        await _npcCompletionSource.RequireCurrentAsync(_fs, _writeLease);
        var original = _npcCompletionSource.OriginalCore;
        var current = await ReadNodeAsync(NpcCoreChangesContract.NpcCorePath) as JsonObject
            ?? throw new InvalidDataException("Terminal NPC publication lost current core.");
        var accepted = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var node in commands)
        {
            if (node is not JsonObject command) throw new InvalidDataException("Terminal NPC command must be an object.");
            var id = CompletionText(command, "NPCId");
            var name = CompletionText(command, "NPCName");
            var activity = CompletionText(command, "activityName");
            var state = CompletionText(command, "finalState");
            var outcome = CompletionText(command, "finalOutcome");
            CompletionText(command, "narrativeSummary");
            if (!accepted.TryAdd(id, command) || command.ContainsKey("completionTurn") ||
                outcome is not ("Success" or "SuccessWithComplication" or "Failure") ||
                state is not ("Completed" or "Abandoned") || state == "Abandoned" && outcome != "Failure" ||
                command.Any(p => p.Key is not ("NPCId" or "NPCName" or "activityName" or "finalState" or "finalOutcome" or "narrativeSummary")))
                throw new InvalidDataException("Terminal NPC command requires unique exact target and explicit compatible outcome; turn is client-owned.");
            var actor = ExactActivityActors(original, id)[0];
            if (actor["name"]?.GetValue<string>() != name || actor["currentActivity"] is not JsonObject originalActivity ||
                originalActivity["activityName"]?.GetValue<string>() != activity)
                throw new InvalidDataException("Terminal NPC command does not target the original active activity.");
            RequireCompletionTargetUnchanged(current, id, actor);
        }
        return new(raw, sidecar, original, accepted, _npcCompletionSource);
    }

    private async Task NormalizeNpcActivityCompletionsAsync(NpcCompletionInput? input)
    {
        if (input is null) return;
        await input.Source.RequireCurrentAsync(_fs, _writeLease!);
        if (await ReadCanonicalFileAsync(NpcActivitiesPath) != input.Raw)
            throw new InvalidDataException("Terminal NPC commands changed after preparation.");
        var current = await ReadNodeAsync(NpcCoreChangesContract.NpcCorePath) as JsonObject
            ?? throw new InvalidDataException("Terminal NPC publication lost current core.");
        var result = current.DeepClone().AsObject();
        foreach (var (id, command) in input.Commands)
        {
            RequireCompletionTargetUnchanged(result, id, ExactActivityActors(input.Original, id)[0]);
            foreach (var actor in ExactActivityActors(result, id))
            {
                var history = actor["completedActivities"] as JsonArray;
                if (history is null && actor["completedActivities"] is not null)
                    throw new InvalidDataException("Terminal NPC history must be an array.");
                if (history is null) actor["completedActivities"] = history = new JsonArray();
                history.Add(new JsonObject { ["activityName"] = command["activityName"]!.DeepClone(),
                    ["completionTurn"] = input.Source.Turn, ["finalOutcome"] = command["finalOutcome"]!.DeepClone(),
                    ["narrativeSummary"] = command["narrativeSummary"]!.DeepClone() });
                actor["currentActivity"] = null;
            }
        }
        // All target/mirror checks precede the first write. Preserve unrelated current fields.
        await input.Source.RequireCurrentAsync(_fs, _writeLease!);
        if (await ReadCanonicalFileAsync(NpcActivitiesPath) != input.Raw)
            throw new InvalidDataException("Terminal NPC commands changed before publication.");
        await WriteIfChangedAsync(NpcCoreChangesContract.NpcCorePath, current, result);
        if (await ReadCanonicalFileAsync(NpcActivitiesPath) != input.Raw)
            throw new InvalidDataException("Terminal NPC commands changed before consumption.");
        await input.Source.RequireCurrentAsync(_fs, _writeLease!);
        var consumed = input.Sidecar.DeepClone().AsObject(); consumed.Remove("completeNPCActivities");
        await WriteIfChangedAsync(NpcActivitiesPath, input.Sidecar, consumed);
    }

    private static void RequireCompletionTargetUnchanged(JsonObject current, string id, JsonObject original)
    {
        foreach (var actor in ExactActivityActors(current, id))
            if (!JsonNode.DeepEquals(actor["currentActivity"], original["currentActivity"]) ||
                !JsonNode.DeepEquals(actor["completedActivities"], original["completedActivities"]) ||
                !JsonNode.DeepEquals(actor["name"], original["name"]))
                throw new InvalidDataException("Terminal NPC original identity/activity/history changed outside its owner.");
    }

    private static string CompletionText(JsonObject command, string key) =>
        command[key] is JsonValue value && value.TryGetValue<string>(out var text) &&
        !string.IsNullOrWhiteSpace(text) && text == text.Trim() ? text :
        throw new InvalidDataException("Terminal NPC command requires exact " + key + ".");
}
