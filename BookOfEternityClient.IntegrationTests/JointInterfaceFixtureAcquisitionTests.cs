using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Keeps fixture generation distinct from ordinary and unsupported recovery selections.
/// </summary>
public sealed class JointInterfaceFixtureAcquisitionTests
{
    /// <summary>
    /// Preserves disabled and absent opt-ins even outside the supported probe platform.
    /// </summary>
    [Fact]
    public void OrdinaryMetadata_RemainsDisabled()
    {
        Assert.False(JointInterfaceFixtureAcquisition.ReadAndValidate(new(), false));
        Assert.False(JointInterfaceFixtureAcquisition.ReadAndValidate(new() { ["FixtureAcquisitionOnly"] = false }, false));
    }

    /// <summary>
    /// Accepts only the explicit seeded ordinary Windows cut0 fixture scope.
    /// </summary>
    [Fact]
    public void ExplicitSupportedProbe_IsEnabled() => Assert.True(JointInterfaceFixtureAcquisition.ReadAndValidate(Parent(), true));

    /// <summary>
    /// Rejects malformed controls and unsupported scope before creating fixtures or processes.
    /// </summary>
    /// <param name="fault">
    /// The unsupported platform, scenario, source or flag shape to reject.
    /// </param>
    [Theory]
    [InlineData("platform")]
    [InlineData("cut")]
    [InlineData("diagnostic")]
    [InlineData("fault")]
    [InlineData("source")]
    [InlineData("string")]
    [InlineData("null")]
    public void UnsupportedOrMalformedProbe_IsRejected(string fault)
    {
        var parent = Parent();
        switch (fault)
        {
            case "cut": parent["Cut"] = 1; break;
            case "diagnostic": parent["Diagnostic"] = true; break;
            case "fault": parent["Fault"] = "CurrentInterface"; break;
            case "source": parent.Remove("Source42Seed"); break;
            case "string": parent["FixtureAcquisitionOnly"] = "true"; break;
            case "null": parent["FixtureAcquisitionOnly"] = null; break;
        }
        Assert.Throws<InvalidDataException>(() => JointInterfaceFixtureAcquisition.ReadAndValidate(parent, fault != "platform"));
    }

    /// <summary>
    /// Verifies real tiny DATA exports and refuses changed role, identity, provenance or archive bytes.
    /// </summary>
    /// <param name="damage">
    /// The index or provenance fault; none preserves both complete exports.
    /// </param>
    [Theory]
    [InlineData("none")]
    [InlineData("role")]
    [InlineData("pid")]
    [InlineData("nonce")]
    [InlineData("bytes")]
    public void Exports_RequireExactDataAndObservedIdentity(string damage)
    {
        var owned = Path.Combine(Path.GetTempPath(), "boe-fixture-export-control-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(owned);
        using var cleanup = new CleanupOwnedFixture(owned, Console.WriteLine);
        var root = Path.Combine(owned, "source");
        var directory = Path.Combine(owned, "exports");
        Directory.CreateDirectory(Path.Combine(root, "game_session", "empty"));
        Directory.CreateDirectory(Path.Combine(root, ".boe_runtime", "session-generation"));
        File.WriteAllText(Path.Combine(root, ".boe_runtime", "session-generation", "current.json"), "{}");
        File.WriteAllText(Path.Combine(root, "game_session", "data.json"), "observed bytes");
        var parent = Parent(); parent["Nonce"] = "actual-parent-control";
        var tuple = new JsonObject { ["SessionId"] = "s", ["RequestId"] = "r", ["SnapshotToken"] = "t", ["TurnNumber"] = 43 };
        var exports = new JsonArray(
            JointInterfaceFixtureAcquisition.Capture(root, directory, "PreparedOriginal", parent),
            JointInterfaceFixtureAcquisition.Capture(root, directory, "AppliedBeforeStory", parent, tuple));
        var expectedPid = Environment.ProcessId;
        if (damage == "role") exports[1]!["Role"] = "PreparedOriginal";
        if (damage == "pid") expectedPid++;
        if (damage == "nonce") parent["Nonce"] = "different-parent";
        if (damage == "bytes") File.AppendAllText(exports[1]!["Archive"]!.GetValue<string>(), "changed");
        if (damage == "none") JointInterfaceFixtureAcquisition.Verify(exports, directory, root, expectedPid, parent);
        else Assert.Throws<InvalidDataException>(() => JointInterfaceFixtureAcquisition.Verify(exports, directory, root, expectedPid, parent));
        Assert.Equal("observed bytes", File.ReadAllText(Path.Combine(root, "game_session", "data.json")));
    }

    /// <summary>
    /// Creates detached selection metadata; the real source binding is validated separately before runtime.
    /// </summary>
    /// <returns>
    /// New private metadata for one control case, carrying no game authority.
    /// </returns>
    private static JsonObject Parent() => new()
    {
        ["FixtureAcquisitionOnly"] = true, ["Cut"] = 0, ["Diagnostic"] = false, ["Fault"] = null,
        ["Source42Seed"] = new JsonObject { ["ArtifactPath"] = "detached-selection", ["ManifestSha256"] = new string('A', 64) }
    };
}
