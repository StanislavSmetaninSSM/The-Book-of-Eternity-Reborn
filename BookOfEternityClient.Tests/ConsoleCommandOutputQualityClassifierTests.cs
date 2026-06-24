using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleCommandOutputQualityClassifierTests
{
    [Fact]
    public void Classify_DefaultPlayerOutputFlagsRawJsonAndTechnicalMarkers()
    {
        var result = new ExplorerCommandResult
        {
            Command = "/status",
            State = CommandExecutionState.Completed,
            Blocks =
            [
                new UiTextBlock { Text = "game_state/meta/soul_state.json" },
                new UiRawJsonBlock
                {
                    Title = "Полный JSON",
                    Json = JsonNode.Parse("""{"requestId":"abc"}""")
                }
            ]
        };

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);

        Assert.Contains(report.Violations, violation => violation.Contains("raw JSON", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Violations, violation => violation.Contains("game_state/", StringComparison.OrdinalIgnoreCase));
        Assert.False(report.IsUsablePlayerOutput);
    }

    [Fact]
    public void Classify_ReadableOutputWithActionsAndPromptPasses()
    {
        var result = new ExplorerCommandResult
        {
            Command = "/книги",
            State = CommandExecutionState.RequiresInput,
            Blocks =
            [
                new UiPanelBlock
                {
                    Title = "Книжная полка",
                    Blocks =
                    [
                        new UiTextBlock { Text = "Выберите документ, который хотите прочитать." }
                    ]
                }
            ],
            Actions =
            [
                new UiAction { Label = "Открыть письмо", Command = "/книги документ letter_001" }
            ],
            Prompts =
            [
                new UiSelectionPrompt
                {
                    Prompt = "Что прочитать?",
                    Options =
                    [
                        new UiSelectionOption { Label = "Письмо с печатью", Description = "Короткое письмо на столе." }
                    ]
                }
            ]
        };

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);

        Assert.True(report.IsUsablePlayerOutput);
        Assert.Empty(report.Violations);
        Assert.Contains("Книжная полка", report.VisibleText, StringComparison.Ordinal);
        Assert.Contains("Открыть письмо", report.VisibleText, StringComparison.Ordinal);
        Assert.Contains("Письмо с печатью", report.VisibleText, StringComparison.Ordinal);
    }

    [Fact]
    public void Classify_DefaultPlayerOutputFlagsAfterlifeContractMarkers()
    {
        var result = new ExplorerCommandResult
        {
            Command = "/chaos_sea",
            State = CommandExecutionState.Completed,
            Blocks =
            [
                new UiTextBlock { Text = "pending_turn_snapshot requestId actionType protocol" }
            ]
        };

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);

        Assert.False(report.IsUsablePlayerOutput);
        Assert.Contains(report.Violations, violation => violation.Contains("pending_", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Violations, violation => violation.Contains("requestId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Violations, violation => violation.Contains("actionType", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Violations, violation => violation.Contains("protocol", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Classify_DefaultPlayerOutputFlagsDebugMarker()
    {
        var result = new ExplorerCommandResult
        {
            Command = "/status",
            State = CommandExecutionState.Completed,
            Blocks =
            [
                new UiTextBlock { Text = "debug trace should not appear in normal player output" }
            ]
        };

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);

        Assert.False(report.IsUsablePlayerOutput);
        Assert.Contains(report.Violations, violation => violation.Contains("debug", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Classify_DefaultPlayerOutputFlagsNullMarker()
    {
        var result = new ExplorerCommandResult
        {
            Command = "/инв",
            State = CommandExecutionState.Completed,
            Blocks =
            [
                new UiKeyValueGridBlock
                {
                    Items =
                    [
                        new UiKeyValueItem { Key = "Аксессуар для", Value = "null" }
                    ]
                }
            ]
        };

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);

        Assert.False(report.IsUsablePlayerOutput);
        Assert.Contains(report.Violations, violation => violation.Contains("null", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Classify_DefaultPlayerOutputFlagsUiInstructionCopyAndGenericReferenceSummaries()
    {
        var result = new ExplorerCommandResult
        {
            Command = "/взаимодействия",
            State = CommandExecutionState.Completed,
            Blocks =
            [
                new UiEntityDossierBlock
                {
                    Title = "Взаимодействия игроков",
                    Summary = "Что уже отмечено в книге.",
                    Sections =
                    [
                        new UiEntityDossierSection
                        {
                            Title = "Записи",
                            Summary = "Последние видимые записи. Полные сведения открываются отдельной карточкой."
                        },
                        new UiEntityDossierSection
                        {
                            Title = "Сводка",
                            Summary = "Известные записи этого раздела."
                        }
                    ]
                }
            ]
        };

        var report = ConsoleCommandOutputQualityClassifier.Classify(result);

        Assert.False(report.IsUsablePlayerOutput);
        Assert.Contains(report.Violations, violation => violation.Contains("Полные сведения", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Violations, violation => violation.Contains("Что уже отмечено в книге", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Violations, violation => violation.Contains("Известные записи этого раздела", StringComparison.OrdinalIgnoreCase));
    }
}
