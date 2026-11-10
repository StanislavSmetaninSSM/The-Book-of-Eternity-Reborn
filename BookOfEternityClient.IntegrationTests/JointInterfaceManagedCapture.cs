#nullable enable
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

// Original owning test parent only. The dump request executes outside the resumer.
internal static class JointInterfaceManagedCapture
{
    internal sealed record Outcome(bool WatchdogLaunched, int? WatchdogNativeExit, bool WatchdogJoined, string[] ObserverErrors);
    internal const string Contract = "EXACT_RECEIPT_FRESH_STICKY_HEALTH_CAPTURE_PHASE_UNKNOWN_ALLOWED";
    internal static bool Enabled => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("L01_G01_SETTINGS"));
    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
    static JsonObject Settings()
    {
        var path = Environment.GetEnvironmentVariable("L01_G01_SETTINGS")!;
        if (Hash(path) != Environment.GetEnvironmentVariable("L01_G01_SETTINGS_SHA256")) throw new InvalidDataException("G01 settings image differs");
        var s = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        if (s["Contract"]?.GetValue<string>() != Contract || !Guid.TryParseExact(s["RunId"]?.GetValue<string>(), "N", out _))
            throw new InvalidDataException("G01 receipt admission contract differs");
        return s;
    }

    // Claim before the original child: an accidental second Fact cannot repeat the fixture operation.
    internal static string ClaimOwnedDirectory(int cut, string? fault)
    {
        var s = Settings();
        if (cut != 1 || fault is not null || Path.GetFullPath(Path.GetTempPath()) != Path.GetFullPath(s["FixtureParent"]!.GetValue<string>()) + Path.DirectorySeparatorChar)
            throw new InvalidDataException("G01 exact fresh-fixture admission differs");
        var claim = Path.Combine(s["ActualRoot"]!.GetValue<string>(), "fixture.once");
        using (new FileStream(claim, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
        var own = Path.Combine(Path.GetTempPath(), "g-" + Guid.NewGuid().ToString("N")[..12]);
        if (Directory.Exists(own) || Encoding.UTF8.GetByteCount(own + "/tmp/dotnet-diagnostic-9999999999-99999999999999999999-socket") > 107)
            throw new InvalidDataException("G01 unused private IPC namespace does not fit");
        Directory.CreateDirectory(own, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Atomic(Path.Combine(s["ActualRoot"]!.GetValue<string>(), "fixture.json"), new { Own = own, RunId = s["RunId"]!.GetValue<string>(), OwnerPid = Environment.ProcessId });
        return own;
    }

    static void Atomic(string path, object value)
    {
        var temp = path + ".next";
        File.WriteAllText(temp, JsonSerializer.Serialize(value));
        File.Move(temp, path, overwrite: true);
    }

    internal static Task<Outcome> ObserveAsync(Process child, ulong birthTick, string root, string nonce, int cut,
        string own, string executable, string[] arguments, long deadline, JointInterfaceDiagnosticDelivery delivery,
        CancellationToken stop) => Task.Run(async () =>
    {
        var dir = Path.Combine(own, "capture");
        var events = new List<object>(); var errors = new List<string>();
        Process? watchdog = null; Task<string>? output = null, stderr = null;
        var policy = new JointInterfaceManagedCapturePolicy();
        var decision = new JointInterfaceManagedCapturePolicy.Decision("WAITING", null, false);
        long sequence = 0; long? cancelledAt = null;
        var cancelled = false; var launched = false; bool joined = false;
        JointInterfaceDiagnosticDelivery.Health? lastHealth = null;
        int? watchdogNative = null;
        try
        {
            Directory.CreateDirectory(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var settings = Settings();
            var ownerTick = ReadBirth(Environment.ProcessId);
            var baseManifest = new JsonObject {
                ["RunId"] = settings["RunId"]!.DeepClone(), ["SettingsPath"] = Environment.GetEnvironmentVariable("L01_G01_SETTINGS"),
                ["SettingsSha256"] = Environment.GetEnvironmentVariable("L01_G01_SETTINGS_SHA256"),
                ["OwnerPid"] = Environment.ProcessId, ["OwnerBirthTick"] = ownerTick,
                ["TargetPid"] = child.Id, ["TargetBirthTick"] = birthTick, ["Root"] = root, ["Nonce"] = nonce, ["Cut"] = cut,
                ["Own"] = own, ["TargetExecutable"] = executable, ["TargetCommand"] = JsonSerializer.SerializeToNode(new[] { executable }.Concat(arguments).ToArray()),
                ["DeadlineStamp"] = deadline, ["ParentFileSha256"] = Hash(Path.Combine(own, "parent.json")),
                ["HealthPath"] = Path.Combine(dir, "health.json"), ["DumpPath"] = Path.Combine(dir, "game-normal.core") };
            while (true)
            {
                var now = Stopwatch.GetTimestamp();
                var health = delivery.ReadHealth();
                lastHealth = health;
                var exited = child.HasExited;
                if (stop.IsCancellationRequested || exited || now >= deadline) cancelled = true;
                JointInterfaceDiagnosticDelivery.Event? item = null;
                while (delivery.Reader.TryRead(out var next))
                {
                    item = next;
                    if (events.Count >= 256) { cancelled = true; if (!errors.Contains("OBSERVED_EVENT_LIMIT256")) errors.Add("OBSERVED_EVENT_LIMIT256"); break; }
                    events.Add(new { next.Ordinal, next.LineOrdinal, next.CharacterOffset, next.ReceivedTimestamp,
                        next.SourceElapsedMilliseconds, next.Stage, next.ControlDetail, next.Control, next.Disposition, next.RawLineSha256 });
                    var eventNow = Stopwatch.GetTimestamp();
                    var step = policy.Step(next, delivery.ReadHealth(), eventNow, (deadline - eventNow) / (double)Stopwatch.Frequency);
                    if (step.Launch) decision = step;
                    else if (!decision.Launch) decision = step;
                    if (step.State == "CANCELLED") cancelled = true;
                }
                now = Stopwatch.GetTimestamp(); health = delivery.ReadHealth(); lastHealth = health;
                if (!decision.Launch) decision = policy.Step(null, health, now, (deadline - now) / (double)Stopwatch.Frequency);
                if (decision.State == "CANCELLED" || !JointInterfaceManagedCapturePolicy.Healthy(health)) cancelled = true;
                if (cancelled) cancelledAt ??= now;
                sequence++;
                // This is receipt admission, never a claim that CapturePhase is known continuously.
                Atomic(Path.Combine(dir, "health.json"), new { Pid = child.Id, BirthTick = birthTick, Root = root, Nonce = nonce, Cut = cut,
                    Sequence = sequence, Stamp = now, Frequency = Stopwatch.Frequency, DeadlineStamp = deadline,
                    Cancelled = cancelled, ReceiptAdmitted = decision.State == "ADMITTED" && !cancelled,
                    PolicyState = decision.State, Health = health, CapturePhase = "UNKNOWN" });
                if (decision.Launch && !cancelled && !launched)
                {
                    launched = true;
                    baseManifest["HealthFloor"] = sequence;
                    Atomic(Path.Combine(dir, "admission.json"), baseManifest);
                    var start = new ProcessStartInfo(settings["Python"]!.GetValue<string>()) { UseShellExecute = false,
                        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = dir };
                    start.ArgumentList.Add(settings["Watchdog"]!.GetValue<string>()); start.ArgumentList.Add(Path.Combine(dir, "admission.json"));
                    watchdog = Process.Start(start) ?? throw new InvalidOperationException("G01 watchdog not started");
                    output = watchdog.StandardOutput.ReadToEndAsync(); stderr = watchdog.StandardError.ReadToEndAsync();
                }
                decision = decision with { Launch = false };
                if (watchdog is not null && cancelled && !watchdog.HasExited)
                {
                    try { await watchdog.StandardInput.WriteLineAsync("CANCEL"); await watchdog.StandardInput.FlushAsync(); }
                    catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException) { errors.Add("Cancel pipe: " + e.GetType().Name); }
                }
                if (watchdog is not null && watchdog.HasExited && !joined)
                {
                    await watchdog.WaitForExitAsync(); watchdogNative = watchdog.ExitCode;
                    await Task.WhenAll(output!, stderr!); joined = true;
                    Atomic(Path.Combine(dir, "watchdog-output.json"), new { NativeExit = watchdogNative, Stdout = output!.Result, Stderr = stderr!.Result });
                }
                if (cancelled && (watchdog is null || joined)) break;
                if (cancelledAt is long at && Stopwatch.GetElapsedTime(at).TotalSeconds >= 5)
                { errors.Add("OWNED_WATCHDOG_JOIN_UNKNOWN_RETAIN_ROOT"); break; }
                await Task.Delay(20);
            }
        }
        catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException)
        {
            errors.Add(e.GetType().Name); cancelled = true;
            try { File.WriteAllText(Path.Combine(dir, "cancel"), "OBSERVER_ERROR"); } catch { /* missing fresh state also refuses request */ }
            if (watchdog is not null)
            {
                try { await watchdog.StandardInput.WriteLineAsync("CANCEL"); await watchdog.StandardInput.FlushAsync(); }
                catch (Exception c) when (c is not OutOfMemoryException and not StackOverflowException) { errors.Add("Cancel pipe: " + c.GetType().Name); }
                try { await watchdog.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); watchdogNative = watchdog.ExitCode; joined = true; }
                catch (Exception c) when (c is not OutOfMemoryException and not StackOverflowException) { errors.Add("Owned join: " + c.GetType().Name); }
            }
        }
        finally
        {
            try { Atomic(Path.Combine(dir, "parent-capture.json"), new { Kind = "G01_PARENT_OBSERVER", TargetPid = child.Id, BirthTick = birthTick,
                Root = root, Nonce = nonce, Cut = cut, Decision = decision, Cancelled = cancelled, WatchdogLaunched = launched,
                WatchdogNativeExit = watchdogNative, WatchdogJoined = joined, ObserverErrors = errors,
                ObservedEventInterval = events, FinalHealth = lastHealth, CapturePhase = "UNKNOWN", SourceEmissionCompleteness = "UNKNOWN", SuspensionDuration = "UNKNOWN" }); }
            catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException) { errors.Add("Record: " + e.GetType().Name); }
            try { watchdog?.Dispose(); }
            catch (Exception e) when (e is not OutOfMemoryException and not StackOverflowException) { errors.Add("Watchdog dispose: " + e.GetType().Name); }
        }
        return new Outcome(launched, watchdogNative, joined, errors.ToArray());
    });

    static ulong ReadBirth(int pid)
    {
        var raw = File.ReadAllText($"/proc/{pid}/stat");
        return ulong.Parse(raw[(raw.LastIndexOf(')') + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries)[19]);
    }
}
