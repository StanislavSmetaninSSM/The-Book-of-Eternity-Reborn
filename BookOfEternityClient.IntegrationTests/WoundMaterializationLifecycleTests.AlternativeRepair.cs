using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AlternativeRepair_CaptureAndExactRetryRefuseWholeWaveWithoutWrites(bool mixed)
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var proposal = CreateRepairRoundtripProposal("severity");
        var response = Response(Decision("materialize", proposal));
        var composed = WoundResponseInputComposer.Compose(authority.Binding, new[] { authority.Opportunity },
            response.WoundDecisions, response.Response, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        var commandRoot = Assert.IsType<JsonObject>(composed.CommandRoot);
        var rejectedDecision = commandRoot["commands"]![0]!["decision"]!.DeepClone().AsObject();
        rejectedDecision["proposal"]!["severity"] = "III";
        commandRoot["commands"]![0]!["decision"] = rejectedDecision.DeepClone();
        var rejected = WoundResponseInputComposer.Compose(authority.Binding, new[] { authority.Opportunity },
            new[] { JsonSerializer.SerializeToElement(rejectedDecision) }, response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        var supported = Assert.Single(WoundRepairPacketBuilder.Build(rejected.Issues));
        var alternative = CreateAlternativeRepairPacket(authority.Binding);
        var packets = mixed ? new[] { supported, alternative } : new[] { alternative };
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath, commandRoot.ToJsonString());
        var bytes = await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath);
        var sentinels = await SeedAlternativeResponseSentinelsAsync(context);
        using var fixture = new GameEngineTurnLifecycleTests();
        var engine = typeof(GameEngineTurnLifecycleTests).GetMethod("CreateGameEngine", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(fixture, new object?[] { null, null, null, context.FileSystem })!;
        var capture = typeof(GameEngine).GetMethod("CaptureWoundRepairRetryObligationsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var captureTask = (Task)capture.Invoke(engine, new object[] { packets })!;
        await captureTask;
        Assert.Empty(((System.Collections.IEnumerable)captureTask.GetType().GetProperty("Result")!.GetValue(captureTask)!).Cast<object>());
        var obligationType = typeof(GameEngine).GetNestedType("WoundRepairRetryObligation", BindingFlags.NonPublic)!;
        var obligations = Array.CreateInstance(obligationType, packets.Length);
        for (var index = 0; index < packets.Length; index++)
            obligations.SetValue(Activator.CreateInstance(obligationType, new object[] { index, packets[index], commandRoot.DeepClone().AsObject() }), index);
        var retry = typeof(GameEngine).GetMethod("HasExactWoundRepairResubmissionAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.False(await (Task<bool>)retry.Invoke(engine, new object[] { obligations })!);
        Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        await AssertAlternativeResponseSentinelsUnchangedAsync(context, sentinels);
        Assert.False(context.FileSystem.FileExists("game_state/control/validation_repair_request.json"));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task AlternativeRepair_PublicOnlyWaveCannotComposeOrResumeLivePending(bool mixed, bool composeGate)
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateAuthorityAsync(context, maximumSeverityRank: 2);
        var response = Response(Decision("none", proposal: null));
        var accepted = WoundResponseInputComposer.Compose(authority.Binding, new[] { authority.Opportunity },
            response.WoundDecisions, response.Response, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(accepted.Success, Describe(accepted.Issues));
        var parsedCommand = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(accepted.CommandRoot));
        Assert.True(parsedCommand.Success, Describe(parsedCommand.Issues));
        var packets = new List<WoundRepairPacket> { CreateAlternativeRepairPacket(authority.Binding) };
        if (mixed)
        {
            var badProposal = CreateRepairRoundtripProposal("severity");
            badProposal["severity"] = "III";
            var rejected = WoundResponseInputComposer.Compose(authority.Binding, new[] { authority.Opportunity },
                Response(Decision("materialize", badProposal)).WoundDecisions, response.Response,
                Array.Empty<WoundOpportunityDecisionReceipt>());
            packets.Add(Assert.Single(WoundRepairPacketBuilder.Build(rejected.Issues)));
        }
        var publicPackets = new JsonArray(packets.Select(packet => (JsonNode?)packet.ToJsonObject()).ToArray());
        var receipts = JsonSerializer.SerializeToNode(packets.Select(packet => packet.CreateReceipt()),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsArray();
        Assert.True(WoundRepairPacketBuilder.IsValidPersistedRepairWave(JsonSerializer.SerializeToElement(publicPackets),
            JsonSerializer.SerializeToElement(receipts), authority.Binding.SessionId, authority.Binding.RequestId,
            authority.Binding.SnapshotToken));
        var sentinels = await SeedAlternativeResponseSentinelsAsync(context);
        if (composeGate)
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                WoundRepairPacketBuilder.ComposePendingRoot(authority.Binding, packets, parsedCommand));
            Assert.Contains("wound_repair_alternative_adapter_unavailable", exception.Message);
            await AssertAlternativeResponseSentinelsUnchangedAsync(context, sentinels);
            Assert.False(context.FileSystem.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
            return;
        }
        var pending = new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = authority.Binding.SessionId,
            ["requestId"] = authority.Binding.RequestId, ["snapshotToken"] = authority.Binding.SnapshotToken,
            ["repairPackets"] = publicPackets, ["repairReceipts"] = receipts
        };
        await context.WriteExactJsonAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath, pending.ToJsonString());
        var readback = await context.FileSystem.ReadFileAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        using var document = JsonDocument.Parse(readback!);
        var history = WoundHistoryState.Parse("{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}", WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, Describe(history.Issues));
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(null, document.RootElement, history);
        Assert.False(catalog.IsValid);
        Assert.Contains(catalog.Issues, issue => issue.Code == "mortal_wound_treatment_persisted_pending_invalid" &&
            issue.Expected == "private alternative repair authority");
        Assert.Empty(catalog.Requests);
        Assert.Empty(catalog.HeldRequests);
        await AssertAlternativeResponseSentinelsUnchangedAsync(context, sentinels);
        Assert.False(context.FileSystem.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(context.FileSystem.FileExists("game_state/control/validation_repair_request.json"));
    }

    private static WoundRepairPacket CreateAlternativeRepairPacket(WoundAcceptedTurnBinding binding)
    {
        var original = JsonNode.Parse(AlternativeAuthoringElement("author").GetRawText())!.AsObject();
        original["route"]!["mode"] = "unsupported_mode";
        var parsed = WoundResponseInputComposer.ParseAlternativeTreatmentAuthoring(
            JsonSerializer.SerializeToElement(original), "woundTreatmentAuthorings[0]");
        return Assert.Single(WoundRepairPacketBuilder.Build(new WoundRepairBuildRequest(binding.SessionId,
            binding.RequestId, binding.SnapshotToken, new[] { new WoundRepairCandidateInput(
                "author_alternative_treatment", "alternative_repair_local_001", "sha256:" + new string('b', 64),
                "request_author", new JsonObject { ["event"] = "Прочитанная запись", ["target"] = "игрок", ["realm"] = "Смертный мир" },
                new[] { "decline", "author" }, "I", "IV", original, parsed.Issues) })));
    }
}
