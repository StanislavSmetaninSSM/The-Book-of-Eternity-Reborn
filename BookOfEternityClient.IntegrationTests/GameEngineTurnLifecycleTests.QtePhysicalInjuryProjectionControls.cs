using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QtePhysicalProjection_SignedPristineOrEmptyRootsNeverBorrowLaterLiveState(bool explicitlyEmpty)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        await PreparePhysicalInjuryAdmissionAsync(async () =>
        {
            if (!explicitlyEmpty) return;
            await _fs.WriteFileAtomicAsync(MortalWoundOccurrenceState.StatePath, "{\"schemaVersion\":1,\"occurrences\":[]}");
            await _fs.WriteFileAtomicAsync(MortalWoundOpportunityReceiptState.StatePath, "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}");
        });
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(["game_state/meta/soul_state.json"],
                [MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath]));
        Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
        foreach (var path in new[] { MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath })
            Assert.Equal(!explicitlyEmpty, read.Snapshot!.AbsentLogicalPaths.Contains(path));
        Assert.Empty(MortalQteWoundOpportunityProjection.ReadCurrent(_fs, lease));
        await _fs.WriteFileAtomicAsync(lease, MortalWoundOccurrenceState.StatePath, "{\"laterInvalidLiveState\":true}");
        var liveBefore = await _fs.ReadFileBytesAsync(lease, MortalWoundOccurrenceState.StatePath);
        Assert.Empty(MortalQteWoundOpportunityProjection.ReadCurrent(_fs, lease));
        Assert.Equal(liveBefore, await _fs.ReadFileBytesAsync(lease, MortalWoundOccurrenceState.StatePath));
        WritePhysicalInjuryDiagnostic("qte_projection_signed_empty", new
        { explicitlyEmpty, read.Snapshot!.RequestId, read.Snapshot.SnapshotToken, read.Snapshot.AbsentLogicalPaths });
    }

    [Theory]
    [InlineData("none")]
    [InlineData("event_order")]
    [InlineData("missing_candidate")]
    [InlineData("corrupt_later_sibling")]
    [InlineData("receipt")]
    [InlineData("signed_present_null_carrier")]
    public async Task QtePhysicalProjection_ValidatesTheEntireSignedBatchBeforeSelectingFirst(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2, bounded: true);
        Assert.Null(run.Failure);
        Assert.True(run.ActionObserved);
        var waiting = GetPrivateField<QteSceneService>(run.Engine!, "_qteSceneService");
        Assert.Equal("awaiting_receipt", (await waiting.ReadRuntimeStateAsync()).ActiveScene!.EffectResolutionState);
        Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));
        await WriteActualPhysicalBoundedReceiptAsync();
        var resumed = CreateGameEngine(new QueuedConsoleInputSource([]), settings =>
        { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; });
        GetPrivateField<GameLoop>(resumed, "_gameLoop").SetSession(run.Source!.SessionId, PhysicalInjurySourceTurn);
        await GetPrivateField<StateManager>(resumed, "_stateManager").RefreshGameStateAsync();
        Assert.True(await InvokePrivateAsync<bool>(resumed, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync"));
        await AssertActualQteStoryFinalizedAsync(_fs, run.Source, run.StoryAtHandoff!, 153m);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(run, 2);
        Assert.True(occurrence.AcceptedEvents.Count > 2);
        Assert.Contains(occurrence.AcceptedEvents, row => row.Kind == "effect_activation");
        var root = JsonNode.Parse((await _fs.ReadFileAsync(MortalWoundOccurrenceState.StatePath))!)!.AsObject();
        var rows = root["occurrences"]!.AsArray();
        var first = Assert.Single(rows)!.AsObject();
        switch (mutation)
        {
            case "event_order":
                first["acceptedEvents"] = new JsonArray(first["acceptedEvents"]!.AsArray().Reverse().Select(node => node!.DeepClone()).ToArray());
                break;
            case "missing_candidate": first["producerCandidateCount"] = 2; break;
            case "corrupt_later_sibling":
                var sibling = first.DeepClone().AsObject();
                sibling["occurrenceId"] = "corrupt_later_sibling";
                rows.Add(sibling);
                break;
            case "receipt":
                await _fs.WriteFileAtomicAsync(MortalWoundOpportunityReceiptState.StatePath, "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[{}]}");
                break;
            case "signed_present_null_carrier":
                await _fs.WriteFileAtomicAsync(WoundCarrierCatalog.PlayerPath, "null");
                break;
        }
        await _fs.WriteFileAtomicAsync(MortalWoundOccurrenceState.StatePath, root.ToJsonString());
        // Component control: the production baseline writer observes the deliberately corrupted
        // next state. No occurrence, source capsule, manifest or hash is manufactured by this test.
        var request = new TurnRequest
        {
            SessionId = run.Source!.SessionId, RequestId = Guid.NewGuid().ToString("N"), TurnNumber = 43,
            Timestamp = DateTime.UtcNow.ToString("O"), PlayerAction = "Проверить сохранённое событие.",
            PreGeneratedDices1d20 = [3, 17], ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
        };
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]));
        await InvokePrivateTaskAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, null, QteSceneService.OrdinaryPlayerTurnSourceLabel);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", JsonSerializer.Serialize(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var before = BrowserRecoveryTree();
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            if (mutation == "none")
            {
                var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, [MortalWoundOccurrenceState.StatePath]);
                Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
                Assert.NotEqual(run.SnapshotToken, read.Snapshot!.SnapshotToken);
                var signed = MortalWoundOccurrenceState.Parse(Encoding.UTF8.GetString(read.Snapshot.ReadRequiredBytes(MortalWoundOccurrenceState.StatePath)).TrimStart('\uFEFF'), MortalWoundOccurrenceState.StatePath);
                Assert.True(signed.IsValid, JsonSerializer.Serialize(signed.Issues));
                Assert.Equal(occurrence.AcceptedEvents.Select(row => (row.EventRef, row.Kind, row.AuthorityId)),
                    Assert.Single(signed.State!.Occurrences).AcceptedEvents.Select(row => (row.EventRef, row.Kind, row.AuthorityId)));
                var safe = Assert.Single(MortalQteWoundOpportunityProjection.ReadCurrent(_fs, lease))!;
                Assert.Equal(occurrence.OpportunityRef, safe["opportunityRef"]!.GetValue<string>());
                Assert.Equal("II", safe["maximumSeverity"]!.GetValue<string>());
                Assert.Equal(occurrence.SafeContext.Cause, safe["safeContext"]!["cause"]!.GetValue<string>());
                WritePhysicalInjuryDiagnostic("qte_projection_complete_ordered_batch", new
                { read.Snapshot.RequestId, read.Snapshot.SnapshotToken, occurrence.AcceptedEvents, Safe = safe });
            }
            else
            {
                if (mutation == "signed_present_null_carrier")
                {
                    var covered = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, [WoundCarrierCatalog.PlayerPath]);
                    Assert.True(covered.Success, JsonSerializer.Serialize(covered.Issues));
                    Assert.DoesNotContain(WoundCarrierCatalog.PlayerPath, covered.Snapshot!.AbsentLogicalPaths);
                    Assert.Equal("null", Encoding.UTF8.GetString(covered.Snapshot.ReadRequiredBytes(WoundCarrierCatalog.PlayerPath)).TrimStart('\uFEFF'));
                }
                var failure = Assert.Throws<InvalidDataException>(() => MortalQteWoundOpportunityProjection.ReadCurrent(_fs, lease));
                Assert.Contains("mortal_qte_projection_authority_invalid", failure.Message, StringComparison.Ordinal);
                WritePhysicalInjuryDiagnostic("qte_projection_full_batch_refusal", new { mutation, failure.Message });
            }
        }
        AssertBrowserRecoveryTree(before);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QtePhysicalProjection_FinalPreparationMatchesPhysicalRequestAndBrowserProof(bool browser)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var run = await RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2);
        Assert.Null(run.Failure);
        var occurrence = await AssertActualPhysicalOccurrenceAsync(run, 2);
        var binding = browser ? await QueueBrowserInputAsync() : null;
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), settings =>
        { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; });
        GetPrivateField<GameLoop>(engine, "_gameLoop").SetSession(run.Source!.SessionId, PhysicalInjurySourceTurn);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        await SessionOperationContext.RunParticipatingCurrentSessionAsync(_fs, async () =>
        {
            var prepared = await InvokePrivateTaskResultAsync(engine, "StagePlayerTurnAsync",
                binding?.Action ?? "Осмотреть ушиб.", null, binding);
            Assert.NotNull(prepared);
            var memory = Assert.IsType<TurnRequest>(prepared.GetType().GetProperty("Request")!.GetValue(prepared));
            var physical = JsonSerializer.Deserialize<TurnRequest>((await _fs.ReadFileAsync("input/turn_request.json"))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            Assert.Equal(43, memory.TurnNumber);
            Assert.Equal(memory.RequestId, physical.RequestId);
            Assert.True(JsonNode.DeepEquals(memory.WoundOpportunities, physical.WoundOpportunities));
            var safe = Assert.Single(memory.WoundOpportunities)!;
            Assert.Equal(occurrence.OpportunityRef, safe["opportunityRef"]!.GetValue<string>());
            Assert.Equal("II", safe["maximumSeverity"]!.GetValue<string>());
            Assert.Equal(new[] { "domain", "maximumSeverity", "opportunityRef", "safeContext" }, safe.AsObject().Select(field => field.Key).Order(StringComparer.Ordinal));
            Assert.Equal(new[] { "allowedLocationKinds", "cause", "target" }, safe["safeContext"]!.AsObject().Select(field => field.Key).Order(StringComparer.Ordinal));
            if (browser)
            {
                var staged = Assert.IsType<PendingPlayerActionService.Staged>(prepared.GetType().GetProperty("BrowserStaged")!.GetValue(prepared));
                Assert.Equal(staged.RequestJson, await _fs.ReadFileAsync("input/turn_request.json"));
                await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
                var state = await PendingPlayerActionService.ReadAsync(_fs, lease);
                Assert.Equal("staged", state!.Phase);
                var proof = PendingPlayerActionService.ReadStaged(state);
                Assert.Equal(staged.RequestJson, proof.RequestJson);
                Assert.Equal(staged.ManifestJson, proof.ManifestJson);
                Assert.Equal(staged.AuthorityJson, proof.AuthorityJson);
                WritePhysicalInjuryDiagnostic("qte_projection_browser_staged_proof", new
                { staged.RequestJson, state.ProofJson });
                Assert.Equal(binding!.ActionId, memory.RequestId);
            }
            WritePhysicalInjuryDiagnostic("qte_projection_final_preparation", new { browser, memory.RequestId, memory.TurnNumber, memory.WoundOpportunities });
            return true;
        });
    }
}
