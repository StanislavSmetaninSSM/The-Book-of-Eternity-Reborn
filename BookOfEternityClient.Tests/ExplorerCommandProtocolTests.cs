using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerCommandProtocolTests
{
    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    [Fact]
    public void ExplorerCommandResult_RoundTripsSupportedUiSurfaces()
    {
        var result = new ExplorerCommandResult
        {
            Command = "/status",
            State = CommandExecutionState.RequiresInput,
            Blocks =
            [
                new UiTextBlock
                {
                    Text = "Душа находится в Море Хаоса.",
                    Tone = UiTone.Default
                },
                new UiPanelBlock
                {
                    Title = "Состояние",
                    Blocks =
                    [
                        new UiTextBlock { Text = "Активных конфликтов нет.", Tone = UiTone.Subtle }
                    ]
                },
                new UiEntityDossierBlock
                {
                    EntityType = "npc",
                    Title = "Мирра Ключница",
                    Subtitle = "Смотрительница архива",
                    Summary = "Знает, кто входил в покои после полуночи.",
                    Badges =
                    [
                        new UiEntityBadge { Label = "Союзник", Tone = UiTone.Success, Icon = "relation" },
                        new UiEntityBadge { Label = "Архив", Tone = UiTone.Accent, Icon = "archive" }
                    ],
                    Media = new UiEntityMedia
                    {
                        Title = "Портрет Мирры",
                        Url = "/api/media/npc-mirra",
                        AltText = "Портрет Мирры Ключницы"
                    },
                    Sections =
                    [
                        new UiEntityDossierSection
                        {
                            Id = "skills",
                            Title = "Навыки",
                            Summary = "Полезны при расследовании письма.",
                            Icon = "skills",
                            Collapsible = true,
                            InitiallyExpanded = true,
                            Blocks =
                            [
                                new UiListBlock { Items = ["Архивная память", "Тихий шаг"] }
                            ]
                        }
                    ]
                },
                new UiTableBlock
                {
                    Title = "Параметры",
                    Columns = ["Параметр", "Значение"],
                    Rows =
                    [
                        new UiTableRow { Cells = ["ОД", "7"] },
                        new UiTableRow { Cells = ["Позиция", "спорная"] }
                    ]
                },
                new UiListBlock
                {
                    Ordered = true,
                    Items = ["Проверить журнал", "Выбрать духовное действие"]
                },
                new UiKeyValueGridBlock
                {
                    Items =
                    [
                        new UiKeyValueItem { Key = "Царство", Value = "Море Хаоса" },
                        new UiKeyValueItem { Key = "Валюта", Value = "Чернильные Перья" }
                    ]
                },
                new UiMessageBlock
                {
                    Severity = UiNotificationSeverity.Warning,
                    Title = "Предупреждение",
                    Message = "Есть незавершенный ход."
                },
                new UiRawJsonBlock
                {
                    Title = "Raw state",
                    Json = JsonNode.Parse("""{"activeConflict":null,"turn":42}""")!
                },
                new UiMapBlock
                {
                    Title = "Карта",
                    Map = new MapViewDto
                    {
                        Realm = "Mortal World",
                        Title = "Тестовая карта",
                        CurrentNodeId = "loc_square",
                        ZLevels = [new MapZLevelDto { Z = 0, Label = "земля" }],
                        Layers = [new MapLayerDto { Id = "world", Label = "Мир", IsDefault = true }],
                        Nodes =
                        [
                            new MapNodeDto
                            {
                                Id = "loc_square",
                                Label = "Площадь",
                                Type = "city",
                                X = 1,
                                Y = 2,
                                Z = 0,
                                Layer = "world",
                                IsCurrent = true
                            }
                        ]
                    }
                }
            ],
            Actions =
            [
                new UiAction
                {
                    Id = "refresh",
                    Label = "Обновить",
                    Command = "/status",
                    Style = UiActionStyle.Secondary
                }
            ],
            Prompts =
            [
                new UiConfirmationPrompt
                {
                    Id = "confirm-incarnate",
                    Prompt = "Начать воплощение?",
                    DefaultValue = false
                },
                new UiSelectionPrompt
                {
                    Id = "realm",
                    Prompt = "Выберите царство",
                    Options =
                    [
                        new UiSelectionOption { Value = "chaos_sea", Label = "Море Хаоса" },
                        new UiSelectionOption { Value = "shining_abode", Label = "Сияющая Обитель" }
                    ],
                    AllowCustom = false
                },
                new UiTextInputPrompt
                {
                    Id = "short",
                    Prompt = "Короткий ответ",
                    DefaultValue = "да"
                },
                new UiLongTextInputPrompt
                {
                    Id = "narrative",
                    Prompt = "Опишите действие",
                    Placeholder = "Введите художественный текст..."
                }
            ],
            Notifications =
            [
                new UiNotification
                {
                    Severity = UiNotificationSeverity.Info,
                    Title = "Готово",
                    Message = "Команда требует выбора."
                }
            ]
        };

        var json = JsonSerializer.Serialize(result, JsonOpts);

        Assert.Contains("\"kind\": \"text\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"entityDossier\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"map\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"selection\"", json, StringComparison.Ordinal);
        Assert.Contains("\"state\": \"RequiresInput\"", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<ExplorerCommandResult>(json, JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal(CommandExecutionState.RequiresInput, restored.State);
        Assert.IsType<UiTextBlock>(restored.Blocks[0]);
        Assert.IsType<UiPanelBlock>(restored.Blocks[1]);
        Assert.IsType<UiEntityDossierBlock>(restored.Blocks[2]);
        Assert.IsType<UiTableBlock>(restored.Blocks[3]);
        Assert.IsType<UiListBlock>(restored.Blocks[4]);
        Assert.IsType<UiKeyValueGridBlock>(restored.Blocks[5]);
        Assert.IsType<UiMessageBlock>(restored.Blocks[6]);
        Assert.IsType<UiRawJsonBlock>(restored.Blocks[7]);
        Assert.IsType<UiMapBlock>(restored.Blocks[8]);
        Assert.IsType<UiConfirmationPrompt>(restored.Prompts[0]);
        Assert.IsType<UiSelectionPrompt>(restored.Prompts[1]);
        Assert.IsType<UiTextInputPrompt>(restored.Prompts[2]);
        Assert.IsType<UiLongTextInputPrompt>(restored.Prompts[3]);

        var dossier = Assert.IsType<UiEntityDossierBlock>(restored.Blocks[2]);
        Assert.Equal("npc", dossier.EntityType);
        Assert.Equal("Мирра Ключница", dossier.Title);
        Assert.Equal("Союзник", dossier.Badges[0].Label);
        Assert.Equal(UiTone.Success, dossier.Badges[0].Tone);
        Assert.Equal("skills", dossier.Sections[0].Id);
        Assert.IsType<UiListBlock>(dossier.Sections[0].Blocks[0]);
        Assert.Equal("/api/media/npc-mirra", dossier.Media?.Url);

        var table = Assert.IsType<UiTableBlock>(restored.Blocks[3]);
        Assert.Equal(["Параметр", "Значение"], table.Columns);
        Assert.Equal(["ОД", "7"], table.Rows[0].Cells);

        var rawJson = Assert.IsType<UiRawJsonBlock>(restored.Blocks[7]);
        Assert.Equal(42, rawJson.Json?["turn"]?.GetValue<int>());

        var map = Assert.IsType<UiMapBlock>(restored.Blocks[8]);
        Assert.Equal("Mortal World", map.Map.Realm);
        Assert.Equal("loc_square", map.Map.CurrentNodeId);
        Assert.Contains(map.Map.Nodes, static node => node.IsCurrent && node.Label == "Площадь");
    }

    [Fact]
    public void CommandProtocolDtos_DoNotExposeSpectreTypes()
    {
        var dtoTypes = typeof(ExplorerCommandResult)
            .Assembly
            .GetTypes()
            .Where(static type => type.Namespace == "BookOfEternityClient.CommandProtocol")
            .ToArray();

        Assert.NotEmpty(dtoTypes);

        foreach (var property in dtoTypes.SelectMany(static type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)))
        {
            foreach (var exposedType in FlattenType(property.PropertyType))
            {
                var assemblyName = exposedType.Assembly.GetName().Name;
                var namespaceName = exposedType.Namespace ?? string.Empty;

                Assert.NotEqual("Spectre.Console", assemblyName);
                Assert.False(
                    namespaceName.StartsWith("Spectre.Console", StringComparison.Ordinal),
                    $"{property.DeclaringType?.Name}.{property.Name} exposes {exposedType.FullName}");
            }
        }
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        yield return type;

        if (type.IsArray)
        {
            foreach (var item in FlattenType(type.GetElementType()!))
            {
                yield return item;
            }
        }

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var item in FlattenType(argument))
            {
                yield return item;
            }
        }
    }
}
