param([string]$Source,[string]$Session,[string]$Scenario)
$ErrorActionPreference='Stop'
if(-not $IsLinux){throw 'Linux fixture only'}
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($Source,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Launcher source parse failed'}
# Extract unchanged production function bodies. Only the external dotnet launch
# leaf is substituted; filesystem, Get-Process and Console probes are real.
$functionTexts=@($ast.EndBlock.Statements | Where-Object {$_ -is [Management.Automation.Language.FunctionDefinitionAst]} | ForEach-Object {$_.Extent.Text})
if($functionTexts.Count -eq 0){throw 'No top-level launcher functions found'}
$extracted=Join-Path $PSScriptRoot 'actual-launcher-functions.ps1'
[IO.File]::WriteAllText($extracted,($functionTexts -join "`n`n"))
. $extracted
$expectedRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if((Get-RepoRoot) -cne $expectedRoot){throw 'Actual extracted functions lost lexical fixture root'}
$path=Get-BridgeStatusPath $Session
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))|Out-Null
$raw=(@{helperPid=$PID;state='ShuttingDown';terminalRunId='retained-original'}|ConvertTo-Json -Compress)
[IO.File]::WriteAllText($path,$raw)
[IO.File]::WriteAllText((Join-Path $Session 'config.json'),'{}')
$script:LaunchCount=0;$script:LaunchArguments=$null
function dotnet { $script:LaunchCount++;$script:LaunchArguments=@($args);$global:LASTEXITCODE=0 }
$failure=$null
try { Start-Bridge -ResolvedSessionPath $Session -VisibleBridge ($Scenario -ne 'hidden') } catch {$failure=$_}
if([IO.File]::ReadAllText($path) -cne $raw){throw 'Retained status was mutated'}
if($Scenario -ceq 'stale-pid') {
 if([Console]::IsInputRedirected){throw 'Fixture lacks a real terminal'}
 if($failure){throw $failure}
 if($script:LaunchCount -ne 1){throw 'CAUSAL: retained PID prevented Linux launch admission'}
 if($script:LaunchArguments[1] -cne '--host' -or $script:LaunchArguments[3] -cne $Session){throw 'Actual host arguments changed'}
} else {
 if(-not $failure -or $failure.Exception.Message -notlike '*caller-supplied foreground terminal*'){throw 'Foreground gate did not refuse'}
 if($script:LaunchCount -ne 0){throw 'Refused foreground invoked external host'}
}
