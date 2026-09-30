using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GameResponseRefreshMergerTests
{
    [Fact]
    public void Merge_PreservesCurrentNarrativeAndThoughts_WhenRefreshedResponseIsEmpty()
    {
        var current = new GameResponse
        {
            Response = "Текущий ход GM",
            GmThoughtsMarkdown = "## Мысли\nСкрытый reasoning",
            ImagePrompt = "scene prompt",
            CombatLogMarkdown = "combat",
            DialogueOptions = new[] { new DialogueOption { Text = "Спросить", Category = "neutral" } }
        };

        var refreshed = new GameResponse();

        var merged = GameResponseRefreshMerger.Merge(current, refreshed);

        Assert.Equal("Текущий ход GM", merged.Response);
        Assert.Equal("## Мысли\nСкрытый reasoning", merged.GmThoughtsMarkdown);
        Assert.Equal("scene prompt", merged.ImagePrompt);
        Assert.Equal("combat", merged.CombatLogMarkdown);
        Assert.Single(merged.DialogueOptions!);
        Assert.Equal("Спросить", merged.DialogueOptions![0].Text);
    }

    [Fact]
    public void Merge_UsesFreshFields_WhenRefreshedResponseProvidesThem()
    {
        var current = new GameResponse
        {
            Response = "Старый текст",
            GmThoughtsMarkdown = "Старые мысли",
            DialogueOptions = new[] { new DialogueOption { Text = "Старый вариант", Category = "neutral" } }
        };

        var refreshed = new GameResponse
        {
            Response = "Новый текст",
            GmThoughtsMarkdown = "Новые мысли",
            DialogueOptions = new[] { new DialogueOption { Text = "Новый вариант", Category = "important" } }
        };

        var merged = GameResponseRefreshMerger.Merge(current, refreshed);

        Assert.Equal("Новый текст", merged.Response);
        Assert.Equal("Новые мысли", merged.GmThoughtsMarkdown);
        Assert.Single(merged.DialogueOptions!);
        Assert.Equal("Новый вариант", merged.DialogueOptions![0].Text);
    }

    [Fact]
    public void Merge_PreservesCurrentDialogueOptions_WhenRefreshedResponseDoesNotContainAny()
    {
        var current = new GameResponse
        {
            DialogueOptions = new[]
            {
                new DialogueOption { Text = "1", Category = "neutral" },
                new DialogueOption { Text = "2", Category = "neutral" }
            }
        };

        var refreshed = new GameResponse
        {
            Response = "Только narrative"
        };

        var merged = GameResponseRefreshMerger.Merge(current, refreshed);

        Assert.Equal("Только narrative", merged.Response);
        Assert.NotNull(merged.DialogueOptions);
        Assert.Equal(2, merged.DialogueOptions!.Length);
    }

    [Fact]
    public void Merge_PreservesTakeOnceWoundNotificationsAcrossReadOnlyRefresh()
    {
        var source = new[]
        {
            "Получена рана: Ожог ладони (I). Подробнее: /раны"
        };
        var current = new GameResponse { WoundNotifications = source };

        var merged = GameResponseRefreshMerger.Merge(
            current,
            new GameResponse { Response = "Обновлённая сцена." });

        Assert.Equal(source, merged.WoundNotifications);
        Assert.NotSame(source, merged.WoundNotifications);
    }

    [Fact]
    public void GameResponse_WoundNotificationsRemainClientOnlyDuringSerialization()
    {
        var response = new GameResponse
        {
            Response = "Сцена.",
            WoundNotifications = new[]
            {
                "Получена рана: Ожог ладони (I). Подробнее: /раны"
            }
        };

        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"response\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("WoundNotifications", json, StringComparison.Ordinal);
        Assert.DoesNotContain("woundNotifications", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Ожог ладони", json, StringComparison.Ordinal);
    }
}
