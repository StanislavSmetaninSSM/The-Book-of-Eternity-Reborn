using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Restricts passive first-recovery profiling to the ordinary cache-enabled Windows selection.
/// </summary>
internal static class JointPassiveTimingControl
{
    /// <summary>
    /// Parses the closed diagnostic setting before fixture access.
    /// </summary>
    /// <param name="setting">
    /// Exactly on enables the window; off or <see langword="null"/> leaves it disabled.
    /// </param>
    /// <returns>
    /// Whether the passive diagnostic window is requested.
    /// </returns>
    internal static bool Parse(string? setting) => setting switch
    {
        null or "off" => false,
        "on" => true,
        _ => throw new InvalidDataException("BOE_TEST_JOINT_PASSIVE_TIMINGS must be exactly on or off.")
    };

    /// <summary>
    /// Checks ordinary source, cache and callback selection without opening any fixture.
    /// </summary>
    /// <param name="enabled">
    /// Whether the passive profiling window is enabled.
    /// </param>
    /// <param name="parent">
    /// Parent-authored observation and cache selection metadata.
    /// </param>
    /// <param name="windows">
    /// Whether the process runs on Windows.
    /// </param>
    /// <param name="seeded">
    /// Whether the normal source42 selector accepted an input.
    /// </param>
    /// <param name="acquisition">
    /// Whether the separate fixture-acquisition probe is selected.
    /// </param>
    /// <param name="mode">
    /// The existing crash scenario selection.
    /// </param>
    internal static void Validate(bool enabled, JsonObject parent, bool windows, bool seeded,
        bool acquisition, JointInterfaceCrashMode mode)
    {
        if (!enabled) return;
        JointProducerReadForwarderControl.Validate(false, parent, windows, seeded, acquisition, mode);
        if (JointInterfaceCrashObservation.Read(parent, windows, seeded, acquisition, mode) != JointInterfaceObservationMode.Off ||
            !JointAdmissionDirectoryControl.Read(parent, windows, seeded, acquisition, mode) ||
            !JointControlGameStateDataControl.Read(parent, windows, seeded, acquisition, mode) ||
            JointProducerReadForwarderControl.Read(parent, windows, seeded, acquisition, mode))
            throw new InvalidDataException("Passive profiling requires ordinary cut0 with caches enabled and legacy/read forwarders disabled.");
    }

    /// <summary>
    /// Reads strictly typed diagnostic metadata before a child accesses the fixture.
    /// </summary>
    /// <param name="parent">
    /// Parent metadata; a missing PassiveRecoveryTimings preserves disabled profiling.
    /// </param>
    /// <param name="windows">
    /// Whether the child runs on Windows.
    /// </param>
    /// <param name="seeded">
    /// Whether the ordinary seed selector accepted an input.
    /// </param>
    /// <param name="acquisition">
    /// Whether the separate acquisition selection is active.
    /// </param>
    /// <param name="mode">
    /// The validated existing scenario.
    /// </param>
    /// <returns>
    /// The validated passive profiling selection.
    /// </returns>
    internal static bool Read(JsonObject parent, bool windows, bool seeded, bool acquisition, JointInterfaceCrashMode mode)
    {
        var enabled = false;
        if (parent.TryGetPropertyValue("PassiveRecoveryTimings", out var node) &&
            (node is not JsonValue value || !value.TryGetValue<bool>(out enabled)))
            throw new InvalidDataException("PassiveRecoveryTimings must be a Boolean.");
        Validate(enabled, parent, windows, seeded, acquisition, mode);
        return enabled;
    }
}
