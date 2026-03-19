namespace BookOfEternityClient.Core;

internal enum ReputationScaleKind
{
    Guardian,
    NpcRelationship,
    Faction
}

internal readonly record struct ReputationBand(
    int Min,
    int Max,
    string Label,
    string Color,
    string? Icon = null);

internal sealed record ReputationScaleDefinition(
    ReputationScaleKind Kind,
    int MinValue,
    int MaxValue,
    IReadOnlyList<ReputationBand> Bands)
{
    public ReputationBand Resolve(int value)
    {
        foreach (var band in Bands)
        {
            if (value >= band.Min && value <= band.Max)
                return band;
        }

        return value > MaxValue
            ? Bands[^1]
            : Bands[0];
    }

    public bool TryGetNextBand(int value, out ReputationBand band)
    {
        foreach (var candidate in Bands)
        {
            if (candidate.Min > value)
            {
                band = candidate;
                return true;
            }
        }

        band = default;
        return false;
    }
}

internal static class ReputationScales
{
    private static readonly ReputationScaleDefinition GuardianScale = new(
        ReputationScaleKind.Guardian,
        -100,
        300,
        new[]
        {
            new ReputationBand(-100, -51, "Враждебный", "bold red"),
            new ReputationBand(-50, -21, "Недружелюбный", "orange1"),
            new ReputationBand(-20, 49, "Нейтральный", "grey"),
            new ReputationBand(50, 129, "Дружелюбный", "green"),
            new ReputationBand(130, 229, "Преданный", "cyan"),
            new ReputationBand(230, 300, "Легендарный", "gold1")
        });

    private static readonly ReputationScaleDefinition NpcRelationshipScale = new(
        ReputationScaleKind.NpcRelationship,
        -400,
        400,
        new[]
        {
            new ReputationBand(-400, -201, "Непримиримый Враг", "bold red", "💀"),
            new ReputationBand(-200, -51, "Противник", "red", "⚔"),
            new ReputationBand(-50, -1, "Неприязнь", "orange1", "😠"),
            new ReputationBand(0, 100, "Нейтралитет", "grey", "😐"),
            new ReputationBand(101, 250, "Доверие и Расположение", "green", "😊"),
            new ReputationBand(251, 350, "Глубокая Связь", "bold cyan", "💙"),
            new ReputationBand(351, 400, "Легендарная Преданность", "bold gold1", "⭐")
        });

    private static readonly ReputationScaleDefinition FactionScale = new(
        ReputationScaleKind.Faction,
        -400,
        400,
        new[]
        {
            new ReputationBand(-400, -201, "Заклятый враг", "bold red"),
            new ReputationBand(-200, -51, "Враг", "red"),
            new ReputationBand(-50, -1, "Недоверие", "orange1"),
            new ReputationBand(0, 100, "Нейтралитет", "grey"),
            new ReputationBand(101, 250, "Сочувствующий", "yellow"),
            new ReputationBand(251, 350, "Почётный член", "green"),
            new ReputationBand(351, 400, "Живая легенда", "bold gold1")
        });

    public static ReputationScaleDefinition Get(ReputationScaleKind kind) => kind switch
    {
        ReputationScaleKind.Guardian => GuardianScale,
        ReputationScaleKind.NpcRelationship => NpcRelationshipScale,
        ReputationScaleKind.Faction => FactionScale,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static ReputationBand Resolve(ReputationScaleKind kind, int value) => Get(kind).Resolve(value);

    public static bool TryGetNextBand(ReputationScaleKind kind, int value, out ReputationBand band)
        => Get(kind).TryGetNextBand(value, out band);
}
