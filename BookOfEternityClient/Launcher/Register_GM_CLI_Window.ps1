param(
    [string]$GameSessionPath = ""
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($GameSessionPath)) {
    $GameSessionPath = Join-Path $projectRoot "game_session"
}

if (!(Test-Path $GameSessionPath)) {
    New-Item -ItemType Directory -Path $GameSessionPath -Force | Out-Null
}
$GameSessionPath = (Resolve-Path $GameSessionPath).Path

$controlDir = Join-Path $GameSessionPath "game_state\control"
if (!(Test-Path $controlDir)) {
    New-Item -ItemType Directory -Path $controlDir -Force | Out-Null
}

$bindingPath = Join-Path $controlDir "gm_cli_window_binding.json"

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class GmWindowBindingApi {
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);
}
"@ -ErrorAction Stop

$foregroundWindow = [GmWindowBindingApi]::GetForegroundWindow()
if ($foregroundWindow -eq [System.IntPtr]::Zero -or -not [GmWindowBindingApi]::IsWindow($foregroundWindow)) {
    throw "Не удалось получить активное окно. Запусти этот скрипт из того окна PowerShell, где будет жить CLI."
}

[uint32]$windowProcessId = 0
[void][GmWindowBindingApi]::GetWindowThreadProcessId($foregroundWindow, [ref]$windowProcessId)
if ($windowProcessId -eq 0) {
    throw "Не удалось определить PID владельца активного окна."
}

$titleLength = [GmWindowBindingApi]::GetWindowTextLength($foregroundWindow)
$titleBuilder = New-Object System.Text.StringBuilder ($titleLength + 1)
[void][GmWindowBindingApi]::GetWindowText($foregroundWindow, $titleBuilder, $titleBuilder.Capacity)
$windowTitle = $titleBuilder.ToString()

$hostProcess = Get-Process -Id $PID -ErrorAction Stop
$windowOwnerProcess = Get-Process -Id ([int]$windowProcessId) -ErrorAction Stop

$binding = [ordered]@{
    processId = [int]$windowProcessId
    mainWindowHandle = [int64]$foregroundWindow
    windowTitleAtRegistration = $windowTitle
    registeredAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    hostProcessId = $PID
    hostProcessName = $hostProcess.ProcessName
    windowOwnerProcessName = $windowOwnerProcess.ProcessName
    note = "Registered from the current foreground window. Re-register if the CLI window is recreated."
}

$binding | ConvertTo-Json -Depth 4 | Set-Content -Path $bindingPath -Encoding UTF8

Write-Host ""
Write-Host "[OK] GM CLI window registered." -ForegroundColor Green
Write-Host "Binding file : $bindingPath" -ForegroundColor Gray
Write-Host "Window PID   : $windowProcessId" -ForegroundColor Gray
Write-Host "Window handle: $([int64]$foregroundWindow)" -ForegroundColor Gray
Write-Host "Window title : $windowTitle" -ForegroundColor Gray
Write-Host ""
Write-Host "Now start your CLI in this same window, or keep using the current one if CLI is already running." -ForegroundColor Yellow
