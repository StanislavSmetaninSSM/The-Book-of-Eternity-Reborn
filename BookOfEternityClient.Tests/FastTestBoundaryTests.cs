using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FastTestBoundaryTests
{
    private const string ProductionDirectory = "BookOfEternityClient";
    private const string ProductionProjectFile = "BookOfEternityClient.csproj";
    private const string FastTestsDirectory = "BookOfEternityClient.Tests";
    private const string FastTestsProjectFile = "BookOfEternityClient.Tests.csproj";
    private const string IntegrationTestsDirectory = "BookOfEternityClient.IntegrationTests";
    private const string IntegrationTestsProjectFile = "BookOfEternityClient.IntegrationTests.csproj";
    private const string TestSupportDirectory = "BookOfEternityClient.TestSupport";
    private const string TestSupportProjectFile = "BookOfEternityClient.TestSupport.csproj";

    private static readonly string[] ReviewedHeavySourcePaths =
    [
        "MortalItemConsumptionPlannerTests.Session.cs",
        "ShiningAbodeTradeAndForgeStateTests.Session.cs",
        "GmWorkerAuditLogTests.Session.cs",
        "AfterlifeSpiritualConflictValidationTests.cs",
        "GameEngineTurnLifecycleTests.cs",
        "GuardianSystemRegressionTests.cs",
        "FileSystemManagerTests.cs",
        "ConsoleE2ESmokeTests.cs",
        "LocalWebUiBuiltFrontendSmokeTests.cs",
        "MortalWoundRecoveryTests.cs",
        "MortalWoundTreatmentCapabilityAuthorityTests.cs",
        "MortalWoundTreatmentAcceptedStateRegistryTests.cs",
        "MortalWoundTreatmentResolverTests.ColdClaimRecovery.cs",
        "MortalWoundTreatmentResolverTests.CourseContinuation.cs",
        "MortalWoundTreatmentResolverTests.CoursePublication.cs",
        "MortalWoundTreatmentResolverTests.ReplayFixture.cs",
        "MortalWoundTreatmentResolverTests.cs",
        "MortalWoundTreatmentResolverTests.DetachedRequirementAuthority.cs",
        "MortalWoundTreatmentResolverTests.DetachedSealCoordinates.cs",
        "MortalWoundTreatmentResolverTests.DetachedSourceValidation.cs",
        "MortalWoundTreatmentResolverTests.DeteriorationPreparation.cs",
        "MortalWoundTreatmentResolverTests.DeteriorationPublication.cs",
        "MortalWoundTreatmentResolverTests.DeteriorationPolicyAuthority.cs",
        "MortalWoundTreatmentResolverTests.FreshAuthority.cs",
        "MortalWoundTreatmentResolverTests.HistoryPersistence.cs",
        "MortalWoundTreatmentResolverTests.Legacy.cs",
        "MortalWoundTreatmentResolverTests.OutcomeIntents.cs",
        "MortalWoundTreatmentResolverTests.PersistedRepairWave.cs",
        "MortalWoundTreatmentResolverTests.Persistence.cs",
        "MortalWoundTreatmentResolverTests.PersistenceHardening.cs",
        "MortalWoundTreatmentResolverTests.PersistenceIngress.cs",
        "MortalWoundTreatmentResolverTests.PrerequisiteAuthorities.cs",
        "MortalWoundTreatmentResolverTests.ProcedureAuthority.ContractRegression.cs",
        "MortalWoundTreatmentResolverTests.ProcedureAuthority.cs",
        "MortalWoundTreatmentResolverTests.ProcedureAuthority.ReviewRegression.cs",
        "MortalWoundTreatmentResolverTests.ProcedurePublication.cs",
        "MortalWoundTreatmentResolverTests.ComplicationGraphApplicability.cs",
        "MortalWoundTreatmentResolverTests.ComplicationAdditionPublication.cs",
        "MortalWoundTreatmentResolverTests.RecoveryPublication.cs",
        "MortalWoundTreatmentResolverTests.ComplicationRemovalPublication.cs",
        "MortalWoundTreatmentResolverTests.Replay.cs",
        "MortalWoundTreatmentResolverTests.ResourceAuthority.cs",
        "MortalWoundTreatmentResolverTests.ResourceFinalization.cs",
        "MortalWoundTreatmentResolverTests.ResourcePublication.cs",
        "MortalWoundTreatmentResolverTests.SeverityReductionPlanner.cs",
        "MortalWoundTreatmentResolverTests.VehicleTopology.cs",
        "QteSceneServiceTests.cs",
        "GmWorkerLiveSmokeTests.cs",
        "LocalWebUiSmokeTests.cs",
        "ShiningCoreActionResolutionValidationTests.cs",
        "GuardianCorrectionServiceTests.cs",
        "ShiningBlessingEffectStateTests.cs",
        "AfterlifeNotificationStateTests.cs",
        "DarenQteShowcaseTests.cs",
        "ShiningTradeRequestStateTests.cs",
        "BrowserLocalWriteCoordinatorTests.cs",
        "TrainingServiceTests.cs",
        "NpcTradeServiceRequestFlowTests.cs",
        "MortalWoundOpportunityAdapterTests.cs",
        "ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs",
        "GmWorkerValidationRepairDelegatorTests.cs",
        "GmWorkerProposalOnlyDispatchTests.cs",
        Path.Combine("WebUi", "BrowserMortalWorldGenerationFencingTests.cs"),
        Path.Combine("WebUi", "BrowserPlayerActionGenerationTests.cs"),
        Path.Combine("WebUi", "BrowserStorageTransportParityTests.cs"),
        Path.Combine("WebUi", "BrowserShiningRelicForgeParityTests.cs"),
        Path.Combine("WebUi", "BrowserResidentInteractionsParityTests.cs"),
        Path.Combine("WebUi", "BrowserAfterlifeArchiveParityTests.cs"),
        Path.Combine("WebUi", "BrowserShiningIncarnationGatesParityTests.cs"),
        Path.Combine("WebUi", "BrowserInkFeatherFateParityTests.cs"),
        Path.Combine("WebUi", "BrowserNpcSocialParityTests.cs"),
        Path.Combine("WebUi", "BrowserTradeParityTests.cs")
    ];

    private static readonly string[] MortalWoundTreatmentResolverPartialSourcePaths =
    [
        "MortalWoundTreatmentResolverTests.ColdClaimRecovery.cs",
        "MortalWoundTreatmentResolverTests.CourseContinuation.cs",
        "MortalWoundTreatmentResolverTests.CoursePublication.cs",
        "MortalWoundTreatmentResolverTests.ReplayFixture.cs",
        "MortalWoundTreatmentResolverTests.cs",
        "MortalWoundTreatmentResolverTests.DetachedRequirementAuthority.cs",
        "MortalWoundTreatmentResolverTests.DetachedSealCoordinates.cs",
        "MortalWoundTreatmentResolverTests.DetachedSourceValidation.cs",
        "MortalWoundTreatmentResolverTests.DeteriorationPreparation.cs",
        "MortalWoundTreatmentResolverTests.DeteriorationPolicyAuthority.cs",
        "MortalWoundTreatmentResolverTests.DeteriorationPublication.cs",
        "MortalWoundTreatmentResolverTests.FreshAuthority.cs",
        "MortalWoundTreatmentResolverTests.HistoryPersistence.cs",
        "MortalWoundTreatmentResolverTests.Legacy.cs",
        "MortalWoundTreatmentResolverTests.OutcomeIntents.cs",
        "MortalWoundTreatmentResolverTests.PersistedRepairWave.cs",
        "MortalWoundTreatmentResolverTests.Persistence.cs",
        "MortalWoundTreatmentResolverTests.PersistenceHardening.cs",
        "MortalWoundTreatmentResolverTests.PersistenceIngress.cs",
        "MortalWoundTreatmentResolverTests.PrerequisiteAuthorities.cs",
        "MortalWoundTreatmentResolverTests.ProcedureAuthority.ContractRegression.cs",
        "MortalWoundTreatmentResolverTests.ProcedureAuthority.cs",
        "MortalWoundTreatmentResolverTests.ProcedureAuthority.ReviewRegression.cs",
        "MortalWoundTreatmentResolverTests.ProcedurePublication.cs",
        "MortalWoundTreatmentResolverTests.ComplicationGraphApplicability.cs",
        "MortalWoundTreatmentResolverTests.ComplicationAdditionPublication.cs",
        "MortalWoundTreatmentResolverTests.RecoveryPublication.cs",
        "MortalWoundTreatmentResolverTests.ComplicationRemovalPublication.cs",
        "MortalWoundTreatmentResolverTests.Replay.cs",
        "MortalWoundTreatmentResolverTests.ResourceAuthority.cs",
        "MortalWoundTreatmentResolverTests.ResourceFinalization.cs",
        "MortalWoundTreatmentResolverTests.ResourcePublication.cs",
        "MortalWoundTreatmentResolverTests.SeverityReductionPlanner.cs",
        "MortalWoundTreatmentResolverTests.SpiritualOriginalIntakeGuard.cs",
        "MortalWoundTreatmentResolverTests.VehicleTopology.cs"
    ];

    /// <summary>
    /// Verifies that the category runner retains bounded child-process ownership and cleanup.
    /// </summary>
    [Fact]
    public void CSharpCategoryRunner_PreservesBoundedOwnedProcesses()
    {
        var runnerPath = Path.Combine(TestRepoPaths.RepoRoot, "scripts", "test-csharp.ps1");
        var source = File.ReadAllText(runnerPath);
        var adapterSource = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot,
            "scripts", "testing", "TestCategoryExecution.ps1"));
        Assert.Matches(
            @"\$fastTestProject\s*=\s*Join-Path\s+\$repoRoot\s+['""']BookOfEternityClient.Tests[\\/]BookOfEternityClient.Tests.csproj['""']",
            source);
        Assert.Matches(
            @"\$integrationTestProject\s*=\s*Join-Path\s+\$repoRoot\s+['""']BookOfEternityClient.IntegrationTests[\\/]BookOfEternityClient.IntegrationTests.csproj['""']",
            source);
        Assert.Matches(
            @"\$projectPath\s*=\s*if\s*\(\$project\s*-eq\s*['""']unit['""']\)\s*\{\s*\$fastTestProject\s*\}\s*else\s*\{\s*\$integrationTestProject\s*\}",
            adapterSource);

        var requiredTokens = new[]
        {
            "[CmdletBinding(DefaultParameterSetName = 'Categories')]",
            "ParameterSetName = 'SelfTest'",
            "$PSCmdlet.ParameterSetName",
            "dotnet-test.log",
            "Stopwatch",
            "WaitForExit",
            "Kill($true)",
            "ArgumentList.Add",
            "[string]$FileName = \"dotnet\"",
            "$startInfo.FileName = $FileName",
            "$fastTestProject",
            "$integrationTestProject",
            "summary.json",
            "DuplicateTests",
            "Get-Command -Name \"npm.cmd\" -CommandType Application",
            "$npmCommandPath = Resolve-NpmCommandPath",
            "-FileName $npmCommandPath",
            "New-UniqueResultDirectory",
            "$initialCleanupSucceeded",
            "FinalizerRetried",
            "$OwnedCleanupPassLimit = 2",
            "Get-OwnedCleanupDisposition",
            "JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE",
            "TerminateJobObject",
            "QueryInformationJobObject",
            "EventWaitHandle",
            "owned-process-launcher.ps1",
            "$launchGate.Set()",
            "Wait-ForOwnedProcess -Run $entry.Run",
            "ContainmentEmpty",
            "Live owned process retained after bounded cleanup retries",
            "Owned cleanup diagnostics",
            "$PID",
            "[Guid]::NewGuid()"
        };
        Assert.All(requiredTokens, token =>
            Assert.Contains(token, source, StringComparison.Ordinal));

        Assert.DoesNotContain(
            "-FileName \"npm.cmd\"",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Start-Sleep -Milliseconds 750",
            source,
            StringComparison.Ordinal);

        var processStart = source.IndexOf(
            "$started = $process.Start()",
            StringComparison.Ordinal);
        var containmentAssignment = source.IndexOf(
            "Add-OwnedProcessToContainment",
            processStart,
            StringComparison.Ordinal);
        var launchRelease = source.IndexOf(
            "$launchGate.Set()",
            containmentAssignment,
            StringComparison.Ordinal);
        Assert.True(
            processStart >= 0 &&
            containmentAssignment > processStart &&
            launchRelease > containmentAssignment,
            "The owned target must remain gated until its launcher is inside containment.");
        var forbiddenBroadProcessCommands = new[]
        {
            "Get-" + "Process",
            "Stop-" + "Process",
            "task" + "kill"
        };
        Assert.All(forbiddenBroadProcessCommands, command =>
            Assert.DoesNotContain(command, source, StringComparison.OrdinalIgnoreCase));

        var registerIndex = source.IndexOf(
            "[void]$allRuns.Add($run)",
            StringComparison.Ordinal);
        var standardOutputDrainIndex = source.IndexOf(
            "$process.StandardOutput.ReadToEndAsync()",
            StringComparison.Ordinal);
        var standardErrorDrainIndex = source.IndexOf(
            "$process.StandardError.ReadToEndAsync()",
            StringComparison.Ordinal);
        var postStartLogIndex = source.IndexOf(
            "Owned process '$Name': FileName=$FileName;",
            StringComparison.Ordinal);
        Assert.True(registerIndex >= 0, source);
        Assert.True(standardOutputDrainIndex > registerIndex, source);
        Assert.True(standardErrorDrainIndex > standardOutputDrainIndex, source);
        Assert.True(postStartLogIndex > standardErrorDrainIndex, source);
        Assert.Contains("[void]$allRuns.Remove($run)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that an omitted selection displays help and retired lanes fail before starting work.
    /// </summary>
    /// <returns>
    /// A task completing after the runner rejects both legacy selections without a workload.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_NoSelectionOrRetiredLaneStartsNoWorkload()
    {
        var help = await RunCSharpRunnerAsync();
        Assert.Equal(0, help.ExitCode);
        Assert.Contains("-ListCategories", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("-Category", help.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("Results:", help.StandardOutput, StringComparison.Ordinal);

        foreach (var lane in new[] { "Fast", "PreMerge", "Complete" })
        {
            var rejected = await RunCSharpRunnerAsync("-Lane", lane);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.Contains("retired", rejected.StandardOutput + rejected.StandardError,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("-Category", rejected.StandardOutput + rejected.StandardError,
                StringComparison.Ordinal);
            Assert.DoesNotContain("PLAN ", rejected.StandardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("Results:", rejected.StandardOutput, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Verifies that npm starts through the owned process launcher with a fully qualified application path.
    /// </summary>
    /// <returns>
    /// A task that completes after npm's owned launch settings are inspected.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_AbsoluteNpmApplicationStartsWithOwnedProcessSettings()
    {
        var probe = await RunCSharpRunnerSelfTestAsync("NpmStartup");

        Assert.True(
            probe.ExitCode == 0,
            $"npm startup probe failed.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{probe.StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{probe.StandardError}");

        var resultDirectory = ResultDirectoryFrom(probe.StandardOutput);
        var log = await File.ReadAllTextAsync(
            Path.Combine(resultDirectory, "dotnet-test.log"));
        var startup = Regex.Match(
            log,
            @"Owned process 'Npm-startup-probe': FileName=(?<path>.+?); " +
            @"UseShellExecute=False; CreateNoWindow=True; " +
            @"RedirectStandardOutput=True; RedirectStandardError=True");

        Assert.True(startup.Success, log);
        var executablePath = startup.Groups["path"].Value;
        Assert.True(Path.IsPathFullyQualified(executablePath), executablePath);
        Assert.True(File.Exists(executablePath), executablePath);
        Assert.EndsWith("npm.cmd", executablePath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that concurrent self-tests retain distinct diagnostic directories.
    /// </summary>
    /// <returns>
    /// A task completing after both self-tests expose different result paths.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_ConcurrentSelfTestsCreateDistinctResultDirectories()
    {
        var probes = await Task.WhenAll(
            RunCSharpRunnerSelfTestAsync("ResultDirectory"),
            RunCSharpRunnerSelfTestAsync("ResultDirectory"));

        Assert.All(probes, probe =>
            Assert.True(
                probe.ExitCode == 0,
                $"Result-directory probe failed.{Environment.NewLine}" +
                $"stdout:{Environment.NewLine}{probe.StandardOutput}{Environment.NewLine}" +
                $"stderr:{Environment.NewLine}{probe.StandardError}"));

        var resultDirectories = probes
            .Select(probe => ResultDirectoryFrom(probe.StandardOutput))
            .ToArray();
        Assert.Equal(2, resultDirectories.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(resultDirectories, resultDirectory =>
        {
            Assert.True(Directory.Exists(resultDirectory), resultDirectory);
            Assert.Matches(
                @"-\d+-[0-9a-f]{32}-selftest$",
                Path.GetFileName(resultDirectory));
        });
    }

    /// <summary>
    /// Verifies that private self-tests cannot be combined with public category planning.
    /// </summary>
    /// <returns>
    /// A task completing after invalid parameter sets are rejected before any probe starts.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_SelfTestCannotCombineWithCategoryOrPlanOnly()
    {
        var realCategoryCombination = await RunCSharpRunnerAsync(
            "-Category",
            "test-selection-contracts",
            "-SelfTest",
            "ResultDirectory");
        var realCategoryError = await RunCSharpRunnerCliXmlAsync(
            "-Category",
            "test-selection-contracts",
            "-SelfTest",
            "ResultDirectory");
        var planOnlyCombination = await RunCSharpRunnerAsync(
            "-PlanOnly",
            "-SelfTest",
            "NpmStartup");
        var planOnlyError = await RunCSharpRunnerCliXmlAsync(
            "-PlanOnly",
            "-SelfTest",
            "NpmStartup");

        AssertRejectedParameterSet(realCategoryCombination, realCategoryError);
        AssertRejectedParameterSet(planOnlyCombination, planOnlyError);
        Assert.DoesNotContain(
            "Npm-startup-probe",
            planOnlyCombination.StandardOutput + planOnlyCombination.StandardError,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a logging failure after process start still kills and disposes the owned child.
    /// </summary>
    /// <returns>
    /// A task that completes after the failed launch is cleaned up.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_PostStartLoggingFailureKillsAndDisposesOwnedProcess()
    {
        var probe = await RunCSharpRunnerSelfTestAsync("OwnedPostStartFailure");

        Assert.True(
            probe.ExitCode == 0,
            $"Owned-process cleanup probe failed.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{probe.StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{probe.StandardError}");
        Assert.Contains(
            "Post-start cleanup: killed=True disposed=True registered=False",
            probe.StandardOutput,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the finalizer retries failed cleanup and reports its retained evidence.
    /// </summary>
    /// <returns>
    /// A task that completes after retry evidence and process exit are checked.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_FailedInitialCleanupIsRetriedAndReportedWithoutOrphaning()
    {
        var probe = await RunCSharpRunnerSelfTestAsync("OwnedPostStartCleanupRetry");

        Assert.Equal(0, probe.ExitCode);
        var retryEvidence = Regex.Match(
            probe.StandardOutput,
            @"Post-start retry: pid=(?<pid>\d+) registered=True " +
            @"finalizerRetried=True cleanupPasses=2 stopAttempts=2 " +
            @"firstStopFailed=True finalCleanupSucceeded=True " +
            @"processExited=True disposed=True registeredAfterFinalCleanup=False " +
            @"errorsPreserved=True");
        Assert.True(retryEvidence.Success, probe.StandardOutput);
        var processId = int.Parse(retryEvidence.Groups["pid"].Value);

        using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(
                ResultDirectoryFrom(probe.StandardOutput),
                "self-test-summary.json")));
        Assert.True(
            summary.RootElement
                .GetProperty("OwnedTreeCleanupSucceeded")
                .GetBoolean());
        var cleanup = summary.RootElement.GetProperty("PostStartCleanup");
        Assert.Equal(processId, cleanup.GetProperty("ProcessId").GetInt32());
        Assert.True(cleanup.GetProperty("RegisteredAfterInitialFailure").GetBoolean());
        Assert.True(cleanup.GetProperty("FinalizerRetried").GetBoolean());
        Assert.Equal(2, cleanup.GetProperty("CleanupPasses").GetInt32());
        Assert.Equal(2, cleanup.GetProperty("StopAttempts").GetInt32());
        Assert.True(cleanup.GetProperty("FirstStopFailed").GetBoolean());
        Assert.True(cleanup.GetProperty("FinalCleanupSucceeded").GetBoolean());
        Assert.True(cleanup.GetProperty("ProcessExited").GetBoolean());
        Assert.True(cleanup.GetProperty("Disposed").GetBoolean());
        Assert.False(cleanup.GetProperty("RegisteredAfterFinalCleanup").GetBoolean());
        Assert.True(cleanup.GetProperty("ErrorsPreserved").GetBoolean());
        Assert.True(
            await WaitForExactProcessExitAsync(processId, TimeSpan.FromSeconds(2)),
            $"Owned child PID {processId} still exists after the runner exited.");
    }

    /// <summary>
    /// Verifies that containment remains effective when the root process exits before its child.
    /// </summary>
    /// <returns>
    /// A task that completes after the descendant has exited.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_ExitedRootCannotConcealOwnedDescendant()
    {
        var probe = await RunCSharpRunnerSelfTestAsync("OwnedExitedRootDescendant");

        Assert.True(
            probe.ExitCode == 0,
            $"Exited-root cleanup probe failed.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{probe.StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{probe.StandardError}");

        using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(
                ResultDirectoryFrom(probe.StandardOutput),
                "self-test-summary.json")));
        Assert.True(
            summary.RootElement
                .GetProperty("OwnedTreeCleanupSucceeded")
                .GetBoolean());
        var containment = summary.RootElement.GetProperty("ExitedRootDescendant");
        var processId = containment.GetProperty("ProcessId").GetInt32();
        Assert.True(containment.GetProperty("RootExitedBeforeCleanup").GetBoolean());
        Assert.True(containment.GetProperty("ObservedAliveBeforeCleanup").GetBoolean());
        Assert.True(containment.GetProperty("ExitedAfterCleanup").GetBoolean());
        Assert.True(
            await WaitForExactProcessExitAsync(processId, TimeSpan.FromSeconds(2)),
            $"Owned descendant PID {processId} still exists after its root and runner exited.");
    }

    /// <summary>
    /// Verifies that a batch finishes after cleaning a descendant whose root has exited.
    /// </summary>
    /// <returns>
    /// A task that completes after the batch-owned descendant has exited.
    /// </returns>
    [Fact]
    public async Task CSharpCategoryRunner_ParallelBatchCannotBlockOnExitedRootDescendant()
    {
        var probe = await RunCSharpRunnerSelfTestAsync(
            "OwnedBatchExitedRootDescendant");

        Assert.True(
            probe.ExitCode == 0,
            $"Parallel-batch cleanup probe failed.{Environment.NewLine}" +
            $"stdout:{Environment.NewLine}{probe.StandardOutput}{Environment.NewLine}" +
            $"stderr:{Environment.NewLine}{probe.StandardError}");

        using var summary = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(
                ResultDirectoryFrom(probe.StandardOutput),
                "self-test-summary.json")));
        Assert.True(
            summary.RootElement
                .GetProperty("OwnedTreeCleanupSucceeded")
                .GetBoolean());
        var containment = summary.RootElement.GetProperty("ExitedRootDescendant");
        var processId = containment.GetProperty("ProcessId").GetInt32();
        Assert.True(containment.GetProperty("RootExitedBeforeCleanup").GetBoolean());
        Assert.True(containment.GetProperty("ObservedAliveBeforeCleanup").GetBoolean());
        Assert.True(containment.GetProperty("ExitedAfterCleanup").GetBoolean());
        Assert.True(
            await WaitForExactProcessExitAsync(processId, TimeSpan.FromSeconds(2)),
            $"Batch-owned descendant PID {processId} still exists after cleanup.");
    }

    /// <summary>
    /// Verifies that exhausted cleanup retries retain ownership of a still-live process.
    /// </summary>
    [Fact]
    public void CSharpCategoryRunner_AllRetriesExhaustedDispositionRetainsLiveRun()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "scripts",
            "test-csharp.ps1"));
        var dispositionStart = source.IndexOf(
            "function Get-OwnedCleanupDisposition",
            StringComparison.Ordinal);
        Assert.True(dispositionStart >= 0, source);
        var nextFunction = source.IndexOf(
            "function ",
            dispositionStart + 1,
            StringComparison.Ordinal);
        Assert.True(nextFunction > dispositionStart, source);
        var disposition = source[dispositionStart..nextFunction];

        Assert.Contains(
            "RemoveFromRegistry = $ProcessExited -and $ContainmentEmpty",
            disposition,
            StringComparison.Ordinal);
        Assert.Contains(
            "DisposeHandle = $ProcessExited -and $ContainmentEmpty",
            disposition,
            StringComparison.Ordinal);
        Assert.Contains(
            "CleanupSucceeded = $ProcessExited -and $ContainmentEmpty -and " +
            "$FinalizationSucceeded",
            disposition,
            StringComparison.Ordinal);
        Assert.Contains(
            "$OwnedCleanupPassLimit = 2",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Live owned process retained after bounded cleanup retries",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$disposition = Get-OwnedCleanupDisposition",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if ($disposition.DisposeHandle)",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewedHeavySources_ExistOnlyUnderIntegrationTests()
    {
        var fastRoot = Path.Combine(TestRepoPaths.RepoRoot, FastTestsDirectory);
        var integrationRoot = Path.Combine(TestRepoPaths.RepoRoot, IntegrationTestsDirectory);
        var supportRoot = Path.Combine(TestRepoPaths.RepoRoot, TestSupportDirectory);

        foreach (var relativePath in ReviewedHeavySourcePaths)
        {
            var matches = new[] { fastRoot, integrationRoot, supportRoot }
                .SelectMany(root => Directory.EnumerateFiles(
                    root,
                    Path.GetFileName(relativePath),
                    SearchOption.AllDirectories))
                .Select(Path.GetFullPath)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var expectedIntegrationPath = Path.GetFullPath(Path.Combine(integrationRoot, relativePath));

            Assert.Equal(new[] { expectedIntegrationPath }, matches);
        }
    }

    [Fact]
    public void MortalWoundTreatmentResolverPartialFamily_HasExactIntegrationInventory()
    {
        var integrationRoot = Path.Combine(TestRepoPaths.RepoRoot, IntegrationTestsDirectory);
        var actual = Directory
            .EnumerateFiles(integrationRoot, "MortalWoundTreatmentResolverTests*.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            MortalWoundTreatmentResolverPartialSourcePaths.Order(StringComparer.Ordinal),
            actual);
    }

    [Fact]
    public void QteDeterministicLogicSource_ExistsOnlyInFastAndIsFixtureFree()
    {
        const string fileName = "QteDeterministicLogicTests.cs";
        var fastRoot = Path.Combine(TestRepoPaths.RepoRoot, FastTestsDirectory);
        var integrationRoot = Path.Combine(TestRepoPaths.RepoRoot, IntegrationTestsDirectory);
        var supportRoot = Path.Combine(TestRepoPaths.RepoRoot, TestSupportDirectory);
        var expectedFastPath = Path.GetFullPath(Path.Combine(fastRoot, fileName));
        var matches = new[] { fastRoot, integrationRoot, supportRoot }
            .SelectMany(root => Directory.EnumerateFiles(
                root,
                fileName,
                SearchOption.AllDirectories))
            .Select(Path.GetFullPath)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(new[] { expectedFastPath }, matches);

        var source = File.ReadAllText(expectedFastPath);
        var forbiddenMarkers = new[]
        {
            "public QteDeterministicLogicTests(",
            ": IDisposable",
            "FileSystemManager",
            "Path.GetTempPath(",
            "private readonly",
            "ILogger<",
            "NullLogger<",
            "IHost",
            "WebApplication",
            "[Trait(",
            "QteNormalizerBackupDirectory",
            "QteRuntimePath",
            "CreateRuntimeCapableService(",
            "CreateSaveLoadServiceAsync(",
            ".WriteFileAtomicAsync(",
            ".ReadFileAsync("
        };

        Assert.All(
            forbiddenMarkers,
            marker => Assert.DoesNotContain(marker, source, StringComparison.Ordinal));
    }

    [Fact]
    public void TestProjectTopology_SeparatesFastIntegrationAndSupportAssemblies()
    {
        var fastProject = ReadProject(FastTestsDirectory, FastTestsProjectFile);
        var integrationProject = ReadProject(IntegrationTestsDirectory, IntegrationTestsProjectFile);
        var supportProject = ReadProject(TestSupportDirectory, TestSupportProjectFile);
        var productionProject = ReadProject(ProductionDirectory, ProductionProjectFile);

        AssertProjectReferences(
            fastProject,
            FastTestsDirectory,
            "Fast test project",
            ProjectPath(ProductionDirectory, ProductionProjectFile),
            ProjectPath(TestSupportDirectory, TestSupportProjectFile));
        AssertProjectReferences(
            integrationProject,
            IntegrationTestsDirectory,
            "Integration test project",
            ProjectPath(ProductionDirectory, ProductionProjectFile),
            ProjectPath(TestSupportDirectory, TestSupportProjectFile));
        AssertDoesNotReference(
            integrationProject,
            IntegrationTestsDirectory,
            "Integration test project",
            ProjectPath(FastTestsDirectory, FastTestsProjectFile));
        AssertProjectReferences(
            supportProject,
            TestSupportDirectory,
            "Test support project",
            ProjectPath(ProductionDirectory, ProductionProjectFile));

        AssertDoesNotReferencePackage(
            supportProject,
            packageId => string.Equals(
                packageId,
                "Microsoft.NET.Test.Sdk",
                StringComparison.OrdinalIgnoreCase),
            "Microsoft.NET.Test.Sdk");
        AssertDoesNotReferencePackage(
            supportProject,
            IsXunitPackageId,
            "an xUnit-family package");

        AssertFriendAssemblies(
            productionProject,
            "Production project",
            "BookOfEternityClient.Tests",
            "BookOfEternityClient.TestSupport",
            "BookOfEternityClient.IntegrationTests");
        AssertFriendAssemblies(
            supportProject,
            "Test support project",
            "BookOfEternityClient.Tests",
            "BookOfEternityClient.IntegrationTests");

        var solutionProjects = ReadSolutionProjectPaths();
        AssertContainsPath(
            solutionProjects,
            ProjectPath(ProductionDirectory, ProductionProjectFile),
            "Production solution must include the production project");
        AssertDoesNotContainPath(
            solutionProjects,
            ProjectPath(FastTestsDirectory, FastTestsProjectFile),
            "Production solution must not include the fast test project");
        AssertDoesNotContainPath(
            solutionProjects,
            ProjectPath(IntegrationTestsDirectory, IntegrationTestsProjectFile),
            "Production solution must not include the integration test project");
        AssertDoesNotContainPath(
            solutionProjects,
            ProjectPath(TestSupportDirectory, TestSupportProjectFile),
            "Production solution must not include the test support project");
    }

    [Fact]
    public void ProjectReferences_UpdateOnlyItem_DoesNotCreateDirectReference()
    {
        var project = XDocument.Parse(
            """
            <Project>
              <ItemGroup>
                <ProjectReference Update="..\BookOfEternityClient\BookOfEternityClient.csproj" />
              </ItemGroup>
            </Project>
            """);

        Assert.Empty(ProjectReferences(project, FastTestsDirectory));
    }

    private static XDocument ReadProject(string directory, string fileName) =>
        XDocument.Load(ProjectPath(directory, fileName));

    private static string ProjectPath(params string[] relativeParts) =>
        Path.GetFullPath(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(relativeParts).ToArray()));

    private static void AssertProjectReferences(
        XDocument project,
        string projectDirectory,
        string projectDescription,
        params string[] expectedReferences)
    {
        var references = ProjectReferences(project, projectDirectory);

        foreach (var expectedReference in expectedReferences)
        {
            AssertContainsPath(
                references,
                expectedReference,
                $"{projectDescription} is missing a required ProjectReference");
        }
    }

    private static void AssertDoesNotReference(
        XDocument project,
        string projectDirectory,
        string projectDescription,
        string forbiddenReference)
    {
        AssertDoesNotContainPath(
            ProjectReferences(project, projectDirectory),
            forbiddenReference,
            $"{projectDescription} must not reference the fast test assembly");
    }

    private static IReadOnlyCollection<string> ProjectReferences(XDocument project, string projectDirectory) =>
        project
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "ProjectReference", StringComparison.Ordinal))
            .Select(element => AttributeValue(element, "Include"))
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => ResolveRelativePath(ProjectPath(projectDirectory), reference!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static void AssertDoesNotReferencePackage(
        XDocument project,
        Func<string, bool> isForbidden,
        string forbiddenPackageDescription)
    {
        var packageIds = PackageReferenceIds(project);

        Assert.False(
            packageIds.Any(isForbidden),
            $"Test support project must not reference {forbiddenPackageDescription}. " +
            $"Parsed package IDs: {string.Join(", ", packageIds)}");
    }

    private static IReadOnlyCollection<string> PackageReferenceIds(XDocument project) =>
        project
            .Descendants()
            .Where(element => string.Equals(
                element.Name.LocalName,
                "PackageReference",
                StringComparison.Ordinal))
            .Select(element =>
                AttributeValue(element, "Include") ??
                AttributeValue(element, "Update"))
            .Where(packageId => !string.IsNullOrWhiteSpace(packageId))
            .Select(packageId => packageId!.Trim())
            .ToArray();

    private static bool IsXunitPackageId(string packageId) =>
        string.Equals(packageId, "xunit", StringComparison.OrdinalIgnoreCase) ||
        packageId.StartsWith("xunit.", StringComparison.OrdinalIgnoreCase);

    private static void AssertFriendAssemblies(
        XDocument project,
        string projectDescription,
        params string[] requiredFriendAssemblies)
    {
        var friendAssemblies = project
            .Descendants()
            .Where(element =>
                string.Equals(element.Name.LocalName, "AssemblyAttribute", StringComparison.Ordinal) &&
                string.Equals(
                    AttributeValue(element, "Include"),
                    "System.Runtime.CompilerServices.InternalsVisibleTo",
                    StringComparison.Ordinal))
            .SelectMany(element => element.Descendants())
            .Where(element => string.Equals(element.Name.LocalName, "_Parameter1", StringComparison.Ordinal))
            .Select(element => element.Value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var requiredFriendAssembly in requiredFriendAssemblies)
        {
            Assert.True(
                friendAssemblies.Contains(requiredFriendAssembly),
                $"{projectDescription} is missing InternalsVisibleTo for '{requiredFriendAssembly}'. " +
                $"Parsed friends: {string.Join(", ", friendAssemblies)}");
        }
    }

    private static IReadOnlyCollection<string> ReadSolutionProjectPaths()
    {
        var solutionPath = ProjectPath(ProductionDirectory, "BookOfEternityClient.sln");
        var projectEntry = new Regex(
            "^\\s*Project\\(\\\"[^\\\"]+\\\"\\)\\s*=\\s*\\\"[^\\\"]+\\\",\\s*\\\"(?<path>[^\\\"]+)\\\",\\s*\\\"[^\\\"]+\\\"\\s*$",
            RegexOptions.CultureInvariant);

        return File
            .ReadLines(solutionPath)
            .Select(line => projectEntry.Match(line))
            .Where(match => match.Success)
            .Select(match => ResolveRelativePath(
                Path.GetDirectoryName(solutionPath)!,
                match.Groups["path"].Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveRelativePath(string baseDirectory, string relativePath)
    {
        var normalizedPath = relativePath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        return Path.GetFullPath(Path.Combine(baseDirectory, normalizedPath));
    }

    private static string? AttributeValue(XElement element, string localName) =>
        element
            .Attributes()
            .SingleOrDefault(attribute => string.Equals(
                attribute.Name.LocalName,
                localName,
                StringComparison.Ordinal))
            ?.Value;

    private static void AssertContainsPath(
        IEnumerable<string> actualPaths,
        string expectedPath,
        string failureDescription)
    {
        var paths = actualPaths.ToArray();
        Assert.True(
            paths.Contains(expectedPath, StringComparer.OrdinalIgnoreCase),
            $"{failureDescription}: '{expectedPath}'. Parsed paths: {string.Join(", ", paths)}");
    }

    private static void AssertDoesNotContainPath(
        IEnumerable<string> actualPaths,
        string forbiddenPath,
        string failureDescription)
    {
        var paths = actualPaths.ToArray();
        Assert.False(
            paths.Contains(forbiddenPath, StringComparer.OrdinalIgnoreCase),
            $"{failureDescription}: '{forbiddenPath}'. Parsed paths: {string.Join(", ", paths)}");
    }

    private static async Task<RunnerSelfTestResult> RunCSharpRunnerSelfTestAsync(
        string selfTest) =>
        await RunCSharpRunnerAsync("-SelfTest", selfTest);

    private static async Task<bool> WaitForExactProcessExitAsync(
        int processId,
        TimeSpan timeout)
    {
        var deadline = Stopwatch.GetTimestamp() +
            (long)(timeout.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                {
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }

            await Task.Delay(25);
        }

        return false;
    }

    private static Task<RunnerSelfTestResult> RunCSharpRunnerAsync(
        params string[] arguments) =>
        RunCSharpRunnerAsync(serializeErrorRecords: false, arguments);

    private static Task<RunnerSelfTestResult> RunCSharpRunnerCliXmlAsync(
        params string[] arguments) =>
        RunCSharpRunnerAsync(serializeErrorRecords: true, arguments);

    private static async Task<RunnerSelfTestResult> RunCSharpRunnerAsync(
        bool serializeErrorRecords,
        params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("pwsh")
        {
            WorkingDirectory = TestRepoPaths.RepoRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        if (serializeErrorRecords)
        {
            startInfo.ArgumentList.Add("-OutputFormat");
            startInfo.ArgumentList.Add("XML");
        }
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(
            Path.Combine(TestRepoPaths.RepoRoot, "scripts", "test-csharp.ps1"));
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("PowerShell runner self-test did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException(
                $"PowerShell runner invocation exceeded 30 seconds: " +
                string.Join(" ", arguments));
        }

        return new RunnerSelfTestResult(
            process.ExitCode,
            await standardOutput,
            await standardError);
    }

    private static void AssertRejectedParameterSet(
        RunnerSelfTestResult result,
        RunnerSelfTestResult structuredError)
    {
        Assert.NotEqual(0, result.ExitCode);
        var xmlStart = structuredError.StandardError.IndexOf(
            "<Objs ",
            StringComparison.Ordinal);
        Assert.True(xmlStart >= 0, structuredError.StandardError);
        var cliXml = XDocument.Parse(structuredError.StandardError[xmlStart..]);
        XNamespace powerShell =
            "http://schemas.microsoft.com/powershell/2004/04";
        var fullyQualifiedErrorIds = cliXml
            .Descendants(powerShell + "S")
            .Where(value => string.Equals(
                (string?)value.Attribute("N"),
                "FullyQualifiedErrorId",
                StringComparison.Ordinal))
            .Select(static value => value.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Contains(
            "AmbiguousParameterSet,test-csharp.ps1",
            fullyQualifiedErrorIds);
        Assert.DoesNotContain("Results:", result.StandardOutput, StringComparison.Ordinal);
    }

    private static string ResultDirectoryFrom(string standardOutput)
    {
        var match = Regex.Match(
            standardOutput,
            @"(?m)^  Self-test results: (?<path>.+?)\r?$");
        Assert.True(match.Success, standardOutput);
        return match.Groups["path"].Value;
    }

    private sealed record RunnerSelfTestResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);
}
