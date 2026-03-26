using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeArchiveStateTests
{
    [Fact]
    public void NormalizeShape_CreatesStoredArray()
    {
        var root = new JsonObject();

        AfterlifeArchiveState.NormalizeShape(root);

        Assert.True(root["afterlifeArchive"] is JsonObject);
        Assert.True(root["afterlifeArchive"]!["stored"] is JsonArray);
        Assert.True(root["afterlifeArchive"]!["actionReceipts"] is JsonArray);
    }

    [Theory]
    [InlineData("Common", 1)]
    [InlineData("Uncommon", 1)]
    [InlineData("Rare", 2)]
    [InlineData("Epic", 3)]
    [InlineData("Legendary", 3)]
    [InlineData("Unique", 3)]
    public void ResolvePowerGainForArchiveRarity_UsesConfiguredBands(string rarity, int expected)
    {
        Assert.Equal(expected, AfterlifeArchiveState.ResolvePowerGainForArchiveRarity(rarity));
    }

    [Theory]
    [InlineData(AfterlifeArchiveState.EntryTypeLoreFragment, GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment)]
    [InlineData(AfterlifeArchiveState.EntryTypeSecretRecord, GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord)]
    public void EntryTypes_MapToExpectedOfferingTypes(string entryType, string expectedOfferingType)
    {
        Assert.True(AfterlifeArchiveState.TryGetOfferingTypeForEntryType(entryType, out var offeringType));
        Assert.Equal(expectedOfferingType, offeringType);
        Assert.True(AfterlifeArchiveState.OfferingTypeMatchesEntryType(offeringType, entryType));
    }

    [Fact]
    public void NormalizeShape_StripsLegacyArchiveSpecializationMetadata()
    {
        var root = new JsonObject
        {
            ["afterlifeArchive"] = new JsonObject
            {
                ["stored"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["archiveId"] = "archive_legacy_001",
                        ["entryType"] = AfterlifeArchiveState.EntryTypeLoreFragment,
                        ["title"] = "Старый фрагмент",
                        ["summary"] = "Описание",
                        ["rarity"] = "Rare",
                        ["sourceLife"] = 1,
                        ["sourceKind"] = AfterlifeArchiveState.SourceKindCodex,
                        ["codexCategory"] = "history",
                        ["facets"] = new JsonArray("politics", "technology")
                    }
                }
            }
        };

        AfterlifeArchiveState.NormalizeShape(root);

        var entry = AfterlifeArchiveState.EnsureStoredArray(root).OfType<JsonObject>().Single();
        Assert.False(entry.ContainsKey("codexCategory"));
        Assert.False(entry.ContainsKey("facets"));
    }

    [Fact]
    public void ApplyActionResolutions_AcceptedResolution_ConsumesReservedEntryAndWritesReceipt()
    {
        var root = new JsonObject
        {
            ["afterlifeArchive"] = new JsonObject
            {
                ["stored"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["archiveId"] = "archive_001",
                        ["entryType"] = AfterlifeArchiveState.EntryTypeLoreFragment,
                        ["title"] = "Запись",
                        ["summary"] = "Описание",
                        ["rarity"] = "Rare",
                        ["sourceLife"] = 1,
                        ["sourceKind"] = AfterlifeArchiveState.SourceKindCodex,
                        ["acquiredAtUtc"] = "2026-03-26T00:00:00Z",
                        ["reservation"] = new JsonObject
                        {
                            ["reservationKind"] = AfterlifeArchiveState.ReservationKindConsultation,
                            ["requestId"] = "req_001",
                            ["guardianId"] = "guardian_alpha",
                            ["guardianName"] = "Азалия",
                            ["createdAtTurn"] = 10,
                            ["createdAtUtc"] = "2026-03-26T00:00:00Z"
                        }
                    }
                }
            }
        };

        AfterlifeArchiveState.NormalizeShape(root);
        AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "req_001",
                ["archiveId"] = "archive_001",
                ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeConsultation,
                ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted,
                ["guardianId"] = "guardian_alpha",
                [AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount] = 1,
                [AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus] = 0
            }
        }, currentTurn: 11);

        Assert.Empty(AfterlifeArchiveState.EnsureStoredArray(root));
        var receipt = AfterlifeArchiveState.EnsureActionReceiptsArray(root).OfType<JsonObject>().Single();
        Assert.Equal("req_001", receipt["requestId"]?.GetValue<string>());
        Assert.Equal("accepted", receipt["status"]?.GetValue<string>());
        Assert.Equal(1, receipt[AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount]?.GetValue<int>());
    }

    [Fact]
    public void ApplyActionResolutions_RejectedResolution_ReleasesReservationAndKeepsEntry()
    {
        var root = new JsonObject
        {
            ["afterlifeArchive"] = new JsonObject
            {
                ["stored"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["archiveId"] = "archive_002",
                        ["entryType"] = AfterlifeArchiveState.EntryTypeSecretRecord,
                        ["title"] = "Тайна",
                        ["summary"] = "Описание",
                        ["rarity"] = "Epic",
                        ["sourceLife"] = 2,
                        ["sourceKind"] = AfterlifeArchiveState.SourceKindCodex,
                        ["acquiredAtUtc"] = "2026-03-26T00:00:00Z",
                        ["reservation"] = new JsonObject
                        {
                            ["reservationKind"] = AfterlifeArchiveState.ReservationKindProjectFuel,
                            ["requestId"] = "req_002",
                            ["guardianId"] = "guardian_beta",
                            ["guardianName"] = "Бранн",
                            ["targetProjectId"] = "proj_1",
                            ["createdAtTurn"] = 12,
                            ["createdAtUtc"] = "2026-03-26T00:00:00Z"
                        }
                    }
                }
            }
        };

        AfterlifeArchiveState.NormalizeShape(root);
        AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "req_002",
                ["archiveId"] = "archive_002",
                ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeProjectFuel,
                ["status"] = AfterlifeArchiveActionState.ResolutionStatusRejected,
                ["guardianId"] = "guardian_beta",
                ["targetProjectId"] = "proj_1"
            }
        }, currentTurn: 13);

        var storedEntry = AfterlifeArchiveState.EnsureStoredArray(root).OfType<JsonObject>().Single();
        Assert.False(AfterlifeArchiveState.IsReserved(storedEntry));
        var receipt = AfterlifeArchiveState.EnsureActionReceiptsArray(root).OfType<JsonObject>().Single();
        Assert.Equal("rejected", receipt["status"]?.GetValue<string>());
    }
}
