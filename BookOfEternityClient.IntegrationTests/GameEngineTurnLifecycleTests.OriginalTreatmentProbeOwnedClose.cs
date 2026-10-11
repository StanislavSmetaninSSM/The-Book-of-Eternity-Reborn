using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("close")]
    [InlineData("generation")]
    [InlineData("generation-close")]
    public async Task OriginalTreatmentProbeOwnerPreservesActualGenerationFailureAndColdState(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var invalidGeneration = mode.StartsWith("generation", StringComparison.Ordinal);
        var throwClose = mode is "close" or "generation-close";
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false; var acquirePauses = 0; var generationCuts = 0;
        var terminal = false; var laterAcquisitions = 0; var laterReads = 0; var laterMutations = 0; var laterRecovery = 0;
        FileSystemManager? files = null;
        var malformed = JsonSerializer.SerializeToUtf8Bytes(new { SchemaVersion = 1, GenerationId = "invalid-t067-" + Guid.NewGuid().ToString("N") });
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                if (terminal) laterAcquisitions++;
                if (armed && acquirePauses == 0) { acquirePauses++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
            },
            AfterCanonicalWriteLockOpenedAsync = () =>
            {
                if (armed && invalidGeneration && generationCuts == 0)
                { generationCuts++; File.WriteAllBytes(files!.SessionGenerationPath, malformed); }
                return Task.CompletedTask;
            },
            AfterCanonicalReadInitialValidationAsync = _ => { if (terminal) laterReads++; return Task.CompletedTask; },
            AfterExactPhysicalReadInitialValidationAsync = _ => { if (terminal) laterReads++; return Task.CompletedTask; },
            BeforeCanonicalMutationBoundaryAsync = _ => { if (terminal) laterMutations++; return Task.CompletedTask; },
            LocalPublicationRecoveryObserver = (_, _) => { if (terminal) laterRecovery++; }
        };
        using var classOwned = new CleanupOwnedFixture(_rootPath, _directGachaOutput!.WriteLine);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault: null, hooks: hooks);
        files = context.FileSystem;
        using var owned = new CleanupOwnedFixture(context.Root, _directGachaOutput.WriteLine);
        await context.ReleaseLeaseAsync();
        var (engine, snapshotContext) = await CreateHeldTreatmentValidationEngineAsync(context);
        await using var audio = GetPrivateField<AudioService>(engine, "_audioService");
        var refresh = await InvokePrivateTaskResultAsync(engine, "RefreshAcceptedTurnCanonicalStateForValidationAsync", HeldTreatmentPipelineContext.Turn, snapshotContext);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(refresh.GetType().GetProperty("TreatmentResourcePublicationTransaction", flags)!.GetValue(refresh));
        Assert.True((bool)refresh.GetType().GetProperty("BaselineUsable", flags)!.GetValue(refresh)!);
        Assert.DoesNotContain((IEnumerable<ValidationIssue>)refresh.GetType().GetProperty("PostSealIssues", flags)!.GetValue(refresh)!, issue => issue.Severity == IssueSeverity.Error);
        JsonElement ProbeEvidence(MortalWoundTreatmentPublicationProbeResult value)
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(new { value.IsValid, value.ChangedCount, value.State, value.OperationKey, value.AttemptId, value.RequestFingerprint, value.ResourceAuthorityFingerprint, value.FinalizationFingerprint, value.AgreementFingerprint, value.SessionGeneration, value.SessionGenerationRevision, value.GenerationFingerprint, Issues = value.Issues }));
            return json.RootElement.Clone();
        }
        var initial = await transaction.ProbeAsync(files);
        Assert.True(initial.IsValid); Assert.Empty(initial.Issues);
        Assert.Equal(context.OriginalSessionGeneration, initial.SessionGeneration);
        Assert.Equal(context.OriginalSessionGenerationRevision, initial.SessionGenerationRevision);
        // The real registry gate only schedules this test. It grants no lease/token.
        object state;
        BrowserOriginalMainCondition baselineCondition;
        await using (var setupLease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            baselineCondition = setupLease.MainAdmission!.CaptureBrowserCondition(initial.SessionGeneration!);
            Assert.Equal("quiescentAbsent", baselineCondition.Kind); Assert.Equal(files.BasePath, baselineCondition.RootKey);
            Assert.Equal(initial.SessionGeneration, baselineCondition.Generation); Assert.Null(baselineCondition.ActiveIdentity);
            state = typeof(AcceptedTurnAuthorityRegistry).GetMethod("GetState", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [files, setupLease])!;
        }
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));
        var actualRegistryGate = state.GetType().GetField("_gate", flags)!.GetValue(state)!;
        Assert.False(Directory.Exists(files.ResolvePath(ConsoleLocalTurnRollbackArtifacts.Root)) &&
                     Directory.EnumerateFiles(files.ResolvePath(ConsoleLocalTurnRollbackArtifacts.Root), "*", SearchOption.AllDirectories).Any());
        var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
        try
        {
            var before = EngineRollbackColdProbe.Images(files);
            var closeFailure = new IOException("T067 actual Probe original owner external-context close " + Guid.NewGuid().ToString("N"));
            var closer = new ProbeThrowingClose(closeFailure, () => terminal = true);
            FileSystemManager.CanonicalWriteLease? original = null; string? ownerState = null;
            Exception? primary = null, observationFailure = null, failure = null;
            string? observedFirstChanceStack = null;
            MortalWoundTreatmentPublicationProbeResult? result = null;
            Task<MortalWoundTreatmentPublicationProbeResult>? operation = null;
            var marker = new AsyncLocal<object?>(); var invocation = new object();
            var primaryObservations = 0; var attachments = 0; var physicalScope = false; var acquired = false;
            BrowserOriginalMainCondition? condition = null;
            void InspectOwner()
            {
                var inspector = typeof(OriginalOwnedLeaseCloseTests).GetMethod("InspectOriginalNestedOwningLease", BindingFlags.Static | BindingFlags.NonPublic)!;
                (original, ownerState) = ((FileSystemManager.CanonicalWriteLease, string))inspector.Invoke(null, [operation, "MortalWoundTreatmentResourcePublicationTransaction+<ProbeAsync>"])!;
                Assert.Same(files, original.Owner); Assert.True(original.IsActive);
                Assert.NotNull(original.MainAdmission); physicalScope = original.MainAdmission.PhysicalScope; acquired = original.MainAdmission.Acquired;
                Assert.True(physicalScope); Assert.True(acquired); Assert.Equal(files.BasePath, original.MainAdmission.Root);
                Assert.Null(original.ExternalPublicationContext);
                if (!invalidGeneration)
                {
                    condition = original.MainAdmission.CaptureBrowserCondition(initial.SessionGeneration!);
                    Assert.Equal("quiescentAbsent", condition.Kind); Assert.Equal(files.BasePath, condition.RootKey);
                    Assert.Equal(initial.SessionGeneration, condition.Generation); Assert.Null(condition.ActiveIdentity);
                }
                if (throwClose) { original.ExternalPublicationContext = closer; attachments++; }
            }
            EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
            {
                if (!ReferenceEquals(marker.Value, invocation) || primary != null || args.Exception is not InvalidDataException) return;
                // Current frames identify the first production throw, before Probe's await-using closes its owner.
                var currentStack = new StackTrace(fNeedFileInfo: false);
                var methods = currentStack.GetFrames().Select(frame => frame.GetMethod()).ToArray();
                if (!methods.Any(method => method?.DeclaringType == typeof(FileSystemManager) && method.Name == "ParseSessionGenerationText") ||
                    !methods.Any(method => method?.DeclaringType == typeof(AcceptedTurnAuthorityRegistry) && method.Name == "GetState") ||
                    !methods.Any(method => method?.Name == "MoveNext" && method.DeclaringType?.FullName?.StartsWith(
                        typeof(MortalWoundTreatmentResourcePublicationTransaction).FullName + "+<ProbeAsync>", StringComparison.Ordinal) == true)) return;
                observedFirstChanceStack = currentStack.ToString();
                primary = args.Exception; primaryObservations++; terminal = true;
                try { InspectOwner(); } catch (Exception error) { observationFailure = error; }
            };
            var gateHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseGate = new ManualResetEventSlim();
            Task? gateOwner = null;
            Exception? fixtureSettlementOutcome = null, fixtureSettlementFailure = null, fixtureGateFailure = null;
            try
            {
                if (!invalidGeneration)
                {
                    gateOwner = Task.Run(() => { lock (actualRegistryGate) { gateHeld.TrySetResult(); Assert.True(releaseGate.Wait(TimeSpan.FromSeconds(12)), "Owned registry scheduling gate timed out."); } });
                    await gateHeld.Task.WaitAsync(TimeSpan.FromSeconds(12));
                }
                armed = true; marker.Value = invocation; AppDomain.CurrentDomain.FirstChanceException += observe;
                operation = transaction.ProbeAsync(files);
                Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
                allowAcquire.TrySetResult();
                if (!invalidGeneration)
                {
                    // Await the actual hoisted Probe owner, not a copied/acquired test lease.
                    var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(12);
                    while (original == null && !operation.IsCompleted && DateTime.UtcNow < deadline)
                    {
                        try { InspectOwner(); } catch (TargetInvocationException) { await Task.Delay(10); }
                    }
                    Assert.NotNull(original); releaseGate.Set();
                }
                failure = await Record.ExceptionAsync(() => operation);
                if (failure == null) result = await operation;
            }
            finally
            {
                try
                {
                    try
                    {
                        allowAcquire.TrySetResult(); releaseGate.Set();
                        if (operation != null && !operation.IsCompleted)
                            fixtureSettlementOutcome = await Record.ExceptionAsync(() => operation);
                    }
                    catch (Exception error) { fixtureSettlementFailure = error; }
                    try { if (gateOwner != null) await gateOwner; }
                    catch (Exception error) { fixtureGateFailure = error; }
                }
                finally
                {
                    AppDomain.CurrentDomain.FirstChanceException -= observe;
                    marker.Value = null; armed = false;
                    if (fixtureSettlementOutcome != null || fixtureSettlementFailure != null || fixtureGateFailure != null)
                        _directGachaOutput.WriteLine(JsonSerializer.Serialize(new { mode, Scope = "fixture settlement/gate cleanup; not target primary or causal RED",
                            FixtureSettlementOutcome = fixtureSettlementOutcome?.ToString(), FixtureSettlementFailure = fixtureSettlementFailure?.ToString(),
                            FixtureGateFailure = fixtureGateFailure?.ToString(), SameTargetPrimary = ReferenceEquals(primary, fixtureSettlementOutcome),
                            TargetPrimary = primary?.ToString(), TargetFailure = failure?.ToString(), ObserverUnsubscribed = true, MarkerReset = marker.Value == null, armed }));
                }
            }
            var after = EngineRollbackColdProbe.Images(files);
            var coldOwner = new OriginalOwnedLeaseCloseTests(_directGachaOutput);
            var coldTask = (Task<JsonElement>)typeof(OriginalOwnedLeaseCloseTests).GetMethod("RunRollbackColdReadAsync", flags)!.Invoke(coldOwner, [files, Path.Combine(context.Root, "probe-cold-result.json")])!;
            var cold = await coldTask;
            bool lockAvailable;
            using (var lockProbe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
            _directGachaOutput.WriteLine(JsonSerializer.Serialize(new { mode, Scope = "actual Probe owner; quiescent physical main guard; cold physical authority only, not treatment-token resurrection",
                context.Root, actualOwnerState = ownerState, actualRegistryState = state.GetType().FullName, physicalScope, acquired, baselineCondition, condition,
                Receipt = ProbeEvidence(initial), acquirePauses, generationCuts, primaryObservations, observedFirstChanceStack, attachments, closer.Calls, primary = primary?.ToString(), failure = failure?.ToString(), observationFailure = observationFailure?.ToString(),
                SamePrimary = ReferenceEquals(primary, failure), SameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure),
                FixtureSettlementOutcome = fixtureSettlementOutcome?.ToString(), FixtureSettlementFailure = fixtureSettlementFailure?.ToString(), FixtureGateFailure = fixtureGateFailure?.ToString(),
                Result = result == null ? (JsonElement?)null : ProbeEvidence(result), lockAvailable, activeAfter = original?.IsActive, ambientAfter = original?.AmbientRegistration != null,
                mainAfter = original?.MainAdmission != null, externalAfter = original?.ExternalPublicationContext != null,
                laterAcquisitions, laterReads, laterMutations, laterRecovery, Before = before, After = after, Cold = cold }));
            // Preserve the exact cut/cold evidence before fixture compensation or restoration.
                var expected = new Dictionary<string, byte[]>(before, StringComparer.Ordinal);
                if (invalidGeneration) expected[Path.GetRelativePath(files.BasePath, files.SessionGenerationPath)] = malformed;
                AssertProbeImages(expected, after);
                AssertProbeImages(after, JsonSerializer.Deserialize<Dictionary<string, byte[]>>(cold.GetProperty("Before"))!);
                AssertProbeImages(after, JsonSerializer.Deserialize<Dictionary<string, byte[]>>(cold.GetProperty("After"))!);
                Assert.True(lockAvailable); Assert.NotNull(original); Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration);
                Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext); Assert.Null(observationFailure);
                Assert.Null(fixtureSettlementFailure); Assert.Null(fixtureGateFailure);
                Assert.Equal(1, acquirePauses); Assert.Equal(invalidGeneration ? 1 : 0, generationCuts);
                Assert.Equal(throwClose ? 1 : 0, attachments); Assert.Equal(attachments, closer.Calls);
                Assert.Equal(0, laterAcquisitions); Assert.Equal(0, laterReads); Assert.Equal(0, laterMutations); Assert.Equal(0, laterRecovery);
                if (invalidGeneration)
                {
                    Assert.Equal(1, primaryObservations); Assert.NotNull(primary); Assert.Null(result);
                    Assert.Equal(typeof(InvalidDataException).FullName, cold.GetProperty("FailureType").GetString());
                    Assert.Contains("ParseSessionGenerationText", cold.GetProperty("Failure").GetString()!);
                    Assert.Empty(cold.GetProperty("reads").EnumerateObject());
                    Assert.Contains("ParseSessionGenerationText", primary.StackTrace!);
                    Assert.Contains("AcceptedTurnAuthorityRegistry.GetState", primary.StackTrace!);
                    Assert.Same(primary, failure);
                    if (throwClose) Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
                }
                else
                {
                    Assert.Null(primary); Assert.True(cold.GetProperty("acquired").GetBoolean()); Assert.Equal(initial.SessionGeneration, cold.GetProperty("generation").GetString());
                    Assert.Equal(JsonValueKind.Null, cold.GetProperty("Failure").ValueKind);
                    if (throwClose) { Assert.Null(result); Assert.Same(closeFailure, failure); }
                    else { Assert.Null(failure); Assert.NotNull(result); Assert.True(result.IsValid); Assert.Empty(result.Issues); Assert.Equal(0, result.ChangedCount); Assert.Equal(ProbeEvidence(initial).GetRawText(), ProbeEvidence(result).GetRawText()); }
                }
        }
        finally
        {
            // Separate fixture teardown, AFTER hot/cold refusal evidence. Probe never compensates.
            terminal = false; File.WriteAllBytes(files.SessionGenerationPath, generationBefore);
            await ((IAsyncDisposable)transaction).DisposeAsync();
        }
    }

    private static void AssertProbeImages(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private sealed class ProbeThrowingClose(IOException failure, Action onClose) : IDisposable
    {
        internal int Calls { get; private set; }
        public void Dispose() { Calls++; onClose(); throw failure; }
    }
}
