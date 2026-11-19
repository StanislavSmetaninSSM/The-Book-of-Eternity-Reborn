using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class MortalPreparationTests
{
    [Fact]
    public Task PrepareOnlyDataContracts() => MortalConsoleRouteTests.RunAsync("test-mortal-preparation.py", 9);
}
