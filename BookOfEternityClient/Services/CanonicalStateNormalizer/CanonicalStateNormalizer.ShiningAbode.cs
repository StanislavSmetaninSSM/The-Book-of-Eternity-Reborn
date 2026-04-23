using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeShiningAbodeStateAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = ShiningAbodeState.StatePath;
        var currentNode = await ReadNodeAsync(path);
        if (currentNode == null)
            return;

        var previous = await ReadBackupObjectAsync(path, backups);
        var result = CloneObject(previous ?? ShiningAbodeState.CreateDefaultState());

        if (currentNode is JsonObject currentObject)
            MergeObject(result, currentObject);
        else
            return;

        JsonObject? residentRoot = null;
        if (await ReadNodeAsync(GuardianAbodeResidentState.StatePath) is JsonObject currentResidentObject)
        {
            residentRoot = CloneObject(currentResidentObject);
            GuardianAbodeResidentState.NormalizeShape(residentRoot);
        }

        JsonObject? guardiansRoot = null;
        if (await ReadNodeAsync("game_state/meta/guardians.json") is JsonObject currentGuardiansObject)
            guardiansRoot = CloneObject(currentGuardiansObject);

        ShiningAbodeState.NormalizeStateRoot(result, residentRoot, guardiansRoot);
        await WriteIfChangedAsync(path, currentNode, result);
    }
}
