using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class CanonicalNormalizationAuthoritySourceGuardTests
{
    private const string ClientOwnedBootstrapMethod =
        "NormalizeClientOwnedBootstrapAccumulatedStateAsync";

    [Fact]
    public void ClientOwnedBootstrapNormalization_HasNoGmOrQteProductionCallsite()
    {
        var productionRoot = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient");
        var canonicalNormalizerSource = File.ReadAllText(Path.Combine(
            productionRoot,
            "Services",
            "CanonicalStateNormalizer.cs"));
        var qteSource = File.ReadAllText(Path.Combine(
            productionRoot,
            "Services",
            "QteSceneService.cs"));

        Assert.Contains(
            ClientOwnedBootstrapMethod + "(",
            canonicalNormalizerSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "allowUnvalidatedMortalItemIdentityAllocation: true",
            canonicalNormalizerSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "NormalizeAccumulatedStateAsync(normalizerBackups)",
            qteSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ClientOwnedBootstrapMethod,
            qteSource,
            StringComparison.Ordinal);

        Assert.Equal(
            2,
            CountOccurrences(
                canonicalNormalizerSource,
                ClientOwnedBootstrapMethod + "("));
        Assert.Contains(
            ".NormalizeClientOwnedBootstrapAccumulatedStateAsync(backups)",
            canonicalNormalizerSource,
            StringComparison.Ordinal);

        var externalProductionOccurrences = Directory
            .EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !string.Equals(
                Path.GetFullPath(path),
                Path.GetFullPath(Path.Combine(
                    productionRoot,
                    "Services",
                    "CanonicalStateNormalizer.cs")),
                StringComparison.OrdinalIgnoreCase))
            .Sum(path => CountOccurrences(
                File.ReadAllText(path),
                ClientOwnedBootstrapMethod + "("));
        Assert.Equal(0, externalProductionOccurrences);
    }

    [Fact]
    public void AcceptedTurnAuthorityRegistry_NeverExposesRootOwnedCaches()
    {
        var registrySource = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "Services",
            "AcceptedTurnAuthorityRegistry.cs"));

        Assert.DoesNotContain(
            "internal static AcceptedMechanicsPlanCache",
            registrySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "internal static EffectAcceptedTurnPlanCache",
            registrySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "internal static MortalItemAcceptedTurnAuthority.Cache",
            registrySource,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "internal static WoundAcceptedTurnPlanCache",
            registrySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "private static AcceptedTurnAuthorityState GetState(",
            registrySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "private sealed class AcceptedTurnAuthorityState",
            registrySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);",
            registrySource,
            StringComparison.Ordinal);
        Assert.Contains(
            "identity.SessionGenerationRevision",
            registrySource,
            StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
