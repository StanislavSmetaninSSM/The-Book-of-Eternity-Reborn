using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Independent full-v7 zero-ledger DATA; no live source-only owner or publication permission is fabricated.</summary>
/// <param name="output">Complete uncapped operands, reached phases and parser observations.</param>
public sealed partial class SpiritualGuardianGroupZeroReceiptTests(ITestOutputHelper output)
{
    /// <summary>Requires one schema3 comparison, unchanged ancestry, strict append and independently parsed exact replay after lawful schema1 premises.</summary>
    [Fact]
    public void GuardianGroupZero_FullSourceParsesAppendsAndReplays()
    {
        var inputs = CreateInputs();
        var before = inputs.PriorRoot;
        var evidence = inputs.Source["turnEvidence"]!;
        var originalConflictBytes = Encoding.UTF8.GetBytes(inputs.Fixture.OriginalConflictJson);
        var originalConflictImage = new CanonicalBeforeImage(true, originalConflictBytes);
        var resolved = SpiritualWoundFirstOfferInstanceResolver.Resolve(
            new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(inputs.PriorJson)),
            originalConflictImage, "chaos_sea",
            Text(evidence["sessionId"]), Text(evidence["requestId"]), Text(evidence["snapshotToken"]), 46);
        Log("instance_resolution", new { priorJson = inputs.PriorJson, inputs.Fixture.RawOrdinaryBase64,
            inputs.Fixture.OriginalConflictJson, originalConflictBytesBase64 = Convert.ToBase64String(originalConflictBytes),
            originalConflictFingerprint = originalConflictImage.Fingerprint,
            evidence, resolved.InstanceId, resolved.ProposedRow, resolved.Issues });
        Assert.Empty(resolved.Issues);
        var instanceId = Assert.IsType<string>(resolved.InstanceId);
        var proposed = Assert.IsType<JsonObject>(resolved.ProposedRow);
        Assert.Equal(originalConflictImage.Fingerprint, Text(proposed["baselineConflictFingerprint"]));
        var skeleton = before.DeepClone().AsObject();
        skeleton["instances"]!.AsArray().Add(proposed.DeepClone());
        skeleton["nextInstanceOrdinal"] = proposed["ordinal"]!.GetValue<int>() + 1;
        var skeletonState = ReadReceipt(skeleton, "schema_one_instance_only");
        var skeletonAppend = SpiritualWoundOpportunityReceiptState.PlanAppend(inputs.PriorState, skeletonState);
        Log("schema_one_instance_only_append", new { before, skeleton, skeletonAppend.Disposition,
            afterReturned = skeletonAppend.After is not null, priorHasPressure = inputs.PriorState.HasRetainedPressureComparisons,
            skeletonHasPressure = skeletonState.HasRetainedPressureComparisons });
        Assert.Equal(1, skeleton["schemaVersion"]!.GetValue<int>());
        Assert.False(inputs.PriorState.HasRetainedPressureComparisons); Assert.False(skeletonState.HasRetainedPressureComparisons);
        Assert.Equal("appended", skeletonAppend.Disposition);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.SerializeCanonical(skeletonState),
            SpiritualWoundOpportunityReceiptState.SerializeCanonical(Assert.IsType<SpiritualWoundOpportunityReceiptState>(skeletonAppend.After)));

        // An empty schema3 baseline is invalid. Only the independently accepted schema1 skeleton precedes this feature assertion.
        var candidate = skeleton.DeepClone().AsObject();
        candidate["schemaVersion"] = 3;
        candidate["healingSources"] = new JsonArray(); candidate["healingDecisions"] = new JsonArray();
        candidate["nextHealingSourceOrdinal"] = 1; candidate["nextHealingDecisionOrdinal"] = 1;
        candidate["nextStrainComparisonOrdinal"] = 2;
        candidate["strainComparisons"] = new JsonArray(new JsonObject
        {
            ["ordinal"] = 1, ["instanceId"] = instanceId, ["instanceComparisonOrdinal"] = 1,
            ["witness"] = inputs.Source.DeepClone()
        });
        var candidateJson = candidate.ToJsonString();
        var skeletonJson = skeleton.ToJsonString();
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(candidateJson, SpiritualWoundOpportunityReceiptState.StatePath);
        var fixtureUnchanged = inputs.FixtureJson == JsonSerializer.Serialize(inputs.Fixture);
        var sourceUnchanged = inputs.SourceJson == inputs.Source.ToJsonString();
        var priorUnchanged = inputs.PriorJson == before.ToJsonString();
        var candidateUnchanged = candidateJson == candidate.ToJsonString();
        var rawUnchanged = inputs.Fixture.RawOrdinaryBase64 == Convert.ToBase64String(inputs.RawOrdinaryBytes);
        Log("schema_three_desired_parse", new
        {
            fullSourceAccepted = true, completeOriginalGraphAccepted = true, allParentCreationAndHistoryAccepted = true,
            priorReceiptAccepted = true, schemaOneInstanceOnlyAppendAccepted = true,
            ordinary = inputs.Comparison.OrdinaryCalculation, actual = inputs.Comparison.ActualCalculation,
            sourceJson = inputs.SourceJson, parents = inputs.Fixture.Retained.Parents,
            rawOrdinaryBytesBase64 = Convert.ToBase64String(inputs.RawOrdinaryBytes), inputs.Fixture.OrdinaryRoot,
            priorJson = inputs.PriorJson, skeletonJson, candidateJson, parsed.IsValid, parsed.Issues,
            fixtureUnchanged, sourceUnchanged, priorUnchanged, candidateUnchanged, rawUnchanged
        });
        Assert.True(fixtureUnchanged); Assert.True(sourceUnchanged); Assert.True(priorUnchanged); Assert.True(candidateUnchanged);
        Assert.True(rawUnchanged);
        // Desired success is unconditional. The old zero-table eligibility guard must be observed here, not accepted as success.
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues)); Assert.Empty(parsed.Issues);
        var state = Assert.IsType<SpiritualWoundOpportunityReceiptState>(parsed.State);
        AssertZeroAppendShape(inputs, candidate, proposed, instanceId);

        var activeActual = inputs.Fixture.ActualRoot["activeConflict"]!.DeepClone().AsObject();
        var retained = state.HasRetainedPressureComparisons;
        var exactHistory = state.HasExactStrainHistory(inputs.History);
        var acceptedLog = state.MatchRetainedPressureHistory("chaos_sea", activeActual, 46);
        var laterAcceptedLog = state.MatchRetainedPressureHistory("chaos_sea", activeActual, 47);
        var ordinaryAfter = state.ReadRetainedStrainOrdinaryAfter("chaos_sea", Text(activeActual["conflictId"]), inputs.Fixture.Actual, true);
        Log("all_parent_history_and_accepted_log", new { receipt = candidate, history = inputs.Fixture.Retained.History,
            activeActual, retained, exactHistory, acceptedLog, laterAcceptedLog, ordinaryAfter,
            expectedOrdinaryAfter = inputs.Fixture.Ordinary["after"] });
        Assert.True(retained); Assert.True(exactHistory);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact, acceptedLog);
        Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact, laterAcceptedLog);
        Assert.True(JsonNode.DeepEquals(inputs.Fixture.Ordinary["after"], ordinaryAfter));

        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(inputs.PriorState, state);
        var canonical = SpiritualWoundOpportunityReceiptState.SerializeCanonical(state);
        Log("complete_prefix_append", new { before, candidate, append.Disposition, afterReturned = append.After is not null, canonical });
        Assert.Equal("appended", append.Disposition);
        var appended = Assert.IsType<SpiritualWoundOpportunityReceiptState>(append.After);
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended));

        // Freshly parse an independent complete candidate, rather than reusing either parsed object or testing self-replay.
        var replayRoot = JsonNode.Parse(candidateJson)!.AsObject();
        var replayState = ReadReceipt(replayRoot, "independently_parsed_exact_replay");
        var replay = SpiritualWoundOpportunityReceiptState.PlanAppend(appended, replayState);
        Log("complete_prefix_exact_replay", new { candidateJson, replayReceipt = replayRoot, replay.Disposition,
            afterReturned = replay.After is not null });
        Assert.NotSame(state, replayState); Assert.NotSame(appended, replayState);
        Assert.Equal("exact_replay", replay.Disposition);
        var replayed = Assert.IsType<SpiritualWoundOpportunityReceiptState>(replay.After);
        Assert.Equal(canonical, SpiritualWoundOpportunityReceiptState.SerializeCanonical(replayed));
        Log("inputs_before_escaped_mutation", new { fixture = inputs.Fixture, source = inputs.Source, before, skeleton, candidate,
            replayRoot, sourceUnchanged = inputs.SourceJson == inputs.Source.ToJsonString(),
            fixtureUnchanged = inputs.FixtureJson == JsonSerializer.Serialize(inputs.Fixture) });
        Assert.Equal(inputs.SourceJson, inputs.Source.ToJsonString());
        Assert.Equal(inputs.FixtureJson, JsonSerializer.Serialize(inputs.Fixture));
        Assert.Equal(inputs.PriorJson, before.ToJsonString());
        Assert.Equal(skeletonJson, skeleton.ToJsonString()); Assert.Equal(candidateJson, candidate.ToJsonString());
        Assert.Equal(candidateJson, replayRoot.ToJsonString());
        Assert.Equal(inputs.Fixture.RawOrdinaryBase64, Convert.ToBase64String(inputs.RawOrdinaryBytes));

        inputs.Source["maximumSeverityRank"] = 4;
        inputs.Fixture.Retained.Parents[1]["display"]!["description"] = "Escaped fixture mutation must not alter retained DATA.";
        inputs.Fixture.Actual["after"]!["oppositionSideStrain"] = "clear";
        candidate["strainComparisons"]![0]!["witness"]!["maximumSeverityRank"] = 4;
        replayRoot["strainComparisons"]!.AsArray().Clear();
        var stateAfterMutation = SpiritualWoundOpportunityReceiptState.SerializeCanonical(state);
        var appendedAfterMutation = SpiritualWoundOpportunityReceiptState.SerializeCanonical(appended);
        var replayAfterMutation = SpiritualWoundOpportunityReceiptState.SerializeCanonical(replayState);
        var replayedAfterMutation = SpiritualWoundOpportunityReceiptState.SerializeCanonical(replayed);
        Log("escaped_mutable_data_is_detached", new { mutatedFixture = inputs.Fixture, mutatedSource = inputs.Source,
            mutatedCandidate = candidate, mutatedReplay = replayRoot, canonical, stateAfterMutation,
            appendedAfterMutation, replayAfterMutation, replayedAfterMutation });
        Assert.Equal(canonical, stateAfterMutation); Assert.Equal(canonical, appendedAfterMutation);
        Assert.Equal(canonical, replayAfterMutation); Assert.Equal(canonical, replayedAfterMutation);
    }

    /// <summary>Checks entire original array prefixes and cursors while adding one instance and one comparison, never an injury or decision.</summary>
    /// <param name="inputs">Once-validated invocation-local ancestry and source DATA.</param>
    /// <param name="candidate">Complete independently authored schema3 image.</param>
    /// <param name="proposed">Exact resolver-proposed new instance.</param>
    /// <param name="instanceId">Exact containing instance identity.</param>
    private void AssertZeroAppendShape(Inputs inputs, JsonObject candidate, JsonObject proposed, string instanceId)
    {
        var before = inputs.PriorRoot;
        Log("zero_append_shape", new { before, candidate, proposed, instanceId, sourceId = inputs.Source["sourceId"] });
        Assert.Equal(3, candidate["schemaVersion"]!.GetValue<int>());
        foreach (var (field, node) in before)
            if (field is not ("schemaVersion" or "instances" or "nextInstanceOrdinal"))
                Assert.True(JsonNode.DeepEquals(node, candidate[field]), field);
        Assert.Equal((2, 2, 2, 2), (before["instances"]!.AsArray().Count, before["sources"]!.AsArray().Count,
            before["decisions"]!.AsArray().Count, before["closures"]!.AsArray().Count));
        Assert.Equal((3, 2, 2, 2), (candidate["instances"]!.AsArray().Count, candidate["sources"]!.AsArray().Count,
            candidate["decisions"]!.AsArray().Count, candidate["closures"]!.AsArray().Count));
        for (var index = 0; index < before["instances"]!.AsArray().Count; index++)
            Assert.True(JsonNode.DeepEquals(before["instances"]![index], candidate["instances"]![index]));
        Assert.True(JsonNode.DeepEquals(proposed, candidate["instances"]![2]));
        Assert.Equal((3, 3, 3, 3), (before["nextInstanceOrdinal"]!.GetValue<int>(), before["nextSourceOrdinal"]!.GetValue<int>(),
            before["nextDecisionOrdinal"]!.GetValue<int>(), before["nextClosureOrdinal"]!.GetValue<int>()));
        Assert.Equal((4, 3, 3, 3), (candidate["nextInstanceOrdinal"]!.GetValue<int>(), candidate["nextSourceOrdinal"]!.GetValue<int>(),
            candidate["nextDecisionOrdinal"]!.GetValue<int>(), candidate["nextClosureOrdinal"]!.GetValue<int>()));
        Assert.Empty(candidate["healingSources"]!.AsArray()); Assert.Empty(candidate["healingDecisions"]!.AsArray());
        Assert.Equal((1, 1), (candidate["nextHealingSourceOrdinal"]!.GetValue<int>(), candidate["nextHealingDecisionOrdinal"]!.GetValue<int>()));
        var row = Assert.Single(candidate["strainComparisons"]!.AsArray())!;
        Assert.Equal((1, 1, 2), (row["ordinal"]!.GetValue<int>(), row["instanceComparisonOrdinal"]!.GetValue<int>(),
            candidate["nextStrainComparisonOrdinal"]!.GetValue<int>()));
        Assert.Equal(instanceId, Text(row["instanceId"])); Assert.True(JsonNode.DeepEquals(inputs.Source, row["witness"]));
        Assert.DoesNotContain(candidate["sources"]!.AsArray(), item => Text(item!["witness"]!["sourceId"]) == Text(inputs.Source["sourceId"]));
        Assert.DoesNotContain(candidate["decisions"]!.AsArray(), item => Text(item!["sourceId"]) == Text(inputs.Source["sourceId"]));
    }
}
