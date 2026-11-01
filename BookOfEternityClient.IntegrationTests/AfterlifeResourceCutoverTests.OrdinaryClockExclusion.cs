using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData("[]")]
    [InlineData("{invalid-stale-mortal-clock")]
    public async Task OrdinaryAfterlifeAdmissionDoesNotParseOrPublishStaleMortalClock(string clock)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactJsonAsync(EffectAcceptedTurnInputComposer.WorldTimePath, clock);
        var before = await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath);
        var issues = await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync();
        AssertNoConflictFrameErrors(issues);
        Assert.NotNull(await PeekPlanAsync(context));
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(EffectAcceptedTurnInputComposer.WorldTimePath));
    }
}
