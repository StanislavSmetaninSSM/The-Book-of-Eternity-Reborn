param([string]$RepoRoot,[string]$SessionPath,[string]$Scenario,[string]$Folder,[string]$TestSupport)
$ErrorActionPreference='Stop'
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
. (Join-Path $RepoRoot 'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1')
$script:Joined=[Collections.Generic.List[object]]::new();$script:OriginalDispose=${function:Dispose-GmOperationTransport}
function Dispose-GmOperationTransport {
 param($Context)
 if($Context.disposed){return (& $script:OriginalDispose $Context)}
 $pidValue=$Context.process.Id;$exited=$Context.process.WaitForExit(3500)
 & $script:OriginalDispose $Context
 $script:Joined.Add([pscustomobject]@{ProcessId=$pidValue;ExitedBeforeDispose=$exited;ExitCode=$Context.exitCode;OriginalClose=$Context.originalClose;TerminalClose=$Context.terminalClose;CloseObserved=$Context.closeObserved;Outcome=$Context.closeOutcome;Uncertain=$Context.publicationUncertain;Lost=$Context.lost;LastReply=$Context.lastCommandReply})
}
function Start-BoeHelperProcess {
 param([string]$Root,[AllowNull()][string]$ExpectedGeneration)
 $start=[Diagnostics.ProcessStartInfo]::new('dotnet');$start.UseShellExecute=$false;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $expected=if($ExpectedGeneration){$ExpectedGeneration}else{'initialize'}
 foreach($arg in @($TestSupport,'helper-current-bootstrap',$Root,$Folder,$expected,$Scenario)){[void]$start.ArgumentList.Add($arg)}
 return [Diagnostics.Process]::Start($start)
}
function Require($Condition,[string]$Message){if(-not $Condition){throw $Message}}
function Expect-Refusal([scriptblock]$Action){$refused=$false;try{& $Action|Out-Null}catch{$refused=$true};Require $refused 'Expected original refusal did not occur.'}
$target='output/helper-current.json';$before=[IO.File]::ReadAllBytes((Join-Path $SessionPath $target));$evidence=[ordered]@{}
try {
 if($Scenario -cne 'nested-default'){Initialize-BoeGmTurnHelper $SessionPath}
 switch($Scenario){
  'large-read' {
   $value=Read-BoeJson 'output/helper-large.json';Require ($value.value.Length -eq 60000) 'Large multibyte read was truncated.'
   $evidence.Characters=$value.value.Length
  }
  'large-write' {
   $value=('Ж🙂' * 600000)
   $expected=[Text.UTF8Encoding]::new($false).GetBytes((([ordered]@{value=$value}|ConvertTo-Json -Depth 100)+[Environment]::NewLine))
   Require ([Convert]::ToBase64String($expected).Length -gt 4194304) 'Write did not exceed the old encoded frame ceiling.'
   Write-BoeJson $target ([ordered]@{value=$value})
   $actual=[IO.File]::ReadAllBytes((Join-Path $SessionPath $target));Require ((Get-BoeSha256Hex $actual) -ceq (Get-BoeSha256Hex $expected)) 'Large exact UTF8/newline bytes changed.'
   $evidence.ActualBytes=$actual.Length;$evidence.EncodedCharacters=[Convert]::ToBase64String($expected).Length
  }
  {$_ -like 'chunk-*'} {
   $context=Open-BoeHelperScope 'write' $SessionPath $script:BoeHelperGeneration
   $id=[Guid]::NewGuid().ToString('N');$payload=[Text.Encoding]::UTF8.GetBytes((@{action='publish';path=$target;bytes=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"value":"uncommitted"}'))}|ConvertTo-Json -Compress))
   try {
    $length=$payload.Length;if($Scenario -eq 'chunk-trailing'){$length--};if($Scenario -eq 'chunk-incomplete'){$length++}
    $hash=Get-BoeSha256Hex $payload;if($Scenario -eq 'chunk-hash'){$hash='0'*64}
    [void](Send-BoeHelperFrame $context @{action='request-begin';transfer=$id;length=$length;hash=$hash})
    if($Scenario -eq 'chunk-duplicate'){
     [void](Send-BoeHelperFrame $context @{action='request-chunk';transfer=$id;offset=0;bytes=[Convert]::ToBase64String($payload,0,8)})
     Expect-Refusal {Send-BoeHelperFrame $context @{action='request-chunk';transfer=$id;offset=0;bytes=[Convert]::ToBase64String($payload,0,8)}}
    } elseif($Scenario -eq 'chunk-offset'){
     Expect-Refusal {Send-BoeHelperFrame $context @{action='request-chunk';transfer=$id;offset=1;bytes=[Convert]::ToBase64String($payload)}}
    } elseif($Scenario -eq 'chunk-trailing'){
     Expect-Refusal {Send-BoeHelperFrame $context @{action='request-chunk';transfer=$id;offset=0;bytes=[Convert]::ToBase64String($payload)}}
    } else {
     [void](Send-BoeHelperFrame $context @{action='request-chunk';transfer=$id;offset=0;bytes=[Convert]::ToBase64String($payload)})
     Expect-Refusal {Send-BoeHelperFrame $context @{action='request-end';transfer=$id}}
    }
    Require ((Get-BoeSha256Hex ([IO.File]::ReadAllBytes((Join-Path $SessionPath $target)))) -ceq (Get-BoeSha256Hex $before)) 'Malformed transfer published partial data.'
    Close-GmParticipatingOperation $context 1
   }finally{Complete-BoeHelperTransport $context}
   # Same row also proves actual read-role publication refusal.
   $context=Open-BoeHelperScope 'read' $SessionPath $script:BoeHelperGeneration;$script:BoeHelperScope=$context
   try {Expect-Refusal {Invoke-BoeHelperRequest @{action='publish';path=$target;bytes=[Convert]::ToBase64String($payload)}};Close-GmParticipatingOperation $context 1}
   finally{$script:BoeHelperScope=$null;Complete-BoeHelperTransport $context}
   Require ((Get-BoeSha256Hex ([IO.File]::ReadAllBytes((Join-Path $SessionPath $target)))) -ceq (Get-BoeSha256Hex $before)) 'Read role published bytes.'
  }
  'partial-close' {
   $context=Open-BoeHelperScope 'write' $SessionPath $script:BoeHelperGeneration;$id=[Guid]::NewGuid().ToString('N')
   try {
    [void](Send-BoeHelperFrame $context @{action='request-begin';transfer=$id;length=100;hash=('0'*64)})
    [void](Send-BoeHelperFrame $context @{action='request-chunk';transfer=$id;offset=0;bytes='YWJj'})
    Close-GmParticipatingOperation $context 2
   }finally{Complete-BoeHelperTransport $context}
   Require ($context.closeOutcome -eq 2 -and $context.localScopeCompleted) 'Partial transfer close did not preserve actual cancellation.'
   Require ((Get-BoeSha256Hex ([IO.File]::ReadAllBytes((Join-Path $SessionPath $target)))) -ceq (Get-BoeSha256Hex $before)) 'Partial close published bytes.'
  }
  'known-rollback' {
   Expect-Refusal {Write-BoeJson $target ([ordered]@{value='after'})}
   Require (-not $script:BoeHelperFailure) 'Known rollback became sticky uncertainty.'
   Require ((Read-BoeJson $target).value -ceq 'before') 'Known rollback did not restore before.'
  }
  'publication-unknown' {
   Expect-Refusal {Write-BoeJson $target ([ordered]@{value='after'})}
   $count=$script:Joined.Count
   Expect-Refusal {Initialize-BoeGmTurnHelper $SessionPath};Expect-Refusal {Read-BoeJson $target}
   Require ($script:Joined.Count -eq $count -and $script:BoeHelperFailure) 'Unknown operation reopened a helper.'
   $last=$script:Joined[$count-1];Require ($last.Uncertain -and $last.Outcome -eq 5 -and -not $last.Lost) 'Unknown decision lost explicit original close.'
  }
  'committed-debt' {
   Write-BoeJson $target ([ordered]@{value='after'})
   $path=[IO.Path]::GetFullPath((Join-Path $SessionPath $target))
   Require ($script:BoeReadBaselines[$path] -ceq (Get-BoeSha256Hex ([IO.File]::ReadAllBytes($path)))) 'Known committed baseline was not advanced.'
   Require (-not $script:BoeHelperFailure) 'Committed debt became uncertainty.'
  }
  'case-baselines' {
   $literal=Read-BoeJson 'output/literal\leaf.json';Require ($literal.value -ceq 'literal') 'Literal Linux backslash changed target during policy/read normalization.'
   $literal.value='updated-literal';Write-BoeJson 'output/literal\leaf.json' $literal
   Require ((Read-BoeJson 'output/literal/leaf.json').value -ceq 'slash-sibling') 'Literal target changed its slash sibling.'
   Require ((Read-BoeJson 'output/literal\leaf.json').value -ceq 'updated-literal') 'Literal destination publication was redirected.'
   $upper=Read-BoeJson 'output/A.json';$lower=Read-BoeJson 'output/a.json'
   [IO.File]::WriteAllText((Join-Path $SessionPath 'output/A.json'),'{"value":"other"}')
   $lower.value='updated-lower';Write-BoeJson 'output/a.json' $lower
   Expect-Refusal {Write-BoeJson 'output/A.json' $upper}
   Require ((Read-BoeJson 'output/a.json').value -ceq 'updated-lower') 'Distinct case target or unrelated cross-call baseline regressed.'
  }
  'nested-default' {
   $context=Open-GmParticipatingOperation $SessionPath;$global:BoeMainOperationContext=$context
   try {
    Expect-Refusal {Initialize-BoeGmTurnHelper $SessionPath};Require ($script:Joined.Count -eq 0) 'Nested helper launched before refusal.'
    Expect-Refusal {Send-GmOperationCommand $context @{action='request-begin';path='game_state/control/helper-role.bin'}}
    [void](Send-GmOperationCommand $context @{action='write';path='game_state/control/helper-role.bin';bytes='AQID'})
    Close-GmParticipatingOperation $context 0
    Require $context.localScopeCompleted 'Original default control was no longer usable/closeable.'
   }finally{$global:BoeMainOperationContext=$null;Dispose-GmOperationTransport $context}
  }
  'running-owner' {
   $value=Read-BoeJson $target;$value.value='running';Write-BoeJson $target $value
   Require ($script:Joined.Count -eq 3) 'Unexpected short helper lifetime count.'
   foreach($child in $script:Joined){Require ($child.CloseObserved -and $child.OriginalClose -and $child.TerminalClose -and $child.Outcome -eq 0) 'Original live main receipt was not observed.'}
  }
  'ancestor-file' {
   [IO.File]::WriteAllText((Join-Path $SessionPath 'output/blocker'),'file')
   Expect-Refusal {Read-BoeJson 'output/blocker/child.json'}
   Require ([IO.File]::ReadAllText((Join-Path $SessionPath 'output/blocker')) -ceq 'file') 'File ancestor changed.'
  }
  'directory-leaf' {
   [IO.Directory]::CreateDirectory((Join-Path $SessionPath 'output/directory.json'))|Out-Null
   Expect-Refusal {Read-BoeJson 'output/directory.json'}
  }
  'parse-fallback' {
   [IO.Directory]::CreateDirectory((Join-Path $SessionPath 'game_state/misc'))|Out-Null
   [IO.Directory]::CreateDirectory((Join-Path $SessionPath 'game_state/player'))|Out-Null
   $first=Join-Path $SessionPath 'game_state/misc/characteristics.json';[IO.File]::WriteAllText($first,'{malformed')
   [IO.File]::WriteAllText((Join-Path $SessionPath 'game_state/player/player_status.json'),'{"trade":27}')
   Require ((Get-BoePlayerTradeValue) -eq 27) 'Existing malformed-JSON trade fallback changed.'
   [IO.File]::Delete($first);[IO.File]::CreateSymbolicLink($first,(Join-Path $SessionPath 'game_state/player/player_status.json'))|Out-Null
   Expect-Refusal {Get-BoePlayerTradeValue}
  }
  default {throw 'Unknown current helper scenario.'}
 }
 $evidence.Success=$true
} catch {$evidence.Failure=$_.Exception.ToString();throw}
finally {$evidence.Joined=@($script:Joined.ToArray());[IO.File]::WriteAllText((Join-Path $Folder 'powershell.json'),($evidence|ConvertTo-Json -Depth 30 -Compress))}
