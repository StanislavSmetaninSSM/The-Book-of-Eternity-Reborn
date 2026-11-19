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
# Only the unrelated gameplay body is injected; execute verbatim actual catch/finally.
$run=[scriptblock]::Create('try { Invoke-ControlledBody } '+$tries[0].CatchClauses[0].Extent.Text+' finally '+$tries[0].Finally.Extent.Text)
function Write-Log {param($Message,$Level,$Color)}
function Invoke-ControlledBody {if($script:bodyFailure){throw $script:bodyFailure}}
function Write-DaemonStatus {param($Status,$Reason);$script:statusCalls++;if($script:statusFailure){throw $script:statusFailure}}
$failures=@();$reports=@()
foreach($body in @('success','read-refusal','ordinary')) {foreach($status in @('success','failure')) {
 $script:IsProcessing=$true;$script:ErrorCount=0;$script:statusCalls=0
 $script:bodyFailure=if($body -eq 'read-refusal'){New-GmDaemonReadFailure 'controlled primary snapshot refusal' $null}elseif($body -eq 'ordinary'){[IO.IOException]::new('controlled ordinary body failure')}else{$null}
 $script:statusFailure=if($status -eq 'failure'){[IO.IOException]::new('controlled secondary status failure')}else{$null}
 $caught=$null;try{& $run}catch{$caught=$_}
 $expectedPrimary=$body -eq 'read-refusal'
 $checks=[ordered]@{reset=(-not $script:IsProcessing);statusOnce=($script:statusCalls -eq 1);ordinaryBehavior=($script:ErrorCount -eq $(if($body -eq 'ordinary'){1}else{0}));primary=($expectedPrimary -eq [bool](Test-GmDaemonReadFailure $caught));surface=([bool]$caught -eq ($expectedPrimary -or $status -eq 'failure'))}
 if($expectedPrimary -and $status -eq 'failure'){$checks.secondaryRetained=($null -ne $caught -and $caught.Exception.Data.Contains('DaemonFinalStatusFailure'))}
 $reports += [pscustomobject]@{body=$body;status=$status;checks=$checks;failure=$(if($caught){$caught.Exception.ToString()}else{$null})}
 foreach($key in $checks.Keys){if(-not $checks[$key]){$failures += "$FunctionName/$body/$status/$key"}}
}}
$reports|ConvertTo-Json -Depth 8
if($failures.Count){throw ('Actual consumer regression: '+($failures -join ', '))}
