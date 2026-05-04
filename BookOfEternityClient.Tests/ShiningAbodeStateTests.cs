using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningAbodeStateTests
{
    [Fact]
    public void NormalizeStateRoot_DerivesRadianceTierAndFactionStrength()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 240,
            "tier": 0
          },
          "lightSparks": 145,
          "halls": [
            {
              "hallId": "hall_dawn",
              "hallName": "Зал Рассвета",
              "description": "Светлый зал",
              "serviceTags": ["social", "invalid"]
            }
          ],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Поют утренний свет."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "investCountThisAscension": 2,
              "projects": [
                {
                  "projectId": "project_dawn_song",
                  "displayName": "Песнь зари",
                  "summary": "Укрепляет союз.",
                  "toneTags": ["bright"],
                  "targetFactionIds": [],
                  "projectArchetype": "accord",
                  "outputEffectFamily": "social",
                  "tier": 2,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 0
                }
              ]
            }
          ],
          "gates": {
            "draftVersion": 1,
            "hasOpenDraft": false,
            "isStale": true,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": ["card_a"],
            "selectedBlessingCardIds": ["card_a"],
            "nextCandidateCursor": 3,
            "rerollsRemaining": 2
          }
        }
        """)!.AsObject();

        var residentRoot = JsonNode.Parse("""
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_dawn"
            },
            {
              "residentId": "resident_mael",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_dawn"
            }
          ]
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, residentRoot);

        Assert.Equal(2, root["radiance"]?["tier"]?.GetValue<int>());
        Assert.Equal(100, root["lightSparks"]?.GetValue<int>());
        Assert.True(root["halls"]?[0]?["serviceTags"] is JsonArray tags && tags.Count == 1);
        Assert.Equal(35, root["factions"]?[0]?["baseStrength"]?.GetValue<int>());
        Assert.Equal(69, root["factions"]?[0]?["factionStrength"]?.GetValue<int>());
    }

    [Fact]
    public void NormalizeStateRoot_PreservesMalformedLegacyPendingNativeFactionDiscoveryForRepair()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 40, "tier": 0 },
          "lightSparks": 50,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "pendingNativeFactionDiscovery": "malformed_contract"
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null);

        Assert.Equal("malformed_contract", root["pendingNativeFactionDiscovery"]?.GetValue<string>());

        root["pendingNativeFactionDiscovery"] = new JsonArray("malformed_contract");

        ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null);

        Assert.True(root["pendingNativeFactionDiscovery"] is JsonArray);
    }

    [Fact]
    public void NormalizeStateRoot_PreservesUnsupportedEnumBackedFactionAndProjectValues()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "factions": [
            {
              "factionId": "faction_invalid",
              "originType": "broken_origin",
              "hallId": "hall_invalid",
              "charter": {
                "factionName": "Испорченная фракция",
                "favoredArchetype": "broken_archetype",
                "patronEffectFamily": "broken_family",
                "summary": "Неправильные enum-backed поля должны остаться видимыми для validation."
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_old",
                "leadershipState": "secure"
              },
              "projects": [
                {
                  "projectId": "project_invalid",
                  "displayName": "Ломанный проект",
                  "summary": "Остаётся как есть до validation.",
                  "toneTags": ["broken"],
                  "targetFactionIds": [],
                  "projectArchetype": "broken_project_archetype",
                  "outputEffectFamily": "broken_output_family",
                  "tier": 2,
                  "status": "broken_status",
                  "isSupported": true,
                  "strengthReward": 0
                }
              ]
            }
          ]
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null, guardiansRoot: null);

        var faction = root["factions"]![0]!.AsObject();
        var project = faction["projects"]![0]!.AsObject();
        Assert.Equal("broken_origin", faction["originType"]?.GetValue<string>());
        Assert.Equal("broken_archetype", faction["charter"]?["favoredArchetype"]?.GetValue<string>());
        Assert.Equal("broken_family", faction["charter"]?["patronEffectFamily"]?.GetValue<string>());
        Assert.Equal("broken_project_archetype", project["projectArchetype"]?.GetValue<string>());
        Assert.Equal("broken_output_family", project["outputEffectFamily"]?.GetValue<string>());
        Assert.Equal("broken_status", project["status"]?.GetValue<string>());
    }

    [Fact]
    public void ValidateRawOwnerStateForActionableMode_InvalidBlessingCardContract_FailsClosed()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "pendingNativeFactionDiscovery": null,
          "gates": {
            "draftVersion": 1,
            "hasOpenDraft": true,
            "isStale": false,
            "allCandidateBlessingCards": [
              {
                "cardId": "card_broken",
                "sourceType": "broken_source",
                "effectFamily": "social",
                "rarity": "rare",
                "displayName": "Ломанная карта",
                "displaySummary": "Повреждённый контракт",
                "effectPayload": {}
              }
            ],
            "availableBlessingCards": [],
            "shownBlessingCardIds": ["card_broken"],
            "selectedBlessingCardIds": []
          },
          "preparedIncarnationPackage": null,
          "gachaSystem": {
            "chargesPerReturn": 1,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        var error = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(root);

        Assert.NotNull(error);
        Assert.Contains("blessing-card", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRawOwnerStateForActionableMode_InvalidPoliticalContract_FailsClosed()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 120, "tier": 1 },
          "lightSparks": 40,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_broken",
              "originType": "player_founded",
              "hallId": "hall_broken",
              "charter": {
                "factionName": "Сломанная фракция",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Тест."
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_old",
                "leadershipState": "broken_state"
              },
              "projects": []
            }
          ],
          "shiningPoliticalActors": [],
          "pendingNativeFactionDiscovery": null,
          "gates": {
            "draftVersion": 1,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": []
          },
          "preparedIncarnationPackage": null,
          "gachaSystem": {
            "chargesPerReturn": 1,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        var error = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(root);

        Assert.NotNull(error);
        Assert.Contains("leadershipState", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRawOwnerStateForActionableMode_MalformedLegacyPendingDiscovery_FailsClosed()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["pendingNativeFactionDiscovery"] = "malformed_contract";

        var error = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(root);
        var shapeError = ShiningAbodeState.ValidateLegacyPendingNativeFactionDiscoveryShape(root);

        Assert.NotNull(error);
        Assert.NotNull(shapeError);
        Assert.Contains("pendingNativeFactionDiscovery", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("повреж", shapeError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRawOwnerStateForActionableMode_DuplicateHallIds_FailsClosed()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["halls"] = new JsonArray(
            new JsonObject
            {
                ["hallId"] = "hall_dawn",
                ["hallName"] = "Зал Рассвета",
                ["description"] = "Первый зал."
            },
            new JsonObject
            {
                ["hallId"] = "HALL_DAWN",
                ["hallName"] = "Дубликат Рассвета",
                ["description"] = "Дубликат не должен authorise actions."
            });

        var error = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(root);

        Assert.NotNull(error);
        Assert.Contains("duplicate hallId", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRawOwnerStateForActionableMode_DuplicatePoliticalActorIds_FailsClosed()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["shiningPoliticalActors"] = new JsonArray(
            new JsonObject
            {
                ["actorId"] = "radiant_actor_dawn",
                ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["displayName"] = "Глашатай Рассвета",
                ["politicalStatus"] = ShiningAbodeState.PoliticalStatusElder
            },
            new JsonObject
            {
                ["actorId"] = "RADIANT_ACTOR_DAWN",
                ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["displayName"] = "Дубликат Глашатая",
                ["politicalStatus"] = ShiningAbodeState.PoliticalStatusClaimant
            });

        var error = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(root);

        Assert.NotNull(error);
        Assert.Contains("duplicate actorId", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRawOwnerStateForActionableMode_UniqueHallAndActorIds_Passes()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["halls"] = new JsonArray(
            new JsonObject
            {
                ["hallId"] = "hall_dawn",
                ["hallName"] = "Зал Рассвета",
                ["description"] = "Первый зал."
            });
        root["shiningPoliticalActors"] = new JsonArray(
            new JsonObject
            {
                ["actorId"] = "radiant_actor_dawn",
                ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
                ["displayName"] = "Глашатай Рассвета",
                ["politicalStatus"] = ShiningAbodeState.PoliticalStatusElder
            });

        var error = ShiningAbodeState.ValidateRawOwnerStateForActionableMode(root);

        Assert.Null(error);
    }

    [Fact]
    public void NormalizeStateRoot_DoesNotRebuildPreparedPackageSelectedCardIds()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 240, "tier": 2 },
          "lightSparks": 60,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "pendingNativeFactionDiscovery": null,
          "gates": {
            "draftVersion": 1,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": []
          },
          "preparedIncarnationPackage": {
            "selectedCardIds": ["card_route_dawn", "card_social_dawn"],
            "selectedCards": [
              {
                "cardId": "card_social_dawn",
                "sourceType": "head",
                "effectFamily": "social",
                "rarity": "rare",
                "displayName": "Песнь Рассвета",
                "displaySummary": "Укрепляет союз.",
                "effectPayload": {}
              },
              {
                "cardId": "card_route_dawn",
                "sourceType": "project",
                "effectFamily": "route",
                "rarity": "rare",
                "displayName": "Тропа Рассвета",
                "displaySummary": "Открывает путь.",
                "effectPayload": {}
              }
            ],
            "generatedFromDraftVersion": 1,
            "preparedAtTurn": 10,
            "preparedAtUtc": "2026-04-23T00:00:00Z"
          },
          "gachaSystem": {
            "chargesPerReturn": 1,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_1",
            "gachaHistory": []
          }
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null, guardiansRoot: null);

        var selectedCardIds = root["preparedIncarnationPackage"]?["selectedCardIds"]?.AsArray()
            .Select(node => node?.GetValue<string>())
            .ToArray();
        Assert.Equal(new[] { "card_route_dawn", "card_social_dawn" }, selectedCardIds);
    }

    [Fact]
    public void ActivateForAscension_ResetsTransientStateAndFactionAscensionCounters()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "sealed_until_next_ascension",
          "radiance": {
            "experience": 600,
            "tier": 4
          },
          "lightSparks": 12,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Поют утренний свет."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "baseStrength": 35,
              "factionStrength": 35,
              "investCountThisAscension": 3,
              "projectArchetypesCountedThisAscension": ["accord", "revelation"],
              "projects": []
            }
          ],
          "gates": {
            "draftVersion": 4,
            "hasOpenDraft": true,
            "isStale": true,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 3
          },
          "preparedIncarnationPackage": {
            "selectedCardIds": ["card_a"],
            "selectedCards": []
          }
        }
        """)!.AsObject();

        var activated = ShiningAbodeState.ActivateForAscension(root, null);

        Assert.Equal(ShiningAbodeState.AvailabilityActive, activated["availability"]?.GetValue<string>());
        Assert.Equal(100, activated["lightSparks"]?.GetValue<int>());
        Assert.True(activated["gates"] is JsonObject gates && gates["hasOpenDraft"]?.GetValue<bool>() == false);
        Assert.Null(activated["preparedIncarnationPackage"]);
        Assert.Equal(0, activated["factions"]?[0]?["investCountThisAscension"]?.GetValue<int>());
        Assert.True(activated["factions"]?[0]?["projectArchetypesCountedThisAscension"] is JsonArray counted && counted.Count == 0);
    }

    [Fact]
    public void ValidatePreparedIncarnationPackageForBootstrap_EmptySelectedCards_FailsClosed()
    {
        var package = new JsonObject
        {
            ["selectedCardIds"] = new JsonArray("card_a"),
            ["selectedCards"] = new JsonArray()
        };

        var result = ShiningAbodeState.ValidatePreparedIncarnationPackageForBootstrap(package);

        Assert.NotNull(result);
        Assert.Contains("preparedIncarnationPackage", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeStateRoot_HydratesFoundingReceiptSnapshotFromCurrentHallAndFaction()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 200, "tier": 0 },
          "lightSparks": 80,
          "halls": [
            {
              "hallId": "hall_dawn",
              "hallName": "Зал Рассвета",
              "description": "Светлый зал союзов.",
              "serviceTags": ["social", "lore"]
            }
          ],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Союз утреннего света."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "projects": [],
              "tradeInventoryReceipts": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "factionFoundingReceipts": [
            {
              "requestId": "founding_dawn_1",
              "proposedFactionId": "faction_dawn",
              "proposedHallId": "hall_dawn",
              "hallName": "Зал Рассвета",
              "hallDescription": "Светлый зал союзов.",
              "hallServiceTags": ["social", "lore"],
              "factionId": "faction_dawn",
              "hallId": "hall_dawn",
              "factionName": "Хор Рассвета",
              "charterSummary": "Союз утреннего света.",
              "favoredArchetype": "accord",
              "patronEffectFamily": "social",
              "status": "accepted",
              "supportingResidentIds": ["resident_liora"],
              "resolvedAtTurn": 55,
              "resolvedAtUtc": "2026-04-20T10:00:00Z"
            }
          ]
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, null);

        var receipt = root["factionFoundingReceipts"]?[0]!.AsObject();
        Assert.NotNull(receipt);
        Assert.Equal("Светлый зал союзов.", receipt!["hallDescription"]?.GetValue<string>());
        Assert.Equal("Хор Рассвета", receipt["factionName"]?.GetValue<string>());
        Assert.Equal("Союз утреннего света.", receipt["charterSummary"]?.GetValue<string>());
        Assert.Equal("accord", receipt["favoredArchetype"]?.GetValue<string>());
        Assert.Equal("social", receipt["patronEffectFamily"]?.GetValue<string>());
        Assert.True(receipt["hallServiceTags"] is JsonArray serviceTags && serviceTags.Count == 2);
    }

    [Fact]
    public void NormalizeStateRoot_HydratesTradeAndCoreReceiptSnapshots()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 200, "tier": 0 },
          "lightSparks": 80,
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Союз утреннего света."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "projects": [
                {
                  "projectId": "project_social",
                  "displayName": "Песнь согласия",
                  "summary": "Укрепляет союз.",
                  "toneTags": [],
                  "targetFactionIds": [],
                  "projectArchetype": "accord",
                  "outputEffectFamily": "social",
                  "tier": 2,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 8
                }
              ],
              "tradeInventory": {
                "tradeCycleId": "shining_return_7",
                "generatedAtUtc": "2026-04-20T10:00:00Z",
                "generationTradeTier": 2,
                "generationRarityCeiling": "rare",
                "serviceMultiplierSnapshot": 1.2,
                "merchantProfile": "shining_faction",
                "items": [
                  { "slotId": "slot_1", "priceInFeathers": 40, "soldOut": true, "relicData": { "relicId": "relic_1", "name": "Реликвия 1", "quality": "Rare" } },
                  { "slotId": "slot_2", "priceInFeathers": 55, "soldOut": false, "relicData": { "relicId": "relic_2", "name": "Реликвия 2", "quality": "Uncommon" } }
                ]
              },
              "tradeInventoryReceipts": [
                {
                  "requestId": "trade_dawn_7",
                  "factionId": "faction_dawn",
                  "factionName": "Хор Рассвета",
                  "tradeCycleId": "shining_return_7",
                  "status": "ready",
                  "itemCount": 2,
                  "resolvedAtTurn": 56,
                  "resolvedAtUtc": "2026-04-20T10:01:00Z"
                }
              ],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "coreActionReceipts": [
            {
              "requestId": "core_project_1",
              "actionType": "complete_project",
              "factionId": "faction_dawn",
              "factionName": "Хор Рассвета",
              "projectId": "project_social",
              "projectName": "Песнь согласия",
              "selectedCardIds": [],
              "newResidentIds": [],
              "seededProjectIds": [],
              "generatedDraftVersion": 0,
              "resolvedAtTurn": 57,
              "resolvedAtUtc": "2026-04-20T10:02:00Z",
              "status": "accepted"
            }
          ]
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, null);

        var tradeReceipt = root["factions"]?[0]?["tradeInventoryReceipts"]?[0]!.AsObject();
        Assert.NotNull(tradeReceipt);
        Assert.Equal("Хор Рассвета", tradeReceipt!["factionName"]?.GetValue<string>());
        Assert.Null(tradeReceipt["soldOutCount"]);

        var coreReceipt = root["coreActionReceipts"]?[0]!.AsObject();
        Assert.NotNull(coreReceipt);
        Assert.Equal("Хор Рассвета", coreReceipt!["factionName"]?.GetValue<string>());
        Assert.Equal("Песнь согласия", coreReceipt["projectName"]?.GetValue<string>());
    }

    [Fact]
    public void NormalizeResidentShiningFields_DerivesFactionLoyaltyAndClearsMissingFactionMembership()
    {
        var resident = JsonNode.Parse("""
        {
          "residentId": "resident_liora",
          "abodeDevotionLevel": 62,
          "restlessness": 26,
          "ascensionState": "ascended",
          "shiningFactionId": "faction_dawn",
          "mortalWorldImprint": {
            "coreTraits": ["memory keeper"],
            "archetypeHints": ["archive"]
          }
        }
        """)!.AsObject();
        var shiningRoot = JsonNode.Parse("""
        {
          "factions": [
            {
              "factionId": "faction_dawn",
              "factionStrength": 74,
              "projects": [
                { "status": "completed", "isSupported": true }
              ],
              "leadership": {
                "leadershipState": "secure"
              }
            }
          ]
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot);

        Assert.Equal("archive_support", resident["residentRole"]?.GetValue<string>());
        Assert.InRange(resident["factionLoyaltyLevel"]!.GetValue<int>(), 0, 100);
        Assert.InRange(resident["factionRestlessness"]!.GetValue<int>(), 0, 100);
        Assert.Equal(
            ShiningAbodeState.ResolveFactionLoyaltyTier(resident["factionLoyaltyLevel"]!.GetValue<int>()),
            resident["factionLoyaltyTier"]?.GetValue<string>());
        Assert.Equal(
            ShiningAbodeState.ResolveFactionRealignmentState(
                resident["factionLoyaltyLevel"]!.GetValue<int>(),
                resident["factionRestlessness"]!.GetValue<int>()),
            resident["factionRealignmentState"]?.GetValue<string>());

        resident["shiningFactionId"] = "missing";
        ShiningAbodeState.NormalizeResidentShiningFields(resident, shiningRoot);
        Assert.Null(resident["shiningFactionId"]);
    }

    [Fact]
    public void ActivateForAscension_WithGuardiansRoot_MaterializesActiveGuardianFaction()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        var guardiansRoot = JsonNode.Parse("""
        {
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "domain": "memory",
            "abode": {
              "abodeName": "Зал Тихой Памяти"
            }
          }
        }
        """)!.AsObject();

        var activated = ShiningAbodeState.ActivateForAscension(root, residentRoot: null, guardiansRoot);

        Assert.True(activated["halls"] is JsonArray halls && halls.Count == 1);
        Assert.True(activated["factions"] is JsonArray factions && factions.Count == 1);
        Assert.Equal("guardian", activated["factions"]?[0]?["leadership"]?["headActorType"]?.GetValue<string>());
        Assert.Equal("guardian_azalia", activated["factions"]?[0]?["leadership"]?["headActorId"]?.GetValue<string>());
    }

    [Fact]
    public void NormalizeStateRoot_SealedAvailability_DoesNotMaterializeActiveGuardianFaction()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["availability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension;
        var guardiansRoot = JsonNode.Parse("""
        {
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "domain": "memory",
            "abode": {
              "abodeName": "Зал Тихой Памяти"
            }
          }
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null, guardiansRoot);

        Assert.Empty(root["halls"]!.AsArray());
        Assert.Empty(root["factions"]!.AsArray());
    }

    [Fact]
    public void ActivateForAscension_WithFoundedGuardian_MaterializesPlayerFoundedProjection()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        var guardiansRoot = JsonNode.Parse($$"""
        {
          "activeGuardian": {
            "guardianId": "guardian_founder",
            "canonicalName": "Северин",
            "originType": "{{PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul}}",
            "domain": "memory",
            "abode": {
              "abodeName": "Зал Основателя"
            }
          }
        }
        """)!.AsObject();

        var activated = ShiningAbodeState.ActivateForAscension(root, residentRoot: null, guardiansRoot);

        Assert.Equal(ShiningAbodeState.OriginTypePlayerFounded, activated["factions"]?[0]?["originType"]?.GetValue<string>());
        Assert.Equal("guardian", activated["factions"]?[0]?["leadership"]?["headActorType"]?.GetValue<string>());
        Assert.Equal("guardian_founder", activated["factions"]?[0]?["leadership"]?["headActorId"]?.GetValue<string>());
        Assert.Equal("Фракция, восходящая к основанному Хранителю Северин.", activated["factions"]?[0]?["charter"]?["summary"]?.GetValue<string>());
        Assert.Equal("Обитель основанного Хранителя Северин внутри Сияющей Обители.", activated["halls"]?[0]?["description"]?.GetValue<string>());
    }

    [Fact]
    public void ReenterOrdinaryActiveState_UpgradesExistingFoundedGuardianProjection()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 0,
            "tier": 0
          },
          "lightSparks": 63,
          "halls": [
            {
              "hallId": "hall_guardian_founder",
              "hallName": "Зал Основателя",
              "description": "Обитель Хранителя Северин внутри Сияющей Обители.",
              "serviceTags": ["memory", "social"]
            }
          ],
          "factions": [
            {
              "factionId": "faction_guardian_founder",
              "originType": "ascended_guardian",
              "hallId": "hall_guardian_founder",
              "charter": {
                "factionName": "Северин",
                "favoredArchetype": "remembrance",
                "patronEffectFamily": "memory",
                "summary": "Фракция, восходящая к Хранителю Северин."
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "guardian_founder",
                "leadershipState": "secure"
              },
              "baseStrength": 35,
              "factionStrength": 35,
              "projects": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "preparedIncarnationPackage": null
        }
        """)!.AsObject();
        var guardiansRoot = JsonNode.Parse($$"""
        {
          "activeGuardian": {
            "guardianId": "guardian_founder",
            "canonicalName": "Северин",
            "originType": "{{PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul}}",
            "domain": "memory",
            "abode": {
              "abodeName": "Зал Основателя"
            }
          }
        }
        """)!.AsObject();

        var reentered = ShiningAbodeState.ReenterOrdinaryActiveState(root, residentRoot: null, guardiansRoot);

        Assert.Equal(ShiningAbodeState.OriginTypePlayerFounded, reentered["factions"]?[0]?["originType"]?.GetValue<string>());
        Assert.Equal("Фракция, восходящая к основанному Хранителю Северин.", reentered["factions"]?[0]?["charter"]?["summary"]?.GetValue<string>());
        Assert.Equal("Обитель основанного Хранителя Северин внутри Сияющей Обители.", reentered["halls"]?[0]?["description"]?.GetValue<string>());
        Assert.True(reentered["factions"] is JsonArray factions && factions.Count == 1);
    }

    [Fact]
    public void ReenterOrdinaryActiveState_PreservesLeadershipOutcomeOnActiveGuardianFaction()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 160,
            "tier": 1
          },
          "lightSparks": 75,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_guardian_azalia",
              "originType": "ascended_guardian",
              "hallId": "hall_guardian_azalia",
              "charter": {
                "factionName": "Дом Азалии",
                "favoredArchetype": "remembrance",
                "patronEffectFamily": "memory",
                "summary": "Хранит отзвуки памяти."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "contested"
              },
              "baseStrength": 35,
              "factionStrength": 48,
              "investCountThisAscension": 0,
              "projectArchetypesCountedThisAscension": [],
              "projects": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          }
        }
        """)!.AsObject();
        var guardiansRoot = JsonNode.Parse("""
        {
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "domain": "memory",
            "abode": {
              "abodeName": "Зал Тихой Памяти"
            }
          }
        }
        """)!.AsObject();

        var reentered = ShiningAbodeState.ReenterOrdinaryActiveState(root, residentRoot: null, guardiansRoot);

        Assert.Equal(ShiningAbodeState.HeadActorTypePlayerSoul, reentered["factions"]?[0]?["leadership"]?["headActorType"]?.GetValue<string>());
        Assert.Equal("player_soul", reentered["factions"]?[0]?["leadership"]?["headActorId"]?.GetValue<string>());
        Assert.Equal(ShiningAbodeState.LeadershipStateContested, reentered["factions"]?[0]?["leadership"]?["leadershipState"]?.GetValue<string>());
    }

    [Fact]
    public void ReenterOrdinaryActiveState_RebindsMalformedGuardianHeadOnActiveGuardianFaction()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 160,
            "tier": 1
          },
          "lightSparks": 75,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_guardian_azalia",
              "originType": "ascended_guardian",
              "hallId": "hall_guardian_azalia",
              "charter": {
                "factionName": "Дом Азалии",
                "favoredArchetype": "remembrance",
                "patronEffectFamily": "memory",
                "summary": "Хранит отзвуки памяти."
              },
              "leadership": {
                "headActorType": "guardian",
                "headActorId": "",
                "leadershipState": "contested"
              },
              "baseStrength": 35,
              "factionStrength": 48,
              "investCountThisAscension": 0,
              "projectArchetypesCountedThisAscension": [],
              "projects": [],
              "leadershipReceipts": [],
              "leadershipHistory": []
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          }
        }
        """)!.AsObject();
        var guardiansRoot = JsonNode.Parse("""
        {
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "domain": "memory",
            "abode": {
              "abodeName": "Зал Тихой Памяти"
            }
          }
        }
        """)!.AsObject();

        var reentered = ShiningAbodeState.ReenterOrdinaryActiveState(root, residentRoot: null, guardiansRoot);

        Assert.Equal(ShiningAbodeState.HeadActorTypeGuardian, reentered["factions"]?[0]?["leadership"]?["headActorType"]?.GetValue<string>());
        Assert.Equal("guardian_azalia", reentered["factions"]?[0]?["leadership"]?["headActorId"]?.GetValue<string>());
        Assert.Equal(ShiningAbodeState.LeadershipStateContested, reentered["factions"]?[0]?["leadership"]?["leadershipState"]?.GetValue<string>());
    }

    [Fact]
    public void ReenterOrdinaryActiveState_PreservesTransientStateAndCounters()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 600,
            "tier": 4
          },
          "lightSparks": 12,
          "pendingNativeFactionDiscovery": {
            "requestId": "discover_native_faction:0021",
            "createdAtTurn": 21,
            "createdAtUtc": "2026-04-18T10:00:00Z",
            "radianceTierAtRequest": 4,
            "costFeathers": 25,
            "costLightSparks": 20
          },
          "halls": [],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Поют утренний свет."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "baseStrength": 35,
              "factionStrength": 35,
              "investCountThisAscension": 3,
              "projectArchetypesCountedThisAscension": ["accord", "revelation"],
              "projects": []
            }
          ],
          "gates": {
            "draftVersion": 4,
            "hasOpenDraft": true,
            "isStale": true,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": ["card_a"],
            "selectedBlessingCardIds": ["card_a"],
            "nextCandidateCursor": 3,
            "rerollsRemaining": 3
          },
          "preparedIncarnationPackage": null
        }
        """)!.AsObject();

        var reentered = ShiningAbodeState.ReenterOrdinaryActiveState(root, residentRoot: null, guardiansRoot: null);

        Assert.Equal(ShiningAbodeState.AvailabilityActive, reentered["availability"]?.GetValue<string>());
        Assert.Equal(12, reentered["lightSparks"]?.GetValue<int>());
        Assert.NotNull(reentered["pendingNativeFactionDiscovery"]);
        Assert.Equal(3, reentered["factions"]?[0]?["investCountThisAscension"]?.GetValue<int>());
        Assert.True(reentered["factions"]?[0]?["projectArchetypesCountedThisAscension"] is JsonArray counted && counted.Count == 2);
        Assert.True(reentered["gates"] is JsonObject gates && gates["hasOpenDraft"]?.GetValue<bool>() == true);
        Assert.Equal(4, reentered["gates"]?["draftVersion"]?.GetValue<int>());
        Assert.Equal(3, reentered["gates"]?["rerollsRemaining"]?.GetValue<int>());
    }

    [Fact]
    public void ResolveProjectCompletionCost_FavoredRevelationWithArchiveSupport_AppliesBothDiscounts()
    {
        var faction = JsonNode.Parse("""
        {
          "factionId": "faction_memory",
          "charter": {
            "favoredArchetype": "revelation"
          }
        }
        """)!.AsObject();
        var residentRoot = JsonNode.Parse("""
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_memory",
              "residentRole": "archive_support"
            }
          ]
        }
        """)!.AsObject();

        var cost = ShiningAbodeState.ResolveProjectCompletionCost(faction, residentRoot, "revelation", 1);

        Assert.Equal(10, cost.Feathers);
        Assert.Equal(5, cost.LightSparks);
    }

    [Fact]
    public void TryQuoteProjectCompletion_InvalidProjectArchetype_FailsInsteadOfNormalizing()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 240,
            "tier": 2
          },
          "lightSparks": 100,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Поют утренний свет."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "baseStrength": 35,
              "factionStrength": 50,
              "investCountThisAscension": 0,
              "projectArchetypesCountedThisAscension": [],
              "projects": []
            }
          ]
        }
        """)!.AsObject();
        var projectDraft = JsonNode.Parse("""
        {
          "displayName": "Ложная Песнь",
          "summary": "Не должна пройти.",
          "toneTags": ["bright"],
          "targetFactionIds": [],
          "projectArchetype": "invalid_archetype",
          "outputEffectFamily": "social",
          "tier": 2
        }
        """)!.AsObject();

        var success = ShiningAbodeState.TryQuoteProjectCompletion(root, residentRoot: null, "faction_dawn", projectDraft, out _, out var error);

        Assert.False(success);
        Assert.Equal("Неподдерживаемый archetype проекта.", error);
    }

    [Fact]
    public void TryInvestInFaction_RecomputesStrengthAndMarksOpenGatesStale()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 240,
            "tier": 2
          },
          "lightSparks": 100,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Поют утренний свет."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "baseStrength": 35,
              "factionStrength": 35,
              "investCountThisAscension": 0,
              "projects": []
            }
          ],
          "gates": {
            "draftVersion": 2,
            "hasOpenDraft": true,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          }
        }
        """)!.AsObject();

        var success = ShiningAbodeState.TryInvestInFaction(root, residentRoot: null, "faction_dawn", out var error);

        Assert.True(success, error);
        Assert.Equal(95, root["lightSparks"]?.GetValue<int>());
        Assert.Equal(43, root["factions"]?[0]?["factionStrength"]?.GetValue<int>());
        Assert.True(root["gates"]?["isStale"]?.GetValue<bool>());
    }

    [Fact]
    public void TrySupportProject_AlreadySupported_FailsWithoutMarkingGatesStale()
    {
        var root = CreateProjectSupportState(isSupported: true);

        var success = ShiningAbodeState.TrySupportProject(root, "faction_dawn", "project_dawn", out var error);

        Assert.False(success);
        Assert.Contains("support_project", error);
        Assert.True(GetSingleProject(root)["isSupported"]?.GetValue<bool>());
        Assert.False(root["gates"]?["isStale"]?.GetValue<bool>());
    }

    [Fact]
    public void TrySupportProject_UnsupportedProject_TogglesSupportAndMarksGatesStale()
    {
        var root = CreateProjectSupportState(isSupported: false);

        var success = ShiningAbodeState.TrySupportProject(root, "faction_dawn", "project_dawn", out var error);

        Assert.True(success, error);
        Assert.True(GetSingleProject(root)["isSupported"]?.GetValue<bool>());
        Assert.True(root["gates"]?["isStale"]?.GetValue<bool>());
    }

    [Fact]
    public void TryUnsupportProject_AlreadyUnsupported_FailsWithoutMarkingGatesStale()
    {
        var root = CreateProjectSupportState(isSupported: false);

        var success = ShiningAbodeState.TryUnsupportProject(root, "faction_dawn", "project_dawn", out var error);

        Assert.False(success);
        Assert.Contains("unsupport_project", error);
        Assert.False(GetSingleProject(root)["isSupported"]?.GetValue<bool>());
        Assert.False(root["gates"]?["isStale"]?.GetValue<bool>());
    }

    [Fact]
    public void TryUnsupportProject_SupportedProject_TogglesSupportAndMarksGatesStale()
    {
        var root = CreateProjectSupportState(isSupported: true);

        var success = ShiningAbodeState.TryUnsupportProject(root, "faction_dawn", "project_dawn", out var error);

        Assert.True(success, error);
        Assert.False(GetSingleProject(root)["isSupported"]?.GetValue<bool>());
        Assert.True(root["gates"]?["isStale"]?.GetValue<bool>());
    }

    [Fact]
    public void OpenGates_ThenPreparePackage_BuildsFrozenSnapshot()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 380,
            "tier": 3
          },
          "lightSparks": 100,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Поют утренний свет."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "baseStrength": 35,
              "factionStrength": 70,
              "investCountThisAscension": 0,
              "projects": [
                {
                  "projectId": "project_social",
                  "displayName": "Песнь согласия",
                  "summary": "Укрепляет связи.",
                  "toneTags": ["radiant"],
                  "targetFactionIds": [],
                  "projectArchetype": "accord",
                  "outputEffectFamily": "social",
                  "tier": 2,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 12
                },
                {
                  "projectId": "project_memory",
                  "displayName": "Хор памяти",
                  "summary": "Хранит отзвуки.",
                  "toneTags": ["memory"],
                  "targetFactionIds": [],
                  "projectArchetype": "remembrance",
                  "outputEffectFamily": "memory",
                  "tier": 2,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 12
                },
                {
                  "projectId": "project_passage",
                  "displayName": "Тропа возвращения",
                  "summary": "Зовёт спутников.",
                  "toneTags": ["passage"],
                  "targetFactionIds": [],
                  "projectArchetype": "passage",
                  "outputEffectFamily": "route",
                  "tier": 1,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 8
                }
              ]
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          }
        }
        """)!.AsObject();
        var residentRoot = JsonNode.Parse("""
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "displayName": "Лиора",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_dawn",
              "residentRole": "descent_support",
              "grantedRelicId": "relic_echo"
            }
          ]
        }
        """)!.AsObject();

        Assert.True(ShiningAbodeState.TryOpenGates(root, residentRoot, out var openError), openError);
        var availableCards = root["gates"]?["availableBlessingCards"] as JsonArray;
        Assert.NotNull(availableCards);
        Assert.True(availableCards!.Count >= 4);

        var firstCardId = availableCards[0]?["cardId"]?.GetValue<string>();
        Assert.False(string.IsNullOrWhiteSpace(firstCardId));
        Assert.True(ShiningAbodeState.TrySelectBlessingCard(root, firstCardId!, out var selectError), selectError);
        Assert.True(ShiningAbodeState.TryPrepareIncarnationPackage(root, 155, out var packageError), packageError);

        var package = Assert.IsType<JsonObject>(root["preparedIncarnationPackage"]);
        Assert.Equal(1, (package["selectedCardIds"] as JsonArray)?.Count);
        Assert.False(root["gates"]?["hasOpenDraft"]?.GetValue<bool>() ?? true);
    }

    [Fact]
    public void TryOpenGates_RadiantTierThreeProjectUpgradesRareToEpic()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 700,
            "tier": 4
          },
          "lightSparks": 100,
          "halls": [],
          "factions": [
            {
              "factionId": "faction_dawn",
              "originType": "player_founded",
              "hallId": "hall_dawn",
              "charter": {
                "factionName": "Хор Рассвета",
                "favoredArchetype": "accord",
                "patronEffectFamily": "social",
                "summary": "Поют утренний свет."
              },
              "leadership": {
                "headActorType": "player_soul",
                "headActorId": "player_soul",
                "leadershipState": "secure"
              },
              "baseStrength": 80,
              "factionStrength": 80,
              "investCountThisAscension": 0,
              "projects": [
                {
                  "projectId": "project_rare_upgrade",
                  "displayName": "Большой Хор",
                  "summary": "Усиливает сияющее согласие.",
                  "toneTags": ["radiant"],
                  "targetFactionIds": [],
                  "projectArchetype": "accord",
                  "outputEffectFamily": "social",
                  "tier": 3,
                  "status": "completed",
                  "isSupported": true,
                  "strengthReward": 16
                }
              ]
            }
          ],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          }
        }
        """)!.AsObject();

        Assert.True(ShiningAbodeState.TryOpenGates(root, residentRoot: null, out var error), error);

        var allCards = Assert.IsType<JsonArray>(root["gates"]?["allCandidateBlessingCards"]);
        var projectCard = Assert.Single(allCards.OfType<JsonObject>(), card =>
            string.Equals(card["sourceActorId"]?.GetValue<string>(), "project_rare_upgrade", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ShiningAbodeState.RarityEpic, projectCard["rarity"]?.GetValue<string>());
    }

    [Fact]
    public void TryOpenGates_InvalidPreparedIncarnationPackageBlocksOrdinaryActions()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["availability"] = ShiningAbodeState.AvailabilityActive;
        root["preparedIncarnationPackage"] = "broken package";

        var opened = ShiningAbodeState.TryOpenGates(root, residentRoot: null, out var error);

        Assert.False(opened);
        Assert.Contains("preparedIncarnationPackage", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryRerollGatesDraft_WithoutEnoughReplacementCards_DoesNotMutateShownHistory()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 250, "tier": 2 },
          "lightSparks": 100,
          "halls": [],
          "factions": [],
          "preparedIncarnationPackage": null,
          "gates": {
            "draftVersion": 4,
            "hasOpenDraft": true,
            "isStale": false,
            "allCandidateBlessingCards": [
              { "cardId": "card_a", "dedupeKey": "a", "sourceType": "faction", "sourceFactionId": "faction_a", "sourceActorId": "faction_a", "effectFamily": "social", "rarity": "common", "displayName": "A", "displaySummary": "A", "effectPayload": { "type": "noop" } },
              { "cardId": "card_b", "dedupeKey": "b", "sourceType": "faction", "sourceFactionId": "faction_b", "sourceActorId": "faction_b", "effectFamily": "route", "rarity": "common", "displayName": "B", "displaySummary": "B", "effectPayload": { "type": "noop" } },
              { "cardId": "card_c", "dedupeKey": "c", "sourceType": "faction", "sourceFactionId": "faction_c", "sourceActorId": "faction_c", "effectFamily": "lore", "rarity": "common", "displayName": "C", "displaySummary": "C", "effectPayload": { "type": "noop" } }
            ],
            "availableBlessingCards": [
              { "cardId": "card_a", "dedupeKey": "a", "sourceType": "faction", "sourceFactionId": "faction_a", "sourceActorId": "faction_a", "effectFamily": "social", "rarity": "common", "displayName": "A", "displaySummary": "A", "effectPayload": { "type": "noop" } },
              { "cardId": "card_b", "dedupeKey": "b", "sourceType": "faction", "sourceFactionId": "faction_b", "sourceActorId": "faction_b", "effectFamily": "route", "rarity": "common", "displayName": "B", "displaySummary": "B", "effectPayload": { "type": "noop" } }
            ],
            "shownBlessingCardIds": [ "card_a", "card_b" ],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 2,
            "rerollsRemaining": 1
          }
        }
        """)!.AsObject();
        var beforeShown = root["gates"]?["shownBlessingCardIds"]?.DeepClone();
        var beforeAvailable = root["gates"]?["availableBlessingCards"]?.DeepClone();

        var rerolled = ShiningAbodeState.TryRerollGatesDraft(root, out var error);

        Assert.False(rerolled);
        Assert.Contains("replacement", error, StringComparison.OrdinalIgnoreCase);
        Assert.True(JsonNode.DeepEquals(beforeShown, root["gates"]?["shownBlessingCardIds"]));
        Assert.True(JsonNode.DeepEquals(beforeAvailable, root["gates"]?["availableBlessingCards"]));
    }

    [Fact]
    public void NormalizeStateRoot_HydratesPreparePackageReceiptSnapshotFromMatchingPreparedPackage()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": {
            "experience": 380,
            "tier": 3
          },
          "lightSparks": 100,
          "halls": [],
          "factions": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "preparedIncarnationPackage": {
            "generatedFromDraftVersion": 4,
            "preparedAtTurn": 155,
            "preparedAtUtc": "2026-04-19T10:00:00Z",
            "selectedCardIds": ["card_route_dawn"],
            "selectedCards": [
              {
                "cardId": "card_route_dawn",
                "dedupeKey": "route_dawn",
                "sourceType": "project",
                "sourceFactionId": "faction_dawn",
                "sourceActorId": "project_passage",
                "effectFamily": "route",
                "rarity": "Epic",
                "displayName": "Тропа возвращения",
                "displaySummary": "Открывает путь через память.",
                "effectPayload": {
                  "routeSeedId": "route_dawn",
                  "remainingUses": 1
                }
              }
            ]
          },
          "coreActionReceipts": [
            {
              "requestId": "core_package_dawn_1",
              "actionType": "prepare_incarnation_package",
              "selectedCardIds": ["card_route_dawn"],
              "newResidentIds": [],
              "seededProjectIds": [],
              "generatedDraftVersion": 4,
              "resolvedAtTurn": 155,
              "resolvedAtUtc": "2026-04-19T10:00:00Z",
              "status": "accepted",
              "reason": "package_frozen_for_next_life"
            }
          ],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": []
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null);

        var receipt = Assert.IsType<JsonObject>(root["coreActionReceipts"]?[0]);
        var selectedCards = Assert.IsType<JsonArray>(receipt["selectedCards"]);
        Assert.Single(selectedCards);
        Assert.Equal("Тропа возвращения", selectedCards[0]?["displayName"]?.GetValue<string>());
    }

    [Fact]
    public void NormalizeStateRoot_PreservesPreparedPackageSelectedCardIdOrder()
    {
        var root = JsonNode.Parse("""
        {
          "availability": "active",
          "radiance": { "experience": 240, "tier": 2 },
          "lightSparks": 60,
          "halls": [],
          "factions": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": [],
            "nextCandidateCursor": 0,
            "rerollsRemaining": 0
          },
          "preparedIncarnationPackage": {
            "generatedFromDraftVersion": 3,
            "preparedAtTurn": 160,
            "preparedAtUtc": "2026-04-21T10:00:00Z",
            "selectedCardIds": ["card_b", "card_a"],
            "selectedCards": [
              {
                "cardId": "card_a",
                "dedupeKey": "a",
                "sourceType": "head",
                "sourceFactionId": "faction_a",
                "effectFamily": "social",
                "rarity": "Rare",
                "displayName": "Карта А",
                "displaySummary": "Первая карта.",
                "effectPayload": {}
              },
              {
                "cardId": "card_b",
                "dedupeKey": "b",
                "sourceType": "project",
                "sourceFactionId": "faction_b",
                "effectFamily": "route",
                "rarity": "Epic",
                "displayName": "Карта Б",
                "displaySummary": "Вторая карта.",
                "effectPayload": {}
              }
            ]
          },
          "gachaSystem": {
            "chargesPerReturn": 2,
            "chargesUsedThisReturn": 0,
            "currentReturnCycleId": "return_3",
            "gachaHistory": []
          },
          "coreActionReceipts": [],
          "factionFoundingReceipts": [],
          "factionRealignmentReceipts": []
        }
        """)!.AsObject();

        ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null);

        var selectedCardIds = Assert.IsType<JsonArray>(root["preparedIncarnationPackage"]?["selectedCardIds"]);
        Assert.Equal(new[] { "card_b", "card_a" }, selectedCardIds.Select(node => node!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void SyncShiningReturnCycle_ResetsUsedGachaChargesOnNewCycle()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["radiance"] = new JsonObject
        {
            ["experience"] = 380,
            ["tier"] = 3
        };
        root["gachaSystem"] = new JsonObject
        {
            ["chargesPerReturn"] = 4,
            ["chargesUsedThisReturn"] = 3,
            ["currentReturnCycleId"] = "shining_return_2",
            ["gachaHistory"] = new JsonArray()
        };

        var changed = ShiningAbodeState.SyncShiningReturnCycle(root, currentIncarnation: 5, out var cycleChanged);

        Assert.True(changed);
        Assert.True(cycleChanged);
        Assert.Equal("shining_return_5", root["gachaSystem"]?["currentReturnCycleId"]?.GetValue<string>());
        Assert.Equal(0, root["gachaSystem"]?["chargesUsedThisReturn"]?.GetValue<int>());
    }

    [Fact]
    public void SyncShiningReturnCycle_EmptyLegacyCycleIdResetsUsedGachaCharges()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["gachaSystem"] = new JsonObject
        {
            ["chargesPerReturn"] = 3,
            ["chargesUsedThisReturn"] = 2,
            ["currentReturnCycleId"] = "",
            ["gachaHistory"] = new JsonArray()
        };

        var changed = ShiningAbodeState.SyncShiningReturnCycle(root, currentIncarnation: 5, out var cycleChanged);

        Assert.True(changed);
        Assert.False(cycleChanged);
        Assert.Equal("shining_return_5", root["gachaSystem"]?["currentReturnCycleId"]?.GetValue<string>());
        Assert.Equal(0, root["gachaSystem"]?["chargesUsedThisReturn"]?.GetValue<int>());
    }

    [Fact]
    public void TryApplyRelicGachaAccounting_EmptyCycleIdPersistsResolvedReturnCycle()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["availability"] = ShiningAbodeState.AvailabilityActive;
        root["radiance"] = new JsonObject
        {
            ["experience"] = 0,
            ["tier"] = 0
        };
        root["gachaSystem"] = new JsonObject
        {
            ["chargesPerReturn"] = 1,
            ["chargesUsedThisReturn"] = 0,
            ["currentReturnCycleId"] = "",
            ["gachaHistory"] = new JsonArray()
        };
        root["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_dawn",
                ["originType"] = ShiningAbodeState.OriginTypePlayerFounded,
                ["hallId"] = "hall_dawn",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Хор Рассвета",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                    ["summary"] = "Поют утренний свет."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                    ["headActorId"] = "player_soul",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["factionStrength"] = 20,
                ["projects"] = new JsonArray()
            }
        };
        var soulRoot = new JsonObject
        {
            ["currentIncarnation"] = 5,
            ["inkFeathers"] = new JsonObject
            {
                ["current"] = 100,
                ["total"] = 100
            }
        };

        var applied = ShiningAbodeState.TryApplyRelicGachaAccounting(
            root,
            soulRoot,
            residentRoot: null,
            factionId: "faction_dawn",
            requestId: "req_gacha",
            relicId: "relic_sun",
            relicName: "Солнечная Реликвия",
            baseRarity: "Common",
            finalRarity: "Common",
            resolvedAtTurn: 17,
            resolvedAtUtc: "2026-04-28T00:00:00Z",
            out _,
            out _,
            out var error);

        Assert.True(applied, error);
        Assert.Equal("shining_return_5", root["gachaSystem"]?["currentReturnCycleId"]?.GetValue<string>());
        Assert.Equal(1, root["gachaSystem"]?["chargesUsedThisReturn"]?.GetValue<int>());
        var history = Assert.IsType<JsonArray>(root["gachaSystem"]?["gachaHistory"]);
        Assert.Equal("shining_return_5", history[0]?["returnCycleId"]?.GetValue<string>());
    }

    [Fact]
    public void TryCompleteProject_FavoredArchetypeDiscountsCostButNotStrengthReward()
    {
        var root = CreateProjectSupportState(isSupported: false);
        root["lightSparks"] = 100;
        var faction = root["factions"]![0]!.AsObject();
        faction["projects"] = new JsonArray();
        faction["projectArchetypesCountedThisAscension"] = new JsonArray();

        var favoredDraft = BuildProjectDraft(
            "Проект согласия",
            ShiningAbodeState.ProjectArchetypeAccord,
            ShiningAbodeState.EffectFamilySocial,
            tier: 2);
        var nonFavoredDraft = BuildProjectDraft(
            "Проект памяти",
            ShiningAbodeState.ProjectArchetypeRemembrance,
            ShiningAbodeState.EffectFamilyMemory,
            tier: 2);

        Assert.True(ShiningAbodeState.TryQuoteProjectCompletion(
            root,
            residentRoot: null,
            factionId: "faction_dawn",
            favoredDraft,
            out var favoredCost,
            out var favoredQuoteError), favoredQuoteError);
        Assert.Equal(new ShiningAbodeState.ResourceCost(25, 10), favoredCost);

        Assert.True(ShiningAbodeState.TryQuoteProjectCompletion(
            root,
            residentRoot: null,
            factionId: "faction_dawn",
            nonFavoredDraft,
            out var nonFavoredCost,
            out var nonFavoredQuoteError), nonFavoredQuoteError);
        Assert.Equal(new ShiningAbodeState.ResourceCost(30, 15), nonFavoredCost);

        Assert.True(ShiningAbodeState.TryCompleteProject(
            root,
            residentRoot: null,
            factionId: "faction_dawn",
            favoredDraft,
            currentTurnNumber: 44,
            projectIdOverride: "project_favored",
            completedAtUtc: "2026-04-28T00:00:00Z",
            out _,
            out var favoredCompletionError), favoredCompletionError);
        Assert.True(ShiningAbodeState.TryCompleteProject(
            root,
            residentRoot: null,
            factionId: "faction_dawn",
            nonFavoredDraft,
            currentTurnNumber: 45,
            projectIdOverride: "project_non_favored",
            completedAtUtc: "2026-04-28T00:01:00Z",
            out _,
            out var nonFavoredCompletionError), nonFavoredCompletionError);

        var projects = faction["projects"]!.AsArray().OfType<JsonObject>().ToList();
        Assert.Equal(12, projects.Single(project => project["projectId"]?.GetValue<string>() == "project_favored")["strengthReward"]?.GetValue<int>());
        Assert.Equal(12, projects.Single(project => project["projectId"]?.GetValue<string>() == "project_non_favored")["strengthReward"]?.GetValue<int>());
    }

    [Theory]
    [InlineData(ShiningAbodeState.RarityCommon, 1)]
    [InlineData(ShiningAbodeState.RarityUncommon, 2)]
    [InlineData(ShiningAbodeState.RarityRare, 3)]
    [InlineData(ShiningAbodeState.RarityEpic, 4)]
    [InlineData(ShiningAbodeState.RarityLegendary, 5)]
    [InlineData(ShiningAbodeState.RarityRadiant, 6)]
    public void BlessingCardRarityWeight_CoversEverySupportedRarity(string rarity, int expectedWeight)
    {
        Assert.True(ShiningAbodeState.IsSupportedRarity(rarity));
        Assert.Equal(expectedWeight, ShiningAbodeState.GetBlessingCardRarityWeight(rarity));
    }

    [Fact]
    public void BlessingCardRarityWeight_SortsEverySupportedRarityInDescendingPower()
    {
        var rarities = GetSupportedBlessingRarities();

        var sorted = rarities
            .OrderByDescending(ShiningAbodeState.GetBlessingCardRarityWeight)
            .ToArray();

        Assert.Equal(
            new[]
            {
                ShiningAbodeState.RarityRadiant,
                ShiningAbodeState.RarityLegendary,
                ShiningAbodeState.RarityEpic,
                ShiningAbodeState.RarityRare,
                ShiningAbodeState.RarityUncommon,
                ShiningAbodeState.RarityCommon
            },
            sorted);
    }

    [Fact]
    public void ResolveLowerBlessingCardRarity_UsesCompleteSupportedLadder()
    {
        var rarities = GetSupportedBlessingRarities();

        for (var leftIndex = 0; leftIndex < rarities.Length; leftIndex++)
        {
            for (var rightIndex = 0; rightIndex < rarities.Length; rightIndex++)
            {
                var expected = rarities[Math.Min(leftIndex, rightIndex)];

                Assert.Equal(
                    expected,
                    ShiningAbodeState.ResolveLowerBlessingCardRarity(rarities[leftIndex], rarities[rightIndex]));
            }
        }
    }

    private static JsonObject CreateProjectSupportState(bool isSupported) => new()
    {
        ["availability"] = ShiningAbodeState.AvailabilityActive,
        ["radiance"] = new JsonObject
        {
            ["experience"] = 250,
            ["tier"] = 2
        },
        ["lightSparks"] = 80,
        ["factions"] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_dawn",
                ["originType"] = ShiningAbodeState.OriginTypePlayerFounded,
                ["hallId"] = "hall_dawn",
                ["charter"] = new JsonObject
                {
                    ["factionName"] = "Хор Рассвета",
                    ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                    ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                    ["summary"] = "Поют утренний свет."
                },
                ["leadership"] = new JsonObject
                {
                    ["headActorType"] = ShiningAbodeState.HeadActorTypePlayerSoul,
                    ["headActorId"] = "player_soul",
                    ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
                },
                ["baseStrength"] = 35,
                ["factionStrength"] = 47,
                ["investCountThisAscension"] = 0,
                ["projectArchetypesCountedThisAscension"] = new JsonArray(),
                ["projects"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["projectId"] = "project_dawn",
                        ["displayName"] = "Песнь зари",
                        ["summary"] = "Укрепляет союз.",
                        ["toneTags"] = new JsonArray("bright"),
                        ["targetFactionIds"] = new JsonArray(),
                        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
                        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
                        ["tier"] = 1,
                        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
                        ["isSupported"] = isSupported,
                        ["strengthReward"] = 8
                    }
                }
            }
        },
        ["gates"] = new JsonObject
        {
            ["draftVersion"] = 1,
            ["hasOpenDraft"] = true,
            ["isStale"] = false,
            ["allCandidateBlessingCards"] = new JsonArray(),
            ["availableBlessingCards"] = new JsonArray(),
            ["shownBlessingCardIds"] = new JsonArray(),
            ["selectedBlessingCardIds"] = new JsonArray(),
            ["nextCandidateCursor"] = 0,
            ["rerollsRemaining"] = 0
        }
    };

    private static JsonObject BuildProjectDraft(string displayName, string archetype, string effectFamily, int tier) => new()
    {
        ["displayName"] = displayName,
        ["summary"] = "Проверочный проект.",
        ["toneTags"] = new JsonArray("radiant"),
        ["targetFactionIds"] = new JsonArray(),
        ["projectArchetype"] = archetype,
        ["outputEffectFamily"] = effectFamily,
        ["tier"] = tier
    };

    private static string[] GetSupportedBlessingRarities() =>
    [
        ShiningAbodeState.RarityCommon,
        ShiningAbodeState.RarityUncommon,
        ShiningAbodeState.RarityRare,
        ShiningAbodeState.RarityEpic,
        ShiningAbodeState.RarityLegendary,
        ShiningAbodeState.RarityRadiant
    ];

    private static JsonObject GetSingleProject(JsonObject root) =>
        root["factions"]![0]!["projects"]![0]!.AsObject();
}
