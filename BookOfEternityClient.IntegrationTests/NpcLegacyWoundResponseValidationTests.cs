using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class NpcLegacyWoundResponseValidationTests
{
    private const string LegacyPath = "response.NPCWoundChanges";
    private const string LegacyCode = "wound_materialization_legacy_response_unsupported";
    private const string EffectsPath = "game_state/npcs/npc_effects.json";
    private const string WoundsPath = "game_state/npcs/npc_wounds.json";
    private const string CorePath = "game_state/npcs/npc_core.json";
    private const string Effects = "{\"schemaVersion\":1,\"entries\":[],\"_note\":\"retained effect metadata\"}";
    private const string Wounds = "{\"schemaVersion\":1,\"entries\":[]}";
    private const string Core = "{\"UpdateNPCs\":[{\"NPCId\":\"npc_guard\",\"name\":\"Страж\"}]}";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidateResponse_RejectsLegacyWoundArraysBeforeAdmission(bool populated)
    {
        await using var context = await CreateContextAsync();
        AssertLawfulControls(context.Validator);
        var response = CurrentResponse();
        response["NPCWoundChanges"] = populated
            ? new JsonArray(new JsonObject { ["NPCId"] = "npc_guard", ["wounds"] = new JsonArray() })
            : new JsonArray();
        var raw = response.ToJsonString();
        using var document = JsonDocument.Parse(raw);

        var issues = context.Validator.ValidateResponse(document.RootElement);

        await AssertUnchangedAsync(context, raw, document);
        // This is the admission oracle: a new code alone cannot manufacture causal RED.
        Assert.Contains(issues, issue => issue.FilePath == LegacyPath && issue.Severity == IssueSeverity.Error);
        AssertLegacyIssue(issues);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("7")]
    public async Task ValidateResponse_ClassifiesLegacyNullAndScalarWithoutArrayRepair(string payload)
    {
        await using var context = await CreateContextAsync();
        AssertLawfulControls(context.Validator);
        var response = CurrentResponse();
        response["NPCWoundChanges"] = JsonNode.Parse(payload);
        var raw = response.ToJsonString();
        using var document = JsonDocument.Parse(raw);

        var issues = context.Validator.ValidateResponse(document.RootElement);

        await AssertUnchangedAsync(context, raw, document);
        // Shape refusal already exists; only the legacy-specific classification is new.
        Assert.Contains(issues, issue => issue.FilePath == LegacyPath && issue.Severity == IssueSeverity.Error);
        AssertLegacyIssue(issues);
    }

    [Fact]
    public async Task ValidateResponse_AcceptsAbsentWoundsAndCurrentNpcRenameWithoutMutation()
    {
        await using var context = await CreateContextAsync();
        AssertLawfulControls(context.Validator);
        var raw = CurrentResponse().ToJsonString();
        using var document = JsonDocument.Parse(raw);

        Assert.Empty(context.Validator.ValidateResponse(document.RootElement));

        await AssertUnchangedAsync(context, raw, document);
    }

    private static async Task<ResourceMaterializationTestContext> CreateContextAsync()
    {
        var context = await ResourceMaterializationTestContext.CreateAsync();
        try
        {
            await context.WriteExactJsonAsync(EffectsPath, Effects);
            await context.WriteExactJsonAsync(WoundsPath, Wounds);
            await context.WriteExactJsonAsync(CorePath, Core);
            return context;
        }
        catch
        {
            await context.DisposeAsync();
            throw;
        }
    }

    private static JsonObject CurrentResponse() => new()
    {
        ["response"] = "Страж продолжает дозор.",
        ["NPCsRenameData"] = new JsonArray(new JsonObject { ["oldName"] = "Страж", ["newName"] = "Дозорный" })
    };

    private static void AssertLawfulControls(ValidationService validator)
    {
        using var absent = JsonDocument.Parse("{\"response\":\"Дозор продолжается.\"}");
        Assert.Empty(validator.ValidateResponse(absent.RootElement));
        using var current = JsonDocument.Parse(CurrentResponse().ToJsonString());
        Assert.Empty(validator.ValidateResponse(current.RootElement));
    }

    private static void AssertLegacyIssue(List<ValidationIssue> issues)
    {
        var issue = Assert.Single(issues);
        Assert.Equal(LegacyPath, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(LegacyCode, issue.Code);
        Assert.DoesNotContain("effectChanges", issue.RepairHint ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertUnchangedAsync(ResourceMaterializationTestContext context, string raw, JsonDocument document)
    {
        Assert.Equal(raw, document.RootElement.GetRawText());
        Assert.Equal(Encoding.UTF8.GetBytes(Effects), await context.FileSystem.ReadFileBytesAsync(EffectsPath));
        Assert.Equal(Encoding.UTF8.GetBytes(Wounds), await context.FileSystem.ReadFileBytesAsync(WoundsPath));
        Assert.Equal(Encoding.UTF8.GetBytes(Core), await context.FileSystem.ReadFileBytesAsync(CorePath));
    }
}
