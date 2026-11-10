using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Controls the new retained-DATA setup through the actual singleton ordinary owner.</summary>
    [Fact]
    public Task SpiritualPressureRetainedData_SingleParentOrdinaryComparisonSucceeds() =>
        RequireRetainedPressureGroupOrdinaryAsync(parentCount: 1);

    /// <summary>
    /// Requires ordinary comparison of two complete retained-DATA parents. On current SOURCE this
    /// expected-success assertion should be RED at the unchanged singular contribution selection.
    /// Neither parent is claimed to be a genuine C2/C4 birth, and no group actual path is opened.
    /// </summary>
    [Fact]
    public Task SpiritualPressureRetainedData_TwoParentOrdinaryComparisonSucceeds() =>
        RequireRetainedPressureGroupOrdinaryAsync(parentCount: 2);

    /// <summary>
    /// Builds a fresh signed baseline for each Fact, completes all preparation oracles, and calls
    /// only the existing comparison owner. Missing preparation is never classified as semantic RED.
    /// </summary>
    private async Task RequireRetainedPressureGroupOrdinaryAsync(int parentCount)
    {
        var label = parentCount == 1 ? "retained_data_singleton" : "retained_data_pair";
        ResourceMaterializationTestContext? context = null;
        var data = SpiritualRetainedPressureGroupTestData.Create(parentCount);
        try
        {
            var preparationStarted = Stopwatch.GetTimestamp();
            context = await CreateCompleteConflictFrameContextAsync(
                playerCurrent: 3m, oppositionCurrent: 3m, spiritFocusTier: 3, oppositionMaximum: 9m,
                initializeContext: allocated => { context = allocated; return Task.CompletedTask; },
                seedOriginalInputs: async fixture =>
                {
                    await SeedOriginalIntakeBaselinesAsync(fixture);
                    await SeedRetainedPressureGroupDataAsync(fixture, data);
                },
                captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(
                    turn: 46, currentRealm: "Chaos Sea", preGeneratedDices1d20: [4, 12],
                    sessionId: "session_" + label, requestId: "request_" + label));
            WritePressureOwnerTiming("retained_data_signed_preparation", label, preparationStarted);
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var baselineFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var preparedData = await AssertRetainedPressureGroupSignedDataAsync(context, lease, data, label);
            var signed = preparedData.Authority;
            await AssertRetainedPressureGroupGraphsAsync(context, lease, preparedData.Parents);
            AssertExistingPressureInputInert(context, lease, baselineFiles, generation);

            var root = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
            var active = root["activeConflict"]!.AsObject();
            Assert.Equal(("clear", "clear", "contested"),
                (active["playerSideStrain"]!.GetValue<string>(), active["oppositionSideStrain"]!.GetValue<string>(),
                 active["conflictPosition"]!.GetValue<string>()));
            Assert.Empty(active["exchangeLog"]!.AsArray());
            Assert.False(root.ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
            var ordinary = SpiritualRetainedPressureGroupTestData.CreateExchange(
                "exchange_retained_pressure_46", 46, "clear", "fractured");
            AssertRetainedPressureOrdinaryInput(ordinary, active, signed);
            active["exchangeLog"]!.AsArray().Add(ordinary.DeepClone()); active["playerSideStrain"] = "fractured";
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                Encoding.UTF8.GetBytes(root.ToJsonString()));
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath, Encoding.UTF8.GetBytes(new JsonObject
            {
                ["response"] = "Душа встречает новое давление Хранителя. Противостояние продолжается.",
                ["timestamp"] = "2026-11-05T00:00:00Z"
            }.ToJsonString()));
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
            AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
            var definitions = (await context.FileSystem.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath))!;
            var generationToken = Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease));
            var copy = new PressureOrdinaryOwnerCopy(context, generationToken, ordinary, root, definitions);
            ValidationService.SpiritualOrdinaryPressureComparisonResult result;
            using (var capture = await BeginPressureOrdinaryOwnerAsync(copy, lease, label))
            {
                var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                var before = await ReadPressureOwnerSnapshotAsync(capture, lease, label + "_before");
                AssertPressureOrdinaryFrontier(before, copy, capture, lease);
                Assert.Equal(0, before.NextOrdinal);
                Assert.Equal(before.AllocationCursor, JsonNode.Parse(before.AllocationJournalJson)!.AsArray().Count);
                var counts = capture.ReadExistingStrainReuseCounts();
                _storageOutput.WriteLine($"retained_pressure_preparation_complete parents={parentCount}; " +
                    "signed/current graphs, first-create receipts, ordinary dice/source/resource DATA, real intake and closed prefix checked");
                try
                {
                    result = await MeasurePressureOwnerPhaseAsync("retained_data_comparison", label,
                        () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
                }
                finally
                {
                    var after = await ReadPressureOwnerSnapshotAsync(capture, lease, label + "_after");
                    AssertPressureOrdinaryFrontiersEqual(before, after);
                    AssertPressureOrdinaryFrontier(after, copy, capture, lease);
                    AssertExistingPressureInputInert(context, lease, files, generation);
                    Assert.True(capture.IsCurrentOwner);
                }
                var afterCounts = capture.ReadExistingStrainReuseCounts();
                Assert.Equal(counts.Queries + 1, afterCounts.Queries);
                Assert.Equal(counts.ActualEvaluations, afterCounts.ActualEvaluations);
                Assert.False(result.PrefixBlocked);
                if (parentCount == 2 && result.Comparison is null)
                {
                    // These exact diagnostics plus zero evaluator/parent-read deltas distinguish
                    // the predicted isolation refusal from every earlier generic contour exit.
                    var refusal = Assert.Single(result.Issues);
                    Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", refusal.Code);
                    Assert.Equal("one complete signed current rank-III/IV parent or a complete rank-III/IV pressure strain group", refusal.Actual);
                    Assert.Equal(counts.OrdinaryEvaluations, afterCounts.OrdinaryEvaluations);
                    Assert.Equal(counts.SignedParentReads, afterCounts.SignedParentReads);
                }
                else
                {
                    AssertNoConflictFrameErrors(result.Issues);
                    Assert.NotNull(result.Comparison);
                    AssertRetainedPressureOrdinaryComparison(result.Comparison, copy, files);
                    Assert.Equal(counts.OrdinaryEvaluations + 1, afterCounts.OrdinaryEvaluations);
                    if (parentCount == 1) Assert.Equal(counts.SignedParentReads + 1, afterCounts.SignedParentReads);
                }
                // Capture disposal is also required to be physically inert before the semantic assertion.
                baselineFiles = files;
            }
            AssertExistingPressureInputInert(context, lease, baselineFiles, generation);
            foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                         SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
            Assert.True(result.Comparison is not null && result.Issues.Count == 0,
                "Expected two-parent retained-DATA ordinary comparison success after complete preparation. " +
                "The unchanged SOURCE singular-selection guard refused the exact pair before evaluation: " +
                string.Join("; ", result.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        }
        finally
        {
            if (context is not null)
            {
                var cleanupStarted = Stopwatch.GetTimestamp();
                await context.DisposeAsync();
                WritePressureOwnerTiming("retained_data_owned_cleanup", label, cleanupStarted);
                Assert.False(Directory.Exists(context.RootPath));
            }
        }
    }

    /// <summary>Installs only baseline DATA before authority composition and the genuine fixture signature.</summary>
    private static async Task SeedRetainedPressureGroupDataAsync(ResourceMaterializationTestContext context,
        RetainedPressureGroupData data)
    {
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(EffectCarrierCatalog.PlayerPath));
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(EffectCarrierCatalog.NpcPath));
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var player = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray(), row =>
            row!["actorType"]!.GetValue<string>() == "player_soul" && row["actorId"]!.GetValue<string>() == "player_soul")!;
        player["activeWounds"] = new JsonArray(data.Parents.Select(row => row.DeepClone()).ToArray());
        player["activeEffects"] = new JsonArray(data.Effects.Select(row => row.DeepClone()).ToArray());
        // One shared DATA gate before installation/signing; current live authority remains independently checked later.
        SpiritualPressureRetainedDataAdmissibility.Require(data, profiles);
        await context.WriteExactJsonAsync(AfterlifeEntityProfileState.StatePath, profiles.ToJsonString());
        foreach (var (path, root) in new[]
        {
            (WoundIdentityState.StatePath, data.WoundIdentities), (WoundHistoryState.HistoryPath, data.History),
            (EffectIdentityState.StatePath, data.EffectIdentities), (SpiritualWoundOpportunityReceiptState.StatePath, data.Receipts),
            (EffectCarrierCatalog.PlayerPath, new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray() }),
            (EffectCarrierCatalog.NpcPath, new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() })
        }) await context.WriteExactJsonAsync(path, root.ToJsonString());
    }

    /// <summary>Authenticates signed/current bytes and independently validates every first-create source/decision/History join.</summary>
    private static async Task<RetainedPressureGroupBaseline> AssertRetainedPressureGroupSignedDataAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease,
        RetainedPressureGroupData data, string label)
    {
        var optional = new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath,
            EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath };
        var required = PressureOrdinarySignedPaths.Append(ResourceMaterializationContract.DefinitionsPath)
            .Append(CanonicalResourceOwnerAuthorityComposer.AuthorityPath).ToArray();
        var read = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(required, optional));
        Assert.True(read.Success, string.Join("\n", read.Issues));
        var authority = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        Assert.Equal((46, "chaos_sea", "request_" + label), (authority.TurnNumber, authority.Realm, authority.RequestId));
        Assert.Equal(new[] { 4, 12 }, authority.AcceptedD20EventValues);
        foreach (var path in required.Concat(optional))
        {
            var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
            if (bytes is null) Assert.Contains(path, authority.AbsentLogicalPaths);
            else Assert.Equal(bytes, authority.ReadRequiredBytes(path));
        }
        foreach (var (path, collection) in new[]
        {
            (EffectCarrierCatalog.PlayerPath, "activeEffects"), (EffectCarrierCatalog.NpcPath, "entries")
        })
        {
            var canonicalEmpty = new JsonObject { ["schemaVersion"] = 1, [collection] = new JsonArray() };
            Assert.True(JsonNode.DeepEquals(canonicalEmpty, JsonNode.Parse(authority.ReadRequiredBytes(path))), path);
        }
        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!)!;
        foreach (var pair in manifest["files"]!.AsObject())
            Assert.Equal(await context.FileSystem.ReadFileBytesAsync(lease, pair.Key),
                await context.FileSystem.ReadFileBytesAsync(lease, pair.Value!.GetValue<string>()));
        var parsed = ReadRetainedPressureParentData(authority, data.Parents.Count);
        var receipt = parsed.Receipts;
        Assert.False(receipt.HasRetainedPressureComparisons); // These parents have original sources, not v3 actual pressure proofs.
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(data.Parents.Count, parsed.ReceiptRoot[field]!.AsArray().Count);
        Assert.Equal(data.Parents.Count, parsed.ReceiptRoot["instances"]!.AsArray()
            .Select(row => row!["instanceId"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(data.Parents.Count, parsed.Catalog.Occurrences.Select(row => row.WoundId).Distinct(StringComparer.Ordinal).Count());
        foreach (var occurrence in parsed.Catalog.Occurrences)
        {
            var parent = occurrence.Wound;
            Assert.Equal(("chaos_sea", "player_soul", "player_soul", WoundCarrierCatalog.AfterlifeProfilesPath),
                (occurrence.Coordinate.Realm, occurrence.Coordinate.OwnerKind, occurrence.Coordinate.OwnerId, occurrence.Coordinate.CarrierPath));
            Assert.Equal(("active", "spiritual", 3), (parent.Lifecycle, parent.Classification.Domain, parent.Severity.Rank));
            var expected = Assert.Single(data.Parents, row => row["woundId"]!.GetValue<string>() == parent.WoundId);
            Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(parent))));
            var creation = Assert.Single(parsed.History.ResolveWoundTransitions(parent.WoundId));
            Assert.Equal((1, "create", parent.Origin.EventRef, parent.Origin.CreatedAtTurn),
                (creation.WoundTransitionOrdinal, creation.Kind, creation.EventRef, creation.Turn));
            Assert.Equal(WoundHistoryState.ComputeNonexistentBeforeFingerprint(parent.WoundId), creation.BeforeFingerprint);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(parent), creation.AfterFingerprint);
            var decision = Assert.Single(parsed.ReceiptRoot["decisions"]!.AsArray(), row => row!["woundId"]!.GetValue<string>() == parent.WoundId)!;
            var sourceRow = Assert.Single(parsed.ReceiptRoot["sources"]!.AsArray(), row =>
                row!["witness"]!["sourceId"]!.GetValue<string>() == decision["sourceId"]!.GetValue<string>())!;
            var source = sourceRow["witness"]!.AsObject();
            var sourceCalculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var strain);
            Assert.Null(strain);
            Assert.Equal((0, 3, 2, 20L, 3), (sourceCalculation.PreviousStrainRank, sourceCalculation.NewStrainRank,
                sourceCalculation.ExtraJumpSteps, sourceCalculation.TraumaPressure, sourceCalculation.MaximumSeverityRank));
            Assert.Equal(("materialize", 3, creation.TransitionId), (decision["decision"]!.GetValue<string>(),
                decision["selectedSeverityRank"]!.GetValue<int>(), decision["transitionId"]!.GetValue<string>()));
            Assert.Equal(source["sourceFingerprint"]!.GetValue<string>(), creation.SourceFingerprint);
            Assert.True(JsonNode.DeepEquals(source, decision["sourceWitness"]));
            var instanceId = sourceRow["instanceId"]!.GetValue<string>();
            Assert.Equal(instanceId, decision["instanceId"]!.GetValue<string>());
            var instance = Assert.Single(parsed.ReceiptRoot["instances"]!.AsArray(), row => row!["instanceId"]!.GetValue<string>() == instanceId)!;
            Assert.Equal(source["conflictId"]!.GetValue<string>(), instance["displayConflictId"]!.GetValue<string>());
            var evidence = source["turnEvidence"]!;
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "turn" })
                Assert.True(JsonNode.DeepEquals(evidence[field], decision["binding"]![field]), field);
            var key = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
            {
                "book_of_eternity.spiritual_wound_source_event", "1", evidence["sessionId"]!.GetValue<string>(),
                evidence["requestId"]!.GetValue<string>(), evidence["snapshotToken"]!.GetValue<string>(),
                source["conflictId"]!.GetValue<string>(), source["exchangeId"]!.GetValue<string>(), "player"
            })[7..];
            Assert.Equal("spiritual_wound_event_" + key, parent.Origin.EventRef);
            Assert.Equal("spiritual_wound_" + key, parent.Origin.OpportunityId);
            Assert.Equal(("spiritual_standard_art", "guardian:guardian_frame:pressure"),
                (parent.Origin.SourceKind, parent.Origin.SourceId));
            Assert.True(receipt.CoversOriginalCreation(parent.WoundId, parent.Origin.OpportunityId, creation));
            var recorded = receipt.FindRecordedConflictSideWound(instanceId, "player");
            Assert.Empty(recorded.Issues);
            Assert.Equal((parent.WoundId, creation.TransitionId), (recorded.Wound!.WoundId, recorded.Wound.CreateTransitionId));
        }
        return new(authority, parsed);
    }

    /// <summary>Parses each authenticated catalog once within the unchanged preparation window, with complete cross-file agreement.</summary>
    private static RetainedPressureParentData ReadRetainedPressureParentData(PendingTurnSnapshotReadAuthority signed,
        int expectedParents)
    {
        string Read(string path) => Encoding.UTF8.GetString(signed.ReadRequiredBytes(path));
        JsonObject? Optional(string path) => signed.AbsentLogicalPaths.Contains(path) ? null : JsonNode.Parse(Read(path))!.AsObject();
        var profiles = JsonNode.Parse(Read(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var carriers = new WoundCarrierCatalogInput(Optional(WoundCarrierCatalog.PlayerPath), Optional(WoundCarrierCatalog.NpcPath),
            Optional(WoundCarrierCatalog.EnemiesPath), Optional(WoundCarrierCatalog.AlliesPath), profiles);
        var catalog = WoundCarrierCatalog.Build(carriers);
        Assert.Empty(catalog.Issues);
        Assert.Equal(expectedParents, catalog.Occurrences.Count);
        var identities = WoundIdentityState.Parse(Read(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(Read(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        var receiptRoot = JsonNode.Parse(Read(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        var receipts = SpiritualWoundOpportunityReceiptState.Parse(receiptRoot.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(identities.IsValid, string.Join("\n", identities.Issues));
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(receipts.IsValid, string.Join("\n", receipts.Issues));
        Assert.Empty(history.State!.ValidateAgreement(identities.State!, catalog));
        Assert.True(receipts.State!.HasExactStrainHistory(history.State));
        return new(catalog, history.State, receipts.State, receiptRoot, profiles, carriers);
    }

    /// <summary>Checks all current source graphs and sibling burdens through production typed DATA composers before intake.</summary>
    private static async Task AssertRetainedPressureGroupGraphsAsync(ResourceMaterializationTestContext context,
        FileSystemManager.CanonicalWriteLease lease, RetainedPressureParentData parents)
    {
        // Same hookless, leased, unchanged preparation window: reuse parsed wound DATA,
        // never any live owner or a cached permission. The actual intake revalidates independently.
        var expectedParents = parents.Catalog.Occurrences.Count;
        var loaded = await EffectMechanicsSnapshot.LoadWithDataAsync(context.FileSystem, lease);
        Assert.True(loaded.Snapshot.IsAccepted, string.Join("\n", loaded.Snapshot.Issues));
        var identities = Assert.IsType<EffectIdentityState>(loaded.Identities);
        var conflict = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
        var sources = EffectAcceptedTurnInputComposer.BuildCanonicalWoundSourceAuthority(parents.Catalog);
        Assert.Empty(sources.Issues);
        var targets = EffectAcceptedTurnInputComposer.BuildCanonicalTargetAuthority(
            new(null, null, parents.Carriers.EnemyCombatants, parents.Carriers.AllyCombatants, parents.Profiles, conflict),
            new Dictionary<string, JsonNode?>());
        Assert.Empty(targets.Issues);
        var projection = SpiritualWoundConflictContributionProjector.Project(loaded.Snapshot, sources, targets, conflict);
        Assert.True(projection.IsAccepted, string.Join("\n", projection.Issues));
        Assert.Equal(3 * expectedParents, projection.Contributions.Count);
        Assert.Equal(expectedParents, loaded.Catalog.Occurrences.Count);
        foreach (var occurrence in parents.Catalog.Occurrences)
        {
            var wound = occurrence.Wound;
            var lineage = WoundEffectLineagePlanner.Plan(wound, identities);
            Assert.True(lineage.Success, string.Join("\n", lineage.Issues));
            var root = Assert.Single(wound.Consequences.OwnedEffectSources.RootBindings);
            var definition = JsonNode.Parse(Assert.Single(wound.Consequences.OwnedEffectSources.Definitions).GetRawText())!.AsObject();
            Assert.True(loaded.Catalog.TryResolveOne(root.EffectId, out var effect));
            Assert.Equal(("active", 1, 1), (effect.Effect["state"]!.GetValue<string>(),
                effect.Effect["stacking"]!["currentStacks"]!.GetValue<int>(), effect.Effect["stacking"]!["maxStacks"]!.GetValue<int>()));
            Assert.Equal(1, definition["stacking"]!["maxStacks"]!.GetValue<int>());
            Assert.True(WoundEffectCarrierAdapter.TryCreateTargetKey(wound.Owner, out var target));
            Assert.True(WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(wound.Owner, target, definition, out var carrier));
            Assert.True(WoundAcceptedTurnPlannerCore.PriorRootHasExactGenerationAuthority(root.EffectId,
                new(wound.Owner.Realm, "wound", wound.WoundId, root.DefinitionKey), target, carrier, definition, loaded.Catalog, identities));
            var rows = projection.Contributions.Where(row => row.WoundSource.SourceId == wound.WoundId).ToArray();
            Assert.Equal(3, rows.Length);
            Assert.All(rows, row => Assert.Equal(("chaos_sea", "player", "player_soul", "player", 1, root.EffectId),
                (row.Actor.Realm, row.Actor.Kind, row.Actor.TargetId, row.ResolvedSide, row.CurrentStacks, row.EffectId)));
            Assert.Equal(1, Assert.Single(rows, row => row.Profile == "spiritual_strain_burden" && row.Operation == "pressure").Magnitude.GetInt32());
            Assert.Single(rows, row => row.Profile == "spiritual_roll_hindrance" && row.Operation == "counter");
            Assert.Equal(2, Assert.Single(rows, row => row.Profile == "spiritual_action_cost_burden" && row.Operation == "guard").Magnitude.GetInt32());
        }
    }

    /// <summary>Checks the complete ordinary shape, signed dice, independent intrinsic source and literal arithmetic before comparison.</summary>
    private static void AssertRetainedPressureOrdinaryInput(JsonObject ordinary, JsonObject active,
        PendingTurnSnapshotReadAuthority signed)
    {
        var source = SpiritualRetainedPressureGroupTestData.CreateSource(ordinary,
            signed.SessionId, signed.RequestId, signed.SnapshotToken, active["conflictId"]!.GetValue<string>(),
            new CanonicalBeforeImage(true, signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)).Fingerprint);
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "fixture.ordinary", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "fixture.ordinary", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
        AssertNoConflictFrameErrors(issues);
        Assert.Equal(new[] { 4, 12 }, source["turnEvidence"]!["acceptedD20Values"]!.AsArray().Select(value => value!.GetValue<int>()));
        Assert.Equal(signed.AcceptedD20EventValues, source["turnEvidence"]!["acceptedD20Values"]!.AsArray().Select(value => value!.GetValue<int>()));
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(source, out var strain);
        Assert.Null(strain);
        Assert.Equal((0, 2, 1, 13L, 2), (calculation.PreviousStrainRank, calculation.NewStrainRank,
            calculation.ExtraJumpSteps, calculation.TraumaPressure, calculation.MaximumSeverityRank));
        Assert.Equal(("pressure", "setback", -8), (ordinary["operationType"]!.GetValue<string>(),
            ordinary["outcome"]!.GetValue<string>(), ordinary["diceAudit"]!["margin"]!.GetValue<int>()));
        foreach (var field in new[] { "oppositionSideStrain", "conflictPosition", "controlState", "tempoAdvantage" })
            Assert.True(JsonNode.DeepEquals(ordinary["before"]![field], ordinary["after"]![field]), field);
        Assert.True(AfterlifeActionCostRules.TryGetDefinition("pressure", out var definition));
        Assert.Equal(3, AfterlifeActionCostRules.ResolveStandardEffectiveCost(definition, 0));
        foreach (var side in new[] { "player", "opposition" })
        {
            var cost = ordinary["actionCostAudit"]![side]!;
            Assert.Equal((3m, 3m, 0m), (cost["effectiveCost"]!.GetValue<decimal>(),
                cost["before"]!.GetValue<decimal>(), cost["after"]!.GetValue<decimal>()));
            Assert.Equal(side == "player" ? 10m : 9m, cost["max"]!.GetValue<decimal>());
        }
    }

    /// <summary>Requires detached ordinaryII DATA and both unpaid3-point resource transitions, never actual group projection.</summary>
    private static void AssertRetainedPressureOrdinaryComparison(
        ValidationService.SpiritualOrdinaryPressureComparisonData comparison, PressureOrdinaryOwnerCopy copy,
        IReadOnlyDictionary<string, byte[]> files)
    {
        Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
        var bytes = files.Single(pair => pair.Key.Replace('\\', '/') == AfterlifeSpiritualConflictState.StatePath).Value;
        Assert.Equal(new CanonicalBeforeImage(true, bytes).Fingerprint, comparison.RawInputFingerprint);
        Assert.True(JsonNode.DeepEquals(copy.Ordinary, comparison.ReadOrdinaryExchange()));
        Assert.True(JsonNode.DeepEquals(copy.Ordinary, JsonNode.Parse(comparison.OrdinaryExchangeJson)));
        Assert.Equal(SpiritualWoundOpportunityMath.Calculate(new(8, 0, 0, "clear", "fractured", "hostile", 4)),
            comparison.OrdinaryCalculation);
        Assert.Equal((13L, 2), (comparison.OrdinaryCalculation.TraumaPressure, comparison.OrdinaryCalculation.MaximumSeverityRank));
        Assert.Equal(2, comparison.ExpectedTransitions.Count);
        var active = copy.Conflict["activeConflict"]!;
        var coordinates = new[]
        {
            new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeActor, "player_soul", "spiritual_action_points"),
            new ResourceCoordinate("chaos_sea", ResourceOwnerKind.AfterlifeConflictSide,
                active[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>(),
                "spiritual_action_points")
        };
        foreach (var coordinate in coordinates)
        {
            var transition = Assert.Single(comparison.ExpectedTransitions, row => row.Coordinate.OwnerKind == coordinate.OwnerKind);
            Assert.Equal(coordinate, transition.Coordinate);
            Assert.Equal(ResourceTransitionOperation.Spend, transition.Operation);
            Assert.Equal((3m, 0m, 3m, 0m), (transition.Before, transition.After, transition.Amount, transition.RecoveryPayment));
            Assert.Equal("exchange_retained_pressure_46", transition.SourceId);
        }
    }

    /// <summary>One unchanged preparation window's authenticated bytes and parsed parent DATA, never current admission.</summary>
    private sealed record RetainedPressureGroupBaseline(PendingTurnSnapshotReadAuthority Authority, RetainedPressureParentData Parents);

    /// <summary>One parse of each complete authenticated parent catalog, History and receipt graph for this Fact only.</summary>
    private sealed record RetainedPressureParentData(WoundCarrierCatalog Catalog, WoundHistoryState History,
        SpiritualWoundOpportunityReceiptState Receipts, JsonObject ReceiptRoot, JsonObject Profiles, WoundCarrierCatalogInput Carriers);
}
