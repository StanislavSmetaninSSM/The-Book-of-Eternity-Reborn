using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ActorMemoryServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ActorMemoryService _service;

    public ActorMemoryServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-actor-memory-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _service = new ActorMemoryService(_fs, NullLogger<ActorMemoryService>.Instance);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_ChaosSea_IncludesGuardianAndResidentMemory()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abode": { "abodeId": "abode_threads" }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "abode": { "abodeId": "abode_threads" }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianThoughtJournalState.StatePath, """
        {
          "entries": [
            {
              "entryId": "gthought_1",
              "guardianId": "guardian_azalia",
              "turn": 12,
              "timestamp": "2026-03-27T10:00:00Z",
              "title": "Внимательный интерес",
              "summary": "Азалия присматривается к выбору души.",
              "intent": "Проверить, насколько душа готова к правде."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianSocialJournalState.StatePath, """
        {
          "entries": [
            {
              "entryId": "gsocial_1",
              "guardianId": "guardian_azalia",
              "turn": 11,
              "timestamp": "2026-03-27T09:50:00Z",
              "eventType": "lesson",
              "title": "Урок о цене обещаний",
              "summary": "Хранитель предупредил, что память требует платы."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_project_journal.json", """
        {
          "entries": [
            {
              "entryId": "gproject_1",
              "guardianId": "guardian_azalia",
              "turn": 12,
              "timestamp": "2026-03-27T10:07:00Z",
              "title": "Проект Лампы Памяти",
              "summary": "Азалия укрепляет нити памяти в своей Обители."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
        {
          "entries": [
            {
              "entryId": "gpower_1",
              "guardianId": "guardian_azalia",
              "turn": 12,
              "appliedAt": "2026-03-27T10:08:00Z",
              "title": "Прилив силы",
              "summary": "В садах Обители стало легче удерживать нити воспоминаний."
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
              "historyRevealed": true,
              "availableInteractions": ["talk", "history"],
              "isPresent": true,
              "mortalWorldImprint": {
                "originWorldSummary": "Была посланницей между осаждёнными городами.",
                "futureCompanionPrompt": "Messenger with ember scarf"
              }
            }
          ],
          "thoughtJournal": [
            {
              "entryId": "rthought_1",
              "residentId": "resident_liora",
              "turn": 12,
              "timestamp": "2026-03-27T10:05:00Z",
              "title": "Ждёт честности",
              "summary": "Лиора надеется, что душа не солжёт ей.",
              "intent": "Проверить искренность."
            }
          ],
          "interactionLog": [
            {
              "entryId": "revent_1",
              "residentId": "resident_liora",
              "turn": 11,
              "timestamp": "2026-03-27T09:55:00Z",
              "eventType": "conversation",
              "title": "Разговор у края сада",
              "summary": "Лиора рассказала, почему боится опоздать снова."
            }
          ],
          "historyLog": [
            {
              "entryId": "rhistory_1",
              "residentId": "resident_liora",
              "title": "Последняя весть",
              "summary": "Когда-то она несла письмо через горящий мост.",
              "revealedAtTurn": 11,
              "revealedAtUtc": "2026-03-27T09:56:00Z"
            }
          ]
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Chaos Sea", 12);

        Assert.NotNull(reminder);
        Assert.Contains("Active Guardian: Азалия", reminder, StringComparison.Ordinal);
        Assert.Contains("Guardian thoughts", reminder, StringComparison.Ordinal);
        Assert.Contains("Guardian project continuity", reminder, StringComparison.Ordinal);
        Assert.Contains("Abode power continuity", reminder, StringComparison.Ordinal);
        Assert.Contains("Лиора", reminder, StringComparison.Ordinal);
        Assert.Contains("Разговор у края сада", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_ShiningAbode_UsesAfterlifeDigest()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "abode": { "abodeId": "abode_threads" }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия",
            "abode": { "abodeId": "abode_threads" }
          }
        }
        """);

        await _fs.WriteFileAtomicAsync(GuardianThoughtJournalState.StatePath, """
        {
          "entries": [
            {
              "entryId": "gthought_1",
              "guardianId": "guardian_azalia",
              "turn": 12,
              "timestamp": "2026-03-27T10:00:00Z",
              "title": "Внимательный интерес",
              "summary": "Азалия слушает тихие перемены в душе."
            }
          ]
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Shining Abode", 12);

        Assert.NotNull(reminder);
        Assert.Contains("Active Guardian: Азалия", reminder, StringComparison.Ordinal);
        Assert.Contains("Guardian thoughts", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_MortalWorld_IncludesNpcThoughtsAndEvents()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_market",
          "name": "Рынок"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "NPCId": "npc_merchant_01",
              "name": "Старый Торговец",
              "currentLocationId": "loc_market"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "NPCJournals": [
            {
              "NPCId": "npc_merchant_01",
              "NPCName": "Старый Торговец",
              "lastJournalNote": "Подозревает, что герой знает цену редким товарам."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync(NpcInteractionJournalState.StatePath, """
        {
          "entries": [
            {
              "entryId": "npc_event_1",
              "npcId": "npc_merchant_01",
              "turn": 9,
              "timestamp": "2026-03-27T08:00:00Z",
              "eventType": "trade",
              "title": "Редкая сделка",
              "summary": "Торговец уступил цену после честного разговора."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "events": [
            {
              "eventId": "world_event_1",
              "turn": 10,
              "timestamp": "2026-03-27T08:10:00Z",
              "title": "Рынок под слухами",
              "summary": "По площади ходят разговоры о пропавшей караванной книге."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/factions/faction_chronicles.json", """
        {
          "entries": [
            {
              "entryId": "faction_1",
              "turn": 9,
              "timestamp": "2026-03-27T08:05:00Z",
              "title": "Гильдия торговцев",
              "entry": "Гильдия требует объяснить новые рыночные сборы."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/character_chronicle.json", """
        {
          "entries": [
            {
              "title": "Долг торговцу",
              "content": "Герой всё ещё помнит о старом обещании рынку.",
              "turnNumber": 8,
              "timestamp": "2026-03-27T08:00:00Z"
            }
          ]
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 10);

        Assert.NotNull(reminder);
        Assert.Contains("Current-scene NPC memory", reminder, StringComparison.Ordinal);
        Assert.Contains("Старый Торговец", reminder, StringComparison.Ordinal);
        Assert.Contains("Подозревает", reminder, StringComparison.Ordinal);
        Assert.Contains("Редкая сделка", reminder, StringComparison.Ordinal);
        Assert.Contains("Wider continuity", reminder, StringComparison.Ordinal);
        Assert.Contains("Долг торговцу", reminder, StringComparison.Ordinal);
        Assert.Contains("Рынок под слухами", reminder, StringComparison.Ordinal);
        Assert.Contains("Гильдия торговцев", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_MortalWorld_PrefersRecentContinuityNearCurrentTurn()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_market",
          "name": "Рынок"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "NPCId": "npc_merchant_01",
              "name": "Старый Торговец",
              "currentLocationId": "loc_market"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "NPCJournals": [
            {
              "NPCId": "npc_merchant_01",
              "NPCName": "Старый Торговец",
              "lastJournalNote": "Ждёт оплаты старого долга."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/character_chronicle.json", """
        {
          "entries": [
            {
              "title": "Будущий шум",
              "content": "Эта запись слишком далека от текущего хода и не должна попадать в digest.",
              "turnNumber": 50,
              "timestamp": "2026-03-27T20:00:00Z"
            },
            {
              "title": "Недавний долг",
              "content": "Это актуальная запись рядом с текущим ходом.",
              "turnNumber": 9,
              "timestamp": "2026-03-27T09:00:00Z"
            }
          ]
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 10);

        Assert.NotNull(reminder);
        Assert.Contains("Недавний долг", reminder, StringComparison.Ordinal);
        Assert.DoesNotContain("Будущий шум", reminder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildSystemReminderFragmentAsync_MortalWorld_FallsBackToLatestContinuityWhenNoRecentEntriesExist()
    {
        await _fs.WriteFileAtomicAsync("game_state/world/current_location.json", """
        {
          "locationId": "loc_market",
          "name": "Рынок"
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "NPCId": "npc_merchant_01",
              "name": "Старый Торговец",
              "currentLocationId": "loc_market"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_journals.json", """
        {
          "NPCJournals": [
            {
              "NPCId": "npc_merchant_01",
              "NPCName": "Старый Торговец",
              "lastJournalNote": "Наблюдает за редким покупателем."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/character_chronicle.json", """
        {
          "entries": [
            {
              "title": "Старый след",
              "content": "Это последняя доступная запись, но она далеко от текущего хода.",
              "turnNumber": 40,
              "timestamp": "2026-03-27T20:00:00Z"
            }
          ]
        }
        """);

        var reminder = await _service.BuildSystemReminderFragmentAsync("Mortal World", 5);

        Assert.NotNull(reminder);
        Assert.Contains("Wider continuity", reminder, StringComparison.Ordinal);
        Assert.Contains("Старый след", reminder, StringComparison.Ordinal);
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
            // ignored
        }
    }
}
