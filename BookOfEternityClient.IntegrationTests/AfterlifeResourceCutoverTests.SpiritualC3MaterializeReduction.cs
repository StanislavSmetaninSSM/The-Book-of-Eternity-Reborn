using System.Text.Json.Nodes;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Joins a real owner-created spiritual wound to its exact receipt and common plan without writing it.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC3Materialize_JoinsRealWoundToReceiptAndCommonPlan()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await CommitInitialC2PairAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var completed = await offer.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(offer.Offer!.OpportunityRef),
            "Чужое давление надломило волю хранителя.");
        Assert.True(completed.Disposition == "completed_unpublished", string.Join(Environment.NewLine,
            completed.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var priorWounds = await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeEntityProfileState.StatePath);
        var priorIdentity = await context.FileSystem.ReadFileBytesAsync(lease,
            WoundIdentityState.StatePath);
        var priorHistory = await context.FileSystem.ReadFileBytesAsync(lease,
            WoundHistoryState.HistoryPath);
        var priorReceipt = await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundOpportunityReceiptState.StatePath);

        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);

        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var proof = Assert.IsType<SpiritualLiveWoundCompletion>(ordinary.LiveWoundCompletion);
        var insertion = Assert.Single(proof.Insertions);
        var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            OriginalCaptureField(terminal, "_capture"));
        var signed = capture.ReadVerifiedSignedC1Origin(lease);
        Assert.Throws<InvalidOperationException>(() =>
            new SpiritualWoundDeclineReceiptReducer.ValidatedReceiptProof(
                new object(), proof, null, signed.Receipt, reduced.ReceiptAfterImage!));
        var packet = JsonNode.Parse(await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath))!["pending"]!.AsObject();
        var omittedProof = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet);
        Assert.Null(omittedProof.AfterImage);
        Assert.Contains(omittedProof.Issues, issue =>
            issue.Code == "spiritual_c3_decline_materialization_deferred");
        var forgedPair = new ValidationService.SpiritualOriginalTurnCapture.AuthenticatedC3Pair(
            new object(), signed, packet, null, new object());
        var untrusted = SpiritualWoundDeclineReceiptReducer.Reduce(signed, packet, proof,
            forgedPair);
        Assert.Null(untrusted.AfterImage);
        Assert.Contains(untrusted.Issues, issue =>
            issue.Code == "spiritual_c3_materialization_owner_pair_missing");
        var changedProposal = packet.DeepClone().AsObject();
        var staged = changedProposal["stagedDecisions"]![0]!.AsObject();
        var proposal = JsonNode.Parse(Convert.FromBase64String(
            staged["woundDraftBase64"]!.GetValue<string>()))!.AsObject();
        proposal["display"]!["name"] = "Подменённый надлом";
        var changedBytes = Encoding.UTF8.GetBytes(proposal.ToJsonString());
        staged["woundDraftBase64"] = Convert.ToBase64String(changedBytes);
        staged["woundDraftFingerprint"] = "sha256:" +
            Convert.ToHexString(SHA256.HashData(changedBytes)).ToLowerInvariant();
        staged["decisionFingerprint"] = SpiritualWoundStateJson.Hash(
            staged, "staged_decision", "decisionFingerprint");
        var mismatched = SpiritualWoundDeclineReceiptReducer.Reduce(signed,
            changedProposal, proof, forgedPair);
        Assert.Null(mismatched.AfterImage);
        Assert.Contains(mismatched.Issues, issue =>
            issue.Code == "spiritual_c3_materialization_owner_pair_missing");
        var decision = Assert.Single(reduced.ReceiptAfterImage!["decisions"]!.AsArray())!.AsObject();
        Assert.Equal("materialize", decision["decision"]!.GetValue<string>());
        Assert.Equal(insertion.Wound.WoundId, decision["woundId"]!.GetValue<string>());
        Assert.Equal(insertion.Wound.LastTransition.TransitionId,
            decision["transitionId"]!.GetValue<string>());
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planned.Plan);
        var genuineComposition = AcceptedMechanicsCarrierAssembler.ComposeLive(
            ordinary.Effects, ordinary.OwnerCompanionAfterImages, proof, null);
        Assert.True(genuineComposition.Success);
        var detachedEffectPlan = EffectAcceptedTurnPlan.DetachedCopyOf(ordinary.Effects!);
        var foreignPlanError = Assert.Throws<ArgumentException>(() => new AcceptedMechanicsPlan(
            plan.InputFingerprint, plan.DefinitionAfterImage, plan.StateAfterImage,
            plan.HistoryAfterImage, plan.EffectCarrierAfterImages,
            plan.EffectIdentityAfterImage, plan.PendingAfterImages,
            plan.OwnerCompanionAfterImages, plan.BeforeImages, plan.TouchedPaths,
            plan.ConsumedPaths, plan.AuthorityFingerprints, plan.ResourceEvents,
            plan.ProjectionInput, plan.OwnerAuthority, detachedEffectPlan,
            plan.OwnerTransitions, carrierComposition: genuineComposition,
            liveWoundCompletion: proof));
        Assert.Equal("liveWoundCompletion", foreignPlanError.ParamName);
        Assert.Null(plan.WoundStageBundle);
        Assert.Contains(WoundIdentityState.StatePath, plan.TouchedPaths);
        Assert.Contains(WoundHistoryState.HistoryPath, plan.TouchedPaths);
        Assert.Contains(AfterlifeEntityProfileState.StatePath, plan.TouchedPaths);
        var currentDecisionCommand = await context.FileSystem.ReadFileBytesAsync(lease,
            AcceptedMechanicsPlan.WoundCommandPath);
        Assert.NotNull(currentDecisionCommand);
        var originalCommand = plan.BeforeImages[AcceptedMechanicsPlan.WoundCommandPath];
        Assert.True(!originalCommand.Existed ||
            !originalCommand.Bytes!.AsSpan().SequenceEqual(currentDecisionCommand));
        Assert.True(JsonNode.DeepEquals(insertion.ReducedState.Identity,
            plan.WoundIdentityAfterImage));
        Assert.True(JsonNode.DeepEquals(insertion.ReducedState.History,
            plan.WoundHistoryAfterImage));
        Assert.Throws<InvalidOperationException>(() =>
            CanonicalStateNormalizer.ComposeWoundPublicationWrites(plan));
        Assert.Equal(priorWounds, await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeEntityProfileState.StatePath));
        Assert.Equal(priorIdentity, await context.FileSystem.ReadFileBytesAsync(lease,
            WoundIdentityState.StatePath));
        Assert.Equal(priorHistory, await context.FileSystem.ReadFileBytesAsync(lease,
            WoundHistoryState.HistoryPath));
        Assert.Equal(priorReceipt, await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundOpportunityReceiptState.StatePath));
    }

    /// <summary>
    /// Keeps both sides' wound receipts independent through common assembly and cold replay, rejecting foreign proofs.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC3Materialize_SealsBothSidesOfOneExchange()
    {
        await using var context = await CreateTwoSourceC2ContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var packet = JsonNode.Parse((await context.FileSystem.ReadFileBytesAsync(lease,
            SpiritualWoundDecisionPendingState.StatePath))!)!["pending"]!;
        var sources = packet["sources"]!.AsArray();
        Assert.Equal(2, sources.Count);
        var sides = sources.Select(source => source!["affectedSide"]!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { "player", "opposition" }, sides);
        Assert.Equal(sources[0]!["coordinate"]!.GetValue<string>(),
            sources[1]!["coordinate"]!.GetValue<string>());
        Assert.Equal(sources[0]!["exchangeId"]!.GetValue<string>(),
            sources[1]!["exchangeId"]!.GetValue<string>());
        var firstNarration = sides[0] == "player"
            ? "Чужое давление надломило волю души."
            : "Чужое давление надломило волю хранителя.";
        var secondNarration = sides[1] == "player"
            ? "Чужое давление надломило волю души."
            : "Чужое давление надломило волю хранителя.";

        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var advanced = await first.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(first.Offer!.OpportunityRef,
                targetKind: sides[0] == "player" ? "player" : "guardian"),
            firstNarration,
            async writeLease => await context.FileSystem.WriteFileAtomicBytesAsync(writeLease,
                ProjectionNarrativePath,
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    response = firstNarration,
                    timestamp = "2026-01-01T00:00:00Z"
                }))));
        Assert.True(advanced.Disposition == "offer", advanced.Disposition + ": " +
            string.Join(Environment.NewLine, advanced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var second = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
        var completed = await second.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(second.Offer!.OpportunityRef,
                targetKind: sides[1] == "player" ? "player" : "guardian"),
            secondNarration,
            async writeLease => await context.FileSystem.WriteFileAtomicBytesAsync(writeLease,
                ProjectionNarrativePath,
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
                {
                    response = firstNarration + " " + secondNarration,
                    timestamp = "2026-01-01T00:00:00Z"
                }))));
        Assert.True(completed.Disposition == "completed_unpublished", completed.Disposition + ": " +
            string.Join(Environment.NewLine, completed.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
        var physicalCarrier = await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeEntityProfileState.StatePath);
        var physicalHistory = await context.FileSystem.ReadFileBytesAsync(lease,
            WoundHistoryState.HistoryPath);
        var physicalRoots = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[]
        {
            WoundIdentityState.StatePath, SpiritualWoundOpportunityReceiptState.StatePath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            EffectAcceptedTurnPlan.IdentityIndexPath, AfterlifeSpiritualConflictState.StatePath,
            SpiritualWoundDecisionPendingState.StatePath, SpiritualWoundCaptureCheckpointState.StatePath,
            AcceptedMechanicsPlan.WoundCommandPath
        })
            physicalRoots.Add(path, await context.FileSystem.ReadFileBytesAsync(lease, path));

        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);

        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var insertions = Assert.IsType<SpiritualLiveWoundCompletion>(
            ordinary.LiveWoundCompletion).Insertions;
        Assert.Equal(2, insertions.Count);
        Assert.All(insertions, insertion => Assert.Equal("create",
            Assert.Single(insertion.Input.Transitions).Kind));
        Assert.NotEqual(insertions[0].Wound.WoundId, insertions[1].Wound.WoundId);
        var decisions = reduced.ReceiptAfterImage!["decisions"]!.AsArray();
        Assert.Equal(2, decisions.Count);
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal("materialize", decisions[index]!["decision"]!.GetValue<string>());
            Assert.Equal(sides[index], decisions[index]!["sourceWitness"]!["affectedSide"]!.GetValue<string>());
            Assert.Equal(insertions[index].Wound.WoundId,
                decisions[index]!["woundId"]!.GetValue<string>());
            Assert.Equal(insertions[index].Wound.LastTransition.TransitionId,
                decisions[index]!["transitionId"]!.GetValue<string>());
        }
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        var plan = Assert.IsType<AcceptedMechanicsPlan>(planned.Plan);
        var carriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            null, null, null, null,
            plan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath]));
        Assert.Empty(carriers.Issues);
        foreach (var insertion in insertions)
        {
            Assert.True(carriers.TryResolveOne(insertion.Wound.WoundId, out var occurrence));
            Assert.Equal(WoundMaterializationContract.SerializeCanonical(insertion.Wound),
                WoundMaterializationContract.SerializeCanonical(occurrence.Wound));
        }
        terminal.Dispose();
        var reopened = await new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance).OpenC2PrivateSessionAsync(lease);
        AssertNoConflictFrameErrors(reopened.Issues);
        Assert.Equal("completed_unpublished", reopened.Disposition);
        using var coldTerminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(reopened.Session);
        var cold = await coldTerminal.ReduceCompletedDecisionsAsync(lease);
        Assert.True(cold.Success, string.Join(Environment.NewLine, cold.Issues));
        Assert.NotSame(ordinary.LiveWoundCompletion, cold.Reduction!.LiveWoundCompletion);
        Assert.True(JsonNode.DeepEquals(reduced.ReceiptAfterImage, cold.ReceiptAfterImage));
        var coldCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(
            OriginalCaptureField(coldTerminal, "_capture"));
        var rawCold = await coldCapture.CompleteOrdinaryReductionAsync(lease);
        Assert.True(rawCold.Success, string.Join(Environment.NewLine, rawCold.Issues));
        Assert.False(rawCold.Reduction!.HasLiveDecisionReceipt);
        var signedCold = coldCapture.ReadVerifiedSignedC1Origin(lease);
        var foreignReceipt = Assert.Throws<InvalidOperationException>(() =>
            rawCold.Reduction.WithOwnerCompanionAfterImage(
                SpiritualWoundOpportunityReceiptState.StatePath, signedCold.Receipt,
                reduced.ReceiptAfterImage!, reduced.ReceiptProof));
        Assert.Equal("The live wound receipt does not bind this owner result.", foreignReceipt.Message);
        var swappedReceipt = cold.ReceiptAfterImage!.DeepClone().AsObject();
        var swappedRows = swappedReceipt["decisions"]!.AsArray();
        var firstRow = swappedRows[0]!.DeepClone();
        swappedRows[0] = swappedRows[1]!.DeepClone();
        swappedRows[1] = firstRow;
        var reorderedReceipt = Assert.Throws<InvalidOperationException>(() =>
            rawCold.Reduction.WithOwnerCompanionAfterImage(
                SpiritualWoundOpportunityReceiptState.StatePath, signedCold.Receipt,
                swappedReceipt, cold.ReceiptProof));
        Assert.Equal("The live wound receipt does not bind this owner result.", reorderedReceipt.Message);
        var rejoined = rawCold.Reduction.WithOwnerCompanionAfterImage(
            SpiritualWoundOpportunityReceiptState.StatePath, signedCold.Receipt,
            cold.ReceiptAfterImage, cold.ReceiptProof);
        Assert.True(rejoined.HasLiveDecisionReceipt);
        var coldPlan = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                cold.Reduction, cold.Reduction.Resources));
        Assert.Empty(coldPlan.Issues);
        Assert.True(JsonNode.DeepEquals(plan.WoundIdentityAfterImage, coldPlan.Plan!.WoundIdentityAfterImage));
        Assert.True(JsonNode.DeepEquals(plan.WoundHistoryAfterImage, coldPlan.Plan.WoundHistoryAfterImage));
        Assert.True(JsonNode.DeepEquals(plan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath],
            coldPlan.Plan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath]));
        Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, coldPlan.Plan.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, coldPlan.Plan.HistoryAfterImage));
        Assert.Equal(physicalCarrier, await context.FileSystem.ReadFileBytesAsync(lease,
            AfterlifeEntityProfileState.StatePath));
        Assert.Equal(physicalHistory, await context.FileSystem.ReadFileBytesAsync(lease,
            WoundHistoryState.HistoryPath));
        foreach (var pair in physicalRoots)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Preserves a declined source before the next source's real wound identity in one receipt.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC3Materialize_MixesDeclineAndRealWoundInSourceOrder()
    {
        await using var context = await CreateTwoSourceC2ContextAsync();
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        using var first = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var advanced = await first.SubmitDecisionAsync(lease,
            JsonSerializer.SerializeToElement(new
            {
                opportunityRef = first.Offer!.OpportunityRef,
                decision = "none"
            }), null);
        Assert.Equal("offer", advanced.Disposition);
        using var second = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
        var completed = await second.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(second.Offer!.OpportunityRef),
            "Чужое давление надломило волю хранителя.",
            async writeLease => await context.FileSystem.WriteFileAtomicBytesAsync(writeLease,
                ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("""
                    {"response":"Оба противника ощутили силу одного столкновения. Чужое давление надломило волю хранителя.","timestamp":"2026-01-01T00:00:00Z"}
                    """)));
        Assert.True(completed.Disposition == "completed_unpublished", string.Join(Environment.NewLine,
            completed.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);

        var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);

        Assert.True(reduced.Success, string.Join(Environment.NewLine,
            reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reduced.Reduction);
        var insertion = Assert.Single(Assert.IsType<SpiritualLiveWoundCompletion>(
            ordinary.LiveWoundCompletion).Insertions);
        var decisions = reduced.ReceiptAfterImage!["decisions"]!.AsArray();
        Assert.Equal(2, decisions.Count);
        Assert.Equal("none", decisions[0]!["decision"]!.GetValue<string>());
        Assert.Null(decisions[0]!["woundId"]);
        Assert.Equal("materialize", decisions[1]!["decision"]!.GetValue<string>());
        Assert.Equal(insertion.Wound.WoundId, decisions[1]!["woundId"]!.GetValue<string>());
        Assert.Equal(insertion.Wound.LastTransition.TransitionId,
            decisions[1]!["transitionId"]!.GetValue<string>());
        var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinary, ordinary.Resources));
        Assert.Empty(planned.Issues);
        Assert.Equal(Assert.IsType<SpiritualLiveWoundCompletion>(ordinary.LiveWoundCompletion)
            .ProofFingerprint, Assert.IsType<AcceptedMechanicsPlan>(planned.Plan)
            .LiveWoundProofFingerprint);
    }

    /// <summary>
    /// Rejects a rank-I second materialization when the same side already owns its conflict wound.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualC2_RejectsSecondSameSideRankIChoice()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
            signedDice: [15, 5, 12, 8]);
        await CommitInitialC2PairAsync(context, conflict =>
        {
            var active = conflict["activeConflict"]!.AsObject();
            var first = active["exchangeLog"]![0]!.AsObject();
            var second = first.DeepClone().AsObject();
            second["exchangeId"] = "exchange_source_second";
            second["before"] = first["after"]!.DeepClone();
            second["after"] = second["before"]!.DeepClone();
            second["after"]!["oppositionSideStrain"] = "broken";
            second["diceAudit"]!["diceUsed"]![0]!["sourceIndex"] = 2;
            second["diceAudit"]!["diceUsed"]![0]!["value"] = 12;
            second["diceAudit"]!["diceUsed"]![1]!["sourceIndex"] = 3;
            second["diceAudit"]!["diceUsed"]![1]!["value"] = 8;
            second["diceAudit"]!["playerTotal"] = 12;
            second["diceAudit"]!["oppositionTotal"] = 8;
            second["diceAudit"]!["margin"] = 4;
            second["diceAudit"]!["outcomeBand"] = "player_success";
            foreach (var side in new[] { "player", "opposition" })
            {
                second["actionCostAudit"]![side]!["before"] = 3;
                second["actionCostAudit"]![side]!["after"] = 0;
            }
            active["exchangeLog"]!.AsArray().Add(second);
            active["oppositionSideStrain"] = "broken";
        });
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var validator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        var opened = await validator.OpenC2PrivateSessionAsync(lease);
        using var firstOffer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
        var advanced = await firstOffer.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(firstOffer.Offer!.OpportunityRef,
                "spiritual_action_cost_burden", "guard"),
            "Чужое давление надломило волю хранителя.",
            async writeLease => await context.FileSystem.WriteFileAtomicBytesAsync(writeLease,
                ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("""
                    {"response":"Первый удар: Чужое давление надломило волю хранителя.","timestamp":"2026-01-01T00:00:00Z"}
                    """)));
        Assert.True(advanced.Disposition == "offer", advanced.Disposition + ": " +
            string.Join(Environment.NewLine,
                advanced.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var secondOffer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(advanced.Session);
        Assert.Equal(2, secondOffer.Offer!.MinimumSeverityRank);
        var completed = await secondOffer.SubmitDecisionAsync(lease,
            OriginalSpiritualWoundDecision(secondOffer.Offer!.OpportunityRef,
                "spiritual_action_cost_burden", "guard"),
            "Чужое давление надломило волю хранителя.",
            async writeLease => await context.FileSystem.WriteFileAtomicBytesAsync(writeLease,
                ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("""
                    {"response":"Первый и второй удары: Чужое давление надломило волю хранителя.","timestamp":"2026-01-01T00:00:00Z"}
                    """)));
        Assert.NotEqual("completed_unpublished", completed.Disposition);
        Assert.Contains(completed.Issues, issue =>
            issue.Code == "wound_worsening_severity_not_higher");
    }
}
