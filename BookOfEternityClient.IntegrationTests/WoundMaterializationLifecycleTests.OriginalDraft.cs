using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    /// <summary>
    /// Verifies sequential signed Mortal worsening against the current draft generation and its retained completion authority.
    /// </summary>
    /// <param name="mode">
    /// The positive, future-work, or authority-corruption contour to exercise.
    /// </param>
    [Theory]
    [InlineData("plain")]
    [InlineData("future_resource")]
    [InlineData("future_descendant")]
    [InlineData("future_reaction")]
    [InlineData("corrupt_retirement")]
    [InlineData("corrupt_create_anchor")]
    [InlineData("corrupt_carrier")]
    [InlineData("wrong_original_stamp")]
    [InlineData("composite_routing_epoch")]
    [InlineData("reordered_insertion_chain")]
    [InlineData("foreign_draft_owner_version")]
    [InlineData("unrelated")]
    public async Task OriginalMortalDraft_SequentialSignedWorseningUsesCurrentGeneration(string mode)
    {
        await using var context = await CreatePlayerContextAsync();
        if (mode == "unrelated")
            await SeedOriginalMortalObserverAsync(context, "resource_spent");
        if (mode == "future_descendant")
            await SeedOriginalMortalObserverAsync(context, "resource_damaged", resourceOutput: true);
        var hasFutureReaction = mode is "future_reaction" or "corrupt_retirement";
        var (original, authorities) = await PrepareSignedMortalWorseningBatchAsync(context,
            hasFutureReaction);
        var commands = new JsonObject { ["resourceChanges"] = new JsonArray() };
        foreach (var ordinal in Enumerable.Range(1, mode is "future_resource" or "future_descendant" ? 3 : 2))
            commands["resourceChanges"]!.AsArray().Add(new JsonObject
            {
                ["operation"] = hasFutureReaction && ordinal == 2 ? "gain" : "spend",
                ["resourceKey"] = "energy", ["amount"] = 1,
                ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                ["eventRef"] = $"turn_43:resource:{ordinal}", ["reason"] = "Принятый расход сил."
            });
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath, commands.ToJsonString());
        var canonical = new Dictionary<string, byte[]?>();
        foreach (var path in new[] { WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath, EffectCarrierCatalog.PlayerPath, EffectAcceptedTurnPlan.IdentityIndexPath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath })
            canonical.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureMortalOriginalTurnAsync(lease);
        Assert.True(captured.Issues.Count == 0, Describe(captured.Issues));
        using var capture = Assert.IsType<ValidationService.MortalOriginalTurnCapture>(captured.Capture);
        var first = await capture.AdvanceNextResourceBoundaryAsync(lease);
        Assert.Empty(first.Issues);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(first.Step!.Checkpoint);
        Assert.Equal(mode == "unrelated" ? 1 : 0,
            checkpoint.EffectPrefix.AcceptedActivations.Count);
        Assert.Empty(checkpoint.EffectPrefix.ReleasedReactions);
        var ambiguous = await capture.MaterializeWoundAsync(lease, checkpoint);
        Assert.Contains(ambiguous.Issues, issue => issue.Code == "mortal_wound_selection_required");
        Assert.True(capture.IsCurrentOwner);
        var unknown = await Select(checkpoint, "unknown_occurrence");
        Assert.Contains(unknown.Issues, issue => issue.Code == "mortal_wound_selection_unknown");
        Assert.True(capture.IsCurrentOwner);
        var untouchedDraft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(ReadOriginalMortalField(capture, "_effects"));
        Assert.Null(ReadOriginalMortalField(untouchedDraft, "identityRoot"));
        Assert.Empty(untouchedDraft.Phases);
        var second = await Select(checkpoint, authorities[0].Opportunity.PublicRef);
        Assert.True(second.Issues.Count == 0, Describe(second.Issues));
        Assert.Equal(original.WoundId, second.Wound!.WoundId);
        Assert.Equal("II", second.Wound.Severity.Value);
        Assert.Same(second, await Select(checkpoint, authorities[0].Opportunity.PublicRef));
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(
            ReadOriginalMortalField(capture, "_effects"));
        var secondInsertion = Assert.IsType<
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion>(
            ReadOriginalMortalField(draft, "_lastWoundInsertion"));
        var next = await capture.AdvanceNextResourceBoundaryAsync(lease);
        Assert.Empty(next.Issues);
        var after = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(next.Step!.Checkpoint);
        var secondRoot = Assert.Single(second.Wound.Consequences.OwnedEffectSources.RootBindings).EffectId;
        Assert.Equal(hasFutureReaction ? 0 : 1,
            after.EffectPrefix.AcceptedActivations.Count(value => value.Activation.Stamp.Identity.EffectId == secondRoot));
        Assert.Empty(after.EffectPrefix.ReleasedReactions);
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(ReadOriginalMortalField(capture, "_resources"));
        var oldPrepared = ReadOriginalMortalPrepared(resources);
        var oldCandidates = ReadOriginalMortalCandidates(resources);
        var sharedSources = Array.Empty<ResourceMutationSourceExport>();
        if (mode is "future_resource" or "future_descendant")
        {
            var historicalSources = oldCandidates.Where(value =>
                    value.Activation.Identity.EffectId == secondRoot &&
                    value.Producer?.EventRef == "turn_43:resource:2")
                .SelectMany(value => value.Origin.SourceExports);
            var futureSources = oldCandidates.Where(value =>
                    value.Activation.Identity.EffectId == secondRoot &&
                    value.Producer?.EventRef == "turn_43:resource:3")
                .SelectMany(value => value.Origin.SourceExports);
            sharedSources = historicalSources.Intersect(futureSources).ToArray();
            Assert.NotEmpty(sharedSources);
        }
        var removedKeys = oldCandidates.Where(value => value.Activation.Identity.EffectId == secondRoot &&
            value.Producer?.EventRef == "turn_43:resource:3").SelectMany(value => value.PlannedMutationKeys).ToHashSet();
        foreach (var candidate in oldCandidates.Where(value => value.Producer != null && removedKeys.Contains(value.Producer)))
            removedKeys.UnionWith(candidate.PlannedMutationKeys);
        var repeated = await Select(after, authorities[0].Opportunity.PublicRef);
        Assert.Contains(repeated.Issues, issue => issue.Code == "mortal_wound_selection_already_applied");
        var third = await Select(after, authorities[1].Opportunity.PublicRef);
        Assert.True(third.Issues.Count == 0, Describe(third.Issues));
        Assert.Equal(original.WoundId, third.Wound!.WoundId);
        Assert.Equal("III", third.Wound.Severity.Value);
        Assert.Equal(3, third.Wound.LastTransition.Ordinal);
        var thirdRoot = Assert.Single(third.Wound.Consequences.OwnedEffectSources.RootBindings).EffectId;
        var thirdInsertion = Assert.IsType<
            EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion>(
            ReadOriginalMortalField(draft, "_lastWoundInsertion"));
        Assert.NotSame(secondInsertion, thirdInsertion);
        Assert.True(secondInsertion.VersionAfter < thirdInsertion.VersionAfter);
        var identity = Assert.IsType<EffectIdentityHistoryOwner>(ReadOriginalMortalField(draft, "identityRoot"));
        var history = EffectIdentityState.Parse(System.Text.Json.JsonSerializer.SerializeToElement(identity.ReadSnapshot()),
            EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.Empty(history.Issues);
        Assert.True(history.State!.TryGetEntry(secondRoot, out var retired));
        Assert.Equal(hasFutureReaction ? new[] { "create", "expire" } : new[] { "create", "trigger", "expire" },
            retired.Transitions.Select(value => value.Kind).ToArray());
        if (mode == "unrelated")
        {
            Assert.True(history.State.TryGetEntry(EffectMaterializationTestFixture.EffectId, out var foreign));
            Assert.Single(foreign.Transitions);
            var arbiter = Assert.IsType<AcceptedEffectUseArbiter>(ReadOriginalMortalField(ReadOriginalMortalField(resources, "_state")!, "arbiter"));
            Assert.True(arbiter.TryGetRemainingUses(EffectMaterializationTestFixture.EffectId, out var remaining));
            Assert.Equal(0, remaining);
        }
        var installedPrepared = ReadOriginalMortalPrepared(resources);
        Assert.All(oldPrepared.Where(value => !removedKeys.Contains(ReadPreparedIntent(value).Key)), value =>
            Assert.Contains(installedPrepared, retained => ReferenceEquals(value, retained)));
        Assert.All(oldPrepared.Where(value => removedKeys.Contains(ReadPreparedIntent(value).Key)), value =>
            Assert.DoesNotContain(installedPrepared, retained => ReferenceEquals(value, retained)));
        var installedCandidates = ReadOriginalMortalCandidates(resources);
        Assert.DoesNotContain(installedCandidates, value => value.Activation.Identity.EffectId == secondRoot &&
            value.Producer?.EventRef == "turn_43:resource:3");
        Assert.All(oldCandidates.Where(value => value.Producer?.EventRef != "turn_43:resource:3" &&
            (value.Producer == null || !removedKeys.Contains(value.Producer))), value =>
            Assert.Contains(installedCandidates, retained => ReferenceEquals(value, retained)));
        Assert.All(sharedSources, source => Assert.Contains(source, ReadOriginalMortalSources(resources)));
        var allocations = identity.AllocationCount;
        var writes = identity.WriteCount;
        Assert.Same(third, await Select(after, authorities[1].Opportunity.PublicRef));
        Assert.Equal(allocations, identity.AllocationCount);
        Assert.Equal(writes, identity.WriteCount);
        Assert.Equal(installedPrepared, ReadOriginalMortalPrepared(resources), ReferenceEqualityComparer.Instance);
        if (mode is "future_resource" or "future_descendant")
        {
            var final = await capture.AdvanceNextResourceBoundaryAsync(lease);
            Assert.Empty(final.Issues);
            var finalCheckpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(final.Step!.Checkpoint);
            Assert.Single(finalCheckpoint.EffectPrefix.AcceptedActivations, value => value.Activation.Stamp.Identity.EffectId == thirdRoot);
            Assert.Single(finalCheckpoint.EffectPrefix.AcceptedActivations, value => value.Activation.Stamp.Identity.EffectId == secondRoot);
            if (mode == "future_descendant")
            {
                Assert.Equal(2, finalCheckpoint.EffectPrefix.AcceptedActivations.Count(value =>
                    value.Activation.Stamp.Identity.EffectId == EffectMaterializationTestFixture.EffectId));
                var arbiter = Assert.IsType<AcceptedEffectUseArbiter>(ReadOriginalMortalField(ReadOriginalMortalField(resources, "_state")!, "arbiter"));
                Assert.True(arbiter.TryGetRemainingUses(EffectMaterializationTestFixture.EffectId, out var remaining));
                Assert.Equal(0, remaining);
            }
        }
        if (mode == "corrupt_retirement")
        {
            var ownedRoot = Assert.IsType<JsonObject>(ReadOriginalMortalField(identity, "_root"));
            var ownedSecond = Assert.Single(ownedRoot["entries"]!.AsArray().OfType<JsonObject>(), value =>
                value["effectId"]?.GetValue<string>() == secondRoot);
            var ownedRetirement = Assert.Single(ownedSecond["transitions"]!.AsArray().OfType<JsonObject>(), value =>
                value["kind"]?.GetValue<string>() == "expire");
            ownedRetirement["eventRef"] = "turn_43:tampered_owned_retirement";
            var failedCompletion = await capture.CompleteEffectsAsync(lease);
            Assert.False(failedCompletion.Success);
            Assert.Contains(failedCompletion.Issues, issue =>
                issue.Code == "spiritual_wound_insertion_agreement_mismatch");
            Assert.Equal(allocations, identity.AllocationCount);
            Assert.Equal(writes, identity.WriteCount);
            Assert.False(capture.IsCurrentOwner);
        }
        if (mode is "corrupt_create_anchor" or "corrupt_carrier")
        {
            var workspace = ReadOriginalMortalField(draft, "workspace")!;
            var ownedPlayer = Assert.IsType<JsonObject>(ReadOriginalMortalField(workspace, "_player"));
            var ownedEffect = Assert.Single(ownedPlayer["activeEffects"]!.AsArray().OfType<JsonObject>(), value =>
                value["effectId"]?.GetValue<string>() == thirdRoot);
            if (mode == "corrupt_create_anchor")
            {
                var ownedRoot = Assert.IsType<JsonObject>(ReadOriginalMortalField(identity, "_root"));
                var ownedThird = Assert.Single(ownedRoot["entries"]!.AsArray().OfType<JsonObject>(), value =>
                    value["effectId"]?.GetValue<string>() == thirdRoot);
                var ownedCreate = Assert.Single(ownedThird["transitions"]!.AsArray().OfType<JsonObject>(), value =>
                    value["kind"]?.GetValue<string>() == "create");
                const string changedTransitionId = "transition_tampered_create_anchor";
                ownedCreate["transitionId"] = changedTransitionId;
                ownedEffect["chronology"]!["lastTransitionId"] = changedTransitionId;
            }
            else
            {
                ownedEffect["components"]![0]!["payload"]!["amount"] = 4;
            }
            var failedCompletion = await capture.CompleteEffectsAsync(lease);
            Assert.False(failedCompletion.Success);
            Assert.Contains(failedCompletion.Issues, issue =>
                issue.Code == "spiritual_wound_insertion_agreement_mismatch");
            Assert.Equal(allocations, identity.AllocationCount);
            Assert.Equal(writes, identity.WriteCount);
            Assert.False(capture.IsCurrentOwner);
        }
        if (mode == "wrong_original_stamp")
        {
            var drained = resources.Result ?? resources.Drain();
            Assert.True(drained.IsValid, Describe(drained.Issues));
            var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(
                drained.EffectBoundaryTranscript);
            Assert.True(transcript.IsComplete);
            var authorityField = transcript.GetType().GetField(
                "_planAuthority",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(authorityField);
            var originalAuthority = Assert.IsType<EffectAcceptedPlanAuthorityStamp>(
                authorityField.GetValue(transcript));
            var changedAuthority = originalAuthority with
            {
                InputFingerprint =
                    "sha256:ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"
            };
            Assert.NotEqual(
                originalAuthority.InputFingerprint,
                changedAuthority.InputFingerprint);
            var transcriptFingerprint = transcript.Fingerprint;
            var allocationsBeforeCompletion = identity.AllocationCount;
            var writesBeforeCompletion = identity.WriteCount;
            var workspace = ReadOriginalMortalField(draft, "workspace")!;
            var carrierEditsBeforeCompletion = Assert.IsType<int>(workspace.GetType()
                .GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace));
            var phasesBeforeCompletion = draft.Phases.ToArray();
            var stateBeforeCompletion = CaptureOriginalMortalDraftState(
                draft,
                identity);

            authorityField.SetValue(transcript, changedAuthority);
            var retainedChangedAuthority = Assert.IsType<EffectAcceptedPlanAuthorityStamp>(
                transcript.PlanAuthority);
            Assert.Equal(changedAuthority.InputFingerprint, retainedChangedAuthority.InputFingerprint);
            Assert.Equal(originalAuthority.CarrierAuthorityFingerprint,
                retainedChangedAuthority.CarrierAuthorityFingerprint);
            Assert.Equal(originalAuthority.SourceAuthorityFingerprint,
                retainedChangedAuthority.SourceAuthorityFingerprint);
            Assert.Equal(originalAuthority.TargetAuthorityFingerprint,
                retainedChangedAuthority.TargetAuthorityFingerprint);
            Assert.Equal(originalAuthority.SkillScopeAuthorityFingerprint,
                retainedChangedAuthority.SkillScopeAuthorityFingerprint);
            Assert.Equal(transcriptFingerprint, transcript.Fingerprint);

            var failedCompletion = await capture.CompleteEffectsAsync(lease);

            Assert.False(failedCompletion.Success);
            Assert.Null(failedCompletion.Plan);
            Assert.Contains(failedCompletion.Issues, issue =>
                issue.Code == "effect_boundary_transcript_plan_mismatch");
            Assert.Equal(allocationsBeforeCompletion, identity.AllocationCount);
            Assert.Equal(writesBeforeCompletion, identity.WriteCount);
            Assert.Equal(carrierEditsBeforeCompletion, Assert.IsType<int>(workspace.GetType()
                .GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace)));
            Assert.Equal(phasesBeforeCompletion, draft.Phases);
            Assert.True(JsonNode.DeepEquals(
                stateBeforeCompletion,
                CaptureOriginalMortalDraftState(draft, identity)));
            Assert.False(capture.IsCurrentOwner);
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                capture.CompleteEffectsAsync(lease));
        }
        if (mode == "composite_routing_epoch")
        {
            var routing = Assert.IsType<EffectAcceptedTurnPlanner.BaseResourceRouting>(resources.Routing);
            var preparations = (System.Collections.IDictionary)ReadOriginalMortalField(
                routing,
                "_woundPreparations")!;
            var secondPreparation = preparations[secondInsertion];
            var thirdPreparation = preparations[thirdInsertion];
            Assert.NotNull(secondPreparation);
            Assert.NotNull(thirdPreparation);
            Assert.NotSame(secondPreparation, thirdPreparation);
            Assert.Same(thirdPreparation, ReadOriginalMortalField(routing, "_currentWoundRouting"));
            Assert.Same(thirdInsertion, ReadOriginalMortalField(routing, "_currentWoundInsertion"));
            var workspace = ReadOriginalMortalField(draft, "workspace")!;
            var carrierEditsBeforeCompletion = Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace));
            var phasesBeforeCompletion = draft.Phases.ToArray();
            var woundReadVersionBeforeCompletion = draft.WoundReadVersion;
            var dependencyCutVersionBeforeCompletion = draft.DependencyCutVersion;
            var triggerJournal = (System.Collections.IDictionary)ReadOriginalMortalField(
                draft,
                "_woundTriggerJournal")!;
            var reactionJournal = (System.Collections.IDictionary)ReadOriginalMortalField(
                draft,
                "_woundReactionJournal")!;
            var triggerJournalCount = triggerJournal.Count;
            var reactionJournalCount = reactionJournal.Count;
            var stateBeforeCompletion = CaptureOriginalMortalDraftState(draft, identity);
            var lastInsertionField = draft.GetType().GetField(
                "_lastWoundInsertion",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var currentInsertionField = routing.GetType().GetField(
                "_currentWoundInsertion",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(lastInsertionField);
            Assert.NotNull(currentInsertionField);

            lastInsertionField.SetValue(draft, secondInsertion);
            currentInsertionField.SetValue(routing, secondInsertion);

            Assert.Same(secondInsertion, ReadOriginalMortalField(draft, "_lastWoundInsertion"));
            Assert.Same(secondInsertion, ReadOriginalMortalField(routing, "_currentWoundInsertion"));
            Assert.Same(thirdPreparation, ReadOriginalMortalField(routing, "_currentWoundRouting"));
            Assert.Same(secondPreparation, preparations[secondInsertion]);
            Assert.Same(thirdPreparation, preparations[thirdInsertion]);
            var failedCompletion = await capture.CompleteEffectsAsync(lease);

            Assert.False(failedCompletion.Success);
            Assert.Null(failedCompletion.Plan);
            Assert.Contains(failedCompletion.Issues, issue =>
                issue.Code == "spiritual_wound_routing_required");
            Assert.Equal(allocations, identity.AllocationCount);
            Assert.Equal(writes, identity.WriteCount);
            Assert.Equal(carrierEditsBeforeCompletion, Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace)));
            Assert.Equal(phasesBeforeCompletion, draft.Phases);
            Assert.Equal(woundReadVersionBeforeCompletion, draft.WoundReadVersion);
            Assert.Equal(dependencyCutVersionBeforeCompletion, draft.DependencyCutVersion);
            Assert.Equal(triggerJournalCount, triggerJournal.Count);
            Assert.Equal(reactionJournalCount, reactionJournal.Count);
            Assert.True(JsonNode.DeepEquals(
                stateBeforeCompletion,
                CaptureOriginalMortalDraftState(draft, identity)));
            Assert.False(capture.IsCurrentOwner);
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                capture.CompleteEffectsAsync(lease));
        }
        if (mode == "reordered_insertion_chain")
        {
            var secondBefore = ReadOriginalMortalField(secondInsertion, "_before")!;
            var thirdBefore = ReadOriginalMortalField(thirdInsertion, "_before")!;
            Assert.Null(ReadOriginalMortalField(secondBefore, "<RetirementHistory>k__BackingField"));
            Assert.Same(secondInsertion, ReadOriginalMortalField(
                thirdBefore,
                "<RetirementHistory>k__BackingField"));
            var workspace = ReadOriginalMortalField(draft, "workspace")!;
            var carrierEditsBeforeCompletion = Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace));
            var phasesBeforeCompletion = draft.Phases.ToArray();
            var woundReadVersionBeforeCompletion = draft.WoundReadVersion;
            var dependencyCutVersionBeforeCompletion = draft.DependencyCutVersion;
            var triggerJournal = (System.Collections.IDictionary)ReadOriginalMortalField(
                draft,
                "_woundTriggerJournal")!;
            var reactionJournal = (System.Collections.IDictionary)ReadOriginalMortalField(
                draft,
                "_woundReactionJournal")!;
            var triggerJournalCount = triggerJournal.Count;
            var reactionJournalCount = reactionJournal.Count;
            var stateBeforeCompletion = CaptureOriginalMortalDraftState(draft, identity);
            var predecessorField = secondBefore.GetType().GetField(
                "<RetirementHistory>k__BackingField",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(predecessorField);

            predecessorField.SetValue(secondBefore, thirdInsertion);

            Assert.Same(thirdInsertion, ReadOriginalMortalField(
                secondBefore,
                "<RetirementHistory>k__BackingField"));
            Assert.Same(secondInsertion, ReadOriginalMortalField(
                thirdBefore,
                "<RetirementHistory>k__BackingField"));
            Assert.Same(thirdInsertion, ReadOriginalMortalField(draft, "_lastWoundInsertion"));
            var routing = Assert.IsType<EffectAcceptedTurnPlanner.BaseResourceRouting>(resources.Routing);
            Assert.Same(thirdInsertion, ReadOriginalMortalField(routing, "_currentWoundInsertion"));
            var failedCompletion = await capture.CompleteEffectsAsync(lease);

            Assert.False(failedCompletion.Success);
            Assert.Null(failedCompletion.Plan);
            Assert.Contains(failedCompletion.Issues, issue =>
                issue.Code == "spiritual_wound_insertion_agreement_mismatch");
            Assert.Equal(allocations, identity.AllocationCount);
            Assert.Equal(writes, identity.WriteCount);
            Assert.Equal(carrierEditsBeforeCompletion, Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace)));
            Assert.Equal(phasesBeforeCompletion, draft.Phases);
            Assert.Equal(woundReadVersionBeforeCompletion, draft.WoundReadVersion);
            Assert.Equal(dependencyCutVersionBeforeCompletion, draft.DependencyCutVersion);
            Assert.Equal(triggerJournalCount, triggerJournal.Count);
            Assert.Equal(reactionJournalCount, reactionJournal.Count);
            Assert.True(JsonNode.DeepEquals(
                stateBeforeCompletion,
                CaptureOriginalMortalDraftState(draft, identity)));
            Assert.False(capture.IsCurrentOwner);
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                capture.CompleteEffectsAsync(lease));
        }
        if (mode == "foreign_draft_owner_version")
        {
            var acceptedPlan = Assert.IsType<EffectAcceptedTurnPlan>(
                ReadOriginalMortalField(draft, "plan"));
            using var foreignDraft = EffectAcceptedTurnPlanner.EffectAcceptedDraft.Begin(
                acceptedPlan,
                new EffectIdentityFactory());
            Assert.NotSame(draft, foreignDraft);
            Assert.Same(acceptedPlan, ReadOriginalMortalField(foreignDraft, "plan"));
            var foreignVersionField = foreignDraft.GetType().GetField(
                "_woundVersion",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(foreignVersionField);
            foreignVersionField.SetValue(foreignDraft, thirdInsertion.VersionAfter);
            Assert.Equal(thirdInsertion.VersionAfter, Assert.IsType<long>(ReadOriginalMortalField(
                foreignDraft,
                "_woundVersion")));
            var foreignInsertions = (System.Collections.IDictionary)ReadOriginalMortalField(
                foreignDraft,
                "_woundInsertions")!;
            Assert.Empty(foreignInsertions.Values.Cast<object>());
            var thirdBefore = ReadOriginalMortalField(thirdInsertion, "_before")!;
            Assert.Same(draft, ReadOriginalMortalField(thirdBefore, "_owner"));
            var insertionOwnerField = thirdInsertion.GetType().GetField(
                "_owner",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(insertionOwnerField);
            var workspace = ReadOriginalMortalField(draft, "workspace")!;
            var carrierEditsBeforeCompletion = Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace));
            var phasesBeforeCompletion = draft.Phases.ToArray();
            var woundReadVersionBeforeCompletion = draft.WoundReadVersion;
            var dependencyCutVersionBeforeCompletion = draft.DependencyCutVersion;
            var triggerJournal = (System.Collections.IDictionary)ReadOriginalMortalField(
                draft,
                "_woundTriggerJournal")!;
            var reactionJournal = (System.Collections.IDictionary)ReadOriginalMortalField(
                draft,
                "_woundReactionJournal")!;
            var triggerJournalCount = triggerJournal.Count;
            var reactionJournalCount = reactionJournal.Count;
            var stateBeforeCompletion = CaptureOriginalMortalDraftState(draft, identity);

            insertionOwnerField.SetValue(thirdInsertion, foreignDraft);

            Assert.Same(foreignDraft, ReadOriginalMortalField(thirdInsertion, "_owner"));
            Assert.Same(draft, ReadOriginalMortalField(thirdBefore, "_owner"));
            Assert.Same(thirdInsertion, ReadOriginalMortalField(draft, "_lastWoundInsertion"));
            var routing = Assert.IsType<EffectAcceptedTurnPlanner.BaseResourceRouting>(resources.Routing);
            Assert.Same(thirdInsertion, ReadOriginalMortalField(routing, "_currentWoundInsertion"));
            Assert.Empty(foreignInsertions.Values.Cast<object>());
            var failedCompletion = await capture.CompleteEffectsAsync(lease);

            Assert.False(failedCompletion.Success);
            Assert.Null(failedCompletion.Plan);
            Assert.Contains(failedCompletion.Issues, issue =>
                issue.Code == "spiritual_wound_insertion_agreement_mismatch");
            Assert.Equal(allocations, identity.AllocationCount);
            Assert.Equal(writes, identity.WriteCount);
            Assert.Equal(carrierEditsBeforeCompletion, Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace)));
            Assert.Equal(phasesBeforeCompletion, draft.Phases);
            Assert.Equal(woundReadVersionBeforeCompletion, draft.WoundReadVersion);
            Assert.Equal(dependencyCutVersionBeforeCompletion, draft.DependencyCutVersion);
            Assert.Equal(triggerJournalCount, triggerJournal.Count);
            Assert.Equal(reactionJournalCount, reactionJournal.Count);
            Assert.True(JsonNode.DeepEquals(
                stateBeforeCompletion,
                CaptureOriginalMortalDraftState(draft, identity)));
            Assert.False(capture.IsCurrentOwner);
            await Assert.ThrowsAsync<ObjectDisposedException>(() =>
                capture.CompleteEffectsAsync(lease));
        }
        if (mode is "plain" or "future_reaction" or "unrelated")
        {
            var completed = await capture.CompleteEffectsAsync(lease);
            Assert.True(completed.Success, Describe(completed.Issues));
            var completedPlan = Assert.IsType<EffectAcceptedTurnPlan>(completed.Plan);
            Assert.Equal(completedPlan.SourceAuthority.CanonicalFingerprint,
                completedPlan.SourceAuthorityFingerprint);
            var completedCatalog = EffectCarrierCatalog.Build(completedPlan.ResourceTriggerCarriers);
            Assert.Empty(completedCatalog.Issues);
            Assert.True(completedCatalog.TryResolveOne(thirdRoot, out _));
            Assert.False(completedCatalog.TryResolveOne(secondRoot, out _));
            var completedIdentity = EffectIdentityState.Parse(
                System.Text.Json.JsonSerializer.SerializeToElement(completedPlan.IdentityIndexAfterImage),
                EffectAcceptedTurnPlan.IdentityIndexPath);
            Assert.Empty(completedIdentity.Issues);
            Assert.Equal("expired", completedIdentity.State!.Entries.Single(value =>
                value.EffectId == secondRoot).State);
            Assert.Equal("active", completedIdentity.State.Entries.Single(value =>
                value.EffectId == thirdRoot).State);
            var currentRoot = Assert.Single(completedPlan.WoundApplicationRootEffectBindings, value =>
                value.EffectId == thirdRoot);
            Assert.DoesNotContain(completedPlan.WoundApplicationRootEffectBindings, value =>
                value.EffectId == secondRoot);
            var group = new EffectIdentitySourceGroup(third.Wound.Owner.Realm, "wound", third.Wound.WoundId);
            Assert.True(completedPlan.SourceAuthority.TryResolveWoundGroup(group, out var currentSources));
            Assert.Contains(currentSources.ApplicationRootLineage, value =>
                value.ApplicationRef == currentRoot.ApplicationRef && value.EffectId == null);
            if (mode == "unrelated")
            {
                Assert.False(completedCatalog.TryResolveOne(EffectMaterializationTestFixture.EffectId, out _));
                var completedForeign = completedIdentity.State.Entries.Single(value =>
                    value.EffectId == EffectMaterializationTestFixture.EffectId);
                Assert.Equal("expired", completedForeign.State);
                Assert.Equal(new[] { "create", "consume", "expire" }, completedForeign.Transitions
                    .Select(value => value.Kind).ToArray());
                Assert.Equal(allocations + 3, identity.AllocationCount);
                Assert.Equal(writes + 3, identity.WriteCount);
            }
            else
            {
                Assert.Equal(allocations + 1, identity.AllocationCount);
                Assert.Equal(writes + 1, identity.WriteCount);
            }
            Assert.Equal(6, draft.Phases.Count);
            var finalLifetime = Assert.Single(draft.Phases, receipt =>
                receipt.Phase == EffectAcceptedTurnPlanner.EffectDraftPhase.FinalLifetime);
            Assert.Single(finalLifetime.IdentityWrites);
            Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
        }
        foreach (var pair in canonical)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));

        static ResourceMutationIntent ReadPreparedIntent(object value) =>
            (ResourceMutationIntent)value.GetType().GetProperty("Intent",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)!.GetValue(value)!;

        Task<ValidationService.OriginalWoundMaterializationResult> Select(
            AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint boundary, string opportunityRef) =>
            capture.MaterializeWoundAsync(lease, boundary, opportunityRef);
    }

    /// <summary>
    /// Verifies that a worsening cut preserves accepted work from an unrelated effect and that
    /// common completion folds its terminal reaction against any last-use expiration exactly once.
    /// </summary>
    /// <param name="unrelatedTerminalKind">
    /// Optional terminal reaction kind for the unrelated observer. A <see langword="null"/> value
    /// omits the observer and exercises only the wound-owned generation.
    /// </param>
    /// <param name="consumingReplacementTarget">
    /// <see langword="true"/> makes the wound replacement consume its target and exercises the
    /// retained-reaction agreement failure contour; otherwise, completion remains available.
    /// </param>
    /// <param name="observerUses">
    /// Initial and remaining use count for the unrelated observer. A value of one makes the
    /// accepted activation expire the observer before its terminal reaction is globally folded.
    /// </param>
    [Theory]
    [InlineData(null, false, 2)]
    [InlineData("remove", false, 2)]
    [InlineData("suspend", false, 2)]
    [InlineData("remove", false, 1)]
    [InlineData("suspend", false, 1)]
    [InlineData(null, true, 2)]
    public async Task OriginalMortalDraft_WorseningMaterializesReleasedOwnedDescendantBeforeRetiringGeneration(
        string? unrelatedTerminalKind, bool consumingReplacementTarget, int observerUses)
    {
        await using var context = await CreatePlayerContextAsync();
        if (unrelatedTerminalKind != null)
            await SeedOriginalMortalObserverAsync(context, "resource_gained",
                reactionResultKind: unrelatedTerminalKind, remainingUses: observerUses);
        var creation = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 3);
        var initialProposal = CreateRepairRoundtripProposal("treatment");
        ConfigureMortalReactionProposal(initialProposal, "III", consumingReplacementTarget);
        var creationDecision = Decision("materialize", initialProposal);
        creationDecision["opportunityRef"] = creation.Opportunity.PublicRef;
        var created = WoundResponseInputComposer.Compose(creation.Binding, new[] { creation.Opportunity },
            Response(creationDecision).WoundDecisions, AcquisitionNarration,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(created.Success, Describe(created.Issues));
        await PublishAsync(context, created.CommandRoot!);
        var player = (await context.ReadJsonAsync(WoundCarrierCatalog.PlayerPath))!.AsObject();
        var parsed = WoundMaterializationContract.Parse(player["activeWounds"]![0]!.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var original = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var worsening = Assert.Single(await CreateSignedAuthorityBatchAsync(context, 4, null, null, 43, 1, original));
        var worseningProposal = CreateRepairRoundtripProposal("treatment");
        worseningProposal["severity"] = "IV";
        worseningProposal["consequenceDefinitions"]![0]!["definition"]!["triggers"]![0]!["eventType"] =
            "resource_spent";
        var worseningDecision = Decision("materialize", worseningProposal);
        worseningDecision["opportunityRef"] = worsening.Opportunity.PublicRef;
        worseningDecision["woundRef"] = "forearm_retrauma_reaction";
        var selected = WoundResponseInputComposer.Compose(worsening.Binding, new[] { worsening.Opportunity },
            new[] { ToElement(worseningDecision) }, AcquisitionNarration,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(selected.Success, Describe(selected.Issues));
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath,
            selected.CommandRoot!.ToJsonString());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath, new JsonObject
        {
            ["resourceChanges"] = new JsonArray(new JsonObject
            {
                ["operation"] = "gain", ["resourceKey"] = "energy", ["amount"] = 1,
                ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                ["eventRef"] = "turn_43:resource:1", ["reason"] = "Принятое восстановление сил."
            })
        }.ToJsonString());
        var canonical = new Dictionary<string, byte[]?>();
        foreach (var path in new[] { WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath, EffectCarrierCatalog.PlayerPath, EffectAcceptedTurnPlan.IdentityIndexPath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath })
            canonical.Add(path, await context.FileSystem.ReadFileBytesAsync(path));

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureMortalOriginalTurnAsync(lease);
        Assert.True(captured.Issues.Count == 0, Describe(captured.Issues));
        using var capture = Assert.IsType<ValidationService.MortalOriginalTurnCapture>(captured.Capture);
        var advanced = await capture.AdvanceNextResourceBoundaryAsync(lease);
        Assert.Empty(advanced.Issues);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(advanced.Step!.Checkpoint);
        var reactionRoot = original.Consequences.OwnedEffectSources.RootBindings.Single(value =>
            value.DefinitionKey == "wound_reaction_root");
        Assert.Single(checkpoint.EffectPrefix.AcceptedActivations, value =>
            value.Activation.Stamp.Identity.EffectId == reactionRoot.EffectId);
        Assert.Equal(unrelatedTerminalKind != null ? 2 : 1, checkpoint.EffectPrefix.ReleasedReactions.Count);
        var result = await capture.MaterializeWoundAsync(lease, checkpoint);
        Assert.True(result.Issues.Count == 0, Describe(result.Issues));
        Assert.Equal("IV", Assert.IsType<WoundMaterializationEnvelope>(result.Wound).Severity.Value);
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(ReadOriginalMortalField(capture, "_effects"));
        var identity = Assert.IsType<EffectIdentityHistoryOwner>(ReadOriginalMortalField(draft, "identityRoot"));
        var allocations = identity.AllocationCount;
        var writes = identity.WriteCount;
        Assert.Equal(1, draft.DependencyCutVersion);
        Assert.Single(((System.Collections.IDictionary)ReadOriginalMortalField(draft,
            "_woundReactionJournal")!).Values.Cast<object>());
        var history = EffectIdentityState.Parse(System.Text.Json.JsonSerializer.SerializeToElement(identity.ReadSnapshot()),
            EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.Empty(history.Issues);
        Assert.True(history.State!.TryGetEntry(reactionRoot.EffectId, out var retiredReaction));
        Assert.Equal("expired", retiredReaction.State);
        Assert.Contains(retiredReaction.Transitions, transition => transition.Kind == "trigger");
        var originalChild = original.Consequences.OwnedEffectSources.RootBindings.Single(value =>
            value.DefinitionKey == "wound_reaction_child");
        Assert.True(history.State.TryGetEntry(originalChild.EffectId, out var replacedChild));
        Assert.Equal("replaced", replacedChild.State);
        var materializedChild = Assert.Single(history.State.Entries, value =>
            value.EffectId != originalChild.EffectId && value.Source["kind"]?.GetValue<string>() == "wound" &&
            value.Source["sourceId"]?.GetValue<string>() == original.WoundId &&
            value.Source["definitionKey"]?.GetValue<string>() == "wound_reaction_child");
        Assert.Equal("expired", materializedChild.State);
        Assert.Equal(new[] { "create", "expire" }, materializedChild.Transitions.Select(value => value.Kind));
        var triggerJournal = (System.Collections.IDictionary)ReadOriginalMortalField(draft, "_woundTriggerJournal")!;
        var childReceipt = Assert.Single(triggerJournal.Values.Cast<object>(), value =>
            ((AcceptedEffectBoundaryActivation)ReadOriginalMortalField(value, "_accepted")!)
            .Activation.Stamp.Identity.EffectId == originalChild.EffectId);
        var receiptAccepted = (AcceptedEffectBoundaryActivation)ReadOriginalMortalField(childReceipt, "_accepted")!;
        var matches = childReceipt.GetType().GetMethod("Matches",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        Assert.True((bool)matches.Invoke(childReceipt, new object?[]
        {
            receiptAccepted,
            checkpoint.EffectPrefix.AppliedComponentEvidence,
            history.State,
            ReadOriginalMortalField(draft, "_lastWoundInsertion"),
            ReadOriginalMortalField(draft, "_woundReactionJournal")
        })!);
        if (consumingReplacementTarget)
        {
            var released = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions, value =>
                value.Reaction.EffectId == reactionRoot.EffectId);
            var reactionJournal = (System.Collections.IDictionary)ReadOriginalMortalField(draft,
                "_woundReactionJournal")!;
            var reactionReceipt = Assert.Single(reactionJournal.Values.Cast<object>());
            var reactionMatches = reactionReceipt.GetType().GetMethod("Matches",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var retainedRelease = Assert.IsType<ReleasedEffectReaction>(ReadOriginalMortalField(
                reactionReceipt, "_released"));
            Assert.Equal(released.Reaction.EventRef, retainedRelease.Reaction.EventRef);
            Assert.True((bool)reactionMatches.Invoke(reactionReceipt,
                new object?[] { retainedRelease, history.State })!);
            var changedAnchorRoot = identity.ReadSnapshot();
            var changedTarget = Assert.Single(changedAnchorRoot["entries"]!.AsArray().OfType<JsonObject>(), value =>
                value["effectId"]?.GetValue<string>() == originalChild.EffectId);
            var changedAnchor = Assert.Single(changedTarget["transitions"]!.AsArray().OfType<JsonObject>(), value =>
                value["kind"]?.GetValue<string>() == "replace");
            changedAnchor["eventRef"] = "turn_43:tampered_replacement_anchor";
            var changed = EffectIdentityState.Parse(
                System.Text.Json.JsonSerializer.SerializeToElement(changedAnchorRoot),
                EffectAcceptedTurnPlan.IdentityIndexPath);
            Assert.Empty(changed.Issues);
            Assert.False((bool)reactionMatches.Invoke(reactionReceipt,
                new object?[] { retainedRelease, changed.State! })!);
            Assert.False((bool)matches.Invoke(childReceipt, new object?[]
            {
                receiptAccepted,
                checkpoint.EffectPrefix.AppliedComponentEvidence,
                changed.State!,
                ReadOriginalMortalField(draft, "_lastWoundInsertion"),
                reactionJournal
            })!);
        }
        if (unrelatedTerminalKind != null)
        {
            Assert.True(history.State.TryGetEntry(EffectMaterializationTestFixture.EffectId, out var unrelated));
            Assert.Equal("active", unrelated.State);
            Assert.Single(unrelated.Transitions);
        }
        Assert.Same(result, await capture.MaterializeWoundAsync(lease, checkpoint));
        Assert.Equal(allocations, identity.AllocationCount);
        Assert.Equal(writes, identity.WriteCount);
        if (!consumingReplacementTarget)
        {
            var completed = await capture.CompleteEffectsAsync(lease);
            Assert.True(completed.Success, Describe(completed.Issues));
            var completedPlan = Assert.IsType<EffectAcceptedTurnPlan>(completed.Plan);
            Assert.Equal(completedPlan.SourceAuthority.CanonicalFingerprint,
                completedPlan.SourceAuthorityFingerprint);
            Assert.Equal(6, draft.Phases.Count);
            if (unrelatedTerminalKind != null)
            {
                var unrelatedRelease = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions, value =>
                    value.Reaction.EffectId == EffectMaterializationTestFixture.EffectId);
                var completedCatalog = EffectCarrierCatalog.Build(completedPlan.ResourceTriggerCarriers);
                Assert.Empty(completedCatalog.Issues);
                Assert.Equal(observerUses > 1 && unrelatedTerminalKind == "suspend",
                    completedCatalog.TryResolveOne(EffectMaterializationTestFixture.EffectId, out _));
                var completedIdentity = EffectIdentityState.Parse(
                    System.Text.Json.JsonSerializer.SerializeToElement(completedPlan.IdentityIndexAfterImage),
                    EffectAcceptedTurnPlan.IdentityIndexPath);
                Assert.Empty(completedIdentity.Issues);
                var completedUnrelated = completedIdentity.State!.Entries.Single(value =>
                    value.EffectId == EffectMaterializationTestFixture.EffectId);
                Assert.Equal(observerUses == 1
                        ? unrelatedTerminalKind == "remove" ? "removed" : "expired"
                        : unrelatedTerminalKind == "suspend" ? "suspended" : "removed",
                    completedUnrelated.State);
                Assert.Single(completedUnrelated.Transitions, transition =>
                    transition.Kind == unrelatedTerminalKind &&
                    transition.EventRef == unrelatedRelease.Reaction.EventRef);
                if (observerUses == 1)
                {
                    Assert.DoesNotContain(completedUnrelated.Transitions,
                        transition => transition.Kind == "consume");
                    var terminalKinds = completedUnrelated.Transitions
                        .Where(transition => transition.Kind is "suspend" or "expire" or "remove")
                        .Select(transition => transition.Kind)
                        .ToArray();
                    Assert.Equal(unrelatedTerminalKind == "suspend"
                            ? new[] { "suspend", "expire" }
                            : new[] { "expire", "remove" },
                        terminalKinds);
                    var expiry = Assert.Single(completedUnrelated.Transitions,
                        transition => transition.Kind == "expire");
                    Assert.Equal(unrelatedRelease.Activation.Identity.EventRef, expiry.EventRef);
                }
                else
                {
                    Assert.Single(completedUnrelated.Transitions, transition =>
                        transition.Kind == "consume" &&
                        transition.EventRef == unrelatedRelease.Activation.Identity.EventRef);
                }
            }
            var completionAllocations = identity.AllocationCount;
            var completionWrites = identity.WriteCount;
            Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
            Assert.Equal(completionAllocations, identity.AllocationCount);
            Assert.Equal(completionWrites, identity.WriteCount);
        }
        if (consumingReplacementTarget)
        {
            var ownedRoot = Assert.IsType<JsonObject>(ReadOriginalMortalField(identity, "_root"));
            var ownedTarget = Assert.Single(ownedRoot["entries"]!.AsArray().OfType<JsonObject>(), value =>
                value["effectId"]?.GetValue<string>() == originalChild.EffectId);
            var ownedAnchor = Assert.Single(ownedTarget["transitions"]!.AsArray().OfType<JsonObject>(), value =>
                value["kind"]?.GetValue<string>() == "replace");
            ownedAnchor["eventRef"] = "turn_43:tampered_owned_replacement_anchor";
            var failedCompletion = await capture.CompleteEffectsAsync(lease);
            Assert.False(failedCompletion.Success);
            Assert.Contains(failedCompletion.Issues, issue =>
                issue.Code == "spiritual_wound_cut_agreement_mismatch");
            Assert.Equal(allocations, identity.AllocationCount);
            Assert.Equal(writes, identity.WriteCount);
            Assert.False(capture.IsCurrentOwner);
        }
        foreach (var pair in canonical)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Verifies that a reaction introduced by a newly inserted physical generation can consume
    /// and replace its owned target at a later worsening cut in the same original draft.
    /// </summary>
    /// <param name="omitConsumptionReceipt">
    /// <see langword="true"/> removes only the retained target-consumption receipt before
    /// common completion; <see langword="false"/> preserves the retained target-consumption receipt.
    /// </param>
    /// <param name="omitReactionReceipt">
    /// <see langword="true"/> removes only the retained reaction-application receipt before
    /// common completion; <see langword="false"/> preserves the retained reaction receipt.
    /// </param>
    /// <param name="tamperReleasedTarget">
    /// <see langword="true"/> changes only the target identifier in the retained released reaction
    /// before the next worsening cut; <see langword="false"/> preserves the accepted reaction.
    /// </param>
    /// <param name="tamperCurrentSourceDefinition">
    /// <see langword="true"/> changes only the valid modifier value in the installed current source
    /// catalog before the next worsening cut; <see langword="false"/> preserves that catalog entry.
    /// </param>
    /// <param name="shareChildSourceAcrossGenerations">
    /// <see langword="true"/> gives generations II and III the same legal child source key and
    /// definition while preserving their distinct source-group epochs; <see langword="false"/>
    /// keeps the earlier generation's existing single-root fixture.
    /// </param>
    /// <param name="reusePriorGenerationSourceBinding">
    /// <see langword="true"/> makes the current lineage resolver use the genuine generation-II
    /// source catalog before the next worsening cut; <see langword="false"/> preserves its
    /// generation-III catalog.
    /// </param>
    /// <param name="skillScopedReplacement">
    /// <see langword="true"/> gives the generation-III replacement child an exact player-skill
    /// roll scope; <see langword="false"/> preserves the resistance-modifier child.
    /// </param>
    /// <param name="tamperSkillScopeAuthority">
    /// <see langword="true"/> disables only the selected skill row inside the retained current
    /// scope catalog before the next worsening cut; <see langword="false"/> preserves it.
    /// </param>
    [Theory]
    [InlineData(false, false, false, false, false, false, false, false)]
    [InlineData(true, false, false, false, false, false, false, false)]
    [InlineData(false, false, false, false, false, false, false, true)]
    [InlineData(false, true, false, false, false, false, false, false)]
    [InlineData(false, false, true, false, false, false, false, false)]
    [InlineData(false, false, false, true, false, false, false, false)]
    [InlineData(false, false, false, true, true, false, false, false)]
    [InlineData(false, false, false, false, false, true, false, false)]
    [InlineData(false, false, false, false, false, true, true, false)]
    public async Task OriginalMortalDraft_ReactionIntroducedByCurrentGenerationRoutesIntoLaterWorsening(
        bool omitConsumptionReceipt,
        bool tamperReleasedTarget,
        bool tamperCurrentSourceDefinition,
        bool shareChildSourceAcrossGenerations,
        bool reusePriorGenerationSourceBinding,
        bool skillScopedReplacement,
        bool tamperSkillScopeAuthority,
        bool omitReactionReceipt)
    {
        await using var context = await CreatePlayerContextAsync();
        if (skillScopedReplacement)
        {
            await context.WriteExactJsonAsync(
                EffectMaterializationTestContext.MaterializableSkillPath,
                CreateMortalSkillScopeRoot(active: true).ToJsonString());
        }
        var creation = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 1);
        var initialProposal = CreateRepairRoundtripProposal("treatment");
        initialProposal["severity"] = "I";
        initialProposal["consequenceDefinitions"]!.AsArray().Clear();
        initialProposal["consequenceDefinitions"]!.AsArray().Add(CreateMortalWoundProposalRoot(
            "resistance_modifier", "wound_initial_root", "Боль мешает сопротивляться."));
        initialProposal["consequenceDefinitions"]![0]!["definition"]!["triggers"]![0]!["eventType"] =
            "resource_spent";
        var creationDecision = Decision("materialize", initialProposal);
        creationDecision["opportunityRef"] = creation.Opportunity.PublicRef;
        var created = WoundResponseInputComposer.Compose(creation.Binding, new[] { creation.Opportunity },
            Response(creationDecision).WoundDecisions, AcquisitionNarration,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(created.Success, Describe(created.Issues));
        await PublishAsync(context, created.CommandRoot!);
        var player = (await context.ReadJsonAsync(WoundCarrierCatalog.PlayerPath))!.AsObject();
        var parsed = WoundMaterializationContract.Parse(player["activeWounds"]![0]!.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var original = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var authorities = await CreateSignedAuthorityBatchAsync(context, 4, null, null, 43, 3, original);
        Assert.Equal(3, authorities.Count);
        var decisions = authorities.Select((authority, index) =>
        {
            var proposal = CreateRepairRoundtripProposal("treatment");
            if (index == 1)
            {
                ConfigureMortalReactionProposal(
                    proposal,
                    "III",
                    consumingReplacementTarget: true,
                    skillScopedReplacement
                        ? EffectMaterializationTestContext.MaterializableSkillId
                        : null);
            }
            else if (index == 0 && shareChildSourceAcrossGenerations)
            {
                proposal["severity"] = "II";
                proposal["consequenceDefinitions"]!.AsArray().Clear();
                proposal["consequenceDefinitions"]!.AsArray().Add(
                    CreateMortalReplacementChildRoot(consumingReplacementTarget: true));
            }
            else
            {
                proposal["severity"] = index == 0 ? "II" : "IV";
                proposal["consequenceDefinitions"]!.AsArray().Clear();
                proposal["consequenceDefinitions"]!.AsArray().Add(CreateMortalWoundProposalRoot(
                    "resistance_modifier", $"wound_generation_{index}", "Боль мешает сопротивляться."));
                proposal["consequenceDefinitions"]![0]!["definition"]!["triggers"]![0]!["eventType"] =
                    "resource_spent";
            }
            var decision = Decision("materialize", proposal);
            decision["opportunityRef"] = authority.Opportunity.PublicRef;
            decision["woundRef"] = $"forearm_retrauma_generation_{index}";
            return ToElement(decision);
        }).ToArray();
        var selected = WoundResponseInputComposer.Compose(authorities[0].Binding,
            authorities.Select(value => value.Opportunity).ToArray(), decisions, AcquisitionNarration,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(selected.Success, Describe(selected.Issues));
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath,
            selected.CommandRoot!.ToJsonString());
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath, new JsonObject
        {
            ["resourceChanges"] = new JsonArray(
                CreateOriginalMortalResourceCommand("spend", 1),
                CreateOriginalMortalResourceCommand("spend", 2),
                CreateOriginalMortalResourceCommand("gain", 3))
        }.ToJsonString());

        var canonical = new Dictionary<string, byte[]?>();
        var canonicalPaths = new[] { WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath, EffectCarrierCatalog.PlayerPath, EffectAcceptedTurnPlan.IdentityIndexPath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath }
            .Concat(skillScopedReplacement
                ? new[] { EffectMaterializationTestContext.MaterializableSkillPath }
                : Array.Empty<string>());
        foreach (var path in canonicalPaths)
            canonical.Add(path, await context.FileSystem.ReadFileBytesAsync(path));

        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureMortalOriginalTurnAsync(lease);
        Assert.True(captured.Issues.Count == 0, Describe(captured.Issues));
        using var capture = Assert.IsType<ValidationService.MortalOriginalTurnCapture>(captured.Capture);
        var first = await capture.AdvanceNextResourceBoundaryAsync(lease);
        Assert.Empty(first.Issues);
        var rankTwo = await capture.MaterializeWoundAsync(lease,
            Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(first.Step!.Checkpoint),
            authorities[0].Opportunity.PublicRef);
        Assert.True(rankTwo.Issues.Count == 0, Describe(rankTwo.Issues));
        Assert.Equal("II", rankTwo.Wound!.Severity.Value);
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(
            ReadOriginalMortalField(capture, "_resources"));
        var routing = Assert.IsType<EffectAcceptedTurnPlanner.BaseResourceRouting>(resources.Routing);
        EffectSourceAuthority? priorGenerationSources = null;
        IReadOnlyList<WoundApplicationRootEffectBinding>? priorGenerationRoots = null;
        string? priorGenerationChildEffectId = null;
        if (shareChildSourceAcrossGenerations)
        {
            Assert.True(routing.TryReadCurrentWoundReactionView(
                out priorGenerationSources,
                out priorGenerationRoots,
                out _));
            priorGenerationChildEffectId = Assert.Single(
                rankTwo.Wound.Consequences.OwnedEffectSources.RootBindings,
                value => value.DefinitionKey == "wound_reaction_child").EffectId;
        }
        var second = await capture.AdvanceNextResourceBoundaryAsync(lease);
        Assert.Empty(second.Issues);
        var rankThree = await capture.MaterializeWoundAsync(lease,
            Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(second.Step!.Checkpoint),
            authorities[1].Opportunity.PublicRef);
        Assert.True(rankThree.Issues.Count == 0, Describe(rankThree.Issues));
        Assert.Equal("III", rankThree.Wound!.Severity.Value);
        var reactionRoot = rankThree.Wound.Consequences.OwnedEffectSources.RootBindings.Single(value =>
            value.DefinitionKey == "wound_reaction_root");
        var replacementTarget = rankThree.Wound.Consequences.OwnedEffectSources.RootBindings.Single(value =>
            value.DefinitionKey == "wound_reaction_child");
        var third = await capture.AdvanceNextResourceBoundaryAsync(lease);
        Assert.Empty(third.Issues);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(third.Step!.Checkpoint);
        var reactionActivation = Assert.Single(checkpoint.EffectPrefix.AcceptedActivations, value =>
            value.Activation.Stamp.Identity.EffectId == reactionRoot.EffectId);
        var targetActivation = Assert.Single(checkpoint.EffectPrefix.AcceptedActivations, value =>
            value.Activation.Stamp.Identity.EffectId == replacementTarget.EffectId);
        Assert.True(targetActivation.Activation.Stamp.ConsumesUse);
        Assert.Equal(2, targetActivation.Activation.Stamp.UsesBefore);
        Assert.Equal(1, targetActivation.Activation.UsesAfter);
        var released = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions);
        Assert.Equal(reactionActivation.Activation.Stamp.Identity, released.Activation.Identity);
        Assert.Equal(reactionRoot.EffectId, released.Reaction.EffectId);
        Assert.Equal(replacementTarget.EffectId, released.Reaction.ReplacementTargetEffectId);
        Assert.Equal("wound_reaction_child", released.Reaction.DownstreamSourceKey?.DefinitionKey);
        EffectSourceAuthority? currentGenerationSources = null;
        WoundReactionLineageAuthority? currentGenerationLineage = null;
        if (shareChildSourceAcrossGenerations || skillScopedReplacement)
        {
            Assert.True(routing.TryReadCurrentWoundReactionView(
                out currentGenerationSources,
                out var currentGenerationRoots,
                out currentGenerationLineage));
            if (skillScopedReplacement)
            {
                var scopedKey = Assert.IsType<EffectSourceKey>(released.Reaction.DownstreamSourceKey);
                var scopedSource = currentGenerationSources.ResolveCanonicalBinding(scopedKey, "player");
                Assert.True(scopedSource.Success, Describe(scopedSource.Issues));
                var releasedComponent = Assert.IsType<JsonObject>(Assert.Single(
                    scopedSource.Source!.Definition["components"]!.AsArray()));
                Assert.Equal("roll_modifier", releasedComponent["profile"]!.GetValue<string>());
                Assert.Equal("disadvantage", releasedComponent["payload"]!["contribution"]!.GetValue<string>());
                Assert.Equal(new[] { "skill_check" }, releasedComponent["payload"]!["operations"]!.AsArray()
                    .Select(value => value!.GetValue<string>()));
                var releasedScope = Assert.IsType<JsonObject>(releasedComponent["payload"]!["scope"]);
                Assert.Equal("skill", releasedScope["kind"]!.GetValue<string>());
                Assert.Equal(EffectMaterializationTestContext.MaterializableSkillId,
                    releasedScope["skillId"]!.GetValue<string>());
            }
            if (shareChildSourceAcrossGenerations)
            {
                Assert.NotSame(priorGenerationSources, currentGenerationSources);
                var childKey = Assert.IsType<EffectSourceKey>(released.Reaction.DownstreamSourceKey);
                var priorChild = priorGenerationSources!.ResolveCanonicalBinding(childKey, "player");
                var currentChild = currentGenerationSources.ResolveCanonicalBinding(childKey, "player");
                Assert.True(priorChild.Success, Describe(priorChild.Issues));
                Assert.True(currentChild.Success, Describe(currentChild.Issues));
                Assert.Equal(priorChild.Source!.Key, currentChild.Source!.Key);
                Assert.True(JsonNode.DeepEquals(priorChild.Source.Definition, currentChild.Source.Definition));
                Assert.NotEqual(priorGenerationChildEffectId, replacementTarget.EffectId);
                var groupKey = new EffectIdentitySourceGroup(
                    childKey.Realm,
                    childKey.Kind,
                    childKey.SourceId);
                Assert.True(priorGenerationSources.TryResolveWoundGroup(groupKey, out var priorGroup));
                Assert.True(currentGenerationSources.TryResolveWoundGroup(groupKey, out var currentGroup));
                var priorRoot = Assert.Single(priorGroup.ApplicationRootLineage,
                    value => value.DefinitionKey == childKey.DefinitionKey);
                var currentRoot = Assert.Single(currentGroup.ApplicationRootLineage,
                    value => value.DefinitionKey == childKey.DefinitionKey);
                Assert.NotEqual(priorRoot.ApplicationRef, currentRoot.ApplicationRef);
                Assert.Contains(priorGenerationRoots!, value =>
                    value.ApplicationRef == priorRoot.ApplicationRef &&
                    value.EffectId == priorGenerationChildEffectId);
                Assert.Contains(currentGenerationRoots, value =>
                    value.ApplicationRef == currentRoot.ApplicationRef &&
                    value.EffectId == replacementTarget.EffectId);
            }
        }
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(ReadOriginalMortalField(capture, "_effects"));
        var identity = Assert.IsType<EffectIdentityHistoryOwner>(ReadOriginalMortalField(draft, "identityRoot"));
        var allocationsBefore = identity.AllocationCount;
        var writesBefore = identity.WriteCount;
        if (tamperReleasedTarget || tamperCurrentSourceDefinition || reusePriorGenerationSourceBinding ||
            tamperSkillScopeAuthority)
        {
            var workspace = ReadOriginalMortalField(draft, "workspace")!;
            var workspaceEditCount = Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace));
            var phasesBefore = draft.Phases.ToArray();
            var woundReadVersionBefore = draft.WoundReadVersion;
            var dependencyCutVersionBefore = draft.DependencyCutVersion;
            var triggerJournalBefore = (System.Collections.IDictionary)ReadOriginalMortalField(
                draft,
                "_woundTriggerJournal")!;
            var reactionJournalBefore = (System.Collections.IDictionary)ReadOriginalMortalField(
                draft,
                "_woundReactionJournal")!;
            var triggerJournalCountBefore = triggerJournalBefore.Count;
            var reactionJournalCountBefore = reactionJournalBefore.Count;
            var stateBefore = CaptureOriginalMortalDraftState(draft, identity);
            var planAuthorityBefore = checkpoint.EffectPrefix.PlanAuthority;
            var prefixFingerprintBefore = checkpoint.EffectPrefix.Fingerprint;
            if (tamperReleasedTarget)
            {
                var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(
                    ReadOriginalMortalField(checkpoint.EffectPrefix, "_image"));
                var retainedReactions = Assert.IsType<ReleasedEffectReaction[]>(
                    ReadOriginalMortalField(transcript, "_releasedReactions"));
                var retainedReaction = Assert.Single(retainedReactions);
                var changedTarget = retainedReaction.Reaction.Target with
                {
                    TargetId = "player_current_tampered"
                };
                Assert.NotEqual(retainedReaction.Reaction.Target.TargetId, changedTarget.TargetId);
                retainedReactions[0] = retainedReaction with
                {
                    Reaction = retainedReaction.Reaction with
                    {
                        Target = changedTarget
                    }
                };

                var changedRelease = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions);
                Assert.Equal(released with { Reaction = changedRelease.Reaction }, changedRelease);
                Assert.Equal(released.Reaction.Target.Realm, changedRelease.Reaction.Target.Realm);
                Assert.Equal(released.Reaction.Target.Kind, changedRelease.Reaction.Target.Kind);
                Assert.Equal(changedTarget.TargetId, changedRelease.Reaction.Target.TargetId);
                Assert.Equal(released.Reaction with
                {
                    Target = changedRelease.Reaction.Target,
                    DownstreamSource = changedRelease.Reaction.DownstreamSource,
                    Parameters = changedRelease.Reaction.Parameters
                }, changedRelease.Reaction);
                if (released.Reaction.DownstreamSource is { } originalSource)
                {
                    var changedSource = Assert.IsType<EffectSourceAuthorityEntry>(
                        changedRelease.Reaction.DownstreamSource);
                    Assert.Equal(originalSource with { Definition = changedSource.Definition }, changedSource);
                    Assert.True(JsonNode.DeepEquals(originalSource.Definition, changedSource.Definition));
                }
                else
                {
                    Assert.Null(changedRelease.Reaction.DownstreamSource);
                }
                Assert.True(JsonNode.DeepEquals(released.Reaction.Parameters, changedRelease.Reaction.Parameters));
            }
            else if (tamperCurrentSourceDefinition)
            {
                Assert.True(routing.TryReadCurrentWoundReactionView(
                    out var currentSources,
                    out _,
                    out _));
                var sourceKey = Assert.IsType<EffectSourceKey>(released.Reaction.DownstreamSourceKey);
                var entries = Assert.IsType<Dictionary<EffectSourceKey, EffectSourceAuthorityEntry>>(
                    ReadOriginalMortalField(currentSources, "_entries"));
                var originalEntry = entries[sourceKey];
                Assert.Equal(-1, originalEntry.Definition["components"]![0]!["payload"]!["value"]!
                    .GetValue<int>());
                var originalDefinition = originalEntry.Definition.DeepClone().AsObject();
                var changedDefinition = originalDefinition.DeepClone().AsObject();
                changedDefinition["components"]![0]!["payload"]!["value"] = -2;
                var sourceFingerprintBefore = currentSources.Fingerprint;
                var groupKey = new EffectIdentitySourceGroup(
                    sourceKey.Realm,
                    sourceKey.Kind,
                    sourceKey.SourceId);
                var sealedDefinitionBefore = Assert.Single(
                        Assert.Single(currentSources.SnapshotWoundGroupAuthorities(), value => value.Key == groupKey)
                            .Definitions,
                        value => value.DefinitionKey == sourceKey.DefinitionKey)
                    .Definition;
                Assert.True(JsonNode.DeepEquals(originalDefinition, sealedDefinitionBefore));

                entries[sourceKey] = originalEntry with { Definition = changedDefinition };

                var changedEntry = entries[sourceKey];
                Assert.Equal(originalEntry with { Definition = changedEntry.Definition }, changedEntry);
                Assert.True(JsonNode.DeepEquals(changedDefinition, changedEntry.Definition));
                Assert.False(JsonNode.DeepEquals(originalDefinition, changedEntry.Definition));
                var sealedDefinitionAfter = Assert.Single(
                        Assert.Single(currentSources.SnapshotWoundGroupAuthorities(), value => value.Key == groupKey)
                            .Definitions,
                        value => value.DefinitionKey == sourceKey.DefinitionKey)
                    .Definition;
                Assert.True(JsonNode.DeepEquals(sealedDefinitionBefore, sealedDefinitionAfter));
                Assert.Equal(sourceFingerprintBefore, currentSources.Fingerprint);
                var unchangedRelease = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions);
                Assert.Equal(released with { Reaction = unchangedRelease.Reaction }, unchangedRelease);
                Assert.Equal(released.Reaction with
                {
                    DownstreamSource = unchangedRelease.Reaction.DownstreamSource,
                    Parameters = unchangedRelease.Reaction.Parameters
                }, unchangedRelease.Reaction);
                Assert.True(JsonNode.DeepEquals(released.Reaction.Parameters, unchangedRelease.Reaction.Parameters));
            }
            else if (tamperSkillScopeAuthority)
            {
                var acceptedPlan = Assert.IsType<EffectAcceptedTurnPlan>(
                    ReadOriginalMortalField(draft, "plan"));
                var skillAuthority = Assert.IsType<EffectRollSkillScopeAuthority>(
                    acceptedPlan.SkillScopeAuthority);
                var selectorPath = $"effect.reactions[{released.Reaction.EventRef}].components[0].payload.scope.skillId";
                var skillId = EffectMaterializationTestContext.MaterializableSkillId;
                var target = released.Reaction.Target;
                var resolutionBefore = skillAuthority.ResolveForNewBinding(target, skillId, selectorPath);
                Assert.True(resolutionBefore.IsUsable, Describe(resolutionBefore.Issues));
                var skillFingerprintBefore = skillAuthority.Fingerprint;
                var scopedKey = Assert.IsType<EffectSourceKey>(released.Reaction.DownstreamSourceKey);
                var scopedSourceBefore = currentGenerationSources!.ResolveCanonicalBinding(scopedKey, "player");
                Assert.True(scopedSourceBefore.Success, Describe(scopedSourceBefore.Issues));
                var releasedDefinitionBefore = scopedSourceBefore.Source!.Definition.ToJsonString();
                var currentCatalogField = skillAuthority.GetType().GetField(
                    "_current",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(currentCatalogField);
                var currentCatalog = currentCatalogField.GetValue(skillAuthority);
                Assert.NotNull(currentCatalog);
                var exactField = currentCatalog.GetType().GetField(
                    "_exact",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(exactField);
                var exact = exactField.GetValue(currentCatalog);
                Assert.NotNull(exact);
                var targetRows = exact.GetType().GetProperty("Item")!.GetValue(exact, new object[] { target });
                Assert.NotNull(targetRows);
                var selectedRows = Assert.IsType<EffectRollSkillScopeRow[]>(
                    targetRows.GetType().GetProperty("Item")!.GetValue(targetRows, new object[] { skillId }));
                var selectedRow = Assert.Single(selectedRows);
                Assert.True(selectedRow.Active);

                selectedRows[0] = selectedRow with { Active = false };

                var resolutionAfter = skillAuthority.ResolveForNewBinding(target, skillId, selectorPath);
                Assert.False(resolutionAfter.IsUsable);
                Assert.Equal("effect_roll_skill_scope_unavailable", Assert.Single(resolutionAfter.Issues).Code);
                Assert.Same(skillAuthority, acceptedPlan.SkillScopeAuthority);
                Assert.Equal(skillFingerprintBefore, skillAuthority.Fingerprint);
                var unchangedRelease = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions);
                Assert.Equal(released with { Reaction = unchangedRelease.Reaction }, unchangedRelease);
                var scopedSourceAfter = currentGenerationSources.ResolveCanonicalBinding(scopedKey, "player");
                Assert.True(scopedSourceAfter.Success, Describe(scopedSourceAfter.Issues));
                Assert.Equal(releasedDefinitionBefore, scopedSourceAfter.Source!.Definition.ToJsonString());
            }
            else
            {
                Assert.NotNull(priorGenerationSources);
                Assert.NotNull(currentGenerationSources);
                Assert.NotNull(currentGenerationLineage);
                var sourceAuthorityField = currentGenerationLineage.GetType().GetField(
                    "_sourceAuthority",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(sourceAuthorityField);
                sourceAuthorityField.SetValue(currentGenerationLineage, priorGenerationSources);
                Assert.Same(priorGenerationSources, sourceAuthorityField.GetValue(currentGenerationLineage));
                Assert.True(routing.TryReadCurrentWoundReactionView(
                    out var retainedCurrentSources,
                    out _,
                    out var retainedCurrentLineage));
                Assert.Same(currentGenerationSources, retainedCurrentSources);
                Assert.Same(currentGenerationLineage, retainedCurrentLineage);
                var unchangedRelease = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions);
                Assert.Equal(released with { Reaction = unchangedRelease.Reaction }, unchangedRelease);
                Assert.Equal(released.Reaction with
                {
                    DownstreamSource = unchangedRelease.Reaction.DownstreamSource,
                    Parameters = unchangedRelease.Reaction.Parameters
                }, unchangedRelease.Reaction);
                Assert.True(JsonNode.DeepEquals(released.Reaction.Parameters, unchangedRelease.Reaction.Parameters));
            }
            Assert.Equal(planAuthorityBefore, checkpoint.EffectPrefix.PlanAuthority);
            Assert.Equal(prefixFingerprintBefore, checkpoint.EffectPrefix.Fingerprint);

            var failedRankFour = await capture.MaterializeWoundAsync(
                lease,
                checkpoint,
                authorities[2].Opportunity.PublicRef);

            Assert.Null(failedRankFour.Wound);
            Assert.Contains(failedRankFour.Issues, issue =>
                issue.Code == (tamperReleasedTarget
                    ? "effect_reaction_wound_lineage_producer_invalid"
                    : tamperSkillScopeAuthority
                        ? "effect_roll_skill_scope_unavailable"
                        : "effect_reaction_wound_lineage_source_invalid"));
            if (tamperSkillScopeAuthority)
            {
                Assert.Contains(failedRankFour.Issues, issue =>
                    issue.Code == "effect_roll_skill_scope_unavailable" &&
                    issue.FilePath ==
                    $"effect.reactions[{released.Reaction.EventRef}].components[0].payload.scope.skillId");
            }
            Assert.Contains(failedRankFour.Issues, issue =>
                issue.Code == "spiritual_wound_generation_required");
            Assert.Equal(allocationsBefore, identity.AllocationCount);
            Assert.Equal(writesBefore, identity.WriteCount);
            Assert.Equal(workspaceEditCount, Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace)));
            Assert.Equal(phasesBefore, draft.Phases);
            Assert.Equal(woundReadVersionBefore, draft.WoundReadVersion);
            Assert.Equal(dependencyCutVersionBefore, draft.DependencyCutVersion);
            Assert.Equal(triggerJournalCountBefore, triggerJournalBefore.Count);
            Assert.Equal(reactionJournalCountBefore, reactionJournalBefore.Count);
            Assert.True(JsonNode.DeepEquals(stateBefore, CaptureOriginalMortalDraftState(draft, identity)));
            Assert.False(capture.IsCurrentOwner);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.MaterializeWoundAsync(
                lease,
                checkpoint,
                authorities[2].Opportunity.PublicRef));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.CompleteEffectsAsync(lease));
            foreach (var pair in canonical)
                Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            return;
        }
        var rankFour = await capture.MaterializeWoundAsync(lease, checkpoint,
            authorities[2].Opportunity.PublicRef);
        Assert.True(rankFour.Issues.Count == 0, Describe(rankFour.Issues));
        Assert.Equal("IV", rankFour.Wound!.Severity.Value);
        Assert.Equal(shareChildSourceAcrossGenerations ? 2 : 3, draft.DependencyCutVersion);
        var allocationsAfterRankFour = identity.AllocationCount;
        var writesAfterRankFour = identity.WriteCount;
        var rankFourSelection = Assert.IsType<ValidationService.MortalOriginalTurnCapture.WoundSelection>(
            ReadOriginalMortalField(capture, "_woundSelection"));
        var signedOnlyPreparation = WoundAcceptedTurnPlanner.Prepare(rankFourSelection.Input);
        Assert.False(signedOnlyPreparation.Success);
        Assert.Null(signedOnlyPreparation.Plan);
        Assert.Contains(signedOnlyPreparation.Issues, issue =>
            issue.Code == "wound_plan_worsening_target_stale");
        Assert.Equal(allocationsAfterRankFour, identity.AllocationCount);
        Assert.Equal(writesAfterRankFour, identity.WriteCount);
        var insertion = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion>(
            ReadOriginalMortalField(draft, "_lastWoundInsertion"));
        Assert.Equal(rankFour.Wound.WoundId, insertion.Wound.WoundId);
        var reactionJournal = (System.Collections.IDictionary)ReadOriginalMortalField(draft,
            "_woundReactionJournal")!;
        var reactionReceipt = Assert.Single(reactionJournal.Values.Cast<object>());
        var reactionApplication = Assert.IsType<EffectAcceptedTurnPlanner.EffectDraftApplicationReceipt>(
            ReadOriginalMortalField(reactionReceipt, "_application"));
        Assert.Equal("replace", reactionApplication.Disposition);
        Assert.Equal(reactionRoot.EffectId, reactionApplication.ProducerEffectId);
        Assert.Equal(rankThree.Wound.WoundId, reactionApplication.Source.Key.SourceId);
        Assert.Equal("wound_reaction_child", reactionApplication.Source.Key.DefinitionKey);
        Assert.Equal(released.Reaction.EventRef, reactionApplication.EventRef);
        Assert.Equal(released.Reaction.CausalEventRef, reactionApplication.CausalEventRef);
        Assert.Equal(replacementTarget.EffectId, reactionApplication.ReplacedIdentity?.EffectId);
        var reactionResultIdentity = Assert.IsType<EffectReplayIdentity>(reactionApplication.ResultIdentity);
        Assert.Equal(reactionApplication.EffectId, reactionResultIdentity.EffectId);
        Assert.Equal(new[] { EffectIdentityWriteKind.AppendTransition, EffectIdentityWriteKind.CreateEntry },
            reactionApplication.IdentityWrites.Select(value => value.Kind));
        Assert.Single(reactionApplication.IdentityWrites, value => value.Kind == EffectIdentityWriteKind.CreateEntry);
        Assert.Single(reactionApplication.Allocations, value => value.Kind == EffectIdentityAllocationKind.Effect);
        var triggerJournal = (System.Collections.IDictionary)ReadOriginalMortalField(draft,
            "_woundTriggerJournal")!;
        var targetReceipt = Assert.Single(triggerJournal.Values.Cast<object>(), value =>
            ((AcceptedEffectBoundaryActivation)ReadOriginalMortalField(value, "_accepted")!)
            .Activation.Stamp.Identity.EffectId == replacementTarget.EffectId);
        var consumptionWrite = Assert.IsType<EffectIdentityWriteReceipt>(
            ReadOriginalMortalField(targetReceipt, "IdentityWrite"));
        Assert.Equal(EffectIdentityWriteKind.InsertBeforeTransition, consumptionWrite.Kind);
        Assert.Equal(reactionApplication.IdentityWrites[0].PayloadJson, consumptionWrite.AnchorJson);
        var replacementAnchor = Assert.IsType<JsonObject>(JsonNode.Parse(consumptionWrite.AnchorJson!));
        Assert.Equal(replacementAnchor["transitionId"]!.GetValue<string>(), consumptionWrite.AnchorTransitionId);
        var consumptionTransition = Assert.IsType<JsonObject>(JsonNode.Parse(consumptionWrite.PayloadJson));
        Assert.Equal("consume", consumptionTransition["kind"]!.GetValue<string>());
        Assert.Equal(targetActivation.Activation.Stamp.Identity.EventRef,
            consumptionTransition["eventRef"]!.GetValue<string>());
        var consumptionTransitionId = consumptionTransition["transitionId"]!.GetValue<string>();
        var consumptionAllocation = Assert.IsType<EffectIdentityAllocationReceipt>(
            ReadOriginalMortalField(targetReceipt, "Allocation"));
        Assert.Equal(EffectIdentityAllocationKind.Transition, consumptionAllocation.Kind);
        Assert.Equal(consumptionTransitionId, consumptionAllocation.Identity);
        var history = EffectIdentityState.Parse(
            System.Text.Json.JsonSerializer.SerializeToElement(identity.ReadSnapshot()),
            EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.Empty(history.Issues);
        var replaced = history.State!.Entries.Single(value => value.EffectId == replacementTarget.EffectId);
        Assert.Equal("replaced", replaced.State);
        Assert.Equal(new[] { "create", "consume", "replace" },
            replaced.Transitions.Select(value => value.Kind));
        Assert.Equal(targetActivation.Activation.Stamp.Identity.EventRef, replaced.Transitions[1].EventRef);
        Assert.Equal(released.Reaction.EventRef, replaced.Transitions[2].EventRef);
        var resultIdentity = history.State.Entries.Single(value =>
            value.EffectId == reactionResultIdentity.EffectId);
        Assert.Equal(new[] { "create", "expire" }, resultIdentity.Transitions.Select(value => value.Kind));
        Assert.Equal(reactionApplication.CreatedEventRef, resultIdentity.Transitions[0].EventRef);
        Assert.Single(reactionJournal.Values.Cast<object>());
        var allocationsAfterInsertion = identity.AllocationCount;
        var writesAfterInsertion = identity.WriteCount;
        Assert.True(allocationsAfterInsertion > allocationsBefore);
        Assert.True(writesAfterInsertion > writesBefore);
        Assert.Same(rankFour, await capture.MaterializeWoundAsync(lease, checkpoint,
            authorities[2].Opportunity.PublicRef));
        Assert.Equal(allocationsAfterInsertion, identity.AllocationCount);
        Assert.Equal(writesAfterInsertion, identity.WriteCount);
        if (omitConsumptionReceipt || omitReactionReceipt)
        {
            var workspace = ReadOriginalMortalField(draft, "workspace")!;
            var workspaceEditCount = Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace));
            var phasesBefore = draft.Phases.ToArray();
            var woundReadVersionBefore = draft.WoundReadVersion;
            var dependencyCutVersionBefore = draft.DependencyCutVersion;
            var stateBefore = CaptureOriginalMortalDraftState(draft, identity);
            if (omitConsumptionReceipt)
            {
                var journalCount = triggerJournal.Count;
                var targetJournalKey = Assert.Single(triggerJournal.Keys.Cast<object>(), value =>
                    ReferenceEquals(triggerJournal[value], targetReceipt));
                triggerJournal.Remove(targetJournalKey);
                Assert.Equal(journalCount - 1, triggerJournal.Count);
                Assert.DoesNotContain(triggerJournal.Values.Cast<object>(), value =>
                    ReferenceEquals(value, targetReceipt));
                Assert.Single(reactionJournal.Values.Cast<object>());
            }
            else
            {
                Assert.True(omitReactionReceipt);
                var reactionJournalKey = Assert.Single(reactionJournal.Keys.Cast<object>());
                var retainedReactionReceipt = reactionJournal[reactionJournalKey];
                Assert.NotNull(retainedReactionReceipt);
                reactionJournal.Remove(reactionJournalKey);
                Assert.Empty(reactionJournal.Values.Cast<object>());
                Assert.Contains(triggerJournal.Values.Cast<object>(), value =>
                    ReferenceEquals(value, targetReceipt));
            }
            var triggerJournalCount = triggerJournal.Count;
            var reactionJournalCount = reactionJournal.Count;

            var failedCompletion = await capture.CompleteEffectsAsync(lease);
            Assert.False(failedCompletion.Success);
            Assert.Null(failedCompletion.Plan);
            Assert.Contains(failedCompletion.Issues, issue =>
                issue.Code == "spiritual_wound_cut_agreement_mismatch");
            Assert.Equal(allocationsAfterInsertion, identity.AllocationCount);
            Assert.Equal(writesAfterInsertion, identity.WriteCount);
            Assert.Equal(workspaceEditCount, Assert.IsType<int>(workspace.GetType().GetProperty(
                    "EditCount",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic)!
                .GetValue(workspace)));
            Assert.Equal(phasesBefore, draft.Phases);
            Assert.Equal(woundReadVersionBefore, draft.WoundReadVersion);
            Assert.Equal(dependencyCutVersionBefore, draft.DependencyCutVersion);
            Assert.Equal(triggerJournalCount, triggerJournal.Count);
            Assert.Equal(reactionJournalCount, reactionJournal.Count);
            Assert.True(JsonNode.DeepEquals(stateBefore, CaptureOriginalMortalDraftState(draft, identity)));
            Assert.False(capture.IsCurrentOwner);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.CompleteEffectsAsync(lease));
            foreach (var pair in canonical)
                Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            return;
        }
        var completed = await capture.CompleteEffectsAsync(lease);
        Assert.True(completed.Success, Describe(completed.Issues));
        var completedPlan = Assert.IsType<EffectAcceptedTurnPlan>(completed.Plan);
        Assert.Equal(completedPlan.SourceAuthority.CanonicalFingerprint,
            completedPlan.SourceAuthorityFingerprint);
        var completedIdentity = EffectIdentityState.Parse(
            System.Text.Json.JsonSerializer.SerializeToElement(completedPlan.IdentityIndexAfterImage),
            EffectAcceptedTurnPlan.IdentityIndexPath);
        Assert.Empty(completedIdentity.Issues);
        var completedReplaced = completedIdentity.State!.Entries.Single(value =>
            value.EffectId == replacementTarget.EffectId);
        var completedResult = completedIdentity.State.Entries.Single(value =>
            value.EffectId == reactionResultIdentity.EffectId);
        Assert.True(JsonNode.DeepEquals(replaced.Raw, completedReplaced.Raw));
        Assert.True(JsonNode.DeepEquals(resultIdentity.Raw, completedResult.Raw));
        var expectedChildEffectIds = new[]
            {
                replacementTarget.EffectId,
                reactionResultIdentity.EffectId
            }
            .Concat(shareChildSourceAcrossGenerations
                ? new[] { priorGenerationChildEffectId! }
                : Array.Empty<string>())
            .OrderBy(value => value, StringComparer.Ordinal);
        Assert.Equal(expectedChildEffectIds,
            completedIdentity.State.Entries.Where(value =>
                    value.Source["sourceId"]?.GetValue<string>() == rankThree.Wound.WoundId &&
                    value.Source["definitionKey"]?.GetValue<string>() == "wound_reaction_child")
                .Select(value => value.EffectId)
                .OrderBy(value => value, StringComparer.Ordinal));
        Assert.Single(completedIdentity.State.Entries.SelectMany(value => value.Transitions), value =>
            value.TransitionId == consumptionTransitionId);
        Assert.Single(completedPlan.AllocatedEffectIds, value =>
            value == reactionResultIdentity.EffectId);
        var completionAllocations = identity.AllocationCount;
        var completionWrites = identity.WriteCount;
        Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
        Assert.Equal(completionAllocations, identity.AllocationCount);
        Assert.Equal(completionWrites, identity.WriteCount);
        foreach (var pair in canonical)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Fact]
    public async Task OriginalMortalCapture_RetainsDistinctSignedWorseningDecisionsForOneExistingWound()
    {
        await using var context = await CreatePlayerContextAsync();
        var (original, authorities) = await PrepareSignedMortalWorseningBatchAsync(context);
        var before = await context.FileSystem.ReadFileBytesAsync(WoundCarrierCatalog.PlayerPath);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureMortalOriginalTurnAsync(lease);
        Assert.True(captured.Issues.Count == 0, Describe(captured.Issues));
        using var capture = Assert.IsType<ValidationService.MortalOriginalTurnCapture>(captured.Capture);
        Assert.True(capture.IsCurrentOwner);
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(ReadOriginalMortalField(capture, "_effects"));
        Assert.Null(ReadOriginalMortalField(draft, "identityRoot"));
        Assert.Equal(false, ReadOriginalMortalField(draft, "_initializationAttempted"));
        Assert.Empty(draft.Phases);
        var retained = Assert.IsType<WoundResponseCommandParsingResult>(ReadOriginalMortalField(capture, "_originalCommands"));
        Assert.Equal(2, retained.Commands.Count);
        Assert.Equal(2, retained.Commands.Select(value => value.Opportunity.OpportunityId).Distinct().Count());
        Assert.Equal(new[] { "II", "III" }, retained.Commands.Select(value =>
            value.Decision.GetProperty("proposal").GetProperty("severity").GetString()).ToArray());
        Assert.All(retained.Commands, value => Assert.Equal(
            WoundMaterializationContract.SerializeCanonical(original),
            WoundMaterializationContract.SerializeCanonical(value.Opportunity.WorseningTarget!.Wound)));
        retained.CommandRoot!.Clear();
        Assert.NotEmpty(retained.CommandRoot!);
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, WoundCarrierCatalog.PlayerPath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Publishes a real physical wound and prepares two signed retrauma decisions for its next turn.
    /// </summary>
    /// <param name="context">
    /// Initialized Mortal workspace receiving the ordinary publication and signed pending producer batch.
    /// </param>
    /// <param name="reactionRoot">
    /// True gives the original wound a reaction-only root on resource gain; false uses periodic damage on that event.
    /// </param>
    /// <returns>
    /// Published rank-I wound and both original signed opportunities; no later draft insertion is performed.
    /// </returns>
    private static async Task<(WoundMaterializationEnvelope Original, IReadOnlyList<CreationAuthority> Authorities)>
        PrepareSignedMortalWorseningBatchAsync(ResourceMaterializationTestContext context, bool reactionRoot = false)
    {
        var creation = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 1);
        var initialProposal = CreateRepairRoundtripProposal("treatment");
        initialProposal["severity"] = "I";
        if (reactionRoot)
        {
            var reaction = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
            initialProposal["consequenceDefinitions"]![0]!["definition"]!["components"] = reaction["components"]!.DeepClone();
            initialProposal["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["eventType"] = "resource_gained";
            initialProposal["consequenceDefinitions"]![0]!["root"]!["slots"]![0]!["profileKey"] = "event_reaction";
        }
        initialProposal["consequenceDefinitions"]![0]!["definition"]!["triggers"]![0]!["eventType"] = "resource_gained";
        var initialDecision = Decision("materialize", initialProposal);
        initialDecision["opportunityRef"] = creation.Opportunity.PublicRef;
        var created = WoundResponseInputComposer.Compose(creation.Binding, new[] { creation.Opportunity },
            Response(initialDecision).WoundDecisions,
            AcquisitionNarration, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(created.Success, Describe(created.Issues));
        await PublishAsync(context, created.CommandRoot!);
        var player = (await context.ReadJsonAsync(WoundCarrierCatalog.PlayerPath))!.AsObject();
        var parsed = WoundMaterializationContract.Parse(player["activeWounds"]![0]!.ToJsonString(),
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var original = Assert.IsType<WoundMaterializationEnvelope>(parsed.Wound);
        var authorities = await CreateSignedAuthorityBatchAsync(context, 3, null, null, 43, 2, original);
        Assert.Equal(2, authorities.Count);
        var decisions = authorities.Select((authority, index) =>
        {
            var proposal = CreateRepairRoundtripProposal("treatment");
            proposal["severity"] = index == 0 ? "II" : "III";
            proposal["consequenceDefinitions"]![0]!["definition"]!["triggers"]![0]!["eventType"] = "resource_spent";
            var decision = Decision("materialize", proposal);
            decision["opportunityRef"] = authority.Opportunity.PublicRef;
            decision["woundRef"] = $"forearm_retrauma_{index}";
            return ToElement(decision);
        }).ToArray();
        var selected = WoundResponseInputComposer.Compose(authorities[0].Binding,
            authorities.Select(value => value.Opportunity).ToArray(), decisions, AcquisitionNarration,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(selected.Success, Describe(selected.Issues));
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath, selected.CommandRoot!.ToJsonString());
        return (original, authorities);
    }

    /// <summary>
    /// Creates one source-owned Mortal wound definition and its base-wound root slot for a response proposal.
    /// </summary>
    /// <param name="profile">
    /// Mechanical component profile placed in both the definition and its single consequence slot.
    /// </param>
    /// <param name="definitionKey">
    /// Proposal-local definition key used by root and reaction references.
    /// </param>
    /// <param name="summary">
    /// Player-readable consequence summary stored with the root slot.
    /// </param>
    /// <returns>
    /// A detached proposal definition whose identity and source links are allocated by wound materialization.
    /// </returns>
    private static JsonObject CreateMortalWoundProposalRoot(string profile, string definitionKey, string summary)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(profile);
        definition["definitionKey"] = definitionKey;
        definition["allowedRealms"] = new JsonArray("mortal_world");
        definition["allowedTargetKinds"] = new JsonArray("player");
        definition["parameterBounds"] = new JsonObject();
        definition["stacking"] = new JsonObject
        {
            ["stackKey"] = "stack_" + definitionKey,
            ["policy"] = "independent",
            ["maxStacks"] = 1,
            ["atMaximum"] = "no_change",
            ["refreshMode"] = null,
            ["mergeRule"] = null
        };
        definition["links"] = new JsonArray();
        return new JsonObject
        {
            ["definitionRef"] = definitionKey,
            ["definition"] = definition,
            ["root"] = new JsonObject
            {
                ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                ["slots"] = new JsonArray(new JsonObject
                {
                    ["profileKey"] = profile,
                    ["readableSummary"] = summary
                })
            }
        };
    }

    /// <summary>
    /// Replaces a proposal's consequence graph with a legal source-owned reaction and replace target.
    /// </summary>
    /// <param name="proposal">
    /// Mutable physical wound proposal receiving the reaction graph.
    /// </param>
    /// <param name="severity">
    /// Rank III or IV severity permitted to own an apply-definition reaction.
    /// </param>
    /// <param name="consumingReplacementTarget">
    /// True makes the replace target consume one use on the same accepted event; false records a non-consuming trigger.
    /// </param>
    /// <param name="replacementSkillId">
    /// Exact player skill selected by the replacement roll modifier, or <see langword="null"/>
    /// to retain the resistance-modifier replacement.
    /// </param>
    private static void ConfigureMortalReactionProposal(JsonObject proposal, string severity,
        bool consumingReplacementTarget, string? replacementSkillId = null)
    {
        proposal["severity"] = severity;
        var definitions = proposal["consequenceDefinitions"]!.AsArray();
        definitions.Clear();
        var reaction = CreateMortalWoundProposalRoot("event_reaction", "wound_reaction_root",
            "Боль выпускает связанное кровотечение.");
        reaction["definition"]!["components"]![0]!["payload"]!["eventType"] = "resource_gained";
        reaction["definition"]!["components"]![0]!["payload"]!["resultKind"] = "apply_definition";
        reaction["definition"]!["components"]![0]!["payload"]!["definitionKey"] = "wound_reaction_child";
        reaction["definition"]!["components"]![0]!["payload"]!["parameters"] = new JsonObject();
        reaction["definition"]!["components"]![0]!["payload"]!["maxExpansion"] = 2;
        reaction["definition"]!["triggers"]![0]!["eventType"] = "resource_gained";
        var child = CreateMortalReplacementChildRoot(consumingReplacementTarget, replacementSkillId);
        definitions.Add(reaction);
        definitions.Add(child);
    }

    /// <summary>
    /// Creates the legal replacement child shared by consecutive physical wound generations.
    /// </summary>
    /// <param name="consumingReplacementTarget">
    /// <see langword="true"/> gives the child a two-use lifetime consumed by resource-gain
    /// events; <see langword="false"/> leaves the trigger non-consuming.
    /// </param>
    /// <param name="skillId">
    /// Exact player skill selected by a roll-modifier child, or <see langword="null"/>
    /// to create the ordinary resistance-modifier child.
    /// </param>
    /// <returns>
    /// A detached source-owned definition with the canonical reaction-child key.
    /// </returns>
    private static JsonObject CreateMortalReplacementChildRoot(
        bool consumingReplacementTarget,
        string? skillId = null)
    {
        var child = CreateMortalWoundProposalRoot(
            skillId is null ? "resistance_modifier" : "roll_modifier",
            "wound_reaction_child",
            "Кровотечение усиливается после принятого восстановления.");
        if (skillId is not null)
        {
            child["definition"]!["components"]![0]!["payload"] =
                EffectMaterializationTestFixture.CreateFocusedRollModifierPayload(skillId);
        }
        child["definition"]!["stacking"]!["policy"] = "replace";
        child["definition"]!["triggers"]![0]!["eventType"] = "resource_gained";
        if (consumingReplacementTarget)
        {
            child["definition"]!["triggers"]![0]!["consumeUses"] = true;
            child["definition"]!["lifetime"] = new JsonObject
            {
                ["mode"] = "uses", ["initialUses"] = 2,
                ["consumingEventTypes"] = new JsonArray("resource_gained")
            };
        }
        return child;
    }

    /// <summary>
    /// Creates the canonical player skill catalog used by the current-generation scope test.
    /// </summary>
    /// <param name="active">
    /// Whether the exact selected skill is currently usable.
    /// </param>
    /// <returns>
    /// A detached active-skill root containing the one exact materializable skill.
    /// </returns>
    private static JsonObject CreateMortalSkillScopeRoot(bool active) => new()
    {
        ["activeSkillChanges"] = new JsonArray(new JsonObject
        {
            ["skillId"] = EffectMaterializationTestContext.MaterializableSkillId,
            ["skillName"] = "Кровавый след",
            ["skillDescription"] = "Помогает точно читать движение раненого противника.",
            ["rarity"] = "common",
            ["actionCost"] = "Main",
            ["combatEffect"] = new JsonObject
            {
                ["isActivatedEffect"] = true,
                ["actionName"] = "Кровавый след",
                ["actionCost"] = "Main",
                ["effects"] = new JsonArray(new JsonObject
                {
                    ["effectType"] = "Damage",
                    ["value"] = "10%",
                    ["targetType"] = "enemy",
                    ["effectDescription"] = "Навык оставляет проверяемый кровавый след.",
                    ["poiseDamage"] = "5%"
                })
            },
            ["active"] = active,
            ["lifecycle"] = active ? "active" : "disabled"
        }),
        ["removeActiveSkills"] = new JsonArray()
    };

    /// <summary>
    /// Creates one deterministic ordinary resource command for a sequential current-generation test.
    /// </summary>
    /// <param name="operation">
    /// Spend or gain operation observed by the selected wound generation.
    /// </param>
    /// <param name="ordinal">
    /// One-based command ordinal used by the accepted event reference.
    /// </param>
    /// <returns>
    /// A detached command row targeting the current Mortal player.
    /// </returns>
    private static JsonObject CreateOriginalMortalResourceCommand(string operation, int ordinal) => new()
    {
        ["operation"] = operation,
        ["resourceKey"] = "energy",
        ["amount"] = 1,
        ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
        ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
        ["eventRef"] = $"turn_43:resource:{ordinal}",
        ["reason"] = "Принятое изменение сил."
    };

    /// <summary>
    /// Verifies that a physical wound root joins the retained resource graph between real ordinary
    /// events without replaying the closed prefix or forcing unrelated effect work through the cut.
    /// </summary>
    /// <param name="mode">
    /// Fixture contour selecting cycle rejection, plain insertion, retained use-budget continuity,
    /// reaction-only insertion, isolation from an unrelated old effect, deferred unrelated refresh work,
    /// or expiry of a reaction-created child during common lifecycle completion.
    /// </param>
    [Theory]
    [InlineData("cycle")]
    [InlineData("plain")]
    [InlineData("old_budget")]
    [InlineData("reaction_only")]
    [InlineData("new_node_old_effect")]
    [InlineData("foreign_refresh")]
    [InlineData("foreign_due_expiry")]
    public async Task OriginalMortalDraft_InsertsPhysicalRootBetweenActualOrdinaryEvents(string mode)
    {
        await using var context = await CreatePlayerContextAsync();
        var hasForeignRefresh = mode is "foreign_refresh" or "foreign_due_expiry";
        (string IncumbentEffectId, string RefreshDefinitionKey, string LateDefinitionKey)? refreshFixture = null;
        if (hasForeignRefresh)
            refreshFixture = await SeedOriginalMortalRefreshObserverAsync(
                context,
                lateInitialTurns: mode == "foreign_due_expiry" ? 1 : 3);
        else if (mode is "old_budget" or "new_node_old_effect")
            await SeedOriginalMortalObserverAsync(context, mode == "old_budget" ? "resource_spent" : "resource_damaged");
        var authority = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 2);
        var proposal = CreateRepairRoundtripProposal("treatment");
        var definition = proposal["consequenceDefinitions"]![0]!["definition"]!;
        if (mode == "reaction_only")
        {
            var reaction = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
            definition["components"] = reaction["components"]!.DeepClone();
            definition["components"]![0]!["payload"]!["eventType"] = "resource_spent";
            proposal["consequenceDefinitions"]![0]!["root"]!["slots"]![0]!["profileKey"] = "event_reaction";
        }
        definition["triggers"]![0]!["eventType"] = mode switch
        {
            "cycle" => "resource_damaged",
            "foreign_refresh" or "foreign_due_expiry" => "resource_gained",
            _ => "resource_spent"
        };
        var decision = Decision("materialize", proposal);
        decision["opportunityRef"] = authority.Opportunity.PublicRef;
        var response = Response(decision);
        var composed = WoundResponseInputComposer.Compose(authority.Binding, new[] { authority.Opportunity },
            response.WoundDecisions, response.Response, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath, composed.CommandRoot!.ToJsonString());
        var commands = new JsonObject { ["resourceChanges"] = new JsonArray() };
        foreach (var ordinal in new[] { 1, 2 })
            commands["resourceChanges"]!.AsArray().Add(new JsonObject
            {
                ["operation"] = mode == "cycle" ? "damage" : hasForeignRefresh && ordinal == 2 ? "gain" : "spend",
                ["resourceKey"] = mode == "cycle" ? "health" : "energy", ["amount"] = 1,
                ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                ["eventRef"] = $"turn_42:resource:{ordinal}", ["reason"] = "Принятый расход сил."
            });
        await context.WriteExactJsonAsync(ResourceMaterializationContract.CommandPath, commands.ToJsonString());
        var originalCanonical = new Dictionary<string, byte[]?>();
        foreach (var path in new[] { WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath, EffectCarrierCatalog.PlayerPath, EffectAcceptedTurnPlan.IdentityIndexPath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath })
            originalCanonical.Add(path, await context.FileSystem.ReadFileBytesAsync(path));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureMortalOriginalTurnAsync(lease);
        Assert.True(captured.Issues.Count == 0, Describe(captured.Issues));
        using var capture = Assert.IsType<ValidationService.MortalOriginalTurnCapture>(captured.Capture);
        var first = await capture.AdvanceNextResourceBoundaryAsync(lease);
        Assert.Empty(first.Issues);
        var checkpoint = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(first.Step!.Checkpoint);
        Assert.Single(checkpoint.AppliedTransitions);
        var originalActivationCount = mode == "old_budget" || hasForeignRefresh ? 1 : 0;
        Assert.Equal(originalActivationCount, checkpoint.EffectPrefix.AcceptedActivations.Count);
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(ReadOriginalMortalField(capture, "_resources"));
        if (hasForeignRefresh)
        {
            var producerCandidate = Assert.Single(ReadOriginalMortalCandidates(resources), value =>
                value.Activation.Identity.EffectId == EffectMaterializationTestFixture.EffectId);
            Assert.Equal(2, producerCandidate.ReactionOutputs.Count);
            var acceptedProducer = Assert.Single(checkpoint.EffectPrefix.AcceptedActivations, value =>
                value.Activation.Stamp.Identity.EffectId == EffectMaterializationTestFixture.EffectId);
            Assert.Equal(2, acceptedProducer.Candidate.ReactionOutputs.Count);
            Assert.All(acceptedProducer.Candidate.ReactionOutputs, value =>
                Assert.Equal("before_current_event", value.Dependency));
        }
        Assert.Equal(hasForeignRefresh ? 2 : 0, checkpoint.EffectPrefix.ReleasedReactions.Count);
        var arbiter = Assert.IsType<AcceptedEffectUseArbiter>(ReadOriginalMortalField(ReadOriginalMortalField(resources, "_state")!, "arbiter"));
        if (mode == "old_budget")
        {
            Assert.True(arbiter.TryGetRemainingUses(EffectMaterializationTestFixture.EffectId, out var remaining));
            Assert.Equal(1, remaining);
        }
        var checkpointCopy = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(typeof(object)
            .GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(checkpoint, null));
        var forged = await capture.MaterializeWoundAsync(lease, checkpointCopy);
        Assert.Contains(forged.Issues, issue => issue.Code == "mortal_wound_checkpoint_mismatch");
        Assert.True(capture.IsCurrentOwner);
        var originalPrepared = ReadOriginalMortalPrepared(resources);
        var result = await capture.MaterializeWoundAsync(lease, checkpoint);
        if (mode == "cycle")
        {
            Assert.Contains(result.Issues, issue => issue.Code == "resource_graph_cycle");
            Assert.Null(result.Wound);
            Assert.False(capture.IsCurrentOwner);
            Assert.False(resources.OwnsCurrentCheckpoint(checkpoint));
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _));
            foreach (var pair in originalCanonical)
                Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.AdvanceNextResourceBoundaryAsync(lease));
            return;
        }
        Assert.True(result.Issues.Count == 0, Describe(result.Issues));
        var installedPrepared = ReadOriginalMortalPrepared(resources);
        Assert.All(originalPrepared, prior => Assert.Contains(installedPrepared, current => ReferenceEquals(prior, current)));
        Assert.Equal(originalPrepared.Length + (mode == "reaction_only" ? 0 : 1), installedPrepared.Length);
        var wound = Assert.IsType<WoundMaterializationEnvelope>(result.Wound);
        var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(ReadOriginalMortalField(capture, "_effects"));
        var identity = Assert.IsType<EffectIdentityHistoryOwner>(ReadOriginalMortalField(draft, "identityRoot"));
        var allocations = identity.AllocationCount;
        var writes = identity.WriteCount;
        Assert.Same(result, await capture.MaterializeWoundAsync(lease, checkpoint));
        Assert.Equal(allocations, identity.AllocationCount);
        Assert.Equal(writes, identity.WriteCount);
        var retriedPrepared = ReadOriginalMortalPrepared(resources);
        Assert.Equal(installedPrepared.Length, retriedPrepared.Length);
        for (var index = 0; index < installedPrepared.Length; index++)
            Assert.Same(installedPrepared[index], retriedPrepared[index]);
        var next = await capture.AdvanceNextResourceBoundaryAsync(lease);
        Assert.Empty(next.Issues);
        var after = Assert.IsType<AcceptedMechanicsPlanner.ResourceClosedBoundaryCheckpoint>(next.Step!.Checkpoint);
        Assert.Single(after.EffectPrefix.AcceptedActivations, value => value.Activation.Stamp.Identity.EffectId == root.EffectId);
        Assert.Equal(originalActivationCount, checkpoint.EffectPrefix.AcceptedActivations.Count);
        if (mode == "reaction_only")
        {
            Assert.Single(after.EffectPrefix.ReleasedReactions);
            Assert.Equal(2, after.AppliedTransitions.Count);
        }
        else if (hasForeignRefresh)
        {
            Assert.Equal(2, after.EffectPrefix.ReleasedReactions.Count);
            Assert.Equal(3, after.AppliedTransitions.Count);
        }
        else
            Assert.Equal(3, after.AppliedTransitions.Count);
        var oldEventRefs = Array.Empty<string>();
        if (mode is "old_budget" or "new_node_old_effect")
        {
            var oldActivations = after.EffectPrefix.AcceptedActivations.Where(value =>
                value.Activation.Stamp.Identity.EffectId == EffectMaterializationTestFixture.EffectId).ToArray();
            Assert.Equal(mode == "old_budget" ? 2 : 1, oldActivations.Length);
            Assert.True(arbiter.TryGetRemainingUses(EffectMaterializationTestFixture.EffectId, out var remaining));
            Assert.Equal(mode == "old_budget" ? 0 : 1, remaining);
            if (mode == "old_budget")
                Assert.Equal(checkpoint.EffectPrefix.AcceptedActivations[0], oldActivations[0]);
            oldEventRefs = oldActivations.Select(value => value.Activation.Stamp.Identity.EventRef).ToArray();
        }
        if (mode == "old_budget")
        {
            var beforeCompletion = EffectIdentityState.Parse(
                System.Text.Json.JsonSerializer.SerializeToElement(identity.ReadSnapshot()),
                EffectAcceptedTurnPlan.IdentityIndexPath);
            Assert.Empty(beforeCompletion.Issues);
            var deferredOld = beforeCompletion.State!.Entries.Single(value =>
                value.EffectId == EffectMaterializationTestFixture.EffectId);
            Assert.Equal("active", deferredOld.State);
            Assert.Equal(new[] { "create" }, deferredOld.Transitions.Select(value => value.Kind));
        }
        else if (hasForeignRefresh)
        {
            var fixture = refreshFixture!.Value;
            var deferredEffects = ReadOriginalMortalDraftCarriers(draft).PlayerEffects!["activeEffects"]!.AsArray()
                .OfType<JsonObject>().ToArray();
            var deferredCarrier = deferredEffects.Single(value =>
                    value["effectId"]!.GetValue<string>() == fixture.IncumbentEffectId);
            Assert.Equal(1, deferredCarrier["lifetime"]!["remainingTurns"]!.GetValue<int>());
            var deferredProducer = deferredEffects.Single(value =>
                value["effectId"]!.GetValue<string>() == EffectMaterializationTestFixture.EffectId);
            Assert.Equal(2, deferredProducer["lifetime"]!["remainingUses"]!.GetValue<int>());
            Assert.DoesNotContain(deferredEffects, value =>
                value["source"]!["definitionKey"]!.GetValue<string>() == fixture.LateDefinitionKey);
            var beforeCompletion = EffectIdentityState.Parse(
                System.Text.Json.JsonSerializer.SerializeToElement(identity.ReadSnapshot()),
                EffectAcceptedTurnPlan.IdentityIndexPath);
            Assert.Empty(beforeCompletion.Issues);
            var deferredIncumbent = beforeCompletion.State!.Entries.Single(value =>
                value.EffectId == fixture.IncumbentEffectId);
            Assert.Equal("active", deferredIncumbent.State);
            Assert.Equal(new[] { "create" }, deferredIncumbent.Transitions.Select(value => value.Kind));
            var deferredProducerIdentity = beforeCompletion.State.Entries.Single(value =>
                value.EffectId == EffectMaterializationTestFixture.EffectId);
            Assert.Equal(new[] { "create" },
                deferredProducerIdentity.Transitions.Select(value => value.Kind));
        }
        var stale = await capture.MaterializeWoundAsync(lease, checkpoint);
        Assert.Contains(stale.Issues, issue => issue.Code == "mortal_wound_checkpoint_mismatch");
        Assert.Equal(allocations, identity.AllocationCount);
        Assert.Equal(writes, identity.WriteCount);
        if (mode == "old_budget")
        {
            var completed = await capture.CompleteEffectsAsync(lease);
            Assert.True(completed.Success, Describe(completed.Issues));
            var completedPlan = Assert.IsType<EffectAcceptedTurnPlan>(completed.Plan);
            var completedIdentity = EffectIdentityState.Parse(
                System.Text.Json.JsonSerializer.SerializeToElement(completedPlan.IdentityIndexAfterImage),
                EffectAcceptedTurnPlan.IdentityIndexPath);
            Assert.Empty(completedIdentity.Issues);
            var completedOld = completedIdentity.State!.Entries.Single(value =>
                value.EffectId == EffectMaterializationTestFixture.EffectId);
            Assert.Equal("expired", completedOld.State);
            Assert.Equal(new[] { "create", "consume", "expire" },
                completedOld.Transitions.Select(value => value.Kind));
            Assert.Equal(oldEventRefs,
                completedOld.Transitions.Skip(1).Select(value => value.EventRef));
            var completedCatalog = EffectCarrierCatalog.Build(completedPlan.ResourceTriggerCarriers);
            Assert.Empty(completedCatalog.Issues);
            Assert.False(completedCatalog.TryResolveOne(EffectMaterializationTestFixture.EffectId, out _));
            var completionAllocations = identity.AllocationCount;
            var completionWrites = identity.WriteCount;
            Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
            Assert.Equal(completionAllocations, identity.AllocationCount);
            Assert.Equal(completionWrites, identity.WriteCount);
        }
        else if (hasForeignRefresh)
        {
            var fixture = refreshFixture!.Value;
            var refreshRelease = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions, value =>
                value.Reaction.DownstreamSourceKey?.DefinitionKey == fixture.RefreshDefinitionKey);
            var lateRelease = Assert.Single(checkpoint.EffectPrefix.ReleasedReactions, value =>
                value.Reaction.DownstreamSourceKey?.DefinitionKey == fixture.LateDefinitionKey);
            Assert.Equal("turn_42:resource:1", refreshRelease.Reaction.TriggerEventRef);
            Assert.Equal("turn_42:resource:1", lateRelease.Reaction.TriggerEventRef);
            var completed = await capture.CompleteEffectsAsync(lease);
            Assert.True(completed.Success, Describe(completed.Issues));
            var completedPlan = Assert.IsType<EffectAcceptedTurnPlan>(completed.Plan);
            var producer = completedPlan.ActiveEffects.Single(value =>
                value["effectId"]!.GetValue<string>() == EffectMaterializationTestFixture.EffectId);
            Assert.Equal(1, producer["lifetime"]!["remainingUses"]!.GetValue<int>());
            var refreshed = completedPlan.ActiveEffects.Single(value =>
                value["effectId"]!.GetValue<string>() == fixture.IncumbentEffectId);
            Assert.Equal(2, refreshed["lifetime"]!["remainingTurns"]!.GetValue<int>());
            var completedIdentity = EffectIdentityState.Parse(
                System.Text.Json.JsonSerializer.SerializeToElement(completedPlan.IdentityIndexAfterImage),
                EffectAcceptedTurnPlan.IdentityIndexPath);
            Assert.Empty(completedIdentity.Issues);
            var completedLate = completedIdentity.State!.Entries.Single(value =>
                value.Source["definitionKey"]!.GetValue<string>() == fixture.LateDefinitionKey);
            var lateEffectId = completedLate.EffectId;
            var late = completedPlan.ActiveEffects.SingleOrDefault(value =>
                value["effectId"]!.GetValue<string>() == lateEffectId);
            if (mode == "foreign_due_expiry")
                Assert.Null(late);
            else
            {
                Assert.NotNull(late);
                Assert.Equal(2, late!["lifetime"]!["remainingTurns"]!.GetValue<int>());
            }
            Assert.DoesNotContain(after.EffectPrefix.AcceptedActivations, value =>
                value.Activation.Stamp.Identity.EffectId == lateEffectId);
            var completedProducer = completedIdentity.State!.Entries.Single(value =>
                value.EffectId == EffectMaterializationTestFixture.EffectId);
            Assert.Equal(new[] { "create", "consume" },
                completedProducer.Transitions.Select(value => value.Kind));
            var producerActivation = Assert.Single(checkpoint.EffectPrefix.AcceptedActivations, value =>
                value.Activation.Stamp.Identity.EffectId == EffectMaterializationTestFixture.EffectId);
            Assert.Equal(producerActivation.Activation.Stamp.Identity.EventRef,
                completedProducer.Transitions[1].EventRef);
            var completedIncumbent = completedIdentity.State!.Entries.Single(value =>
                value.EffectId == fixture.IncumbentEffectId);
            Assert.Equal(new[] { "create", "refresh", "consume" },
                completedIncumbent.Transitions.Select(value => value.Kind));
            Assert.Equal(refreshRelease.Reaction.EventRef, completedIncumbent.Transitions[1].EventRef);
            Assert.Equal(EffectAcceptedTurnPlanner.CreateLifecycleTransitionEventRef(
                    "turn_42:lifecycle:owner_turn_end:player_current",
                    fixture.IncumbentEffectId,
                    new ResourcePendingAuthorityBinding(
                        "accepted_application", completedIncumbent.Transitions[0].EventRef)),
                completedIncumbent.Transitions[2].EventRef);
            Assert.Equal(mode == "foreign_due_expiry"
                    ? new[] { "create", "expire" }
                    : new[] { "create", "consume" },
                completedLate.Transitions.Select(value => value.Kind));
            Assert.Equal(mode == "foreign_due_expiry" ? "expired" : "active", completedLate.State);
            Assert.Equal(lateRelease.Reaction.EventRef, completedLate.Transitions[0].EventRef);
            Assert.Equal(EffectAcceptedTurnPlanner.CreateLifecycleTransitionEventRef(
                    "turn_42:lifecycle:owner_turn_end:player_current",
                    lateEffectId,
                    new ResourcePendingAuthorityBinding(
                        "accepted_application", completedLate.Transitions[0].EventRef)),
                completedLate.Transitions[1].EventRef);
            Assert.NotEqual(completedLate.Transitions[0].EventRef, completedLate.Transitions[1].EventRef);
            Assert.Single(draft.Phases, receipt =>
                receipt.Phase == EffectAcceptedTurnPlanner.EffectDraftPhase.FinalLifetime);
            var completionAllocations = identity.AllocationCount;
            var completionWrites = identity.WriteCount;
            var completionPhaseCount = draft.Phases.Count;
            Assert.Same(completed, await capture.CompleteEffectsAsync(lease));
            Assert.Equal(completionAllocations, identity.AllocationCount);
            Assert.Equal(completionWrites, identity.WriteCount);
            Assert.Equal(completionPhaseCount, draft.Phases.Count);
        }
        foreach (var pair in originalCanonical)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Reads a retained implementation owner for identity and budget assertions without recreating authority.
    /// </summary>
    /// <param name="owner">
    /// Actual capture, draft or resource executor under test.
    /// </param>
    /// <param name="name">
    /// Exact private field containing the retained owner or state.
    /// </param>
    /// <returns>
    /// Existing field value; no mutable copy or replacement is created.
    /// </returns>
    private static object? ReadOriginalMortalField(object owner, string name) => owner.GetType()
        .GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(owner);

    /// <summary>
    /// Reads the retained carrier workspace before common effect completion makes the public draft image available.
    /// </summary>
    /// <param name="draft">
    /// Active effect draft whose mutable carrier workspace is under test.
    /// </param>
    /// <returns>
    /// A carrier input backed by the current workspace roots for read-only assertions. Callers must not mutate it.
    /// </returns>
    private static EffectCarrierCatalogInput ReadOriginalMortalDraftCarriers(
        EffectAcceptedTurnPlanner.EffectAcceptedDraft draft)
    {
        var workspace = ReadOriginalMortalField(draft, "workspace")!;
        var method = workspace.GetType().GetMethod("ToInput",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic)!;
        return Assert.IsType<EffectCarrierCatalogInput>(method.Invoke(workspace, null));
    }

    /// <summary>
    /// Captures complete detached wound and effect images from the current Mortal draft owners.
    /// </summary>
    /// <param name="draft">
    /// The active or revoked draft whose retained images are inspected.
    /// </param>
    /// <param name="identity">
    /// The draft's exact effect identity owner.
    /// </param>
    /// <returns>
    /// A detached JSON image containing every current wound carrier, wound identity and history,
    /// effect carrier, and effect identity root.
    /// </returns>
    private static JsonObject CaptureOriginalMortalDraftState(
        EffectAcceptedTurnPlanner.EffectAcceptedDraft draft,
        EffectIdentityHistoryOwner identity)
    {
        var wounds = Assert.IsType<WoundOperationBeforeData>(
            ReadOriginalMortalField(draft, "_currentWoundState"));
        var woundCarriers = Assert.IsType<WoundCarrierCatalogInput>(wounds.WoundCarriers);
        var effectCarriers = ReadOriginalMortalDraftCarriers(draft);
        var effectIdentity = Assert.IsType<JsonObject>(
            ReadOriginalMortalField(identity, "_root"));
        return new JsonObject
        {
            ["woundCarriers"] = new JsonObject
            {
                ["player"] = woundCarriers.PlayerWounds?.DeepClone(),
                ["npcs"] = woundCarriers.NpcWounds?.DeepClone(),
                ["enemies"] = woundCarriers.EnemyCombatants?.DeepClone(),
                ["allies"] = woundCarriers.AllyCombatants?.DeepClone(),
                ["afterlife"] = woundCarriers.AfterlifeProfiles?.DeepClone()
            },
            ["woundIdentity"] = wounds.WoundIdentity,
            ["woundHistory"] = wounds.WoundHistory,
            ["effectCarriers"] = new JsonObject
            {
                ["player"] = effectCarriers.PlayerEffects?.DeepClone(),
                ["npcs"] = effectCarriers.NpcEffects?.DeepClone(),
                ["enemies"] = effectCarriers.EnemyCombatants?.DeepClone(),
                ["allies"] = effectCarriers.AllyCombatants?.DeepClone(),
                ["afterlife"] = effectCarriers.AfterlifeProfiles?.DeepClone(),
                ["spiritualConflict"] = effectCarriers.SpiritualConflict?.DeepClone()
            },
            ["effectIdentity"] = effectIdentity.DeepClone()
        };
    }

    /// <summary>
    /// Reads actual prepared resource objects to prove original identities survive refresh and retries add nothing.
    /// </summary>
    /// <param name="resources">
    /// Actual retained resource executor whose graph is being inspected.
    /// </param>
    /// <returns>
    /// Original prepared object references in their retained order.
    /// </returns>
    private static object[] ReadOriginalMortalPrepared(AcceptedMechanicsPlanner.ResourceExecutionSession resources) =>
        ((System.Collections.IEnumerable)ReadOriginalMortalField(ReadOriginalMortalField(resources, "_state")!,
            "preparedMutations")!).Cast<object>().ToArray();

    /// <summary>
    /// Reads the actual retained trigger candidates so refresh behavior can be asserted by reference identity.
    /// </summary>
    /// <param name="resources">
    /// Actual retained resource executor whose graph preparation is being inspected.
    /// </param>
    /// <returns>
    /// Trigger candidate objects in their current retained order.
    /// </returns>
    private static EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate[] ReadOriginalMortalCandidates(
        AcceptedMechanicsPlanner.ResourceExecutionSession resources)
    {
        var state = ReadOriginalMortalField(resources, "_state")!;
        var preparation = ReadOriginalMortalField(state, "graphPreparation")!;
        var property = preparation.GetType().GetProperty("TriggerCandidates",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic)!;
        return ((IEnumerable<EffectAcceptedTurnPlanner.EffectResourceTriggerCandidate>)property.GetValue(preparation)!)
            .ToArray();
    }

    /// <summary>
    /// Reads the current source exports to verify shared historical authority survives future-branch retirement.
    /// </summary>
    /// <param name="resources">
    /// Actual retained resource executor whose graph preparation is being inspected.
    /// </param>
    /// <returns>
    /// Source exports in the current retained graph image.
    /// </returns>
    private static ResourceMutationSourceExport[] ReadOriginalMortalSources(
        AcceptedMechanicsPlanner.ResourceExecutionSession resources)
    {
        var state = ReadOriginalMortalField(resources, "_state")!;
        var preparation = ReadOriginalMortalField(state, "graphPreparation")!;
        var property = preparation.GetType().GetProperty("SourceExports",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic)!;
        return ((IEnumerable<ResourceMutationSourceExport>)property.GetValue(preparation)!).ToArray();
    }

    /// <summary>
    /// Seeds an unrelated reaction that releases one refresh and one new reaction definition during the first
    /// resource boundary, while keeping both applications owned by common effect completion.
    /// </summary>
    /// <param name="context">
    /// Initialized Mortal fixture whose next snapshot will sign the producer, incumbent and child definitions.
    /// </param>
    /// <param name="lateInitialTurns">
    /// Positive initial owner-turn lifetime assigned to the reaction-created child definition.
    /// The default of three preserves the existing nonterminal <c>foreign_refresh</c> contour.
    /// </param>
    /// <returns>
    /// The incumbent effect identity plus the refresh and late-reaction definition keys used by the assertions.
    /// </returns>
    private static async Task<(string IncumbentEffectId, string RefreshDefinitionKey, string LateDefinitionKey)>
        SeedOriginalMortalRefreshObserverAsync(ResourceMaterializationTestContext context, int lateInitialTurns = 3)
    {
        const string producerDefinitionKey = "original_mortal_foreign_refresh_producer";
        const string producerStackKey = "original-mortal-foreign-refresh-producer";
        const string refreshDefinitionKey = "original_mortal_foreign_refresh_target";
        const string refreshStackKey = "original-mortal-foreign-refresh-target";
        const string lateDefinitionKey = "original_mortal_late_resource_reaction";
        const string lateStackKey = "original-mortal-late-resource-reaction";
        const string incumbentEffectId = "effect_original_mortal_refresh_incumbent";
        const string incumbentTransitionId = "effect_transition_original_mortal_refresh_incumbent";
        const string incumbentEventRef = "turn_42:original_mortal_refresh_incumbent";

        var producerDefinition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        producerDefinition["definitionKey"] = producerDefinitionKey;
        producerDefinition["stacking"]!["stackKey"] = producerStackKey;
        producerDefinition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["initialUses"] = 2,
            ["consumingEventTypes"] = new JsonArray("resource_spent")
        };
        var refreshReaction = producerDefinition["components"]![0]!.DeepClone().AsObject();
        refreshReaction["componentId"] = "component_foreign_refresh";
        refreshReaction["payload"]!["eventType"] = "resource_spent";
        refreshReaction["payload"]!["resultKind"] = "apply_definition";
        refreshReaction["payload"]!["dependency"] = "before_current_event";
        refreshReaction["payload"]!["definitionKey"] = refreshDefinitionKey;
        refreshReaction["payload"]!["parameters"] = new JsonObject { ["amount"] = 3 };
        refreshReaction["payload"]!["maxExpansion"] = 2;
        var lateReaction = refreshReaction.DeepClone().AsObject();
        lateReaction["componentId"] = "component_foreign_late_reaction";
        lateReaction["payload"]!["definitionKey"] = lateDefinitionKey;
        lateReaction["payload"]!["parameters"] = new JsonObject();
        producerDefinition["components"] = new JsonArray(refreshReaction, lateReaction);
        producerDefinition["triggers"]![0]!["triggerId"] = "on_foreign_resource_spent";
        producerDefinition["triggers"]![0]!["eventType"] = "resource_spent";
        producerDefinition["triggers"]![0]!["componentIds"] = new JsonArray(
            "component_foreign_refresh", "component_foreign_late_reaction");
        producerDefinition["triggers"]![0]!["consumeUses"] = true;

        var refreshDefinition = EffectMaterializationTestFixture.CreateDefinition("periodic_restore");
        refreshDefinition["definitionKey"] = refreshDefinitionKey;
        refreshDefinition["stacking"]!["stackKey"] = refreshStackKey;
        refreshDefinition["stacking"]!["policy"] = "refresh";
        refreshDefinition["stacking"]!["maxStacks"] = 1;
        refreshDefinition["stacking"]!["atMaximum"] = "no_change";
        refreshDefinition["stacking"]!["refreshMode"] = "reset";
        refreshDefinition["triggers"]![0]!["triggerId"] = "on_foreign_resource_restored";
        refreshDefinition["triggers"]![0]!["eventType"] = "resource_restored";

        var lateDefinition = EffectMaterializationTestFixture.CreateDefinition("event_reaction");
        lateDefinition["definitionKey"] = lateDefinitionKey;
        lateDefinition["stacking"]!["stackKey"] = lateStackKey;
        lateDefinition["stacking"]!["policy"] = "independent";
        lateDefinition["stacking"]!["maxStacks"] = 1;
        lateDefinition["lifetime"]!["initialTurns"] = lateInitialTurns;
        lateDefinition["components"]![0]!["payload"]!["eventType"] = "resource_gained";
        lateDefinition["components"]![0]!["payload"]!["resultKind"] = "suspend";
        lateDefinition["components"]![0]!["payload"]!["dependency"] = "after_current_event";
        lateDefinition["components"]![0]!["payload"]!["maxExpansion"] = 1;
        lateDefinition["triggers"]![0]!["triggerId"] = "on_late_resource_gained";
        lateDefinition["triggers"]![0]!["eventType"] = "resource_gained";

        var producer = EffectMaterializationTestFixture.CreateCanonicalEffect("player", "event_reaction");
        producer["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
            ["definitionKey"] = producerDefinitionKey
        };
        producer["display"]!["sourceLabel"] = "Чужой отклик";
        producer["components"] = producerDefinition["components"]!.DeepClone();
        producer["triggers"] = producerDefinition["triggers"]!.DeepClone();
        producer["stacking"]!["stackKey"] = producerStackKey;
        producer["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 2,
            ["consumingTriggerIds"] = new JsonArray("on_foreign_resource_spent")
        };

        var incumbent = EffectMaterializationTestFixture.CreateCanonicalEffect("player", "periodic_restore");
        incumbent["effectId"] = incumbentEffectId;
        incumbent["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
            ["definitionKey"] = refreshDefinitionKey
        };
        incumbent["display"]!["sourceLabel"] = "Чужой обновляемый эффект";
        incumbent["components"] = refreshDefinition["components"]!.DeepClone();
        incumbent["triggers"] = refreshDefinition["triggers"]!.DeepClone();
        incumbent["stacking"] = refreshDefinition["stacking"]!.DeepClone();
        incumbent["stacking"]!.AsObject().Remove("atMaximum");
        incumbent["stacking"]!["currentStacks"] = 1;
        incumbent["lifetime"]!["remainingTurns"] = 1;
        incumbent["chronology"]!["createdEventRef"] = incumbentEventRef;
        incumbent["chronology"]!["lastTransitionId"] = incumbentTransitionId;

        await context.WriteExactJsonAsync(EffectCarrierCatalog.PlayerPath, new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeEffects"] = new JsonArray(producer, incumbent)
        }.ToJsonString());
        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(producer, incumbent);
        identity["entries"]![1]!["transitions"]![0]!["transitionId"] = incumbentTransitionId;
        identity["entries"]![1]!["transitions"]![0]!["eventRef"] = incumbentEventRef;
        await context.WriteExactJsonAsync(EffectAcceptedTurnPlan.IdentityIndexPath, identity.ToJsonString());
        await context.WriteExactJsonAsync(EffectMaterializationTestContext.MaterializableSkillPath, new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(new JsonObject
            {
                ["skillId"] = EffectMaterializationTestContext.MaterializableSkillId,
                ["skillName"] = "Кровавый след",
                ["skillDescription"] = "Принятый навык обновляет отдельный эффект.",
                ["rarity"] = "common",
                ["actionCost"] = "Main",
                ["combatEffect"] = new JsonObject
                {
                    ["isActivatedEffect"] = true,
                    ["actionName"] = "Кровавый след",
                    ["actionCost"] = "Main",
                    ["effects"] = new JsonArray(new JsonObject
                    {
                        ["effectType"] = "Damage",
                        ["value"] = "10%",
                        ["targetType"] = "enemy",
                        ["effectDescription"] = "Проверяемый след навыка.",
                        ["poiseDamage"] = "5%"
                    })
                },
                ["activeEffectDefinitions"] = new JsonArray(
                    producerDefinition, refreshDefinition, lateDefinition)
            }),
            ["removeActiveSkills"] = new JsonArray()
        }.ToJsonString());
        return (incumbentEffectId, refreshDefinitionKey, lateDefinitionKey);
    }

    /// <summary>
    /// Seeds a real skill-owned consuming observer into the original signed effect base.
    /// </summary>
    /// <param name="context">
    /// Initialized Mortal fixture whose next snapshot will sign this source and effect.
    /// </param>
    /// <param name="eventType">
    /// Resource event observed by the effect.
    /// </param>
    /// <param name="resourceOutput">
    /// True gives the observer an energy-gain consequence; false selects a non-resource resistance modifier.
    /// </param>
    /// <param name="reactionResultKind">
    /// Optional terminal reaction kind; <see langword="null"/> keeps a direct mechanical component.
    /// </param>
    /// <param name="remainingUses">
    /// Positive initial and remaining use count assigned to the observer.
    /// </param>
    private static async Task SeedOriginalMortalObserverAsync(ResourceMaterializationTestContext context, string eventType,
        bool resourceOutput = false, string? reactionResultKind = null, int remainingUses = 2)
    {
        var profile = reactionResultKind != null ? "event_reaction" : resourceOutput ? "periodic_gain" : "resistance_modifier";
        var definition = EffectMaterializationTestFixture.CreateDefinition(profile);
        if (resourceOutput)
            definition["components"]![0]!["payload"]!["resource"] = "energy";
        if (reactionResultKind != null)
        {
            definition["components"]![0]!["payload"]!["eventType"] = eventType;
            definition["components"]![0]!["payload"]!["resultKind"] = reactionResultKind;
        }
        definition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses", ["initialUses"] = remainingUses,
            ["consumingEventTypes"] = new JsonArray(eventType)
        };
        definition["triggers"]![0]!["eventType"] = eventType;
        definition["triggers"]![0]!["consumeUses"] = true;
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("player", profile);
        if (resourceOutput)
            effect["components"]![0]!["payload"]!["resource"] = "energy";
        if (reactionResultKind != null)
        {
            effect["components"]![0]!["payload"]!["eventType"] = eventType;
            effect["components"]![0]!["payload"]!["resultKind"] = reactionResultKind;
        }
        effect["source"] = new JsonObject
        {
            ["kind"] = "skill", ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        effect["display"]!["sourceLabel"] = "Кровавый след";
        effect["lifetime"] = new JsonObject
        {
            ["mode"] = "uses", ["remainingUses"] = remainingUses,
            ["consumingTriggerIds"] = new JsonArray(definition["triggers"]![0]!["triggerId"]!.GetValue<string>())
        };
        effect["triggers"] = definition["triggers"]!.DeepClone();
        await context.WriteExactJsonAsync(EffectCarrierCatalog.PlayerPath,
            new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray(effect) }.ToJsonString());
        await context.WriteExactJsonAsync(EffectAcceptedTurnPlan.IdentityIndexPath,
            EffectMaterializationTestFixture.CreateIdentityIndex(effect).ToJsonString());
        await context.WriteExactJsonAsync(EffectMaterializationTestContext.MaterializableSkillPath, new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(new JsonObject
            {
                ["skillId"] = EffectMaterializationTestContext.MaterializableSkillId, ["skillName"] = "Кровавый след",
                ["skillDescription"] = "Принятый навык наблюдает расход сил.", ["rarity"] = "common", ["actionCost"] = "Main",
                ["combatEffect"] = new JsonObject
                {
                    ["isActivatedEffect"] = true, ["actionName"] = "Кровавый след", ["actionCost"] = "Main",
                    ["effects"] = new JsonArray(new JsonObject
                    {
                        ["effectType"] = "Damage", ["value"] = "10%", ["targetType"] = "enemy",
                        ["effectDescription"] = "Проверяемый след навыка.", ["poiseDamage"] = "5%"
                    })
                },
                ["activeEffectDefinitions"] = new JsonArray(definition)
            }),
            ["removeActiveSkills"] = new JsonArray()
        }.ToJsonString());
    }

    [Theory]
    [InlineData("large_snapshot")]
    [InlineData("snapshot_payload")]
    [InlineData("game_state/control/validation_repair_request.json")]
    [InlineData("ready/turn_complete.json")]
    [InlineData("game_state/misc/player_interactions.json")]
    [InlineData("superseded_effect_binding")]
    [InlineData(null)]
    [InlineData(AcceptedMechanicsPlan.WoundCommandPath)]
    [InlineData(MortalWoundOccurrenceState.StatePath)]
    [InlineData(MortalWoundOpportunityReceiptState.StatePath)]
    public async Task OriginalMortalCapture_ValidatesSignedSelectionWithoutMaterializingItsWound(string? changedPath)
    {
        await using var context = await CreatePlayerContextAsync();
        var extraPaths = changedPath == "large_snapshot"
            ? Enumerable.Range(0, 65).Select(index => $"game_state/misc/capture_fixture_{index}.json").ToArray()
            : Array.Empty<string>();
        foreach (var path in extraPaths)
            await context.WriteExactJsonAsync(path, "{}");
        var authority = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 2, extraSnapshotPaths: extraPaths);
        var decision = Decision("materialize", CreateRepairRoundtripProposal("treatment"));
        decision["opportunityRef"] = authority.Opportunity.PublicRef;
        var response = Response(decision);
        var composed = WoundResponseInputComposer.Compose(authority.Binding, new[] { authority.Opportunity },
            response.WoundDecisions, response.Response, Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composed.Success, Describe(composed.Issues));
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.WoundCommandPath, composed.CommandRoot!.ToJsonString());
        var before = await context.FileSystem.ReadFileBytesAsync(WoundCarrierCatalog.PlayerPath);
        var effectBefore = await context.FileSystem.ReadFileBytesAsync(EffectCarrierCatalog.PlayerPath);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await context.Validator.CaptureMortalOriginalTurnAsync(lease);
        Assert.True(result.Issues.Count == 0, Describe(result.Issues));
        using var owned = Assert.IsType<ValidationService.MortalOriginalTurnCapture>(result.Capture);
        Assert.True(owned.IsCurrentOwner);
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, WoundCarrierCatalog.PlayerPath));
        Assert.Equal(effectBefore, await context.FileSystem.ReadFileBytesAsync(lease, EffectCarrierCatalog.PlayerPath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
        Assert.True(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out var baseline));
        Assert.Empty(baseline.Plan!.WoundApplicationRootEffectBindings);
        Assert.Empty(baseline.Plan.AllocatedEffectIds);
        Assert.Empty(await owned.CheckRetainedInputsAsync(lease));
        if (changedPath != null)
        {
            var mutationPath = changedPath;
            if (changedPath is "snapshot_payload" or "large_snapshot")
            {
                var manifest = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
                    "game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
                mutationPath = manifest["files"]![changedPath == "large_snapshot"
                    ? extraPaths[^1] : MortalWoundOccurrenceState.StatePath]!.GetValue<string>();
            }
            if (changedPath == "superseded_effect_binding")
                EffectAcceptedTurnPlanAuthority.InvalidateValidated(context.FileSystem, lease);
            else
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, mutationPath, System.Text.Encoding.UTF8.GetBytes("{\"changed\":true}"));
            var changed = await owned.CheckRetainedInputsAsync(lease);
            Assert.NotEmpty(changed);
            Assert.False(owned.IsCurrentOwner);
            Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _));
        }
    }
}
