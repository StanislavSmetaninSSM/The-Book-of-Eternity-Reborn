using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QtePhysicalInjuryProducer_FinalizedProjection_ZeroOrReplayCannotCreateNewHarm(bool replay)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var selection = PhysicalProducerComponentSelection();
        _ = await PreparePhysicalInjuryAdmissionAsync(async () =>
        {
            var seed = replay ? selection : new QteTerminalResourceSelection(41,
                "qte_health_seed", "impact", "brace", "success", 1, "health_seed",
                new JsonObject { ["resourceChanges"] = new JsonArray(new JsonObject
                {
                    ["operation"] = "damage", ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                    ["resourceKey"] = "health", ["amount"] = 160,
                    ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                    ["eventRef"] = "turn_41:qte_terminal:1:resource:1", ["reason"] = "Предшествующий удар."
                }) });
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            var canonical = await QteTerminalResourceOutcome.BuildAsync(_fs, lease, seed);
            Assert.True(canonical.IsValid, JsonSerializer.Serialize(canonical.Issues));
            Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(_fs, lease, canonical.Writes.ToArray()));
        });
        var before = await CapturePhysicalInjuryProtectedBytesAsync(includeOfferAndFreeze: false);
        var component = await PreparePhysicalProducerComponentAsync(selection);
        var resources = component.Reduction.Completed!.Resources;
        var mutation = Assert.Single(resources.EffectBoundaryTranscript!.ResourceMutations);
        Assert.Equal(replay ? ResourceMutationExecutionKind.Replay : ResourceMutationExecutionKind.Applied, mutation.ExecutionKind);
        Assert.Equal(replay ? 5m : 0m, mutation.Transition.AppliedAmount);
        var producer = QtePhysicalWoundOccurrenceProducer.Create(component.Source, selection, component.Reduction.Completed);
        Assert.True(producer.FinalizedProjection.Success, JsonSerializer.Serialize(producer.FinalizedProjection.Issues));
        Assert.Empty(producer.FinalizedProjection.Harms);
        var final = AcceptedMechanicsPlanner.CompleteQtePhysicalWoundOccurrenceReduction(component.Reduction, producer, component.Roots);
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction, final.Kind);
        Assert.Same(resources, final.Completed!.Resources);
        Assert.False(final.Completed.OwnerCompanionAfterImages.ContainsKey(MortalWoundOccurrenceState.StatePath));
        var result = AcceptedMechanicsPlanner.CompleteAcceptedReduction(final);
        Assert.True(result.Success, JsonSerializer.Serialize(result.Issues));
        Assert.True(new AcceptedMechanicsPlanCache((_, _) => result).GetOrBuildValidated(final.Completed.Input).Success);
        await AssertPhysicalInjuryProtectedBytesAsync(before);
        Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));
        WritePhysicalInjuryDiagnostic("qte_physical_component_no_new_harm", new
        { replay, component.Source.RequestId, component.Source.SnapshotToken, mutation.ExecutionKind, mutation.Transition.AppliedAmount,
            final.Completed.InputFingerprint, ResourceTranscript = resources.EffectBoundaryTranscript.Fingerprint });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QtePhysicalInjuryProducer_FinalCompletion_MissingOriginalSourceOrChangedSiblingCannotPublish(bool changedSibling)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var fixture = await PreparePhysicalInjuryAdmissionAsync();
        if (!changedSibling)
        {
            var raw = BuildPhysicalInjuryOffer(true, 2);
            await _fs.WriteFileAtomicAsync(QteSceneService.QteOfferPath, raw.ToJsonString());
            Assert.DoesNotContain(await fixture.Validator.ValidateAcceptedTurnQteOfferAsync(), issue => issue.Severity == IssueSeverity.Error);
            var offer = (await fixture.Service.TryReadOfferAsync())!;
            offer = await fixture.Service.BindAcceptedTurnAuthorityAsync(offer, PhysicalInjurySourceTurn);
            var before = await CapturePhysicalInjuryProtectedBytesAsync(includeOfferAndFreeze: true);
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service.BeginAcceptedSceneAsync(offer, PhysicalInjurySourceTurn));
            Assert.Contains("qte_physical_injury_original_source_missing", failure.Message, StringComparison.Ordinal);
            await AssertPhysicalInjuryProtectedBytesAsync(before);
            Assert.False(_fs.FileExists(QteSceneService.QteRuntimePath));
            Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));
            WritePhysicalInjuryDiagnostic("qte_physical_component_missing_source", new { failure.Message });
            return;
        }
        var selection = PhysicalProducerComponentSelection(includeSibling: true);
        var component = await PreparePhysicalProducerComponentAsync(selection);
        var completed = component.Reduction.Completed!;
        Assert.Equal(2, completed.Resources.EffectBoundaryTranscript!.ResourceMutations.Count);
        var producer = QtePhysicalWoundOccurrenceProducer.Create(component.Source, selection, completed);
        var final = AcceptedMechanicsPlanner.CompleteQtePhysicalWoundOccurrenceReduction(component.Reduction, producer, component.Roots);
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction, final.Kind);
        var sealedOwner = final.Completed!;
        Assert.Same(completed.Resources, sealedOwner.Resources);
        Assert.Same(completed.Effects, sealedOwner.Effects);
        var events = sealedOwner.Input.AcceptedEvents;
        Assert.True(events["acceptedEvents"]!.AsArray().Count >= 2);
        Assert.NotEqual(completed.Input.AuthorityFingerprints.AcceptedEvents, sealedOwner.Input.AuthorityFingerprints.AcceptedEvents);
        Assert.NotEqual(completed.Input.AuthorityFingerprints.InternalInputs, sealedOwner.Input.AuthorityFingerprints.InternalInputs);
        Assert.Equal(AcceptedMechanicsPlanFingerprints.ComputePlanningInput(sealedOwner.Input), sealedOwner.InputFingerprint);
        var result = AcceptedMechanicsPlanner.CompleteAcceptedReduction(final);
        Assert.True(result.Success, JsonSerializer.Serialize(result.Issues));
        Assert.True(new AcceptedMechanicsPlanCache((_, _) => result).GetOrBuildValidated(sealedOwner.Input).Success);
        events["acceptedEvents"]![1]!["authorityId"] = "changed_sibling_authority";
        var input = sealedOwner.Input;
        var changed = new AcceptedMechanicsInput(input.SessionId, input.RequestId, input.SnapshotToken, input.Realm, input.Turn,
            events, input.ResourceCommands, input.EffectCommands, input.PendingInput, input.InternalInputs,
            input.AuthorityFingerprints with { AcceptedEvents = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
                { "qte-finalized-accepted-events-v1", WoundAcceptedTurnFingerprintWriter.CanonicalJson(events) }) },
            input.BeforeImages, input.ValidationIssues, input.PlanningContext);
        var refused = new AcceptedMechanicsPlanCache((_, _) => result).GetOrBuildValidated(changed);
        Assert.False(refused.Success);
        Assert.Contains(refused.Issues, issue => issue.Code == "accepted_mechanics_plan_fingerprint_mismatch");
        Assert.Throws<InvalidOperationException>(() => sealedOwner.WithOwnerCompanionAfterImage(
            MortalWoundOccurrenceState.StatePath, component.Roots[MortalWoundOccurrenceState.StatePath], new JsonObject()));
        Assert.False(_fs.FileExists(MortalWoundOccurrenceState.StatePath));
        WritePhysicalInjuryDiagnostic("qte_physical_component_final_seal", new
        { component.Source.RequestId, component.Source.SnapshotToken, sealedOwner.InputFingerprint, Events = input.AcceptedEvents,
            Proof = input.InternalInputs, refused.Issues });
    }

    private static QteTerminalResourceSelection PhysicalProducerComponentSelection(bool includeSibling = false)
    {
        var raw = BuildPhysicalInjuryOffer(true, 2);
        var outcome = raw["terminalOutcomes"]![0]!.AsObject();
        var response = outcome["responseFragment"]!.DeepClone().AsObject();
        if (includeSibling)
        {
            var sibling = response["resourceChanges"]![0]!.DeepClone().AsObject();
            sibling["amount"] = 1;
            sibling["eventRef"] = "turn_42:qte_terminal:1:resource:2";
            response["resourceChanges"]!.AsArray().Add(sibling);
        }
        return new QteTerminalResourceSelection(PhysicalInjurySourceTurn, raw["qteId"]!.GetValue<string>(), "impact", "brace", "success", 1,
            outcome["outcomeId"]!.GetValue<string>(), response,
            JsonSerializer.Deserialize<QteSceneService.QtePhysicalInjury>(outcome["physicalInjury"]!.ToJsonString(), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    // A bounded component seam over this fixture's genuine original source and canonical roots.
    // The reduction, source capsule and occurrence draft are issued by production factories.
    private async Task<(QtePhysicalInjurySource Source, AcceptedMechanicsPlanner.AcceptedMechanicsReduction Reduction,
        IReadOnlyDictionary<string, CanonicalBeforeImage> Roots)> PreparePhysicalProducerComponentAsync(QteTerminalResourceSelection selection)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
            [ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath]);
        Assert.True(read.Success, JsonSerializer.Serialize(read.Issues));
        var original = read.Snapshot!;
        var source = await QtePhysicalInjurySource.CaptureCurrentAsync(_fs, lease,
            original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
        var roots = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var path in QteDeferredEffectContinuation.SealedRootPaths.Concat(QtePhysicalInjurySource.WoundRootPaths)
            .Append(CanonicalResourceOwnerAuthorityComposer.AuthorityPath).Append("input/turn_request.json").Distinct(StringComparer.Ordinal))
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, path);
            roots.Add(path, new CanonicalBeforeImage(bytes is not null, bytes));
        }
        string? Read(string path) => roots[path].Bytes is { } bytes ? Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF') : null;
        var definitions = ResourceDefinitionCatalog.ParseCanonical(Read(ResourceMaterializationContract.DefinitionsPath), false).Catalog!;
        var state = ResourceStateContract.ParseCanonical(Read(ResourceMaterializationContract.StatePath), definitions, false).Ledger!;
        var history = ResourceHistoryState.ParseCanonical(Read(ResourceMaterializationContract.HistoryPath), definitions, false).History!;
        var owners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions,
            path => Task.FromResult(Read(path)), state, history, CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(owners.IsValid, JsonSerializer.Serialize(owners.Issues));
        var draft = QteTerminalResourceOutcome.BuildPlanningDraft(selection, definitions, state, history, owners.Authority!);
        Assert.True(draft.IsValid, JsonSerializer.Serialize(draft.Issues));
        Assert.NotNull(draft.RegisteredOutcome);
        var commands = ResourceAcceptedTurnInputComposer.Parse(null);
        var effects = EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot();
        var events = new JsonObject { ["acceptedEvents"] = new JsonArray() };
        var pending = new JsonObject();
        var proof = new JsonObject { ["componentSelection"] = JsonSerializer.SerializeToNode(selection.PhysicalInjury, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) };
        string Hash(string domain, string? value) => WoundAcceptedTurnFingerprintWriter.Compute(new string?[] { domain, value });
        var identity = JsonNode.Parse(Read(EffectAcceptedTurnPlan.IdentityIndexPath) ?? "{\"schemaVersion\":1,\"entries\":[]}")!.AsObject();
        var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
            Hash("definitions", definitions.ToCanonicalJson()), owners.Authority!.Fingerprint, state.Fingerprint, history.Fingerprint,
            Hash("effect-sources", "absent"), Hash("effect-targets", "absent"), Hash("effect-carriers", "absent"),
            Hash("effect-index", Read(EffectAcceptedTurnPlan.IdentityIndexPath)), Hash("events", events.ToJsonString()),
            Hash("commands", commands.Root.ToJsonString() + effects.ToJsonString()), Hash("pending", pending.ToJsonString()),
            Hash("internal", proof.ToJsonString()), Hash("wounds", string.Join("\n", QtePhysicalInjurySource.WoundRootPaths.Select(path => roots[path].Fingerprint))),
            Hash("wound-index", Read(WoundIdentityState.StatePath)), Hash("wound-history", Read(WoundHistoryState.HistoryPath)));
        var context = new AcceptedMechanicsPlanningContext(definitions.ToCanonicalRoot(), definitions, state, history, owners.Authority!,
            draft.Sources!, commands, identity, null, registeredSystemOutcomes: [draft.RegisteredOutcome!],
            executionSequenceOffset: draft.ExecutionSequenceOffset);
        var input = new AcceptedMechanicsInput(source.SessionId, source.RequestId, source.SnapshotToken, "mortal_world", source.Turn,
            events, commands.Root, effects, pending, proof, fingerprints, roots, [], context);
        var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(input, AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input));
        Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction, reduction.Kind);
        return (source, reduction, roots);
    }
}
