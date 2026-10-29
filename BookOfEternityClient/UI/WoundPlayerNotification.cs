using System.Text.Encodings.Web;
using BookOfEternityClient.Services;
using Spectre.Console;

namespace BookOfEternityClient.UI;

internal sealed record WoundPlayerTextProjection(
    string PlainText,
    string ConsoleMarkup,
    string BrowserText);

internal sealed record WoundAcquisitionNarrationClaim(
    string LocalWoundRef,
    string EventRef,
    string Domain,
    string WoundName,
    int SeverityRank,
    string Text);

internal sealed record WoundAcquisitionOutputRequest(
    string LocalWoundRef,
    WoundMaterializationEnvelope Wound,
    WoundAcquisitionNarrationClaim Claim,
    string FinalSceneText);

internal sealed record WoundWorseningOutputRequest(
    string LocalWoundRef,
    string EventRef,
    WoundMaterializationEnvelope Wound,
    WoundAcquisitionNarrationClaim Claim,
    string FinalSceneText);

internal sealed record WoundAcquisitionOutputResult(
    WoundPlayerNotification? Notification,
    WoundPlayerTextProjection? AcquisitionNarration,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success =>
        Notification is not null &&
        AcquisitionNarration is not null &&
        Issues.Count == 0;
}

internal sealed class WoundAcceptedTurnOutputBindingResult
{
    private readonly WoundPlayerNotification[] _notifications;
    private readonly ValidationIssue[] _issues;

    internal WoundAcceptedTurnOutputBindingResult(
        IReadOnlyList<WoundPlayerNotification> notifications,
        IReadOnlyList<ValidationIssue> issues)
    {
        _notifications = notifications.Select(static value => value with
        {
            Text = value.Text with { }
        }).ToArray();
        _issues = issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray();
    }

    internal IReadOnlyList<WoundPlayerNotification> Notifications =>
        Array.AsReadOnly(_notifications.Select(static value => value with
        {
            Text = value.Text with { }
        }).ToArray());

    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray());

    internal bool Success => _issues.Length == 0;
}

internal sealed record WoundPlayerNotification(
    WoundPlayerTextProjection Text,
    string DetailCommand)
{
    private const string Command = "/раны";

    internal static WoundAcquisitionOutputResult Compose(
        WoundAcquisitionOutputRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Wound);
        return ComposeCore(
            request.LocalWoundRef,
            request.Wound,
            request.Claim,
            request.Wound.Origin.EventRef,
            request.FinalSceneText,
            worsening: false);
    }

    internal static WoundAcquisitionOutputResult ComposeWorsening(
        WoundWorseningOutputRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ComposeCore(
            request.LocalWoundRef,
            request.Wound,
            request.Claim,
            request.EventRef,
            request.FinalSceneText,
            worsening: true);
    }

    internal static WoundAcceptedTurnOutputBindingResult ComposeAcceptedTurn(
        AcceptedMechanicsWoundStageBundle bundle,
        string finalSceneText)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (bundle.SpiritualHealingContinuation is not null)
            return ComposeAcceptedSpiritualHealing(bundle, bundle.FinalMutations);

        var input = bundle.Input;
        var prepared = bundle.PreparedPlan;
        var final = bundle.FinalPlan;
        var transitions = input.Transitions;
        var batches = prepared.EffectOperationBatches;
        var mutations = final.CarrierContributions
            .SelectMany(static value => value.Mutations)
            .ToArray();

        if (prepared.JointRepairContinuationAuthority is not null)
            return ComposeAcceptedJointRepair(bundle, finalSceneText, mutations);

        if (prepared.TreatmentContinuationAuthority is not null)
        {
            return ComposeAcceptedTreatmentContinuation(
                bundle,
                mutations);
        }

        var issues = new List<ValidationIssue>();
        var notifications = new List<WoundPlayerNotification>(transitions.Count);
        var matchedWoundIds = new HashSet<string>(StringComparer.Ordinal);

        if (transitions.Count != batches.Count ||
            transitions.Count != mutations.Length)
        {
            AddAcceptedTurnBindingIssue(
                issues,
                "transition/mutation cardinality",
                $"{transitions.Count}/{batches.Count}/{mutations.Length}");
            return new WoundAcceptedTurnOutputBindingResult(
                Array.Empty<WoundPlayerNotification>(),
                issues);
        }

        for (var index = 0; index < transitions.Count; index++)
        {
            var transition = transitions[index];
            var batch = batches[index];
            var matches = mutations.Where(value => string.Equals(
                    value.WoundId,
                    batch.PreparedWoundId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 ||
                matches[0].AfterWound is not { } wound ||
                !matchedWoundIds.Add(wound.WoundId) ||
                !string.Equals(
                    transition.LocalWoundRef,
                    batch.LocalWoundRef,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    transition.Kind,
                    batch.TransitionAuthority.TransitionKind,
                    StringComparison.Ordinal) ||
                transition.Kind is not ("create" or "worsen") ||
                !string.Equals(
                    transition.Kind == "create" ? "add" : "update",
                    matches[0].Operation,
                    StringComparison.Ordinal))
            {
                AddAcceptedTurnBindingIssue(
                    issues,
                    $"transition[{index}]",
                    $"{transition.Kind}/{batch.PreparedWoundId}/matches={matches.Length}");
                continue;
            }

            var claim = new WoundAcquisitionNarrationClaim(
                transition.LocalWoundRef,
                batch.SourceExport.CausalEventRef,
                wound.Classification.Domain,
                wound.Display.Name,
                wound.Severity.Rank,
                wound.Display.AcquisitionNarration);
            var composed = string.Equals(
                    transition.Kind,
                    "worsen",
                    StringComparison.Ordinal)
                ? ComposeWorsening(new WoundWorseningOutputRequest(
                    transition.LocalWoundRef,
                    batch.SourceExport.CausalEventRef,
                    wound,
                    claim,
                    finalSceneText))
                : Compose(new WoundAcquisitionOutputRequest(
                    transition.LocalWoundRef,
                    wound,
                    claim,
                    finalSceneText));
            issues.AddRange(composed.Issues.Select(WoundAcceptedTurnData.CloneIssue));
            if (composed.Notification is { } notification)
                notifications.Add(notification);
        }

        return issues.Count == 0 && notifications.Count == transitions.Count
            ? new WoundAcceptedTurnOutputBindingResult(notifications, issues)
            : new WoundAcceptedTurnOutputBindingResult(
                Array.Empty<WoundPlayerNotification>(),
                issues.Count == 0
                    ? new[]
                    {
                        AcceptedTurnBindingIssue(
                            "notification cardinality",
                            $"{notifications.Count}/{transitions.Count}")
                    }
                    : issues);
    }

    /// <summary>
    /// Composes the completed spiritual insertions in their actual chronological order.
    /// Each insertion keeps its own wound image, including earlier ranks of a later worsened wound.
    /// </summary>
    /// <param name="completion">
    /// Immutable insertion evidence issued by the completed live owner; this method grants no publication authority.
    /// </param>
    /// <param name="finalSceneText">
    /// Final accepted scene containing the exact narration of every insertion.
    /// </param>
    /// <returns>
    /// Detached escaped notifications, or diagnostics with no notifications when any insertion cannot be bound.
    /// </returns>
    internal static WoundAcceptedTurnOutputBindingResult ComposeSpiritualAcceptedTurn(
        SpiritualLiveWoundCompletion completion, string finalSceneText)
    {
        ArgumentNullException.ThrowIfNull(completion);
        var notifications = new List<WoundPlayerNotification>();
        var issues = new List<ValidationIssue>();
        foreach (var insertion in completion.Insertions)
        {
            var input = insertion.Input;
            var prepared = insertion.Prepared;
            var wound = insertion.Wound;
            if (insertion.IsCombatHealing)
            {
                var stages = insertion.CombatStages;
                var history = WoundHistoryState.Parse(insertion.ReducedState.History.ToJsonString(), WoundHistoryState.HistoryPath);
                var result = history.State?.Transitions.Select(row => row.TransitionResult).OfType<SpiritualCombatHealingPersistedResult>()
                    .SingleOrDefault(value => value.Stages.Count != 0 && value.Stages[0].TransitionId == stages.FirstOrDefault()?.After.LastTransition.TransitionId);
                if (input.Transitions.Count != 0 || prepared.EffectOperationBatches.Count != 0 || stages.Count == 0 ||
                    !history.IsValid || result is null || result.Stages.Count != stages.Count ||
                    wound.Owner != new WoundOwnerCoordinate("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath) ||
                    WoundMaterializationContract.SerializeCanonical(stages[^1].After) != WoundMaterializationContract.SerializeCanonical(wound) ||
                    !stages.Select(stage => (stage.OperationKey, stage.EventRef, Before: WoundMaterializationContract.SerializeCanonical(stage.Before), After: WoundMaterializationContract.SerializeCanonical(stage.After)))
                        .SequenceEqual(result.Stages.Select(stage => (stage.OperationKey, stage.EventRef, Before: WoundMaterializationContract.SerializeCanonical(stage.Before), After: WoundMaterializationContract.SerializeCanonical(stage.After)))))
                {
                    AddAcceptedTurnBindingIssue(issues, "complete sealed combat treatment stages", insertion.SourceCoordinate);
                    continue;
                }
                if (!WoundPlayerProjection.Visible(wound.Display.Visibility)) continue;
                var text = wound.Lifecycle == "healed"
                    ? $"В бою исцелена духовная рана: {wound.Display.Name}. Подробнее: {Command}"
                    : $"Исцеление в бою: {wound.Display.Name} ({wound.Severity.Value}); восстановление {wound.Recovery.CurrentStepProgress}/{wound.Recovery.CurrentStepThreshold}. Подробнее: {Command}";
                notifications.Add(new(Project(text), Command));
                continue;
            }
            if (input.Transitions.Count != 1 || prepared.EffectOperationBatches.Count != 1)
            {
                AddAcceptedTurnBindingIssue(issues, "one transition and batch per live insertion", insertion.SourceCoordinate);
                continue;
            }
            var transition = input.Transitions[0];
            var batch = prepared.EffectOperationBatches[0];
            if (transition.Kind is not ("create" or "worsen") ||
                transition.Kind != batch.TransitionAuthority.TransitionKind ||
                transition.Kind != wound.LastTransition.Kind ||
                transition.LocalWoundRef != batch.LocalWoundRef ||
                wound.WoundId != batch.PreparedWoundId)
            {
                AddAcceptedTurnBindingIssue(issues, "exact ordered live transition", insertion.SourceCoordinate);
                continue;
            }
            var claim = new WoundAcquisitionNarrationClaim(transition.LocalWoundRef,
                batch.SourceExport.CausalEventRef, wound.Classification.Domain,
                wound.Display.Name, wound.Severity.Rank, wound.Display.AcquisitionNarration);
            var composed = transition.Kind == "worsen"
                ? ComposeWorsening(new WoundWorseningOutputRequest(transition.LocalWoundRef,
                    batch.SourceExport.CausalEventRef, wound, claim, finalSceneText))
                : Compose(new WoundAcquisitionOutputRequest(transition.LocalWoundRef, wound, claim, finalSceneText));
            issues.AddRange(composed.Issues);
            if (composed.Notification is { } notification)
                notifications.Add(notification);
        }
        return new WoundAcceptedTurnOutputBindingResult(
            issues.Count == 0 ? notifications : Array.Empty<WoundPlayerNotification>(), issues);
    }

    private static WoundAcceptedTurnOutputBindingResult ComposeAcceptedJointRepair(
        AcceptedMechanicsWoundStageBundle bundle, string scene, IReadOnlyList<WoundCarrierMutation> mutations)
    {
        // Authenticate the WHOLE joint final contribution before selecting its
        // one CREATE. A nullable treatment batch is not an acquisition batch.
        if (!WoundAcceptedTurnPlanner.JointRepairFinalPlanAgrees(bundle) ||
            !WoundAcceptedTurnPlanner.TryReadJointRepairFinal(bundle.PreparedPlan, out var joint))
            return new(Array.Empty<WoundPlayerNotification>(), [AcceptedTurnBindingIssue("joint final owner", "refused")]);
        var creates = bundle.Input.Transitions.Where(row => row.Kind == "create").ToArray();
        var batches = bundle.PreparedPlan.EffectOperationBatches.Where(row => row.PreparedWoundId != joint.Outcome.Before.WoundId).ToArray();
        var additions = mutations.Where(row => row.Operation == "add" && row.BeforeWound is null).ToArray();
        if (creates.Length != 1 || batches.Length != 1 || additions.Length != 1 || additions[0].AfterWound is not { } wound ||
            creates[0].LocalWoundRef != batches[0].LocalWoundRef || batches[0].PreparedWoundId != wound.WoundId ||
            batches[0].TransitionAuthority.TransitionKind != "create")
            return new(Array.Empty<WoundPlayerNotification>(), [AcceptedTurnBindingIssue("joint acquisition selection", "refused")]);
        var claim = new WoundAcquisitionNarrationClaim(creates[0].LocalWoundRef, batches[0].SourceExport.CausalEventRef,
            wound.Classification.Domain, wound.Display.Name, wound.Severity.Rank, wound.Display.AcquisitionNarration);
        var output = Compose(new WoundAcquisitionOutputRequest(creates[0].LocalWoundRef, wound, claim, scene));
        return output.Success && output.Notification is { } notification
            ? new([notification], output.Issues)
            : new(Array.Empty<WoundPlayerNotification>(), output.Issues);
    }

    internal static WoundAcceptedTurnOutputBindingResult
        ComposeAcceptedSpiritualHealing(AcceptedMechanicsWoundStageBundle bundle,
            IReadOnlyList<WoundCarrierMutation> mutations)
    {
        // Publication has already closed the owner. Read only its sealed immutable
        // execution DATA; this output binding grants no new mutation authority.
        if (bundle.SpiritualHealingContinuation is not { } owner ||
            !bundle.TryReadSpiritualHealingExecution(out var stages) ||
            stages.Count != owner.Stages.Count)
            return SpiritualHealingBindingFailure("missing complete sealed execution");

        var groups = stages.GroupBy(stage => stage.Before.WoundId, StringComparer.Ordinal).ToArray();
        if (groups.Length != mutations.Count)
            return SpiritualHealingBindingFailure("execution/mutation cardinality");
        var notifications = new List<WoundPlayerNotification>(groups.Length);
        foreach (var group in groups)
        {
            var before = group.First().Before;
            var after = group.Last().After;
            var matches = mutations.Where(mutation => mutation.WoundId == before.WoundId).ToArray();
            var terminal = after.Lifecycle == "healed";
            if (matches.Length != 1 || before.Classification.Domain != "spiritual" ||
                after.WoundId != before.WoundId || after.Owner != before.Owner ||
                after.Lifecycle is not ("active" or "healed") ||
                matches[0].Operation != (terminal ? "delete" : "update") ||
                matches[0].BeforeWound is not { } actualBefore ||
                WoundMaterializationContract.SerializeCanonical(actualBefore) != WoundMaterializationContract.SerializeCanonical(before) ||
                (terminal ? matches[0].AfterWound is not null : matches[0].AfterWound is not { } actualAfter ||
                    WoundMaterializationContract.SerializeCanonical(actualAfter) != WoundMaterializationContract.SerializeCanonical(after)))
                return SpiritualHealingBindingFailure("final carrier differs from chronological execution");

            // Only the explicitly selected known aid target gains a notification;
            // unrelated natural actor recovery remains outside this disclosure.
            if (after.Owner.OwnerKind != "player_soul" && !owner.IsVisibleAidTarget(after) ||
                !WoundPlayerProjection.Visible(after.Display.Visibility)) continue;
            var target = after.Owner.OwnerKind == "player_soul" ? "" : " Хранителя";
            var text = terminal
                ? $"Исцелена духовная рана{target}: {after.Display.Name}. Подробнее: {Command}"
                : $"Духовная рана{target}: {after.Display.Name} ({after.Severity.Value}); восстановление {after.Recovery.CurrentStepProgress}/{after.Recovery.CurrentStepThreshold}. Подробнее: {Command}";
            if (owner.ServiceWoundId == before.WoundId && owner.ServiceAmount is { } fee)
                text += $" Оплачена одна попытка лечения: {fee} Чернильных Перьев.";
            notifications.Add(new(Project(text), Command));
        }
        return new(notifications, Array.Empty<ValidationIssue>());
    }

    private static WoundAcceptedTurnOutputBindingResult SpiritualHealingBindingFailure(string actual) => new(
        Array.Empty<WoundPlayerNotification>(), new[] { AcceptedTurnBindingIssue("complete finalized spiritual healing DATA", actual) });

    private static WoundAcceptedTurnOutputBindingResult
        ComposeAcceptedTreatmentContinuation(
            AcceptedMechanicsWoundStageBundle bundle,
            IReadOnlyList<WoundCarrierMutation> mutations)
    {
        var input = bundle.Input;
        var prepared = bundle.PreparedPlan;
        if (!WoundAcceptedTurnPlanner.TryReadTreatmentContinuation(
                prepared.TreatmentContinuationAuthority,
                out var continuation) ||
            !WoundAcceptedTurnPlanner.TreatmentContinuationPreparedAgrees(prepared) ||
            input.Transitions.Count != 0 ||
            prepared.EffectOperationBatches.Count !=
                (continuation.OutcomePreparation.SeverityReduction is null ? 0 : 1) ||
            mutations.Count != 1)
        {
            return TreatmentContinuationBindingFailure(
                $"transitions={input.Transitions.Count}," +
                $"batches={prepared.EffectOperationBatches.Count}," +
                $"mutations={mutations.Count}");
        }

        var applicationByRef = new Dictionary<
            string,
            EffectAcceptedApplicationResult>(StringComparer.Ordinal);
        foreach (var application in bundle.EffectBatchPlan.ApplicationResults)
        {
            if (!applicationByRef.TryAdd(application.ApplicationRef, application))
            {
                return TreatmentContinuationBindingFailure(
                    "duplicate accepted applicationRef");
            }
        }
        var batch = continuation.OutcomePreparation.SeverityReduction is null
            ? null
            : prepared.EffectOperationBatches[0];
        var publication = MortalWoundTreatmentOutcomePublicationPlanner.Finalize(
            continuation.OutcomePreparation,
            continuation.Resolution,
            batch,
            applicationByRef);
        if (!publication.IsValid || publication.After is null)
        {
            return TreatmentContinuationBindingFailure(
                "invalid authenticated treatment finalization");
        }

        var mutation = mutations[0];
        if (mutation.BeforeWound is not { } before ||
            mutation.AfterWound is not { } after ||
            !string.Equals(mutation.Operation, "update", StringComparison.Ordinal) ||
            !string.Equals(
                mutation.WoundId,
                continuation.Before.WoundId,
                StringComparison.Ordinal) ||
            !string.Equals(
                mutation.WoundId,
                publication.After.WoundId,
                StringComparison.Ordinal) ||
            !string.Equals(
                WoundMaterializationContract.SerializeCanonical(before),
                WoundMaterializationContract.SerializeCanonical(
                    continuation.Before),
                StringComparison.Ordinal) ||
            !string.Equals(
                WoundMaterializationContract.SerializeCanonical(after),
                WoundMaterializationContract.SerializeCanonical(
                    publication.After),
                StringComparison.Ordinal))
        {
            return TreatmentContinuationBindingFailure(
                $"operation={mutation.Operation},woundId={mutation.WoundId}");
        }

        return new WoundAcceptedTurnOutputBindingResult(
            Array.Empty<WoundPlayerNotification>(),
            Array.Empty<ValidationIssue>());
    }

    private static WoundAcceptedTurnOutputBindingResult
        TreatmentContinuationBindingFailure(string actual) => new(
            Array.Empty<WoundPlayerNotification>(),
            new[]
            {
                AcceptedTurnBindingIssue(
                    "one exact sealed treatment update",
                    actual)
            });

    private static WoundAcquisitionOutputResult ComposeCore(
        string localWoundRef,
        WoundMaterializationEnvelope wound,
        WoundAcquisitionNarrationClaim claim,
        string expectedEventRef,
        string finalSceneText,
        bool worsening)
    {
        ArgumentNullException.ThrowIfNull(wound);
        ArgumentNullException.ThrowIfNull(claim);
        var issues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(claim.Text) ||
            string.IsNullOrWhiteSpace(finalSceneText) ||
            !finalSceneText.Contains(
                claim.Text,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "woundAcquisition.narration",
                "wound_acquisition_narration_missing",
                "the exact non-empty acquisition narration inside the final scene",
                string.IsNullOrWhiteSpace(claim.Text)
                    ? "empty claim"
                    : "claim absent from final scene");
            return new WoundAcquisitionOutputResult(null, null, issues);
        }

        var contradictions = new List<string>();
        if (!string.Equals(
                localWoundRef,
                claim.LocalWoundRef,
                StringComparison.Ordinal))
        {
            contradictions.Add("wound_ref");
        }
        if (!string.Equals(
                expectedEventRef,
                claim.EventRef,
                StringComparison.Ordinal))
        {
            contradictions.Add("event_ref");
        }
        if (!string.Equals(
                wound.Classification.Domain,
                claim.Domain,
                StringComparison.Ordinal))
        {
            contradictions.Add("domain");
        }
        if (!string.Equals(
                wound.Display.Name,
                claim.WoundName,
                StringComparison.Ordinal))
        {
            contradictions.Add("name");
        }
        if (wound.Severity.Rank != claim.SeverityRank)
            contradictions.Add("severity");
        if (!string.Equals(
                wound.Display.AcquisitionNarration,
                claim.Text,
                StringComparison.Ordinal))
        {
            contradictions.Add("text");
        }
        if (contradictions.Count != 0)
        {
            Add(
                issues,
                "woundAcquisition.narration",
                "wound_acquisition_narration_contradiction",
                "exact local wound, event, domain, name, severity, and narration",
                string.Join(",", contradictions));
            return new WoundAcquisitionOutputResult(null, null, issues);
        }

        var spiritual = string.Equals(
            wound.Classification.Domain,
            "spiritual",
            StringComparison.Ordinal);
        var prefix = worsening
            ? spiritual
                ? "Духовная рана ухудшилась"
                : "Рана ухудшилась"
            : spiritual
                ? "Получена духовная рана"
                : "Получена рана";
        var plain =
            $"{prefix}: {wound.Display.Name} ({wound.Severity.Value}). Подробнее: {Command}";
        return new WoundAcquisitionOutputResult(
            new WoundPlayerNotification(Project(plain), Command),
            Project(claim.Text),
            Array.Empty<ValidationIssue>());
    }

    private static WoundPlayerTextProjection Project(string value) => new(
        value,
        Markup.Escape(value),
        HtmlEncoder.Default.Encode(value));

    private static void AddAcceptedTurnBindingIssue(
        ICollection<ValidationIssue> issues,
        string expected,
        string actual) => issues.Add(AcceptedTurnBindingIssue(expected, actual));

    private static ValidationIssue AcceptedTurnBindingIssue(
        string expected,
        string actual) => new(
        "acceptedWoundOutput",
        IssueSeverity.Error,
        "Accepted wound output no longer agrees with its finalized typed transition.",
        code: "wound_accepted_output_binding_invalid",
        section: "wound_acquisition_output",
        expected: expected,
        actual: actual,
        repairHint:
            "Rebuild the accepted wound output from the exact finalized wound plan.");

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) => issues.Add(new ValidationIssue(
        path,
        IssueSeverity.Error,
        "Wound acquisition output does not match the accepted wound.",
        code: code,
        section: "wound_acquisition_output",
        expected: expected,
        actual: actual,
        repairHint:
            "Keep the accepted wound unchanged and correct only its acquisition narration."));
}
