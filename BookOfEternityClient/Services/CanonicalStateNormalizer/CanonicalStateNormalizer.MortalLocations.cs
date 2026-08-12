using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    internal async Task NormalizeMortalLocationsAsync(
        IReadOnlyDictionary<string, string>? backups)
    {
        var currentMap = await ReadMortalLocationObjectRootAsync(
            MortalLocationMaterializationContract.WorldMapPath);
        var currentProjection = await ReadMortalLocationObjectRootAsync(
            MortalLocationMaterializationContract.CurrentLocationPath);
        var hasCurrentCommand = currentProjection?["currentLocationData"] is JsonObject;
        var hasMapCommand = currentMap?["worldMapUpdates"] is JsonObject;
        if (!hasCurrentCommand && !hasMapCommand)
            return;

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

        var result = MortalLocationAcceptedTurnPlanner.Build(
            new MortalLocationAcceptedTurnInput(
                preTurnMap,
                preTurnCurrent,
                preTurnIndex,
                hasCurrentCommand ? currentProjection : null,
                hasMapCommand ? currentMap : null,
                acceptedTurn,
                bootstrapRequest));
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
