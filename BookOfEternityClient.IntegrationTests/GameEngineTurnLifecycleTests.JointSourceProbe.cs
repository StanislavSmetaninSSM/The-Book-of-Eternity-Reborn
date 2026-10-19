using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Investigates source42 once within the existing owned-child deadline, without qualifying a crash cut.
    /// </summary>
    /// <returns>
    /// Completion only when the actual source turn and QTE action return successfully.
    /// </returns>
    [Fact]
    public async Task JointInterface_Source42WindowsProbeObservesActualPreparation()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("T227 is a local Windows investigation.");
        using var classOwned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        var own = Path.Combine(Path.GetTempPath(), "boe-joint-interface-source42-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(own, "tmp"));
        using var owned = new JointInterfaceCrashOwnedContainer(own, line => _directGachaOutput?.WriteLine(line), false);
        var nonce = Guid.NewGuid().ToString("N");
        File.WriteAllText(Path.Combine(own, "parent.json"), JsonSerializer.Serialize(new
        {
            Nonce = nonce, RunId = Guid.NewGuid().ToString("N"), ParentPid = Environment.ProcessId,
            Cut = 0, Fault = (string?)null, Diagnostic = false, Source42Probe = true
        }));
        var output = Path.Combine(own, "source42.json");
        var child = await RunJointInterfaceNativeChildAsync("engine-joint-interface-source-probe", own, output, 0, own, owned);
        WritePhysicalInjuryDiagnostic("crash5_source42_probe_exit", child);
        Assert.Equal(0, child.ExitCode);
        Assert.True(child.Eof); Assert.True(child.Joined);
        var result = JsonNode.Parse(File.ReadAllText(output))!.AsObject();
        AssertJointInterfaceChildIdentity(child, result);
        Assert.Equal(child.Pid, result["Pid"]!.GetValue<int>());
        Assert.Equal(nonce, result["Nonce"]!.GetValue<string>());
        Assert.Equal(42, result["Turn"]!.GetValue<int>());
        Assert.True(result["ActionObserved"]!.GetValue<bool>());
        Assert.Null(result["OutputFailure"]);
        Assert.StartsWith(Path.GetFullPath(Path.Combine(own, "tmp")) + Path.DirectorySeparatorChar,
            Path.GetFullPath(result["Root"]!.GetValue<string>()));
    }

    /// <summary>
    /// Runs only the unchanged common source42 preparation with optional scalar checkpoint observations.
    /// </summary>
    /// <param name="ignoredRoot">
    /// Dispatcher placeholder; the child creates a fresh fixture inside its private temporary root.
    /// </param>
    /// <param name="output">
    /// The owned result filename provided by the parent.
    /// </param>
    /// <param name="cut">
    /// Zero for this investigation; no crash boundary is armed or simulated.
    /// </param>
    /// <param name="own">
    /// The owned container holding explicit ordinary mode and source-probe metadata.
    /// </param>
    /// <returns>
    /// Completion after the actual source helper returns and its existing success conditions hold.
    /// </returns>
    public static async Task WriteJointInterfaceSourceProbeAsync(string ignoredRoot, string output, int cut, string own)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("T227 is a local Windows investigation.");
        _ = JointInterfaceCrashProcessSupport.ReadAndValidateEntryMode(own, cut, true, false);
        Assert.Equal(0, cut);
        var parent = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "parent.json")))!.AsObject();
        Assert.True(parent["Source42Probe"]?.GetValue<bool>() == true);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        var stages = JointInterfaceCrashStageReport.CreateForOrdinary(parent, cut)!;
        stages.Observe("fixture", "enter");
        using var fixture = new GameEngineTurnLifecycleTests(new JointInterfaceConsoleOutput());
        using var ownedFixture = new CleanupOwnedFixture(fixture._rootPath, line => fixture._directGachaOutput?.WriteLine(line));
        stages.Observe("fixture", "return");
        IDisposable? sourceInput = null;
        try
        {
            stages.Observe("engine", "enter");
            var source = await fixture.RunActualQteStorySourceAsync(physicalDeclaration: true, physicalMaximum: 2,
                retainLiveInput: input => sourceInput = input,
                configureBeforePreparation: fixture.ConfigureJointCompletionWorldBeforeOriginalAsync, sourceStages: stages);
            stages.Observe("engine", "return");
            Assert.Null(source.Failure);
            Assert.True(source.ActionObserved);
            Assert.NotNull(source.Source);
            Assert.Equal(42, source.Source.TurnNumber);
            var identity = JointInterfaceCrashProcessSupport.ReadCurrentIdentity();
            File.WriteAllText(output, JsonSerializer.Serialize(new
            {
                identity.Pid, identity.BirthUtc, BirthTick = identity.LinuxBirthTick, identity.BirthKind,
                identity.WindowsStartTimeUtcTicks, Nonce = parent["Nonce"]!.GetValue<string>(),
                Root = fixture._rootPath, Turn = source.Source.TurnNumber, source.ActionObserved, stages.OutputFailure
            }));
        }
        finally { sourceInput?.Dispose(); }
    }
}
