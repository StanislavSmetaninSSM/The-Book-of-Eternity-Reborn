using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData(2, false, false)]
    [InlineData(1, false, false)]
    [InlineData(1, false, true)]
    [InlineData(1, true, false)]
    public async Task OriginalSpiritualGeneration_OwnRootActivationRequiresMaterializedDependencyCut(int? initialUses, bool corruptBefore, bool relocatedStack)
    {
        var consumesUse = initialUses.HasValue;
        await using var context = await CreateCompleteConflictFrameContextAsync();
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        foreach (var profile in profiles["profiles"]!.AsArray())
            profile!["standardArts"]!["pressure"] = 2;
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        const string soulPath = "game_state/meta/soul_state.json";
        var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(soulPath));
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 2;
        await context.WriteExactJsonAsync(soulPath, soul.ToJsonString());
        var conflict = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        conflict["activeConflict"]!["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!["pressure"] = 2;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, conflict.ToJsonString());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [5, 15, 3, 18, 3, 18]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var initial = await ReadProjectedSourceContinuationCandidateAsync(context);
        SetPlayerHarm(initial, 0, initialUses == 1 ? "fractured" : "strained", 5, 15);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, initial.ToJsonString());
        await using var firstLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(firstLease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(firstLease));
        var first = await capture.AdvanceNextResourceExchangeAsync(firstLease);
        AssertNoConflictFrameErrors(first.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var admission = await capture.AdmitWoundSourceAsync(firstLease, first.Step!.Interval!, Assert.Single(source.Sources));
        AssertNoConflictFrameErrors(admission.Issues);
        var offer = await capture.ReadWoundOpportunityAsync(firstLease, admission.Admission!);
        AssertNoConflictFrameErrors(offer.Issues);
        const string narration = "Чужое давление надломило волю души.";
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offer.Opportunity!.PublicRef,
            "spiritual_action_cost_burden", "guard", "player").GetRawText())!;
        decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "own_ap_spent", ["eventType"] = "resource_spent", ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_001"), ["consumeUses"] = consumesUse,
            ["resolutionMode"] = "deterministic"
        });
        if (consumesUse)
            decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["lifetime"] = new JsonObject
            {
                ["mode"] = "uses", ["initialUses"] = initialUses!.Value,
                ["consumingEventTypes"] = new JsonArray("resource_spent")
            };
        if (initialUses == 1)
        {
            decision["proposal"]!["severity"] = "II";
            var initialDefinitions = decision["proposal"]!["consequenceDefinitions"]!.AsArray();
            var observation = initialDefinitions[0]!.DeepClone();
            observation["definitionRef"] = "maneuver_observer";
            observation["definition"]!["definitionKey"] = "maneuver_observer";
            observation["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_observer";
            observation["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
            observation["definition"]!["triggers"]![0]!["consumeUses"] = false;
            observation["definition"]!["triggers"]![0]!["priority"] = 99;
            observation["definition"]!["lifetime"] = new JsonObject
            {
                ["mode"] = "source_bound", ["activePredicate"] = "active", ["onSourceLoss"] = "expire"
            };
            initialDefinitions.Add(observation);
        }
        var inserted = await capture.MaterializeWoundAsync(firstLease, admission.Admission!,
            JsonSerializer.SerializeToElement(decision), narration);
        AssertNoConflictFrameErrors(inserted.Issues);
        var wound = inserted.Wound!;
        var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings,
            value => value.DefinitionKey == decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["definitionKey"]!.GetValue<string>());
        await firstLease.DisposeAsync();

        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        SetPlayerHarm(candidate, 1, initialUses == 1 ? "overwhelmed" : "fractured", 3, 18);
        candidate["activeConflict"]!["exchangeLog"]![1]!["spiritualWoundTarget"] = new JsonObject
        {
            ["actorType"] = "player_soul", ["actorId"] = "player_soul", ["retraumaWoundRef"] = wound.WoundId
        };
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonicalBefore = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(second.Issues);
        Assert.Contains(second.Step!.Interval!.EffectAfter.AcceptedActivations,
            activation => activation.Activation.Stamp.Identity.EffectId == root.EffectId);
        if (initialUses == 1)
        {
            var actual = second.Step.Interval.EffectAfter.AcceptedActivations
                .Where(value => wound.Consequences.OwnedEffectSources.RootBindings.Any(binding =>
                    binding.EffectId == value.Activation.Stamp.Identity.EffectId)).ToArray();
            Assert.Equal(2, actual.Length);
            Assert.False(actual[0].Activation.Stamp.ConsumesUse);
            Assert.True(actual[1].Activation.EffectTerminal);
        }
        var secondAdmission = await capture.AdmitWoundSourceAsync(lease, second.Step.Interval,
            Assert.Single(source.Sources, value => value.ExchangeId == "exchange_source_second"));
        AssertNoConflictFrameErrors(secondAdmission.Issues);
        var secondOffer = await capture.ReadWoundOpportunityAsync(lease, secondAdmission.Admission!);
        AssertNoConflictFrameErrors(secondOffer.Issues);
        decision["opportunityRef"] = secondOffer.Opportunity!.PublicRef;
        decision["proposal"]!["severity"] = initialUses == 1 ? "III" : "II";
        var definitions = decision["proposal"]!["consequenceDefinitions"]!.AsArray();
        if (relocatedStack)
            definitions[0]!["definition"]!["stacking"]!["stackKey"] = "relocated_terminal_guard";
        var extra = definitions[0]!.DeepClone();
        extra["definitionRef"] = "maneuver_burden";
        extra["definition"]!["definitionKey"] = "maneuver_burden";
        extra["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_burden";
        extra["definition"]!["components"]![0]!["payload"]!["operation"] = initialUses == 1 ? "pressure" : "maneuver";
        definitions.Add(extra);
        if (initialUses == 1)
            foreach (var definition in definitions)
                definition!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 2;
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(OriginalCaptureField(capture, "_effects"));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var issues = new List<ValidationIssue>();
        var before = draft.ReadCurrentWoundView(capture, resources, source, draft.WoundReadVersion, issues)!;
        AssertNoConflictFrameErrors(issues);
        if (corruptBefore)
        {
            // Fault injection changes only the candidate operation-before budget.
            // Actual writer state and accepted arbiter evidence stay untouched.
            var invalidCarriers = before.EffectCarriers!;
            var invalidRoot = invalidCarriers.AfterlifeProfiles!["profiles"]!.AsArray().OfType<JsonObject>()
                .SelectMany(value => value["activeEffects"] as JsonArray ?? new JsonArray())
                .OfType<JsonObject>().Single(value => value["effectId"]!.GetValue<string>() == root.EffectId);
            invalidRoot["lifetime"]!["remainingUses"] = 2;
            before = new WoundOperationBeforeData(before.WoundCarriers, before.WoundIdentity, before.WoundHistory,
                invalidCarriers, before.EffectIdentity);
            Assert.True(EffectCarrierCatalog.Build(before.EffectCarriers!).TryResolveOne(root.EffectId, out var corrupted));
            Assert.Equal(2, corrupted.Effect["lifetime"]!["remainingUses"]!.GetValue<int>());
            draft.GetType().GetField("_currentWoundState",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(draft, before);
        }
        var version = draft.WoundReadVersion;
        var phases = draft.Phases.Count;
        var applied = OriginalContinuationApplied(resources);
        var identityOwner = Assert.IsType<EffectIdentityHistoryOwner>(OriginalCaptureField(draft, "identityRoot"));
        var workspace = OriginalCaptureField(draft, "workspace")!;
        var editCount = workspace.GetType().GetProperty("EditCount",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var editsBefore = Assert.IsType<int>(editCount.GetValue(workspace));
        var writesBefore = identityOwner.WriteCount;
        var allocationsBefore = identityOwner.AllocationCount;
        var arbiter = Assert.IsType<AcceptedEffectUseArbiter>(OriginalCaptureField(
            OriginalCaptureField(resources, "_state")!, "arbiter"));
        if (consumesUse)
        {
            Assert.True(arbiter.TryGetRemainingUses(root.EffectId, out var remaining));
            Assert.Equal(initialUses - 1, remaining);
        }
        if (!corruptBefore)
        {
            var cached = resources.CaptureSpiritualMechanics(capture, source, issues)!;
            AssertNoConflictFrameErrors(issues);
            Assert.NotNull(cached.ReadCurrentWoundView(issues));
            Assert.True(cached.IsCurrent);
            var selection = await ValidationService.SpiritualOriginalTurnCapture.WoundSelection.SelectAsync(
                capture, lease, secondAdmission.Admission!, JsonSerializer.SerializeToElement(decision), narration, issues);
            AssertNoConflictFrameErrors(issues);
            var preparedBefore = draft.AdvanceForWoundInsertion(selection!, issues);
            AssertNoConflictFrameErrors(issues);
            Assert.NotNull(preparedBefore);
            Assert.Equal(1, draft.DependencyCutVersion);
            Assert.Equal(version, draft.WoundReadVersion);
            Assert.False(draft.HasPendingWoundIntegration);
            Assert.False(cached.IsCurrent);
            var staleIssues = new List<ValidationIssue>();
            Assert.Null(cached.ReadCurrentWoundView(staleIssues));
            Assert.Contains(staleIssues, issue => issue.Code == "spiritual_wound_read_stale");
            Assert.Same(preparedBefore, draft.AdvanceForWoundInsertion(selection!, issues));
            Assert.Equal(1, draft.DependencyCutVersion);
            if (consumesUse)
            {
                Assert.True(arbiter.TryGetRemainingUses(root.EffectId, out var remaining));
                Assert.Equal(initialUses - 1, remaining);
                var exists = EffectCarrierCatalog.Build(preparedBefore!.Data.EffectCarriers!)
                    .TryResolveOne(root.EffectId, out var consumed);
                Assert.Equal(initialUses > 1, exists);
                if (exists)
                    Assert.Equal(initialUses - 1, consumed.Effect["lifetime"]!["remainingUses"]!.GetValue<int>());
            }
        }
        var result = await capture.MaterializeWoundAsync(lease, secondAdmission.Admission!,
            JsonSerializer.SerializeToElement(decision), narration);
        if (!corruptBefore)
        {
            AssertNoConflictFrameErrors(result.Issues);
            Assert.Equal(wound.WoundId, result.Wound!.WoundId);
            Assert.Equal(initialUses == 1 ? 3 : 2, result.Wound.Severity.Rank);
            Assert.Equal(version + 1, draft.WoundReadVersion);
            Assert.Equal(phases, draft.Phases.Count);
            var changed = draft.ReadCurrentWoundView(capture, resources, source, draft.WoundReadVersion, issues)!;
            AssertNoConflictFrameErrors(issues);
            var retired = changed.EffectIdentity!["entries"]!.AsArray().OfType<JsonObject>()
                .Single(value => value["effectId"]!.GetValue<string>() == root.EffectId);
            Assert.Equal(initialUses == 1 ? new[] { "create", "expire" } :
                new[] { "create", consumesUse ? "consume" : "trigger", "expire" }, retired["transitions"]!.AsArray()
                .Select(value => value!["kind"]!.GetValue<string>()));
            var ownActivation = Assert.Single(second.Step.Interval.EffectAfter.AcceptedActivations,
                value => value.Activation.Stamp.Identity.EffectId == root.EffectId);
            Assert.Equal(ownActivation.Activation.Stamp.Identity.EventRef,
                retired["transitions"]![1]!["eventRef"]!.GetValue<string>());
            var successor = result.Wound.Consequences.OwnedEffectSources.RootBindings.Single(value => value.DefinitionKey == root.DefinitionKey);
            var successorEntry = changed.EffectIdentity!["entries"]!.AsArray().OfType<JsonObject>()
                .Single(value => value["effectId"]!.GetValue<string>() == successor.EffectId);
            var parents = successorEntry["transitions"]![0]!["sourceEffectIds"]!.AsArray();
            if (relocatedStack)
                Assert.Empty(parents);
            else
                Assert.Contains(parents, value => value!.GetValue<string>() == root.EffectId);
            var retry = await capture.MaterializeWoundAsync(lease, secondAdmission.Admission!,
                JsonSerializer.SerializeToElement(decision), narration);
            Assert.Same(result, retry);
            Assert.Equal(applied, OriginalContinuationApplied(resources));
            if (consumesUse)
            {
                Assert.Same(arbiter, OriginalCaptureField(OriginalCaptureField(resources, "_state")!, "arbiter"));
                Assert.True(arbiter.TryGetRemainingUses(root.EffectId, out var remaining));
                Assert.Equal(initialUses - 1, remaining);
                foreach (var newRoot in result.Wound.Consequences.OwnedEffectSources.RootBindings)
                {
                    if (!arbiter.TryGetRemainingUses(newRoot.EffectId, out var newRemaining))
                        Assert.Equal("maneuver_observer", newRoot.DefinitionKey);
                    else
                        Assert.Equal(initialUses, newRemaining);
                }
            }
            Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            if (initialUses == 1)
            {
                var observer = wound.Consequences.OwnedEffectSources.RootBindings.Single(value => value.DefinitionKey == "maneuver_observer");
                var observerIdentity = changed.EffectIdentity!["entries"]!.AsArray().OfType<JsonObject>()
                    .Single(value => value["effectId"]!.GetValue<string>() == observer.EffectId);
                Assert.Equal(new[] { "create", "trigger", "expire" }, observerIdentity["transitions"]!.AsArray()
                    .Select(value => value!["kind"]!.GetValue<string>()));
            }
            var previousRetired = retired.DeepClone();
            await lease.DisposeAsync();
            var thirdCandidate = await ReadProjectedSourceContinuationCandidateAsync(context);
            var active = thirdCandidate["activeConflict"]!;
            var thirdExchange = active["exchangeLog"]![1]!.DeepClone();
            thirdExchange["exchangeId"] = "exchange_source_third";
            thirdExchange["before"] = thirdExchange["after"]!.DeepClone();
            thirdExchange["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 4;
            thirdExchange["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 5;
            active["exchangeLog"]!.AsArray().Add(thirdExchange);
            SetPlayerHarm(thirdCandidate, 2, initialUses == 1 ? "broken" : "overwhelmed", 3, 18);
            if (initialUses == 1)
            {
                thirdExchange["actionCostAudit"]!["player"]!["effectiveCost"] = 3;
                thirdExchange["actionCostAudit"]!["player"]!["after"] = 1;
            }
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, thirdCandidate.ToJsonString());
            await using var thirdLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var third = await capture.AdvanceNextResourceExchangeAsync(thirdLease);
            AssertNoConflictFrameErrors(third.Issues);
            Assert.Contains(third.Step!.Interval!.EffectAfter.AcceptedActivations,
                value => value.Activation.Stamp.Identity.EffectId == root.EffectId);
            var currentRoots = result.Wound.Consequences.OwnedEffectSources.RootBindings;
            foreach (var currentRoot in currentRoots)
            {
                var activation = Assert.Single(third.Step.Interval.EffectAfter.AcceptedActivations,
                    value => value.Activation.Stamp.Identity.EffectId == currentRoot.EffectId);
                if (arbiter.TryGetRemainingUses(currentRoot.EffectId, out var remaining))
                {
                    Assert.Equal(initialUses, activation.Activation.Stamp.UsesBefore);
                    Assert.Equal(initialUses - 1, activation.Activation.UsesAfter);
                    Assert.Equal(initialUses - 1, remaining);
                    Assert.Equal(initialUses == 1, activation.Activation.EffectTerminal);
                }
            }
            var thirdAdmission = await capture.AdmitWoundSourceAsync(thirdLease, third.Step.Interval,
                Assert.Single(source.Sources, value => value.ExchangeId == "exchange_source_third"));
            AssertNoConflictFrameErrors(thirdAdmission.Issues);
            var thirdOffer = await capture.ReadWoundOpportunityAsync(thirdLease, thirdAdmission.Admission!);
            AssertNoConflictFrameErrors(thirdOffer.Issues);
            decision["opportunityRef"] = thirdOffer.Opportunity!.PublicRef;
            decision["proposal"]!["severity"] = initialUses == 1 ? "IV" : "III";
            var thirdDefinition = definitions[0]!.DeepClone();
            if (initialUses == 1)
            {
                thirdDefinition["definitionRef"] = "final_roll_hindrance";
                var hindrance = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition("spiritual_roll_hindrance",
                    targetKind: "player", definitionKey: "final_roll_hindrance");
                hindrance["links"] = new JsonArray();
                thirdDefinition["definition"] = hindrance;
                thirdDefinition["root"]!["slots"]![0]!["profileKey"] = "spiritual_roll_hindrance";
            }
            else
            {
                thirdDefinition["definitionRef"] = "pressure_burden";
                thirdDefinition["definition"]!["definitionKey"] = "pressure_burden";
                thirdDefinition["definition"]!["stacking"]!["stackKey"] = "stack_pressure_burden";
                thirdDefinition["definition"]!["components"]![0]!["payload"]!["operation"] = "pressure";
            }
            definitions.Add(thirdDefinition);
            foreach (var definition in definitions)
                if (definition!["definition"]!["components"]![0]!["profile"]!.GetValue<string>() == "spiritual_action_cost_burden")
                    definition["definition"]!["components"]![0]!["payload"]!["magnitude"] = initialUses == 1 ? 3 : 2;
            var thirdResult = await capture.MaterializeWoundAsync(thirdLease, thirdAdmission.Admission!,
                JsonSerializer.SerializeToElement(decision), narration);
            AssertNoConflictFrameErrors(thirdResult.Issues);
            Assert.Equal(initialUses == 1 ? 4 : 3, thirdResult.Wound!.Severity.Rank);
            Assert.Equal(2, draft.DependencyCutVersion);
            Assert.Equal(version + 2, draft.WoundReadVersion);
            Assert.Equal(phases, draft.Phases.Count);
            var final = draft.ReadCurrentWoundView(capture, resources, source, draft.WoundReadVersion, issues)!;
            AssertNoConflictFrameErrors(issues);
            var entries = final.EffectIdentity!["entries"]!.AsArray().OfType<JsonObject>().ToArray();
            Assert.True(JsonNode.DeepEquals(previousRetired,
                entries.Single(value => value["effectId"]!.GetValue<string>() == root.EffectId)));
            foreach (var currentRoot in currentRoots)
            {
                var usedBudget = arbiter.TryGetRemainingUses(currentRoot.EffectId, out _);
                var activation = Assert.Single(third.Step.Interval.EffectAfter.AcceptedActivations,
                    value => value.Activation.Stamp.Identity.EffectId == currentRoot.EffectId);
                var history = entries.Single(value => value["effectId"]!.GetValue<string>() == currentRoot.EffectId)
                    ["transitions"]!.AsArray();
                Assert.Equal(initialUses == 1 && usedBudget ? new[] { "create", "expire" } :
                    new[] { "create", usedBudget ? "consume" : "trigger", "expire" },
                    history.Select(value => value!["kind"]!.GetValue<string>()));
                Assert.Equal(activation.Activation.Stamp.Identity.EventRef, history[1]!["eventRef"]!.GetValue<string>());
            }
            var finalFingerprint = WoundAcceptedTurnFingerprints.ComputeOperationBefore(final);
            var finalWrites = identityOwner.WriteCount;
            var finalAllocations = identityOwner.AllocationCount;
            var thirdRetry = await capture.MaterializeWoundAsync(thirdLease, thirdAdmission.Admission!,
                JsonSerializer.SerializeToElement(decision), narration);
            Assert.Same(thirdResult, thirdRetry);
            Assert.Equal(finalWrites, identityOwner.WriteCount);
            Assert.Equal(finalAllocations, identityOwner.AllocationCount);
            Assert.Equal(finalFingerprint, WoundAcceptedTurnFingerprints.ComputeOperationBefore(
                draft.ReadCurrentWoundView(capture, resources, source, draft.WoundReadVersion, issues)!));
            AssertNoConflictFrameErrors(issues);
            Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(thirdLease, AfterlifeEntityProfileState.StatePath));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, thirdLease));
            return;
        }
        Assert.Null(result.Wound);
        Assert.Contains(result.Issues, issue => issue.Code == "spiritual_wound_generation_required");
        Assert.Equal(version, draft.WoundReadVersion);
        Assert.Equal(phases, draft.Phases.Count);
        Assert.Equal(0, draft.DependencyCutVersion);
        Assert.Equal(writesBefore, identityOwner.WriteCount);
        Assert.Equal(allocationsBefore, identityOwner.AllocationCount);
        Assert.Equal(editsBefore, Assert.IsType<int>(editCount.GetValue(workspace)));
        Assert.Empty(Assert.IsAssignableFrom<System.Collections.IDictionary>(OriginalCaptureField(draft, "_woundTriggerJournal")));
        Assert.Null(draft.ReadCurrentWoundView(capture, resources, source, version, issues));
        Assert.Contains(issues, issue => issue.Code == "spiritual_wound_read_stale");
        // A failed materialization revokes its capture. Inspect the retained private
        // state only to prove rejection did not apply pending work before revocation.
        var after = Assert.IsType<WoundOperationBeforeData>(OriginalCaptureField(draft, "_currentWoundState"));
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeOperationBefore(before),
            WoundAcceptedTurnFingerprints.ComputeOperationBefore(after));
        Assert.Equal(applied, OriginalContinuationApplied(resources));
        Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    /// <summary>
    /// Authors a tier-two opposition pressure victory harming the player, with one action point spent per side.
    /// </summary>
    /// <param name="candidate">
    /// Mutable conflict candidate whose active state must match the exchange after-image.
    /// </param>
    /// <param name="ordinal">
    /// Zero-based exchange index already present in the candidate.
    /// </param>
    /// <param name="strain">
    /// Resulting player strain; opposition strain remains clear.
    /// </param>
    /// <param name="playerRoll">
    /// Player roll from the signed dice pool at this exchange's retained source index.
    /// </param>
    /// <param name="oppositionRoll">
    /// Larger opposition roll producing decisive opposition success.
    /// </param>
    private static void SetPlayerHarm(JsonObject candidate, int ordinal, string strain, int playerRoll, int oppositionRoll)
    {
        var active = candidate["activeConflict"]!;
        var exchange = active["exchangeLog"]![ordinal]!;
        exchange["outcome"] = "setback";
        exchange["after"]!["playerSideStrain"] = strain;
        exchange["after"]!["oppositionSideStrain"] = "clear";
        foreach (var side in new[] { "player", "opposition" })
        {
            var cost = exchange["actionCostAudit"]![side]!;
            cost["artTier"] = 2;
            cost["effectiveCost"] = 1;
            cost["before"] = 6 - ordinal;
            cost["after"] = 5 - ordinal;
        }
        var dice = exchange["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = playerRoll;
        dice["diceUsed"]![1]!["value"] = oppositionRoll;
        dice["playerTotal"] = playerRoll;
        dice["oppositionTotal"] = oppositionRoll;
        dice["margin"] = playerRoll - oppositionRoll;
        dice["outcomeBand"] = "decisive_opposition_success";
        active["playerSideStrain"] = strain;
        active["oppositionSideStrain"] = "clear";
    }
}
