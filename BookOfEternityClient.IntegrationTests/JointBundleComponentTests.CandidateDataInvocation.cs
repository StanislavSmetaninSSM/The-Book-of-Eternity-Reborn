using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    /// <summary>Requires one actual preparation source in each of two independent fixed paired-read invocations.</summary>
    /// <returns>Completion after genuine stages, four successful reads and full closing, before the strict source oracle.</returns>
    [Fact]
    public async Task CandidateDataInvocation_TwoFreshCallsRequireIndependentExpectedSources()
    {
        var fixture = new Fixture(_output, nameof(CandidateDataInvocation_TwoFreshCallsRequireIndependentExpectedSources),
            passiveTimingEvents: true, gzipRecords: true);
        var invocations = new List<CandidateDataInvocation<PairedPreparationEvidence>>();
        Dictionary<string, byte[]?> before;
        string expectedBundle;
        string[] expectedProjection;
        await using (fixture)
        {
            await fixture.InitializeAsync();
            var bundle = await fixture.PrepareJointStagesAsync();
            expectedBundle = fixture.CaptureCandidateBundleView(bundle);
            expectedProjection = fixture.CaptureCandidatePreparationProjection(bundle.PreparedPlan);
            var inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input);
            var original = fixture.RequireIssuedCandidateStage(bundle);
            before = await fixture.CaptureCandidateInvocationBeforeImagesAsync();
            for (var index = 0; index < 2; index++)
            {
                Assert.Null(fixture.Files.GetCurrentSemanticDataOperation());
                var invocation = await fixture.InvokePairedPreparationReadsAsync(bundle, index, expectedProjection, inputFingerprint);
                invocations.Add(invocation);
                Assert.True(invocation.Scope.EntryAmbientNull); Assert.True(invocation.Scope.ExitAmbientNull);
                Assert.Equal(2, invocation.Result.SuccessfulReads);
                Assert.Equal(0, invocation.Result.Counts.GetValueOrDefault("WoundPlanCache.PreparedExpectedReplay.Source"));
                Assert.All(invocation.Result.Projections, projection => Assert.Equal(expectedProjection, projection));
                Assert.Equal(expectedBundle, fixture.CaptureCandidateBundleView(bundle));
                Assert.Equal(inputFingerprint, WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input));
                Assert.True(fixture.Files.IsCurrentSessionGeneration(fixture.Lease, original.Generation));
                Assert.Null(fixture.Files.GetCurrentSemanticDataOperation());
            }
            foreach (var row in before)
                Assert.Equal(row.Value, await fixture.Files.ReadFileBytesAsync(fixture.Lease, row.Key));
            Assert.Equal(expectedBundle, fixture.CaptureCandidateBundleView(bundle));
            Assert.Null(fixture.Files.GetCurrentSemanticDataOperation());
        }
        Assert.Equal(2, invocations.Count);
        var report = AssertCandidateInvocationClosedCapture(fixture);
        var replay = invocations.Select(value => value.Result.Counts.GetValueOrDefault("JointPreparation.Replay")).ToArray();
        var core = invocations.Select(value => value.Result.Counts.GetValueOrDefault("WoundPreparation.PrepareCore")).ToArray();
        var richer = invocations.Select(value => value.Result.Counts.GetValueOrDefault("WoundPlanCache.PreparedExpectedReplay.Source")).ToArray();
        var oracle = Path.Combine(fixture.Output.DiagnosticDirectory, "candidate-per-call-source-oracle.json");
        File.WriteAllText(oracle, JsonSerializer.Serialize(new
        {
            Kind = "T608CandidatePerCallSourceOracle", ActualReplay = replay, ActualPrepareCore = core, ActualRicherSource = richer,
            LogicalInvocations = 2, SuccessfulRealReads = 4, ExpectedProjection = expectedProjection,
            ActualProjections = invocations.Select(value => value.Result.Projections),
            InvocationCounts = invocations.Select(value => value.Result.Counts),
            Ownership = invocations.Select(value => value.Scope.DiagnosticData()),
            InBodyEntryStates = invocations.Select(value => value.Result.InBodyEntryState),
            BeforeImages = before, BundleView = expectedBundle, DiagnosticComplete = true, FixtureClosed = true,
            ActualGenuineRegistryQualification = false, CompletedTimingRecords = report.GetProperty("TimingStored").GetInt64()
        }, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608CandidatePerCallSourceReference", Path = oracle,
            fixture.Output.ReportPath, fixture.Output.RecordsPath, FixtureClosed = true }));
        // Sole intended RED: original no-owner wiring computes2+2, while shared per-call DATA must compute exactly1+1.
        Assert.Equal(new[] { 1, 1 }, replay);
        Assert.Equal(new[] { 1, 1 }, core);
        Assert.Equal(new[] { 0, 0 }, richer);
        // GREEN-only ownership facts follow the same final source oracle; absence is honest DATA under RED.
        foreach (var invocation in invocations)
        {
            AssertFreshCandidateInvocationReceipt(invocation.Scope);
            Assert.NotNull(invocation.Result.InBodyEntryState);
            Assert.Equal(new ValidationOperationState(0, 0, false), invocation.Result.InBodyEntryState);
        }
        Assert.NotSame(invocations[0].Scope.ClosedOwnerIdentity, invocations[1].Scope.ClosedOwnerIdentity);
        Assert.NotSame(invocations[0].Scope.ClosedOperationIdentity, invocations[1].Scope.ClosedOperationIdentity);
    }

    /// <summary>GREEN-only lexical-owner control for exactly-once normal/refusal/throw and untouched existing registration, without a world.</summary>
    /// <returns>Completion after real source disposal, original body errors and ordinary fixture closing.</returns>
    [Fact]
    public async Task CandidateDataInvocation_LifetimeErrorsAndExistingOwnerStayExact()
    {
        var fixture = new Fixture(_output, nameof(CandidateDataInvocation_LifetimeErrorsAndExistingOwnerStayExact),
            passiveTimingEvents: true, gzipRecords: true);
        CandidateScopeReceipt normalScope, refusalScope;
        CandidateLifetimeProbe thrownProbe;
        await using (fixture)
        {
            fixture.RequireCandidateVisibilityHasNoAuthority();
            var normal = new CandidateLifetimeProbe(returnValue: true);
            var normalResult = await fixture.InvokeCandidateLifetimeProbeAsync(normal);
            Assert.True(normalResult.Result); Assert.Equal(1, normal.Calls);
            normalScope = normalResult.Scope; AssertFreshCandidateInvocationReceipt(normalScope);
            Assert.Same(normal.Operation, normalScope.ClosedOperationIdentity);
            var refusal = new CandidateLifetimeProbe(returnValue: false);
            var refusedResult = await fixture.InvokeCandidateLifetimeProbeAsync(refusal);
            Assert.False(refusedResult.Result); Assert.Equal(1, refusal.Calls);
            refusalScope = refusedResult.Scope; AssertFreshCandidateInvocationReceipt(refusalScope);
            Assert.NotSame(normalScope.ClosedOwnerIdentity, refusalScope.ClosedOwnerIdentity);
            Assert.NotSame(normalScope.ClosedOperationIdentity, refusalScope.ClosedOperationIdentity);
            thrownProbe = new CandidateLifetimeProbe(returnValue: false,
                error: new InvalidDataException("T608 original synchronous lexical-body failure."));
            var error = await Record.ExceptionAsync(() => fixture.InvokeCandidateLifetimeProbeAsync(thrownProbe));
            Assert.Same(thrownProbe.Error, error); Assert.Equal(1, thrownProbe.Calls);
            Assert.Contains(nameof(Fixture.InvokeCandidateLifetimeProbeAsync), error!.StackTrace ?? string.Empty);
            Assert.NotNull(thrownProbe.Operation);
            Assert.Equal(new ValidationOperationState(0, 0, true), thrownProbe.Operation.CaptureState());
            Assert.Null(fixture.Files.GetCurrentSemanticDataOperation());
            using (var existing = fixture.Files.BeginSourceDataOperationIfNeeded())
            {
                Assert.NotNull(existing);
                var existingOperation = Assert.IsType<ValidationOperation>(fixture.Files.GetCurrentSemanticDataOperation());
                var prior = existingOperation.CaptureState();
                var preexisting = new CandidateLifetimeProbe(returnValue: true);
                var rejected = await Record.ExceptionAsync(() => fixture.InvokeCandidateLifetimeProbeAsync(preexisting));
                var protocol = Assert.IsType<InvalidOperationException>(rejected);
                Assert.Equal(Fixture.CandidateDataOwnerRequiredMessage, protocol.Message);
                Assert.Equal(0, preexisting.Calls);
                Assert.Same(existingOperation, fixture.Files.GetCurrentSemanticDataOperation());
                Assert.Equal(prior, existingOperation.CaptureState()); Assert.False(existing.CaptureState().Closed);
                await existing.CompleteAsync();
            }
            Assert.Null(fixture.Files.GetCurrentSemanticDataOperation());
            fixture.RequireCandidateVisibilityHasNoAuthority();
        }
        _ = AssertCandidateInvocationClosedCapture(fixture);
        var path = Path.Combine(fixture.Output.DiagnosticDirectory, "candidate-per-call-lifetime-oracle.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            Kind = "T608CandidatePerCallLifetimeOracle", Normal = normalScope.DiagnosticData(), Refusal = refusalScope.DiagnosticData(),
            ThrowCalls = thrownProbe.Calls, ThrowOperationClosed = thrownProbe.Operation!.CaptureState(),
            OriginalThrowObjectAndStackPreserved = true, ExistingOwnerUntouched = true,
            RuntimeLeaseIssuerAndSemanticOwnerAbsentAfter = true, DiagnosticComplete = true, FixtureClosed = true,
            ActualGenuineRegistryQualification = false
        }, new JsonSerializerOptions { WriteIndented = true }));
        _output.WriteLine(JsonSerializer.Serialize(new { Kind = "T608CandidatePerCallLifetimeReference", Path = path,
            fixture.Output.ReportPath, fixture.Output.RecordsPath, FixtureClosed = true }));
    }

    /// <summary>Requires actual successful ownership, empty entry, completed fence and closed zero-entry storage after return.</summary>
    /// <param name="receipt">Detached state with identity references retained only after their owner closed.</param>
    private static void AssertFreshCandidateInvocationReceipt(CandidateScopeReceipt receipt)
    {
        Assert.True(receipt.EntryAmbientNull); Assert.True(receipt.ExitAmbientNull);
        Assert.NotNull(receipt.ClosedOwnerIdentity); Assert.NotNull(receipt.ClosedOperationIdentity);
        Assert.Equal(new ValidationOperationState(0, 0, false), receipt.EntryState);
        Assert.True(receipt.CompleteReturned); Assert.Equal(new ValidationOperationState(0, 0, true), receipt.ClosedState);
    }

    /// <summary>Checks the actual complete stored/source accounting and marked-root close before either final qualification assertion.</summary>
    /// <param name="fixture">The already disposed real fixture.</param>
    /// <returns>Detached successful report DATA, never a capability or runtime authority.</returns>
    private static JsonElement AssertCandidateInvocationClosedCapture(Fixture fixture)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(fixture.Output.ReportPath)); var report = document.RootElement.Clone();
        foreach (var field in new[] { "DiagnosticComplete", "WriterClosed", "TimingComplete", "PassiveTimingRequired", "GzipFooterClosed" })
            Assert.True(report.GetProperty(field).GetBoolean());
        Assert.Empty(report.GetProperty("Failures").EnumerateArray());
        var close = report.GetProperty("FixtureClosing"); Assert.Equal(fixture.Output.ExpectedOwnedRoot, close.GetProperty("Owned").GetString());
        Assert.True(close.GetProperty("Closing").GetBoolean()); Assert.False(close.GetProperty("Retained").GetBoolean());
        Assert.Empty(close.GetProperty("Failures").EnumerateArray());
        Assert.Throws<FileNotFoundException>(() => File.GetAttributes(fixture.Output.ExpectedOwnedRoot));
        var storedBytes = new FileInfo(fixture.Output.RecordsPath).Length;
        using (var stored = File.OpenRead(fixture.Output.RecordsPath))
            Assert.Equal(Convert.ToHexString(SHA256.HashData(stored)), report.GetProperty("Sha256").GetString());
        Assert.Equal(storedBytes, report.GetProperty("StoredBytes").GetInt64());
        JointBundleDiagnosticOutput.ValidateClosedGzipIdentity(fixture.Output.RecordsPath, storedBytes,
            report.GetProperty("Sha256").GetString()!, report.GetProperty("DecodedBytes").GetInt64(), report.GetProperty("DecodedSha256").GetString()!);
        var complete = report.GetProperty("PassiveTimingCompletion"); Assert.True(complete.GetProperty("Complete").GetBoolean());
        var events = complete.GetProperty("Events"); Assert.True(events.GetProperty("Enabled").GetBoolean());
        Assert.Equal(0, events.GetProperty("Pending").GetInt64()); Assert.Equal(0, events.GetProperty("Failed").GetInt64());
        var expected = complete.GetProperty("ExpectedRecords").GetInt64();
        foreach (var name in new[] { "Attempted", "Enqueued", "Drained" }) Assert.Equal(expected, events.GetProperty(name).GetInt64());
        foreach (var name in new[] { "TransportAttempted", "TransportStored" }) Assert.Equal(expected, complete.GetProperty(name).GetInt64());
        Assert.Equal(expected, report.GetProperty("TimingAttempted").GetInt64()); Assert.Equal(expected, report.GetProperty("TimingStored").GetInt64());
        Assert.Equal(report.GetProperty("OutputAttempted").GetInt64(), report.GetProperty("OutputStored").GetInt64());
        Assert.Equal(expected + report.GetProperty("OutputStored").GetInt64(), report.GetProperty("DecodedRows").GetInt64());
        var snapshot = complete.GetProperty("Snapshot"); Assert.True(snapshot.GetProperty("Closed").GetBoolean());
        Assert.True(snapshot.GetProperty("Complete").GetBoolean()); Assert.Equal(0, snapshot.GetProperty("OpenScopes").GetInt64());
        Assert.Equal(0, snapshot.GetProperty("LateCompletions").GetInt64());
        return report;
    }

    /// <summary>Private return envelope; its optional ownership receipt never exports cached DATA.</summary>
    /// <typeparam name="T">The fixed typed result, never a permission.</typeparam>
    /// <param name="Result">The fixed synchronous body's actual result.</param>
    /// <param name="Scope">Honest absent or closed ownership DATA.</param>
    private sealed record CandidateDataInvocation<T>(T Result, CandidateScopeReceipt Scope);

    /// <summary>Only closed identities and detached scalar states; identity objects are never serialized.</summary>
    /// <param name="ClosedOwnerIdentity">The actual already disposed owner identity, unavailable under RED.</param>
    /// <param name="ClosedOperationIdentity">The actual already closed operation identity, unavailable under RED.</param>
    /// <param name="EntryState">Actual pre-body state, unavailable under RED.</param>
    /// <param name="ClosedState">Actual post-disposal state, unavailable under RED.</param>
    /// <param name="CompleteReturned">Actual completion success, unavailable under RED.</param>
    /// <param name="EntryAmbientNull">Whether caller registration was absent on entry.</param>
    /// <param name="ExitAmbientNull">Whether caller registration was absent on return.</param>
    private sealed record CandidateScopeReceipt(object? ClosedOwnerIdentity, object? ClosedOperationIdentity,
        ValidationOperationState? EntryState, ValidationOperationState? ClosedState, bool? CompleteReturned,
        bool EntryAmbientNull, bool ExitAmbientNull)
    {
        /// <summary>Exports only comparison scalars, never a live or closed capability object.</summary>
        /// <returns>Honest nullable states and identity availability flags.</returns>
        internal object DiagnosticData() => new
        {
            OwnerPresent = ClosedOwnerIdentity is not null, OperationPresent = ClosedOperationIdentity is not null,
            EntryState, ClosedState, CompleteReturned, EntryAmbientNull, ExitAmbientNull
        };
    }

    /// <summary>Actual two-read DATA and counters; no expected-source result is retained for another invocation.</summary>
    /// <param name="SuccessfulReads">Actual successful producer-consumer reads.</param>
    /// <param name="Projections">Independent detached scalar/ordered pair vectors.</param>
    /// <param name="Counts">Every actual completed operation count in the measured interval.</param>
    /// <param name="InBodyEntryState">Actual operation state before any validator; unavailable under RED.</param>
    private sealed record PairedPreparationEvidence(int SuccessfulReads, string[][] Projections,
        Dictionary<string, int> Counts, ValidationOperationState? InBodyEntryState);

    /// <summary>Fixed finite lifetime/error probe DATA, never a callback installed into any service or recorder.</summary>
    /// <param name="returnValue">The literal normal/refusal result.</param>
    /// <param name="error">The exact original exception to throw after one body entry, or null.</param>
    private sealed class CandidateLifetimeProbe(bool returnValue, Exception? error = null)
    {
        /// <summary>Gets the literal fixed normal/refusal DATA.</summary>
        internal bool ReturnValue { get; } = returnValue;
        /// <summary>Gets the exact original body error, when selected.</summary>
        internal Exception? Error { get; } = error;
        /// <summary>Counts actual synchronous body entries.</summary>
        internal int Calls { get; set; }
        /// <summary>Observes the actual operation for post-disposal assertions; never serialized or reused.</summary>
        internal ValidationOperation? Operation { get; set; }
    }

    private sealed partial class Fixture
    {
        /// <summary>The fixed fixture-only preexisting-owner protocol diagnostic.</summary>
        internal const string CandidateDataOwnerRequiredMessage = "Candidate source invocation requires no preexisting semantic DATA owner.";

        /// <summary>Shares ONLY lexical invocation wiring; RED executes the original body inline once with no owner.</summary>
        /// <typeparam name="TResult">The actual fixed typed adapter result.</typeparam>
        /// <param name="invoke">One of exactly three fixed fixture-local synchronous bodies; never stored, scheduled or registered.</param>
        /// <returns>The original result and honest unavailable ownership DATA, not a fabricated completed fence.</returns>
        private Task<CandidateDataInvocation<TResult>> WithFreshSourceDataAsync<TResult>(Func<TResult> invoke)
        {
            var entryNull = Files.GetCurrentSemanticDataOperation() is null;
            if (!entryNull) throw new InvalidOperationException(CandidateDataOwnerRequiredMessage);
            var result = invoke(); // original synchronous call exactly once, before any await or Task is returned
            var exitNull = Files.GetCurrentSemanticDataOperation() is null;
            return Task.FromResult(new CandidateDataInvocation<TResult>(result,
                new(null, null, null, null, null, entryNull, exitNull)));
        }

        /// <summary>Unwired typed candidate adapter; its original literal timer contains ONLY the exact synchronous registry call.</summary>
        /// <param name="input">The actual previously issued common input.</param>
        /// <param name="authority">Its genuine current common authority.</param>
        /// <param name="existingTimingName">An unchanged literal supplied by the later reviewed candidate callsite.</param>
        /// <returns>The unchanged registry result after the shared lexical invocation returns.</returns>
        internal async Task<AcceptedTurnAuthorityRegistry.JointCommonCandidateResult> CreateCandidateWithFreshDataAsync(
            AcceptedMechanicsInput input, WoundAcceptedTurnPlanner.MortalJointRepairCommonAuthority authority,
            string existingTimingName)
        {
            var invocation = await WithFreshSourceDataAsync(() =>
            {
                using (Files.MeasureOperation(existingTimingName))
                    return AcceptedTurnAuthorityRegistry.CreateJointRepairCommonCandidate(Files, Lease, input, authority);
            });
            return invocation.Result;
        }

        /// <summary>Runs the exact qualified pair of actual PreparedAgrees calls with no validator before its entry receipt.</summary>
        /// <param name="bundle">One actual issued stage bundle retained by this fixture.</param>
        /// <param name="index">The fixed first or second invocation ordinal.</param>
        /// <param name="expectedProjection">Independent five-scalar/ordered-pair values saved before both invocations.</param>
        /// <param name="expectedInput">The independent genuine full input fingerprint.</param>
        /// <returns>Two actual successful reads, all interval counters/projections and the shared ownership receipt.</returns>
        internal Task<CandidateDataInvocation<PairedPreparationEvidence>> InvokePairedPreparationReadsAsync(
            AcceptedMechanicsWoundStageBundle bundle, int index, string[] expectedProjection, string expectedInput) =>
            WithFreshSourceDataAsync(() =>
            {
                var entryState = Files.GetCurrentSemanticDataOperation()?.CaptureState();
                var projections = new List<string[]>();
                Dictionary<string, int> counts;
                using (Files.EnterOperationTimingContext()) // entire counted body is synchronous; no await/context crosses it
                {
                    var offset = ResetCandidateCapture("CandidatePerCallPrepared" + index);
                    for (var read = 0; read < 2; read++)
                    {
                        var prepared = bundle.PreparedPlan;
                        Assert.NotSame(prepared, bundle.PreparedPlan);
                        Assert.Equal(expectedInput, WoundAcceptedTurnFingerprints.ComputeInput(bundle.Input));
                        Assert.Equal(expectedProjection, PreparedExpectedReplayProjection(prepared));
                        Assert.Same(_jointContinuation, prepared.JointRepairContinuationAuthority);
                        Assert.True(WoundAcceptedTurnPlanner.JointRepairPreparedAgrees(prepared));
                        var projection = PreparedExpectedReplayProjection(prepared);
                        Assert.Equal(expectedProjection, projection); projections.Add(projection);
                    }
                    counts = CompleteCandidateCapture("CandidatePerCallPrepared" + index, offset);
                }
                Assert.Equal(2, projections.Count);
                return new PairedPreparationEvidence(projections.Count, projections.ToArray(), counts, entryState);
            });

        /// <summary>Third and last fixed helper consumer: a finite no-world body that returns or throws its own literal DATA.</summary>
        /// <param name="probe">Exactly-once counters, fixed result/error and actual operation observation.</param>
        /// <returns>The original normal/refusal result, or its original synchronous exception.</returns>
        internal Task<CandidateDataInvocation<bool>> InvokeCandidateLifetimeProbeAsync(CandidateLifetimeProbe probe) =>
            WithFreshSourceDataAsync(() =>
            {
                probe.Calls++;
                probe.Operation = Files.GetCurrentSemanticDataOperation();
                if (probe.Error is { } error) throw error;
                return probe.ReturnValue;
            });

        /// <summary>Exports the existing complete bundle comparison through its original helper without admitting new authority.</summary>
        /// <param name="bundle">The actual issued bundle or its independent detached DATA.</param>
        /// <returns>The original comparison string.</returns>
        internal string CaptureCandidateBundleView(AcceptedMechanicsWoundStageBundle bundle) => ReadBundleView(bundle);

        /// <summary>Exports the same complete scalar/ordered batch contract used by the accepted source fixture.</summary>
        /// <param name="plan">An independent actual prepared stage export.</param>
        /// <returns>Detached original comparison values.</returns>
        internal string[] CaptureCandidatePreparationProjection(WoundPreparedAcceptedTurnPlan plan) => PreparedExpectedReplayProjection(plan);

        /// <summary>Requires the genuine issuer/frame/reservation/current generation before the counted lexical calls.</summary>
        /// <param name="bundle">The same bundle returned by the actual stage preparation.</param>
        /// <returns>The current original DATA, not a new issuer or owner.</returns>
        internal GameEngine.JointComponentOriginal RequireIssuedCandidateStage(AcceptedMechanicsWoundStageBundle bundle)
        {
            var original = _jointOriginal ?? throw new InvalidOperationException("Genuine candidate-stage original missing.");
            Assert.Same(bundle, _jointBundle); Assert.NotNull(_jointContinuation); Assert.NotNull(_jointReservation);
            var binding = bundle.Input.Binding;
            Assert.Equal(original.SessionId, binding.SessionId); Assert.Equal(original.RequestId, binding.RequestId);
            Assert.Equal(original.SnapshotToken, binding.SnapshotToken); Assert.Equal(original.Turn, binding.Turn);
            Assert.Same(_jointContinuation, bundle.PreparedPlan.JointRepairContinuationAuthority);
            Assert.True(Files.IsCurrentSessionGeneration(Lease, original.Generation));
            Assert.Null(Files.GetCurrentSemanticDataOperation());
            return original;
        }

        /// <summary>Captures the same complete current-path set used by the accepted prepared-source proof, outside either measured invocation.</summary>
        /// <returns>Independent actual physical bytes/absence for final unchanged-path assertions.</returns>
        internal async Task<Dictionary<string, byte[]?>> CaptureCandidateInvocationBeforeImagesAsync()
        {
            var paths = AcceptedMechanicsWoundCommonInputComposer.TreatmentOrdinaryGuardPaths.Concat(new[]
            {
                WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
                WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
                AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
                "output/narrative_response.json", MortalJointInterfacePublication.InterfacePath
            }).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal);
            var result = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
            foreach (var path in paths) result.Add(path, await Files.ReadFileBytesAsync(Lease, path));
            return result;
        }
    }
}
