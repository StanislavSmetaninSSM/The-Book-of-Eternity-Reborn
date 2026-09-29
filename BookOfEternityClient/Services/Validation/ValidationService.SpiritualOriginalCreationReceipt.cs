using System.Text;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>
        /// Requires signed creation receipts for retained original spiritual identities before offering any new decision.
        /// Healed identities retain the same creation obligation without an active carrier.
        /// Current-turn staged wounds are excluded because their receipts are still unpublished.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease for authenticating the receipt's original snapshot when needed.
        /// </param>
        /// <param name="signedReceipt">
        /// Already authenticated original receipt image, or <see langword="null"/> to read it under the lease.
        /// </param>
        /// <returns>
        /// Baseline or receipt integrity issues, or an empty list when every retained creation is covered.
        /// </returns>
        private IReadOnlyList<ValidationIssue> ValidateSignedOriginalCreationReceipts(
            FileSystemManager.CanonicalWriteLease lease, CanonicalBeforeImage? signedReceipt = null)
        {
            var issues = new List<ValidationIssue>();
            var baseline = ReadInitialWoundState(issues, applyInitialStage: false);
            if (baseline == null || issues.Count != 0)
                return issues;
            var catalog = WoundCarrierCatalog.Build(baseline.WoundCarriers!);
            var identity = WoundIdentityState.Parse(baseline.WoundIdentity!.ToJsonString(),
                WoundIdentityState.StatePath).State!;
            const string eventPrefix = "spiritual_wound_event_";
            var originalIdentities = identity.Entries.Where(entry =>
                entry.Domain == "spiritual" && entry.Realm is "chaos_sea" or "shining_abode" &&
                entry.CreatedEventRef.StartsWith(eventPrefix, StringComparison.Ordinal) &&
                entry.CreatedEventRef.Length == eventPrefix.Length + 64 &&
                entry.CreatedEventRef[eventPrefix.Length..].All(value => value is >= '0' and <= '9' or >= 'a' and <= 'f') &&
                (entry.Status == "healed" || catalog.TryResolveOne(entry.WoundId, out var occurrence) &&
                    occurrence.Wound.Origin.SourceKind is "spiritual_standard_art" or "spiritual_art"))
                .ToArray();
            if (originalIdentities.Length == 0)
                return issues;
            if (signedReceipt is null)
            {
                try { signedReceipt = ReadSignedConflictSideOriginCore(lease).Receipt; }
                catch (InvalidOperationException)
                {
                    issues.Add(SourceIssue(SpiritualWoundOpportunityReceiptState.StatePath,
                        "spiritual_wound_conflict_side_signed_origin_invalid",
                        "the exact signed original receipt image for retained spiritual creations"));
                    return issues;
                }
            }
            SpiritualWoundOpportunityReceiptState? receipts = null;
            if (signedReceipt.Existed)
            {
                string receiptJson;
                try { receiptJson = new UTF8Encoding(false, true).GetString(signedReceipt.Bytes!); }
                catch (DecoderFallbackException)
                {
                    issues.Add(SourceIssue(SpiritualWoundOpportunityReceiptState.StatePath,
                        "spiritual_wound_receipt_invalid_state", "a valid UTF-8 spiritual wound receipt history"));
                    return issues;
                }
                var parsed = SpiritualWoundOpportunityReceiptState.Parse(
                    receiptJson, SpiritualWoundOpportunityReceiptState.StatePath);
                issues.AddRange(parsed.Issues);
                if (parsed.State == null || issues.Count != 0)
                    return issues;
                receipts = parsed.State;
            }
            var history = WoundHistoryState.Parse(baseline.WoundHistory!.ToJsonString(),
                WoundHistoryState.HistoryPath).State!;
            foreach (var entry in originalIdentities)
            {
                var creation = history.Transitions.SingleOrDefault(transition =>
                    transition.WoundId == entry.WoundId && transition.WoundTransitionOrdinal == 1 &&
                    transition.Kind == "create" && transition.EventRef == entry.CreatedEventRef);
                var opportunityRef = catalog.TryResolveOne(entry.WoundId, out var occurrence)
                    ? occurrence.Wound.Origin.OpportunityId
                    : "spiritual_wound_" + entry.CreatedEventRef[eventPrefix.Length..];
                if (creation != null && receipts?.CoversOriginalCreation(entry.WoundId, opportunityRef, creation) == true)
                    continue;
                issues.Add(SourceIssue(SpiritualWoundOpportunityReceiptState.StatePath,
                    "spiritual_wound_conflict_side_receipt_missing",
                    "one signed non-retrauma creation decision matching the retained wound, transition and opportunity"));
                return issues;
            }
            return issues;
        }
    }
}
