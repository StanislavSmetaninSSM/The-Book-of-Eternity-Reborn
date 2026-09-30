using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    [Theory]
    [InlineData("author")]
    [InlineData("decline")]
    public async Task AlternativeResponse_RawInputAloneRejectsBeforeAnyWrite(string decision)
    {
        await using var context = await CreatePlayerContextAsync();
        var sentinels = await SeedAlternativeResponseSentinelsAsync(context);
        var response = new GameResponse
        {
            Response = "Эта сцена не должна быть опубликована.",
            WoundTreatmentAuthorings = [AlternativeAuthoringElement(decision)]
        };
        var distributor = new StateDistributor(
            context.FileSystem,
            NullLogger<StateDistributor>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => distributor.DistributeAsync(response));

        Assert.Contains("wound_authorings_require_accepted_adapter", exception.Message);
        await AssertAlternativeResponseSentinelsUnchangedAsync(context, sentinels);
        Assert.False(context.FileSystem.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Empty(Directory.GetFiles(context.RootPath, "*.backup.*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("author")]
    [InlineData("decline")]
    public async Task AlternativeResponse_RawInputBesideAcceptedOpportunityRejectsBeforeAnyWrite(
        string decision)
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var response = Response(Decision("none", proposal: null));
        var accepted = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(accepted.Success, Describe(accepted.Issues));
        response.WoundTreatmentAuthorings = [AlternativeAuthoringElement(decision)];
        var sentinels = await SeedAlternativeResponseSentinelsAsync(context);
        var distributor = new StateDistributor(
            context.FileSystem,
            NullLogger<StateDistributor>.Instance);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => distributor.DistributeAsync(response, accepted));

        Assert.Contains("wound_authorings_require_accepted_adapter", exception.Message);
        await AssertAlternativeResponseSentinelsUnchangedAsync(context, sentinels);
        Assert.False(context.FileSystem.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Empty(Directory.GetFiles(context.RootPath, "*.backup.*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlternativeResponse_NullOrEmptyPreservesNoInputBehavior(bool empty)
    {
        await using var context = await CreatePlayerContextAsync();
        var response = new GameResponse
        {
            WoundTreatmentAuthorings = empty ? Array.Empty<JsonElement>() : null
        };
        var distributor = new StateDistributor(
            context.FileSystem,
            NullLogger<StateDistributor>.Instance);

        var modified = await distributor.DistributeAsync(response);

        Assert.Empty(modified);
        Assert.False(context.FileSystem.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(context.FileSystem.FileExists(WoundMaterializationTestContext.NarrativeOutputPath));
        Assert.Empty(Directory.GetFiles(context.RootPath, "*.backup.*", SearchOption.AllDirectories));
    }

    private static JsonElement AlternativeAuthoringElement(string decision)
    {
        JsonObject entry;
        if (decision == "decline")
        {
            entry = new JsonObject
            {
                ["authoringRequestRef"] = "request_decline",
                ["decision"] = "decline",
                ["route"] = null,
                ["diagnosisPath"] = null
            };
        }
        else
        {
            var route = WoundContractTestData.CreateActiveWound()["treatment"]!["routes"]![0]!
                .DeepClone().AsObject();
            entry = new JsonObject
            {
                ["authoringRequestRef"] = "request_author",
                ["decision"] = "author",
                ["route"] = route,
                ["diagnosisPath"] = null
            };
        }
        return JsonSerializer.SerializeToElement(entry);
    }

    private static async Task<IReadOnlyDictionary<string, byte[]>> SeedAlternativeResponseSentinelsAsync(
        ResourceMaterializationTestContext context)
    {
        var values = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [WoundCarrierCatalog.PlayerPath] = Encoding.UTF8.GetBytes(
                "{\"sentinel\":\"canonical-before\"}"),
            [WoundMaterializationTestContext.NarrativeOutputPath] = Encoding.UTF8.GetBytes(
                "{\"sentinel\":\"output-before\"}")
        };
        foreach (var (path, bytes) in values)
            await context.WriteExactBytesAsync(path, bytes);
        return values;
    }

    private static async Task AssertAlternativeResponseSentinelsUnchangedAsync(
        ResourceMaterializationTestContext context,
        IReadOnlyDictionary<string, byte[]> expected)
    {
        foreach (var (path, bytes) in expected)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(path));
    }
}
