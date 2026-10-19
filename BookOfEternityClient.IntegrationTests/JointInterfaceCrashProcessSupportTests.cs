using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks crash adapter guards, identity and retained ownership without Linux startup.
/// </summary>
public sealed class JointInterfaceCrashProcessSupportTests
{
    /// <summary>
    /// Keeps the original wait when no extended source observation is requested.
    /// </summary>
    /// <param name="windows">
    /// Whether the requested platform is Windows.
    /// </param>
    /// <param name="role">
    /// The ordinary producer, resumer or source-probe dispatcher role.
    /// </param>
    [Theory]
    [InlineData(true, "engine-joint-interface-original-crash")]
    [InlineData(true, "engine-joint-interface-resume")]
    [InlineData(true, "engine-joint-interface-source-probe")]
    [InlineData(false, "engine-joint-interface-source-probe")]
    public void ChildTimeout_DefaultPreservesOriginalWait(bool windows, string role)
    {
        Assert.Equal(TimeSpan.FromSeconds(220),
            JointInterfaceCrashProcessSupport.GetDirectChildTimeout(windows, role, false));
    }

    /// <summary>
    /// Gives the explicit Windows source-probe request its fixed extended observation window.
    /// </summary>
    [Fact]
    public void ChildTimeout_ExtendedWindowsSourceUsesSixHundredSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(600),
            JointInterfaceCrashProcessSupport.GetDirectChildTimeout(true, "engine-joint-interface-source-probe", true));
    }

    /// <summary>
    /// Rejects extension requests for ordinary roles or non-Windows source probes before launch.
    /// </summary>
    /// <param name="windows">
    /// Whether the requested platform is Windows.
    /// </param>
    /// <param name="role">
    /// The role that must not receive an extended observation window on this platform.
    /// </param>
    [Theory]
    [InlineData(true, "engine-joint-interface-original-crash")]
    [InlineData(true, "engine-joint-interface-resume")]
    [InlineData(false, "engine-joint-interface-source-probe")]
    public void ChildTimeout_RejectsExtensionOutsideWindowsSource(bool windows, string role)
    {
        Assert.Throws<InvalidDataException>(() =>
            JointInterfaceCrashProcessSupport.GetDirectChildTimeout(windows, role, true));
    }

    /// <summary>
    /// Ordinary mode never reads inherited capture settings, including a malformed setting.
    /// </summary>
    [Fact]
    public void OrdinaryDoesNotReadCaptureSettings()
    {
        Assert.False(JointInterfaceCrashProcessSupport.UsesManagedCapture(new(1, false, null),
            () => throw new InvalidOperationException("Inherited settings must not be inspected.")));
        Assert.True(JointInterfaceCrashProcessSupport.UsesManagedCapture(new(1, true, null), () => true));
    }
    /// <summary>
    /// Ordinary Windows and Linux runs select different owners; Linux diagnostic consumers stay direct.
    /// </summary>
    [Fact]
    public void ModeMatrix_PreservesPlatformAndConsumerBoundaries()
    {
        for (var cut = 0; cut < 5; cut++)
        {
            Assert.Equal(JointInterfaceCrashBackend.DirectWindows,
                JointInterfaceCrashProcessSupport.SelectBackend(true, false, new(cut, false, null)));
            Assert.Equal(JointInterfaceCrashBackend.NativeLinux,
                JointInterfaceCrashProcessSupport.SelectBackend(false, true, new(cut, false, null)));
        }
        Assert.Equal(JointInterfaceCrashBackend.DirectLinux,
            JointInterfaceCrashProcessSupport.SelectBackend(false, true, new(1, true, null)));
        Assert.Equal(JointInterfaceCrashBackend.DirectLinux,
            JointInterfaceCrashProcessSupport.SelectBackend(false, true, new(0, false, "Version")));
        foreach (var mode in new JointInterfaceCrashMode[] { new(0, true, null), new(0, false, "Version"), new(-1, false, null), new(5, false, null) })
            Assert.Throws<InvalidDataException>(() => JointInterfaceCrashProcessSupport.SelectBackend(true, false, mode));
        Assert.Throws<PlatformNotSupportedException>(() => JointInterfaceCrashProcessSupport.SelectBackend(false, false, new(0, false, null)));
    }

    /// <summary>
    /// Windows requires explicit matching parent metadata rather than missing-field defaults.
    /// </summary>
    [Fact]
    public void EntryMode_RejectsMissingMalformedAndInconsistentParent()
    {
        using var root = new OwnedTestRoot();
        var path = Path.Combine(root.Path, "parent.json");
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashProcessSupport.ReadAndValidateEntryMode(root.Path, 0, true, false));
        foreach (var json in new[] { "{", "{}", "{\"Cut\":0,\"Fault\":null}", "{\"Cut\":0,\"Diagnostic\":false}",
            "{\"Cut\":1,\"Diagnostic\":false,\"Fault\":null}", "{\"Cut\":0,\"Diagnostic\":\"false\",\"Fault\":null}" })
        {
            File.WriteAllText(path, json);
            Assert.Throws<InvalidDataException>(() => JointInterfaceCrashProcessSupport.ReadAndValidateEntryMode(root.Path, 0, true, false));
        }
        File.WriteAllText(path, "{\"Cut\":0,\"Diagnostic\":false,\"Fault\":null}");
        Assert.Equal(new JointInterfaceCrashMode(0, false, null), JointInterfaceCrashProcessSupport.ReadAndValidateEntryMode(root.Path, 0, true, false));
    }

    /// <summary>
    /// Both dispatcher entrypoints reject forbidden Windows mode before any game fixture is created.
    /// </summary>
    /// <param name="role">
    /// The isolated original-crash or resume entrypoint.
    /// </param>
    [Theory]
    [InlineData("engine-joint-interface-original-crash")]
    [InlineData("engine-joint-interface-resume")]
    public async Task BothPublicEntryGuardsPrecedeFixture(string role)
    {
        Assert.True(OperatingSystem.IsWindows(), "These direct-entry controls are run in local Windows verification.");
        using var root = new OwnedTestRoot();
        var temporary = Path.Combine(root.Path, "tmp");
        Directory.CreateDirectory(temporary);
        File.WriteAllText(Path.Combine(root.Path, "parent.json"), JsonSerializer.Serialize(new
            { Cut = 0, Diagnostic = true, Fault = (string?)null, Nonce = Guid.NewGuid().ToString("N"), ParentPid = Environment.ProcessId }));
        File.WriteAllText(Path.Combine(root.Path, "cut.json"), JsonSerializer.Serialize(new { Root = root.Path, Cut = 0, Pid = -1 }));
        var paths = (Dictionary<string, string>)typeof(GameEngineTurnLifecycleTests)
            .GetMethod("MortalArchiveDependencyPaths", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        var executable = Path.Combine(Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? Path.GetDirectoryName(Environment.ProcessPath)!, "dotnet.exe");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = root.Path };
        foreach (var argument in new[] { "exec", "--runtimeconfig", paths["runtimeconfig"], "--depsfile", paths["depsfile"], paths["support"],
            role, paths["integration"], root.Path, Path.Combine(root.Path, "result.json"), "0", root.Path }) start.ArgumentList.Add(argument);
        start.Environment["TEMP"] = temporary; start.Environment["TMP"] = temporary;
        start.Environment["L01_G01_SETTINGS"] = Path.Combine(root.Path, "must-never-open.json");
        using var child = Process.Start(start) ?? throw new InvalidOperationException("Guard child did not start.");
        var stdout = child.StandardOutput.ReadToEndAsync(); var stderr = child.StandardError.ReadToEndAsync();
        var fixtureCreated = false;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (!child.HasExited)
            {
                fixtureCreated |= Directory.EnumerateDirectories(temporary).Any();
                if (fixtureCreated) break;
                await Task.Delay(10, timeout.Token);
            }
        }
        finally
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
            await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
        }
        Assert.False(fixtureCreated, "Invalid mode reached a game fixture before the entry guard.");
        Assert.Contains("CRASH5 mode", await stderr);
        Assert.NotEqual(0, child.ExitCode);
        Assert.NotEqual(73, child.ExitCode);
    }

    /// <summary>
    /// Windows identity comes from the supplied original process and never populates Linux ticks.
    /// </summary>
    [Fact]
    public void Identity_UsesOriginalWindowsProcessAndExplicitKind()
    {
        Assert.True(OperatingSystem.IsWindows());
        using var original = Process.GetCurrentProcess();
        var identity = JointInterfaceCrashProcessSupport.ReadLiveIdentity(original.Id, original);
        Assert.Equal("windows-process-start-utc-ticks", identity.BirthKind);
        Assert.Equal(original.StartTime.ToUniversalTime().Ticks, identity.WindowsStartTimeUtcTicks);
        Assert.True(identity.WindowsStartTimeUtcTicks > 0);
        Assert.Null(identity.LinuxBirthTick);
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashProcessSupport.ReadLiveIdentity(original.Id + 1, original));
        var node = JointInterfaceCrashProcessSupport.AddIdentity(new(), identity);
        Assert.False(node.ContainsKey("BirthTick"));
        Assert.Equal(identity.WindowsStartTimeUtcTicks, node["WindowsStartTimeUtcTicks"]!.GetValue<long>());
        var linux = new JointInterfaceCrashIdentity(42, DateTime.UnixEpoch, "linux-proc-start-tick", 1234, null);
        var legacy = JointInterfaceCrashProcessSupport.AddIdentity(new(), linux);
        Assert.Equal(1234UL, legacy["BirthTick"]!.GetValue<ulong>());
        Assert.False(legacy.ContainsKey("WindowsStartTimeUtcTicks"));
        Assert.False(legacy.ContainsKey("BirthKind"));
    }

    /// <summary>
    /// Child start info uses platform host names and private environment without shell quoting.
    /// </summary>
    [Fact]
    public void ChildStartInfo_PreservesSpacesAndChildOnlyTemporaryRoot()
    {
        using var root = new OwnedTestRoot();
        var runtime = Path.Combine(root.Path, "runtime with spaces");
        Directory.CreateDirectory(runtime);
        File.WriteAllText(Path.Combine(runtime, "dotnet.exe"), "fixture");
        File.WriteAllText(Path.Combine(runtime, "dotnet"), "fixture");
        var arguments = new[] { "exec", "assembly with spaces.dll", "literal\"quote" };
        var originalTmp = Environment.GetEnvironmentVariable("TMP");
        var windows = JointInterfaceCrashProcessSupport.CreateChildStartInfo(runtime, arguments, root.Path, true);
        Assert.Equal(Path.Combine(runtime, "dotnet.exe"), windows.FileName);
        Assert.Equal(arguments, windows.ArgumentList);
        Assert.Equal(Path.Combine(root.Path, "tmp"), windows.Environment["TEMP"]);
        Assert.Equal(Path.Combine(root.Path, "tmp"), windows.Environment["TMP"]);
        Assert.False(windows.UseShellExecute);
        Assert.True(windows.RedirectStandardOutput && windows.RedirectStandardError);
        Assert.Equal(originalTmp, Environment.GetEnvironmentVariable("TMP"));
        var linux = JointInterfaceCrashProcessSupport.CreateChildStartInfo(runtime, arguments, root.Path, false);
        Assert.Equal(Path.Combine(runtime, "dotnet"), linux.FileName);
        Assert.Equal(Path.Combine(root.Path, "tmp"), linux.Environment["TMPDIR"]);
        Assert.Throws<InvalidDataException>(() => JointInterfaceCrashProcessSupport.CreateChildStartInfo(Path.Combine(root.Path, "absent"), arguments, root.Path, true));
    }

    /// <summary>
    /// Failed disposal cannot release retained custody; confirmed disposal releases exactly that owner.
    /// </summary>
    [Fact]
    public async Task UncertainOwnerRetainsOriginalCustody()
    {
        var owner = new DisposalOwner();
        var custody = new JointInterfaceCrashRetainedOwner(owner);
        try
        {
            Assert.Same(owner, custody.Owner);
            await Assert.ThrowsAsync<InvalidOperationException>(() => custody.DisposeConfirmedAsync());
            Assert.True(custody.IsRetained);
            Assert.Same(owner, custody.Owner);
        }
        finally
        {
            owner.AllowDisposal = true;
            await custody.DisposeConfirmedAsync();
        }
        Assert.False(custody.IsRetained);
        Assert.True(owner.Disposed);
    }

    /// <summary>
    /// Output persistence errors remain visible without replacing an earlier process failure.
    /// </summary>
    [Fact]
    public void OutputFailure_PreservesPrimaryAndAllSecondaryErrors()
    {
        using var root = new OwnedTestRoot();
        var missing = Path.Combine(root.Path, "absent", "result");
        var original = new TimeoutException("original");
        var failure = JointInterfaceCrashProcessSupport.SaveOutputs(missing, "output", "error", original);
        Assert.Same(original, failure);
        Assert.Equal(2, failure!.Data.Count);
        Assert.IsAssignableFrom<IOException>(JointInterfaceCrashProcessSupport.SaveOutputs(missing, "output", null, null));
    }

    /// <summary>
    /// Owns a unique test directory without shared fixture state.
    /// </summary>
    private sealed class OwnedTestRoot : IDisposable
    {
        /// <summary>
        /// Gets this test's exclusive temporary directory.
        /// </summary>
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "crash5-contract-" + Guid.NewGuid().ToString("N"));

        /// <summary>
        /// Creates this test's exclusive directory.
        /// </summary>
        internal OwnedTestRoot() => Directory.CreateDirectory(Path);

        /// <summary>
        /// Deletes this exclusive test directory after child ownership has settled.
        /// </summary>
        public void Dispose() => new CleanupOwnedFixture(Path, _ => { }).Dispose();
    }

    /// <summary>
    /// Supplies a disposal boundary whose uncertainty can be cleared only by the test's explicit observation.
    /// </summary>
    private sealed class DisposalOwner : GmWorkerOwnedLaunch
    {
        /// <summary>
        /// Controls the fixture's positive disposal outcome.
        /// </summary>
        internal bool AllowDisposal;
        /// <summary>
        /// Records whether the original owner was actually disposed.
        /// </summary>
        internal bool Disposed;
        /// <inheritdoc/>
        internal override GmWorkerExecutionIdentity Identity => new("fixture", GmWorkerBackend.NativeLineage, "fixture");
        /// <inheritdoc/>
        internal override int HostProcessId => 42;
        /// <inheritdoc/>
        internal override int SupervisorProcessId => 43;
        /// <inheritdoc/>
        internal override int? AdmittedHostProcessId => 42;
        /// <inheritdoc/>
        internal override Task HostExited => Task.CompletedTask;
        /// <inheritdoc/>
        internal override Task WaitUntilReadyAsync(GmWorkerProcessHostLaunch host, CancellationToken cancellationToken) => throw new NotSupportedException();
        /// <inheritdoc/>
        internal override Task<int> WaitForWorkerCompletionAsync(GmWorkerProcessHostLaunch host, CancellationToken cancellationToken) => throw new NotSupportedException();
        /// <inheritdoc/>
        internal override Task<GmWorkerStopEvidence> StopAndObserveAsync() => throw new NotSupportedException();
        /// <inheritdoc/>
        internal override Task<GmWorkerOwnedOutputs> SettleOutputsAsync() => throw new NotSupportedException();

        /// <summary>
        /// Refuses disposal until the fixture explicitly supplies a positive outcome.
        /// </summary>
        /// <returns>
        /// A completed disposal or an uncertainty failure.
        /// </returns>
        public override ValueTask DisposeAsync()
        {
            if (!AllowDisposal) throw new InvalidOperationException("Original owner is uncertain.");
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
