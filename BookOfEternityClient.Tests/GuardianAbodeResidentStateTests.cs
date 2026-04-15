using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GuardianAbodeResidentStateTests
{
    [Fact]
    public void NormalizeResidentObject_BackfillsPersonalityDispositionAndDerivedAbodeState()
    {
        var resident = JsonNode.Parse("""
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
          "historyRevealed": true,
          "availableInteractions": ["talk", "history"],
          "isPresent": true,
          "mortalWorldImprint": {
            "originWorldSummary": "Была посланницей между осаждёнными городами.",
            "futureCompanionPrompt": "Messenger with ember scarf",
            "bondReason": "Осталась ради долга и дороги.",
            "coreTraits": ["loyal", "restless"],
            "archetypeHints": ["road-keeper"]
          }
        }
        """)!.AsObject();

        GuardianAbodeResidentState.NormalizeResidentObject(resident, currentAbodePower: 72);

        Assert.Equal("trusted", resident["bondTier"]?.GetValue<string>());
        Assert.True(resident["personalityProfile"] is JsonObject);
        Assert.True(resident["abodeDisposition"] is JsonObject);
        Assert.False(string.IsNullOrWhiteSpace(resident["personalityProfile"]?["archetype"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(resident["personalityProfile"]?["worldview"]?.GetValue<string>()));
        Assert.False(string.IsNullOrWhiteSpace(resident["personalityProfile"]?["culturalLayer"]?.GetValue<string>()));
        Assert.True(resident["personalityProfile"]?["coreValues"] is JsonArray coreValues && coreValues.Count > 0);
        Assert.True(resident["personalityProfile"]?["personalityTraits"] is JsonArray personalityTraits && personalityTraits.Count > 0);
        Assert.True(GuardianAbodeResidentState.IsSupportedPowerSensitivity(resident["abodeDisposition"]?["powerSensitivity"]?.GetValue<string>()));
        Assert.True(GuardianAbodeResidentState.IsSupportedMigrationDisposition(resident["abodeDisposition"]?["migrationDisposition"]?.GetValue<string>()));
        Assert.True(GuardianAbodeResidentState.IsSupportedCommunalOrientation(resident["abodeDisposition"]?["communalOrientation"]?.GetValue<string>()));
        Assert.True(GuardianAbodeResidentState.IsSupportedStabilityNeed(resident["abodeDisposition"]?["stabilityNeed"]?.GetValue<string>()));

        var abodeDevotionLevel = resident["abodeDevotionLevel"]!.GetValue<int>();
        var restlessness = resident["restlessness"]!.GetValue<int>();
        Assert.InRange(abodeDevotionLevel, 0, 100);
        Assert.InRange(restlessness, 0, 100);
        Assert.Equal(
            GuardianAbodeResidentState.ResolveAbodeDevotionTier(abodeDevotionLevel),
            resident["abodeDevotionTier"]?.GetValue<string>());
        Assert.Equal(
            GuardianAbodeResidentState.ResolveMigrationState(abodeDevotionLevel, restlessness),
            resident["migrationState"]?.GetValue<string>());
    }

    [Fact]
    public void CollectEntries_ProjectsBackfilledPersonalityAndDevotionForLegacyResidents()
    {
        using var doc = JsonDocument.Parse("""
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
                "futureCompanionPrompt": "Messenger",
                "coreTraits": ["loyal"],
                "archetypeHints": ["road-keeper"]
              }
            }
          ]
        }
        """);

        var residents = GuardianAbodeResidentState.CollectEntries(doc.RootElement, "guardian_azalia", "abode_threads", currentAbodePower: 72);

        var resident = Assert.Single(residents);
        Assert.False(string.IsNullOrWhiteSpace(resident.PersonalityProfile.Archetype));
        Assert.False(string.IsNullOrWhiteSpace(resident.PersonalityProfile.Worldview));
        Assert.NotEmpty(resident.PersonalityProfile.CoreValues);
        Assert.NotEmpty(resident.PersonalityProfile.PersonalityTraits);
        Assert.True(GuardianAbodeResidentState.IsSupportedAbodeDevotionTier(resident.AbodeDevotionTier));
        Assert.True(GuardianAbodeResidentState.IsSupportedMigrationState(resident.MigrationState));
        Assert.InRange(resident.AbodeDevotionLevel, 0, 100);
        Assert.InRange(resident.Restlessness, 0, 100);
    }

    [Fact]
    public void BuildCompanionSeed_CapturesResidentPersonalityAndAbodeSnapshot()
    {
        var resident = JsonNode.Parse("""
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
          "mortalWorldImprint": {
            "originWorldSummary": "Была посланницей.",
            "futureCompanionPrompt": "Messenger",
            "bondReason": "Осталась ради долга.",
            "coreTraits": ["loyal"],
            "archetypeHints": ["road-keeper"],
            "appearanceMotifs": ["ember scarf"]
          }
        }
        """)!.AsObject();

        var companionSeed = GuardianAbodeResidentState.BuildCompanionSeed(resident);

        Assert.True(companionSeed["personalityProfile"] is JsonObject);
        Assert.True(companionSeed["abodeDisposition"] is JsonObject);
        Assert.InRange(companionSeed["abodeDevotionLevel"]!.GetValue<int>(), 0, 100);
        Assert.InRange(companionSeed["restlessness"]!.GetValue<int>(), 0, 100);
        Assert.Equal(
            GuardianAbodeResidentState.ResolveAbodeDevotionTier(companionSeed["abodeDevotionLevel"]!.GetValue<int>()),
            companionSeed["abodeDevotionTier"]?.GetValue<string>());
        Assert.Equal(
            GuardianAbodeResidentState.ResolveMigrationState(
                companionSeed["abodeDevotionLevel"]!.GetValue<int>(),
                companionSeed["restlessness"]!.GetValue<int>()),
            companionSeed["migrationState"]?.GetValue<string>());
    }

    [Fact]
    public void ProjectCanonicalAbodeDrift_PowerDeclineUsesDispositionAndBondProtection()
    {
        var previousResident = JsonNode.Parse("""
        {
          "residentId": "resident_liora",
          "guardianId": "guardian_azalia",
          "abodeId": "abode_threads",
          "displayName": "Лиора",
          "residentKind": "attendant_spirit",
          "originType": "native_spirit",
          "roleLabel": "Хранительница порога",
          "summary": "Слушает дыхание Обители.",
          "bondLevel": 52,
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
        """)!.AsObject();
        var currentResident = previousResident.DeepClone().AsObject();
        var context = new GuardianAbodeResidentState.ResidentAbodeDriftContext
        {
            TouchesResidentTurnSurface = true,
            PreviousAbodePower = 72,
            CurrentAbodePower = 24,
            HasPowerTierDecline = true
        };

        var projection = GuardianAbodeResidentState.ProjectCanonicalAbodeDrift(previousResident, currentResident, context);

        Assert.True(projection.HasCanonicalTrigger);
        Assert.Equal(63, projection.AbodeDevotionLevel);
        Assert.Equal(25, projection.Restlessness);
        Assert.Equal(
            GuardianAbodeResidentState.ResolveAbodeDevotionTier(projection.AbodeDevotionLevel),
            projection.AbodeDevotionTier);
        Assert.Equal(
            GuardianAbodeResidentState.ResolveMigrationState(projection.AbodeDevotionLevel, projection.Restlessness),
            projection.MigrationState);
    }

    [Fact]
    public void ProjectCanonicalAbodeDrift_QuestAndRewardProgressStayBounded()
    {
        var previousResident = JsonNode.Parse("""
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
          "abodeDisposition": {
            "powerSensitivity": "medium",
            "migrationDisposition": "selective",
            "communalOrientation": "high",
            "stabilityNeed": "medium"
          },
          "abodeDevotionLevel": 55,
          "abodeDevotionTier": "attached",
          "restlessness": 40,
          "migrationState": "wavering",
          "mortalWorldImprint": {
            "originWorldSummary": "Была посланницей.",
            "futureCompanionPrompt": "Messenger"
          }
        }
        """)!.AsObject();
        var currentResident = previousResident.DeepClone().AsObject();
        var context = new GuardianAbodeResidentState.ResidentAbodeDriftContext
        {
            TouchesResidentTurnSurface = true,
            PreviousAbodePower = 58,
            CurrentAbodePower = 58,
            HasQuestProgress = true,
            HasRewardFulfilled = true
        };

        var projection = GuardianAbodeResidentState.ProjectCanonicalAbodeDrift(previousResident, currentResident, context);

        Assert.True(projection.HasCanonicalTrigger);
        Assert.Equal(63, projection.AbodeDevotionLevel);
        Assert.Equal(35, projection.Restlessness);
        Assert.Equal(
            GuardianAbodeResidentState.ResolveAbodeDevotionTier(projection.AbodeDevotionLevel),
            projection.AbodeDevotionTier);
        Assert.Equal(
            GuardianAbodeResidentState.ResolveMigrationState(projection.AbodeDevotionLevel, projection.Restlessness),
            projection.MigrationState);
    }

    [Fact]
    public void BuildCanonicalTransferArrivalResident_ReanchorsResidentToTargetAbodeBaseline()
    {
        var resident = JsonNode.Parse("""
        {
          "residentId": "resident_liora",
          "guardianId": "guardian_azalia",
          "abodeId": "abode_threads",
          "displayName": "Лиора",
          "residentKind": "wayfaring_soul",
          "originType": "traveler_soul",
          "bondLevel": 61,
          "bondTier": "trusted",
          "canGrantCompanionRelic": true,
          "bondRewardState": "none",
          "historyRevealed": true,
          "isPresent": true,
          "abodeDisposition": {
            "powerSensitivity": "medium",
            "migrationDisposition": "selective",
            "communalOrientation": "high",
            "stabilityNeed": "medium"
          },
          "abodeDevotionLevel": 14,
          "abodeDevotionTier": "alienated",
          "restlessness": 82,
          "migrationState": "ready_to_transfer",
          "mortalWorldImprint": {
            "originWorldSummary": "Была посланницей.",
            "futureCompanionPrompt": "Messenger"
          }
        }
        """)!.AsObject();

        var arrivalResident = GuardianAbodeResidentState.BuildCanonicalTransferArrivalResident(resident, targetAbodePower: 74);

        Assert.True(arrivalResident["isPresent"]?.GetValue<bool>());
        var devotionLevel = arrivalResident["abodeDevotionLevel"]!.GetValue<int>();
        var restlessness = arrivalResident["restlessness"]!.GetValue<int>();
        Assert.InRange(devotionLevel, 0, 100);
        Assert.InRange(restlessness, 0, 100);
        Assert.Equal(
            GuardianAbodeResidentState.ResolveAbodeDevotionTier(devotionLevel),
            arrivalResident["abodeDevotionTier"]?.GetValue<string>());
        Assert.Equal(
            GuardianAbodeResidentState.ResolveMigrationState(devotionLevel, restlessness),
            arrivalResident["migrationState"]?.GetValue<string>());
        Assert.NotEqual(GuardianAbodeResidentState.MigrationStateReadyToTransfer, arrivalResident["migrationState"]?.GetValue<string>());
    }
}
