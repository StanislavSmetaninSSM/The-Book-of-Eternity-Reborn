[CmdletBinding(DefaultParameterSetName = 'Categories')]
param(
    [Parameter(ParameterSetName = 'Categories')][string[]]$Category,
    [Parameter(Mandatory, ParameterSetName = 'List')][switch]$ListCategories,
    [Parameter(Mandatory, ParameterSetName = 'Audit')][switch]$ValidateCatalog,
    [Parameter(Mandatory, ParameterSetName = 'Selection')][string]$SelectionFile,
    [Parameter(ParameterSetName = 'Categories')]
    [Parameter(ParameterSetName = 'Selection')]
    [Parameter(ParameterSetName = 'Audit')]
    [ValidateRange(0, 30)][int]$TimeoutMinutes = 0,
    [Parameter(ParameterSetName = 'Categories')]
    [Parameter(ParameterSetName = 'Selection')]
    [ValidateRange(1, 4)][int]$Parallelism = 2,
    [Parameter(ParameterSetName = 'Categories')]
    [Parameter(ParameterSetName = 'Selection')]
    [Parameter(ParameterSetName = 'Audit')][switch]$NoBuild,
    [Parameter(ParameterSetName = 'Categories')]
    [Parameter(ParameterSetName = 'Selection')][switch]$PlanOnly,
    [Parameter(Mandatory, ParameterSetName = 'Retired')][string]$Lane,
    [Parameter(ParameterSetName = 'Retired')][string]$Filter,
    [Parameter(ParameterSetName = 'Retired')][string]$FocusedProject,
    [Parameter(Mandatory, ParameterSetName = 'SelfTest', DontShow)]
    [ValidateSet('NpmStartup', 'ResultDirectory', 'TrxSummary',
        'OwnedPostStartFailure', 'OwnedPostStartCleanupRetry', 'RuntimeCleanupDeadline',
        'OwnedExitedRootDescendant', 'OwnedBatchExitedRootDescendant')]
    [string]$SelfTest,
    [Parameter(ParameterSetName = 'SelfTest', DontShow)][string]$SelfTestTrxDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'This runner requires PowerShell 7.' }
if ($PSCmdlet.ParameterSetName -eq 'Retired') {
    throw "Lane '$Lane' is retired. Use -ListCategories, then -Category with explicit domain categories. No tests were started."
}
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$isSelfTest = $PSCmdlet.ParameterSetName -eq 'SelfTest'
if (-not $isSelfTest -and -not $ListCategories -and -not $ValidateCatalog -and
    [string]::IsNullOrWhiteSpace($SelectionFile) -and ($null -eq $Category -or $Category.Count -eq 0)) {
    Write-Host @"
Select verification by affected contract; there is no default or complete suite.
  ./scripts/test-csharp.ps1 -ListCategories
  ./scripts/test-csharp.ps1 -Category category-id -PlanOnly
  ./scripts/test-csharp.ps1 -Category category-id
  ./scripts/test-csharp.ps1 -SelectionFile tests/selection.json
  ./scripts/test-csharp.ps1 -ValidateCatalog   # discovery only, no tests
See docs/testing.md and tests/categories.json.
"@
    exit 0
}
Import-Module (Join-Path $PSScriptRoot 'testing/TestCategoryCatalog.psm1') -Force
$catalog = $null
$selectionReasons = @()
$selectedCategories = @()
$effectiveTimeoutMinutes = 0
if (-not $isSelfTest) {
    $catalog = Read-TestCategoryCatalog -Path (Join-Path $repoRoot 'tests/categories.json')
    if ($ListCategories) {
        $catalog.categories | Select-Object id, responsibility, excludes, changeHints, related, timeoutMinutes, expectedSeconds |
            ConvertTo-Json -Depth 8
        exit 0
    }
    if (-not [string]::IsNullOrWhiteSpace($SelectionFile)) {
        $request = Get-Content -LiteralPath $SelectionFile -Raw | ConvertFrom-Json -AsHashtable
        if ($request.schemaVersion -ne 1 -or @($request.selections).Count -eq 0) {
            throw 'Selection file requires schemaVersion 1 and explicit selections with reasons.'
        }
        foreach ($entry in $request.selections) {
            if ([string]::IsNullOrWhiteSpace($entry.category) -or
                [string]::IsNullOrWhiteSpace($entry.reason) -or @($entry.contracts).Count -eq 0) {
                throw 'Each selection requires a category, reason and affected contracts.'
            }
        }
        $Category = @($request.selections | ForEach-Object category)
        $selectionReasons = @($request.selections)
    }
    foreach ($id in $Category) {
        $matches = @($catalog.categories | Where-Object { $_.id -ceq $id })
        if ($matches.Count -ne 1) { throw "Unknown category '$id'. Use -ListCategories; no workload started." }
        $selectedCategories += $matches[0]
    }
    if (-not $ValidateCatalog -and $selectedCategories.Count -eq 0) {
        throw 'Choose at least one explicit category.'
    }
    $budget = if ($ValidateCatalog) { 10 } else {
        [Math]::Min(30, 5 + ($selectedCategories | Measure-Object timeoutMinutes -Sum).Sum)
    }
    $effectiveTimeoutMinutes = if ($TimeoutMinutes -gt 0) { $TimeoutMinutes } else { [int]$budget }
}
$effectiveLane = 'Categories'
$Lane = 'Categories'
$OwnedCleanupPassLimit = 2
$fastTestProject = Join-Path $repoRoot 'BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj'
$integrationTestProject = Join-Path $repoRoot 'BookOfEternityClient.IntegrationTests/BookOfEternityClient.IntegrationTests.csproj'
function New-UniqueResultDirectory {
    param(
        [Parameter(Mandatory)]
        [string]$RunLabel
    )

    $resultRoot = Join-Path $repoRoot "TestResults\test-categories"
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

$resultLabel = if ($isSelfTest) { "selftest" } else { "categories" }
$resultDirectory = New-UniqueResultDirectory -RunLabel $resultLabel
$logPath = Join-Path $resultDirectory "dotnet-test.log"
$testWorkerRuntimeBase = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine(
        [System.IO.Path]::GetTempPath(),
        ("boe-test-worker-runtime-" + [Guid]::NewGuid().ToString("N"))))

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
$testRuns = @()
$trxSummaryOverride = $null
$lastPostStartCleanup = $null
$lastExitedRootDescendant = $null
$runtimeCleanupSucceeded = $true
$runtimeCleanup = $null
$runtimeCleanupSelfTest = $false

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
    $npmName = if ($IsWindows) { "npm.cmd" } else { "npm" }
    $npmCommands = @(
        Get-Command -Name $npmName -CommandType Application -ErrorAction Stop
    )
    foreach ($npmCommand in $npmCommands) {
        $candidate = $npmCommand.Path
        if ([System.IO.Path]::IsPathFullyQualified($candidate) -and
            (Test-Path -LiteralPath $candidate -PathType Leaf)) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    throw "Could not resolve $npmName to an absolute application path."
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

        [string]$WorkingDirectory = $repoRoot,

        [switch]$Quiet,

        [switch]$SimulateInitialCleanupFailure
    )

    $jobHandle = New-OwnedProcessContainment
    $launchGate = $null
    $payloadPath = $null
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    # Keep child workspaces outside virtualized AppData and canonical fixtures.
    # Only owned children inherit this setting; the caller's environment is unchanged.
    $startInfo.Environment["BOE_WORKER_RUNTIME_BASE_PATH"] = $testWorkerRuntimeBase
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

function Invoke-OwnedWorkerRuntimeCleanup {
    if (-not (Test-Path -LiteralPath $testWorkerRuntimeBase)) {
        return
    }
    $script:runtimeCleanupSucceeded = $false
    foreach ($workload in @($allRuns)) {
        if (-not $workload.Process.HasExited -or
            -not (Test-OwnedProcessContainmentEmpty -Run $workload)) {
            throw "Worker runtime cleanup requires all workload trees to be exited and empty."
        }
    }
    if ([DateTime]::UtcNow -ge $deadlineUtc) {
        $script:timedOut = $true
        throw "Worker runtime cleanup has no remaining lane budget."
    }
    $startedPath = Join-Path $resultDirectory "runtime-cleanup-started.json"
    $finishedPath = Join-Path $resultDirectory "runtime-cleanup-delay-finished.txt"
    $arguments = @(
        "-NoProfile", "-File",
        (Join-Path $PSScriptRoot "test-csharp-worker-runtime-cleanup.ps1"),
        "-RuntimeBasePath", $testWorkerRuntimeBase
    )
    if ($runtimeCleanupSelfTest) {
        $arguments += @(
            "-SelfTestDelayMilliseconds", "5000",
            "-SelfTestStartedPath", $startedPath,
            "-SelfTestFinishedPath", $finishedPath
        )
    }
    $script:runtimeCleanup = [pscustomobject]@{
        Attempted = $true
        RuntimeBasePath = $testWorkerRuntimeBase
        StartedPath = $startedPath
        FinishedPath = $finishedPath
        ChildProcessId = $null
        Run = $null
        OwnedProcessExited = $false
        ContainmentEmpty = $false
        Disposed = $false
        RegisteredAfterCleanup = $true
    }
    $run = Start-OwnedProcess `
        -Name "Worker-runtime-cleanup" `
        -FileName (Get-Command pwsh -ErrorAction Stop).Source `
        -Arguments $arguments `
        -Quiet
    $runtimeCleanup.Run = $run
    if ($runtimeCleanupSelfTest) {
        # Establish that the actual cleanup child reached its delay before making
        # the same existing deadline expire; startup speed cannot fake this test.
        while (-not (Test-Path -LiteralPath $startedPath -PathType Leaf)) {
            if ($run.Process.HasExited) {
                Complete-OwnedProcess -Run $run
                throw "Runtime cleanup probe exited before establishing its delay."
            }
            if ([DateTime]::UtcNow -ge $deadlineUtc) {
                $script:timedOut = $true
                throw "Runtime cleanup probe startup exceeded the lane deadline."
            }
            Start-Sleep -Milliseconds 25
        }
        $runtimeCleanup.ChildProcessId = (
            Get-Content -LiteralPath $startedPath -Raw | ConvertFrom-Json).ProcessId
        $script:deadlineUtc = [DateTime]::UtcNow.AddMilliseconds(750)
    }
    Invoke-OwnedPhase `
        -Run $run `
        -TimeoutMessage "Worker runtime cleanup exceeded the lane deadline." `
        -FailureDescription "Worker runtime cleanup"
    if (Test-Path -LiteralPath $testWorkerRuntimeBase) {
        throw "Worker runtime cleanup returned without removing its owned directory."
    }
    $script:runtimeCleanupSucceeded = $true
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

    if ([string]::IsNullOrWhiteSpace($TestFilter)) {
        throw 'Test execution requires an explicit resolved method filter; full-project execution is disabled.'
    }
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

function Get-TestDiscoveryArguments {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

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

    return @($arguments)
}

function ConvertFrom-TestDiscoveryOutput {
    param(
        [Parameter(Mandatory)]
        [object]$DiscoveryRun,

        [Parameter(Mandatory)]
        [string]$SelectionName
    )

    if (-not $DiscoveryRun.Finalized -or $DiscoveryRun.ExitCode -ne 0) {
        throw "Test discovery for '$SelectionName' did not complete successfully."
    }

    $testCases = [System.Collections.Generic.List[object]]::new()
    foreach ($line in $DiscoveryRun.StandardOutputText -split "\r?\n") {
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

function Get-DiscoveredTestCases {
    param(
        [Parameter(Mandatory)]
        [string]$ProjectPath,

        [Parameter(Mandatory)]
        [string]$SelectionName,

        [AllowNull()]
        [string]$TestFilter
    )

    $arguments = @(
        Get-TestDiscoveryArguments -ProjectPath $ProjectPath -TestFilter $TestFilter
    )
    $discoveryRun = Start-OwnedProcess `
        -Name "$SelectionName-discovery" `
        -Arguments $arguments `
        -Quiet
    Invoke-OwnedPhase `
        -Run $discoveryRun `
        -TimeoutMessage "Test discovery exceeded the lane deadline." `
        -FailureDescription "Test discovery for '$SelectionName'"
    return @(
        ConvertFrom-TestDiscoveryOutput `
            -DiscoveryRun $discoveryRun `
            -SelectionName $SelectionName
    )
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







function Get-ExpectedMethodCounts {
    param(
        [Parameter(Mandatory)]
        [object[]]$TestCases,

        [Parameter(Mandatory)]
        [string[]]$MethodNames
    )

    $selected = [System.Collections.Generic.HashSet[string]]::new(
        [StringComparer]::Ordinal)
    foreach ($methodName in $MethodNames) {
        [void]$selected.Add($methodName)
    }
    $groups = @($TestCases | Where-Object { $selected.Contains($_.MethodName) } |
        Group-Object MethodName -CaseSensitive)
    if ($groups.Count -ne $selected.Count) {
        throw "A test descriptor contains methods absent from its discovered selection."
    }
    return @($groups | ForEach-Object {
        [pscustomobject]@{ Name = $_.Name; Cases = $_.Count }
    })
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

        [Parameter(Mandatory)]
        [object[]]$ExpectedMethodCounts,

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
        TrxFileName = $TrxFileName
        EstimatedCost = $EstimatedCost
        ExpectedMethodCounts = @($ExpectedMethodCounts)
        SchedulingGroup = $SchedulingGroup
        ConcurrencyWeight = $ConcurrencyWeight
        Arguments = New-TestArguments `
            -ProjectPath $ProjectPath `
            -TrxFileName $TrxFileName `
            -TestFilter $TestFilter
    }
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
            if ($null -ne $descriptor.PSObject.Properties["Quiet"]) {
                $startParameters.Quiet = [bool]$descriptor.Quiet
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
    $casesByTrxFile = [System.Collections.Generic.Dictionary[string, int]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $methodCasesByTrxFile = [System.Collections.Generic.Dictionary[string, object]]::new(
        [StringComparer]::OrdinalIgnoreCase)

    foreach ($trxFile in @(
        Get-ChildItem -LiteralPath $TrxDirectory -Filter "*.trx" -File
    )) {
        try {
            [xml]$trx = Get-Content -LiteralPath $trxFile.FullName -Raw
            $trxCounters = $trx.SelectSingleNode("//*[local-name()='Counters']")
            if ($null -eq $trxCounters) {
                throw "TRX '$($trxFile.Name)' has no test counters."
            }
            $fileCounters = @{}
            foreach ($property in @("Total", "Executed", "Passed", "Failed")) {
                $value = 0
                if (-not [int]::TryParse($trxCounters.GetAttribute($property.ToLowerInvariant()), [ref]$value) -or $value -lt 0) {
                    throw "TRX '$($trxFile.Name)' has an invalid $property counter."
                }
                $fileCounters[$property] = $value
            }

            $resultRows = @($trx.SelectNodes("//*[local-name()='UnitTestResult']"))
            $passedRows = @($resultRows | Where-Object { $_.GetAttribute('outcome') -eq 'Passed' }).Count
            $failedRows = @($resultRows | Where-Object { $_.GetAttribute('outcome') -eq 'Failed' }).Count
            $executedRows = @($resultRows | Where-Object { $_.GetAttribute('outcome') -ne 'NotExecuted' }).Count
            if (@($resultRows | Where-Object { [string]::IsNullOrWhiteSpace($_.GetAttribute('outcome')) }).Count -ne 0 -or
                $fileCounters.Total -ne $resultRows.Count -or $fileCounters.Executed -ne $executedRows -or
                $fileCounters.Passed -ne $passedRows -or $fileCounters.Failed -ne $failedRows) {
                throw "TRX '$($trxFile.Name)' counters disagree with physical result outcomes."
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
            $fileOccurrences = [System.Collections.Generic.List[object]]::new()
            $methodCases = [System.Collections.Generic.Dictionary[string, int]]::new(
                [StringComparer]::Ordinal)
            foreach ($result in @(
                $trx.SelectNodes("//*[local-name()='UnitTestResult']")
            )) {
                $testId = $result.GetAttribute("testId")
                if ([string]::IsNullOrWhiteSpace($testId)) {
                    throw "UnitTestResult in '$($trxFile.Name)' has no testId."
                }
                if (-not $storageByTestId.ContainsKey($testId)) {
                    throw (
                        "UnitTestResult testId '$testId' in '$($trxFile.Name)' " +
                        "has no UnitTest storage mapping.")
                }
                $storage = $storageByTestId[$testId]
                $key = "$storage::$testId"
                if ($seenInTrx.Add($key)) {
                    [void]$fileOccurrences.Add([pscustomobject]@{
                        Key = $key
                        TestId = $testId
                        TrxFile = $trxFile.Name
                    })
                }
                $testName = $result.GetAttribute("testName")
                if ([string]::IsNullOrWhiteSpace($testName)) {
                    throw "UnitTestResult testId '$testId' in '$($trxFile.Name)' has no testName."
                }
                $argumentStart = $testName.IndexOf('(')
                $methodName = if ($argumentStart -ge 0) {
                    $testName.Substring(0, $argumentStart)
                }
                else {
                    $testName
                }
                if (-not $methodCases.TryAdd($methodName, 1)) {
                    $methodCases[$methodName]++
                }
            }
            foreach ($property in @("Total", "Executed", "Passed", "Failed")) {
                $counters[$property] += $fileCounters[$property]
            }
            $casesByTrxFile.Add($trxFile.Name, $fileCounters.Total)
            $methodCasesByTrxFile.Add($trxFile.Name, $methodCases)
            foreach ($occurrence in $fileOccurrences) {
                [void]$testOccurrences.Add($occurrence)
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
        CasesByTrxFile = $casesByTrxFile
        MethodCasesByTrxFile = $methodCasesByTrxFile
    }
}

function Get-UnderfilledDescriptorNames {
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [object[]]$Descriptors,

        [Parameter(Mandatory)]
        [object]$TrxSummary
    )

    foreach ($descriptor in $Descriptors) {
        $fileName = $descriptor.TrxFileName
        if (-not $TrxSummary.CasesByTrxFile.ContainsKey($fileName) -or
            -not $TrxSummary.MethodCasesByTrxFile.ContainsKey($fileName) -or
            $TrxSummary.CasesByTrxFile[$fileName] -lt $descriptor.EstimatedCases) {
            $descriptor.Name
            continue
        }
        $actualMethods = $TrxSummary.MethodCasesByTrxFile[$fileName]
        foreach ($expected in $descriptor.ExpectedMethodCounts) {
            if (-not $actualMethods.ContainsKey($expected.Name) -or
                $actualMethods[$expected.Name] -lt $expected.Cases) {
                $descriptor.Name
                break
            }
        }
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

. (Join-Path $PSScriptRoot 'testing/TestCategoryExecution.ps1')

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
            "RuntimeCleanupDeadline" {
                [void][System.IO.Directory]::CreateDirectory($testWorkerRuntimeBase)
                Set-Content -LiteralPath (Join-Path $testWorkerRuntimeBase "deadline-sentinel.txt") -Value "diagnostic runtime"
                $runtimeCleanupSelfTest = $true
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
        Invoke-TestCategoryWork
    }
    $runtimeCleanupClock = [Diagnostics.Stopwatch]::StartNew()
    try { Invoke-OwnedWorkerRuntimeCleanup }
    finally { $script:categoryPhaseResults.Add([ordered]@{ Name = 'Runtime-cleanup'; Seconds = $runtimeCleanupClock.Elapsed.TotalSeconds; Completed = $runtimeCleanupSucceeded }) }
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
        if ($null -ne $runtimeCleanup -and
            [object]::ReferenceEquals($runtimeCleanup.Run, $run)) {
            $runtimeCleanup.OwnedProcessExited = $processExited
            $runtimeCleanup.ContainmentEmpty = $containmentEmpty
            $runtimeCleanup.Disposed = $disposed
            $runtimeCleanup.RegisteredAfterCleanup = $allRuns.Contains($run)
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

    if (Test-Path -LiteralPath $testWorkerRuntimeBase) {
        $runtimeCleanupSucceeded = $false
        Add-Content -LiteralPath $logPath -Value (
            "Owned test worker runtime retained after failure or timeout: $testWorkerRuntimeBase")
        if ($exitCode -eq 0) { $exitCode = 1; $failureMessage = 'Owned runtime directory cleanup did not complete.' }
    }
    if (-not $cleanupSucceeded -and $exitCode -eq 0) {
        $exitCode = 1
        $failureMessage = "Owned process-tree cleanup did not complete."
    }
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
$runtimeCleanupSummary = if ($null -eq $runtimeCleanup) {
    $null
}
else {
    [ordered]@{
        Attempted = $runtimeCleanup.Attempted
        RuntimeBasePath = $runtimeCleanup.RuntimeBasePath
        StartedPath = $runtimeCleanup.StartedPath
        FinishedPath = $runtimeCleanup.FinishedPath
        ChildProcessId = $runtimeCleanup.ChildProcessId
        OwnedProcessExited = $runtimeCleanup.OwnedProcessExited
        ContainmentEmpty = $runtimeCleanup.ContainmentEmpty
        Disposed = $runtimeCleanup.Disposed
        RegisteredAfterCleanup = $runtimeCleanup.RegisteredAfterCleanup
    }
}
$plannedDescriptorCount = @($testRuns).Count
$plannedCaseCount = 0
foreach ($plannedRun in $testRuns) {
    $plannedCaseCount += $plannedRun.EstimatedCases
}
$completedDescriptorCount = @($testRuns | Where-Object {
    Test-Path -LiteralPath (Join-Path $resultDirectory $_.TrxFileName) -PathType Leaf
}).Count
$underfilledDescriptorNames = @(Get-UnderfilledDescriptorNames `
    -Descriptors $testRuns -TrxSummary $trxSummary)
$selectionComplete = -not $PlanOnly -and $plannedDescriptorCount -gt 0 -and
    $completedDescriptorCount -eq $plannedDescriptorCount -and
    $underfilledDescriptorNames.Count -eq 0 -and
    $trxSummary.Executed -eq $trxSummary.Total -and
    $trxSummary.ParseErrors.Count -eq 0
$summary = if ($isSelfTest) {
    [ordered]@{
        SelfTest = $SelfTest
        WallTime = $stopwatch.Elapsed.ToString()
        ExitCode = $exitCode
        TimedOut = $timedOut
        OwnedTreeCleanupSucceeded = $cleanupSucceeded
        RuntimeCleanupSucceeded = $runtimeCleanupSucceeded
        RuntimeCleanup = $runtimeCleanupSummary
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
        Categories = @($Category)
        Reasons = $selectionReasons
        DiscoveryOnly = [bool]$ValidateCatalog
        PlanOnly = [bool]$PlanOnly
        Revision = $script:categoryRevision
        CatalogAudit = $script:catalogAudit
        CategoryResults = @($script:categoryResults)
        AdapterResults = @($script:adapterResults)
        PhaseResults = @($script:categoryPhaseResults)
        Strategy = "SelectedCategories"
        TimeoutMinutes = $effectiveTimeoutMinutes
        WallTime = $stopwatch.Elapsed.ToString()
        ExitCode = $exitCode
        TimedOut = $timedOut
        OwnedTreeCleanupSucceeded = $cleanupSucceeded
        RuntimeCleanupSucceeded = $runtimeCleanupSucceeded
        RuntimeCleanup = $runtimeCleanupSummary
        Selection = [ordered]@{
            PlannedDescriptors = $plannedDescriptorCount
            CompletedDescriptors = $completedDescriptorCount
            PlannedCases = $plannedCaseCount
            CompletedCases = $trxSummary.Total
            UnderfilledDescriptors = $underfilledDescriptorNames
            Complete = $selectionComplete
        }
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
        "Category result"
        "  Categories: $($Category -join ', ')"
        "  Filter: $(if ([string]::IsNullOrWhiteSpace($laneFilter)) { "<none>" } else { $laneFilter })"
        "  Timeout: $effectiveTimeoutMinutes minute(s)"
        "  Wall time: $($stopwatch.Elapsed)"
        "  Exit code: $exitCode"
        "  Timed out: $timedOut"
        "  Owned-tree cleanup: $(if ($cleanupSucceeded) { "complete" } else { "failed" })"
        "  Planned selection: descriptors=$plannedDescriptorCount, cases=$plannedCaseCount; completed descriptors=$completedDescriptorCount, cases=$($trxSummary.Total), complete=$selectionComplete"
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

if ($exitCode -eq 0 -and [DateTime]::UtcNow -ge $deadlineUtc) {
    $timedOut = $true
    $exitCode = 124
    $failureMessage = "The lane deadline expired before cleanup and report generation completed."
    $summary.ExitCode = $exitCode
    $summary.TimedOut = $true
    $summaryLines = @($summaryLines | ForEach-Object {
        if ($_.StartsWith("  Exit code:", [StringComparison]::Ordinal)) { "  Exit code: $exitCode" }
        elseif ($_.StartsWith("  Timed out:", [StringComparison]::Ordinal)) { "  Timed out: $timedOut" }
        else { $_ }
    })
    $summaryLines += "  Failure: $failureMessage"
}
$summary.WallTime = $stopwatch.Elapsed.ToString()
$summaryLines = @($summaryLines | ForEach-Object {
    if ($_.StartsWith("  Wall time:", [StringComparison]::Ordinal)) { "  Wall time: $($summary.WallTime)" }
    else { $_ }
})
$summary | ConvertTo-Json -Depth 12 |
    Set-Content -LiteralPath (Join-Path $resultDirectory $summaryFileName)
$summaryLines | Tee-Object -FilePath $logPath -Append | Write-Host
$stopwatch.Stop()
if ($exitCode -eq 0 -and [DateTime]::UtcNow -ge $deadlineUtc) {
    $timedOut = $true
    $exitCode = 124
    $summary.ExitCode = $exitCode
    $summary.TimedOut = $true
    $summary.WallTime = $stopwatch.Elapsed.ToString()
    $summary | ConvertTo-Json -Depth 12 |
        Set-Content -LiteralPath (Join-Path $resultDirectory $summaryFileName)
    Add-Content -LiteralPath $logPath -Value (
        "The lane deadline expired before successful runner exit; complete wall time: $($summary.WallTime).")
}
exit $exitCode
