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
    /// <summary>
    /// Desired ordinary-comparison success for a complete signed guardian III parent. Current SOURCE refuses the
    /// pressure/success contour before evaluating ordinary or reading signed parents. This test does not call actual Advance.
    /// </summary>
    [Fact]
    public async Task SpiritualGuardianAttribution_RealOwnerOrdinaryComparisonSucceeds()
    {
        const string label = "guardian_attribution_owner";
        var data = SpiritualGuardianAttributionTestData.CreateRetained();
        ResourceMaterializationTestContext? context = null;
        FileSystemManager.CanonicalWriteLease? lease = null;
        ValidationService.SpiritualOriginalTurnCapture? capture = null;
        ValidationService.SpiritualOrdinaryPressureProbeState? frontier = null;
        IReadOnlyDictionary<string, byte[]>? files = null;
        IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
        byte[]? generation = null;
        JsonObject? ordinary = null;
        JsonObject? root = null;
        ValidationService.SpiritualOrdinaryPressureComparisonResult? result = null;
        // Reuse the accepted fixed fixture stages unchanged, including primary/secondary failure preservation.
        await ExecutePressureGroupFixtureStagesAsync(new(
            Work: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                context = await CreateCompleteConflictFrameContextAsync(playerCurrent: 3m, oppositionCurrent: 3m,
                    spiritFocusTier: 3, oppositionMaximum: 9m,
                    initializeContext: allocated => { context = allocated; return Task.CompletedTask; },
                    seedOriginalInputs: async fixture =>
                    {
                        await SeedOriginalIntakeBaselinesAsync(fixture);
                        var profiles = (await fixture.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
                        var guardian = Assert.Single(profiles["profiles"]!.AsArray(), row =>
                            row!["actorType"]!.GetValue<string>() == "guardian" &&
                            row["actorId"]!.GetValue<string>() == SpiritualGuardianAttributionTestData.GuardianId)!;
                        guardian["activeWounds"] = new JsonArray(data.Parents.Select(row => row.DeepClone()).ToArray());
                        guardian["activeEffects"] = new JsonArray(data.Effects.Select(row => row.DeepClone()).ToArray());
                        var conflict = (await fixture.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
                        conflict["activeConflict"]!["oppositionSideStrain"] = "strained";
                        // All independent joins precede the first retained write, authority composition and real signature.
                        SpiritualGuardianAttributionDataCheck.Require(data, profiles, conflict);
                        foreach (var (path, image) in new[]
                        {
                            (AfterlifeEntityProfileState.StatePath, profiles), (AfterlifeSpiritualConflictState.StatePath, conflict),
                            (WoundIdentityState.StatePath, data.WoundIdentities), (WoundHistoryState.HistoryPath, data.History),
                            (EffectIdentityState.StatePath, data.EffectIdentities), (SpiritualWoundOpportunityReceiptState.StatePath, data.Receipts),
                            (EffectCarrierCatalog.PlayerPath, new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray() }),
                            (EffectCarrierCatalog.NpcPath, new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() })
                        }) await fixture.WriteExactJsonAsync(path, image.ToJsonString());
                    },
                    captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(turn: 46, currentRealm: "Chaos Sea",
                        preGeneratedDices1d20: [12, 4], sessionId: "session_" + label, requestId: "request_" + label));
                WritePressureOwnerTiming("guardian_signed_preparation", label, started);
                lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                var optional = new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
                    WoundCarrierCatalog.AlliesPath, EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath };
                var required = PressureOrdinarySignedPaths.Append(ResourceMaterializationContract.DefinitionsPath)
                    .Append(CanonicalResourceOwnerAuthorityComposer.AuthorityPath).ToArray();
                var read = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
                    PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(required, optional));
                Assert.True(read.Success, string.Join("\n", read.Issues));
                var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
                Assert.Equal((46, "chaos_sea", "request_" + label), (signed.TurnNumber, signed.Realm, signed.RequestId));
                Assert.Equal(new[] { 12, 4 }, signed.AcceptedD20EventValues);
                foreach (var path in required.Concat(optional))
                {
                    var current = await context.FileSystem.ReadFileBytesAsync(lease, path);
                    if (current is null) Assert.Contains(path, signed.AbsentLogicalPaths);
                    else Assert.Equal(current, signed.ReadRequiredBytes(path));
                }
                JsonObject ReadSigned(string path) => SpiritualWoundStateJson.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(signed.ReadRequiredBytes(path)));
                root = ReadSigned(AfterlifeSpiritualConflictState.StatePath);
                var signedProfiles = ReadSigned(AfterlifeEntityProfileState.StatePath);
                var signedData = new RetainedPressureGroupData(data.Parents, data.Effects, ReadSigned(WoundIdentityState.StatePath),
                    ReadSigned(WoundHistoryState.HistoryPath), ReadSigned(EffectIdentityState.StatePath), ReadSigned(SpiritualWoundOpportunityReceiptState.StatePath));
                Assert.Empty(SpiritualGuardianAttributionDataCheck.Check(signedData, signedProfiles, root));
                var active = root["activeConflict"]!.AsObject();
                Assert.Empty(active["exchangeLog"]!.AsArray());
                Assert.Equal(("clear", "strained"), (active["playerSideStrain"]!.GetValue<string>(), active["oppositionSideStrain"]!.GetValue<string>()));
                ordinary = SpiritualGuardianAttributionTestData.CreateExchange("exchange_guardian_attribution_46", 46,
                    "strained", "fractured", declaration: true);
                var issues = new List<ValidationIssue>();
                ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "guardian.ordinary", issues);
                ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "guardian.ordinary", issues);
                ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
                var source = SpiritualGuardianAttributionTestData.CreateSource(ordinary, signed.SessionId, signed.RequestId,
                    signed.SnapshotToken, active["conflictId"]!.GetValue<string>(), new CanonicalBeforeImage(true,
                    signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)).Fingerprint);
                ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, source["turnEvidence"]!.AsObject(), issues);
                Assert.Empty(issues);
                var ordinaryCalculation = SpiritualWoundSourceWitness.Validate(source);
                Assert.Equal((10L, 1), (ordinaryCalculation.TraumaPressure, ordinaryCalculation.MaximumSeverityRank));
                Assert.Equal(active["oppositionSide"]!["leadContestant"]!["actorId"]!.GetValue<string>(),
                    ordinary["spiritualWoundFailedAction"]!["actorId"]!.GetValue<string>());
                Assert.Equal(ordinary["incomingAction"]!["actorId"]!.GetValue<string>(), ordinary["spiritualWoundFailedAction"]!["actorId"]!.GetValue<string>());
                active["exchangeLog"]!.AsArray().Add(ordinary.DeepClone()); active["oppositionSideStrain"] = "fractured";
                var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, raw);
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath, Encoding.UTF8.GetBytes(new JsonObject
                {
                    ["response"] = "Душа оказывает давление; Хранитель не справляется со своим встречным давлением. Противостояние продолжается.",
                    ["timestamp"] = "2026-11-07T00:00:00Z"
                }.ToJsonString()));
                AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
                AssertNoConflictFrameErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
                var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                capture = recorded.Capture; // Retain cleanup ownership before any assertion can fail.
                AssertNoConflictFrameErrors(recorded.Issues);
                Assert.NotNull(capture);
                AssertNoConflictFrameErrors(await capture.BeginResourceExecutionAsync(lease));
                var prefix = await capture.PrepareOriginalResourcePrefixAsync(lease);
                AssertNoConflictFrameErrors(prefix.Issues); Assert.True(prefix.Ready); Assert.False(prefix.PrefixBlocked);
                files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, label + "_before");
                Assert.Equal(0, frontier.NextOrdinal); Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
                var beforeCounts = capture.ReadExistingStrainReuseCounts();
                result = await ExecutePressureGroupComparisonStagesAsync(
                    () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease), () => capture.IsCurrentOwner,
                    async () =>
                    {
                        AssertPressureOrdinaryFrontiersEqual(frontier, await ReadPressureOwnerSnapshotAsync(capture, lease, label + "_query_close"));
                        AssertExistingPressureInputInert(context, lease, files, generation);
                    });
                var afterCounts = capture.ReadExistingStrainReuseCounts();
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_owner_ordinary_result",
                    issues = result.Issues.Select(issue => new
                    {
                        issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor,
                        issue.Section, issue.Expected, issue.Actual, issue.RepairHint, issue.RepairTargetFiles
                    }),
                    result.PrefixBlocked,
                    comparison = result.Comparison is not { } comparisonData ? null : new
                    {
                        comparisonData.OrdinaryExchangeJson, comparisonData.OrdinaryCalculation, comparisonData.ExpectedTransitions,
                        comparisonData.RawInputFingerprint, comparisonData.ExchangePointer
                    },
                    beforeCounts = new { beforeCounts.Queries, beforeCounts.SignedParentReads,
                        beforeCounts.OrdinaryEvaluations, beforeCounts.ActualEvaluations,
                        beforeCounts.FreshInputChecks, beforeCounts.FreshDependencyChecks },
                    afterCounts = new { afterCounts.Queries, afterCounts.SignedParentReads,
                        afterCounts.OrdinaryEvaluations, afterCounts.ActualEvaluations,
                        afterCounts.FreshInputChecks, afterCounts.FreshDependencyChecks },
                    currentOwner = capture.IsCurrentOwner,
                    rawInputFingerprint = new CanonicalBeforeImage(true, raw).Fingerprint }));
                AssertPressureOrdinaryFrontiersEqual(frontier, await ReadPressureOwnerSnapshotAsync(capture, lease, label + "_after"));
                AssertExistingPressureInputInert(context, lease, files, generation);
                Assert.True(capture.IsCurrentOwner); Assert.NotNull(result); Assert.False(result.PrefixBlocked);
                Assert.Equal(beforeCounts.Queries + 1, afterCounts.Queries);
                if (result.Comparison is null)
                {
                    // Confirm the actual first owner gate; actual Advance and its later guardian integration gate are not reached.
                    var issue = Assert.Single(result.Issues);
                    Assert.Equal("spiritual_pressure_ordinary_contour_unsupported", issue.Code);
                    Assert.Equal("one complete signed current rank-III/IV or exactly two rank-III/IV pressure strain parents", issue.Actual);
                    Assert.Equal(beforeCounts.OrdinaryEvaluations, afterCounts.OrdinaryEvaluations);
                    Assert.Equal(beforeCounts.ActualEvaluations, afterCounts.ActualEvaluations);
                    Assert.Equal(beforeCounts.SignedParentReads, afterCounts.SignedParentReads);
                }
                else
                {
                    Assert.Empty(result.Issues);
                    var comparison = result.Comparison;
                    Assert.True(JsonNode.DeepEquals(ordinary, comparison.ReadOrdinaryExchange()));
                    Assert.Equal(SpiritualWoundOpportunityMath.Calculate(new(8, 0, 0, "strained", "fractured", "hostile", 4)), comparison.OrdinaryCalculation);
                    Assert.Equal(new CanonicalBeforeImage(true, raw).Fingerprint, comparison.RawInputFingerprint);
                    Assert.Equal("/activeConflict/exchangeLog/0", comparison.ExchangePointer);
                    Assert.Equal(2, comparison.ExpectedTransitions.Count);
                    foreach (var (kind, id) in new[]
                    {
                        (ResourceOwnerKind.AfterlifeActor, "player_soul"),
                        (ResourceOwnerKind.AfterlifeConflictSide, active[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]!["opposition"]!["resourceOwnerId"]!.GetValue<string>())
                    })
                    {
                        var transition = Assert.Single(comparison.ExpectedTransitions, row => row.Coordinate.OwnerKind == kind);
                        var side = kind == ResourceOwnerKind.AfterlifeActor ? "player" : "opposition";
                        Assert.Equal(new AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition(
                            "turn_46:afterlife_conflict:exchange_guardian_attribution_46:" + side, "afterlife_cost",
                            "exchange_guardian_attribution_46", new("chaos_sea", kind, id, "spiritual_action_points"),
                            ResourceTransitionOperation.Spend, 3m, 0m, 3m, 0m), transition);
                    }
                    Assert.Equal(beforeCounts.OrdinaryEvaluations + 1, afterCounts.OrdinaryEvaluations);
                    Assert.Equal(beforeCounts.ActualEvaluations, afterCounts.ActualEvaluations);
                    Assert.Equal(beforeCounts.SignedParentReads + 1, afterCounts.SignedParentReads);
                }
            },
            HasWindow: () => frontier is not null, HasCapture: () => capture is not null,
            HasLease: () => lease is not null, HasContext: () => context is not null,
            AssertWindowInert: async () =>
            {
                if (capture!.IsCurrentOwner)
                    AssertPressureOrdinaryFrontiersEqual(frontier!, await ReadPressureOwnerSnapshotAsync(capture, lease!, label + "_cleanup"));
                AssertExistingPressureInputInert(context!, lease!, files!, generation!);
            },
            CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context!.FileSystem.GameSessionPath),
            DisposeCaptureExplicitly: () => capture!.Dispose(),
            AssertDisposedCaptureInert: () =>
            {
                if (disposalFiles is not null) AssertExistingPressureInputInert(context!, lease!, disposalFiles, generation!);
            },
            DisposeCaptureImplicitly: () => capture!.Dispose(),
            AssertNoPrivatePair: () => AssertPressureNoPrivatePair(context!, lease!),
            DisposeLease: async () => await lease!.DisposeAsync(),
            DisposeContext: async () => await context!.DisposeAsync(),
            AssertRootRemoved: () => Assert.False(Directory.Exists(context!.RootPath))));
        Assert.True(result?.Comparison is not null && result.Issues.Count == 0,
            "Expected complete guardian ordinary comparison after lawful DATA, signed/current preflight and genuine closed prefix. " +
            "Current SOURCE stops at its pressure/setback/player-only comparison capture: " + string.Join("; ", result?.Issues.Select(issue =>
                issue.Code + ": " + issue.Actual) ?? Array.Empty<string>()));
    }
}
