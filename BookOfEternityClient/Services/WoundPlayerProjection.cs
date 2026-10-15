using System.Globalization;
using BookOfEternityClient.UI;

namespace BookOfEternityClient.Services;

internal sealed record WoundPlayerFact(string Label, string Value);
internal sealed record WoundPlayerMethod(string Name, IReadOnlyList<string> Requirements, string Policy);
internal sealed record WoundPlayerEntry(
    string Name, string Severity, string Description, IReadOnlyList<WoundPlayerFact> Facts,
    IReadOnlyList<WoundPlayerMethod> Methods, IReadOnlyList<string> Diagnosis);
internal sealed record WoundPlayerHistoryEntry(string Name, string Severity, string Summary);
internal sealed record WoundPlayerProjectionResult(
    bool IsAvailable, string StatusMessage, IReadOnlyList<WoundPlayerEntry> Active,
    IReadOnlyList<WoundPlayerHistoryEntry> History);
internal sealed record WoundPlayerProjectionInput(
    WoundCarrierCatalogInput Carriers, WoundIdentityState Identities, WoundHistoryState History,
    EffectMechanicsSnapshot Effects, string Realm, ResourceDefinitionCatalog Resources,
    IReadOnlyDictionary<string, string> KnownItemNames, bool SelfVisible = true);

/// <summary>Display-only self projection. No command, quote or mutation authority.</summary>
internal static partial class WoundPlayerProjection
{
    internal const string UnavailableMessage = "Сейчас невозможно надёжно определить состояние ран.";
    internal static WoundPlayerProjectionResult Unavailable() => new(false, UnavailableMessage, [], []);
    internal static WoundPlayerProjectionResult Build(WoundPlayerProjectionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Realm is not ("mortal_world" or "chaos_sea" or "shining_abode") ||
            input.Carriers.PlayerWounds is null || input.Carriers.NpcWounds is null ||
            input.Carriers.EnemyCombatants is null || input.Carriers.AllyCombatants is null ||
            input.Carriers.AfterlifeProfiles is null || !input.Effects.IsAccepted)
            return Unavailable();

        var catalog = WoundCarrierCatalog.Build(input.Carriers);
        if (catalog.Issues.Count != 0 || input.History.ValidateAgreement(input.Identities, catalog).Count != 0)
            return Unavailable();
        if (!input.SelfVisible)
            return new(true, EmptyMessage, [], []);

        var active = catalog.Occurrences
            .Where(o => IsSelf(o.Wound.Owner, input.Realm) && o.Wound.Lifecycle == "active" && Visible(o.Wound.Display.Visibility))
            .OrderBy(o => o.Wound.Origin.CreatedAtTurn)
            .ThenBy(o => o.WoundId, StringComparer.Ordinal)
            .Select(o => Project(o.Wound, input))
            .ToArray();
        return new(true, active.Length == 0 ? EmptyMessage : string.Empty, active, ProjectHistory(input));
    }

    private const string EmptyMessage = "Сейчас нет видимых активных ран в текущем царстве.";
    private static bool Visible(string visibility) => visibility is "public" or "known_to_player";
    private static bool IsSelf(WoundOwnerCoordinate owner, string realm) =>
        owner.Realm == realm && (realm == "mortal_world"
            ? owner.OwnerKind == "player" && owner.OwnerId == "player_current" && owner.CarrierPath == WoundCarrierCatalog.PlayerPath
            : owner.OwnerKind == "player_soul" && owner.OwnerId == "player_soul" && owner.CarrierPath == WoundCarrierCatalog.AfterlifeProfilesPath);

    // These are explicitly player-authored display fields, never raw semantic objects.
    // Renderers must still escape markup / render as text. Controls are not UI content.
    internal static string Text(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : new string(value.Where(c => !char.IsControl(c) || c is '\n' or '\t').ToArray()).Trim();

    private static WoundPlayerEntry Project(WoundMaterializationEnvelope wound, WoundPlayerProjectionInput input)
    {
        var facts = new List<WoundPlayerFact>();
        Add("Место", wound.Classification.LocationProfile.ReadableLocus);
        Add("Причина", wound.Origin.ReadableCause);
        Add("Признаки", string.Join("; ", wound.Display.VisibleSymptoms.Select(Text)));
        Add("Прогноз", wound.Display.Prognosis);
        Add("Уход", wound.Care.State switch
        {
            "fresh" => "Свежая рана", "untreated" => "Не обработана", "stabilized" => "Стабилизирована",
            "recovering" => "Восстанавливается", "healed" => "Зажила", _ => "Состояние ухода неизвестно"
        });
        Add("Восстановление", wound.Recovery.Mode switch
        {
            "requires_stabilization" => "Для восстановления нужна стабилизация",
            "no_natural_recovery" => "Самостоятельное восстановление невозможно",
            _ => "Постепенное восстановление"
        });
        Add("Прогресс", string.Create(CultureInfo.InvariantCulture,
            $"{wound.Recovery.CurrentStepProgress} / {wound.Recovery.CurrentStepThreshold}"));
        // Blocker tokens may encode undiscovered conditions. They are never printed.
        if (wound.Recovery.Blockers.Count > 0 && wound.Recovery.Mode != "requires_stabilization")
            Add("Условия восстановления", "Восстановление приостановлено; условия следует уточнить");
        foreach (var complication in wound.Complications.Where(c => Visible(c.Visibility)))
            Add("Осложнение", complication.DisplayName);

        var privateRoots = wound.Complications.Where(c => !Visible(c.Visibility))
            .SelectMany(c => c.OwnedEffectIds).ToHashSet(StringComparer.Ordinal);
        var ownedRoots = wound.Consequences.Entries.Select(e => e.EffectId)
            .Concat(wound.Complications.Where(c => Visible(c.Visibility)).SelectMany(c => c.OwnedEffectIds))
            .Where(id => !privateRoots.Contains(id)).ToHashSet(StringComparer.Ordinal);
        var effects = input.Effects with
        {
            Effects = input.Effects.Effects.Where(e => ownedRoots.Contains(e.EffectId) &&
                e.Realm == wound.Owner.Realm && e.TargetKind == "player" && e.TargetId == wound.Owner.OwnerId).ToArray()
        };
        foreach (var effect in EffectPlayerProjection.Build(new(effects, wound.Owner.Realm, "player", wound.Owner.OwnerId)).Entries)
        {
            Add("Последствие", effect.Name);
            if (!string.IsNullOrWhiteSpace(effect.Summary)) Add("Влияние", effect.Summary);
            foreach (var fact in effect.Facts) Add(fact.Label, fact.Value);
        }
        return new(Text(wound.Display.Name), wound.Severity.Value, Text(wound.Display.Description),
            facts, ProjectMethods(wound, input), ProjectDiagnosis(wound));

        void Add(string label, string? value)
        {
            var safe = Text(value);
            if (safe.Length > 0) facts.Add(new(label, safe));
        }
    }
}
