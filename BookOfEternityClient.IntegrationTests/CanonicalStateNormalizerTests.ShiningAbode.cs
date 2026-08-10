using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_PreservesMalformedLegacyPendingNativeFactionDiscovery()
    {
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "radiance": {
            "experience": 40,
            "tier": 0
          },
          "lightSparks": 50,
          "halls": [],
          "factions": [],
          "shiningPoliticalActors": [],
          "pendingNativeFactionDiscovery": "malformed_contract",
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
        Assert.Equal(
            "malformed_contract",
            shiningDoc.RootElement.GetProperty("pendingNativeFactionDiscovery").GetString());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AbsentGuardianFactionAndHallStayAbsentUntilAuthored()
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
        Assert.Equal(JsonValueKind.Null, resident.GetProperty("shiningFactionId").ValueKind);
        Assert.Equal(JsonValueKind.Null, resident.GetProperty("residentRole").ValueKind);
        Assert.Equal(0, resident.GetProperty("factionLoyaltyLevel").GetInt32());
        Assert.Equal("alienated", resident.GetProperty("factionLoyaltyTier").GetString());

        using var shiningDoc = JsonDocument.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!);
        Assert.Empty(shiningDoc.RootElement.GetProperty("factions").EnumerateArray());
        Assert.Empty(shiningDoc.RootElement.GetProperty("halls").EnumerateArray());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_UnreadableShiningStatePreservesResidentFactionMembership()
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
              "guardianRelationships": []
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, """
        {
          "entries": [
            {
              "residentId": "resident_mira_001",
              "guardianId": "guardian_azalia",
              "displayName": "Мира",
              "residentKind": "echo",
              "originType": "afterlife",
              "bondLevel": 72,
              "abodeDevotionLevel": 74,
              "ascensionState": "ascended",
              "shiningFactionId": "faction_dawn",
              "residentRole": "social_support",
              "factionLoyaltyLevel": 68,
              "factionRestlessness": 12,
              "factionRealignmentState": "settled"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, "{ malformed");

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        using var residentsDoc = JsonDocument.Parse((await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath))!);
        var resident = residentsDoc.RootElement.GetProperty("entries")[0];
        Assert.Equal("ascended", resident.GetProperty("ascensionState").GetString());
        Assert.Equal("faction_dawn", resident.GetProperty("shiningFactionId").GetString());
        Assert.Equal("social_support", resident.GetProperty("residentRole").GetString());
        Assert.Equal(68, resident.GetProperty("factionLoyaltyLevel").GetInt32());
        Assert.Equal("devoted", resident.GetProperty("factionLoyaltyTier").GetString());
        Assert.Equal(12, resident.GetProperty("factionRestlessness").GetInt32());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ReceiptlessGuardianFactionGetsNoCompatibilitySemantics()
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

        var receiptlessShiningRoot = ShiningAbodeState.CreateDefaultState();
        receiptlessShiningRoot["availability"] = ShiningAbodeState.AvailabilityActive;
        receiptlessShiningRoot["lightSparks"] = 55;
        receiptlessShiningRoot["halls"] = new JsonArray();
        receiptlessShiningRoot["factions"] = new JsonArray(
            new JsonObject
            {
                ["factionId"] = "faction_guardian_founder",
                ["baseStrength"] = 35,
                ["factionStrength"] = 35
            });
        const string receiptlessBackupPath =
            "test_backups/shining_abode_receiptless_guardian.json";
        await _fs.WriteFileAtomicAsync(
            receiptlessBackupPath,
            receiptlessShiningRoot.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            ShiningAbodeState.StatePath,
            receiptlessShiningRoot.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ShiningAbodeState.StatePath] = receiptlessBackupPath
            });

        using var shiningDoc = JsonDocument.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!);
        var receiptlessFaction = Assert.Single(
            shiningDoc.RootElement.GetProperty("factions").EnumerateArray(),
            faction => string.Equals(faction.GetProperty("factionId").GetString(), "faction_guardian_founder", StringComparison.OrdinalIgnoreCase));
        Assert.False(receiptlessFaction.TryGetProperty("originType", out _));
        Assert.False(receiptlessFaction.TryGetProperty("leadership", out _));
        Assert.False(receiptlessFaction.TryGetProperty("charter", out _));
        Assert.False(receiptlessFaction.TryGetProperty("materialization", out _));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RawFenceRejectsMissingShiningSemanticsBeforeNormalization()
    {
        var faction = CreateCompleteAuthoredShiningFaction(
            "shine_faction_unlaundered",
            "hall_unlaundered");
        faction.Remove("charter");
        faction.Remove("leadership");
        faction.Remove(ShiningAbodeState.FactionStrategicMemoryProperty);
        var current = CreateAuthoredShiningRoot(
            faction,
            CreateShiningHall("hall_unlaundered", "Unlaundered Hall"));
        var preTurn = ShiningAbodeState.CreateDefaultState();
        preTurn["factions"] = new JsonArray();
        preTurn["halls"] = new JsonArray();

        await _fs.WriteFileAtomicAsync(
            ShiningAbodeState.StatePath,
            current.ToJsonString());
        var backupPath = await WriteValidatedShiningSnapshotAsync(preTurn);

        var validator = new ValidationService(
            _fs,
            NullLogger<ValidationService>.Instance);
        var issues =
            await validator.ValidateAcceptedTurnRawFactionMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_shining_charter_missing");
        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_shining_leadership_missing");
        Assert.Contains(issues, issue =>
            issue.Code == "faction_materialization_shining_memory_missing");

        var normalizer = new CanonicalStateNormalizer(
            _fs,
            NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ShiningAbodeState.StatePath] = backupPath
            });

        using var doc = JsonDocument.Parse(
            (await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!);
        var normalizedFaction = Assert.Single(
            doc.RootElement.GetProperty("factions").EnumerateArray());
        Assert.False(normalizedFaction.TryGetProperty("charter", out _));
        Assert.False(normalizedFaction.TryGetProperty("leadership", out _));
        Assert.False(normalizedFaction.TryGetProperty(
            ShiningAbodeState.FactionStrategicMemoryProperty,
            out _));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MaterializedGuardianFactionPreservesAuthoredSemantics()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/meta/guardians.json",
            """
            {
              "guardians": [
                {
                  "guardianId": "guardian_authority",
                  "canonicalName": "Derived Guardian",
                  "domain": "memory",
                  "abode": {
                    "abodeId": "abode_authority",
                    "abodeName": "Derived Guardian Hall"
                  }
                }
              ],
              "activeGuardian": {
                "guardianId": "guardian_authority",
                "canonicalName": "Derived Guardian",
                "domain": "memory",
                "abode": {
                  "abodeId": "abode_authority",
                  "abodeName": "Derived Guardian Hall"
                }
              }
            }
            """);

        var faction = CreateCompleteAuthoredShiningFaction(
            "faction_guardian_authority",
            "hall_authored_guardian");
        faction["originType"] = ShiningAbodeState.OriginTypeAscendedGuardian;
        faction["creationProvenance"] = new JsonObject
        {
            ["route"] = "story",
            ["authorityType"] = "guardian_ascension",
            ["authorityId"] = "guardian_authority"
        };
        faction["storyAuthority"] = new JsonObject
        {
            ["authorityType"] = "guardian_ascension",
            ["authorityId"] = "guardian_authority",
            ["factionRole"] = "patron_guardian"
        };
        faction["leadership"] = new JsonObject
        {
            ["leadershipState"] = ShiningAbodeState.LeadershipStateSecure,
            ["headActorType"] = ShiningAbodeState.HeadActorTypeGuardian,
            ["headActorId"] = "guardian_authority"
        };
        faction["materialization"]!["capabilities"]!["usesStoryState"] = true;
        faction["materialization"]!["sections"]!["storyState"] =
            new JsonObject
            {
                ["state"] = "populated"
            };
        var authoredHall = CreateShiningHall(
            "hall_authored_guardian",
            "Authored Guardian Hall");
        authoredHall["description"] =
            "Authored hall semantics must remain unchanged.";
        authoredHall["serviceTags"] = new JsonArray("lore");
        var current = CreateAuthoredShiningRoot(faction, authoredHall);
        await _fs.WriteFileAtomicAsync(
            ShiningAbodeState.StatePath,
            current.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(
            _fs,
            NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        using var doc = JsonDocument.Parse(
            (await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!);
        var root = doc.RootElement;
        var normalizedFaction = Assert.Single(
            root.GetProperty("factions").EnumerateArray());
        Assert.Equal(
            "hall_authored_guardian",
            normalizedFaction.GetProperty("hallId").GetString());
        Assert.Equal(
            "Dawn Archive",
            normalizedFaction
                .GetProperty("charter")
                .GetProperty("factionName")
                .GetString());
        Assert.Equal(
            "Recover the names erased from the western gallery.",
            normalizedFaction.GetProperty("currentAgenda").GetString());
        Assert.Equal(
            "guardian_authority",
            normalizedFaction
                .GetProperty("creationProvenance")
                .GetProperty("authorityId")
                .GetString());
        Assert.Equal(
            "The Archive remembers the first dimming.",
            normalizedFaction
                .GetProperty(ShiningAbodeState.FactionStrategicMemoryProperty)
                .GetProperty("summary")
                .GetString());
        Assert.Equal(
            "The Dawn Archive opened its hall.",
            normalizedFaction
                .GetProperty(ShiningAbodeState.FactionChronicleProperty)[0]
                .GetProperty("summary")
                .GetString());

        var normalizedHall = Assert.Single(
            root.GetProperty("halls").EnumerateArray());
        Assert.Equal(
            "hall_authored_guardian",
            normalizedHall.GetProperty("hallId").GetString());
        Assert.Equal(
            "Authored Guardian Hall",
            normalizedHall.GetProperty("hallName").GetString());
        Assert.DoesNotContain(
            root.GetProperty("halls").EnumerateArray(),
            hall => string.Equals(
                hall.GetProperty("hallId").GetString(),
                "hall_guardian_authority",
                StringComparison.Ordinal));
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

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesShiningFactionPoliticalUpdateSurfaces()
    {
        var baseline = ShiningAbodeState.CreateDefaultState();
        baseline["halls"] = new JsonArray(CreateShiningHall("hall_mirror", "Зал Зеркал"));
        baseline["factions"] = new JsonArray(CreateShiningFaction("faction_mirrors", "hall_mirror", "project_mirror"));

        var commandRoot = new JsonObject
        {
            [ShiningAbodeState.FactionChronicleUpdatesProperty] = new JsonArray(new JsonObject
            {
                ["factionId"] = "faction_mirrors",
                ["entryId"] = "chronicle_turn_88",
                ["turnNumber"] = 88,
                ["eventType"] = "political_setback",
                ["summary"] = "Фракция потеряла право голоса в Зале Зеркал.",
                ["visibility"] = "known",
                ["consequences"] = new JsonArray("influence_reduced")
            }),
            [ShiningAbodeState.FactionInfluenceUpdatesProperty] = new JsonArray(new JsonObject
            {
                ["factionId"] = "faction_mirrors",
                ["zoneId"] = "zone_hall_mirror",
                ["scopeType"] = "hall",
                ["scopeId"] = "hall_mirror",
                ["displayName"] = "Зал Зеркал",
                ["controlLevel"] = 72,
                ["influenceValue"] = 72,
                ["publicStatus"] = "dominant",
                ["updatedAtTurn"] = 88,
                ["sourceEntryId"] = "chronicle_turn_88"
            }),
            [ShiningAbodeState.FactionStrategicMemoryUpdatesProperty] = new JsonArray(new JsonObject
            {
                ["factionId"] = "faction_mirrors",
                ["summary"] = "Фракция больше не может действовать открыто.",
                ["lastUpdatedTurn"] = 88,
                ["recentCampaigns"] = new JsonArray("campaign_mirror_trial"),
                ["losses"] = new JsonArray("lost_hall_vote"),
                ["alliances"] = new JsonArray("faction_lanterns"),
                ["enemies"] = new JsonArray("faction_wings")
            }),
            [ShiningAbodeState.FactionResourceLedgerUpdatesProperty] = new JsonArray(new JsonObject
            {
                ["factionId"] = "faction_mirrors",
                ["entryId"] = "ledger_turn_88",
                ["turnNumber"] = 88,
                ["resourceType"] = "light_sparks",
                ["delta"] = -12,
                ["balanceAfter"] = 31,
                ["reason"] = "Цена политического поражения."
            })
        };

        const string backupPath = "test_backups/shining_abode_state_baseline_politics.json";
        await _fs.WriteFileAtomicAsync(backupPath, baseline.ToJsonString());
        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, commandRoot.ToJsonString());

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiningAbodeState.StatePath] = backupPath
        });

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync(ShiningAbodeState.StatePath))!);
        var root = doc.RootElement;
        Assert.False(root.TryGetProperty(ShiningAbodeState.FactionChronicleUpdatesProperty, out _));
        Assert.False(root.TryGetProperty(ShiningAbodeState.FactionInfluenceUpdatesProperty, out _));

        var faction = Assert.Single(root.GetProperty("factions").EnumerateArray(), item => item.GetProperty("factionId").GetString() == "faction_mirrors");
        Assert.Contains(faction.GetProperty(ShiningAbodeState.FactionChronicleProperty).EnumerateArray(), entry =>
            entry.GetProperty("entryId").GetString() == "chronicle_turn_88" &&
            entry.GetProperty("summary").GetString()!.Contains("Зале Зеркал", StringComparison.Ordinal));
        Assert.Contains(faction.GetProperty(ShiningAbodeState.FactionInfluenceProperty).EnumerateArray(), zone =>
            zone.GetProperty("zoneId").GetString() == "zone_hall_mirror" &&
            zone.GetProperty("controlLevel").GetInt32() == 72);
        Assert.Equal("Фракция больше не может действовать открыто.", faction.GetProperty(ShiningAbodeState.FactionStrategicMemoryProperty).GetProperty("summary").GetString());
        Assert.Contains(faction.GetProperty(ShiningAbodeState.FactionResourceLedgerProperty).EnumerateArray(), entry =>
            entry.GetProperty("entryId").GetString() == "ledger_turn_88" &&
            entry.GetProperty("delta").GetInt32() == -12);
    }

    private async Task<string> WriteValidatedShiningSnapshotAsync(
        JsonObject snapshotRoot)
    {
        const string sessionId = "session_shining_laundering_fence";
        const string requestId = "request_shining_laundering_fence";
        const string playerAction =
            "Validate the authored Shining faction before normalization.";
        const string snapshotPath =
            "game_state/control/pending_turn_snapshot/game_state/meta/shining_abode_state.json";
        var snapshotJson = snapshotRoot.ToJsonString();

        await _fs.WriteFileAtomicAsync(
            "input/turn_request.json",
            $$"""
              {
                "sessionId": "{{sessionId}}",
                "requestId": "{{requestId}}",
                "turnNumber": 12,
                "playerAction": "{{playerAction}}"
              }
              """);
        await _fs.WriteFileAtomicAsync(snapshotPath, snapshotJson);

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = 12,
            ["requestTimestamp"] = "2026-08-03T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = new JsonObject
            {
                [ShiningAbodeState.StatePath] = snapshotPath
            },
            ["snapshotFileHashes"] = new JsonObject
            {
                [ShiningAbodeState.StatePath] =
                    PendingTurnSnapshotAuthority.ComputeSha256(snapshotJson)
            },
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = new JsonArray(
                ShiningAbodeState.StatePath),
            ["sourceLabel"] = "accepted Shining faction turn",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(
                manifest);
        await _fs.WriteFileAtomicAsync(
            "game_state/control/pending_turn_snapshot.json",
            manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority
            .SyncAuthorityForCurrentManifestAsync(_fs);
        return snapshotPath;
    }

    private static JsonObject CreateAuthoredShiningRoot(
        JsonObject faction,
        JsonObject hall)
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root["availability"] = ShiningAbodeState.AvailabilityActive;
        root["radiance"] = new JsonObject
        {
            ["experience"] = 40,
            ["tier"] = 1
        };
        root["halls"] = new JsonArray(hall);
        root["factions"] = new JsonArray(faction);
        root["shiningPoliticalActors"] = new JsonArray();
        return root;
    }

    private static JsonObject CreateCompleteAuthoredShiningFaction(
        string factionId,
        string hallId) =>
        new()
        {
            ["factionId"] = factionId,
            ["originType"] = ShiningAbodeState.OriginTypeNativeRadiant,
            ["hallId"] = hallId,
            ["creationProvenance"] = new JsonObject
            {
                ["route"] = "native_discovery",
                ["authorityType"] = "shining_core_action_request",
                ["authorityId"] = $"request_{factionId}"
            },
            ["charter"] = new JsonObject
            {
                ["factionName"] = "Dawn Archive",
                ["favoredArchetype"] =
                    ShiningAbodeState.ProjectArchetypeRemembrance,
                ["patronEffectFamily"] =
                    ShiningAbodeState.EffectFamilyMemory,
                ["summary"] = "Preserve the truths carried into light."
            },
            ["currentAgenda"] =
                "Recover the names erased from the western gallery.",
            ["visibility"] = "revealed",
            ["storyAuthority"] = null,
            ["factionLifecycle"] = new JsonObject
            {
                ["state"] = ShiningAbodeState.FactionLifecycleStateActive
            },
            ["leadership"] = new JsonObject
            {
                ["leadershipState"] =
                    ShiningAbodeState.LeadershipStateSecure,
                ["headActorType"] =
                    ShiningAbodeState.HeadActorTypePlayerSoul,
                ["headActorId"] =
                    ShiningAbodeState.HeadActorTypePlayerSoul
            },
            [ShiningAbodeState.FactionStrategicMemoryProperty] =
                new JsonObject
                {
                    ["summary"] =
                        "The Archive remembers the first dimming.",
                    ["lastUpdatedTurn"] = 12,
                    ["recentCampaigns"] = new JsonArray(),
                    ["losses"] = new JsonArray(),
                    ["alliances"] = new JsonArray(),
                    ["enemies"] = new JsonArray()
                },
            [ShiningAbodeState.FactionChronicleProperty] =
                new JsonArray(
                    new JsonObject
                    {
                        ["entryId"] =
                            $"chronicle_{factionId}_founding",
                        ["turnNumber"] = 12,
                        ["eventType"] = "faction_materialized",
                        ["summary"] =
                            "The Dawn Archive opened its hall.",
                        ["visibility"] = "known",
                        ["consequences"] = new JsonArray()
                    }),
            ["baseStrength"] = 30,
            ["factionStrength"] = 30,
            ["investCountThisAscension"] = 0,
            ["projectArchetypesCountedThisAscension"] = new JsonArray(),
            ["projects"] = new JsonArray(),
            [ShiningAbodeState.FactionInfluenceProperty] = new JsonArray(),
            [ShiningAbodeState.FactionResourceLedgerProperty] =
                new JsonArray(),
            ["tradeInventory"] = null,
            ["tradeInventoryReceipts"] = new JsonArray(),
            ["leadershipReceipts"] = new JsonArray(),
            ["leadershipHistory"] = new JsonArray(),
            ["materialization"] =
                CreateShiningMaterializationEnvelope(factionId)
        };

    private static JsonObject CreateShiningMaterializationEnvelope(
        string factionId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = $"fmat_{factionId}_12",
            ["factionType"] = "shining_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["runsProjects"] = false,
                ["holdsTerritorialInfluence"] = false,
                ["usesResourceLedger"] = false,
                ["hasResidentAffiliations"] = false,
                ["canTrade"] = true,
                ["hasLeadershipHistory"] = false,
                ["usesStoryState"] = false
            },
            ["sections"] = new JsonObject
            {
                ["projects"] =
                    ShiningEmptyDisposition("No projects exist yet."),
                ["territorialInfluence"] =
                    ShiningEmptyDisposition("No influence exists yet."),
                ["resourceLedger"] =
                    ShiningEmptyDisposition("No resource ledger exists yet."),
                ["residentAffiliations"] =
                    ShiningEmptyDisposition("No affiliations exist yet."),
                ["trade"] =
                    ShiningEmptyDisposition("No trade inventory exists yet."),
                ["leadershipHistory"] =
                    ShiningEmptyDisposition("No leadership history exists yet."),
                ["storyState"] =
                    ShiningEmptyDisposition("No story state exists yet.")
            }
        };

    private static JsonObject ShiningEmptyDisposition(string reason) =>
        new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };

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
