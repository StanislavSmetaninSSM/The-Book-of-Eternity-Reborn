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
    /// <summary>One independently signed guardian-zero window; all live capabilities remain confined to its test and cleanup stages.</summary>
    private sealed record GuardianZeroPressureWindow(string Name, GuardianZeroPressureData Data,
        ResourceMaterializationTestContext Context, FileSystemManager.CanonicalWriteLease Lease,
        ValidationService.SpiritualOriginalTurnCapture Capture, ValidationService.SpiritualOrdinaryPressureProbeState Frontier,
        JsonObject OrdinaryRoot, byte[] RawOrdinaryBytes, IReadOnlyDictionary<string, byte[]> Files, byte[] Generation)
    {
        /// <summary>Gets the independently expected complete actual root without changing the physically authored ordinary bytes.</summary>
        internal JsonObject ActualRoot
        {
            get
            {
                var actual = OrdinaryRoot.DeepClone().AsObject();
                actual["activeConflict"]!["exchangeLog"]![0] = Data.Actual.DeepClone();
                actual["activeConflict"]!["oppositionSideStrain"] = "overwhelmed";
                return actual;
            }
        }
    }

    /// <summary>Detached signed/authored setup shared only by serial, independently disposed capture phases of this invocation.</summary>
    private sealed record GuardianZeroPressureSetup(string Name, GuardianZeroPressureData Data,
        ResourceMaterializationTestContext Context, JsonObject OrdinaryRoot, byte[] RawOrdinaryBytes,
        byte[] OriginalConflictBytes, byte[] OriginalReceiptBytes, byte[] OriginalHistoryBytes,
        IReadOnlyDictionary<string, byte[]?> Authored, JsonObject OriginalProfile, JsonObject OriginalConflict,
        JsonObject OrdinarySource, JsonObject ActualOnly);

    /// <summary>Preserves the existing core owner body while separating signed setup from its genuine capture/lease lifetime.</summary>
    /// <param name="name">Unchanged independent session/request label.</param>
    /// <param name="data">Unchanged closed primary or sentinel originals.</param>
    /// <param name="work">The existing core assertion body, with no decision or publication.</param>
    private Task WithGuardianZeroPressureOwnerAsync(string name, GuardianZeroPressureData data,
        Func<GuardianZeroPressureWindow, Task> work) =>
        WithGuardianZeroPressureSetupAsync(name, data, setup =>
            WithGuardianZeroPressureCaptureAsync(setup, setup.Context.Validator, "owner", work));

    /// <summary>Owns the unchanged pre-sign authoring and final context cleanup while serial warm/cold captures dispose independently.</summary>
    /// <param name="name">Independent original session/request label.</param>
    /// <param name="data">Closed primary or sentinel inputs fixed before signing.</param>
    /// <param name="work">Serial work on this invocation's exact unchanged ordinary bytes.</param>
    /// <param name="hooks">Optional real filesystem observation for the dedicated pre-take control; default behavior is unchanged.</param>
    /// <param name="observeSignedOrigin">Optional observation under the real authoring lease, after signed validation and before any authored input.</param>
    private async Task WithGuardianZeroPressureSetupAsync(string name, GuardianZeroPressureData data,
        Func<GuardianZeroPressureSetup, Task> work, FileSystemManagerHooks? hooks = null,
        Func<ResourceMaterializationTestContext, FileSystemManager.CanonicalWriteLease, Task>? observeSignedOrigin = null)
    {
        ResourceMaterializationTestContext? context = null;
        await ExecuteGuardianZeroFixtureStagesAsync(name, "guardian_zero_owner_failure_after_cleanup", new(
            Work: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                context = await CreateCompleteConflictFrameContextAsync(hooks: hooks, playerCurrent: 3m, oppositionCurrent: 3m,
                    spiritFocusTier: 3, oppositionMaximum: 9m,
                    initializeContext: allocated => { context = allocated; return Task.CompletedTask; },
                    seedOriginalInputs: async fixture =>
                    {
                        await SeedOriginalIntakeBaselinesAsync(fixture);
                        var profiles = (await fixture.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
                        var guardian = ReadGuardianAttributionProfile(profiles);
                        guardian["standardArts"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId] = data.Resilience;
                        guardian["activeWounds"] = new JsonArray(data.Retained.Parents.Select(row => row.DeepClone()).ToArray());
                        guardian["activeEffects"] = new JsonArray(data.Retained.Effects.Select(row => row.DeepClone()).ToArray());
                        var conflict = (await fixture.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
                        var active = conflict["activeConflict"]!.AsObject();
                        active["oppositionSideStrain"] = "strained";
                        active["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"] = guardian["standardArts"]!.DeepClone();
                        Assert.Equal(data.Resilience, active["oppositionSide"]!["leadContestant"]!["actorArtTierSnapshot"]!
                            [AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>());
                        SpiritualGuardianAttributionDataCheck.Require(data.Retained, profiles, conflict, data.Rank);
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
                        currentRealm: "Chaos Sea", preGeneratedDices1d20: [12, 4], sessionId: "session_" + name,
                        requestId: "request_" + name));
                WritePressureOwnerTiming("guardian_zero_signed_preparation", name, started);
                var current = Assert.IsType<ResourceMaterializationTestContext>(context);
                GuardianZeroPressureSetup? setup = null;
                await WithGuardianZeroAuthoringLeaseAsync(current, name, async lease =>
                {
                    var signed = await ReadGuardianAttributionSignedInputsAsync(current, lease, name, data.Retained, data.Rank);
                    if (observeSignedOrigin is not null) await observeSignedOrigin(current, lease);
                    var original = ReadGuardianAttributionJson(signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath));
                    var signedProfiles = ReadGuardianAttributionJson(signed.ReadRequiredBytes(AfterlifeEntityProfileState.StatePath));
                    var signedGuardian = ReadGuardianAttributionProfile(signedProfiles);
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
                    var ordinarySource = SpiritualGuardianZeroPressureTestData.CreateComparisonFreeSource(ordinary, data.Resilience,
                        signed.SessionId, signed.RequestId, signed.SnapshotToken, original["activeConflict"]!["conflictId"]!.GetValue<string>(),
                        new CanonicalBeforeImage(true, signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)).Fingerprint);
                    var issues = new List<ValidationIssue>();
                    ValidationService.ValidateRetainedSpiritualExchangeShape(ordinary, "guardianZero.owner.ordinary", issues);
                    ValidationService.ValidateSpiritualWoundSourceActionShape(ordinary, "guardianZero.owner.ordinary", issues);
                    ValidationService.ValidateRetainedSpiritualDiceAudit(ordinary, ordinarySource["turnEvidence"]!.AsObject(), issues);
                    ValidationService.ValidateRetainedSpiritualExchangeRules(ordinary, issues);
                    RequireGuardianJourneyIssues(name, "guardian_zero_authored_ordinary_rules_dice", issues);
                    Assert.Equal(data.OrdinaryCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(ordinarySource, out var noComparison));
                    Assert.Null(noComparison);
                    var actualOnly = SpiritualGuardianZeroPressureTestData.CreateComparisonFreeSource(data.Actual, data.Resilience,
                        signed.SessionId, signed.RequestId, signed.SnapshotToken, original["activeConflict"]!["conflictId"]!.GetValue<string>(),
                        new CanonicalBeforeImage(true, signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath)).Fingerprint);
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
                    RequireGuardianJourneyIssues(name, "guardian_zero_location_preflight",
                        await current.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
                    RequireGuardianJourneyIssues(name, "guardian_zero_item_preflight",
                        await current.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
                    var authored = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
                    foreach (var path in PressureAuthoredDraftPaths)
                        authored[path] = await current.FileSystem.ReadFileBytesAsync(lease, path);
                    Assert.Equal(raw, authored[AfterlifeSpiritualConflictState.StatePath]);
                    setup = new(name, data, current, root, raw,
                        signed.ReadRequiredBytes(AfterlifeSpiritualConflictState.StatePath),
                        signed.ReadRequiredBytes(SpiritualWoundOpportunityReceiptState.StatePath),
                        signed.ReadRequiredBytes(WoundHistoryState.HistoryPath), authored,
                        signedGuardian, original, ordinarySource, actualOnly);
                });
                await work(Assert.IsType<GuardianZeroPressureSetup>(setup));
            },
            HasWindow: () => false, HasCapture: () => false, HasLease: () => false, HasContext: () => context is not null,
            AssertWindowInert: () => Task.CompletedTask, CaptureDisposalFiles: () => { }, DisposeCaptureExplicitly: () => { },
            AssertDisposedCaptureInert: () => { }, DisposeCaptureImplicitly: () => { }, AssertNoPrivatePair: () => { },
            DisposeLease: () => ValueTask.CompletedTask,
            DisposeContext: async () =>
            {
                var started = Stopwatch.GetTimestamp();
                await context!.DisposeAsync();
                WritePressureOwnerTiming("guardian_zero_owned_cleanup", name, started);
            },
            AssertRootRemoved: () => Assert.False(Directory.Exists(context!.RootPath))));
    }

    /// <summary>Closes the authoring lease before any capture begins, exposing every retained cleanup error without changing shared stages.</summary>
    /// <param name="context">The exact independent signed fixture.</param>
    /// <param name="name">Original fixture label for complete diagnostics.</param>
    /// <param name="work">Unchanged preflight and ordinary authoring under this real lease.</param>
    private async Task WithGuardianZeroAuthoringLeaseAsync(ResourceMaterializationTestContext context, string name,
        Func<FileSystemManager.CanonicalWriteLease, Task> work)
    {
        FileSystemManager.CanonicalWriteLease? lease = null;
        await ExecuteGuardianZeroFixtureStagesAsync(name, "guardian_zero_authoring_failure_after_cleanup", new(
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
    /// <param name="expectedPublication">Exact authority retained only after a normal caller successfully registers it; null keeps strict unregistered cleanup.</param>
    private async Task WithGuardianZeroPressureCaptureAsync(GuardianZeroPressureSetup setup, ValidationService validator,
        string phase, Func<GuardianZeroPressureWindow, Task> work,
        Func<ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority?>? expectedPublication = null)
    {
        var name = setup.Name; var data = setup.Data; var context = setup.Context;
        var raw = setup.RawOrdinaryBytes; var root = setup.OrdinaryRoot;
        var signedGuardian = setup.OriginalProfile; var original = setup.OriginalConflict;
        var ordinarySource = setup.OrdinarySource; var actualOnly = setup.ActualOnly;
        FileSystemManager.CanonicalWriteLease? lease = null;
        ValidationService.SpiritualOriginalTurnCapture? capture = null;
        GuardianZeroPressureWindow? window = null;
        IReadOnlyDictionary<string, byte[]>? disposalFiles = null;
        byte[]? generation = null;
        var phaseStarted = Stopwatch.GetTimestamp();
        await ExecuteGuardianZeroFixtureStagesAsync(name, "guardian_zero_capture_failure_after_cleanup_" + phase, new(
            Work: async () =>
            {
                lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                foreach (var pair in setup.Authored)
                    Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
                if (!ReferenceEquals(validator, context.Validator))
                {
                    RequireGuardianJourneyIssues(name, phase + "_location_preflight",
                        await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
                    RequireGuardianJourneyIssues(name, phase + "_item_preflight",
                        await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
                }
                var intake = await MeasurePressureOwnerPhaseAsync("guardian_zero_intake", name,
                    () => validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease));
                capture = intake.Capture;
                RequireGuardianJourneyIssues(name, "guardian_zero_intake", intake.Issues); Assert.NotNull(capture);
                RequireGuardianJourneyIssues(name, "guardian_zero_resource_begin", await capture.BeginResourceExecutionAsync(lease));
                var prepared = await MeasurePressureOwnerPhaseAsync("guardian_zero_original_prefix", name,
                    () => capture.PrepareOriginalResourcePrefixAsync(lease));
                RequireGuardianJourneyIssues(name, "guardian_zero_original_prefix", prepared.Issues);
                Assert.True(prepared.Ready); Assert.False(prepared.PrefixBlocked);
                var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
                generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
                var frontier = await ReadPressureOwnerSnapshotAsync(capture, lease, name + "_before");
                Assert.Equal(0, frontier.NextOrdinal); Assert.Empty(capture.ReadClosedExchangeEvidence(lease));
                Assert.Equal(raw, files[AfterlifeSpiritualConflictState.StatePath]);
                window = new(name, data, context, lease, capture, frontier, root, raw, files, generation);
                _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_signed_current_prerequisites", name,
                    data.Rank, data.Resilience, expectedOrdinary = data.OrdinaryCalculation, expectedActual = data.ActualCalculation,
                    originalProfile = signedGuardian, originalConflict = original,
                    historicalReceipt = data.Retained.Receipts, historicalHistory = data.Retained.History,
                    rawOrdinaryBytesBase64 = Convert.ToBase64String(raw), ordinarySource, actualOnly,
                    counters = ReadGuardianZeroPressureCounts(capture) }));
                await work(window);
            },
            HasWindow: () => window is not null, HasCapture: () => capture is not null,
            HasLease: () => lease is not null, HasContext: () => false,
            AssertWindowInert: () =>
            {
                AssertPressureZeroCaptureInert(context, lease!, window!.Files, window.Generation, expectedPublication?.Invoke());
                return Task.CompletedTask;
            },
            CaptureDisposalFiles: () => disposalFiles = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath),
            DisposeCaptureExplicitly: () => capture!.Dispose(),
            AssertDisposedCaptureInert: () =>
            {
                Assert.False(capture!.IsCurrentOwner);
                if (disposalFiles is not null && generation is not null)
                    AssertPressureZeroCaptureInert(context, lease!, disposalFiles, generation, expectedPublication?.Invoke());
            },
            DisposeCaptureImplicitly: () => capture!.Dispose(),
            AssertNoPrivatePair: () => AssertPressureZeroCapturePublicationState(context, lease!, expectedPublication?.Invoke()),
            DisposeLease: async () => await lease!.DisposeAsync(), DisposeContext: () => Task.CompletedTask, AssertRootRemoved: () => { }));
        Assert.False(capture!.IsCurrentOwner); Assert.False(lease!.IsActive);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_capture_scope_closed", name, capturePhase = phase,
            captureCurrent = capture.IsCurrentOwner, leaseActive = lease.IsActive }));
        WritePressureOwnerTiming("guardian_zero_capture_" + phase, name, phaseStarted);
    }

    /// <summary>Reports complete unfiltered primary Data after every guardian-specific cleanup boundary and rethrows the unchanged primary.</summary>
    /// <param name="name">Exact fixture identity.</param>
    /// <param name="phase">Exact owning boundary, not a filter on retained errors.</param>
    /// <param name="stages">The existing failure-preserving stage protocol; its shared implementation is unchanged.</param>
    private async Task ExecuteGuardianZeroFixtureStagesAsync(string name, string phase, PressureGroupFixtureStages stages)
    {
        try { await ExecutePressureGroupFixtureStagesAsync(stages); }
        catch (Exception primary)
        {
            try
            {
                var dataCount = primary.Data.Count;
                var retained = primary.Data.Cast<System.Collections.DictionaryEntry>().ToArray();
                var entries = retained.Select(entry => new
                {
                    Key = entry.Key?.ToString(), KeyType = entry.Key?.GetType().FullName,
                    ValueType = entry.Value?.GetType().FullName,
                    Value = entry.Value is Exception secondary ? secondary.ToString() : entry.Value?.ToString()
                }).ToArray();
                _storageOutput.WriteLine(JsonSerializer.Serialize(new
                {
                    phase, name, primaryType = primary.GetType().FullName, primaryException = primary.ToString(),
                    dataCount, dataEntries = entries, dataEntriesComplete = entries.Length == dataCount,
                    secondaryCount = retained.Count(entry => entry.Value is Exception)
                }));
            }
            catch (Exception diagnosticError)
            {
                try { RetainPressureFixtureFailure(primary, "guardian_zero_diagnostics", diagnosticError); }
                catch (Exception) { /* A diagnostic failure must not replace the original primary or stack. */ }
            }
            throw;
        }
    }

    /// <summary>Serializes real scalar counters explicitly rather than relying on internal tuple/record JSON visibility.</summary>
    /// <param name="capture">The current test's genuine capture; reads perform no source calculation.</param>
    private static object ReadGuardianZeroPressureCounts(ValidationService.SpiritualOriginalTurnCapture capture)
    {
        var reuse = capture.ReadExistingStrainReuseCounts(); var batches = capture.ReadExistingPressureGroupComputationCounts();
        return new
        {
            reuse.Queries, reuse.SignedParentReads, reuse.OrdinaryEvaluations, reuse.ActualEvaluations,
            reuse.FreshInputChecks, reuse.FreshDependencyChecks,
            currentCatalogBuildAttempts = batches.CurrentBatch.CatalogBuildAttempts,
            currentIdentityParseAttempts = batches.CurrentBatch.IdentityParseAttempts,
            currentHistoryParseAttempts = batches.CurrentBatch.HistoryParseAttempts,
            currentHistoryAgreementAttempts = batches.CurrentBatch.HistoryAgreementAttempts,
            currentParentResolutionAttempts = batches.CurrentBatch.ParentResolutionAttempts,
            signedCatalogBuildAttempts = batches.SignedBatch.CatalogBuildAttempts,
            signedIdentityParseAttempts = batches.SignedBatch.IdentityParseAttempts,
            signedHistoryParseAttempts = batches.SignedBatch.HistoryParseAttempts,
            signedHistoryAgreementAttempts = batches.SignedBatch.HistoryAgreementAttempts,
            signedParentResolutionAttempts = batches.SignedBatch.ParentResolutionAttempts,
            batches.ResourceBatchComputations
        };
    }
}
