using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ClientStartupOptionsTests : IDisposable
{
    private readonly string _rootPath;

    public ClientStartupOptionsTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-startup-options-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void Parse_NoWebFlag_PreservesConsoleModeAndDefaultPath()
    {
        var options = ClientStartupOptions.Parse(Array.Empty<string>(), _rootPath);

        Assert.False(options.WebMode);
        Assert.Equal(_rootPath, options.BasePath);
        Assert.Equal(ClientStartupOptions.DefaultWebUrl, options.WebUrl);
    }

    [Fact]
    public void Parse_LegacyPathArgument_OverridesBasePath()
    {
        var sessionRoot = Path.Combine(_rootPath, "custom");
        Directory.CreateDirectory(sessionRoot);

        var options = ClientStartupOptions.Parse(new[] { sessionRoot }, _rootPath);

        Assert.False(options.WebMode);
        Assert.Equal(sessionRoot, options.BasePath);
    }

    [Fact]
    public void Parse_WebMode_UsesDefaultLocalUrl()
    {
        var options = ClientStartupOptions.Parse(new[] { "--web" }, _rootPath);

        Assert.True(options.WebMode);
        Assert.Equal(_rootPath, options.BasePath);
        Assert.Equal(ClientStartupOptions.DefaultWebUrl, options.WebUrl);
    }

    [Fact]
    public void Parse_WebModeWithUrl_UsesExplicitUrlAndLegacyPath()
    {
        var sessionRoot = Path.Combine(_rootPath, "custom");
        Directory.CreateDirectory(sessionRoot);

        var options = ClientStartupOptions.Parse(
            new[] { sessionRoot, "--web", "--web-url", "http://127.0.0.1:8788" },
            _rootPath);

        Assert.True(options.WebMode);
        Assert.Equal(sessionRoot, options.BasePath);
        Assert.Equal("http://127.0.0.1:8788", options.WebUrl);
    }

    [Fact]
    public void Parse_E2EScriptOptions_UsesExplicitScriptArtifactsAndPlainOutput()
    {
        var sessionRoot = Path.Combine(_rootPath, "custom");
        Directory.CreateDirectory(sessionRoot);
        var scriptPath = Path.Combine(_rootPath, "script.json");
        var artifactRoot = Path.Combine(_rootPath, "artifacts");

        var options = ClientStartupOptions.Parse(
            new[] { sessionRoot, "--e2e-script", scriptPath, "--e2e-artifacts", artifactRoot, "--plain-output" },
            _rootPath);

        Assert.False(options.WebMode);
        Assert.Equal(sessionRoot, options.BasePath);
        Assert.Equal(scriptPath, options.E2EScriptPath);
        Assert.Equal(artifactRoot, options.E2EArtifactsPath);
        Assert.True(options.PlainOutput);
    }

    [Fact]
    public void Parse_E2EScriptMissingValue_ThrowsDiagnostic()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ClientStartupOptions.Parse(new[] { "--e2e-script" }, _rootPath));

        Assert.Contains("Missing value for --e2e-script", ex.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
