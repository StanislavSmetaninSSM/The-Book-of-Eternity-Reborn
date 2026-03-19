using BookOfEternityClient.Models;

namespace BookOfEternityClient.Core;

internal static class GameResponseRefreshMerger
{
    public static GameResponse Merge(GameResponse? current, GameResponse? refreshed)
    {
        refreshed ??= new GameResponse();

        if (current == null)
            return refreshed;

        if (string.IsNullOrWhiteSpace(refreshed.Response))
            refreshed.Response = current.Response;
        if (string.IsNullOrWhiteSpace(refreshed.GmThoughtsMarkdown))
            refreshed.GmThoughtsMarkdown = current.GmThoughtsMarkdown;
        if (string.IsNullOrWhiteSpace(refreshed.ImagePrompt))
            refreshed.ImagePrompt = current.ImagePrompt;
        if (string.IsNullOrWhiteSpace(refreshed.CombatLogMarkdown))
            refreshed.CombatLogMarkdown = current.CombatLogMarkdown;
        if ((refreshed.DialogueOptions == null || refreshed.DialogueOptions.Length == 0) &&
            current.DialogueOptions is { Length: > 0 })
        {
            refreshed.DialogueOptions = current.DialogueOptions;
        }

        return refreshed;
    }
}
