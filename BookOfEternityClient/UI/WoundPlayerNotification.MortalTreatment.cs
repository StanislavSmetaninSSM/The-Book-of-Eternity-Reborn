using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal sealed partial record WoundPlayerNotification
{
    /// <summary>
    /// Binds terminal output to the whole actual publication before applying
    /// selected-wound visibility. Closed DATA requires no current owner or read.
    /// </summary>
    private static WoundAcceptedTurnOutputBindingResult ComposeAcceptedTerminalTreatment(AcceptedMechanicsWoundStageBundle bundle,
        WoundAcceptedTurnPlanner.TreatmentContinuationView continuation, WoundCarrierMutation mutation)
    {
        if (!WoundAcceptedTurnPlanner.MortalProcedureFinalPlanAgrees(bundle.PreparedPlan.TreatmentContinuationAuthority!, bundle) ||
            !EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(bundle.PreparedPlan, bundle.EffectBatchPlan.EffectPlan, out var stages) ||
            stages.Count != 2 || mutation.Operation != "delete" || mutation.AfterWound is not null ||
            mutation.WoundId != continuation.Before.WoundId || mutation.BeforeWound is not { } before ||
            Canonical(before) != Canonical(continuation.Before) || Canonical(stages[0].Before) != Canonical(before) ||
            stages[1].After.Lifecycle != "healed")
            return TreatmentContinuationBindingFailure("terminal deletion or complete actual executed chain differs");
        if (!WoundPlayerProjection.Visible(before.Display.Visibility))
            return new(Array.Empty<WoundPlayerNotification>(), Array.Empty<ValidationIssue>());
        var notifications = new List<WoundPlayerNotification>
        { new(Project($"Исцелена рана: {before.Display.Name}. Подробнее: {Command}"), Command) };
        foreach (var declaration in continuation.LegacyPreparation!.Declarations)
            notifications.Add(new(Project($"Последствие лечения: {declaration.ReadableSummary}. Подробнее: {Command}"), Command));
        return new(notifications, Array.Empty<ValidationIssue>());
    }
}
