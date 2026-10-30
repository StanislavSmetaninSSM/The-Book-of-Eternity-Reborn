namespace BookOfEternityClient.Services;

/// <summary>Closed immutable locality DATA; no variant carries another realm's owner aliases.</summary>
internal sealed record SpiritualAidLocality
{
    internal abstract record Shape;
    internal sealed record ChaosAbode(string GuardianId, string AbodeId) : Shape;
    internal sealed record ShiningFactionHall(string FactionId, string HallId) : Shape;
    internal sealed record ElyaraShiningHall(string HallId) : Shape;

    private SpiritualAidLocality(Shape shape) => Value = shape;
    internal SpiritualAidLocality(string guardianId, string abodeId) : this(new ChaosAbode(guardianId, abodeId)) { }
    internal Shape Value { get; }
    internal static SpiritualAidLocality ForFaction(string factionId, string hallId) => new(new ShiningFactionHall(factionId, hallId));
    internal static SpiritualAidLocality ForElyara(string hallId) => new(new ElyaraShiningHall(hallId));
    internal string Realm => Value is ChaosAbode ? "chaos_sea" : "shining_abode";
    internal string LocationId => Value switch
    {
        ChaosAbode chaos => chaos.AbodeId,
        ShiningFactionHall faction => faction.HallId,
        ElyaraShiningHall elyara => elyara.HallId,
        _ => throw new InvalidDataException("Unknown spiritual aid locality.")
    };
    internal string GuardianId => Value is ChaosAbode chaos ? chaos.GuardianId
        : throw new InvalidDataException("A Shining locality cannot be read as a Guardian Abode.");
    internal string AbodeId => Value is ChaosAbode chaos ? chaos.AbodeId
        : throw new InvalidDataException("A Shining hall cannot be read as a Chaos Abode.");
}
