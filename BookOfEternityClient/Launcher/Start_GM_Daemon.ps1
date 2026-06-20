param(
    [string]$GameSessionPath = "",
    [string]$CliWindowTitle = "",
    [switch]$AutoPaste,
    [ValidateSet("RightClick","ShiftInsert","CtrlV")]
    [string]$PasteMode = "RightClick",
    [int]$TurnTimeout = 0,
    [int]$PollingInterval = 500,
    [string]$LogFile = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
$daemonPath = Join-Path $projectRoot "game_master_daemon.ps1"
$launchScriptGenerator = Join-Path $PSScriptRoot "Generate_CLI_Launch_Script.ps1"

if ([string]::IsNullOrWhiteSpace($GameSessionPath)) {
    $GameSessionPath = Join-Path $projectRoot "game_session"
}

$controlDir = Join-Path $GameSessionPath "game_state\control"
if (!(Test-Path $controlDir)) {
    New-Item -ItemType Directory -Path $controlDir -Force | Out-Null
}
$generatedLaunchScriptPath = Join-Path $controlDir "CLI_Launch_Script.generated.md"

$invokeArgs = @{
    GameSessionPath = $GameSessionPath
    TurnTimeout = $TurnTimeout
    PollingInterval = $PollingInterval
    LaunchScriptPath = $generatedLaunchScriptPath
}

if (Test-Path $launchScriptGenerator) {
    & $launchScriptGenerator -OutputPath $generatedLaunchScriptPath -GameSessionPath $GameSessionPath | Out-Null
}

if ($CliWindowTitle) {
    $invokeArgs.CliWindowTitle = $CliWindowTitle
}
if ($AutoPaste) {
    $invokeArgs.AutoPaste = $true
    $invokeArgs.PasteMode = $PasteMode
}
if ($LogFile) {
    $invokeArgs.LogFile = $LogFile
}

Set-Location $projectRoot
& $daemonPath @invokeArgs
