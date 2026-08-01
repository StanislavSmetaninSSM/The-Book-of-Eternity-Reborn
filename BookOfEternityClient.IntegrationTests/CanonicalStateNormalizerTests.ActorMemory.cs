using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MergesGuardianAndResidentActorJournals()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianThoughtJournalState.StatePath, """
        {
          "guardianThoughtJournalUpdates": [
            {
              "entryId": "gthought_1",
              "guardianId": "guardian_azalia",
              "turn": 12,
              "timestamp": "2026-03-27T10:00:00Z",
              "title": "Внимательный интерес",
              "summary": "Азалия присматривается к душе."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, """
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "guardianId": "guardian_azalia",
              "abodeId": "abode_threads",
              "displayName": "Лиора",
              "residentKind": "wayfaring_soul",
              "originType": "traveler_soul",
              "roleLabel": "Вестница",
              "summary": "Слушает нити дорог.",
              "bondLevel": 61,
              "bondTier": "trusted",
              "canGrantCompanionRelic": true,
              "bondRewardState": "none",
              "linkedSoulQuestId": "",
              "grantedRelicId": "",
              "historyRevealed": false,
              "availableInteractions": ["talk"],
              "isPresent": true,
              "mortalWorldImprint": {
                "originWorldSummary": "Была посланницей.",
                "futureCompanionPrompt": "Messenger"
              }
            }
          ],
          "residentThoughtJournalUpdates": [
            {
              "entryId": "rthought_1",
              "residentId": "resident_liora",
              "turn": 12,
              "timestamp": "2026-03-27T10:01:00Z",
              "title": "Ждёт честности",
              "summary": "Лиора хочет понять, не солжёт ли ей душа."
            }
          ],
          "residentInteractionLogUpdates": [
            {
              "entryId": "revent_1",
              "residentId": "resident_liora",
              "turn": 12,
              "timestamp": "2026-03-27T10:02:00Z",
              "eventType": "conversation",
              "title": "Разговор у края сада",
              "summary": "Лиора призналась, что боится опоздать снова."
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/soul_state.json"] = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json"
        });

        var guardianThoughtJson = await _fs.ReadFileAsync(GuardianThoughtJournalState.StatePath);
        Assert.NotNull(guardianThoughtJson);
        Assert.Contains("\"entries\": [", guardianThoughtJson, StringComparison.Ordinal);
        Assert.DoesNotContain("guardianThoughtJournalUpdates", guardianThoughtJson, StringComparison.Ordinal);

        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        Assert.NotNull(residentJson);
        Assert.Contains("\"thoughtJournal\": [", residentJson, StringComparison.Ordinal);
        Assert.Contains("\"interactionLog\": [", residentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("residentThoughtJournalUpdates", residentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("residentInteractionLogUpdates", residentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_ProjectsCanonicalResidentAbodeDriftForTouchedResidentTurn()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_guardians_resident_drift.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Ясный прилив."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_threads", "title": "Сад Нитей" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 32, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 72, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Ясный прилив."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_threads", "title": "Сад Нитей" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 32, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 72, "tier": "Могущественная", "lastUpdatedAt": "2026-03-24T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 1, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
              "manifestation": {
                "currentDisplayName": "Азалия",
                "formFlexibility": "selective",
                "currentPresentationStyle": "feminine",
                "currentPronouns": "она/её",
                "appearanceDescription": "Прилив ослаб."
              },
              "manifestationHistory": [],
              "domain": "Tide",
              "abode": { "abodeId": "abode_threads", "title": "Сад Нитей" },
              "personalityProfile": {
                "archetype": "Tide Keeper",
                "speechPattern": "Measured and tidal",
                "coreValues": [ "balance", "memory", "patience" ]
              },
              "relationshipData": { "currentReputation": 32, "reputationHistory": [], "lastInteraction": null },
              "abodePower": { "currentPower": 24, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
              "guardianRelationships": [],
              "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
              "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "nameVariants": { "default": "Азалия", "feminine": "Азалия", "masculine": null, "neutral": null },
            "manifestation": {
              "currentDisplayName": "Азалия",
              "formFlexibility": "selective",
              "currentPresentationStyle": "feminine",
              "currentPronouns": "она/её",
              "appearanceDescription": "Прилив ослаб."
            },
            "manifestationHistory": [],
            "domain": "Tide",
            "abode": { "abodeId": "abode_threads", "title": "Сад Нитей" },
            "personalityProfile": {
              "archetype": "Tide Keeper",
              "speechPattern": "Measured and tidal",
              "coreValues": [ "balance", "memory", "patience" ]
            },
            "relationshipData": { "currentReputation": 32, "reputationHistory": [], "lastInteraction": null },
            "abodePower": { "currentPower": 24, "tier": "Хрупкая", "lastUpdatedAt": "2026-03-28T00:00:00Z", "history": [] },
            "guardianRelationships": [],
            "questManagement": { "availableQuests": [], "activeQuests": [], "completedQuests": [] },
            "gachaSystem": { "chargesPerReturn": 0, "chargesUsedThisReturn": 0, "gachaHistory": [] }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("test_backups/preturn_residents_resident_drift.json", """
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "guardianId": "guardian_azalia",
              "abodeId": "abode_threads",
              "displayName": "Лиора",
              "residentKind": "attendant_spirit",
              "originType": "native_spirit",
              "roleLabel": "Смотрительница сада",
              "summary": "Слушает дыхание нитей.",
              "bondLevel": 52,
              "bondTier": "trusted",
              "canGrantCompanionRelic": false,
              "bondRewardState": "none",
              "linkedSoulQuestId": "",
              "grantedRelicId": "",
              "historyRevealed": false,
              "availableInteractions": [ "talk" ],
              "isPresent": true,
              "abodeDisposition": {
                "powerSensitivity": "high",
                "migrationDisposition": "rooted",
                "communalOrientation": "high",
                "stabilityNeed": "high"
              },
              "abodeDevotionLevel": 68,
              "abodeDevotionTier": "devoted",
              "restlessness": 22,
              "migrationState": "settled",
              "mortalWorldImprint": {
                "originWorldSummary": "Дух дома.",
                "futureCompanionPrompt": "Threshold keeper"
              }
            }
          ],
          "rosterReceipts": [],
          "interactionReceipts": [],
          "historyLog": [],
          "thoughtJournal": [],
          "interactionLog": []
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath, """
        {
          "UpdateGuardianAbodeResidents": [
            {
              "residentId": "resident_liora",
              "guardianId": "guardian_azalia",
              "abodeId": "abode_threads",
              "displayName": "Лиора",
              "residentKind": "attendant_spirit",
              "originType": "native_spirit",
              "roleLabel": "Смотрительница сада",
              "summary": "Слушает дыхание нитей.",
              "bondLevel": 52,
              "bondTier": "trusted",
              "canGrantCompanionRelic": false,
              "bondRewardState": "none",
              "linkedSoulQuestId": "",
              "grantedRelicId": "",
              "historyRevealed": false,
              "availableInteractions": [ "talk" ],
              "isPresent": true,
              "abodeDisposition": {
                "powerSensitivity": "high",
                "migrationDisposition": "rooted",
                "communalOrientation": "high",
                "stabilityNeed": "high"
              },
              "abodeDevotionLevel": 95,
              "abodeDevotionTier": "steadfast",
              "restlessness": 0,
              "migrationState": "settled",
              "mortalWorldImprint": {
                "originWorldSummary": "Дух дома.",
                "futureCompanionPrompt": "Threshold keeper"
              }
            }
          ],
          "residentThoughtJournalUpdates": [
            {
              "entryId": "resident_drift_memory_1",
              "residentId": "resident_liora",
              "turn": 12,
              "timestamp": "2026-03-28T00:10:00Z",
              "title": "Сад стих",
              "summary": "Лиора чувствует, как Обитель ослабла.",
              "eventType": "abode_pressure",
              "consequence": "watchful"
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/guardians.json"] = "test_backups/preturn_guardians_resident_drift.json",
            [GuardianAbodeResidentState.StatePath] = "test_backups/preturn_residents_resident_drift.json"
        });

        var residentJson = await _fs.ReadFileAsync(GuardianAbodeResidentState.StatePath);
        Assert.NotNull(residentJson);
        Assert.Contains("\"abodeDevotionLevel\": 63", residentJson, StringComparison.Ordinal);
        Assert.Contains("\"restlessness\": 25", residentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"abodeDevotionLevel\": 95", residentJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedInkFeatherBuckets_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": {
            "current": 10,
            "total": 10
          },
          "metaStateUpdates": {
            "inkFeatherChanges": {
              "add": "5",
              "spend": "3"
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("metaStateUpdates.inkFeatherChanges", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.True(soulDoc.RootElement.TryGetProperty("metaStateUpdates", out var metaState));
        Assert.Equal("5", metaState.GetProperty("inkFeatherChanges").GetProperty("add").GetString());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedSoulRelicOperations_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "soulRelics": {
            "equipped": [],
            "stored": []
          },
          "metaStateUpdates": {
            "soulRelicOperations": {
              "unknownRelicOp": {
                "relicId": "relic_alpha"
              }
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("metaStateUpdates.soulRelicOperations", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.True(soulDoc.RootElement.TryGetProperty("metaStateUpdates", out var metaState));
        Assert.True(metaState.GetProperty("soulRelicOperations").TryGetProperty("unknownRelicOp", out _));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedTopLevelMetaStateUpdates_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "metaStateUpdates": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current metaStateUpdates", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Array, soulDoc.RootElement.GetProperty("metaStateUpdates").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_UnknownMetaStateUpdatesCommand_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "unknownCommand": {
              "value": 1
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("unknownCommand", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.True(soulDoc.RootElement.GetProperty("metaStateUpdates").TryGetProperty("unknownCommand", out _));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedAfterlifeArchiveUpdatesRoot_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "afterlifeArchive": {
            "stored": [],
            "actionReceipts": []
          },
          "afterlifeArchiveUpdates": {}
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current afterlifeArchiveUpdates", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Object, soulDoc.RootElement.GetProperty("afterlifeArchiveUpdates").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_NullAfterlifeArchiveUpdatesRoot_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "afterlifeArchive": {
            "stored": [],
            "actionReceipts": []
          },
          "afterlifeArchiveUpdates": null
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current afterlifeArchiveUpdates", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Null, soulDoc.RootElement.GetProperty("afterlifeArchiveUpdates").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedArchiveActionResolutionsRoot_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "afterlifeArchive": {
            "stored": [],
            "actionReceipts": []
          },
          "archiveActionResolutions": {}
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current archiveActionResolutions", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Object, soulDoc.RootElement.GetProperty("archiveActionResolutions").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_NullArchiveActionResolutionsRoot_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "afterlifeArchive": {
            "stored": [],
            "actionReceipts": []
          },
          "archiveActionResolutions": null
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current archiveActionResolutions", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Null, soulDoc.RootElement.GetProperty("archiveActionResolutions").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedLifeTransitions_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": {},
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": []
              }
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("metaStateUpdates.lifeTransitions", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.True(soulDoc.RootElement.GetProperty("metaStateUpdates").TryGetProperty("lifeTransitions", out _));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedMemoryLegacyGrant_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "memoryLegacyGrant": {
              "legacyId": "legacy_alpha",
              "legacyType": "startingCharacteristicBonus",
              "sourceLifeHint": "life_001",
              "characteristic": "strength",
              "bonus": 1
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("metaStateUpdates.memoryLegacyGrant", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.True(soulDoc.RootElement.GetProperty("metaStateUpdates").TryGetProperty("memoryLegacyGrant", out _));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedEnlightenmentProgression_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "enlightenmentProgression": {
              "foo": 1
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("metaStateUpdates.enlightenmentProgression", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.True(soulDoc.RootElement.GetProperty("metaStateUpdates").TryGetProperty("enlightenmentProgression", out _));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RecordLifeCompletionWithoutTriggerLifeEnd_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": {},
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("recordLifeCompletion", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.True(soulDoc.RootElement.GetProperty("metaStateUpdates").TryGetProperty("lifeTransitions", out _));
        Assert.False(soulDoc.RootElement.TryGetProperty("livesHistory", out _));
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RecordLifeCompletionWithCanonicalTriggerLifeEnd_Materializes()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death",
          "summary": "Жизнь завершена."
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": {},
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/soul_state.json"] = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json"
        });

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.False(soulDoc.RootElement.TryGetProperty("metaStateUpdates", out _));
        var livesHistory = soulDoc.RootElement.GetProperty("livesHistory");
        Assert.Equal(1, livesHistory.GetArrayLength());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_RecordLifeCompletionWithTriggerLifeEndMissingSummary_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/control/life_transitions.json", """
        {
          "reason": "Death"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 1,
          "metaStateUpdates": {
            "lifeTransitions": {
              "recordLifeCompletion": {
                "characterFinalState": {},
                "majorAchievements": [],
                "relationshipsFormed": [],
                "moralChoices": [],
                "skillsLearned": [],
                "enlightenmentGained": 0
              }
            }
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("recordLifeCompletion", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedCanonicalInkFeathersRoot_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": 5
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current inkFeathers", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Number, soulDoc.RootElement.GetProperty("inkFeathers").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CanonicalInkFeathersWithUnsupportedVisibleKey_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "inkFeathers": {
            "current": 5,
            "foo": 99
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current inkFeathers", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(99, soulDoc.RootElement.GetProperty("inkFeathers").GetProperty("foo").GetInt32());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedCanonicalSoulRelicsRoot_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "soulRelics": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current soulRelics", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Array, soulDoc.RootElement.GetProperty("soulRelics").ValueKind);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CanonicalSoulRelicsWithSkeletalRelic_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "soulRelics": {
            "equipped": [
              {}
            ],
            "stored": []
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current soulRelics", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(1, soulDoc.RootElement.GetProperty("soulRelics").GetProperty("equipped").GetArrayLength());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_CanonicalSoulRelicsWithEmbeddedImprintMissingTraits_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "soulRelics": {
            "equipped": [
              {
                "relicId": "relic_imprint_1",
                "name": "Печать Стража",
                "rarity": "Rare",
                "soulImprint": {
                  "imprintId": "imprint_guard_1",
                  "npcName": "Страж Кел",
                  "description": "Бывший страж северных ворот."
                }
              }
            ],
            "stored": []
          }
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current soulRelics", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal("relic_imprint_1", soulDoc.RootElement.GetProperty("soulRelics").GetProperty("equipped")[0].GetProperty("relicId").GetString());
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_MalformedCanonicalAfterlifeArchiveRoot_FailClosed()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", """
        {
          "turnNumber": 12
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 1,
          "afterlifeArchive": []
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => normalizer.NormalizeAccumulatedStateAsync());
        Assert.Contains("current afterlifeArchive", exception.Message, StringComparison.Ordinal);

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        Assert.Equal(JsonValueKind.Array, soulDoc.RootElement.GetProperty("afterlifeArchive").ValueKind);
    }
}
