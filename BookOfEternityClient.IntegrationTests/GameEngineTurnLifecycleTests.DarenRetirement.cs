using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task DarenRetirement_ActualConsoleMenuKeepsPracticeWithoutCampaignLaunch()
    {
        using var cleanup = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var input = new QueuedConsoleInputSource([]);
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; });
        await GetPrivateField<StateManager>(engine, "_stateManager").BootstrapLocalStorageAsync();
        var method = typeof(GameEngine).GetMethod("BuildMainMenuOptionsAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(engine, null));
        await task;
        var options = Assert.IsAssignableFrom<IEnumerable>(task.GetType().GetProperty("Result")!.GetValue(task));
        var keys = options.Cast<object>().Select(option => (string)option.GetType().GetProperty("Key")!.GetValue(option)!).ToArray();
        Assert.Contains("qte_practice", keys);
        Assert.Contains("new_game", keys);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { ActualConsoleMenuKeys = keys, RealConstructedEngine = true }));
        Assert.DoesNotContain("daren_showcase", keys);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    public async Task DarenRetirement_RealFreshSoulDoesNotReadNormalizeOrGrantOldProfile(string variant)
    {
        using var cleanup = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var input = new QueuedConsoleInputSource([]);
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; });
        await GetPrivateField<StateManager>(engine, "_stateManager").BootstrapLocalStorageAsync();
        byte[] profile = variant switch
        {
            "valid" => Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"darenShowcase\":{\"bestTierId\":\"perfect_shadow\",\"bestScore\":90,\"inkFeatherBonus\":6}}"),
            "duplicate" => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"darenShowcases\":[{\"bestTierId\":\"perfect_shadow\",\"bestScore\":90},{\"bestTierId\":\"clean_heist\",\"bestScore\":75}]}" )).ToArray(),
            _ => [0xEF, 0xBB, 0xBF, 0xFF, 0, 41]
        };
        DarenRetirementEvidenceFixtures.Put(_rootPath, DarenRetirementEvidenceFixtures.Profile, profile);
        DarenRetirementEvidenceFixtures.Put(_rootPath, "retirement-foreign-history.bin", DarenRetirementEvidenceFixtures.Before);
        var guardian = new SystemGuardianLibraryService(_fs, NullLogger<SystemGuardianLibraryService>.Instance)
            .BuildFreeformPendingGuardianCreationNode("Спокойный Хранитель маяка встречает душу на берегу Моря Хаоса.", "Пробная Душа");
        await InvokePrivateAsync<string>(engine, "InitializeChaosSea", "Пробная Душа", "Мягкий силуэт синего света.", guardian, null)
            .WaitAsync(TimeSpan.FromSeconds(20));
        var soul = JsonNode.Parse(File.ReadAllText(_fs.ResolvePath("game_state/meta/soul_state.json")))!.AsObject();
        var profileAfter = File.ReadAllBytes(Path.Combine(_rootPath, DarenRetirementEvidenceFixtures.Profile));
        var issues = new List<string>();
        if (!profile.AsSpan().SequenceEqual(profileAfter)) issues.Add("old-profile-read-normalized-or-mutated");
        if (soul["inkFeathers"]!["current"]!.GetValue<int>() != 0 || soul["inkFeathers"]!["total"]!.GetValue<int>() != 0)
            issues.Add("fresh-soul-received-retired-campaign-currency");
        if ((soul["clientRewardGrants"] as JsonObject)?.ContainsKey("darenQteShowcase") == true)
            issues.Add("fresh-soul-received-retired-campaign-grant-marker");
        Assert.Equal(DarenRetirementEvidenceFixtures.Before, File.ReadAllBytes(Path.Combine(_rootPath, "retirement-foreign-history.bin")));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { Variant = variant, RealInitializeChaosSeaCompleted = true,
            ProfileBefore = DarenRetirementEvidenceFixtures.Hash(profile), ProfileAfter = DarenRetirementEvidenceFixtures.Hash(profileAfter),
            InkFeathers = soul["inkFeathers"], Grant = soul["clientRewardGrants"]?["darenQteShowcase"], Issues = issues }));
        Assert.Empty(issues);
    }
}
