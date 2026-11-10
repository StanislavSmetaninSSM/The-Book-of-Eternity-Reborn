using System.Text.Json.Nodes;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Selects detailed timing observation independently of the original crash scenario and its scalar evidence.
/// </summary>
internal enum JointInterfaceObservationMode
{
    /// <summary>
    /// Retains every detailed timing boundary without a record or byte quota.
    /// </summary>
    Full,

    /// <summary>
    /// Omits the optional detailed timing observer while preserving scenario execution and scalar evidence.
    /// </summary>
    Off
}

/// <summary>
/// Validates test-only detailed observation selection without creating fixtures or changing game contracts.
/// </summary>
internal static class JointInterfaceCrashObservation
{
    /// <summary>
    /// Parses the explicitly supplied test setting without reading or changing process environment.
    /// </summary>
    /// <param name="setting">
    /// The exact lowercase full or off value; <see langword="null"/> retains full observation.
    /// Empty, whitespace and other spellings must be refused.
    /// </param>
    /// <returns>
    /// The selected observation mode, independent of the original scenario's diagnostic flag.
    /// </returns>
    internal static JointInterfaceObservationMode Parse(string? setting) => setting switch
    {
        null or "full" => JointInterfaceObservationMode.Full,
        "off" => JointInterfaceObservationMode.Off,
        _ => throw new InvalidDataException("BOE_TEST_JOINT_TIMINGS must be exactly full or off when provided.")
    };

    /// <summary>
    /// Checks that timing suppression is limited to the explicitly selected ordinary seeded Windows scenario.
    /// </summary>
    /// <param name="observation">
    /// Full observation or the explicit off selection; undefined enum values must be refused.
    /// </param>
    /// <param name="windows">
    /// Whether the existing scenario runs on Windows.
    /// </param>
    /// <param name="extendedWindowsCrash">
    /// Whether the unchanged shared Windows crash observation budget is selected.
    /// </param>
    /// <param name="seeded">
    /// Whether the existing source42 selection has been validated by its normal consumer.
    /// </param>
    /// <param name="fixtureAcquisitionOnly">
    /// Whether the distinct fixture acquisition selection is active; it cannot suppress detailed observation.
    /// </param>
    /// <param name="mode">
    /// The existing crash cut, diagnostic selection and integrity fault.
    /// </param>
    internal static void Validate(JointInterfaceObservationMode observation, bool windows,
        bool extendedWindowsCrash, bool seeded, bool fixtureAcquisitionOnly, JointInterfaceCrashMode mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        if (!Enum.IsDefined(observation))
            throw new InvalidDataException("The detailed observation mode is undefined.");
        if (observation == JointInterfaceObservationMode.Off &&
            (!windows || !extendedWindowsCrash || !seeded || fixtureAcquisitionOnly ||
                mode.Diagnostic || mode.Fault is not null || mode.Cut is < 0 or > 4))
            throw new InvalidDataException("Detailed timing suppression requires an ordinary extended seeded Windows crash selection.");
    }

    /// <summary>
    /// Reads parent-authored observation metadata before a child opens its fixture.
    /// </summary>
    /// <param name="parent">
    /// Parent metadata; absent ObservationMode preserves full observation, while supplied values must be strings.
    /// ExtendedWindowsCrash, when supplied, must be a Boolean and must be true for off selection.
    /// </param>
    /// <param name="windows">
    /// Whether the child runs on Windows.
    /// </param>
    /// <param name="seeded">
    /// Whether the child's normal source42 selection reader accepted an actual selection.
    /// </param>
    /// <param name="fixtureAcquisitionOnly">
    /// Whether the child's normal acquisition selection reader accepted fixture acquisition.
    /// </param>
    /// <param name="mode">
    /// The already validated original crash scenario mode.
    /// </param>
    /// <returns>
    /// A validated detailed observation mode; malformed metadata is refused before fixture creation.
    /// </returns>
    internal static JointInterfaceObservationMode Read(JsonObject parent, bool windows, bool seeded,
        bool fixtureAcquisitionOnly, JointInterfaceCrashMode mode)
    {
        ArgumentNullException.ThrowIfNull(parent);
        string? setting = null;
        if (parent.TryGetPropertyValue("ObservationMode", out var observationNode) &&
            (observationNode is not JsonValue observationValue ||
                !observationValue.TryGetValue<string>(out setting) || setting is null))
            throw new InvalidDataException("ObservationMode must be an explicit full or off string when provided.");
        var extendedWindowsCrash = false;
        if (parent.TryGetPropertyValue("ExtendedWindowsCrash", out var extendedNode) &&
            (extendedNode is not JsonValue extendedValue ||
                !extendedValue.TryGetValue<bool>(out extendedWindowsCrash)))
            throw new InvalidDataException("ExtendedWindowsCrash must be a Boolean when provided.");
        var observation = Parse(setting);
        Validate(observation, windows, extendedWindowsCrash, seeded, fixtureAcquisitionOnly, mode);
        return observation;
    }
}
