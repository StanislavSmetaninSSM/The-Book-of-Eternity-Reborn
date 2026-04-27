using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AscendedResidentKeepsActiveGuardianFactionMembership()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "manifestation": {
                "currentDisplayName": "Азалия"
              },
              "domain": "memory",
              "abode": {
                "abodeId": "abode_azalia",
                "abodeName": "Зал Тихой Памяти"
              },
              "relationshipData": {
                "currentReputation": 90,
                "reputationHistory": []
              },
              "abodePower": {
                "currentPower": 42,
                "tier": "Стабильная",
                "lastUpdatedAt": "2026-04-18T00:00:00Z",
                "history": []
              },
              "guardianRelationships": [],
              "gachaSystem": {
                "chargesPerReturn": 0,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "manifestation": {
              "currentDisplayName": "Азалия"
            },
            "domain": "memory",
            "abode": {
              "abodeId": "abode_azalia",
              "abodeName": "Зал Тихой Памяти"
            },
            "relationshipData": {
              "currentReputation": 90,
              "reputationHistory": []
            },
            "abodePower": {
              "currentPower": 42,
              "tier": "Стабильная",
              "lastUpdatedAt": "2026-04-18T00:00:00Z",
              "history": []
            },
            "guardianRelationships": [],
            "gachaSystem": {
              "chargesPerReturn": 0,
              "chargesUsedThisReturn": 0,
              "gachaHistory": []
            }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, """
        {
          "entries": [
            {
              "residentId": "resident_azalia_001",
              "guardianId": "guardian_azalia",
              "displayName": "Илира",
              "ascensionState": "ascended",
              "shiningFactionId": "faction_guardian_azalia",
              "abodeDevotionLevel": 74
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "radiance": {
            "experience": 0,
            "tier": 0
          },
          "lightSparks": 55,
          "halls": [],
          "factions": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "rerollsRemaining": 0,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": []
          },
          "preparedIncarnationPackage": null
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        using var residentsDoc = JsonDocument.Parse((await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath))!);
        var resident = residentsDoc.RootElement.GetProperty("entries")[0];
        Assert.Equal("faction_guardian_azalia", resident.GetProperty("shiningFactionId").GetString());
        Assert.True(resident.GetProperty("factionLoyaltyLevel").GetInt32() > 0);
        Assert.NotEqual("alienated", resident.GetProperty("factionLoyaltyTier").GetString());

        using var shiningDoc = JsonDocument.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!);
        Assert.Contains(
            shiningDoc.RootElement.GetProperty("factions").EnumerateArray(),
            faction => string.Equals(faction.GetProperty("factionId").GetString(), "faction_guardian_azalia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PlayerFoundedGuardianKeepsFoundedShiningProjection()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", $$"""
        {
          "guardians": [
            {
              "guardianId": "guardian_founder",
              "canonicalName": "Северин",
              "originType": "{{PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul}}",
              "manifestation": {
                "currentDisplayName": "Северин"
              },
              "domain": "memory",
              "abode": {
                "abodeId": "abode_founder",
                "abodeName": "Зал Основателя"
              },
              "relationshipData": {
                "currentReputation": 230,
                "reputationHistory": []
              },
              "abodePower": {
                "currentPower": 42,
                "tier": "Стабильная",
                "lastUpdatedAt": "2026-04-18T00:00:00Z",
                "history": []
              },
              "guardianRelationships": [],
              "gachaSystem": {
                "chargesPerReturn": 1,
                "chargesUsedThisReturn": 0,
                "gachaHistory": []
              }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_founder",
            "canonicalName": "Северин",
            "originType": "{{PlayerGuardianFoundationState.OriginTypePlayerFoundedAscendedSoul}}",
            "manifestation": {
              "currentDisplayName": "Северин"
            },
            "domain": "memory",
            "abode": {
              "abodeId": "abode_founder",
              "abodeName": "Зал Основателя"
            },
            "relationshipData": {
              "currentReputation": 230,
              "reputationHistory": []
            },
            "abodePower": {
              "currentPower": 42,
              "tier": "Стабильная",
              "lastUpdatedAt": "2026-04-18T00:00:00Z",
              "history": []
            },
            "guardianRelationships": [],
            "gachaSystem": {
              "chargesPerReturn": 1,
              "chargesUsedThisReturn": 0,
              "gachaHistory": []
            }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, """
        {
          "entries": []
        }
        """);

        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "radiance": {
            "experience": 0,
            "tier": 0
          },
          "lightSparks": 55,
          "halls": [],
          "factions": [],
          "gates": {
            "draftVersion": 0,
            "hasOpenDraft": false,
            "isStale": false,
            "rerollsRemaining": 0,
            "allCandidateBlessingCards": [],
            "availableBlessingCards": [],
            "shownBlessingCardIds": [],
            "selectedBlessingCardIds": []
          },
          "preparedIncarnationPackage": null
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        using var shiningDoc = JsonDocument.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!);
        var foundedFaction = Assert.Single(
            shiningDoc.RootElement.GetProperty("factions").EnumerateArray(),
            faction => string.Equals(faction.GetProperty("factionId").GetString(), "faction_guardian_founder", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(ShiningAbodeState.OriginTypePlayerFounded, foundedFaction.GetProperty("originType").GetString());
        Assert.Equal("guardian", foundedFaction.GetProperty("leadership").GetProperty("headActorType").GetString());
        Assert.Equal("guardian_founder", foundedFaction.GetProperty("leadership").GetProperty("headActorId").GetString());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ShiningPartialPatchPreservesUnrelatedCanonicalArrays()
    {
        var baseline = ShiningAbodeState.CreateDefaultState();
        baseline["halls"] = new JsonArray(
            CreateShiningHall("hall_keep", "Зал, который нужно сохранить"),
            CreateShiningHall("hall_update", "Старое имя"));
        baseline["factions"] = new JsonArray(
            CreateShiningFaction("faction_keep", "hall_keep", "project_keep"),
            CreateShiningFaction("faction_update", "hall_update", "project_existing"));
        baseline["shiningPoliticalActors"] = new JsonArray(
            CreateShiningActor("actor_keep", "faction_keep", "Актор сохранения"),
            CreateShiningActor("actor_update", "faction_update", "Старое имя"));
        baseline["coreActionReceipts"] = new JsonArray(new JsonObject
        {
            ["requestId"] = "core_old",
            ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
            ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
            ["resolvedAtTurn"] = 1,
            ["resolvedAtUtc"] = "2026-04-24T00:00:00Z"
        });

        var partialPatch = new JsonObject
        {
            ["halls"] = new JsonArray(CreateShiningHall("hall_update", "Новое имя")),
            ["factions"] = new JsonArray(new JsonObject
            {
                ["factionId"] = "faction_update",
                ["charter"] = new JsonObject
                {
                    ["summary"] = "Обновленное описание"
                },
                ["projects"] = new JsonArray(CreateShiningProject("project_new"))
            }),
            ["shiningPoliticalActors"] = new JsonArray(CreateShiningActor("actor_update", "faction_update", "Новое имя")),
            ["coreActionReceipts"] = new JsonArray(new JsonObject
            {
                ["requestId"] = "core_new",
                ["actionType"] = ShiningCoreActionRequestState.ActionTypeInvestInFaction,
                ["status"] = ShiningCoreActionRequestState.RequestStatusAccepted,
                ["resolvedAtTurn"] = 2,
                ["resolvedAtUtc"] = "2026-04-24T00:01:00Z"
            })
        };

        const string backupPath = "test_backups/shining_abode_state_baseline.json";
        await _fs.WriteFileAtomicAsync(backupPath, baseline.ToJsonString());
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, partialPatch.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningAbodeState.StatePath] = backupPath
        });

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!);
        var root = doc.RootElement;
        Assert.Contains(root.GetProperty("halls").EnumerateArray(), hall => hall.GetProperty("hallId").GetString() == "hall_keep");
        Assert.Contains(root.GetProperty("halls").EnumerateArray(), hall =>
            hall.GetProperty("hallId").GetString() == "hall_update" &&
            hall.GetProperty("hallName").GetString() == "Новое имя");
        Assert.Contains(root.GetProperty("factions").EnumerateArray(), faction => faction.GetProperty("factionId").GetString() == "faction_keep");
        var updatedFaction = Assert.Single(root.GetProperty("factions").EnumerateArray(), faction => faction.GetProperty("factionId").GetString() == "faction_update");
        Assert.Contains(updatedFaction.GetProperty("projects").EnumerateArray(), project => project.GetProperty("projectId").GetString() == "project_existing");
        Assert.Contains(updatedFaction.GetProperty("projects").EnumerateArray(), project => project.GetProperty("projectId").GetString() == "project_new");
        Assert.Contains(root.GetProperty("shiningPoliticalActors").EnumerateArray(), actor => actor.GetProperty("actorId").GetString() == "actor_keep");
        Assert.Contains(root.GetProperty("shiningPoliticalActors").EnumerateArray(), actor =>
            actor.GetProperty("actorId").GetString() == "actor_update" &&
            actor.GetProperty("displayName").GetString() == "Новое имя");
        Assert.Contains(root.GetProperty("coreActionReceipts").EnumerateArray(), receipt => receipt.GetProperty("requestId").GetString() == "core_old");
        Assert.Contains(root.GetProperty("coreActionReceipts").EnumerateArray(), receipt => receipt.GetProperty("requestId").GetString() == "core_new");
    }

    private static JsonObject CreateShiningHall(string hallId, string hallName) => new()
    {
        ["hallId"] = hallId,
        ["hallName"] = hallName,
        ["description"] = hallName,
        ["serviceTags"] = new JsonArray("social")
    };

    private static JsonObject CreateShiningFaction(string factionId, string hallId, string projectId) => new()
    {
        ["factionId"] = factionId,
        ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
        ["hallId"] = hallId,
        ["charter"] = new JsonObject
        {
            ["factionName"] = factionId,
            ["favoredArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
            ["patronEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
            ["summary"] = factionId
        },
        ["leadership"] = new JsonObject
        {
            ["headActorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
            ["headActorId"] = $"actor_{factionId}",
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure
        },
        ["baseStrength"] = 30,
        ["projects"] = new JsonArray(CreateShiningProject(projectId)),
        ["tradeInventoryReceipts"] = new JsonArray(),
        ["leadershipReceipts"] = new JsonArray(),
        ["leadershipHistory"] = new JsonArray()
    };

    private static JsonObject CreateShiningProject(string projectId) => new()
    {
        ["projectId"] = projectId,
        ["displayName"] = projectId,
        ["summary"] = projectId,
        ["toneTags"] = new JsonArray("social"),
        ["targetFactionIds"] = new JsonArray(),
        ["projectArchetype"] = ShiningAbodeState.ProjectArchetypeAccord,
        ["outputEffectFamily"] = ShiningAbodeState.EffectFamilySocial,
        ["tier"] = 1,
        ["status"] = ShiningAbodeState.ProjectStatusCompleted,
        ["isSupported"] = false,
        ["completedAtTurn"] = 1,
        ["completedAtUtc"] = "2026-04-24T00:00:00Z"
    };

    private static JsonObject CreateShiningActor(string actorId, string factionId, string displayName) => new()
    {
        ["actorId"] = actorId,
        ["actorType"] = ShiningAbodeState.HeadActorTypeRadiantActor,
        ["displayName"] = displayName,
        ["summary"] = displayName,
        ["originFactionId"] = factionId,
        ["currentFactionId"] = factionId,
        ["politicalStatus"] = ShiningAbodeState.PoliticalStatusElder
    };
}
