param([string]$RepoRoot,[string]$SessionPath)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$refused=$false
Invoke-GmParticipatingConsumer $SessionPath {
    try { Write-GmCanonicalText $SessionPath (Join-Path $SessionPath 'game_state/control/oversized.txt') ('x' * 4194304) }
    catch { $script:refused=$true }
}
if(-not $script:refused){throw 'Unsent oversized command was not refused.'}
[ordered]@{refused=$true;closed=$true}|ConvertTo-Json -Compress
