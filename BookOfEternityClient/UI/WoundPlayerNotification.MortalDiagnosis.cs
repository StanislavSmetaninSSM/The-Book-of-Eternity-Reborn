using System.Text.Json;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

internal sealed partial record WoundPlayerNotification
{
    private static WoundAcceptedTurnOutputBindingResult ComposeAcceptedMortalDiagnosis(
        AcceptedMechanicsWoundStageBundle bundle, WoundAcceptedTurnInput input,
        WoundPreparedAcceptedTurnPlan prepared, WoundAcceptedTurnPlan final, IReadOnlyList<WoundCarrierMutation> mutations)
    {
        try
        {
            var owner = prepared.MortalDiagnosisAuthority;
            // Publication may already have closed the live owner. Authenticate
            // only its complete sealed immutable final/execution DATA here.
            var effectBatch = bundle.EffectBatchPlan;
            if (owner is null || input.Transitions.Count != 0 || prepared.EffectOperationBatches.Count != 0 ||
                !WoundAcceptedTurnPlanner.MortalProcedureFinalPlanAgrees(owner, prepared, effectBatch, final))
                return DiagnosisBindingFailure("missing exact sealed diagnosis final plan");
            var effects = effectBatch.EffectPlan;
            if (!EffectAcceptedTurnPlanner.TryReadMortalDiagnosisExecution(prepared, effects, out var execution))
                return DiagnosisBindingFailure("missing exact sealed diagnosis execution");
            var result = EffectAcceptedTurnPlanner.ReadMortalDiagnosisExecutedResult(prepared, effects);
            var original = result.Original;
            var knowledge = result.KnowledgeAfter;
            var members = result.Recoveries;
            var request = result.Request;
            if (request.Mode != "diagnosis" || request.Coordinates.Purpose != "diagnosis" ||
                result.Resolution.ResultCategory is not ("success" or "failed_attempt") || members.Length == 0 ||
                WoundAcceptedTurnFingerprints.ComputeBinding(result.Binding) != WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding) ||
                members.Select(member => member.Original.WoundId).Distinct(StringComparer.Ordinal).Count() != members.Length ||
                !members.Select(member => member.Original.WoundId).SequenceEqual(prepared.AllocatedWoundIds, StringComparer.Ordinal) ||
                members.Count(member => member.Original.WoundId == request.Coordinates.WoundId) != 1 ||
                original.WoundId != request.Coordinates.WoundId || knowledge.WoundId != original.WoundId ||
                execution[0].Kind != "diagnose" || execution[0].OperationKey != request.Coordinates.OperationKey ||
                Canonical(execution[0].Before) != Canonical(original) || Canonical(execution[0].After) != Canonical(knowledge))
                return DiagnosisBindingFailure("diagnosis result differs from its exact selected boundary and complete cohort");

            var recoveryStages = members.SelectMany(member => member.Stages).ToArray();
            if (execution.Count != recoveryStages.Length + 1)
                return DiagnosisBindingFailure("diagnosis/recovery execution cardinality");
            for (var index = 0; index < recoveryStages.Length; index++)
            {
                var expected = recoveryStages[index];
                var actual = execution[index + 1];
                if (actual.TransitionId != expected.TransitionId || actual.Kind != expected.Kind ||
                    actual.OperationKey != expected.OperationKey || actual.EventRef != expected.EventRef ||
                    Canonical(actual.Before) != Canonical(expected.Before) || Canonical(actual.After) != Canonical(expected.After))
                    return DiagnosisBindingFailure("diagnosis recovery sequence differs from complete sealed History");
            }

            var byWound = mutations.ToLookup(mutation => mutation.WoundId, StringComparer.Ordinal);
            var matched = 0;
            WoundMaterializationEnvelope? selectedAfter = null;
            var selectedRecovered = false;
            foreach (var member in members)
            {
                var before = member.Original;
                var working = member.Working;
                var after = member.Stages.IsEmpty ? working : member.Stages[^1].After;
                var selected = before.WoundId == original.WoundId;
                var expectedWorking = selected ? knowledge : before;
                var beforeCanonical = Canonical(before);
                var afterCanonical = Canonical(after);
                if (before.Classification.Domain != "physical" || working.WoundId != before.WoundId ||
                    after.WoundId != before.WoundId || working.Owner != before.Owner || after.Owner != before.Owner ||
                    after.Lifecycle is not ("active" or "healed") || Canonical(working) != Canonical(expectedWorking))
                    return DiagnosisBindingFailure("diagnosis cohort identity or selected-only knowledge changed");
                var matches = byWound[before.WoundId].ToArray();
                // A complete zero-due member legitimately has no carrier write.
                // Its absence is required, rather than an omitted recovery.
                if (beforeCanonical == afterCanonical)
                {
                    if (matches.Length != 0) return DiagnosisBindingFailure("unchanged cohort member gained a mutation");
                }
                else
                {
                    if (matches.Length != 1) return DiagnosisBindingFailure("changed cohort member lacks one exact final mutation");
                    var actual = matches[0];
                    var actualBefore = actual.BeforeWound;
                    var actualAfter = actual.AfterWound;
                    var healed = after.Lifecycle == "healed";
                    if (actual.Operation != (healed ? "delete" : "update") || actualBefore is null ||
                        Canonical(actualBefore) != beforeCanonical ||
                        (healed ? actualAfter is not null : actualAfter is null || Canonical(actualAfter) != afterCanonical))
                        return DiagnosisBindingFailure("final diagnosis cohort carrier differs from its complete execution");
                    matched++;
                }
                if (selected) { selectedAfter = after; selectedRecovered = !member.Stages.IsEmpty; }
            }
            if (matched != mutations.Count || selectedAfter is null)
                return DiagnosisBindingFailure("additional or missing diagnosis cohort mutation");

            var notifications = new List<WoundPlayerNotification>();
            // All private/unselected members were authenticated above. Only the
            // exact selected player-visible wound contributes public text.
            if (WoundPlayerProjection.Visible(original.Display.Visibility))
            {
                var text = result.Resolution.ResultCategory == "success"
                    ? $"Осмотр завершён: {original.Display.Name}. Сведения о ране уточнены. Подробнее: {Command}"
                    : $"Осмотр не дал новых сведений о ране: {original.Display.Name}. Подробнее: {Command}";
                notifications.Add(new(Project(text), Command));
                if (selectedRecovered)
                {
                    var recoveryText = selectedAfter.Lifecycle == "healed"
                        ? $"За прошедшее время зажила рана: {original.Display.Name}. Подробнее: {Command}"
                        : $"Естественное восстановление: {original.Display.Name} ({selectedAfter.Severity.Value}); восстановление {selectedAfter.Recovery.CurrentStepProgress}/{selectedAfter.Recovery.CurrentStepThreshold}. Подробнее: {Command}";
                    notifications.Add(new(Project(recoveryText), Command));
                }
            }
            return new(notifications, Array.Empty<ValidationIssue>());
        }
        catch (Exception failure) when (failure is InvalidDataException or InvalidOperationException or ArgumentException or JsonException or OverflowException)
        {
            return DiagnosisBindingFailure(failure.Message);
        }
    }

    private static string Canonical(WoundMaterializationEnvelope wound) => WoundMaterializationContract.SerializeCanonical(wound);

    private static WoundAcceptedTurnOutputBindingResult DiagnosisBindingFailure(string actual) => new(
        Array.Empty<WoundPlayerNotification>(), new[] { AcceptedTurnBindingIssue("complete sealed Mortal diagnosis output DATA", actual) });
}
