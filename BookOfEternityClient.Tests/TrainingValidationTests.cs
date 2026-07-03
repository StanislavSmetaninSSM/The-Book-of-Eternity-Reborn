using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TrainingValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public TrainingValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-training-validation-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_AfterlifeMentorShowcaseWithStaleHash_ReportsTrainingIssue()
    {
        await WriteAfterlifeMentorProfileAsync(sourceActorSnapshotHash: "stale-hash", sourceCap: 4);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "training_showcase_stale_source_actor_snapshot", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("afterlife_entity_profiles.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AfterlifeMentorShowcaseAboveSourceCap_ReportsTrainingIssue()
    {
        var mentorWithoutShowcase = BuildAfterlifeMentorProfile(sourceActorSnapshotHash: null, sourceCap: 6, includeShowcase: false);
        var snapshotHash = TrainingService.ComputeSourceSnapshotHash(mentorWithoutShowcase);
        await WriteAfterlifeMentorProfileAsync(sourceActorSnapshotHash: snapshotHash, sourceCap: 6);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "training_showcase_source_cap_exceeds_actor_cap", StringComparison.OrdinalIgnoreCase) &&
            issue.Message.Contains("sourceCap", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_AfterlifeMentorShowcaseWrongRealmAndSourceActor_ReportsRepairableIssues()
    {
        var mentor = BuildAfterlifeMentorProfile(sourceActorSnapshotHash: null, sourceCap: 4, includeShowcase: true);
        var showcase = mentor["mentorTrainingShowcase"]!.AsObject();
        showcase["realm"] = "mortal_world";
        showcase["sourceActorId"] = "missing_guardian";
        await WriteAfterlifeMentorProfileObjectAsync(mentor);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "training_showcase_wrong_realm", StringComparison.OrdinalIgnoreCase) &&
            issue.RepairHint?.Contains("realm", StringComparison.OrdinalIgnoreCase) == true);
        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "training_showcase_source_actor_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.RepairHint?.Contains("sourceActorId", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_DuplicateTrainingOfferId_ReportsTrainingIssue()
    {
        var mentor = BuildAfterlifeMentorProfile(sourceActorSnapshotHash: null, sourceCap: 4, includeShowcase: true);
        var offers = mentor["mentorTrainingShowcase"]!.AsObject()["offers"]!.AsArray();
        offers.Add(new JsonObject
        {
            ["offerId"] = "mentor_liora_guard_2",
            ["targetKind"] = "standard_spiritual_art",
            ["targetId"] = "guard",
            ["targetName"] = "Защита",
            ["currentValue"] = 2,
            ["targetValue"] = 3,
            ["sourceCap"] = 4,
            ["cost"] = new JsonObject { ["inkFeathers"] = 120 }
        });
        await WriteAfterlifeMentorProfileObjectAsync(mentor);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "training_showcase_duplicate_offer_id", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("offers[1].offerId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalTrainingReceiptWithResourceMismatch_ReportsTrainingIssue()
    {
        await WriteMortalTeacherWithReceiptAsync(moneySpent: 999, experiencePercent: 15);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "training_purchase_receipt_resource_mismatch", StringComparison.OrdinalIgnoreCase) &&
            issue.Message.Contains("ресурс", StringComparison.OrdinalIgnoreCase) &&
            issue.RepairHint?.Contains("receipt", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MortalTrainingReceiptWithInitialIdTeacher_ResolvesSourceActor()
    {
        await WriteMortalInitialIdTeacherWithReceiptAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "training_purchase_receipt_missing_source_actor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "training_showcase_source_actor_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteAfterlifeMentorProfileAsync(string? sourceActorSnapshotHash, int sourceCap)
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulName": "Тестовая душа",
              "inkFeathers": { "current": 1000, "total": 1000 },
              "afterlifeCombatProfile": {
                "enlightenmentRank": 3,
                "radianceRank": 0,
                "retainedRadianceRank": 0,
                "spiritFocusTier": 1,
                "artTiers": { "guard": 1 },
                "specialArts": []
              }
            }
            """);

        await WriteAfterlifeMentorProfileObjectAsync(BuildAfterlifeMentorProfile(sourceActorSnapshotHash, sourceCap, includeShowcase: true));
    }

    private async Task WriteAfterlifeMentorProfileObjectAsync(JsonObject profile)
    {
        await WriteAfterlifeSoulStateAsync();
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/afterlife_entity_profiles.json",
            new JsonObject
            {
                ["profiles"] = new JsonArray(profile)
            }.ToJsonString());
    }

    private async Task WriteAfterlifeSoulStateAsync()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "currentRealm": "Chaos Sea",
              "soulName": "Тестовая душа",
              "inkFeathers": { "current": 1000, "total": 1000 },
              "afterlifeCombatProfile": {
                "enlightenmentRank": 3,
                "radianceRank": 0,
                "retainedRadianceRank": 0,
                "spiritFocusTier": 1,
                "artTiers": { "guard": 1 },
                "specialArts": []
              }
            }
            """);
    }

    private static System.Text.Json.Nodes.JsonObject BuildAfterlifeMentorProfile(
        string? sourceActorSnapshotHash,
        int sourceCap,
        bool includeShowcase)
    {
        var mentor = new System.Text.Json.Nodes.JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = "guardian_liora",
            ["displayName"] = "Лиора, Хранительница Тихого Света",
            ["realm"] = "Chaos Sea",
            ["locationName"] = "Светлая кромка Моря Хаоса",
            ["currencies"] = new System.Text.Json.Nodes.JsonObject
            {
                ["inkFeathers"] = 120,
                ["lightSparks"] = 0
            },
            ["progression"] = new System.Text.Json.Nodes.JsonObject
            {
                ["enlightenment"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["experience"] = 48,
                    ["tier"] = 4
                },
                ["radiance"] = new System.Text.Json.Nodes.JsonObject
                {
                    ["experience"] = 0,
                    ["tier"] = 0
                }
            },
            ["mentorProfile"] = new System.Text.Json.Nodes.JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 62
            },
            ["standardArts"] = new System.Text.Json.Nodes.JsonObject
            {
                ["guard"] = 4
            },
            ["specialArts"] = new System.Text.Json.Nodes.JsonArray(),
            ["soulDissipationTier"] = 1,
            ["progressionStrategy"] = new System.Text.Json.Nodes.JsonObject
            {
                ["strategyId"] = "strategy_guardian_liora",
                ["summary"] = "Сначала укрепляет защиту.",
                ["priorityOrder"] = new System.Text.Json.Nodes.JsonArray("guard")
            },
            ["ledger"] = new System.Text.Json.Nodes.JsonArray()
        };

        if (includeShowcase)
        {
            mentor["mentorTrainingShowcase"] = new System.Text.Json.Nodes.JsonObject
            {
                ["requestKind"] = "afterlife_teacher_showcase",
                ["sourceActorId"] = "guardian_liora",
                ["sourceActorSnapshotHash"] = sourceActorSnapshotHash ?? TrainingService.ComputeSourceSnapshotHash(mentor),
                ["offers"] = new System.Text.Json.Nodes.JsonArray
                {
                    new System.Text.Json.Nodes.JsonObject
                    {
                        ["offerId"] = "mentor_liora_guard_2",
                        ["targetKind"] = "standard_spiritual_art",
                        ["targetId"] = "guard",
                        ["targetName"] = "Защита",
                        ["currentValue"] = 1,
                        ["targetValue"] = 2,
                        ["sourceCap"] = sourceCap,
                        ["cost"] = new System.Text.Json.Nodes.JsonObject
                        {
                            ["inkFeathers"] = 105
                        }
                    }
                }
            };
        }

        return mentor;
    }

    private async Task WriteMortalTeacherWithReceiptAsync(int moneySpent, int experiencePercent)
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "currentRealm": "Eternia",
              "soulName": "Тестовая душа"
            }
            """);
        await _fs.WriteFileAtomicAsync(
            "game_state/core/player_status.json",
            """
            {
              "name": "Асуран",
              "level": 3,
              "money": 500,
              "health": 100,
              "energy": 100,
              "balance": 100,
              "healthPercentage": 100,
              "energyPercentage": 100,
              "poisePercentage": 100
            }
            """);

        var teacher = new JsonObject
        {
            ["npcId"] = "npc_teacher_reina",
            ["name"] = "Рейна Быстрый Нож",
            ["teacherProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 45,
                ["skills"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["skillId"] = "knife",
                        ["skillName"] = "Ножевой бой",
                        ["masteryLevel"] = 3
                    }
                }
            }
        };
        teacher["trainingShowcase"] = new JsonObject
        {
            ["realm"] = "mortal_world",
            ["sourceActorId"] = "npc_teacher_reina",
            ["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(teacher),
            ["offers"] = new JsonArray
            {
                new JsonObject
                {
                    ["offerId"] = "offer_knife_mastery_2",
                    ["targetKind"] = "active_skill_mastery",
                    ["targetId"] = "knife",
                    ["targetName"] = "Ножевой бой",
                    ["currentValue"] = 1,
                    ["targetValue"] = 2,
                    ["sourceCap"] = 3,
                    ["cost"] = new JsonObject
                    {
                        ["money"] = 120,
                        ["currentLevelExperiencePercent"] = 15
                    }
                }
            }
        };

        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(teacher),
                ["trainingPurchaseReceipts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["receiptId"] = "receipt_bad_money",
                        ["realm"] = "mortal",
                        ["sourceActorId"] = "npc_teacher_reina",
                        ["offerId"] = "offer_knife_mastery_2",
                        ["targetKind"] = "active_skill_mastery",
                        ["targetId"] = "knife",
                        ["targetValue"] = 2,
                        ["sourceCap"] = 3,
                        ["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(teacher),
                        ["moneySpent"] = moneySpent,
                        ["currentLevelExperiencePercent"] = experiencePercent,
                        ["currentLevelExperienceSpent"] = 120
                    }
                }
            }.ToJsonString());
    }

    private async Task WriteMortalInitialIdTeacherWithReceiptAsync()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/soul_state.json",
            """
            {
              "currentRealm": "Eternia",
              "soulName": "Тестовая душа"
            }
            """);
        await _fs.WriteFileAtomicAsync(
            "game_state/core/player_status.json",
            """
            {
              "name": "Асуран",
              "level": 3,
              "money": 500,
              "health": 100,
              "energy": 100,
              "balance": 100,
              "healthPercentage": 100,
              "energyPercentage": 100,
              "poisePercentage": 100
            }
            """);

        var teacher = new JsonObject
        {
            ["initialId"] = "npc_teacher_selene",
            ["name"] = "Магистра Селена",
            ["teacherProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 45,
                ["skills"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["skillId"] = "etiquette",
                        ["skillName"] = "Этикет",
                        ["masteryLevel"] = 3
                    }
                }
            }
        };
        teacher["trainingShowcase"] = new JsonObject
        {
            ["realm"] = "mortal_world",
            ["sourceActorId"] = "npc_teacher_selene",
            ["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(teacher),
            ["offers"] = new JsonArray
            {
                new JsonObject
                {
                    ["offerId"] = "offer_etiquette_mastery_2",
                    ["targetKind"] = "passive_skill_mastery",
                    ["targetId"] = "etiquette",
                    ["targetName"] = "Этикет",
                    ["currentValue"] = 1,
                    ["targetValue"] = 2,
                    ["sourceCap"] = 3,
                    ["cost"] = new JsonObject
                    {
                        ["money"] = 120,
                        ["currentLevelExperiencePercent"] = 15
                    }
                }
            }
        };

        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(teacher),
                ["trainingPurchaseReceipts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["receiptId"] = "receipt_initial_id",
                        ["realm"] = "mortal",
                        ["sourceActorId"] = "npc_teacher_selene",
                        ["offerId"] = "offer_etiquette_mastery_2",
                        ["targetKind"] = "passive_skill_mastery",
                        ["targetId"] = "etiquette",
                        ["targetValue"] = 2,
                        ["sourceCap"] = 3,
                        ["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(teacher),
                        ["moneySpent"] = 120,
                        ["currentLevelExperiencePercent"] = 15,
                        ["currentLevelExperienceSpent"] = 150
                    }
                }
            }.ToJsonString());
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
        }
    }
}
