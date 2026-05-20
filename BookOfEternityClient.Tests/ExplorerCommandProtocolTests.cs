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
        Assert.Contains("\"kind\": \"selection\"", json, StringComparison.Ordinal);
        Assert.Contains("\"state\": \"RequiresInput\"", json, StringComparison.Ordinal);

        var restored = JsonSerializer.Deserialize<ExplorerCommandResult>(json, JsonOpts);

        Assert.NotNull(restored);
        Assert.Equal(CommandExecutionState.RequiresInput, restored.State);
        Assert.IsType<UiTextBlock>(restored.Blocks[0]);
        Assert.IsType<UiPanelBlock>(restored.Blocks[1]);
        Assert.IsType<UiTableBlock>(restored.Blocks[2]);
        Assert.IsType<UiListBlock>(restored.Blocks[3]);
        Assert.IsType<UiKeyValueGridBlock>(restored.Blocks[4]);
        Assert.IsType<UiMessageBlock>(restored.Blocks[5]);
        Assert.IsType<UiRawJsonBlock>(restored.Blocks[6]);
        Assert.IsType<UiConfirmationPrompt>(restored.Prompts[0]);
        Assert.IsType<UiSelectionPrompt>(restored.Prompts[1]);
        Assert.IsType<UiTextInputPrompt>(restored.Prompts[2]);
        Assert.IsType<UiLongTextInputPrompt>(restored.Prompts[3]);

        var table = Assert.IsType<UiTableBlock>(restored.Blocks[2]);
        Assert.Equal(["Параметр", "Значение"], table.Columns);
        Assert.Equal(["ОД", "7"], table.Rows[0].Cells);

        var rawJson = Assert.IsType<UiRawJsonBlock>(restored.Blocks[6]);
        Assert.Equal(42, rawJson.Json?["turn"]?.GetValue<int>());
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
