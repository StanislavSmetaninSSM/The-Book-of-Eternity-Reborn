using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointInterfaceCrashProcessSupportTests
{
    /// <summary>
    /// Accepts only the closed test-only reuse setting before any source42 access.
    /// </summary>
    /// <param name="setting">
    /// Absent, on or off setting.
    /// </param>
    /// <param name="expected">
    /// Expected production reuse selection.
    /// </param>
    [Theory]
    [InlineData(null, true)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void ControlGameStateData_ParsesClosedSetting(string? setting, bool expected) =>
        Assert.Equal(expected, JointControlGameStateDataControl.Parse(setting));

    /// <summary>
    /// Refuses ambiguous comparison configuration before loading any fixture.
    /// </summary>
    /// <param name="setting">
    /// Invalid supplied spelling.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData("OFF")]
    [InlineData("false")]
    [InlineData(" off")]
    public void ControlGameStateData_RejectsUnknownSetting(string setting) =>
        Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Parse(setting));

    /// <summary>
    /// Preserves default reuse and confines the fresh control to an ordinary seeded Windows cut-zero comparison.
    /// </summary>
    [Fact]
    public void ControlGameStateData_ValidatesParentAndChildBeforeFixtureAccess()
    {
        var parent = new JsonObject { ["ObservationMode"] = "off", ["ExtendedWindowsCrash"] = true,
            ["ControlGameStateDataReuse"] = false };
        var mode = new JointInterfaceCrashMode(0, false, null);
        Assert.False(JointControlGameStateDataControl.Read(parent, true, true, false, mode));
        Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Read(parent, false, true, false, mode));
        Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Read(parent, true, false, false, mode));
        Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Read(parent, true, true, true, mode));
        Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Read(parent, true, true, false, new(1, false, null)));
        Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Read(parent, true, true, false, new(0, true, null)));
        Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Read(parent, true, true, false, new(0, false, "tamper")));
        parent["ObservationMode"] = "full";
        Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Read(parent, true, true, false, mode));
        parent["ControlGameStateDataReuse"] = true;
        Assert.True(JointControlGameStateDataControl.Read(parent, true, true, false, mode));
        parent.Remove("ControlGameStateDataReuse");
        Assert.True(JointControlGameStateDataControl.Read(parent, true, true, false, mode));
        foreach (var malformed in new JsonNode?[] { null, JsonValue.Create("off"), JsonValue.Create(0), new JsonObject() })
        {
            parent["ControlGameStateDataReuse"] = malformed;
            Assert.Throws<InvalidDataException>(() => JointControlGameStateDataControl.Read(parent, true, true, false, mode));
        }
    }
}
