param([string]$RepoRoot,[string]$SessionPath,[string]$Scenario,[string]$Folder,[string]$TestSupport)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1')

# Only the dedicated helper's process factory changes. The old helper does not
# call it on RED. A later production helper retains its actual framing, body,
# scope/lease, close and Dispose functions; TestSupport injects original FS hooks.
$script:JoinedHelperTransports=[Collections.Generic.List[object]]::new()
$script:ControlChildPid=$null; $script:ControlSeq0Read=$null; $script:ControlObserverErrors=0
$script:ControlReadProvenance=$null
if($Scenario -ceq 'catch-config-missing'){
    # This fixture copies the value returned by the SAME existing GetResult,
    # before the unchanged NULL/size guard. No alternate/second read or wait.
    $original=${function:Read-BoeHelperFrame}.ToString()
    $anchor='        $line=$Context.pendingRead.GetAwaiter().GetResult()'
    if(([regex]::Matches($original,[regex]::Escape($anchor))).Count -ne 1){throw 'Original control read anchor is not unique.'}
    $observation=@'
        try {
            if($Context.process.Id -eq $script:ControlChildPid -and $Context.sequence -eq 0){
                $isNull=$null -eq $line
                $count=if($isNull){$null}else{[Text.Encoding]::UTF8.GetByteCount($line)}
                $stored=if($isNull){$null}elseif($count -le 65536){$line}else{$line.Substring(0,[Math]::Min(4096,$line.Length))}
                $script:ControlSeq0Read=[pscustomobject]@{HelperPid=$Context.process.Id;WaitCompleted=$true;IsNull=$isNull;Utf8Bytes=$count;Frame=$stored;PrefixOnly=(-not $isNull -and $count -gt 65536);StopwatchTicks=[Diagnostics.Stopwatch]::GetTimestamp();StopwatchFrequency=[Diagnostics.Stopwatch]::Frequency}
            }
        }catch{$script:ControlObserverErrors++}
'@
    $instrumented=$original.Replace($anchor,$anchor+"`n"+$observation)
    $script:ControlReadProvenance=[pscustomobject]@{OriginalDefinition=$original;InstrumentedDefinition=$instrumented;OriginalSHA256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($original))).ToLowerInvariant();InstrumentedSHA256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($instrumented))).ToLowerInvariant();ExactSubstitutionCount=1;Limit='Control-only passive copy after original completed GetResult; original guard/timers/throw preserved.'}
    ${function:Read-BoeHelperFrame}=[scriptblock]::Create($instrumented)
}
$script:OriginalDispose=${function:Dispose-GmOperationTransport}
function Dispose-GmOperationTransport {
    param($Context)
    if($Context.disposed){return (& $script:OriginalDispose $Context)}
    $originalPid=$Context.process.Id
    # Observe the original handle, never reopen a PID. A helper requiring the
    # production Dispose kill fallback is a fixture failure, not causal evidence.
    $exited=$Context.process.WaitForExit(3500)
    & $script:OriginalDispose $Context
    $joined=[ordered]@{ProcessId=$originalPid;ExitedBeforeDispose=$exited;Disposed=$Context.disposed;ExitCode=$Context.exitCode}
    if($Scenario -ceq 'catch-config-missing'){$joined.Diagnostic=$Context.diagnostic}
    $script:JoinedHelperTransports.Add([pscustomobject]$joined)
}
function Start-BoeHelperProcess {
    param([string]$Root,[AllowNull()][string]$ExpectedGeneration)
    $start=[Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute=$false
    $start.RedirectStandardInput=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $expected=if([string]::IsNullOrEmpty($ExpectedGeneration)){'initialize'}else{$ExpectedGeneration}
    foreach($arg in @($TestSupport,'helper-storage-bootstrap',$Root,$Folder,$expected)){[void]$start.ArgumentList.Add($arg)}
    $start.Environment.Remove('BOE_TEST_HELPER_FAILURE_NONCE')|Out-Null
    $captured=$script:ControlCaptureNextChild
    if($captured){$start.Environment['BOE_TEST_HELPER_FAILURE_NONCE']=$script:ControlNonce;$script:ControlCaptureNextChild=$false}
    $process=[Diagnostics.Process]::Start($start)
    if($captured){$script:ControlChildPid=$process.Id}
    return $process
}
function Wait-FixtureRelease {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not [IO.File]::Exists((Join-Path $Folder 'continue')) -and $watch.Elapsed.TotalSeconds -lt 8){Start-Sleep -Milliseconds 10}
    if(-not [IO.File]::Exists((Join-Path $Folder 'continue'))){throw 'Fixture did not release the initialized helper.'}
}

$initialized=$false; $value=$null; $failure=$null
$script:ControlBodyEntered=$false; $script:ControlCaptureNextChild=$false; $script:ControlNonce=$null
try {
    Initialize-BoeGmTurnHelper -GameSessionPath $SessionPath
    $initialized=$true
    [IO.File]::WriteAllText((Join-Path $Folder 'initialized'),'actual public Init returned')
    if($Scenario -notin @('init-held','generation-missing','generation-malformed')){Wait-FixtureRelease}
    switch($Scenario) {
        'catch-config-missing' {
            $script:ControlNonce=[Guid]::NewGuid().ToString('N'); $script:ControlCaptureNextChild=$true
            Invoke-BoeHelperScope -Mode 'write' -Body {$script:ControlBodyEntered=$true}
        }
        {$_ -in @('init-held','read-held','stale-load','generation-missing','generation-malformed')} {
            $value=(Read-BoeJson -RelativePath 'output/helper-snapshot.json').value
        }
        {$_ -in @('realm-held','realm-link')} {
            Write-BoeJson -RelativePath 'game_state/world/helper-policy.json' -Data ([ordered]@{value='forbidden-world-write'})
        }
        'terminal-held' { Complete-BoeTurn }
        'path-dot' {
            Write-BoeJson -RelativePath 'game_state/control/../control/pending_turn_snapshot.authority.json' -Data ([ordered]@{value='protected-overwrite'})
        }
        'path-sibling' {
            $value=(Read-BoeJson -RelativePath (Join-Path ($SessionPath+'-sibling') 'outside.json')).value
        }
        default {throw 'Unknown fixture scenario.'}
    }
} catch {$failure=$_.Exception.ToString()}
$report=[ordered]@{Initialized=$initialized;Value=$value;Failure=$failure;JoinedHelperTransports=@($script:JoinedHelperTransports.ToArray())}
if($Scenario -ceq 'catch-config-missing'){$report.ControlBodyEntered=$script:ControlBodyEntered;$report.ControlNonce=$script:ControlNonce;$report.ControlChildPid=$script:ControlChildPid;$report.ControlSeq0Read=$script:ControlSeq0Read;$report.ControlObserverErrors=$script:ControlObserverErrors;$report.ControlReadProvenance=$script:ControlReadProvenance}
[IO.File]::WriteAllText((Join-Path $Folder 'powershell.json'),($report|ConvertTo-Json -Depth 12 -Compress))
