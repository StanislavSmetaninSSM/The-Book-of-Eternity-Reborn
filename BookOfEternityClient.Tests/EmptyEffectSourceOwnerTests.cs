using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EmptyEffectSourceOwnerTests
{
    [Fact]
    public void EmptyNewLocationDefinitionsDoNotDemandOrGrantEffectOwnerAuthority()
    {
        var roots = LocationSources(new JsonArray(), permanent: false);
        var before = roots.Values.Single()!.ToJsonString();
        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        var empty = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(new Dictionary<string, JsonNode?>());
        Assert.Empty(authority.Issues);
        Assert.Equal(empty.CanonicalFingerprint, authority.CanonicalFingerprint);
        Assert.Equal(before, roots.Values.Single()!.ToJsonString());
        Assert.Empty(EffectAcceptedTurnInputComposer.ValidateRegisteredSourceOwners(roots));
    }

    [Fact]
    public void NonemptyUnresolvedLocationStillRequiresEffectOwnerIdentity()
    {
        var roots = LocationSources(new JsonArray(EffectMaterializationTestFixture.CreateDefinition()), permanent: false);
        var issue = Assert.Single(EffectAcceptedTurnInputComposer.ValidateRegisteredSourceOwners(roots));
        Assert.Equal("effect_source_authority_invalid_owner_identity", issue.Code);
        Assert.Equal(MortalLocationMaterializationContract.CurrentLocationPath + ".activeEffectDefinitions", issue.FilePath);
    }

    [Fact]
    public void StableLocationWithValidDefinitionsRetainsCanonicalSourceAuthority()
    {
        var definitions = new JsonArray(EffectMaterializationTestFixture.CreateDefinition());
        using var document = JsonDocument.Parse(definitions.ToJsonString());
        Assert.Empty(EffectSourceDefinitionContract.ValidateArray(document.RootElement,
            "location.activeEffectDefinitions", "mortal_world"));
        var roots = LocationSources(definitions, permanent: true);
        Assert.Empty(EffectAcceptedTurnInputComposer.ValidateRegisteredSourceOwners(roots));
        var authority = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots);
        Assert.Empty(authority.Issues);
        var empty = EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(new Dictionary<string, JsonNode?>());
        Assert.NotEqual(empty.CanonicalFingerprint, authority.CanonicalFingerprint);
    }

    [Fact]
    public void NonarrayDefinitionsRemainMalformedAtDefinitionContract()
    {
        // The production consumer validates definitions separately before owner identity.
        // The identity-only helper historically skips nonarrays; do not relabel that as acceptance.
        using var document = JsonDocument.Parse("{}");
        var issues = EffectSourceDefinitionContract.ValidateArray(document.RootElement,
            "location.activeEffectDefinitions", "mortal_world");
        Assert.Contains(issues, issue => issue.Code == "effect_source_definition_invalid_field");
    }

    /// <summary>Builds detached source DATA only, without an admitted turn or publication permission.</summary>
    private static Dictionary<string, JsonNode?> LocationSources(JsonArray definitions, bool permanent) => new()
    {
        [MortalLocationMaterializationContract.CurrentLocationPath] = new JsonObject
        {
            ["currentLocationData"] = new JsonObject
            {
                ["locationId"] = permanent ? "loc_court" : null,
                ["initialId"] = permanent ? null : "locref_court",
                ["activeEffectDefinitions"] = definitions
            }
        }
    };
}
