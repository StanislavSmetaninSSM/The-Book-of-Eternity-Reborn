using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class MortalPreparationTests
{
    [Fact]
    public Task PrepareOnlyOwnedProcessContracts() => MortalConsoleRouteTests.RunAsync("test-mortal-preparation-process.py", 4);
    [Fact]
    public Task PrepareOnlyDataContracts() => MortalConsoleRouteTests.RunAsync("test-mortal-preparation.py", 19);
}
