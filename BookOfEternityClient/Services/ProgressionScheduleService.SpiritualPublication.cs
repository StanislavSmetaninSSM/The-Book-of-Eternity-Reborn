using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
    internal static bool SpiritualPublishedScheduleAgrees(JsonObject afterRoot, string beforeJson,
        string reportJson, ProgressionControl control, string sessionId, string requestId, int turn, string realm)
    {
        var beforeRoot = JsonNode.Parse(beforeJson) as JsonObject;
        if (beforeRoot is null) return false;
        AfterlifeWoundTreatmentAuthority.MaterializeProperties(beforeRoot);
        var before = beforeRoot.Deserialize<ProgressionScheduleState>(JsonOpts);
        var after = afterRoot.Deserialize<ProgressionScheduleState>(JsonOpts);
        if (before is null || after is null ||
            !JsonNode.DeepEquals(beforeRoot, JsonSerializer.SerializeToNode(before, JsonOpts)) ||
            !JsonNode.DeepEquals(afterRoot, JsonSerializer.SerializeToNode(after, JsonOpts)) ||
            !TryBindPreparedSpiritualCycle(beforeRoot, null, null, control, realm, out var ordinal) ||
            after.CurrentRealm != before.CurrentRealm || !DateTimeOffset.TryParse(after.LastUpdatedUtc, out _))
            return false;
        var reportRoot = JsonNode.Parse(reportJson) as JsonObject;
        if (reportRoot is null) return false;
        AfterlifeWoundTreatmentAuthority.MaterializeProperties(reportRoot);
        var report = (reportRoot["progressionProcessingReport"] ?? reportRoot).Deserialize<ProgressionProcessingReport>(JsonOpts);
        var issues = new List<ValidationIssue>();
        var context = new PendingTurnRequestContext(sessionId, requestId, turn);
        ValidateAfterlifeOutcome(control, new(report, ProgressionFileReadState.Valid, true), context, issues);
        if (issues.Any(i => i.Severity == IssueSeverity.Error) || !HasVerifiedAfterlifeProgressionOutcome(control, report, context))
            return false;
        ApplyVerifiedAfterlifeProgression(before, control, report);
        before.PendingWorldCycles = 0; before.PendingFactionCycles = 0;
        ClearAfterlifePendingCycles(before);
        before.LastUpdatedUtc = after.LastUpdatedUtc;
        return before.CurrentChaosSeaTurnOrdinal == ordinal && JsonNode.DeepEquals(afterRoot, JsonSerializer.SerializeToNode(before, JsonOpts));
    }

    internal async Task<IReadOnlyList<ValidationIssue>> ValidateSpiritualTreatmentOutcomeAsync(
        FileSystemManager.CanonicalWriteLease lease, BookOfEternityClient.Models.ProgressionControl control)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var issues = new List<ValidationIssue>();
        ValidateAfterlifeOutcome(control, await ReadProcessingReportSnapshotAsync(lease),
            await ReadCurrentTurnRequestContextAsync(lease), issues);
        return issues;
    }

    internal void ClaimSpiritualTreatmentMutation(FileSystemManager.CanonicalWriteLease lease,
        AcceptedTurnOutcomeMutation mutation)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        if (!mutation.IsBoundTo(_fs, lease, _acceptedTurnMutationCapability) ||
            !mutation.TryConsume(_acceptedTurnMutationCapability))
            throw new InvalidDataException("The spiritual scheduler contribution is foreign, closed or already claimed.");
        foreach (var pair in new[] { (SchedulePath, mutation.ScheduleBefore), (ReportPath, mutation.ReportBefore) })
        {
            var actual = _fs.ReadFileBytesSync(lease, pair.Item1);
            if (new CanonicalBeforeImage(actual is not null, actual).Fingerprint != pair.Item2.Fingerprint)
                throw new InvalidDataException("The spiritual scheduler contribution changed: " + pair.Item1);
        }
    }
}
