using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    // OLD-compilable semantic RED: only the existing B1 API is used.
    [Fact]
    public async Task SourceContinuation_OldOwnerLosesAuthorityWhenBeginReplacesIt()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var first = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var old = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(first.Session);
        var oldSource = Assert.Single(old.Sources);

        var second = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        AssertNoConflictFrameErrors(second.Issues);
        var current = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(second.Session);
        var currentSource = Assert.Single(current.Sources);
        Assert.NotSame(old, current);
        Assert.Equal(oldSource, currentSource);
        Assert.False(old.Owns(oldSource)); // Fails semantically on unchanged B1.
        Assert.False(current.Owns(oldSource));
        Assert.True(current.Owns(currentSource));
    }
}
