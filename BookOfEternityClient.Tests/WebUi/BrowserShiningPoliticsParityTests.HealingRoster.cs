using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests.WebUi;

public sealed partial class BrowserShiningPoliticsParityTests
{
    [Fact]
    public async Task HealingRosterBrowserAndConsoleRendererShowLiteralRolesWithoutActionsOrMutation()
    {
        await SeedHealingRosterAsync(2);
        var before = SnapshotHealingRosterSources();
        var result = await ReadHealingRosterAsync();
        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectResultAndPromptText(result);
        Assert.Contains("Жители фракции", text);
        Assert.Contains("Лекарь [red]Огонь[/]", text);
        Assert.Contains("целитель; Исцеление I", text);
        Assert.Contains("целитель; Исцеление V", text);
        Assert.Contains("роль не означает открытого приёма", text);
        Assert.Empty(result.Actions); Assert.Empty(result.Prompts); Assert.Null(result.InteractiveSession);
        AssertNoRawShiningDiagnosticText(text);
        foreach (var id in new[] { "resident_healer_0", "resident_healer_1", "healing_support", "materialization" })
            Assert.DoesNotContain(id, text);

        var console = new TestExplorerConsole();
        ExplorerCommandResultConsoleRenderer.Render(console, result);
        using var output = new StringWriter();
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(output)
        });
        renderer.Profile.Width = 180;
        foreach (var renderable in console.Rendered) renderer.Write(renderable);
        Assert.Contains("Лекарь [red]Огонь[/]", output.ToString());
        Assert.Contains("Исцеление V", output.ToString());
        AssertHealingRosterSourcesUnchanged(before);
    }

    [Theory]
    [InlineData("hidden_faction")]
    [InlineData("hidden_profile")]
    [InlineData("invalid_profile")]
    [InlineData("foreign_affiliation")]
    [InlineData("duplicate_profile")]
    public async Task HealingRosterNeverRevealsUnconfirmedHealerDetails(string change)
    {
        await SeedHealingRosterAsync(1);
        if (change == "hidden_faction") await SetFactionVisibilityAsync("faction_lanterns", false);
        else
        {
            var path = change == "foreign_affiliation" ? GuardianAbodeResidentState.StatePath : AfterlifeEntityProfileState.StatePath;
            var root = JsonNode.Parse((await _fs.ReadFileAsync(path))!)!.AsObject();
            if (change == "foreign_affiliation") root["entries"]![0]!["shiningFactionId"] = "faction_mirrors";
            else
            {
                var profile = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]?.GetValue<string>() == "resident_healer_0");
                if (change == "hidden_profile") profile["isPlayerVisible"] = false;
                else if (change == "invalid_profile") profile["materialization"]!["state"] = "pending";
                else root["profiles"]!.AsArray().Add(profile.DeepClone());
            }
            await _fs.WriteFileAtomicAsync(path, root.ToJsonString());
        }
        var before = SnapshotHealingRosterSources();
        var result = await ReadHealingRosterAsync();
        var text = CollectResultAndPromptText(result);
        Assert.DoesNotContain("Лекарь [red]Огонь[/]", text);
        Assert.DoesNotContain("Исцеление I", text);
        Assert.Empty(result.Actions); Assert.Empty(result.Prompts);
        AssertNoRawShiningDiagnosticText(text);
        AssertHealingRosterSourcesUnchanged(before);
    }

    [Fact]
    public async Task HealingRosterSourceReadsDoNotScaleWithMembersAndNextActionReadsFreshTier()
    {
        Dictionary<string, int>? baseline = null;
        foreach (var count in new[] { 1, 20 })
        {
            var reads = new Dictionary<string, int>(StringComparer.Ordinal);
            var measuring = false;
            using var context = new BrowserShiningPoliticsParityTests(new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (measuring) reads[path] = reads.GetValueOrDefault(path) + 1;
                    return Task.CompletedTask;
                }
            });
            await context.SeedHealingRosterAsync(count);
            measuring = true;
            var result = await context.ReadHealingRosterAsync();
            measuring = false;
            Assert.Equal(count, CollectResultAndPromptText(result).Split("целитель; Исцеление ").Length - 1);
            Assert.True(reads.GetValueOrDefault(AfterlifeEntityProfileState.StatePath) > 0);
            _healingRosterOutput!.WriteLine("members={0}; actual opens={1}; sources={2}", count,
                reads.Values.Sum(), string.Join(", ", reads.OrderBy(p => p.Key).Select(p => p.Key + "=" + p.Value)));
            if (baseline is null) baseline = new(reads, StringComparer.Ordinal);
            else Assert.Equal(baseline.OrderBy(p => p.Key), reads.OrderBy(p => p.Key));

            var profiles = JsonNode.Parse((await context._fs.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
            var healer = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]?.GetValue<string>() == "resident_healer_0");
            healer["standardArts"]!["spiritual_healing"] = 3;
            await context._fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
            var fresh = CollectResultAndPromptText(await context.ReadHealingRosterAsync());
            Assert.Contains("целитель; Исцеление III", fresh);
            Assert.DoesNotContain("целитель; Исцеление I\n", fresh);
            Assert.Contains("целитель; Исцеление I", CollectResultAndPromptText(result));
        }
    }

    [Fact]
    public async Task HealingRosterActualConsoleDetailCommandIsReadOnlyAndShowsTheSameRole()
    {
        await SeedHealingRosterAsync(2);
        var before = SnapshotHealingRosterSources();
        var console = new TestExplorerConsole();
        var explorer = new ExplorerMode(_stateManager, _fs, new LocalizationManager(), console: console);
        Assert.Equal(string.Empty, await explorer.TryProcessCommand("/shining_politics faction faction_lanterns"));
        using var output = new StringWriter();
        var renderer = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors, Out = new AnsiConsoleOutput(output)
        });
        renderer.Profile.Width = 180;
        foreach (var renderable in console.Rendered) renderer.Write(renderable);
        var text = output.ToString();
        Assert.Contains("Лекарь [red]Огонь[/]", text);
        Assert.Contains("целитель; Исцеление V", text);
        AssertNoRawShiningDiagnosticText(text);
        AssertHealingRosterSourcesUnchanged(before);
    }

    private Task<ExplorerCommandResult> ReadHealingRosterAsync() => _commandService.ExecuteAsync(
        new ExplorerWebCommandRequest("/shining_politics faction faction_lanterns"));

    private async Task SeedHealingRosterAsync(int count)
    {
        await SeedShiningPoliticsStateAsync();
        var entries = new JsonArray(); var profiles = new JsonArray();
        for (var i = 0; i < count; i++)
        {
            var id = "resident_healer_" + i;
            var resident = CreateResident(id, i == 0 ? "Лекарь [red]Огонь[/]" : "Лекарь Зари " + i,
                "faction_lanterns", "Дом Фонарей", 74, 18);
            resident["residentRole"] = "healing_support"; resident["isPresent"] = false;
            entries.Add(resident);
            var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("resident", id, "Shining Abode", 88);
            profile["standardArts"]!["spiritual_healing"] = i == 0 ? 1 : 5;
            profiles.Add(profile);
        }
        await _fs.WriteFileAtomicAsync(GuardianAbodeResidentState.StatePath,
            new JsonObject { ["schemaVersion"] = 1, ["entries"] = entries }.ToJsonString());
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath,
            new JsonObject { ["schemaVersion"] = 1, ["profiles"] = profiles }.ToJsonString());
        // Existing refresh owns player-profile synchronization. Complete initial
        // preparation before the read-only action; never reuse pre-refresh DATA.
        await _stateManager.RefreshGameStateAsync();
    }

    private Dictionary<string, byte[]?> SnapshotHealingRosterSources()
    {
        // Snapshot this tiny private fixture's whole canonical tree, including
        // world time, History and any newly created pending/repair surface.
        // Also track real absent control roots explicitly; no guessed paths.
        var root = _fs.ResolvePath("game_state");
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => "game_state/" + Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Concat(new[] { ProgressionScheduleService.SchedulePath, PendingTurnStateService.PendingDiceStatePath,
                AcceptedMechanicsPlan.WoundCommandPath })
            .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal)
            .ToDictionary(path => path, path => File.Exists(_fs.ResolvePath(path)) ? File.ReadAllBytes(_fs.ResolvePath(path)) : null);
    }

    private void AssertHealingRosterSourcesUnchanged(Dictionary<string, byte[]?> before)
    {
        var after = SnapshotHealingRosterSources();
        Assert.Equal(before.Keys, after.Keys);
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
    }
}
