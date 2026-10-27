using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ProgressionScheduleService
{
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
