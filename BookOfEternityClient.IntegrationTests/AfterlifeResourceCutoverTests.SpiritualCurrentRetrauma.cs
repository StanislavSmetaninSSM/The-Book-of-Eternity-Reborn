using System.Collections;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Verifies that successive accepted spiritual sources target the actual current wound generation
    /// while preserving owned routing, lineage, and retirement across worsening.
    /// </summary>
    /// <param name="guaranteeRank">
    /// Optional special-art guaranteed severity rank. Rank one rejects worsening; rank two constrains
    /// the accepted worsening minimum; a <see langword="null"/> value uses ordinary severity selection.
    /// </param>
    /// <param name="relocatedStack">
    /// <see langword="true"/> seeds tier-two art authority and relocates the root stack coordinate during worsening.
    /// </param>
    /// <param name="deferredActivation">
    /// <see langword="true"/> retains an unrelated last-use activation across the local generation cut.
    /// </param>
    /// <param name="authorityTamper">
    /// Optional adversarial change applied to the rank-II draft-before preparation. A
    /// <see langword="null"/> value follows the lawful materialization path.
    /// </param>
    [Theory]
    [InlineData(null, false, false, null)]
    [InlineData(1, false, false, null)]
    [InlineData(2, false, false, null)]
    [InlineData(null, true, false, null)]
    [InlineData(null, false, true, null)]
    [InlineData(null, false, false, "before_fingerprint")]
    [InlineData(null, false, false, "selected_root")]
    public async Task OriginalSpiritualGeneration_NextSourceCanTargetTheActuallyInsertedWound(
        int? guaranteeRank, bool relocatedStack, bool deferredActivation, string? authorityTamper)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        if (deferredActivation)
        {
            await SeedOriginalPrefixActionPointEffectAsync(context, gain: true, bounded: false);
        }
        if (guaranteeRank.HasValue)
        {
            var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
            var art = SourceOwnerSpecialArt("player_soul", "player_soul");
            art["tier"] = 1;
            art["spiritualWoundEnvelope"] = new JsonObject
            {
                ["schemaVersion"] = 1, ["maximumSeverityRank"] = 2,
                ["guaranteedSeverityRank"] = guaranteeRank.Value
            };
            profiles["profiles"]![0]!["specialArts"]!.AsArray().Add(art);
            profiles["profiles"]![0]!["standardArts"]!["pressure"] = 1;
            await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
            const string soulPath = "game_state/meta/soul_state.json";
            var soul = Assert.IsType<JsonObject>(await context.ReadJsonAsync(soulPath));
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"] = 1;
            await context.WriteExactJsonAsync(soulPath, soul.ToJsonString());
        }
        if (relocatedStack)
        {
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
        }
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 18, 3, 18, 3]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (guaranteeRank.HasValue)
        {
            var initial = await ReadProjectedSourceContinuationCandidateAsync(context);
            var cost = initial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]!["player"]!;
            cost["artTier"] = 1;
            cost["effectiveCost"] = 2;
            cost["after"] = 4;
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, initial.ToJsonString());
        }
        if (relocatedStack)
        {
            var initial = await ReadProjectedSourceContinuationCandidateAsync(context);
            foreach (var side in new[] { "player", "opposition" })
            {
                var cost = initial["activeConflict"]!["exchangeLog"]![0]!["actionCostAudit"]![side]!;
                cost["artTier"] = 2;
                cost["effectiveCost"] = 1;
                cost["after"] = 5;
            }
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, initial.ToJsonString());
        }
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        Assert.NotNull(capture);
        string woundId;
        byte[] canonicalBefore;
        ValidationService.SpiritualWoundSourceSession source;
        try
        {
            canonicalBefore = (await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath))!;
            AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
            var first = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(first.Issues);
            source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
            var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step!.Interval!, Assert.Single(source.Sources));
            AssertNoConflictFrameErrors(admitted.Issues);
            var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
            AssertNoConflictFrameErrors(offered.Issues);
            var inserted = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
                OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef, "spiritual_action_cost_burden", "guard"),
                "Чужое давление надломило волю хранителя.");
            AssertNoConflictFrameErrors(inserted.Issues);
            woundId = Assert.IsType<WoundMaterializationEnvelope>(inserted.Wound).WoundId;
        }
        finally { await lease.DisposeAsync(); }

        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var secondExchange = candidate["activeConflict"]!["exchangeLog"]![1]!;
        secondExchange["spiritualWoundTarget"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian_frame", ["retraumaWoundRef"] = woundId
        };
        var dice = secondExchange["diceAudit"]!;
        dice["diceUsed"]![0]!["value"] = 18;
        dice["diceUsed"]![1]!["value"] = 3;
        dice["playerTotal"] = 18;
        dice["oppositionTotal"] = 3;
        dice["margin"] = 15;
        dice["outcomeBand"] = "decisive_player_success";
        if (relocatedStack)
            foreach (var side in new[] { "player", "opposition" })
            {
                secondExchange["actionCostAudit"]![side]!["before"] = 5;
                secondExchange["actionCostAudit"]![side]!["after"] = 4;
            }
        if (deferredActivation)
        {
            secondExchange["actionCostAudit"]!["player"]!["before"] = 4;
            secondExchange["actionCostAudit"]!["player"]!["after"] = 1;
        }
        if (guaranteeRank.HasValue)
        {
            secondExchange["specialArtAudit"] = new JsonObject
            {
                ["artId"] = "art_source_owner", ["ownerActorType"] = "player_soul",
                ["ownerActorId"] = "player_soul", ["baseOperation"] = "pressure",
                ["costMultiplierPercent"] = 200, ["effectNote"] = "Искусство усиливает прежний надлом."
            };
            var cost = secondExchange["actionCostAudit"]!["player"]!;
            cost["before"] = 4;
            cost["artTier"] = 1;
            cost["effectiveCost"] = 4;
            cost["after"] = 0;
            cost["standardEffectiveCost"] = 2;
            cost["specialArtId"] = "art_source_owner";
            cost["specialCostMultiplierPercent"] = 200;
        }
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());

        await using var nextLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var second = await capture.AdvanceNextResourceExchangeAsync(nextLease);
        AssertNoConflictFrameErrors(second.Issues);
        Assert.Equal(1, second.Step!.Interval!.Ordinal);
        var repeatedHarm = Assert.Single(source.Sources, value => value.ExchangeId == "exchange_source_second");
        Assert.Equal(woundId, repeatedHarm.RetraumaWoundId);
        var copiedSource = await capture.AdmitWoundSourceAsync(nextLease, second.Step.Interval,
            repeatedHarm with { RetraumaWoundJson = "{}" });
        Assert.Null(copiedSource.Admission);
        Assert.Contains(copiedSource.Issues, issue => issue.Code == "spiritual_wound_frontier_mismatch");
        var expiredContext = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>(
            OriginalCaptureField(source, "_mechanicsContext"));
        Assert.False(expiredContext.IsCurrent);
        var staleIssues = new List<ValidationIssue>();
        Assert.Null(expiredContext.ReadCurrentWoundView(staleIssues));
        Assert.Contains(staleIssues, issue => issue.Code == "spiritual_wound_read_stale");
        var secondAdmission = await capture.AdmitWoundSourceAsync(nextLease, second.Step.Interval, repeatedHarm);
        AssertNoConflictFrameErrors(secondAdmission.Issues);
        var opportunity = await capture.ReadWoundOpportunityAsync(nextLease, secondAdmission.Admission!);
        if (guaranteeRank == 1)
        {
            foreach (var rejected in new[] { opportunity,
                         await capture.ReadWoundOpportunityAsync(nextLease, secondAdmission.Admission!) })
            {
                Assert.Null(rejected.Opportunity);
                Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_wound_guarantee_worsening_invalid");
            }
            Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease, AfterlifeEntityProfileState.StatePath));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, nextLease));
            return;
        }
        AssertNoConflictFrameErrors(opportunity.Issues);
        Assert.Equal(guaranteeRank, opportunity.Opportunity!.MinimumSeverityRank);
        Assert.True(WoundOpportunityAuthority.HasCompleteShape(opportunity.Opportunity));
        Assert.Equal(woundId, opportunity.Opportunity!.WorseningTarget!.Wound.WoundId);
        Assert.Equal(1, opportunity.Opportunity.WorseningTarget.Wound.Severity.Rank);
        var safeOffer = ValidationService.ProjectC2SafeOffer(opportunity.Opportunity);
        Assert.Equal(2, safeOffer.MinimumSeverityRank);
        Assert.Equal(guaranteeRank, safeOffer.RequiredSeverityRank);
        Assert.Equal(guaranteeRank is null
            ? new[] { "none", "materialize" }
            : new[] { "materialize" }, safeOffer.AllowedDecisions);
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(OriginalCaptureField(capture, "_effects"));
        var resources = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession>(OriginalCaptureField(capture, "_resources"));
        var viewIssues = new List<ValidationIssue>();
        var view = draft.ReadCurrentWoundView(capture, resources, source, draft.WoundReadVersion, viewIssues)!;
        AssertNoConflictFrameErrors(viewIssues);
        var detachedIdentity = view.WoundIdentity!;
        detachedIdentity.Clear();
        AssertNoConflictFrameErrors((await capture.ReadWoundOpportunityAsync(nextLease, secondAdmission.Admission!)).Issues);
        using var foreignDraft = EffectAcceptedTurnPlanner.EffectAcceptedDraft.Begin(
            Assert.IsType<EffectAcceptedTurnPlan>(OriginalCaptureField(draft, "plan")), new EffectIdentityFactory());
        var foreignIssues = new List<ValidationIssue>();
        Assert.Null(foreignDraft.ReadCurrentWoundView(capture, resources, source, 0, foreignIssues));
        Assert.Contains(foreignIssues, issue => issue.Code == "spiritual_wound_read_stale");
        Assert.Empty(foreignDraft.Phases);

        // Fault injection proves cached offers still validate actual current state.
        // It does not simulate a valid worsening or grant a new publication authority.
        var stateField = draft.GetType().GetField("_currentWoundState",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var originalState = stateField.GetValue(draft);
        foreach (var invalidPart in new[] { "identity", "history", "target" })
        {
            var carriers = view.WoundCarriers!;
            if (invalidPart == "target")
            {
                var owner = opportunity.Opportunity.WorseningTarget.Wound.Owner;
                var root = WoundCarrierCollectionAuthority.GetRoot(carriers, owner.CarrierPath)!;
                Assert.True(WoundCarrierCollectionAuthority.TryResolve(root, owner, out var collection, out _));
                collection[0]!["display"]!["description"] = "Изменённая рана после сохранения предложения.";
            }
            stateField.SetValue(draft, new WoundOperationBeforeData(carriers,
                invalidPart == "identity" ? new JsonObject() : view.WoundIdentity,
                invalidPart == "history" ? new JsonObject() : view.WoundHistory,
                view.EffectCarriers, view.EffectIdentity));
            try
            {
                var rejected = await capture.ReadWoundOpportunityAsync(nextLease, secondAdmission.Admission!);
                Assert.Null(rejected.Opportunity);
                Assert.Contains(rejected.Issues, issue => issue.Code == "spiritual_wound_retrauma_target_stale");
            }
            finally { stateField.SetValue(draft, originalState); }
        }
        var restored = await capture.ReadWoundOpportunityAsync(nextLease, secondAdmission.Admission!);
        AssertNoConflictFrameErrors(restored.Issues);
        Assert.Same(opportunity, restored);
        var worsening = JsonNode.Parse(OriginalSpiritualWoundDecision(opportunity.Opportunity.PublicRef,
            "spiritual_action_cost_burden", "guard").GetRawText())!;
        worsening["proposal"]!["severity"] = "II";
        var definitions = worsening["proposal"]!["consequenceDefinitions"]!.AsArray();
        if (relocatedStack)
            definitions[0]!["definition"]!["stacking"]!["stackKey"] = "relocated_guard_burden";
        var extra = definitions[0]!.DeepClone();
        extra["definitionRef"] = "maneuver_burden";
        extra["definition"]!["definitionKey"] = "maneuver_burden";
        extra["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_burden";
        extra["definition"]!["components"]![0]!["payload"]!["operation"] = relocatedStack ? "pressure" : "maneuver";
        definitions.Add(extra);
        var selectionIssues = new List<ValidationIssue>();
        var selection = await ValidationService.SpiritualOriginalTurnCapture.WoundSelection.SelectAsync(capture,
            nextLease, secondAdmission.Admission!, System.Text.Json.JsonSerializer.SerializeToElement(worsening),
            "Чужое давление надломило волю хранителя.", selectionIssues);
        AssertNoConflictFrameErrors(selectionIssues);
        Assert.NotNull(selection);
        Assert.Equal("worsen", Assert.Single(selection.Input.Transitions).Kind);
        var identityOwner = Assert.IsType<EffectIdentityHistoryOwner>(OriginalCaptureField(draft, "identityRoot"));
        var allocationsBeforeSignedOnlyProbe = identityOwner.AllocationCount;
        var writesBeforeSignedOnlyProbe = identityOwner.WriteCount;
        var signedOnlyPreparation = WoundAcceptedTurnPlanner.Prepare(selection.Input);
        Assert.False(signedOnlyPreparation.Success);
        Assert.Null(signedOnlyPreparation.Plan);
        Assert.Contains(signedOnlyPreparation.Issues, issue =>
            issue.Code == "wound_plan_worsening_target_stale");
        Assert.Equal(allocationsBeforeSignedOnlyProbe, identityOwner.AllocationCount);
        Assert.Equal(writesBeforeSignedOnlyProbe, identityOwner.WriteCount);
        if (authorityTamper is not null)
        {
            var authorityIssues = new List<ValidationIssue>();
            var before = draft.AdvanceForWoundInsertion(selection, authorityIssues);
            AssertNoConflictFrameErrors(authorityIssues);
            Assert.NotNull(before);
            var prepared = before.Prepare();
            AssertNoConflictFrameErrors(prepared.Issues);
            Assert.True(prepared.Success);
            var preparedPlan = Assert.IsType<WoundPreparedAcceptedTurnPlan>(prepared.Plan);
            var currentBefore = before.Data;
            var allocationsBeforeTamper = identityOwner.AllocationCount;
            var writesBeforeTamper = identityOwner.WriteCount;
            if (authorityTamper == "before_fingerprint")
            {
                var fingerprintField = before.GetType().GetField("<Fingerprint>k__BackingField",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.NotNull(fingerprintField);
                fingerprintField.SetValue(before, before.Fingerprint + "_tampered");
            }
            else
            {
                Assert.Equal("selected_root", authorityTamper);
                preparedPlan = RewrapSpiritualDraftPreparationWithTamperedSelectedRoot(preparedPlan);
            }

            var applyMethod = draft.GetType().GetMethod("ApplyPreparedWound",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(applyMethod);
            var rejected = applyMethod.Invoke(draft, new object[] { before, preparedPlan, authorityIssues });
            Assert.Null(rejected);
            Assert.Contains(authorityIssues, issue => issue.Code == "wound_plan_prepared_seal_mismatch");
            Assert.Equal(allocationsBeforeTamper, identityOwner.AllocationCount);
            Assert.Equal(writesBeforeTamper, identityOwner.WriteCount);
            var unchangedIssues = new List<ValidationIssue>();
            var unchanged = draft.ReadCurrentWoundView(capture, resources, source,
                draft.WoundReadVersion, unchangedIssues);
            AssertNoConflictFrameErrors(unchangedIssues);
            Assert.NotNull(unchanged);
            Assert.True(JsonNode.DeepEquals(currentBefore.WoundCarriers!.PlayerWounds,
                unchanged.WoundCarriers!.PlayerWounds));
            Assert.True(JsonNode.DeepEquals(currentBefore.WoundCarriers.NpcWounds,
                unchanged.WoundCarriers.NpcWounds));
            Assert.True(JsonNode.DeepEquals(currentBefore.WoundCarriers.EnemyCombatants,
                unchanged.WoundCarriers.EnemyCombatants));
            Assert.True(JsonNode.DeepEquals(currentBefore.WoundCarriers.AllyCombatants,
                unchanged.WoundCarriers.AllyCombatants));
            Assert.True(JsonNode.DeepEquals(currentBefore.WoundCarriers.AfterlifeProfiles,
                unchanged.WoundCarriers.AfterlifeProfiles));
            Assert.True(JsonNode.DeepEquals(currentBefore.WoundIdentity, unchanged.WoundIdentity));
            Assert.True(JsonNode.DeepEquals(currentBefore.WoundHistory, unchanged.WoundHistory));
            Assert.True(JsonNode.DeepEquals(currentBefore.EffectCarriers!.PlayerEffects,
                unchanged.EffectCarriers!.PlayerEffects));
            Assert.True(JsonNode.DeepEquals(currentBefore.EffectCarriers.NpcEffects,
                unchanged.EffectCarriers.NpcEffects));
            Assert.True(JsonNode.DeepEquals(currentBefore.EffectCarriers.EnemyCombatants,
                unchanged.EffectCarriers.EnemyCombatants));
            Assert.True(JsonNode.DeepEquals(currentBefore.EffectCarriers.AllyCombatants,
                unchanged.EffectCarriers.AllyCombatants));
            Assert.True(JsonNode.DeepEquals(currentBefore.EffectCarriers.AfterlifeProfiles,
                unchanged.EffectCarriers.AfterlifeProfiles));
            Assert.True(JsonNode.DeepEquals(currentBefore.EffectCarriers.SpiritualConflict,
                unchanged.EffectCarriers.SpiritualConflict));
            Assert.True(JsonNode.DeepEquals(currentBefore.EffectIdentity, unchanged.EffectIdentity));
            Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(
                nextLease, AfterlifeEntityProfileState.StatePath));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, nextLease));
            return;
        }
        var priorPhaseCount = draft.Phases.Count;
        var priorVersion = draft.WoundReadVersion;
        var useArbiter = Assert.IsType<AcceptedEffectUseArbiter>(OriginalCaptureField(
            OriginalCaptureField(resources, "_state")!, "arbiter"));
        if (deferredActivation)
        {
            Assert.NotEmpty(second.Step.Interval.EffectAfter.AcceptedActivations);
            Assert.True(useArbiter.TryGetRemainingUses(EffectMaterializationTestFixture.EffectId, out var remaining));
            Assert.Equal(0, remaining);
        }
        var changed = await capture.MaterializeWoundAsync(nextLease, secondAdmission.Admission!,
            System.Text.Json.JsonSerializer.SerializeToElement(worsening), "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(changed.Issues);
        Assert.Equal(woundId, changed.Wound!.WoundId);
        Assert.Equal(2, changed.Wound.Severity.Rank);
        Assert.Equal(priorVersion + 1, draft.WoundReadVersion);
        Assert.Equal(priorPhaseCount, draft.Phases.Count);
        var oldRoots = opportunity.Opportunity.WorseningTarget.Wound.Consequences.OwnedEffectSources.RootBindings;
        Assert.DoesNotContain(changed.Wound.Consequences.OwnedEffectSources.RootBindings,
            root => oldRoots.Any(old => old.EffectId == root.EffectId));
        var generationIssues = new List<ValidationIssue>();
        var generation = draft.ReadCurrentWoundView(capture, resources, source, draft.WoundReadVersion, generationIssues)!;
        AssertNoConflictFrameErrors(generationIssues);
        using var identityDocument = System.Text.Json.JsonDocument.Parse(generation.EffectIdentity!.ToJsonString());
        var identities = EffectIdentityState.Parse(identityDocument.RootElement, EffectIdentityState.StatePath);
        AssertNoConflictFrameErrors(identities.Issues);
        if (deferredActivation)
        {
            Assert.Same(useArbiter, OriginalCaptureField(OriginalCaptureField(resources, "_state")!, "arbiter"));
            Assert.True(useArbiter.TryGetRemainingUses(EffectMaterializationTestFixture.EffectId, out var remaining));
            Assert.Equal(0, remaining);
            var untouched = view.EffectIdentity!["entries"]!.AsArray().OfType<JsonObject>()
                .Single(entry => entry["effectId"]!.GetValue<string>() == EffectMaterializationTestFixture.EffectId);
            Assert.True(identities.State!.TryGetEntry(EffectMaterializationTestFixture.EffectId, out var deferredIdentity));
            Assert.True(JsonNode.DeepEquals(untouched, deferredIdentity.Raw));
            var beforeEffects = EffectCarrierCatalog.Build(view.EffectCarriers!);
            var afterEffects = EffectCarrierCatalog.Build(generation.EffectCarriers!);
            Assert.True(beforeEffects.TryResolveOne(EffectMaterializationTestFixture.EffectId, out var beforeEffect));
            Assert.True(afterEffects.TryResolveOne(EffectMaterializationTestFixture.EffectId, out var afterEffect));
            Assert.True(JsonNode.DeepEquals(beforeEffect.Effect, afterEffect.Effect));
            Assert.Contains(second.Step.Interval.EffectAfter.TerminalAvailabilityReservations,
                reservation => reservation.Subject.EffectId == EffectMaterializationTestFixture.EffectId &&
                    reservation.Kind == EffectTerminalAvailabilityReservationKind.LastUse);
        }
        foreach (var old in oldRoots)
        {
            Assert.True(identities.State!.TryGetEntry(old.EffectId, out var retired));
            Assert.Equal("expired", retired.State);
            Assert.Equal("expire", retired.Transitions.Last().Kind);
            var successor = Assert.Single(changed.Wound.Consequences.OwnedEffectSources.RootBindings,
                root => root.DefinitionKey == old.DefinitionKey);
            Assert.True(identities.State.TryGetEntry(successor.EffectId, out var newIdentity));
            if (relocatedStack)
                Assert.Empty(Assert.Single(newIdentity.Transitions).SourceEffectIds);
            else
                Assert.Contains(old.EffectId, Assert.Single(newIdentity.Transitions).SourceEffectIds);
        }
        var currentMechanics = resources.CaptureSpiritualMechanics(capture, source, generationIssues)!;
        AssertNoConflictFrameErrors(generationIssues);
        Assert.True(currentMechanics.IsCurrent);
        Assert.DoesNotContain(currentMechanics.Contributions, contribution => oldRoots.Any(old => old.EffectId == contribution.EffectId));
        Assert.Contains(currentMechanics.Contributions, contribution => contribution.Operation == (relocatedStack ? "pressure" : "maneuver") &&
            changed.Wound.Consequences.OwnedEffectSources.RootBindings.Any(root => root.EffectId == contribution.EffectId));
        var routing = Assert.IsType<EffectAcceptedTurnPlanner.BaseResourceRouting>(OriginalCaptureField(resources, "_routing"));
        var epoch = Assert.IsType<EffectAcceptedTurnPlanner.BaseResourceRouting.WoundRoutingPreparation>(
            OriginalCaptureField(routing, "_currentWoundRouting"));
        foreach (var corruption in new[] { "retired_header", "retired_history", "retired_missing", "retired_source_group_changed", "active_carrier" })
        {
            var invalidIdentity = generation.EffectIdentity!;
            var invalidCarriers = generation.EffectCarriers!;
            var invalidCatalog = EffectCarrierCatalog.Build(invalidCarriers);
            var oldEntry = invalidIdentity["entries"]!.AsArray().OfType<JsonObject>()
                .Single(entry => entry["effectId"]!.GetValue<string>() == oldRoots[0].EffectId);
            if (corruption == "retired_header")
                oldEntry["stackCoordinate"]!["stackKey"] = "changed_historical_stack";
            else if (corruption == "retired_history")
                oldEntry["transitions"]!.AsArray().Last()!["eventRef"] = "changed_historical_event";
            else if (corruption == "retired_source_group_changed")
            {
                oldEntry["source"]!["sourceId"] = "foreign_wound";
                oldEntry["stackCoordinate"]!["sourceId"] = "foreign_wound";
            }
            else if (corruption == "retired_missing")
                invalidIdentity["entries"]!.AsArray().Remove(oldEntry);
            else
            {
                Assert.True(invalidCatalog.TryResolveOne(changed.Wound.Consequences.OwnedEffectSources.RootBindings[0].EffectId,
                    out var activeOccurrence));
                activeOccurrence.Effect["stacking"]!["stackKey"] = "changed_active_stack";
            }
            using var invalidDocument = System.Text.Json.JsonDocument.Parse(invalidIdentity.ToJsonString());
            var parsedInvalid = EffectIdentityState.Parse(invalidDocument.RootElement, EffectIdentityState.StatePath);
            AssertNoConflictFrameErrors(parsedInvalid.Issues);
            var invalidLineage = WoundReactionLineageAuthority.Build(epoch.Sources, parsedInvalid.State!,
                invalidCatalog, epoch.Roots, epoch.Lineage,
                Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion>(
                    OriginalCaptureField(routing, "_currentWoundInsertion")));
            Assert.False(invalidLineage.Success);
            Assert.Contains(invalidLineage.Issues, issue => issue.Code == (corruption == "active_carrier"
                ? "effect_reaction_wound_lineage_occurrence_invalid" : "effect_reaction_wound_lineage_history_changed"));
        }
        var retry = await capture.MaterializeWoundAsync(nextLease, secondAdmission.Admission!,
            System.Text.Json.JsonSerializer.SerializeToElement(worsening), "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(retry.Issues);
        Assert.Same(changed, retry);
        Assert.Equal(priorVersion + 1, draft.WoundReadVersion);
        var changedRetry = await capture.MaterializeWoundAsync(nextLease, secondAdmission.Admission!,
            System.Text.Json.JsonSerializer.SerializeToElement(worsening), "Другой текст сцены.");
        Assert.Contains(changedRetry.Issues, issue => issue.Code == "spiritual_wound_selection_conflict");
        Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease, AfterlifeEntityProfileState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, nextLease));
        if (!relocatedStack && !deferredActivation && guaranteeRank is null)
        {
            var completed = await capture.CompleteOrdinaryReductionAsync(nextLease);
            Assert.True(completed.Success, string.Join(Environment.NewLine, completed.Issues));
            var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
                completed.Reduction);
            var chain = Assert.IsType<SpiritualLiveWoundCompletion>(ordinary.LiveWoundCompletion);
            Assert.Equal(2, chain.Insertions.Count);
            Assert.Equal(new long[] { 1, 2 }, chain.Insertions.Select(value => value.VersionAfter));
            Assert.All(chain.Insertions, value => Assert.Equal(woundId, value.Wound.WoundId));
            Assert.True(JsonNode.DeepEquals(chain.Insertions[0].ReducedState.Identity,
                chain.Insertions[1].OperationBefore.WoundIdentity));
            Assert.True(JsonNode.DeepEquals(chain.Insertions[1].ReducedState.History,
                chain.FinalState.History));
            var unjoined = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
                AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                    ordinary, ordinary.Resources));
            Assert.Null(unjoined.Plan);
            Assert.Contains(unjoined.Issues, issue =>
                issue.Code == "spiritual_live_wound_receipt_join_required");
            Assert.Equal("create", chain.Insertions[0].Wound.LastTransition.Kind);
            Assert.Equal("worsen", chain.Insertions[1].Wound.LastTransition.Kind);
            var detachedEffects = EffectAcceptedTurnPlan.DetachedCopyOf(ordinary.Effects!);
            var foreignComposition = AcceptedMechanicsCarrierAssembler.ComposeLive(
                detachedEffects, ordinary.OwnerCompanionAfterImages, chain, null);
            Assert.False(foreignComposition.Success);
            Assert.Contains(foreignComposition.Issues, issue =>
                issue.Code == "spiritual_live_wound_effect_completion_mismatch");
            var common = AcceptedMechanicsCarrierAssembler.ComposeLive(
                ordinary.Effects, ordinary.OwnerCompanionAfterImages, chain, null);
            Assert.True(common.Success, string.Join(Environment.NewLine,
                common.Issues.Select(issue => $"{issue.Code}: {issue}")));
            var publication = Assert.IsType<AcceptedMechanicsWoundPublication>(
                common.WoundPublication);
            Assert.Equal(chain.ProofFingerprint, publication.LiveWoundProofFingerprint);
            Assert.True(JsonNode.DeepEquals(chain.FinalState.History,
                publication.HistoryAfterImage));
            var registered = Assert.IsType<Dictionary<
                ValidationService.SpiritualOriginalTurnCapture.WoundSelection,
                EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion>>(
                OriginalCaptureField(capture, "_registeredWoundInsertions"));
            var reversed = registered.OrderByDescending(pair => pair.Value.VersionAfter)
                .Select(pair => (pair.Key, pair.Value)).ToArray();
            var ordered = reversed.Reverse().ToArray();
            Assert.Null(SpiritualLiveWoundCompletion.Seal(draft,
                ordinary.Effects!, resources.Routing!,
                resources.Result!.EffectBoundaryTranscript!, resources, reversed));
            Assert.Null(SpiritualLiveWoundCompletion.Seal(draft,
                ordinary.Effects!, resources.Routing!,
                resources.Result.EffectBoundaryTranscript!, resources,
                reversed.Take(1).ToArray()));
            var mispaired = reversed.Reverse()
                .Select((pair, index) => (reversed[index].Key, pair.Value))
                .ToArray();
            Assert.Null(SpiritualLiveWoundCompletion.Seal(draft,
                ordinary.Effects!, resources.Routing!,
                resources.Result.EffectBoundaryTranscript!, resources, mispaired));
            var divergent = new DivergentReadOnlyList<(
                ValidationService.SpiritualOriginalTurnCapture.WoundSelection,
                EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion)>(
                ordered, reversed);
            Assert.Null(SpiritualLiveWoundCompletion.Seal(draft,
                ordinary.Effects!, resources.Routing!,
                resources.Result.EffectBoundaryTranscript!, resources, divergent));
        }
        if (relocatedStack)
        {
            await nextLease.DisposeAsync();
            var thirdCandidate = await ReadProjectedSourceContinuationCandidateAsync(context);
            var active = thirdCandidate["activeConflict"]!;
            var thirdExchange = active["exchangeLog"]![1]!.DeepClone();
            thirdExchange["exchangeId"] = "exchange_source_third";
            thirdExchange["before"] = thirdExchange["after"]!.DeepClone();
            thirdExchange["after"]!["oppositionSideStrain"] = "overwhelmed";
            thirdExchange["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 4;
            thirdExchange["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 5;
            foreach (var side in new[] { "player", "opposition" })
            {
                var cost = thirdExchange["actionCostAudit"]![side]!;
                cost["before"] = 4;
                cost["effectiveCost"] = side == "player" ? 1 : 2;
                cost["after"] = side == "player" ? 3 : 2;
            }
            active["exchangeLog"]!.AsArray().Add(thirdExchange);
            active["oppositionSideStrain"] = "overwhelmed";
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, thirdCandidate.ToJsonString());
            await using var thirdLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var third = await capture.AdvanceNextResourceExchangeAsync(thirdLease);
            AssertNoConflictFrameErrors(third.Issues);
            Assert.Equal(2, third.Step!.Interval!.Ordinal);
            var thirdSource = Assert.Single(source.Sources, value => value.ExchangeId == "exchange_source_third");
            var thirdAdmission = await capture.AdmitWoundSourceAsync(thirdLease, third.Step.Interval, thirdSource);
            AssertNoConflictFrameErrors(thirdAdmission.Issues);
            var thirdOffer = await capture.ReadWoundOpportunityAsync(thirdLease, thirdAdmission.Admission!);
            AssertNoConflictFrameErrors(thirdOffer.Issues);
            var thirdDecision = worsening.DeepClone();
            thirdDecision["opportunityRef"] = thirdOffer.Opportunity!.PublicRef;
            thirdDecision["proposal"]!["severity"] = "III";
            thirdDecision["proposal"]!["display"]!["acquisitionNarration"] = "Надлом усилился в следующем обмене.";
            var thirdDefinitions = thirdDecision["proposal"]!["consequenceDefinitions"]!.AsArray();
            var thirdBurden = thirdDefinitions[0]!.DeepClone();
            thirdBurden["definitionRef"] = "third_maneuver_burden";
            thirdBurden["definition"]!["definitionKey"] = "third_maneuver_burden";
            thirdBurden["definition"]!["stacking"]!["stackKey"] = "stack_third_maneuver_burden";
            thirdBurden["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
            thirdDefinitions.Add(thirdBurden);
            foreach (var definition in thirdDefinitions)
                definition!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 2;
            var finalGeneration = await capture.MaterializeWoundAsync(thirdLease, thirdAdmission.Admission!,
                System.Text.Json.JsonSerializer.SerializeToElement(thirdDecision), "Надлом усилился в следующем обмене.");
            AssertNoConflictFrameErrors(finalGeneration.Issues);
            Assert.Equal(woundId, finalGeneration.Wound!.WoundId);
            Assert.Equal(3, finalGeneration.Wound.Severity.Rank);
            Assert.Equal(priorVersion + 2, draft.WoundReadVersion);
            Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(thirdLease, AfterlifeEntityProfileState.StatePath));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, thirdLease));
        }
    }

    /// <summary>
    /// Copies a live worsening preparation while replacing its selected
    /// severity-generation predecessor and retaining the original preparation seal.
    /// </summary>
    /// <param name="prepared">
    /// Live owner-bound preparation whose one non-null predecessor is changed in the detached copy.
    /// </param>
    /// <returns>
    /// A detached preparation carrying a changed predecessor with the original claimed seal.
    /// </returns>
    private static WoundPreparedAcceptedTurnPlan RewrapSpiritualDraftPreparationWithTamperedSelectedRoot(
        WoundPreparedAcceptedTurnPlan prepared)
    {
        var originalBatch = Assert.Single(prepared.EffectOperationBatches);
        var roots = originalBatch.RootApplications.ToArray();
        var selectedRootIndex = Array.FindIndex(roots, static root => root.PriorRootEffectId is not null);
        Assert.True(selectedRootIndex >= 0);
        var selectedRoot = roots[selectedRootIndex];
        roots[selectedRootIndex] = new WoundRootEffectApplication(
            selectedRoot.ApplicationRef,
            selectedRoot.MechanicsOrdinal,
            selectedRoot.OperationOrdinal,
            selectedRoot.OperationKind,
            selectedRoot.OperationKey,
            selectedRoot.DefinitionKey,
            selectedRoot.TargetSelector,
            selectedRoot.ExpectedTargetKey,
            selectedRoot.SourceSelector,
            selectedRoot.ExpectedSourceKey,
            selectedRoot.Parameters,
            selectedRoot.SlotBindings,
            selectedRoot.ExpectedComponentCount,
            selectedRoot.ExpectedMaterializationFingerprint,
            selectedRoot.OwnershipDomain,
            selectedRoot.CausalEventRef,
            selectedRoot.ExpectedCarrierCoordinate,
            "fx_tampered_selected_root");
        var changedBatch = new WoundEffectOperationBatch(
            originalBatch.LocalWoundRef,
            originalBatch.PreparedWoundId,
            originalBatch.SourceExport,
            roots,
            originalBatch.TerminalOperations,
            originalBatch.RootLineageAuthority,
            originalBatch.SourceExportFingerprint,
            originalBatch.TransitionAuthority);
        return new WoundPreparedAcceptedTurnPlan(
            prepared.Binding,
            prepared.BindingFingerprint,
            prepared.InputFingerprint,
            prepared.WoundPreparationFingerprint,
            prepared.AllocatedWoundIds,
            prepared.AllocatedTransitionIds,
            prepared.PreparedWounds,
            new[] { changedBatch },
            prepared.BaselineAuthority,
            draftBefore: prepared.DraftBefore);
    }

    /// <summary>
    /// Exposes different indexer and enumerator sequences to test proof input freezing.
    /// </summary>
    /// <typeparam name="T">
    /// Immutable item type supplied to the owner-sealing API.
    /// </typeparam>
    private sealed class DivergentReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly T[] _indexed;
        private readonly T[] _enumerated;

        /// <summary>
        /// Retains the two independently observable sequences.
        /// </summary>
        /// <param name="indexed">
        /// Sequence exposed by the indexer and count.
        /// </param>
        /// <param name="enumerated">
        /// Different sequence exposed by enumeration.
        /// </param>
        internal DivergentReadOnlyList(IEnumerable<T> indexed, IEnumerable<T> enumerated)
        {
            _indexed = indexed.ToArray();
            _enumerated = enumerated.ToArray();
        }

        /// <inheritdoc />
        public int Count => _indexed.Length;
        /// <inheritdoc />
        public T this[int index] => _indexed[index];
        /// <inheritdoc />
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_enumerated).GetEnumerator();
        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
