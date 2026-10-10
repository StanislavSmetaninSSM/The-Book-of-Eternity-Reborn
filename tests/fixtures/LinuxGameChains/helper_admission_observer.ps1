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
        $row = [ordered]@{ Phase=$Phase; ScopeId=$probe.ScopeId; ConsumerPid=$PID; Mode='write';
            Method='Complete-BoeTurn Open-BoeHelperScope write seq0'; Sequence=$(if ($Context) { $Context.sequence } else { $null });
            HelperPid=$probe.HelperPid; LinuxStarttime=$probe.LinuxStarttime; BootId=$probe.BootId;
            StopwatchTicks=[Diagnostics.Stopwatch]::GetTimestamp(); StopwatchFrequency=[Diagnostics.Stopwatch]::Frequency;
            Utc=[DateTimeOffset]::UtcNow.ToString('O'); Data=$Data }
        $text = (ConvertTo-Json -InputObject $row -Depth 12 -Compress) + "`n"
        $bytes = [Text.Encoding]::UTF8.GetByteCount($text)
        if ($probe.Rows -ge 32 -or $probe.Bytes + $bytes -gt 1048576) { $probe.Dropped++; return }
        [IO.File]::AppendAllText($probe.EventPath, $text, [Text.UTF8Encoding]::new($false))
        $probe.Rows++; $probe.Bytes += $bytes
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Begin-BoeAdmissionObservation {
    param([string]$Mode, [string]$SessionPath, [AllowNull()][string]$ExpectedGeneration)
    try {
        if ($Mode -cne 'write' -or $script:BoeAdmissionObserverTargetClaimed -or
            -not $env:BOE_TEST_HELPER_ADMISSION_OBSERVER_DIR) { return }
        $stack = @(Get-PSCallStack)
        if (-not @($stack | Where-Object { $_.ScriptName -ceq $env:BOE_TEST_HELPER_TARGET_CONSUMER_SCRIPT }).Count) { return }
        if (-not @($stack | Where-Object { $_.FunctionName -ceq 'Complete-BoeTurn' -and
            $_.ScriptName -ceq (Join-Path $PSScriptRoot 'GM_Turn_Helper.ps1') }).Count) { return }
        $script:BoeAdmissionObserverTargetClaimed = $true
        $stem = Join-Path $env:BOE_TEST_HELPER_ADMISSION_OBSERVER_DIR "consumer-$PID"
        $script:BoeAdmissionProbe = [pscustomobject]@{ ScopeId=[Guid]::NewGuid().ToString('N');
            HelperPid=$null; LinuxStarttime=$null; BootId=$null; Attached=$false;
            FailureNonce=[Guid]::NewGuid().ToString('N'); NonceConfigured=$false;
            EventPath="$stem.jsonl"; FramePath="$stem-seq0.txt"; StderrPath="$stem-stderr.txt";
            SummaryPath="$stem-summary.json"; Rows=0; Bytes=0; Dropped=0; Errors=0; Truncated=$false }
        Write-BoeAdmissionObservation $null 'launch-begin' @{ Session=$SessionPath; ExpectedGeneration=$ExpectedGeneration; FailureNonce=$script:BoeAdmissionProbe.FailureNonce;
            ConsumerScript=$env:BOE_TEST_HELPER_TARGET_CONSUMER_SCRIPT;
            CallStack=@($stack | ForEach-Object { @{ Function=$_.FunctionName; Script=$_.ScriptName; Line=$_.ScriptLineNumber } }) }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}

function Configure-BoeAdmissionFailureCapture {
    param($Start)
    try {
        # Remove inherited test settings from every child. Only the actual selected
        # write launch gets this valid nonce; no process-global setting is changed.
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
        if (-not $probe -or $probe.Attached -or $Context.mode -cne 'write') { return }
        $probe.Attached=$true; $probe.HelperPid=$Context.process.Id
        $Context | Add-Member -NotePropertyName BoeAdmissionObservation -NotePropertyValue $probe
        # Read only this actual newly launched managed child's Linux identity; no PID search.
        $stat=[IO.File]::ReadAllText("/proc/$($probe.HelperPid)/stat")
        $fields=$stat.Substring($stat.LastIndexOf(')')+2).Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
        $probe.LinuxStarttime=$fields[19]
        $probe.BootId=[IO.File]::ReadAllText('/proc/sys/kernel/random/boot_id').Trim()
        Write-BoeAdmissionObservation $Context 'launch-return' @{ ProcessStartTimeUtc=$Context.process.StartTime.ToUniversalTime().ToString('O');
            HasExited=$Context.process.HasExited; Command='dotnet BookOfEternityClient.dll --gm-turn-helper --root <actual-root> --generation <actual-generation>' }
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
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
            [IO.File]::WriteAllText($Context.BoeAdmissionObservation.FramePath,$stored,[Text.UTF8Encoding]::new($false))
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
        [IO.File]::WriteAllText($probe.StderrPath,$stored,[Text.UTF8Encoding]::new($false))
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
        $summary=@{ ScopeId=$probe.ScopeId; ConsumerPid=$PID; HelperPid=$probe.HelperPid;
            LinuxStarttime=$probe.LinuxStarttime; BootId=$probe.BootId; Rows=$probe.Rows; Bytes=$probe.Bytes;
            FailureNonce=$probe.FailureNonce; NonceConfigured=$probe.NonceConfigured;
            Dropped=$probe.Dropped; Errors=$probe.Errors; StderrTruncated=$probe.Truncated;
            CaptureIncomplete=($probe.Dropped -ne 0 -or $probe.Errors -ne 0 -or $probe.Truncated -or -not $probe.LinuxStarttime);
            DisposeReturned=$true; ExitCode=$Context.exitCode }
        [IO.File]::WriteAllText($probe.SummaryPath,(ConvertTo-Json $summary -Depth 4),[Text.UTF8Encoding]::new($false))
    } catch { if ($script:BoeAdmissionProbe) { $script:BoeAdmissionProbe.Errors++ } }
}
