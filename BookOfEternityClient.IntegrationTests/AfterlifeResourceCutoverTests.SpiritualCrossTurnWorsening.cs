using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Reuses the exact signed prior-turn conflict wound when a later turn adds a stronger source.
    /// </summary>
    /// <param name="foreignOrigin">
    /// Receipt reference or registration to alter before signing the next turn; <see langword="null"/> keeps the valid origin.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("wound")]
    [InlineData("transition")]
    [InlineData("missing_identity")]
    [InlineData("missing_history")]
    [InlineData("missing_receipt")]
    [InlineData("empty_receipt")]
    [InlineData("missing_receipt_direct")]
    [InlineData("missing_receipt_retrauma")]
    [InlineData("relocated_receipt")]
    [InlineData("healed_missing_receipt")]
    [InlineData("healed_intact_receipt")]
    [InlineData("malformed_receipt_utf8")]
    [InlineData("malformed_receipt_utf8_direct")]
    public async Task OriginalSpiritualC2_NextTurnTargetsSignedConflictWound(string? foreignOrigin)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Первый обмен завершён.\"}"));
        JsonObject firstReceipt;
        AcceptedMechanicsPlan firstPlan;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            _ = context.FileSystem.GetOrCreateSessionGeneration(lease);
            var validator = new ValidationService(context.FileSystem,
                NullLogger<ValidationService>.Instance);
            var captured = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            using (var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
            {
                AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
                var step = await warm.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(step.Issues);
                var committed = await warm.CommitC2FirstTransportAsync(lease, step.Step!.Interval!);
                Assert.True(committed.Disposition == "committed", committed.Disposition + ": " +
                    string.Join(Environment.NewLine,
                        committed.Issues.Select(issue => $"{issue.Code}: {issue}")));
            }
            var opened = await validator.OpenC2PrivateSessionAsync(lease);
            Assert.True(opened.Disposition == "offer", opened.Disposition + ": " +
                string.Join(Environment.NewLine,
                    opened.Issues.Select(issue => $"{issue.Code}: {issue}")));
            using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var completed = await offer.SubmitDecisionAsync(lease,
                OriginalSpiritualWoundDecision(offer.Offer!.OpportunityRef,
                    "spiritual_action_cost_burden", "guard"),
                "Чужое давление надломило волю хранителя.");
            Assert.Equal("completed_unpublished", completed.Disposition);
            using var terminal = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(completed.Session);
            var reduced = await terminal.ReduceCompletedDecisionsAsync(lease);
            Assert.True(reduced.Success, string.Join(Environment.NewLine,
                reduced.Issues.Select(issue => $"{issue.Code}: {issue}")));
            firstReceipt = reduced.ReceiptAfterImage!.DeepClone().AsObject();
            var ordinary = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
                reduced.Reduction);
            var planned = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
                AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                    ordinary, ordinary.Resources));
            Assert.Empty(planned.Issues);
            firstPlan = Assert.IsType<AcceptedMechanicsPlan>(planned.Plan);
        }

        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.DefinitionPath,
            firstPlan.DefinitionAfterImage.ToJsonString());
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.StatePath,
            firstPlan.StateAfterImage.ToJsonString());
        await context.WriteExactJsonAsync(AcceptedMechanicsPlan.HistoryPath,
            firstPlan.HistoryAfterImage.ToJsonString());
        await context.WriteExactJsonAsync(EffectAcceptedTurnPlan.IdentityIndexPath,
            firstPlan.EffectIdentityAfterImage.ToJsonString());
        foreach (var (path, root) in firstPlan.EffectCarrierAfterImages)
            await context.WriteExactJsonAsync(path, root.ToJsonString());
        foreach (var (path, root) in firstPlan.OwnerCompanionAfterImages)
            await context.WriteExactJsonAsync(path, root.ToJsonString());
        foreach (var (path, root) in firstPlan.WoundCarrierAfterImages)
            await context.WriteExactJsonAsync(path, root.ToJsonString());
        await context.WriteExactJsonAsync(WoundIdentityState.StatePath,
            firstPlan.WoundIdentityAfterImage!.ToJsonString());
        await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath,
            firstPlan.WoundHistoryAfterImage!.ToJsonString());
        await context.WriteExactJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath,
            firstReceipt.ToJsonString());
        foreach (var (path, root) in firstPlan.PendingAfterImages)
        {
            if (root is null)
                await context.DeleteAsync(path);
            else
                await context.WriteExactJsonAsync(path, root.ToJsonString());
        }
        foreach (var path in firstPlan.ConsumedPaths)
            await context.DeleteAsync(path);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            firstPlan.DefinitionAfterImage.ToJsonString(), allowMissingPristine: false);
        Assert.True(definitions.IsValid);
        var state = ResourceStateContract.ParseCanonical(
            firstPlan.StateAfterImage.ToJsonString(), definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(state.IsValid);
        var history = ResourceHistoryState.ParseCanonical(
            firstPlan.HistoryAfterImage.ToJsonString(), definitions.Catalog!,
            allowMissingPristine: false);
        Assert.True(history.IsValid);
        await WriteComposedAuthorityAsync(context, definitions.Catalog!, state.Ledger!,
            history.History!);
        await context.DeleteAsync(SpiritualWoundCaptureCheckpointState.StatePath);
        await context.DeleteAsync(SpiritualWoundDecisionPendingState.StatePath);

        if (foreignOrigin is "wound" or "transition" or "relocated_receipt")
        {
            var alteredReceipt = firstReceipt.DeepClone().AsObject();
            var row = alteredReceipt["decisions"]![0]!.AsObject();
            if (foreignOrigin == "relocated_receipt")
            {
                var instance = alteredReceipt["instances"]![0]!.AsObject();
                instance["displayConflictId"] = "another_conflict";
                var instanceHash = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(instance, "instance");
                instance["instanceFingerprint"] = instanceHash;
                instance["instanceId"] = "spiritual_instance_" + instanceHash[7..];
                var source = alteredReceipt["sources"]![0]!.AsObject();
                source["instanceId"] = instance["instanceId"]!.DeepClone();
                var witness = source["witness"]!.AsObject();
                witness["conflictId"] = "another_conflict";
                var sourceHash = SpiritualWoundStateJson.Hash(witness, "source", "sourceId", "sourceFingerprint");
                witness["sourceFingerprint"] = sourceHash;
                witness["sourceId"] = "spiritual_source_" + sourceHash[7..];
                row["instanceId"] = instance["instanceId"]!.DeepClone();
                row["sourceId"] = witness["sourceId"]!.DeepClone();
                row["sourceFingerprint"] = sourceHash;
                row["sourceWitness"] = witness.DeepClone();
            }
            else
                row[foreignOrigin == "wound" ? "woundId" : "transitionId"] = "foreign_origin";
            row["decisionFingerprint"] = SpiritualWoundStateJson.Hash(
                row, "decision", "decisionFingerprint");
            var parsed = SpiritualWoundOpportunityReceiptState.Parse(alteredReceipt.ToJsonString(),
                SpiritualWoundOpportunityReceiptState.StatePath);
            Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
            await context.WriteExactJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath,
                alteredReceipt.ToJsonString());
        }
        if (foreignOrigin == "missing_identity")
        {
            var missingIdentity = new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() };
            Assert.True(WoundIdentityState.Parse(missingIdentity.ToJsonString(), WoundIdentityState.StatePath).IsValid);
            await context.WriteExactJsonAsync(WoundIdentityState.StatePath, missingIdentity.ToJsonString());
        }
        if (foreignOrigin == "missing_history")
        {
            var missingHistory = new JsonObject
            {
                ["schemaVersion"] = 1, ["nextOrdinal"] = 1, ["transitions"] = new JsonArray()
            };
            Assert.True(WoundHistoryState.Parse(missingHistory.ToJsonString(), WoundHistoryState.HistoryPath).IsValid);
            await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath, missingHistory.ToJsonString());
        }
        if (foreignOrigin?.StartsWith("missing_receipt", StringComparison.Ordinal) == true)
            await context.DeleteAsync(SpiritualWoundOpportunityReceiptState.StatePath);
        if (foreignOrigin is "healed_missing_receipt" or "healed_intact_receipt")
        {
            // Exercise an admitted terminal baseline, not an unimplemented live healing dispatch.
            var healedProfiles = firstPlan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath]
                .DeepClone().AsObject();
            foreach (var profile in healedProfiles["profiles"]!.AsArray())
                if (profile!["activeWounds"] is JsonArray wounds)
                    wounds.Clear();
            var healedHistory = firstPlan.WoundHistoryAfterImage!.DeepClone().AsObject();
            var transitions = healedHistory["transitions"]!.AsArray();
            var heal = transitions[0]!.DeepClone().AsObject();
            heal["transitionId"] = "transition_healed_before_next_turn";
            heal["operationKey"] = "operation_healed_before_next_turn";
            heal["eventRef"] = "event_healed_before_next_turn";
            heal["ordinal"] = 2;
            heal["woundTransitionOrdinal"] = 2;
            heal["kind"] = "heal";
            heal["terminal"] = true;
            heal["beforeFingerprint"] = heal["afterFingerprint"]!.DeepClone();
            heal["afterFingerprint"] = "sha256:" + new string('b', 64);
            transitions.Add(heal);
            healedHistory["nextOrdinal"] = 3;
            var healedIdentity = firstPlan.WoundIdentityAfterImage!.DeepClone().AsObject();
            var entry = healedIdentity["entries"]![0]!;
            entry["status"] = "healed";
            entry["lastTransitionOrdinal"] = 2;
            entry["terminalTransitionId"] = heal["transitionId"]!.DeepClone();
            entry["semanticFingerprint"] = heal["afterFingerprint"]!.DeepClone();
            var parsedHistory = WoundHistoryState.Parse(healedHistory.ToJsonString(), WoundHistoryState.HistoryPath);
            var parsedIdentity = WoundIdentityState.Parse(healedIdentity.ToJsonString(), WoundIdentityState.StatePath);
            var healedCatalog = WoundCarrierCatalog.Build(new(null, null, null, null, healedProfiles));
            Assert.True(parsedHistory.IsValid, string.Join(Environment.NewLine, parsedHistory.Issues));
            Assert.True(parsedIdentity.IsValid, string.Join(Environment.NewLine, parsedIdentity.Issues));
            Assert.Empty(healedCatalog.Issues);
            Assert.Empty(parsedHistory.State!.ValidateAgreement(parsedIdentity.State!, healedCatalog));
            await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, healedProfiles.ToJsonString());
            await context.WriteExactJsonAsync(WoundIdentityState.StatePath, healedIdentity.ToJsonString());
            await context.WriteExactJsonAsync(WoundHistoryState.HistoryPath, healedHistory.ToJsonString());
            if (foreignOrigin == "healed_missing_receipt")
                await context.DeleteAsync(SpiritualWoundOpportunityReceiptState.StatePath);
        }
        if (foreignOrigin == "empty_receipt")
        {
            var emptyReceipt = new JsonObject
            {
                ["schemaVersion"] = 1, ["nextInstanceOrdinal"] = 1, ["nextClosureOrdinal"] = 1,
                ["nextSourceOrdinal"] = 1, ["nextDecisionOrdinal"] = 1,
                ["instances"] = new JsonArray(), ["closures"] = new JsonArray(),
                ["sources"] = new JsonArray(), ["decisions"] = new JsonArray()
            };
            Assert.True(SpiritualWoundOpportunityReceiptState.Parse(emptyReceipt.ToJsonString(),
                SpiritualWoundOpportunityReceiptState.StatePath).IsValid);
            await context.WriteExactJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath,
                emptyReceipt.ToJsonString());
        }
        if (foreignOrigin is "malformed_receipt_utf8" or "malformed_receipt_utf8_direct")
            await context.WriteExactBytesAsync(SpiritualWoundOpportunityReceiptState.StatePath, [0xff]);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8],
            sessionId: "session_resource_materialization_43",
            requestId: "request_resource_materialization_43");
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var second = candidate["activeConflict"]!["exchangeLog"]![1]!;
        second["turnNumber"] = 43;
        second["after"]!["oppositionSideStrain"] = "broken";
        if (foreignOrigin == "missing_receipt_retrauma")
        {
            second["spiritualWoundTarget"] = new JsonObject
            {
                ["actorType"] = "guardian", ["actorId"] = "guardian_frame",
                ["retraumaWoundRef"] = firstReceipt["decisions"]![0]!["woundId"]!.DeepClone()
            };
        }
        candidate["activeConflict"]!["oppositionSideStrain"] = "broken";
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath,
            candidate.ToJsonString());
        await context.WriteExactBytesAsync(ProjectionNarrativePath,
            Encoding.UTF8.GetBytes("{\"response\":\"Второй обмен завершён.\"}"));
        var canonicalBefore = await context.FileSystem.ReadFileBytesAsync(
            AfterlifeEntityProfileState.StatePath);
        var identityBefore = await context.FileSystem.ReadFileBytesAsync(
            WoundIdentityState.StatePath);
        var historyBefore = await context.FileSystem.ReadFileBytesAsync(
            WoundHistoryState.HistoryPath);
        var nextValidator = new ValidationService(context.FileSystem,
            NullLogger<ValidationService>.Instance);
        await using var nextLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var nextCapture = foreignOrigin is "missing_receipt_direct" or "missing_receipt_retrauma" or "malformed_receipt_utf8_direct"
            ? await nextValidator.CaptureSpiritualOriginalTurnWithPrefixAsync(nextLease)
            : await nextValidator.CaptureSpiritualOriginalTurnWithIntakeAsync(nextLease);
        Assert.True(nextCapture.Issues.All(issue => issue.Severity != IssueSeverity.Error),
            string.Join(Environment.NewLine, nextCapture.Issues.Select(issue =>
                $"{issue.Code}: {issue.Message} expected={issue.Expected} actual={issue.Actual}")));
        using (var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(nextCapture.Capture))
        {
            AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(nextLease));
            var step = await warm.AdvanceNextResourceExchangeAsync(nextLease);
            AssertNoConflictFrameErrors(step.Issues);
            if (foreignOrigin is "missing_receipt_direct" or "missing_receipt_retrauma" or "malformed_receipt_utf8_direct")
            {
                var sourceOwner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(
                    OriginalCaptureField(warm, "_source"));
                var source = Assert.Single(sourceOwner.Sources);
                if (foreignOrigin == "missing_receipt_retrauma")
                    Assert.Equal(firstReceipt["decisions"]![0]!["woundId"]!.GetValue<string>(),
                        source.RetraumaWoundId);
                var admission = await warm.AdmitWoundSourceAsync(nextLease, step.Step!.Interval!,
                    source);
                AssertNoConflictFrameErrors(admission.Issues);
                var offered = await warm.ReadWoundOpportunityAsync(nextLease, admission.Admission!);
                Assert.Null(offered.Opportunity);
                Assert.Null(offered.Satisfaction);
                var directExpectedCode = foreignOrigin == "malformed_receipt_utf8_direct"
                    ? "spiritual_wound_receipt_invalid_state"
                    : "spiritual_wound_conflict_side_receipt_missing";
                Assert.Contains(offered.Issues, issue => issue.Code == directExpectedCode &&
                    issue.FilePath == SpiritualWoundOpportunityReceiptState.StatePath);
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(nextLease,
                    SpiritualWoundCaptureCheckpointState.StatePath));
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(nextLease,
                    SpiritualWoundDecisionPendingState.StatePath));
                Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                    AfterlifeEntityProfileState.StatePath));
                Assert.Equal(identityBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                    WoundIdentityState.StatePath));
                Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                    WoundHistoryState.HistoryPath));
                return;
            }
            var committed = await warm.CommitC2FirstTransportAsync(nextLease, step.Step!.Interval!);
            if (foreignOrigin is not null and not "healed_intact_receipt")
            {
                Assert.Equal("blocked", committed.Disposition);
                Assert.Null(committed.Checkpoint);
                Assert.Null(committed.Pending);
                var expectedCode = foreignOrigin switch
                {
                    "missing_identity" => "wound_history_identity_missing",
                    "missing_history" => "wound_history_missing_for_identity",
                    "malformed_receipt_utf8" => "spiritual_wound_receipt_invalid_state",
                    _ => "spiritual_wound_conflict_side_receipt_missing"
                };
                Assert.Contains(committed.Issues, issue => issue.Code == expectedCode);
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(nextLease,
                    SpiritualWoundCaptureCheckpointState.StatePath));
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(nextLease,
                    SpiritualWoundDecisionPendingState.StatePath));
                Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                    AfterlifeEntityProfileState.StatePath));
                Assert.Equal(identityBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                    WoundIdentityState.StatePath));
                Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                    WoundHistoryState.HistoryPath));
                return;
            }
            Assert.True(committed.Disposition == "committed", committed.Disposition + ": " +
                string.Join(Environment.NewLine,
                    committed.Issues.Select(issue => $"{issue.Code}: {issue}")));
        }
        var next = await nextValidator.OpenC2PrivateSessionAsync(nextLease);
        if (foreignOrigin == "healed_intact_receipt")
        {
            // Receipt integrity passes; a healed conflict wound cannot become a fresh creation target.
            Assert.Equal("blocked", next.Disposition);
            Assert.Null(next.Session);
            Assert.Contains(next.Issues, issue => issue.Code == "spiritual_wound_same_conflict_target_stale");
            Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                AfterlifeEntityProfileState.StatePath));
            Assert.Equal(identityBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                WoundIdentityState.StatePath));
            Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
                WoundHistoryState.HistoryPath));
            return;
        }
        Assert.True(next.Disposition == "offer", next.Disposition + ": " +
            string.Join(Environment.NewLine,
                next.Issues.Select(issue => $"{issue.Code}: {issue}")));
        using var nextOffer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(next.Session);
        Assert.Equal(2, nextOffer.Offer!.MinimumSeverityRank);
        var worsening = JsonNode.Parse(OriginalSpiritualWoundDecision(
            nextOffer.Offer.OpportunityRef, "spiritual_action_cost_burden", "guard").GetRawText())!;
        worsening["proposal"]!["severity"] = "II";
        var consequenceDefinitions = worsening["proposal"]!["consequenceDefinitions"]!.AsArray();
        var extra = consequenceDefinitions[0]!.DeepClone();
        extra["definitionRef"] = "maneuver_burden";
        extra["definition"]!["definitionKey"] = "maneuver_burden";
        extra["definition"]!["stacking"]!["stackKey"] = "stack_maneuver_burden";
        extra["definition"]!["components"]![0]!["payload"]!["operation"] = "maneuver";
        consequenceDefinitions.Add(extra);
        var completedSecond = await nextOffer.SubmitDecisionAsync(nextLease,
            System.Text.Json.JsonSerializer.SerializeToElement(worsening),
            "Чужое давление надломило волю хранителя.");
        Assert.True(completedSecond.Disposition == "completed_unpublished",
            completedSecond.Disposition + ": " + string.Join(Environment.NewLine,
                completedSecond.Issues.Select(issue =>
                    $"{issue.Code}: {issue.Message} expected={issue.Expected} actual={issue.Actual}")));
        using var terminalSecond = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(
            completedSecond.Session);
        var reducedSecond = await terminalSecond.ReduceCompletedDecisionsAsync(nextLease);
        Assert.True(reducedSecond.Success, string.Join(Environment.NewLine,
            reducedSecond.Issues.Select(issue => $"{issue.Code}: {issue}")));
        var ordinarySecond = Assert.IsType<AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction>(
            reducedSecond.Reduction);
        var proof = Assert.IsType<SpiritualLiveWoundCompletion>(ordinarySecond.LiveWoundCompletion);
        var insertion = Assert.Single(proof.Insertions);
        Assert.Equal("worsen", Assert.Single(insertion.Input.Transitions).Kind);
        Assert.Equal(firstReceipt["decisions"]![0]!["woundId"]!.GetValue<string>(),
            insertion.Wound.WoundId);
        Assert.Equal(2, insertion.Wound.Severity.Rank);
        Assert.Equal(insertion.Wound.WoundId,
            reducedSecond.ReceiptAfterImage!["decisions"]![1]!["woundId"]!.GetValue<string>());
        var receiptRows = reducedSecond.ReceiptAfterImage["decisions"]!.AsArray();
        Assert.Equal(2, receiptRows.Count);
        Assert.Equal("materialize", receiptRows[1]!["decision"]!.GetValue<string>());
        Assert.NotEqual(receiptRows[0]!["transitionId"]!.GetValue<string>(),
            receiptRows[1]!["transitionId"]!.GetValue<string>());
        var plannedSecond = AcceptedMechanicsPlanner.CompleteAcceptedReduction(
            AcceptedMechanicsPlanner.AcceptedMechanicsReduction.CompletedOrdinary(
                ordinarySecond, ordinarySecond.Resources));
        Assert.Empty(plannedSecond.Issues);
        var secondPlan = Assert.IsType<AcceptedMechanicsPlan>(plannedSecond.Plan);
        var finalCarriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            null, null, null, null,
            secondPlan.WoundCarrierAfterImages[AfterlifeEntityProfileState.StatePath]));
        Assert.Empty(finalCarriers.Issues);
        Assert.True(finalCarriers.TryResolveOne(insertion.Wound.WoundId,
            out var finalOccurrence));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(insertion.Wound),
            WoundMaterializationContract.SerializeCanonical(finalOccurrence.Wound));
        Assert.True(JsonNode.DeepEquals(insertion.ReducedState.Identity,
            secondPlan.WoundIdentityAfterImage));
        Assert.True(JsonNode.DeepEquals(insertion.ReducedState.History,
            secondPlan.WoundHistoryAfterImage));
        Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
            AfterlifeEntityProfileState.StatePath));
        Assert.Equal(identityBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
            WoundIdentityState.StatePath));
        Assert.Equal(historyBefore, await context.FileSystem.ReadFileBytesAsync(nextLease,
            WoundHistoryState.HistoryPath));
    }
}
