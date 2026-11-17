using System.Diagnostics;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Services.GmRuntime;
using Xunit;

namespace BookOfEternityClient.Tests;

// Linux executes the real external helper branch explicitly, not a fake Desktop
// runtime. Actual Windows PowerShell 5.1 qualification remains separate.
public sealed class GmBridgeDesktopControlTests
{
    private const string Id = "0123456789abcdef0123456789abcdef";
    private static string Frame(string status = "{}", int timeout = 3000) => JsonSerializer.Serialize(new
    { requestId = Id, statusJson = status, requestJson = "{\"command\":\"diagnostics\"}", timeoutMilliseconds = timeout },
        new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    [Fact]
    public void EnvelopePreservesRawStatusRequestIdentityAndDeadline()
    {
        const string status = "{\"pipeName\":\"diagnostic only\",\"transportEndpoint\":null}";
        var parsed = GmBridgeRequestControl.ParseRequest(Frame(status));
        Assert.Equal(Id, parsed.RequestId);
        Assert.Equal(status, parsed.StatusJson);
        Assert.Equal("{\"command\":\"diagnostics\"}", parsed.RequestJson);
        Assert.Equal(3000, parsed.TimeoutMilliseconds);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"requestId\":\"x\",\"statusJson\":\"{}\",\"requestJson\":\"{}\",\"timeoutMilliseconds\":1}")]
    [InlineData("{\"requestId\":\"0123456789abcdef0123456789abcdef\",\"statusJson\":\"{}\",\"requestJson\":\"{}\",\"timeoutMilliseconds\":0}")]
    [InlineData("{\"requestId\":\"0123456789abcdef0123456789abcdef\",\"statusJson\":\"{}\",\"requestJson\":\"{}\",\"timeoutMilliseconds\":-1}")]
    [InlineData("{\"requestId\":\"0123456789abcdef0123456789abcdef\",\"requestId\":\"0123456789abcdef0123456789abcdef\",\"statusJson\":\"{}\",\"requestJson\":\"{}\",\"timeoutMilliseconds\":1}")]
    [InlineData("{\"RequestId\":\"0123456789abcdef0123456789abcdef\",\"statusJson\":\"{}\",\"requestJson\":\"{}\",\"timeoutMilliseconds\":1}")]
    [InlineData("{\"requestId\":\"0123456789abcdef0123456789abcdef\",\"statusJson\":\"{}\",\"requestJson\":\"{}\",\"timeoutMilliseconds\":1,\"fallback\":true}")]
    public void EnvelopeRefusesMissingDuplicateUnknownCorrelationOrDeadline(string json)
        => Assert.Throws<InvalidDataException>(() => GmBridgeRequestControl.ParseRequest(json));

    [Fact]
    public async Task StdinEofRefusesWithoutInventingResponse()
    {
        using var input = new MemoryStream(); using var output = new MemoryStream();
        Assert.Equal(2, await GmBridgeRequestControl.RunAsync(input, output, CancellationToken.None));
        Assert.Empty(output.ToArray());
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("duplicate")]
    [InlineData("double-bom")]
    [InlineData("utf16")]
    [InlineData("invalid-utf8")]
    public async Task ActualStdinRefusesMalformedFramesWithoutUncorrelatedOutput(string kind)
    {
        var frame = Frame("{\"pipeName\":\"старый\"}") + "\n";
        byte[] bytes = kind switch
        {
            "malformed" => Encoding.UTF8.GetBytes("{broken\n"),
            "duplicate" => Encoding.UTF8.GetBytes(frame.Replace("{\"requestId\":", "{\"requestId\":\"" + Id + "\",\"requestId\":")),
            "double-bom" => new byte[] { 0xef, 0xbb, 0xbf, 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(frame)).ToArray(),
            "utf16" => Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(frame)).ToArray(),
            _ => new byte[] { 0xc3, 0x28, 0x0a }
        };
        using var input = new MemoryStream(bytes); using var output = new MemoryStream();
        Assert.Equal(2, await GmBridgeRequestControl.RunAsync(input, output, CancellationToken.None));
        Assert.Empty(output.ToArray());
    }

    [Fact]
    public async Task ActualUtf8BomFramePreservesCorrelationAndCyrillicPayload()
    {
        var frame = Frame("{\"pipeName\":\"старый\"}") + "\n";
        using var input = new MemoryStream(new byte[] { 0xef, 0xbb, 0xbf }.Concat(Encoding.UTF8.GetBytes(frame)).ToArray());
        using var output = new MemoryStream();
        Assert.Equal(2, await GmBridgeRequestControl.RunAsync(input, output, CancellationToken.None));
        using var response = JsonDocument.Parse(output.ToArray());
        Assert.Equal(Id, response.RootElement.GetProperty("requestId").GetString());
        Assert.False(response.RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task MissingEndpointProducesCorrelatedRefusalWithoutLegacyFallback()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Frame("{\"pipeName\":\"legacy-only\"}") + "\n"));
        using var output = new MemoryStream();
        Assert.Equal(2, await GmBridgeRequestControl.RunAsync(input, output, CancellationToken.None));
        using var reply = JsonDocument.Parse(output.ToArray());
        Assert.Equal(Id, reply.RootElement.GetProperty("requestId").GetString());
        Assert.False(reply.RootElement.GetProperty("ok").GetBoolean());
        Assert.False(reply.RootElement.TryGetProperty("response", out _));
    }

    [Fact]
    public void DesktopDispatchReturnsBeforeNet8AssemblyLoadAndCoreRetainsDirectConnector()
    {
        var source = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/Launcher/bookofeternity.ps1"));
        var body = source[source.IndexOf("function Invoke-BridgeRequest {", StringComparison.Ordinal)..];
        body = body[..body.IndexOf("function Get-BridgePromptContentHash", StringComparison.Ordinal)];
        var desktop = body.IndexOf("$PSVersionTable.PSEdition -eq 'Desktop'", StringComparison.Ordinal);
        var helper = body.IndexOf("Invoke-BoeBridgeRequestProcess", StringComparison.Ordinal);
        var returned = body.IndexOf("return $receipt.ResponseJson", StringComparison.Ordinal);
        var load = body.IndexOf("Add-Type -Path $assembly", StringComparison.Ordinal);
        Assert.True(desktop >= 0 && helper > desktop && returned > helper && load > returned);
        Assert.Contains("BridgeTransportConnector]::RequestAsync", body);
    }

    [Theory]
    [InlineData("reply")]
    [InlineData("withheld")]
    [InlineData("eof")]
    public async Task RealExternalProcessUsesSameFifoAndJoinsOriginalIo(string outcome)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(TestDiagnosticPaths.Root, "bridge-desktop-control", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var compiled = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/bin", configuration, "net8.0");
            var codeRoot = Path.Combine(root, "пакет с пробелами");
            Directory.CreateSymbolicLink(codeRoot, compiled);
            await using var listener = LinuxBridgeFifoListener.Create(root);
            var statusPath = Path.Combine(root, "status.json");
            await File.WriteAllTextAsync(statusPath, JsonSerializer.Serialize(new
            { transportEndpoint = new BridgeTransportEndpoint(1, "linux-fifo-v1", listener.Endpoint.DirectoryPath, listener.Endpoint.RunId) }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var start = new ProcessStartInfo("pwsh")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
            foreach (var arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
                Path.Combine(TestRepoPaths.RepoRoot, "tests/fixtures/GmBridgeDesktopControl/request.ps1"),
                "-RepoRoot", TestRepoPaths.RepoRoot, "-CodeRoot", codeRoot, "-StatusPath", statusPath, "-TimeoutMilliseconds", "3000" })
                start.ArgumentList.Add(arg);
            using var child = Process.Start(start) ?? throw new IOException("External control fixture did not start.");
            child.StandardInput.Close();
            var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
            var exited = child.WaitForExitAsync(deadline.Token);
            var accepting = listener.AcceptAsync(deadline.Token);
            Stream? stream = null;
            try
            {
                var first = await Task.WhenAny(accepting, exited);
                Assert.True(first == accepting, "External helper must connect through the original typed FIFO before its caller exits: " +
                    (first == exited ? await stdout : ""));
                stream = await accepting;
                using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, true);
                using var request = JsonDocument.Parse((await reader.ReadLineAsync(deadline.Token))!);
                Assert.Equal("diagnostics", request.RootElement.GetProperty("command").GetString());
                Assert.Equal("Проверка реле", request.RootElement.GetProperty("text").GetString());
                if (outcome == "reply")
                    await stream.WriteAsync("{\"ok\":true,\"actual\":73}\n"u8.ToArray(), deadline.Token);
                else if (outcome == "eof") { await stream.DisposeAsync(); stream = null; }
                await exited;
                Assert.Equal(0, child.ExitCode); Assert.True(string.IsNullOrWhiteSpace(await stderr), await stderr);
                using var response = JsonDocument.Parse((await stdout).Trim());
                var value = response.RootElement;
                Assert.Equal(outcome == "reply", value.GetProperty("Succeeded").GetBoolean());
                if (outcome == "reply")
                {
                    var receipt = value.GetProperty("Receipt");
                    Assert.Equal(0, receipt.GetProperty("ExitCode").GetInt32());
                    Assert.True(receipt.GetProperty("StdoutEof").GetBoolean());
                    Assert.True(receipt.GetProperty("StderrEof").GetBoolean());
                    Assert.True(receipt.GetProperty("HelperExited").GetBoolean());
                    Assert.True(receipt.GetProperty("WriteCompleted").GetBoolean());
                    Assert.True(receipt.GetProperty("CleanupComplete").GetBoolean());
                    Assert.Equal("{\"ok\":true,\"actual\":73}", receipt.GetProperty("ResponseJson").GetString());
                }
                else
                {
                    Assert.True(value.GetProperty("HelperExited").GetBoolean());
                    Assert.True(value.GetProperty("WriteCompleted").GetBoolean());
                    Assert.True(value.GetProperty("CleanupComplete").GetBoolean());
                    Assert.True(value.GetProperty("StdoutEof").GetBoolean());
                    Assert.True(value.GetProperty("StderrEof").GetBoolean());
                }
                if (stream != null) Assert.Null(await reader.ReadLineAsync(deadline.Token));
            }
            finally
            {
                deadline.Cancel();
                if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
                try { await accepting; } catch (OperationCanceledException) { }
                if (accepting.IsCompletedSuccessfully) await accepting.Result.DisposeAsync();
                try { await exited; } catch (OperationCanceledException) { }
                await Task.WhenAll(stdout, stderr);
                if (stream != null) await stream.DisposeAsync();
            }
        }
        finally { Directory.Delete(root, true); }
    }
}
