namespace BookOfEternityClient.Services;

/// <summary>
/// Reconstructs Mortal wound opportunity authority for accepted-turn validation from
/// signed occurrence, receipt, and carrier before-images. The submitted command is
/// comparison input only; none of its authority fields are used as evidence.
/// </summary>
internal static class MortalWoundOpportunityValidationAuthority
{
    internal static MortalWoundOpportunityValidationResult Reconstruct(
        string sessionId,
        string requestId,
        string snapshotToken,
        string realm,
        int turn,
        MortalWoundOccurrenceState signedOccurrences,
        MortalWoundOpportunityReceiptState signedReceipts,
        WoundCarrierCatalogInput preTurnCarriers,
        IReadOnlyList<WoundOpportunityAuthority> submittedOpportunities)
    {
        ArgumentNullException.ThrowIfNull(signedOccurrences);
        ArgumentNullException.ThrowIfNull(signedReceipts);
        ArgumentNullException.ThrowIfNull(preTurnCarriers);
        ArgumentNullException.ThrowIfNull(submittedOpportunities);

        var issues = new List<ValidationIssue>();
        if (!ResourceMaterializationContract.IsExactIdentifier(sessionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(requestId) ||
            !ResourceMaterializationContract.IsExactIdentifier(snapshotToken) ||
            !string.Equals(realm, "mortal_world", StringComparison.Ordinal) ||
            turn <= 0)
        {
            Add(
                issues,
                AcceptedMechanicsPlan.WoundCommandPath,
                "mortal_wound_validation_binding_invalid",
                "one exact active Mortal-world snapshot binding",
                "the validation binding is malformed");
        }

        if (submittedOpportunities.Count == 0 &&
            signedOccurrences.Occurrences.Count != 0)
        {
            Add(
                issues,
                AcceptedMechanicsPlan.WoundCommandPath + ".commands",
                "mortal_wound_validation_opportunity_missing",
                "one or more submitted wound opportunities",
                "the command batch is empty");
        }

        issues.AddRange(
            MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
                signedOccurrences,
                signedReceipts));

        var carrierCatalog = WoundCarrierCatalog.Build(preTurnCarriers);
        issues.AddRange(carrierCatalog.Issues);
        if (issues.Count != 0)
            return Failure(issues);

        WoundAcceptedTurnBinding? binding = submittedOpportunities.Count == 0
            ? new WoundAcceptedTurnBinding(
                sessionId,
                requestId,
                snapshotToken,
                realm,
                turn,
                Array.Empty<WoundAcceptedEventAuthority>(),
                WoundAcceptedEventSetFingerprint.Compute(
                    Array.Empty<WoundAcceptedEventAuthority>()))
            : null;
        var expectedOpportunities = new List<WoundOpportunityAuthority>(
            submittedOpportunities.Count);
        var submittedIds = new HashSet<string>(StringComparer.Ordinal);
        var submittedAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < submittedOpportunities.Count; index++)
        {
            var submitted = submittedOpportunities[index];
            var path = $"{AcceptedMechanicsPlan.WoundCommandPath}.commands[{index}].opportunity";
            if (submitted is null ||
                !submittedIds.Add(submitted.OpportunityId) ||
                !submittedAliases.Add(
                    MortalLocationIdentityState.BuildConfusableKey(
                        submitted.OpportunityId)))
            {
                Add(
                    issues,
                    path,
                    "mortal_wound_validation_opportunity_ambiguous",
                    "one exact/confusable-unique signed occurrence per command",
                    "the submitted opportunity is null or duplicated");
                continue;
            }

            var matches = signedOccurrences.Occurrences
                .Where(value => string.Equals(
                    value.OccurrenceId,
                    submitted.OpportunityId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                Add(
                    issues,
                    path,
                    "mortal_wound_validation_occurrence_unresolved",
                    "one exact signed pending Mortal-wound occurrence",
                    $"matches={matches.Length}");
                continue;
            }

            var occurrence = matches[0];
            var producerBatch = signedOccurrences.Occurrences
                .Where(value => string.Equals(
                    value.ProducerOperationKey,
                    occurrence.ProducerOperationKey,
                    StringComparison.Ordinal))
                .ToArray();
            var consumedProducerBatch = signedReceipts.Receipts
                .Where(value => string.Equals(
                    value.ProducerOperationKey,
                    occurrence.ProducerOperationKey,
                    StringComparison.Ordinal))
                .ToArray();
            var rebound = WoundAcceptedEventAuthorityComposer
                .RebindMortalOccurrence(
                    occurrence,
                    producerBatch,
                    consumedProducerBatch,
                    sessionId,
                    requestId,
                    snapshotToken,
                    turn);
            if (!rebound.Success)
            {
                issues.AddRange(rebound.Issues);
                continue;
            }

            var reconstructedBinding = new WoundAcceptedTurnBinding(
                sessionId,
                requestId,
                snapshotToken,
                realm,
                turn,
                rebound.Events,
                rebound.EventsFingerprint);
            if (binding is null)
            {
                binding = reconstructedBinding;
            }
            else if (!SameBindingEventAuthority(binding, reconstructedBinding))
            {
                Add(
                    issues,
                    path,
                    "mortal_wound_validation_event_set_mismatch",
                    "one identical complete ordered event set for the command batch",
                    "the signed occurrences reconstruct different active event sets");
                continue;
            }

            WoundOpportunityWorseningTargetEvidence? worseningEvidence = null;
            if (occurrence.WorseningTarget is not null &&
                !TryResolveWorseningTarget(
                    carrierCatalog,
                    occurrence,
                    path,
                    issues,
                    out worseningEvidence))
            {
                continue;
            }

            var selectedEvent = rebound.Events[occurrence.AcceptedEventOrdinal];
            var composed = WoundOpportunityAuthority.Compose(
                new WoundOpportunityBuildRequest(
                    reconstructedBinding,
                    occurrence.OccurrenceId,
                    occurrence.OpportunityRef,
                    selectedEvent.EventRef,
                    occurrence.Owner with { },
                    occurrence.Domain,
                    occurrence.ProfileKey,
                    occurrence.Source.Kind,
                    occurrence.Source.SourceId,
                    occurrence.Source.State,
                    new WoundOpportunityEventEvidence(
                        occurrence.AdapterKind,
                        selectedEvent.Kind,
                        selectedEvent.AuthorityId,
                        occurrence.Outcome.Kind,
                        occurrence.Outcome.MaximumSeverityRank,
                        occurrence.Outcome.ReadableCause),
                    occurrence.HardMaximumSeverityRank,
                    ProjectGuarantee(occurrence.GuaranteedTrigger),
                    WoundOpportunityAuthority.CloneSafeContext(
                        occurrence.SafeContext),
                    worseningEvidence));
            if (!composed.Success || composed.Opportunity is null)
            {
                issues.AddRange(composed.Issues);
                continue;
            }

            if (!SameOpportunityAuthority(
                    composed.Opportunity,
                    submitted))
            {
                Add(
                    issues,
                    path,
                    "mortal_wound_validation_opportunity_authority_mismatch",
                    "the exact opportunity independently derived from signed occurrence authority",
                    "the submitted self-consistent authority differs from the signed occurrence");
                continue;
            }

            expectedOpportunities.Add(composed.Opportunity);
        }

        if (issues.Count != 0 ||
            binding is null ||
            expectedOpportunities.Count != submittedOpportunities.Count)
        {
            return Failure(issues);
        }

        var priorReceipts = signedReceipts.Receipts
            .Select(value => new WoundOpportunityDecisionReceipt(
                value.OpportunityId,
                value.DecisionFingerprint,
                value.OperationKey))
            .ToArray();
        return new MortalWoundOpportunityValidationResult(
            binding,
            expectedOpportunities,
            priorReceipts,
            Array.Empty<ValidationIssue>());
    }

    private static bool TryResolveWorseningTarget(
        WoundCarrierCatalog catalog,
        MortalWoundOccurrence occurrence,
        string path,
        ICollection<ValidationIssue> issues,
        out WoundOpportunityWorseningTargetEvidence? evidence)
    {
        evidence = null;
        var target = occurrence.WorseningTarget!;
        if (catalog.CountExactOccurrences(target.WoundId) != 1 ||
            !catalog.TryResolveOne(target.WoundId, out var resolved) ||
            !string.Equals(
                resolved.FilePath,
                occurrence.Owner.CarrierPath,
                StringComparison.Ordinal) ||
            !string.Equals(
                resolved.Coordinate.Realm,
                occurrence.Owner.Realm,
                StringComparison.Ordinal) ||
            !string.Equals(
                resolved.Coordinate.OwnerKind,
                occurrence.Owner.OwnerKind,
                StringComparison.Ordinal) ||
            !string.Equals(
                resolved.Coordinate.OwnerId,
                occurrence.Owner.OwnerId,
                StringComparison.Ordinal) ||
            !string.Equals(
                resolved.Coordinate.CarrierPath,
                occurrence.Owner.CarrierPath,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                path + ".worseningTarget",
                "mortal_wound_validation_worsening_target_unresolved",
                "one exact active wound in the signed owner carrier before-image",
                target.WoundId);
            return false;
        }

        evidence = new WoundOpportunityWorseningTargetEvidence(
            resolved.Wound,
            target.CauseKind);
        return true;
    }

    private static WoundGuaranteedTriggerEvidence? ProjectGuarantee(
        WoundGuaranteedTriggerAuthority? value) => value is null
        ? null
        : new WoundGuaranteedTriggerEvidence(
            value.TriggerId,
            value.SourceKind,
            value.SourceId,
            value.SourceState,
            value.Realm,
            value.Domain,
            value.Owner with { },
            value.RequiredSeverityRank,
            value.MaterializedAtTurn,
            value.SourceContractFingerprint);

    private static bool SameBindingEventAuthority(
        WoundAcceptedTurnBinding left,
        WoundAcceptedTurnBinding right) =>
        string.Equals(
            left.AcceptedEventsFingerprint,
            right.AcceptedEventsFingerprint,
            StringComparison.Ordinal) &&
        left.AcceptedEvents.SequenceEqual(right.AcceptedEvents);

    private static bool SameOpportunityAuthority(
        WoundOpportunityAuthority expected,
        WoundOpportunityAuthority submitted) =>
        string.Equals(expected.SessionId, submitted.SessionId, StringComparison.Ordinal) &&
        string.Equals(expected.RequestId, submitted.RequestId, StringComparison.Ordinal) &&
        string.Equals(expected.SnapshotToken, submitted.SnapshotToken, StringComparison.Ordinal) &&
        string.Equals(expected.OpportunityId, submitted.OpportunityId, StringComparison.Ordinal) &&
        string.Equals(expected.PublicRef, submitted.PublicRef, StringComparison.Ordinal) &&
        string.Equals(expected.EventRef, submitted.EventRef, StringComparison.Ordinal) &&
        string.Equals(expected.EventKind, submitted.EventKind, StringComparison.Ordinal) &&
        string.Equals(expected.EventAuthorityId, submitted.EventAuthorityId, StringComparison.Ordinal) &&
        string.Equals(expected.AcceptedEventsFingerprint, submitted.AcceptedEventsFingerprint, StringComparison.Ordinal) &&
        expected.Owner == submitted.Owner &&
        string.Equals(expected.Domain, submitted.Domain, StringComparison.Ordinal) &&
        string.Equals(expected.ProfileKey, submitted.ProfileKey, StringComparison.Ordinal) &&
        string.Equals(expected.SourceKind, submitted.SourceKind, StringComparison.Ordinal) &&
        string.Equals(expected.SourceId, submitted.SourceId, StringComparison.Ordinal) &&
        string.Equals(expected.SourceState, submitted.SourceState, StringComparison.Ordinal) &&
        expected.MinimumSeverityRank == submitted.MinimumSeverityRank &&
        expected.MaximumSeverityRank == submitted.MaximumSeverityRank &&
        Equals(expected.GuaranteedTrigger, submitted.GuaranteedTrigger) &&
        expected.SafeContext.Equals(submitted.SafeContext) &&
        SameWorseningTarget(expected.WorseningTarget, submitted.WorseningTarget) &&
        string.Equals(expected.InputEvidenceFingerprint, submitted.InputEvidenceFingerprint, StringComparison.Ordinal) &&
        string.Equals(expected.AuthorityFingerprint, submitted.AuthorityFingerprint, StringComparison.Ordinal);

    private static bool SameWorseningTarget(
        WoundOpportunityWorseningTargetAuthority? expected,
        WoundOpportunityWorseningTargetAuthority? submitted)
    {
        if (expected is null || submitted is null)
            return expected is null && submitted is null;
        return string.Equals(
                   expected.CauseKind,
                   submitted.CauseKind,
                   StringComparison.Ordinal) &&
               string.Equals(
                   expected.ExpectedBeforeFingerprint,
                   submitted.ExpectedBeforeFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   WoundMaterializationContract.SerializeCanonical(expected.Wound),
                   WoundMaterializationContract.SerializeCanonical(submitted.Wound),
                   StringComparison.Ordinal);
    }

    private static MortalWoundOpportunityValidationResult Failure(
        IEnumerable<ValidationIssue> issues)
    {
        var values = issues.ToArray();
        if (values.Length == 0)
        {
            values = new[]
            {
                new ValidationIssue(
                    AcceptedMechanicsPlan.WoundCommandPath,
                    IssueSeverity.Error,
                    "Mortal wound validation authority failed closed.",
                    "mortal_wound_validation_failed")
            };
        }
        return new MortalWoundOpportunityValidationResult(
            null,
            Array.Empty<WoundOpportunityAuthority>(),
            Array.Empty<WoundOpportunityDecisionReceipt>(),
            values);
    }

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal wound command does not match signed occurrence authority.",
            code,
            actor: "Client",
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
            "Restore the signed pending-turn roots and rebuild the wound command from the accepted source adapter."));
}

internal sealed class MortalWoundOpportunityValidationResult
{
    private readonly WoundAcceptedTurnBinding? _binding;
    private readonly WoundOpportunityAuthority[] _opportunities;
    private readonly WoundOpportunityDecisionReceipt[] _priorReceipts;
    private readonly ValidationIssue[] _issues;

    internal MortalWoundOpportunityValidationResult(
        WoundAcceptedTurnBinding? binding,
        IReadOnlyList<WoundOpportunityAuthority> opportunities,
        IReadOnlyList<WoundOpportunityDecisionReceipt> priorReceipts,
        IReadOnlyList<ValidationIssue> issues)
    {
        _binding = WoundAcceptedTurnData.CloneBinding(binding);
        _opportunities = opportunities
            .Select(WoundAcceptedTurnData.CloneOpportunity)
            .ToArray();
        _priorReceipts = priorReceipts.Select(value => value with { }).ToArray();
        _issues = issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray();
    }

    internal bool Success =>
        _binding is not null &&
        _issues.Length == 0;

    internal WoundAcceptedTurnBinding? Binding =>
        WoundAcceptedTurnData.CloneBinding(_binding);

    internal IReadOnlyList<WoundOpportunityAuthority> Opportunities =>
        Array.AsReadOnly(_opportunities
            .Select(WoundAcceptedTurnData.CloneOpportunity)
            .ToArray());

    internal IReadOnlyList<WoundOpportunityDecisionReceipt> PriorReceipts =>
        Array.AsReadOnly(_priorReceipts.Select(value => value with { }).ToArray());

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues
            .Select(WoundAcceptedTurnData.CloneIssue)
            .ToArray());
}
