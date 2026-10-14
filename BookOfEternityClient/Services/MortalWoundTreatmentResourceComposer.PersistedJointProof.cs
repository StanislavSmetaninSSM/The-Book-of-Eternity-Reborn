namespace BookOfEternityClient.Services;

// Detached calculation for a strict durable history result. It restores no live
// request authority, confirmed agreement, resolution, reservation or publication.
internal static partial class MortalWoundTreatmentResourceComposer
{
    internal sealed record PersistedGuaranteedFinalizationProof(string Fingerprint,
        IReadOnlyList<MortalWoundTreatmentResourceConsumptionIntent> Consumptions);

    internal static PersistedGuaranteedFinalizationProof? RecomputePersistedGuaranteedFinalization(
        MortalWoundTreatmentPersistedResult result)
    {
        var request = result.Request;
        var receipt = result.Receipt;
        if (request.Mode != "guaranteed" || receipt.Mode != request.Mode || receipt.AttemptDisposition != "AcceptedTerminal" ||
            receipt.Interruption || receipt.SelectedOutcomeIndex != 0 || receipt.CourseId is not null ||
            receipt.CourseMilestoneOrdinal is not null || receipt.CourseDisposition is not null ||
            receipt.RequestFingerprint != request.RequestFingerprint ||
            receipt.ResourceAuthorityFingerprint != request.ResourceAuthority.AuthorityFingerprint ||
            !MortalWoundTreatmentDetachedSealValidator.IsValid(request)) return null;
        var resource = request.ResourceAuthority;
        var trigger = resource.Policy.ConsumeOn.Contains(receipt.ResultCategory, StringComparer.Ordinal)
            ? receipt.ResultCategory : "none";
        if (receipt.ConsumptionTrigger != trigger ||
            !TrySelectCurrentClaims("guaranteed", null, resource, trigger, out var selected, out _) || selected is null)
            return null;
        var byFingerprint = resource.Claims.ToDictionary(row => row.ClaimFingerprint, StringComparer.Ordinal);
        var consumptions = selected.Select(key => MortalWoundTreatmentResourceConsumptionIntent.Create(byFingerprint[key], null)).ToArray();
        var selectedSet = selected.ToHashSet(StringComparer.Ordinal);
        var released = resource.Claims.Where(row => !selectedSet.Contains(row.ClaimFingerprint))
            .Select(row => row.ClaimFingerprint).ToArray();
        string disposition;
        if (resource.ReservationDisposition == "not_required")
        {
            if (resource.ReservationId is not null || resource.Claims.Count != 0 || consumptions.Length != 0 || released.Length != 0) return null;
            disposition = "not_required";
        }
        else
        {
            if (resource.ReservationDisposition != "held" || string.IsNullOrWhiteSpace(resource.ReservationId) ||
                resource.Claims.Count == 0 || consumptions.Length + released.Length != resource.Claims.Count) return null;
            disposition = consumptions.Length == 0 ? "release_only" : "consume";
        }
        return new(MortalWoundTreatmentResourceFinalization.ComputePersistedFingerprint(disposition,
            resource, request.RequestFingerprint, receipt.ResultFingerprint, trigger, consumptions, released), consumptions);
    }
}

internal sealed partial class MortalWoundTreatmentResourceFinalization
{
    internal static string ComputePersistedFingerprint(string disposition,
        MortalWoundTreatmentResourceReservationAuthority resource, string requestFingerprint, string resultFingerprint,
        string trigger, IReadOnlyList<MortalWoundTreatmentResourceConsumptionIntent> consumptions,
        IReadOnlyList<string> released) =>
        ComputeFinalizationFingerprint(disposition, resource, requestFingerprint, resultFingerprint, trigger, consumptions, released);
}
