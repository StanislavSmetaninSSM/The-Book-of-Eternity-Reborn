using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmMemorySearchToolTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public GmMemorySearchToolTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-gm-memory-search-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task SearchGmMemory_FindsExpandedContinuitySources()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_project_journal.json", """
        {
          "entries": [
            {
              "entryId": "project_entry_1",
              "guardianId": "guardian_azalia",
              "turn": 12,
              "timestamp": "2026-03-27T10:00:00Z",
              "title": "LanternKey Project",
              "summary": "LanternKey guardian project summary."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/abode_power_journal.json", """
        {
          "entries": [
            {
              "entryId": "power_entry_1",
              "guardianId": "guardian_azalia",
              "turn": 11,
              "appliedAt": "2026-03-27T09:55:00Z",
              "title": "LanternKey Offering",
              "summary": "LanternKey abode power rose."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/factions/faction_chronicles.json", """
        {
          "entries": [
            {
              "entryId": "faction_entry_1",
              "turn": 10,
              "timestamp": "2026-03-27T09:50:00Z",
              "title": "LanternKey Faction Chronicle",
              "entry": "LanternKey reshaped the compact."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/world/world_events.json", """
        {
          "events": [
            {
              "eventId": "world_event_1",
              "turn": 9,
              "timestamp": "2026-03-27T09:45:00Z",
              "title": "LanternKey World Event",
              "summary": "LanternKey spread through the market."
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/character_chronicle.json", """
        {
          "entries": [
            {
              "title": "LanternKey Chronicle",
              "content": "LanternKey became part of the soul's memory.",
              "turnNumber": 8,
              "timestamp": "2026-03-27T09:40:00Z"
            }
          ]
        }
        """);

        var output = await RunSearchToolAsync("-Query", "LanternKey", "-Limit", "20");

        Assert.Contains("[guardian_project_journal]", output, StringComparison.Ordinal);
        Assert.Contains("[abode_power_journal]", output, StringComparison.Ordinal);
        Assert.Contains("[faction_chronicles]", output, StringComparison.Ordinal);
        Assert.Contains("[world_events]", output, StringComparison.Ordinal);
        Assert.Contains("[character_chronicle]", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchGmMemory_ResolvesGuardianNameForGuardianScopedSources()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_project_journal.json", """
        {
          "entries": [
            {
              "entryId": "project_entry_1",
              "guardianId": "guardian_azalia",
              "turn": 12,
              "timestamp": "2026-03-27T10:00:00Z",
              "title": "Проект памяти",
              "summary": "Поиск должен уметь находить это по имени Хранителя."
            }
          ]
        }
        """);

        var output = await RunSearchToolAsync("-EntityType", "guardian", "-EntityName", "Азалия", "-Query", "Проект", "-Limit", "5");

        Assert.Contains("guardian:guardian_azalia [Азалия]", output, StringComparison.Ordinal);
        Assert.Contains("[guardian_project_journal]", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchGmMemory_FindsStoryHitsByEntityRefs()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":14,"timestamp":"2026-03-27T10:10:00Z","realm":"Chaos Sea","player":"[ABODE_RESIDENT_TALK]","narrative":"Фигура в тумане отвечает уклончиво, не называя себя.","entityRefs":[{"entityType":"guardian","entityId":"guardian_azalia"}]}
        """);

        var output = await RunSearchToolAsync("-EntityType", "guardian", "-EntityId", "guardian_azalia", "-Query", "уклончиво", "-Limit", "10");

        Assert.Contains("[stories/chaos_sea.jsonl]", output, StringComparison.Ordinal);
        Assert.Contains("guardian:guardian_azalia [Азалия]", output, StringComparison.Ordinal);
        Assert.Contains("Фигура в тумане отвечает уклончиво", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchGmMemory_SourceFilterAndJsonMode_ReturnStructuredStoryOnlyResults()
    {
        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":18,"timestamp":"2026-03-27T10:18:00Z","realm":"Chaos Sea","player":"[ABODE_RESIDENT_TALK]","narrative":"LanternKey echoed through the mist.","entityRefs":[{"entityType":"resident","entityId":"resident_alpha_1","displayName":"Лиора"}]}
        """);

        await _fs.WriteFileAtomicAsync("game_state/meta/character_chronicle.json", """
        {
          "entries": [
            {
              "title": "LanternKey Chronicle",
              "content": "This should be filtered out by -Source stories.",
              "turnNumber": 7,
              "timestamp": "2026-03-27T09:40:00Z"
            }
          ]
        }
        """);

        var output = await RunSearchToolAsync("-Source", "stories", "-Json", "-Query", "LanternKey", "-Limit", "10");
        using var doc = JsonDocument.Parse(output);
        var entries = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : throw new Xunit.Sdk.XunitException("Expected JSON array from -Json mode.");

        Assert.Single(entries);
        Assert.Equal("stories/chaos_sea.jsonl", entries[0].GetProperty("Source").GetString());
        Assert.Equal("resident", entries[0].GetProperty("ActorType").GetString());
    }

    [Fact]
    public async Task SearchGmMemory_EntityTypeFaction_FindsFactionChronicleHits()
    {
        await _fs.WriteFileAtomicAsync("game_state/factions/faction_core.json", """
        {
          "entries": [
            {
              "factionId": "faction_lantern",
              "factionName": "Орден Фонаря"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("game_state/factions/faction_chronicles.json", """
        {
          "entries": [
            {
              "entryId": "faction_entry_1",
              "factionId": "faction_lantern",
              "factionName": "Орден Фонаря",
              "turn": 12,
              "timestamp": "2026-03-27T10:00:00Z",
              "title": "Фонарь над башней",
              "entry": "LanternKey illuminated the oath chamber."
            }
          ]
        }
        """);

        var output = await RunSearchToolAsync("-EntityType", "faction", "-Query", "LanternKey", "-Limit", "10");

        Assert.Contains("[faction_chronicles]", output, StringComparison.Ordinal);
        Assert.Contains("faction:faction_lantern [Орден Фонаря]", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchGmMemory_CyrillicQueryAndEntityName_WorkInJsonMode()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardian_abode_residents.json", """
        {
          "entries": [
            {
              "residentId": "resident_liora",
              "displayName": "Лиора"
            }
          ]
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":21,"timestamp":"2026-03-27T10:21:00Z","realm":"Chaos Sea","player":"[ABODE_RESIDENT_HISTORY_REQUEST]","narrative":"Лиора шепчет о клятве, данной у северных ворот.","entityRefs":[{"entityType":"resident","entityId":"resident_liora","displayName":"Лиора"}]}
        """);

        var output = await RunSearchToolAsync("-EntityType", "resident", "-EntityName", "лиора", "-Query", "клятве", "-Json", "-Limit", "10");

        using var doc = JsonDocument.Parse(output);
        var entries = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToArray()
            : throw new Xunit.Sdk.XunitException("Expected JSON array from -Json mode.");

        var entry = Assert.Single(entries);
        Assert.Equal("resident", entry.GetProperty("ActorType").GetString());
        Assert.Equal("resident_liora", entry.GetProperty("ActorId").GetString());
        Assert.Equal("Лиора", entry.GetProperty("ActorName").GetString());
        Assert.Contains("клятве", entry.GetProperty("Excerpt").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchGmMemory_CyrillicLiteralQuery_DoesNotDependOnWildcardLikeMatching()
    {
        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":22,"timestamp":"2026-03-27T10:22:00Z","realm":"Chaos Sea","player":"[ABODE_RESIDENT_TALK]","narrative":"Слово *клятва* всё ещё звучит в тишине."}
        """);

        var output = await RunSearchToolAsync("-Query", "клятва", "-Limit", "10");

        Assert.Contains("[stories/chaos_sea.jsonl]", output, StringComparison.Ordinal);
        Assert.Contains("Слово *клятва* всё ещё звучит", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchGmMemory_OldStoryEntryWithoutEntityRefs_FallsBackToGenericTextHit()
    {
        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":23,"timestamp":"2026-03-27T10:23:00Z","realm":"Chaos Sea","player":"[SCENE]","narrative":"Старый лог без entity refs всё ещё должен находиться по тексту."}
        """);

        var output = await RunSearchToolAsync("-Query", "старый лог", "-Json", "-Limit", "10");

        using var doc = JsonDocument.Parse(output);
        var entries = doc.RootElement.EnumerateArray().ToArray();
        var entry = Assert.Single(entries);
        Assert.Equal("stories/chaos_sea.jsonl", entry.GetProperty("Source").GetString());
        Assert.Equal("any", entry.GetProperty("ActorType").GetString());
        Assert.Contains("entity refs", entry.GetProperty("Excerpt").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchGmMemory_OldStoryEntryWithoutEntityRefs_DoesNotFakeActorScopedHit()
    {
        await _fs.WriteFileAtomicAsync("stories/chaos_sea.jsonl", """
        {"turn":24,"timestamp":"2026-03-27T10:24:00Z","realm":"Chaos Sea","player":"[SCENE]","narrative":"Азалия упомянута в старой записи без actor refs."}
        """);

        var output = await RunSearchToolAsync("-EntityType", "guardian", "-EntityId", "guardian_azalia", "-Query", "Азалия", "-Json", "-Limit", "10");

        using var doc = JsonDocument.Parse(output);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Empty(doc.RootElement.EnumerateArray());
    }

    private async Task<string> RunSearchToolAsync(params string[] arguments)
    {
        var scriptPath = Path.Combine(TestRepoPaths.RepoRoot, "Tools", "Search-GmMemory.ps1");
        Assert.True(File.Exists(scriptPath), $"Search tool not found: {scriptPath}");

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-SessionRoot");
        psi.ArgumentList.Add(_fs.GameSessionPath);
        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        using var process = Process.Start(psi);
        Assert.NotNull(process);
        var stdOut = await process!.StandardOutput.ReadToEndAsync();
        var stdErr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(process.ExitCode == 0, $"Search-GmMemory exited with code {process.ExitCode}:{Environment.NewLine}{stdErr}");
        return stdOut;
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
