namespace BookOfEternityClient.Services;

public static class AfterlifeProgressionTuning
{
    public const int AscensionReadyEnlightenmentExperience = 60;
    public const int CultivateEnlightenmentExperiencePerFeather = 4;

    public static int ComputeCultivateEnlightenmentExperienceGain(int costInFeathers) =>
        checked(costInFeathers * CultivateEnlightenmentExperiencePerFeather);

    public static bool IsAscensionReadyEnlightenmentExperience(int experience) =>
        experience >= AscensionReadyEnlightenmentExperience;

    public static int ComputeAscensionProgressPercent(int experience) =>
        Math.Clamp(experience * 100 / AscensionReadyEnlightenmentExperience, 0, 100);
}
