using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Publishes two genuine player III pressure parents in distinct terminal instances,
    /// recovers the first parent on the safe start43 and authenticates complete two-parent DATA at45.
    /// This does not qualify live group ownership, ordinary isolation or actual strain admission.
    /// </summary>
    [Fact]
    public async Task SpiritualPressurePair_TwoPublishedParentsRemainValidAcrossInstances()
    {
        var prepared = await PrepareSpiritualPressurePairFixtureAsync();
        Assert.False(Directory.Exists(prepared.OriginalRoot));
        Assert.NotEqual(prepared.FirstWoundId, prepared.SecondWoundId);
        var manifest = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(prepared.Tree.ReadFileBytes(
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!;
        Assert.Equal(45, manifest["turnNumber"]!.GetValue<int>());
        foreach (var path in PressureOrdinarySignedPaths)
            Assert.Equal(prepared.Tree.ReadFileBytes(path), prepared.Tree.ReadFileBytes(
                manifest["files"]![path]!.GetValue<string>()));
        // The tree is private to this test. No materialized copies or shared owners are retained.
    }

    /// <summary>
    /// Prepares one independently owned fixture and freezes only its completed immutable game-session bytes.
    /// Proposals and lawful capacities are fixed before its first signature; cleanup is verified even on failure.
    /// </summary>
    /// <returns>Detached template DATA after disposal of its only original filesystem and owners.</returns>
    private async Task<PressurePairFixtureTemplate> PrepareSpiritualPressurePairFixtureAsync()
    {
        var firstProposal = CreatePressurePairParentProposal("pressure_pair_parent_one");
        var secondProposal = CreatePressurePairParentProposal("pressure_pair_parent_two");
        var started = Stopwatch.GetTimestamp();
        ResourceMaterializationTestContext? context = null;
        try
        {
            context = await CreateCompleteConflictFrameContextAsync(
                seedOriginalInputs: SeedOriginalIntakeBaselinesAsync,
                playerCurrent: 10m, spiritFocusTier: 3,
                initializeContext: allocated =>
                {
                    context = allocated; // Retain ownership before bootstrap, seeding or signing can fail.
                    return Task.CompletedTask;
                },
                captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(
                    turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [5, 15],
                    sessionId: "spiritual_pressure_pair_fixture", requestId: "pressure_pair_birth_42"));
            WritePressureOwnerTiming("pair_initial_signed_fixture", "fixture", started);
            var definitionBytes = Assert.IsType<byte[]>(await context.FileSystem.ReadFileBytesAsync(
                ResourceMaterializationContract.DefinitionsPath));
            var definitions = ResourceDefinitionCatalog.ParseCanonical(
                System.Text.Encoding.UTF8.GetString(definitionBytes), allowMissingPristine: false);
            Assert.True(definitions.IsValid, string.Join("\n", definitions.Issues));
            var catalog = Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog);
            var seed = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!
                .DeepClone().AsObject();
            var firstOwner = seed["resourceOwnerBindings"]!["opposition"]!["resourceOwnerId"]!.GetValue<string>();

            var firstBirth = await PublishPressurePairTerminalParentAsync(context, catalog, firstProposal,
                turn: 42, operation: "pressure", playerBefore: 10m, playerAfter: 7m, playerDie: 5,
                position: "contested", expectedParentCount: 1);
            var first = firstBirth.Parent;
            var firstReceipt = firstBirth.Parents.ReceiptRoot;

            var secondOwner = await StartPressurePairInstanceAsync(context, catalog, seed, firstBirth.Parents,
                "pressure_pair_second", "player_advantaged", turn: 43, playerCurrent: 7m, retiredOwners: [firstOwner]);
            Assert.NotEqual(firstOwner, secondOwner);
            var recovered = await ReadPressurePairParentsAsync(context, expectedCount: 1);
            var recoveredFirst = Assert.Single(recovered.Catalog.Occurrences).Wound;
            Assert.Equal((3, 1L), (recoveredFirst.Severity.Rank, recoveredFirst.Recovery.CurrentStepProgress));
            first = first with { CanonicalJson = WoundMaterializationContract.SerializeCanonical(recoveredFirst) };
            await context.CaptureValidatedPendingSnapshotAsync(turn: 44, currentRealm: "Chaos Sea",
                preGeneratedDices1d20: [3, 15], sessionId: "spiritual_pressure_pair_fixture",
                requestId: "pressure_pair_birth_44");
            var secondBirth = await PublishPressurePairTerminalParentAsync(context, catalog, secondProposal,
                turn: 44, operation: "binding", playerBefore: 7m, playerAfter: 3m, playerDie: 3,
                position: "player_advantaged", expectedParentCount: 2);
            var second = secondBirth.Parent;
            Assert.NotEqual(first.WoundId, second.WoundId);
            var pair = secondBirth.Parents;
            Assert.Equal(first.CanonicalJson, WoundMaterializationContract.SerializeCanonical(
                pair.Catalog.Occurrences.Single(row => row.WoundId == first.WoundId).Wound));
            Assert.True(JsonNode.DeepEquals(firstReceipt["sources"]![0], pair.ReceiptRoot["sources"]![0]));
            Assert.True(JsonNode.DeepEquals(firstReceipt["decisions"]![0], pair.ReceiptRoot["decisions"]![0]));
            Assert.True(JsonNode.DeepEquals(firstReceipt["instances"]![0], pair.ReceiptRoot["instances"]![0]));
            Assert.True(JsonNode.DeepEquals(firstReceipt["closures"]![0], pair.ReceiptRoot["closures"]![0]));
            AssertPressurePairWoundHistoryPrefix(recovered.History, pair.History);
            var beforeSignature = await CaptureExistingStrainCanonicalAsync(context);
            beforeSignature[AfterlifeSpiritualConflictState.StatePath] =
                await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath);
            beforeSignature[ResourceMaterializationContract.DefinitionsPath] =
                await context.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.DefinitionsPath);
            var nextControl = await new ProgressionScheduleService(context.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync();
            JsonObject? nextProjection;
            await using (var projectionLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                nextProjection = await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(context.FileSystem,
                    projectionLease, new("spiritual_pressure_pair_fixture", "pressure_pair_target_45", 45, nextControl));
            Assert.NotNull(nextProjection);
            await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = "spiritual_pressure_pair_fixture", RequestId = "pressure_pair_target_45", TurnNumber = 45,
                CurrentRealm = "Chaos Sea", PlayerAction = "Наблюдать после завершения второго противостояния.",
                ProgressionControl = nextControl
            });
            AssertNoConflictFrameErrors(await ValidatePublishedConflictPhaseAsync(context));
            var observationStarted = Stopwatch.GetTimestamp();
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            // Common publication may normalize JSON bytes; it cannot change any definition DATA.
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(definitionBytes)),
                JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath))!)));
            var requiredPaths = PressureOrdinarySignedPaths.Append(ResourceMaterializationContract.DefinitionsPath).ToArray();
            var optionalPaths = new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
                WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath };
            var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(requiredPaths, optionalPaths));
            Assert.True(signed.Success, string.Join("\n", signed.Issues));
            var authority = Assert.IsType<PendingTurnSnapshotReadAuthority>(signed.Snapshot);
            Assert.Equal((45, "chaos_sea", "pressure_pair_target_45"),
                (authority.TurnNumber, authority.Realm, authority.RequestId));
            var request = (await context.ReadJsonAsync(LiveTurnPreparationService.TurnRequestPath))!;
            Assert.Equal(request["preGeneratedDices1d20"]!.AsArray().Select(row => row!.GetValue<int>()),
                authority.AcceptedD20EventValues);
            foreach (var path in requiredPaths)
                Assert.Equal(await context.FileSystem.ReadFileBytesAsync(lease, path), authority.ReadRequiredBytes(path));
            foreach (var path in optionalPaths)
            {
                var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
                if (bytes is null) Assert.Contains(path, authority.AbsentLogicalPaths);
                else Assert.Equal(bytes, authority.ReadRequiredBytes(path));
            }
            foreach (var (path, bytes) in beforeSignature)
                Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));

            var observedFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var finalParents = await ReadPressurePairParentsAsync(context, expectedCount: 2, lease: lease);
            AssertPressurePairReceiptJoins(finalParents, first, second);
            await AssertPressurePairEffectLineageAsync(context, lease, finalParents);
            var resources = await ReadPressurePairResourcesAsync(context, lease, catalog);
            Assert.Equal((3m, 10m), PressurePairPlayerBalance(resources.State));
            AssertPressurePairRetiredOwners(resources, [firstOwner, secondOwner]);
            Assert.Null((await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]);
            AssertExistingPressureInputInert(context, lease, observedFiles, generation);
            foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                         SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
            WritePressureOwnerTiming("pair_signed_two_parent_data_observation", "fixture", observationStarted);
            var frozen = PreparedFixtureTree.Capture(context.FileSystem.GameSessionPath);
            return new(frozen, context.RootPath, first.WoundId, second.WoundId);
        }
        finally
        {
            if (context is not null)
            {
                var cleanupStarted = Stopwatch.GetTimestamp();
                await context.DisposeAsync();
                WritePressureOwnerTiming("pair_owned_cleanup", context.RootPath, cleanupStarted);
                Assert.False(Directory.Exists(context.RootPath));
            }
        }
    }

    /// <summary>Fixes each III proposal before signing, retaining all three independently applicable sibling components.</summary>
    /// <param name="definitionKey">Exact source-local key fixed for this parent's real later decision.</param>
    /// <returns>Detached proposal, not a source, opportunity or accepted decision.</returns>
    private static JsonObject CreatePressurePairParentProposal(string definitionKey)
    {
        var proposal = CreateSpiritualStrainThirdDegreeDecision("prepared_pair_proposal")["proposal"]!.DeepClone().AsObject();
        var consequence = proposal["consequenceDefinitions"]![0]!;
        consequence["definitionRef"] = definitionKey;
        consequence["definition"]!["definitionKey"] = definitionKey;
        consequence["definition"]!["stacking"]!["stackKey"] = "stack_" + definitionKey;
        consequence["definition"]!["components"]![1]!["payload"]!["operation"] = "counter";
        consequence["root"]!["slots"]![1]!["readableSummary"] = "Контрприём душе даётся труднее.";
        Assert.Equal(1, consequence["definition"]!["stacking"]!["maxStacks"]!.GetValue<int>());
        return proposal;
    }

    /// <summary>Executes one exact losing terminal exchange through actual resource admission, C2 and common C4.</summary>
    /// <param name="context">This test's independently owned context.</param>
    /// <param name="definitions">Unchanged parsed original resource definitions.</param>
    /// <param name="proposal">III proposal configured before the first signature.</param>
    /// <param name="turn">Real signed turn owning the exchange.</param>
    /// <param name="operation">Player operation, pressure for the first birth and binding for the second.</param>
    /// <param name="playerBefore">Cumulative player's exact signed balance.</param>
    /// <param name="playerAfter">Balance after the ordinary standard-art cost.</param>
    /// <param name="playerDie">Exact first signed die, before the lawful position modifier.</param>
    /// <param name="position">Signed starting position, unchanged by the failed exchange.</param>
    /// <param name="expectedParentCount">Number of real published parents after C4.</param>
    /// <returns>Immutable original parent identity and the same publication's already validated complete parent DATA.</returns>
    private async Task<(PressurePairParent Parent, PressurePairParents Parents)> PublishPressurePairTerminalParentAsync(
        ResourceMaterializationTestContext context, ResourceDefinitionCatalog definitions, JsonObject proposal,
        int turn, string operation, decimal playerBefore, decimal playerAfter, int playerDie,
        string position, int expectedParentCount)
    {
        var started = Stopwatch.GetTimestamp();
        var root = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        var active = root["activeConflict"]!.AsObject();
        var before = new JsonObject { ["playerSideStrain"] = "clear", ["oppositionSideStrain"] = "clear",
            ["conflictPosition"] = position };
        if (active["controlState"] is { } control) before["controlState"] = control.DeepClone();
        var after = before.DeepClone().AsObject();
        after["playerSideStrain"] = "overwhelmed";
        var modifier = position == "player_advantaged" ? 2 : 0;
        Assert.Equal(-10, playerDie + modifier - 15);
        Assert.True(AfterlifeActionCostRules.TryGetDefinition(operation, out var costDefinition));
        var playerCost = CostAudit(operation, playerBefore - playerAfter, playerBefore, playerAfter);
        playerCost["baseCost"] = costDefinition.BaseCost;
        playerCost["minCost"] = costDefinition.MinCost;
        playerCost["max"] = 10m;
        Assert.Equal(playerBefore - playerAfter, (decimal)AfterlifeActionCostRules.ResolveStandardEffectiveCost(costDefinition, 0));
        var dice = new JsonObject
        {
            ["formulaVersion"] = "afterlife_spiritual_conflict_v1",
            ["diceSource"] = "input/turn_request.json.preGeneratedDices1d20",
            ["diceUsed"] = new JsonArray(
                new JsonObject { ["side"] = "player", ["sourceIndex"] = 0, ["sides"] = 20, ["value"] = playerDie },
                new JsonObject { ["side"] = "opposition", ["sourceIndex"] = 1, ["sides"] = 20, ["value"] = 15 }),
            ["playerTotal"] = playerDie + modifier, ["oppositionTotal"] = 15,
            ["margin"] = -10, ["outcomeBand"] = "decisive_opposition_success",
            ["modifierBreakdown"] = new JsonObject
            {
                ["player"] = modifier == 0 ? new JsonArray() : new JsonArray(new JsonObject
                { ["modifierType"] = "conflict_position", ["source"] = "conflictPosition", ["position"] = position, ["value"] = 2 }),
                ["opposition"] = new JsonArray()
            }
        };
        var exchange = new JsonObject
        {
            ["exchangeId"] = "pressure_pair_birth_exchange_" + turn, ["turnNumber"] = turn,
            ["operationType"] = operation, ["outcome"] = "setback", ["before"] = before, ["after"] = after,
            ["incomingAction"] = new JsonObject { ["operationType"] = "pressure", ["actorType"] = "guardian",
                ["actorId"] = "guardian_frame", ["summary"] = "Хранитель оказывает духовное давление." },
            ["matchupAudit"] = new JsonObject { ["playerOperation"] = operation, ["oppositionOperation"] = "pressure",
                ["primaryResolutionLane"] = operation, ["riskProfile"] = operation == "binding" ? "control_leverage" : "offensive_pressure",
                ["matchupRationale"] = operation == "binding"
                    ? "Душа пытается наложить оковы с исходного преимущества, но встречное давление срывает попытку."
                    : "Более сильное встречное давление Хранителя надламывает волю души." },
            ["actionCostAudit"] = new JsonObject { ["player"] = playerCost,
                ["opposition"] = CostAudit("pressure", 3m, 6m, 3m) }, ["diceAudit"] = dice
        };
        Assert.True(JsonNode.DeepEquals(before["controlState"], after["controlState"]));
        if (operation == "binding")
        {
            Assert.Equal("player_advantaged", before["conflictPosition"]!.GetValue<string>());
            Assert.Equal(2, Assert.Single(dice["modifierBreakdown"]!["player"]!.AsArray())!["value"]!.GetValue<int>());
            Assert.Equal(4m, playerBefore - playerAfter);
        }
        var projected = AfterlifeSpiritualConflictState.ApplyUpdate(root,
            new JsonObject { ["mode"] = "exchange", ["exchange"] = exchange.DeepClone() });
        Assert.Null(projected["lastInvalidUpdate"]);
        var terminal = AfterlifeSpiritualConflictState.ApplyUpdate(projected, new JsonObject
        {
            ["mode"] = "resolve", ["resolution"] = new JsonObject
            {
                ["conflictId"] = active["conflictId"]!.DeepClone(), ["operationType"] = operation,
                ["resolvedAtTurn"] = turn, ["resolvedAtUtc"] = "2026-01-01T00:00:00Z",
                ["guardianId"] = "guardian_frame", ["playerOutcome"] = "lost", ["resolutionKind"] = "player_loss",
                ["diceAudit"] = dice.DeepClone(), ["terminalExchange"] = exchange.DeepClone(),
                ["summary"] = "Душа уступила встречному давлению; Хранитель завершил это противостояние."
            }
        });
        Assert.Null(terminal["lastInvalidUpdate"]);
        Assert.Null(terminal["activeConflict"]);
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, terminal.ToJsonString());
        const string scene = "Чужое давление надломило волю души.";
        await context.WriteExactJsonAsync(ProjectionNarrativePath, new JsonObject
            { ["response"] = scene, ["timestamp"] = "2026-09-30T00:00:00Z" }.ToJsonString());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        PressurePairResources resourcesBefore;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            // The signed original still owns the live side here; the authored terminal draft does not change its ledger.
            resourcesBefore = ReadPressurePairResourceData(definitions,
                await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.StatePath),
                await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath));
            Assert.Equal((playerBefore, 10m), PressurePairPlayerBalance(resourcesBefore.State));
            var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
            AssertNoConflictFrameErrors(captured.Issues);
            using (var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture))
            {
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
                var beforeAdvanceCounts = capture.ReadExistingStrainReuseCounts();
                var next = await capture.AdvanceNextResourceExchangeAsync(lease);
                AssertNoConflictFrameErrors(next.Issues);
                var afterAdvanceCounts = capture.ReadExistingStrainReuseCounts();
                var closed = Assert.Single(capture.ReadClosedExchangeEvidence(lease));
                Assert.Equal(new[] { playerDie, 15 }, closed.DiceClaims.Select(row => row.Value));
                Assert.Equal(new[] { 0, 1 }, closed.DiceClaims.Select(row => row.SourceIndex));
                Assert.True(JsonNode.DeepEquals(exchange, JsonNode.Parse(closed.ExchangeJson)));
                var source = Assert.Single(closed.Sources);
                Assert.Equal(("player", "pressure", 20L, 3),
                    (source.AffectedSide, source.Operation, source.Calculation.TraumaPressure, source.Calculation.MaximumSeverityRank));
                Assert.Equal(("guardian:guardian_frame", "player_soul:player_soul"), (source.ActingActor, source.AffectedActor));
                Assert.Equal(SpiritualWoundOpportunityMath.Calculate(new(10, 0, 0, "clear", "overwhelmed", "hostile", 4)),
                    source.Calculation);
                Assert.Null(source.RetraumaWoundId);
                Assert.Null(source.GuaranteedSeverityRank);
                Assert.Null(source.StrainPreimageJson);
                Assert.Equal(0, beforeAdvanceCounts.OrdinaryEvaluations);
                Assert.Equal(0, afterAdvanceCounts.OrdinaryEvaluations);
                // This counts the general non-isolated source evaluation, not a group or pressure projection.
                Assert.Equal(beforeAdvanceCounts.ActualEvaluations + 1, afterAdvanceCounts.ActualEvaluations);
                var committed = await capture.CommitC2FirstTransportAsync(lease,
                    Assert.IsType<AcceptedMechanicsPlanner.SpiritualExchangeInterval>(next.Step?.Interval));
                AssertNoConflictFrameErrors(committed.Issues);
                Assert.Equal("committed", committed.Disposition);
            }
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            AssertNoConflictFrameErrors(opened.Issues);
            using var offer = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            Assert.Equal(3, offer.Offer!.MaximumSeverityRank);
            var selected = await offer.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new JsonObject
            {
                ["opportunityRef"] = offer.Offer.OpportunityRef, ["decision"] = "materialize",
                ["woundRef"] = "new_wound", ["proposal"] = proposal.DeepClone()
            }), scene);
            AssertNoConflictFrameErrors(selected.Issues);
            Assert.Equal("completed_unpublished", selected.Disposition);
            selected.Session?.Dispose();
        }
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, context.Validator, await ReadSpiritualC4BackupsAsync(context));
        AssertNoConflictFrameErrors(published.Issues);
        Assert.NotNull(published.MechanicsPlan);
        Assert.Null((await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var resources = await ReadPressurePairResourcesAsync(context, lease, definitions);
            var appended = AssertPressurePairResourceAppend(resourcesBefore.History, resources.History, 3);
            var playerSpend = Assert.Single(appended, row => row.Operation == ResourceTransitionOperation.Spend &&
                row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor);
            Assert.Equal((playerBefore, playerAfter, playerBefore - playerAfter),
                (playerSpend.BeforeState!.Current, playerSpend.AfterState!.Current, playerSpend.AppliedAmount));
            var sideSpend = Assert.Single(appended, row => row.Operation == ResourceTransitionOperation.Spend &&
                row.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
            Assert.Equal((6m, 3m, 3m), (sideSpend.BeforeState!.Current, sideSpend.AfterState!.Current, sideSpend.AppliedAmount));
            var retirement = Assert.Single(appended, row => row.Operation == ResourceTransitionOperation.Retire);
            Assert.Equal(sideSpend.Coordinate, retirement.Coordinate);
            Assert.Equal(3m, retirement.BeforeState!.Current);
            Assert.Null(retirement.AfterState);
            Assert.True(retirement.ExecutionSequence > playerSpend.ExecutionSequence && retirement.ExecutionSequence > sideSpend.ExecutionSequence);
            AssertPressurePairRetiredOwners(resources, [retirement.Coordinate.ResourceOwnerId]);
            Assert.Equal((playerAfter, 10m), PressurePairPlayerBalance(resources.State));
        }
        var parents = await ReadPressurePairParentsAsync(context, expectedParentCount);
        Assert.Equal(expectedParentCount, parents.ReceiptRoot["closures"]!.AsArray().Count);
        var created = Assert.Single(parents.History.Transitions, row => row.Kind == "create" && row.Turn == turn);
        var wound = parents.Catalog.Occurrences.Single(row => row.WoundId == created.WoundId).Wound;
        Assert.Equal(3, wound.Severity.Rank);
        WritePressureOwnerTiming("pair_terminal_birth_c2_c4", operation, started);
        return (new(wound.WoundId, WoundMaterializationContract.SerializeCanonical(wound), created.TransitionId), parents);
    }

    /// <summary>Publishes a separately signed ordinary start through the existing common plan, retaining verified retired owners and History.</summary>
    /// <param name="context">The same independent context retaining all accepted first/second parents.</param>
    /// <param name="definitions">Unchanged original resource definitions.</param>
    /// <param name="seed">Detached original membership, not a resource binding or current balance.</param>
    /// <param name="parents">Already validated actual parent DATA from the preceding terminal publication.</param>
    /// <param name="conflictId">New distinct display identity, used by the production owner composer.</param>
    /// <param name="position">Lawful initial tactical position fixed before the next signature.</param>
    /// <param name="turn">Separate signed accepted start turn, strictly between birth/observation turns.</param>
    /// <param name="playerCurrent">Persistent player balance that the start must leave unchanged.</param>
    /// <param name="retiredOwners">All prior side owners, which must remain absent and terminal.</param>
    /// <returns>The actual newly composed opposition owner ID.</returns>
    private async Task<string> StartPressurePairInstanceAsync(ResourceMaterializationTestContext context,
        ResourceDefinitionCatalog definitions, JsonObject seed, PressurePairParents parents, string conflictId, string position,
        int turn, decimal playerCurrent, IReadOnlyList<string> retiredOwners)
    {
        var started = Stopwatch.GetTimestamp();
        var preserved = await CaptureExistingStrainCanonicalAsync(context);
        var root = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
        Assert.Null(root["activeConflict"]);
        Assert.True(JsonNode.DeepEquals(parents.Profiles,
            JsonNode.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(
                Assert.IsType<byte[]>(preserved[AfterlifeEntityProfileState.StatePath])))));
        const string sessionId = "spiritual_pressure_pair_fixture";
        var requestId = "pressure_pair_start_" + turn;
        var control = await new ProgressionScheduleService(context.FileSystem, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync();
        JsonObject? projection;
        await using (var projectionLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
            projection = await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(context.FileSystem, projectionLease,
                new(sessionId, requestId, turn, control));
        Assert.NotNull(projection);
        await new LiveTurnPreparationService(context.FileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = sessionId, RequestId = requestId, TurnNumber = turn, CurrentRealm = "Chaos Sea",
            PlayerAction = "После спокойного восстановления объявить новое духовное противостояние без обменов.",
            ProgressionControl = control
        });
        await context.FileSystem.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, JsonSerializer.Serialize(new
        {
            progressionProcessingReport = new
            {
                sessionId, requestId, turnNumber = turn,
                worldCyclesProcessed = 0, factionCyclesProcessed = 0,
                chaosSeaCyclesProcessed = control.ChaosSeaCyclesExpectedThisTurn,
                guardianProjectCyclesProcessed = control.GuardianProjectCyclesExpectedThisTurn,
                residentAgencyCyclesProcessed = control.ResidentAgencyCyclesExpectedThisTurn,
                shiningAbodeCyclesProcessed = control.ShiningAbodeCyclesExpectedThisTurn,
                shiningFactionCyclesProcessed = control.ShiningFactionCyclesExpectedThisTurn,
                shiningTradeCyclesProcessed = control.ShiningTradeCyclesExpectedThisTurn,
                newLastChaosSeaSimulationOrdinal = control.NextChaosSeaTurnOrdinal,
                newLastGuardianProjectCycleOrdinal = control.NextGuardianProjectCycleOrdinal,
                newLastResidentAgencyCycleOrdinal = control.NextResidentAgencyCycleOrdinal,
                newLastShiningAbodeCycleOrdinal = control.NextShiningAbodeCycleOrdinal,
                newLastShiningFactionCycleOrdinal = control.NextShiningFactionCycleOrdinal,
                newLastShiningTradeCycleOrdinal = control.NextShiningTradeCycleOrdinal,
                afterlifeCatchupProcessed = false, afterlifeCatchupSummaryEventsProcessed = 0
            }
        }));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, PressureOrdinarySignedPaths);
        Assert.True(signed.Success, string.Join("\n", signed.Issues));
        var authority = Assert.IsType<PendingTurnSnapshotReadAuthority>(signed.Snapshot);
        Assert.Equal((turn, "pressure_pair_start_" + turn), (authority.TurnNumber, authority.RequestId));
        foreach (var path in PressureOrdinarySignedPaths)
            Assert.Equal(await context.FileSystem.ReadFileBytesAsync(lease, path), authority.ReadRequiredBytes(path));
        foreach (var (path, bytes) in preserved)
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        var before = await ReadPressurePairResourcesAsync(context, lease, definitions);
        Assert.Equal((playerCurrent, 10m), PressurePairPlayerBalance(before.State));
        AssertPressurePairRetiredOwners(before, retiredOwners);
        var next = seed.DeepClone().AsObject();
        next.Remove("resourceOwnerBindings");
        next.Remove("combatConditions");
        next["conflictId"] = conflictId;
        next["playerSideStrain"] = "clear";
        next["oppositionSideStrain"] = "clear";
        next["conflictPosition"] = position;
        next["exchangeLog"] = new JsonArray();
        next["oppositionSide"]!["resourceMaterialization"] = new JsonObject
        { ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "spiritual_action_points", ["maximum"] = 6 }) };
        // Keep the inactive signed parent and author only the clean-start response.
        // Recovery and the new owner must publish through the same accumulated plan.
        root[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            { ["mode"] = AfterlifeSpiritualConflictState.ModeStart, ["conflictState"] = next };
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
            System.Text.Encoding.UTF8.GetBytes(root.ToJsonString()));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
        AssertNoConflictFrameErrors(await context.Validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(context.FileSystem, lease, out var binding, out var result));
        Assert.Equal((turn, "pressure_pair_start_" + turn), (binding.Turn, binding.RequestId));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        var opposition = Assert.Single(plan.OwnerAuthority.Entries.Values,
            row => row.Key.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.True(opposition.SameTurn);
        foreach (var retired in retiredOwners)
            Assert.Contains(new ResourceOwnerKey("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide, retired),
                plan.OwnerAuthority.ExportHistoricalOwners());
        var planned = ReadPressurePairResourceData(definitions, plan.StateAfterImage.ToJsonString(), plan.HistoryAfterImage.ToJsonString());
        AssertPressurePairRetiredOwners(planned, retiredOwners);
        Assert.Equal((playerCurrent, 10m), PressurePairPlayerBalance(planned.State));
        Assert.Single(AssertPressurePairResourceAppend(before.History, planned.History, 1));
        Assert.Same(plan, await context.Normalizer.BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(new Dictionary<string, string>()));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync(lease));
        AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnCanonicalEffectMaterializationAsync(lease));
        Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(plan.EffectIdentityAfterImage, await context.ReadJsonAsync(EffectIdentityState.StatePath)));
        var finalCarrierImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var pair in plan.OwnerCompanionAfterImages.Concat(plan.EffectCarrierAfterImages)
                     .Concat(plan.WoundCarrierAfterImages))
            finalCarrierImages[pair.Key] = pair.Value;
        foreach (var pair in finalCarrierImages)
            Assert.True(JsonNode.DeepEquals(pair.Value, await context.ReadJsonAsync(pair.Key)), pair.Key);
        Assert.NotNull(plan.WoundIdentityAfterImage);
        Assert.NotNull(plan.WoundHistoryAfterImage);
        Assert.True(JsonNode.DeepEquals(plan.WoundIdentityAfterImage, await context.ReadJsonAsync(WoundIdentityState.StatePath)));
        Assert.True(JsonNode.DeepEquals(plan.WoundHistoryAfterImage, await context.ReadJsonAsync(WoundHistoryState.HistoryPath)));
        var after = await ReadPressurePairResourcesAsync(context, lease, definitions);
        Assert.Equal((playerCurrent, 10m), PressurePairPlayerBalance(after.State));
        var initialization = Assert.Single(AssertPressurePairResourceAppend(before.History, after.History, 1));
        Assert.Equal(ResourceTransitionOperation.Initialize, initialization.Operation);
        Assert.Equal(ResourceOwnerKind.AfterlifeConflictSide, initialization.Coordinate.OwnerKind);
        Assert.Equal(opposition.Key.ResourceOwnerId, initialization.Coordinate.ResourceOwnerId);
        Assert.Equal(turn, initialization.Turn);
        Assert.Null(initialization.BeforeState);
        Assert.Equal((6m, 6m), (initialization.AfterState!.Current, initialization.AfterState.Maximum));
        Assert.DoesNotContain(initialization.Coordinate.ResourceOwnerId, retiredOwners);
        AssertPressurePairRetiredOwners(after, retiredOwners);
        var actual = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!;
        Assert.False(actual.AsObject().ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        Assert.Equal(initialization.Coordinate.ResourceOwnerId,
            actual["activeConflict"]!["resourceOwnerBindings"]!["opposition"]!["resourceOwnerId"]!.GetValue<string>());
        Assert.Null(actual["activeConflict"]!["oppositionSide"]!["resourceMaterialization"]);
        Assert.True(JsonNode.DeepEquals(root["recentConflicts"], actual["recentConflicts"]));
        var currentParents = await ReadPressurePairParentsAsync(context, parents.Catalog.Occurrences.Count, lease);
        AssertPressurePairWoundHistoryPrefix(parents.History, currentParents.History);
        var tick = Assert.Single(currentParents.History.Transitions.Skip(parents.History.Transitions.Count));
        Assert.Equal(("recover", turn, Assert.Single(parents.Catalog.Occurrences).WoundId),
            (tick.Kind, tick.Turn, tick.WoundId));
        Assert.Null(tick.AttemptId);
        Assert.Equal("natural", Assert.IsType<SpiritualHealingPersistedResult>(tick.TransitionResult).Mode);
        var priorWound = Assert.Single(parents.Catalog.Occurrences).Wound;
        var currentWound = Assert.Single(currentParents.Catalog.Occurrences).Wound;
        Assert.Equal((3, 0L), (priorWound.Severity.Rank, priorWound.Recovery.CurrentStepProgress));
        Assert.Equal((3, 1L), (currentWound.Severity.Rank, currentWound.Recovery.CurrentStepProgress));
        var priorJson = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(priorWound))!.AsObject();
        var currentJson = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(currentWound))!.AsObject();
        // Only the source-issued recovery and last-transition fields may change at this III progress tick.
        priorJson.Remove("recovery");
        priorJson.Remove("lastTransition");
        currentJson.Remove("recovery");
        currentJson.Remove("lastTransition");
        Assert.True(JsonNode.DeepEquals(priorJson, currentJson));
        Assert.True(JsonNode.DeepEquals(parents.ReceiptRoot, currentParents.ReceiptRoot));
        Assert.Empty(actual["activeConflict"]!["exchangeLog"]!.AsArray());
        Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(control, sessionId,
            requestId, turn, path => context.FileSystem.ReadFileBytesAsync(lease, path),
            authority.SnapshotToken, expectedNaturalProjection: projection));
        var parentRoots = parents.Catalog.Occurrences.SelectMany(row => row.Wound.Consequences.OwnedEffectSources.RootBindings)
            .Select(row => row.EffectId).ToArray();
        AssertPressurePairStartIdentityAppend(Assert.IsType<byte[]>(preserved[EffectIdentityState.StatePath]),
            plan.EffectIdentityAfterImage, parentRoots, plan, turn);
        foreach (var (path, bytes) in preserved.Where(pair => pair.Key != ResourceMaterializationContract.StatePath &&
                     pair.Key != ResourceMaterializationContract.HistoryPath && pair.Key != EffectIdentityState.StatePath &&
                     pair.Key != AfterlifeEntityProfileState.StatePath && pair.Key != WoundIdentityState.StatePath &&
                     pair.Key != WoundHistoryState.HistoryPath))
            Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, path));
        WritePressureOwnerTiming("pair_real_start_publication", conflictId, started);
        return initialization.Coordinate.ResourceOwnerId;
    }

    /// <summary>Checks sealed completed-plan lifecycle output and each retained parent's exact append-only identity delta on a signed start turn.</summary>
    /// <remarks>
    /// The source definition configures a non-consuming trigger. This DATA check relies on the real issued completed plan;
    /// it does not independently observe activation authority stamps, actual use counters or transcript terminal results.
    /// </remarks>
    /// <param name="beforeBytes">Exact index bytes retained before the separately signed start.</param>
    /// <param name="after">The issued common plan's index, already proved equal to published current bytes.</param>
    /// <param name="parentRoots">All exact roots from genuine previously published parents.</param>
    /// <param name="plan">The actual source-issued common plan, not a caller-created expected plan.</param>
    /// <param name="turn">This separate accepted start turn.</param>
    private static void AssertPressurePairStartIdentityAppend(byte[] beforeBytes, JsonObject after,
        IReadOnlyList<string> parentRoots, AcceptedMechanicsPlan plan, int turn)
    {
        var before = JsonNode.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(beforeBytes))!.AsObject();
        var priorParsed = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(before), EffectIdentityState.StatePath);
        var nextParsed = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(after), EffectIdentityState.StatePath);
        Assert.Empty(priorParsed.Issues);
        Assert.Empty(nextParsed.Issues);
        var prior = Assert.IsType<EffectIdentityState>(priorParsed.State);
        var current = Assert.IsType<EffectIdentityState>(nextParsed.State);
        Assert.Equal(prior.Entries.Select(row => row.EffectId).Order(), current.Entries.Select(row => row.EffectId).Order());
        var effects = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        Assert.True(effects.IsAcceptedBoundaryComplete);
        Assert.Empty(effects.AllocatedEffectIds);
        Assert.Equal(parentRoots.Count, effects.AllocatedTransitionIds.Count);
        var restored = after.DeepClone().AsObject();
        foreach (var entry in prior.Entries)
        {
            Assert.True(current.TryGetEntry(entry.EffectId, out var next));
            if (!parentRoots.Contains(entry.EffectId))
            {
                Assert.True(JsonNode.DeepEquals(entry.Raw, next.Raw), entry.EffectId);
                continue;
            }
            Assert.Equal("active", next.State);
            Assert.Equal(entry.Transitions.Count + 1, next.Transitions.Count);
            for (var index = 0; index < entry.Transitions.Count; index++)
                Assert.True(JsonNode.DeepEquals(entry.Transitions[index].Raw, next.Transitions[index].Raw), entry.EffectId);
            var ownerEvent = Assert.Single(effects.EventInput["lifecycleEvents"]!.AsArray().OfType<JsonObject>(), row =>
                row["phase"]!.GetValue<string>() == "owner_turn_end" && row["realm"]!.GetValue<string>() == entry.Realm &&
                JsonNode.DeepEquals(row["target"], entry.Target));
            Assert.Equal(turn, ownerEvent["turn"]!.GetValue<int>());
            var eventRef = ownerEvent["eventRef"]!.GetValue<string>();
            Assert.Equal($"turn_{turn}:lifecycle:owner_turn_end:chaos_sea:player:player_soul", eventRef);
            var appended = next.Transitions[^1];
            Assert.Equal(("trigger", turn), (appended.Kind, appended.Turn));
            Assert.Equal(EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(eventRef, entry.EffectId,
                new ResourcePendingAuthorityBinding("permanent", entry.EffectId), "on_owner_turn_end"), appended.EventRef);
            Assert.Equal(new[] { entry.EffectId }, appended.SourceEffectIds);
            Assert.Equal(new[] { entry.EffectId }, appended.ResultEffectIds);
            Assert.Null(appended.ReceiptId);
            Assert.Contains(appended.TransitionId, effects.AllocatedTransitionIds);
            Assert.DoesNotContain(appended.TransitionId, entry.Transitions.Select(row => row.TransitionId));
            var definition = Assert.Single(effects.SourceBindings, row => row.Key == new EffectSourceKey(
                entry.Realm, "wound", entry.Source["sourceId"]!.GetValue<string>(), entry.Source["definitionKey"]!.GetValue<string>())).Definition;
            Assert.Equal("source_bound", definition["lifetime"]!["mode"]!.GetValue<string>());
            Assert.False(Assert.Single(definition["triggers"]!.AsArray())!["consumeUses"]!.GetValue<bool>());
            var restoredEntry = Assert.Single(restored["entries"]!.AsArray().OfType<JsonObject>(),
                row => row["effectId"]!.GetValue<string>() == entry.EffectId);
            restoredEntry["transitions"] = entry.Raw["transitions"]!.DeepClone();
        }
        Assert.True(JsonNode.DeepEquals(before, restored));
    }

    /// <summary>Parses one current complete ledger and checks both History and canonical owner/quartet agreement before signing.</summary>
    /// <param name="context">Independent fixture whose current published resources are checked.</param>
    /// <param name="lease">Actual lease keeping the source/read window stable.</param>
    /// <param name="definitions">Unchanged original parsed resource definitions.</param>
    /// <returns>Detached resource DATA after fresh complete agreement checks.</returns>
    private static async Task<PressurePairResources> ReadPressurePairResourcesAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, ResourceDefinitionCatalog definitions)
    {
        var data = ReadPressurePairResourceData(definitions,
            await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.StatePath),
            await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath));
        var canonical = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions,
            path => context.FileSystem.ReadFileAsync(lease, path), data.State, data.History,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(canonical.IsValid, string.Join("\n", canonical.Issues));
        Assert.Empty(canonical.Authority!.ValidateCanonicalAgreement(data.State, data.History));
        return data;
    }

    /// <summary>Checks exact complete resource DATA without treating a pending terminal draft as already published authority.</summary>
    /// <param name="definitions">Unchanged parsed sealed definitions.</param>
    /// <param name="state">Complete canonical current ledger JSON.</param>
    /// <param name="history">Complete retained resource History JSON.</param>
    /// <returns>Validated DATA, never execution or publication permission.</returns>
    private static PressurePairResources ReadPressurePairResourceData(ResourceDefinitionCatalog definitions, string? state, string? history)
    {
        var parsedState = ResourceStateContract.ParseCanonical(state, definitions, allowMissingPristine: false);
        var parsedHistory = ResourceHistoryState.ParseCanonical(history, definitions, allowMissingPristine: false);
        Assert.True(parsedState.IsValid, string.Join("\n", parsedState.Issues));
        Assert.True(parsedHistory.IsValid, string.Join("\n", parsedHistory.Issues));
        Assert.Empty(parsedHistory.History!.ValidateStateAgreement(parsedState.Ledger!));
        return new(parsedState.Ledger!, parsedHistory.History!);
    }

    /// <summary>Proves append-only complete resource History, returning only the exact newly accepted transitions.</summary>
    /// <param name="before">Complete accepted History before the real operation.</param>
    /// <param name="after">Complete accepted History after the real operation.</param>
    /// <param name="count">Exact expected newly appended transition count.</param>
    /// <returns>New transition DATA after every existing row was proved unchanged.</returns>
    private static ResourceTransition[] AssertPressurePairResourceAppend(ResourceHistoryState before, ResourceHistoryState after, int count)
    {
        var ids = before.Transitions.Select(row => row.TransitionId).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(ResourceHistoryState.SerializeCanonicalTransitions(before.Transitions),
            ResourceHistoryState.SerializeCanonicalTransitions(after.Transitions.Where(row => ids.Contains(row.TransitionId))));
        var appended = after.Transitions.Where(row => !ids.Contains(row.TransitionId)).ToArray();
        Assert.Equal(count, appended.Length);
        return appended;
    }

    /// <summary>Reads the exact persistent player coordinate, rather than whichever actor happens to be first.</summary>
    /// <param name="state">Complete current validated ledger.</param>
    /// <returns>The player's exact current and maximum balance.</returns>
    private static (decimal Current, decimal Maximum) PressurePairPlayerBalance(ResourceStateLedger state)
    {
        Assert.True(state.TryResolveExact(new("chaos_sea", ResourceOwnerKind.AfterlifeActor,
            "player_soul", "spiritual_action_points"), out var entry));
        return (entry!.Current, entry.Maximum);
    }

    /// <summary>Requires every previous opposition owner to remain terminal and absent from live state.</summary>
    /// <param name="data">Complete current resource state and retained History.</param>
    /// <param name="owners">All actual prior opposition owner IDs.</param>
    private static void AssertPressurePairRetiredOwners(PressurePairResources data, IReadOnlyList<string> owners)
    {
        foreach (var owner in owners)
        {
            var coordinate = new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide, owner, "spiritual_action_points");
            Assert.True(data.History.IsTerminal(coordinate));
            Assert.False(data.State.TryResolveExact(coordinate, out _));
            Assert.Single(data.History.Transitions, row => row.Coordinate == coordinate && row.Operation == ResourceTransitionOperation.Retire);
        }
    }

    /// <summary>Validates the complete current parent set once per changed publication window, with exact identity and creation receipt joins.</summary>
    /// <param name="context">Independent fixture retaining actual accepted parents.</param>
    /// <param name="expectedCount">Exact active parent count in the complete carrier catalog.</param>
    /// <param name="lease">Optional existing read-window lease, used for final authenticated observation.</param>
    /// <returns>One parsed current parent catalog and its checked History/receipt DATA.</returns>
    private static async Task<PressurePairParents> ReadPressurePairParentsAsync(ResourceMaterializationTestContext context,
        int expectedCount, FileSystemManager.CanonicalWriteLease? lease = null)
    {
        async Task<string?> Read(string path) => lease is null
            ? await context.FileSystem.ReadFileAsync(path) : await context.FileSystem.ReadFileAsync(lease, path);
        async Task<JsonObject?> ReadObject(string path)
        {
            var json = await Read(path);
            return json is null ? null : JsonNode.Parse(json)!.AsObject();
        }
        var profiles = JsonNode.Parse((await Read(AfterlifeEntityProfileState.StatePath))!)!.AsObject();
        var carriers = new WoundCarrierCatalogInput(await ReadObject(WoundCarrierCatalog.PlayerPath),
            await ReadObject(WoundCarrierCatalog.NpcPath), await ReadObject(WoundCarrierCatalog.EnemiesPath),
            await ReadObject(WoundCarrierCatalog.AlliesPath), profiles);
        var catalog = WoundCarrierCatalog.Build(carriers);
        Assert.Empty(catalog.Issues);
        Assert.Equal(expectedCount, catalog.Occurrences.Count);
        var identities = WoundIdentityState.Parse(await Read(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(await Read(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        var receiptRoot = JsonNode.Parse((await Read(SpiritualWoundOpportunityReceiptState.StatePath))!)!.AsObject();
        var receipts = SpiritualWoundOpportunityReceiptState.Parse(receiptRoot.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(identities.IsValid, string.Join("\n", identities.Issues));
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(receipts.IsValid, string.Join("\n", receipts.Issues));
        Assert.Empty(history.State!.ValidateAgreement(identities.State!, catalog));
        Assert.True(receipts.State!.HasExactStrainHistory(history.State));
        foreach (var occurrence in catalog.Occurrences)
        {
            Assert.Equal(("chaos_sea", "player_soul", "player_soul", AfterlifeEntityProfileState.StatePath),
                (occurrence.Coordinate.Realm, occurrence.Coordinate.OwnerKind, occurrence.Coordinate.OwnerId, occurrence.Coordinate.CarrierPath));
            Assert.Equal("active", occurrence.Wound.Lifecycle);
            Assert.Equal(3, occurrence.Wound.Severity.Rank);
            var creation = Assert.Single(history.State.ResolveWoundTransitions(occurrence.WoundId), row => row.Kind == "create");
            var decision = Assert.Single(receiptRoot["decisions"]!.AsArray(), row => row!["woundId"]!.GetValue<string>() == occurrence.WoundId)!;
            Assert.True(receipts.State.CoversOriginalCreation(occurrence.WoundId,
                decision["opportunityRef"]!.GetValue<string>(), creation));
        }
        return new(catalog, history.State, receiptRoot, profiles, carriers);
    }

    /// <summary>Requires exactly two distinct genuine births/decisions/closures, preserving full current parent graphs.</summary>
    /// <param name="data">Validated complete current parent DATA.</param>
    /// <param name="first">Original first real publication facts.</param>
    /// <param name="second">Original second real publication facts.</param>
    private static void AssertPressurePairReceiptJoins(PressurePairParents data, PressurePairParent first, PressurePairParent second)
    {
        Assert.Equal(3, data.History.Transitions.Count);
        foreach (var parent in new[] { first, second })
        {
            var wound = data.Catalog.Occurrences.Single(row => row.WoundId == parent.WoundId).Wound;
            Assert.Equal(parent.CanonicalJson, WoundMaterializationContract.SerializeCanonical(wound));
            var creation = Assert.Single(data.History.ResolveWoundTransitions(parent.WoundId), row => row.Kind == "create");
            Assert.Equal(parent.TransitionId, creation.TransitionId);
        }
        Assert.NotEqual(first.TransitionId, second.TransitionId);
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(2, data.ReceiptRoot[field]!.AsArray().Count);
        Assert.Equal(2, data.ReceiptRoot["instances"]!.AsArray().Select(row => row!["instanceId"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, data.ReceiptRoot["instances"]!.AsArray().Select(row => row!["displayConflictId"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, data.ReceiptRoot["decisions"]!.AsArray().Select(row => row!["decisionId"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(new[] { 42, 43, 44 }, data.History.Transitions.Select(row => row.Turn).Order());
    }

    /// <summary>Compares full canonical transition rows, including persisted results, across a changed publication.</summary>
    private static void AssertPressurePairWoundHistoryPrefix(WoundHistoryState before, WoundHistoryState after)
    {
        var prior = JsonNode.Parse(WoundHistoryState.SerializeCanonical(before))!["transitions"]!.AsArray();
        var current = JsonNode.Parse(WoundHistoryState.SerializeCanonical(after))!["transitions"]!.AsArray();
        Assert.True(current.Count >= prior.Count);
        for (var index = 0; index < prior.Count; index++)
            Assert.True(JsonNode.DeepEquals(prior[index], current[index]), $"Wound History prefix row {index}");
    }

    /// <summary>Checks both retained effect graphs without a third conflict or any group admission.</summary>
    private static async Task AssertPressurePairEffectLineageAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, PressurePairParents parents)
    {
        var loaded = await EffectMechanicsSnapshot.LoadWithDataAsync(context.FileSystem, lease);
        Assert.True(loaded.Snapshot.IsAccepted, string.Join("\n", loaded.Snapshot.Issues));
        var identities = Assert.IsType<EffectIdentityState>(loaded.Identities);
        foreach (var occurrence in parents.Catalog.Occurrences)
        {
            var wound = occurrence.Wound;
            var lineage = WoundEffectLineagePlanner.Plan(wound, identities);
            Assert.True(lineage.Success, string.Join("\n", lineage.Issues));
            var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
            Assert.True(loaded.Catalog.TryResolveOne(root.EffectId, out var effect));
            Assert.Equal("active", effect.Effect["state"]!.GetValue<string>());
            Assert.Equal(1, effect.Effect["stacking"]!["currentStacks"]!.GetValue<int>());
            Assert.Equal(1, effect.Effect["stacking"]!["maxStacks"]!.GetValue<int>());
            var definition = JsonNode.Parse(Assert.Single(wound.Consequences.OwnedEffectSources.Definitions).GetRawText())!.AsObject();
            Assert.Equal(1, definition["stacking"]!["maxStacks"]!.GetValue<int>());
            Assert.True(WoundEffectCarrierAdapter.TryCreateTargetKey(wound.Owner, out var target));
            Assert.True(WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(wound.Owner, target, definition, out var carrier));
            Assert.True(WoundAcceptedTurnPlannerCore.PriorRootHasExactGenerationAuthority(root.EffectId,
                new(wound.Owner.Realm, "wound", wound.WoundId, root.DefinitionKey), target, carrier, definition,
                loaded.Catalog, identities));
        }
    }

    /// <summary>Immutable parent identity and graph captured only after real C4 publication.</summary>
    private sealed record PressurePairParent(string WoundId, string CanonicalJson, string TransitionId);
    /// <summary>One observation's validated complete canonical parent DATA, never a live owner or selected group.</summary>
    private sealed record PressurePairParents(WoundCarrierCatalog Catalog, WoundHistoryState History,
        JsonObject ReceiptRoot, JsonObject Profiles, WoundCarrierCatalogInput Carriers);
    /// <summary>One changed resource window's validated state and complete retained History.</summary>
    private sealed record PressurePairResources(ResourceStateLedger State, ResourceHistoryState History);
    /// <summary>Per-test immutable completed template, retained only after its original filesystem has been disposed.</summary>
    private sealed record PressurePairFixtureTemplate(PreparedFixtureTree Tree, string OriginalRoot, string FirstWoundId, string SecondWoundId);
}
