param([string]$RepoRoot,[string]$Scenario)
$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Actual daemon parse failed.'}
foreach($name in @('Test-GmBridgeArtifactWritingIntent','Test-GmBridgeArtifactWritingStall')) {
 $node=$ast.Find({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name},$true)
 if($null -eq $node){throw "Actual function missing: $name"}
 . ([scriptblock]::Create($node.Extent.Text))
}
# Only the diagnostics transport is controlled. No daemon/CLI top-level code,
# writes, owner acquisition, real devices, network or model execution.
$script:ArtifactWritingStallMinimumSeconds=120
$script:ArtifactWritingStallNoProgressSeconds=180
$script:Snapshot=[pscustomobject]@{
 status=[pscustomobject]@{ready=$false;state='OperatorNotReady'}
 diagnostics=[pscustomobject]@{outputVersion=10;visibleScreenText="NEUTRAL WORKING`nRELAY> ";recentOutputTail='TERMINAL CHECKLIST: write the terminal signal as the LAST step. Complete-BoeTurn'}
}
function Get-GmBridgeDiagnosticsSnapshot {$script:Snapshot}
$watch=@{}
switch($Scenario) {
 'echo-only' {
  [void](Test-GmBridgeArtifactWritingStall -ElapsedSeconds 30 -WatchState $watch)
  $result=Test-GmBridgeArtifactWritingStall -ElapsedSeconds 225 -WatchState $watch
  if($result.isStalled){throw 'Historical echoed request falsely stopped current model generation.'}
  if(-not $result.recentOutputTail.Contains('Complete-BoeTurn')){throw 'Historical diagnostics lost.'}
 }
 'visible-stall' {
  $script:Snapshot.diagnostics.visibleScreenText='Writing the accepted-turn artifacts with Complete-BoeTurn'
  [void](Test-GmBridgeArtifactWritingStall -ElapsedSeconds 30 -WatchState $watch)
  $result=Test-GmBridgeArtifactWritingStall -ElapsedSeconds 225 -WatchState $watch
  if(-not $result.isStalled){throw 'Actual visible artifact-writing stall was lost.'}
 }
 'output-progress' {
  $script:Snapshot.diagnostics.visibleScreenText='Writing the accepted-turn artifacts'
  [void](Test-GmBridgeArtifactWritingStall -ElapsedSeconds 30 -WatchState $watch)
  $script:Snapshot.diagnostics.outputVersion=11
  $result=Test-GmBridgeArtifactWritingStall -ElapsedSeconds 225 -WatchState $watch
  if($result.isStalled -or $result.noProgressElapsedSeconds -ne 0){throw 'Actual output progress ignored.'}
 }
 'ready' {
  $script:Snapshot.diagnostics.visibleScreenText='Writing the accepted-turn artifacts'
  [void](Test-GmBridgeArtifactWritingStall -ElapsedSeconds 30 -WatchState $watch)
  $script:Snapshot.status.ready=$true
  $result=Test-GmBridgeArtifactWritingStall -ElapsedSeconds 225 -WatchState $watch
  if($result.isStalled){throw 'Ready terminal classified as a working stall.'}
 }
 'intent-cleared' {
  $script:Snapshot.diagnostics.visibleScreenText='Writing the accepted-turn artifacts'
  [void](Test-GmBridgeArtifactWritingStall -ElapsedSeconds 30 -WatchState $watch)
  $script:Snapshot.diagnostics.visibleScreenText="NEUTRAL WORKING`nRELAY> "
  $result=Test-GmBridgeArtifactWritingStall -ElapsedSeconds 225 -WatchState $watch
  if($result.isStalled){throw 'Cleared visible intent survived through historical tail.'}
 }
 'missing-diagnostics' {
  $script:Snapshot=$null
  if($null -ne (Test-GmBridgeArtifactWritingStall -ElapsedSeconds 225 -WatchState $watch)){throw 'Unavailable observation fabricated stall.'}
 }
 default {throw 'Unknown inert scenario.'}
}
@{Scenario=$Scenario;Passed=$true;RealDaemonFunctions=$true;ModelCalls=0;ProcessesStarted=0;CanonicalWrites=0}|ConvertTo-Json -Compress
