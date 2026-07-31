using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class ReadableDocumentAuthorityValidationTests : IDisposable
{
    private const string IssueCode = "readable_document_missing_detail_authority";
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ReadableDocumentAuthorityValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-readable-document-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ReadableDocumentWithInlineText_DoesNotReportReadableDocumentIssue()
    {
        await WriteInventoryAsync(CreateDocumentItem(
            "doc_inline_1",
            "Письмо с площади",
            "\"textContent\": [\"Лира просит встретиться у фонтана до рассвета.\"],"));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.DoesNotContain(issues, IsReadableDocumentIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_InventoryTextContentWithTurnAnchor_ReportsPlayerFacingAnchorIssue()
    {
        await WriteInventoryAsync(CreateDocumentItem(
            "doc_anchor_inline_1",
            "Письмо с техническим якорем",
            "\"textContent\": [\"#[5]. Осмотр: на бумаге видны следы соли.\"],"));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "inventory_text_content_turn_anchor_player_facing", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("textContent[0]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ItemTextSidecarWithTurnAnchor_ReportsPlayerFacingAnchorIssue()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", BuildUpdateInventoryJson(CreateNewDocumentItem(
            "doc_anchor_sidecar_1",
            "Записка с якорем",
            "\"textContent\": null,")));
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            {
              "itemId": "doc_anchor_sidecar_1",
              "itemName": "Записка с якорем",
              "textContent": [
                "#[5] - Осмотр: на полях появился новый знак."
              ]
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "inventory_text_content_turn_anchor_player_facing", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("item_text_updates.json", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("textContent[0]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ItemJournalEntriesWithTurnAnchor_ReportsPlayerFacingAnchorIssue()
    {
        await WriteInventoryAsync(CreateDocumentItem(
            "doc_journal_anchor_inline_1",
            "Дневник с техническим якорем",
            "\"textContent\": [\"На первой странице виден знак старого владельца.\"],",
            "\"journalEntries\": [\"#[4]. Предмет найден на столе у окна.\"],"));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "item_journal_entry_turn_anchor_player_facing", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("journalEntries[0]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ItemJournalSidecarWithTurnAnchor_ReportsPlayerFacingAnchorIssue()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", BuildUpdateInventoryJson(CreateNewDocumentItem(
            "doc_journal_anchor_update_1",
            "Журнал с якорем",
            "\"textContent\": [\"На обложке выбит тусклый знак.\"],")));
        await _fs.WriteFileAtomicAsync("game_state/npcs/item_journals.json", """
        {
          "entries": [
            {
              "itemId": "doc_journal_anchor_update_1",
              "itemName": "Журнал с якорем",
              "journalEntries": [
                "#[5] - На полях появилась новая отметка."
              ]
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "item_journal_entry_turn_anchor_player_facing", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("item_journals.json", StringComparison.OrdinalIgnoreCase) &&
            issue.FilePath.Contains("journalEntries[0]", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ReadableDocumentWithoutDetailAuthority_ReportsReadableDocumentIssue()
    {
        await WriteInventoryAsync(CreateDocumentItem(
            "doc_missing_1",
            "Письмо без текста",
            "\"textContent\": null,"));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.Contains(issues, issue =>
            IsReadableDocumentIssue(issue) &&
            issue.FilePath.Contains("doc_missing_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Письмо без текста", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SealedDocumentWithReason_DoesNotReportReadableDocumentIssue()
    {
        await WriteInventoryAsync(CreateDocumentItem(
            "doc_sealed_1",
            "Запечатанное письмо",
            """
            "textContent": null,
                  "unreadableReason": "Письмо запечатано неизвестной печатью.",
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.DoesNotContain(issues, IsReadableDocumentIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ItemTextSidecarResolvesDocumentByStableId()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", BuildUpdateInventoryJson(CreateNewDocumentItem(
            "doc_sidecar_1",
            "Записка с рынка",
            "\"textContent\": null,")));
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            {
              "itemId": "doc_sidecar_1",
              "itemName": "Не это имя",
              "textContent": [
                "На обороте записки указан путь через северные ворота."
              ]
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.DoesNotContain(issues, IsReadableDocumentIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ItemJournalSidecarResolvesDocumentByStableId()
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", BuildUpdateInventoryJson(CreateNewDocumentItem(
            "doc_journal_1",
            "Памятная книга",
            "\"textContent\": null,")));
        await _fs.WriteFileAtomicAsync("game_state/npcs/item_journals.json", """
        {
          "entries": [
            {
              "itemId": "doc_journal_1",
              "itemName": "Другое имя",
              "journalEntries": [
                {
                  "timestamp": "2026-03-19T12:00:00Z",
                  "event": "Пробуждение",
                  "description": "Книга шепчет о владельце."
                }
              ]
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.DoesNotContain(issues, IsReadableDocumentIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ItemTextSidecarWithWrongStableIdAndSameName_DoesNotSatisfyDocumentAuthority()
    {
        await WriteInventoryAsync(CreateDocumentItem(
            "doc_target_same_name_1",
            "Записка с рынка",
            "\"textContent\": null,"));
        await _fs.WriteFileAtomicAsync("game_state/inventory/item_text_updates.json", """
        {
          "entries": [
            {
              "itemId": "doc_other_same_name_1",
              "itemName": "Записка с рынка",
              "textContent": [
                "Этот текст относится к другому предмету с тем же именем."
              ]
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.Contains(issues, issue =>
            IsReadableDocumentIssue(issue) &&
            issue.FilePath.Contains("doc_target_same_name_1", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_NonDocumentInventoryItemWithoutText_DoesNotReportReadableDocumentIssue()
    {
        await WriteInventoryAsync("""
            {
              "existedId": "tool_plain_1",
              "itemId": "tool_plain_1",
              "name": "Стальной нож",
              "description": "Обычный походный нож без текста.",
              "type": "Инструмент",
              "group": "Снаряжение",
              "textContent": null
            }
        """);

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.ReadableDocument);

        Assert.DoesNotContain(issues, IsReadableDocumentIssue);
    }

    private async Task WriteInventoryAsync(params string[] items)
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", BuildInventoryJson(items));
    }

    private static string BuildInventoryJson(params string[] items)
    {
        return $$"""
        {
          "items": [
        {{string.Join(",\n", items)}}
          ]
        }
        """;
    }

    private static string BuildUpdateInventoryJson(params string[] items)
    {
        return $$"""
        {
          "UpdateInventory": [
        {{string.Join(",\n", items)}}
          ]
        }
        """;
    }

    private static string CreateDocumentItem(
        string id,
        string name,
        string readableFields,
        string journalEntriesField = "\"journalEntries\": null,")
    {
        return $$"""
            {
              "existedId": "{{id}}",
              "itemId": "{{id}}",
              "name": "{{name}}",
              "description": "{{name}} как физический предмет.",
              "image_prompt": "a readable inventory document",
              "quality": "Common",
              "price": 0,
              "count": 1,
              "weight": 0.1,
              "volume": 0.01,
              "contentsPath": null,
              "isContainer": false,
              "isConsumption": false,
              "requiresTwoHands": false,
              "durability": "100%",
              "type": "Документ",
              "group": "Документы и медиа",
              {{readableFields}}
              {{journalEntriesField}}
              "equipmentSlot": null,
              "accessoryForSlot": null
            }
        """;
    }

    private static string CreateNewDocumentItem(string id, string name, string readableFields)
    {
        return $$"""
            {
              "existedId": null,
              "itemId": "{{id}}",
              "name": "{{name}}",
              "description": "{{name}} как физический предмет.",
              "image_prompt": "a readable inventory document",
              "quality": "Common",
              "price": 0,
              "count": 1,
              "weight": 0.1,
              "volume": 0.01,
              "contentsPath": null,
              "isContainer": false,
              "isConsumption": false,
              "requiresTwoHands": false,
              "durability": "100%",
              "type": "Документ",
              "group": "Документы и медиа",
              {{readableFields}}
              "journalEntries": null,
              "equipmentSlot": null,
              "accessoryForSlot": null
            }
        """;
    }

    private static bool IsReadableDocumentIssue(ValidationIssue issue) =>
        string.Equals(issue.Code, IssueCode, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
