param([string]$RepoRoot,[string]$SessionPath)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$script:refused=$false
$script:bodyStarted=$false
try {
Invoke-GmParticipatingConsumer $SessionPath {
    $script:bodyStarted=$true
    try { Write-GmCanonicalText $SessionPath (Join-Path $SessionPath 'game_state/control/oversized.txt') ('x' * 4194304) }
    catch { $script:refused=$true }
}
} catch { [Console]::Error.WriteLine("controlled-body-started=$script:bodyStarted; controlled-bound-refused=$script:refused");throw }
if(-not $script:refused){throw 'Unsent oversized command was not refused.'}
[ordered]@{refused=$true;closed=$true}|ConvertTo-Json -Compress
