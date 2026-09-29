using System.Collections.Immutable;

namespace BookOfEternityClient.Services;

internal sealed partial class WoundHistoryState
{
    /// <summary>
    /// Reads the latest consumed ordinals belonging to the wound's exact current epochs.
    /// </summary>
    /// <param name="wound">
    /// The current accepted wound; a changed anchor identity starts a different consumption epoch.
    /// </param>
    /// <returns>
    /// Recovery and deterioration consumed ordinals, each zero when its exact epoch has no persisted evaluation.
    /// </returns>
    internal (long Recovery, long Deterioration) GetRecoveryConsumption(WoundMaterializationEnvelope wound) =>
        GetRecoveryConsumption(_transitions, wound);

    /// <summary>
    /// Finds consumed ordinals only for exact matching wound and epoch identities.
    /// </summary>
    /// <param name="rows">
    /// The complete ordered preceding accepted history rows.
    /// </param>
    /// <param name="wound">
    /// The accepted wound whose current epoch identities select prior consumption.
    /// </param>
    /// <returns>
    /// The most recent recovery and condition consumed ordinals, or zero for an unseen epoch.
    /// </returns>
    private static (long Recovery, long Deterioration) GetRecoveryConsumption(
        IEnumerable<WoundHistoryTransition> rows, WoundMaterializationEnvelope wound)
    {
        long recovery = 0;
        long deterioration = 0;
        foreach (var row in rows)
        {
            if (row.WoundId != wound.WoundId || row.TransitionResult is not MortalWoundRecoveryPersistedResult result)
                continue;
            var current = wound.Recovery.RecoveryAnchor;
            var saved = result.RecoveryConsumption;
            if (current is not null && saved.ConditionKey is null && saved.EpochKind == current.AnchorKind &&
                saved.AnchorMinute == current.AnchorMinute && saved.AnchorTransitionId == current.AnchorTransitionId)
                recovery = saved.ConsumedAfter;
            var condition = wound.Recovery.DeteriorationAnchor;
            var savedCondition = result.DeteriorationConsumption;
            if (condition is not null && savedCondition is not null && savedCondition.EpochKind == "condition" &&
                savedCondition.ConditionKey == condition.ConditionKey && savedCondition.AnchorMinute == condition.AnchorMinute &&
                savedCondition.AnchorTransitionId == condition.AnchorTransitionId)
                deterioration = savedCondition.ConsumedAfter;
        }
        return (recovery, deterioration);
    }

    /// <summary>
    /// Resolves an original same-minute receipt using the current last tick and verified durable history.
    /// </summary>
    /// <param name="wound">
    /// The accepted current wound, including its last tick key and final semantic state.
    /// </param>
    /// <param name="signedMinute">
    /// The minute retained by the current signed accepted-state authority, never a live-clock read.
    /// </param>
    /// <param name="receipt">
    /// Receives the original four-field receipt on exact replay, otherwise null.
    /// </param>
    /// <param name="issues">
    /// Receives invalid-history issues for an unresolved tick or a changed same-minute state
    /// without a matching accepted continuation in the complete history.
    /// </param>
    /// <returns>
    /// True only for a matching original same-minute evaluation; false permits fresh math only when issues is empty.
    /// </returns>
    internal bool TryResolveRecoveryReplay(WoundMaterializationEnvelope wound, long signedMinute,
        out MortalWoundRecoveryReceipt? receipt, out IReadOnlyList<ValidationIssue> issues)
    {
        receipt = null;
        issues = ImmutableArray<ValidationIssue>.Empty;
        if (wound.Recovery.LastTickKey is null)
            return false;
        var results = _transitions.Where(row => row.WoundId == wound.WoundId)
            .Select(row => row.TransitionResult).OfType<MortalWoundRecoveryPersistedResult>()
            .Where(result => result.Receipt.TickKey == wound.Recovery.LastTickKey).ToArray();
        if (results.Length != 1)
        {
            issues = RecoveryHistoryIssue("wound_history_recovery_tick_missing",
                "one durable result for the current lastTickKey", wound.Recovery.LastTickKey);
            return false;
        }
        var result = results[0];
        if (result.Resolution.CurrentTimeInMinutes != signedMinute)
            return false;
        if (result.Stages[^1].AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(wound))
        {
            var originalFinalStage = result.Stages[^1];
            // Parsing proves the complete per-wound fingerprint chain. Fresh math
            // may follow a later accepted transition, including genuine treatment
            // that retains LastTickKey while starting a new stabilization epoch.
            if (TryResolveExactTransition(originalFinalStage.TransitionId, out var originalFinalRow) &&
                originalFinalRow is not null &&
                originalFinalRow.WoundId == wound.WoundId &&
                originalFinalRow.AfterFingerprint == originalFinalStage.AfterFingerprint &&
                _byWoundId.TryGetValue(wound.WoundId, out var woundRows) &&
                woundRows[^1] is { } latest &&
                latest.WoundTransitionOrdinal > originalFinalRow.WoundTransitionOrdinal &&
                latest.AfterFingerprint == WoundIdentityState.ComputeSemanticFingerprint(wound) &&
                latest.TransitionId == wound.LastTransition.TransitionId &&
                latest.WoundTransitionOrdinal == wound.LastTransition.Ordinal &&
                latest.Turn == wound.LastTransition.Turn && latest.Kind == wound.LastTransition.Kind)
                return false;
            issues = RecoveryHistoryIssue("wound_history_recovery_replay_source_mismatch",
                "current wound matching the original evaluation's final stage", wound.WoundId);
            return false;
        }
        receipt = result.Receipt;
        return true;
    }

    /// <summary>
    /// Creates one closed invalid-history failure for durable recovery evidence.
    /// </summary>
    /// <param name="code">
    /// The stable invalid-history classification.
    /// </param>
    /// <param name="expected">
    /// The complete accepted evidence required at this boundary.
    /// </param>
    /// <param name="actual">
    /// The rejected coordinate or reason.
    /// </param>
    /// <returns>
    /// One immutable error issue without a replay receipt or publication authority.
    /// </returns>
    private static ImmutableArray<ValidationIssue> RecoveryHistoryIssue(string code, string expected, string actual) =>
        ImmutableArray.Create(new ValidationIssue(HistoryPath, IssueSeverity.Error,
            "The durable recovery result disagrees with complete canonical history.", code: code,
            actor: "Client", section: "wound_materialization", expected: expected, actual: actual,
            repairHint: "Restore accepted history and re-export the current signed wound state."));

    /// <summary>
    /// Checks every primary outer-row coordinate against its original durable recovery stage.
    /// </summary>
    /// <param name="row">
    /// The primary outer history row carrying the recovery result.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of that row.
    /// </param>
    /// <param name="issues">
    /// The collection receiving coordinate disagreement failures.
    /// </param>
    private static void ValidateRecoveryPrimaryRow(WoundHistoryTransition row, string path,
        ICollection<ValidationIssue> issues)
    {
        if (row.TransitionResult is not MortalWoundRecoveryPersistedResult result)
            return;
        var first = result.Stages[0];
        if (row.Kind != "recover" || row.WoundId != result.SourceWound.WoundId ||
            !MatchesRecoveryStage(row, first, result.Resolution.AuthorityFingerprint) ||
            row.Ordinal != result.Source.HistoryNextOrdinal || row.Terminal ||
            row.AttemptId is not null || row.CourseId is not null || row.CourseMilestoneOrdinal is not null ||
            row.PaymentFingerprint is not null)
            AddIssue(issues, path + ".transitionResult", "wound_history_recovery_coordinate_mismatch",
                "primary recover row matching every original accepted stage coordinate", row.TransitionId);
    }

    /// <summary>
    /// Compares all outer stage, tick, source and output coordinates with the original durable evidence.
    /// </summary>
    /// <param name="row">
    /// The exact outer row to compare.
    /// </param>
    /// <param name="stage">
    /// The detached original executed stage.
    /// </param>
    /// <param name="authorityFingerprint">
    /// The original reconstructed recovery resolution authority seal.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when every original coordinate agrees; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool MatchesRecoveryStage(WoundHistoryTransition row,
        MortalWoundRecoveryPublishedStage stage, string authorityFingerprint) =>
        row.TransitionId == stage.TransitionId && row.Kind == stage.Kind && row.Turn == stage.Turn &&
        row.EventRef == stage.EventRef && row.OperationKey == stage.OperationKey &&
        row.BeforeFingerprint == stage.BeforeFingerprint && row.AfterFingerprint == stage.AfterFingerprint &&
        row.Terminal == stage.Terminal && row.CycleKey == stage.AfterWound.Recovery.LastTickKey &&
        row.SourceFingerprint == authorityFingerprint && row.ReadableSummary == WoundAcceptedTurnPlanner.RecoveryPublicationSummary &&
        row.OutputFingerprint == ComputeOutputFingerprint(stage.OperationKey, stage.EventRef, row.ReadableSummary);

    /// <summary>
    /// Verifies complete original history prefixes, epoch consumption and linked recovery stage chains.
    /// </summary>
    /// <param name="transitions">
    /// The complete ordered parsed history, including every linked stage.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of the history root.
    /// </param>
    /// <param name="issues">
    /// The collection receiving prefix, epoch, receipt or linked-stage failures.
    /// </param>
    private static void ValidateRecoveryReplayAgreement(IEnumerable<WoundHistoryTransition> transitions,
        string path, ICollection<ValidationIssue> issues)
    {
        var rows = transitions.ToArray();
        var ticks = new HashSet<string>(StringComparer.Ordinal);
        var receipts = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < rows.Length; index++)
        {
            if (rows[index].TransitionResult is not MortalWoundRecoveryPersistedResult result)
                continue;
            var rowPath = path + $".transitions[{index}].transitionResult";
            if (!ticks.Add(result.Receipt.TickKey) || !receipts.Add(result.Receipt.ReceiptFingerprint))
                AddIssue(issues, rowPath, "wound_history_recovery_receipt_duplicate",
                    "one primary result and receipt per logical evaluation", result.Receipt.TickKey);
            var prefix = new WoundHistoryState(index + 1, rows.Take(index));
            var historySeal = WoundAcceptedTurnFingerprintWriter.Compute(new[]
            {
                "mortal_wound_treatment_history", "1", SerializeCanonical(prefix)
            });
            if (result.Source.HistoryNextOrdinal != index + 1 || result.Source.HistoryFingerprint != historySeal)
                AddIssue(issues, rowPath + ".source.historyFingerprint", "wound_history_recovery_prefix_mismatch",
                    "recomputed complete original history prefix", result.Source.HistoryFingerprint);
            var recoveryEpoch = result.SourceWound.Recovery.RecoveryAnchor!;
            var recoveryAllocation = rows.Take(index).SingleOrDefault(value => value.TransitionId == recoveryEpoch.AnchorTransitionId);
            if (recoveryAllocation is null || recoveryAllocation.WoundId != result.SourceWound.WoundId ||
                !IsRecoveryEpochAllocation(recoveryAllocation, recoveryEpoch))
                AddIssue(issues, rowPath + ".recoveryConsumption", "wound_history_recovery_epoch_mismatch",
                    "recovery epoch allocated by the exact original create or stabilization row", recoveryEpoch.AnchorTransitionId);
            if (result.SourceWound.Recovery.DeteriorationAnchor is { } conditionEpoch)
            {
                var allocation = rows.Take(index).SingleOrDefault(value => value.TransitionId == conditionEpoch.AnchorTransitionId);
                if (allocation is null || allocation.WoundId != result.SourceWound.WoundId || allocation.Kind is not ("create" or "worsen"))
                    AddIssue(issues, rowPath + ".deteriorationConsumption", "wound_history_recovery_epoch_mismatch",
                        "condition epoch allocated by the exact original create or condition reentry row", conditionEpoch.AnchorTransitionId);
            }
            var consumed = GetRecoveryConsumption(rows.Take(index), result.SourceWound);
            if (result.RecoveryConsumption.ConsumedBefore != consumed.Recovery ||
                (result.DeteriorationConsumption?.ConsumedBefore ?? 0) != consumed.Deterioration)
                AddIssue(issues, rowPath, "wound_history_recovery_consumption_mismatch",
                    "previous consumed ordinals from the same exact epochs", result.Receipt.TickKey);
            for (var stageIndex = 0; stageIndex < result.Stages.Count; stageIndex++)
            {
                var linkedIndex = index + stageIndex;
                if (linkedIndex >= rows.Length || rows[linkedIndex].WoundId != result.SourceWound.WoundId ||
                    !MatchesRecoveryStage(rows[linkedIndex], result.Stages[stageIndex], result.Resolution.AuthorityFingerprint) ||
                    (stageIndex > 0 && rows[linkedIndex].TransitionResult is not null) ||
                    rows[linkedIndex].AttemptId is not null || rows[linkedIndex].CourseId is not null ||
                    rows[linkedIndex].CourseMilestoneOrdinal is not null || rows[linkedIndex].PaymentFingerprint is not null)
                {
                    AddIssue(issues, rowPath + $".stages[{stageIndex}]", "wound_history_recovery_stage_mismatch",
                        "complete contiguous original stage chain with one primary result", result.Stages[stageIndex].TransitionId);
                }
            }
        }
    }

    /// <summary>
    /// Recognizes the existing creation and sealed stabilization publication paths
    /// without granting epoch allocation to an ordinary treatment row.
    /// </summary>
    /// <param name="transition">
    /// The exact same-wound history row selected by the original anchor transition ID.
    /// </param>
    /// <param name="epoch">
    /// The canonical epoch whose allocation kind must agree with accepted evidence.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for a creation row or an established stabilization
    /// publication; otherwise, <see langword="false"/>.
    /// </returns>
    private static bool IsRecoveryEpochAllocation(WoundHistoryTransition transition, WoundRecoveryAnchor epoch) =>
        epoch.AnchorKind switch
        {
            "creation" => transition.Kind == "create",
            "stabilization" => transition.Kind == "stabilize" ||
                transition.Kind == "treat" && transition.TreatmentResult is { } treatment &&
                treatment.Receipt.DeclaredResult.Any(static operation => operation is MortalWoundStabilizeOperation),
            _ => false
        };
}
