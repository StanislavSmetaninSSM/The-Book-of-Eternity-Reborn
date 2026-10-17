using System.IO.Compression;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SaveLoadServiceTests
{
    [Fact]
    public async Task RelayOperationalRecords_ActualSaveLoadExcludesOperationalAuthorityAndReplacesGeneration()
    {
        using var cleanup = new CleanupOwnedFixture(_rootPath, Console.WriteLine);
        string[] operational = ["game_state/control/gm_prompt_delivery.json", "game_state/control/gm_relay_apply_receipt.json"];
        const string neighbor = "game_state/core/relay_ordinary_neighbor.json";
        await _fs.WriteFileAtomicAsync(neighbor, "{\"revision\":1}");
        foreach (var path in operational) await _fs.WriteFileAtomicAsync(path, "{\"schemaVersion\":1,\"fixture\":\"OPAQUE_OPERATIONAL_ARCHIVE_EXCLUSION_NOT_REAL_DECISION\"}");
        var originalGeneration = File.ReadAllBytes(_fs.SessionGenerationPath);
        Assert.True(await _service.SaveGameAsync("relay_operational", "owned relay operational lifetime"));
        var save = Directory.GetFiles(_fs.ResolvePath("saves/manual_saves"), "*.zip").Single();
        string[] captured;
        using (var archive = ZipFile.OpenRead(save)) captured = archive.Entries.Select(entry => entry.FullName).ToArray();
        Assert.Contains(neighbor, captured);
        await _fs.WriteFileAtomicAsync(neighbor, "{\"revision\":2}");
        foreach (var path in operational) await _fs.WriteFileAtomicAsync(path, "{\"schemaVersion\":1,\"currentOperationalMarker\":true}");
        Assert.True(await _service.LoadGameAsync(save));
        var restored = JsonDocument.Parse((await _fs.ReadFileAsync(neighbor))!);
        using (restored) Assert.Equal(1, restored.RootElement.GetProperty("revision").GetInt32());
        Assert.NotEqual(Convert.ToHexString(originalGeneration), Convert.ToHexString(File.ReadAllBytes(_fs.SessionGenerationPath)));
        var issues = operational.Where(path => captured.Contains(path, StringComparer.OrdinalIgnoreCase) || _fs.FileExists(path)).ToArray();
        Assert.Empty(issues);
    }
}
