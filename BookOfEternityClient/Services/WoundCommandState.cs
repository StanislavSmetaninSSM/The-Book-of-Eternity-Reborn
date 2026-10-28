namespace BookOfEternityClient.Services;

internal sealed record WoundTreatmentChoice(string Value, SpiritualSelfTreatmentSelection Selection,
    string Name, string Severity, int? HealingTier, bool Available, string Reason);

/// <summary>Detached selection DATA retained by the existing prompt session or console action.</summary>
internal sealed class WoundCommandState(string generation, IEnumerable<WoundTreatmentChoice> choices)
{
    private readonly WoundTreatmentChoice[] _choices = choices.ToArray();
    internal string Generation { get; } = generation;
    internal IReadOnlyList<WoundTreatmentChoice> Choices => Array.AsReadOnly(_choices);
    internal WoundTreatmentChoice Resolve(string value) => _choices.SingleOrDefault(c => c.Value == value && c.Available)
        ?? throw new InvalidDataException("Выбранная рана больше недоступна; откройте /лечить заново.");
}
