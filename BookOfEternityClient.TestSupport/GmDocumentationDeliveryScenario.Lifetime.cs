using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

internal static partial class GmDocumentationDeliveryScenario
{
    // Adapter qualification only. The independent actual production writer remains
    // RunAsync(publish:true); this controlled probe never loads its source/prefix.
    internal static async Task<Result> RunOwnedLifetimeProbeAsync(string repo, bool missingDriver = false)
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Linux-only owned lifetime probe.");
        var clock = new Clock(); var id = Guid.NewGuid().ToString("N");
        var package = Path.Combine(Path.GetTempPath(), "boe-gm1557-package-" + id);
        var root = Path.Combine(Path.GetTempPath(), "boe-gm1557-session-" + id);
        var artifacts = Path.Combine(Path.GetTempPath(), "boe-gm1557-evidence-" + id);
        var driver = Path.Combine(package, "owned-lifetime.ps1");
        var pidPath = Path.Combine(artifacts, "helper.pid"); var ackPath = Path.Combine(artifacts, "identity-observed.ack");
        string[] Arguments(string mode) => ["-NoLogo", "-NoProfile", "-NonInteractive", "-File", driver,
            "-Mode", mode, "-Root", root, "-Package", package, "-PidPath", pidPath, "-AckPath", ackPath];
        var expectedHelper = Arguments("helper"); var start = new ProcessStartInfo("pwsh")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = package };
        foreach (var argument in Arguments("launcher")) start.ArgumentList.Add(argument);
        var evidence = new JsonObject { ["kind"] = "CONTROLLED_ADAPTER_LIFETIME_NOT_PRODUCER_DELIVERY",
            ["package"] = package, ["root"] = root, ["artifacts"] = artifacts,
            ["workLimitSeconds"] = 45, ["cleanupLimitSeconds"] = 15, ["totalLimitSeconds"] = 60 };
        var ownership = new OwnershipEvidence(); var output = new PipeLog(); var errors = new PipeLog();
        Process? launcher = null; Identity? original = null, helper = null; Task? stdout = null, stderr = null;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var issues = new List<string>(); var cleanupErrors = new List<string>(); string? failure = null;
        bool launchAttempted = false, initialHelperParentProved = false;
        bool orphanObserved = false, exitConfirmed = false, rootsRemoved = false;
        void CensusProbe()
        {
            _ = clock.Operation;
            foreach (var directory in Directory.EnumerateDirectories("/proc").Where(path => int.TryParse(Path.GetFileName(path), out _)))
            {
                Identity candidate;
                try { candidate = ReadIdentity(int.Parse(Path.GetFileName(directory)), "candidate"); }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                var exact = candidate.Args.Length > 0 && Path.GetFileName(candidate.Args[0]) == "pwsh" &&
                    candidate.Args.Skip(1).SequenceEqual(expectedHelper);
                if (exact && (helper is null || candidate.Pid != helper.Pid || candidate.BirthTicks != helper.BirthTicks) ||
                    !exact && candidate.Pid != original?.Pid && (candidate.ParentPid == original?.Pid || candidate.ParentPid == helper?.Pid))
                {
                    ownership.Unresolved = true; issues.Add("delivery.unexpected_lifetime_descendant:" + candidate.Pid);
                }
            }
            if (original is not null) ObserveOwned(original, launcher, ownership, clock, "lifetime_launcher_census");
            if (helper is not null) ObserveOwned(helper, null, ownership, clock, "lifetime_helper_census");
        }
        try
        {
            Directory.CreateDirectory(artifacts); Directory.CreateDirectory(package); Directory.CreateDirectory(root);
            evidence["ownedRootsPresentBeforeDriverCopy"] = Directory.Exists(package) && Directory.Exists(root);
            var driverSource = Path.Combine(repo, "tests/fixtures/GmDocumentationDelivery",
                missingDriver ? "missing-owned-lifetime-" + id + ".ps1" : "owned-lifetime.ps1");
            if (missingDriver && File.Exists(driverSource)) throw new InvalidDataException("delivery.setup_fault_not_absent");
            evidence["setupFaultSourceAbsent"] = missingDriver && !File.Exists(driverSource);
            File.Copy(driverSource, driver);
            evidence["driverHash"] = Hash(File.ReadAllBytes(driver));
            launchAttempted = true;
            launcher = Process.Start(start) ?? throw new InvalidOperationException("Controlled launcher did not start.");
            stdout = output.PumpAsync(launcher.StandardOutput, clock, cancellation.Token);
            stderr = errors.PumpAsync(launcher.StandardError, clock, cancellation.Token);
            original = ReadIdentity(launcher.Id, "pwsh", ownership.Observations, start.ArgumentList.ToArray());
            while (!File.Exists(pidPath)) await Task.Delay(20).WaitAsync(clock.Work);
            var pid = int.Parse(File.ReadAllText(pidPath), System.Globalization.CultureInfo.InvariantCulture);
            var discovered = ReadIdentity(pid, "controlled-helper", ownership.Observations, expectedHelper);
            if (discovered.ParentPid != original.Pid)
            {
                ownership.Unresolved = true;
                throw new InvalidDataException("delivery.initial_helper_parent_unproved");
            }
            helper = discovered; initialHelperParentProved = true;
            CensusProbe();
            if (ownership.Unresolved) throw new InvalidDataException("delivery.initial_lifetime_census_unproved");
            File.WriteAllText(ackPath, "Exact helper birth/argv and initial parent observed.");
            await launcher.WaitForExitAsync().WaitAsync(clock.Work);
            if (launcher.ExitCode != 0 || !ObserveOwned(helper, null, ownership, clock, "after_launcher_exit"))
                throw new InvalidDataException("delivery.controlled_orphan_not_live");
            var orphan = ReadStat(helper.Pid);
            if (orphan.BirthTicks != helper.BirthTicks || orphan.ParentPid == original.Pid || Terminal(orphan))
                throw new InvalidDataException("delivery.controlled_orphan_parent_unproved");
            orphanObserved = true; evidence["orphanAfterLauncherExit"] = JsonSerializer.SerializeToNode(orphan);
            var timeoutAt = clock.Watch.Elapsed.TotalSeconds + 3;
            evidence["controlledTimeoutAtElapsedSeconds"] = timeoutAt;
            while (clock.Watch.Elapsed.TotalSeconds < timeoutAt)
            {
                CensusProbe();
                if (ownership.Unresolved || output.Error is not null || errors.Error is not null ||
                    !ObserveOwned(helper, null, ownership, clock, "controlled_timeout_wait"))
                    throw new InvalidDataException("delivery.controlled_lifetime_prerequisite_failed");
                await Task.Delay(20).WaitAsync(clock.Work);
            }
            throw new TimeoutException("delivery.controlled_owned_lifetime_timeout");
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            evidence["workElapsedSeconds"] = clock.Watch.Elapsed.TotalSeconds; clock.BeginCleanup();
            try
            {
                CensusProbe();
                foreach (var identity in new[] { helper, original }.OfType<Identity>())
                {
                    var managed = identity == original ? launcher : null;
                    if (!ObserveOwned(identity, managed, ownership, clock, "lifetime_before_handle")) continue;
                    using var process = Process.GetProcessById(identity.Pid);
                    if (!ObserveOwned(identity, managed, ownership, clock, "lifetime_before_signal")) continue;
                    process.Kill(); await process.WaitForExitAsync().WaitAsync(clock.Cleanup);
                }
                if (launcher is not null) await launcher.WaitForExitAsync().WaitAsync(clock.Cleanup);
                CensusProbe();
                var remaining = new[] { helper, original }.OfType<Identity>().Where(identity =>
                    ObserveOwned(identity, identity == original ? launcher : null, ownership, clock, "lifetime_final_exit")).ToArray();
                evidence["remaining"] = JsonSerializer.SerializeToNode(remaining);
                var noStart = !launchAttempted && launcher is null && original is null && helper is null;
                // Physical cleanup authority is independent of successful orphan/pipe semantics.
                // An attempted launch without retained identity, or any unknown identity, stays unproved.
                exitConfirmed = !ownership.Unresolved && (noStart ||
                    original is not null && helper is not null && initialHelperParentProved &&
                    remaining.Length == 0 && launcher?.HasExited == true);
                evidence["exitProof"] = !exitConfirmed ? "unproved" : noStart
                    ? "demonstrably_no_process_start" : "all_exact_retained_owned_identities_closed";
                if (!exitConfirmed) throw new InvalidDataException("delivery.lifetime_owned_exit_unproved");
                foreach (var path in new[] { package, root })
                {
                    _ = clock.Cleanup;
                    if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
                    if (Directory.Exists(path)) throw new IOException("delivery.lifetime_root_removal_unproved");
                }
                rootsRemoved = true;
                if (stdout is not null && stderr is not null)
                    await Task.WhenAll(stdout, stderr).WaitAsync(clock.Cleanup);
                if (!noStart && (stdout is null || stderr is null || !output.Eof || !errors.Eof ||
                    output.Error is not null || errors.Error is not null))
                    cleanupErrors.Add("delivery.lifetime_pipe_qualification_failed");
            }
            catch (Exception error) { cleanupErrors.Add(error.ToString()); }
            finally
            {
                cancellation.Cancel();
                if (launcher is not null) { launcher.StandardOutput.Dispose(); launcher.StandardError.Dispose(); launcher.Dispose(); }
                evidence["stdout"] = output.Text; evidence["stderr"] = errors.Text;
                evidence["stdoutEof"] = output.Eof; evidence["stderrEof"] = errors.Eof;
                evidence["stdoutFailure"] = output.Error; evidence["stderrFailure"] = errors.Error;
                evidence["identities"] = JsonSerializer.SerializeToNode(new[] { original, helper });
                evidence["ownershipObservations"] = ownership.Observations; evidence["ownershipUnresolved"] = ownership.Unresolved;
                evidence["launchAttempted"] = launchAttempted;
                evidence["initialHelperParentProved"] = initialHelperParentProved;
                evidence["pipeQualification"] = !launchAttempted ? "not_started" :
                    output.Eof && errors.Eof && output.Error is null && errors.Error is null ? "complete" : "failed";
                evidence["orphanPrerequisitesProved"] = initialHelperParentProved && orphanObserved &&
                    !ownership.Unresolved && output.Eof && errors.Eof && output.Error is null && errors.Error is null;
                evidence["orphanObserved"] = orphanObserved; evidence["exitConfirmed"] = exitConfirmed; evidence["rootsRemoved"] = rootsRemoved;
                evidence["elapsedSeconds"] = clock.Watch.Elapsed.TotalSeconds;
                if (clock.Watch.Elapsed.TotalSeconds > 60) cleanupErrors.Add("delivery.absolute_deadline_exceeded");
                evidence["failure"] = failure; evidence["cleanupFailures"] = JsonSerializer.SerializeToNode(cleanupErrors);
                evidence["issues"] = JsonSerializer.SerializeToNode(issues);
                if (Directory.Exists(artifacts))
                {
                    File.WriteAllText(Path.Combine(artifacts, "stdout.log"), output.Text);
                    File.WriteAllText(Path.Combine(artifacts, "stderr.log"), errors.Text);
                    File.WriteAllText(Path.Combine(artifacts, "outcome.json"), evidence.ToJsonString());
                }
            }
        }
        if (cleanupErrors.Count != 0) issues.Add("delivery.cleanup_failed");
        return new(evidence, null, failure, issues);
    }
}
