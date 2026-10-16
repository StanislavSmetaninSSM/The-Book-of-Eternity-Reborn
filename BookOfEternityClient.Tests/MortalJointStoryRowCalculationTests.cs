using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalJointStoryRowCalculationTests
{
    private const string FrozenUtc = "2026-10-16T00:00:00.0000000Z";
    private const string Soul = "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":2}";
    private const string Narrative = "{\"response\":\"Первая\\\\nВторая\"}";
    private const string Location = "{\"currentLocationData\":{\"name\":\"Лес\"}}";
    private static readonly string[] RequiredReads =
        ["game_state/meta/soul_state.json", "output/narrative_response.json", "game_state/world/current_location.json"];

    // A changed serializer order/escaping, UTC conversion, missing normalization,
    // or append framing inside the calculator changes this literal owner row.
    [Fact]
    public void ExactRow_PreservesUnicodeFrozenUtcAndNormalizationWithoutAppendFraming()
    {
        var row = StoryService.CalculateMortalJointStoryRow(Inputs());
        Assert.Equal("stories/mortal_life_2.jsonl", row.Path);
        Assert.Equal("""{"requestId":"request_43","sessionGeneration":"G_001","turn":43,"timestamp":"2026-10-16T00:00:00.0000000Z","realm":"Mortal World","player":"Осмотреть","narrative":"Первая\nВторая","location":"Лес"}""", row.RowJson);
        Assert.Equal(RequiredReads, row.DerivedReadPaths);
        Assert.DoesNotContain('\uFEFF', row.RowJson);
        Assert.False(row.RowJson.EndsWith('\n'));
    }

    [Theory]
    [InlineData(0, "{}", "stories/mortal_life_1.jsonl", "Неизвестно")]
    [InlineData(5, "{\"name\":\"Площадь\"}", "stories/mortal_life_5.jsonl", "Площадь")]
    [InlineData(2, "{\"name\":\"outer\",\"currentLocationData\":{}}", "stories/mortal_life_2.jsonl", "Неизвестно")]
    public void ExistingIncarnationAndLocationFallbacks_AreUnchanged(int incarnation, string location, string path, string name)
    {
        var row = StoryService.CalculateMortalJointStoryRow(Inputs(incarnation: incarnation,
            soul: "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":" + incarnation + "}", location: location));
        Assert.Equal(path, row.Path);
        using var doc = JsonDocument.Parse(row.RowJson);
        Assert.Equal(name, doc.RootElement.GetProperty("location").GetString());
    }

    [Fact]
    public void EntityLabels_UseActualManifestationResidentAndCanonicalNpcPoliciesInOwnerOrder()
    {
        var row = StoryService.CalculateMortalJointStoryRow(Inputs(
            action: "[guardianId=G1 residentId=R1 NPCId=N1]",
            guardians: """{"activeGuardian":{"guardianId":"g1","canonicalName":"Канон","manifestation":{"currentDisplayName":"Лиса"}},"guardians":[{"guardianId":"G1","name":"Не выбирать"}]}""",
            residents: """{"entries":[{"residentId":"r1","displayName":"Житель"}]}""",
            npc: """{"UpdateNPCs":[{"NPCId":"n1","NPCName":"Торговец"}]}"""));
        using var doc = JsonDocument.Parse(row.RowJson);
        Assert.Equal("""[{"entityType":"guardian","entityId":"G1","displayName":"Лиса"},{"entityType":"resident","entityId":"R1","displayName":"Житель"},{"entityType":"npc","entityId":"N1","displayName":"Торговец"}]""",
            doc.RootElement.GetProperty("entityRefs").GetRawText());
        Assert.Equal(RequiredReads.Concat(["game_state/meta/guardians.json", "game_state/meta/guardian_abode_residents.json", "game_state/npcs/npc_core.json"]), row.DerivedReadPaths);
    }

    [Fact]
    public void ReferencedAbsentRoots_AreReadBindingsAndKeepIdsWithOmittedNullLabels()
    {
        var row = StoryService.CalculateMortalJointStoryRow(Inputs(action: "guardianId=g residentId=r npcId=n"));
        using var doc = JsonDocument.Parse(row.RowJson);
        Assert.Equal("""[{"entityType":"guardian","entityId":"g"},{"entityType":"resident","entityId":"r"},{"entityType":"npc","entityId":"n"}]""",
            doc.RootElement.GetProperty("entityRefs").GetRawText());
        Assert.Equal(6, row.DerivedReadPaths.Count);
    }

    [Fact]
    public void UnreferencedOptionalRoots_AreNeitherParsedNorReportedAsReads()
    {
        var row = StoryService.CalculateMortalJointStoryRow(Inputs(guardians: "malformed", residents: "[", npc: "null"));
        Assert.Equal(RequiredReads, row.DerivedReadPaths);
        using var doc = JsonDocument.Parse(row.RowJson);
        Assert.False(doc.RootElement.TryGetProperty("entityRefs", out _));
    }

    [Theory]
    [InlineData("guardianId=g", "malformed", null, null)]
    [InlineData("residentId=r", null, "[", null)]
    [InlineData("npcId=n", null, null, "null")]
    public void ReferencedMalformedRoots_RefuseInsteadOfDroppingLabels(string action, string? guardians, string? residents, string? npc)
    {
        void Calculate() => StoryService.CalculateMortalJointStoryRow(Inputs(action: action,
            guardians: guardians, residents: residents, npc: npc));
        if (npc == "null") Assert.Throws<InvalidDataException>(Calculate);
        else Assert.Throws<JsonException>(Calculate);
    }

    [Theory]
    [InlineData("sourceGuardianId=first guardianId=second", "first")]
    [InlineData("GUARDIANID=first,guardianId=second", "first")]
    [InlineData("guardianId= guardianId=second", null)]
    public void TagSelection_PreservesExistingFirstSubstringAndDelimiterPolicy(string action, string? id)
    {
        var row = StoryService.CalculateMortalJointStoryRow(Inputs(action: action));
        using var doc = JsonDocument.Parse(row.RowJson);
        if (id is null) Assert.False(doc.RootElement.TryGetProperty("entityRefs", out _));
        else Assert.Equal(id, doc.RootElement.GetProperty("entityRefs")[0].GetProperty("entityId").GetString());
    }

    [Theory]
    [InlineData("{\"currentRealm\":\"Chaos Sea\",\"currentIncarnation\":2}")]
    [InlineData("{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":3}")]
    [InlineData("{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":-1}")]
    public void CurrentSoul_DoesNotReplaceOriginalRealmOrIncarnation(string soul)
    {
        Assert.Throws<InvalidDataException>(() => StoryService.CalculateMortalJointStoryRow(Inputs(soul: soul)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"response\":\" \\n\"}")]
    [InlineData("{\"response\":\"one\",\"response\":\"two\"}")]
    public void Narrative_RequiresNonemptyUniqueActualResponse(string narrative)
    {
        Assert.Throws<InvalidDataException>(() => StoryService.CalculateMortalJointStoryRow(Inputs(narrative: narrative)));
    }

    [Fact]
    public void Inputs_CopyAllNamedRootsAndResultHasNoMutableEntryOrReadList()
    {
        var soul = Bytes(Soul); var narrative = Bytes(Narrative); var location = Bytes(Location);
        var guardians = Bytes("""{"guardians":[{"guardianId":"g","name":"Страж"}]}""");
        var residents = Bytes("""{"entries":[{"residentId":"r","displayName":"Житель"}]}""");
        var npc = Bytes("""{"UpdateNPCs":[{"NPCId":"n","NPCName":"Торговец"}]}""");
        var input = new StoryService.JointStoryRowInputs("G_001", "request_43", 43, "Mortal World", 2,
            "guardianId=g residentId=r npcId=n", FrozenUtc, soul, narrative, location, guardians, residents, npc);
        foreach (var bytes in new[] { soul, narrative, location, guardians, residents, npc }) Array.Fill(bytes, (byte)'!');
        var row = StoryService.CalculateMortalJointStoryRow(input);
        Assert.Contains("Страж", row.RowJson); Assert.Contains("Житель", row.RowJson); Assert.Contains("Торговец", row.RowJson);
        var reads = Assert.IsAssignableFrom<IList<string>>(row.DerivedReadPaths);
        Assert.Throws<NotSupportedException>(() => reads[0] = "caller-replacement");
        Assert.Equal(row.RowJson, StoryService.CalculateMortalJointStoryRow(input).RowJson);
    }

    private static StoryService.JointStoryRowInputs Inputs(string action = "Осмотреть", int incarnation = 2,
        string soul = Soul, string narrative = Narrative, string location = Location,
        string? guardians = null, string? residents = null, string? npc = null) =>
        new("G_001", "request_43", 43, "Mortal World", incarnation, action, FrozenUtc,
            Bytes(soul), Bytes(narrative), Bytes(location), guardians is null ? null : Bytes(guardians),
            residents is null ? null : Bytes(residents), npc is null ? null : Bytes(npc));
    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);
}
