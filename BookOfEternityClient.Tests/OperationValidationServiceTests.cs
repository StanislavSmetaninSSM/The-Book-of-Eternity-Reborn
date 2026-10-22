using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks reusable operation computations without preparing files or game objects.
/// </summary>
/// <param name="output">
/// Receiver for actual computation counts and operation lifetime observations.
/// </param>
public sealed class OperationValidationServiceTests(ITestOutputHelper output)
{
    /// <summary>
    /// Reuses an unchanged value within one operation and executes again in another operation.
    /// </summary>
    [Fact]
    public async Task RepeatedValueComputationsExecuteOncePerOperation()
    {
        var service = new OperationValidationService();
        var rule = new OperationComputation<int>("fixture.value", 1, value => value);
        var input = new object();
        var executions = 0;
        Task<int> Compute(CancellationToken _)
        {
            executions++;
            return Task.FromResult(7);
        }
        using (var operation = service.BeginOperation())
        {
            for (var request = 0; request < 8; request++)
            {
                var result = await operation.GetOrComputeAsync(rule, input, [], Compute);
                output.WriteLine($"request={request + 1}; executions={executions}; result={result}");
                Assert.Equal(7, result);
            }
            Assert.Equal(1, executions);
        }
        using (var operation = service.BeginOperation())
            Assert.Equal(7, await operation.GetOrComputeAsync(rule, input, [], Compute));
        Assert.Equal(2, executions);
    }

    /// <summary>
    /// Stores explicit successful validation independently from a computed Boolean value.
    /// </summary>
    [Fact]
    public async Task SuccessfulValidationFactsExecuteOncePerOperation()
    {
        var service = new OperationValidationService();
        var rule = new OperationValidationRule("fixture.validation", 1);
        var input = new object();
        var executions = 0;
        Task Validate(CancellationToken _)
        {
            executions++;
            return Task.CompletedTask;
        }
        using var operation = service.BeginOperation();
        for (var request = 0; request < 8; request++)
            await operation.ValidateAsync(rule, input, [], Validate);
        output.WriteLine($"requests=8; validationExecutions={executions}");
        Assert.Equal(1, executions);
    }
}
