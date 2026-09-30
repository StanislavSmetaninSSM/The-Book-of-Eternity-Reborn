using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>
    /// Admits original raw location and item owners from one retained current draft under physical authority.
    /// </summary>
    /// <param name="lease">
    /// Active real canonical lease for signing and cache ownership checks.
    /// </param>
    /// <param name="currentInputs">
    /// Detached original current paths and byte images, including registered absence.
    /// </param>
    /// <param name="allocations">
    /// Private location and item factories sharing this original allocation journal.
    /// </param>
    /// <param name="allocationScope">
    /// Still-open combined scope belonging to <paramref name="allocations"/>.
    /// </param>
    /// <returns>
    /// Actual owner-admission issues without attaching or committing a capture.
    /// </returns>
    private async Task<IReadOnlyList<ValidationIssue>> ValidateSpiritualOriginalLocationItemIntakeAsync(
        FileSystemManager.CanonicalWriteLease lease,
        SpiritualOriginalDraftInputs currentInputs,
        SpiritualOriginalAllocationOwner allocations,
        SpiritualWoundProjectionClock.Speculation allocationScope)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(currentInputs);
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(allocationScope);
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        if (allocations.Items is not { IsHealthy: true } itemFactory ||
            allocations.Locations is not { IsHealthy: true } ||
            !allocations.Journal.IsHealthy)
            throw new InvalidOperationException("Original intake requires healthy private item and location owners.");
        allocationScope.EnsureActiveFor(allocations.Clock);
        if (_prevalidatedPendingTurnSnapshotOverride != null ||
            !AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                _fs, lease, itemFactory, checkPlansAndItems: true))
            return [SourceIssue(SpiritualWoundSourceSession.SoulPath,
                "spiritual_original_intake_claim_conflict",
                "physical snapshot and vacant unrelated accepted-turn claims")];

        var signed = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
            [SpiritualWoundSourceSession.SoulPath]);
        if (!signed.Success || signed.Snapshot is not { } snapshot)
            return signed.Issues;
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync(lease);
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest is not { } manifest ||
            !currentInputs.MatchesIdentity(snapshot.SessionId, snapshot.RequestId,
                snapshot.SnapshotToken, snapshot.TurnNumber) ||
            !currentInputs.MatchesIdentity(manifest.SessionId, manifest.RequestId,
                manifest.ManifestPayloadHash, manifest.TurnNumber))
            return [SourceIssue(SpiritualWoundSourceSession.SoulPath,
                "spiritual_original_input_identity_mismatch",
                "the exact physical signed session, request, snapshot and positive turn")];

        foreach (var path in SpiritualOriginalTurnCapture.FixedOriginalItemLocationCurrentPaths)
            _ = currentInputs.ReadImage(path);

        var accepted = false;
        try
        {
            var issues = new List<ValidationIssue>();
            issues.AddRange(await ValidateAcceptedTurnRawMortalLocationMaterializationCoreAsync(
                lease, allocations.Locations, currentInputs));
            if (!issues.Any(issue => issue.Severity == IssueSeverity.Error))
                await ValidateAcceptedTurnRawMortalItemMaterializationAsync(
                    issues, lease, allocations, currentInputs);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return issues;

            _fs.EnsureCanonicalWriteLeaseActive(lease);
            allocations.Journal.EnsureUsable();
            allocationScope.EnsureActiveFor(allocations.Clock);
            if (!AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                    _fs, lease, itemFactory, checkPlansAndItems: true))
                return [SourceIssue(SpiritualWoundSourceSession.SoulPath,
                    "spiritual_original_intake_claim_conflict",
                    "unchanged physical treatment and exact private item authority")];
            accepted = true;
            return issues;
        }
        finally
        {
            if (!accepted)
                MortalItemAcceptedTurnAuthority.InvalidateValidatedItems(_fs, lease, itemFactory);
        }
    }
}
