[CmdletBinding(DefaultParameterSetName = "Lane")]
param(
    [Parameter(ParameterSetName = "Lane")]
    [ValidateSet(
        "Fast",
        "Focused",
        "FullValidation",
        "RegressionIntegration",
        "DeepValidation",
        "ProcessIntegration",
        "E2E",
        "LifecycleIntegration",
        "Complete",
        "PreMerge"
    )]
    [string]$Lane = "Fast",

    [Parameter(ParameterSetName = "Lane")]
    [string]$Filter,

    [Parameter(ParameterSetName = "Lane")]
    [ValidateSet("Fast", "Integration")]
    [string]$FocusedProject = "Fast",

    [Parameter(ParameterSetName = "Lane")]
    [ValidateRange(0, 120)]
    [int]$TimeoutMinutes = 0,

    [Parameter(ParameterSetName = "Lane")]
    [ValidateRange(1, 8)]
    [Alias("GuardianParallelism")]
    [int]$Parallelism = 4,

    [Parameter(ParameterSetName = "Lane")]
    [switch]$NoBuild,

    [Parameter(ParameterSetName = "Lane")]
    [switch]$PlanOnly,

    [ValidateSet(
        "NpmStartup",
        "ResultDirectory",
        "TrxSummary",
        "DurationSchedule",
        "OwnedPostStartFailure",
        "OwnedPostStartCleanupRetry",
        "OwnedExitedRootDescendant",
        "OwnedBatchExitedRootDescendant"
    )]
    [Parameter(Mandatory, ParameterSetName = "SelfTest", DontShow)]
    [string]$SelfTest,

    [Parameter(ParameterSetName = "SelfTest", DontShow)]
    [string]$SelfTestTrxDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$FastParallelismLimit = 2
$PreMergeParallelism = 4
$PreMergeFastParallelismLimit = 2
$ComposedSmallClassBinCount = 4
$DeepValidationSmallClassBinCount = 6
$DeepValidationInProcessParallelismEstimate = 3
$DeepValidationGuardianCaseTarget = 30
$DeepValidationStorageHeavySchedulingGroup = "DeepValidationStorageHeavy"
$DeepValidationStorageHeavyClasses = @(
    "BookOfEternityClient.Tests.ValidatorFixtureTests",
    "BookOfEternityClient.Tests.GuardianArchiveAndTradeRequestValidationTests"
)
$DeepValidationClassConcurrencyWeights = @{
    "BookOfEternityClient.Tests.ValidatorFixtureTests" = 2
}
$LargeClassCaseTarget = 120
$OwnedCleanupPassLimit = 2
$PreMergeMinimumCases = 4240
$DeepValidationMinimumCases = 1950
$DeepValidationClassDurationCosts = @{
    "BookOfEternityClient.Tests.ValidatorFixtureTests" = 542
    "BookOfEternityClient.Tests.GuardianArchiveAndTradeRequestValidationTests" = 600
    "BookOfEternityClient.Tests.ActorMaterializationValidationTests" = 285
    "BookOfEternityClient.Tests.SarefMainStoryStateValidationTests" = 271
    "BookOfEternityClient.Tests.SourceOfLightCapstoneValidationTests" = 201
    "BookOfEternityClient.Tests.NpcCoreChangesTests" = 194
    "BookOfEternityClient.Tests.ExampleDocumentationValidationTests" = 192
    "BookOfEternityClient.Tests.MortalBootstrapValidationTests" = 178
    "BookOfEternityClient.Tests.PlayerGuardianFoundationValidationTests" = 170
    "BookOfEternityClient.Tests.MechanicalBonusAuthorityValidationTests" = 170
    "BookOfEternityClient.Tests.MortalCommandDisplaySaveTests" = 154
    "BookOfEternityClient.Tests.ChaosSeaCommandDisplaySaveTests" = 138
    "BookOfEternityClient.Tests.CanonicalStateNormalizerTests" = 136
    "BookOfEternityClient.Tests.AfterlifeRealmSegregationValidationTests" = 132
    "BookOfEternityClient.Tests.ShiningAbodeCommandDisplaySaveTests" = 132
    "BookOfEternityClient.Tests.AfterlifeEntityProfileValidationTests" = 131
    "BookOfEternityClient.Tests.GuardianPolicyKernelTests" = 119
    "BookOfEternityClient.Tests.ShiningPoliticalResolutionValidationTests" = 108
    "BookOfEternityClient.Tests.ReadableDocumentAuthorityValidationTests" = 100
    "BookOfEternityClient.Tests.AfterlifeSpiritualConflictBalanceTests" = 87
    "BookOfEternityClient.Tests.QuestRewardAuthorityValidationTests" = 84
    "BookOfEternityClient.Tests.ChaosSeaPendingRequestHygieneTests" = 83
    "BookOfEternityClient.Tests.ValidationServiceQteTests" = 50
}
$RegressionIntegrationClassDurationCosts = @{
    "BookOfEternityClient.Tests.AfterlifeSpiritualConflictValidationTests" = 350
    "BookOfEternityClient.Tests.BrowserCommandPresentationAuditTests" = 339
    "BookOfEternityClient.Tests.ExplorerModeCommandTests" = 251
    "BookOfEternityClient.Tests.ExplorerWebCommandServiceTests" = 548
}
$RetainedGuardianShardDurationCosts = @{
    AcceptedAuthority = 280
    ActorBrain = 85
    Lifecycle = 35
    MortalFactPersistence = 10
    PowerJournalOfferings = 160
    ProjectsPower = 285
    QuestProgress = 55
    RivalResidents = 330
    TradeOfferingResonance = 430
}
$LifecycleIntegrationMinimumCases = 186
$coreIntegrationFilter =
    "Category!=FullValidation&Category!=DeepValidation&" +
    "Category!=ProcessIntegration&Category!=E2E&" +
    "(Category!=LifecycleIntegration|Category=PreMergeSentinel)&" +
    "(Category!=RegressionIntegrationOnly|Category=PreMergeSentinel)"
$deepValidationFilter =
    "(Category=FullValidation|Category=DeepValidation)&" +
    "Category!=LifecycleIntegration&" +
    "Category!=ProcessIntegration&Category!=E2E"
$lifecycleIntegrationFilter =
    "Category=LifecycleIntegration&" +
    "Category!=ProcessIntegration&Category!=E2E"

if ($PSVersionTable.PSVersion.Major -lt 7) {
    throw "scripts/test-csharp.ps1 requires PowerShell 7 or newer."
}

$laneDefinitions = @{
    Fast = @{
        Project = "Fast"
        Filter = $null
        TimeoutMinutes = 5
    }
    Focused = @{
        Project = "Fast"
        Filter = $null
        TimeoutMinutes = 5
    }
    FullValidation = @{
        Project = "Integration"
        Filter = "Category=FullValidation"
        TimeoutMinutes = 15
    }
    RegressionIntegration = @{
        Project = "Integration"
        Filter = "Category=RegressionIntegration"
        TimeoutMinutes = 15
    }
    DeepValidation = @{
        Project = "Integration"
        Filter = $deepValidationFilter
        TimeoutMinutes = 15
    }
    LifecycleIntegration = @{
        Project = "Integration"
        Filter = $lifecycleIntegrationFilter
        TimeoutMinutes = 10
    }
    ProcessIntegration = @{
        Project = "Integration"
        Filter = "Category=ProcessIntegration"
        TimeoutMinutes = 15
    }
    E2E = @{
        Project = "Integration"
        Filter = "Category=E2E"
        TimeoutMinutes = 15
    }
    PreMerge = @{
        Project = "Both"
        Filter = $null
        TimeoutMinutes = 15
    }
}

$isSelfTest = $PSCmdlet.ParameterSetName -eq "SelfTest"
$effectiveLane = $null
$laneDefinition = $null
$selectedProject = $null
$effectiveTimeoutMinutes = $null
if (-not $isSelfTest) {
    $effectiveLane = if ($Lane -eq "Complete") { "PreMerge" } else { $Lane }
    $laneDefinition = $laneDefinitions[$effectiveLane]
    $selectedProject = if ($effectiveLane -eq "Focused") {
        $FocusedProject
    }
    else {
        $laneDefinition.Project
    }
    $effectiveTimeoutMinutes = if ($TimeoutMinutes -gt 0) {
        $TimeoutMinutes
    }
    else {
        [int]$laneDefinition.TimeoutMinutes
    }
}

$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$fastTestProject = Join-Path $repoRoot "BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj"
$integrationTestProject = Join-Path $repoRoot "BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj"

function New-UniqueResultDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$RunLabel
    )

    $resultRoot = Join-Path $repoRoot "TestResults\test-lanes"
    [void][System.IO.Directory]::CreateDirectory($resultRoot)

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        $runStamp = Get-Date -Format "yyyyMMdd-HHmmss-fff"
        $uniqueName = "{0}-{1}-{2}-{3}" -f @(
            $runStamp,
            $PID,
            [Guid]::NewGuid().ToString("N"),
            $RunLabel.ToLowerInvariant()
        )
        $candidate = Join-Path $resultRoot $uniqueName
        try {
            $created = New-Item `
                -ItemType Directory `
                -Path $candidate `
                -ErrorAction Stop
            return $created.FullName
        }
        catch [System.IO.IOException] {
            if ($attempt -eq 5) {
                throw "Could not create a unique result directory after $attempt attempts."
            }
        }
    }
}

$resultLabel = if ($isSelfTest) { "fast" } else { $Lane }
$resultDirectory = New-UniqueResultDirectory -RunLabel $resultLabel
$logPath = Join-Path $resultDirectory "dotnet-test.log"

$logHeader = if ($isSelfTest) {
    @(
        "SelfTest: $SelfTest"
        "StartedUtc: $([DateTime]::UtcNow.ToString("O"))"
    )
}
else {
    @(
        "RequestedLane: $Lane"
        "EffectiveLane: $effectiveLane"
        "Filter: <pending validation>"
        "TimeoutMinutes: $effectiveTimeoutMinutes"
        "StartedUtc: $([DateTime]::UtcNow.ToString("O"))"
    )
}
Set-Content -LiteralPath $logPath -Value $logHeader

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$deadlineDuration = if ($isSelfTest) {
    [TimeSpan]::FromSeconds(30)
}
else {
    [TimeSpan]::FromMinutes($effectiveTimeoutMinutes)
}
$deadlineUtc = [DateTime]::UtcNow.Add($deadlineDuration)
$allRuns = [System.Collections.Generic.List[object]]::new()
$timedOut = $false
$cleanupSucceeded = $true
$exitCode = 0
$failureMessage = $null
$laneFilter = $null
$trxSummaryOverride = $null
$lastPostStartCleanup = $null
$lastExitedRootDescendant = $null

if ($IsWindows -and
    $null -eq ("BookOfEternity.Testing.OwnedProcessJob" -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BookOfEternity.Testing
{
    public sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeJobHandle() : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle() => CloseHandle(handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }

    public static class OwnedProcessJob
    {
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
        private const int JobObjectBasicAccountingInformationClass = 1;
        private const int JobObjectExtendedLimitInformationClass = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicLimitInformation
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectExtendedLimitInformation
        {
            public JobObjectBasicLimitInformation BasicLimitInformation;
            public IoCounters IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JobObjectBasicAccountingInformation
        {
            public long TotalUserTime;
            public long TotalKernelTime;
            public long ThisPeriodTotalUserTime;
            public long ThisPeriodTotalKernelTime;
            public uint TotalPageFaultCount;
            public uint TotalProcesses;
            public uint ActiveProcesses;
            public uint TotalTerminatedProcesses;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeJobHandle CreateJobObject(
            IntPtr jobAttributes,
            string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetInformationJobObject(
            SafeJobHandle job,
            int informationClass,
            IntPtr information,
            uint informationLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AssignProcessToJobObject(
            SafeJobHandle job,
            IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryInformationJobObject(
            SafeJobHandle job,
            int informationClass,
            out JobObjectBasicAccountingInformation information,
            uint informationLength,
            IntPtr returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateJobObject(
            SafeJobHandle job,
            uint exitCode);

        public static SafeJobHandle CreateKillOnClose()
        {
            var job = CreateJobObject(IntPtr.Zero, null);
            if (job.IsInvalid)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "CreateJobObject failed.");
            }

            var limits = new JobObjectExtendedLimitInformation
            {
                BasicLimitInformation =
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };
            var length = Marshal.SizeOf<JobObjectExtendedLimitInformation>();
            var buffer = Marshal.AllocHGlobal(length);
            try
            {
                Marshal.StructureToPtr(limits, buffer, fDeleteOld: false);
                if (!SetInformationJobObject(
                    job,
                    JobObjectExtendedLimitInformationClass,
                    buffer,
                    checked((uint)length)))
                {
                    throw new Win32Exception(
                        Marshal.GetLastWin32Error(),
                        "SetInformationJobObject failed.");
                }
            }
            catch
            {
                job.Dispose();
                throw;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return job;
        }

        public static void Assign(SafeJobHandle job, Process process)
        {
            if (!AssignProcessToJobObject(job, process.Handle))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    $"AssignProcessToJobObject failed for PID {process.Id}.");
            }
        }

        public static uint GetActiveProcessCount(SafeJobHandle job)
        {
            if (!QueryInformationJobObject(
                job,
                JobObjectBasicAccountingInformationClass,
                out var information,
                checked((uint)Marshal.SizeOf<JobObjectBasicAccountingInformation>()),
                IntPtr.Zero))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "QueryInformationJobObject failed.");
            }

            return information.ActiveProcesses;
        }

        public static void Terminate(SafeJobHandle job)
        {
            if (!TerminateJobObject(job, 1))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "TerminateJobObject failed.");
            }
        }
    }
}
'@
}

$ownedProcessLauncherPath = $null
$ownedProcessLauncherCommandPath = $null
if ($IsWindows) {
    $ownedProcessLauncherCommandPath = @(
        Get-Command -Name "pwsh" -CommandType Application -ErrorAction Stop
    )[0].Path
    $ownedProcessLauncherPath = Join-Path `
        $resultDirectory `
        "owned-process-launcher.ps1"
    Set-Content `
        -LiteralPath $ownedProcessLauncherPath `
        -Encoding utf8NoBOM `
        -Value @'
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$LaunchGateName,

    [Parameter(Mandatory)]
    [string]$PayloadPath
)

$ErrorActionPreference = "Stop"
$launchGate =
    [System.Threading.EventWaitHandle]::OpenExisting($LaunchGateName)
try {
    if (-not $launchGate.WaitOne(30000)) {
        throw "Owned process launch gate timed out."
    }

    $payload = Get-Content -LiteralPath $PayloadPath -Raw | ConvertFrom-Json
    & ([string]$payload.FileName) @($payload.Arguments)
    $targetExitCode = if ($null -eq $LASTEXITCODE) {
        0
    }
    else {
        [int]$LASTEXITCODE
    }
    exit $targetExitCode
}
catch {
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 1
}
finally {
    $launchGate.Dispose()
}
'@
}

function Get-ProjectDisplayPath {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath
    )

    return [System.IO.Path]::GetRelativePath($repoRoot, $ProjectPath).
        Replace([System.IO.Path]::DirectorySeparatorChar, '/')
}

function Resolve-NpmCommandPath {
    $npmCommands = @(
        Get-Command -Name "npm.cmd" -CommandType Application -ErrorAction Stop
    )
    foreach ($npmCommand in $npmCommands) {
        $candidate = $npmCommand.Path
        if ([System.IO.Path]::IsPathFullyQualified($candidate) -and
            (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "Could not resolve npm.cmd to an absolute application path."
}

function New-OwnedProcessContainment {
    if (-not $IsWindows) {
        return $null
    }

    return [BookOfEternity.Testing.OwnedProcessJob]::CreateKillOnClose()
}

function Add-OwnedProcessToContainment {
    param(
        [Parameter(Mandatory)]
        [object]$JobHandle,

        [Parameter(Mandatory)]
        [System.Diagnostics.Process]$Process
    )

    [BookOfEternity.Testing.OwnedProcessJob]::Assign($JobHandle, $Process)
}

function Test-OwnedProcessContainmentEmpty {
    param(
        [Parameter(Mandatory)]
        [object]$Run
    )

    if ($null -eq $Run.JobHandle) {
        return $Run.Process.HasExited
    }

    return [BookOfEternity.Testing.OwnedProcessJob]::GetActiveProcessCount(
        $Run.JobHandle) -eq 0
}

function Close-OwnedProcessContainment {
    param(
        [Parameter(Mandatory)]
        [object]$Run
    )

    if ($null -ne $Run.JobHandle) {
        $Run.JobHandle.Dispose()
        $Run.JobHandle = $null
    }
    if ($null -ne $Run.LaunchGate) {
        $Run.LaunchGate.Dispose()
        $Run.LaunchGate = $null
    }
}

function Start-OwnedProcess {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string[]]$Arguments,

        [string]$FileName = "dotnet",

        [switch]$Quiet,

        [switch]$SimulateInitialCleanupFailure
    )

    $jobHandle = New-OwnedProcessContainment
    $launchGate = $null
    $payloadPath = $null
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.WorkingDirectory = $repoRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::new()
    $started = $false
    try {
        if ($null -ne $jobHandle) {
            $launchToken = [Guid]::NewGuid().ToString("N")
            $payloadPath = Join-Path `
                $resultDirectory `
                "owned-process-$launchToken.json"
            [ordered]@{
                FileName = $FileName
                Arguments = @($Arguments)
            } |
                ConvertTo-Json -Compress -Depth 3 |
                Set-Content `
                    -LiteralPath $payloadPath `
                    -Encoding utf8NoBOM
            $launchGateName =
                "Local\BookOfEternity.TestRunner.$PID.$launchToken"
            $launchGate = [System.Threading.EventWaitHandle]::new(
                $false,
                [System.Threading.EventResetMode]::ManualReset,
                $launchGateName)
            $startInfo.FileName = $ownedProcessLauncherCommandPath
            foreach ($argument in @(
                "-NoProfile",
                "-File",
                $ownedProcessLauncherPath,
                "-LaunchGateName",
                $launchGateName,
                "-PayloadPath",
                $payloadPath
            )) {
                [void]$startInfo.ArgumentList.Add($argument)
            }
        }
        else {
            $startInfo.FileName = $FileName
            foreach ($argument in $Arguments) {
                [void]$startInfo.ArgumentList.Add($argument)
            }
        }

        $process.StartInfo = $startInfo
        $started = $process.Start()
        if ($started -and $null -ne $jobHandle) {
            Add-OwnedProcessToContainment `
                -JobHandle $jobHandle `
                -Process $process
            [void]$launchGate.Set()
        }
    }
    catch {
        if ($started -and -not $process.HasExited) {
            try {
                $process.Kill($true)
                [void]$process.WaitForExit(10000)
            }
            catch {
                # Closing a successfully assigned kill-on-close job is the
                # remaining exact-ownership cleanup path.
            }
        }
        if ($null -ne $jobHandle) {
            $jobHandle.Dispose()
        }
        if ($null -ne $launchGate) {
            $launchGate.Dispose()
        }
        $process.Dispose()
        throw
    }
    if (-not $started) {
        if ($null -ne $jobHandle) {
            $jobHandle.Dispose()
        }
        if ($null -ne $launchGate) {
            $launchGate.Dispose()
        }
        $process.Dispose()
        throw "Failed to start '$FileName' for owned process '$Name'."
    }

    $run = $null
    try {
        $run = [pscustomobject]@{
            Name = $Name
            FileName = $FileName
            Process = $process
            JobHandle = $jobHandle
            LaunchGate = $launchGate
            StandardOutput = $null
            StandardError = $null
            Finalized = $false
            ExitCode = $null
            StandardOutputText = $null
            StandardErrorText = $null
            Quiet = $Quiet.IsPresent
            PostStartInitializationFailed = $false
            SimulateFinalizerStopFailureOnce = $false
            FinalizerStopFailureInjected = $false
            ContainmentObservedNonEmptyAfterRootExit = $false
        }
        [void]$allRuns.Add($run)
        $run.StandardOutput = $process.StandardOutput.ReadToEndAsync()
        $run.StandardError = $process.StandardError.ReadToEndAsync()
        Add-Content -LiteralPath $logPath -Value (
            "Owned process '$Name': FileName=$FileName; " +
            "UseShellExecute=$($startInfo.UseShellExecute); " +
            "CreateNoWindow=$($startInfo.CreateNoWindow); " +
            "RedirectStandardOutput=$($startInfo.RedirectStandardOutput); " +
            "RedirectStandardError=$($startInfo.RedirectStandardError); " +
            "LauncherFileName=$($startInfo.FileName)")
        return $run
    }
    catch {
        $initializationError = $_
        $run.PostStartInitializationFailed = $true
        $processId = $process.Id
        $killed = $false
        $disposed = $false
        $initialCleanupSucceeded = $false
        $initialCleanupError = $null
        try {
            if ($SimulateInitialCleanupFailure) {
                throw [InvalidOperationException]::new(
                    "Simulated initial cleanup failure.")
            }
            $hadActiveProcess =
                -not $process.HasExited -or
                -not (Test-OwnedProcessContainmentEmpty -Run $run)
            $initialCleanupSucceeded = Stop-OwnedProcess -Run $run
            $killed = $hadActiveProcess -and $initialCleanupSucceeded
            if (-not $initialCleanupSucceeded) {
                throw [TimeoutException]::new(
                    "Initial owned-process cleanup did not confirm process exit.")
            }
        }
        catch {
            $initialCleanupError = $_
        }
        if ($initialCleanupSucceeded) {
            [void]$allRuns.Remove($run)
            Close-OwnedProcessContainment -Run $run
            $process.Dispose()
            $disposed = $true
        }
        $script:lastPostStartCleanup = [pscustomobject]@{
            Run = $run
            ProcessId = $processId
            Killed = $killed
            InitialCleanupSucceeded = $initialCleanupSucceeded
            Disposed = $disposed
            Registered = $allRuns.Contains($run)
            FinalizerRetried = $false
            CleanupPasses = 0
            StopAttempts = 0
            FirstStopFailed = $false
            FinalCleanupSucceeded = $null
            ProcessExited = $initialCleanupSucceeded
            ErrorsPreserved = $null
            FinalizerErrors = @()
            RegisteredAfterFinalCleanup = $allRuns.Contains($run)
        }
        if ($null -ne $initialCleanupError) {
            throw [AggregateException]::new(
                "Owned process initialization and initial cleanup both failed.",
                [Exception[]]@(
                    $initializationError.Exception,
                    $initialCleanupError.Exception
                ))
        }
        throw $initializationError
    }
}

function Complete-OwnedProcess {
    param(
        [Parameter(Mandatory)]
        [object]$Run
    )

    if ($Run.Finalized) {
        return
    }

    $Run.Process.WaitForExit()
    $standardOutput = $Run.StandardOutput.GetAwaiter().GetResult()
    $standardError = $Run.StandardError.GetAwaiter().GetResult()
    $Run.ExitCode = $Run.Process.ExitCode
    $Run.StandardOutputText = $standardOutput
    $Run.StandardErrorText = $standardError
    $Run.Finalized = $true

    Add-Content -LiteralPath $logPath -Value @(
        ""
        "===== $($Run.Name): stdout ====="
        $standardOutput
        "===== $($Run.Name): stderr ====="
        $standardError
        "===== $($Run.Name): exit $($Run.ExitCode) ====="
    )

    if (-not $Run.Quiet -and -not [string]::IsNullOrWhiteSpace($standardOutput)) {
        Write-Host $standardOutput.TrimEnd()
    }
    if (-not $Run.Quiet -and -not [string]::IsNullOrWhiteSpace($standardError)) {
        [Console]::Error.WriteLine($standardError.TrimEnd())
    }
}

function Stop-OwnedProcess {
    param(
        [Parameter(Mandatory)]
        [object]$Run,

        [switch]$SimulateFailureBeforeKill
    )

    try {
        if ($SimulateFailureBeforeKill) {
            Add-Content -LiteralPath $logPath -Value (
                "Simulated one-shot finalizer Stop failure before Kill for $($Run.Name).")
            return $false
        }

        if ($null -ne $Run.JobHandle) {
            $activeProcesses =
                [BookOfEternity.Testing.OwnedProcessJob]::GetActiveProcessCount(
                    $Run.JobHandle)
            if ($activeProcesses -gt 0) {
                [BookOfEternity.Testing.OwnedProcessJob]::Terminate(
                    $Run.JobHandle)
            }

            $containmentDeadline = [DateTime]::UtcNow.AddSeconds(10)
            while (-not (Test-OwnedProcessContainmentEmpty -Run $Run)) {
                if ([DateTime]::UtcNow -ge $containmentDeadline) {
                    return $false
                }
                Start-Sleep -Milliseconds 25
            }
        }
        elseif (-not $Run.Process.HasExited) {
            $Run.Process.Kill($true)
        }

        if (-not $Run.Process.HasExited) {
            if (-not $Run.Process.WaitForExit(10000)) {
                return $false
            }
        }
        return $Run.Process.HasExited -and
            (Test-OwnedProcessContainmentEmpty -Run $Run)
    }
    catch {
        Add-Content -LiteralPath $logPath -Value (
            "Cleanup error for $($Run.Name): $($_.Exception.Message)")
        return $false
    }
}

function Get-OwnedCleanupDisposition {
    param(
        [Parameter(Mandatory)]
        [bool]$ProcessExited,

        [Parameter(Mandatory)]
        [bool]$ContainmentEmpty,

        [Parameter(Mandatory)]
        [bool]$FinalizationSucceeded
    )

    return [pscustomobject]@{
        CleanupSucceeded = $ProcessExited -and $ContainmentEmpty -and $FinalizationSucceeded
        RemoveFromRegistry = $ProcessExited -and $ContainmentEmpty
        DisposeHandle = $ProcessExited -and $ContainmentEmpty
    }
}

function Wait-ForOwnedProcess {
    param(
        [Parameter(Mandatory)]
        [object]$Run
    )

    while (-not $Run.Process.HasExited) {
        if ([DateTime]::UtcNow -ge $deadlineUtc) {
            return $false
        }
        [void]$Run.Process.WaitForExit(250)
    }

    if (-not (Test-OwnedProcessContainmentEmpty -Run $Run)) {
        $Run.ContainmentObservedNonEmptyAfterRootExit = $true
        if (-not (Stop-OwnedProcess -Run $Run)) {
            throw (
                "Owned process '$($Run.Name)' exited while its contained " +
                "descendants could not be stopped.")
        }
    }
    Complete-OwnedProcess -Run $Run
    return $true
}

function Invoke-OwnedPhase {
    param(
        [Parameter(Mandatory)]
        [object]$Run,

        [Parameter(Mandatory)]
        [string]$TimeoutMessage,

        [Parameter(Mandatory)]
        [string]$FailureDescription
    )

    if (-not (Wait-ForOwnedProcess -Run $Run)) {
        $script:timedOut = $true
        throw $TimeoutMessage
    }
    if ($Run.ExitCode -ne 0) {
        $script:exitCode = $Run.ExitCode
        throw "$FailureDescription failed with exit code $($Run.ExitCode)."
    }
}

function Get-GuardianTestMethods {
    param(
        [Parameter(Mandatory)]
        [string]$SourceRoot,

        [Parameter(Mandatory)]
        [string[]]$SourceFiles
    )

    $methods = [System.Collections.Generic.List[string]]::new()
    foreach ($sourceFile in $SourceFiles) {
        $path = Join-Path $SourceRoot $sourceFile
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Guardian shard source not found: $path"
        }

        $pendingTestAttribute = $false
        foreach ($line in Get-Content -LiteralPath $path) {
            if ($line -match '^\s*\[(Fact|Theory)(Attribute)?(\(|\])') {
                $pendingTestAttribute = $true
                continue
            }

            if (-not $pendingTestAttribute) {
                continue
            }

            if ($line -match '^\s*$' -or $line -match '^\s*\[') {
                continue
            }

            if ($line -match '^\s*public\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|void)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(') {
                [void]$methods.Add($Matches.name)
                $pendingTestAttribute = $false
                continue
            }

            throw "Could not parse guardian test declaration after Fact/Theory in ${sourceFile}: $line"
        }
    }

    return @($methods)
}

function New-TestArguments {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$TrxFileName,

        [AllowNull()]
        [string]$TestFilter
    )

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in @(
        "test",
        $ProjectPath,
        "--no-build",
        "--no-restore",
        "--logger",
        "trx;LogFileName=$TrxFileName",
        "--results-directory",
        $resultDirectory,
        "--verbosity",
        "minimal"
    )) {
        [void]$arguments.Add($argument)
    }

    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        [void]$arguments.Add("--filter")
        [void]$arguments.Add($TestFilter)
    }

    return @($arguments)
}

function Get-DiscoveredTestCases {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$SelectionName,

        [AllowNull()]
        [string]$TestFilter
    )

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in @(
        "test",
        $ProjectPath,
        "--no-build",
        "--no-restore",
        "--list-tests",
        "--verbosity",
        "quiet"
    )) {
        [void]$arguments.Add($argument)
    }
    if (-not [string]::IsNullOrWhiteSpace($TestFilter)) {
        [void]$arguments.Add("--filter")
        [void]$arguments.Add($TestFilter)
    }

    $discoveryRun = Start-OwnedProcess `
        -Name "$SelectionName-discovery" `
        -Arguments @($arguments) `
        -Quiet
    Invoke-OwnedPhase `
        -Run $discoveryRun `
        -TimeoutMessage "Test discovery exceeded the lane deadline." `
        -FailureDescription "Test discovery for '$SelectionName'"

    $testCases = [System.Collections.Generic.List[object]]::new()
    foreach ($line in $discoveryRun.StandardOutputText -split "\r?\n") {
        $displayName = $line.Trim()
        if (-not $displayName.StartsWith(
            "BookOfEternityClient.Tests.",
            [StringComparison]::Ordinal)) {
            continue
        }

        $methodName = [regex]::Replace($displayName, '\(.*$', '')
        $lastSeparator = $methodName.LastIndexOf('.')
        if ($lastSeparator -le 0) {
            throw "Could not derive a test class from discovered name: $displayName"
        }

        [void]$testCases.Add([pscustomobject]@{
            ClassName = $methodName.Substring(0, $lastSeparator)
            MethodName = $methodName
            DisplayName = $displayName
        })
    }

    if ($testCases.Count -eq 0) {
        throw "Selection '$SelectionName' discovery returned no test cases."
    }
    return @($testCases)
}

function Get-IntegrationClassDurationCost {
    param(
        [Parameter(Mandatory)]
        [string]$LaneName,

        [Parameter(Mandatory)]
        [string]$ClassName,

        [Parameter(Mandatory)]
        [ValidateRange(1, 1000000)]
        [int]$FallbackCost
    )

    if ($LaneName -eq "PreMerge" -and
        $ClassName -eq
            "BookOfEternityClient.Tests.AfterlifeSpiritualConflictValidationTests") {
        return $FallbackCost
    }

    $durationCosts = switch ($LaneName) {
        "DeepValidation" { $DeepValidationClassDurationCosts }
        "RegressionIntegration" { $RegressionIntegrationClassDurationCosts }
        "PreMerge" { $RegressionIntegrationClassDurationCosts }
        default { $null }
    }
    if ($null -ne $durationCosts -and $durationCosts.ContainsKey($ClassName)) {
        return [int]$durationCosts[$ClassName]
    }
    return $FallbackCost
}

function Get-RetainedGuardianShardDurationCost {
    param(
        [Parameter(Mandatory)]
        [string]$ShardName,

        [Parameter(Mandatory)]
        [ValidateRange(1, 1000000)]
        [int]$FallbackCost,

        [Parameter(Mandatory)]
        [ValidateRange(1, 1000)]
        [int]$ChunkCount
    )

    if ($RetainedGuardianShardDurationCosts.ContainsKey($ShardName)) {
        return [int][Math]::Ceiling(
            $RetainedGuardianShardDurationCosts[$ShardName] / $ChunkCount)
    }
    return $FallbackCost
}

function Test-DescriptorSchedulingGroupAvailable {
    param(
        [Parameter(Mandatory)]
        [object]$Descriptor,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]]$ActiveEntries
    )

    $groupProperty = $Descriptor.PSObject.Properties["SchedulingGroup"]
    if ($null -eq $groupProperty -or
        [string]::IsNullOrWhiteSpace([string]$groupProperty.Value)) {
        return $true
    }

    foreach ($entry in $ActiveEntries) {
        $activeGroupProperty =
            $entry.Descriptor.PSObject.Properties["SchedulingGroup"]
        if ($null -ne $activeGroupProperty -and
            [StringComparer]::Ordinal.Equals(
                [string]$groupProperty.Value,
                [string]$activeGroupProperty.Value)) {
            return $false
        }
    }
    return $true
}

function Get-DescriptorConcurrencyWeight {
    param(
        [Parameter(Mandatory)]
        [object]$Descriptor
    )

    $weightProperty = $Descriptor.PSObject.Properties["ConcurrencyWeight"]
    if ($null -eq $weightProperty) {
        return 1
    }

    $weight = [int]$weightProperty.Value
    if ($weight -lt 1) {
        throw "Descriptor concurrency weight must be at least one."
    }
    return $weight
}

function Test-DescriptorCapacityAvailable {
    param(
        [Parameter(Mandatory)]
        [object]$Descriptor,

        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]]$ActiveEntries,

        [Parameter(Mandatory)]
        [ValidateRange(1, 8)]
        [int]$MaximumParallelism
    )

    $activeWeight = 0
    foreach ($entry in $ActiveEntries) {
        $activeWeight += [Math]::Min(
            $MaximumParallelism,
            (Get-DescriptorConcurrencyWeight -Descriptor $entry.Descriptor))
    }
    $descriptorWeight = [Math]::Min(
        $MaximumParallelism,
        (Get-DescriptorConcurrencyWeight -Descriptor $Descriptor))
    return (
        $activeWeight + $descriptorWeight) -le $MaximumParallelism
}

function Get-DeepValidationSmallClassBinCost {
    param(
        [Parameter(Mandatory)]
        [object]$Bin
    )

    $parallelizedCost = [Math]::Ceiling(
        $Bin.Weight / $DeepValidationInProcessParallelismEstimate)
    return [Math]::Max([int]$Bin.PeakWeight, [int]$parallelizedCost)
}

function New-BalancedBins {
    param(
        [Parameter(Mandatory)]
        [object[]]$Items,

        [Parameter(Mandatory)]
        [ValidateRange(1, 1000)]
        [int]$BinCount
    )

    $bins = for ($index = 0; $index -lt $BinCount; $index++) {
        [pscustomobject]@{
            Weight = 0
            PeakWeight = 0
            Cases = 0
            Items = [System.Collections.Generic.List[object]]::new()
        }
    }

    foreach ($item in $Items | Sort-Object Weight -Descending) {
        $bin = $bins | Sort-Object Weight | Select-Object -First 1
        [void]$bin.Items.Add($item)
        $bin.Weight += [int]$item.Weight
        $bin.PeakWeight = [Math]::Max($bin.PeakWeight, [int]$item.Weight)
        $bin.Cases += if ($null -ne $item.PSObject.Properties["Cases"]) {
            [int]$item.Cases
        }
        else {
            [int]$item.Weight
        }
    }

    return @($bins | Where-Object { $_.Items.Count -gt 0 })
}

function Join-TestFilter {
    param(
        [AllowNull()]
        [string]$SelectionFilter,

        [AllowNull()]
        [string]$CategoryFilter
    )

    if ([string]::IsNullOrWhiteSpace($SelectionFilter)) {
        return $CategoryFilter
    }
    if ([string]::IsNullOrWhiteSpace($CategoryFilter)) {
        return $SelectionFilter
    }
    return "($SelectionFilter)&($CategoryFilter)"
}

function New-RunDescriptor {
    param(
        [Parameter(Mandatory)]
        [string]$Phase,

        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [AllowNull()]
        [string]$TestFilter,

        [Parameter(Mandatory)]
        [string]$TrxFileName,

        [Parameter(Mandatory)]
        [int]$EstimatedCases,

        [Parameter(Mandatory)]
        [int]$EstimatedCost,

        [AllowNull()]
        [string]$SchedulingGroup,

        [ValidateRange(1, 8)]
        [int]$ConcurrencyWeight = 1
    )

    return [pscustomobject]@{
        Phase = $Phase
        Name = $Name
        Project = Get-ProjectDisplayPath -ProjectPath $ProjectPath
        ProjectPath = $ProjectPath
        Filter = $TestFilter
        EstimatedCases = $EstimatedCases
        EstimatedCost = $EstimatedCost
        SchedulingGroup = $SchedulingGroup
        ConcurrencyWeight = $ConcurrencyWeight
        Arguments = New-TestArguments `
            -ProjectPath $ProjectPath `
            -TrxFileName $TrxFileName `
            -TestFilter $TestFilter
    }
}

function New-SelectionRuns {
    param(
        [Parameter(Mandatory)]
        [object]$Selection,

        [Parameter(Mandatory)]
        [string]$Phase,

        [switch]$Balanced
    )

    $testCases = @(
        Get-DiscoveredTestCases `
            -ProjectPath $Selection.ProjectPath `
            -SelectionName $Selection.Name `
            -TestFilter $Selection.Filter
    )
    if (-not $Balanced) {
        return @(
            New-RunDescriptor `
                -Phase $Phase `
                -Name $Selection.Name `
                -ProjectPath $Selection.ProjectPath `
                -TestFilter $Selection.Filter `
                -TrxFileName "$($Selection.Name.ToLowerInvariant())-test-results.trx" `
                -EstimatedCases $testCases.Count `
                -EstimatedCost $testCases.Count
        )
    }

    $descriptors = [System.Collections.Generic.List[object]]::new()
    $guardianClass = "BookOfEternityClient.Tests.GuardianSystemRegressionTests"
    $guardianCases = @(
        $testCases |
            Where-Object ClassName -eq "BookOfEternityClient.Tests.GuardianSystemRegressionTests"
    )
    $baseCases = @($testCases | Where-Object ClassName -ne $guardianClass)
    $baseClassGroups = @($baseCases | Group-Object ClassName)
    $runIndex = 0

    foreach ($classGroup in $baseClassGroups | Where-Object Count -gt $LargeClassCaseTarget) {
        $classDurationCost = Get-IntegrationClassDurationCost `
            -LaneName $effectiveLane `
            -ClassName $classGroup.Name `
            -FallbackCost $classGroup.Count
        $classSchedulingGroup = if (
            $effectiveLane -eq "DeepValidation" -and
            $classGroup.Name -in $DeepValidationStorageHeavyClasses
        ) {
            $DeepValidationStorageHeavySchedulingGroup
        }
        else {
            $null
        }
        $classConcurrencyWeight = if (
            $effectiveLane -eq "DeepValidation" -and
            $DeepValidationClassConcurrencyWeights.ContainsKey($classGroup.Name)
        ) {
            [int]$DeepValidationClassConcurrencyWeights[$classGroup.Name]
        }
        else {
            1
        }
        $methodItems = @(
            $classGroup.Group |
                Group-Object MethodName |
                ForEach-Object {
                    [pscustomobject]@{
                        Weight = [Math]::Max(
                            1,
                            [Math]::Ceiling(
                                $_.Count * $classDurationCost / $classGroup.Count))
                        Cases = $_.Count
                        Selection = "FullyQualifiedName=$($_.Name)"
                    }
                }
        )
        $binCount = [Math]::Ceiling($classGroup.Count / $LargeClassCaseTarget)
        foreach ($bin in @(New-BalancedBins -Items $methodItems -BinCount $binCount)) {
            $runIndex++
            $selectionFilter = ($bin.Items | ForEach-Object Selection) -join "|"
            $testFilter = Join-TestFilter `
                -SelectionFilter $selectionFilter `
                -CategoryFilter $Selection.Filter
            [void]$descriptors.Add((
                New-RunDescriptor `
                    -Phase $Phase `
                    -Name "$($Selection.Name)-Base-$($classGroup.Name.Split('.')[-1])-$($runIndex.ToString('D2'))" `
                    -ProjectPath $Selection.ProjectPath `
                    -TestFilter $testFilter `
                    -TrxFileName "$($Selection.Name.ToLowerInvariant())-base-$($runIndex.ToString('D2')).trx" `
                    -EstimatedCases $bin.Cases `
                    -EstimatedCost $bin.Weight `
                    -SchedulingGroup $classSchedulingGroup `
                    -ConcurrencyWeight $classConcurrencyWeight
            ))
        }
    }

    $smallClassItems = @(
        $baseClassGroups |
            Where-Object Count -le $LargeClassCaseTarget |
            ForEach-Object {
                $durationCost = Get-IntegrationClassDurationCost `
                    -LaneName $effectiveLane `
                    -ClassName $_.Name `
                    -FallbackCost $_.Count
                [pscustomobject]@{
                    Weight = $durationCost
                    Cases = $_.Count
                    Selection = "FullyQualifiedName~$($_.Name)."
                    SchedulingGroup = if (
                        $effectiveLane -eq "DeepValidation" -and
                        $_.Name -in $DeepValidationStorageHeavyClasses
                    ) {
                        $DeepValidationStorageHeavySchedulingGroup
                    }
                    else {
                        $null
                    }
                    ConcurrencyWeight = if (
                        $effectiveLane -eq "DeepValidation" -and
                        $DeepValidationClassConcurrencyWeights.ContainsKey($_.Name)
                    ) {
                        [int]$DeepValidationClassConcurrencyWeights[$_.Name]
                    }
                    else {
                        1
                    }
                }
            }
    )
    $smallClassBinCount = if ($effectiveLane -eq "RegressionIntegration") {
        1
    }
    elseif ($effectiveLane -eq "DeepValidation") {
        $DeepValidationSmallClassBinCount
    }
    else {
        $ComposedSmallClassBinCount
    }
    foreach ($bin in @(
        New-BalancedBins -Items $smallClassItems -BinCount $smallClassBinCount
    )) {
        $runIndex++
        $selectionFilter = ($bin.Items | ForEach-Object Selection) -join "|"
        $testFilter = Join-TestFilter `
            -SelectionFilter $selectionFilter `
            -CategoryFilter $Selection.Filter
        $estimatedCost = if ($effectiveLane -eq "DeepValidation") {
            Get-DeepValidationSmallClassBinCost -Bin $bin
        }
        else {
            $bin.Weight
        }
        $schedulingGroups = @(
            $bin.Items |
                ForEach-Object SchedulingGroup |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                Sort-Object -Unique
        )
        if ($schedulingGroups.Count -gt 1) {
            throw "A balanced descriptor contains incompatible scheduling groups: $($schedulingGroups -join ', ')"
        }
        $schedulingGroup = if ($schedulingGroups.Count -eq 1) {
            $schedulingGroups[0]
        }
        else {
            $null
        }
        $concurrencyWeight = (
            $bin.Items |
                Measure-Object ConcurrencyWeight -Maximum
        ).Maximum
        [void]$descriptors.Add((
            New-RunDescriptor `
                -Phase $Phase `
                -Name "$($Selection.Name)-Base-mixed-$($runIndex.ToString('D2'))" `
                -ProjectPath $Selection.ProjectPath `
                -TestFilter $testFilter `
                -TrxFileName "$($Selection.Name.ToLowerInvariant())-base-$($runIndex.ToString('D2')).trx" `
                -EstimatedCases $bin.Cases `
                -EstimatedCost $estimatedCost `
                -SchedulingGroup $schedulingGroup `
                -ConcurrencyWeight $concurrencyWeight
        ))
    }

    if ($guardianCases.Count -gt 0) {
        $guardianSourceRoot = Split-Path -Parent $Selection.ProjectPath
        $guardianShards = [ordered]@{
            AcceptedAuthority = @("GuardianSystemRegressionTests.AcceptedAuthority.cs")
            ActorBrain = @("GuardianSystemRegressionTests.ActorBrain.cs")
            Lifecycle = @(
                "GuardianSystemRegressionTests.IdleValidation.cs",
                "GuardianSystemRegressionTests.LifecycleSnapshots.cs"
            )
            MortalFactPersistence = @("MortalFactPersistenceValidationTests.cs")
            PowerJournalOfferings = @("GuardianSystemRegressionTests.PowerJournalOfferings.cs")
            ProjectsPower = @("GuardianSystemRegressionTests.ProjectsPower.cs")
            QuestProgress = @("GuardianSystemRegressionTests.QuestProgress.cs")
            RivalResidents = @("GuardianSystemRegressionTests.RivalResidents.cs")
            TradeOfferingResonance = @("GuardianSystemRegressionTests.TradeOfferingResonance.cs")
        }

        $guardianCaseGroups = @($guardianCases | Group-Object MethodName)
        $guardianCaseCounts = [System.Collections.Generic.Dictionary[string, int]]::new(
            [StringComparer]::Ordinal)
        foreach ($methodGroup in $guardianCaseGroups) {
            $guardianCaseCounts.Add($methodGroup.Name, $methodGroup.Count)
        }

        $listedFiles = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::Ordinal)
        $sourceMethods = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::Ordinal)
        $selectedGuardianMethods = [System.Collections.Generic.HashSet[string]]::new(
            [StringComparer]::Ordinal)
        $guardianRunIndex = 0

        foreach ($shard in $guardianShards.GetEnumerator()) {
            foreach ($sourceFile in $shard.Value) {
                if (-not $listedFiles.Add($sourceFile)) {
                    throw "Guardian shard source is listed more than once: $sourceFile"
                }
            }

            $methodNames = @(
                Get-GuardianTestMethods `
                    -SourceRoot $guardianSourceRoot `
                    -SourceFiles $shard.Value
            )
            if ($methodNames.Count -eq 0) {
                throw "Guardian shard '$($shard.Key)' contains no Fact/Theory methods."
            }

            $methodItems = [System.Collections.Generic.List[object]]::new()
            foreach ($methodName in $methodNames) {
                $fullyQualifiedMethod = "$guardianClass.$methodName"
                if (-not $sourceMethods.Add($fullyQualifiedMethod)) {
                    throw "Guardian test method is assigned to more than one source shard: $fullyQualifiedMethod"
                }
                if (-not $guardianCaseCounts.ContainsKey($fullyQualifiedMethod)) {
                    continue
                }
                [void]$selectedGuardianMethods.Add($fullyQualifiedMethod)
                [void]$methodItems.Add([pscustomobject]@{
                    Weight = $guardianCaseCounts[$fullyQualifiedMethod]
                    Cases = $guardianCaseCounts[$fullyQualifiedMethod]
                    Selection = "FullyQualifiedName=$fullyQualifiedMethod"
                })
            }

            if ($methodItems.Count -eq 0) {
                continue
            }

            $domainCaseCount = ($methodItems | Measure-Object Weight -Sum).Sum
            $domainCaseTarget = if ($effectiveLane -eq "DeepValidation") {
                $DeepValidationGuardianCaseTarget
            }
            else {
                60
            }
            $domainBinCount = [Math]::Max(
                1,
                [Math]::Ceiling($domainCaseCount / $domainCaseTarget))
            $domainChunkIndex = 0
            foreach ($bin in @(
                New-BalancedBins -Items @($methodItems) -BinCount $domainBinCount
            )) {
                $runIndex++
                $guardianRunIndex++
                $domainChunkIndex++
                $selectionFilter = ($bin.Items | ForEach-Object Selection) -join "|"
                $testFilter = Join-TestFilter `
                    -SelectionFilter $selectionFilter `
                    -CategoryFilter $Selection.Filter
                $estimatedCost = if ($effectiveLane -in @(
                    "DeepValidation",
                    "RegressionIntegration"
                )) {
                    Get-RetainedGuardianShardDurationCost `
                        -ShardName $shard.Key `
                        -FallbackCost ($bin.Weight * 4) `
                        -ChunkCount $domainBinCount
                }
                else {
                    $bin.Weight * 4
                }
                [void]$descriptors.Add((
                    New-RunDescriptor `
                        -Phase $Phase `
                        -Name "$($Selection.Name)-Guardian-$($shard.Key)-$($domainChunkIndex.ToString('D2'))" `
                        -ProjectPath $Selection.ProjectPath `
                        -TestFilter $testFilter `
                        -TrxFileName "$($Selection.Name.ToLowerInvariant())-guardian-$($guardianRunIndex.ToString('D2')).trx" `
                        -EstimatedCases $bin.Cases `
                        -EstimatedCost $estimatedCost
                ))
            }
        }

        $unassignedGuardianMethods = @(
            $guardianCaseGroups |
                Where-Object { -not $selectedGuardianMethods.Contains($_.Name) } |
                Select-Object -ExpandProperty Name
        )
        if ($unassignedGuardianMethods.Count -gt 0) {
            throw "Discovered Guardian methods are not assigned to a shard: $($unassignedGuardianMethods -join ', ')"
        }

        $unlistedTestSources = @(
            Get-ChildItem -LiteralPath $guardianSourceRoot -Filter "*.cs" |
                Where-Object {
                    $source = Get-Content -LiteralPath $_.FullName -Raw
                    $source -match 'partial\s+class\s+GuardianSystemRegressionTests' -and
                    $source -match '(?m)^\s*\[(Fact|Theory)(Attribute)?(\(|\])' -and
                    -not $listedFiles.Contains($_.Name)
                } |
                Select-Object -ExpandProperty Name
        )
        if ($unlistedTestSources.Count -gt 0) {
            throw "Guardian test source is not assigned to a shard: $($unlistedTestSources -join ', ')"
        }
    }

    return @(
        $descriptors |
            Sort-Object `
                @{ Expression = "ConcurrencyWeight"; Descending = $true },
                @{ Expression = "EstimatedCost"; Descending = $true },
                @{ Expression = "Name"; Descending = $false }
    )
}

function New-TestRuns {
    if ($effectiveLane -eq "PreMerge") {
        $preMergeParallelSelections = @(
            [pscustomobject]@{
                Name = "Fast"
                ProjectPath = $fastTestProject
                Filter = $null
            },
            [pscustomobject]@{
                Name = "Integration"
                ProjectPath = $integrationTestProject
                Filter = $coreIntegrationFilter
            }
        )
        $preMergeExclusiveSelections = @(
            [pscustomobject]@{
                Name = "ProcessIntegration"
                ProjectPath = $integrationTestProject
                Filter = "Category=ProcessIntegration&Category!=E2E"
            },
            [pscustomobject]@{
                Name = "E2E"
                ProjectPath = $integrationTestProject
                Filter = "Category=E2E"
            }
        )

        $preMergeRuns = [System.Collections.Generic.List[object]]::new()
        foreach ($selection in $preMergeParallelSelections) {
            foreach ($descriptor in @(
                New-SelectionRuns `
                    -Selection $selection `
                    -Phase "Parallel" `
                    -Balanced
            )) {
                [void]$preMergeRuns.Add($descriptor)
            }
        }
        foreach ($selection in $preMergeExclusiveSelections) {
            foreach ($descriptor in @(
                New-SelectionRuns `
                    -Selection $selection `
                    -Phase $selection.Name
            )) {
                [void]$preMergeRuns.Add($descriptor)
            }
        }
        return @($preMergeRuns)
    }

    $projectPath = if ($selectedProject -eq "Fast") {
        $fastTestProject
    }
    else {
        $integrationTestProject
    }
    $selection = [pscustomobject]@{
        Name = $effectiveLane
        ProjectPath = $projectPath
        Filter = $laneFilter
    }
    $balanced = $effectiveLane -in @(
        "Fast",
        "FullValidation",
        "RegressionIntegration",
        "DeepValidation"
    )
    return @(
        New-SelectionRuns `
            -Selection $selection `
            -Phase $effectiveLane `
            -Balanced:$balanced
    )
}

function Invoke-DescriptorBatch {
    param(
        [Parameter(Mandatory)]
        [object[]]$Descriptors,

        [Parameter(Mandatory)]
        [ValidateRange(1, 8)]
        [int]$MaximumParallelism,

        [ValidateRange(0, 8)]
        [int]$MaximumFastParallelism = 0
    )

    $pending = [System.Collections.Generic.List[object]]::new()
    foreach ($descriptor in $Descriptors) {
        [void]$pending.Add($descriptor)
    }
    $active = [System.Collections.Generic.List[object]]::new()

    while ($pending.Count -gt 0 -or $active.Count -gt 0) {
        if ([DateTime]::UtcNow -ge $deadlineUtc) {
            $script:timedOut = $true
            throw "Lane '$Lane' exceeded its $effectiveTimeoutMinutes minute timeout."
        }

        while ($pending.Count -gt 0 -and $active.Count -lt $MaximumParallelism) {
            $activeFastCount = @(
                $active |
                    Where-Object {
                        [StringComparer]::OrdinalIgnoreCase.Equals(
                            $_.Descriptor.ProjectPath,
                            $fastTestProject)
                    }
            ).Count
            $descriptor = $pending |
                Where-Object {
                    $MaximumFastParallelism -eq 0 -or
                        -not [StringComparer]::OrdinalIgnoreCase.Equals(
                            $_.ProjectPath,
                            $fastTestProject) -or
                        $activeFastCount -lt $MaximumFastParallelism
                } |
                Where-Object {
                    Test-DescriptorCapacityAvailable `
                        -Descriptor $_ `
                        -ActiveEntries @($active) `
                        -MaximumParallelism $MaximumParallelism
                } |
                Where-Object {
                    Test-DescriptorSchedulingGroupAvailable `
                        -Descriptor $_ `
                        -ActiveEntries @($active)
                } |
                Select-Object -First 1
            if ($null -eq $descriptor) {
                break
            }

            [void]$pending.Remove($descriptor)
            $startParameters = @{
                Name = $descriptor.Name
                Arguments = $descriptor.Arguments
            }
            if ($null -ne $descriptor.PSObject.Properties["FileName"]) {
                $startParameters.FileName = $descriptor.FileName
            }
            $run = Start-OwnedProcess @startParameters
            [void]$active.Add([pscustomobject]@{
                Descriptor = $descriptor
                Run = $run
            })
        }

        $completed = @($active | Where-Object { $_.Run.Process.HasExited })
        if ($completed.Count -eq 0) {
            Start-Sleep -Milliseconds 200
            continue
        }

        foreach ($entry in $completed) {
            if (-not (Wait-ForOwnedProcess -Run $entry.Run)) {
                $script:timedOut = $true
                throw "Lane '$Lane' exceeded its $effectiveTimeoutMinutes minute timeout."
            }
            [void]$active.Remove($entry)
            if ($entry.Run.ExitCode -ne 0) {
                $script:exitCode = $entry.Run.ExitCode
                throw "Test run '$($entry.Run.Name)' failed with exit code $($entry.Run.ExitCode)."
            }
        }
    }
}

function Get-TrxSummary {
    param(
        [string]$TrxDirectory = $resultDirectory
    )

    $counters = @{
        Total = 0
        Executed = 0
        Passed = 0
        Failed = 0
    }
    $testOccurrences = [System.Collections.Generic.List[object]]::new()
    $parseErrors = [System.Collections.Generic.List[string]]::new()

    foreach ($trxFile in @(
        Get-ChildItem -LiteralPath $TrxDirectory -Filter "*.trx" -File
    )) {
        try {
            [xml]$trx = Get-Content -LiteralPath $trxFile.FullName -Raw
            $trxCounters = $trx.SelectSingleNode("//*[local-name()='Counters']")
            if ($null -ne $trxCounters) {
                foreach ($property in @("Total", "Executed", "Passed", "Failed")) {
                    $attributeName = $property.ToLowerInvariant()
                    $counters[$property] += [int]$trxCounters.GetAttribute($attributeName)
                }
            }

            $storageByTestId = [System.Collections.Generic.Dictionary[string, string]]::new(
                [StringComparer]::Ordinal)
            foreach ($unitTest in @(
                $trx.SelectNodes("//*[local-name()='UnitTest']")
            )) {
                $testId = $unitTest.GetAttribute("id")
                $storage = $unitTest.GetAttribute("storage")
                if ([string]::IsNullOrWhiteSpace($testId) -or
                    [string]::IsNullOrWhiteSpace($storage) -or
                    $storageByTestId.ContainsKey($testId)) {
                    continue
                }
                $storageByTestId.Add(
                    $testId,
                    [System.IO.Path]::GetFileName($storage).ToLowerInvariant())
            }

            $seenInTrx = [System.Collections.Generic.HashSet[string]]::new(
                [StringComparer]::Ordinal)
            foreach ($result in @(
                $trx.SelectNodes("//*[local-name()='UnitTestResult']")
            )) {
                $testId = $result.GetAttribute("testId")
                if ([string]::IsNullOrWhiteSpace($testId)) {
                    continue
                }
                if (-not $storageByTestId.ContainsKey($testId)) {
                    throw (
                        "UnitTestResult testId '$testId' in '$($trxFile.Name)' " +
                        "has no UnitTest storage mapping.")
                }
                $storage = $storageByTestId[$testId]
                $key = "$storage::$testId"
                if ($seenInTrx.Add($key)) {
                    [void]$testOccurrences.Add([pscustomobject]@{
                        Key = $key
                        TestId = $testId
                        TrxFile = $trxFile.Name
                    })
                }
            }
        }
        catch {
            [void]$parseErrors.Add("$($trxFile.Name): $($_.Exception.Message)")
        }
    }

    $duplicateTests = @(
        $testOccurrences |
            Group-Object Key |
            Where-Object Count -gt 1 |
            ForEach-Object { $_.Group[0] } |
            Select-Object -ExpandProperty TestId -Unique
    )
    return [pscustomobject]@{
        Total = $counters.Total
        Executed = $counters.Executed
        Passed = $counters.Passed
        Failed = $counters.Failed
        DuplicateTests = @($duplicateTests)
        ParseErrors = @($parseErrors)
    }
}

function New-ExitedRootDescendantProbe {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [switch]$RedirectDescendantOutput
    )

    $pwshCommand = @(
        Get-Command -Name "pwsh" -CommandType Application -ErrorAction Stop
    )[0]
    $probeToken = [Guid]::NewGuid().ToString("N")
    $descendantPidPath = Join-Path `
        $resultDirectory `
        "$Name-$probeToken-descendant.pid"
    $descendantOutputPath = Join-Path `
        $resultDirectory `
        "$Name-$probeToken-descendant.stdout.log"
    $descendantErrorPath = Join-Path `
        $resultDirectory `
        "$Name-$probeToken-descendant.stderr.log"
    $descendantScriptPath = Join-Path `
        $resultDirectory `
        "$Name-$probeToken-descendant-sleep.ps1"
    $rootScriptPath = Join-Path `
        $resultDirectory `
        "$Name-$probeToken-exited-root.ps1"
    Set-Content -LiteralPath $descendantScriptPath -Value @'
Start-Sleep -Seconds 30
'@
    Set-Content -LiteralPath $rootScriptPath -Value @'
param(
    [Parameter(Mandatory)]
    [string]$PwshPath,

    [Parameter(Mandatory)]
    [string]$DescendantScriptPath,

    [Parameter(Mandatory)]
    [string]$DescendantPidPath,

    [Parameter(Mandatory)]
    [string]$DescendantOutputPath,

    [Parameter(Mandatory)]
    [string]$DescendantErrorPath,

    [switch]$RedirectDescendantOutput
)

$startParameters = @{
    FilePath = $PwshPath
    ArgumentList = @("-NoProfile", "-File", "`"$DescendantScriptPath`"")
    PassThru = $true
    WindowStyle = "Hidden"
}
if ($RedirectDescendantOutput) {
    $startParameters.RedirectStandardOutput = $DescendantOutputPath
    $startParameters.RedirectStandardError = $DescendantErrorPath
}
$child = Start-Process @startParameters
Set-Content -LiteralPath $DescendantPidPath -Value $child.Id
'@

    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($argument in @(
        "-NoProfile",
        "-File",
        $rootScriptPath,
        "-PwshPath",
        $pwshCommand.Path,
        "-DescendantScriptPath",
        $descendantScriptPath,
        "-DescendantPidPath",
        $descendantPidPath,
        "-DescendantOutputPath",
        $descendantOutputPath,
        "-DescendantErrorPath",
        $descendantErrorPath
    )) {
        [void]$arguments.Add($argument)
    }
    if ($RedirectDescendantOutput) {
        [void]$arguments.Add("-RedirectDescendantOutput")
    }

    return [pscustomobject]@{
        PwshPath = $pwshCommand.Path
        Arguments = @($arguments)
        DescendantPidPath = $descendantPidPath
    }
}

function Test-ExactProcessAlive {
    param(
        [Parameter(Mandatory)]
        [int]$ProcessId
    )

    try {
        $process = [System.Diagnostics.Process]::GetProcessById($ProcessId)
        try {
            return -not $process.HasExited
        }
        finally {
            $process.Dispose()
        }
    }
    catch [ArgumentException] {
        return $false
    }
}

try {
    if ($isSelfTest) {
        switch ($SelfTest) {
            "NpmStartup" {
                $npmCommandPath = Resolve-NpmCommandPath
                $npmProbe = Start-OwnedProcess `
                    -Name "Npm-startup-probe" `
                    -FileName $npmCommandPath `
                    -Arguments @("--version")
                Invoke-OwnedPhase `
                    -Run $npmProbe `
                    -TimeoutMessage "npm startup probe exceeded the lane deadline." `
                    -FailureDescription "npm startup probe"
            }
            "TrxSummary" {
                if ([string]::IsNullOrWhiteSpace($SelfTestTrxDirectory) -or
                    -not (Test-Path -LiteralPath $SelfTestTrxDirectory -PathType Container)) {
                    throw "TrxSummary self-test requires an existing -SelfTestTrxDirectory."
                }
                $trxSummaryOverride = Get-TrxSummary `
                    -TrxDirectory ([System.IO.Path]::GetFullPath($SelfTestTrxDirectory))
                if ($trxSummaryOverride.ParseErrors.Count -ne 0) {
                    $exitCode = 1
                    throw "TRX parsing failed: $($trxSummaryOverride.ParseErrors -join '; ')"
                }
                if ($trxSummaryOverride.DuplicateTests.Count -ne 0) {
                    $exitCode = 1
                    throw "TRX summary self-test found duplicate TRX test IDs: $($trxSummaryOverride.DuplicateTests -join ', ')"
                }
            }
            "DurationSchedule" {
                $probeItems = @(
                    [pscustomobject]@{
                        Weight = 542
                        Cases = 2
                        Selection = "heavy"
                    },
                    [pscustomobject]@{
                        Weight = 399
                        Cases = 58
                        Selection = "medium"
                    },
                    [pscustomobject]@{
                        Weight = 100
                        Cases = 45
                        Selection = "light"
                    }
                )
                $probeBins = @(
                    New-BalancedBins -Items $probeItems -BinCount 2
                )
                $caseCount = (
                    $probeBins |
                        Measure-Object Cases -Sum
                ).Sum
                $rankedBins = @(
                    $probeBins |
                        ForEach-Object {
                            [pscustomobject]@{
                                Bin = $_
                                Cost = Get-DeepValidationSmallClassBinCost -Bin $_
                            }
                        } |
                        Sort-Object Cost -Descending
                )
                $maxCost = $rankedBins[0].Cost
                $firstSelection = (
                    $rankedBins[0].Bin.Items |
                        Sort-Object Weight -Descending |
                        Select-Object -First 1
                ).Selection
                $exclusiveDescriptor = [pscustomobject]@{
                    SchedulingGroup = "storage-heavy"
                }
                $exclusiveActive = @(
                    [pscustomobject]@{
                        Descriptor = [pscustomobject]@{
                            SchedulingGroup = "storage-heavy"
                        }
                    }
                )
                $sameGroupAvailable =
                    Test-DescriptorSchedulingGroupAvailable `
                        -Descriptor $exclusiveDescriptor `
                        -ActiveEntries $exclusiveActive
                $otherGroupAvailable =
                    Test-DescriptorSchedulingGroupAvailable `
                        -Descriptor ([pscustomobject]@{
                            SchedulingGroup = "other"
                        }) `
                        -ActiveEntries $exclusiveActive
                $exclusive = -not $sameGroupAvailable -and $otherGroupAvailable
                $weightedDescriptor = [pscustomobject]@{
                    ConcurrencyWeight = 2
                }
                $twoActiveSlots = @(
                    1..2 |
                        ForEach-Object {
                            [pscustomobject]@{
                                Descriptor = [pscustomobject]@{
                                    ConcurrencyWeight = 1
                                }
                            }
                        }
                )
                $threeActiveSlots = @(
                    1..3 |
                        ForEach-Object {
                            [pscustomobject]@{
                                Descriptor = [pscustomobject]@{
                                    ConcurrencyWeight = 1
                                }
                            }
                        }
                )
                $weighted =
                    (Test-DescriptorCapacityAvailable `
                        -Descriptor $weightedDescriptor `
                        -ActiveEntries $twoActiveSlots `
                        -MaximumParallelism 4) -and
                    -not (Test-DescriptorCapacityAvailable `
                        -Descriptor $weightedDescriptor `
                        -ActiveEntries $threeActiveSlots `
                        -MaximumParallelism 4)
                $bounded = Test-DescriptorCapacityAvailable `
                    -Descriptor $weightedDescriptor `
                    -ActiveEntries @() `
                    -MaximumParallelism 1
                $regression =
                    (Get-IntegrationClassDurationCost `
                        -LaneName "RegressionIntegration" `
                        -ClassName "BookOfEternityClient.Tests.BrowserCommandPresentationAuditTests" `
                        -FallbackCost 166) -eq 339 -and
                    (Get-RetainedGuardianShardDurationCost `
                        -ShardName "TradeOfferingResonance" `
                        -FallbackCost 160 `
                        -ChunkCount 2) -eq 215
                $preMerge =
                    (Get-IntegrationClassDurationCost `
                        -LaneName "PreMerge" `
                        -ClassName "BookOfEternityClient.Tests.BrowserCommandPresentationAuditTests" `
                        -FallbackCost 166) -eq 339 -and
                    (Get-IntegrationClassDurationCost `
                        -LaneName "PreMerge" `
                        -ClassName "BookOfEternityClient.Tests.AfterlifeSpiritualConflictValidationTests" `
                        -FallbackCost 10) -eq 10
                if ($caseCount -ne 105 -or
                    $maxCost -ne 542 -or
                    $firstSelection -ne "heavy" -or
                    -not $exclusive -or
                    -not $weighted -or
                    -not $bounded -or
                    -not $regression -or
                    -not $preMerge) {
                    throw "Duration schedule did not preserve case counts or long-first order."
                }
                Write-Host (
                    "DURATION-SCHEDULE cases=$caseCount; " +
                    "maxCost=$maxCost; first=$firstSelection; " +
                    "exclusive=$exclusive; weighted=$weighted; " +
                    "bounded=$bounded; regression=$regression; " +
                    "preMerge=$preMerge")
            }
            "ResultDirectory" {
                Add-Content -LiteralPath $logPath -Value (
                    "Result-directory self-test: $resultDirectory")
            }
            "OwnedPostStartFailure" {
                $pwshCommand = @(
                    Get-Command -Name "pwsh" -CommandType Application -ErrorAction Stop
                )[0]
                $savedLogPath = $logPath
                try {
                    $logPath = $resultDirectory
                    [void](Start-OwnedProcess `
                        -Name "Post-start-failure-probe" `
                        -FileName $pwshCommand.Path `
                        -Arguments @(
                            "-NoProfile",
                            "-Command",
                            "Start-Sleep -Seconds 30"
                        ) `
                        -Quiet)
                }
                catch {
                    Add-Content -LiteralPath $savedLogPath -Value (
                        "Expected post-start failure: $($_.Exception.Message)")
                }
                finally {
                    $logPath = $savedLogPath
                }

                if ($null -eq $lastPostStartCleanup) {
                    throw "Post-start failure probe did not exercise initialization cleanup."
                }
                if (-not $lastPostStartCleanup.Killed -or
                    -not $lastPostStartCleanup.Disposed -or
                    $lastPostStartCleanup.Registered) {
                    throw "Post-start failure probe did not clean up the owned process."
                }
                Write-Host (
                    "Post-start cleanup: killed=$($lastPostStartCleanup.Killed) " +
                    "disposed=$($lastPostStartCleanup.Disposed) " +
                    "registered=$($lastPostStartCleanup.Registered)")
            }
            "OwnedPostStartCleanupRetry" {
                $pwshCommand = @(
                    Get-Command -Name "pwsh" -CommandType Application -ErrorAction Stop
                )[0]
                $savedLogPath = $logPath
                $caughtFailure = $null
                try {
                    $logPath = $resultDirectory
                    [void](Start-OwnedProcess `
                        -Name "Post-start-cleanup-retry-probe" `
                        -FileName $pwshCommand.Path `
                        -Arguments @(
                            "-NoProfile",
                            "-Command",
                            "Start-Sleep -Seconds 30"
                        ) `
                        -Quiet `
                        -SimulateInitialCleanupFailure)
                }
                catch {
                    $caughtFailure = $_
                    Add-Content -LiteralPath $savedLogPath -Value @(
                        "Expected combined post-start failure:"
                        $_.Exception.ToString()
                    )
                }
                finally {
                    $logPath = $savedLogPath
                }

                if ($null -eq $lastPostStartCleanup -or
                    $null -eq $caughtFailure) {
                    throw "Post-start cleanup-retry probe did not exercise the failure path."
                }
                $aggregateFailure = $caughtFailure.Exception -as [AggregateException]
                $lastPostStartCleanup.ErrorsPreserved =
                    $null -ne $aggregateFailure -and
                    $aggregateFailure.InnerExceptions.Count -eq 2 -and
                    -not [string]::IsNullOrWhiteSpace(
                        $aggregateFailure.InnerExceptions[0].Message) -and
                    $aggregateFailure.InnerExceptions[1].Message -eq
                        "Simulated initial cleanup failure."
                if ($lastPostStartCleanup.InitialCleanupSucceeded -or
                    -not $lastPostStartCleanup.Registered -or
                    $lastPostStartCleanup.Disposed -or
                    -not $lastPostStartCleanup.ErrorsPreserved) {
                    throw "Failed initial cleanup was not retained for finalizer retry."
                }

                $lastPostStartCleanup.Run.SimulateFinalizerStopFailureOnce =
                    $true
            }
            "OwnedExitedRootDescendant" {
                if (-not $IsWindows) {
                    throw "OwnedExitedRootDescendant requires Windows Job Objects."
                }

                $probe = New-ExitedRootDescendantProbe `
                    -Name "direct" `
                    -RedirectDescendantOutput

                $rootProbe = Start-OwnedProcess `
                    -Name "Exited-root-descendant-probe" `
                    -FileName $probe.PwshPath `
                    -Arguments $probe.Arguments `
                    -Quiet

                while (-not $rootProbe.Process.HasExited -or
                    -not (Test-Path `
                        -LiteralPath $probe.DescendantPidPath `
                        -PathType Leaf)) {
                    if ([DateTime]::UtcNow -ge $deadlineUtc) {
                        throw "Exited-root descendant probe exceeded the lane deadline."
                    }
                    [void]$rootProbe.Process.WaitForExit(25)
                }
                $descendantPid = [int](
                    Get-Content `
                        -LiteralPath $probe.DescendantPidPath `
                        -Raw).Trim()
                $descendantObservedAlive =
                    Test-ExactProcessAlive -ProcessId $descendantPid

                if (-not $rootProbe.Process.HasExited -or
                    -not $descendantObservedAlive) {
                    throw (
                        "Exited-root descendant probe did not establish the " +
                        "required root-exited/child-live precondition.")
                }
                $script:lastExitedRootDescendant = [pscustomobject]@{
                    ProcessId = $descendantPid
                    RootExitedBeforeCleanup = $rootProbe.Process.HasExited
                    ObservedAliveBeforeCleanup = $descendantObservedAlive
                }
                Write-Host (
                    "Exited-root descendant: pid=$descendantPid " +
                    "rootExited=True observedAlive=True")
            }
            "OwnedBatchExitedRootDescendant" {
                if (-not $IsWindows) {
                    throw (
                        "OwnedBatchExitedRootDescendant requires Windows Job Objects.")
                }

                $probe = New-ExitedRootDescendantProbe -Name "batch"
                $descriptorName = "Batch-exited-root-descendant-probe"
                $descriptor = [pscustomobject]@{
                    Name = $descriptorName
                    ProjectPath = $integrationTestProject
                    FileName = $probe.PwshPath
                    Arguments = $probe.Arguments
                }
                Invoke-DescriptorBatch `
                    -Descriptors @($descriptor) `
                    -MaximumParallelism 1

                $batchRun = @(
                    $allRuns |
                        Where-Object Name -eq $descriptorName
                ) | Select-Object -First 1
                if ($null -eq $batchRun -or
                    -not $batchRun.Process.HasExited -or
                    -not $batchRun.ContainmentObservedNonEmptyAfterRootExit) {
                    throw (
                        "Parallel batch did not observe the required " +
                        "root-exited/child-live containment state.")
                }
                if (-not (Test-Path `
                    -LiteralPath $probe.DescendantPidPath `
                    -PathType Leaf)) {
                    throw "Parallel batch descendant did not publish its PID."
                }
                $descendantPid = [int](
                    Get-Content `
                        -LiteralPath $probe.DescendantPidPath `
                        -Raw).Trim()
                $script:lastExitedRootDescendant = [pscustomobject]@{
                    ProcessId = $descendantPid
                    RootExitedBeforeCleanup = $batchRun.Process.HasExited
                    ObservedAliveBeforeCleanup =
                        $batchRun.ContainmentObservedNonEmptyAfterRootExit
                }
                Write-Host (
                    "Batch exited-root descendant: pid=$descendantPid " +
                    "rootExited=True observedAlive=True")
            }
        }
    }
    else {
        if ($Lane -eq "Focused" -and [string]::IsNullOrWhiteSpace($Filter)) {
            throw "Lane Focused requires -Filter with a VSTest filter expression."
        }
        if ($Lane -ne "Focused" -and -not [string]::IsNullOrWhiteSpace($Filter)) {
            throw "-Filter is supported only with -Lane Focused."
        }
        if ($PSBoundParameters.ContainsKey("FocusedProject") -and
            $Lane -ne "Focused") {
            throw "-FocusedProject is supported only with -Lane Focused."
        }
        if ($TimeoutMinutes -gt [int]$laneDefinition.TimeoutMinutes) {
            throw "Lane '$Lane' has a hard limit of $($laneDefinition.TimeoutMinutes) minute(s)."
        }

        $laneFilter = if ($Lane -eq "Focused") { $Filter } else { $laneDefinition.Filter }
        Add-Content -LiteralPath $logPath -Value (
            "ValidatedFilter: $(if ([string]::IsNullOrWhiteSpace($laneFilter)) { "<none>" } else { $laneFilter })")

        if (-not $PlanOnly) {
        if ($effectiveLane -in @("E2E", "PreMerge")) {
            $npmCommandPath = Resolve-NpmCommandPath
            $frontendRun = Start-OwnedProcess `
                -Name "Frontend-verify" `
                -FileName $npmCommandPath `
                -Arguments @("run", "verify", "--prefix", "BookOfEternityClient.WebFrontend")
            Invoke-OwnedPhase `
                -Run $frontendRun `
                -TimeoutMessage "Frontend verification exceeded the lane deadline." `
                -FailureDescription "Frontend verification"
        }

        if (-not $NoBuild) {
            $buildSelections = if ($selectedProject -eq "Both") {
                @(
                    [pscustomobject]@{
                        Name = "Build-Fast"
                        ProjectPath = $fastTestProject
                    },
                    [pscustomobject]@{
                        Name = "Build-Integration"
                        ProjectPath = $integrationTestProject
                    }
                )
            }
            elseif ($selectedProject -eq "Fast") {
                @(
                    [pscustomobject]@{
                        Name = "Build-Fast"
                        ProjectPath = $fastTestProject
                    }
                )
            }
            else {
                @(
                    [pscustomobject]@{
                        Name = "Build-Integration"
                        ProjectPath = $integrationTestProject
                    }
                )
            }

            foreach ($buildSelection in $buildSelections) {
                $buildRun = Start-OwnedProcess `
                    -Name $buildSelection.Name `
                    -Arguments @(
                        "build",
                        $buildSelection.ProjectPath,
                        "--no-restore",
                        "--verbosity",
                        "minimal"
                    )
                Invoke-OwnedPhase `
                    -Run $buildRun `
                    -TimeoutMessage "$($buildSelection.Name) exceeded the lane deadline." `
                    -FailureDescription $buildSelection.Name
            }
        }
    }

    $testRuns = @(New-TestRuns)
    if ($PlanOnly) {
        $planRows = @(
            $testRuns |
                Select-Object Phase, Name, Project, Filter, EstimatedCases, EstimatedCost,
                    SchedulingGroup, ConcurrencyWeight
        )
        $planLines = [System.Collections.Generic.List[string]]::new()
        [void]$planLines.Add("PLAN-BEGIN EffectiveLane=$effectiveLane")
        foreach ($planRow in $planRows) {
            [void]$planLines.Add(
                "PLAN $($planRow | ConvertTo-Json -Compress -Depth 3)")
        }
        [void]$planLines.Add("PLAN-END EffectiveLane=$effectiveLane")
        Add-Content -LiteralPath $logPath -Value @("", "Execution plan", @($planLines))
        $planLines | Write-Host
    }
    else {
        if ($effectiveLane -eq "PreMerge") {
            $parallelRuns = @($testRuns | Where-Object Phase -eq "Parallel")
            Invoke-DescriptorBatch `
                -Descriptors $parallelRuns `
                -MaximumParallelism ([Math]::Min($Parallelism, $PreMergeParallelism)) `
                -MaximumFastParallelism $PreMergeFastParallelismLimit

            foreach ($phase in @("ProcessIntegration", "E2E")) {
                $exclusiveRuns = @($testRuns | Where-Object Phase -eq $phase)
                Invoke-DescriptorBatch `
                    -Descriptors $exclusiveRuns `
                    -MaximumParallelism 1
            }
        }
        else {
            $effectiveParallelism = if ($effectiveLane -eq "Fast") {
                [Math]::Min($Parallelism, $FastParallelismLimit)
            }
            elseif ($effectiveLane -eq "DeepValidation") {
                [Math]::Min($Parallelism, $PreMergeParallelism)
            }
            elseif ($effectiveLane -eq "LifecycleIntegration") {
                1
            }
            elseif ($effectiveLane -in @(
                "FullValidation",
                "RegressionIntegration"
            )) {
                $Parallelism
            }
            else {
                1
            }
            Invoke-DescriptorBatch `
                -Descriptors $testRuns `
                -MaximumParallelism $effectiveParallelism
        }

        $runSummary = Get-TrxSummary
        if ($runSummary.ParseErrors.Count -ne 0) {
            $exitCode = 1
            throw "TRX parsing failed: $($runSummary.ParseErrors -join '; ')"
        }
        if ($runSummary.Total -eq 0) {
            $exitCode = 1
            throw "Lane '$Lane' produced no discovered test results."
        }
        $isComposedCoverageLane = $effectiveLane -in @(
            "PreMerge",
            "DeepValidation"
        )
        if ($isComposedCoverageLane -and $runSummary.DuplicateTests.Count -ne 0) {
            $exitCode = 1
            throw "$effectiveLane produced duplicate TRX test IDs: " +
                "$($runSummary.DuplicateTests -join ', ')"
        }
        $minimumCases = switch ($effectiveLane) {
            "PreMerge" { $PreMergeMinimumCases }
            "DeepValidation" { $DeepValidationMinimumCases }
            "LifecycleIntegration" { $LifecycleIntegrationMinimumCases }
            default { 0 }
        }
        if ($minimumCases -gt 0 -and $runSummary.Total -lt $minimumCases) {
            $exitCode = 1
            throw "$effectiveLane produced $($runSummary.Total) cases; " +
                "expected at least the $minimumCases-case reviewed baseline."
        }
    }
    }
}
catch {
    if ($exitCode -eq 0) {
        $exitCode = if ($timedOut) { 124 } else { 1 }
    }
    $failureMessage = $_.Exception.Message
}
finally {
    foreach ($run in @($allRuns)) {
        $ownedProcessId = $run.Process.Id
        $processExited = $false
        $containmentEmpty = $false
        $finalizationSucceeded = $true
        $disposed = $false
        $cleanupPasses = 0
        $stopAttempts = 0
        $firstStopFailed = $false
        $finalizerErrors = [System.Collections.Generic.List[string]]::new()
        $isPostStartRetry =
            $run.PostStartInitializationFailed -and
            $null -ne $lastPostStartCleanup -and
            [object]::ReferenceEquals($lastPostStartCleanup.Run, $run)
        if ($isPostStartRetry) {
            $lastPostStartCleanup.FinalizerRetried = $true
        }

        for (
            $cleanupPass = 1;
            $cleanupPass -le $OwnedCleanupPassLimit;
            $cleanupPass++
        ) {
            $cleanupPasses = $cleanupPass
            try {
                $processExited = $run.Process.HasExited
            }
            catch {
                $processExited = $false
                [void]$finalizerErrors.Add(
                    "Pass ${cleanupPass} exit check failed: $($_.Exception.Message)")
            }
            try {
                $containmentEmpty =
                    Test-OwnedProcessContainmentEmpty -Run $run
            }
            catch {
                $containmentEmpty = $false
                [void]$finalizerErrors.Add(
                    "Pass ${cleanupPass} containment check failed: $($_.Exception.Message)")
            }

            if (-not $processExited -or -not $containmentEmpty) {
                $stopAttempts++
                $simulateStopFailure =
                    $run.SimulateFinalizerStopFailureOnce -and
                    -not $run.FinalizerStopFailureInjected
                if ($simulateStopFailure) {
                    $run.FinalizerStopFailureInjected = $true
                }
                $stopSucceeded = Stop-OwnedProcess `
                    -Run $run `
                    -SimulateFailureBeforeKill:$simulateStopFailure
                if (-not $stopSucceeded) {
                    if ($stopAttempts -eq 1) {
                        $firstStopFailed = $true
                    }
                    [void]$finalizerErrors.Add(
                        "Cleanup pass ${cleanupPass} did not confirm exit.")
                }
                try {
                    $processExited = $run.Process.HasExited
                }
                catch {
                    $processExited = $false
                    [void]$finalizerErrors.Add(
                        "Pass ${cleanupPass} post-stop exit check failed: $($_.Exception.Message)")
                }
                try {
                    $containmentEmpty =
                        Test-OwnedProcessContainmentEmpty -Run $run
                }
                catch {
                    $containmentEmpty = $false
                    [void]$finalizerErrors.Add(
                        "Pass ${cleanupPass} post-stop containment check failed: " +
                        $_.Exception.Message)
                }
            }

            if (-not $processExited -or -not $containmentEmpty) {
                continue
            }

            if (-not $run.Finalized) {
                try {
                    Complete-OwnedProcess -Run $run
                }
                catch {
                    $finalizationSucceeded = $false
                    [void]$finalizerErrors.Add(
                        "Finalization failed: $($_.Exception.Message)")
                    Add-Content -LiteralPath $logPath -Value (
                        "Finalization error for $($run.Name): $($_.Exception.Message)")
                }
            }
            break
        }

        try {
            $processExited = $run.Process.HasExited
        }
        catch {
            $processExited = $false
            [void]$finalizerErrors.Add(
                "Final exit check failed: $($_.Exception.Message)")
        }
        try {
            $containmentEmpty =
                Test-OwnedProcessContainmentEmpty -Run $run
        }
        catch {
            $containmentEmpty = $false
            [void]$finalizerErrors.Add(
                "Final containment check failed: $($_.Exception.Message)")
        }
        if ($finalizerErrors.Count -ne 0) {
            Add-Content -LiteralPath $logPath -Value (
                "Owned cleanup diagnostics: name=$($run.Name); " +
                "pid=$ownedProcessId; errors=$($finalizerErrors -join ' | ')")
        }

        $disposition = Get-OwnedCleanupDisposition `
            -ProcessExited $processExited `
            -ContainmentEmpty $containmentEmpty `
            -FinalizationSucceeded $finalizationSucceeded
        if ($disposition.DisposeHandle) {
            try {
                Close-OwnedProcessContainment -Run $run
                $run.Process.Dispose()
                $disposed = $true
                if ($disposition.RemoveFromRegistry) {
                    [void]$allRuns.Remove($run)
                }
            }
            catch {
                $disposed = $false
                $disposition.CleanupSucceeded = $false
                [void]$finalizerErrors.Add(
                    "Dispose failed after confirmed exit: $($_.Exception.Message)")
            }
        }
        else {
            Add-Content -LiteralPath $logPath -Value (
                "Live owned process retained after bounded cleanup retries: " +
                "name=$($run.Name); pid=$ownedProcessId; " +
                "containmentEmpty=$containmentEmpty; " +
                "passes=$OwnedCleanupPassLimit.")
        }

        if (-not $disposition.CleanupSucceeded) {
            $cleanupSucceeded = $false
        }
        if ($isPostStartRetry) {
            $lastPostStartCleanup.CleanupPasses = $cleanupPasses
            $lastPostStartCleanup.StopAttempts = $stopAttempts
            $lastPostStartCleanup.FirstStopFailed = $firstStopFailed
            $lastPostStartCleanup.FinalCleanupSucceeded =
                $disposition.CleanupSucceeded
            $lastPostStartCleanup.ProcessExited = $processExited
            $lastPostStartCleanup.Disposed = $disposed
            $lastPostStartCleanup.FinalizerErrors = @($finalizerErrors)
            $lastPostStartCleanup.RegisteredAfterFinalCleanup =
                $allRuns.Contains($run)
        }
    }

    if (-not $cleanupSucceeded -and $exitCode -eq 0) {
        $exitCode = 1
        $failureMessage = "Owned process-tree cleanup did not complete."
    }
    $stopwatch.Stop()
}

$trxSummary = if ($null -ne $trxSummaryOverride) {
    $trxSummaryOverride
}
else {
    Get-TrxSummary
}
$postStartCleanupSummary = if ($null -eq $lastPostStartCleanup) {
    $null
}
else {
    [ordered]@{
        ProcessId = $lastPostStartCleanup.ProcessId
        InitialCleanupSucceeded = $lastPostStartCleanup.InitialCleanupSucceeded
        RegisteredAfterInitialFailure =
            -not $lastPostStartCleanup.InitialCleanupSucceeded -and
            $lastPostStartCleanup.Registered
        FinalizerRetried = $lastPostStartCleanup.FinalizerRetried
        CleanupPasses = $lastPostStartCleanup.CleanupPasses
        StopAttempts = $lastPostStartCleanup.StopAttempts
        FirstStopFailed = $lastPostStartCleanup.FirstStopFailed
        FinalCleanupSucceeded = if (
            $null -eq $lastPostStartCleanup.FinalCleanupSucceeded
        ) {
            $lastPostStartCleanup.InitialCleanupSucceeded
        }
        else {
            $lastPostStartCleanup.FinalCleanupSucceeded
        }
        ProcessExited = $lastPostStartCleanup.ProcessExited
        Disposed = $lastPostStartCleanup.Disposed
        RegisteredAfterFinalCleanup =
            $lastPostStartCleanup.RegisteredAfterFinalCleanup
        ErrorsPreserved = [bool]$lastPostStartCleanup.ErrorsPreserved
        FinalizerErrors = @($lastPostStartCleanup.FinalizerErrors)
    }
}
$exitedRootDescendantSummary = if ($null -eq $lastExitedRootDescendant) {
    $null
}
else {
    $descendantExited = $true
    try {
        $descendantProcess = [System.Diagnostics.Process]::GetProcessById(
            $lastExitedRootDescendant.ProcessId)
        try {
            $descendantExited = $descendantProcess.HasExited
        }
        finally {
            $descendantProcess.Dispose()
        }
    }
    catch [ArgumentException] {
        $descendantExited = $true
    }

    if (-not $descendantExited) {
        $cleanupSucceeded = $false
        if ($exitCode -eq 0) {
            $exitCode = 1
            $failureMessage =
                "Exited-root descendant remained alive after owned containment cleanup."
        }
    }
    [ordered]@{
        ProcessId = $lastExitedRootDescendant.ProcessId
        RootExitedBeforeCleanup =
            $lastExitedRootDescendant.RootExitedBeforeCleanup
        ObservedAliveBeforeCleanup =
            $lastExitedRootDescendant.ObservedAliveBeforeCleanup
        ExitedAfterCleanup = $descendantExited
    }
}
$summary = if ($isSelfTest) {
    [ordered]@{
        SelfTest = $SelfTest
        WallTime = $stopwatch.Elapsed.ToString()
        ExitCode = $exitCode
        TimedOut = $timedOut
        OwnedTreeCleanupSucceeded = $cleanupSucceeded
        Tests = [ordered]@{
            Total = $trxSummary.Total
            Executed = $trxSummary.Executed
            Passed = $trxSummary.Passed
            Failed = $trxSummary.Failed
        }
        DuplicateTests = @($trxSummary.DuplicateTests)
        PostStartCleanup = $postStartCleanupSummary
        ExitedRootDescendant = $exitedRootDescendantSummary
    }
}
else {
    [ordered]@{
        RequestedLane = $Lane
        EffectiveLane = $effectiveLane
        TimeoutMinutes = $effectiveTimeoutMinutes
        WallTime = $stopwatch.Elapsed.ToString()
        ExitCode = $exitCode
        TimedOut = $timedOut
        OwnedTreeCleanupSucceeded = $cleanupSucceeded
        Tests = [ordered]@{
            Total = $trxSummary.Total
            Executed = $trxSummary.Executed
            Passed = $trxSummary.Passed
            Failed = $trxSummary.Failed
        }
        DuplicateTests = @($trxSummary.DuplicateTests)
    }
}
$summaryFileName = if ($isSelfTest) { "self-test-summary.json" } else { "summary.json" }
$summary | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $resultDirectory $summaryFileName)

$summaryLines = if ($isSelfTest) {
    @(
        ""
        "Self-test result"
        "  Self-test: $SelfTest"
        "  Wall time: $($stopwatch.Elapsed)"
        "  Exit code: $exitCode"
        "  Timed out: $timedOut"
        "  Owned-tree cleanup: $(if ($cleanupSucceeded) { "complete" } else { "failed" })"
        "  Tests: total=$($trxSummary.Total), executed=$($trxSummary.Executed), passed=$($trxSummary.Passed), failed=$($trxSummary.Failed)"
        "  Duplicate test IDs: $($trxSummary.DuplicateTests.Count)"
        "  Self-test results: $resultDirectory"
        "  Log: $logPath"
    )
}
else {
    @(
        ""
        "Lane result"
        "  Requested lane: $Lane"
        "  Effective lane: $effectiveLane"
        "  Filter: $(if ([string]::IsNullOrWhiteSpace($laneFilter)) { "<none>" } else { $laneFilter })"
        "  Timeout: $effectiveTimeoutMinutes minute(s)"
        "  Wall time: $($stopwatch.Elapsed)"
        "  Exit code: $exitCode"
        "  Timed out: $timedOut"
        "  Owned-tree cleanup: $(if ($cleanupSucceeded) { "complete" } else { "failed" })"
        "  Tests: total=$($trxSummary.Total), executed=$($trxSummary.Executed), passed=$($trxSummary.Passed), failed=$($trxSummary.Failed)"
        "  Duplicate test IDs: $($trxSummary.DuplicateTests.Count)"
        "  Results: $resultDirectory"
        "  Log: $logPath"
    )
}
if ($isSelfTest -and
    $SelfTest -eq "OwnedPostStartCleanupRetry" -and
    $null -ne $postStartCleanupSummary) {
    $summaryLines += (
        "  Post-start retry: " +
        "pid=$($postStartCleanupSummary.ProcessId) " +
        "registered=$($postStartCleanupSummary.RegisteredAfterInitialFailure) " +
        "finalizerRetried=$($postStartCleanupSummary.FinalizerRetried) " +
        "cleanupPasses=$($postStartCleanupSummary.CleanupPasses) " +
        "stopAttempts=$($postStartCleanupSummary.StopAttempts) " +
        "firstStopFailed=$($postStartCleanupSummary.FirstStopFailed) " +
        "finalCleanupSucceeded=$($postStartCleanupSummary.FinalCleanupSucceeded) " +
        "processExited=$($postStartCleanupSummary.ProcessExited) " +
        "disposed=$($postStartCleanupSummary.Disposed) " +
        "registeredAfterFinalCleanup=$($postStartCleanupSummary.RegisteredAfterFinalCleanup) " +
        "errorsPreserved=$($postStartCleanupSummary.ErrorsPreserved)")
}
if (-not [string]::IsNullOrWhiteSpace($failureMessage)) {
    $summaryLines += "  Failure: $failureMessage"
}

$summaryLines | Tee-Object -FilePath $logPath -Append | Write-Host
exit $exitCode
