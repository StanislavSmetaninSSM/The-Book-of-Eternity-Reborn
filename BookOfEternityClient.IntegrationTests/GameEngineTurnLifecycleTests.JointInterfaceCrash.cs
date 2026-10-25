using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(0)] // Prepared committed; I not published
    [InlineData(1)] // I member physically applied; second cohort not committed
    [InlineData(2)] // Published committed; cleanup/ack/owning release did not return, later culture rejects
    [InlineData(3)] // actual FIRST2 graph installed; no result, missing-result restart must hold
    [InlineData(4)] // ErrorPrepared committed; restart must report frozen errors and hold
    public async Task GuaranteedTreatment_ActualProcessLossReentersThroughCurrentOwnerWithoutReprojection(int cut)
        => await RunJointInterfaceActualProcessLossAsync(cut);

    [Theory]
    [InlineData("Version")]
    [InlineData("RecordFingerprint")]
    [InlineData("OriginalManifest")]
    [InlineData("CurrentInterface")]
    public async Task GuaranteedTreatment_CurrentResumeRefusesPartialSelectedIntegrityWithoutNormalizerFallback(string fault)
        => await RunJointInterfaceActualProcessLossAsync(0, fault);

    /// <summary>
    /// Executes the original crash scenario through its permitted platform owner.
    /// </summary>
    /// <param name="cut">
    /// Original cut index0 through4.
    /// </param>
    /// <param name="fault">
    /// Existing Linux integrity fault, or null for ordinary execution.
    /// </param>
    /// <param name="diagnostic">
    /// Whether to use the existing Linux diagnostic consumer.
    /// </param>
    /// <param name="extendedWindowsCrash">
    /// Whether this explicitly selected Windows case shares an 820 second observation window with at most 600 seconds per child wait.
    /// </param>
    /// <param name="seedArtifactPath">
    /// The exact existing source42 artifact for a supported ordinary Windows fixture selection;
    /// <see langword="null"/> preserves the original source-turn preparation path.
    /// </param>
    /// <param name="fixtureAcquisitionOnly">
    /// Whether the distinct seeded Windows cut0 probe exports actual prepared/applied DATA
    /// and expects diagnostic exit74 rather than asserting gameplay completion.
    /// </param>
    /// <param name="seedUsage">
    /// The explicit five-cut usage, or <see langword="null"/> for the unchanged legacy cut0-only opt-in.
    /// </param>
    /// <param name="seedPackageRoot">
    /// The explicit closed five-cut package directory, or <see langword="null"/> for the original raw/default branches.
    /// Hydration remains inside the actual child-owning crash container until confirmed retirement.
    /// </param>
    /// <param name="ownedContainerParent">
    /// An explicit existing ordinary parent for the ordinary Windows five-cut package selection only.
    /// <see langword="null"/> preserves temporary or managed-capture placement; no global environment is changed.
    /// </param>
    /// <param name="observationMode">
    /// Full detailed timing by default. Off is permitted only for an ordinary extended seeded Windows selection
    /// and preserves scalar observations, gameplay assertions, process ownership and all existing budgets.
    /// </param>
    /// <param name="admissionDirectoryReuse">
    /// Production reuse by default; false selects only the explicitly validated ordinary seeded Windows cut0 control.
    /// </param>
    /// <param name="controlGameStateDataReuse">
    /// Production source DATA reuse by default; false selects the restricted ordinary Windows cut0 control.
    /// </param>
    /// <param name="producerReadForwarders">
    /// Existing read observation by default; <see langword="false"/> is restricted to the ordinary seeded Windows cut-zero producer.
    /// </param>
    /// <param name="passiveRecoveryTimings">
    /// Whether the restricted ordinary resumer records its first recovery through a sealed accumulator.
    /// </param>
    /// <returns>
    /// Completion after the selected probe's process and DATA assertions, or the original game assertions.
    /// </returns>
    /// <param name="ordinaryReadPathReuse">
    /// Production ordinary preparation directory DATA, or the strictly restricted fresh comparison.
    /// </param>
    /// <param name="ordinaryReadAdmissionReuse">
    /// Production initial-admission directory DATA sharing, or its restricted independent-context comparison.
    /// </param>
    /// <param name="sourceValidationBatchReuse">
    /// Production bounded source-directory DATA, or its strictly restricted fresh comparison.
    /// </param>
    private async Task RunJointInterfaceActualProcessLossAsync(int cut, string? fault = null, bool diagnostic = false,
        bool extendedWindowsCrash = false, string? seedArtifactPath = null, bool fixtureAcquisitionOnly = false,
        string? seedUsage = null, string? seedPackageRoot = null, string? ownedContainerParent = null,
        JointInterfaceObservationMode observationMode = JointInterfaceObservationMode.Full, bool admissionDirectoryReuse = true, bool controlGameStateDataReuse = true,
        bool producerReadForwarders = true, bool passiveRecoveryTimings = false, bool ordinaryReadPathReuse = true, bool ordinaryReadAdmissionReuse = true, bool sourceValidationBatchReuse = true)
    {
        if (extendedWindowsCrash && !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Extended crash qualification is a local Windows selection.");
        var mode = new JointInterfaceCrashMode(cut, diagnostic, fault);
        _ = JointInterfaceCrashProcessSupport.SelectBackend(OperatingSystem.IsWindows(), OperatingSystem.IsLinux(), mode);
        if (fixtureAcquisitionOnly && (!extendedWindowsCrash || seedArtifactPath is null))
            throw new InvalidDataException("Fixture acquisition requires an explicit seeded Windows selection.");
        if (seedPackageRoot is not null)
        {
            if (!extendedWindowsCrash || seedArtifactPath is not null || fixtureAcquisitionOnly ||
                seedUsage != JointInterfaceSeedCrashInput.OrdinaryWindowsFiveCutsUsage)
                throw new InvalidDataException("A five-cut package requires its exclusive ordinary Windows fixture selection.");
            JointInterfaceSeedCrashInput.ValidateMode(OperatingSystem.IsWindows(), mode, seedUsage, fixtureAcquisitionOnly);
        }
        if (seedUsage is not null && seedArtifactPath is null && seedPackageRoot is null)
            throw new InvalidDataException("Explicit source42 fixture usage requires an artifact selection.");
        JointInterfaceCrashObservation.Validate(observationMode, OperatingSystem.IsWindows(), extendedWindowsCrash,
            seedArtifactPath is not null || seedPackageRoot is not null, fixtureAcquisitionOnly, mode);
        JointAdmissionDirectoryControl.Validate(admissionDirectoryReuse, new JsonObject
        {
            ["ObservationMode"] = observationMode.ToString().ToLowerInvariant(),
            ["ExtendedWindowsCrash"] = extendedWindowsCrash
        }, OperatingSystem.IsWindows(), seedArtifactPath is not null || seedPackageRoot is not null, fixtureAcquisitionOnly, mode);
        JointControlGameStateDataControl.Validate(controlGameStateDataReuse, new JsonObject
        {
            ["ObservationMode"] = observationMode.ToString().ToLowerInvariant(),
            ["ExtendedWindowsCrash"] = extendedWindowsCrash
        }, OperatingSystem.IsWindows(), seedArtifactPath is not null || seedPackageRoot is not null, fixtureAcquisitionOnly, mode);
        JointOrdinaryReadPathControl.Validate(ordinaryReadPathReuse, new JsonObject
        {
            ["ObservationMode"] = observationMode.ToString().ToLowerInvariant(),
            ["ExtendedWindowsCrash"] = extendedWindowsCrash
        }, OperatingSystem.IsWindows(), seedArtifactPath is not null || seedPackageRoot is not null, fixtureAcquisitionOnly, mode);
        JointReadAdmissionControl.Validate(ordinaryReadAdmissionReuse, new JsonObject
        {
            ["ObservationMode"] = observationMode.ToString().ToLowerInvariant(),
            ["ExtendedWindowsCrash"] = extendedWindowsCrash
        }, OperatingSystem.IsWindows(), seedArtifactPath is not null || seedPackageRoot is not null, fixtureAcquisitionOnly, mode);
        JointSourceValidationBatchControl.Validate(sourceValidationBatchReuse, new JsonObject
        {
            ["ObservationMode"] = observationMode.ToString().ToLowerInvariant(),
            ["ExtendedWindowsCrash"] = extendedWindowsCrash
        }, OperatingSystem.IsWindows(), seedArtifactPath is not null || seedPackageRoot is not null, fixtureAcquisitionOnly, mode);
        JointProducerReadForwarderControl.Validate(producerReadForwarders, new JsonObject
        {
            ["ObservationMode"] = observationMode.ToString().ToLowerInvariant(),
            ["ExtendedWindowsCrash"] = extendedWindowsCrash
        }, OperatingSystem.IsWindows(), seedArtifactPath is not null || seedPackageRoot is not null, fixtureAcquisitionOnly, mode);
        JointPassiveTimingControl.Validate(passiveRecoveryTimings, new JsonObject
        {
            ["ObservationMode"] = observationMode.ToString().ToLowerInvariant(), ["ExtendedWindowsCrash"] = extendedWindowsCrash,
            ["AdmissionDirectoryReuse"] = admissionDirectoryReuse, ["ControlGameStateDataReuse"] = controlGameStateDataReuse,
            ["ProducerReadForwarders"] = producerReadForwarders
        }, OperatingSystem.IsWindows(), seedArtifactPath is not null || seedPackageRoot is not null, fixtureAcquisitionOnly, mode);
        JointInterfaceSeedCrashInput? source42Seed = null;
        if (seedArtifactPath is not null)
        {
            if (!extendedWindowsCrash) throw new InvalidDataException("Seeded cut0 requires the existing shared Windows crash budget.");
            source42Seed = JointInterfaceSeedCrashInput.Bind(seedArtifactPath, TestRepoPaths.RepoRoot,
                OperatingSystem.IsWindows(), mode, seedUsage, fixtureAcquisitionOnly);
        }
        var nonce = Guid.NewGuid().ToString("N");
        var parentMetadata = JsonSerializer.SerializeToNode(new
        {
            Nonce = nonce, ParentPid = Environment.ProcessId, Cut = cut, Fault = fault, Diagnostic = diagnostic,
            ObservationMode = observationMode.ToString().ToLowerInvariant(), ExtendedWindowsCrash = extendedWindowsCrash,
            AdmissionDirectoryReuse = admissionDirectoryReuse, ControlGameStateDataReuse = controlGameStateDataReuse,
            ProducerReadForwarders = producerReadForwarders, PassiveRecoveryTimings = passiveRecoveryTimings,
            OrdinaryReadPathReuse = ordinaryReadPathReuse,
            OrdinaryReadAdmissionReuse = ordinaryReadAdmissionReuse,
            SourceValidationBatchReuse = sourceValidationBatchReuse
        })!.AsObject();
        if (!diagnostic && fault == null) parentMetadata["RunId"] = Guid.NewGuid().ToString("N");
        if (source42Seed is not null) parentMetadata["Source42Seed"] = JsonSerializer.SerializeToNode(source42Seed);
        if (fixtureAcquisitionOnly) parentMetadata["FixtureAcquisitionOnly"] = true;
        Assert.Equal(fixtureAcquisitionOnly, JointInterfaceFixtureAcquisition.ReadAndValidate(parentMetadata, OperatingSystem.IsWindows()));
        var failures = new JointChildFailureLedger();
        using var classOwned = new JointInterfaceCrashOwnedContainer(_rootPath, line => _directGachaOutput?.WriteLine(line), failures: failures);
        try
        {
        var capture = JointInterfaceCrashProcessSupport.UsesManagedCapture(mode, () => JointInterfaceManagedCapture.Enabled);
        var uncapturedRoot = !capture || ownedContainerParent is not null
            ? JointInterfaceCrashStorage.SelectOwnedRoot(Path.GetTempPath(), ownedContainerParent,
                Guid.NewGuid().ToString("N"), new(OperatingSystem.IsWindows(), extendedWindowsCrash,
                    seedPackageRoot is not null, seedArtifactPath is not null, fixtureAcquisitionOnly, capture, mode))
            : null;
        var own = capture ? JointInterfaceManagedCapture.ClaimOwnedDirectory(cut, fault)
            : uncapturedRoot!;
        var storagePlacement = new { ExplicitParent = ownedContainerParent, SelectedParent = Path.GetDirectoryName(own), OwnedRoot = own };
        parentMetadata["StoragePlacement"] = JsonSerializer.SerializeToNode(storagePlacement);
        Directory.CreateDirectory(Path.Combine(own, "tmp"));
        using var owned = new JointInterfaceCrashOwnedContainer(own, line => _directGachaOutput?.WriteLine(line), capture, failures);
        try
        {
        _directGachaOutput?.WriteLine("JOINT_CRASH_STORAGE " + JsonSerializer.Serialize(storagePlacement));
        if (seedPackageRoot is not null)
        {
            var hydratedArtifact = Path.Combine(own, "source42-artifact");
            JointInterfaceSeedPackage.Hydrate(seedPackageRoot, hydratedArtifact);
            source42Seed = JointInterfaceSeedCrashInput.Bind(hydratedArtifact, TestRepoPaths.RepoRoot,
                OperatingSystem.IsWindows(), mode, seedUsage, fixtureAcquisitionOnly);
            parentMetadata["Source42Seed"] = JsonSerializer.SerializeToNode(source42Seed);
            Assert.Equal(source42Seed, JointInterfaceSeedCrashInput.Read(parentMetadata, OperatingSystem.IsWindows(), mode));
        }
        File.WriteAllText(Path.Combine(own, "parent.json"), parentMetadata.ToJsonString());
        var cutPath = Path.Combine(own, "cut.json");
        var sharedWindowsCrashStarted = extendedWindowsCrash ? Stopwatch.GetTimestamp() : (long?)null;
        var original = await RunJointInterfaceNativeChildAsync("engine-joint-interface-original-crash", own, cutPath, cut, own, owned,
            extendedWindowsCrash: extendedWindowsCrash, sharedWindowsCrashStarted: sharedWindowsCrashStarted);
        WritePhysicalInjuryDiagnostic("iobs_actual_abrupt_original_exit", original);
        Assert.Equal(73, original.ExitCode); // Environment.Exit, not an exception/catch/compensation surrogate
        Assert.True(original.Eof); Assert.True(original.Joined);
        var cutBytes = File.ReadAllBytes(cutPath);
        WritePhysicalInjuryDiagnostic("iobs_actual_abrupt_cut_raw_before_identity", new
        { Bytes = cutBytes.Length, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(cutBytes)),
            Base64 = Convert.ToBase64String(cutBytes) });
        var atCut = ParseJsonObjectBytes(cutBytes);
        Assert.Equal(original.Pid, atCut["Pid"]!.GetValue<int>()); Assert.Equal(nonce, atCut["Nonce"]!.GetValue<string>());
        AssertJointInterfaceChildIdentity(original, atCut);
        var root = atCut["Root"]!.GetValue<string>();
        Assert.StartsWith(Path.GetFullPath(Path.Combine(own, "tmp")) + Path.DirectorySeparatorChar, Path.GetFullPath(root));
        Assert.True(Directory.Exists(root)); // abrupt original did not run managed fixture disposal
        if (source42Seed is not null)
        {
            var imported = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "source-seed-import.json")))!.AsObject();
            Assert.Equal(original.Pid, imported["Pid"]!.GetValue<int>());
            Assert.Equal(nonce, imported["Nonce"]!.GetValue<string>());
            Assert.Equal(root, imported["Root"]!.GetValue<string>());
            Assert.Equal(source42Seed.ManifestSha256, imported["ManifestSha256"]!.GetValue<string>());
            Assert.Equal(42, imported["Metadata"]!["TurnNumber"]!.GetValue<int>());
            WritePhysicalInjuryDiagnostic("joint_source_seed_import_at_actual_cut", imported);
        }
        Assert.Equal(0, atCut["CommonCallbacks"]!.GetValue<int>()); Assert.Equal(42, atCut["Turn"]!.GetValue<int>());
        Assert.NotNull(atCut["JournalBase64"]); Assert.Equal(cut, atCut["Cut"]!.GetValue<int>());
        var output = Path.Combine(own, "resume.json");
        var resumed = await RunJointInterfaceNativeChildAsync("engine-joint-interface-resume", root, output, cut, own, owned,
            observeLive: diagnostic, deliveryNonce: nonce, extendedWindowsCrash: extendedWindowsCrash, sharedWindowsCrashStarted: sharedWindowsCrashStarted);
        var raw = File.Exists(output) ? File.ReadAllText(output) : null;
        WritePhysicalInjuryDiagnostic("iobs_actual_current_resume_exit", new
        { Original = original, Cut = atCut, Resume = resumed, ResultRaw = raw,
            OwnedContainer = own, ActualContainedRoots = Directory.EnumerateDirectories(Path.Combine(own, "tmp")).ToArray() });
        if (fixtureAcquisitionOnly)
        {
            Assert.Equal(JointInterfaceFixtureAcquisition.CapturedExitCode, resumed.ExitCode);
            Assert.NotEqual(original.Pid, resumed.Pid); Assert.True(resumed.Eof); Assert.True(resumed.Joined);
            var captured = JsonNode.Parse(Assert.IsType<string>(raw))!.AsObject();
            Assert.Equal(resumed.Pid, captured["Pid"]!.GetValue<int>());
            AssertJointInterfaceChildIdentity(resumed, captured);
            Assert.Equal(nonce, captured["Nonce"]!.GetValue<string>()); Assert.Equal(root, captured["Root"]!.GetValue<string>());
            Assert.True(captured["FixtureAcquisitionOnly"]!.GetValue<bool>());
            Assert.True(captured["ActualCallerEntered"]!.GetValue<bool>());
            Assert.False(captured["Completed"]!.GetValue<bool>());
            Assert.Equal(SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization.ToString(), captured["Checkpoint"]!.GetValue<string>());
            var tuple = JointInterfaceFixtureAcquisition.Verify(captured["Exports"]!.AsArray(),
                Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-cleanup-captures"), root, resumed.Pid, parentMetadata);
            Assert.True(JsonNode.DeepEquals(tuple, captured["Original"]));
            Assert.Equal(atCut["Result"]!["resultFingerprint"]!.GetValue<string>(), captured["PublishedResultFingerprint"]!.GetValue<string>());
            return;
        }
        Assert.Equal(0, resumed.ExitCode); Assert.NotEqual(original.Pid, resumed.Pid);
        Assert.True(resumed.Eof); Assert.True(resumed.Joined);
        var result = JsonNode.Parse(Assert.IsType<string>(raw))!.AsObject();
        if (diagnostic) Assert.Null(result["DiagnosticOutputFailure"]);
        Assert.Equal(resumed.Pid, result["Pid"]!.GetValue<int>()); Assert.Equal(nonce, result["Nonce"]!.GetValue<string>());
        AssertJointInterfaceChildIdentity(resumed, result);
        Assert.True(result["ActualCallerEntered"]!.GetValue<bool>()); Assert.Null(result["Failure"]);
        Assert.Equal(cut < 3 && fault is null, result["Completed"]!.GetValue<bool>());
        Assert.Equal(cut >= 3 || fault is not null, result["Held"]!.GetValue<bool>());
        Assert.Equal(fault is null && cut < 2 ? 1 : 0, result["NewChangingICommits"]!.GetValue<int>());
        Assert.True(result["InertRepeatBytesEqual"]!.GetValue<bool>());
        Assert.True(result["FreshAuthorityFieldsAbsentBeforeEntry"]!.GetValue<bool>());
        if (cut == 2)
        {
            Assert.True(atCut["AmbientParseAccepted"]!.GetValue<bool>());
            Assert.False(result["AmbientParseAccepted"]!.GetValue<bool>());
            Assert.True(result["IndependentOrdinaryPolicyRejected"]!.GetValue<bool>());
            Assert.Equal("ParseAccepted", result["Result"]!["timestampCheck"]!["timestamp"]!.GetValue<string>());
        }
        }
        catch (Exception error) { failures.Record(error, "actual-process-loss"); throw; }
        }
        catch (Exception error) { failures.Record(error, "actual-container"); throw; }
    }

    /// <summary>
    /// Records one original child and its independent identity and observed outcome.
    /// </summary>
    /// <param name="Pid">
    /// Original child PID.
    /// </param>
    /// <param name="BirthUtc">
    /// Independent UTC creation time.
    /// </param>
    /// <param name="BirthTick">
    /// Linux stat field22, or null on Windows.
    /// </param>
    /// <param name="ExitCode">
    /// Root exit code.
    /// </param>
    /// <param name="Stdout">
    /// The complete or explicitly incomplete external stdout artifact.
    /// </param>
    /// <param name="Stderr">
    /// The complete or explicitly incomplete external stderr artifact.
    /// </param>
    /// <param name="Executable">
    /// Absolute runtime path.
    /// </param>
    /// <param name="Arguments">
    /// Literal child arguments.
    /// </param>
    /// <param name="Dependencies">
    /// Test dependency context.
    /// </param>
    /// <param name="Eof">
    /// Whether both streams reached EOF.
    /// </param>
    /// <param name="Joined">
    /// Whether original ownership retired.
    /// </param>
    /// <param name="BirthKind">
    /// Explicit platform birth token kind.
    /// </param>
    /// <param name="WindowsStartTimeUtcTicks">
    /// Windows UTC creation ticks, or null on Linux.
    /// </param>
    /// <param name="NativeEvidence">
    /// Native scoped retirement evidence, or null for direct consumers.
    /// </param>
    private sealed record JointInterfaceNativeOutcome(int Pid, DateTime BirthUtc, ulong? BirthTick, int ExitCode, JointChildOutputReference Stdout, JointChildOutputReference Stderr,
        string Executable, string[] Arguments, object Dependencies, bool Eof, bool Joined,
        string BirthKind, long? WindowsStartTimeUtcTicks, object? NativeEvidence = null);

    /// <summary>
    /// Compares independently read parent and child birth tokens using the explicit platform kind.
    /// </summary>
    /// <param name="parent">
    /// The original parent's live-process observation.
    /// </param>
    /// <param name="child">
    /// The separate child's own serialized result.
    /// </param>
    private static void AssertJointInterfaceChildIdentity(JointInterfaceNativeOutcome parent, JsonObject child)
    {
        if (parent.BirthKind == "windows-process-start-utc-ticks")
        {
            Assert.Equal(parent.BirthKind, child["BirthKind"]!.GetValue<string>());
            Assert.Equal(parent.WindowsStartTimeUtcTicks, child["WindowsStartTimeUtcTicks"]!.GetValue<long>());
            Assert.Null(child["BirthTick"]);
        }
        else
        {
            Assert.Equal("linux-proc-start-tick", parent.BirthKind);
            Assert.Equal(parent.BirthTick, child["BirthTick"]!.GetValue<ulong>());
        }
    }

    private static ulong ReadJointInterfaceLinuxBirthTick(int pid)
    {
        var stat = File.ReadAllText($"/proc/{pid}/stat");
        Assert.Equal(pid, int.Parse(stat.AsSpan(0, stat.IndexOf(' ')), CultureInfo.InvariantCulture));
        var close = stat.LastIndexOf(')'); // comm can contain spaces or parentheses
        Assert.True(close > 0 && close + 2 < stat.Length && stat[close + 1] == ' ');
        var fields = stat[(close + 2)..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.True(fields.Length > 19);
        var birth = ulong.Parse(fields[19], CultureInfo.InvariantCulture); // field22: kernel ticks since boot
        Assert.True(birth > 0);
        return birth;
    }

    /// <summary>
    /// Runs one child through NativeLineage or a permitted direct path.
    /// </summary>
    /// <param name="role">
    /// Original isolated fixture role.
    /// </param>
    /// <param name="root">
    /// Producer-owned directory or retained game fixture.
    /// </param>
    /// <param name="output">
    /// Owned result filename.
    /// </param>
    /// <param name="cut">
    /// Original publication cut.
    /// </param>
    /// <param name="own">
    /// Exclusive parent container.
    /// </param>
    /// <param name="ownership">
    /// Original-child and native-owner custody.
    /// </param>
    /// <param name="observeLive">
    /// Whether the existing diagnostic resumer observes live delivery.
    /// </param>
    /// <param name="deliveryNonce">
    /// Diagnostic identity nonce, or null without live delivery.
    /// </param>
    /// <param name="extendedSourceProbe">
    /// Requests the fixed extended Windows source-probe wait; the default preserves the ordinary wait.
    /// </param>
    /// <param name="extendedWindowsCrash">
    /// Requests the extended Windows crash policy; cannot be combined with the source extension.
    /// </param>
    /// <param name="sharedWindowsCrashStarted">
    /// The common monotonic producer-start timestamp for an opted-in Windows crash case;
    /// <see langword="null"/> preserves the existing fixed role wait without pooling.
    /// </param>
    /// <returns>
    /// The independently identified complete child outcome.
    /// </returns>
    private async Task<JointInterfaceNativeOutcome> RunJointInterfaceNativeChildAsync(string role, string root, string output, int cut, string own,
        JointInterfaceCrashOwnedContainer ownership, bool observeLive = false, string? deliveryNonce = null,
        bool extendedSourceProbe = false, bool extendedWindowsCrash = false, long? sharedWindowsCrashStarted = null)
    {
        var paths = MortalArchiveDependencyPaths();
        var windows = OperatingSystem.IsWindows();
        var childTimeout = JointInterfaceCrashProcessSupport.GetDirectChildTimeout(windows, role, extendedSourceProbe,
            extendedWindowsCrash);
        if (sharedWindowsCrashStarted is { } sharedStart)
            childTimeout = JointInterfaceCrashProcessSupport.GetSharedWindowsCrashTimeout(windows, role,
                extendedSourceProbe, extendedWindowsCrash, Stopwatch.GetElapsedTime(sharedStart));
        var mode = JointInterfaceCrashProcessSupport.ReadAndValidateEntryMode(own, cut, windows, OperatingSystem.IsLinux());
        var backend = JointInterfaceCrashProcessSupport.SelectBackend(windows, OperatingSystem.IsLinux(), mode);
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT")
            ?? throw new InvalidDataException("DOTNET_ROOT is required for CRASH5.");
        string[] arguments = ["exec", "--runtimeconfig", paths["runtimeconfig"], "--depsfile", paths["depsfile"], paths["support"],
            role, paths["integration"], root, output, cut.ToString(CultureInfo.InvariantCulture), own];
        var start = JointInterfaceCrashProcessSupport.CreateChildStartInfo(dotnetRoot, arguments, own, windows);
        var executable = start.FileName;
        var streams = new JointChildOutputCapture(own, new
        {
            Role = role, Root = root, Cut = cut, ParentPid = Environment.ProcessId,
            Parent = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "parent.json"))),
            Dependencies = MortalArchiveDependencyContext()
        });
        ownership.TrackCapture(streams);
        if (backend == JointInterfaceCrashBackend.NativeLinux)
        {
            var native = await JointInterfaceCrashProcessSupport.RunNativeAsync(start, streams,
                ownership.RetainNative, ownership.JoinNativeAsync,
                evidence => WritePhysicalInjuryDiagnostic("iobs_actual_native_lineage_join", evidence));
            return new(native.Identity.Pid, native.Identity.BirthUtc, native.Identity.LinuxBirthTick, native.ExitCode,
                native.Stdout, native.Stderr, executable, arguments, MortalArchiveDependencyContext(), true, true,
                native.Identity.BirthKind, native.Identity.WindowsStartTimeUtcTicks, native.Evidence);
        }
        using var child = await JointInterfaceCrashProcessSupport.StartPreparedDirectChildAsync(streams, () =>
        {
            if (sharedWindowsCrashStarted is { } launchStarted)
                childTimeout = JointInterfaceCrashProcessSupport.GetSharedWindowsCrashTimeout(windows, role,
                    extendedSourceProbe, extendedWindowsCrash, Stopwatch.GetElapsedTime(launchStarted));
            return Process.Start(start) ?? throw new InvalidOperationException("Actual joint engine child was not started.");
        });
        ownership.Started(child.Id);
        var directJoined = false;
        DateTime birth = default;
        ulong birthTick = 0;
        JointInterfaceCrashIdentity? identity = null;
        Task<FinitePipeDrainResult>? stdout = null, stderr = null;
        JointInterfaceDiagnosticDelivery? delivery = null;
        JointInterfaceDiagnosticDelivery.Health? deliveryHealth = null;
        string? deliveryObserverFailure = null;
        Task<JointInterfaceManagedCapture.Outcome>? captureTask = null;
        JointInterfaceManagedCapture.Outcome? captureOutcome = null;
        using var captureStop = new CancellationTokenSource();
        Exception? failure = null;
        object? sharedWindowsBudget = null;
        try
        {
            identity = JointInterfaceCrashProcessSupport.ReadLiveIdentity(child.Id, windows ? child : null);
            birth = identity.BirthUtc;
            birthTick = identity.LinuxBirthTick ?? 0; // Existing Linux diagnostic consumers only use this local field.
            streams.BindIdentity(identity);
            if (observeLive && role == "engine-joint-interface-resume")
            {
                try { delivery = new(new(child.Id, birthTick, root, deliveryNonce ?? "", cut)); }
                catch (Exception error) when (FinitePipeOutput.Recoverable(error)) { deliveryObserverFailure = error.ToString(); }
            }
            stdout = delivery != null ? delivery.DrainToAsync(child.StandardOutput, streams.StandardOutput)
                : FinitePipeOutput.DrainAsync(child.StandardOutput, streams.StandardOutput);
            stderr = FinitePipeOutput.DrainAsync(child.StandardError, streams.StandardError);
            if (sharedWindowsCrashStarted is { } budgetStarted)
            {
                var elapsed = Stopwatch.GetElapsedTime(budgetStarted);
                sharedWindowsBudget = new { TotalSeconds = 820, ChildCapSeconds = 600,
                    ElapsedAtWaitSeconds = elapsed.TotalSeconds, ChildWaitSeconds = (double?)null };
                childTimeout = JointInterfaceCrashProcessSupport.GetSharedWindowsCrashTimeout(windows, role,
                    extendedSourceProbe, extendedWindowsCrash, elapsed);
                sharedWindowsBudget = new { TotalSeconds = 820, ChildCapSeconds = 600,
                    ElapsedAtWaitSeconds = elapsed.TotalSeconds, ChildWaitSeconds = (double?)childTimeout.TotalSeconds };
            }
            var deadline = Stopwatch.GetTimestamp() + (long)(childTimeout.TotalSeconds * Stopwatch.Frequency);
            var nativeWait = child.WaitForExitAsync().WaitAsync(childTimeout);
            if (observeLive && role == "engine-joint-interface-resume" && delivery is not null && JointInterfaceManagedCapture.Enabled)
                captureTask = JointInterfaceManagedCapture.ObserveAsync(child, birthTick, root, deliveryNonce ?? "", cut, own,
                    executable, arguments, deadline, delivery, captureStop.Token);
            await nativeWait;
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            captureStop.Cancel();
            Exception? cleanupFailure = null;
            try
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true); // only this original owned child tree
                await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                stdout ??= FinitePipeOutput.DrainAsync(child.StandardOutput, streams.StandardOutput);
                stderr ??= FinitePipeOutput.DrainAsync(child.StandardError, streams.StandardError);
                await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(10));
                foreach (var drain in new[] { stdout.Result, stderr.Result })
                    if (!drain.Eof || drain.ReadFailure != null)
                        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(drain.ReadFailure ??
                            new IOException("Original child pipe EOF was not observed.")).Throw();
                ownership.Joined(child.Id); // only positive terminal+join+EOF can permit container deletion
                directJoined = true;
            }
            catch (Exception error) { cleanupFailure = error; if (failure is not null) failure.Data["JointInterfaceChildCleanupFailure"] = error; }
            try { deliveryHealth = delivery?.ReadHealth(); }
            catch (Exception observerError) when (observerError is not OutOfMemoryException and not StackOverflowException)
            { deliveryObserverFailure ??= observerError.GetType().Name; }
            if (captureTask is not null)
            {
                try { captureOutcome = await captureTask.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception observerError) when (observerError is not OutOfMemoryException and not StackOverflowException)
                { deliveryObserverFailure ??= "Capture: " + observerError.GetType().Name; }
            }
            await streams.FinishAsync(stdout?.IsCompletedSuccessfully == true ? stdout.Result : null,
                stderr?.IsCompletedSuccessfully == true ? stderr.Result : null);
            if (stdout != null && stderr != null && (!stdout.IsCompleted || !stderr.IsCompleted))
                streams.ObserveDeferredCompletion(stdout, stderr);
            var completion = JointInterfaceCrashProcessSupport.ObserveDirectCompletion(child.HasExited, directJoined,
                stdout?.IsCompletedSuccessfully == true ? stdout.Result : null,
                stderr?.IsCompletedSuccessfully == true ? stderr.Result : null);
            streams.RecordOutcome(new { ExitCode = child.HasExited ? (int?)child.ExitCode : null,
                completion.Exited, completion.Eof, completion.Joined, PrimaryFailure = failure?.ToString(), CleanupFailure = cleanupFailure?.ToString() });
            var outputFailure = streams.PreserveFailure(failure ?? cleanupFailure);
            if (failure == null && outputFailure != null) cleanupFailure = outputFailure;
            try
            {
            WritePhysicalInjuryDiagnostic("iobs_actual_native_join", new
            { child.Id, BirthUtc = birth, BirthTick = identity?.LinuxBirthTick, BirthKind = identity?.BirthKind,
                WindowsStartTimeUtcTicks = identity?.WindowsStartTimeUtcTicks,
                ExitCode = child.HasExited ? (int?)child.ExitCode : null, completion.Eof,
                completion.Joined, Failure = failure?.ToString(), Executable = executable, Arguments = arguments,
                Dependencies = MortalArchiveDependencyContext(), SharedWindowsBudget = sharedWindowsBudget, Stdout = streams.OutputReference,
                Stderr = streams.ErrorReference, CleanupFailure = cleanupFailure?.ToString(),
                SecondaryFailures = failure?.Data.Cast<System.Collections.DictionaryEntry>().Select(entry => new
                    { Stage = entry.Key.ToString(), Error = entry.Value?.ToString() }).ToArray(),
                RetainedOwnedContainer = ownership.HasUnsettledChildren ? own : null });
            }
            catch (Exception error) when (FinitePipeOutput.Recoverable(error))
            {
                streams.RecordFailure("direct-report", error);
                var combined = streams.PreserveFailure(failure ?? cleanupFailure);
                if (failure == null) cleanupFailure = combined;
            }
            if (observeLive && role == "engine-joint-interface-resume")
            {
                try { WritePhysicalInjuryDiagnostic("iobs_parent_live_delivery_health", new
                    { child.Id, BirthTick = birthTick, Root = root, Nonce = deliveryNonce, Cut = cut,
                        Health = deliveryHealth, ConstructionFailure = deliveryObserverFailure, Capture = captureOutcome,
                        SourceEmissionCompleteness = "UNKNOWN", CapturePhase = "UNKNOWN" }); }
                catch (Exception observerError) when (observerError is not OutOfMemoryException and not StackOverflowException)
                {
                    // A missing observer report is UNKNOWN to the future observer, never native failure.
                    deliveryObserverFailure ??= observerError.GetType().Name;
                }
            }
            if (cleanupFailure is not null && failure is null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
        return new(child.Id, birth, identity!.LinuxBirthTick, child.ExitCode, streams.OutputReference, streams.ErrorReference, executable, arguments,
            MortalArchiveDependencyContext(), stdout!.IsCompletedSuccessfully && stdout.Result.Eof && stderr!.IsCompletedSuccessfully && stderr.Result.Eof, directJoined,
            identity.BirthKind, identity.WindowsStartTimeUtcTicks);
    }

    /// <summary>
    /// Prevents fixture deletion while original children or native owners remain unsettled.
    /// </summary>
    /// <param name="root">
    /// Exclusive test container.
    /// </param>
    /// <param name="output">
    /// Writes ownership and cleanup reports.
    /// </param>
    /// <param name="retainCapture">
    /// Whether the existing diagnostic path retains its directory.
    /// </param>
    /// <param name="failures">
    /// Shared original-failure ledger; null creates an independent cleanup ledger.
    /// </param>
    private sealed class JointInterfaceCrashOwnedContainer(string root, Action<string> output, bool retainCapture = false,
        JointChildFailureLedger? failures = null) : IDisposable
    {
        private readonly JointChildFailureLedger _failures = failures ?? new();
        private readonly List<JointChildOutputCapture> _captures = [];
        private readonly HashSet<int> _unsettled = [];
        private readonly Dictionary<NativeLineageOwner, JointInterfaceCrashRetainedOwner> _nativeOwners = [];
        /// <summary>
        /// Reports direct children or native original owners whose retirement remains unconfirmed.
        /// </summary>
        internal bool HasUnsettledChildren => _unsettled.Count != 0 || _nativeOwners.Count != 0;

        /// <summary>
        /// Retains original artifact sessions separately from native ownership.
        /// </summary>
        /// <param name="capture">
        /// The prepared external artifact capture, including unresolved destinations.
        /// </param>
        internal void TrackCapture(JointChildOutputCapture capture) => _captures.Add(capture);

        /// <summary>
        /// Retains a native owner once, even before it has an admitted root PID.
        /// </summary>
        /// <param name="owner">
        /// The original owner received before exec or from the partial-launch exception.
        /// </param>
        internal void RetainNative(NativeLineageOwner owner)
        {
            if (!_nativeOwners.ContainsKey(owner)) _nativeOwners.Add(owner, new(owner));
        }

        /// <summary>
        /// Releases original custody only when that owner confirms successful disposal.
        /// </summary>
        /// <param name="owner">
        /// The exact retained owner with already observed scoped retirement and EOF.
        /// </param>
        /// <returns>
        /// Completion of disposal and removal from this test's unsettled owners.
        /// </returns>
        internal async Task JoinNativeAsync(NativeLineageOwner owner)
        {
            await _nativeOwners[owner].DisposeConfirmedAsync();
            _nativeOwners.Remove(owner);
        }
        internal void Started(int pid) => Assert.True(_unsettled.Add(pid));
        internal void Joined(int pid) => Assert.True(_unsettled.Remove(pid));
        /// <summary>
        /// Removes this fixture only after original ownership settles; uncertain owners retain their handles and directories.
        /// </summary>
        public void Dispose()
        {
            _failures.Cleanup(() =>
            {
            if (HasUnsettledChildren)
            {
                output(JsonSerializer.Serialize(new { CleanupOwnedRoot = root, OwnedFixtureRemoved = false,
                    OwnedFixtureRetainedForUnresolvedChild = true, UnsettledOwnedPids = _unsettled.Order().ToArray(),
                    RetainedNativeOwners = _nativeOwners.Keys.Select(owner => new
                        { owner.Identity.RunId, RootPid = owner.AdmittedHostProcessId, owner.BootstrapDirectory }).ToArray(),
                    CleanupFailure = "Native exit/join/EOF could not all be positively established; do not delete under an unconfirmed child." }));
                return;
            }
            if (retainCapture || _captures.Any(capture => capture.EvidenceRetentionRequired))
            {
                output(JsonSerializer.Serialize(new { CleanupOwnedRoot = root, OwnedFixtureRemoved = false,
                    EvidenceRetentionRequired = true, NativeOwnershipUnsettled = false,
                    CaptureDirectories = _captures.Select(capture => capture.DirectoryPath).ToArray(),
                    PrivateCaptureRetainedThroughIndependentEvidenceAndFinalReport = retainCapture }));
                return;
            }
            new CleanupOwnedFixture(root, output).Dispose();
            });
        }
    }

    /// <summary>
    /// Validates mode and drives the producer to the original abrupt exit boundary.
    /// </summary>
    /// <param name="ignoredRoot">
    /// Dispatcher placeholder; the producer creates or independently imports its own fixture.
    /// </param>
    /// <param name="output">
    /// Owned cut record filename.
    /// </param>
    /// <param name="cut">
    /// Requested cut0 through4.
    /// </param>
    /// <param name="own">
    /// Owned directory containing explicit parent mode metadata.
    /// </param>
    /// <returns>
    /// Preparation until abrupt exit; a missing cut faults instead of simulating a crash.
    /// </returns>
    public static async Task WriteJointInterfaceOriginalCrashAsync(string ignoredRoot, string output, int cut, string own)
    {
        var mode = JointInterfaceCrashProcessSupport.ReadAndValidateEntryMode(own, cut, OperatingSystem.IsWindows(), OperatingSystem.IsLinux());
        Assert.InRange(cut, 0, 4);
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        var parent = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "parent.json")))!.AsObject();
        var fixtureAcquisitionOnly = JointInterfaceFixtureAcquisition.ReadAndValidate(parent, OperatingSystem.IsWindows());
        var source42Seed = JointInterfaceSeedCrashInput.Read(parent, OperatingSystem.IsWindows(), mode);
        var observationMode = JointInterfaceCrashObservation.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var admissionDirectoryReuse = JointAdmissionDirectoryControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var controlGameStateDataReuse = JointControlGameStateDataControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var ordinaryReadPathReuse = JointOrdinaryReadPathControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var ordinaryReadAdmissionReuse = JointReadAdmissionControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var sourceValidationBatchReuse = JointSourceValidationBatchControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var producerReadForwarders = JointProducerReadForwarderControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        _ = JointPassiveTimingControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Boundary = "joint_observation_mode", Role = "producer", Pid = Environment.ProcessId,
            Nonce = parent["Nonce"]!.GetValue<string>(), Cut = cut,
            ObservationMode = observationMode.ToString().ToLowerInvariant()
        }));
        var stages = JointInterfaceCrashStageReport.CreateForOrdinary(parent, cut);
        var operationTimings = OperatingSystem.IsWindows() && stages != null &&
            observationMode == JointInterfaceObservationMode.Full ? new JointOperationTimings() : null;
        Action<string, string, TimeSpan>? timingObserver = operationTimings is null ? null : operationTimings.Observe;
        operationTimings?.Observe("Producer.ImportFixture", "begin", TimeSpan.Zero);
        var fixtureStarted = Stopwatch.GetTimestamp();
        stages?.Observe("fixture", "enter");
        JointInterfaceCrashSeedMetadata? sourceSeedMetadata = null;
        var readObservers = new LifecycleFixtureReadObservers(producerReadForwarders);
        GameEngineTurnLifecycleTests fixture;
        if (source42Seed is null)
            fixture = new GameEngineTurnLifecycleTests(new JointInterfaceConsoleOutput(), null, timingObserver, readObservers);
        else
        {
            var importStarted = Stopwatch.GetTimestamp();
            var template = source42Seed.Open(TestRepoPaths.RepoRoot);
            var root = Path.Combine(own, "tmp", "boe-joint-seeded-" + Guid.NewGuid().ToString("N"));
            sourceSeedMetadata = await template.MaterializeAsync(root);
            fixture = new GameEngineTurnLifecycleTests(new JointInterfaceConsoleOutput(), root, timingObserver, readObservers);
            File.WriteAllText(Path.Combine(own, "source-seed-import.json"), JsonSerializer.Serialize(new
            {
                Pid = Environment.ProcessId, Nonce = parent["Nonce"]!.GetValue<string>(), Root = root,
                source42Seed.ManifestSha256, template.ContractFingerprint, template.FileCount, template.PayloadBytes,
                Metadata = sourceSeedMetadata, ImportSeconds = Stopwatch.GetElapsedTime(importStarted).TotalSeconds
            }));
        }
        fixture._fs.AdmissionDirectoryReuseEnabled = admissionDirectoryReuse;
        fixture._fs.ControlGameStateDataReuseEnabled = controlGameStateDataReuse;
        fixture._fs.OrdinaryReadPathReuseEnabled = ordinaryReadPathReuse;
        fixture._fs.OrdinaryReadAdmissionReuseEnabled = ordinaryReadAdmissionReuse;
        fixture._fs.SourceValidationBatchReuseEnabled = sourceValidationBatchReuse;
        using var ownedFixture = fixture;
        stages?.Observe("fixture", "return");
        operationTimings?.Observe("Producer.ImportFixture", "end", Stopwatch.GetElapsedTime(fixtureStarted));
        var scenario = cut == 2 ? "Crash:PublishedCulture" : cut == 4 ? "Crash:Error" : "Crash:Changed";
        using var probe = new JointInterfaceLifecycleProbe(fixture, scenario);
        probe.ConfigureActualCrash(cut, output, own, stages);
        stages?.Observe("engine", "enter");
        using var producerEngine = fixture._fs.MeasureOperation("Producer.EngineUntilActualCut");
        await fixture.RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe, sourceSeed: sourceSeedMetadata);
        stages?.Observe("engine", "return");
        // An unarmed/missing cut must fail after actual old/new work, never write
        // a synthetic cut file or claim that an exception simulated process loss.
        throw new InvalidOperationException("Actual requested joint publication crash boundary did not execute.");
    }

    /// <summary>
    /// Validates mode before opening a fixture and resumes through the current game owner.
    /// </summary>
    /// <param name="root">
    /// Retained producer fixture.
    /// </param>
    /// <param name="output">
    /// Owned resumer result filename.
    /// </param>
    /// <param name="cut">
    /// Original cut index.
    /// </param>
    /// <param name="own">
    /// Owned directory with parent and cut metadata.
    /// </param>
    /// <returns>
    /// Completion after original current-owner, mechanical and repeat assertions, unless the explicit
    /// fixture-only selection exits74 after exporting actual accepted-checkpoint DATA.
    /// </returns>
    public static async Task WriteJointInterfaceCurrentResumeAsync(string root, string output, int cut, string own)
    {
        var mode = JointInterfaceCrashProcessSupport.ReadAndValidateEntryMode(own, cut, OperatingSystem.IsWindows(), OperatingSystem.IsLinux());
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        var parent = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "parent.json")))!.AsObject();
        var fixtureAcquisitionOnly = JointInterfaceFixtureAcquisition.ReadAndValidate(parent, OperatingSystem.IsWindows());
        var source42Seed = JointInterfaceSeedCrashInput.Read(parent, OperatingSystem.IsWindows(), mode);
        var observationMode = JointInterfaceCrashObservation.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var admissionDirectoryReuse = JointAdmissionDirectoryControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var controlGameStateDataReuse = JointControlGameStateDataControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var ordinaryReadPathReuse = JointOrdinaryReadPathControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var ordinaryReadAdmissionReuse = JointReadAdmissionControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var sourceValidationBatchReuse = JointSourceValidationBatchControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        _ = JointProducerReadForwarderControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var passiveRecoveryTimings = JointPassiveTimingControl.Read(parent, OperatingSystem.IsWindows(),
            source42Seed is not null, fixtureAcquisitionOnly, mode);
        var passiveTimings = passiveRecoveryTimings ? new OperationTimingAccumulator() : null;
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            Boundary = "joint_observation_mode", Role = "resumer", Pid = Environment.ProcessId,
            Nonce = parent["Nonce"]!.GetValue<string>(), Cut = cut,
            ObservationMode = observationMode.ToString().ToLowerInvariant()
        }));
        var fixtureExports = new JsonArray();
        var stages = JointInterfaceCrashStageReport.CreateForOrdinary(parent, cut);
        var resumeActivity = OperatingSystem.IsWindows() && stages != null ? new JointInterfaceResumeActivity(stages) : null;
        var resumeReadBindings = resumeActivity?.CreateReadBindings(observationMode == JointInterfaceObservationMode.Off);
        var operationTimings = OperatingSystem.IsWindows() && stages != null &&
            observationMode == JointInterfaceObservationMode.Full ? new JointOperationTimings() : null;
        var atCut = JsonNode.Parse(File.ReadAllText(Path.Combine(own, "cut.json")))!.AsObject();
        var fault = parent["Fault"]?.GetValue<string>();
        Assert.Equal(root, atCut["Root"]!.GetValue<string>()); Assert.Equal(cut, atCut["Cut"]!.GetValue<int>());
        Assert.NotEqual(atCut["Pid"]!.GetValue<int>(), Environment.ProcessId);
        var diagnostic = new JointInterfaceResumeDiagnostic(parent["Diagnostic"]?.GetValue<bool>() == true,
            cut, root, parent["Nonce"]!.GetValue<string>());
        diagnostic.Observe("factory-enter");
        stages?.Observe("fixture", "enter");
        using var factory = new GameEngineTurnLifecycleTests(new JointInterfaceConsoleOutput());
        stages?.Observe("fixture", "return");
        diagnostic.Observe("factory-return");
        using var auxiliaryOwned = new CleanupOwnedFixture(factory._rootPath, Console.WriteLine);
        var policyRejected = false;
        if (cut == 2)
        {
            var contrast = CreateGameEngineRuntime(factory._fs, new QueuedConsoleInputSource([]), InertRealmSettings);
            await using var contrastAudio = contrast.AudioService;
            var raw = new JsonObject { ["dialogueOptions"] = new JsonArray("Продолжить разговор."), ["timestamp"] = "13/02/2026" };
            await factory._fs.WriteFileAtomicAsync(MortalJointInterfacePublication.InterfacePath, raw.ToJsonString());
            var issues = await contrast.ValidationService.ValidateAcceptedTurnInterfacePayloadAsync();
            policyRejected = issues.Any(issue => issue.Code == "interface_updates_invalid_timestamp" && issue.Severity == IssueSeverity.Error);
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_independent_ordinary_policy_contrast", Culture = CultureInfo.CurrentCulture.Name,
                Raw = raw, Issues = issues, SeparateOwnedRoot = factory._rootPath }));
            Assert.True(policyRejected);
        }
        FileSystemManager files = null!;
        var changingI = 0; var actualCaller = false;
        var cleanupDataCaptured = 0;
        MortalJointInterfacePublication.Record? completedProof = null;
        var publications = new List<object>(); var recovery = new List<object>();
        diagnostic.Observe("file-system-enter");
        stages?.Observe("file-system", "enter");
        files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
        {
            PassiveOperationTimings = passiveTimings,
            OperationTimingObserver = operationTimings is null ? null : (name, boundary, elapsed) =>
            {
                operationTimings.Observe(name, boundary, elapsed);
                if (name != "JointStory.CleanupEligibleTerminalAsync" || boundary != "begin" ||
                    Interlocked.CompareExchange(ref cleanupDataCaptured, 1, 0) != 0) return;
                var captureDirectory = Path.Combine(Path.GetDirectoryName(JointChildOutputCapture.ResolveDefaultArtifactParent())!,
                    "joint-cleanup-captures");
                var archive = Path.Combine(captureDirectory, Guid.NewGuid().ToString("N") + ".zip");
                operationTimings.Observe("JointCleanup.DiagnosticDataCapture", "begin", TimeSpan.Zero);
                var captureClock = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    Directory.CreateDirectory(captureDirectory);
                    JointCleanupDataCapture.Capture(root, archive, new JsonObject
                    {
                        ["Operation"] = name, ["Cut"] = cut, ["Parent"] = parent.DeepClone()
                    });
                    Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_cleanup_data_capture", Complete = true,
                        Archive = archive, Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(archive))),
                        Milliseconds = captureClock.Elapsed.TotalMilliseconds }));
                }
                catch (Exception error)
                {
                    Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_cleanup_data_capture", Complete = false,
                        Archive = archive, Error = error.ToString(), Milliseconds = captureClock.Elapsed.TotalMilliseconds }));
                }
                finally { operationTimings.Observe("JointCleanup.DiagnosticDataCapture", "end", captureClock.Elapsed); }
            },
            RecoveryValidationObserver = metrics => Console.WriteLine(JsonSerializer.Serialize(new
            {
                Boundary = "recovery_validation_metrics", Metrics = metrics
            })),
            CanonicalReadActivityObserver = resumeReadBindings?.CanonicalObserver,
            ExactPhysicalReadActivityObserver = resumeReadBindings?.ExactObserver,
            PassiveReadActivities = resumeReadBindings?.Passive,
            AfterCanonicalWriteLockOpenedAsync = resumeActivity is null ? null : () => { resumeActivity.Observe(JointResumeObservation.CanonicalOpened); return Task.CompletedTask; },
            CanonicalWriteLockContendedAsync = resumeActivity is null ? null : () => { resumeActivity.Observe(JointResumeObservation.CanonicalContended); return Task.CompletedTask; },
            MainOwnerLockContendedAsync = resumeActivity is null ? null : () => { resumeActivity.Observe(JointResumeObservation.MainContended); return Task.CompletedTask; },
            LocalPublicationRecoveryObserver = (phase, index) =>
            {
                resumeActivity?.Publication(phase, true);
                recovery.Add(new { Phase = phase.ToString(), Index = index });
            },
            LocalPublicationObserver = (phase, index) =>
            {
                resumeActivity?.Publication(phase, false);
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
                foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray())
                {
                    if (member.GetProperty("Path").GetString() == files.ResolvePath(MortalJointInterfacePublication.InterfacePath) &&
                        !JointInterfaceLifecycleProbe.ImagesEqual(JointInterfaceLifecycleProbe.Image(member, "Before"), JointInterfaceLifecycleProbe.Image(member, "After"))) changingI++;
                    if (member.GetProperty("Path").GetString() == files.ResolvePath(MortalQteDecisionPublication.StatePath))
                        publications.Add(new { Marker = JointInterfaceLifecycleProbe.Image(member, "After"), Journal = journal.RootElement.Clone() });
                }
            }
        });
        files.AdmissionDirectoryReuseEnabled = admissionDirectoryReuse;
        files.ControlGameStateDataReuseEnabled = controlGameStateDataReuse;
        files.OrdinaryReadPathReuseEnabled = ordinaryReadPathReuse;
        files.OrdinaryReadAdmissionReuseEnabled = ordinaryReadAdmissionReuse;
        files.SourceValidationBatchReuseEnabled = sourceValidationBatchReuse;
        diagnostic.Observe("file-system-return");
        stages?.Observe("file-system", "return");
        var log = new SpiritualLifecycleTestLogger();
        ILogger<GameEngine> engineLogger = diagnostic.Enabled ? new JointInterfaceResumeDiagnosticLogger(log, diagnostic) : log;
        diagnostic.Observe("engine-enter");
        stages?.Observe("engine", "enter");
        var runtime = CreateGameEngineRuntime(files, new QueuedConsoleInputSource([]), settings => { InertRealmSettings(settings); settings.AutosaveIntervalTurns = 0; },
            logger: engineLogger, finalizationHooks: new GameEngineSessionFinalizationHooks
            { AtCheckpointAsync = async point =>
                {
                    diagnostic.Observe("finalization-enter", point.ToString());
                    if (resumeActivity != null) resumeActivity.Checkpoint(point, true);
                    else stages?.Observe("finalization-before", "enter");
                    if (point == SessionFinalizationCheckpoint.LateTerminalAndIdleOperationBound) actualCaller = true;
                    if (cut < 3 && fault is null && point == SessionFinalizationCheckpoint.AcceptedOutcomeValidatedBeforeMaterialization)
                    {
                        diagnostic.Observe("completed-proof-lease-enter");
                        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                        diagnostic.Observe("completed-proof-lease-return");
                        var actualOriginal = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
                        completedProof = await MortalQteDecisionPublication.ReadCurrentJointInterfaceDataAsync(files, lease,
                            actualOriginal.SessionId, actualOriginal.RequestId, actualOriginal.SnapshotToken, actualOriginal.TurnNumber);
                        Assert.NotNull(completedProof);
                        AssertActualJointInterfaceRegistrySettled(files);
                        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_actual_completed_data_before_terminal_cleanup",
                            Original = new { actualOriginal.SessionId, actualOriginal.RequestId, actualOriginal.SnapshotToken, actualOriginal.TurnNumber },
                            Record = completedProof.ToJson(), Interface = await files.ReadFileBytesAsync(lease, MortalJointInterfacePublication.InterfacePath),
                            Meaning = "Full existing current original/history/resource proof while actual original remains present; no post-cleanup authority recreation" }));
                        if (fixtureAcquisitionOnly)
                        {
                            Assert.True(actualCaller);
                            var tuple = JsonSerializer.SerializeToNode(new
                            { actualOriginal.SessionId, actualOriginal.RequestId, actualOriginal.SnapshotToken, actualOriginal.TurnNumber })!.AsObject();
                            using (files.MeasureOperation("FixtureData.ExportAppliedBeforeStory"))
                                fixtureExports.Add(JointInterfaceFixtureAcquisition.Capture(root,
                                    Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-cleanup-captures"), "AppliedBeforeStory", parent, tuple));
                            File.WriteAllText(output, JointInterfaceCrashProcessSupport.SerializeCurrentIdentity(new
                            {
                                Nonce = parent["Nonce"]!.GetValue<string>(), Root = root, FixtureAcquisitionOnly = true,
                                ActualCallerEntered = actualCaller, Completed = false, Checkpoint = point.ToString(), Original = tuple,
                                PublishedResultFingerprint = completedProof.ResultFingerprint, Exports = fixtureExports
                            }, stages));
                            Console.Out.Flush(); Console.Error.Flush();
                            Environment.Exit(JointInterfaceFixtureAcquisition.CapturedExitCode);
                        }
                    }
                    diagnostic.Observe("finalization-return", point.ToString());
                    if (resumeActivity != null) resumeActivity.Checkpoint(point, false);
                    else stages?.Observe("finalization-after", "return");
                } });
        var engine = runtime.Engine;
        diagnostic.Observe("engine-return");
        stages?.Observe("engine", "return");
        await using var audio = runtime.AudioService;
        var freshObservation = engine.CaptureMortalJointRuntimeObservation();
        var noFresh = !freshObservation.CheckpointPresent && !freshObservation.FreshCheckpointPresent &&
            !freshObservation.ReadyPresent;
        Assert.True(noFresh);
        MortalJointInterfacePublication.Record? result = null;
        Dictionary<string, byte[]?>? before = null, after = null, repeated = null;
        byte[]? recoveredPending = null, recoveredI = null;
        bool? handled = null, second = null;
        var integrityGuardEntries = new List<object>();
        const string integrityReason = "joint_interface_actual_publication_preimage_or_readback_changed";
        static int ReasonCount(string logs) => logs.Split(integrityReason, StringSplitOptions.None).Length - 1;
        void RequireCurrentInterfaceGuard(int entry, string logsBefore, string logsAfter)
        {
            if (fault != "CurrentInterface") return;
            var countBefore = ReasonCount(logsBefore); var countAfter = ReasonCount(logsAfter);
            var observed = new { Boundary = "iobs_actual_current_interface_guard_entry", Entry = entry,
                Reason = integrityReason, CountBefore = countBefore, CountAfter = countAfter,
                LogsBefore = logsBefore, LogsAfter = logsAfter };
            integrityGuardEntries.Add(observed);
            Console.WriteLine(JsonSerializer.Serialize(observed)); // retained even when the stronger oracle fails
            Assert.True(countAfter > countBefore, "Each real entry must newly report " + integrityReason);
        }
        Exception? failure = null;
        var completed = false;
        OperationTimingAccumulator.Window? passiveWindow = null;
        try
        {
            PendingTurnSnapshotReadAuthority original;
            diagnostic.Observe("preflight-lease-enter");
            stages?.Observe("preflight-lease", "enter");
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                diagnostic.Observe("preflight-lease-return");
                stages?.Observe("preflight-lease", "return");
                // This actual new ordinary lease recovers the original journal
                // BEFORE the original/result proof. No copied root or old Ready.
                diagnostic.Observe("snapshot-enter");
                stages?.Observe("snapshot-refresh", "enter");
                original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
                diagnostic.Observe("snapshot-return");
                stages?.Observe("snapshot-refresh", "return");
                Assert.Equal(43, original.TurnNumber);
                recoveredPending = await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
                var pending = ParseJsonObjectBytes(Assert.IsType<byte[]>(recoveredPending));
                var parsed = MortalJointRepairCheckpoint.Parse(JsonSerializer.SerializeToElement(pending["jointRepairCheckpoint"]));
                Assert.True(parsed.IsValid, DescribeValidationIssues(parsed.Issues)); Assert.True(parsed.Checkpoint!.UsesInterfacePublication);
                if (cut != 3)
                {
                    result = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(pending[MortalJointInterfacePublication.PendingField]));
                    result.RequireCheckpoint(parsed.Checkpoint.ToJsonObject()); result.RequireLegacyPending(pending);
                    var cutRecord = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(atCut["Result"]));
                    Assert.Equal(cutRecord.ResultFingerprint, result.ResultFingerprint);
                    if (cut == 4) Assert.True(JsonNode.DeepEquals(cutRecord.ToJson(), result.ToJson()));
                    else Assert.True(JsonNode.DeepEquals(cutRecord.AsPublished().ToJson(), result.AsPublished().ToJson()));
                    Assert.Equal(cut == 2 ? "Published" : "Prepared", result.State);
                    recoveredI = await files.ReadFileBytesAsync(lease, MortalJointInterfacePublication.InterfacePath);
                    Assert.Equal(cut == 2 ? result.AfterBytes : result.BeforeBytes, recoveredI);
                }
                else Assert.False(pending.ContainsKey(MortalJointInterfacePublication.PendingField));
                diagnostic.Observe("runtime-refresh-enter");
                stages?.Observe("snapshot-refresh", "enter");
                await runtime.StateManager.RefreshGameStateReadOnlyAsync(lease);
                diagnostic.Observe("runtime-refresh-return");
                stages?.Observe("snapshot-refresh", "return");
                Assert.Equal(42, runtime.StateManager.CurrentState.TurnNumber);
                runtime.GameLoop.SetSession(original.SessionId, 42);
                if (fault is not null)
                {
                    Assert.Equal(0, cut); Assert.NotNull(result); Assert.Equal("Prepared", result.State);
                    var damagedPending = ParseJsonObjectBytes(Assert.IsType<byte[]>(recoveredPending));
                    var faultPath = fault switch
                    {
                        "Version" or "RecordFingerprint" => WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                        "OriginalManifest" => LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                        "CurrentInterface" => MortalJointInterfacePublication.InterfacePath,
                        _ => throw new InvalidDataException("Unknown actual resume test fault: " + fault)
                    };
                    var beforeFault = await files.ReadFileBytesAsync(lease, faultPath); Assert.NotNull(beforeFault);
                    byte[] afterFault;
                    if (fault == "Version")
                    { damagedPending["jointRepairCheckpoint"]!["schemaVersion"] = 7; afterFault = JsonSerializer.SerializeToUtf8Bytes(damagedPending); }
                    else if (fault == "RecordFingerprint")
                    { damagedPending[MortalJointInterfacePublication.PendingField]!["fingerprint"] = new string('0', 64); afterFault = JsonSerializer.SerializeToUtf8Bytes(damagedPending); }
                    else afterFault = beforeFault.Concat(new byte[] { 32 }).ToArray();
                    File.WriteAllBytes(files.ResolvePath(faultPath), afterFault); // one partial physical fault, never reseal/reissue original/capability
                    Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_actual_current_resume_integrity_fault", fault,
                        Path = faultPath, Before = beforeFault, After = afterFault, GenuinePrepared = result.ToJson(),
                        Meaning = "Actual abrupt producer and complete baseline proof precede this fault; only new current engine may classify it" }));
                    if (faultPath == WoundAcceptedTurnSnapshotContract.PendingResolutionPath) recoveredPending = afterFault;
                }
                diagnostic.Observe("before-images-enter");
                before = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                diagnostic.Observe("before-images-return");
            }
            diagnostic.Observe("preflight-lease-release-return");
            var firstLogsBefore = log.Describe();
            passiveWindow = passiveTimings?.StartWindow();
            diagnostic.Observe("late-first-enter");
            stages?.Observe("late-first", "enter");
            using var namedResumerTimer1 = files.MeasureOperation("Resumer.FirstCurrentOwnerCall");
            if (fixtureAcquisitionOnly)
                using (files.MeasureOperation("FixtureData.ExportPreparedOriginal"))
                    fixtureExports.Add(JointInterfaceFixtureAcquisition.Capture(root,
                        Path.Combine(TestRepoPaths.RepoRoot, "TestResults", "joint-cleanup-captures"), "PreparedOriginal", parent));
            var sourceBatchWindowBefore = files.CaptureSourceValidationBatchStatistics();
            handled = await engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync();
            var sourceBatchWindowAfter = files.CaptureSourceValidationBatchStatistics();
            namedResumerTimer1?.Dispose();
            passiveWindow?.Dispose();
            diagnostic.Observe("late-first-return", handled.ToString());
            resumeActivity?.Complete();
            stages?.Observe("late-first", "return");
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "ordinary_read_path_counts", Role = "resumer", Stage = "first",
                Pid = Environment.ProcessId, Enabled = ordinaryReadPathReuse, Counts = files.CaptureOrdinaryReadPathStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "shared_read_admission_counts", Role = "resumer", Stage = "first",
                Pid = Environment.ProcessId, Enabled = ordinaryReadAdmissionReuse, Hits = files.CaptureSharedReadAdmissionHits() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "source_validation_batch_counts", Role = "resumer", Stage = "first",
                Pid = Environment.ProcessId, Enabled = files.SourceValidationBatchReuseEnabled, Counts = files.CaptureSourceValidationBatchStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "source_validation_batch_window_counts", Role = "resumer", Stage = "first",
                Pid = Environment.ProcessId, Enabled = files.SourceValidationBatchReuseEnabled, Before = sourceBatchWindowBefore,
                After = sourceBatchWindowAfter, Delta = sourceBatchWindowAfter - sourceBatchWindowBefore }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "admission_directory_counts", Role = "resumer", Stage = "first",
                Pid = Environment.ProcessId, Enabled = admissionDirectoryReuse, Counts = files.CaptureAdmissionDirectoryStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "control_game_state_counts", Role = "resumer", Stage = "first",
                Pid = Environment.ProcessId, Enabled = controlGameStateDataReuse, Counts = files.CaptureControlGameStateStatistics() }));
            var firstLogsAfter = log.Describe();
            RequireCurrentInterfaceGuard(1, firstLogsBefore, firstLogsAfter);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                diagnostic.Observe("after-images-enter");
                after = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                diagnostic.Observe("after-images-return");
                if (cut < 3 && fault is null)
                {
                    var current = completedProof; // physical proof frozen at the actual pre-cleanup checkpoint
                    Assert.NotNull(current); Assert.Equal("Published", current.State); Assert.False(current.HasErrors);
                    var cutRecord = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(atCut["Result"]));
                    Assert.Equal(cutRecord.ResultFingerprint, current.ResultFingerprint);
                    Assert.True(JsonNode.DeepEquals(cutRecord.AsPublished().ToJson(), current.ToJson()));
                    Assert.Equal(cutRecord.AfterBytes, await files.ReadFileBytesAsync(lease, MortalJointInterfacePublication.InterfacePath));
                    var image = ParseJsonObjectBytes(Assert.IsType<byte[]>(cutRecord.AfterBytes));
                    var option = Assert.IsType<JsonObject>(Assert.Single(image["dialogueOptions"]!.AsArray()));
                    Assert.Equal("[NI_CONTROL:second]Продолжить обычный ответ.", option["text"]!.GetValue<string>());
                    Assert.Equal("[NI_ACTION:first][NI_CONTROL:second]Продолжить обычный ответ.", option["inputValue"]!.GetValue<string>());
                    Assert.Equal(43, runtime.GameLoop.TurnNumber);
                    await AssertJointInterfaceResumedMechanicalOutcomeAsync(files, lease, before, current.Binding);
                    foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                        MortalWoundOpportunityAdapter.DecisionPath, "input/turn_request.json", "ready/turn_complete.json",
                        LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
                        Assert.Null(await files.ReadFileBytesAsync(lease, path));
                    result = current; completed = true;
                }
                else
                {
                    Assert.Equal(42, runtime.GameLoop.TurnNumber);
                    Assert.Equal(recoveredPending, await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
                    Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
                    foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
                    if (cut == 4 && result is not null)
                    {
                        Assert.True(result.HasErrors);
                        foreach (var issue in result.Issues) Assert.Contains(JsonSerializer.Serialize(issue), log.Describe());
                    }
                }
            }
            var secondLogsBefore = log.Describe();
            diagnostic.Observe("late-second-enter");
            stages?.Observe("late-repeat", "enter");
            using var namedResumerTimer2 = files.MeasureOperation("Resumer.SecondCurrentOwnerCall");
            second = await engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync();
            namedResumerTimer2?.Dispose();
            diagnostic.Observe("late-second-return", second.ToString());
            stages?.Observe("late-repeat", "return");
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "ordinary_read_path_counts", Role = "resumer", Stage = "repeat",
                Pid = Environment.ProcessId, Enabled = ordinaryReadPathReuse, Counts = files.CaptureOrdinaryReadPathStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "shared_read_admission_counts", Role = "resumer", Stage = "repeat",
                Pid = Environment.ProcessId, Enabled = ordinaryReadAdmissionReuse, Hits = files.CaptureSharedReadAdmissionHits() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "source_validation_batch_counts", Role = "resumer", Stage = "repeat",
                Pid = Environment.ProcessId, Enabled = files.SourceValidationBatchReuseEnabled, Counts = files.CaptureSourceValidationBatchStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "admission_directory_counts", Role = "resumer", Stage = "repeat",
                Pid = Environment.ProcessId, Enabled = admissionDirectoryReuse, Counts = files.CaptureAdmissionDirectoryStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "control_game_state_counts", Role = "resumer", Stage = "repeat",
                Pid = Environment.ProcessId, Enabled = controlGameStateDataReuse, Counts = files.CaptureControlGameStateStatistics() }));
            var secondLogsAfter = log.Describe();
            RequireCurrentInterfaceGuard(2, secondLogsBefore, secondLogsAfter);
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                diagnostic.Observe("repeat-images-enter");
                repeated = await CaptureConfirmedClaimReadImagesAsync(files, lease);
                diagnostic.Observe("repeat-images-return");
                await AssertConfirmedClaimReadImagesAsync(files, lease, after);
            }
            Assert.Equal(fault is null && cut < 2 ? 1 : 0, changingI); Assert.True(actualCaller);
            if (completed) Assert.False(second!.Value); else Assert.True(engine.CaptureMortalJointRuntimeObservation().PreparationHeld);
        }
        catch (Exception error) { failure = error; throw; }
        finally
        {
            passiveWindow?.Dispose();
            if (passiveWindow is not null)
                Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "recovery_named_aggregate", Role = "resumer",
                    Stage = "first", Pid = Environment.ProcessId, Limited = false, Snapshot = passiveWindow.Capture() }));
            diagnostic.Observe("resume-finally", failure?.GetType().FullName);
            resumeActivity?.CompletePendingReadTail();
            stages?.Observe(failure == null ? "exit" : "error", failure == null ? "return" : "error", failure?.GetType().FullName);
            var finalReport = JointInterfaceCrashProcessSupport.SerializeCurrentIdentity(new
            { Nonce = parent["Nonce"]!.GetValue<string>(),
                Root = root, AuxiliaryRoot = factory._rootPath, ActualCallerEntered = actualCaller, Handled = handled, Second = second,
                Fault = fault, Completed = completed, Held = !completed && failure is null, Failure = failure?.ToString(),
                NewChangingICommits = changingI, InertRepeatBytesEqual = repeated is not null && after is not null &&
                    repeated.Count == after.Count && repeated.All(row => after.TryGetValue(row.Key, out var bytes) && MortalJointInterfacePublication.BytesEqual(row.Value, bytes)),
                FreshAuthorityFieldsAbsentBeforeEntry = noFresh, AmbientParseAccepted = DateTimeOffset.TryParse("13/02/2026", out _),
                IndependentOrdinaryPolicyRejected = policyRejected, Culture = CultureInfo.CurrentCulture.Name,
                Result = result?.ToJson(), RecoveredPending = recoveredPending, RecoveredI = recoveredI,
                Before = before, After = after, Repeated = repeated, Publications = publications, Recovery = recovery, Logs = log.Describe(),
                IntegrityGuardEntries = integrityGuardEntries, DiagnosticOutputFailure = diagnostic.OutputFailure,
                Dependencies = MortalArchiveDependencyContext(), Loaded = MortalArchiveLoadedDependencyBindings() }, stages);
            var reportBytes = Encoding.UTF8.GetBytes(finalReport);
            File.WriteAllBytes(output, reportBytes);
            JointResumeDiagnosticReport.PublishConfigured(reportBytes,
                Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT"), reference =>
            {
                Console.WriteLine(JointInterfaceCrashProcessSupport.SerializeCurrentIdentity(new
                { Boundary = "joint_resume_report_reference", Cut = cut, Nonce = parent["Nonce"]!.GetValue<string>(),
                    Reference = reference, GrantsAuthority = false }, stages));
                Console.Out.Flush();
            }, failure);
        }
    }

    private static async Task AssertJointInterfaceResumedMechanicalOutcomeAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, IReadOnlyDictionary<string, byte[]?> before, JsonObject binding)
    {
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.DefinitionsPath), false);
        Assert.True(definitions.IsValid, DescribeValidationIssues(definitions.Issues));
        var state = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.StatePath), definitions.Catalog!, false);
        var history = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(lease, ResourceMaterializationContract.HistoryPath), definitions.Catalog!, false);
        Assert.True(state.IsValid, DescribeValidationIssues(state.Issues)); Assert.True(history.IsValid, DescribeValidationIssues(history.Issues));
        Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
        var spend = Assert.Single(history.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "energy");
        Assert.Equal(ResourceTransitionOperation.Spend, spend.Operation); Assert.Equal(ResourceMutationPhase.RegisteredSystemOutcome, spend.Phase);
        Assert.Equal(2m, spend.RequestedAmount); Assert.Equal(2m, spend.AppliedAmount); Assert.Equal(2m, spend.BeforeState!.Current); Assert.Equal(0m, spend.AfterState!.Current);
        Assert.Equal(0m, Assert.Single(state.Ledger!.Entries, row => row.Coordinate.ResourceKey == "energy").Current);
        var damage = Assert.Single(history.History.Transitions, row => row.Turn == 43 && row.Coordinate.ResourceKey == "health");
        Assert.Equal(ResourceTransitionOperation.Damage, damage.Operation); Assert.Equal(ResourceMutationPhase.EffectTrigger, damage.Phase);
        Assert.Equal(3m, damage.AppliedAmount); Assert.Equal(5m, damage.BeforeState!.Current); Assert.Equal(2m, damage.AfterState!.Current);
        Assert.Equal(2m, Assert.Single(state.Ledger.Entries, row => row.Coordinate.ResourceKey == "health").Current);
        Assert.Equal(2, history.History.Transitions.Count(row => row.Turn == 43));
        var prior = WoundHistoryState.Parse(MortalArchiveText(before[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        var current = WoundHistoryState.Parse(await files.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
        Assert.True(prior.IsValid, DescribeValidationIssues(prior.Issues)); Assert.True(current.IsValid, DescribeValidationIssues(current.Issues));
        foreach (var row in prior.State!.Transitions)
            Assert.Equal(JsonSerializer.Serialize(row, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                JsonSerializer.Serialize(Assert.Single(current.State!.Transitions, now => now.Ordinal == row.Ordinal), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var suffix = current.State!.Transitions.Skip(prior.State.Transitions.Count).ToArray();
        Assert.Equal(2, suffix.Length); Assert.Equal("create", suffix[0].Kind); Assert.Equal("treat", suffix[1].Kind);
        Assert.Equal(prior.State.NextOrdinal, suffix[0].Ordinal); Assert.Equal(prior.State.NextOrdinal + 1, suffix[1].Ordinal);
        Assert.All(suffix, row => Assert.Equal(43, row.Turn));
        var treatment = Assert.IsType<MortalWoundTreatmentPersistedResult>(suffix[1].TreatmentResult);
        Assert.Equal(binding["requestFingerprint"]!.GetValue<string>(), treatment.Request.RequestFingerprint);
        Assert.Equal(suffix[1].WoundId, treatment.Request.Coordinates.WoundId);
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_actual_resumed_once_mechanics", spend, damage,
            OriginalWoundPrefix = prior.State.Transitions, ExactSuffix = suffix, RequestFingerprint = treatment.Request.RequestFingerprint,
            Meaning = "Independent 2→0 charge, 5→2 damage, exactly two target-turn resource rows and create/treat suffix; full current proof was frozen before cleanup" }));
    }

    /// <summary>
    /// Requires the actual existing current publisher state to have released its reservation and open receipt.
    /// </summary>
    /// <param name="files">
    /// The real filesystem whose current physical completion proof was already checked by the caller.
    /// </param>
    private static void AssertActualJointInterfaceRegistrySettled(FileSystemManager files)
    {
        var observation = AcceptedTurnAuthorityRegistry.ObserveExistingJointPublication(files.CanonicalRootAuthorityIdentity);
        Assert.True(observation.RootSlotPresent);
        Assert.True(observation.AuthorityStatePresent);
        Assert.False(observation.TreatmentReservationPresent);
        Assert.False(observation.OpenJointRepairReceiptPresent);
        Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "iobs_actual_current_common_claim_settled",
            Root = files.BasePath, observation.RootSlotPresent, observation.AuthorityStatePresent,
            ReservationAbsent = !observation.TreatmentReservationPresent, OpenTakeAbsent = !observation.OpenJointRepairReceiptPresent,
            Meaning = "Actual completed physical proof precedes this read of existing owner state, not a fabricated receipt or old-process claim" }));
    }

    private sealed partial class JointInterfaceLifecycleProbe
    {
        private int? _actualCrashCut;
        private string? _crashOutput, _crashOwn;
        private JointInterfaceCrashStageReport? _crashStages;

        /// <summary>
        /// Arms one original publication cut with its existing owned result path.
        /// </summary>
        /// <param name="cut">
        /// The original cut index from zero through four.
        /// </param>
        /// <param name="output">
        /// The owned cut result filename.
        /// </param>
        /// <param name="own">
        /// The parent metadata directory.
        /// </param>
        /// <param name="stages">
        /// Ordinary scalar stages, or <see langword="null"/> for the unchanged diagnostic and integrity paths.
        /// </param>
        internal void ConfigureActualCrash(int cut, string output, string own, JointInterfaceCrashStageReport? stages)
        { Assert.Null(_actualCrashCut); _actualCrashCut = cut; _crashOutput = output; _crashOwn = own; _crashStages = stages; }

        /// <summary>
        /// Records the actual cut and exits the producer without managed compensation.
        /// </summary>
        /// <param name="phase">
        /// Observed trusted publication phase.
        /// </param>
        /// <param name="index">
        /// Member index at that boundary.
        /// </param>
        private void ObserveActualCrashCut(TrustedLocalPublicationPhase phase, int index)
        {
            if (_actualCrashCut is not { } cut || (cut != 3 && !_configured) ||
                phase is not (TrustedLocalPublicationPhase.Committed or TrustedLocalPublicationPhase.MemberPublished)) return;
            var journalBytes = File.ReadAllBytes(Path.Combine(fixture._fs.RuntimeRootPath, "trusted-local-publication-v1", "active.json"));
            using var journal = CleanupPublicationCut.Metadata(journalBytes);
            var members = journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
            var member = members.FirstOrDefault(row => row.GetProperty("Path").GetString() == fixture._fs.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath));
            if (member.ValueKind == JsonValueKind.Undefined || Image(member, "After") is not { } pendingBytes) return;
            var pending = ParseJsonObjectBytes(pendingBytes);
            if (pending["jointRepairCheckpoint"] is not JsonObject checkpoint || checkpoint["schemaVersion"]?.GetValue<int>() != 2) return;
            var record = pending[MortalJointInterfacePublication.PendingField] is { } value
                ? MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(value)) : null;
            var reached = cut switch
            {
                0 => phase == TrustedLocalPublicationPhase.Committed && record is { State: "Prepared", HasErrors: false },
                1 => phase == TrustedLocalPublicationPhase.MemberPublished && record is { State: "Published", HasErrors: false } &&
                    members[index].GetProperty("Path").GetString() == fixture._fs.ResolvePath(InterfacePath),
                2 => phase == TrustedLocalPublicationPhase.Committed && record is { State: "Published", HasErrors: false },
                3 => phase == TrustedLocalPublicationPhase.Committed && record is null,
                4 => phase == TrustedLocalPublicationPhase.Committed && record is { State: "Prepared", HasErrors: true },
                _ => false
            };
            if (!reached) return;
            var parent = JsonNode.Parse(File.ReadAllText(Path.Combine(_crashOwn!, "parent.json")))!;
            Assert.Equal(0, _captureCallbacks); Assert.Null(_firstMarker);
            if (cut == 1) Assert.False(journal.RootElement.GetProperty("Committed").GetBoolean());
            else Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
            foreach (var (path, bytes) in _mechanicalBefore) Assert.Equal(bytes, ReadPhysical(path));
            using (fixture._fs.MeasureOperation("Producer.AuthenticatedCrashCut"))
            {
                _crashStages?.Observe("exit", "enter");
                File.WriteAllText(_crashOutput!, JointInterfaceCrashProcessSupport.SerializeCurrentIdentity(new
                { Nonce = parent["Nonce"]!.GetValue<string>(),
                    ParentPid = parent["ParentPid"]!.GetValue<int>(), Root = fixture._fs.BasePath, Cut = cut,
                    Phase = phase.ToString(), Index = index, JournalBase64 = Convert.ToBase64String(journalBytes),
                    ActualPending = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath), PendingMemberAfter = pendingBytes,
                    Interface = ReadPhysical(InterfacePath), InterfaceMtime = Mtime(), Result = record?.ToJson(), Checkpoint = checkpoint,
                    CommonCallbacks = _captureCallbacks, FirstMarker = _firstMarker, Turn = _runtime!.GameLoop.TurnNumber,
                    MechanicalBefore = _mechanicalBefore, MechanicalAtCut = _mechanicalBefore.Keys.ToDictionary(path => path, ReadPhysical),
                    AmbientParseAccepted = DateTimeOffset.TryParse("13/02/2026", out _), Culture = CultureInfo.CurrentCulture.Name,
                    Dependencies = MortalArchiveDependencyContext(), Loaded = MortalArchiveLoadedDependencyBindings() }, _crashStages));
            }
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "ordinary_read_path_counts", Role = "producer", Stage = "cut",
                Pid = Environment.ProcessId, Enabled = fixture._fs.OrdinaryReadPathReuseEnabled, Counts = fixture._fs.CaptureOrdinaryReadPathStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "shared_read_admission_counts", Role = "producer", Stage = "cut",
                Pid = Environment.ProcessId, Enabled = fixture._fs.OrdinaryReadAdmissionReuseEnabled, Hits = fixture._fs.CaptureSharedReadAdmissionHits() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "source_validation_batch_counts", Role = "producer", Stage = "cut",
                Pid = Environment.ProcessId, Enabled = fixture._fs.SourceValidationBatchReuseEnabled, Counts = fixture._fs.CaptureSourceValidationBatchStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "admission_directory_counts", Role = "producer", Stage = "cut",
                Pid = Environment.ProcessId, Enabled = fixture._fs.AdmissionDirectoryReuseEnabled,
                Counts = fixture._fs.CaptureAdmissionDirectoryStatistics() }));
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "control_game_state_counts", Role = "producer", Stage = "cut",
                Pid = Environment.ProcessId, Enabled = fixture._fs.ControlGameStateDataReuseEnabled,
                Counts = fixture._fs.CaptureControlGameStateStatistics() }));
            var sources = fixture._readObservers.CaptureSources();
            Console.WriteLine(JsonSerializer.Serialize(new { Boundary = "producer_read_forwarder_counts", Role = "producer", Stage = "cut",
                Pid = Environment.ProcessId, Enabled = fixture._readObservers.ForwardingEnabled,
                Sources = sources, Total = sources.Values.Sum() }));
            Console.Out.Flush(); Console.Error.Flush();
            Environment.Exit(73); // actual abrupt process exit; no catch, finally, release or compensation
        }
    }

    private sealed class JointInterfaceConsoleOutput : ITestOutputHelper
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public void WriteLine(string format, params object[] args) => Console.WriteLine(format, args);
    }
}
