using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class MortalWoundOpportunityAdapter
{
    internal static bool TryReadPublishedJointReceiptPartition(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease, PendingTurnSnapshotReadAuthority snapshot,
        MortalQteDecisionPublication.JointHistorySuffixProof suffix, List<ValidationIssue> issues,
        out MortalWoundOpportunityReceiptState? currentReceipts)
    {
        fs.EnsureCanonicalWriteLeaseActive(lease);
        currentReceipts = null;
        if (!TryParseOccurrenceState(Encoding.UTF8.GetBytes(MortalQteWoundOpportunityProjection.ReadObservedRoot(snapshot,
                MortalWoundOccurrenceState.StatePath) ?? "{\"schemaVersion\":1,\"occurrences\":[]}"), issues, out var occurrences) ||
            !TryParseReceiptState(Encoding.UTF8.GetBytes(MortalQteWoundOpportunityProjection.ReadObservedRoot(snapshot,
                MortalWoundOpportunityReceiptState.StatePath) ?? EmptyReceiptJson), issues, out var receipts) ||
            !TryParseHistoryState(Encoding.UTF8.GetBytes(MortalQteWoundOpportunityProjection.ReadObservedRoot(snapshot,
                WoundHistoryState.HistoryPath) ?? EmptyHistoryJson), issues, out var history)) return false;
        issues.AddRange(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(occurrences!, receipts!));
        issues.AddRange(MortalWoundOpportunityReceiptState.ValidateHistoryAgreement(receipts!, history!));
        return issues.Count == 0 && TryReadCurrentPartition(fs, snapshot, occurrences!, receipts!, history!, issues,
            out currentReceipts, out _, suffix);
    }
}
