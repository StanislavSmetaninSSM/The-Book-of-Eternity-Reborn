using System.Diagnostics;
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
    internal static JointInterfaceCrashBackend SelectBackend(bool windows, bool linux, JointInterfaceCrashMode mode) => JointInterfaceCrashBackend.DirectLinux;

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
    internal static JointInterfaceCrashMode ReadAndValidateEntryMode(string own, int cut, bool windows, bool linux) => new(cut, false, null);

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
        var start = new ProcessStartInfo(Path.Combine(dotnetRoot, "dotnet")) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = own };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["TMPDIR"] = Path.Combine(own, "tmp");
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
    internal static JointInterfaceCrashIdentity ReadLiveIdentity(int pid, Process? originalWindowsProcess) =>
        throw new PlatformNotSupportedException("Windows identity is not implemented.");

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
    internal static JsonObject AddIdentity(JsonObject record, JointInterfaceCrashIdentity identity) => record;

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
    internal static Exception? SaveOutputs(string output, string? stdout, string? stderr, Exception? primary) => primary;

}

/// <summary>
/// Retains one original native owner through failed cleanup without sharing it across tests.
/// </summary>
/// <param name="owner">
/// The original returned or partial-launch owner.
/// </param>
internal sealed class JointInterfaceCrashRetainedOwner(GmWorkerOwnedLaunch owner)
{
    /// <summary>
    /// Gets the original owner, without acquiring replacement authority.
    /// </summary>
    internal GmWorkerOwnedLaunch Owner { get; } = owner;

    /// <summary>
    /// Reports whether this custody token still retains the original owner.
    /// </summary>
    internal bool IsRetained => false;

    /// <summary>
    /// Releases custody only after the original owner confirms its own disposal.
    /// </summary>
    /// <returns>
    /// Completion of confirmed original disposal; failures retain custody.
    /// </returns>
    internal Task DisposeConfirmedAsync() => Task.CompletedTask;

}
