using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services.GmWorkers;
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

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("protocol")]
    [InlineData("rid")]
    [InlineData("guarantee")]
    [InlineData("libc")]
    [InlineData("hash")]
    [InlineData("elf")]
    public async Task RuntimePreflight_RejectsIncompatibleActualPackage(string mutation)
    {
        Assert.True(OperatingSystem.IsLinux());
        var evidence = Environment.GetEnvironmentVariable("BOE_NATIVE_EVIDENCE_ROOT") ?? Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "native-package");
        var output = Path.Combine(evidence, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(output);
        var start = new ProcessStartInfo("pwsh") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-NoProfile", "-File", Path.Combine(TestRepoPaths.RepoRoot, "scripts", "build-linux-supervisor.ps1"), "-OutputDirectory", output }) start.ArgumentList.Add(arg);
        using (var compiler = Process.Start(start)!)
        {
            var log = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(compiler, TimeSpan.FromSeconds(40), Path.Combine(output, "build.log"));
            Assert.True(compiler.ExitCode == 0, "Package preparation failure: " + log);
        }
        Assert.Equal(Path.Combine(output, "boe-lineage-supervisor"), GmWorkerNativePackage.Validate(output));
        var manifest = Path.Combine(output, "package-manifest.json");
        var original = await File.ReadAllTextAsync(manifest);
        await File.WriteAllTextAsync(Path.Combine(output, "package-original.json"), original);
        var document = JsonNode.Parse(original)!.AsObject();
        switch (mutation)
        {
            case "missing": File.Delete(manifest); break;
            case "duplicate": await File.WriteAllTextAsync(manifest, original.Replace("{", "{\"schemaVersion\":1,", StringComparison.Ordinal)); break;
            case "protocol": document["maximumProtocolVersion"] = 1; break;
            case "rid": document["runtimeIdentifier"] = "linux-arm64"; break;
            case "guarantee": document["guarantee"] = "root-only"; break;
            case "libc": document["minimumGlibc"] = "99.0"; break;
            case "hash": document["binarySha256"] = new string('0', 64); break;
            case "elf":
                var executable = Path.Combine(output, "boe-lineage-supervisor");
                var bytes = await File.ReadAllBytesAsync(executable); bytes[18] = 183;
                await File.WriteAllBytesAsync(executable, bytes);
                document["binarySha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                break;
        }
        if (mutation is not ("missing" or "duplicate")) await File.WriteAllTextAsync(manifest, document.ToJsonString());
        var failure = Record.Exception(() => GmWorkerNativePackage.Validate(output));
        Assert.NotNull(failure);
        Assert.True(failure is InvalidDataException or PlatformNotSupportedException or FileNotFoundException, failure.ToString());
        await File.WriteAllTextAsync(Path.Combine(output, "preflight-result.json"), JsonSerializer.Serialize(new { mutation, rejected = true, failureType = failure.GetType().Name, nativeExecutions = 0 }));
    }
}
