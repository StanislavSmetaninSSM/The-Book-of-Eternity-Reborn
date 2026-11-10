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
    public void ReadAdmission_ParsesClosedSetting(string? setting, bool expected) =>
        Assert.Equal(expected, JointReadAdmissionControl.Parse(setting));

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
    public void ReadAdmission_RejectsUnknownSetting(string setting) =>
        Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Parse(setting));

    /// <summary>
    /// Preserves default reuse and confines the fresh control to an ordinary seeded Windows cut-zero comparison.
    /// </summary>
    [Fact]
    public void ReadAdmission_ValidatesParentAndChildBeforeFixtureAccess()
    {
        var parent = new JsonObject { ["ObservationMode"] = "off", ["ExtendedWindowsCrash"] = true,
            ["OrdinaryReadAdmissionReuse"] = false };
        var mode = new JointInterfaceCrashMode(0, false, null);
        Assert.False(JointReadAdmissionControl.Read(parent, true, true, false, mode));
        Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Read(parent, false, true, false, mode));
        Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Read(parent, true, false, false, mode));
        Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Read(parent, true, true, true, mode));
        Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Read(parent, true, true, false, new(1, false, null)));
        Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Read(parent, true, true, false, new(0, true, null)));
        Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Read(parent, true, true, false, new(0, false, "tamper")));
        parent["ObservationMode"] = "full";
        Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Read(parent, true, true, false, mode));
        parent["OrdinaryReadAdmissionReuse"] = true;
        Assert.True(JointReadAdmissionControl.Read(parent, true, true, false, mode));
        parent.Remove("OrdinaryReadAdmissionReuse");
        Assert.True(JointReadAdmissionControl.Read(parent, true, true, false, mode));
        foreach (var malformed in new JsonNode?[] { null, JsonValue.Create("off"), JsonValue.Create(0), new JsonObject() })
        {
            parent["OrdinaryReadAdmissionReuse"] = malformed;
            Assert.Throws<InvalidDataException>(() => JointReadAdmissionControl.Read(parent, true, true, false, mode));
        }
    }
}
