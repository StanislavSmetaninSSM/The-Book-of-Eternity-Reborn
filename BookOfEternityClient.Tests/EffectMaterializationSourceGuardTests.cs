using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectMaterializationSourceGuardTests
{
    [Fact]
    public void EffectPublication_MustOnlyRunThroughTheCommonAcceptedMechanicsPlan()
    {
        var normalizerRoot = Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer");
        var effects = File.ReadAllText(Path.Combine(
            normalizerRoot,
            "CanonicalStateNormalizer.Effects.cs"));
        var accumulated = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "CanonicalStateNormalizer.cs"));
        var effectCache = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "EffectAcceptedTurnPlanCache.cs"));

        Assert.DoesNotContain("NormalizeEffectsAsync(", effects, StringComparison.Ordinal);
        Assert.DoesNotContain("TryGetValidated(", effectCache, StringComparison.Ordinal);
        Assert.DoesNotContain("HasValidated(", effectCache, StringComparison.Ordinal);
        Assert.Contains(
            "ValidateEffectPlanPublicationBindingAsync(",
            effects,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeAcceptedMechanicsAsync(",
            accumulated,
            StringComparison.Ordinal);
    }
}
