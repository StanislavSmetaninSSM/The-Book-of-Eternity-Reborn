using BookOfEternityClient.CommandProtocol;
using Spectre.Console;

namespace BookOfEternityClient.UI;

public partial class ExplorerMode
{
    private Task ShowHelpDto()
    {
        const string showAllSections = "Показать все разделы";
        const string backToSections = "← К разделам";
        const string closeHelp = "Закрыть";

        var context = BuildHelpContext();
        var fullResult = ExplorerHelpCommandResultBuilder.Build(context);
        var sectionTitles = GetHelpSectionTitles(fullResult);
        if (sectionTitles.Count <= 1)
        {
            ExplorerCommandResultConsoleRenderer.Render(_console, fullResult);
            WaitForKey();
            return Task.CompletedTask;
        }

        while (true)
        {
            var selectedSection = Prompt(new SelectionPrompt<string>()
                .Title("Раздел справки")
                .PageSize(Math.Min(sectionTitles.Count + 2, 10))
                .AddChoices(sectionTitles.Append(showAllSections).Append(closeHelp)));

            if (string.Equals(selectedSection, closeHelp, StringComparison.Ordinal))
                break;

            var result = string.Equals(selectedSection, showAllSections, StringComparison.Ordinal)
                ? fullResult
                : ExplorerHelpCommandResultBuilder.Build(BuildHelpContext(selectedSection));

            ExplorerCommandResultConsoleRenderer.Render(_console, result);

            var next = Prompt(new SelectionPrompt<string>()
                .Title("Справка")
                .AddChoices(backToSections, closeHelp));
            if (string.Equals(next, closeHelp, StringComparison.Ordinal))
                break;
        }

        return Task.CompletedTask;
    }

    private ExplorerHelpCommandContext BuildHelpContext(string? sectionFilter = null) =>
        new()
        {
            Command = "/help",
            Title = _loc.T("help"),
            IsChaosSea = _stateManager.CurrentState.IsInChaosSea,
            IsShiningAbode = _stateManager.CurrentState.IsInShiningAbode,
            IsPendingShiningAbodeBootstrap = _stateManager.CurrentState.IsInShiningAbodePendingBootstrap,
            CanReenterShiningAbode = _stateManager.CurrentState.CanReenterShiningAbode,
            SectionFilter = sectionFilter
        };

    private static IReadOnlyList<string> GetHelpSectionTitles(ExplorerCommandResult result)
    {
        var dossier = result.Blocks.OfType<UiEntityDossierBlock>().FirstOrDefault();
        if (dossier == null)
            return Array.Empty<string>();

        return dossier.Sections
            .Select(static section => section.Title)
            .Where(static title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
