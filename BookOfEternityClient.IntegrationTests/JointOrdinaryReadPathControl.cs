using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Selects the test-only original source DATA control without changing production configuration.
/// </summary>
internal static class JointOrdinaryReadPathControl
{
    /// <summary>
    /// Parses a closed explicit cache comparison setting before fixture access.
    /// </summary>
    /// <param name="setting">
    /// Null or exactly on enables the production default; exactly off selects fresh classification.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for production reuse; <see langword="false"/> for the fresh comparison route.
    /// </returns>
    internal static bool Parse(string? setting) => setting switch
    {
        null or "on" => true,
        "off" => false,
        _ => throw new InvalidDataException("BOE_TEST_JOINT_READ_PATH_CACHE must be exactly on or off.")
    };

    /// <summary>
    /// Restricts the uncached control to the ordinary seeded Windows cut-zero comparison.
    /// </summary>
    /// <param name="enabled">
    /// Whether normal production reuse is enabled.
    /// </param>
    /// <param name="parent">
    /// Already parsed parent metadata with explicit platform-budget and detailed-observation selection.
    /// </param>
    /// <param name="windows">
    /// Whether the actual parent or child runs on Windows.
    /// </param>
    /// <param name="seeded">
    /// Whether a source42 selection exists; its ordinary fingerprint validation still follows.
    /// </param>
    /// <param name="acquisition">
    /// Whether the distinct acquisition route is selected; it cannot use the control.
    /// </param>
    /// <param name="mode">
    /// Existing cut, fault and diagnostic selection.
    /// </param>
    internal static void Validate(bool enabled, JsonObject parent, bool windows, bool seeded,
        bool acquisition, JointInterfaceCrashMode mode)
    {
        var observation = JointInterfaceCrashObservation.Read(parent, windows, seeded, acquisition, mode);
        if (!enabled && (observation != JointInterfaceObservationMode.Off || !windows || !seeded ||
            acquisition || mode.Cut != 0 || mode.Diagnostic || mode.Fault != null))
            throw new InvalidDataException("The fresh ordinary-path comparison requires ordinary seeded Windows cut0 with detailed timing off.");
    }

    /// <summary>
    /// Reads strictly typed parent-authored comparison metadata before a child opens the fixture.
    /// </summary>
    /// <param name="parent">
    /// Parent metadata; an absent OrdinaryReadPathReuse preserves the production default.
    /// </param>
    /// <param name="windows">
    /// Whether the child runs on Windows.
    /// </param>
    /// <param name="seeded">
    /// Whether the ordinary seed selection reader accepted a selection.
    /// </param>
    /// <param name="acquisition">
    /// Whether the separate acquisition route is selected.
    /// </param>
    /// <param name="mode">
    /// Validated existing scenario mode.
    /// </param>
    /// <returns>
    /// Validated production reuse or the explicitly restricted fresh control.
    /// </returns>
    internal static bool Read(JsonObject parent, bool windows, bool seeded, bool acquisition, JointInterfaceCrashMode mode)
    {
        var enabled = true;
        if (parent.TryGetPropertyValue("OrdinaryReadPathReuse", out var node) &&
            (node is not JsonValue value || !value.TryGetValue<bool>(out enabled)))
            throw new InvalidDataException("OrdinaryReadPathReuse must be a Boolean.");
        Validate(enabled, parent, windows, seeded, acquisition, mode);
        return enabled;
    }
}
