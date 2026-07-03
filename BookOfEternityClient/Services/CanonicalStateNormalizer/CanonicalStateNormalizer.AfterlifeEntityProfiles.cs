using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private async Task NormalizeAfterlifeEntityProfilesAsync(IReadOnlyDictionary<string, string>? backups)
    {
        var currentNode = await ReadNodeAsync(AfterlifeEntityProfileState.StatePath);
        if (currentNode == null)
            return;

        if (currentNode is not JsonObject currentRoot)
            return;

        var previousRoot = await ReadBackupObjectAsync(AfterlifeEntityProfileState.StatePath, backups);
        var progressionReportRoot = await ReadCurrentTurnAfterlifeProgressionReportRootAsync();
        var result = AfterlifeEntityProfileState.ProjectCanonicalRoot(currentRoot, previousRoot, progressionReportRoot);
        var soulRoot = await ReadNodeAsync("game_state/meta/soul_state.json") as JsonObject;
        var shiningRoot = await ReadNodeAsync(ShiningAbodeState.StatePath) as JsonObject;
        AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(result, soulRoot, shiningRoot);
        await WriteIfChangedAsync(AfterlifeEntityProfileState.StatePath, currentNode, result);
    }

    private async Task<JsonObject?> ReadCurrentTurnAfterlifeProgressionReportRootAsync()
    {
        var root = await ReadNodeAsync(ProgressionScheduleService.ReportPath) as JsonObject;
        if (root == null)
            return null;

        var report = root["progressionProcessingReport"] as JsonObject ?? root;
        var context = await ReadCurrentTurnRequestContextForAfterlifeEntityProgressionAsync();
        if (context == null || !ProgressionReportMatchesTurnContext(report, context.Value))
            return null;

        return root;
    }

    private async Task<PendingTurnContext?> ReadCurrentTurnRequestContextForAfterlifeEntityProgressionAsync()
    {
        foreach (var path in new[]
                 {
                     "input/turn_request.json",
                     "ready/turn_complete.json",
                     "game_state/control/validation_repair_request.json"
                 })
        {
            var root = await ReadNodeAsync(path) as JsonObject;
            if (root == null)
                continue;

            var sessionId = GetNodeString(root["sessionId"]);
            var requestId = GetNodeString(root["requestId"]);
            var turnNumber = GetNodeInt(root["turnNumber"]);
            if (!string.IsNullOrWhiteSpace(sessionId) &&
                !string.IsNullOrWhiteSpace(requestId) &&
                turnNumber > 0)
            {
                return new PendingTurnContext(sessionId, requestId, turnNumber);
            }
        }

        return null;
    }

    private static bool ProgressionReportMatchesTurnContext(JsonObject report, PendingTurnContext context)
    {
        var sessionId = GetNodeString(report["sessionId"]);
        var requestId = GetNodeString(report["requestId"]);
        var turnNumber = GetNodeInt(report["turnNumber"]);
        return turnNumber == context.TurnNumber &&
               !string.IsNullOrWhiteSpace(sessionId) &&
               string.Equals(sessionId, context.SessionId, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(requestId) &&
               string.Equals(requestId, context.RequestId, StringComparison.OrdinalIgnoreCase);
    }

    private readonly record struct PendingTurnContext(string SessionId, string RequestId, int TurnNumber);
}
