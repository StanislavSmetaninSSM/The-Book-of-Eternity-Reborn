using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    internal async Task<MortalLocationAcceptedTurnPlan?> NormalizeMortalLocationsAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        var currentMap = await ReadMortalLocationObjectRootAsync(
            MortalLocationMaterializationContract.WorldMapPath);
        var currentProjection = await ReadMortalLocationObjectRootAsync(
            MortalLocationMaterializationContract.CurrentLocationPath);
        var hasCurrentCommand = currentProjection?["currentLocationData"] is JsonObject;
        var hasMapCommand = currentMap?["worldMapUpdates"] is JsonObject;
        if (!hasCurrentCommand && !hasMapCommand)
            return null;

        var preTurnMap = await ReadRequiredMortalLocationBackupAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            backups);
        var preTurnIndex = await ReadRequiredMortalLocationBackupAsync(
            MortalLocationIdentityState.StatePath,
            backups);
        JsonObject? preTurnCurrent = null;
        if (backups?.ContainsKey(MortalLocationMaterializationContract.CurrentLocationPath) == true)
        {
            preTurnCurrent = await ReadRequiredMortalLocationBackupAsync(
                MortalLocationMaterializationContract.CurrentLocationPath,
                backups);
        }
        JsonObject? preTurnStorageContents = null;
        if (backups?.ContainsKey(MortalLocationStorageContentsState.StatePath) == true)
        {
            preTurnStorageContents = await ReadRequiredMortalLocationBackupAsync(
                MortalLocationStorageContentsState.StatePath,
                backups);
        }

        JsonObject? preTurnBootstrapScaffold = null;
        JsonObject? currentBootstrapScaffold = null;
        JsonObject? bootstrapRequest = null;
        if (backups?.ContainsKey(MortalBootstrapLocationScaffold.StatePath) == true)
        {
            preTurnBootstrapScaffold = await ReadRequiredMortalLocationBackupAsync(
                MortalBootstrapLocationScaffold.StatePath,
                backups);
            currentBootstrapScaffold = await ReadMortalLocationObjectRootAsync(
                MortalBootstrapLocationScaffold.StatePath);
            if (currentBootstrapScaffold == null ||
                !JsonNode.DeepEquals(currentBootstrapScaffold, preTurnBootstrapScaffold))
            {
                throw new InvalidDataException(
                    "The client-owned Mortal bootstrap scaffold changed after pending-turn snapshot capture.");
            }
            bootstrapRequest = preTurnBootstrapScaffold["locationMaterializationRequest"] as JsonObject;
        }

        var acceptedTurn = await TryReadCurrentTurnNumberAsync();
        if (acceptedTurn < 1)
        {
            throw new InvalidOperationException(
                "Mortal location sealing requires a positive accepted turn number in input/turn_request.json.");
        }

        var companionAuthority = MortalLocationCompanionAuthority.FromCanonicalRoots(
            await ReadBackupNodeAsync(MortalLocationCompanionAuthority.CodexPath, backups),
            await ReadBackupNodeAsync(MortalLocationCompanionAuthority.RegularQuestsPath, backups),
            await ReadBackupNodeAsync(MortalLocationCompanionAuthority.WorldEventsPath, backups));
        var rawNpcCore = await ReadMortalLocationObjectRootAsync(
            NpcCoreChangesContract.NpcCorePath);
        var rawFactionCore = await ReadMortalLocationObjectRootAsync(
            FactionCoreChangesContract.FactionCorePath);

        var result = MortalLocationAcceptedTurnPlanAuthority.GetOrBuild(
            _fs,
            new MortalLocationAcceptedTurnInput(
                preTurnMap,
                preTurnCurrent,
                preTurnIndex,
                hasCurrentCommand ? currentProjection : null,
                hasMapCommand ? currentMap : null,
                acceptedTurn,
                bootstrapRequest,
                companionAuthority,
                rawNpcCore,
                rawFactionCore,
                preTurnStorageContents,
                MortalItemCurrentLocationCarrier.Select(currentProjection)));
        if (!result.Success || result.Plan == null)
        {
            var issue = result.Issues.FirstOrDefault();
            throw new InvalidDataException(
                issue == null
                    ? "Mortal location normalization failed without a bounded issue."
                    : $"Mortal location normalization failed: {issue.Code} at {issue.FilePath}.");
        }

        var plan = result.Plan;
        if (plan.TouchedPaths.Contains(
                MortalLocationMaterializationContract.WorldMapPath,
                StringComparer.Ordinal))
        {
            await WriteCanonicalFileAtomicAsync(
                MortalLocationMaterializationContract.WorldMapPath,
                plan.FinalWorldMap.ToJsonString(JsonOpts));
        }
        if (plan.TouchedPaths.Contains(
                MortalLocationMaterializationContract.CurrentLocationPath,
                StringComparer.Ordinal) &&
            plan.FinalCurrentLocation != null)
        {
            await WriteCanonicalFileAtomicAsync(
                MortalLocationMaterializationContract.CurrentLocationPath,
                plan.FinalCurrentLocation.ToJsonString(JsonOpts));
        }
        if (plan.TouchedPaths.Contains(
                MortalLocationIdentityState.StatePath,
                StringComparer.Ordinal))
        {
            await WriteCanonicalFileAtomicAsync(
                MortalLocationIdentityState.StatePath,
                plan.FinalIdentityIndex.ToJsonString(JsonOpts));
        }
        if (plan.TouchedPaths.Contains(
                MortalLocationStorageContentsState.StatePath,
                StringComparer.Ordinal))
        {
            await WriteCanonicalFileAtomicAsync(
                MortalLocationStorageContentsState.StatePath,
                plan.FinalStorageContents.ToJsonString(JsonOpts));
        }
        if (plan.TouchedPaths.Contains(
                MortalBootstrapLocationScaffold.StatePath,
                StringComparer.Ordinal) &&
            plan.FinalBootstrapScaffold != null &&
            preTurnBootstrapScaffold != null)
        {
            var settledRoot = preTurnBootstrapScaffold.DeepClone().AsObject();
            settledRoot["locationMaterializationRequest"] =
                plan.FinalBootstrapScaffold.DeepClone();
            await WriteCanonicalFileAtomicAsync(
                MortalBootstrapLocationScaffold.StatePath,
                settledRoot.ToJsonString(JsonOpts));
        }
        await ApplyMortalLocationGovernedRewritesAsync(plan, rawNpcCore);
        return plan;
    }

    private async Task ApplyMortalLocationGovernedRewritesAsync(
        MortalLocationAcceptedTurnPlan plan,
        JsonObject? rawNpcCore)
    {
        var rewrites = plan.GovernedRewrites
            .Where(rewrite => string.Equals(
                rewrite.CarrierPath,
                NpcCoreChangesContract.NpcCorePath,
                StringComparison.Ordinal))
            .ToArray();
        if (rewrites.Length == 0)
            return;
        if (rawNpcCore == null)
        {
            throw new InvalidDataException(
                "The governed NPC location rewrite carrier changed after planning.");
        }

        var rewritten = rawNpcCore.DeepClone().AsObject();
        foreach (var rewrite in rewrites)
        {
            if (!TryResolveMortalLocationRewriteEntry(
                    rewritten,
                    rewrite.EntryPath,
                    out var entry) ||
                !TryReadExactMortalLocationRewriteString(
                    entry,
                    rewrite.OwnerField,
                    out var ownerId) ||
                !string.Equals(ownerId, rewrite.OwnerId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The governed NPC location rewrite target '{rewrite.EntryPath}' changed after planning.");
            }

            var target = (rewrite.TemporaryFieldPath, rewrite.PermanentFieldPath) switch
            {
                ("location.initialLocationId", "location.currentLocationId") =>
                    entry["location"] as JsonObject,
                ("initialLocationId", "currentLocationId") => entry,
                _ => null
            };
            if (target == null ||
                target["currentLocationId"] != null ||
                !TryReadExactMortalLocationRewriteString(
                    target,
                    "initialLocationId",
                    out var initialId) ||
                !string.Equals(initialId, rewrite.InitialId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"The governed NPC location rewrite target '{rewrite.EntryPath}' changed after planning.");
            }

            target["currentLocationId"] = rewrite.PermanentId;
            target["initialLocationId"] = null;
        }

        await WriteCanonicalFileAtomicAsync(
            NpcCoreChangesContract.NpcCorePath,
            rewritten.ToJsonString(JsonOpts));
    }

    private static bool TryResolveMortalLocationRewriteEntry(
        JsonObject root,
        string entryPath,
        out JsonObject entry)
    {
        entry = new JsonObject();
        var open = entryPath.IndexOf('[', StringComparison.Ordinal);
        if (open <= 0 ||
            !entryPath.EndsWith(']') ||
            !int.TryParse(entryPath[(open + 1)..^1], out var index) ||
            index < 0 ||
            root[entryPath[..open]] is not JsonArray values ||
            index >= values.Count ||
            values[index] is not JsonObject resolved)
        {
            return false;
        }

        entry = resolved;
        return true;
    }

    private static bool TryReadExactMortalLocationRewriteString(
        JsonObject value,
        string field,
        out string result)
    {
        result = string.Empty;
        if (value[field] is not JsonValue scalar ||
            !scalar.TryGetValue<string>(out var candidate) ||
            string.IsNullOrEmpty(candidate) ||
            !string.Equals(candidate, candidate.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        result = candidate;
        return true;
    }

    private async Task<JsonObject?> ReadMortalLocationObjectRootAsync(string path)
    {
        var json = await ReadCanonicalFileAsync(path);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonNode.Parse(json) as JsonObject ??
                throw new InvalidDataException($"{path} must have an object root.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{path} contains malformed JSON.", exception);
        }
    }

    private async Task<JsonObject> ReadRequiredMortalLocationBackupAsync(
        string path,
        IReadOnlyDictionary<string, string>? backups)
    {
        if (backups == null || !backups.ContainsKey(path))
        {
            throw new InvalidOperationException(
                $"Mortal location normalization requires a validated pre-turn backup for '{path}'.");
        }

        return await ReadBackupObjectAsync(path, backups) ??
            throw new InvalidDataException(
                $"Mortal location pre-turn backup for '{path}' must be readable object JSON.");
    }
}
