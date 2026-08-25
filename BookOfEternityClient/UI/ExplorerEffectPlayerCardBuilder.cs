using BookOfEternityClient.CommandProtocol;

namespace BookOfEternityClient.UI;

internal static class ExplorerEffectPlayerCardBuilder
{
    internal static UiEntityDossierBlock BuildOverview(
        EffectPlayerEntry effect,
        string entityType = "effect-summary") =>
        new()
        {
            EntityType = entityType,
            Title = effect.Name,
            Subtitle = DescribeState(effect.State),
            Summary = FirstNonEmpty(
                effect.Summary ?? string.Empty,
                "Подробности доступны в карточке эффекта."),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeState(effect.State),
                    Tone = UiTone.Accent,
                    Icon = "effect"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "facts",
                    Title = "Кратко",
                    Icon = "effect",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks =
                    [
                        new UiKeyValueGridBlock
                        {
                            Items = effect.Facts
                                .Select(static fact => new UiKeyValueItem
                                {
                                    Key = fact.Label,
                                    Value = fact.Value
                                })
                                .ToList()
                        }
                    ]
                }
            ]
        };

    private static string DescribeState(string state) => state switch
    {
        "active" => "Действует",
        "suspended" => "Приостановлен",
        _ => "Недоступен"
    };

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
}
