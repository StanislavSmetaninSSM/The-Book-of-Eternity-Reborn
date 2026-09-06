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
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    public async Task AcceptedTransitionCommand_ValidationExplicitlyRejectsUnsupportedAdapter(string variant)
    {
        await using var context = await CreatePlayerContextAsync();
        var (localBinding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var binding = localBinding with { SessionId = SessionId, RequestId = RequestId,
            SnapshotToken = await WoundMaterializationValidationTests.ReadSnapshotTokenAsync(context) };
        var root = WoundResponseInputComposer.ComposeAcceptedTransitionCommandRoot(binding, request, "Accepted scene");
        var paths = WoundMaterializationValidationTests.SnapshotWoundPaths
            .Append(AcceptedMechanicsPlan.WoundCommandPath).Distinct().ToArray();
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath, root.ToJsonString());
        var before = new Dictionary<string, byte[]?>();
        foreach (var path in paths) before[path] = await ReadAcceptedTransitionBytesAsync(context, path);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
            Assert.Contains(issues, issue => issue.Code == "wound_command_transition_adapter_unavailable");
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(context.FileSystem, lease, out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _, out _));
        }
        foreach (var path in paths) Assert.Equal(before[path], await ReadAcceptedTransitionBytesAsync(context, path));
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    [InlineData("author_alternative_treatment_hidden")]
    public async Task AcceptedTransitionCommand_ValidationAndDistributionRejectWithoutWrites(string variant)
    {
        await using var context = await CreatePlayerContextAsync();
        var (localBinding, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var binding = localBinding with { SessionId = SessionId, RequestId = RequestId,
            SnapshotToken = await WoundMaterializationValidationTests.ReadSnapshotTokenAsync(context) };
        const string scene = "The examination is complete.";
        var root = WoundResponseInputComposer.ComposeAcceptedTransitionCommandRoot(binding, request, scene);
        var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(root));
        Assert.True(parsed.Success, Describe(parsed.Issues));
        Assert.Single(parsed.AcceptedTransitionCommands);
        var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(binding, parsed,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(recomposed.Success, Describe(recomposed.Issues));
        var paths = WoundMaterializationValidationTests.SnapshotWoundPaths
            .Append(AcceptedMechanicsPlan.WoundCommandPath)
            .Append(WoundMaterializationTestContext.NarrativeOutputPath).Distinct().ToArray();
        var before = new Dictionary<string, byte[]?>();
        foreach (var path in paths)
            before[path] = await ReadAcceptedTransitionBytesAsync(context, path);
        var distributor = new StateDistributor(context.FileSystem, NullLogger<StateDistributor>.Instance);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            distributor.DistributeAsync(new GameResponse { Response = scene }, recomposed));
        Assert.Contains("wound_command_transition_adapter_unavailable", exception.Message);
        foreach (var path in paths)
            Assert.Equal(before[path], await ReadAcceptedTransitionBytesAsync(context, path));

        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath, root.ToJsonString());
        before[AcceptedMechanicsPlan.WoundCommandPath] = await ReadAcceptedTransitionBytesAsync(
            context, AcceptedMechanicsPlan.WoundCommandPath);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
            Assert.Contains(issues, issue => issue.Code == "wound_command_transition_adapter_unavailable");
            Assert.False(WoundAcceptedTurnPlanAuthority.TryPeekPrepared(context.FileSystem, lease, out _));
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _, out _));
        }
        foreach (var path in paths)
            Assert.Equal(before[path], await ReadAcceptedTransitionBytesAsync(context, path));
    }

    [Theory]
    [InlineData("diagnose")]
    [InlineData("author_alternative_treatment")]
    public async Task AcceptedTransitionCommand_RetainsMixedFamiliesButNeverConsumesThem(string variant)
    {
        await using var context = await CreatePlayerContextAsync();
        var (_, request) = WoundAcceptedTransitionCommandTestData.Create(variant);
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2, turn: 43,
            eventRef: request.EventRef);
        var response = Response(Decision("none", proposal: null));
        var opportunity = WoundResponseInputComposer.Compose(authority.Binding, new[] { authority.Opportunity },
            response.WoundDecisions, response.Response, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(opportunity.Success, Describe(opportunity.Issues));
        var root = WoundResponseInputComposer.ComposeAcceptedTransitionCommandRoot(authority.Binding, request, response.Response!);
        root["commands"]!.AsArray().Add(opportunity.CommandRoot!["commands"]![0]!.DeepClone());
        var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(root));
        Assert.True(parsed.Success, Describe(parsed.Issues));
        Assert.Single(parsed.AcceptedTransitionCommands);
        Assert.Single(parsed.Commands);
        var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(authority.Binding, parsed,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.False(recomposed.Success);
        Assert.Contains(recomposed.Issues, issue => issue.Code == "wound_command_mixed_transition_kinds");
        var history = WoundHistoryState.Parse("{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}", WoundHistoryState.HistoryPath);
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(root), null, history);
        Assert.False(catalog.IsValid);
        Assert.Contains(catalog.Issues, issue => issue.Code == "wound_command_transition_adapter_unavailable");
    }

    private static async Task<byte[]?> ReadAcceptedTransitionBytesAsync(
        ResourceMaterializationTestContext context, string path)
    {
        var absolutePath = context.FileSystem.ResolvePath(path);
        return File.Exists(absolutePath) ? await File.ReadAllBytesAsync(absolutePath) : null;
    }
}
