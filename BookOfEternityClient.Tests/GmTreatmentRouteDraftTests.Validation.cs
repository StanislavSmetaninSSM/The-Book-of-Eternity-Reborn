using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmTreatmentRouteDraftTests
{
    [Theory]
    [InlineData("invalid_mode", "author.route.mode", "wound_materialization_invalid_field")]
    [InlineData("unknown_member", "author.route.unregistered", "wound_materialization_unknown_field")]
    [InlineData("missing_member", "author.route.routeId", "wound_materialization_missing_field")]
    [InlineData("invalid_outcome", "author.route.outcomes[1].category", "wound_materialization_invalid_field")]
    [InlineData("fractional_scalar", "author.route.requirements[0].quantity", "wound_materialization_invalid_field")]
    [InlineData("overflow_scalar", "author.route.outcomes[0].result[0].points", "wound_materialization_invalid_field")]
    public void GmParser_RejectsInvalidModeMembersOutcomesAndScalars(
        string mutation,
        string path,
        string code)
    {
        var route = Route();
        Valid(Element(route));
        switch (mutation)
        {
            case "invalid_mode":
                route["mode"] = "invented";
                break;
            case "unknown_member":
                route["unregistered"] = true;
                break;
            case "missing_member":
                route.Remove("routeId");
                break;
            case "invalid_outcome":
                route["outcomes"]![1]!["category"] = "invented";
                break;
            case "fractional_scalar":
                route["requirements"]![0]!["quantity"] = 1.5;
                break;
            case "overflow_scalar":
                route["outcomes"]![0]!["result"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "add_recovery",
                    ["points"] = 2147483648L
                });
                break;
        }

        AssertRejected(route, path, code);
    }

    [Theory]
    [InlineData("requirements", 17, "author.route.requirements")]
    [InlineData("outcomes", 17, "author.route.outcomes")]
    [InlineData("outcomes/0/result", 9, "author.route.outcomes[0].result")]
    public void GmParser_RejectsCollectionLimits(
        string memberPath,
        int count,
        string expectedPath)
    {
        var route = Route();
        Valid(Element(route));
        var array = ValidationAt(route, memberPath).AsArray();
        var member = memberPath switch
        {
            "requirements" => array[0]!.DeepClone(),
            "outcomes" => array[0]!.DeepClone(),
            _ => new JsonObject { ["kind"] = "stabilize" }
        };
        array.Clear();
        for (var index = 0; index < count; index++)
            array.Add(member.DeepClone());

        AssertRejected(
            route,
            expectedPath,
            "wound_materialization_limit_exceeded");
    }

    [Theory]
    [InlineData(
        "graph_owner",
        "author.route.outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].root.ownership.complicationRef")]
    [InlineData(
        "component",
        "author.route.outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].definition.components[0].profile")]
    [InlineData(
        "parameter",
        "author.route.outcomes[0].result[0].legacies[1].effectDraft.applications[0].parameters")]
    [InlineData(
        "slot",
        "author.route.outcomes[3].result[0].complicationDraft.consequenceDefinitions[0].root.slots[0].profileKey")]
    public void GmParser_RejectsNestedGraphComponentParameterAndSlotFaults(
        string mutation,
        string expectedPath)
    {
        var route = mutation == "parameter" ? LegacyRoute() : EffectfulComplicationRoute();
        Valid(Element(route));
        switch (mutation)
        {
            case "graph_owner":
                ValidationAt(
                    route,
                    "outcomes/3/result/0/complicationDraft/consequenceDefinitions/0/root/ownership")!
                    ["complicationRef"] = "other_complication";
                break;
            case "component":
                ValidationAt(
                    route,
                    "outcomes/3/result/0/complicationDraft/consequenceDefinitions/0/definition/components/0")!
                    ["profile"] = "invented";
                break;
            case "parameter":
                ValidationAt(
                    route,
                    "outcomes/0/result/0/legacies/1/effectDraft/applications/0")!
                    ["parameters"] = new JsonArray();
                break;
            case "slot":
                ValidationAt(
                    route,
                    "outcomes/3/result/0/complicationDraft/consequenceDefinitions/0/root/slots/0")!
                    ["profileKey"] = "different_profile";
                break;
        }

        AssertRejected(
            route,
            expectedPath,
            "wound_materialization_invalid_field");
    }

    [Fact]
    public void Writer_RejectsRecognizedOperationWithInvalidScalarBeforeCallerBytes()
    {
        var route = Assert.IsType<GmProcedureRouteDraft>(Valid(Element(Route())));
        var invalid = route with
        {
            Bands = route.Bands.SetItem(0, route.Bands[0] with
            {
                DeclaredResult = [new GmAddRecoveryDraft(0)]
            })
        };
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        Assert.Throws<InvalidOperationException>(() =>
            MortalWoundTreatmentContract.WriteGmRouteDraft(writer, invalid));

        Assert.Equal(0, writer.BytesPending);
        Assert.Equal(0, stream.Length);
    }

    private static void AssertRejected(JsonObject route, string path, string code)
    {
        var parsed = MortalWoundTreatmentContract.ParseGmRouteDraftShape(
            Element(route),
            "author.route");
        Assert.False(parsed.IsValid);
        Assert.Null(parsed.Route);
        Assert.Contains(parsed.Issues, issue =>
            issue.FilePath == path && issue.Code == code);
    }

    private static JsonNode ValidationAt(JsonNode root, string path)
    {
        foreach (var segment in path.Split('/'))
        {
            root = root is JsonArray array
                ? array[int.Parse(segment)]!
                : root[segment]!;
        }
        return root;
    }
}
