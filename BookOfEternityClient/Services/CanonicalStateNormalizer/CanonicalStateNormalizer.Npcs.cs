using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeNpcTradeCoreAsync(IReadOnlyDictionary<string, string>? backups)
    {
        const string path = "game_state/npcs/npc_core.json";
        var currentNode = await ReadNodeAsync(path);
        if (currentNode is not JsonObject currentObj)
            return;

        var result = CloneObject(currentObj);
        var changed = false;

        if (result[NpcTradeRequestState.UpdateReceiptsProperty] is JsonArray receiptUpdates)
        {
            NpcTradeRequestState.ApplyReceiptUpdates(result, receiptUpdates);
            result.Remove(NpcTradeRequestState.UpdateReceiptsProperty);
            changed = true;
        }

        foreach (var npcs in GuardianPolicyContracts.EnumerateCanonicalNpcObjectArrays(result))
        {
            foreach (var npc in npcs.OfType<JsonObject>())
            {
                var before = npc.ToJsonString();
                NpcTradeRequestState.NormalizeNpcTradeReceiptsShape(npc);
                if (!string.Equals(before, npc.ToJsonString(), StringComparison.Ordinal))
                    changed = true;
            }
        }

        if (changed)
            await WriteIfChangedAsync(path, currentNode, result);
    }
}
