# Fixture-only passive observer, copied into the owned ship by prepare_helper_observation.py.
# No protocol reads, writes, retries, waits, authority, or product decisions occur here.
$script:BoeAdmissionObserverTargetClaimed = $false
$script:BoeAdmissionProbe = $null

function Write-BoeAdmissionObservation {
    param($Context, [string]$Phase, [hashtable]$Data = @{})
    try {
        $probe = if ($Context) { $Context.BoeAdmissionObservation } else { $script:BoeAdmissionProbe }
        if (-not $probe) { return }
        if ($Phase.StartsWith('read-') -and $Context.sequence -ne 0) { return }
        $row = [ordered]@{ Phase=$Phase; ScopeId=$probe.ScopeId; ConsumerPid=$PID; Mode='read';
            Method='Resolve-BoeSessionPath Open-BoeHelperScope default-read seq0'; Sequence=$(if ($Context) { $Context.sequence } else { $null });
            HelperPid=$probe.HelperPid; LinuxStarttime=$probe.LinuxStarttime; BootId=$probe.BootId;
            StopwatchTicks=[Diagnostics.Stopwatch]::GetTimestamp(); StopwatchFrequency=[Diagnostics.Stopwatch]::Frequency;
            Utc=[DateTimeOffset]::UtcNow.ToString('O'); Data=$Data }
        if ($probe.Rows -ge 32) { $probe.Dropped++; return }
        [void]$probe.Events.Add($row)
        $probe.Rows++
        # This hook follows the unchanged original guard's successful decision.
        if ($Phase -ceq 'admission-return') { Observe-BoeAdmissionLinuxIdentity $Context 'after-original-guard-passed' }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Begin-BoeAdmissionObservation {
    param([string]$Mode, [string]$SessionPath, [AllowNull()][string]$ExpectedGeneration)
    try {
        if ($Mode -cne 'read' -or $script:BoeAdmissionObserverTargetClaimed -or
            -not $env:BOE_TEST_HELPER_ADMISSION_OBSERVER_DIR) { return }
        $stack = @(Get-PSCallStack)
        if (-not @($stack | Where-Object { $_.ScriptName -ceq $env:BOE_TEST_HELPER_TARGET_CONSUMER_SCRIPT -and
            $_.ScriptLineNumber -eq 30 }).Count) { return }
        if (-not @($stack | Where-Object { $_.FunctionName -ceq 'Resolve-BoeSessionPath' -and
            $_.ScriptName -ceq (Join-Path $PSScriptRoot 'GM_Turn_Helper.ps1') }).Count) { return }
        $matchedRequestPath=$null
        if ($env:BOE_TEST_HELPER_TARGET_FIRST_QUEUE) {
            $caller=@($stack | Where-Object { $_.ScriptName -ceq $env:BOE_TEST_HELPER_TARGET_CONSUMER_SCRIPT -and $_.ScriptLineNumber -eq 30 })[0]
            $variables=$caller.GetFrameVariables()
            if (-not $variables.ContainsKey('RequestPath') -or $variables['RequestPath'].Value -isnot [string]) { return }
            $matchedRequestPath=[IO.Path]::GetFullPath($variables['RequestPath'].Value)
            $requestDirectory=[IO.Path]::GetDirectoryName($matchedRequestPath)
            if ([IO.Path]::GetFileName($matchedRequestPath) -cne 'request.json' -or
                [IO.Path]::GetFileName($requestDirectory) -cnotmatch '^request-1-[0-9a-f]{32}$' -or
                [IO.Path]::GetDirectoryName($requestDirectory) -cne $env:BOE_TEST_HELPER_TARGET_FIRST_QUEUE) { return }
        }
        $script:BoeAdmissionObserverTargetClaimed = $true
        $stem = Join-Path $env:BOE_TEST_HELPER_ADMISSION_OBSERVER_DIR "consumer-$PID"
        $script:BoeAdmissionProbe = [pscustomobject]@{ ScopeId=[Guid]::NewGuid().ToString('N');
            HelperPid=$null; LinuxStarttime=$null; BootId=$null; Attached=$false;
            LinuxIdentityAttempted=$false; LinuxIdentityFailure=$null; MatchedRequestPath=$matchedRequestPath;
            Events=[Collections.Generic.List[object]]::new(); Frame=$null; Stderr=$null;
            FailureNonce=[Guid]::NewGuid().ToString('N'); NonceConfigured=$false;
            EventPath="$stem.jsonl"; FramePath="$stem-seq0.txt"; StderrPath="$stem-stderr.txt";
            SummaryPath="$stem-summary.json"; Rows=0; Bytes=0; Dropped=0; Errors=0; Truncated=$false }
        Write-BoeAdmissionObservation $null 'launch-begin' @{ Session=$SessionPath; ExpectedGeneration=$ExpectedGeneration; FailureNonce=$script:BoeAdmissionProbe.FailureNonce;
            MatchedRequestPath=$matchedRequestPath; FirstQueue=$env:BOE_TEST_HELPER_TARGET_FIRST_QUEUE;
            ConsumerScript=$env:BOE_TEST_HELPER_TARGET_CONSUMER_SCRIPT;
            CallStack=@($stack | ForEach-Object { @{ Function=$_.FunctionName; Script=$_.ScriptName; Line=$_.ScriptLineNumber } }) }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Configure-BoeAdmissionFailureCapture {
    param($Start)
    try {
        # Remove inherited test settings from every child. Only the actual selected
        # default-read launch gets this valid nonce; no process-global setting is changed.
        [void]$Start.Environment.Remove('BOE_TEST_HELPER_FAILURE_NONCE')
        $probe=$script:BoeAdmissionProbe
        if(-not $probe -or $probe.Attached -or $probe.NonceConfigured){return}
        $Start.Environment['BOE_TEST_HELPER_FAILURE_NONCE']=$probe.FailureNonce
        $probe.NonceConfigured=$true
        Write-BoeAdmissionObservation $null 'managed-failure-capture-configured' @{ FailureNonce=$probe.FailureNonce; Scope='Only this selected ProcessStartInfo environment' }
    } catch { if($script:BoeAdmissionProbe){$script:BoeAdmissionProbe.Errors++} }
}

function Attach-BoeAdmissionObservation {
    param($Context)
    try {
        $probe = $script:BoeAdmissionProbe
        if (-not $probe -or $probe.Attached -or $Context.mode -cne 'read') { return }
        $probe.Attached=$true; $probe.HelperPid=$Context.process.Id
        $Context | Add-Member -NotePropertyName BoeAdmissionObservation -NotePropertyValue $probe
        Write-BoeAdmissionObservation $Context 'launch-return' @{ HasExited=$Context.process.HasExited;
            LinuxIdentityDeferred=$true; Command='dotnet BookOfEternityClient.dll --gm-turn-helper --root <actual-root> --generation <actual-generation>' }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Observe-BoeAdmissionLinuxIdentity {
    param($Context, [string]$Boundary)
    $probe=$Context.BoeAdmissionObservation
    if (-not $probe -or $probe.LinuxIdentityAttempted) { return }
    $probe.LinuxIdentityAttempted=$true
    try {
        # Only after original admission decision, or original refusal disposal.
        # A child already gone leaves explicit missing data; never hold or retry.
        $stat=[IO.File]::ReadAllText("/proc/$($probe.HelperPid)/stat")
        $fields=$stat.Substring($stat.LastIndexOf(')')+2).Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
        $probe.LinuxStarttime=$fields[19]
        $probe.BootId=[IO.File]::ReadAllText('/proc/sys/kernel/random/boot_id').Trim()
        Write-BoeAdmissionObservation $Context 'linux-identity-observed' @{ Boundary=$Boundary }
    } catch { $probe.LinuxIdentityFailure=$_.Exception.GetType().FullName }
}

function Observe-BoeAdmissionRead {
    param($Context, [AllowNull()]$Line)
    try {
        if (-not $Context.BoeAdmissionObservation -or $Context.sequence -ne 0) { return }
        $isNull=$null -eq $Line
        $count=if ($isNull) { $null } else { [Text.Encoding]::UTF8.GetByteCount($Line) }
        $kind=if ($isNull) { 'NULL-EOF' } elseif ($count -gt 65536) { 'oversized' } else { 'within-frame-bound' }
        Write-BoeAdmissionObservation $Context 'read-return' @{ WaitCompleted=$true; IsNull=$isNull; Utf8Bytes=$count; FrameOutcome=$kind }
        if (-not $isNull) {
            $stored=if ($count -le 65536) { $Line } else { $Line.Substring(0, [Math]::Min(4096,$Line.Length)) }
            $Context.BoeAdmissionObservation.Frame=$stored
            Write-BoeAdmissionObservation $Context 'read-frame-retained' @{ Path=$Context.BoeAdmissionObservation.FramePath;
                PrefixOnly=$count -gt 65536; StoredUtf8Bytes=[Text.Encoding]::UTF8.GetByteCount($stored) }
        }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Observe-BoeAdmissionIdentity {
    param($Context, $Reply, [AllowNull()][string]$ExpectedGeneration)
    try {
        if (-not $Context.BoeAdmissionObservation -or $Context.sequence -ne 0) { return }
        # Copies the five literal predicates immediately before the unchanged original guard.
        # This records all predicates; the product's short-circuit decision is not replaced.
        $predicates=[ordered]@{
            ReplyNotOk=(-not $Reply.ok)
            StateNotActive=($Reply.state -cne 'active')
            SequenceNotZero=($Reply.sequence -ne 0)
            GenerationFormatInvalid=($Reply.generation -cnotmatch '^[0-9a-f]{32}$')
            ExpectedGenerationMismatch=[bool]($ExpectedGeneration -and $Reply.generation -cne $ExpectedGeneration)
        }
        Write-BoeAdmissionObservation $Context 'identity-guard-observed' @{
            Ok=$Reply.ok; State=$Reply.state; Sequence=$Reply.sequence; Generation=$Reply.generation;
            ExpectedGeneration=$ExpectedGeneration; Predicates=$predicates;
            FailedPredicates=@($predicates.Keys | Where-Object { $predicates[$_] });
            Limit='Immediately before original guard; all predicate copies, not original short-circuit evaluation order.'
        }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Observe-BoeAdmissionExit {
    param($Context, [string]$Phase)
    try {
        if (-not $Context.BoeAdmissionObservation) { return }
        if ($Phase -ceq 'dispose-begin') { Observe-BoeAdmissionLinuxIdentity $Context 'original-dispose-begin' }
        $exited=$Context.process.HasExited
        $code=if ($exited) { $Context.process.ExitCode } else { $null }
        Write-BoeAdmissionObservation $Context $Phase @{ HasExited=$exited; ExitCode=$code;
            Lost=$Context.lost; PublicationUncertain=$Context.publicationUncertain }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Observe-BoeAdmissionJoined {
    param($Context)
    try {
        if (-not $Context.BoeAdmissionObservation) { return }
        $probe=$Context.BoeAdmissionObservation; $text=[string]$Context.diagnostic
        $count=[Text.Encoding]::UTF8.GetByteCount($text)
        $stored=$text.Substring(0,[Math]::Min(65536,$text.Length))
        $probe.Truncated=$stored.Length -ne $text.Length
        $probe.Stderr=$stored
        Write-BoeAdmissionObservation $Context 'dispose-joined' @{ ExitCode=$Context.exitCode; StderrUtf8Bytes=$count;
            StoredUtf8Bytes=[Text.Encoding]::UTF8.GetByteCount($stored); StderrTruncated=$probe.Truncated;
            PendingReadCompleted=($null -eq $Context.pendingRead -or $Context.pendingRead.IsCompleted);
            StderrReadCompleted=$Context.errorRead.IsCompleted; Lost=$Context.lost; PublicationUncertain=$Context.publicationUncertain }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Complete-BoeAdmissionObservation {
    param($Context)
    try {
        if (-not $Context.BoeAdmissionObservation) { return }
        $probe=$Context.BoeAdmissionObservation
        Write-BoeAdmissionObservation $Context 'dispose-return' @{}
        # Export only after all original protocol/exit/stdio joins and Dispose.
        foreach ($row in $probe.Events) {
            $text=(ConvertTo-Json -InputObject $row -Depth 12 -Compress)+"`n"
            $count=[Text.Encoding]::UTF8.GetByteCount($text)
            if ($probe.Bytes+$count -gt 1048576) { $probe.Dropped++; continue }
            [IO.File]::AppendAllText($probe.EventPath,$text,[Text.UTF8Encoding]::new($false))
            $probe.Bytes+=$count
        }
        if ($null -ne $probe.Frame) { [IO.File]::WriteAllText($probe.FramePath,$probe.Frame,[Text.UTF8Encoding]::new($false)) }
        if ($null -ne $probe.Stderr) { [IO.File]::WriteAllText($probe.StderrPath,$probe.Stderr,[Text.UTF8Encoding]::new($false)) }
        $summary=@{ ScopeId=$probe.ScopeId; ConsumerPid=$PID; HelperPid=$probe.HelperPid;
            LinuxStarttime=$probe.LinuxStarttime; BootId=$probe.BootId; Rows=$probe.Rows; Bytes=$probe.Bytes;
            FailureNonce=$probe.FailureNonce; NonceConfigured=$probe.NonceConfigured;
            MatchedRequestPath=$probe.MatchedRequestPath; LinuxIdentityFailure=$probe.LinuxIdentityFailure;
            Dropped=$probe.Dropped; Errors=$probe.Errors; StderrTruncated=$probe.Truncated;
            CaptureIncomplete=($probe.Dropped -ne 0 -or $probe.Errors -ne 0 -or $probe.Truncated -or -not $probe.LinuxStarttime);
            DisposeReturned=$true; ExitCode=$Context.exitCode }
        [IO.File]::WriteAllText($probe.SummaryPath,(ConvertTo-Json $summary -Depth 4),[Text.UTF8Encoding]::new($false))
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}
