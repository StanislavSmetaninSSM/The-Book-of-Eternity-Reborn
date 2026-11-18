using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalNpcResourceTemplateGuidanceTests
{
    [Fact]
    public void CompactNpcCreationJsonDeclaresClosedResourceMaxima()
    {
        var template = ReadTemplate();
        var heading = template.IndexOf("## Minimal safe NPC scene object", StringComparison.Ordinal);
        Assert.True(heading >= 0);
        var start = template.IndexOf("```json\n", heading, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += "```json\n".Length;
        var end = template.IndexOf("\n```", start, StringComparison.Ordinal);
        Assert.True(end > start);
        using var document = JsonDocument.Parse(template[start..end]);
        var actor = document.RootElement;
        Assert.Equal(JsonValueKind.Null, actor.GetProperty("NPCId").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(actor.GetProperty("initialId").GetString()));
        // This checks the published creation fragment, not whole-placeholder NPC acceptance.
        Assert.True(actor.TryGetProperty("resourceMaterialization", out var envelope),
            "The compact new-NPC creation object omits mandatory resourceMaterialization.");
        Assert.Equal(JsonValueKind.Object, envelope.ValueKind);
        Assert.Equal("resources", Assert.Single(envelope.EnumerateObject()).Name);
        var resources = envelope.GetProperty("resources");
        Assert.Equal(JsonValueKind.Array, resources.ValueKind);
        Assert.NotEmpty(resources.EnumerateArray());
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in resources.EnumerateArray())
        {
            Assert.Equal(new[] { "maximum", "resourceKey" },
                resource.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
            Assert.Equal(JsonValueKind.String, resource.GetProperty("resourceKey").ValueKind);
            Assert.True(keys.Add(resource.GetProperty("resourceKey").GetString()!));
            Assert.Equal(JsonValueKind.Number, resource.GetProperty("maximum").ValueKind);
            Assert.True(resource.GetProperty("maximum").TryGetDouble(out var maximum) &&
                        double.IsFinite(maximum) && maximum > 0);
        }
        Assert.Contains("health", keys);
        foreach (var forbidden in new[] { "current", "currentHealthPercentage", "maxHealthPercentage", "resourceOwnerBindings", "resourceReceipts" })
            Assert.False(actor.TryGetProperty(forbidden, out _));
        Assert.Contains("resourceMaterialization.resources", template, StringComparison.Ordinal);
        Assert.Contains("maximum", template, StringComparison.Ordinal);
        Assert.Contains("Existing NPCs must not resend resourceMaterialization", template, StringComparison.Ordinal);
        Assert.Contains("Do not author current values or resource receipts", template, StringComparison.Ordinal);
        Assert.Contains("Poise is not universal", template, StringComparison.Ordinal);
        var source = ReadDaemon();
        var directive = Assert.Single(source.Split('\n'), line => line.TrimStart().StartsWith(
            "\"Use MORTAL_NPC_UPDATE_TEMPLATE.md before editing", StringComparison.Ordinal));
        Assert.Contains("resourceMaterialization.resources", directive, StringComparison.Ordinal);
        Assert.Contains("maximum", directive, StringComparison.Ordinal);
    }

    /// <summary>Reads the shipped daemon source without executing its bootstrap.</summary>
    private static string ReadDaemon() => File.ReadAllText(Path.Combine(
        TestRepoPaths.RepoRoot, "BookOfEternityClient", "game_master_daemon.ps1")).Replace("\r\n", "\n");

    /// <summary>Extracts the uniquely named literal here-string exported as the compact NPC template.</summary>
    private static string ReadTemplate()
    {
        var source = ReadDaemon();
        const string marker = "-Role \"compact_mortal_npc_update_template\" -Content @'\n";
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.Equal(start, source.LastIndexOf(marker, StringComparison.Ordinal));
        start += marker.Length;
        var end = source.IndexOf("\n'@", start, StringComparison.Ordinal);
        Assert.True(end > start);
        return source[start..end];
    }
}
