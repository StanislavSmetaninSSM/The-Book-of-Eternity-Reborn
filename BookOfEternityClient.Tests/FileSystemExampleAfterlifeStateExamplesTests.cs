using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FileSystemExampleAfterlifeStateExamplesTests
{
    [Fact]
    public void RequiredAfterlifeExampleStateFiles_ExistAndCoverDisplayFilteringSurfaces()
    {
        using var chronicles = ReadGameStateExample("afterlife", "afterlife_chronicles.json");
        var chronicleEntries = GetRequiredArray(chronicles.RootElement, "chronicles").EnumerateArray().ToList();
        Assert.Contains(chronicleEntries, entry =>
            HasNonEmptyArray(entry, "eventDescriptions") &&
            HasNonEmptyString(entry, "lastEventsDescription") &&
            HasNonEmptyArray(entry, "persistentConsequences"));
        Assert.Contains(chronicleEntries, entry => HasNonEmptyArray(entry, "openThreads"));

        using var threats = ReadGameStateExample("afterlife", "afterlife_active_threats.json");
        var threatEntries = GetRequiredArray(threats.RootElement, "threats").EnumerateArray().ToList();
        Assert.Contains(threatEntries, threat => IsBoolean(threat, "visibleToPlayer", expected: true));
        Assert.Contains(threatEntries, threat =>
            IsBoolean(threat, "visibleToPlayer", expected: false) &&
            threat.TryGetProperty("sarefLink", out var sarefLink) &&
            IsHiddenVisibility(GetString(sarefLink, "visibility")));

        using var flags = ReadGameStateExample("afterlife", "afterlife_global_flags.json");
        var flagEntries = GetRequiredArray(flags.RootElement, "flags").EnumerateArray().ToList();
        Assert.Contains(flagEntries, flag => string.Equals(GetString(flag, "visibility"), "visible", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(flagEntries, flag =>
            IsHiddenVisibility(GetString(flag, "visibility")) &&
            !flag.TryGetProperty("playerFacingSummary", out _) &&
            !flag.TryGetProperty("publicSummary", out _) &&
            !flag.TryGetProperty("playerHint", out _));

        using var storyOutline = ReadGameStateExample("afterlife", "afterlife_story_outline.json");
        Assert.True(HasNonEmptyArray(storyOutline.RootElement, "pendingRevelations"));
        Assert.True(HasNonEmptyArray(storyOutline.RootElement, "nextLikelySceneBeats"));
        Assert.True(HasNonEmptyString(storyOutline.RootElement, "playerAgencyNotes"));
        Assert.DoesNotContain(storyOutline.RootElement.EnumerateObject(), property =>
            property.Name.Equals("playerVisibleText", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Equals("publicSummary", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Equals("narrativeResponse", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Equals("interfaceText", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Equals("visibleToPlayer", StringComparison.OrdinalIgnoreCase));

        var profileDocuments = ReadEntityProfileExamples();
        try
        {
            var profiles = profileDocuments
                .SelectMany(document => GetRequiredArray(document.RootElement, "profiles").EnumerateArray())
                .ToList();

            Assert.Contains(profiles, profile =>
                profile.TryGetProperty("goals", out var goals) &&
                HasNonEmptyString(goals, "gmThoughtsSummary") &&
                profile.TryGetProperty("currentActivity", out var activity) &&
                HasNonEmptyString(activity, "gmThoughtsSummary"));

            var fateCards = profiles.SelectMany(profile => GetOptionalArray(profile, "fateCards")).ToList();
            Assert.Contains(fateCards, card =>
                string.Equals(GetString(card, "status"), "hidden", StringComparison.OrdinalIgnoreCase) ||
                IsBoolean(card, "isSecret", expected: true));
            Assert.Contains(fateCards, card =>
                !IsBoolean(card, "isSecret", expected: true) &&
                (string.Equals(GetString(card, "status"), "available", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(GetString(card, "status"), "unlocked", StringComparison.OrdinalIgnoreCase)));

            var masks = profiles.SelectMany(profile => GetOptionalArray(profile, "masks")).ToList();
            Assert.Contains(masks, mask =>
                IsBoolean(mask, "isRevealed", expected: false) &&
                HasNonEmptyString(mask, "concealedTruth") &&
                HasNonEmptyArray(mask, "directives"));

            var relationships = profiles.SelectMany(profile => GetOptionalArray(profile, "relationships")).ToList();
            Assert.Contains(relationships, relationship =>
                relationship.TryGetProperty("relationshipLock", out var relationshipLock) &&
                string.Equals(GetString(relationshipLock, "lockState"), "positive_locked", StringComparison.OrdinalIgnoreCase) &&
                HasNonEmptyArray(relationship, "relationshipGateQuests"));
        }
        finally
        {
            foreach (var document in profileDocuments)
                document.Dispose();
        }

        using var shiningFactionChronicles = ReadGameStateExample("shining_abode", "faction_chronicles.json");
        var factionEntries = GetRequiredArray(shiningFactionChronicles.RootElement, "factions").EnumerateArray().ToList();
        var shiningChronicleEntries = factionEntries.SelectMany(faction => GetOptionalArray(faction, "chronicle")).ToList();
        Assert.Contains(shiningChronicleEntries, entry => IsVisibleVisibility(GetString(entry, "visibility")));
        Assert.Contains(shiningChronicleEntries, entry => IsHiddenVisibility(GetString(entry, "visibility")));
        Assert.Contains(factionEntries, faction =>
            faction.TryGetProperty("strategicMemory", out var memory) &&
            HasNonEmptyString(memory, "summary") &&
            HasNonEmptyArray(memory, "enemies"));

        using var guardianPolitics = ReadGameStateExample("chaos_sea", "guardian_politics.json");
        var relations = GetRequiredArray(guardianPolitics.RootElement, "relations").EnumerateArray().ToList();
        Assert.Contains(relations, relation => IsVisibleVisibility(GetString(relation, "visibility")));
        Assert.Contains(relations, relation =>
            IsHiddenVisibility(GetString(relation, "visibility")) &&
            !IsBoolean(relation, "isPlayerVisible", expected: true));
        Assert.Contains(GetRequiredArray(guardianPolitics.RootElement, "sarefLinks").EnumerateArray(), link =>
            IsHiddenVisibility(GetString(link, "visibility")));
    }

    private static JsonDocument ReadGameStateExample(params string[] pathSegments)
    {
        var fullPath = Path.Combine(new[] { TestRepoPaths.BaseSessionRoot, "game_state" }.Concat(pathSegments).ToArray());
        Assert.True(File.Exists(fullPath), $"Missing FileSystemExample game_state example: {ToGameStateRelativePath(pathSegments)}");

        var document = JsonDocument.Parse(File.ReadAllText(fullPath));
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        return document;
    }

    private static List<JsonDocument> ReadEntityProfileExamples()
    {
        var directory = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "afterlife", "entity_profiles");
        Assert.True(Directory.Exists(directory), "Missing FileSystemExample game_state example directory: afterlife/entity_profiles");

        var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        Assert.True(files.Length >= 2, "Expected multiple afterlife/entity_profiles/*.json examples.");
        return files
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                var document = JsonDocument.Parse(File.ReadAllText(path));
                Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
                return document;
            })
            .ToList();
    }

    private static JsonElement GetRequiredArray(JsonElement root, string propertyName)
    {
        Assert.True(root.TryGetProperty(propertyName, out var array), $"Missing required array '{propertyName}'.");
        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        Assert.NotEmpty(array.EnumerateArray());
        return array;
    }

    private static IEnumerable<JsonElement> GetOptionalArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array))
            return Array.Empty<JsonElement>();

        Assert.Equal(JsonValueKind.Array, array.ValueKind);
        return array.EnumerateArray();
    }

    private static bool HasNonEmptyArray(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var array) &&
        array.ValueKind == JsonValueKind.Array &&
        array.EnumerateArray().Any();

    private static bool HasNonEmptyString(JsonElement root, string propertyName) =>
        !string.IsNullOrWhiteSpace(GetString(root, propertyName));

    private static string? GetString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool IsBoolean(JsonElement root, string propertyName, bool expected) =>
        root.TryGetProperty(propertyName, out var value) &&
        (expected ? value.ValueKind == JsonValueKind.True : value.ValueKind == JsonValueKind.False);

    private static bool IsVisibleVisibility(string? visibility) =>
        string.Equals(visibility, "visible", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(visibility, "known", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(visibility, "rumored", StringComparison.OrdinalIgnoreCase);

    private static bool IsHiddenVisibility(string? visibility) =>
        string.Equals(visibility, "hidden", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(visibility, "gm_only", StringComparison.OrdinalIgnoreCase);

    private static string ToGameStateRelativePath(IEnumerable<string> pathSegments) =>
        "game_state/" + string.Join('/', pathSegments);
}
