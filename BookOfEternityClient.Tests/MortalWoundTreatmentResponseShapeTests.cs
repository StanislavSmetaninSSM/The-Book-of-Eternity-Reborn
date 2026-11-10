using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalWoundTreatmentResponseShapeTests
{
    [Theory]
    [InlineData(null, JsonValueKind.Undefined)]
    [InlineData("null", JsonValueKind.Null)]
    [InlineData("{}", JsonValueKind.Object)]
    [InlineData("[]", JsonValueKind.Array)]
    [InlineData("\"memory\"", JsonValueKind.String)]
    [InlineData("0", JsonValueKind.Number)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("false", JsonValueKind.False)]
    public void CompletePublicationResponseShapeDistinguishesOmissionFromEveryDefinedForeignValue(string? value, JsonValueKind kind)
    {
        var response = value is null ? new GameResponse() : JsonSerializer.Deserialize<GameResponse>(
            "{\"spiritualShiningPatientMemory\":" + value + "}")!;
        Assert.Equal(kind, response.SpiritualShiningPatientMemory.ValueKind);
        var original = JsonSerializer.Serialize(response);
        var wire = Assert.IsType<JsonObject>(JsonNode.Parse(original));
        Assert.Equal(value is not null, wire.ContainsKey("spiritualShiningPatientMemory"));
        if (value is not null)
            Assert.Equal(value, response.SpiritualShiningPatientMemory.GetRawText());

        var issues = WoundAcceptedTurnPlanner.ValidateMortalTreatmentResponseShape(response);

        if (kind == JsonValueKind.Undefined)
        {
            Assert.Empty(issues);
            var omitted = Assert.IsType<GameResponse>(JsonSerializer.Deserialize<GameResponse>("{}"));
            Assert.Equal(JsonValueKind.Undefined, omitted.SpiritualShiningPatientMemory.ValueKind);
            Assert.Empty(WoundAcceptedTurnPlanner.ValidateMortalTreatmentResponseShape(omitted));
            Assert.False(Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(omitted)).ContainsKey("spiritualShiningPatientMemory"));
            Assert.Equal(original, JsonSerializer.Serialize(omitted));
        }
        else
        {
            var issue = Assert.Single(issues);
            Assert.Equal("mortal_wound_treatment_publication_response_unsupported", issue.Code);
            Assert.Equal("treatmentPublication.response.SpiritualShiningPatientMemory", issue.FilePath);
            Assert.Equal(nameof(GameResponse.SpiritualShiningPatientMemory), issue.Actual);
            Assert.Equal(IssueSeverity.Error, issue.Severity);
        }
        Assert.Equal(original, JsonSerializer.Serialize(response));
        Assert.Equal(kind, response.SpiritualShiningPatientMemory.ValueKind);
    }

    [Fact]
    public void CompletePublicationResponseShapePreservesAllSupportedFieldsAndExactLegacyFateFlag()
    {
        var response = new GameResponse
        {
            ActiveSkillChanges = Array.Empty<JsonElement>(), RemoveActiveSkills = Array.Empty<string>(),
            PassiveSkillChanges = Array.Empty<JsonElement>(), RemovePassiveSkills = Array.Empty<string>(),
            NPCActiveSkillChanges = Array.Empty<JsonElement>(), NPCPassiveSkillChanges = Array.Empty<JsonElement>(),
            UpdateInventory = Array.Empty<JsonElement>(), MoveInventoryItems = Array.Empty<JsonElement>(),
            RemoveInventoryItems = Array.Empty<JsonElement>(), NPCInventoryAdds = Array.Empty<JsonElement>(),
            NPCInventoryUpdates = Array.Empty<JsonElement>(), NPCInventoryRemovals = Array.Empty<JsonElement>(),
            NPCEquipmentChanges = Array.Empty<JsonElement>()
        };
        var original = JsonSerializer.Serialize(response);
        Assert.Empty(WoundAcceptedTurnPlanner.ValidateMortalTreatmentResponseShape(response));
        Assert.Equal(original, JsonSerializer.Serialize(response));

        response.EffectEventReports = Array.Empty<JsonElement>();
        var fate = Assert.Single(WoundAcceptedTurnPlanner.ValidateMortalTreatmentResponseShape(response));
        Assert.Equal("treatmentPublication.response.EffectEventReports", fate.FilePath);
        Assert.Empty(WoundAcceptedTurnPlanner.ValidateMortalTreatmentResponseShape(response, ignoreLegacyFateDuplicate: true));

        response.WeatherChange = JsonSerializer.SerializeToElement(new { weather = "rain" });
        var withForeign = JsonSerializer.Serialize(response);
        var unrelated = Assert.Single(WoundAcceptedTurnPlanner.ValidateMortalTreatmentResponseShape(response, ignoreLegacyFateDuplicate: true));
        Assert.Equal("treatmentPublication.response.WeatherChange", unrelated.FilePath);
        Assert.Equal("mortal_wound_treatment_publication_response_unsupported", unrelated.Code);
        Assert.Equal(2, WoundAcceptedTurnPlanner.ValidateMortalTreatmentResponseShape(response).Count);
        Assert.Equal(withForeign, JsonSerializer.Serialize(response));
    }
}
