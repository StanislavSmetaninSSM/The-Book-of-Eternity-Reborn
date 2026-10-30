namespace BookOfEternityClient.Services;

internal static partial class WoundApplicationService
{
    internal const string DiagnosisActionText = "Осмотреть выбранную духовную рану по отдельно подтверждённому способу и продолжить один обычный безопасный цикл мира. Сам осмотр не лечит и не расходует ОД, Перья или предметы.";

    private static SpiritualDiagnosisSourceData? PrepareDiagnosisSource(WoundPlayerReadContext context) =>
        context.Input.SelfVisible && (context.Input.Realm is "chaos_sea" or "shining_abode") &&
        context.SpiritualConflict?["activeConflict"] is null && context.Catalog.Occurrences.Any(row =>
            row.Wound.Owner.Realm == context.Input.Realm && row.Wound.Classification.Domain == "spiritual" &&
            row.Wound.Lifecycle == "active" && row.Wound.Treatment.DiagnosisPaths.Count != 0)
            ? new(context) : null;

    private static void AddDiagnosisChoices(List<WoundTreatmentChoice> choices, WoundPlayerReadContext context,
        SpiritualDiagnosisSourceData? source, WoundTargetChoice? target = null)
    {
        if (source is null || context.SpiritualConflict?["activeConflict"] is not null) return;
        var owner = target?.Owner ?? new WoundOwnerCoordinate(context.Input.Realm, "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath);
        foreach (var wound in context.Catalog.Occurrences.Select(row => row.Wound).Where(w => w.Owner == owner &&
            w.Lifecycle == "active" && w.Classification.Domain == "spiritual" && WoundPlayerProjection.Visible(w.Display.Visibility))
            .OrderBy(w => w.Origin.CreatedAtTurn).ThenBy(w => w.WoundId, StringComparer.Ordinal))
        foreach (var path in wound.Treatment.DiagnosisPaths)
        {
            var selection = source.DescribeSelection(wound, path.DiagnosisPathId);
            var name = WoundPlayerProjection.Text(path.DisplayName);
            if (selection is null || name.Length == 0) continue;
            var admission = source.Admit(wound, path.DiagnosisPathId, target);
            if (admission.IssueCode is "target_unavailable" or "path_unavailable" or "conflict_active") continue;
            var woundName = WoundPlayerProjection.Text(wound.Display.Name);
            var locus = WoundPlayerProjection.Text(wound.Classification.LocationProfile.ReadableLocus);
            var label = name + " — " + woundName + (locus.Length == 0 ? "" : " (" + locus + ")");
            choices.Add(new("diagnosis_" + choices.Count, selection, label, wound.Severity.Value,
                admission.Data?.HealingTier, admission.Data is not null, admission.Data is not null
                    ? "Условия осмотра подтверждены. Он раскроет предусмотренные сведения за один обычный безопасный цикл; сам осмотр не лечит, бросок и плата не нужны."
                    : "Сейчас невозможно подтвердить все условия этого духовного осмотра."));
        }
    }
}
