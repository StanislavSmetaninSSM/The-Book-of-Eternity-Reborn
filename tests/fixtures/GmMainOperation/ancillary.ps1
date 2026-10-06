param([string]$RepoRoot,[string]$SessionPath,[string]$Scenario)
$ErrorActionPreference='Stop'
$source=Join-Path $RepoRoot ('BookOfEternityClient/Launcher/'+$(switch($Scenario){'start-daemon'{'Start_GM_Daemon.ps1'};'generate-session'{'Generate_CLI_Launch_Script.ps1'};'register-window'{'Register_GM_CLI_Window.ps1'}}))
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Actual source parse failed.'}
$text=$ast.Extent.Text
# Preserve the real param block and pass the isolated paths explicitly.
# Only the external native/daemon action is replaced with a controlled refusal.
if($Scenario -eq 'start-daemon') {
 $call=$ast.Find({param($n)$n -is [Management.Automation.Language.CommandAst] -and $n.CommandElements[0].Extent.Text -eq '$daemonPath'},$true)
 if(-not $call){throw 'Actual daemon invocation not found.'}
 $text=$text.Replace($call.Extent.Text,"throw 'Inert fixture forbids daemon launch.'")
}
if($Scenario -eq 'register-window') {
 $call=$ast.Find({param($n)$n -is [Management.Automation.Language.CommandAst] -and $n.GetCommandName() -eq 'Add-Type'},$true)
 if(-not $call){throw 'Actual native API declaration not found.'}
 $text=$text.Replace($call.Extent.Text,"throw 'Inert fixture forbids native window calls.'")
}
$origin=Split-Path $source -Parent
$text=$text.Replace('$PSScriptRoot',("'"+$origin.Replace("'","''")+"'"))
$args=@{GameSessionPath=$SessionPath}
if($Scenario -eq 'generate-session'){$args.OutputPath=Join-Path $SessionPath 'game_state/control/CLI_Launch_Script.generated.md'}
try {& ([scriptblock]::Create($text)) @args}catch{}
