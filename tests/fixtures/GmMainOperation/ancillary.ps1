param([string]$RepoRoot,[string]$SessionPath,[string]$Scenario)
$ErrorActionPreference='Stop'
$source=Join-Path $RepoRoot ('BookOfEternityClient/Launcher/'+$(switch($Scenario){'start-daemon'{'Start_GM_Daemon.ps1'};'generate-session'{'Generate_CLI_Launch_Script.ps1'};'register-window'{'Register_GM_CLI_Window.ps1'}}))
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile($source,[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Actual source parse failed.'}
# Inert preparation only: truncate before daemon invocation or native window API.
$text=$ast.EndBlock.Extent.Text
if($Scenario -eq 'start-daemon'){$text=$text.Substring(0,$text.IndexOf('$invokeArgs ='))}
if($Scenario -eq 'register-window'){$text=$text.Substring(0,$text.IndexOf('Add-Type'))}
$GameSessionPath=$SessionPath;$OutputPath=Join-Path $SessionPath 'game_state/control/CLI_Launch_Script.generated.md';$UsePlaceholders=$false
$origin=Split-Path $source -Parent
$text=$text.Replace('$PSScriptRoot',("'"+$origin.Replace("'","''")+"'"))
try {& ([scriptblock]::Create($text))}catch{}
