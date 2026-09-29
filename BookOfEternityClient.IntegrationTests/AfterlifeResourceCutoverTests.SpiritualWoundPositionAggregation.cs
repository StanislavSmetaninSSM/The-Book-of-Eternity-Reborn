using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Sums two real wounds from distinct accepted conflict instances on one acting guardian before clamping the position rank.
    /// </summary>
    /// <returns>
    /// A task completing after genuine prior publication and later insertion prove two distinct live components and a three-step total clamped to rank two in the actual later exchange.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualGeneration_PositionSumsPriorAndCurrentConflictWoundsForSameActor()
    {
        var previous = await PublishPriorTerminalPositionWoundAsync();
        await using var context = await CreateCompleteConflictFrameContextAsync(
            signedDice: [15, 5, 11, 8], seedOriginalInputs: async original =>
            {
                await SeedOriginalIntakeBaselinesAsync(original);
                var fresh = Assert.IsType<JsonObject>(await original.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
                fresh["activeConflict"]!["conflictPosition"] = "opposition_advantaged";
                var started = AfterlifeSpiritualConflictState.ApplyUpdate(previous[AfterlifeSpiritualConflictState.StatePath],
                    new JsonObject { ["mode"] = "start", ["conflictState"] = fresh["activeConflict"]!.DeepClone() });
                Assert.Null(started["lastInvalidUpdate"]);
                foreach (var pair in previous.Where(pair => pair.Key != AfterlifeSpiritualConflictState.StatePath))
                    await original.WriteExactJsonAsync(pair.Key, pair.Value.ToJsonString());
                await original.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, started.ToJsonString());
            });
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43, currentRealm: "Chaos Sea",
            preGeneratedDices1d20: [15, 5, 11, 8], sessionId: "position_aggregate_session",
            requestId: "position_aggregate_43");
        await WritePositionBurdenExchangesAsync(context, playerWound: false, modifier: "exact");
        var candidate = await ReadProjectedSourceContinuationCandidateAsync(context);
        var active = candidate["activeConflict"]!.AsObject();
        var first = active["exchangeLog"]![0]!;
        var second = active["exchangeLog"]![1]!.AsObject();
        foreach (var exchange in active["exchangeLog"]!.AsArray())
        {
            exchange!["turnNumber"] = 43;
            exchange["before"]!["conflictPosition"] = "opposition_advantaged";
            exchange["after"]!["conflictPosition"] = "opposition_advantaged";
        }
        active["conflictPosition"] = "opposition_advantaged";
        first["exchangeId"] = "position_aggregate_first";
        second["exchangeId"] = "position_aggregate_second";
        first["diceAudit"]!["modifierBreakdown"]!["player"] = second["diceAudit"]!["modifierBreakdown"]!["player"]!.DeepClone();
        first["diceAudit"]!["playerTotal"] = 17;
        first["diceAudit"]!["margin"] = 12;
        second["diceAudit"]!["diceUsed"]![0]!["value"] = 11;
        second["diceAudit"]!["modifierBreakdown"]!["player"]![0]!["position"] = "player_dominant";
        second["diceAudit"]!["modifierBreakdown"]!["player"]![0]!["value"] = 4;
        second["diceAudit"]!["playerTotal"] = 15;
        second["diceAudit"]!["margin"] = 7;
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, candidate.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var physicalProfiles = await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath);
        var physicalHistory = await context.FileSystem.ReadFileBytesAsync(lease, WoundHistoryState.HistoryPath);
        var (capture, source) = await InsertFirstPositionActorWoundAsync(context, lease, player: false);
        using (capture)
        {
            var issues = new List<ValidationIssue>();
            var mechanics = Assert.IsType<AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext>(
                capture.PrepareSourceMechanics(source, issues));
            AssertNoConflictFrameErrors(issues);
            var rows = mechanics.Contributions.Where(row => row.Profile == "spiritual_position_burden" &&
                row.Operation == "pressure").ToArray();
            Assert.Equal(2, rows.Length);
            Assert.Equal(2, rows.Select(row => row.EffectId).Distinct(StringComparer.Ordinal).Count());
            Assert.All(rows, row =>
            {
                Assert.Equal("guardian_frame", row.Actor.TargetId);
                Assert.Equal("guardian", row.Actor.Kind);
                Assert.Equal("opposition", row.ResolvedSide);
            });
            Assert.Equal(new[] { 1, 2 }, rows.Select(row => row.Magnitude.GetInt32()).Order());
            Assert.Equal(3, rows.Sum(row => row.Magnitude.GetInt32()));
            var view = Assert.IsType<WoundOperationBeforeData>(mechanics.ReadCurrentWoundView(issues));
            AssertNoConflictFrameErrors(issues);
            var effects = EffectCarrierCatalog.Build(view.EffectCarriers!);
            var woundIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                Assert.True(effects.TryResolveOne(row.EffectId, out var effect));
                woundIds.Add(effect.Effect["source"]!["sourceId"]!.GetValue<string>());
            }
            Assert.Equal(2, woundIds.Count);
            Assert.Contains(previous[SpiritualWoundOpportunityReceiptState.StatePath]["decisions"]![0]!["woundId"]!.GetValue<string>(), woundIds);
            Assert.Equal(2, ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, active, second));
            var shifted = second.DeepClone().AsObject();
            shifted["before"]!["conflictPosition"] = "contested";
            Assert.Equal(2, ValidationService.SpiritualWoundSourceSession.ReadCurrentEffectivePosition(mechanics, active, shifted));
            var next = await capture.AdvanceNextResourceExchangeAsync(lease);
            AssertNoConflictFrameErrors(next.Issues);
            Assert.Equal(new int?[] { 1, 2 }, source.ReadAdmittedExchangeMechanics(capture)
                .Select(row => row.EffectivePositionRank));
            Assert.All(source.ReadAdmittedExchangeMechanics(capture), row =>
            {
                var exchange = JsonNode.Parse(row.ExchangeJson)!;
                Assert.Equal("opposition_advantaged", exchange["before"]!["conflictPosition"]!.GetValue<string>());
                Assert.Equal("opposition_advantaged", exchange["after"]!["conflictPosition"]!.GetValue<string>());
            });
            Assert.Equal(physicalProfiles, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeEntityProfileState.StatePath));
            Assert.Equal(physicalHistory, await context.FileSystem.ReadFileBytesAsync(lease, WoundHistoryState.HistoryPath));
        }
    }

    /// <summary>
    /// Captures the complete terminal draft and its narrative before the first checkpoint, publishes the real position wound,
    /// and returns detached canonical wound, effect and closure evidence for a later signed fixture.
    /// </summary>
    /// <returns>
    /// Actual common-publication images; these are baseline data and do not retain a private execution owner.
    /// </returns>
    private static async Task<IReadOnlyDictionary<string, JsonObject>> PublishPriorTerminalPositionWoundAsync()
    {
        await using var prior = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(prior);
        var terminalDraft = await ReadProjectedSourceContinuationCandidateAsync(prior);
        terminalDraft["activeConflict"]!["exchangeLog"]![0]!["after"]!["oppositionSideStrain"] = "overwhelmed";
        terminalDraft["activeConflict"]!["oppositionSideStrain"] = "overwhelmed";
        ResolveTerminalFixture(terminalDraft);
        await prior.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, terminalDraft.ToJsonString());
        const string scene = "Чужое давление надломило волю хранителя.";
        await prior.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject { ["response"] = scene }.ToJsonString());
        AssertNoConflictFrameErrors(await prior.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await prior.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        await using (var lease = await prior.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var captured = await prior.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            using (var warm = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
            {
                AssertNoConflictFrameErrors(await warm.BeginResourceExecutionAsync(lease));
                var first = await warm.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(first.Issues);
                var committed = await warm.CommitC2FirstTransportAsync(lease, first.Step!.Interval!);
                AssertNoConflictFrameErrors(committed.Issues);
                Assert.Equal("committed", committed.Disposition);
            }
            var opened = await prior.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var decision = JsonNode.Parse(OriginalSpiritualWoundDecision(offer.Offer!.OpportunityRef,
                "spiritual_position_burden", "pressure").GetRawText())!;
            decision["proposal"]!["severity"] = "III";
            var definitions = decision["proposal"]!["consequenceDefinitions"]!.AsArray();
            definitions[0]!["definition"]!["components"]![0]!["payload"]!["magnitude"] = 2;
            foreach (var operation in new[] { "guard", "counter" })
            {
                var additional = definitions[0]!.DeepClone();
                additional["definitionRef"] = "position_prior_" + operation;
                additional["definition"]!["definitionKey"] = "position_prior_" + operation;
                additional["definition"]!["stacking"]!["stackKey"] = "position_prior_stack_" + operation;
                additional["definition"]!["components"]![0]!["payload"]!["operation"] = operation;
                additional["definition"]!["components"]![0]!["payload"]!["magnitude"] = 1;
                definitions.Add(additional);
            }
            var completed = await offer.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(decision), scene);
            AssertNoConflictFrameErrors(completed.Issues);
            Assert.Equal("completed_unpublished", completed.Disposition);
            completed.Session?.Dispose();
        }
        AssertNoConflictFrameErrors(await prior.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            prior.FileSystem, prior.Normalizer, prior.Validator, await ReadSpiritualC4BackupsAsync(prior));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.NotNull(published.MechanicsPlan);
        var paths = new[] { AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
            WoundIdentityState.StatePath, WoundHistoryState.HistoryPath, EffectIdentityState.StatePath,
            SpiritualWoundOpportunityReceiptState.StatePath };
        var images = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var path in paths)
            images.Add(path, Assert.IsType<JsonObject>(await prior.ReadJsonAsync(path)));
        Assert.Null(images[AfterlifeSpiritualConflictState.StatePath]["activeConflict"]);
        Assert.Single(images[SpiritualWoundOpportunityReceiptState.StatePath]["closures"]!.AsArray());
        Assert.Single(images[SpiritualWoundOpportunityReceiptState.StatePath]["decisions"]!.AsArray());
        return images;
    }
}
