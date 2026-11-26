param([Parameter(Mandatory)][string]$Daemon)
$ErrorActionPreference='Stop'
$tokens=$null;$errors=$null
$ast=[System.Management.Automation.Language.Parser]::ParseFile($Daemon,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Source parse failed'}
$wanted='Get-GmObjectPropertyValue','Test-GmRequestCorrelation','Wait-CorrelatedValidationRepairRequest'
foreach($name in $wanted){
 $m=@($ast.FindAll({param($n) $n -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name},$false))
 if($m.Count -ne 1){throw "Unexpected function count: $name"}
 . ([scriptblock]::Create($m[0].Extent.Text))
}
$RepairRequestFile='game_state/control/validation_repair_request.json'
$turn=[pscustomobject]@{sessionId='session-control';requestId='request-control';turnNumber=42}
$script:CorrelatedRepairPollMilliseconds=60
function New-GmDaemonReadFailure {param($message,$inner) [IO.IOException]::new($message,$inner)}
function Get-GmDaemonCorrelatedRepairObservation {param($path)
 if($path -cne $RepairRequestFile){throw 'Wrong path'}
 $script:calls++
 if(!$script:samples.Count){throw 'Unexpected extra observation after known grace and admitted final sample.'}
 $sample=$script:samples.Dequeue()
 switch ($sample) {
  'missing' { return [pscustomobject]@{contended=$false;file=@{kind='Missing'}} }
  'busy' { return [pscustomobject]@{contended=$true;file=$null} }
  'repair' {
    $wire=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(($turn|ConvertTo-Json -Compress)))
    return [pscustomobject]@{contended=$false;file=@{kind='File';bytes=$wire}}
  }
  'loss' {throw (New-GmDaemonReadFailure 'Original wire loss' ([IO.EndOfStreamException]::new()))}
  default {throw 'Unknown test sample'}
 }
}
function Run-Row([string]$name,[object[]]$rows,[string]$expect){
 $script:samples=[Collections.Generic.Queue[string]]::new()
 foreach($row in $rows){$script:samples.Enqueue([string]$row)}
 $script:calls=0
 $wall=[Diagnostics.Stopwatch]::StartNew()
 $result=$null
 try{
   $r=Wait-CorrelatedValidationRepairRequest -TurnRequest $turn -GraceMilliseconds 200
   $result=if($r){if($r.requestId -cne $turn.requestId){throw 'Wrong repair'};'repair'}else{'absent'}
 }
 catch {
   if($_.Exception.Message -ceq 'Original wire loss'){$result='loss'}else{throw}
 }
 if($result -cne $expect -or $script:calls -ne $rows.Count -or $script:samples.Count){throw "$name unexpected $result x$script:calls"}
 if($rows.Count -gt 2 -and $wall.ElapsedMilliseconds -lt 200){throw "$name returned before elapsed grace"}
 [ordered]@{name=$name;outcome=$result;calls=$script:calls;ms=$wall.ElapsedMilliseconds}
}
@(
 (Run-Row 'four lock refusals do not restart grace or stand in for final absence' @('missing','busy','busy','busy','busy','missing') 'absent'),
 (Run-Row 'matching repair has priority over elapsed grace' @('missing','busy','busy','busy','busy','repair') 'repair'),
 (Run-Row 'actual read reply loss still refuses' @('missing','busy','busy','busy','busy','loss') 'loss'),
 (Run-Row 'prior busy does not start grace' @('busy','busy','busy','busy','missing','busy','busy','busy','busy','missing') 'absent'),
 (Run-Row 'first matching repair is immediate' @('repair') 'repair')
) | ConvertTo-Json -Depth 5

function Get-GmDaemonCorrelatedRepairObservation {param($path)
 $script:allBusyCalls++
 return [pscustomobject]@{contended=$true;file=$null}
}
$script:allBusyCalls=0
$script:CorrelatedRepairPollMilliseconds=100
$total=[Diagnostics.Stopwatch]::StartNew()
try {
  $null=Wait-CorrelatedValidationRepairRequest -TurnRequest $turn -GraceMilliseconds 200
  throw 'All-busy was falsely accepted'
} catch {
  if($_.Exception.Message -cne 'Original repair observation did not settle within its bounded window; no replay.'){throw}
}
if($total.ElapsedMilliseconds -lt 20000 -or $total.ElapsedMilliseconds -gt 23000 -or $script:allBusyCalls -lt 80){throw 'Bounded busy proof missing'}
[ordered]@{name='all busy never claims absence';outcome='bounded refusal';calls=$script:allBusyCalls;ms=$total.ElapsedMilliseconds}|ConvertTo-Json
