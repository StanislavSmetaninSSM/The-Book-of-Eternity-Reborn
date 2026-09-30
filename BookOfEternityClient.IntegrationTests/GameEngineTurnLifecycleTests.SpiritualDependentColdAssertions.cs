using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Proves initial and cold worker tasks traverse apply and Ready exactly once without requesting another materialization.
    /// </summary>
    /// <param name="context">
    /// Session retaining both real task packets, proposals and audit events.
    /// </param>
    /// <param name="initial">
    /// Actual original decision request observed before interruption.
    /// </param>
    /// <param name="dependent">
    /// Actual cold dependent request reconstructed from the durable selected choice.
    /// </param>
    /// <returns>
    /// A task completing after both separately correlated proposals and their allowed changes are verified.
    /// </returns>
    private static async Task AssertDependentSpiritualWorkersAsync(ResourceMaterializationTestContext context,
        SpiritualWoundContinuationRequest initial, SpiritualWoundContinuationRequest dependent)
    {
        var events = await new GmWorkerAuditLog(context.FileSystem).ReadEventsAsync();
        var dispatched = events.Where(entry => entry.EventType == "task-dispatched").ToArray();
        Assert.Equal(2, dispatched.Length);
        Assert.Equal(2, dispatched.Select(entry => entry.TaskId).Distinct(StringComparer.Ordinal).Count());
        foreach (var eventType in new[] { "proposal-applied", "validation-repair-ready-created" })
            Assert.Equal(2, events.Count(entry => entry.EventType == eventType));
        var phases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dispatch in dispatched)
        {
            var task = GmWorkerJson.Deserialize<WorkerTaskPacket>((await context.FileSystem.ReadFileAsync(
                GmWorkerBridgePool.GetTaskPacketPath(dispatch.TaskId!)))!);
            Assert.NotNull(task);
            Assert.Equal("session_engine_spiritual", task.SourceTurn.SessionId);
            Assert.Equal("request_engine_spiritual_42", task.SourceTurn.RequestId);
            Assert.Equal(42, task.SourceTurn.TurnNumber);
            var request = Assert.IsType<SpiritualWoundContinuationRequest>(task.SpiritualWoundContinuation);
            Assert.True(phases.Add(request.Phase));
            var expected = request.Phase == "decision" ? initial : dependent;
            Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(request));
            var proposal = GmWorkerJson.Deserialize<WorkerProposal>((await context.FileSystem.ReadFileAsync(
                GmWorkerBridgePool.GetProposalInboxPath(task.TaskId)))!);
            Assert.NotNull(proposal);
            Assert.Equal("proposal_" + task.TaskId, proposal.ProposalId);
            Assert.Equal(task.TaskId, proposal.TaskId);
            var response = Assert.IsType<SpiritualWoundContinuationResponse>(proposal.SpiritualWoundContinuation);
            Assert.Empty(SpiritualWoundContinuationProtocol.ValidateResponse(request, response));
            var changed = Assert.Single(proposal.ChangedFiles);
            if (request.Phase == "decision")
            {
                Assert.Empty(task.ValidationIssues);
                Assert.Equal("materialize", Assert.Single(response.WoundDecisions).GetProperty("decision").GetString());
                Assert.Equal("output/narrative_response.json", changed.Path);
            }
            else
            {
                Assert.Equal("dependent_draft", request.Phase);
                Assert.NotEmpty(task.ValidationIssues);
                Assert.Empty(response.WoundDecisions);
                Assert.Null(request.Offer);
                Assert.Equal(AfterlifeSpiritualConflictState.StatePath, changed.Path);
            }
            foreach (var eventType in new[] { "proposal-applied", "validation-repair-ready-created" })
                Assert.Single(events, entry => entry.EventType == eventType && entry.TaskId == task.TaskId);
        }
        Assert.Equal(new[] { "decision", "dependent_draft" }, phases.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Verifies one canonical wound publication with corrected costs, unchanged unrelated draft fields and one real notification.
    /// </summary>
    /// <param name="context">
    /// Session after successful cold late-terminal completion.
    /// </param>
    /// <param name="engine">
    /// Cold engine whose actual successful caller holds the player response and advanced turn.
    /// </param>
    /// <param name="originalDraft">
    /// Original complete two-exchange draft before the only permitted dependent arithmetic correction.
    /// </param>
    /// <param name="initialRequest">
    /// Real first offer whose opportunity reference must appear exactly once in the published receipt.
    /// </param>
    /// <param name="decisionFingerprint">
    /// Exact staged decision fingerprint saved before interruption and used to derive the published decision ID.
    /// </param>
    /// <param name="position">
    /// Whether the published draft must contain only the exact position correction; defaults to the guard-cost scenario.
    /// </param>
    /// <returns>
    /// A task completing after costs, wound/effect identities, receipt, story, notification and terminal cleanup are checked.
    /// </returns>
    private static async Task AssertDependentSpiritualPublicationAsync(ResourceMaterializationTestContext context,
        GameEngine engine, byte[] originalDraft, SpiritualWoundContinuationRequest initialRequest, string decisionFingerprint, bool position = false)
    {
        var receipt = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        var parsedReceipt = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsedReceipt.IsValid, string.Join("\n", parsedReceipt.Issues));
        Assert.Single(receipt["instances"]!.AsArray());
        Assert.Single(receipt["sources"]!.AsArray());
        var decision = Assert.Single(receipt["decisions"]!.AsArray())!;
        Assert.Equal("materialize", decision["decision"]!.GetValue<string>());
        Assert.Equal(initialRequest.Offer!.OpportunityRef, decision["opportunityRef"]!.GetValue<string>());
        Assert.Equal("spiritual_decision_" + decisionFingerprint[7..], decision["decisionId"]!.GetValue<string>());
        await AssertSpiritualLifecycleMaterializedWoundAsync(context, decision);
        var effects = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(
            await context.ReadJsonAsync(EffectIdentityState.StatePath)), EffectIdentityState.StatePath);
        Assert.Empty(effects.Issues);
        var effect = Assert.Single(effects.State!.Entries);
        Assert.Equal(decision["woundId"]!.GetValue<string>(), effect.Source["sourceId"]!.GetValue<string>());
        Assert.Equal(42, effect.CreatedAtTurn);
        Assert.Equal("create", Assert.Single(effect.Transitions).Kind);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid, string.Join("\n", definitions.Issues));
        var resources = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(resources.IsValid, string.Join("\n", resources.Issues));
        var balances = resources.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(2, balances.Length);
        Assert.All(balances, entry => Assert.Equal(0m, entry.Current));
        var history = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        var spends = history.History!.Transitions.Where(entry => entry.Turn == 42 &&
            entry.Operation == ResourceTransitionOperation.Spend &&
            entry.OriginId is "exchange_conflict_frame_42" or "exchange_source_second").ToArray();
        Assert.Equal(4, spends.Length);
        foreach (var origin in new[] { "exchange_conflict_frame_42", "exchange_source_second" })
        {
            var exchangeSpends = spends.Where(entry => entry.OriginId == origin).ToArray();
            Assert.Equal(2, exchangeSpends.Length);
            Assert.All(exchangeSpends, entry =>
            {
                Assert.Equal(3m, entry.AppliedAmount);
                Assert.Equal(origin == "exchange_conflict_frame_42" ? 6m : 3m, entry.BeforeState!.Current);
                Assert.Equal(origin == "exchange_conflict_frame_42" ? 3m : 0m, entry.AfterState!.Current);
            });
        }
        var expectedDraft = ParseDependentSpiritualBytes(originalDraft);
        if (position)
            ApplySpiritualPositionDependentCorrection(expectedDraft);
        else
        {
            expectedDraft["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 3;
            expectedDraft["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 0;
        }
        var actualDraft = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        Assert.False(actualDraft.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        if (position)
            AssertSpiritualPositionDependentPublication(ParseDependentSpiritualBytes(originalDraft), actualDraft, receipt);
        Assert.True(JsonNode.DeepEquals(expectedDraft["activeConflict"]!["exchangeLog"], actualDraft["activeConflict"]!["exchangeLog"]),
            "Publication changed a closed exchange, action, dice coordinate, order or unrelated audit field.");
        Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        var response = GetPrivateField<GameResponse>(engine, "_lastResponse");
        Assert.Contains("Чужое давление надломило волю хранителя.", response.Response, StringComparison.Ordinal);
        var notification = Assert.Single(Assert.IsType<string[]>(response.WoundNotifications));
        Assert.Contains("Получена духовная рана", notification, StringComparison.Ordinal);
        Assert.Contains("Надлом воли", notification, StringComparison.Ordinal);
        var refreshedResponse = Assert.IsType<GameResponse>(await InvokePrivateTaskResultAsync(engine, "BuildGameResponseFromFiles"));
        Assert.True(refreshedResponse.WoundNotifications is null or { Length: 0 });
        var stories = new StoryService(context.FileSystem, NullLogger<StoryService>.Instance);
        var story = Assert.Single(await stories.ReadStoryAsync("stories/chaos_sea.jsonl"), entry => entry.Turn == 42);
        Assert.Equal("Удержать встречное духовное давление.", story.Player);
        Assert.Contains("Чужое давление надломило волю хранителя.", story.Narrative, StringComparison.Ordinal);
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
                     "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
                     "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
                     SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                     AcceptedMechanicsPlan.WoundCommandPath })
            Assert.False(context.FileSystem.FileExists(path), path);
    }
}
