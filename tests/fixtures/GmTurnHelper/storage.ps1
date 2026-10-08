param([string]$RepoRoot,[string]$SessionPath,[string]$Scenario,[string]$Folder,[string]$TestSupport)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1')

# Only the dedicated helper's process factory changes. The old helper does not
# call it on RED. A later production helper retains its actual framing, body,
# scope/lease, close and Dispose functions; TestSupport injects original FS hooks.
$script:JoinedHelperTransports=[Collections.Generic.List[object]]::new()
$script:OriginalDispose=${function:Dispose-GmOperationTransport}
function Dispose-GmOperationTransport {
    param($Context)
    if($Context.disposed){return (& $script:OriginalDispose $Context)}
    $originalPid=$Context.process.Id
    # Observe the original handle, never reopen a PID. A helper requiring the
    # production Dispose kill fallback is a fixture failure, not causal evidence.
    $exited=$Context.process.WaitForExit(3500)
    & $script:OriginalDispose $Context
    $script:JoinedHelperTransports.Add([pscustomobject]@{ProcessId=$originalPid;ExitedBeforeDispose=$exited;Disposed=$Context.disposed;ExitCode=$Context.exitCode})
}
function Start-BoeHelperProcess {
    param([string]$Root,[AllowNull()][string]$ExpectedGeneration)
    $start=[Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute=$false
    $start.RedirectStandardInput=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    $expected=if([string]::IsNullOrEmpty($ExpectedGeneration)){'initialize'}else{$ExpectedGeneration}
    foreach($arg in @($TestSupport,'helper-storage-bootstrap',$Root,$Folder,$expected)){[void]$start.ArgumentList.Add($arg)}
    return [Diagnostics.Process]::Start($start)
}
function Wait-FixtureRelease {
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not [IO.File]::Exists((Join-Path $Folder 'continue')) -and $watch.Elapsed.TotalSeconds -lt 8){Start-Sleep -Milliseconds 10}
    if(-not [IO.File]::Exists((Join-Path $Folder 'continue'))){throw 'Fixture did not release the initialized helper.'}
}

$initialized=$false; $value=$null; $failure=$null
try {
    Initialize-BoeGmTurnHelper -GameSessionPath $SessionPath
    $initialized=$true
    [IO.File]::WriteAllText((Join-Path $Folder 'initialized'),'actual public Init returned')
    if($Scenario -notin @('init-held','generation-missing','generation-malformed')){Wait-FixtureRelease}
    switch($Scenario) {
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
[IO.File]::WriteAllText((Join-Path $Folder 'powershell.json'),($report|ConvertTo-Json -Depth 12 -Compress))
