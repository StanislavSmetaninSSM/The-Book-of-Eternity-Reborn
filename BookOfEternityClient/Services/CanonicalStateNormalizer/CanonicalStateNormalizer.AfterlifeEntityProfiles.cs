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
        var progressionReportRoot = await ReadNodeAsync(ProgressionScheduleService.ReportPath) as JsonObject;
        var result = AfterlifeEntityProfileState.ProjectCanonicalRoot(currentRoot, previousRoot, progressionReportRoot);
        await WriteIfChangedAsync(AfterlifeEntityProfileState.StatePath, currentNode, result);
    }
}
