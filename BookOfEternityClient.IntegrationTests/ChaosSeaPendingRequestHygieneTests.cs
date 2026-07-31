using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class ChaosSeaPendingRequestHygieneTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ChaosSeaPendingRequestHygieneTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-chaos-pending-hygiene-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task GuardianAbodeOffering_EnsureHealthyAsync_AfterlifePreservesMalformedPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeOfferingState.PendingRequestPath, "{");

        await GuardianAbodeOfferingState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianTradeRequest_EnsureHealthyAsync_AfterlifePreservesMalformedPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{");

        await GuardianTradeRequestState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianTradeRequest_EnsureHealthyAsync_UnresolvedRealmPreservesPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{");

        await GuardianTradeRequestState.EnsureHealthyAsync(_fs, "");

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task PlayerGuardianFoundation_EnsureHealthyAsync_ChaosSeaPreservesMalformedPendingFile()
    {
        await _fs.WriteFileAtomicAsync(PlayerGuardianFoundationState.PendingRequestPath, "{");

        await PlayerGuardianFoundationState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(PlayerGuardianFoundationState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianAbodeOffering_EnsureHealthyAsync_UnresolvedRealmPreservesPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeOfferingState.PendingRequestPath, "{");

        await GuardianAbodeOfferingState.EnsureHealthyAsync(_fs, "");

        Assert.True(_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianAbodeOffering_EnsureHealthyAsync_MatchingOfferingJournalEntryClearsPendingFile()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeOfferingState.PendingRequestPath, """
        {
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "offeringType": "ink_feathers",
          "inkFeathersOffered": 50,
          "returnCycleId": "return_4",
          "createdAtUtc": "2026-04-27T00:00:00Z"
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianPowerEventState.JournalPath, """
        {
          "entries": [
            {
              "entryId": "journal_offering_001",
              "eventId": "gpe_offering_001",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "turn": 42,
              "delta": 1,
              "reasonType": "offering",
              "sourceSurface": "guardianAbodeOffering",
              "sourceId": "pending_abode_offering.json",
              "title": "Подношение Обители",
              "summary": "Игрок принес подношение.",
              "visibility": "public",
              "appliedAt": "2026-04-27T00:01:00Z",
              "audit": {
                "offeringType": "ink_feathers",
                "returnCycleId": "return_4",
                "inkFeathersOffered": 50,
                "baseDelta": 1,
                "finalDelta": 1,
                "capRemainingBefore": 10
              }
            }
          ]
        }
        """);

        await GuardianAbodeOfferingState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.False(_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianAbodeOffering_WriteAsync_ExistingPendingRequest_ThrowsWithoutOverwrite()
    {
        await GuardianAbodeOfferingState.WriteAsync(_fs, new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
        {
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            OfferingType = GuardianAbodeOfferingState.OfferingTypeInkFeathers,
            InkFeathersOffered = 50,
            ReturnCycleId = "return_2"
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => GuardianAbodeOfferingState.WriteAsync(_fs, new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
        {
            GuardianId = "guardian_beta",
            GuardianName = "Варак",
            OfferingType = GuardianAbodeOfferingState.OfferingTypeInkFeathers,
            InkFeathersOffered = 100,
            ReturnCycleId = "return_2"
        }));

        var pending = await GuardianAbodeOfferingState.ReadAsync(_fs);
        Assert.NotNull(pending);
        Assert.Equal("guardian_alpha", pending!.GuardianId);
        Assert.Equal(50, pending.InkFeathersOffered);
    }

    [Fact]
    public async Task GuardianTradeRequest_WriteAsync_MalformedExistingFile_ThrowsAndPreservesCorruption()
    {
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{");

        await Assert.ThrowsAsync<InvalidOperationException>(() => GuardianTradeRequestState.WriteAsync(_fs, new GuardianTradeRequestState.PendingGuardianTradeRequest
        {
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            ReturnCycleId = "return_2",
            DerivedTradeSlotCount = 2
        }));

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
        Assert.Equal("{", await _fs.ReadFileAsync(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task GuardianTradeRequest_WritePreparedJsonAsync_ExistingForeignRequest_ThrowsWithoutOverwrite()
    {
        await GuardianTradeRequestState.WriteAsync(_fs, new GuardianTradeRequestState.PendingGuardianTradeRequest
        {
            RequestId = "guardian_trade_existing",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            ReturnCycleId = "return_2",
            DerivedTradeSlotCount = 2
        });

        var incomingJson = JsonSerializer.Serialize(new GuardianTradeRequestState.PendingGuardianTradeRequest
        {
            RequestId = "guardian_trade_incoming",
            GuardianId = "guardian_beta",
            GuardianName = "Варак",
            AbodeId = "abode_beta",
            ReturnCycleId = "return_3",
            DerivedTradeSlotCount = 4
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => GuardianTradeRequestState.WritePreparedJsonAsync(_fs, incomingJson));

        var pending = await GuardianTradeRequestState.ReadAsync(_fs);
        Assert.NotNull(pending);
        Assert.Equal("guardian_trade_existing", pending!.RequestId);
        Assert.Equal("guardian_alpha", pending.GuardianId);
        Assert.Equal("return_2", pending.ReturnCycleId);
    }

    [Fact]
    public async Task GuardianTradeRequest_WritePreparedJsonAsync_MalformedExistingFile_ThrowsAndPreservesCorruption()
    {
        await _fs.WriteFileAtomicAsync(GuardianTradeRequestState.PendingRequestPath, "{");

        var incomingJson = JsonSerializer.Serialize(new GuardianTradeRequestState.PendingGuardianTradeRequest
        {
            RequestId = "guardian_trade_incoming",
            GuardianId = "guardian_alpha",
            GuardianName = "Азалия",
            AbodeId = "abode_alpha",
            ReturnCycleId = "return_2",
            DerivedTradeSlotCount = 2
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => GuardianTradeRequestState.WritePreparedJsonAsync(_fs, incomingJson));

        Assert.True(_fs.FileExists(GuardianTradeRequestState.PendingRequestPath));
        Assert.Equal("{", await _fs.ReadFileAsync(GuardianTradeRequestState.PendingRequestPath));
    }

    [Fact]
    public async Task PlayerGuardianFoundation_WriteAsync_MalformedExistingFile_ThrowsAndPreservesCorruption()
    {
        await _fs.WriteFileAtomicAsync(PlayerGuardianFoundationState.PendingRequestPath, "{");

        await Assert.ThrowsAsync<InvalidOperationException>(() => PlayerGuardianFoundationState.WriteAsync(_fs, new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
        {
            FounderSoulName = "Душа",
            PreviousGuardianId = "guardian_alpha",
            PreviousGuardianName = "Азалия",
            SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
            ProposedDisplayName = "Северин",
            MantleSummary = "Страж сумерек",
            MantleCreed = "Храни зов",
            AppearanceMotifs = new List<string> { "нить", "заря" },
            DominantAspect = "path",
            CreatedAtTurn = 7
        }));

        Assert.True(_fs.FileExists(PlayerGuardianFoundationState.PendingRequestPath));
        Assert.Equal("{", await _fs.ReadFileAsync(PlayerGuardianFoundationState.PendingRequestPath));
    }

    [Fact]
    public async Task PlayerGuardianFoundation_WriteAsync_ExistingForeignRequest_ThrowsWithoutOverwrite()
    {
        await PlayerGuardianFoundationState.WriteAsync(_fs, new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
        {
            RequestId = "foundation_existing",
            FounderSoulName = "Душа",
            PreviousGuardianId = "guardian_alpha",
            PreviousGuardianName = "Азалия",
            SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
            ProposedDisplayName = "Северин",
            MantleSummary = "Страж сумерек",
            MantleCreed = "Храни зов",
            AppearanceMotifs = new List<string> { "нить", "заря" },
            DominantAspect = "path",
            CreatedAtTurn = 7
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => PlayerGuardianFoundationState.WriteAsync(_fs, new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
        {
            RequestId = "foundation_incoming",
            FounderSoulName = "Другая Душа",
            PreviousGuardianId = "guardian_beta",
            PreviousGuardianName = "Варак",
            SourceShiningAvailability = ShiningAbodeState.AvailabilitySealedUntilNextAscension,
            ProposedDisplayName = "Лириан",
            MantleSummary = "Страж эха",
            MantleCreed = "Помни путь",
            AppearanceMotifs = new List<string> { "эхо" },
            DominantAspect = "memory",
            CreatedAtTurn = 8
        }));

        var pending = await PlayerGuardianFoundationState.ReadAsync(_fs);
        Assert.NotNull(pending);
        Assert.Equal("foundation_existing", pending!.RequestId);
        Assert.Equal("guardian_alpha", pending.PreviousGuardianId);
    }

    [Fact]
    public async Task ManifestationRequests_MalformedBundle_DoesNotRewriteSurvivingSubset()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, """
        {
          "requests": [
            {
              "requestId": "manifest_1",
              "manifestationSource": "resident_relic",
              "relicId": "relic_alpha",
              "relicName": "Эхо Зари",
              "sourceResidentId": "resident_alpha",
              "sourceImprintId": "imprint_alpha",
              "sourceGuardianId": "guardian_alpha",
              "sourceGuardianName": "Азалия",
              "targetIncarnation": 3,
              "companionNameHint": "Ирия",
              "createdAtUtc": "2026-04-20T00:00:00Z"
            },
            {
              "requestId":
            }
          ]
        }
        """);

        var before = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);
        await GuardianAbodeResidentRequestState.EnsureHealthyAsync(_fs, "Mortal World");
        var after = await _fs.ReadFileAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath);

        Assert.Equal(before, after);
        Assert.True(await GuardianAbodeResidentRequestState.IsManifestationRequestFileMalformedAsync(_fs));
    }

    [Fact]
    public async Task ValidatePendingResidentsRequestContextAsync_MalformedBundle_RaisesExplicitError()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingResidentsRequestPath, """
        {
          "requests": [
            {
              "requestId": "roster_req_1",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "abodeName": "Сад Нитей",
              "createdAtTurn": 7,
              "createdAtUtc": "2026-03-27T00:00:00Z"
            },
            {
              "requestId":
            }
          ]
        }
        """);

        var issues = await InvokeValidationAsync("ValidatePendingGuardianAbodeResidentsRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "abode_resident_roster_malformed_file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingResidentInteractionContextAsync_MalformedBundle_RaisesExplicitError()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingInteractionsRequestPath, """
        {
          "requests": [
            {
              "requestId": "interaction_req_1",
              "guardianId": "guardian_alpha",
              "guardianName": "Азалия",
              "abodeId": "abode_alpha",
              "abodeName": "Сад Нитей",
              "residentId": "resident_alpha",
              "residentName": "Лиора",
              "interactionType": "talk",
              "createdAtTurn": 7,
              "createdAtUtc": "2026-03-27T00:00:00Z"
            },
            {
              "requestId":
            }
          ]
        }
        """);

        var issues = await InvokeValidationAsync("ValidatePendingGuardianAbodeResidentInteractionRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "abode_resident_interactions_malformed_file", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePendingResidentTransferContextAsync_MalformedBundle_RaisesExplicitError()
    {
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingTransfersRequestPath, """
        {
          "requests": [
            {
              "requestId": "transfer_req_1",
              "residentId": "resident_alpha",
              "residentName": "Лиора",
              "sourceGuardianId": "guardian_alpha",
              "sourceGuardianName": "Азалия",
              "sourceAbodeId": "abode_alpha",
              "sourceAbodeName": "Сад Нитей",
              "targetGuardianId": "guardian_beta",
              "targetGuardianName": "Мириэль",
              "targetAbodeId": "abode_beta",
              "targetAbodeName": "Сад Перекрёстков",
              "transferMode": "accepted_transfer",
              "createdAtTurn": 7,
              "createdAtUtc": "2026-03-27T00:00:00Z"
            },
            {
              "requestId":
            }
          ]
        }
        """);

        var issues = await InvokeValidationAsync("ValidatePendingGuardianAbodeResidentTransferRequestContextAsync");

        Assert.Contains(issues, issue => string.Equals(issue.Code, "abode_resident_transfer_malformed_file", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Chaos Sea")]
    [InlineData("Shining Abode")]
    public async Task ValidateGameStateAsync_ValidPendingManifestationRequestInAfterlife_IsPreserved(string currentRealm)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "currentRealm": "{{currentRealm}}",
          "currentIncarnation": 4,
          "inkFeathers": { "current": 10, "total": 10 },
          "soulRelics": { "equipped": [], "stored": [] }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, """
        {
          "requests": [
            {
              "requestId": "manifest_1",
              "manifestationSource": "resident_relic",
              "relicId": "relic_alpha",
              "relicName": "Эхо Зари",
              "sourceResidentId": "resident_alpha",
              "sourceGuardianId": "guardian_alpha",
              "sourceGuardianName": "Азалия",
              "targetIncarnation": 5,
              "companionNameHint": "Ирия",
              "originWorldSummary": "Будущая смертная жизнь.",
              "futureCompanionPrompt": "Ирия должна проявиться как ранняя спутница в следующей смертной жизни.",
              "bondReason": "Связь создана через реликвию резидента.",
              "coreTraits": ["loyal"],
              "archetypeHints": ["guide"],
              "appearanceMotifs": ["dawn"],
              "createdAtUtc": "2026-04-20T00:00:00Z"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.Contains("pending_resident_companion_manifestation_request", StringComparison.OrdinalIgnoreCase) &&
            issue.Severity == IssueSeverity.Error);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MaterializedPendingGuardianKeepsPendingCreation_Fails()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0,
          "inkFeathers": { "current": 0, "total": 0 },
          "soulRelics": { "equipped": [], "stored": [] }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": {
            "guardianId": "guardian_selene",
            "name": "Селена"
          },
          "pendingGuardianCreation": {
            "mode": "freeform",
            "description": "Селена",
            "soulName": "Сумрачная Искра"
          },
          "guardians": [
            {
              "guardianId": "guardian_selene",
              "name": "Селена"
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "stale_pending_guardian_creation_after_materialization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnPendingGuardianCreationRemovedWithoutGuardian_Fails()
    {
        const string sessionId = "fresh-bootstrap-session";
        const string requestId = "fresh-bootstrap-turn";
        const int turnNumber = 1;

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}}
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0,
          "soulName": "Сумрачная Искра",
          "inkFeathers": { "current": 0, "total": 0 },
          "soulRelics": { "equipped": [], "stored": [] }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": null,
          "guardians": [],
          "chaosSeaNavigation": {
            "currentAbodeId": null,
            "discoveredAbodes": []
          }
        }
        """);

        await WritePendingTurnSnapshotFileAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0,
          "soulName": "Сумрачная Искра",
          "inkFeathers": { "current": 0, "total": 0 },
          "soulRelics": { "equipped": [], "stored": [] }
        }
        """);
        await WritePendingTurnSnapshotFileAsync("game_state/meta/guardians.json", """
        {
          "activeGuardian": null,
          "guardians": [],
          "pendingGuardianCreation": {
            "mode": "freeform",
            "description": "Селена",
            "soulName": "Сумрачная Искра"
          },
          "chaosSeaNavigation": {
            "currentAbodeId": null,
            "discoveredAbodes": []
          }
        }
        """);
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber,
            "Душа пробуждается в Море Хаоса и ожидает первого Хранителя.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "pending_guardian_creation_missing_materialized_guardian", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_PreTurnPendingGuardianCreationLeftUnresolvedAfterStartupTurn_Fails()
    {
        const string sessionId = "fresh-bootstrap-session";
        const string requestId = "fresh-bootstrap-turn";
        const int turnNumber = 1;

        const string soulStateJson = """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0,
          "soulName": "Пепельная Искра",
          "inkFeathers": { "current": 0, "total": 0 },
          "soulRelics": { "equipped": [], "stored": [] }
        }
        """;
        const string guardiansJson = """
        {
          "activeGuardian": null,
          "guardians": [],
          "chaosSeaNavigation": {
            "currentAbodeId": null,
            "discoveredAbodes": []
          },
          "pendingGuardianCreation": {
            "mode": "freeform",
            "description": "Хранительница Эйра из Дома Тлеющих Звёзд.",
            "soulName": "Пепельная Искра"
          }
        }
        """;

        await _fs.WriteFileAtomicAsync("input/turn_request.json", $$"""
        {
          "sessionId": "{{sessionId}}",
          "requestId": "{{requestId}}",
          "turnNumber": {{turnNumber}}
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soulStateJson);
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansJson);
        await WritePendingTurnSnapshotFileAsync("game_state/meta/soul_state.json", soulStateJson);
        await WritePendingTurnSnapshotFileAsync("game_state/meta/guardians.json", guardiansJson);
        await WritePendingTurnSnapshotManifestAsync(sessionId, requestId, turnNumber,
            "Душа выбирает свободно описанную Хранительницу Эйру как первого Хранителя.");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "pending_guardian_creation_unresolved_after_startup_turn", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("Chaos Sea")]
    [InlineData("Shining Abode")]
    public async Task ValidateGameStateAsync_MalformedPendingManifestationRequestInAfterlife_FailsClosed(string currentRealm)
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", $$"""
        {
          "currentRealm": "{{currentRealm}}",
          "currentIncarnation": 4,
          "inkFeathers": { "current": 10, "total": 10 },
          "soulRelics": { "equipped": [], "stored": [] }
        }
        """);
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentRequestState.PendingManifestationRequestPath, "{ malformed");

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "pending_resident_companion_manifestation_afterlife_malformed", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WritePendingTurnSnapshotFileAsync(string logicalPath, string json)
    {
        await _fs.WriteFileAtomicAsync($"game_state/control/pending_turn_snapshot/{logicalPath}", json);
    }

    private async Task WritePendingTurnSnapshotManifestAsync(
        string sessionId,
        string requestId,
        int turnNumber,
        string playerAction)
    {
        var files = new JsonObject();
        var snapshotFileHashes = new JsonObject();

        foreach (var logicalPath in new[]
        {
            "game_state/meta/soul_state.json",
            "game_state/meta/guardians.json"
        })
        {
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{logicalPath}";
            var snapshotJson = await _fs.ReadFileAsync(snapshotPath);
            Assert.False(string.IsNullOrWhiteSpace(snapshotJson), $"Missing snapshot file for {logicalPath}");
            files[logicalPath] = snapshotPath;
            snapshotFileHashes[logicalPath] = PendingTurnSnapshotAuthority.ComputeSha256(snapshotJson);
        }

        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turnNumber,
            ["requestTimestamp"] = DateTimeOffset.Parse("2026-06-28T00:00:00Z").ToString("O"),
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotFileHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = new JsonArray(),
            ["sourceLabel"] = "chaos-sea-pending-request-hygiene-tests",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);

        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", manifest.ToJsonString());
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task<List<ValidationIssue>> InvokeValidationAsync(string methodName)
    {
        var issues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = method!.Invoke(_validator, new object[] { issues }) as Task;
        Assert.NotNull(task);
        await task!;
        return issues;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
