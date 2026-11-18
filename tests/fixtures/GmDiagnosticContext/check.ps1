param([Parameter(Mandatory)][string]$Repo,[Parameter(Mandatory)][string]$Case)
$ErrorActionPreference='Stop'
. (Join-Path $Repo 'BookOfEternityClient/Launcher/gm_main_operation.ps1')
. (Join-Path $Repo 'BookOfEternityClient/Launcher/GM_Turn_Helper.ps1')
function Require($value,[string]$message){if(-not $value){throw $message}}
$identity=[pscustomobject]@{rootKey='/inert/context';runId='run';epoch=1;hostInstanceId='host';backend=2;bootId='boot';generationId=('a'*32)}
$original=[pscustomobject]@{pinId='pin';closeId='close';operationId='operation';identity=$identity;outcome=0;closingFailed=$false}
$terminal=[pscustomobject]@{pinId='pin';closeId='close';operationId='operation';identity=$identity;outcome=0;closingFailed=$false}
$reply=[pscustomobject]@{ok=$true;sequence=1;state='closed-observed';completionKind='original-main';terminalClose=$terminal;closeObserved=$true;continuationFailed=$false;publicationDisposition='Committed'}
$inputWriter=[IO.StringWriter]::new()
$process=[pscustomobject]@{StandardInput=$inputWriter;StandardOutput=[IO.StringReader]::new(($reply|ConvertTo-Json -Depth 12 -Compress)+"`n");StandardError=[IO.StringReader]::new('')}
$session='/inert/context/game_session';$Mode='initialize'
$constructor=if($Case -eq 'main-close'){'Open-GmParticipatingOperation'}else{'Open-BoeHelperScope'}
$source=if($constructor -eq 'Open-GmParticipatingOperation'){'gm_main_operation.ps1'}else{'gm_turn_helper_transport.ps1'}
$tokens=$null;$parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $Repo ('BookOfEternityClient/Launcher/'+$source)),[ref]$tokens,[ref]$parseErrors)
Require ($parseErrors.Count -eq 0) 'Production parse failed.'
$function=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $constructor},$false)
$assignment=@($function.Body.FindAll({param($node) $node -is [Management.Automation.Language.AssignmentStatementAst] -and $node.Left.Extent.Text -ceq '$context'},$true))
Require ($assignment.Count -eq 1) 'Exact original constructor assignment missing or ambiguous.'
$context=& ([scriptblock]::Create($assignment[0].Extent.Text+'; $context'))
$context.originalClose=$original
if($Case -ne 'main-close'){$context.generation='a'*32}
if($Case -in @('throwing-write','throwing-timeout')){
    $context | Add-Member -MemberType ScriptProperty -Name lastAction -Value {throw 'hostile diagnostic getter'} -SecondValue {param($value) throw 'hostile diagnostic setter'}
}
if($Case -in @('missing-timeout','throwing-timeout')){
    $pending=[pscustomobject]@{Result=$null}
    $pending | Add-Member ScriptMethod Wait {param($milliseconds) return $false}
    $reader=[pscustomobject]@{Pending=$pending}
    $reader | Add-Member ScriptMethod ReadLineAsync {return $this.Pending}
    $context.process.StandardOutput=$reader
    Set-StrictMode -Version Latest
    $failure=$null
    try {Read-GmOperationReply $context | Out-Null}catch {$failure=$_}
    Require ($null -ne $failure) 'Missing original timeout.'
    Require ($failure.Exception.Message -ceq 'Original operation reply unavailable; no replay.') 'Optional diagnostic replaced original timeout.'
    Require ($context.lost -eq $true) 'Mandatory transport loss was not recorded.'
    Require ($inputWriter.ToString() -ceq '') 'Timeout emitted a command.'
}else{
    if($Case -eq 'dedicated-initialize'){
        $script:returnedContext=$context
        function Open-BoeHelperScope {param($Mode,$SessionPath,$ExpectedGeneration) return $script:returnedContext}
        $script:completed=0
        function Complete-BoeHelperTransport {param($Context) $script:completed++}
        Initialize-BoeGmTurnHelper -GameSessionPath $session
        Require ($script:completed -eq 1 -and $script:BoeHelperGeneration -ceq ('a'*32) -and $script:BoeGameSessionPath -ceq $session) 'Initialize state changed.'
    }else{Close-GmParticipatingOperation $context 0}
    $lines=@($inputWriter.ToString().Split("`n",[StringSplitOptions]::RemoveEmptyEntries))
    Require ($lines.Count -eq 1) 'Expected exactly one original close frame.'
    $frame=$lines[0]|ConvertFrom-Json
    Require ($frame.action -ceq 'close' -and $frame.sequence -eq 1 -and $frame.outcome -eq 0) 'Close frame changed.'
    Require ($context.sequence -eq 1 -and $context.closed -and -not $context.lost -and $context.closeObserved) 'Close state changed.'
    Require ([object]::ReferenceEquals($context.originalClose,$original)) 'Original tuple replaced.'
    foreach($key in @('pinId','closeId','operationId')){Require ($context.terminalClose.$key -ceq $original.$key) 'Terminal tuple mismatch.'}
    foreach($key in @('rootKey','runId','epoch','hostInstanceId','backend','bootId','generationId')){Require ($context.terminalClose.identity.$key -ceq $identity.$key) 'Terminal owner mismatch.'}
}
[Console]::Out.WriteLine(($Case+' PASS'))
