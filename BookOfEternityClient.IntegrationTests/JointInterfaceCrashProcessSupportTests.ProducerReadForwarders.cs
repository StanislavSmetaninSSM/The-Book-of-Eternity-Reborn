using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointInterfaceCrashProcessSupportTests
{
    /// <summary>
    /// Accepts only explicit lowercase hook controls or an absent route default.
    /// </summary>
    /// <param name="setting">
    /// The exact environment control value.
    /// </param>
    /// <param name="expected">
    /// The nullable forwarding selection.
    /// </param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("on", true)]
    [InlineData("off", false)]
    public void ProducerForwardersParseClosedSetting(string? setting, bool? expected) =>
        Assert.Equal(expected, JointProducerReadForwarderControl.Parse(setting));

    /// <summary>
    /// Refuses ambiguous settings before fixture construction.
    /// </summary>
    /// <param name="setting">
    /// An invalid supplied value.
    /// </param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("OFF")]
    [InlineData("false")]
    public void ProducerForwardersRejectInvalidSetting(string setting) =>
        Assert.Throws<InvalidDataException>(() => JointProducerReadForwarderControl.Parse(setting));

    /// <summary>
    /// Preserves old metadata defaults and permits the explicit ordinary selection.
    /// </summary>
    /// <param name="timings">
    /// The existing detailed timing mode, independent of read callback selection.
    /// </param>
    [Theory]
    [InlineData("full")]
    [InlineData("off")]
    public void ProducerForwardersReadTypedOrdinaryMetadata(string timings)
    {
        var parent = new JsonObject { ["ExtendedWindowsCrash"] = true, ["ObservationMode"] = timings };
        var mode = new JointInterfaceCrashMode(0, false, null);
        Assert.True(JointProducerReadForwarderControl.Read(parent, true, true, false, mode));
        parent["ProducerReadForwarders"] = false;
        Assert.False(JointProducerReadForwarderControl.Read(parent, true, true, false, mode));
        parent["ProducerReadForwarders"] = true;
        Assert.True(JointProducerReadForwarderControl.Read(parent, true, true, false, mode));
    }

    /// <summary>
    /// Requires Boolean metadata rather than coercing <see langword="null"/>, strings or numbers.
    /// </summary>
    /// <param name="json">
    /// A malformed parent-authored property value.
    /// </param>
    [Theory]
    [InlineData("null")]
    [InlineData("\"false\"")]
    [InlineData("0")]
    [InlineData("{}")]
    public void ProducerForwardersRefuseUntypedMetadata(string json)
    {
        var parent = new JsonObject { ["ExtendedWindowsCrash"] = true, ["ProducerReadForwarders"] = JsonNode.Parse(json) };
        Assert.Throws<InvalidDataException>(() => JointProducerReadForwarderControl.Read(parent, true, true, false,
            new JointInterfaceCrashMode(0, false, null)));
    }

    /// <summary>
    /// Keeps observation hooks for every route outside the ordinary producer selection.
    /// </summary>
    /// <param name="route">
    /// The unsupported mode or metadata dimension changed from the ordinary selection.
    /// </param>
    [Theory]
    [InlineData("linux")]
    [InlineData("unseeded")]
    [InlineData("acquisition")]
    [InlineData("not-extended")]
    [InlineData("missing-extended")]
    [InlineData("diagnostic")]
    [InlineData("fault")]
    [InlineData("cut1")]
    [InlineData("cut2")]
    [InlineData("cut3")]
    [InlineData("cut4")]
    public void ProducerForwardersRefuseDisabledHooksOnOtherRoutes(string route)
    {
        var parent = new JsonObject { ["ExtendedWindowsCrash"] = route != "not-extended" };
        if (route == "missing-extended") parent.Remove("ExtendedWindowsCrash");
        var cut = route.StartsWith("cut", StringComparison.Ordinal) ? int.Parse(route[3..]) : 0;
        var mode = new JointInterfaceCrashMode(cut, route == "diagnostic", route == "fault" ? "drift" : null);
        var windows = route != "linux";
        var seeded = route != "unseeded";
        var acquisition = route == "acquisition";
        JointProducerReadForwarderControl.Validate(true, parent, windows, seeded, acquisition, mode);
        Assert.Throws<InvalidDataException>(() =>
            JointProducerReadForwarderControl.Validate(false, parent, windows, seeded, acquisition, mode));
    }
}
