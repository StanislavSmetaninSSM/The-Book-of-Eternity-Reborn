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
}
