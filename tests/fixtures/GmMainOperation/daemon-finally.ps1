param([string]$RepoRoot,[string]$FunctionName)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $RepoRoot 'BookOfEternityClient/game_master_daemon.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Production daemon parse failure'}
$fn=@($ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $FunctionName},$true))
if($fn.Count -ne 1){throw 'Exact production consumer missing'}
$tries=@($fn[0].Body.EndBlock.Statements|Where-Object{$_ -is [Management.Automation.Language.TryStatementAst]})
if($tries.Count -ne 1 -or $tries[0].CatchClauses.Count -ne 1 -or -not $tries[0].Finally){throw 'Production catch/finally shape changed'}
# Preserve the entire real function, including params, pre-try guards/setup and tail.
# Replace ONLY this selected gameplay try body; no reconstructed catch/finally.
$source=$fn[0].Extent.Text
$bodyStart=$tries[0].Body.Extent.StartOffset-$fn[0].Extent.StartOffset
$bodyLength=$tries[0].Body.Extent.EndOffset-$tries[0].Body.Extent.StartOffset
$controlled=$source.Remove($bodyStart,$bodyLength).Insert($bodyStart,'{ Invoke-ControlledBody }')
. ([scriptblock]::Create($controlled))
function Contains-ExactException($Actual,$Expected) {
 if($Actual -is [Management.Automation.ErrorRecord]){$Actual=$Actual.Exception}
 while($null -ne $Actual){if([object]::ReferenceEquals($Actual,$Expected)){return $true};$Actual=$Actual.InnerException}
 return $false
}
function Write-Log {param($Message,$Level,$Color)}
function Invoke-ControlledBody {if($script:bodyFailure){throw $script:bodyFailure}}
function Write-DaemonStatus {param($Status,$Reason);$script:statusCalls++;$script:observedStatus=$Status;$script:observedReason=$Reason;if($script:statusFailure){throw $script:statusFailure}}
$failures=@();$reports=@()
foreach($body in @('success','read-refusal','ordinary')) {foreach($status in @('success','failure')) {
 $script:IsProcessing=$false;$script:ErrorCount=0;$script:statusCalls=0;$script:observedStatus=$null;$script:observedReason=$null
 $script:bodyFailure=if($body -eq 'read-refusal'){New-GmDaemonReadFailure 'controlled primary snapshot refusal' $null}elseif($body -eq 'ordinary'){[IO.IOException]::new('controlled ordinary body failure')}else{$null}
 $script:statusFailure=if($status -eq 'failure'){[IO.IOException]::new('controlled secondary status failure')}else{$null}
 $caught=$null;try{& $FunctionName -RequestPath 'controlled-unused'}catch{$caught=$_}
 $expectedPrimary=$body -eq 'read-refusal'
 $checks=[ordered]@{reset=(-not $script:IsProcessing);statusOnce=($script:statusCalls -eq 1);ordinaryBehavior=($script:ErrorCount -eq $(if($body -eq 'ordinary'){1}else{0}));primary=($expectedPrimary -eq [bool](Test-GmDaemonReadFailure $caught));surface=([bool]$caught -eq ($expectedPrimary -or $status -eq 'failure'))}
 $checks.statusReason=($script:observedStatus -ceq 'running' -and $script:observedReason -ceq $(if($FunctionName -ceq 'Process-TurnCore'){'turn_processing_finished'}else{'qte_effect_resolution_processing_finished'}))
 if($expectedPrimary){$checks.primaryIdentity=Contains-ExactException $caught $script:bodyFailure}
 if(-not $expectedPrimary -and $status -eq 'failure'){$checks.statusIdentity=Contains-ExactException $caught $script:statusFailure}
 if($expectedPrimary -and $status -eq 'failure'){
  $secondary=$null;$ex=$caught.Exception
  while($null -ne $ex){if($ex.Data.Contains('DaemonFinalStatusFailure')){$secondary=$ex.Data['DaemonFinalStatusFailure'];break};$ex=$ex.InnerException}
  $checks.secondaryRetained=Contains-ExactException $secondary $script:statusFailure
 }
 $reports += [pscustomobject]@{body=$body;status=$status;checks=$checks;failure=$(if($caught){$caught.Exception.ToString()}else{$null})}
 foreach($key in $checks.Keys){if(-not $checks[$key]){$failures += "$FunctionName/$body/$status/$key"}}
}}
$reports|ConvertTo-Json -Depth 8
if($failures.Count){throw ('Actual consumer regression: '+($failures -join ', '))}
