using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

/// <summary>Display-only blocks shared by the console and browser; no command or target binding.</summary>
internal static class WoundPlayerStatusBlocks
{
    internal static List<UiBlock> Build(WoundPlayerProjectionResult projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var blocks = new List<UiBlock>
        {
            new UiEntityDossierBlock
            {
                EntityType = "wound-status", Title = "Раны", Summary = projection.StatusMessage,
                Cards = projection.IsAvailable ? projection.Active.Select(ActiveCard).ToList() : []
            }
        };
        if (projection.IsAvailable && projection.History.Count > 0)
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "wound-history", Title = "История ран",
                Cards = projection.History.Select(wound => new UiEntityCard
                {
                    Title = wound.Name, Subtitle = "Последняя тяжесть: " + wound.Severity, Summary = wound.Summary,
                    Facts = [new UiEntityFact { Label = "Название", Value = wound.Name }]
                }).ToList()
            });
        return blocks;
    }

    private static UiEntityCard ActiveCard(WoundPlayerEntry wound) => new()
    {
        Title = wound.Name, Subtitle = "Тяжесть: " + wound.Severity, Summary = wound.Description,
        Facts = wound.Facts.Select(fact => new UiEntityFact { Label = fact.Label, Value = fact.Value }).ToList(),
        List = (wound.Methods.Count == 0 ? new[] { "Известные способы лечения пока не определены." } : []).ToList(),
        Cards = wound.Methods.Select(method => new UiEntityCard
        {
            Title = method.Name, Subtitle = "Известный способ лечения", Summary = method.Policy,
            List = method.Requirements.ToList()
        }).Concat(wound.Diagnosis.Select(path => new UiEntityCard
        {
            Title = path.Name, Subtitle = "Известное обследование (рекомендация)", Summary = path.Policy,
            List = path.Requirements.ToList()
        })).ToList()
    };
}
