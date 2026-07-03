using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TrainingServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public TrainingServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-training-service-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task EnsureTrainingAsync_MortalTeacherWithoutShowcase_CreatesPendingRefreshRequest()
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalTeacherAsync(includeShowcase: false);

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 12);

        Assert.Equal("mortal", view.Realm);
        Assert.Single(view.Teachers);
        Assert.False(view.Teachers[0].ShowcaseReady);
        Assert.True(view.RequestCreatedThisCall);
        Assert.Contains("подготовь витрину обучения", view.PendingGmAction, StringComparison.OrdinalIgnoreCase);

        var pendingRaw = await _fs.ReadFileAsync(TrainingRequestState.PendingRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"sourceActorId\": \"npc_hunter_001\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"requestKind\": \"mortal_teacher_showcase\"", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuyTrainingAsync_MortalOffer_SpendsMoneyAndCurrentLevelExperienceAndRaisesMastery()
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalTeacherAsync(includeShowcase: true);
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 400, experienceForNextLevel: 1000);
        await SeedPlayerActiveSkillAsync(skillName: "Ножи", masteryLevel: 1);

        var service = CreateService();
        var result = await service.BuyTrainingAsync("npc_hunter_001", "offer_knife_mastery_2", currentTurn: 13);

        Assert.True(result.Success);
        Assert.True(result.StateChanged);

        using var statusDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/core/player_status.json") ?? "{}");
        Assert.Equal(380, statusDoc.RootElement.GetProperty("money").GetInt32());

        using var experienceDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/player/experience.json") ?? "{}");
        Assert.Equal(150, experienceDoc.RootElement.GetProperty("currentLevelExperience").GetInt32());
        Assert.Equal(1000, experienceDoc.RootElement.GetProperty("experienceForNextLevel").GetInt32());

        using var masteryDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/player/skill_mastery.json") ?? "{}");
        var mastery = masteryDoc.RootElement.GetProperty("skillMasteryChanges").EnumerateArray().Single();
        Assert.Equal("Ножи", mastery.GetProperty("skillName").GetString());
        Assert.Equal(2, mastery.GetProperty("newMasteryLevel").GetInt32());

        using var npcDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}");
        var receipt = npcDoc.RootElement.GetProperty("trainingPurchaseReceipts")[0];
        Assert.Equal("offer_knife_mastery_2", receipt.GetProperty("offerId").GetString());
        Assert.Equal(120, receipt.GetProperty("moneySpent").GetInt32());
        Assert.Equal(250, receipt.GetProperty("currentLevelExperienceSpent").GetInt32());
    }

    [Fact]
    public async Task BuyTrainingAsync_MortalTeacherWithInitialIdOnly_UsesInitialIdAsStableSourceActor()
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalTeacherWithInitialIdOnlyAsync();
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 400, experienceForNextLevel: 1000);
        await SeedPlayerActiveSkillAsync(skillName: "Этикет", masteryLevel: 1);

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 14);

        var teacher = Assert.Single(view.Teachers);
        Assert.Equal("npc_selene_initial", teacher.SourceActorId);
        Assert.True(teacher.ShowcaseReady);

        var result = await service.BuyTrainingAsync("npc_selene_initial", "offer_etiquette_mastery_2", currentTurn: 15);

        Assert.True(result.Success);
        Assert.True(result.StateChanged);

        using var npcDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/npcs/npc_core.json") ?? "{}");
        var receipt = npcDoc.RootElement.GetProperty("trainingPurchaseReceipts")[0];
        Assert.Equal("npc_selene_initial", receipt.GetProperty("sourceActorId").GetString());
        Assert.Equal("offer_etiquette_mastery_2", receipt.GetProperty("offerId").GetString());
    }

    [Fact]
    public async Task BuyTrainingAsync_MortalOffer_BlocksWhenCurrentLevelExperienceWouldGoNegative()
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalTeacherAsync(includeShowcase: true);
        await SeedMortalPlayerProgressAsync(money: 500, currentLevelExperience: 100, experienceForNextLevel: 1000);
        await SeedPlayerActiveSkillAsync(skillName: "Ножи", masteryLevel: 1);

        var service = CreateService();
        var beforeStatus = await _fs.ReadFileAsync("game_state/core/player_status.json");
        var beforeExperience = await _fs.ReadFileAsync("game_state/player/experience.json");

        var result = await service.BuyTrainingAsync("npc_hunter_001", "offer_knife_mastery_2", currentTurn: 13);

        Assert.False(result.Success);
        Assert.False(result.StateChanged);
        Assert.Contains("опыта текущего уровня", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(beforeStatus, await _fs.ReadFileAsync("game_state/core/player_status.json"));
        Assert.Equal(beforeExperience, await _fs.ReadFileAsync("game_state/player/experience.json"));
    }

    [Fact]
    public async Task EnsureTrainingAsync_AfterlifeSelfTraining_UsesExpensiveFallbackMultipliersAndBlocksNewSpecialUnlock()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "availability": "active",
          "lightSparks": 50,
          "radiance": { "tier": 0, "experience": 0 }
        }
        """);

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 21);

        var pressure = Assert.Single(view.SelfTrainingOffers, offer => offer.OfferId == "self_art_pressure_tier_1");
        Assert.Equal(500, pressure.Cost.InkFeathers);
        Assert.True(pressure.Available);

        var focus = Assert.Single(view.SelfTrainingOffers, offer => offer.OfferId == "self_spirit_focus_tier_2");
        Assert.Equal(900, focus.Cost.InkFeathers);
        Assert.True(focus.Available);

        var lockedSpecial = Assert.Single(view.SelfTrainingOffers, offer => offer.TargetId == "special_art_unlearned_shadow_chain");
        Assert.False(lockedSpecial.Available);
        Assert.Contains("нельзя открыть самостоятельно", lockedSpecial.BlockReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuyTrainingAsync_AfterlifeSelfTraining_SpendsExpensiveFallbackCostAndRaisesArtTier()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);

        var service = CreateService();
        var result = await service.BuyTrainingAsync("self", "self_art_pressure_tier_1", currentTurn: 22);

        Assert.True(result.Success);
        Assert.True(result.StateChanged);

        using var soulDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json") ?? "{}");
        var root = soulDoc.RootElement;
        Assert.Equal(2000, root.GetProperty("inkFeathers").GetProperty("current").GetInt32());
        Assert.Equal(1, root.GetProperty("afterlifeCombatProfile").GetProperty("artTiers").GetProperty("pressure").GetInt32());

        var receipt = root.GetProperty("afterlifeTrainingPurchaseReceipts")[0];
        Assert.Equal("self_art_pressure_tier_1", receipt.GetProperty("offerId").GetString());
        Assert.Equal(500, receipt.GetProperty("inkFeathersSpent").GetInt32());
        Assert.Equal("self_fallback", receipt.GetProperty("sourceActorKind").GetString());
    }

    [Fact]
    public async Task EnsureTrainingAsync_AfterlifeMentorShowcase_ReturnsFreshDiscountedOffers()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        await SeedAfterlifeMentorAsync(includeShowcase: true);

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 31);

        var teacher = Assert.Single(view.Teachers);
        Assert.Equal("guardian_liora", teacher.SourceActorId);
        Assert.True(teacher.ShowcaseReady);
        Assert.False(teacher.ShowcaseStale);

        var offer = Assert.Single(teacher.Offers);
        Assert.Equal("mentor_liora_guard_2", offer.OfferId);
        Assert.Equal("guard", offer.TargetId);
        Assert.Equal(105, offer.Cost.InkFeathers);
        Assert.Equal(60, offer.Details["mentorPriceMultiplierPercent"]?.GetValue<int>());
        Assert.True(offer.Available);
    }

    [Fact]
    public async Task EnsureTrainingAsync_AfterlifeMentorFreshShowcase_ClearsSatisfiedPendingRequest()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        await SeedAfterlifeMentorAsync(includeShowcase: true);
        await TrainingRequestState.WriteRequestAsync(
            _fs,
            "afterlife_teacher_showcase",
            "guardian_liora",
            "Лиора, Хранительница Тихого Света",
            "afterlife_mentor",
            "afterlife",
            createdAtTurn: 12,
            sourceActorSnapshotHash: "older-request-hash",
            reason: "missing_showcase");

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 31);

        Assert.False(view.RequestPending);
        Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task EnsureTrainingAsync_AfterlifeTeachableSpecialArtWithoutShowcase_CreatesPendingMentorRefreshRequest()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 0);
        await SeedAfterlifeMentorWithTeachableSpecialArtOnlyAsync();

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 41);

        var teacher = Assert.Single(view.Teachers);
        Assert.Equal("guard_system_myriel_001", teacher.SourceActorId);
        Assert.Equal("Мириэль Пепельная Звезда", teacher.SourceActorName);
        Assert.False(teacher.ShowcaseReady);
        Assert.Equal("ГМ ещё не подготовил витрину наставника.", teacher.BlockReason);
        Assert.True(view.RequestCreatedThisCall);
        Assert.True(view.RequestPending);
        Assert.Contains("витрину обучения для наставника посмертия", view.PendingGmAction, StringComparison.OrdinalIgnoreCase);

        var pendingRaw = await _fs.ReadFileAsync(TrainingRequestState.PendingRequestPath);
        Assert.NotNull(pendingRaw);
        Assert.Contains("\"requestKind\": \"afterlife_teacher_showcase\"", pendingRaw, StringComparison.Ordinal);
        Assert.Contains("\"sourceActorId\": \"guard_system_myriel_001\"", pendingRaw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnsureTrainingAsync_MortalTeacherFreshShowcase_ClearsSatisfiedPendingRequest()
    {
        await SeedMortalSoulStateAsync();
        await SeedMortalTeacherAsync(includeShowcase: true);
        await TrainingRequestState.WriteRequestAsync(
            _fs,
            "mortal_teacher_showcase",
            "npc_hunter_001",
            "Старый охотник",
            "npc_teacher",
            "mortal",
            createdAtTurn: 8,
            sourceActorSnapshotHash: "older-request-hash",
            reason: "missing_showcase");

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 12);

        Assert.False(view.RequestPending);
        Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task EnsureTrainingAsync_AfterlifeMentorShowcase_AppliesGoodRelationshipDiscount()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        await SeedAfterlifeMentorAsync(includeShowcase: true, relationshipLevel: 35, offerInkFeathers: 999);

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 31);

        var teacher = Assert.Single(view.Teachers);
        var offer = Assert.Single(teacher.Offers);
        Assert.Equal(140, offer.Cost.InkFeathers);
        Assert.Equal(80, offer.Details["mentorPriceMultiplierPercent"]?.GetValue<int>());
        Assert.Equal(175, offer.Details["baseInkFeatherCost"]?.GetValue<int>());
    }

    [Fact]
    public async Task EnsureTrainingAsync_AfterlifeMentorShowcase_NormalizesNaturalGmOfferShape()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        await SeedAfterlifeMentorWithNaturalShowcaseShapeAsync();

        var service = CreateService();
        var view = await service.EnsureTrainingAsync(currentTurn: 33);

        var teacher = Assert.Single(view.Teachers);
        var offer = Assert.Single(teacher.Offers);
        Assert.Equal("Защита", offer.TargetName);
        Assert.Equal("standard_spiritual_art", offer.TargetKind);
        Assert.Equal(75, offer.Cost.InkFeathers);
        Assert.True(offer.Available);
    }

    [Fact]
    public async Task BuyTrainingAsync_AfterlifeMentorOffer_SpendsCurrencyAndRaisesArtTier()
    {
        await SeedAfterlifeSoulStateAsync(inkFeathers: 2500);
        await SeedAfterlifeMentorAsync(includeShowcase: true);

        var service = CreateService();
        var result = await service.BuyTrainingAsync("guardian_liora", "mentor_liora_guard_2", currentTurn: 32);

        Assert.True(result.Success);
        Assert.True(result.StateChanged);

        using var soulDoc = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/meta/soul_state.json") ?? "{}");
        var soul = soulDoc.RootElement;
        Assert.Equal(2395, soul.GetProperty("inkFeathers").GetProperty("current").GetInt32());
        Assert.Equal(2, soul.GetProperty("afterlifeCombatProfile").GetProperty("artTiers").GetProperty("guard").GetInt32());

        var receipt = soul.GetProperty("afterlifeTrainingPurchaseReceipts")[0];
        Assert.Equal("mentor_liora_guard_2", receipt.GetProperty("offerId").GetString());
        Assert.Equal("guardian_liora", receipt.GetProperty("sourceActorId").GetString());
        Assert.Equal("afterlife_mentor", receipt.GetProperty("sourceActorKind").GetString());
        Assert.Equal(105, receipt.GetProperty("inkFeathersSpent").GetInt32());
    }

    private TrainingService CreateService() =>
        new(_fs, NullLogger<TrainingService>.Instance);

    private async Task SeedMortalSoulStateAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal Realm",
          "currentIncarnation": 2
        }
        """);
    }

    private async Task SeedAfterlifeSoulStateAsync(int inkFeathers)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "inkFeathers": { "current": {{inkFeathers}}, "total": {{inkFeathers}} },
          "afterlifeCombatProfile": {
            "enlightenmentRank": 3,
            "radianceRank": 0,
            "retainedRadianceRank": 0,
            "spiritFocusTier": 1,
            "artTiers": {},
            "specialArts": [
              {
                "artId": "special_art_unlearned_shadow_chain",
                "displayName": "Теневая цепь",
                "baseOperation": "binding",
                "tier": 0,
                "upgradeCost": { "inkFeathers": 200, "lightSparks": 0 }
              }
            ]
          }
        }
        """);
    }

    private async Task SeedMortalTeacherAsync(bool includeShowcase)
    {
        var teacher = new JsonObject
        {
            ["npcId"] = "npc_hunter_001",
            ["name"] = "Старый охотник",
            ["currentLocationId"] = "forest_lodge",
            ["teacherProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 45,
                ["skills"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["skillId"] = "skill_knife",
                        ["skillName"] = "Ножи",
                        ["skillKind"] = "active",
                        ["masteryLevel"] = 3
                    }
                }
            }
        };

        if (includeShowcase)
        {
            var snapshotHash = TrainingService.ComputeSourceSnapshotHash(teacher);
            teacher["trainingShowcase"] = new JsonObject
            {
                ["showcaseId"] = "showcase_hunter_001",
                ["revision"] = 1,
                ["sourceActorId"] = "npc_hunter_001",
                ["sourceActorName"] = "Старый охотник",
                ["sourceActorSnapshotHash"] = snapshotHash,
                ["offers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["offerId"] = "offer_knife_mastery_2",
                        ["targetId"] = "skill_knife",
                        ["targetName"] = "Ножи",
                        ["targetKind"] = "active_skill_mastery",
                        ["currentValue"] = 1,
                        ["targetValue"] = 2,
                        ["sourceCap"] = 3,
                        ["cost"] = new JsonObject
                        {
                            ["money"] = 120,
                            ["currentLevelExperiencePercent"] = 25
                        },
                        ["requirements"] = new JsonObject
                        {
                            ["minimumRelationship"] = 20
                        },
                        ["summary"] = "Охотник учит короткому выпаду ножом."
                    }
                }
            };
        }

        var root = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(teacher)
        };
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", root.ToJsonString());
    }

    private async Task SeedMortalTeacherWithInitialIdOnlyAsync()
    {
        var teacher = new JsonObject
        {
            ["initialId"] = "npc_selene_initial",
            ["name"] = "Магистра Селена",
            ["currentLocationId"] = "academy_hall",
            ["teacherProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 50,
                ["skills"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["skillId"] = "etiquette",
                        ["skillName"] = "Этикет",
                        ["skillKind"] = "passive",
                        ["masteryLevel"] = 3
                    }
                }
            }
        };

        var snapshotHash = TrainingService.ComputeSourceSnapshotHash(teacher);
        teacher["trainingShowcase"] = new JsonObject
        {
            ["showcaseId"] = "showcase_selene_initial",
            ["revision"] = 1,
            ["sourceActorId"] = "npc_selene_initial",
            ["sourceActorName"] = "Магистра Селена",
            ["sourceActorSnapshotHash"] = snapshotHash,
            ["offers"] = new JsonArray
            {
                new JsonObject
                {
                    ["offerId"] = "offer_etiquette_mastery_2",
                    ["targetId"] = "etiquette",
                    ["targetName"] = "Этикет",
                    ["targetKind"] = "passive_skill_mastery",
                    ["currentValue"] = 1,
                    ["targetValue"] = 2,
                    ["sourceCap"] = 3,
                    ["cost"] = new JsonObject
                    {
                        ["money"] = 90,
                        ["currentLevelExperiencePercent"] = 10
                    },
                    ["requirements"] = new JsonObject
                    {
                        ["minimumRelationship"] = 20
                    },
                    ["summary"] = "Селена учит держаться при дворе без лишних слов."
                }
            }
        };

        var root = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(teacher)
        };
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", root.ToJsonString());
    }

    private async Task SeedAfterlifeMentorAsync(
        bool includeShowcase,
        int relationshipLevel = 62,
        int offerInkFeathers = 180)
    {
        var mentor = new JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = "guardian_liora",
            ["displayName"] = "Лиора, Хранительница Тихого Света",
            ["mentorProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = relationshipLevel,
                ["summary"] = "Лиора учит защите только души, доказавшие доверие."
            },
            ["standardArts"] = new JsonObject
            {
                ["guard"] = 4,
                ["pressure"] = 2
            },
            ["relationships"] = new JsonArray
            {
                new JsonObject
                {
                    ["axis"] = "trust",
                    ["value"] = relationshipLevel,
                    ["summary"] = "Лиора доверяет душе после защиты обители."
                }
            }
        };

        if (includeShowcase)
        {
            var snapshotHash = TrainingService.ComputeSourceSnapshotHash(mentor);
            mentor["mentorTrainingShowcase"] = new JsonObject
            {
                ["showcaseId"] = "mentor_showcase_liora_001",
                ["requestKind"] = "afterlife_teacher_showcase",
                ["sourceActorId"] = "guardian_liora",
                ["sourceActorName"] = "Лиора, Хранительница Тихого Света",
                ["sourceActorSnapshotHash"] = snapshotHash,
                ["offers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["offerId"] = "mentor_liora_guard_2",
                        ["targetKind"] = "standard_spiritual_art",
                        ["targetId"] = "guard",
                        ["targetName"] = "Защита",
                        ["currentValue"] = 1,
                        ["targetValue"] = 2,
                        ["sourceCap"] = 4,
                        ["cost"] = new JsonObject
                        {
                            ["inkFeathers"] = offerInkFeathers,
                            ["lightSparks"] = 0
                        },
                        ["requirements"] = new JsonObject
                        {
                            ["minimumRelationship"] = 50,
                            ["maxPlayerUnlockedTier"] = 2
                        },
                        ["summary"] = "Лиора показывает, как принять удар поворотом света."
                    }
                }
            };
        }

        var root = new JsonObject
        {
            ["profiles"] = new JsonArray(mentor)
        };
        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", root.ToJsonString());
    }

    private async Task SeedAfterlifeMentorWithNaturalShowcaseShapeAsync()
    {
        var mentor = new JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = "guardian_myriel",
            ["displayName"] = "Мириэль Пепельная Звезда",
            ["mentorProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 62
            },
            ["standardArts"] = new JsonObject
            {
                ["guard"] = 3
            }
        };

        var snapshotHash = TrainingService.ComputeSourceSnapshotHash(mentor);
        mentor["mentorTrainingShowcase"] = new JsonObject
        {
            ["showcaseId"] = "mentor_showcase_myriel_natural_shape",
            ["requestKind"] = "afterlife_teacher_showcase",
            ["sourceActorId"] = "guardian_myriel",
            ["sourceActorName"] = "Мириэль Пепельная Звезда",
            ["sourceActorSnapshotHash"] = snapshotHash,
            ["offers"] = new JsonArray
            {
                new JsonObject
                {
                    ["offerId"] = "myriel_guard_tier_1",
                    ["targetKind"] = "standard_art",
                    ["targetId"] = "guard",
                    ["displayName"] = "Защита",
                    ["targetValue"] = 1,
                    ["sourceCap"] = 3,
                    ["cost"] = new JsonObject
                    {
                        ["currency"] = "inkFeathers",
                        ["amount"] = 8
                    },
                    ["requirements"] = new JsonObject
                    {
                        ["minimumRelationship"] = 50,
                        ["maxPlayerUnlockedTier"] = 1
                    },
                    ["summary"] = "Базовая защитная стойка для удержания давления."
                }
            }
        };

        var root = new JsonObject
        {
            ["profiles"] = new JsonArray(mentor)
        };
        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", root.ToJsonString());
    }

    private async Task SeedAfterlifeMentorWithTeachableSpecialArtOnlyAsync()
    {
        var mentor = new JsonObject
        {
            ["actorType"] = "guardian",
            ["actorId"] = "guard_system_myriel_001",
            ["displayName"] = "Мириэль Пепельная Звезда",
            ["realm"] = "Chaos Sea",
            ["standardArts"] = new JsonObject
            {
                ["guard"] = 2,
                ["maneuver"] = 1
            },
            ["specialArts"] = new JsonArray
            {
                new JsonObject
                {
                    ["artId"] = "myriel_ash_star_ward",
                    ["displayName"] = "Оберег Пепельной Звезды",
                    ["ownerActorType"] = "guardian",
                    ["ownerActorId"] = "guard_system_myriel_001",
                    ["baseOperation"] = "guard",
                    ["tier"] = 1,
                    ["costMultiplierPercent"] = 150,
                    ["upgradeCost"] = new JsonObject
                    {
                        ["inkFeathers"] = 35,
                        ["lightSparks"] = 0
                    },
                    ["effectSummary"] = "Особая защита Мириэль: пепельные звезды удерживают давление.",
                    ["canTeachPlayer"] = true,
                    ["trainingConditions"] = new JsonArray
                    {
                        "Провести отдельную сцену обучения с Мириэль."
                    }
                }
            }
        };

        var root = new JsonObject
        {
            ["profiles"] = new JsonArray(mentor)
        };
        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", root.ToJsonString());
    }

    private async Task SeedMortalPlayerProgressAsync(int money, int currentLevelExperience, int experienceForNextLevel)
    {
        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", $$"""
        {
          "money": {{money}}
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", $$"""
        {
          "level": 4,
          "currentLevelExperience": {{currentLevelExperience}},
          "experienceForNextLevel": {{experienceForNextLevel}}
        }
        """);
    }

    private async Task SeedPlayerActiveSkillAsync(string skillName, int masteryLevel)
    {
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", $$"""
        {
          "activeSkillChanges": [
            {
              "skillName": "{{skillName}}",
              "skillDescription": "Ближний бой коротким клинком.",
              "category": "Combat",
              "rarity": "Common",
              "currentMasteryLevel": {{masteryLevel}},
              "maxMasteryLevel": 5
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", """
        {
          "passiveSkillChanges": []
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        {
          "skillMasteryChanges": []
        }
        """);
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
