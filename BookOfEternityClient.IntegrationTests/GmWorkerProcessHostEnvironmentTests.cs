using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

// These are pure tests, placed alongside the existing host codec tests.
public sealed class GmWorkerProcessHostEnvironmentTests
{
    private const string Nonce = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

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
        var json = $$"""{"schemaVersion":1,"launchNonce":"{{Nonce}}","kind":"launch","payload":{"fileName":"synthetic","arguments":[],"workingDirectory":"","environment":{"SYNTHETIC_KEY":"SYNTHETIC_VALUE","{{secondKey}}":"second"}}}""";
        var error = Assert.Throws<InvalidDataException>(() =>
            GmWorkerProcessHostProtocol.ParseControl(json, Nonce, GmWorkerProcessHostControlKind.Launch));
        Assert.Contains("duplicate property", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETIC_KEY", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SYNTHETIC_VALUE", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Wire_EnvironmentCaseAliasesDoNotRelaxEnvelopePropertyCasing()
    {
        var json = $$"""{"schemaVersion":1,"launchNonce":"{{Nonce}}","kind":"launch","payload":{"FileName":"synthetic","arguments":[],"workingDirectory":"","environment":{"HTTP_PROXY":"upper","http_proxy":"lower"}}}""";
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
}
