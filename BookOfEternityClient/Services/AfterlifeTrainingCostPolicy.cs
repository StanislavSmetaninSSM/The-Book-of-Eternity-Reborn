using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

public static class AfterlifeTrainingCostPolicy
{
    public const int SelfStandardArtMultiplierPercent = 400;
    public const int SelfSpiritFocusMultiplierPercent = 300;
    public const int SelfSpecialArtMultiplierPercent = 500;
    public const int MentorNeutralMultiplierPercent = 100;
    public const int MentorGoodMultiplierPercent = 80;
    public const int MentorExcellentMultiplierPercent = 60;

    public static int ComputeStandardArtBaseInkFeatherCost(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition art,
        int nextTier) =>
        checked(50 + nextTier * 50 + art.MinUnlockTier * 25);

    public static int ComputeStandardArtBaseLightSparkCost(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition art,
        int nextTier) =>
        checked(4 + nextTier * 3 + art.MinUnlockTier);

    public static int ComputeSpiritFocusBaseInkFeatherCost(int nextTier) =>
        checked(100 + nextTier * 100);

    public static int ComputeSpiritFocusBaseLightSparkCost(int nextTier) =>
        checked(8 + nextTier * 4);

    public static int ComputeSelfStandardArtInkFeatherCost(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition art,
        int nextTier) =>
        ApplyMultiplier(ComputeStandardArtBaseInkFeatherCost(art, nextTier), SelfStandardArtMultiplierPercent);

    public static int ComputeSelfStandardArtLightSparkCost(
        AfterlifeSpiritualConflictState.SpiritualArtDefinition art,
        int nextTier) =>
        ApplyMultiplier(ComputeStandardArtBaseLightSparkCost(art, nextTier), SelfStandardArtMultiplierPercent);

    public static int ComputeSelfSpiritFocusInkFeatherCost(int nextTier) =>
        ApplyMultiplier(ComputeSpiritFocusBaseInkFeatherCost(nextTier), SelfSpiritFocusMultiplierPercent);

    public static int ComputeSelfSpiritFocusLightSparkCost(int nextTier) =>
        ApplyMultiplier(ComputeSpiritFocusBaseLightSparkCost(nextTier), SelfSpiritFocusMultiplierPercent);

    public static int ComputeSelfSpecialArtInkFeatherCost(int baseInkFeatherCost) =>
        ApplyMultiplier(baseInkFeatherCost, SelfSpecialArtMultiplierPercent);

    public static int ComputeSelfSpecialArtLightSparkCost(int baseLightSparkCost) =>
        ApplyMultiplier(baseLightSparkCost, SelfSpecialArtMultiplierPercent);

    public static int ResolveMentorMultiplierPercent(int relationshipLevel)
    {
        if (relationshipLevel >= 60)
            return MentorExcellentMultiplierPercent;
        if (relationshipLevel >= 30)
            return MentorGoodMultiplierPercent;
        return MentorNeutralMultiplierPercent;
    }

    public static int ComputeMentorCost(int baseCost, int relationshipLevel) =>
        ApplyMultiplier(baseCost, ResolveMentorMultiplierPercent(relationshipLevel));

    private static int ApplyMultiplier(int baseCost, int multiplierPercent) =>
        baseCost <= 0
            ? 0
            : checked((int)Math.Ceiling(baseCost * multiplierPercent / 100m));
}
