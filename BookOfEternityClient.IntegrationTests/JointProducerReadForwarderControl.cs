using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Selects read observation only for the ordinary seeded Windows producer.
/// </summary>
internal static class JointProducerReadForwarderControl
{
    /// <summary>
    /// Parses the closed test setting before any fixture access.
    /// </summary>
    /// <param name="setting">
    /// Exactly on or off; null leaves the caller's route default unspecified.
    /// </param>
    /// <returns>
    /// True for forwarding, false for absent hooks, or null for the route default.
    /// </returns>
    internal static bool? Parse(string? setting) => setting switch
    {
        null => null,
        "on" => true,
        "off" => false,
        _ => throw new InvalidDataException("BOE_TEST_JOINT_PRODUCER_READ_FORWARDERS must be exactly on or off.")
    };

    /// <summary>
    /// Refuses absent hooks outside the expressly supported ordinary producer route.
    /// </summary>
    /// <param name="enabled">
    /// Whether the existing late-attachable forwarding hooks remain installed.
    /// </param>
    /// <param name="parent">
    /// Parent metadata with strictly typed observation and extended-budget selection.
    /// </param>
    /// <param name="windows">
    /// Whether the actual process runs on Windows.
    /// </param>
    /// <param name="seeded">
    /// Whether the ordinary source42 reader selected an input.
    /// </param>
    /// <param name="acquisition">
    /// Whether the separate acquisition probe is selected.
    /// </param>
    /// <param name="mode">
    /// The original cut, fault and diagnostic selection.
    /// </param>
    internal static void Validate(bool enabled, JsonObject parent, bool windows, bool seeded,
        bool acquisition, JointInterfaceCrashMode mode)
    {
        _ = JointInterfaceCrashObservation.Read(parent, windows, seeded, acquisition, mode);
        if (!enabled && (!windows || !seeded || acquisition || mode.Cut != 0 || mode.Diagnostic ||
            mode.Fault != null || parent["ExtendedWindowsCrash"]?.GetValue<bool>() != true))
            throw new InvalidDataException("Absent producer read hooks require ordinary extended seeded Windows cut0.");
    }

    /// <summary>
    /// Reads strictly typed parent-authored hook selection before opening the fixture.
    /// </summary>
    /// <param name="parent">
    /// Metadata; a missing ProducerReadForwarders preserves existing forwarding hooks.
    /// </param>
    /// <param name="windows">
    /// Whether the child runs on Windows.
    /// </param>
    /// <param name="seeded">
    /// Whether the ordinary seed reader selected a source42 input.
    /// </param>
    /// <param name="acquisition">
    /// Whether the distinct acquisition probe is selected.
    /// </param>
    /// <param name="mode">
    /// The validated original scenario.
    /// </param>
    /// <returns>
    /// The validated hook selection, without weakening production reuse eligibility.
    /// </returns>
    internal static bool Read(JsonObject parent, bool windows, bool seeded, bool acquisition, JointInterfaceCrashMode mode)
    {
        var enabled = true;
        if (parent.TryGetPropertyValue("ProducerReadForwarders", out var node) &&
            (node is not JsonValue value || !value.TryGetValue<bool>(out enabled)))
            throw new InvalidDataException("ProducerReadForwarders must be a Boolean.");
        Validate(enabled, parent, windows, seeded, acquisition, mode);
        return enabled;
    }
}
