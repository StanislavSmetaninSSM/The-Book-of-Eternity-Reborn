using BookOfEternityClient.CommandProtocol;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerHelpCommandResultBuilderTests
{
    [Fact]
    public void Build_ChaosSeaHelp_ReturnsLogicalDtoRowsWithoutSpectreMarkup()
    {
        var result = ExplorerHelpCommandResultBuilder.Build(new ExplorerHelpCommandContext
        {
            Command = "/помощь",
            Title = "Помощь",
            IsChaosSea = true,
            IsShiningAbode = false,
            IsPendingShiningAbodeBootstrap = false,
            CanReenterShiningAbode = true
        });

        Assert.Equal("/помощь", result.Command);
        Assert.Equal(CommandExecutionState.Completed, result.State);
        var dossier = Assert.IsType<UiEntityDossierBlock>(Assert.Single(result.Blocks));
        Assert.Equal("Помощь", dossier.Title);
        Assert.Equal("help", dossier.EntityType);
        Assert.Contains(dossier.Sections, static section =>
            section.Title.Equals("МОРЕ ХАОСА (загробная жизнь)", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(dossier.Sections.SelectMany(static section => section.Cards), static card =>
            card.Facts.Any(static fact =>
                fact.Label == "Команда" &&
                fact.Value == "/chaos_sea") &&
            card.Facts.Any(static fact =>
                fact.Label == "Русская команда" &&
                fact.Value == "/море_хаоса") &&
            card.Summary == "Обзор Моря Хаоса: активный Хранитель, навигация, ожидающие контракты и доступные действия");
        Assert.Contains(dossier.Sections.SelectMany(static section => section.Cards), static card =>
            card.Facts.Any(static fact =>
                fact.Label == "Команда" &&
                fact.Value == "/reenter_shining_abode") &&
            card.Facts.Any(static fact =>
                fact.Label == "Русская команда" &&
                fact.Value == "/вернуться_в_обитель"));
        Assert.Contains(dossier.Sections.SelectMany(static section => section.Cards), static card =>
            card.Facts.Any(static fact =>
                fact.Label == "Команда" &&
                fact.Value == "/trade") &&
            card.Facts.Any(static fact =>
                fact.Label == "Русская команда" &&
                fact.Value == "/торговля") &&
            card.Summary.Contains("текущ", StringComparison.OrdinalIgnoreCase) &&
            card.Summary.Contains("реальност", StringComparison.OrdinalIgnoreCase));

        var text = CollectDossierText(dossier);
        Assert.DoesNotContain("[", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[/]", text, StringComparison.Ordinal);
    }

    private static string CollectDossierText(UiEntityDossierBlock dossier)
    {
        var parts = new List<string>
        {
            dossier.Title,
            dossier.Subtitle,
            dossier.Summary
        };
        parts.AddRange(dossier.Badges.Select(static badge => badge.Label));
        parts.AddRange(dossier.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(dossier.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(dossier.List);

        foreach (var section in dossier.Sections)
        {
            parts.Add(section.Title);
            parts.Add(section.Summary);
            parts.Add(section.CollectionLabel);
            parts.AddRange(section.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
            parts.AddRange(section.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
            parts.AddRange(section.List);
            foreach (var card in section.Cards)
                CollectCardText(card, parts);
        }

        return string.Join('\n', parts);
    }

    private static void CollectCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(static badge => badge.Label));
        parts.AddRange(card.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(card.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(card.List);

        foreach (var child in card.Nested)
            CollectCardText(child, parts);
        foreach (var child in card.Cards)
            CollectCardText(child, parts);
    }
}
