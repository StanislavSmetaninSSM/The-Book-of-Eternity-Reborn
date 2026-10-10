using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class LinuxInventoryReadCloseTests(ITestOutputHelper output)
{
    [Fact]
    public async Task ActualReadContextOwnedLateCloseRetainsBytesAndColdRead()
    {
        Assert.True(OperatingSystem.IsLinux());
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        output.WriteLine("Owned read-close evidence: " + own);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false; var pauses = 0; var mutations = 0;
        var files = new FileSystemManager(Path.Combine(own, "play"), NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = async path =>
                {
                    if (armed && path == InventoryEquipmentService.ItemsPath && pauses++ == 0)
                    { entered.TrySetResult(); await allow.Task; }
                },
                BeforeCanonicalMutationBoundaryAsync = _ =>
                { if (armed) mutations++; return Task.CompletedTask; }
            });
        PortableSaveFixture.Seed(files);
        var item = MortalItemTestFixture.CreateRawRoot(creationRef: "new_item_read_close", materializationId: "mat_item_read_close");
        item["quality"] = "Rare"; item["rarity"] = "Rare"; item["count"] = 5;
        var receipt = MortalItemIdentityState.CreateRootReceipt(item, "gc_stack", acceptedTurn: 42);
        item["itemId"] = "gc_stack"; item["existedId"] = "gc_stack"; item.Remove("creationRef"); item["materializationReceipt"] = receipt;
        await files.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(item), ["equippedItems"] = new JsonObject() }.ToJsonString());
        await files.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, MortalItemTestFixture.CreateIndex(item).ToJsonString());
        var pristine = ResourceBootstrapStateBuilder.BuildPristine();
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(pristine.Definitions!, files.ReadFileAsync,
            pristine.State, pristine.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
        await files.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        Dictionary<string, string> Snapshot() => Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).ToDictionary(p => Path.GetRelativePath(files.GameSessionPath, p),
                p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant());
        var before = Snapshot();
        var generation = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(files.SessionGenerationPath))).ToLowerInvariant();
        var closeFailure = new IOException("controlled actual source-only104 read-context owned late close");
        var closer = new ThrowingClose(closeFailure);
        FileSystemManager.CanonicalWriteLease? original = null;
        Task<InventoryManagementContext?>? operation = null;
        Exception? observed = null;
        try
        {
            armed = true;
            operation = InventoryManagementService.ReadContextAsync(files);
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(8)));
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var state = operation.GetType().GetField("StateMachine", flags)!.GetValue(operation)!;
            Assert.Contains("InventoryManagementService+<ReadContextAsync>", state.GetType().FullName);
            original = Assert.Single(state.GetType().GetFields(flags).Select(f => f.GetValue(state))
                .OfType<FileSystemManager.CanonicalWriteLease>().Distinct());
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            original.ExternalPublicationContext = closer;
            allow.TrySetResult();
            observed = await Record.ExceptionAsync(async () => await operation.WaitAsync(TimeSpan.FromSeconds(8)));
        }
        finally
        {
            allow.TrySetResult();
            if (operation != null) { try { await operation.WaitAsync(TimeSpan.FromSeconds(8)); } catch { } }
        }
        Assert.Same(closeFailure, observed);
        Assert.Equal(1, closer.Calls);
        Assert.NotNull(original); Assert.False(original.IsActive);
        Assert.Null(original.ExternalPublicationContext); Assert.Null(original.MainAdmission); Assert.Null(original.AmbientRegistration);
        Assert.Equal(0, mutations);
        Assert.Equal(before, Snapshot());
        Assert.Equal(generation, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(files.SessionGenerationPath))).ToLowerInvariant());
        using (File.Open(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        File.WriteAllText(Path.Combine(own, "read-close.json"), JsonSerializer.Serialize(new
        {
            SourceOnlyRow = 104, Scope = "actual read owner/component fault, not whole gameplay chain",
            ModelCalls = 0, InitialFixture = "current sealed Rare item5; receipt turn42, not accepted GM turn",
            Exception = observed.ToString(), CloseCalls = closer.Calls, OriginalLeaseInactive = !original.IsActive,
            MutationsAfterArm = mutations, Before = before, After = Snapshot(), GenerationSHA256 = generation
        }));
        var repo = TestRepoPaths.RepoRoot;
        var package = Path.Combine(own, "package");
        async Task Run(string executable, string[] arguments, string log, int seconds)
        {
            var info = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = repo };
            foreach (var arg in arguments) info.ArgumentList.Add(arg);
            using var process = Process.Start(info)!;
            var text = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(process, TimeSpan.FromSeconds(seconds), Path.Combine(own, log));
            Assert.True(process.ExitCode == 0, own + Environment.NewLine + text);
        }
        await Run("pwsh", ["-NoLogo", "-NoProfile", "-File", Path.Combine(repo, "scripts/build-linux-supervisor.ps1"), "-OutputDirectory", package, "-IncludeHostGuardian"], "native.log", 20);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        var dotnet = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? throw new InvalidOperationException("DOTNET_ROOT must identify the actual Linux runtime"), "dotnet");
        Assert.True(File.Exists(dotnet));
        await Run(Path.Combine(package, "host-guardian"), ["--live-turn", Path.Combine(own, "guardian.json"), "12000",
            dotnet, Path.Combine(repo, "BookOfEternityClient.TestSupport/bin", configuration, "net8.0/BookOfEternityClient.TestSupport.dll"),
            "inventory-read-cold", files.BasePath, Path.Combine(own, "cold-read.json")], "cold.log", 18);
        using var guardian = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(own, "guardian.json")));
        Assert.Equal(0, guardian.RootElement.GetProperty("driverExitCode").GetInt32());
        using var cold = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(own, "cold-read.json")));
        Assert.NotEqual(Environment.ProcessId, cold.RootElement.GetProperty("ProcessId").GetInt32());
        var coldItem = Assert.Single(cold.RootElement.GetProperty("Items").EnumerateArray());
        Assert.Equal("gc_stack", coldItem.GetProperty("Identity").GetString()); Assert.Equal(5, coldItem.GetProperty("Count").GetInt32());
        Assert.Equal(generation, cold.RootElement.GetProperty("Generation").GetString());
        Assert.Equal(before, JsonSerializer.Deserialize<Dictionary<string, string>>(cold.RootElement.GetProperty("State")));
        Assert.True(guardian.RootElement.GetProperty("echild").GetBoolean()); Assert.Equal(0, guardian.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0, guardian.RootElement.GetProperty("failures").GetInt32()); Assert.False(guardian.RootElement.GetProperty("deadline").GetBoolean());
    }

    private sealed class ThrowingClose(IOException failure) : IDisposable
    {
        internal int Calls { get; private set; }
        public void Dispose() { Calls++; throw failure; }
    }
}
