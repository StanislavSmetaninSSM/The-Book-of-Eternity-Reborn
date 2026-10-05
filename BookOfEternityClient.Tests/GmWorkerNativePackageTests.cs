using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerNativePackageTests
{
    [Fact]
    public async Task Build_ProducesRelocatableManifestWithMeasuredElfAbi()
    {
        Assert.True(OperatingSystem.IsLinux(), "Actual Linux ELF packaging preparation required.");
        var evidence = Environment.GetEnvironmentVariable("BOE_NATIVE_EVIDENCE_ROOT") ?? Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "native-package");
        var output = Path.Combine(evidence, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(TestRepoPaths.RepoRoot, "scripts", "build-linux-supervisor.ps1"), "-OutputDirectory", output })
            start.ArgumentList.Add(argument);
        using var compiler = Process.Start(start)!;
        var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(compiler, TimeSpan.FromSeconds(40), Path.Combine(output, "build.log"));
        Assert.True(compiler.ExitCode == 0, "Preparation failure, not behavioral RED: " + log);
        var packagePath = Path.Combine(output, "package-manifest.json");
        Assert.True(File.Exists(packagePath), "A player package must carry protocol/source/binary/ELF ABI provenance.");
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(packagePath));
        var package = json.RootElement;
        Assert.Equal(1, package.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("linux-x64", package.GetProperty("runtimeIdentifier").GetString());
        Assert.Equal("boe-lineage-supervisor", package.GetProperty("binary").GetString());
        var binary = await File.ReadAllBytesAsync(Path.Combine(output, "boe-lineage-supervisor"));
        Assert.Equal("7F454C46", Convert.ToHexString(binary.AsSpan(0, 4)));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(binary)).ToLowerInvariant(), package.GetProperty("binarySha256").GetString());
        var symbols = Regex.Matches(System.Text.Encoding.Latin1.GetString(binary), @"GLIBC_(\d+\.\d+(?:\.\d+)?)")
            .Select(m => Version.Parse(m.Groups[1].Value)).Distinct().OrderBy(v => v).ToArray();
        Assert.NotEmpty(symbols);
        Assert.Equal(symbols[^1].ToString(), package.GetProperty("minimumGlibc").GetString());
        Assert.Matches("^[0-9a-f]{40}$", package.GetProperty("sourceCommit").GetString()!);
        Assert.Matches("^[0-9a-f]{64}$", package.GetProperty("sourceSha256").GetString()!);
        Assert.False(Path.IsPathRooted(package.GetProperty("binary").GetString()));
    }
}
