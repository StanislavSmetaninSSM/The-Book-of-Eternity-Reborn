using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Fact]
    public async Task OriginalSpiritualGeneration_RejectsMagnitudeOutsideCurrentSeverityBeforeInstallation()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonicalBefore = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step!.Interval!, Assert.Single(source.Sources));
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
            "spiritual_action_cost_burden", "guard").GetRawText())!;
        decision["proposal"]!["consequenceDefinitions"]![0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 3;
        var result = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
            System.Text.Json.JsonSerializer.SerializeToElement(decision), "Чужое давление надломило волю хранителя.");
        Assert.Null(result.Wound);
        Assert.Contains(result.Issues, issue => issue.Code == "wound_consequence_spiritual_magnitude_invalid");
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(OriginalCaptureField(capture, "_effects"));
        Assert.Empty(draft.Phases);
        Assert.False(Assert.IsType<bool>(OriginalCaptureField(draft, "_initializationAttempted")));
        var corrected = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
            OriginalSpiritualWoundDecision(offered.Opportunity.PublicRef, "spiritual_action_cost_burden", "guard"),
            "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(corrected.Issues);
        Assert.NotNull(corrected.Wound);
        Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData(false, false, "guard", false)]
    [InlineData(true, true, "guard", false)]
    [InlineData(true, false, "guard", false)]
    [InlineData(true, false, "pressure", false)]
    [InlineData(false, true, "guard", false)]
    [InlineData(true, true, "guard", true)]
    [InlineData(true, false, "guard", true)]
    public async Task OriginalSpiritualGeneration_InsertedCostBurdenControlsNextExchange(
        bool materialize, bool includeBurden, string woundOperation, bool upperCaseOperation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var secondExchange = active["exchangeLog"]![1]!.AsObject();
        secondExchange["outcome"] = "no_effect";
        secondExchange["after"] = secondExchange["before"]!.DeepClone();
        secondExchange.Remove("diceAudit");
        secondExchange["matchupAudit"]!["oppositionOperation"] = upperCaseOperation ? "GUARD" : "guard";
        var oppositionCost = secondExchange["actionCostAudit"]!["opposition"]!;
        oppositionCost["operationType"] = upperCaseOperation ? "GUARD" : "guard";
        oppositionCost["baseCost"] = 2;
        oppositionCost["effectiveCost"] = includeBurden ? 3 : 2;
        oppositionCost["after"] = includeBurden ? 0 : 1;
        active["oppositionSideStrain"] = secondExchange["before"]!["oppositionSideStrain"]!.DeepClone();
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonicalBefore = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstSource = Assert.Single(source.Sources);
        var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = materialize ? OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef,
            "spiritual_action_cost_burden", woundOperation) :
            System.Text.Json.JsonSerializer.SerializeToElement(new { opportunityRef = offered.Opportunity!.PublicRef, decision = "none" });
        var wound = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            materialize ? "Чужое давление надломило волю хранителя." : null);
        AssertNoConflictFrameErrors(wound.Issues);
        Assert.Equal(materialize, wound.Wound != null);
        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        if (includeBurden != (materialize && woundOperation == "guard"))
        {
            Assert.Null(second.Step);
            Assert.Contains(second.Issues, issue => issue.Code == "afterlife_conflict_opposition_action_cost_mismatch");
        }
        else
        {
            Assert.True(!second.Issues.Any(issue => issue.Severity == IssueSeverity.Error),
                string.Join(Environment.NewLine, second.Issues.Select(issue =>
                    $"{issue.Code}: expected={issue.Expected}; actual={issue.Actual}")));
            AssertNoConflictFrameErrors(second.Issues);
            Assert.Equal(1, second.Step!.Interval!.Ordinal);
        }
        Assert.Same(firstSource, Assert.Single(source.Sources));
        Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }

    [Theory]
    [InlineData(false, null, false)]
    [InlineData(true, null, false)]
    [InlineData(true, "scalar", false)]
    [InlineData(true, "array", false)]
    [InlineData(true, "scalar", true)]
    [InlineData(true, "array", true)]
    public async Task OriginalSpiritualGeneration_HindranceDoesNotCreateRollForNoEffect(
        bool materialize, string? malformedAudit, bool appendAfterWound)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var secondExchange = active["exchangeLog"]![1]!.AsObject();
        secondExchange["outcome"] = "no_effect";
        secondExchange["after"] = secondExchange["before"]!.DeepClone();
        secondExchange.Remove("diceAudit");
        if (malformedAudit != null)
            secondExchange["diceAudit"] = malformedAudit == "array" ? new JsonArray() : JsonValue.Create(1);
        active["oppositionSideStrain"] = secondExchange["before"]!["oppositionSideStrain"]!.DeepClone();
        if (appendAfterWound)
            active["exchangeLog"]!.AsArray().RemoveAt(1);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonicalBefore = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        if (malformedAudit != null && !appendAfterWound)
        {
            Assert.Null(first.Step);
            Assert.Contains(first.Issues, issue => issue.Code == "spiritual_source_continuation_invalid");
            Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            return;
        }
        AssertNoConflictFrameErrors(first.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstSource = Assert.Single(source.Sources);
        var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(admitted.Issues);
        var offered = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(offered.Issues);
        var decision = materialize ? OriginalSpiritualWoundDecision(offered.Opportunity!.PublicRef) :
            System.Text.Json.JsonSerializer.SerializeToElement(new { opportunityRef = offered.Opportunity!.PublicRef, decision = "none" });
        var wound = await capture.MaterializeWoundAsync(lease, admitted.Admission!, decision,
            materialize ? "Чужое давление надломило волю хранителя." : null);
        AssertNoConflictFrameErrors(wound.Issues);
        Assert.Equal(materialize, wound.Wound != null);
        await lease.DisposeAsync();
        if (appendAfterWound)
        {
            active["exchangeLog"]!.AsArray().Add(secondExchange.DeepClone());
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        }
        await using var nextLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var second = await capture.AdvanceNextResourceExchangeAsync(nextLease);
        if (appendAfterWound)
        {
            Assert.Null(second.Step);
            Assert.Contains(second.Issues, issue => issue.Code == "spiritual_wound_dice_audit_invalid");
        }
        else
        {
            AssertNoConflictFrameErrors(second.Issues);
            Assert.Equal(1, second.Step!.Interval!.Ordinal);
        }
        Assert.Same(firstSource, Assert.Single(source.Sources));
        Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(nextLease, AfterlifeEntityProfileState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, nextLease));
    }

    /// <summary>
    /// Verifies that a newly inserted, source-owned spiritual hindrance keeps its exact
    /// wound, effect, owner, target, dice, and accepted-prefix authority into the next exchange.
    /// </summary>
    /// <param name="includeHindrance">
    /// <see langword="true"/> supplies the exact next-exchange disadvantage required by the
    /// inserted wound; <see langword="false"/> omits it and must be rejected.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OriginalSpiritualGeneration_InsertedHindranceControlsNextExchange(bool includeHindrance)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 12, 8, 4]);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteSourceContinuationAppendAsync(context, duplicateDice: false);
        if (includeHindrance)
        {
            var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
            var dice = candidate["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!;
            dice["diceUsed"]![1]!["selection"] = "discarded";
            dice["diceUsed"]!.AsArray().Add(new JsonObject
            {
                ["side"] = "opposition", ["sourceIndex"] = 4, ["sides"] = 20,
                ["value"] = 4, ["selection"] = "selected"
            });
            dice["oppositionTotal"] = 4;
            dice["margin"] = 8;
            dice["outcomeBand"] = "decisive_player_success";
            dice["rollMode"] = new JsonObject
            {
                ["opposition"] = new JsonObject
                {
                    ["effectiveMode"] = "disadvantage",
                    ["advantageSources"] = new JsonArray(),
                    ["disadvantageSources"] = new JsonArray("Надлом воли мешает встречному давлению.")
                }
            };
            await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        }
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var canonicalBefore = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithPrefixAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = captured.Capture!;
        AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
        var first = await capture.AdvanceNextResourceExchangeAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var source = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(OriginalCaptureField(capture, "_source"));
        var firstMechanics = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>(
            OriginalCaptureField(source, "_mechanicsContext"));
        Assert.False(firstMechanics.IsCurrent);
        var firstSource = Assert.Single(source.Sources);
        var admitted = await capture.AdmitWoundSourceAsync(lease, first.Step!.Interval!, firstSource);
        AssertNoConflictFrameErrors(admitted.Issues);
        var opportunity = await capture.ReadWoundOpportunityAsync(lease, admitted.Admission!);
        AssertNoConflictFrameErrors(opportunity.Issues);
        var oldTicket = await source.PrepareContinuationAsync(lease);
        AssertNoConflictFrameErrors(oldTicket.Issues);
        Assert.NotNull(oldTicket.Ticket);
        var wound = await capture.MaterializeWoundAsync(lease, admitted.Admission!,
            OriginalSpiritualWoundDecision(opportunity.Opportunity!.PublicRef),
            "Чужое давление надломило волю хранителя.");
        AssertNoConflictFrameErrors(wound.Issues);
        var insertedWound = Assert.IsType<WoundMaterializationEnvelope>(wound.Wound);
        Assert.Equal("guardian", insertedWound.Owner.OwnerKind);
        Assert.Equal("guardian_frame", insertedWound.Owner.OwnerId);
        var insertedRoot = Assert.Single(insertedWound.Consequences.OwnedEffectSources.RootBindings);
        Assert.Equal("definition_spiritual_wound_test", insertedRoot.DefinitionKey);
        var draft = Assert.IsType<EffectAcceptedTurnPlanner.EffectAcceptedDraft>(
            OriginalCaptureField(capture, "_effects"));
        var activeEffects = Assert.IsType<List<JsonObject>>(draft.GetType().GetField(
                "activeEffects",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(draft));
        var insertedEffect = Assert.Single(activeEffects, effect =>
            effect["effectId"]?.GetValue<string>() == insertedRoot.EffectId);
        Assert.Equal(insertedWound.WoundId,
            insertedEffect["source"]!["sourceId"]!.GetValue<string>());
        Assert.Equal("definition_spiritual_wound_test",
            insertedEffect["source"]!["definitionKey"]!.GetValue<string>());
        Assert.Equal("guardian", insertedEffect["target"]!["kind"]!.GetValue<string>());
        Assert.Equal("guardian_frame", insertedEffect["target"]!["targetId"]!.GetValue<string>());
        Assert.Equal("spiritual_roll_hindrance",
            Assert.Single(insertedEffect["components"]!.AsArray())!["profile"]!.GetValue<string>());
        Assert.Equal("guardian:guardian_frame", firstSource.AffectedActor);
        Assert.Equal("exchange_conflict_frame_42", firstSource.ExchangeId);
        Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        var stale = source.CommitPreparedContinuation(lease, oldTicket.Ticket!);
        Assert.Null(stale.Session);
        Assert.Contains(stale.Issues, issue => issue.Code == "spiritual_source_continuation_ticket_stale");
        var second = await capture.AdvanceNextResourceExchangeAsync(lease);
        if (includeHindrance)
        {
            AssertNoConflictFrameErrors(second.Issues);
            Assert.Equal(1, second.Step!.Interval!.Ordinal);
            Assert.Equal(2, source.Sources.Count);
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, source.ClaimedDice);
        }
        else
        {
            Assert.Null(second.Step);
            Assert.Contains(second.Issues, issue => issue.Code == "spiritual_wound_roll_hindrance_missing");
            Assert.Single(source.Sources);
            Assert.Equal(new[] { 0, 1 }, source.ClaimedDice);
        }
        Assert.Same(firstSource, source.Sources[0]);
        Assert.Equal(canonicalBefore, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
    }
}
