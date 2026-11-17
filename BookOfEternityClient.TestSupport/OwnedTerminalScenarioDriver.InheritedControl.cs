using System.Diagnostics;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

internal static partial class OwnedTerminalScenarioDriver
{
    // Real original helper/root held behind its native gate. The L probe is
    // deliberately a blocked writer, NOT a second lawful launch admission.
    private static async Task<int> RunInheritedControlAsync(string mode, string package, string output)
    {
        if (mode == "terminal-inherited-control-init-failure") return await RunInheritedControlInitFailureAsync(package, output);
        var result = new Dictionary<string, object?>();
        NativeLineageOwner? owner = null;
        IDisposable? gateLease = null;
        Task? pending = null;
        var marker = Path.Combine(output, "unexpected-exec");
        try
        {
            var start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, WorkingDirectory = output };
            start.ArgumentList.Add("-c"); start.ArgumentList.Add("printf released > unexpected-exec");
            owner = await NativeLineageOwner.PrepareTerminalAsync(start, GmWorkerNativePackage.Validate(package),
                Guid.NewGuid().ToString("N"), 80, 25, CancellationToken.None);
            if (!owner.UsesInheritedTerminalBootstrap) throw new InvalidOperationException("Inherited path was not exercised.");
            gateLease = await owner.HoldInheritedControlForTestAsync();
            if (mode == "terminal-inherited-control-abort-timeout")
            {
                using var cancelled = new CancellationTokenSource();
                cancelled.Cancel();
                var timer = Stopwatch.StartNew();
                try { await owner.ReleaseTerminalAsync(cancelled.Token); throw new InvalidOperationException("Cancelled release unexpectedly succeeded."); }
                catch (OperationCanceledException) { }
                if (timer.Elapsed < TimeSpan.FromSeconds(1.5) || timer.Elapsed > TimeSpan.FromSeconds(4))
                    throw new InvalidOperationException("Original abort did not take its bounded gate-timeout path.");
                if (owner.Uncertainty == null || File.Exists(marker))
                    throw new InvalidOperationException("Abort timeout erased uncertainty or released the payload.");
                var beforeRetry = owner.InheritedControlObservation;
                if (!beforeRetry.AbortRequested || beforeRetry.CloseConfirmed || beforeRetry.OriginalHandleClosed)
                    throw new InvalidOperationException("Abort timeout falsely claimed original control closure.");
                var refusedDispose = false;
                try { await owner.DisposeAsync(); } catch (InvalidOperationException) { refusedDispose = true; }
                if (!refusedDispose) throw new InvalidOperationException("Unresolved original owner was disposed.");
                gateLease.Dispose(); gateLease = null;
                var retry = await owner.StopAndObserveAsync();
                if (!retry.CleanupComplete || retry.AuthorityRetained || owner.Uncertainty == null || File.Exists(marker))
                    throw new InvalidOperationException("Same-owner observation failed after releasing the blocked gate.");
                var afterRetry = owner.InheritedControlObservation;
                if (!afterRetry.CloseConfirmed || !afterRetry.OriginalHandleClosed)
                    throw new InvalidOperationException("Same-owner stop did not close the original aborted control handle.");
                result["OriginalControlClosed"] = true;
                result["Success"] = true;
                result["AbortWaitMilliseconds"] = timer.Elapsed.TotalMilliseconds;
                result["DisposeRefused"] = true;
                result["SameOwnerCleanupComplete"] = true;
                return 0;
            }
            var expiry = mode.EndsWith("expiry", StringComparison.Ordinal);
            using var token = new CancellationTokenSource();
            if (!expiry) token.CancelAfter(TimeSpan.FromMilliseconds(200));
            var command = mode.Contains("-launch-", StringComparison.Ordinal) ? 'L' : 'A';
            pending = command == 'L' ? owner.LaunchInheritedAsync(token.Token) : owner.ReleaseInheritedAsync(token.Token);
            var cancelled = false;
            try { await pending.WaitAsync(TimeSpan.FromSeconds(expiry ? 6 : 1)); }
            catch (OperationCanceledException) { cancelled = true; }
            if (!cancelled) throw new InvalidOperationException("Blocked original control did not cancel before gate release.");
            if (!owner.IsInheritedRootHeld)
                throw new InvalidOperationException("Blocked command advanced admission phase.");
            result["CancelledBeforeGateRelease"] = true;
            result["Command"] = command.ToString();
            gateLease.Dispose(); gateLease = null;
            var evidence = await owner.StopAndObserveAsync();
            if (!evidence.CleanupComplete || evidence.AuthorityRetained || owner.Uncertainty == null)
                throw new InvalidOperationException("Unconfirmed command did not retain uncertainty with observed native cleanup.");
            if (File.Exists(marker)) throw new InvalidOperationException("Blocked command released executable.");
            result["Success"] = true;
            result["CleanupComplete"] = true;
            result["UncertaintyRetained"] = true;
            return 0;
        }
        catch (Exception ex) { result["Failure"] = ex.ToString(); return 1; }
        finally
        {
            gateLease?.Dispose();
            if (pending != null) try { await pending.WaitAsync(TimeSpan.FromSeconds(2)); } catch { }
            if (owner != null) try { result["FinalCleanup"] = await owner.StopAndObserveAsync(); } catch (Exception ex) { result["CleanupFailure"] = ex.ToString(); }
            // A failed/uncertain owner is not falsely disposed as positively retired.
            // This disposable driver remains beneath the independent ECHILD guardian.
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(result));
        }
    }
    private static async Task<int> RunInheritedControlInitFailureAsync(string package, string output)
    {
        var result = new Dictionary<string, object?>();
        GmWorkerOwnedLaunch? owner = null;
        NativeLineageOwner? expectedOwner = null;
        var injected = new IOException("injected-before-control-assignment");
        try
        {
            var start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false, WorkingDirectory = output };
            start.ArgumentList.Add("-c"); start.ArgumentList.Add("printf released > unexpected-exec");
            try
            {
                await NativeLineageOwner.PrepareTerminalAsync(start, GmWorkerNativePackage.Validate(package),
                    Guid.NewGuid().ToString("N"), 80, 25, CancellationToken.None,
                    beforeInheritedControl: original => { expectedOwner = original; throw injected; });
                throw new InvalidOperationException("Injected initialization failure was not reached.");
            }
            catch (GmWorkerOwnedLaunchException partial)
            {
                owner = partial.Owner;
                if (!ReferenceEquals(expectedOwner, owner))
                    throw new InvalidOperationException("Returned owner is not the exact original instance.");
                if (!ReferenceEquals(injected, partial.InnerException))
                    throw new InvalidOperationException("Original initialization failure was masked.");
                if (owner is not NativeLineageOwner { UsesInheritedTerminalBootstrap: true })
                    throw new InvalidOperationException("Original inherited owner was not retained.");
            }
            var evidence = await owner.StopAndObserveAsync();
            if (!evidence.CleanupComplete || evidence.AuthorityRetained || owner.AdmittedHostProcessId != null)
                throw new InvalidOperationException("Partial helper cleanup or no-root premise was not proven.");
            if (File.Exists(Path.Combine(output, "unexpected-exec"))) throw new InvalidOperationException("Partial initialization executed payload.");
            result["OriginalOwnerRetained"] = true;
            result["OriginalFailureRetained"] = true;
            result["Success"] = true;
            return 0;
        }
        catch (Exception ex) { result["Failure"] = ex.ToString(); return 1; }
        finally
        {
            var diagnosticOwner = owner ?? expectedOwner;
            if (diagnosticOwner != null) try { result["FinalCleanup"] = await diagnosticOwner.StopAndObserveAsync(); } catch (Exception ex) { result["CleanupFailure"] = ex.ToString(); }
            await File.WriteAllTextAsync(Path.Combine(output, "scenario.json"), JsonSerializer.Serialize(result));
        }
    }
}
