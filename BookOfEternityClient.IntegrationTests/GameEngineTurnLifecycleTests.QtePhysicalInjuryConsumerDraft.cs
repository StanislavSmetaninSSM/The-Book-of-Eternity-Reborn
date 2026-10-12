using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("valid_none")]
    [InlineData("missing")]
    [InlineData("json_null")]
    [InlineData("duplicate")]
    [InlineData("case_alias")]
    [InlineData("wrong_case")]
    [InlineData("unknown")]
    [InlineData("foreign")]
    [InlineData("receipt_null")]
    [InlineData("history_null")]
    [InlineData("missing_candidate")]
    [InlineData("event_order")]
    [InlineData("proposal_null")]
    public async Task QtePhysicalInjuryConsumer_ComponentDraftRequiresExactSignedOfferAndObservedAbsence(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var source = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2);
        Assert.Null(source.Failure);
        Assert.True(source.ActionObserved);
        await AssertActualQteStoryFinalizedAsync(_fs, source.Source!, source.StoryAtHandoff!, 155m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(source, 2);
        Assert.False(_fs.FileExists(MortalWoundOpportunityReceiptState.StatePath));
        Assert.False(_fs.FileExists(WoundHistoryState.HistoryPath));
        if (mutation == "receipt_null")
            await _fs.WriteFileAtomicAsync(MortalWoundOpportunityReceiptState.StatePath, "null");
        if (mutation == "history_null")
            await _fs.WriteFileAtomicAsync(WoundHistoryState.HistoryPath, "null");
        if (mutation is "missing_candidate" or "event_order")
        {
            var root = JsonNode.Parse((await _fs.ReadFileAsync(MortalWoundOccurrenceState.StatePath))!)!.AsObject();
            var row = Assert.Single(root["occurrences"]!.AsArray())!.AsObject();
            if (mutation == "missing_candidate") row["producerCandidateCount"] = 2;
            else row["acceptedEvents"] = new JsonArray(row["acceptedEvents"]!.AsArray().Reverse().Select(value => value!.DeepClone()).ToArray());
            await _fs.WriteFileAtomicAsync(MortalWoundOccurrenceState.StatePath, root.ToJsonString());
        }

        // Component admission control. The production baseline writer observes the real
        // finalized occurrence and any deliberately corrupt root; no source or hash is authored.
        var request = new TurnRequest
        {
            SessionId = source.Source!.SessionId, RequestId = Guid.NewGuid().ToString("N"), TurnNumber = 43,
            Timestamp = DateTime.UtcNow.ToString("O"), PlayerAction = "Осмотреть ушиб.",
            PreGeneratedDices1d20 = [3, 17], ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        await InvokePrivateTaskAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, QteSceneService.OrdinaryPlayerTurnSourceLabel);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var reference = JsonSerializer.Serialize(occurrence.OpportunityRef);
        string? raw = mutation switch
        {
            "missing" => null,
            "json_null" => "null",
            "duplicate" => "{\"opportunityRef\":" + reference + ",\"decision\":\"none\",\"decision\":\"none\"}",
            "case_alias" => "{\"opportunityRef\":" + reference + ",\"decision\":\"none\",\"Decision\":\"none\"}",
            "wrong_case" => "{\"opportunityRef\":" + reference + ",\"Decision\":\"none\"}",
            "unknown" => "{\"opportunityRef\":" + reference + ",\"decision\":\"none\",\"owner\":\"player\"}",
            "foreign" => "{\"opportunityRef\":\"foreign_opportunity\",\"decision\":\"none\"}",
            "proposal_null" => "{\"opportunityRef\":" + reference + ",\"decision\":\"materialize\",\"woundRef\":\"impact\",\"proposal\":null}",
            _ => "{\"opportunityRef\":" + reference + ",\"decision\":\"none\"}"
        };
        var before = BrowserRecoveryTree();
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            var read = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, lease);
            Assert.Equal(request.RequestId, read.RequestId);
            Assert.Equal(43, read.TurnNumber);
            foreach (var path in new[] { MortalWoundOpportunityReceiptState.StatePath, WoundHistoryState.HistoryPath })
            {
                var coveredNull = (mutation == "receipt_null" && path == MortalWoundOpportunityReceiptState.StatePath) ||
                    (mutation == "history_null" && path == WoundHistoryState.HistoryPath);
                Assert.Equal(!coveredNull, read.AbsentLogicalPaths.Contains(path));
                if (coveredNull) Assert.Equal("null", Encoding.UTF8.GetString(read.ReadRequiredBytes(path)).TrimStart('\uFEFF'));
            }
            var result = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(_fs, lease, raw, "Герой осматривает ушиб.");
            Assert.NotNull(result);
            if (mutation == "valid_none")
            {
                Assert.True(result.Success, JsonSerializer.Serialize(result.Issues));
                Assert.NotNull(result.CommandRoot);
                Assert.Empty(result.Transitions);
                Assert.Empty(result.MaterializedOpportunities);
                Assert.Single(result.DecisionReceipts);
            }
            else
            {
                Assert.False(result.Success);
                Assert.Null(result.CommandRoot);
                Assert.NotEmpty(result.Issues);
            }
            WritePhysicalInjuryDiagnostic("qte_consumer_component_draft_admission", new
            { mutation, read.SessionId, read.RequestId, read.SnapshotToken, read.TurnNumber, read.AbsentLogicalPaths,
                RawDraft = raw, result.Success, result.CommandRoot, result.Issues });
        }
        AssertBrowserRecoveryTree(before);
    }
}
