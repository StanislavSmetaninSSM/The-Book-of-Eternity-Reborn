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
    internal static JointInterfaceObservationMode Parse(string? setting) => JointInterfaceObservationMode.Full;

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
        if (observation != JointInterfaceObservationMode.Full)
            throw new InvalidDataException("Only full observation is currently available.");
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
        bool fixtureAcquisitionOnly, JointInterfaceCrashMode mode) => JointInterfaceObservationMode.Full;
}
