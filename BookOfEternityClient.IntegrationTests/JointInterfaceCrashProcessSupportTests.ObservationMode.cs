using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointInterfaceCrashProcessSupportTests
{
    /// <summary>
    /// Keeps full as the default and admits off only by its exact explicit spelling.
    /// </summary>
    /// <param name="setting">
    /// The supplied test setting; null means no override.
    /// </param>
    /// <param name="expected">
    /// The literal underlying enum value required for this setting.
    /// </param>
    [Theory]
    [InlineData(null, (int)JointInterfaceObservationMode.Full)]
    [InlineData("full", (int)JointInterfaceObservationMode.Full)]
    [InlineData("off", (int)JointInterfaceObservationMode.Off)]
    public void ObservationMode_ParsesOnlyExplicitSelection(string? setting, int expected)
    {
        Assert.Equal((JointInterfaceObservationMode)expected, JointInterfaceCrashObservation.Parse(setting));
    }

    /// <summary>
    /// Refuses empty or noncanonical settings instead of silently disabling or enabling observation.
    /// </summary>
    /// <param name="setting">
    /// An explicit invalid spelling that must fail before fixture creation.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("FULL")]
    [InlineData("OFF")]
    [InlineData("unknown")]
    public void ObservationMode_RejectsInvalidSetting(string setting)
    {
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashObservation.Parse(setting));
    }

    /// <summary>
    /// Allows timing suppression for the same ordinary seeded Windows crash without changing its existing mode.
    /// </summary>
    [Fact]
    public void ObservationMode_OffRequiresOrdinaryExtendedSeededWindows()
    {
        var original = new JointInterfaceCrashMode(0, false, null);
        JointInterfaceCrashObservation.Validate(JointInterfaceObservationMode.Off,
            true, true, true, false, original);
        Assert.Equal(JointInterfaceCrashBackend.DirectWindows,
            JointInterfaceCrashProcessSupport.SelectBackend(true, false, original));
        Assert.Equal(TimeSpan.FromSeconds(600), JointInterfaceCrashProcessSupport.GetSharedWindowsCrashTimeout(
            true, "engine-joint-interface-resume", false, true, TimeSpan.Zero));
    }

    /// <summary>
    /// Refuses suppression when any ordinary seeded Windows prerequisite is missing.
    /// </summary>
    /// <param name="invalidContext">
    /// The single prerequisite removed from an otherwise eligible selection.
    /// </param>
    [Theory]
    [InlineData("platform")]
    [InlineData("extended")]
    [InlineData("seed")]
    [InlineData("acquisition")]
    [InlineData("diagnostic")]
    [InlineData("fault")]
    [InlineData("cut")]
    public void ObservationMode_OffRejectsOtherScenarioSelections(string invalidContext)
    {
        var original = new JointInterfaceCrashMode(invalidContext == "cut" ? 5 : 0,
            invalidContext == "diagnostic", invalidContext == "fault" ? "Version" : null);
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashObservation.Validate(JointInterfaceObservationMode.Off,
            invalidContext != "platform", invalidContext != "extended", invalidContext != "seed",
            invalidContext == "acquisition", original));
    }

    /// <summary>
    /// Preserves full observation for existing unseeded, Linux diagnostic and acquisition consumers.
    /// </summary>
    [Fact]
    public void ObservationMode_FullPreservesExistingConsumersAndRefusesUnknownEnum()
    {
        JointInterfaceCrashObservation.Validate(JointInterfaceObservationMode.Full,
            false, false, false, false, new(1, true, null));
        JointInterfaceCrashObservation.Validate(JointInterfaceObservationMode.Full,
            true, true, true, true, new(0, false, null));
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashObservation.Validate((JointInterfaceObservationMode)42,
            true, true, true, false, new(0, false, null)));
    }

    /// <summary>
    /// Reads the same explicit parent selection in each child while retaining absent-field full behavior.
    /// </summary>
    [Fact]
    public void ObservationMode_ChildReadsFullDefaultAndExplicitOff()
    {
        var original = new JointInterfaceCrashMode(0, false, null);
        Assert.Equal(JointInterfaceObservationMode.Full,
            JointInterfaceCrashObservation.Read(new(), true, false, false, original));
        Assert.Equal(JointInterfaceObservationMode.Full, JointInterfaceCrashObservation.Read(
            new() { ["ObservationMode"] = "full" }, true, false, false, original));
        Assert.Equal(JointInterfaceObservationMode.Off, JointInterfaceCrashObservation.Read(
            new() { ["ObservationMode"] = "off", ["ExtendedWindowsCrash"] = true }, true, true, false, original));
    }

    /// <summary>
    /// Refuses malformed parent fields and off selection without its typed existing budget flag.
    /// </summary>
    /// <param name="invalidField">
    /// The independently malformed or missing parent field to exercise.
    /// </param>
    [Theory]
    [InlineData("null-mode")]
    [InlineData("boolean-mode")]
    [InlineData("unknown-mode")]
    [InlineData("missing-extended")]
    [InlineData("string-extended")]
    [InlineData("false-extended")]
    public void ObservationMode_ChildRefusesMalformedOrIneligibleMetadata(string invalidField)
    {
        var parent = new JsonObject { ["ObservationMode"] = "off", ["ExtendedWindowsCrash"] = true };
        switch (invalidField)
        {
            case "null-mode": parent["ObservationMode"] = null; break;
            case "boolean-mode": parent["ObservationMode"] = false; break;
            case "unknown-mode": parent["ObservationMode"] = "other"; break;
            case "missing-extended": parent.Remove("ExtendedWindowsCrash"); break;
            case "string-extended": parent["ExtendedWindowsCrash"] = "true"; break;
            case "false-extended": parent["ExtendedWindowsCrash"] = false; break;
        }
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashObservation.Read(parent,
            true, true, false, new(0, false, null)));
    }
}
