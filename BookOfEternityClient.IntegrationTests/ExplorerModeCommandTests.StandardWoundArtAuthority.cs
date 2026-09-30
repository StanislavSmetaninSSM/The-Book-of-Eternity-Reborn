using BookOfEternityClient.Services;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    public static IEnumerable<object[]> StandardWoundArtInvalidConsoleAuthorityCases()
    {
        foreach (var artId in AfterlifeSpiritualConflictState.RequiredWoundArtIds)
        {
            foreach (var mutation in new[] { "missing_leaf", "null_leaf", "string_leaf", "object_leaf" })
                yield return [artId, mutation, $"afterlifeCombatProfile.artTiers.{artId}"];

            var sibling = artId == AfterlifeSpiritualConflictState.SpiritualResilienceArtId
                ? AfterlifeSpiritualConflictState.SpiritualHealingArtId
                : AfterlifeSpiritualConflictState.SpiritualResilienceArtId;
            yield return [artId, "invalid_sibling", $"afterlifeCombatProfile.artTiers.{sibling}"];
        }

        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "null_profile", "afterlifeCombatProfile"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "scalar_profile", "afterlifeCombatProfile"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "null_map", "afterlifeCombatProfile.artTiers"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "array_map", "afterlifeCombatProfile.artTiers"];
        yield return ["pressure", "invalid_healing_for_old_art", "afterlifeCombatProfile.artTiers.spiritual_healing"];
    }

    [Theory]
    [MemberData(nameof(StandardWoundArtInvalidConsoleAuthorityCases))]
    public async Task StandardWoundArt_InvalidPlayerAuthority_ConsoleRejectsBeforeQuoteAndPreservesState(
        string selectedArtId,
        string mutation,
        string expectedContext)
    {
        var soul = new JsonObject
        {
            ["soulName"] = "Тестовая Душа",
            ["currentRealm"] = "Chaos Sea",
            ["currentIncarnation"] = 1,
            ["inkFeathers"] = new JsonObject { ["current"] = 2000, ["total"] = 2000 },
            ["enlightenment"] = new JsonObject { ["currentTier"] = "Illuminated", ["experience"] = 100, ["level"] = 5 },
            ["soulProgression"] = new JsonObject { ["totalExperience"] = 100, ["tier"] = 5, ["progressPercent"] = 100 },
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile()
        };
        soul["afterlifeCombatProfile"]!["artTiers"]!["pressure"] = 0;
        MutateConsoleWoundArtAuthority(soul, selectedArtId, mutation);
        await WriteRawJsonAsync("game_state/meta/soul_state.json", soul.ToJsonString());
        await _stateManager.RefreshGameStateAsync();
        var before = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        var label = selectedArtId switch
        {
            AfterlifeSpiritualConflictState.SpiritualResilienceArtId => "Духовная стойкость",
            AfterlifeSpiritualConflictState.SpiritualHealingArtId => "Духовное исцеление",
            _ => "Давление"
        };
        _console.QueueAnySelection("⬆ Прокачать духовное искусство", "← Назад");
        _console.QueueSelection("Выберите духовное искусство", $"{label} — уровень 0->1, 500 🪶");
        _console.QueueSelection("Выберите валюту", "Чернильные Перья — 500 🪶");
        _console.QueueAnyConfirmResponse(true);

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        Assert.Equal(before, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        var rendered = ExtractRenderedText();
        Assert.Contains(expectedContext, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("следующий уровень", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StandardWoundArt_ConsoleHelpFramesHealingAsUnavailableFutureRules()
    {
        await WriteJsonAsync("game_state/meta/soul_state.json", new
        {
            soulName = "Тестовая Душа",
            currentRealm = "Chaos Sea",
            currentIncarnation = 1,
            inkFeathers = new { current = 500, total = 500 },
            enlightenment = new { currentTier = "Illuminated", experience = 100, level = 5 },
            soulProgression = new { totalExperience = 100, tier = 5, progressPercent = 100 },
            afterlifeCombatProfile = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile()
        });
        await _stateManager.RefreshGameStateAsync();
        _console.QueueAnySelection("← Назад");

        var ex = await Record.ExceptionAsync(() => _explorer.TryProcessCommand("/spiritual_arts"));

        Assert.Null(ex);
        var rendered = ExtractRenderedText();
        Assert.Contains(
            "Диагностика и лечение духовных ран пока недоступны",
            rendered,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Будущее правило искусства", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "Сильнее против: диагностику и лечение духовных ран",
            rendered,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "диагностика доступна на нулевой ступени",
            rendered,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void MutateConsoleWoundArtAuthority(JsonObject soul, string selectedArtId, string mutation)
    {
        if (mutation == "null_profile")
        {
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = null;
            return;
        }

        if (mutation == "scalar_profile")
        {
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = 1;
            return;
        }

        var profile = Assert.IsType<JsonObject>(soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]);
        if (mutation == "null_map")
        {
            profile["artTiers"] = null;
            return;
        }

        if (mutation == "array_map")
        {
            profile["artTiers"] = new JsonArray();
            return;
        }

        var arts = Assert.IsType<JsonObject>(profile["artTiers"]);
        var targetId = mutation switch
        {
            "invalid_sibling" when selectedArtId == AfterlifeSpiritualConflictState.SpiritualResilienceArtId =>
                AfterlifeSpiritualConflictState.SpiritualHealingArtId,
            "invalid_sibling" => AfterlifeSpiritualConflictState.SpiritualResilienceArtId,
            "invalid_healing_for_old_art" => AfterlifeSpiritualConflictState.SpiritualHealingArtId,
            _ => selectedArtId
        };
        switch (mutation)
        {
            case "missing_leaf":
                arts.Remove(targetId);
                break;
            case "null_leaf":
                arts[targetId] = null;
                break;
            case "string_leaf":
                arts[targetId] = "1";
                break;
            case "object_leaf":
            case "invalid_sibling":
            case "invalid_healing_for_old_art":
                arts[targetId] = new JsonObject { ["tier"] = 1, ["experience"] = 0 };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
    }
}
