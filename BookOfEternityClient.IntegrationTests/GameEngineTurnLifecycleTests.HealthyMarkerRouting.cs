using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>Explicit malformed healthy routing cannot fall back; real failure diagnostics remain unchanged.</summary>
    [Theory]
    [InlineData("unset", true)]
    [InlineData("stderr", true)]
    [InlineData("unknown", false)]
    [InlineData("external-missing", false)]
    [InlineData("stderr-path", false)]
    public void HealthyMarkerRoutingPreservesFailureChannel(string mode, bool healthyOnStderr)
    {
        var nonce = Guid.NewGuid().ToString("N");
        var routeKey = MainOperationFailureDiagnostic.PhysicalRouteVariable;
        var pathKey = MainOperationFailureDiagnostic.PhysicalDirectoryVariable;
        var nonceKey = GmSessionRunCoordinator.StopDiagnosticNonceVariable;
        var priorRoute = Environment.GetEnvironmentVariable(routeKey);
        var priorPath = Environment.GetEnvironmentVariable(pathKey);
        var priorNonce = Environment.GetEnvironmentVariable(nonceKey);
        var priorError = Console.Error;
        using var captured = new StringWriter();
        try
        {
            Environment.SetEnvironmentVariable(routeKey, mode switch
            { "unset" => null, "stderr" or "stderr-path" => "stderr", "external-missing" => "external", _ => "UNKNOWN" });
            Environment.SetEnvironmentVariable(pathKey, mode == "stderr-path" ? Path.Combine(_rootPath, "not-created") : null);
            Environment.SetEnvironmentVariable(nonceKey, nonce);
            Console.SetError(captured);
            MainOperationFailureDiagnostic.Mark(nonce, "canonical-current-generation-lease-acquired");
            MainOperationFailureDiagnostic.Report(nonce, "actual-failure-channel", null, new IOException("preserved-failure"));
        }
        finally
        {
            Console.SetError(priorError);
            Environment.SetEnvironmentVariable(routeKey, priorRoute);
            Environment.SetEnvironmentVariable(pathKey, priorPath);
            Environment.SetEnvironmentVariable(nonceKey, priorNonce);
        }
        var lines = captured.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        const string failurePrefix = "BOE_MAIN_OPERATION_FAILURE ";
        using var failure = JsonDocument.Parse(Assert.Single(lines.Where(x => x.StartsWith(failurePrefix, StringComparison.Ordinal)))[failurePrefix.Length..]);
        Assert.Equal(nonce, failure.RootElement.GetProperty("Nonce").GetString());
        Assert.Equal("preserved-failure", failure.RootElement.GetProperty("Message").GetString());
        Assert.Equal("System.IO.IOException", failure.RootElement.GetProperty("ExceptionType").GetString());
        const string healthyPrefix = "BOE_MAIN_OPERATION_MARKER ";
        var healthy = lines.Where(x => x.StartsWith(healthyPrefix, StringComparison.Ordinal)).ToArray();
        Assert.Equal(healthyOnStderr ? 1 : 0, healthy.Length);
        Assert.Equal(1 + healthy.Length, lines.Length);
        if (healthyOnStderr)
        {
            using var marker = JsonDocument.Parse(healthy[0][healthyPrefix.Length..]);
            Assert.Equal(nonce, marker.RootElement.GetProperty("Nonce").GetString());
            Assert.Equal(Environment.ProcessId, marker.RootElement.GetProperty("ProcessId").GetInt32());
            Assert.Equal("canonical-current-generation-lease-acquired", marker.RootElement.GetProperty("Stage").GetString());
        }
    }
}
