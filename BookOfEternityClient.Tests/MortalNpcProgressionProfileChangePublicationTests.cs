using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Fixture = BookOfEternityClient.Tests.MortalNpcProgressionPublicationTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcProgressionProfileChangePublicationTests
{
    [Fact]
    public async Task ProfileOnlyReplacement_PublishesDerivedThresholdWithoutXpOrLevelGain()
    {
        using var fixture = await Fixture.Create(progression: false);
        await WriteReplacement(fixture);
        var result = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            fixture.Files, fixture.Normalizer, fixture.Validator, fixture.Backups);
        Assert.Empty(result.Issues);
        Assert.Null(result.MechanicsPlan);
        var core = JsonNode.Parse((await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!;
        Assert.Null(core["NPCCoreChanges"]);
        AssertPublished(core);
    }

    [Fact]
    public async Task ProfileOnlyReplacement_LateFailureRollsBackAllCanonicalBytes()
    {
        using var fixture = await Fixture.Create(progression: false);
        await WriteReplacement(fixture);
        await fixture.AddUnrelatedNormalizableCommand();
        await fixture.Files.WriteFileAtomicAsync(ValidationService.CanonicalSkillMasteryPath, "{}");
        var before = await fixture.CaptureCanonical();
        var injected = false;
        var files = fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (injected || path != ValidationService.CanonicalSkillMasteryPath) return Task.CompletedTask;
                var core = JsonNode.Parse(File.ReadAllText(fixture.Physical(NpcCoreChangesContract.NpcCorePath)))!;
                if (core["NPCCoreChanges"] is not null) return Task.CompletedTask;
                AssertPublished(core);
                injected = true;
                throw new InvalidDataException("injected profile replacement postvalidation failure");
            }
        });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
                new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance),
                new ValidationService(files, NullLogger<ValidationService>.Instance), fixture.Backups));
        Assert.True(injected, "Must publish the actual replacement before the injected late failure.");
        Assert.Contains("injected profile replacement", error.Message, StringComparison.Ordinal);
        await fixture.AssertCanonicalUnchanged(before);
    }

    private static void AssertPublished(JsonNode core)
    {
        var actor = core["NPCsInScene"]![0]!;
        Assert.Equal(1, actor["level"]!.GetValue<int>());
        Assert.Equal(99, actor["experience"]!.GetValue<int>());
        Assert.Equal(200, actor["experienceForNextLevel"]!.GetValue<int>());
        Assert.Equal("table", actor["progressionProfile"]!["mode"]!.GetValue<string>());
        Assert.Empty(actor["activeSkills"]!.AsArray());
        Assert.Empty(actor["passiveSkills"]!.AsArray());
    }

    private static async Task WriteReplacement(Fixture fixture)
    {
        var core = JsonNode.Parse((await fixture.Files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
        core["NPCCoreChanges"] = JsonNode.Parse("""
            [{"NPCId":"npc_source","reason":"Adopt declared curve","progressionProfileChange":{
              "source":{"sessionId":"session_source","requestId":"request_source","turnNumber":42},
              "operation":"replace","level":1,"experience":99,"experienceForNextLevel":200,
              "profile":{"schemaVersion":1,"mode":"table","thresholds":[200,600],"capOverflow":"discard",
                "consequences":{"skills":{"levelsPerAward":2,"allowedAwards":["newSkill","masteryIncrease"]},
                  "characteristics":{"mode":"currentWorld"}}}}}]
            """);
        await fixture.Files.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, core.ToJsonString());
    }
}
