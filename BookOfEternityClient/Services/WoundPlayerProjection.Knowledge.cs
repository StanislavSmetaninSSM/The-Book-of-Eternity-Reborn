using System.Globalization;

namespace BookOfEternityClient.Services;

internal static partial class WoundPlayerProjection
{
    internal const string RequirementNotice =
        "Указанные требования не подтверждают наличие ресурсов, доступность помощника или точную цену.";

    private static IReadOnlyList<WoundPlayerMethod> ProjectMethods(WoundMaterializationEnvelope wound,
        WoundPlayerProjectionInput input)
    {
        var known = wound.Treatment.KnownRouteIds.ToHashSet(StringComparer.Ordinal);
        if (wound.Classification.Domain != "physical")
        {
            // Spiritual requirements have no registered player application codec yet.
            // The known name is safe; opaque route JSON is not a readable requirement.
            return wound.Treatment.Routes.Where(r => r.Visibility != "gm_only" && known.Contains(r.RouteId))
                .Select(r => new WoundPlayerMethod(Text(r.DisplayName),
                    ["Условия известного способа следует уточнить у специалиста."], RequirementNotice)).ToArray();
        }
        var parsed = MortalWoundTreatmentContract.ParseProjection(wound.Treatment, "projection.treatment",
            wound.Owner.Realm, wound.Owner.OwnerKind, wound.Severity.Rank, wound.Complications,
            wound.Recovery.DeteriorationPolicy);
        if (!parsed.IsValid || parsed.Treatment is null) return [];
        return parsed.Treatment.Routes.Where(r => known.Contains(r.RouteId) && r.Visibility != "gm_only")
            .Select(r => new WoundPlayerMethod(Text(r.DisplayName),
                r.Requirements.Select(q => Requirement(q, input)).ToArray(),
                string.Join(" ", RequirementNotice, DescribeMode(r.Mode),
                    r.ResourcePolicy.ConsumeOn.Length == 0 ? "" :
                        "Расход предусмотрен при: " + string.Join(", ", r.ResourcePolicy.ConsumeOn.Select(Outcome)) + ".",
                    r.ResourcePolicy.RefundOn.Length == 0 ? "" :
                        "Возврат предусмотрен при: " + string.Join(", ", r.ResourcePolicy.RefundOn.Select(Outcome)) + ".")))
            .ToArray();
    }

    private static string Requirement(MortalWoundTreatmentRequirement requirement, WoundPlayerProjectionInput input) => requirement switch
    {
        MortalWoundItemQuantityRequirement q => $"{ItemName(q, input)}: {q.Quantity.ToString(CultureInfo.InvariantCulture)} шт. ({Role(q.OwnerRole)})",
        MortalWoundResourceQuantityRequirement q => Resource(q, input),
        MortalWoundSkillTierRequirement q => $"Навык: не ниже {q.MinimumTier.ToString(CultureInfo.InvariantCulture)} ступени ({Role(q.ActorRole)})",
        MortalWoundSourceCapabilityRequirement q => $"Необходимое умение ({Role(q.ActorRole)})",
        MortalWoundProviderRequirement => "Требуется подходящий специалист",
        MortalWoundConsentRequirement => "Требуется согласие участников",
        MortalWoundFacilityRequirement => "Требуется подходящее место лечения",
        MortalWoundLocationRequirement => "Требуется находиться в подходящем месте",
        MortalWoundQuestStateRequirement => "Требуется выполнение условия договорённости",
        MortalWoundEffectStateRequirement => "Требуется подходящее состояние участника",
        MortalWoundEnvironmentRequirement => "Требуются подходящие условия окружающей среды",
        _ => "Условие лечения следует уточнить"
    };

    private static string ItemName(MortalWoundItemQuantityRequirement q, WoundPlayerProjectionInput input) =>
        q.OwnerRole == "target" && input.KnownItemNames.TryGetValue(q.ItemRef, out var name) && Text(name).Length > 0
            ? Text(name) : "Лечебные материалы";

    private static string Resource(MortalWoundResourceQuantityRequirement q, WoundPlayerProjectionInput input)
    {
        if (!input.Resources.TryResolveExact(q.ResourceRef, out var definition) || definition is null ||
            definition.Visibility is ResourceVisibility.Hidden or ResourceVisibility.GmOnly ||
            definition.Visibility == ResourceVisibility.OwnerVisible && q.OwnerRole != "target")
            return "Необходимый запас участника следует уточнить";
        return $"{Text(definition.DisplayName)}: {q.Quantity.ToString(CultureInfo.InvariantCulture)} ед. ({Role(q.OwnerRole)})";
    }

    private static IReadOnlyList<string> ProjectDiagnosis(WoundMaterializationEnvelope wound)
    {
        var current = wound.Treatment.KnownRouteIds.Select(id => "route:" + id)
            .Concat(wound.Complications.Where(c => Visible(c.Visibility)).Select(c => "complication:" + c.ComplicationId))
            .ToHashSet(StringComparer.Ordinal);
        // Future reachable reveals validate a contract; they are not current knowledge.
        return wound.Treatment.DiagnosisPaths.Where(p => p.Visibility != "gm_only" &&
                (Visible(p.Visibility) || p.Visibility == "hidden" && p.RequiresKnownFacts.Count > 0) &&
                p.RequiresKnownFacts.All(current.Contains))
            .Select(p => Text(p.DisplayName)).Where(n => n.Length > 0).ToArray();
    }

    private static string Role(string role) => role == "target" ? "у получателя лечения" : "у лечащего";
    private static string DescribeMode(string mode) => mode switch
    {
        "procedure" => "Исход процедуры зависит от проверки.",
        "course" => "Лечение проводится курсом; условия перерыва следует уточнить.",
        _ => "Способ требует выполнения всех заявленных условий."
    };
    private static string Outcome(string value) => value switch
    {
        "success" => "успехе", "partial_success" => "частичном успехе", "failed_attempt" => "неудачной попытке",
        "cancelled" => "отмене", "validation_failed" => "неподтверждённых условиях", "rolled_back" => "непринятом результате",
        _ => "оговорённом исходе"
    };
}
