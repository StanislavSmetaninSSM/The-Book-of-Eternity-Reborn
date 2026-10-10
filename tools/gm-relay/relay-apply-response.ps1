param([Parameter(Mandatory)][string]$SessionPath,[Parameter(Mandatory)][string]$RequestPath,[Parameter(Mandatory)][string]$ResponsePath)
$ErrorActionPreference = 'Stop'
trap {
    $relayOriginalFailure = $_
    try {
        # Retain the existing causal exception; diagnostics cannot replace it.
        [Console]::Error.WriteLine($relayOriginalFailure.Exception.ToString())
        [Console]::Error.WriteLine($relayOriginalFailure.ScriptStackTrace)
    } catch {}
    break
}
# Fixed developer packet consumer, no dynamic script evaluation, jobs or spawned commands.
. (Join-Path $SessionPath 'game_state/control/gm_turn_helper.bootstrap.ps1')
$request = Get-Content -LiteralPath $RequestPath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
$packet = Get-Content -LiteralPath $ResponsePath -Raw -Encoding utf8 | ConvertFrom-Json -AsHashtable
if ($packet.Completion -cne $request.Kind -or $packet.Writes.Count -gt 32) { throw 'Response kind/count mismatch.' }
function Assert-RelayWitnesses {
    foreach ($entry in $request.Witnesses.GetEnumerator()) {
        $path = Join-Path $SessionPath $entry.Key
        $actual = if ([IO.File]::Exists($path)) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() } else { 'missing' }
        if ($actual -cne $entry.Value) { throw "Original request/pending changed: $($entry.Key)" }
    }
}
Assert-RelayWitnesses
$paths = @{}
foreach ($write in $packet.Writes) {
    $path = [string]$write.Path
    if ($paths.ContainsKey($path)) { throw 'Duplicate output target.' }
    $paths[$path] = $true
    $full = Resolve-BoeSessionPath -RelativePath $path
    $actual = if ([IO.File]::Exists($full)) { (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() } else { 'missing' }
    if ($actual -cne $write.ExpectedSHA256) { throw "GM read witness changed: $path" }
    if ([IO.File]::Exists($full)) { $null = Read-BoeJson -RelativePath $path }
}
foreach ($write in $packet.Writes) {
    Assert-RelayWitnesses
    $witnesses = @{} + $request.Witnesses
    $witnesses[[string]$write.Path] = [string]$write.ExpectedSHA256
    Write-BoeJson -RelativePath $write.Path -Data $write.Data -ExpectedReadWitnesses $witnesses
}
Assert-RelayWitnesses
if ($request.Kind -ceq 'turn') { Complete-BoeTurn -FilesModified @($packet.FilesModified) }
else { Complete-BoeValidationRepair }
