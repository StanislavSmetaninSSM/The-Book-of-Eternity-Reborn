param([string]$RepoRoot,[string]$CodeRoot,[string]$StatusPath,[int]$TimeoutMilliseconds)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_bridge_request_transport.ps1')
try {
    $receipt=Invoke-BoeBridgeRequestProcess -CodeRoot $CodeRoot -StatusJson ([IO.File]::ReadAllText($StatusPath)) -RequestJson '{"command":"diagnostics"}' -TimeoutMilliseconds $TimeoutMilliseconds
    [Console]::Out.WriteLine((@{Succeeded=$true;Receipt=$receipt}|ConvertTo-Json -Depth 10 -Compress))
} catch {
    [Console]::Out.WriteLine((@{Succeeded=$false;HelperExited=$_.Exception.Data['HelperExited'];StdoutEof=$_.Exception.Data['StdoutEof'];StderrEof=$_.Exception.Data['StderrEof'];FailureType=$_.Exception.GetType().Name}|ConvertTo-Json -Depth 5 -Compress))
}
