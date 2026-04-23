using System.Text.Json;
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
}
