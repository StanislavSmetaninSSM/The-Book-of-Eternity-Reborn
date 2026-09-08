using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualWoundConsequenceTests
{
    [Fact]
    public void ProjectorContract_AcceptedComponentCarriesTypedSourceAndProjectorExists()
    {
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(
            "spiritual_roll_hindrance");
        effect["target"]!["targetId"] = "guardian_projector";
        var profiles = new JsonObject
        {
            ["profiles"] = new JsonArray(new JsonObject
            {
                ["actorType"] = "guardian",
                ["actorId"] = "guardian_projector",
                ["realm"] = "chaos_sea",
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            })
        };
        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var owner = identity["entries"]![0]!["owner"]!.AsObject();
        owner["ownerId"] = "guardian_projector";
        owner["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;

        var snapshot = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(
            new EffectCarrierCatalogInput(
                null,
                null,
                null,
                null,
                profiles,
                null),
            identity));

        Assert.True(snapshot.IsAccepted, DescribeContractIssues(snapshot.Issues));
        var component = Assert.Single(snapshot.Components);
        var sourceProperty = typeof(EffectMechanicalComponent).GetProperty(
            "Source",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(sourceProperty);
        Assert.IsType<EffectSourceKey>(sourceProperty!.GetValue(component));
        Assert.NotNull(typeof(EffectMechanicsSnapshot).Assembly.GetType(
            "BookOfEternityClient.Services.SpiritualWoundConflictContributionProjector"));
    }

    private static string DescribeContractIssues(IReadOnlyList<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(static issue =>
                $"{issue.FilePath}: {issue.Code}: {issue.Message}"));
}
