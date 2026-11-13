using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalNpcProgressionPublicationTests
{
    private const string ProgressionWorldSettingPath = "lore/current_world/world_setting.json";
    private const string NoCharacteristicGrowthPolicy =
        """{"npcProgression":{"schemaVersion":1,"characteristics":{"mode":"none"}}}""";
    private const string FixedCharacteristicGrowthPolicy =
        """{"npcProgression":{"schemaVersion":1,"characteristics":{"mode":"fixedIncrements","perLevel":{"strength":1}}}}""";
    private const string MalformedCharacteristicGrowthPolicy = """{"npcProgression":null}""";

    [Theory]
    [InlineData("absent_to_present")]
    [InlineData("changed_policy")]
    [InlineData("malformed_present")]
    public async Task Coordinator_CoreOnlyPostValidatorWorldPolicyMutationRollsBackCanonicalOutputs(string mutation)
    {
        using var fixture = await Fixture.Create();
        Assert.False(fixture.Files.FileExists(MortalNpcAcceptedSkillInput.SidecarPath));
        if (mutation == "absent_to_present")
            Assert.False(fixture.Files.FileExists(ProgressionWorldSettingPath));
        else
            await fixture.Files.WriteFileAtomicAsync(ProgressionWorldSettingPath, NoCharacteristicGrowthPolicy);
        await fixture.Files.WriteFileAtomicAsync("game_state/misc/characteristics.json", """{"strength":10}""");
        await fixture.AddUnrelatedNormalizableCommand();
        await fixture.Files.WriteFileAtomicAsync(ValidationService.CanonicalSkillMasteryPath, "{}");
        Assert.Empty(await fixture.Validator.ValidateNpcCoreChangesBeforeNormalizationAsync());
        var injectedPolicy = mutation == "malformed_present"
            ? MalformedCharacteristicGrowthPolicy : FixedCharacteristicGrowthPolicy;
        var before = await fixture.CaptureCanonical();
        // World policy is an observed input, not an output owned by this publication.
        Assert.DoesNotContain(ProgressionWorldSettingPath, before.Keys);
        var injected = false;
        var files = fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (injected || path != ValidationService.CanonicalSkillMasteryPath) return Task.CompletedTask;
                var npc = JsonNode.Parse(File.ReadAllText(fixture.Physical(NpcCoreChangesContract.NpcCorePath)))!;
                if (npc["NPCCoreChanges"] is not null) return Task.CompletedTask;
                // The real awaited canonical post-validator has reached both normalized outputs.
                Assert.Equal(2, npc["NPCsInScene"]![0]!["level"]!.GetValue<int>());
                Assert.Equal(1, npc["NPCsInScene"]![0]!["experience"]!.GetValue<int>());
                var quests = JsonNode.Parse(File.ReadAllText(fixture.Physical("game_state/quests/quest_history.json")))!;
                Assert.Null(quests["quests"]);
                Assert.False(File.Exists(fixture.Physical(MortalNpcAcceptedSkillInput.SidecarPath)));
                injected = true;
                Directory.CreateDirectory(Path.GetDirectoryName(fixture.Physical(ProgressionWorldSettingPath))!);
                File.WriteAllText(fixture.Physical(ProgressionWorldSettingPath), injectedPolicy);
                return Task.CompletedTask;
            }
        });

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
                new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(files, NullLogger<ValidationService>.Instance), fixture.Backups));

        Assert.True(injected, "The real canonical post-validator must reach the late policy mutation.");
        Assert.Equal("NPC progression current-world policy changed before publication completed.", error.Message);
        await fixture.AssertCanonicalUnchanged(before);
        Assert.Equal(injectedPolicy, await fixture.Files.ReadFileAsync(ProgressionWorldSettingPath));
    }

    [Fact]
    public async Task Coordinator_CoreOnlyMalformedPresentWorldPolicyRefusesBeforeNormalizerWrites()
    {
        using var fixture = await Fixture.Create();
        Assert.False(fixture.Files.FileExists(MortalNpcAcceptedSkillInput.SidecarPath));
        await fixture.Files.WriteFileAtomicAsync(ProgressionWorldSettingPath, MalformedCharacteristicGrowthPolicy);
        await fixture.AddUnrelatedNormalizableCommand();
        var before = await fixture.CaptureCanonical();

        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);

        Assert.Contains(result.Issues, issue => issue.Severity == IssueSeverity.Error &&
            issue.Code == "npc_progression_consequence_policy_invalid");
        Assert.Null(result.MechanicsPlan);
        await fixture.AssertCanonicalUnchanged(before);
        Assert.Equal(MalformedCharacteristicGrowthPolicy, await fixture.Files.ReadFileAsync(ProgressionWorldSettingPath));
    }
}
