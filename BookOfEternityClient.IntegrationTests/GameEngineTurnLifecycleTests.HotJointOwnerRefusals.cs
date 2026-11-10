using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task GuaranteedTreatment_ActualJointOwnerRefusesForeignCloseReentryAndDelegatedClose()
    {
        var foreignRoot = Path.Combine(Path.GetTempPath(), "boe-gameengine-turnlifecycle-foreign-" + Guid.NewGuid().ToString("N"));
        using var foreignOwned = new CleanupOwnedFixture(foreignRoot, line => _directGachaOutput?.WriteLine(line));
        var foreign = new FileSystemManager(foreignRoot, NullLogger<FileSystemManager>.Instance);
        foreign.EnsureDirectoryStructure();
        await using (var bootstrap = await foreign.AcquireCanonicalWriteLeaseAsync())
            foreign.GetOrCreateSessionGeneration(bootstrap);
        GameEngine? actualEngine = null;
        GameEngine.MortalJointPublicationAttempt? actualAttempt = null;
        object? continuation = null, reservation = null;
        var probing = false;
        var notReserved = 0; var foreignRefusal = 0; var reentry = 0; var delegated = 0;
        JsonObject? final = null;

        async Task AtReadAsync(string path)
        {
            if (probing || actualEngine is null) return;
            var candidate = GetPrivateFieldValue<GameEngine.MortalJointPublicationAttempt?>(actualEngine, "_mortalJointPublicationAttempt");
            if (candidate is not { Live: true, Delegated: false }) return;
            probing = true;
            try
            {
                var lease = GetPrivateField<FileSystemManager.CanonicalWriteLease>(candidate, "_lease");
                Assert.True(lease.IsActive);
                if (candidate.Reservation is null && notReserved == 0)
                {
                    var refused = AcceptedTurnAuthorityRegistry.CloseOwnedJointAttemptUnattempted(_fs, lease, candidate);
                    Assert.Equal("UnavailableNotReserved", refused.Disposition);
                    Assert.Null(GetPrivateFieldValue<object?>(refused, "_receipt"));
                    Assert.False(candidate.CloseRecorded); notReserved++;
                }
                if (candidate.Continuation is null || candidate.Reservation is null || foreignRefusal != 0) return;
                actualAttempt = candidate; continuation = candidate.Continuation; reservation = candidate.Reservation;
                // Independent real owner; do not flow the original root's ambient
                // Main/lease into it, and join it before releasing the read hook.
                Task<string> foreignTask;
                using (ExecutionContext.SuppressFlow())
                    foreignTask = Task.Run(() => SessionOperationContext.RunParticipatingCurrentSessionAsync(foreign, async () =>
                    {
                        await using var foreignLease = await foreign.AcquireCanonicalWriteLeaseAsync();
                        var refused = AcceptedTurnAuthorityRegistry.CloseOwnedJointAttemptUnattempted(foreign, foreignLease, candidate);
                        Assert.Null(GetPrivateFieldValue<object?>(refused, "_receipt"));
                        return refused.Disposition;
                    }));
                Assert.Equal("UnavailableForeignOwner", await foreignTask);
                foreignRefusal++;
                Assert.Same(continuation, candidate.Continuation); Assert.Same(reservation, candidate.Reservation);
                Assert.Same(candidate, GetPrivateFieldValue<object?>(actualEngine, "_mortalJointPublicationAttempt"));
                Assert.True(candidate.Live); Assert.False(candidate.CloseRecorded);
                // The SAME real engine receives its own actual frame/capture and
                // lease solely for a rejection probe; no source is imported or seeded.
                var frame = GetPrivateField<object>(candidate, "_frame");
                var capture = GetPrivateField<MortalJointRepairCheckpoint.RejectedCapture>(candidate, "_capture");
                var method = typeof(GameEngine).GetMethod("BeginCurrentJointAttempt", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var wrapped = Assert.Throws<TargetInvocationException>(() => method.Invoke(actualEngine, [lease, frame, capture]));
                var failure = Assert.IsType<InvalidOperationException>(wrapped.InnerException);
                Assert.Contains("live joint attempt", failure.Message);
                Assert.Same(candidate, GetPrivateFieldValue<object?>(actualEngine, "_mortalJointPublicationAttempt"));
                Assert.Same(reservation, candidate.Reservation); reentry++;
                WritePhysicalInjuryDiagnostic("fr063_hot_joint_actual_reserved_refusals", new
                    { path, notReserved, foreignRefusal, reentry, actualLeaseActive = lease.IsActive, reservationUnchanged = true });
            }
            finally { probing = false; }
        }

        void AtMutation(string path)
        {
            if (delegated != 0 || actualEngine is null) return;
            var candidate = GetPrivateFieldValue<GameEngine.MortalJointPublicationAttempt?>(actualEngine, "_mortalJointPublicationAttempt");
            if (candidate is not { Live: true, Delegated: true }) return;
            Assert.Same(actualAttempt, candidate);
            var lease = GetPrivateField<FileSystemManager.CanonicalWriteLease>(candidate, "_lease");
            Assert.True(lease.IsActive);
            var refused = AcceptedTurnAuthorityRegistry.CloseOwnedJointAttemptUnattempted(_fs, lease, candidate);
            Assert.Equal("UnavailableDelegated", refused.Disposition);
            Assert.Null(GetPrivateFieldValue<object?>(refused, "_receipt"));
            Assert.Same(continuation, candidate.Continuation); Assert.Same(reservation, candidate.Reservation);
            Assert.False(candidate.CloseRecorded); delegated++;
            WritePhysicalInjuryDiagnostic("fr063_hot_joint_actual_delegated_refusal", new { path, delegated, actualLeaseActive = true });
        }

        try
        {
            await RunJointCompletionControlAsync(nonzeroProgression: false, useLiveCaller: true,
                attachActualOwnerProbe: engine =>
                {
                    actualEngine = engine;
                    _consoleCanonicalReadObserver = AtReadAsync;
                    _consoleMutationObserver = AtMutation;
                },
                observeSettledCommonPlan: _ =>
                {
                    final = InvokePrivateValue<JsonObject>(actualEngine!, "ExportMortalJointPublicationAttemptDiagnostic");
                    Assert.True(final["liveCapabilitiesExpired"]!.GetValue<bool>());
                    Assert.False(final["captureCanOpen"]!.GetValue<bool>());
                });
        }
        finally { _consoleCanonicalReadObserver = null; _consoleMutationObserver = null; }
        Assert.Equal(1, notReserved); Assert.Equal(1, foreignRefusal); Assert.Equal(1, reentry); Assert.Equal(1, delegated);
        Assert.NotNull(final); Assert.True(final!["delegated"]!.GetValue<bool>());
        Assert.NotEqual("ClosedNoNativeAttempt", final["closeDisposition"]!.GetValue<string>());
        Assert.False(final["hasS"]!.GetValue<bool>()); Assert.False(final["hasB"]!.GetValue<bool>()); Assert.False(final["hasM3"]!.GetValue<bool>());
        // RunJointCompletionControlAsync independently proves its real accepted
        // resource/effect/story/turn/cleanup union and inert repeat after these probes.
    }
}
