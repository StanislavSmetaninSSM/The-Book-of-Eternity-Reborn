using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ShiningCurrentHallContractTests
{
    [Theory]
    [InlineData("valid")] [InlineData("absent")] [InlineData("sealed")]
    [InlineData("null")] [InlineData("number")] [InlineData("whitespace")]
    [InlineData("unknown")] [InlineData("case")] [InlineData("duplicate_hall")]
    [InlineData("hall_alias")] [InlineData("key_case")] [InlineData("alias")] [InlineData("no_halls")]
    public void OptionalCoordinateIsExactAndNeverInvented(string change)
    {
        var root = Root();
        switch (change)
        {
            case "absent": root.Remove(ShiningCurrentHallContract.Property); break;
            case "sealed": root["availability"] = ShiningAbodeState.AvailabilitySealedUntilNextAscension; break;
            case "null": root[ShiningCurrentHallContract.Property] = null; break;
            case "number": root[ShiningCurrentHallContract.Property] = 1; break;
            case "whitespace": root[ShiningCurrentHallContract.Property] = " hall_current "; break;
            case "unknown": root[ShiningCurrentHallContract.Property] = "hall_unknown"; break;
            case "case": root[ShiningCurrentHallContract.Property] = "HALL_CURRENT"; break;
            case "duplicate_hall": root["halls"]!.AsArray().Add(root["halls"]![0]!.DeepClone()); break;
            case "hall_alias": root["halls"]!.AsArray().Add(new JsonObject { ["hallId"] = "HALL_CURRENT" }); break;
            case "key_case": root.Remove(ShiningCurrentHallContract.Property); root["CurrentHallId"] = "hall_current"; break;
            case "alias": root["selectedHallId"] = "hall_current"; break;
            case "no_halls": root.Remove("halls"); break;
        }
        var before = root.ToJsonString(); var valid = change is "valid" or "absent" or "sealed";
        Assert.Equal(valid, ShiningCurrentHallContract.Validate(root) is null);
        using var document = JsonDocument.Parse(before);
        Assert.Equal(valid, ShiningCurrentHallContract.Validate(document.RootElement) is null);
        Assert.Equal(before, root.ToJsonString());
    }

    [Fact]
    public void DuplicateCoordinateKeysCannotHideInAJsonDocument()
    {
        using var document = JsonDocument.Parse("{\"currentHallId\":\"hall_current\",\"currentHallId\":\"hall_current\",\"halls\":[{\"hallId\":\"hall_current\"}]}");
        Assert.NotNull(ShiningCurrentHallContract.Validate(document.RootElement));
    }

    [Fact]
    public void NormalizationPreservesKnownAndMissingCoordinatesWithoutSelectingAHall()
    {
        var root = Root(); ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null);
        Assert.Equal("hall_current", root[ShiningCurrentHallContract.Property]!.GetValue<string>());
        root.Remove(ShiningCurrentHallContract.Property);
        ShiningAbodeState.NormalizeStateRoot(root, residentRoot: null);
        Assert.False(root.ContainsKey(ShiningCurrentHallContract.Property));
    }

    [Theory]
    [InlineData("same")] [InlineData("both_absent")] [InlineData("added")]
    [InlineData("removed")] [InlineData("moved")]
    public void PreservationIncludesPresenceAndExactValue(string change)
    {
        var before = Root(); var after = Root();
        if (change is "both_absent" or "added") before.Remove(ShiningCurrentHallContract.Property);
        if (change is "both_absent" or "removed") after.Remove(ShiningCurrentHallContract.Property);
        if (change == "moved") after[ShiningCurrentHallContract.Property] = "hall_other";
        Assert.Equal(change is "same" or "both_absent", ShiningCurrentHallContract.Preserves(before, after));
    }

    [Fact]
    public void BothProgressionProtectionPathsRejectAnotherOtherwiseValidHall()
    {
        var before = Root(); var after = Root(); after[ShiningCurrentHallContract.Property] = "hall_other";
        Assert.Null(ShiningCurrentHallContract.Validate(after));
        var closure = new List<ValidationIssue>(); var capstone = new List<ValidationIssue>();
        ValidationService.ValidateShiningClosureForbiddenProgressionDeltas(before, after, closure);
        ValidationService.ValidateSourceOfLightForbiddenShiningProgressionDeltas(before, after, capstone);
        Assert.Contains(closure, i => i.FilePath == ShiningAbodeState.StatePath + ".currentHallId");
        Assert.Contains(capstone, i => i.FilePath == ShiningAbodeState.StatePath + ".currentHallId");
    }

    private static JsonObject Root()
    {
        var root = ShiningAbodeState.CreateDefaultState();
        root[ShiningCurrentHallContract.Property] = "hall_current";
        root["halls"] = new JsonArray(Hall("hall_current"), Hall("hall_other"));
        return root;
    }

    private static JsonObject Hall(string id) => new()
    { ["hallId"] = id, ["hallName"] = id, ["description"] = "Зал фикстуры.", ["serviceTags"] = new JsonArray() };
}
