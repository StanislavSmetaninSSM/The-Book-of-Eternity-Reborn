namespace BookOfEternityClient.Services;

internal sealed record WoundTreatmentChoice(string Value, SpiritualSelfTreatmentSelection Selection,
    string Name, string Severity, int? HealingTier, bool Available, string Reason);

/// <summary>Detached selection DATA retained by the existing prompt session or console action.</summary>
internal sealed class WoundCommandState(string generation, IEnumerable<WoundTreatmentChoice> choices)
{
    private readonly WoundTreatmentChoice[] _choices = choices.ToArray();
    private WoundTargetChoice[] _targets = [];
    internal string Generation { get; } = generation;
    internal IReadOnlyList<WoundTreatmentChoice> Choices => Array.AsReadOnly(_choices);
    internal IReadOnlyList<WoundTargetChoice> Targets => Array.AsReadOnly(_targets);
    internal WoundTargetChoice? SelectedTarget { get; private init; }
    internal bool IsTargetSelection { get; private init; }
    internal WoundCommandState WithTargets(IEnumerable<WoundTargetChoice> targets)
    {
        var detached = targets.ToArray();
        return new(Generation, _choices) { _targets = detached, IsTargetSelection = detached.Length > 1 };
    }
    internal WoundTargetChoice ResolveTarget(string value) => IsTargetSelection
        ? _targets.SingleOrDefault(t => t.Value == value && t.Available)
            ?? throw new InvalidDataException("Выбранная цель больше недоступна; откройте /лечить заново.")
        : throw new InvalidDataException("Выбор цели уже завершён; продолжите текущую форму.");
    internal WoundCommandState ForTarget(WoundTargetChoice target) => new(Generation,
        _choices.Where(c => c.Selection.Diagnosis is { } diagnosis ? diagnosis.Target == target.Owner :
            c.Selection.Aid?.Target(target.Owner.Realm) == target.Owner ||
            c.Selection.Aid is null && target.Owner.OwnerKind is "player" or "player_soul"))
        { _targets = _targets.ToArray(), SelectedTarget = target, IsTargetSelection = false };
    internal WoundTreatmentChoice Resolve(string value) => !IsTargetSelection
        ? _choices.SingleOrDefault(c => c.Value == value && c.Available)
        ?? throw new InvalidDataException("Выбранная рана больше недоступна; откройте /лечить заново.")
        : throw new InvalidDataException("Сначала выберите, кому требуется помощь.");
}
