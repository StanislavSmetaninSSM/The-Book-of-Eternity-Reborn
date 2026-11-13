using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalNpcProgressionPublicationTests
{
    [Theory]
    [InlineData("policy", true)]
    [InlineData("allowed_keys", true)]
    [InlineData("unrelated_lore", false)]
    public async Task PublicationSource_RetainedReductionAuthorityMustMatchIssuedCurrentPolicy(
        string mutation, bool shouldReject)
    {
        using var fixture = await Fixture.Create();
        const string keysPath = "game_state/misc/characteristics.json";
        await fixture.Files.WriteFileAtomicAsync(ProgressionWorldSettingPath, NoCharacteristicGrowthPolicy);
        await fixture.Files.WriteFileAtomicAsync(keysPath, """{"strength":10}""");
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        // A is the real detached authority retained before publication preparation.
        var retained = await NpcCoreChangesContract.ReadProgressionPolicyAuthorityAsync(fixture.Files);
        Assert.True(retained.NpcCharacteristicPolicy.IsValid);
        if (mutation == "policy")
            await fixture.Files.WriteFileAtomicAsync(lease, ProgressionWorldSettingPath,
                FixedCharacteristicGrowthPolicy);
        else if (mutation == "allowed_keys")
            await fixture.Files.WriteFileAtomicAsync(lease, keysPath,
                """{"strength":10,"agility":7}""");
        else
            await fixture.Files.WriteFileAtomicAsync(lease, ProgressionWorldSettingPath,
                """{"lore":"unrelated current prose","npcProgression":{"schemaVersion":1,"characteristics":{"mode":"none"}}}""");

        // B is issued by the actual authenticated snapshot/publication owner, not a fabricated DTO.
        var source = await fixture.Validator.PrepareNpcProgressionPublicationAsync(lease,
            await fixture.Files.ReadFileBytesAsync(lease, NpcCoreChangesContract.NpcCorePath));
        Assert.Empty(source.Issues);
        Assert.NotNull(source.Context);
        await source.RequireCurrentAsync(fixture.Files, lease);
        if (shouldReject)
            Assert.Throws<InvalidDataException>(() => source.RequirePolicyAuthority(retained));
        else
            source.RequirePolicyAuthority(retained);
    }
}
