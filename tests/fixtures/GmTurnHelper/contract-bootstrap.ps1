param([string]$Folder,[string]$TestSupport)
# Inject process construction and observation only; all public helper bodies and
# the dedicated C# role remain original. Re-dot-sourcing installs no new authority.
$script:ContractFolder=$Folder;$script:ContractSupport=$TestSupport
if(-not $script:ContractDispose){$script:ContractDispose=${function:Dispose-GmOperationTransport}}
function Dispose-GmOperationTransport {
 param($Context)
 if($Context.disposed){return (& $script:ContractDispose $Context)}
 $originalPid=$Context.process.Id;$exited=$Context.process.WaitForExit(3500)
 & $script:ContractDispose $Context
 [IO.File]::AppendAllText((Join-Path $script:ContractFolder 'joined.jsonl'),((@{ProcessId=$originalPid;ExitedBeforeDispose=$exited;ExitCode=$Context.exitCode}|ConvertTo-Json -Compress)+[Environment]::NewLine))
}
function Start-BoeHelperProcess {
 param([string]$Root,[AllowNull()][string]$ExpectedGeneration)
 $start=[Diagnostics.ProcessStartInfo]::new('dotnet');$start.UseShellExecute=$false;$start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $expected=if($ExpectedGeneration){$ExpectedGeneration}else{'initialize'}
 $arguments=@($script:ContractSupport,'helper-current-bootstrap',$Root,$script:ContractFolder,$expected,'original-contract')
 if($start.PSObject.Properties['ArgumentList']){foreach($argument in $arguments){[void]$start.ArgumentList.Add($argument)}}
 else{$start.Arguments=($arguments|ForEach-Object{ConvertTo-BoeWindowsArgument $_}) -join ' '}
 return [Diagnostics.Process]::Start($start)
}
