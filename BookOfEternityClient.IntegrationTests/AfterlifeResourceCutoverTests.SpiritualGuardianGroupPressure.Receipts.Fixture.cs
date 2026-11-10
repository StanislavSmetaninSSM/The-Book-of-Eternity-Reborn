using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Checks exact physical bytes/absence and generation without imposing a pre-registration authority oracle.</summary>
    /// <param name="context">This invocation's still-owned physical context.</param>
    /// <param name="before">Complete independently captured physical file inventory, including retained positive transport.</param>
    /// <param name="generation">Original physical generation bytes.</param>
    private static void AssertGuardianGroupReceiptExactFiles(ResourceMaterializationTestContext context,
        IReadOnlyDictionary<string, byte[]> before, byte[] generation)
    {
        var after = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        Assert.Equal(before.Keys.OrderBy(path => path, StringComparer.Ordinal), after.Keys.OrderBy(path => path, StringComparer.Ordinal));
        foreach (var (path, bytes) in before) Assert.True(bytes.SequenceEqual(after[path]), path);
        Assert.Equal(generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
    }

    /// <summary>Times the existing failure-preserving stages and reports every retained cleanup failure without replacing it.</summary>
    /// <param name="name">Exact independent fixture identity.</param>
    /// <param name="phase">Named owned stage, not an execution-success claim.</param>
    /// <param name="stages">Existing fixed cleanup callbacks for the resources this stage actually owns.</param>
    /// <returns>Completion only after all eligible cleanup stages have been attempted.</returns>
    private async Task ExecuteGuardianGroupReceiptStagesAsync(string name, string phase, PressureGroupFixtureStages stages)
    {
        var started = Stopwatch.GetTimestamp();
        Exception? primary = null;
        try { await ExecutePressureGroupFixtureStagesAsync(stages); }
        catch (Exception error)
        {
            primary = error;
            try
            {
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_stage_failure", name,
                    stage = phase, error = error.ToString(), cleanup = error.Data.Cast<System.Collections.DictionaryEntry>()
                        .Select(row => new { key = row.Key?.ToString(), value = row.Value?.ToString() }).ToArray() }));
            }
            catch (Exception diagnosticError) { RetainPressureFixtureFailure(primary, "receipt_stage_diagnostics", diagnosticError); }
            throw;
        }
        finally
        {
            try { WritePressureOwnerTiming("guardian_group_receipt_" + phase, name, started); }
            catch (Exception timingError) when (primary is not null) { RetainPressureFixtureFailure(primary, "receipt_stage_timing", timingError); }
        }
    }

    /// <summary>Builds a complete independent six-field expected proof using canonical owner encoding and every pre-sign parent.</summary>
    /// <param name="setup">Independent signed mixed N2 fixture, not a source-owner capability.</param>
    /// <param name="raw">Exact post-sign authored ordinary bytes, including BOM and whitespace.</param>
    /// <returns>Expected v7 DATA for complete semantic comparison against the owner source.</returns>
    private JsonObject CreateGuardianGroupReceiptExpectedProof(GuardianAttributionJourney setup, byte[] raw)
    {
        Assert.Equal(GuardianGroupPressureCase.MixedPair, setup.GroupContour);
        Assert.Equal(new[] { 3, 4 }, setup.Retained.Parents.Select(parent => parent["severity"]!["rank"]!.GetValue<int>()));
        var input = new JsonObject { ["path"] = AfterlifeSpiritualConflictState.StatePath, ["existed"] = true,
            ["contentFingerprint"] = new CanonicalBeforeImage(true, raw).Fingerprint, ["exchangePointer"] = "/activeConflict/exchangeLog/0" };
        var proof = SpiritualGuardianGroupPressureProofData.CreateProof(setup.Retained.Parents, setup.Ordinary, input,
            GuardianAttributionOrdinaryCalculation());
        proof["ordinaryExchangeJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(setup.Ordinary)));
        for (var index = 0; index < setup.Retained.Parents.Count; index++)
        {
            var parsed = WoundMaterializationContract.Parse(setup.Retained.Parents[index].ToJsonString(), "guardian.group.receipt.expected_parent");
            RequireGuardianJourneyIssues(setup.Name, "receipt_expected_parent", parsed.Issues); Assert.True(parsed.IsValid);
            proof["parents"]![index]!["parentWoundJsonBase64"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                WoundMaterializationContract.SerializeCanonical(parsed.Wound!)));
        }
        return proof;
    }

    /// <summary>Validates complete actualIV/ordinaryII witness operands and every group parent without using singular accessors.</summary>
    /// <param name="setup">Immutable original and independently authored calculation inputs.</param>
    /// <param name="witness">Actual owner-issued full witness from the first packet.</param>
    /// <param name="expectedProof">Independent complete expected six-field group proof.</param>
    private void AssertGuardianGroupReceiptWitness(GuardianAttributionJourney setup, JsonObject witness, JsonObject expectedProof)
    {
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_full_witness", setup.Name, witness, expectedProof }));
        Assert.Equal(GuardianGroupOwnerActualCalculation(), SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain));
        Assert.NotNull(strain); Assert.Equal(7, strain.Version);
        Assert.Equal(GuardianAttributionOrdinaryCalculation(), strain.OrdinaryCalculation);
        Assert.Equal(GuardianGroupOwnerActualCalculation(), strain.ActualCalculation);
        Assert.Equal(new[] { "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64", "originalInput", "parents", "version" },
            witness["strainPreimage"]!.AsObject().Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.True(JsonNode.DeepEquals(expectedProof, witness["strainPreimage"]));
        Assert.Equal(2, strain.Parents.Count);
        for (var index = 0; index < strain.Parents.Count; index++)
        {
            var parent = strain.Parents[index];
            Assert.Null(parent.ParentSourceId);
            Assert.True(JsonNode.DeepEquals(setup.Retained.Parents[index],
                JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent.ParentWound))));
            Assert.Equal(new[] { "componentId", "effectId", "parentWoundJsonBase64" },
                witness["strainPreimage"]!["parents"]![index]!.AsObject().Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        }
        Assert.Equal("/activeConflict/exchangeLog/0", strain.OriginalExchangePointer);
        Assert.Equal(new CanonicalBeforeImage(true, setup.Authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint, strain.OriginalInputFingerprint);
        Assert.True(JsonNode.DeepEquals(setup.Actual, strain.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(setup.Ordinary["after"], strain.ReadOrdinaryAfter(setup.Actual)));
        Assert.True(JsonNode.DeepEquals(setup.Actual, ReadGuardianAttributionJson(Convert.FromBase64String(witness["exchangeJsonBase64"]!.GetValue<string>()))));
        Assert.Equal(("activeConflict.exchangeLog[0]", "opposition", "pressure", 0, 0),
            (witness["coordinate"]!.GetValue<string>(), witness["affectedSide"]!.GetValue<string>(), witness["operation"]!.GetValue<string>(),
                witness["exchangeOrdinal"]!.GetValue<int>(), witness["sourceOrdinal"]!.GetValue<int>()));
        Assert.Equal(setup.ExchangeId, witness["exchangeId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" }, witness["actingActor"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian_frame" }, witness["affectedActor"]));
        Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), strain.ReadActualExchange()["spiritualWoundFailedAction"]));
        Assert.Null(witness["guaranteedSeverityRank"]); Assert.Null(witness["retraumaWoundId"]);
        var evidence = witness["turnEvidence"]!;
        Assert.Equal(("session_" + setup.Name, "request_" + setup.Name, 46, "chaos_sea"),
            (evidence["sessionId"]!.GetValue<string>(), evidence["requestId"]!.GetValue<string>(), evidence["turn"]!.GetValue<int>(), evidence["realm"]!.GetValue<string>()));
        Assert.Equal(new[] { 12, 4 }, evidence["acceptedD20Values"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Equal(new[] { 0, 1 }, witness["selectedDiceIndices"]!.AsArray().Select(row => row!.GetValue<int>()));
    }

    /// <summary>Checks the first packet's whole context/source projection and exact initial cursor without allocating another source.</summary>
    /// <param name="setup">Expected request, dice and original graph.</param>
    /// <param name="packet">Genuine C1 pending packet.</param>
    private static void AssertGuardianGroupReceiptFirstPacket(GuardianAttributionJourney setup, JsonObject packet)
    {
        var witness = Assert.Single(packet["sources"]!.AsArray())!;
        var evidence = witness["turnEvidence"]!;
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm", "originalSnapshotFingerprint", "bounds", "diceClaims" })
            Assert.True(JsonNode.DeepEquals(packet[field], evidence[field]), field);
        Assert.Equal((1, 2, 2), (packet["bounds"]!["exchangeCount"]!.GetValue<int>(),
            packet["bounds"]!["sourceSlotCount"]!.GetValue<int>(), packet["bounds"]!["diceCount"]!.GetValue<int>()));
        Assert.Equal((1, 0, 0), (packet["cursor"]!["exchangeOrdinal"]!.GetValue<int>(),
            packet["cursor"]!["waveOrdinal"]!.GetValue<int>(), packet["cursor"]!["nextSourceOrdinal"]!.GetValue<int>()));
        Assert.Empty(packet["stagedDecisions"]!.AsArray());
        Assert.Equal(new[] { 0, 1 }, packet["diceClaims"]!.AsArray().Select(row => row!["sourceIndex"]!.GetValue<int>()));
        Assert.Equal(setup.Dice, packet["diceClaims"]!.AsArray().Select(row => row!["value"]!.GetValue<int>()));
    }

    /// <summary>Requires a single none cursor advance while retaining every first-packet source and original-turn field.</summary>
    /// <param name="first">Independently retained first packet DATA, never a live capability.</param>
    /// <param name="completed">Actual physical completed pending packet after one submission.</param>
    /// <param name="opportunityRef">Exact genuine offered opportunity.</param>
    private static void AssertGuardianGroupReceiptNonePacket(JsonObject first, JsonObject completed, string opportunityRef)
    {
        foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn", "realm", "originalSnapshotFingerprint", "bounds", "diceClaims", "sources", "beforeImages" })
            Assert.True(JsonNode.DeepEquals(first[field], completed[field]), field);
        Assert.Equal((1, 0, 1), (completed["cursor"]!["exchangeOrdinal"]!.GetValue<int>(),
            completed["cursor"]!["waveOrdinal"]!.GetValue<int>(), completed["cursor"]!["nextSourceOrdinal"]!.GetValue<int>()));
        var staged = Assert.Single(completed["stagedDecisions"]!.AsArray())!;
        var witness = Assert.Single(completed["sources"]!.AsArray())!;
        Assert.Equal((opportunityRef, witness["sourceId"]!.GetValue<string>(), "none"),
            (staged["opportunityRef"]!.GetValue<string>(), staged["sourceId"]!.GetValue<string>(), staged["decision"]!.GetValue<string>()));
        foreach (var field in new[] { "selectedSeverityRank", "woundDraftBase64", "woundDraftFingerprint" }) Assert.Null(staged[field]);
    }

    /// <summary>Reads immutable original History once and logs every parent anchor before any C3 failure can mask them.</summary>
    /// <param name="setup">Genuine per-case original History and all independently authored signed parents.</param>
    /// <returns>Parsed immutable History DATA for later detached receipt joins, never live permission.</returns>
    private WoundHistoryState ReadGuardianGroupReceiptOriginalHistory(GuardianAttributionJourney setup)
    {
        var history = WoundHistoryState.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(setup.Origin.Tracked[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        RequireGuardianJourneyIssues(setup.Name, "receipt_history_parse", history.Issues); Assert.True(history.IsValid);
        foreach (var parent in setup.Retained.Parents)
        {
            var envelope = WoundMaterializationContract.Parse(parent.ToJsonString(), "guardian.group.receipt.history_parent");
            RequireGuardianJourneyIssues(setup.Name, "receipt_history_parent", envelope.Issues); Assert.True(envelope.IsValid);
            var wound = envelope.Wound!;
            Assert.True(history.State!.TryResolveExactTransition(wound.LastTransition.TransitionId, out var anchor)); Assert.NotNull(anchor);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_every_parent_anchor", setup.Name,
                parent, parentSemanticFingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound), anchor }));
            Assert.Equal((wound.WoundId, wound.LastTransition.TransitionId, wound.LastTransition.Kind, wound.LastTransition.Ordinal, wound.LastTransition.Turn, false),
                (anchor.WoundId, anchor.TransitionId, anchor.Kind, anchor.WoundTransitionOrdinal, anchor.Turn, anchor.Terminal));
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(wound), anchor.AfterFingerprint);
        }
        return history.State!;
    }

    /// <summary>Requires the exact N2 ancestry prefix, one new instance/source/none and typed append/replay, accepted-log and all-parent History joins.</summary>
    /// <param name="setup">Independently signed original receipt, History and complete parents.</param>
    /// <param name="receipt">Actual detached authenticated C3 receipt after-image.</param>
    /// <param name="witness">Original full first-packet source to match against every durable copy.</param>
    /// <param name="history">Already parsed immutable original History, with every parent anchor independently checked before C3.</param>
    private void AssertGuardianGroupReceiptAppendAndReplay(GuardianAttributionJourney setup, JsonObject receipt, JsonObject witness, WoundHistoryState history)
    {
        var before = JsonNode.Parse(setup.Origin.ReceiptJson)!.AsObject();
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_append_operands", setup.Name, before, receipt, witness }));
        Assert.Equal(1, receipt["schemaVersion"]!.GetValue<int>());
        foreach (var field in new[] { "instances", "sources", "decisions", "closures" })
        {
            var oldRows = before[field]!.AsArray(); var newRows = receipt[field]!.AsArray();
            Assert.Equal(2, oldRows.Count); Assert.Equal(oldRows.Count + (field == "closures" ? 0 : 1), newRows.Count);
            for (var index = 0; index < oldRows.Count; index++) Assert.True(JsonNode.DeepEquals(oldRows[index], newRows[index]), field);
        }
        foreach (var field in new[] { "nextInstanceOrdinal", "nextSourceOrdinal", "nextDecisionOrdinal" })
            Assert.Equal(before[field]!.GetValue<int>() + 1, receipt[field]!.GetValue<int>());
        Assert.Equal(before["nextClosureOrdinal"]!.GetValue<int>(), receipt["nextClosureOrdinal"]!.GetValue<int>());
        var sourceIndex = before["sources"]!.AsArray().Count;
        var decisionIndex = before["decisions"]!.AsArray().Count;
        var source = receipt["sources"]![sourceIndex]!;
        var decision = receipt["decisions"]![decisionIndex]!;
        Assert.True(JsonNode.DeepEquals(witness, source["witness"]));
        Assert.True(JsonNode.DeepEquals(witness, decision["sourceWitness"]));
        Assert.Equal(("none", witness["sourceId"]!.GetValue<string>()),
            (decision["decision"]!.GetValue<string>(), decision["sourceId"]!.GetValue<string>()));
        Assert.Null(decision["woundId"]); Assert.Null(decision["selectedSeverityRank"]); Assert.Null(decision["transitionId"]);
        var prior = SpiritualWoundOpportunityReceiptState.Parse(setup.Origin.ReceiptJson, SpiritualWoundOpportunityReceiptState.StatePath);
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        RequireGuardianJourneyIssues(setup.Name, "receipt_before_parse", prior.Issues); Assert.True(prior.IsValid);
        RequireGuardianJourneyIssues(setup.Name, "receipt_after_parse", parsed.Issues); Assert.True(parsed.IsValid);
        var ledger = parsed.State!;
        Assert.True(ledger.HasRetainedPressureComparisons); Assert.True(ledger.HasExactStrainHistory(history));
        foreach (var turn in new[] { 46, 47 })
            Assert.Equal(SpiritualWoundOpportunityReceiptState.RetainedPressureHistoryMatch.Exact,
                ledger.MatchRetainedPressureHistory("chaos_sea", setup.ActualRoot["activeConflict"]!.AsObject(), turn));
        Assert.True(JsonNode.DeepEquals(setup.Ordinary["after"], ledger.ReadRetainedStrainOrdinaryAfter("chaos_sea",
            setup.ActualRoot["activeConflict"]!["conflictId"]!.GetValue<string>(), setup.Actual, allowHistoricalSummaryDrift: false)));
        var append = SpiritualWoundOpportunityReceiptState.PlanAppend(prior.State!, ledger);
        Assert.Equal("appended", append.Disposition); Assert.NotNull(append.After);
        var independentReplay = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        RequireGuardianJourneyIssues(setup.Name, "receipt_independent_replay_parse", independentReplay.Issues); Assert.True(independentReplay.IsValid);
        var replay = SpiritualWoundOpportunityReceiptState.PlanAppend(append.After!, independentReplay.State!);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_receipt_typed_append_replay", setup.Name,
            append = append.Disposition, replay = replay.Disposition,
            replayReceipt = replay.After is null ? null : SpiritualWoundOpportunityReceiptState.SerializeCanonical(replay.After) }));
        Assert.Equal("exact_replay", replay.Disposition); Assert.NotNull(replay.After);
        Assert.True(JsonNode.DeepEquals(receipt, JsonNode.Parse(SpiritualWoundOpportunityReceiptState.SerializeCanonical(replay.After!))));
    }
}
