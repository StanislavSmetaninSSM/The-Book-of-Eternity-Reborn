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

    [Fact]
    public void ApplyActionResolutions_AcceptedCorrectionAfterRejectedReceipt_ConsumesStoredEntry()
    {
        var root = new JsonObject
        {
            ["afterlifeArchive"] = new JsonObject
            {
                ["stored"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["archiveId"] = "archive_003",
                        ["entryType"] = AfterlifeArchiveState.EntryTypeLoreFragment,
                        ["title"] = "Осколок",
                        ["summary"] = "Описание",
                        ["rarity"] = "Rare",
                        ["sourceLife"] = 3,
                        ["sourceKind"] = AfterlifeArchiveState.SourceKindCodex,
                        ["acquiredAtUtc"] = "2026-03-26T00:00:00Z",
                        ["reservation"] = new JsonObject
                        {
                            ["reservationKind"] = AfterlifeArchiveState.ReservationKindConsultation,
                            ["requestId"] = "req_003",
                            ["guardianId"] = "guardian_gamma",
                            ["guardianName"] = "Мира",
                            ["createdAtTurn"] = 14,
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
                ["requestId"] = "req_003",
                ["archiveId"] = "archive_003",
                ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeConsultation,
                ["status"] = AfterlifeArchiveActionState.ResolutionStatusRejected,
                ["guardianId"] = "guardian_gamma"
            }
        }, currentTurn: 15);

        Assert.Single(AfterlifeArchiveState.EnsureStoredArray(root));
        Assert.False(AfterlifeArchiveState.IsReserved(AfterlifeArchiveState.EnsureStoredArray(root).OfType<JsonObject>().Single()));

        AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "req_003",
                ["archiveId"] = "archive_003",
                ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeConsultation,
                ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted,
                ["guardianId"] = "guardian_gamma",
                [AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount] = 1,
                [AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus] = 0
            }
        }, currentTurn: 16);

        Assert.Empty(AfterlifeArchiveState.EnsureStoredArray(root));
        var receipt = AfterlifeArchiveState.EnsureActionReceiptsArray(root).OfType<JsonObject>().Single();
        Assert.Equal("req_003", receipt["requestId"]?.GetValue<string>());
        Assert.Equal(AfterlifeArchiveActionState.ResolutionStatusAccepted, receipt["status"]?.GetValue<string>());
        Assert.Equal(1, receipt[AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount]?.GetValue<int>());
    }

    [Fact]
    public void ApplyUpdates_MalformedArchiveUpdateItem_FailClosed()
    {
        var root = new JsonObject();
        AfterlifeArchiveState.NormalizeShape(root);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AfterlifeArchiveState.ApplyUpdates(root, new JsonArray
            {
                new JsonObject
                {
                    ["command"] = "unknown",
                    ["archiveId"] = "archive_001"
                }
            }));

        Assert.Contains("afterlifeArchiveUpdates", exception.Message, StringComparison.Ordinal);
        Assert.Empty(AfterlifeArchiveState.EnsureStoredArray(root));
    }

    [Fact]
    public void ApplyUpdates_AddWithoutCanonicalEntryShape_FailClosed()
    {
        var root = new JsonObject();
        AfterlifeArchiveState.NormalizeShape(root);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AfterlifeArchiveState.ApplyUpdates(root, new JsonArray
            {
                new JsonObject
                {
                    ["command"] = "add",
                    ["entry"] = new JsonObject
                    {
                        ["archiveId"] = "archive_001"
                    }
                }
            }));

        Assert.Contains("afterlifeArchiveUpdates", exception.Message, StringComparison.Ordinal);
        Assert.Empty(AfterlifeArchiveState.EnsureStoredArray(root));
    }

    [Fact]
    public void ApplyActionResolutions_MalformedResolutionItem_FailClosed()
    {
        var root = new JsonObject();
        AfterlifeArchiveState.NormalizeShape(root);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "req_001",
                    ["archiveId"] = "archive_001",
                    ["requestedMode"] = "consultation"
                }
            }, currentTurn: 14));

        Assert.Contains("archiveActionResolutions", exception.Message, StringComparison.Ordinal);
        Assert.Empty(AfterlifeArchiveState.EnsureActionReceiptsArray(root));
    }

    [Fact]
    public void ApplyActionResolutions_AcceptedConsultationWithoutOutcome_FailClosed()
    {
        var root = new JsonObject();
        AfterlifeArchiveState.NormalizeShape(root);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "req_consult",
                    ["archiveId"] = "archive_001",
                    ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeConsultation,
                    ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted
                }
            }, currentTurn: 14));

        Assert.Contains("archiveActionResolutions", exception.Message, StringComparison.Ordinal);
        Assert.Empty(AfterlifeArchiveState.EnsureActionReceiptsArray(root));
    }

    [Fact]
    public void ApplyActionResolutions_AcceptedProjectFuelWithoutResultPayload_FailClosed()
    {
        var root = new JsonObject();
        AfterlifeArchiveState.NormalizeShape(root);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "req_project",
                    ["archiveId"] = "archive_002",
                    ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeProjectFuel,
                    ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted,
                    ["targetProjectId"] = "project_alpha"
                }
            }, currentTurn: 14));

        Assert.Contains("archiveActionResolutions", exception.Message, StringComparison.Ordinal);
        Assert.Empty(AfterlifeArchiveState.EnsureActionReceiptsArray(root));
    }

    [Fact]
    public void ApplyActionResolutions_OrphanConsultationResolution_FailClosed()
    {
        var root = new JsonObject();
        AfterlifeArchiveState.NormalizeShape(root);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "req_orphan",
                    ["archiveId"] = "archive_orphan",
                    ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeConsultation,
                    ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted,
                    [AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount] = 1,
                    [AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount] = 0,
                    [AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks] = 0,
                    [AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus] = 0,
                    [AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus] = 0
                }
            }, currentTurn: 14));

        Assert.Contains("archiveActionResolutions", exception.Message, StringComparison.Ordinal);
        Assert.Empty(AfterlifeArchiveState.EnsureActionReceiptsArray(root));
    }

    [Fact]
    public void ApplyActionResolutions_OrphanProjectFuelResolution_FailClosed()
    {
        var root = new JsonObject();
        AfterlifeArchiveState.NormalizeShape(root);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
            {
                new JsonObject
                {
                    ["requestId"] = "req_orphan",
                    ["archiveId"] = "archive_orphan",
                    ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeProjectFuel,
                    ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted,
                    ["targetProjectId"] = "project_alpha",
                    ["resultMode"] = AfterlifeArchiveActionState.ProjectFuelResultModeProjectWork,
                    ["resultAmount"] = 2
                }
            }, currentTurn: 14));

        Assert.Contains("archiveActionResolutions", exception.Message, StringComparison.Ordinal);
        Assert.Empty(AfterlifeArchiveState.EnsureActionReceiptsArray(root));
    }

    [Fact]
    public void ApplyActionResolutions_SameRequestIdDifferentArchiveIdentity_KeepsDistinctReceipts()
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
                        ["title"] = "Первая запись",
                        ["summary"] = "Описание",
                        ["rarity"] = "Rare",
                        ["sourceLife"] = 1,
                        ["sourceKind"] = AfterlifeArchiveState.SourceKindCodex,
                        ["acquiredAtUtc"] = "2026-03-26T00:00:00Z",
                        ["reservation"] = new JsonObject
                        {
                            ["reservationKind"] = AfterlifeArchiveState.ReservationKindConsultation,
                            ["requestId"] = "req_shared",
                            ["guardianId"] = "guardian_alpha",
                            ["guardianName"] = "Азалия",
                            ["createdAtTurn"] = 10,
                            ["createdAtUtc"] = "2026-03-26T00:00:00Z"
                        }
                    },
                    new JsonObject
                    {
                        ["archiveId"] = "archive_002",
                        ["entryType"] = AfterlifeArchiveState.EntryTypeSecretRecord,
                        ["title"] = "Вторая запись",
                        ["summary"] = "Описание",
                        ["rarity"] = "Epic",
                        ["sourceLife"] = 1,
                        ["sourceKind"] = AfterlifeArchiveState.SourceKindCodex,
                        ["acquiredAtUtc"] = "2026-03-26T00:00:00Z",
                        ["reservation"] = new JsonObject
                        {
                            ["reservationKind"] = AfterlifeArchiveState.ReservationKindProjectFuel,
                            ["requestId"] = "req_shared",
                            ["guardianId"] = "guardian_alpha",
                            ["guardianName"] = "Азалия",
                            ["targetProjectId"] = "project_alpha",
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
                ["requestId"] = "req_shared",
                ["archiveId"] = "archive_001",
                ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeConsultation,
                ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted,
                ["guardianId"] = "guardian_alpha",
                [AfterlifeArchiveActionState.ConsultationOutcomeGuaranteedArchiveQuestCount] = 1,
                [AfterlifeArchiveActionState.ConsultationOutcomeQuestHookCount] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeSpecialQuestLineUnlocks] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeVisibleRivalClueBonus] = 0,
                [AfterlifeArchiveActionState.ConsultationOutcomeArchiveWarningTierBonus] = 0
            },
            new JsonObject
            {
                ["requestId"] = "req_shared",
                ["archiveId"] = "archive_002",
                ["requestedMode"] = AfterlifeArchiveActionState.RequestedModeProjectFuel,
                ["status"] = AfterlifeArchiveActionState.ResolutionStatusAccepted,
                ["guardianId"] = "guardian_alpha",
                ["targetProjectId"] = "project_alpha",
                ["resultMode"] = AfterlifeArchiveActionState.ProjectFuelResultModeProjectWork,
                ["resultAmount"] = 2
            }
        }, currentTurn: 14);

        var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(root).OfType<JsonObject>().ToList();
        Assert.Equal(2, receipts.Count);
        Assert.Contains(receipts, receipt =>
            AfterlifeArchiveState.ActionReceiptMatchesRequest(
                receipt,
                "req_shared",
                "archive_001",
                AfterlifeArchiveActionState.RequestedModeConsultation));
        Assert.Contains(receipts, receipt =>
            AfterlifeArchiveState.ActionReceiptMatchesRequest(
                receipt,
                "req_shared",
                "archive_002",
                AfterlifeArchiveActionState.RequestedModeProjectFuel));
    }

    [Fact]
    public void ApplyActionResolutions_WhitespaceVariantFullIdentity_UpsertsExistingReceiptInPlace()
    {
        var root = new JsonObject
        {
            ["afterlifeArchive"] = new JsonObject
            {
                ["stored"] = new JsonArray(),
                ["actionReceipts"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["requestId"] = " req_shared ",
                        ["archiveId"] = " archive_001 ",
                        ["requestedMode"] = $" {AfterlifeArchiveActionState.RequestedModeConsultation} ",
                        ["status"] = AfterlifeArchiveActionState.ResolutionStatusRejected,
                        ["resolvedAtTurn"] = 12,
                        ["resolvedAtUtc"] = "2026-03-26T00:00:00Z"
                    }
                }
            }
        };

        AfterlifeArchiveState.NormalizeShape(root);
        AfterlifeArchiveState.ApplyActionResolutions(root, new JsonArray
        {
            new JsonObject
            {
                ["requestId"] = "req_shared",
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
        }, currentTurn: 14);

        var receipts = AfterlifeArchiveState.EnsureActionReceiptsArray(root).OfType<JsonObject>().ToList();
        Assert.Single(receipts);
        Assert.Equal(AfterlifeArchiveActionState.ResolutionStatusAccepted, receipts[0]["status"]?.GetValue<string>());
        Assert.True(AfterlifeArchiveState.ActionReceiptMatchesRequest(
            receipts[0],
            "req_shared",
            "archive_001",
            AfterlifeArchiveActionState.RequestedModeConsultation));
    }
}
