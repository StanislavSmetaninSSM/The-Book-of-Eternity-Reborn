using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerWebCommandServiceTestsShiningAdvancedDiagnostics : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _service;

    public ExplorerWebCommandServiceTestsShiningAdvancedDiagnostics()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-shining-advanced-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation);
    }

    [Theory]
    [InlineData("/shining_treasury", "Казначейство Сияющей Обители")]
    [InlineData("/source_of_light", "Источник Света")]
    public async Task ExecuteAsync_ShiningTreasuryAndSource_DefaultProjectionOmitsRawDiagnostics(string command, string expectedTitle)
    {
        await SeedShiningAdvancedDiagnosticFilesAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var visibleText = CollectVisibleText(result);
        Assert.Contains(expectedTitle, visibleText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Сия", visibleText, StringComparison.OrdinalIgnoreCase);
        AssertNoIssue1072DefaultTechnicalLeak(visibleText);

        var payload = SerializeResult(result);
        Assert.DoesNotContain("rawJson", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("issue1072_shining_raw_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("issue1072_soul_raw_marker", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("issue1072_source_raw_marker", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/shining_treasury", "issue1072_shining_raw_marker")]
    [InlineData("/source_of_light", "issue1072_source_raw_marker")]
    public async Task ExecuteAsync_ShiningTreasuryAndSource_AdvancedProjectionKeepsExplicitRawDiagnostics(
        string command,
        string expectedRawMarker)
    {
        await SeedShiningAdvancedDiagnosticFilesAsync();

        var advanced = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command, AdvancedEnabled: true));

        Assert.Equal(CommandExecutionState.RequiresInput, advanced.State);
        Assert.Contains(advanced.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains(expectedRawMarker, SerializeResult(advanced), StringComparison.OrdinalIgnoreCase);

        _stateManager.Settings.ShowGmThoughts = true;
        var gmThoughts = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.RequiresInput, gmThoughts.State);
        Assert.Contains(gmThoughts.Blocks, static block => block is UiRawJsonBlock);
        Assert.Contains(expectedRawMarker, SerializeResult(gmThoughts), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/shining_treasury", ShiningAbodeState.StatePath)]
    [InlineData("/source_of_light", SourceOfLightCapstoneState.PendingRequestPath)]
    public async Task ExecuteAsync_ShiningTreasuryAndSource_MalformedDefaultDiagnosticsUseSafePlayerCopy(
        string command,
        string malformedPath)
    {
        await SeedShiningAdvancedDiagnosticFilesAsync();
        await _fs.WriteFileAtomicAsync(malformedPath, "{ malformed issue1072 diagnostic payload");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(command));

        Assert.Equal(CommandExecutionState.RequiresInput, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiRawJsonBlock);
        var visibleText = CollectVisibleText(result);
        Assert.Contains("провер", visibleText, StringComparison.OrdinalIgnoreCase);
        AssertNoIssue1072DefaultTechnicalLeak(visibleText);
        Assert.DoesNotContain("malformed issue1072 diagnostic payload", SerializeResult(result), StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temporary test files.
        }
    }

    private async Task SeedShiningAdvancedDiagnosticFilesAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentRealm": "Shining Abode",
          "currentIncarnation": 5,
          "inkFeathers": { "current": 24, "total": 90 },
          "afterlifeCombatProfile": { "capstones": {} },
          "_debug": "issue1072_soul_raw_marker"
        }
        """);

        await _fs.WriteFileAtomicAsync(ShiningAbodeState.StatePath, """
        {
          "availability": "active",
          "lightSparks": 12,
          "radiance": { "experience": 580, "tier": 4 },
          "gates": { "hasOpenDraft": true },
          "treasury": {
            "depositedInkFeathers": 20,
            "claimableInkFeatherInterest": 2,
            "lastInterestSettlementCycleId": "cycle_5",
            "exchangeCycleId": "cycle_5",
            "exchangeThisCycleLightSparks": 1
          },
          "sourceOfLightCapstone": { "completed": false },
          "_debug": "issue1072_shining_raw_marker"
        }
        """);

        await _fs.WriteFileAtomicAsync(SourceOfLightCapstoneState.PendingRequestPath, """
        {
          "requests": [
            {
              "requestId": "source_of_light_capstone:issue1072",
              "createdAtTurn": 42,
              "radianceExperienceAtRequest": 580,
              "radianceTierAtRequest": 4,
              "_debug": "issue1072_source_raw_marker"
            }
          ]
        }
        """);
    }

    private static string CollectVisibleText(ExplorerCommandResult result) =>
        CollectBlockText(result.Blocks) + "\n" + string.Join("\n", result.Prompts.Select(CollectPromptText));

    private static string CollectPromptText(UiPrompt prompt)
    {
        var parts = new List<string> { prompt.Prompt };
        if (prompt is UiSelectionPrompt selection)
        {
            foreach (var option in selection.Options)
            {
                parts.Add(option.Label);
                parts.Add(option.Description);
            }
        }

        if (prompt is UiTextInputPrompt input)
            parts.Add(input.Placeholder);

        return string.Join("\n", parts);
    }

    private static string CollectBlockText(IEnumerable<UiBlock> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts);
        return string.Join("\n", parts);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiEntityDossierBlock dossier:
                parts.Add(dossier.Title);
                parts.Add(dossier.Subtitle);
                parts.Add(dossier.Summary);
                parts.AddRange(dossier.Badges.Select(static badge => badge.Label));
                CollectEntityFacts(dossier.Facts, parts);
                CollectEntityMetrics(dossier.Metrics, parts);
                CollectEntityHints(dossier.Hints, parts);
                parts.AddRange(dossier.List);
                foreach (var card in dossier.Cards)
                    CollectEntityCardText(card, parts);
                foreach (var section in dossier.Sections)
                {
                    parts.Add(section.Title);
                    parts.Add(section.Summary);
                    parts.Add(section.CollectionLabel);
                    CollectEntityFacts(section.Facts, parts);
                    CollectEntityMetrics(section.Metrics, parts);
                    CollectEntityHints(section.Hints, parts);
                    parts.AddRange(section.List);
                    foreach (var card in section.Cards)
                        CollectEntityCardText(card, parts);
                    foreach (var child in section.Blocks)
                        CollectBlockText(child, parts);
                }
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                foreach (var row in table.Rows)
                    parts.AddRange(row.Cells);
                break;
            case UiKeyValueGridBlock grid:
                foreach (var item in grid.Items)
                {
                    parts.Add(item.Key);
                    parts.Add(item.Value);
                }
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiRawJsonBlock raw:
                parts.Add(raw.Title);
                parts.Add(raw.Json?.ToJsonString() ?? string.Empty);
                break;
        }
    }

    private static void CollectEntityCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(static badge => badge.Label));
        CollectEntityFacts(card.Facts, parts);
        CollectEntityMetrics(card.Metrics, parts);
        CollectEntityHints(card.Hints, parts);
        parts.AddRange(card.List);
        foreach (var child in card.Nested)
            CollectEntityCardText(child, parts);
        foreach (var child in card.Cards)
            CollectEntityCardText(child, parts);
    }

    private static void CollectEntityFacts(IEnumerable<UiEntityFact> facts, List<string> parts)
    {
        foreach (var fact in facts)
        {
            parts.Add(fact.Label);
            parts.Add(fact.Value);
        }
    }

    private static void CollectEntityMetrics(IEnumerable<UiEntityMetric> metrics, List<string> parts)
    {
        foreach (var metric in metrics)
        {
            parts.Add(metric.Label);
            parts.Add(metric.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            parts.Add(metric.Max.ToString(System.Globalization.CultureInfo.InvariantCulture));
            parts.Add(metric.Note);
        }
    }

    private static void CollectEntityHints(IEnumerable<UiEntityHint> hints, List<string> parts)
    {
        foreach (var hint in hints)
        {
            parts.Add(hint.Title);
            parts.Add(hint.Text);
        }
    }

    private static void AssertNoIssue1072DefaultTechnicalLeak(string visibleText)
    {
        foreach (var forbidden in new[]
                 {
                     ".json", "game_state/", "UiRawJsonBlock", "rawJson", "DTO", "API", "endpoint",
                     "protocol", "протокол", "debug", "отлад", "exception", "parser", "stack",
                     "pending_", "pending-файл"
                 })
        {
            Assert.DoesNotContain(forbidden, visibleText, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string SerializeResult(ExplorerCommandResult result) =>
        JsonSerializer.Serialize(result, JsonOptions);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
