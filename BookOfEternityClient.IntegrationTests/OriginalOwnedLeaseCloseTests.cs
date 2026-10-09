using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class OriginalOwnedLeaseCloseTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("refresh")]
    [InlineData("ordinary_write")]
    public async Task OriginalOwnedLeaseCloseRetainsGenuinePublicationUncertainty(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-owned-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseArmed = false;
        var pauses = 0;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = cut.Hooks.LocalPublicationObserver,
                BeforeCanonicalWriteLockOpenAsync = cut.Hooks.BeforeCanonicalWriteLockOpenAsync,
                AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
                SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync,
                BeforeCanonicalMutationBoundaryAsync = async path =>
                {
                    await cut.Hooks.BeforeCanonicalMutationBoundaryAsync!(path);
                    if (mode == "ordinary_write" && pauseArmed && path == "game_state/world/weather.json" && pauses == 0)
                    { pauses++; entered.TrySetResult(); await allow.Task; }
                }
            });
        cut.Attach(files); files.EnsureDirectoryStructure();
        await using (var seed = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seed);
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
            { "soulName":"Искра", "currentRealm":"Chaos Sea",
              "inkFeathers":{"current":0,"total":0}, "enlightenment":{"experience":0,"level":0},
              "afterlifeCombatProfile":{"spiritFocusTier":0,"artTiers":{"guard":0,"recover_spiritual_power":0}} }
            """);
        await files.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", """
            { "schemaVersion":1, "profiles":[{"actorType":"player_soul","actorId":"player_soul",
              "displayName":"Искра","realm":"Chaos Sea","gmRevision":"before",
              "currencies":{"inkFeathers":4,"lightSparks":0},"progression":{},
              "standardArts":{"guard":1,"recover_spiritual_power":1},"progressionLedger":[]}] }
            """);
        var manager = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance,
            new StateManagerHooks
            {
                AfterPlayerSoulProfileInputsReadAsync = async () =>
                { pauses++; entered.TrySetResult(); await allow.Task; }
            });
        var beforeRuntime = manager.CurrentState;
        var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
        var target = files.ResolvePath(mode == "refresh" ? "game_state/meta/afterlife_entity_profiles.json" : "game_state/world/weather.json");
        cut.Select = (path, _) => path == target;
        var releaseFailure = new IOException("Actual original lease external-context close failure.");
        var closer = new ThrowingClose(releaseFailure);
        FileSystemManager.CanonicalWriteLease? original = null;
        var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            // Attach only after the real publisher has selected its route and emitted
            // its genuine uncertainty. Do not replace the lease or forge the result.
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null &&
                Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        Task? operation = null;
        Exception? failure = null;
        string? actualBoxType = null;
        try
        {
            pauseArmed = true;
            operation = mode == "refresh" ? manager.RefreshGameStateAsync()
                : files.WriteFileAtomicBytesAsync("game_state/world/weather.json", [7, 11, 13]);
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            actualBoxType = operation.GetType().FullName;
            original = InspectOriginalHoistedLease(operation);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive);
            Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe;
            cut.Armed = true; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
        }
        finally
        {
            allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        var lockAvailable = false;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            lockAvailable = true;
        output.WriteLine(JsonSerializer.Serialize(new
        {
            mode, root, actualBoxType, pauses, attachments, closer.Calls,
            failure = failure?.ToString(), samePrimary = ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], releaseFailure),
            releaseFailure = releaseFailure.ToString(), lockAvailable, activeAfter = original!.IsActive,
            ambientClosed = original.AmbientRegistration == null, mainClosed = original.MainAdmission == null,
            contextClosed = original.ExternalPublicationContext == null, runtimeUnchanged = ReferenceEquals(beforeRuntime, manager.CurrentState),
            generationBefore, generationAfter = File.ReadAllBytes(files.SessionGenerationPath), Cut = cut.Evidence()
        }));
        Assert.Equal(1, pauses); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls);
        Assert.Same(cut.OriginalUncertainty, failure);
        Assert.Same(releaseFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission);
        Assert.Null(original.ExternalPublicationContext); Assert.True(lockAvailable);
        Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Same(beforeRuntime, manager.CurrentState); cut.AssertReachedAndStopped();
    }

    private static FileSystemManager.CanonicalWriteLease InspectOriginalHoistedLease(Task operation)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var stateField = operation.GetType().GetField("StateMachine", flags);
        Assert.NotNull(stateField);
        var state = stateField.GetValue(operation);
        Assert.NotNull(state);
        return Assert.Single(state.GetType().GetFields(flags).Select(field => field.GetValue(state))
            .OfType<FileSystemManager.CanonicalWriteLease>().Distinct());
    }

    private sealed class ThrowingClose(IOException failure) : IDisposable
    {
        internal int Calls { get; private set; }
        public void Dispose() { Calls++; throw failure; }
    }
}
