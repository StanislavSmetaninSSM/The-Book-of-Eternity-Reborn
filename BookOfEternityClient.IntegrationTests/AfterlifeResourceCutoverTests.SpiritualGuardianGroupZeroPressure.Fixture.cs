using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>One closed low-margin N2 DATA input, independently authored before genuine signing; no live capability is retained.</summary>
    /// <param name="Fixture">One invocation-local complete low-margin DATA graph; mutable candidates never cross cases.</param>
    private sealed record GuardianGroupZeroPressureData(GuardianGroupPressureData Fixture)
    {
        /// <summary>Gets all retained III+IV parents and complete independent ancestry.</summary>
        internal RetainedPressureGroupData Retained => Fixture.Retained;
        /// <summary>Gets the independently authored ordinaryII exchange.</summary>
        internal JsonObject Ordinary => Fixture.Ordinary;
        /// <summary>Gets the independently authored actualIV exchange.</summary>
        internal JsonObject Actual => Fixture.Actual;
        /// <summary>Gets the guardian resilience fixed in both original profile and lead snapshot before signing.</summary>
        internal int Resilience => 5;
        /// <summary>Gets the independent ordinary pressure−4/max0 literal.</summary>
        internal SpiritualWoundCalculation OrdinaryCalculation => new(new(4, 0, 5, "strained", "fractured", "hostile", 4), 1, 2, 0, -4, 0, 2, 4, true, 0);
        /// <summary>Gets the independent actual pressure6/max0 literal.</summary>
        internal SpiritualWoundCalculation ActualCalculation => new(new(4, 0, 5, "strained", "broken", "hostile", 4), 1, 4, 2, 6, 0, 4, 4, true, 0);
    }

    /// <summary>One independently signed guardian-zero window; all live capabilities remain confined to its test and cleanup stages.</summary>
    /// <param name="Name">The exact original session/request label.</param>
    /// <param name="Data">The closed ordinary/actual/all-parent DATA for this case.</param>
    /// <param name="Context">The invocation-owned physical fixture.</param>
    /// <param name="Lease">The current physical canonical write lease.</param>
    /// <param name="Capture">The real owner issued by the selected validator.</param>
    /// <param name="Frontier">The zero-ordinal snapshot before ordinary comparison.</param>
    /// <param name="OrdinaryRoot">The complete ordinary root authored only after signing.</param>
    /// <param name="RawOrdinaryBytes">Exact BOM/whitespace-sensitive ordinary bytes.</param>
    /// <param name="Files">Complete pre-operation physical file inventory.</param>
    /// <param name="Generation">Exact original generation bytes.</param>
    /// <param name="ExpectedProofJson">Once-built canonical all-parent expected DATA, not live permission.</param>
    /// <param name="CapturePhase">The warm or cold ownership label for complete diagnostics.</param>
    /// <param name="SignedSnapshotFingerprint">The real capture reader's verified complete signed-original fingerprint.</param>
    private sealed record GuardianGroupZeroPressureWindow(string Name, GuardianGroupZeroPressureData Data,
        ResourceMaterializationTestContext Context, FileSystemManager.CanonicalWriteLease Lease,
        ValidationService.SpiritualOriginalTurnCapture Capture, ValidationService.SpiritualOrdinaryPressureProbeState Frontier,
        JsonObject OrdinaryRoot, byte[] RawOrdinaryBytes, IReadOnlyDictionary<string, byte[]> Files, byte[] Generation, string ExpectedProofJson, string CapturePhase, string SignedSnapshotFingerprint)
    {
        /// <summary>Gets the independently expected complete actual root without changing the physically authored ordinary bytes.</summary>
        internal JsonObject ActualRoot
        {
            get
            {
                var actual = OrdinaryRoot.DeepClone().AsObject();
                actual["activeConflict"]!["exchangeLog"]![0] = Data.Actual.DeepClone();
                actual["activeConflict"]!["oppositionSideStrain"] = "broken";
                return actual;
            }
        }
    }

    /// <summary>Detached signed/authored setup shared only by serial, independently disposed capture phases of this invocation.</summary>
    /// <param name="Name">The original request/session label.</param>
    /// <param name="Data">This invocation's closed low-margin N2 DATA.</param>
    /// <param name="Context">The owned genuine fixture, disposed by the fixed outer stages.</param>
    /// <param name="OrdinaryRoot">The complete ordinary candidate built over the signed original root.</param>
    /// <param name="RawOrdinaryBytes">Exact post-sign ordinary bytes.</param>
    /// <param name="OriginalConflictBytes">The signed zero-exchange original bytes.</param>
    /// <param name="OriginalReceiptBytes">The signed complete ancestry receipt bytes.</param>
    /// <param name="OriginalHistoryBytes">The signed complete ancestry History bytes.</param>
    /// <param name="Authored">Exact authored images and absences checked again by every owner.</param>
    /// <param name="OriginalProfile">The signed guardian profile with resilience5.</param>
    /// <param name="OriginalConflict">The complete independently signed original conflict.</param>
    /// <param name="OrdinarySource">Comparison-free ordinary−4/max0 prerequisite DATA.</param>
    /// <param name="ActualOnly">Separate comparison-free actual6/max0 prerequisite DATA.</param>
    /// <param name="ExpectedProofJson">The once-built complete expected v7 proof as immutable text.</param>
    /// <param name="AuthoredFiles">Complete detached pre-owner physical file inventory captured at the end of authoring.</param>
    /// <param name="AuthoredGeneration">Exact generation bytes captured with the complete pre-owner inventory.</param>
    private sealed record GuardianGroupZeroPressureSetup(string Name, GuardianGroupZeroPressureData Data,
        ResourceMaterializationTestContext Context, JsonObject OrdinaryRoot, byte[] RawOrdinaryBytes,
        byte[] OriginalConflictBytes, byte[] OriginalReceiptBytes, byte[] OriginalHistoryBytes,
        IReadOnlyDictionary<string, byte[]?> Authored, JsonObject OriginalProfile, JsonObject OriginalConflict,
        JsonObject OrdinarySource, JsonObject ActualOnly, string ExpectedProofJson,
        IReadOnlyDictionary<string, byte[]> AuthoredFiles, byte[] AuthoredGeneration);

    /// <summary>Owns the unchanged pre-sign authoring and final context cleanup while serial warm/cold captures dispose independently.</summary>
    /// <param name="name">Independent original session/request label.</param>
    /// <param name="data">Closed low-margin N2 originals fixed before signing.</param>
    /// <param name="work">Serial work on this invocation's exact unchanged ordinary bytes.</param>
    private async Task WithGuardianGroupZeroPressureSetupAsync(string name, GuardianGroupZeroPressureData data,
        Func<GuardianGroupZeroPressureSetup, Task> work)
    {
        ResourceMaterializationTestContext? context = null;
        await ExecuteGuardianZeroFixtureStagesAsync(name, "guardian_group_zero_owner_failure_after_cleanup", new(
            Work: async () =>
            {
                context = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_signed_preparation", name,
                    () => CreateCompleteConflictFrameContextAsync(playerCurrent: 3m, oppositionCurrent: 3m,
                    spiritFocusTier: 3, oppositionMaximum: 9m,
                    initializeContext: allocated => { context = allocated; return Task.CompletedTask; },
                    seedOriginalInputs: async fixture =>
                    {
                        await SeedOriginalIntakeBaselinesAsync(fixture);
                        var profiles = (await fixture.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
                        var guardian = ReadGuardianAttributionProfile(profiles);
                        foreach (var art in AfterlifeEntityProfileState.StandardArtIds) guardian["standardArts"]![art] = 0;
                        guardian["standardArts"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId] = data.Resilience;
                        guardian["activeWounds"] = new JsonArray(data.Retained.Parents.Select(row => row.DeepClone()).ToArray());
                        guardian["activeEffects"] = new JsonArray(data.Retained.Effects.Select(row => row.DeepClone()).ToArray());
                        var conflict = (await fixture.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
                        var active = conflict["activeConflict"]!.AsObject();
                        active["oppositionSideStrain"] = "strained";
                        active["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"] = guardian["standardArts"]!.DeepClone();
                        Assert.Equal(data.Resilience, active["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!
                            [AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>());
                        RequireGuardianJourneyIssues(name, "group_original_graph", SpiritualGuardianAttributionDataCheck.CheckGroup(data.Retained, profiles, conflict, [3, 4]));
                        foreach (var (path, image) in new[]
                        {
                            (AfterlifeEntityProfileState.StatePath, profiles), (AfterlifeSpiritualConflictState.StatePath, conflict),
                            (WoundIdentityState.StatePath, data.Retained.WoundIdentities), (WoundHistoryState.HistoryPath, data.Retained.History),
                            (EffectIdentityState.StatePath, data.Retained.EffectIdentities), (SpiritualWoundOpportunityReceiptState.StatePath, data.Retained.Receipts),
                            (EffectCarrierCatalog.PlayerPath, new JsonObject { ["schemaVersion"] = 1, ["activeEffects"] = new JsonArray() }),
                            (EffectCarrierCatalog.NpcPath, new JsonObject { ["schemaVersion"] = 1, ["entries"] = new JsonArray() })
                        }) await fixture.WriteExactJsonAsync(path, image.ToJsonString());
                    },
                    captureOriginalSnapshot: fixture => fixture.CaptureValidatedPendingSnapshotAsync(turn: 46,
                        currentRealm: "Chaos Sea", preGeneratedDices1d20: [8, 4], sessionId: "session_" + name,
                        requestId: "request_" + name)));
                var current = Assert.IsType<ResourceMaterializationTestContext>(context);
                GuardianGroupZeroPressureSetup? setup = null;
                await WithGuardianGroupZeroAuthoringLeaseAsync(current, name, async lease =>
                {
                    var signed = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_signed_read", name,
                        () => ReadGuardianGroupZeroSignedInputsAsync(current, lease, name, data.Retained));
                    var original = ReadGuardianAttributionJson(signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath));
                    var signedProfiles = ReadGuardianAttributionJson(signed.ReadRequiredBytes(AfterlifeEntityProfileState.StatePath));
                    var signedGuardian = ReadGuardianAttributionProfile(signedProfiles);
                    foreach (var art in AfterlifeEntityProfileState.StandardArtIds)
                        Assert.Equal(art == AfterlifeSpiritualConflictState.SpiritualResilienceArtId ? 5 : 0,
                            signedGuardian["standardArts"]![art]!.GetValue<int>());
                    Assert.Equal(data.Resilience, signedGuardian["standardArts"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>());
                    Assert.True(JsonNode.DeepEquals(signedGuardian["standardArts"], original["activeConflict"]!["oppositionSide"]!
                        ["leadContestant"]!["actorArtTierSnapshot"]));
                    Assert.Equal(("direct_duel", "hostile"), (original["activeConflict"]!["sideModel"]!.GetValue<string>(),
                        original["activeConflict"]!["dangerMode"]!.GetValue<string>()));
                    var signedSoul = ReadGuardianAttributionJson(signed.ReadRequiredBytes("game_state/meta/soul_state.json"));
                    Assert.Equal(0, signedSoul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!["pressure"]!.GetValue<int>());
                    foreach (var (path, expected) in new[]
                    {
                        (WoundIdentityState.StatePath, data.Retained.WoundIdentities), (WoundHistoryState.HistoryPath, data.Retained.History),
                        (EffectIdentityState.StatePath, data.Retained.EffectIdentities), (SpiritualWoundOpportunityReceiptState.StatePath, data.Retained.Receipts)
                    }) Assert.True(JsonNode.DeepEquals(expected, ReadGuardianAttributionJson(signed.ReadRequiredBytes(path))), path);
                    Assert.Equal("strained", original["activeConflict"]!["oppositionSideStrain"]!.GetValue<string>());
                    Assert.Empty(original["activeConflict"]!["exchangeLog"]!.AsArray());
                    var ordinary = data.Ordinary.DeepClone().AsObject();
                    var ordinarySource = SpiritualGuardianAttributionTestData.CreateSource(ordinary, signed.SessionId, signed.RequestId, signed.SnapshotToken,
                        original["activeConflict"]!["conflictId"]!.GetValue<string>(),
                        new CanonicalBeforeImage(true, signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)).Fingerprint, data.Resilience);
                    var issues = new List<ValidationIssue>();
                    ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "guardianGroupZero.owner.ordinary", issues);
                    ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "guardianGroupZero.owner.ordinary", issues);
                    ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, ordinarySource["turnEvidence"]!.AsObject(), issues);
                    ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
                    RequireGuardianJourneyIssues(name, "guardian_group_zero_authored_ordinary_rules_dice", issues);
                    Assert.Equal(data.OrdinaryCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var noComparison));
                    Assert.Null(noComparison);
                    var actualOnly = SpiritualGuardianAttributionTestData.CreateSource(data.Actual, signed.SessionId, signed.RequestId, signed.SnapshotToken,
                        original["activeConflict"]!["conflictId"]!.GetValue<string>(),
                        new CanonicalBeforeImage(true, signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)).Fingerprint, data.Resilience);
                    ValidationService.ValidateRetainedSpiritualExchangeShape(data.Actual, "guardianGroupZero.owner.actual", issues);
                    ValidationService.ValidateSpiritualWoundSourceActionShape(data.Actual, "guardianGroupZero.owner.actual", issues);
                    ValidationService.ValidateRetainedSpiritualDiceAudit(data.Actual, actualOnly["turnEvidence"]!.AsObject(), issues);
                    ValidationService.ValidateRetainedSpiritualExchangeRules(data.Actual, issues);
                    RequireGuardianJourneyIssues(name, "group_zero_comparison_free_actual_rules_dice", issues);
                    Assert.False(ordinarySource.ContainsKey("strainPreimage")); Assert.False(actualOnly.ContainsKey("strainPreimage"));
                    Assert.Equal(data.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(actualOnly, out noComparison));
                    Assert.Null(noComparison);
                    var root = original.DeepClone().AsObject();
                    root["activeConflict"]!["exchangeLog"]!.AsArray().Add(ordinary);
                    root["activeConflict"]!["oppositionSideStrain"] = "fractured";
                    var raw = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(" \n" + root.ToJsonString() + "\n")).ToArray();
                    await current.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, raw);
                    await current.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath, Encoding.UTF8.GetBytes(new JsonObject
                    {
                        ["response"] = "Душа оказывает давление; Хранитель не справляется со своим встречным давлением. Противостояние продолжается.",
                        ["timestamp"] = "2026-11-09T00:00:00Z"
                    }.ToJsonString()));
                    RequireGuardianJourneyIssues(name, "guardian_group_zero_location_preflight",
                        await current.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
                    RequireGuardianJourneyIssues(name, "guardian_group_zero_item_preflight",
                        await current.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
                    var authored = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                    foreach (var path in PressureAuthoredDraftPaths)
                        authored[path] = await current.FileSystem.ReadFileBytesAsync(lease, path);
                    Assert.Equal(raw, authored[AfterlifeSpiritualConflictState.StatePath]);
                    Assert.NotEqual(new CanonicalBeforeImage(true, raw).Fingerprint,
                        new CanonicalBeforeImage(true, Encoding.UTF8.GetBytes(root.ToJsonString())).Fingerprint);
                    _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_signed_original_prerequisites", name,
                        signed.SessionId, signed.RequestId, signed.SnapshotToken, signed.TurnNumber, signed.AcceptedD20EventValues,
                        originalConflictBase64 = Convert.ToBase64String(signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)),
                        originalProfilesBase64 = Convert.ToBase64String(signed.ReadRequiredBytes(AfterlifeEntityProfileState.StatePath)),
                        data.Retained, ordinarySource, actualOnly, rawOrdinaryBase64 = Convert.ToBase64String(raw) }));
                    var expectedProofJson = CreateGuardianGroupZeroExpectedProof(data, raw).ToJsonString();
                    var authoredFiles = CaptureExactSpiritualFiles(current.FileSystem.GameSessionPath);
                    var authoredGeneration = File.ReadAllBytes(current.FileSystem.SessionGenerationPath);
                    setup = new(name, data, current, root, raw,
                        signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath),
                        signed.ReadRequiredBytes(SpiritualWoundOpportunityReceiptState.StatePath),
                        signed.ReadRequiredBytes(WoundHistoryState.HistoryPath), authored,
                        signedGuardian, original, ordinarySource, actualOnly, expectedProofJson, authoredFiles, authoredGeneration);
                });
                await work(Assert.IsType<GuardianGroupZeroPressureSetup>(setup));
            },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => false, HasContext: () => context is not null,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { }, AssertNoPrivatePair: () => { },
            DisposeLease: () => ValueTask.CompletedTask,
            DisposeContext: async () =>
            {
                await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_owned_cleanup", name,
                    async () => await context!.DisposeAsync());
            },
            AssertRootRemoved: () => Assert.False(Directory.Exists(context!.RootPath))));
    }

    /// <summary>Closes the authoring lease before any capture begins, exposing every retained cleanup error without changing shared stages.</summary>
    /// <param name="context">The exact independent signed fixture.</param>
    /// <param name="name">Original fixture label for complete diagnostics.</param>
    /// <param name="work">Unchanged preflight and ordinary authoring under this real lease.</param>
    private async Task WithGuardianGroupZeroAuthoringLeaseAsync(ResourceMaterializationTestContext context, string name,
        Func<FileSystemManager.CanonicalWriteLease, Task> work)
    {
        FileSystemManager.CanonicalWriteLease? lease = null;
        await ExecuteGuardianZeroFixtureStagesAsync(name, "guardian_group_zero_authoring_failure_after_cleanup", new(
            Work: async () => { lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync(); await work(lease); },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { },
            AssertNoPrivatePair: () => AssertPressureNoPrivatePair(context, lease!),
            DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        Assert.False(lease!.IsActive);
    }

    /// <summary>Owns one genuine intake/resource/actual capture and lease; no live capability crosses its completed disposal.</summary>
    /// <param name="setup">Original signed images and identical authored ordinary bytes.</param>
    /// <param name="validator">The exact validator for this independently owned phase.</param>
    /// <param name="phase">Diagnostic warm/cold/core label, never an authority selector.</param>
    /// <param name="work">Current core body or separately released source-only work.</param>
    private async Task WithGuardianGroupZeroPressureCaptureAsync(GuardianGroupZeroPressureSetup setup, ValidationService validator,
        string phase, Func<GuardianGroupZeroPressureWindow, Task> work)
    {
        var name = setup.Name; var data = setup.Data; var context = setup.Context;
        var raw = setup.RawOrdinaryBytes; var root = setup.OrdinaryRoot;
        var signedGuardian = setup.OriginalProfile; var original = setup.OriginalConflict;
        var ordinarySource = setup.OrdinarySource; var actualOnly = setup.ActualOnly;
        FileSystemManager.CanonicalWriteLease? lease = null;
        ValidationService.SpiritualOriginalTurnCapture? capture = null;
        GuardianGroupZeroPressureWindow? window = null;
        IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
        await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_capture_" + phase, name, () =>
            ExecuteGuardianZeroFixtureStagesAsync(name, "guardian_group_zero_capture_failure_after_cleanup_" + phase, new(
            Work: async () =>
            {
                lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                AssertPressureZeroCaptureInert(context, lease, setup.AuthoredFiles, setup.AuthoredGeneration, expectedPublication: null);
                foreach (var pair in setup.Authored)
                    Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                if (!ReferenceEquals(validator, context.Validator))
                {
                    RequireGuardianJourneyIssues(name, phase + "_location_preflight",
                        await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
                    RequireGuardianJourneyIssues(name, phase + "_item_preflight",
                        await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
                }
                var intake = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_intake_" + phase, name, async () =>
                {
                    var allocated = await validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
                    capture = allocated.Capture; // Retain ownership before the safe timer or any assertion can fail.
                    return allocated;
                });
                RequireGuardianJourneyIssues(name, "guardian_group_zero_intake", intake.Issues); Assert.NotNull(capture);
                RequireGuardianJourneyIssues(name, "guardian_group_zero_resource_begin", await MeasureGuardianGroupZeroPhaseAsync(
                    "guardian_group_zero_resource_begin_" + phase, name, () => capture.BeginResourceExecutionAsync(lease)));
                var prepared = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_original_prefix", name,
                    () => capture.PrepareOriginalResourcePrefixAsync(lease));
                RequireGuardianJourneyIssues(name, "guardian_group_zero_original_prefix", prepared.Issues);
                Assert.True(prepared.Ready); Assert.False(prepared.PrefixBlocked);
                AssertPressureZeroCaptureInert(context, lease, setup.AuthoredFiles, setup.AuthoredGeneration, expectedPublication: null);
                var signedOrigin = capture.ReadVerifiedSignedC1Origin(lease);
                Assert.Equal(setup.OriginalConflictBytes, signedOrigin.Conflict.Bytes);
                Assert.Equal(setup.OriginalReceiptBytes, signedOrigin.Receipt.Bytes);
                var frontier = await ReadGuardianGroupZeroSnapshotAsync(capture, lease, name + "_before");
                Assert.Equal(0, frontier.NextOrdinal); Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
                Assert.Equal(raw, setup.AuthoredFiles[AfterlifeSpiritualConflictState.StatePath]);
                window = new(name, data, context, lease, capture, frontier, root, raw, setup.AuthoredFiles, setup.AuthoredGeneration, setup.ExpectedProofJson, phase, signedOrigin.SnapshotFingerprint);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_signed_current_prerequisites", name,
                    capturePhase = phase, parentRanks = new[] { 3, 4 }, data.Resilience, expectedOrdinary = data.OrdinaryCalculation, expectedActual = data.ActualCalculation,
                    signedOrigin.SnapshotFingerprint, originalProfile = signedGuardian, originalConflict = original,
                    historicalReceipt = data.Retained.Receipts, historicalHistory = data.Retained.History,
                    rawOrdinaryBytesBase64 = Convert.ToBase64String(raw), ordinarySource, actualOnly,
                    counters = ReadGroupZeroMeasurementCounters(capture) }));
                await work(window);
            },
            // A complete authored baseline already exists. Partial intake/begin/prefix/frontier failures must close it too.
            HasWindow: () => lease is not null, HasCapture: () => capture is not null,
            HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () =>
            {
                AssertPressureZeroCaptureInert(context, lease!, setup.AuthoredFiles, setup.AuthoredGeneration, expectedPublication: null);
                return Task.CompletedTask;
            },
            CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath),
            DisposeCaptureExplicitly: () => capture?.Dispose(),
            AssertDisposedCaptureInert: () =>
            {
                if (capture is not null) Assert.False(capture.IsCurrentOwner);
                if (disposalFiles is not null)
                    AssertPressureZeroCaptureInert(context, lease!, disposalFiles, setup.AuthoredGeneration, expectedPublication: null);
                AssertPressureZeroCaptureInert(context, lease!, setup.AuthoredFiles, setup.AuthoredGeneration, expectedPublication: null);
            },
            DisposeCaptureImplicitly: () => capture!.Dispose(),
            AssertNoPrivatePair: () =>
            {
                AssertPressureZeroCapturePublicationState(context, lease!, expectedPublication: null);
                AssertPressureZeroCaptureInert(context, lease!, setup.AuthoredFiles, setup.AuthoredGeneration, expectedPublication: null);
            },
            DisposeLease: async () =>
            {
                await lease!.DisposeAsync();
                AssertGuardianGroupReceiptExactFiles(context, setup.AuthoredFiles, setup.AuthoredGeneration);
            }, DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { })));
        Assert.False(capture!.IsCurrentOwner); Assert.False(lease!.IsActive);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_group_zero_capture_scope_closed", name, capturePhase = phase,
            captureCurrent = capture.IsCurrentOwner, leaseActive = lease.IsActive }));
    }

    /// <summary>Authenticates exact signed/current inputs, retained guardian graph and participant ownership before authoring.</summary>
    /// <param name="context">This invocation's genuine owned context.</param>
    /// <param name="lease">Its active canonical lease.</param>
    /// <param name="name">Expected request identity.</param>
    /// <param name="data">Independent pre-sign authored graph.</param>
    /// <returns>The genuine typed signed reader after exact physical and all-parent graph checks.</returns>
    private async Task<PendingTurnSnapshotReadAuthority> ReadGuardianGroupZeroSignedInputsAsync(
        ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease, string name,
        RetainedPressureGroupData data)
    {
        var optional = new[] { WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
            WoundCarrierCatalog.AlliesPath, EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath };
        var required = PressureOrdinarySignedPaths.Append(ResourceMaterializationContract.DefinitionsPath)
            .Append(CanonicalResourceOwnerAuthorityComposer.AuthorityPath).ToArray();
        var read = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(required, optional));
        RequireGuardianJourneyIssues(name, "signed_input_read", read.Issues);
        Assert.True(read.Success, string.Join("\n", read.Issues));
        var signed = Assert.IsType<PendingTurnSnapshotReadAuthority>(read.Snapshot);
        Assert.Equal((46, "chaos_sea", "session_" + name, "request_" + name),
            (signed.TurnNumber, signed.Realm, signed.SessionId, signed.RequestId));
        Assert.Equal(new[] { 8, 4 }, signed.AcceptedD20EventValues);
        foreach (var path in required.Concat(optional))
        {
            var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
            if (bytes is null) Assert.Contains(path, signed.AbsentLogicalPaths);
            else Assert.Equal(bytes, signed.ReadRequiredBytes(path));
        }
        JsonObject Read(string path) => ReadGuardianAttributionJson(signed.ReadRequiredBytes(path));
        var signedData = new RetainedPressureGroupData(data.Parents, data.Effects, Read(WoundIdentityState.StatePath),
            Read(WoundHistoryState.HistoryPath), Read(EffectIdentityState.StatePath), Read(SpiritualWoundOpportunityReceiptState.StatePath));
        RequireGuardianJourneyIssues(name, "group_zero_signed_current_graph", SpiritualGuardianAttributionDataCheck.CheckGroup(signedData,
            Read(AfterlifeEntityProfileState.StatePath), Read(AfterlifeSpiritualConflictState.StatePath), [3, 4]));
        var conflict = Read(AfterlifeSpiritualConflictState.StatePath)["activeConflict"]!.AsObject();
        Assert.Equal(SpiritualGuardianAttributionTestData.GuardianId,
            conflict["oppositionSide"]!["leadContestant"]!["actorId"]!.GetValue<string>());
        var balance = AfterlifeConflictActionPointProjectionService.Resolve(
            SpiritualWoundStateJson.DecodeUtf8JsonText(signed.ReadRequiredBytes(ResourceMaterializationContract.DefinitionsPath)),
            SpiritualWoundStateJson.DecodeUtf8JsonText(signed.ReadRequiredBytes(ResourceMaterializationContract.StatePath)), conflict);
        RequireGuardianJourneyIssues(name, "signed_ap_projection", balance.Issues);
        Assert.Equal((3m, 10m, 3m, 9m), (balance.Projection!.Player.Current, balance.Projection.Player.Maximum,
            balance.Projection.Opposition.Current, balance.Projection.Opposition.Maximum));
        foreach (var field in new[] { "instances", "closures", "sources", "decisions" })
            Assert.Equal(2, signedData.Receipts[field]!.AsArray().Count);
        return signed;
    }

}
