using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    public Task QtePhysicalInjuryConsumer_ComponentCompletedResourceWorkReplaysExactMechanicalBytes(bool materialize) =>
        AssertCompletedMortalResourceWorkReplaysExactMechanicalBytesAsync(materialize);

    [Fact]
    public Task QtePhysicalInjuryConsumer_ComponentCreatedConsequenceAndResourceWorkReplayExactMechanicalBytes() =>
        AssertCompletedMortalResourceWorkReplaysExactMechanicalBytesAsync(true);

    private async Task AssertCompletedMortalResourceWorkReplaysExactMechanicalBytesAsync(bool materialize)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(materialize);
        var stateBefore = await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath);
        var historyBefore = await _fs.ReadFileBytesAsync(ResourceMaterializationContract.HistoryPath);
        var health = new ResourceCoordinate("mortal_world", ResourceOwnerKind.Player, "player_current", "health");
        if (materialize)
        {
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
                [ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                    ResourceMaterializationContract.HistoryPath]);
            Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
            Assert.Equal(component.Snapshot.RequestId, read.Snapshot!.RequestId);
            Assert.Equal(component.Snapshot.SnapshotToken, read.Snapshot.SnapshotToken);
            Assert.Equal(stateBefore, read.Snapshot.ReadRequiredBytes(ResourceMaterializationContract.StatePath));
            Assert.Equal(historyBefore, read.Snapshot.ReadRequiredBytes(ResourceMaterializationContract.HistoryPath));
            var originalDefinitions = ResourceDefinitionCatalog.ParseCanonical(Encoding.UTF8.GetString(
                read.Snapshot.ReadRequiredBytes(ResourceMaterializationContract.DefinitionsPath)).TrimStart('\uFEFF'), false);
            Assert.True(originalDefinitions.IsValid);
            var originalState = ResourceStateContract.ParseCanonical(Encoding.UTF8.GetString(stateBefore!).TrimStart('\uFEFF'),
                originalDefinitions.Catalog!, false);
            Assert.True(originalState.IsValid);
            Assert.True(originalState.Ledger!.TryResolveExact(health, out var originalHealth));
            Assert.Equal(155m, originalHealth!.Current);
        }
        await WriteMortalReplayResourceWorkAsync();
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        var first = await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null);
        Assert.NotNull(first);
        Assert.NotEqual(stateBefore, await _fs.ReadFileBytesAsync(ResourceMaterializationContract.StatePath));
        Assert.NotEqual(historyBefore, await _fs.ReadFileBytesAsync(ResourceMaterializationContract.HistoryPath));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid);
        var history = ResourceHistoryState.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(history.IsValid);
        var damage = Assert.Single(history.History!.Transitions, row => row.EventRef == "turn_43:resource:1");
        Assert.Equal(ResourceTransitionOperation.Damage, damage.Operation);
        Assert.Equal(ResourceTransitionOutcome.Applied, damage.Outcome);
        Assert.Equal(1m, damage.AppliedAmount);
        if (materialize)
        {
            var player = JsonNode.Parse((await _fs.ReadFileAsync(WoundCarrierCatalog.PlayerPath))!)!.AsObject();
            var wound = Assert.IsType<JsonObject>(Assert.Single(player["activeWounds"]!.AsArray()));
            var parsedWound = WoundMaterializationContract.Parse(wound.ToJsonString(), WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
            Assert.True(parsedWound.IsValid, JsonSerializer.Serialize(parsedWound.Issues));
            var rootBinding = Assert.IsType<JsonObject>(Assert.Single(wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray()));
            var effectId = rootBinding["effectId"]!.GetValue<string>();
            var effects = first!.EffectCarrierAfterImages[EffectCarrierCatalog.PlayerPath];
            Assert.True(JsonNode.DeepEquals(effects, JsonNode.Parse((await _fs.ReadFileAsync(EffectCarrierCatalog.PlayerPath))!)));
            var effect = Assert.Single(effects["activeEffects"]!.AsArray().OfType<JsonObject>(), row => row["effectId"]?.GetValue<string>() == effectId);
            Assert.Equal("wound", effect["source"]!["kind"]!.GetValue<string>());
            Assert.Equal(parsedWound.Wound!.WoundId, effect["source"]!["sourceId"]!.GetValue<string>());
            Assert.Equal(rootBinding["definitionKey"]!.GetValue<string>(), effect["source"]!["definitionKey"]!.GetValue<string>());
            var componentEffect = Assert.Single(effect["components"]!.AsArray().OfType<JsonObject>());
            Assert.Equal("periodic_damage", componentEffect["profile"]!.GetValue<string>());
            Assert.Equal("health", componentEffect["payload"]!["resource"]!.GetValue<string>());
            using var componentDocument = JsonDocument.Parse(componentEffect.ToJsonString());
            Assert.Equal(3m, componentDocument.RootElement.GetProperty("payload").GetProperty("amount").GetDecimal());
            var effectAuthority = EffectAcceptedTurnPlanner.ResolvePendingEffectAuthority(effect, 43);
            Assert.Equal("accepted_application", effectAuthority.BindingKind);
            var tickRef = EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(
                "turn_43:lifecycle:owner_turn_end:player_current", effectId, effectAuthority, "on_owner_turn_end");
            var tick = Assert.Single(history.History.Transitions, row => row.Turn == 43 && row.Coordinate == health &&
                row.SourceEvidence.SourceKind == "effect_component");
            Assert.Equal(tickRef, tick.EventRef);
            Assert.Equal(health, tick.Coordinate);
            Assert.Equal(43, tick.Turn);
            Assert.Equal("effect_component", tick.SourceEvidence.SourceKind);
            Assert.Equal(ResourceMutationPhase.EffectTrigger, tick.Phase);
            Assert.Equal(ResourceTransitionOperation.Damage, tick.Operation);
            Assert.Equal(ResourceTransitionOutcome.Applied, tick.Outcome);
            Assert.Equal(3m, tick.AppliedAmount);
            Assert.Equal(155m, tick.BeforeState!.Current);
            Assert.Equal(152m, tick.AfterState!.Current);
            Assert.True(tick.ExecutionSequence < damage.ExecutionSequence);
            Assert.Equal(health, damage.Coordinate);
            Assert.Equal(43, damage.Turn);
            Assert.Equal("narrative_outcome", damage.SourceEvidence.SourceKind);
            Assert.Equal(tick.AfterState.Current, damage.BeforeState!.Current);
            Assert.Equal(151m, damage.AfterState!.Current);
            var finalState = ResourceStateContract.ParseCanonical(await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
            Assert.True(finalState.IsValid);
            Assert.True(finalState.Ledger!.TryResolveExact(health, out var finalHealth));
            Assert.Equal(151m, finalHealth!.Current);
            Assert.Empty(history.History.ValidateStateAgreement(finalState.Ledger));
            WritePhysicalInjuryDiagnostic("qte_consumer_component_owned_tick_resource_chain", new
            { effectId, parsedWound.Wound.WoundId, effectAuthority, tick, damage, Health = finalHealth.Current });
        }
        else
        {
            Assert.Equal(155m, damage.BeforeState!.Current);
            Assert.Equal(154m, damage.AfterState!.Current);
        }
        var marker = await _fs.ReadFileBytesAsync(MortalQteDecisionPublication.StatePath);
        Assert.NotNull(marker);
        var outputAgreement = await WoundAcceptedTurnSnapshotContract.ValidatePublishedOutputAuthorityAsync(first!, path => _fs.ReadFileBytesAsync(path));
        Assert.Empty(outputAgreement); // Includes the planned consumed draft for NONE and CREATE.
        var beforeReplay = await CaptureMortalComponentImagesAsync();
        await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, component.Decision.ToJsonString());
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        var replay = await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null);
        Assert.NotNull(replay);
        Assert.Null(replay!.WoundStageBundle);
        Assert.Null(replay.WoundHistoryAfterImage);
        foreach (var image in beforeReplay) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.Equal(marker, await _fs.ReadFileBytesAsync(MortalQteDecisionPublication.StatePath));
        var receipts = MortalWoundOpportunityReceiptState.Parse(await _fs.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath), MortalWoundOpportunityReceiptState.StatePath);
        Assert.True(receipts.IsValid);
        Assert.Single(receipts.State!.Receipts);
        Assert.False(_fs.FileExists(MortalWoundOpportunityAdapter.DecisionPath));
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(_fs.FileExists(ResourceMaterializationContract.CommandPath));
        WritePhysicalInjuryDiagnostic("qte_consumer_component_completed_resource_replay", new
        { materialize, component.Snapshot.RequestId, component.Snapshot.SnapshotToken, Marker = Encoding.UTF8.GetString(marker!),
            CanonicalPaths = beforeReplay.Keys, first.InputFingerprint, ReplayFingerprint = replay.InputFingerprint });
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("generation")]
    public async Task QtePhysicalInjuryConsumer_ComponentReplayRequiresExactDurableCompletion(string mutation)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(false);
        await WriteMortalReplayResourceWorkAsync();
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        Assert.NotNull(await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null));
        var originalMarker = await _fs.ReadFileBytesAsync(MortalQteDecisionPublication.StatePath);
        Assert.NotNull(originalMarker);
        if (mutation == "missing") _fs.DeleteFile(MortalQteDecisionPublication.StatePath);
        else if (mutation == "corrupt") await _fs.WriteFileAtomicAsync(MortalQteDecisionPublication.StatePath, "null");
        else
        {
            var marker = JsonNode.Parse(Encoding.UTF8.GetString(originalMarker!).TrimStart('\uFEFF'))!.AsObject();
            marker["generation"] = "foreign_generation"; // Deliberately invalid checkpoint; no seal/authority is fabricated.
            await _fs.WriteFileAtomicAsync(MortalQteDecisionPublication.StatePath, marker.ToJsonString());
        }
        var before = await CaptureMortalComponentImagesAsync();
        await _fs.WriteFileAtomicAsync(MortalWoundOpportunityAdapter.DecisionPath, component.Decision.ToJsonString());
        var issues = await ValidateMortalDecisionComponentAsync(component.Engine);
        Assert.Contains(issues, issue => issue.Code == "mortal_qte_completed_publication_invalid" &&
            issue.Section == MortalWoundOpportunityAdapter.PrivateSourceIssueSection);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync()) Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease));
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        WritePhysicalInjuryDiagnostic("qte_consumer_component_completion_refusal", new { mutation, issues, component.Snapshot.RequestId, component.Snapshot.SnapshotToken });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QtePhysicalInjuryConsumer_ComponentDirectPostcommitFailureCompensatesWholeBatch(bool materialize)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var component = await PrepareMortalDecisionComponentAsync(materialize);
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        var before = await CaptureMortalComponentImagesAsync();
        var draft = await _fs.ReadFileBytesAsync(MortalWoundOpportunityAdapter.DecisionPath);
        var command = await _fs.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath);
        var scene = await _fs.ReadFileBytesAsync("output/narrative_response.json");
        var committedObserved = false;
        _consolePublicationObserver = (phase, index) =>
        {
            if (committedObserved || phase != TrustedLocalPublicationPhase.Committed) return;
            var journalPath = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(journalPath));
            if (!journal.RootElement.GetProperty("Members").EnumerateArray().Any(row =>
                    row.GetProperty("Path").GetString() == _fs.ResolvePath(MortalQteDecisionPublication.StatePath))) return;
            Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
            committedObserved = true;
            // Known common after-images remain intact; drift a retained public output
            // to force the owning bound child's postcommit agreement failure.
            File.WriteAllText(_fs.ResolvePath("output/narrative_response.json"), "{\"response\":\"Несогласованная послепубликационная сцена.\"}");
        };
        Exception? failure;
        try { failure = await Record.ExceptionAsync(() => GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null)); }
        finally { _consolePublicationObserver = null; }
        Assert.True(committedObserved);
        Assert.NotNull(failure);
        foreach (var image in before) Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(image.Key));
        Assert.Equal(draft, await _fs.ReadFileBytesAsync(MortalWoundOpportunityAdapter.DecisionPath));
        Assert.Equal(command, await _fs.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(_fs.FileExists(MortalQteDecisionPublication.StatePath));
        Assert.False(_fs.FileExists(MortalWoundOpportunityReceiptState.StatePath));
        await _fs.WriteFileAtomicBytesAsync("output/narrative_response.json", scene!);
        Assert.Empty(await ValidateMortalDecisionComponentAsync(component.Engine));
        Assert.NotNull(await GetPrivateField<CanonicalStateNormalizer>(component.Engine, "_normalizer").NormalizeAcceptedMechanicsAsync(null));
        Assert.True(_fs.FileExists(MortalQteDecisionPublication.StatePath));
        var receipts = MortalWoundOpportunityReceiptState.Parse(await _fs.ReadFileAsync(MortalWoundOpportunityReceiptState.StatePath), MortalWoundOpportunityReceiptState.StatePath);
        Assert.True(receipts.IsValid);
        Assert.Single(receipts.State!.Receipts);
        WritePhysicalInjuryDiagnostic("qte_consumer_component_direct_compensation", new { materialize, committedObserved, Failure = failure.ToString() });
    }

    private Task WriteMortalReplayResourceWorkAsync() => _fs.WriteFileAtomicAsync(ResourceMaterializationContract.CommandPath,
        new JsonObject { ["resourceChanges"] = new JsonArray(new JsonObject
        {
            ["operation"] = "damage", ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
            ["resourceKey"] = "health", ["amount"] = 1, ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
            ["eventRef"] = "turn_43:resource:1", ["reason"] = "Осмотр ушиба причиняет небольшую боль."
        }) }.ToJsonString());
}
