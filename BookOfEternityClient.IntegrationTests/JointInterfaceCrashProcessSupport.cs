using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Selects the process transport without conflating Windows and Linux evidence.
/// </summary>
internal enum JointInterfaceCrashBackend
{
    /// <summary>
    /// Ordinary Linux launch through the existing native owner.
    /// </summary>
    NativeLinux,
    /// <summary>
    /// Existing Linux integrity or diagnostic direct launch.
    /// </summary>
    DirectLinux,
    /// <summary>
    /// Ordinary Windows launch contained by the test runner.
    /// </summary>
    DirectWindows
}

/// <summary>
/// Describes the exact crash scenario requested by the parent.
/// </summary>
/// <param name="Cut">
/// The original cut index from zero through four.
/// </param>
/// <param name="Diagnostic">
/// Whether this is the existing Linux diagnostic consumer.
/// </param>
/// <param name="Fault">
/// The existing integrity fault, or null for an unmodified ordinary scenario.
/// </param>
internal sealed record JointInterfaceCrashMode(int Cut, bool Diagnostic, string? Fault);

/// <summary>
/// Keeps independently observed process birth tokens distinct by operating system.
/// </summary>
/// <param name="Pid">
/// The original live process identifier.
/// </param>
/// <param name="BirthUtc">
/// The independently observed UTC process creation time.
/// </param>
/// <param name="BirthKind">
/// The explicit platform-specific birth token kind.
/// </param>
/// <param name="LinuxBirthTick">
/// Linux stat field22, or null on Windows.
/// </param>
/// <param name="WindowsStartTimeUtcTicks">
/// Windows creation time in UTC ticks, or null on Linux.
/// </param>
internal sealed record JointInterfaceCrashIdentity(int Pid, DateTime BirthUtc, string BirthKind, ulong? LinuxBirthTick, long? WindowsStartTimeUtcTicks);

/// <summary>
/// Provides ordinary crash child mode, identity and output contracts.
/// </summary>
internal static class JointInterfaceCrashProcessSupport
{
    /// <summary>
    /// Selects the finite wait for a direct crash child or an explicitly extended Windows source probe.
    /// </summary>
    /// <param name="windows">
    /// Whether the requested child runs on Windows.
    /// </param>
    /// <param name="role">
    /// The original dispatcher role; only the source-probe role may request an extension.
    /// </param>
    /// <param name="extendedSourceProbe">
    /// Whether to request the fixed extended observation window instead of the ordinary wait.
    /// </param>
    /// <returns>
    /// The selected finite child wait; an ineligible extension request is rejected.
    /// </returns>
    internal static TimeSpan GetDirectChildTimeout(bool windows, string role, bool extendedSourceProbe)
    {
        if (!extendedSourceProbe) return TimeSpan.FromSeconds(220);
        if (!windows || !string.Equals(role, "engine-joint-interface-source-probe", StringComparison.Ordinal))
            throw new InvalidDataException("The extended child wait is only available to the Windows source42 probe.");
        return TimeSpan.FromSeconds(600);
    }

    /// <summary>
    /// Preserves the diagnostic-only short circuit before inspecting managed capture settings.
    /// </summary>
    /// <param name="mode">
    /// The already validated parent mode.
    /// </param>
    /// <param name="enabled">
    /// Reads the existing capture setting only for a diagnostic consumer.
    /// </param>
    /// <returns>
    /// <see langword="true"/> only for an enabled diagnostic capture; otherwise <see langword="false"/>.
    /// </returns>
    internal static bool UsesManagedCapture(JointInterfaceCrashMode mode, Func<bool> enabled) => mode.Diagnostic && enabled();

    /// <summary>
    /// Selects an allowed backend before allocating a game fixture.
    /// </summary>
    /// <param name="windows">
    /// Whether the requested platform is Windows.
    /// </param>
    /// <param name="linux">
    /// Whether the requested platform is Linux.
    /// </param>
    /// <param name="mode">
    /// The explicitly supplied scenario.
    /// </param>
    /// <returns>
    /// The permitted process backend; unsupported modes are rejected.
    /// </returns>
    internal static JointInterfaceCrashBackend SelectBackend(bool windows, bool linux, JointInterfaceCrashMode mode)
    {
        if (windows == linux) throw new PlatformNotSupportedException("CRASH5 requires exactly one supported platform.");
        if (mode.Cut is < 0 or > 4 || (windows && (mode.Diagnostic || mode.Fault != null)))
            throw new InvalidDataException("CRASH5 mode is not an ordinary Windows cut0..4.");
        return windows ? JointInterfaceCrashBackend.DirectWindows :
            mode.Diagnostic || mode.Fault != null ? JointInterfaceCrashBackend.DirectLinux : JointInterfaceCrashBackend.NativeLinux;
    }

    /// <summary>
    /// Reads explicit parent mode fields before either public child entry creates a fixture.
    /// </summary>
    /// <param name="own">
    /// The current test's owned directory containing parent.json.
    /// </param>
    /// <param name="cut">
    /// The entrypoint's requested cut, which must match the parent.
    /// </param>
    /// <param name="windows">
    /// Whether the process is running on Windows.
    /// </param>
    /// <param name="linux">
    /// Whether the process is running on Linux.
    /// </param>
    /// <returns>
    /// The validated parent mode; missing or malformed fields are rejected.
    /// </returns>
    internal static JointInterfaceCrashMode ReadAndValidateEntryMode(string own, int cut, bool windows, bool linux)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(own, "parent.json")));
            var parent = document.RootElement;
            var mode = new JointInterfaceCrashMode(parent.GetProperty("Cut").GetInt32(),
                parent.GetProperty("Diagnostic").GetBoolean(), parent.GetProperty("Fault").ValueKind == JsonValueKind.Null
                    ? null : parent.GetProperty("Fault").GetString());
            if (mode.Cut != cut) throw new InvalidDataException("CRASH5 mode cut does not match its parent.");
            _ = SelectBackend(windows, linux, mode);
            return mode;
        }
        catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException)
        {
            throw new InvalidDataException("CRASH5 mode metadata is missing, invalid or forbidden.", error);
        }
    }

    /// <summary>
    /// Builds a literal child command with its private temporary directory.
    /// </summary>
    /// <param name="dotnetRoot">
    /// The absolute installed runtime directory.
    /// </param>
    /// <param name="arguments">
    /// Literal argument values passed without shell interpolation.
    /// </param>
    /// <param name="own">
    /// The absolute owned test directory.
    /// </param>
    /// <param name="windows">
    /// Whether to select dotnet.exe and TEMP/TMP instead of dotnet and TMPDIR.
    /// </param>
    /// <returns>
    /// A separate child configuration; process-wide environment is unchanged.
    /// </returns>
    internal static ProcessStartInfo CreateChildStartInfo(string dotnetRoot, string[] arguments, string own, bool windows)
    {
        var executable = Path.Combine(dotnetRoot, windows ? "dotnet.exe" : "dotnet");
        if (!Path.IsPathFullyQualified(dotnetRoot) || !Path.IsPathFullyQualified(own) || !File.Exists(executable))
            throw new InvalidDataException("CRASH5 requires an installed absolute runtime and owned working directory.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = own };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (windows)
        {
            start.Environment["TEMP"] = Path.Combine(own, "tmp");
            start.Environment["TMP"] = Path.Combine(own, "tmp");
        }
        else start.Environment["TMPDIR"] = Path.Combine(own, "tmp");
        return start;
    }

    /// <summary>
    /// Reads a live identity independently of child-authored JSON.
    /// </summary>
    /// <param name="pid">
    /// The expected original process identifier.
    /// </param>
    /// <param name="originalWindowsProcess">
    /// The retained Windows Process, or null for an owned Linux root.
    /// </param>
    /// <returns>
    /// An explicit platform identity without reinterpreting Linux kernel ticks.
    /// </returns>
    internal static JointInterfaceCrashIdentity ReadLiveIdentity(int pid, Process? originalWindowsProcess)
    {
        if (OperatingSystem.IsWindows())
        {
            if (originalWindowsProcess == null || originalWindowsProcess.Id != pid)
                throw new InvalidDataException("CRASH5 Windows identity requires the original Process.");
            var birth = originalWindowsProcess.StartTime.ToUniversalTime();
            if (birth.Ticks <= 0) throw new InvalidDataException("CRASH5 Windows birth timestamp is invalid.");
            return new(pid, birth, "windows-process-start-utc-ticks", null, birth.Ticks);
        }
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("CRASH5 identity requires Windows or Linux.");
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        var opening = stat.IndexOf(' '); var closing = stat.LastIndexOf(')');
        if (opening < 1 || closing < opening || closing + 2 >= stat.Length || stat[closing + 1] != ' ' ||
            int.Parse(stat.AsSpan(0, opening), CultureInfo.InvariantCulture) != pid)
            throw new InvalidDataException("CRASH5 Linux process identity is malformed.");
        var fields = stat[(closing + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length <= 19 || !ulong.TryParse(fields[19], NumberStyles.None, CultureInfo.InvariantCulture, out var tick) || tick == 0)
            throw new InvalidDataException("CRASH5 Linux birth tick is invalid.");
        using var process = Process.GetProcessById(pid);
        return new(pid, process.StartTime.ToUniversalTime(), "linux-proc-start-tick", tick, null);
    }

    /// <summary>
    /// Reads the child process's own identity independently of its parent's observation.
    /// </summary>
    /// <returns>
    /// The current process's explicit platform identity.
    /// </returns>
    internal static JointInterfaceCrashIdentity ReadCurrentIdentity()
    {
        using var process = Process.GetCurrentProcess();
        return ReadLiveIdentity(process.Id, OperatingSystem.IsWindows() ? process : null);
    }

    /// <summary>
    /// Adds platform birth fields while preserving the existing Linux JSON shape.
    /// </summary>
    /// <param name="record">
    /// The record to receive process identity fields.
    /// </param>
    /// <param name="identity">
    /// The independently observed original process identity.
    /// </param>
    /// <returns>
    /// The same record with identity fields.
    /// </returns>
    internal static JsonObject AddIdentity(JsonObject record, JointInterfaceCrashIdentity identity)
    {
        record["Pid"] = identity.Pid;
        record["BirthUtc"] = identity.BirthUtc;
        if (identity.BirthKind == "windows-process-start-utc-ticks" && identity.WindowsStartTimeUtcTicks is > 0 && identity.LinuxBirthTick == null)
        {
            record.Remove("BirthTick");
            record["BirthKind"] = identity.BirthKind;
            record["WindowsStartTimeUtcTicks"] = identity.WindowsStartTimeUtcTicks;
        }
        else if (identity.BirthKind == "linux-proc-start-tick" && identity.LinuxBirthTick is > 0 && identity.WindowsStartTimeUtcTicks == null)
        {
            record.Remove("BirthKind"); record.Remove("WindowsStartTimeUtcTicks");
            record["BirthTick"] = identity.LinuxBirthTick;
        }
        else throw new InvalidDataException("CRASH5 identity token kind is inconsistent.");
        return record;
    }

    /// <summary>
    /// Serializes a child result with that child's independently read platform identity.
    /// </summary>
    /// <param name="fields">
    /// The existing scenario result fields, without a parent-supplied identity.
    /// </param>
    /// <param name="stages">
    /// The ordinary stage reporter, or <see langword="null"/> to preserve diagnostic and integrity result fields.
    /// </param>
    /// <returns>
    /// JSON preserving Linux birth fields and explicitly identifying Windows creation ticks.
    /// </returns>
    internal static string SerializeCurrentIdentity(object fields, JointInterfaceCrashStageReport? stages = null)
    {
        var result = AddIdentity(JsonSerializer.SerializeToNode(fields)!.AsObject(), ReadCurrentIdentity());
        if (stages != null) result["OrdinaryStageOutputFailure"] = stages.OutputFailure;
        return result.ToJsonString();
    }

    /// <summary>
    /// Runs one ordinary Linux child through the original finite native owner and scoped cleanup.
    /// </summary>
    /// <param name="start">
    /// The absolute child command with its private environment.
    /// </param>
    /// <param name="output">
    /// The owned result path used as the raw stream filename prefix.
    /// </param>
    /// <param name="retain">
    /// Idempotently attaches the original owner, including a partial owner with no admitted PID.
    /// </param>
    /// <param name="disposeAndJoin">
    /// Disposes and releases the attached original owner only after confirmed retirement and EOF.
    /// </param>
    /// <param name="report">
    /// Writes parent evidence without changing the primary child failure.
    /// </param>
    /// <returns>
    /// The original root result after positive scoped retirement, complete streams and disposal.
    /// </returns>
    internal static async Task<JointInterfaceCrashNativeResult> RunNativeAsync(ProcessStartInfo start, string output,
        Action<NativeLineageOwner> retain, Func<NativeLineageOwner, Task> disposeAndJoin, Action<object> report)
    {
        var package = Environment.GetEnvironmentVariable("BOE_CRASH5_NATIVE_PACKAGE")
            ?? throw new InvalidDataException("Prepare BOE_CRASH5_NATIVE_PACKAGE before ordinary Linux crash5.");
        NativeLineageOwner? owner = null;
        JointInterfaceCrashIdentity? identity = null;
        GmWorkerStopEvidence? stop = null;
        GmWorkerOwnedOutputs? outputs = null;
        Exception? failure = null;
        int? supervisorPid = null;
        var joined = false;
        var watch = Stopwatch.StartNew();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(220));
        try
        {
            owner = await NativeLineageOwner.StartFinitePipeAsync(start, package, (original, pid) =>
            {
                owner = original;
                retain(original);
                supervisorPid = original.SupervisorProcessId;
                identity = ReadLiveIdentity(pid, null);
            }, deadline.Token);
            await owner.HostExited.WaitAsync(deadline.Token);
        }
        catch (GmWorkerOwnedLaunchException error)
        {
            owner = (NativeLineageOwner)error.Owner;
            retain(owner);
            failure = error;
        }
        catch (Exception error) { failure = error; }

        if (owner != null)
        {
            try
            {
                stop = await owner.StopAndObserveAsync().WaitAsync(TimeSpan.FromSeconds(10));
                if (stop.State != GmWorkerStopState.StoppedWithinScope || !stop.CleanupComplete || stop.AuthorityRetained ||
                    !owner.HostExited.IsCompletedSuccessfully || !owner.SupervisorExited.IsCompletedSuccessfully)
                    throw new InvalidOperationException("CRASH5 native retirement remains unconfirmed.");
                outputs = await owner.SettleOutputsAsync().WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception error) { failure = PreserveFailure(failure, error, "native-retirement"); }
            var stdout = outputs?.StandardOutput ?? (owner.HostStandardOutput.IsCompletedSuccessfully ? owner.HostStandardOutput.Result : null);
            var stderr = outputs?.StandardError ?? (owner.HostStandardError.IsCompletedSuccessfully ? owner.HostStandardError.Result : null);
            failure = SaveOutputs(output, stdout, stderr, failure);
            if (outputs != null)
            {
                try { await disposeAndJoin(owner); joined = true; }
                catch (Exception error) { failure = PreserveFailure(failure, error, "native-disposal"); }
            }
            try
            {
                report(new
                {
                    NativeRunId = owner.Identity.RunId, SupervisorPid = supervisorPid, Root = identity,
                    BootstrapDirectory = owner.BootstrapDirectory, Stop = stop, Eof = outputs != null, Joined = joined,
                    ElapsedMilliseconds = watch.Elapsed.TotalMilliseconds, RawStdout = stdout, RawStderr = stderr,
                    Failure = failure?.ToString(), RetainedOriginalOwner = !joined,
                    SecondaryFailures = failure?.Data.Cast<System.Collections.DictionaryEntry>().Select(entry => new
                        { Stage = entry.Key.ToString(), Error = entry.Value?.ToString() }).ToArray()
                });
            }
            catch (Exception error) { failure = PreserveFailure(failure, error, "native-report"); }
        }
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        if (identity == null || stop?.RootExitCode == null || outputs == null || !joined)
            throw new InvalidOperationException("CRASH5 native result lacks complete original-owner evidence.");
        return new(identity, stop.RootExitCode.Value, outputs.StandardOutput, outputs.StandardError,
            new { NativeRunId = owner!.Identity.RunId, SupervisorPid = supervisorPid, Stop = stop, BootstrapDirectory = owner.BootstrapDirectory });
    }

    /// <summary>
    /// Keeps the first failure while retaining a separately named secondary failure.
    /// </summary>
    /// <param name="primary">
    /// The original failure, or null when this is the first error.
    /// </param>
    /// <param name="secondary">
    /// The cleanup or observation error that must remain visible.
    /// </param>
    /// <param name="stage">
    /// The bounded stage label under which the secondary error is retained.
    /// </param>
    /// <returns>
    /// The original failure with attached evidence, or the secondary failure when no primary exists.
    /// </returns>
    internal static Exception PreserveFailure(Exception? primary, Exception secondary, string stage)
    {
        if (primary == null) return secondary;
        primary.Data["CRASH5-" + stage + "-" + primary.Data.Count.ToString(CultureInfo.InvariantCulture)] = secondary;
        return primary;
    }

    /// <summary>
    /// Persists complete output streams and preserves primary error precedence.
    /// </summary>
    /// <param name="output">
    /// The output filename prefix owned by this child run.
    /// </param>
    /// <param name="stdout">
    /// The complete standard output, or null when EOF was not observed.
    /// </param>
    /// <param name="stderr">
    /// The complete standard error, or null when EOF was not observed.
    /// </param>
    /// <param name="primary">
    /// An existing failure, or null before output persistence.
    /// </param>
    /// <returns>
    /// The original failure with secondary errors, a new output failure, or null on success.
    /// </returns>
    internal static Exception? SaveOutputs(string output, string? stdout, string? stderr, Exception? primary)
    {
        foreach (var (suffix, text) in new[] { (".stdout", stdout), (".stderr", stderr) })
        {
            if (text == null) continue;
            try { File.WriteAllText(output + suffix, text); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (primary == null) primary = error;
                else primary.Data["CRASH5-output" + suffix] = error;
            }
        }
        return primary;
    }

}

/// <summary>
/// Carries the original finite root outcome after confirmed native retirement.
/// </summary>
/// <param name="Identity">
/// The independent parent identity recorded before exec.
/// </param>
/// <param name="ExitCode">
/// The root exit code, distinct from the supervisor result.
/// </param>
/// <param name="Stdout">
/// Complete standard output after EOF.
/// </param>
/// <param name="Stderr">
/// Complete standard error after EOF.
/// </param>
/// <param name="Evidence">
/// Original run, supervisor and scoped retirement observations.
/// </param>
internal sealed record JointInterfaceCrashNativeResult(JointInterfaceCrashIdentity Identity, int ExitCode,
    string Stdout, string Stderr, object Evidence);

/// <summary>
/// Retains one original native owner through failed cleanup without sharing it across tests.
/// </summary>
/// <param name="owner">
/// The original returned or partial-launch owner.
/// </param>
internal sealed class JointInterfaceCrashRetainedOwner(GmWorkerOwnedLaunch owner)
{
    private GCHandle _retention = GCHandle.Alloc(owner, GCHandleType.Normal);
    /// <summary>
    /// Gets the original owner, without acquiring replacement authority.
    /// </summary>
    internal GmWorkerOwnedLaunch Owner { get; } = owner;

    /// <summary>
    /// Reports whether this custody token still retains the original owner.
    /// </summary>
    internal bool IsRetained => _retention.IsAllocated;

    /// <summary>
    /// Releases custody only after the original owner confirms its own disposal.
    /// </summary>
    /// <returns>
    /// Completion of confirmed original disposal; failures retain custody.
    /// </returns>
    internal async Task DisposeConfirmedAsync()
    {
        if (!_retention.IsAllocated) return;
        await Owner.DisposeAsync();
        _retention.Free();
    }

}
