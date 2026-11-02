using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Raw combat response admission must not accept a retired wound-effect route.</summary>
public sealed class CombatantLegacyWoundResponseValidationTests
{
    private const string LegacyCode = "wound_materialization_legacy_effect_unsupported";

    /// <summary>Both sides and both collection names must reject the formerly accepted wound-reference effect type.</summary>
    [Theory]
    [InlineData("EnemiesData", "activeBuffs", "WoundReference")]
    [InlineData("EnemiesData", "activeDebuffs", "woundreference")]
    [InlineData("AlliesData", "activeBuffs", "WoundReference")]
    [InlineData("AlliesData", "activeDebuffs", "WoundReference")]
    public async Task ValidateResponse_RejectsLegacyWoundReferenceBeforeAdmission(string side, string collection, string effectType)
    {
        await AssertRefusedWithoutMutationAsync(side, collection, CreateLegacyReferenceEffect(effectType), "effectType");
    }

    /// <summary>The retired source property cannot be smuggled into a neighboring ordinary effect, even as explicit null.</summary>
    [Theory]
    [InlineData("EnemiesData", "activeBuffs", false)]
    [InlineData("AlliesData", "activeDebuffs", true)]
    public async Task ValidateResponse_RejectsLegacyWoundSourcePropertyBeforeAdmission(string side, string collection, bool explicitNull)
    {
        var effect = CurrentAdjacentEffect();
        effect["sourceWoundId"] = explicitNull ? null : JsonValue.Create("missing_wound");
        await AssertRefusedWithoutMutationAsync(side, collection, effect, "sourceWoundId");
    }

    /// <summary>Absent legacy input and adjacent non-wound raw effect syntax preserve their existing validation behavior.</summary>
    [Fact]
    public async Task ValidateResponse_PreservesCurrentEmptyAndAdjacentRawCombatFieldsWithoutMutation()
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var sources = await SeedCurrentSourcesAsync(context);
        AssertLawfulRawControls(context.Validator);
        var raw = CurrentResponse(adjacentEffects: true).ToJsonString();
        using var document = JsonDocument.Parse(raw);

        Assert.Empty(context.Validator.ValidateResponse(document.RootElement));

        await AssertUnchangedAsync(context, sources, raw, document);
    }

    /// <summary>Creates detached complete named-NPC input DATA; it grants no source, resource or publication authority.</summary>
    internal static JsonObject CreateNamedNpcCombatant(string npcId) => new()
    {
        ["NPCId"] = npcId, ["name"] = "Страж", ["image_prompt"] = "Караульный в дорожном плаще",
        ["description"] = "Страж у ворот", ["type"] = "NPC", ["isGroup"] = false,
        ["actions"] = new JsonArray(), ["resistances"] = new JsonArray(),
        ["activeBuffs"] = new JsonArray(), ["activeDebuffs"] = new JsonArray()
    };

    /// <summary>Complete formerly accepted input uses an unresolved wound ID deliberately; expected refusal is independently literal.</summary>
    internal static JsonObject CreateLegacyReferenceEffect(string effectType = "WoundReference") => new()
    {
        ["effectType"] = effectType, ["value"] = "0%", ["description"] = "Устаревшая ссылка на рану.",
        ["duration"] = 999, ["sourceWoundId"] = "missing_wound"
    };

    /// <summary>Requires a real Error before code/hint checks; exact canonical source and submitted JSON comparisons run first.</summary>
    private static async Task AssertRefusedWithoutMutationAsync(string side, string collection, JsonObject effect, string field)
    {
        await using var context = await ResourceMaterializationTestContext.CreateAsync();
        var sources = await SeedCurrentSourcesAsync(context);
        AssertLawfulRawControls(context.Validator);
        var response = CurrentResponse(adjacentEffects: false);
        response[side]![0]![collection] = new JsonArray(effect);
        var raw = response.ToJsonString();
        using var document = JsonDocument.Parse(raw);

        var issues = context.Validator.ValidateResponse(document.RootElement);

        await AssertUnchangedAsync(context, sources, raw, document);
        var path = "response." + side + "[0]." + collection + "[0]." + field;
        Assert.Contains(issues, issue => issue.FilePath == path && issue.Severity == IssueSeverity.Error);
        var issue = Assert.Single(issues);
        Assert.Equal(path, issue.FilePath);
        Assert.Equal(IssueSeverity.Error, issue.Severity);
        Assert.Equal(LegacyCode, issue.Code);
        Assert.DoesNotContain("999", issue.RepairHint ?? string.Empty, StringComparison.Ordinal);
    }

    /// <summary>Uses existing current empty combat effect carriers, checked through their typed contract before writing.</summary>
    private static async Task<Dictionary<string, byte[]>> SeedCurrentSourcesAsync(ResourceMaterializationTestContext context)
    {
        var sources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var (path, key, id) in new[]
        {
            (EffectCarrierCatalog.EnemiesPath, "enemiesData", "npc_enemy_guard"),
            (EffectCarrierCatalog.AlliesPath, "alliesData", "npc_ally_guard")
        })
        {
            var actor = CreateNamedNpcCombatant(id);
            using var document = JsonDocument.Parse(actor.ToJsonString());
            Assert.Empty(EffectMaterializationContract.ValidateCarrier(document.RootElement, path + "." + key + "[0]", EffectCarrierKind.Combatant));
            sources.Add(path, Encoding.UTF8.GetBytes(new JsonObject { [key] = new JsonArray(actor) }.ToJsonString()));
        }
        sources.Add("game_state/npcs/npc_core.json", Encoding.UTF8.GetBytes(
            "{\"UpdateNPCs\":[{\"NPCId\":\"npc_enemy_guard\",\"name\":\"Страж\"},{\"NPCId\":\"npc_ally_guard\",\"name\":\"Страж\"}]}"));
        foreach (var (path, bytes) in sources) await context.WriteExactBytesAsync(path, bytes);
        return sources;
    }

    /// <summary>Builds independent supported raw controls; legacy non-wound syntax here is not a canonical/publication claim.</summary>
    private static JsonObject CurrentResponse(bool adjacentEffects)
    {
        var result = new JsonObject { ["response"] = "Стражи остаются у ворот." };
        foreach (var (side, id) in new[] { ("EnemiesData", "npc_enemy_guard"), ("AlliesData", "npc_ally_guard") })
        {
            var actor = CreateNamedNpcCombatant(id);
            if (adjacentEffects)
                foreach (var collection in new[] { "activeBuffs", "activeDebuffs" })
                    actor[collection] = new JsonArray(CurrentAdjacentEffect());
            result[side] = new JsonArray(actor);
        }
        return result;
    }

    /// <summary>Unchanged neighboring raw effect form; this test does not authorize publishing a nonempty direct carrier.</summary>
    private static JsonObject CurrentAdjacentEffect() => new()
    {
        ["effectType"] = "Buff", ["targetType"] = "Damage", ["value"] = "0%",
        ["description"] = "Обычное соседнее поле.", ["duration"] = 1
    };

    /// <summary>Establishes absent and complete current neighboring raw inputs before the target mutation.</summary>
    private static void AssertLawfulRawControls(ValidationService validator)
    {
        foreach (var adjacent in new[] { false, true })
        {
            using var document = JsonDocument.Parse(CurrentResponse(adjacent).ToJsonString());
            Assert.Empty(validator.ValidateResponse(document.RootElement));
        }
    }

    /// <summary>Read-only evidence is exact submitted JSON and real source bytes, not a cloned validator result.</summary>
    private static async Task AssertUnchangedAsync(ResourceMaterializationTestContext context, Dictionary<string, byte[]> sources, string raw, JsonDocument document)
    {
        Assert.Equal(raw, document.RootElement.GetRawText());
        foreach (var (path, bytes) in sources) Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(path));
    }
}
