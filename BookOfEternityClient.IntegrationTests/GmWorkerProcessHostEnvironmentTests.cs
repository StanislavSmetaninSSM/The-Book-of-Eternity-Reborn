using System.Diagnostics;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

// These are pure tests, placed alongside the existing host codec tests.
public sealed class GmWorkerProcessHostEnvironmentTests
{
    private const string Nonce = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Theory]
    [InlineData("HTTP_PROXY", "http_proxy")]
    [InlineData("BOE_SYNTHETIC_NAME", "boe_synthetic_name")]
    public void Capture_RoundTripPreservesPlatformCaseAliases(string upperName, string lowerName)
    {
        var source = SyntheticStartInfo();
        source.Environment[upperName] = "SYNTHETIC_UPPER_VALUE";
        source.Environment[lowerName] = "synthetic_lower_value";
        var restored = GmWorkerProcessHost.CreateWorkerStartInfo(
            RoundTrip(GmWorkerProcessHostPayload.Capture(source)));
        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 2, restored.Environment.Count);
        Assert.Equal("synthetic_lower_value", restored.Environment[lowerName]);
        Assert.Equal(OperatingSystem.IsWindows() ? "synthetic_lower_value" : "SYNTHETIC_UPPER_VALUE",
            restored.Environment[upperName]);
        Assert.Contains(upperName, restored.Environment.Keys, StringComparer.Ordinal);
        if (!OperatingSystem.IsWindows())
            Assert.Contains(lowerName, restored.Environment.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void Capture_LookupRetainsPlatformNameSemantics()
    {
        var source = SyntheticStartInfo();
        source.Environment["BOE_CASE"] = "synthetic";
        var payload = GmWorkerProcessHostPayload.Capture(source);
        Assert.Equal(OperatingSystem.IsWindows(), payload.Environment.ContainsKey("boe_case"));
        Assert.Contains("BOE_CASE", payload.Environment.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void Capture_OwnsIndependentEnvironmentAndArgumentSnapshot()
    {
        var source = SyntheticStartInfo();
        source.Environment["BOE_KEEP"] = "before";
        source.Environment["BOE_REMOVE"] = "retained";
        source.ArgumentList.Add("original argument");
        var payload = GmWorkerProcessHostPayload.Capture(source);
        source.Environment["BOE_KEEP"] = "after";
        source.Environment.Remove("BOE_REMOVE");
        source.Environment["BOE_LATER"] = "not captured";
        source.ArgumentList[0] = "changed argument";
        payload.Environment["BOE_PAYLOAD_ONLY"] = "not in source";

        var restored = GmWorkerProcessHost.CreateWorkerStartInfo(RoundTrip(payload));
        Assert.Equal("before", restored.Environment["BOE_KEEP"]);
        Assert.Equal("retained", restored.Environment["BOE_REMOVE"]);
        Assert.False(restored.Environment.ContainsKey("BOE_LATER"));
        Assert.False(source.Environment.ContainsKey("BOE_PAYLOAD_ONLY"));
        Assert.Equal(new[] { "original argument" }, restored.ArgumentList);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("synthetic-\u0441\u043d\u0435\u0433-\u2603=\"quoted\"\nsecond line")]
    public void RoundTrip_PreservesNullableEmptyAndUnicodeValues(string? value)
    {
        var source = SyntheticStartInfo();
        source.Environment["BOE_SYNTHETIC_VALUE"] = value;
        var restored = GmWorkerProcessHost.CreateWorkerStartInfo(
            RoundTrip(GmWorkerProcessHostPayload.Capture(source)));
        Assert.Single(restored.Environment);
        Assert.True(restored.Environment.ContainsKey("BOE_SYNTHETIC_VALUE"));
        Assert.Equal(value, restored.Environment["BOE_SYNTHETIC_VALUE"]);
    }

    [Fact]
    public void Reconstruct_EmptyEnvironmentReplacesAmbientAndPreservesLaunchFields()
    {
        var source = SyntheticStartInfo();
        source.ArgumentList.Add("argument with spaces");
        source.ArgumentList.Add("");
        source.ArgumentList.Add("\u0442\u0435\u043a\u0441\u0442");
        var restored = GmWorkerProcessHost.CreateWorkerStartInfo(
            RoundTrip(GmWorkerProcessHostPayload.Capture(source)));
        Assert.Empty(restored.Environment);
        Assert.Equal(source.FileName, restored.FileName);
        Assert.Equal(source.WorkingDirectory, restored.WorkingDirectory);
        Assert.Equal(source.ArgumentList, restored.ArgumentList);
        Assert.False(restored.UseShellExecute);
        Assert.True(restored.CreateNoWindow);
        Assert.Equal(ProcessWindowStyle.Hidden, restored.WindowStyle);
        Assert.True(restored.RedirectStandardInput);
        Assert.True(restored.RedirectStandardOutput);
        Assert.True(restored.RedirectStandardError);
    }

    [Fact]
    public void Reconstruct_WireAliasesUseExistingPlatformAssignmentSemantics()
    {
        var payload = new GmWorkerProcessHostPayload("synthetic-executable", [], "", new(StringComparer.Ordinal)
        {
            ["BOE_CASE"] = "upper first",
            ["boe_case"] = "lower last"
        });
        var restored = GmWorkerProcessHost.CreateWorkerStartInfo(RoundTrip(payload));
        Assert.Equal(OperatingSystem.IsWindows() ? 1 : 2, restored.Environment.Count);
        Assert.Equal("lower last", restored.Environment["boe_case"]);
        Assert.Equal(OperatingSystem.IsWindows() ? "lower last" : "upper first", restored.Environment["BOE_CASE"]);
    }

    [Fact]
    public void Wire_CaseDistinctEnvironmentKeysRemainDistinct()
    {
        var payload = new GmWorkerProcessHostPayload("synthetic-executable", [], "", new(StringComparer.Ordinal)
        {
            ["HTTP_PROXY"] = "SYNTHETIC_UPPER_VALUE",
            ["http_proxy"] = "synthetic_lower_value"
        });
        var parsed = RoundTrip(payload);
        Assert.Equal(2, parsed.Environment.Count);
        Assert.Equal("SYNTHETIC_UPPER_VALUE", parsed.Environment["HTTP_PROXY"]);
        Assert.Equal("synthetic_lower_value", parsed.Environment["http_proxy"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Wire_ExactDuplicateEnvironmentKeysAreRejectedWithoutPayloadDiagnostics(bool escaped)
    {
        var secondKey = escaped ? "SYNTHETIC_\\u004bEY" : "SYNTHETIC_KEY";
        var json = $$$$"""{"schemaVersion":1,"launchNonce":"{{{{Nonce}}}}","kind":"launch","payload":{"fileName":"synthetic","arguments":[],"workingDirectory":"","environment":{"SYNTHETIC_KEY":"SYNTHETIC_VALUE","{{{{secondKey}}}}":"second"}}}""";
        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(json, Nonce, GmWorkerProcessHostControlKind.Launch));
        Assert.Contains("duplicate property", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETIC_KEY", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETIC_VALUE", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Wire_EnvironmentCaseAliasesDoNotRelaxEnvelopePropertyCasing()
    {
        var json = $$$$"""{"schemaVersion":1,"launchNonce":"{{{{Nonce}}}}","kind":"launch","payload":{"FileName":"synthetic","arguments":[],"workingDirectory":"","environment":{"HTTP_PROXY":"upper","http_proxy":"lower"}}}""";
        Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(json, Nonce, GmWorkerProcessHostControlKind.Launch));
    }

    private static GmWorkerProcessHostPayload RoundTrip(GmWorkerProcessHostPayload payload)
    {
        var json = GmWorkerProcessHostProtocol.SerializeControl(new(1, Nonce,
            GmWorkerProcessHostControlKind.Launch, payload));
        return GmWorkerProcessHostProtocol.ParseControl(json, Nonce,
            GmWorkerProcessHostControlKind.Launch).Payload!;
    }

    private static ProcessStartInfo SyntheticStartInfo()
    {
        var source = new ProcessStartInfo
        {
            FileName = "synthetic-executable",
            WorkingDirectory = "synthetic-directory",
            UseShellExecute = false
        };
        // This is a per-test map, not a mutation of the test process environment.
        source.Environment.Clear();
        return source;
    }
}
