using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class IntegrationTestBoundaryTests
{
    /// <summary>
    /// Verifies exact methods in both C# projects and exact PowerShell files in one category plan.
    /// </summary>
    /// <returns>
    /// A task completing after discovery produces bounded descriptors without running tests.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_SelectedContractSpansBothProjectsWithExactMethodFilters()
    {
        var plan = await RunCSharpCategoryPlanAsync("test-selection-contracts");
        Assert.True(plan.ExitCode == 0, plan.StandardOutput + plan.StandardError);
        var descriptors = plan.StandardOutput.Split('\n')
            .Where(line => line.StartsWith("PLAN ", StringComparison.Ordinal))
            .Select(line => JsonDocument.Parse(line[5..]))
            .ToArray();
        try
        {
            Assert.NotEmpty(descriptors);
            Assert.Contains(descriptors, descriptor =>
                descriptor.RootElement.GetProperty("Project").GetString()!
                    .EndsWith("BookOfEternityClient.Tests.csproj", StringComparison.Ordinal));
            Assert.Contains(descriptors, descriptor =>
                descriptor.RootElement.GetProperty("Project").GetString()!
                    .EndsWith("BookOfEternityClient.IntegrationTests.csproj", StringComparison.Ordinal));
            var csharpDescriptors = descriptors.Where(descriptor =>
                descriptor.RootElement.GetProperty("Adapter").GetString() is "unit" or "integration")
                .ToArray();
            Assert.NotEmpty(csharpDescriptors);
            Assert.All(csharpDescriptors, descriptor =>
            {
                var filter = descriptor.RootElement.GetProperty("Filter").GetString();
                Assert.NotNull(filter);
                Assert.StartsWith("FullyQualifiedName=", filter, StringComparison.Ordinal);
                Assert.DoesNotContain("FullyQualifiedName~", filter, StringComparison.Ordinal);
                var methods = descriptor.RootElement.GetProperty("ExpectedMethodCounts")
                    .EnumerateArray().Select(row => row.GetProperty("Name").GetString()).ToArray();
                Assert.NotEmpty(methods);
                Assert.Equal(methods.Length, methods.Distinct(StringComparer.Ordinal).Count());
                Assert.All(methods, method =>
                    Assert.Contains("FullyQualifiedName=" + method, filter, StringComparison.Ordinal));
            });
            var powershellDescriptors = descriptors.Where(descriptor =>
                descriptor.RootElement.GetProperty("Adapter").GetString() == "powershell")
                .ToArray();
            Assert.NotEmpty(powershellDescriptors);
            Assert.All(powershellDescriptors, descriptor =>
            {
                Assert.Equal("powershell", descriptor.RootElement.GetProperty("Project").GetString());
                Assert.Equal(JsonValueKind.Null, descriptor.RootElement.GetProperty("Filter").ValueKind);
                var files = descriptor.RootElement.GetProperty("ExpectedMethodCounts")
                    .EnumerateArray().Select(row => row.GetProperty("Name").GetString()!).ToArray();
                Assert.NotEmpty(files);
                Assert.All(files, file =>
                {
                    Assert.StartsWith("scripts/tests/", file, StringComparison.Ordinal);
                    Assert.EndsWith(".tests.ps1", file, StringComparison.Ordinal);
                });
            });
            var selectedNames = descriptors.SelectMany(descriptor =>
                descriptor.RootElement.GetProperty("ExpectedMethodCounts")
                    .EnumerateArray().Select(row => row.GetProperty("Name").GetString()!))
                .ToArray();
            Assert.Equal(selectedNames.Length, selectedNames.Distinct(StringComparer.Ordinal).Count());
            Assert.Contains("Planned selection: descriptors=", plan.StandardOutput,
                StringComparison.Ordinal);
        }
        finally
        {
            foreach (var descriptor in descriptors) descriptor.Dispose();
        }
    }

    /// <summary>
    /// Invokes discovery-only category planning without building or executing the selected tests.
    /// </summary>
    /// <param name="category">
    /// The documented category ID to inspect.
    /// </param>
    /// <returns>
    /// The runner exit code and captured output.
    /// </returns>
    private static async Task<RunnerSelfTestResult> RunCSharpCategoryPlanAsync(string category)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = TestRepoPaths.RepoRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in new[] { "-NoProfile", "-File",
            Path.Combine(TestRepoPaths.RepoRoot, "scripts", "test-csharp.ps1"),
            "-Category", category, "-PlanOnly", "-NoBuild" })
            startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("PowerShell category plan did not start.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("PowerShell category plan exceeded 45 seconds.");
        }
        return new RunnerSelfTestResult(process.ExitCode, await output, await error);
    }
}
