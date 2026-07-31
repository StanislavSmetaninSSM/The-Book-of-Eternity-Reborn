using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class AfterlifeArchiveActionStateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public AfterlifeArchiveActionStateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-archive-action-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task ValidateGameStateAsync_CurrentArchiveEntryMissingSourceLife_ReturnsBlockingIssue()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_missing_source_life",
                "entryType": "lore_fragment",
                "title": "Запись без жизни-источника",
                "summary": "Эта запись не должна проходить accepted-turn strict canonical path.",
                "rarity": "Rare",
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-06-18T00:00:00Z"
              }
            ],
            "actionReceipts": []
          }
        }
        """);

        var issues = await new ValidationService(_fs, NullLogger<ValidationService>.Instance).ValidateGameStateAsync();

        Assert.Contains(issues, static issue =>
            issue.Severity == IssueSeverity.Error &&
            string.Equals(issue.FilePath, "game_state/meta/soul_state.json.afterlifeArchive.stored[0].sourceLife", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EnsureHealthyAsync_OutsideAfterlife_UnresolvedRequestRetainsReservationAndPendingFile()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "crossIncarnationData": {
            "legacyThreadId": "thread_alpha"
          },
          "metaStateUpdates": {
            "memoryLegacyGrant": {
              "legacyId": "legacy_keep",
              "legacyType": "startingCharacteristicBonus",
              "sourceLifeHint": "life_001",
              "characteristic": "strength",
              "bonus": 2
            }
          },
          "afterlifeArchiveUpdates": [
            {
              "command": "remove",
              "archiveId": "archive_consult"
            },
            {
              "command": "remove",
              "archiveId": "archive_keep"
            }
          ],
          "archiveActionResolutions": [
            {
              "requestId": "consult_req_001",
              "archiveId": "archive_consult",
              "requestedMode": "consultation",
              "status": "cancelled"
            },
            {
              "requestId": "req_keep",
              "archiveId": "archive_keep",
              "requestedMode": "project_fuel",
              "status": "rejected"
            }
          ],
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_consult",
                "entryType": "lore_fragment",
                "title": "Консультационная запись",
                "summary": "Нужно снять reservation.",
                "rarity": "Rare",
                "sourceLife": 1,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z",
                "reservation": {
                  "reservationKind": "consultation",
                  "requestId": "consult_req_001",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "createdAtTurn": 7,
                  "createdAtUtc": "2026-03-26T00:00:00Z"
                }
              },
              {
                "archiveId": "archive_keep",
                "entryType": "secret_record",
                "title": "Сохранённая запись",
                "summary": "Не должна быть затронута.",
                "rarity": "Epic",
                "sourceLife": 1,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z"
              }
            ]
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(AfterlifeArchiveActionState.ConsultationRequestPath, """
        {
          "requestId": "consult_req_001",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "archiveId": "archive_consult",
          "archiveTitle": "Консультационная запись",
          "archiveEntryType": "lore_fragment",
          "archiveRarity": "Rare",
          "archiveSourceKind": "codex",
          "targetIncarnation": 2,
          "createdAtTurn": 7,
          "createdAtUtc": "2026-03-26T00:00:00Z",
          "requestedMode": "consultation"
        }
        """);

        await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, "Mortal World");

        Assert.True(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var storedEntries = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored").EnumerateArray().ToList();
        Assert.Equal(2, storedEntries.Count);
        var releasedEntry = storedEntries.Single(entry => entry.GetProperty("archiveId").GetString() == "archive_consult");
        Assert.True(releasedEntry.TryGetProperty("reservation", out var reservation));
        Assert.Equal("consult_req_001", reservation.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task EnsureHealthyAsync_OutsideAfterlife_MalformedConsultationRequest_RetainsRequestAndReservation()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_consult",
                "entryType": "lore_fragment",
                "title": "Консультационная запись",
                "summary": "Reservation должна сохраниться.",
                "rarity": "Rare",
                "sourceLife": 1,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z",
                "reservation": {
                  "reservationKind": "consultation",
                  "requestId": "consult_req_broken",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "createdAtTurn": 7,
                  "createdAtUtc": "2026-03-26T00:00:00Z"
                }
              }
            ]
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(
            AfterlifeArchiveActionState.ConsultationRequestPath,
            """
            {
              "requestId": "consult_req_broken",
              "guardianId":
            """
        );

        await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, "Mortal World");

        Assert.True(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var storedEntries = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored").EnumerateArray().ToList();
        var reservedEntry = storedEntries.Single(entry => entry.GetProperty("archiveId").GetString() == "archive_consult");
        Assert.True(reservedEntry.TryGetProperty("reservation", out _));
    }

    [Fact]
    public async Task EnsureHealthyAsync_OutsideAfterlife_ProjectFuelWithUnreconciledReservation_RetainsRequestFile()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_fuel",
                "entryType": "secret_record",
                "title": "Топливо проекта",
                "summary": "Reservation не совпадает с requestId.",
                "rarity": "Epic",
                "sourceLife": 1,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z",
                "reservation": {
                  "reservationKind": "project_fuel",
                  "requestId": "fuel_req_other",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "targetProjectId": "project_alpha",
                  "targetProjectName": "Грань Снов",
                  "createdAtTurn": 7,
                  "createdAtUtc": "2026-03-26T00:00:00Z"
                }
              }
            ]
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(AfterlifeArchiveActionState.ProjectFuelRequestPath, """
        {
          "requestId": "fuel_req_expected",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "archiveId": "archive_fuel",
          "archiveTitle": "Топливо проекта",
          "archiveEntryType": "secret_record",
          "archiveRarity": "Epic",
          "archiveSourceKind": "codex",
          "targetProjectId": "project_alpha",
          "targetProjectName": "Грань Снов",
          "createdAtTurn": 7,
          "createdAtUtc": "2026-03-26T00:00:00Z",
          "requestedMode": "project_fuel"
        }
        """);

        await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, "Mortal World");

        Assert.True(_fs.FileExists(AfterlifeArchiveActionState.ProjectFuelRequestPath));

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var storedEntries = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored").EnumerateArray().ToList();
        var reservedEntry = storedEntries.Single(entry => entry.GetProperty("archiveId").GetString() == "archive_fuel");
        Assert.True(reservedEntry.TryGetProperty("reservation", out var reservation));
        Assert.Equal("fuel_req_other", reservation.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task EnsureHealthyAsync_OutsideAfterlife_MatchingReceiptWithoutReservation_ClearsStaleRequestFile()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_consult",
                "entryType": "lore_fragment",
                "title": "Консультационная запись",
                "summary": "Reservation уже снята.",
                "rarity": "Rare",
                "sourceLife": 1,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z"
              }
            ],
            "actionReceipts": [
              {
                "requestId": "consult_req_completed",
                "archiveId": "archive_consult",
                "requestedMode": "consultation",
                "status": "cancelled",
                "resolvedAtTurn": 8,
                "resolvedAtUtc": "2026-03-26T00:05:00Z"
              }
            ]
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(AfterlifeArchiveActionState.ConsultationRequestPath, """
        {
          "requestId": "consult_req_completed",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "archiveId": "archive_consult",
          "archiveTitle": "Консультационная запись",
          "archiveEntryType": "lore_fragment",
          "archiveRarity": "Rare",
          "archiveSourceKind": "codex",
          "targetIncarnation": 2,
          "createdAtTurn": 7,
          "createdAtUtc": "2026-03-26T00:00:00Z",
          "requestedMode": "consultation"
        }
        """);

        await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, "Mortal World");

        Assert.False(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var storedEntry = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored").EnumerateArray().Single();
        Assert.Equal("archive_consult", storedEntry.GetProperty("archiveId").GetString());
        Assert.False(storedEntry.TryGetProperty("reservation", out _));
    }

    [Fact]
    public async Task EnsureHealthyAsync_OutsideAfterlife_MatchingReceiptWithArchiveMismatch_RetainsRequestAndReservation()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 2,
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_actual",
                "entryType": "lore_fragment",
                "title": "Дрейфующая reservation",
                "summary": "Не должна быть очищена по чужому archiveId.",
                "rarity": "Rare",
                "sourceLife": 1,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z",
                "reservation": {
                  "reservationKind": "consultation",
                  "requestId": "consult_req_drift",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "createdAtTurn": 7,
                  "createdAtUtc": "2026-03-26T00:00:00Z"
                }
              }
            ],
            "actionReceipts": [
              {
                "requestId": "consult_req_drift",
                "archiveId": "archive_expected",
                "requestedMode": "consultation",
                "status": "cancelled",
                "resolvedAtTurn": 8,
                "resolvedAtUtc": "2026-03-26T00:05:00Z"
              }
            ]
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(AfterlifeArchiveActionState.ConsultationRequestPath, """
        {
          "requestId": "consult_req_drift",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "archiveId": "archive_expected",
          "archiveTitle": "Ожидаемая запись",
          "archiveEntryType": "lore_fragment",
          "archiveRarity": "Rare",
          "archiveSourceKind": "codex",
          "targetIncarnation": 2,
          "createdAtTurn": 7,
          "createdAtUtc": "2026-03-26T00:00:00Z",
          "requestedMode": "consultation"
        }
        """);

        await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, "Mortal World");

        Assert.True(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var storedEntry = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored").EnumerateArray().Single();
        Assert.Equal("archive_actual", storedEntry.GetProperty("archiveId").GetString());
        Assert.True(storedEntry.TryGetProperty("reservation", out var reservation));
        Assert.Equal("consult_req_drift", reservation.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task EnsureHealthyAsync_InAfterlife_InvalidConsultationRequest_IsRetainedUntilReadableRepair()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 2,
          "afterlifeArchive": {
            "stored": [
              {
                "archiveId": "archive_consult",
                "entryType": "lore_fragment",
                "title": "Консультационная запись",
                "summary": "Запрос ещё нужно починить.",
                "rarity": "Rare",
                "sourceLife": 1,
                "sourceKind": "codex",
                "acquiredAtUtc": "2026-03-26T00:00:00Z",
                "reservation": {
                  "reservationKind": "consultation",
                  "requestId": "consult_req_invalid",
                  "guardianId": "guardian_alpha",
                  "guardianName": "Азалия",
                  "createdAtTurn": 7,
                  "createdAtUtc": "2026-03-26T00:00:00Z"
                }
              }
            ]
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(AfterlifeArchiveActionState.ConsultationRequestPath, """
        {
          "requestId": "consult_req_invalid",
          "guardianId": "guardian_alpha",
          "guardianName": "Азалия",
          "archiveId": "archive_consult",
          "archiveTitle": "Консультационная запись",
          "archiveEntryType": "lore_fragment",
          "archiveRarity": "Rare",
          "archiveSourceKind": "codex",
          "targetIncarnation": 0,
          "createdAtTurn": 7,
          "createdAtUtc": "2026-03-26T00:00:00Z",
          "requestedMode": "consultation"
        }
        """);

        await AfterlifeArchiveActionState.EnsureHealthyAsync(_fs, "Chaos Sea");

        Assert.True(_fs.FileExists(AfterlifeArchiveActionState.ConsultationRequestPath));

        using var soulDoc = JsonDocument.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!);
        var storedEntries = soulDoc.RootElement.GetProperty("afterlifeArchive").GetProperty("stored").EnumerateArray().ToList();
        var reservedEntry = storedEntries.Single(entry => entry.GetProperty("archiveId").GetString() == "archive_consult");
        Assert.True(reservedEntry.TryGetProperty("reservation", out _));
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
            // ignore temp cleanup failures
        }
    }
}
