using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SpiritualWoundSourceEnvelopeTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":1,\"maximumSeverityRank\":5,\"guaranteedSeverityRank\":null}")]
    [InlineData("{\"schemaVersion\":1,\"maximumSeverityRank\":1,\"guaranteedSeverityRank\":2}")]
    [InlineData("{\"schemaVersion\":1,\"maximumSeverityRank\":1.5,\"guaranteedSeverityRank\":null}")]
    [InlineData("{\"schemaVersion\":1,\"maximumSeverityRank\":1,\"guaranteedSeverityRank\":null,\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"maximumSeverityRank\":1,\"maximumSeverityRank\":4,\"guaranteedSeverityRank\":null}")]
    public void SourceOwner_DeclarationSchemaIsClosedDataNotAuthority(string declaration)
    {
        using var document = JsonDocument.Parse("{\"spiritualWoundEnvelope\":" + declaration + "}");
        Assert.False(SpiritualWoundSourceEnvelope.TryRead(document.RootElement, out var envelope, out var error));
        Assert.Null(envelope);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("SpiritualWoundTarget", "guardian", "guardian_frame", "wound_one")]
    [InlineData("spiritualWoundTarget ", "guardian", "guardian_frame", "wound_one")]
    [InlineData("spirituаlWoundTarget", "guardian", "guardian_frame", "wound_one")]
    [InlineData("spiritualWoundTarget", " guardian", "guardian_frame", "wound_one")]
    [InlineData("spiritualWoundTarget", "guardian ", "guardian_frame", "wound_one")]
    [InlineData("spiritualWoundTarget", "guardian", " guardian_frame", "wound_one")]
    [InlineData("spiritualWoundTarget", "guardian", "guardian_frame ", "wound_one")]
    [InlineData("spiritualWoundTarget", "guardian", "guardian_frame", " wound_one")]
    [InlineData("spiritualWoundTarget", "guardian", "guardian_frame", "wound_one ")]
    public void SourceOwner_TargetShapeRejectsConfusableSpellingAndTrimmedIdentity(
        string property, string actorType, string actorId, string woundId)
    {
        var exchange = new JsonObject
        {
            [property] = new JsonObject
            {
                ["actorType"] = actorType, ["actorId"] = actorId, ["retraumaWoundRef"] = woundId
            }
        };
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "exchange", issues);
        Assert.Contains(issues, issue => issue.Code == "spiritual_source_target_invalid");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceOwner_TargetShapeAcceptsExactOptionalRefOnEitherAction(bool incoming)
    {
        var target = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_frame" };
        var exchange = incoming
            ? new JsonObject { ["incomingAction"] = new JsonObject { ["spiritualWoundTarget"] = target } }
            : new JsonObject { ["spiritualWoundTarget"] = target };
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "exchange", issues);
        Assert.Empty(issues);
    }

    [Fact]
    public void SourceOwner_TargetShapeRejectsExactPlusConfusableDuplicate()
    {
        var target = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_frame" };
        var exchange = new JsonObject
        {
            ["spiritualWoundTarget"] = target, ["SpiritualWoundTarget"] = target.DeepClone()
        };
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "exchange", issues);
        Assert.Contains(issues, issue => issue.Code == "spiritual_source_target_invalid");
    }

    [Fact]
    public void SourceOwner_DeclarationSurvivesRealLearningAndTierProgression()
    {
        var player = PlayerSoulProfile();
        player["specialArts"] = new JsonArray();
        var teacher = GuardianProfile("guardian_source_teacher");
        var art = SourceOwnerSpecialArt("guardian", "guardian_source_teacher");
        art["tier"] = 3;
        art["canTeachPlayer"] = true;
        art["trainingConditions"] = new JsonArray("Пройти испытание хранителя");
        art["spiritualWoundEnvelope"] = new JsonObject
        {
            ["schemaVersion"] = 1, ["maximumSeverityRank"] = 2, ["guaranteedSeverityRank"] = null
        };
        teacher["specialArts"] = new JsonArray(art);
        var prior = Profiles(player, teacher);
        var commands = new JsonObject
        {
            [AfterlifeEntityProfileState.SpecialArtLearningReceiptsProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["receiptId"] = "receipt_source_learning", ["teacherActorType"] = "guardian",
                    ["teacherActorId"] = "guardian_source_teacher", ["playerActorId"] = "player_soul",
                    ["artId"] = "art_source_owner", ["learnedAtTurn"] = 42,
                    ["trainingConditionSatisfied"] = true, ["roleplayEvidence"] = "Испытание завершено.",
                    ["summary"] = "Душа изучила искусство."
                }
            },
            [AfterlifeEntityProfileState.ProgressionOverridesProperty] = new JsonArray
            {
                new JsonObject
                {
                    ["actorType"] = "player_soul", ["actorId"] = "player_soul",
                    ["cycleKey"] = "source_cycle_42", ["reason"] = "accepted_training",
                    ["summary"] = "Искусство укрепилось.",
                    ["specialArtTierDeltas"] = new JsonObject { ["art_source_owner"] = 1 }
                }
            }
        };
        var projected = AfterlifeEntityProfileState.ProjectCanonicalRoot(commands, prior);
        var learned = projected["profiles"]!.AsArray().OfType<JsonObject>()
            .Single(profile => profile["actorType"]!.GetValue<string>() == "player_soul")
            ["specialArts"]!.AsArray().OfType<JsonObject>().Single();
        Assert.Equal(1, learned["tier"]!.GetValue<int>());
        Assert.Equal("player_soul", learned["ownerActorId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(art["spiritualWoundEnvelope"], learned["spiritualWoundEnvelope"]));
        learned["spiritualWoundEnvelope"]!["maximumSeverityRank"] = 1;
        Assert.Equal(2, art["spiritualWoundEnvelope"]!["maximumSeverityRank"]!.GetValue<int>());
    }

    private static JsonObject SourceOwnerSpecialArt(string actorType, string actorId) => new()
    {
        ["artId"] = "art_source_owner",
        ["displayName"] = "Нить надлома",
        ["effectSummary"] = "Давление оставляет след в духовном узоре.",
        ["ownerActorType"] = actorType, ["ownerActorId"] = actorId,
        ["baseOperation"] = "pressure", ["tier"] = 0,
        ["costMultiplierPercent"] = 200,
        ["upgradeCost"] = new JsonObject { ["inkFeathers"] = 1 },
        ["canTeachPlayer"] = false, ["trainingConditions"] = new JsonArray()
    };
    private static JsonObject PlayerSoulProfile() => Profile("player_soul", "player_soul");
    private static JsonObject GuardianProfile(string id) => Profile("guardian", id);
    private static JsonObject Profile(string type, string id) => new()
    {
        ["actorType"] = type, ["actorId"] = id, ["displayName"] = id, ["realm"] = "Chaos Sea",
        ["resourceOwnerBindings"] = new JsonArray(new JsonObject
        {
            ["realm"] = "chaos_sea", ["resourceOwnerId"] = id, ["state"] = "active"
        })
    };
    private static JsonObject Profiles(params JsonObject[] profiles) => new()
    {
        [AfterlifeEntityProfileState.ProfilesProperty] =
            new JsonArray(profiles.Select(profile => (JsonNode)profile).ToArray())
    };
}
