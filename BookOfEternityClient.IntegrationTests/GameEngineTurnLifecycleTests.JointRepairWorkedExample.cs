using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using BookOfEternityClient.UI;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    // No Fact-to-Fact call or authored snapshot/claim. The fixture owns actual QTE42,
    // real Stage43 and cleanup. Returned packet/witness are diagnostic data only.
    internal static async Task<JsonObject> VerifyJointSeverityRepairWorkedExampleAsync(
        JsonObject sourceOffer, JsonObject rejectedResponse, JsonObject packetView,
        JsonObject correctedResponse)
    {
        using var fixture = new GameEngineTurnLifecycleTests();
        var cleanup = new List<string>();
        JsonObject witness;
        using (var owned = new CleanupOwnedFixture(fixture._rootPath, receipt =>
        {
            cleanup.Add(receipt);
            Console.WriteLine("JointWorkedExampleActualCleanup=" + receipt);
        }))
        {
            IDisposable? sourceInput = null;
            QteStoryRun source;
            try
            {
                source = await fixture.RunActualQteStorySourceAsync(physicalDeclaration: true,
                    physicalMaximum: 2, retainLiveInput: input => sourceInput = input);
            }
            finally { sourceInput?.Dispose(); }
            Assert.Null(source.Failure);
            Assert.True(source.ActionObserved);
            Assert.NotNull(source.Source);
            await AssertActualQteStoryFinalizedAsync(fixture._fs, source.Source!,
                source.StoryAtHandoff!, 155m);
            var occurrence = await fixture.AssertActualPhysicalOccurrenceAsync(source, 2);
            var engine = fixture.CreateGameEngine(new QueuedConsoleInputSource([]), settings =>
            { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; });
            GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(source.Source!.SessionId, 42);
            await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
            await SessionOperationContext.RunParticipatingCurrentSessionAsync(fixture._fs, async () =>
            {
                Assert.NotNull(await InvokePrivateTaskNullableResultAsync(engine,
                    "StagePlayerTurnAsync", "Осмотреть ушиб.", null, null));
                return true;
            });
            await using var lease = await fixture._fs.AcquireCanonicalWriteLeaseAsync();
            var original = MortalQteWoundOpportunityProjection.ReadSnapshot(fixture._fs, lease);
            Assert.Equal(43, original.TurnNumber);
            Assert.Equal(source.Source.SessionId, original.SessionId);
            Assert.NotEqual(source.Source.RequestId, original.RequestId);
            var offer = Assert.Single(MortalQteWoundOpportunityProjection.Project(original));
            var reference = offer!["opportunityRef"]!.GetValue<string>();
            Assert.Equal(occurrence.OpportunityRef, reference);
            Assert.Equal("II", offer["maximumSeverity"]!.GetValue<string>());
            var before = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in QtePhysicalInjurySource.WoundRootPaths.Concat(new[]
            {
                ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                "input/turn_request.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                PendingTurnSnapshotAuthority.AuthorityPath, MortalWoundOpportunityAdapter.DecisionPath,
                "output/narrative_response.json"
            }).Distinct(StringComparer.Ordinal))
                before.Add(path, await fixture._fs.ReadFileBytesAsync(lease, path));
            var generationBefore = fixture._fs.ReadLocalGenerationSnapshot(lease);
            var generation = Assert.IsType<string>(generationBefore.Binding.Id);
            var generationBytes = Assert.IsType<byte[]>(generationBefore.Bytes).ToArray();

            // Owner/source observations precede the documentation assertions.
            Console.WriteLine(JsonSerializer.Serialize(new { ExampleProducer = "real QTE42 -> original43",
                original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber,
                Generation = generation, Offer = offer }));
            Assert.True(JsonNode.DeepEquals(BuildPhysicalInjuryOffer(true, 2), sourceOffer));
            var rejected = rejectedResponse.DeepClone().AsObject();
            var corrected = correctedResponse.DeepClone().AsObject();
            var expectedView = packetView.DeepClone().AsObject();
            BindExampleOpportunity(rejected, reference);
            BindExampleOpportunity(corrected, reference);
            BindExampleOpportunity(expectedView["requiredResponseShape"]!.AsObject(), reference);
            var rejectedDecision = Assert.Single(rejected["woundDecisions"]!.AsArray())!.AsObject();
            var correctedDecision = Assert.Single(corrected["woundDecisions"]!.AsArray())!.AsObject();
            var expectedProposal = CreateMortalComponentProposal();
            expectedProposal["severity"] = "III";
            Assert.True(JsonNode.DeepEquals(expectedProposal, rejectedDecision["proposal"]));
            Assert.Equal("III", rejectedDecision["proposal"]!["severity"]!.GetValue<string>());
            Assert.Equal("II", correctedDecision["proposal"]!["severity"]!.GetValue<string>());
            Assert.Equal(rejected["response"]!.GetValue<string>(), corrected["response"]!.GetValue<string>());
            var withOnlySeverityChanged = rejected.DeepClone().AsObject();
            withOnlySeverityChanged["woundDecisions"]![0]!["proposal"]!["severity"] = "II";
            Assert.True(JsonNode.DeepEquals(withOnlySeverityChanged, corrected));
            var scene = rejected["response"]!.GetValue<string>();
            var rejectedResult = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(
                fixture._fs, lease, rejectedDecision.ToJsonString(), scene,
                out var rejectedReplay, out var opportunity);
            Console.WriteLine(JsonSerializer.Serialize(new { ExampleAdapterStage = "rejected III",
                ResultPresent = rejectedResult is not null, Success = rejectedResult?.Success,
                Issues = rejectedResult?.Issues.Select(static issue => new { issue.FilePath, issue.Code,
                    issue.Section, Severity = issue.Severity.ToString(), issue.Expected, issue.Actual }).ToArray(),
                ReplayPresent = rejectedReplay is not null, OpportunityPresent = opportunity is not null }));
            Assert.NotNull(rejectedResult);
            Assert.False(rejectedResult!.Success);
            Assert.Null(rejectedReplay);
            Assert.NotNull(opportunity);
            Assert.Single(rejectedResult.Issues);
            Assert.Equal("wound_severity_above_opportunity", rejectedResult.Issues[0].Code);
            var packet = Assert.Single(WoundRepairPacketBuilder.Build(rejectedResult.Issues));
            var fullPacket = packet.ToJsonObject();
            Console.WriteLine("CompleteActualRepairPacketBeforeDocumentOracles=" + fullPacket.ToJsonString());
            // The document excludes exactly these live client-issued correlation fields.
            var semanticView = fullPacket.DeepClone().AsObject();
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "candidateRef", "semanticFingerprint" })
                Assert.True(semanticView.Remove(field));
            Assert.True(JsonNode.DeepEquals(expectedView, semanticView), semanticView.ToJsonString());
            var receipts = JsonSerializer.SerializeToElement(new[] { packet.CreateReceipt() },
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            Assert.True(WoundRepairPacketBuilder.IsValidPersistedRepairWave(
                JsonSerializer.SerializeToElement(new JsonArray(fullPacket.DeepClone())), receipts,
                original.SessionId, original.RequestId, original.SnapshotToken));
            Assert.True(packet.MatchesCorrectedDecision(correctedDecision, scene));
            var accepted = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(
                fixture._fs, lease, correctedDecision.ToJsonString(), scene,
                out var exactReplay, out var correctedOpportunity);
            Console.WriteLine(JsonSerializer.Serialize(new { ExampleAdapterStage = "corrected II",
                ResultPresent = accepted is not null, Success = accepted?.Success,
                Issues = accepted?.Issues.Select(static issue => new { issue.FilePath, issue.Code,
                    issue.Section, Severity = issue.Severity.ToString(), issue.Expected, issue.Actual }).ToArray(),
                ReplayPresent = exactReplay is not null, OpportunityPresent = correctedOpportunity is not null }));
            Assert.NotNull(accepted);
            Assert.True(accepted!.Success, JsonSerializer.Serialize(accepted.Issues));
            Assert.Null(exactReplay);
            Assert.Single(accepted.MaterializedOpportunities);
            Assert.Single(accepted.Transitions);
            Assert.NotNull(accepted.CommandRoot);
            var changedSibling = correctedDecision.DeepClone().AsObject();
            changedSibling["proposal"]!["display"]!["name"] = "Unrelated replacement";
            Assert.False(packet.MatchesCorrectedDecision(changedSibling, scene));
            Assert.False(packet.MatchesCorrectedDecision(correctedDecision, "Narration removed."));
            var copiedAuthority = correctedDecision.DeepClone().AsObject();
            copiedAuthority["operationKey"] = "copied-private-authority";
            Assert.False(packet.MatchesCorrectedDecision(copiedAuthority, scene));
            var stillRejected = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(
                fixture._fs, lease, rejectedDecision.ToJsonString(), scene, out _);
            Assert.NotNull(stillRejected);
            Assert.False(stillRejected!.Success);
            Assert.Equal("wound_severity_above_opportunity", Assert.Single(stillRejected.Issues).Code);
            foreach (var image in before)
                Assert.Equal(image.Value, await fixture._fs.ReadFileBytesAsync(lease, image.Key));
            var generationAfter = fixture._fs.ReadLocalGenerationSnapshot(lease);
            Assert.Equal(generation, generationAfter.Binding.Id);
            Assert.Equal(generationBytes, Assert.IsType<byte[]>(generationAfter.Bytes));
            witness = new JsonObject { ["fullClientIssuedPacket"] = fullPacket,
                ["sourceTurn"] = 42, ["originalTurn"] = original.TurnNumber,
                ["generation"] = generation, ["canonicalFilesUnchanged"] = true };
            Console.WriteLine(witness.ToJsonString());
        }
        Assert.Single(cleanup);
        witness["ownedCleanupDiagnostic"] = JsonNode.Parse(cleanup[0]);
        return witness;
    }

    private static void BindExampleOpportunity(JsonObject response, string reference)
    {
        var decision = Assert.Single(response["woundDecisions"]!.AsArray())!.AsObject();
        Assert.Equal("<current-client-opportunityRef>", decision["opportunityRef"]!.GetValue<string>());
        decision["opportunityRef"] = reference;
    }
}
