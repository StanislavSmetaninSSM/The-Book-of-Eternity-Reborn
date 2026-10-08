using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("write", "carrier")]
    [InlineData("write", "later-mutation")]
    [InlineData("delete", "carrier")]
    [InlineData("delete", "later-mutation")]
    public async Task ActualPreTurnRestorationUncertaintyRetainsDecisionAndStopsLaterMutations(string operation, string observation)
    {
        const string present = "game_state/world/weather.json";
        const string second = "lore/current_world/world_setting.json";
        const string absent = "game_state/wounds/wound_history.json";
        await _fs.WriteFileAtomicBytesAsync(present, [0x41]);
        await _fs.WriteFileAtomicBytesAsync(second, [0x51]);
        var engine = CreateGameEngine();
        var snapshot = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "restoration_outcome");
        var backups = Assert.IsAssignableFrom<Dictionary<string, string>>(snapshot.GetType().GetProperty("BackupFiles")!.GetValue(snapshot));
        var evidence = backups.ToDictionary(pair => pair.Value, pair => File.ReadAllBytes(_fs.ResolvePath(pair.Value)));
        await _fs.WriteFileAtomicBytesAsync(present, [0x42]);
        await _fs.WriteFileAtomicBytesAsync(second, [0x52]);
        await _fs.WriteFileAtomicBytesAsync(absent, [0x61]);
        var target = operation == "write" ? present : absent;
        var reached = 0;
        var laterMutations = 0;
        byte[] unknown = [0xFF, 0x41];
        byte[]? journal = null;
        _consoleMutationObserver = _ => { if (reached > 0) laterMutations++; };
        _consolePublicationObserver = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || reached > 0) return;
            var journalPath = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var document = JsonDocument.Parse(File.ReadAllBytes(journalPath));
            var member = document.RootElement.GetProperty("Members")[index];
            if (member.GetProperty("Path").GetString() != _fs.ResolvePath(target)) return;
            Assert.Equal(operation == "write", member.GetProperty("After").GetProperty("Exists").GetBoolean());
            if (operation == "write") Assert.Equal(new byte[] { 0x41 }, File.ReadAllBytes(_fs.ResolvePath(target)));
            else Assert.False(File.Exists(_fs.ResolvePath(target)));
            reached++;
            File.WriteAllBytes(_fs.ResolvePath(target), unknown);
            journal = File.ReadAllBytes(journalPath);
            throw new PreTurnRestorationCutFailure();
        };
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "RestorePreTurnBackup", snapshot));
        _consoleMutationObserver = null;
        _consolePublicationObserver = null;
        Assert.Equal(1, reached);
        Assert.Equal(unknown, File.ReadAllBytes(_fs.ResolvePath(target)));
        Assert.Equal(journal, File.ReadAllBytes(Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        foreach (var (path, bytes) in evidence) Assert.Equal(bytes, File.ReadAllBytes(_fs.ResolvePath(path)));
        if (observation == "carrier") Assert.IsType<CoordinatedStatePublicationUncertainException>(failure);
        else Assert.Equal(0, laterMutations);
    }

    private sealed class PreTurnRestorationCutFailure : Exception { }
}
