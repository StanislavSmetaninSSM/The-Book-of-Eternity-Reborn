using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointInterfaceCrashProcessSupportTests
{
    /// <summary>
    /// Accepts only closed lowercase diagnostic selections.
    /// </summary>
    /// <param name="setting">
    /// Missing or exact on/off setting.
    /// </param>
    /// <param name="expected">
    /// Expected window selection.
    /// </param>
    [Theory]
    [InlineData(null, false)]
    [InlineData("off", false)]
    [InlineData("on", true)]
    public void PassiveTimingsParseClosedSetting(string? setting, bool expected) =>
        Assert.Equal(expected, JointPassiveTimingControl.Parse(setting));

    /// <summary>
    /// Refuses ambiguous environment spellings.
    /// </summary>
    /// <param name="setting">
    /// The invalid provided setting.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ON")]
    [InlineData("true")]
    public void PassiveTimingsRefuseBadSetting(string setting) =>
        Assert.Throws<InvalidDataException>(() => JointPassiveTimingControl.Parse(setting));

    /// <summary>
    /// Preserves missing metadata and accepts the exclusively selected diagnostic route.
    /// </summary>
    [Fact]
    public void PassiveTimingsReadTypedEligibleMetadata()
    {
        var parent = PassiveTimingParent();
        var mode = new JointInterfaceCrashMode(0, false, null);
        Assert.False(JointPassiveTimingControl.Read(parent, true, true, false, mode));
        parent["PassiveRecoveryTimings"] = true;
        Assert.True(JointPassiveTimingControl.Read(parent, true, true, false, mode));
        parent["PassiveRecoveryTimings"] = false;
        Assert.False(JointPassiveTimingControl.Read(parent, true, true, false, mode));
    }

    /// <summary>
    /// Requires a Boolean rather than coercing other JSON values.
    /// </summary>
    /// <param name="json">
    /// A malformed diagnostic flag value.
    /// </param>
    [Theory]
    [InlineData("null")]
    [InlineData("\"true\"")]
    [InlineData("1")]
    [InlineData("{}")]
    public void PassiveTimingsRefuseBadMetadata(string json)
    {
        var parent = PassiveTimingParent(); parent["PassiveRecoveryTimings"] = JsonNode.Parse(json);
        Assert.Throws<InvalidDataException>(() => JointPassiveTimingControl.Read(parent, true, true, false,
            new JointInterfaceCrashMode(0, false, null)));
    }

    /// <summary>
    /// Refuses changed cache, observer, platform and scenario assumptions before fixture access.
    /// </summary>
    /// <param name="change">
    /// The dimension changed from the ordinary profile route.
    /// </param>
    [Theory]
    [InlineData("full")]
    [InlineData("directory-off")]
    [InlineData("data-off")]
    [InlineData("forwarded")]
    [InlineData("linux")]
    [InlineData("unseeded")]
    [InlineData("not-extended")]
    [InlineData("acquisition")]
    [InlineData("cut1")]
    [InlineData("diagnostic")]
    [InlineData("fault")]
    public void PassiveTimingsRefuseOtherRoutes(string change)
    {
        var parent = PassiveTimingParent(); parent["PassiveRecoveryTimings"] = true;
        if (change == "full") parent["ObservationMode"] = "full";
        if (change == "directory-off") parent["AdmissionDirectoryReuse"] = false;
        if (change == "data-off") parent["ControlGameStateDataReuse"] = false;
        if (change == "forwarded") parent["ProducerReadForwarders"] = true;
        if (change == "not-extended") parent["ExtendedWindowsCrash"] = false;
        Assert.Throws<InvalidDataException>(() => JointPassiveTimingControl.Read(parent, change != "linux",
            change != "unseeded", change == "acquisition", new JointInterfaceCrashMode(change == "cut1" ? 1 : 0,
                change == "diagnostic", change == "fault" ? "drift" : null)));
    }

    /// <summary>
    /// Creates only parent protocol metadata, without a world or source artifact.
    /// </summary>
    /// <returns>
    /// Independent metadata selecting the ordinary cache-enabled profiling route.
    /// </returns>
    private static JsonObject PassiveTimingParent() => new()
    {
        ["ObservationMode"] = "off", ["ExtendedWindowsCrash"] = true,
        ["AdmissionDirectoryReuse"] = true, ["ControlGameStateDataReuse"] = true,
        ["ProducerReadForwarders"] = false
    };
}
