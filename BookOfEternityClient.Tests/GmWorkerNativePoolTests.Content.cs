using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GmWorkerNativePoolTests
{
    [Fact]
    public async Task ActualPool_ImportsExactDetachedContentWithoutApplyingCanonicalChange()
    {
        var result = await RunScenario("pool-content-valid");
        AssertSuccessfulPublication(result);
        Assert.True(result.GetProperty("contentImportedExactly").GetBoolean());
        Assert.True(result.GetProperty("inboxMatches").GetBoolean());
    }

    [Theory]
    [InlineData("bad-hash", "do not match afterSha256")]
    [InlineData("missing", "is missing from detached execution output")]
    public async Task ActualPool_RejectsMissingOrChangedDetachedContent(string mode, string expectedError)
    {
        var result = await RunScenario("pool-content-" + mode);
        AssertRejectedAndCleaned(result);
        Assert.Equal(1, result.GetProperty("workerStarts").GetInt32());
        Assert.Equal(0, result.GetProperty("result").GetProperty("ExitCode").GetInt32());
        Assert.Equal(0, result.GetProperty("publicationCalls").GetInt32());
        Assert.Contains(expectedError, result.GetProperty("failure").GetString());
        Assert.True(result.GetProperty("failureCopyRejected").GetBoolean());
    }
}
