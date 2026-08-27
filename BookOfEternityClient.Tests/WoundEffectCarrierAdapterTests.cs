using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundEffectCarrierAdapterTests
{
    [Theory]
    [InlineData(
        "mortal_world",
        "player_soul",
        "player_current",
        WoundCarrierCatalog.PlayerPath)]
    [InlineData(
        "chaos_sea",
        "player",
        "player_soul",
        WoundCarrierCatalog.AfterlifeProfilesPath)]
    [InlineData(
        " mortal_world",
        "player",
        "player_current",
        WoundCarrierCatalog.PlayerPath)]
    [InlineData(
        "mortal_world",
        "player",
        " player_current ",
        WoundCarrierCatalog.PlayerPath)]
    [InlineData(
        "mortal_world",
        "npc",
        " npc_invalid ",
        WoundCarrierCatalog.NpcPath)]
    [InlineData(
        "chaos_sea",
        "guardian",
        " guardian_invalid ",
        WoundCarrierCatalog.AfterlifeProfilesPath)]
    public void Adapter_RejectsCrossRealmKindsAndInexactOwnerAuthority(
        string realm,
        string ownerKind,
        string ownerId,
        string carrierPath)
    {
        var owner = new WoundOwnerCoordinate(
            realm,
            ownerKind,
            ownerId,
            carrierPath);
        var targetKind = ownerKind switch
        {
            "player" or "player_soul" => "player",
            "combatant" or "combatant_member" => "combatant",
            _ => ownerKind
        };
        var target = new EffectTargetKey(realm, targetKind, ownerId);
        var definition = EffectMaterializationTestFixture.CreateDefinition();

        Assert.False(WoundEffectCarrierAdapter.TryCreateTargetKey(owner, out _));
        Assert.False(WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
            owner,
            target,
            definition,
            out _));
    }
}
