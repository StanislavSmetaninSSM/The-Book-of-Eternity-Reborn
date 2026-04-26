<#
.SYNOPSIS
    Game Master Daemon — мост между C# клиентом и CLI-агентом геймастера.

.DESCRIPTION
    Следит за turn_request.json и отправляет команду обработки хода
    в ВИДИМОЕ окно CLI-агента. Игрок может видеть работу агента
    и вмешаться при необходимости.

    Архитектура (3 окна):
    - Окно 1: C# клиент (игра) — игрок вводит действия
    - Окно 2: CLI-агент (ГМ) — обрабатывает ходы, виден игроку
    - Окно 3: Этот демон — мост между ними

    CLI-агент запускается ОТДЕЛЬНО игроком с промптом из Launcher\CLI_Launch_Script.md.
    Демон НЕ запускает CLI, а только отправляет ему сообщения.

.PARAMETER GameSessionPath
    Путь к директории game_session.

.PARAMETER CliWindowTitle
    Fallback-заголовок окна CLI-агента для отправки сообщений.
    Используется только если отсутствует gm_cli_window_binding.json или binding невалиден.

.PARAMETER AutoPaste
    Автоматически вставлять команду в окно CLI.
    Если $false — только копирует в буфер обмена и уведомляет.

.PARAMETER PasteMode
    Способ вставки при AutoPaste:
    - RightClick (по умолчанию)
    - ShiftInsert
    - CtrlV

.EXAMPLE
    # Основной режим: использовать зарегистрированное окно CLI
    .\game_master_daemon.ps1 -AutoPaste

.EXAMPLE
    # Fallback по заголовку окна, если binding ещё не зарегистрирован:
    .\game_master_daemon.ps1 -CliWindowTitle "GM Gemini" -AutoPaste

#>

param(
    [string]$GameSessionPath = ".\game_session",
    [string]$CliWindowTitle = "",
    [switch]$AutoPaste,
    [ValidateSet("RightClick","ShiftInsert","CtrlV")]
    [string]$PasteMode = "RightClick",
    [int]$TurnTimeout = 0,
    [int]$PollingInterval = 500,
    [string]$LogFile = ""
)

# ═══════════════════════════════════════════════
# Initialization
# ═══════════════════════════════════════════════

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

$script:TurnCount = 0
$script:ErrorCount = 0
$script:StartTime = Get-Date
$script:IsProcessing = $false
$script:BootstrapSent = $false
$script:AfterlifeExamplesDirective = " If game_state/meta/soul_state.json.currentRealm is Chaos Sea or Shining Abode, or progressionControl contains any afterlife mustEvaluate*/afterlifeCatchup debt, you MUST also read OtherGuides/Afterlife_Contract_Matrix.md and Examples/E_CLI_Afterlife_Turns.txt before writing or repairing files; use the matrix to select exact canonical surfaces/receipts, use example 14 for Shining core action fragments, examples 16-18 for combined scheduler + pending contract + player-action turns, and example 19 for ordinary scheduler-only Chaos Sea living-world turns."

# Resolve paths
if (!(Test-Path $GameSessionPath)) { New-Item -ItemType Directory -Path $GameSessionPath -Force | Out-Null }
$GameSessionPath = (Resolve-Path $GameSessionPath).Path

$InputDir  = Join-Path $GameSessionPath "input"
$ReadyDir  = Join-Path $GameSessionPath "ready"
$OutputDir = Join-Path $GameSessionPath "output"
$ControlDir = Join-Path $GameSessionPath "game_state\control"
$TurnRequestFile = Join-Path $InputDir "turn_request.json"
$RepairRequestFile = Join-Path $ControlDir "validation_repair_request.json"
$TerminalProtocolFailureRequestFile = Join-Path $ControlDir "terminal_protocol_failure_request.json"
$CliBindingFile = Join-Path $ControlDir "gm_cli_window_binding.json"
$BridgeStatusFile = Join-Path $ControlDir "gm_bridge_status.json"
$BridgeControlScript = Join-Path $PSScriptRoot "Launcher\bookofeternity.ps1"

foreach ($dir in @($InputDir, $ReadyDir, $OutputDir, $ControlDir)) {
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

$script:LastRepairRequestWrite = [datetime]::MinValue
$script:LastTerminalProtocolFailureWrite = [datetime]::MinValue
$script:BridgeAutoStartAttempted = $false

function Get-GameConfig {
    $configPath = Join-Path $GameSessionPath "config.json"
    $defaults = [ordered]@{
        GmBridgeEnabled = $true
        GmBridgeBackend = "ConPTYBridge"
        GmCliLaunchCommand = "gemini"
        GmBridgeAutoStart = $false
        GmBridgePipeNameOverride = ""
    }

    if (!(Test-Path $configPath)) {
        return [pscustomobject]$defaults
    }

    try {
        $loaded = Get-Content -Path $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($key in @($defaults.Keys)) {
            if ($null -eq $loaded.$key) {
                $loaded | Add-Member -NotePropertyName $key -NotePropertyValue $defaults[$key]
            }
        }

        return $loaded
    }
    catch {
        return [pscustomobject]$defaults
    }
}

function Get-GmBridgeStatus {
    if (!(Test-Path $BridgeStatusFile)) {
        return $null
    }

    try {
        $status = Get-Content -Path $BridgeStatusFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -eq $status.helperPid) {
            return $status
        }

        try {
            $null = Get-Process -Id ([int]$status.helperPid) -ErrorAction Stop
            return $status
        }
        catch {
            Write-Log "  -> Removing stale GM bridge status file (dead helper pid)." -Level "WARN" -Color Yellow
            Remove-Item $BridgeStatusFile -Force -ErrorAction SilentlyContinue
            return $null
        }
    }
    catch {
        return $null
    }
}

function Ensure-GmBridgeStarted {
    $config = Get-GameConfig
    if (-not $config.GmBridgeEnabled -or $config.GmBridgeBackend -ne "ConPTYBridge") {
        return
    }

    if (-not $config.GmBridgeAutoStart -or $script:BridgeAutoStartAttempted) {
        return
    }

    $script:BridgeAutoStartAttempted = $true

    if (!(Test-Path $BridgeControlScript)) {
        Write-Log "  -> GM bridge control script not found. Auto-start skipped." -Level "WARN" -Color Yellow
        return
    }

    try {
        & $BridgeControlScript start-bridge -SessionPath $GameSessionPath | Out-Null
        Write-Log "  -> Requested GM bridge auto-start" -Color DarkGray
    }
    catch {
        Write-Log "  -> GM bridge auto-start failed: $_" -Level "WARN" -Color Yellow
    }
}

function Send-ToGmBridge {
    param(
        [string]$Message,
        [switch]$AllowNotReady
    )

    $config = Get-GameConfig
    if (-not $config.GmBridgeEnabled -or $config.GmBridgeBackend -ne "ConPTYBridge") {
        return $null
    }

    Ensure-GmBridgeStarted

    $status = Get-GmBridgeStatus
    if ($null -eq $status) {
        Write-Log "  -> GM bridge status file not found. Falling back." -Level "WARN" -Color Yellow
        return "bridge-unavailable"
    }

    if (-not $status.ready -and -not $AllowNotReady) {
        Write-Log "  -> GM bridge is running but not marked ready. Falling back." -Level "WARN" -Color Yellow
        return "bridge-not-ready"
    }

    if (!(Test-Path $BridgeControlScript)) {
        Write-Log "  -> GM bridge control script missing. Falling back." -Level "WARN" -Color Yellow
        return "bridge-control-missing"
    }

    try {
        if ($AllowNotReady) {
            & $BridgeControlScript addText $Message -SessionPath $GameSessionPath | Out-Null
            Start-Sleep -Milliseconds 100
            & $BridgeControlScript sendEnter -SessionPath $GameSessionPath | Out-Null
            Write-Log "  -> Sent bootstrap/reminder to GM bridge via addText+sendEnter" -Color Green
        }
        else {
            & $BridgeControlScript dispatchPrompt $Message -SessionPath $GameSessionPath | Out-Null
            Write-Log "  -> Sent to GM bridge via named pipe" -Color Green
        }
        return "sent"
    }
    catch {
        Write-Log "  -> GM bridge dispatch failed: $_" -Level "WARN" -Color Yellow
        return "bridge-failed"
    }
}

function Write-Log {
    param([string]$Message, [string]$Level = "INFO", [ConsoleColor]$Color = [ConsoleColor]::White)
    $timestamp = Get-Date -Format "HH:mm:ss"
    $logLine = "[$timestamp][$Level] $Message"
    Write-Host $logLine -ForegroundColor $Color
    if ($LogFile) {
        try { Add-Content -Path $LogFile -Value $logLine -Encoding UTF8 -ErrorAction SilentlyContinue } catch { }
    }
}

# Banner
Write-Host ""
Write-Host "  +===============================================+" -ForegroundColor Cyan
Write-Host "  |  Book of Eternity: Game Master Daemon         |" -ForegroundColor Cyan
Write-Host "  +===============================================+" -ForegroundColor Cyan
Write-Host ""
Write-Log "Game Session : $GameSessionPath" -Color Gray
if ((Get-GameConfig).GmBridgeEnabled -and (Get-GameConfig).GmBridgeBackend -eq "ConPTYBridge") {
    Write-Log "GM Backend   : ConPTYBridge" -Color Gray
    if (Test-Path $BridgeStatusFile) {
        Write-Log "Bridge Status: '$BridgeStatusFile'" -Color Gray
    } else {
        Write-Log "Bridge Status: bridge not started yet (fallbacks remain available)" -Color Yellow
    }
}
elseif (Test-Path $CliBindingFile) {
    Write-Log "CLI Binding  : '$CliBindingFile'" -Color Gray
} elseif ($CliWindowTitle) {
    Write-Log "CLI Window   : '$CliWindowTitle' (title fallback)" -Color Gray
} else {
    Write-Log "Mode         : Clipboard only (no window targeting)" -Color Yellow
}
Write-Log "Auto-Paste   : $AutoPaste" -Color Gray
if ($AutoPaste) {
    Write-Log "Paste Mode   : $PasteMode" -Color Gray
}
if ($script:LaunchScriptPath) {
    Write-Log "Bootstrap    : '$($script:LaunchScriptPath)'" -Color Gray
}
if ($TurnTimeout -le 0) {
    Write-Log "Timeout      : disabled (wait indefinitely)" -Color Gray
}
else {
    Write-Log "Timeout      : ${TurnTimeout}s" -Color Gray
}
Write-Host ""

# ═══════════════════════════════════════════════
# CLI Window Communication
# ═══════════════════════════════════════════════

# Win32 API for window activation
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Win32Window {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    public const int SW_RESTORE = 9;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP = 0x0010;
}
"@ -ErrorAction SilentlyContinue

function Invoke-RightClickPaste {
    param([System.IntPtr]$WindowHandle)

    $originalPoint = [Win32Window+POINT]::new()
    $null = [Win32Window]::GetCursorPos([ref]$originalPoint)

    $rect = [Win32Window+RECT]::new()
    if (-not [Win32Window]::GetWindowRect($WindowHandle, [ref]$rect)) {
        return $false
    }

    $targetX = [Math]::Max($rect.Left + 32, $rect.Left + [Math]::Floor(($rect.Right - $rect.Left) / 3))
    $targetY = [Math]::Max($rect.Top + 32, $rect.Top + [Math]::Floor(($rect.Bottom - $rect.Top) / 3))

    $null = [Win32Window]::SetCursorPos([int]$targetX, [int]$targetY)
    Start-Sleep -Milliseconds 120
    [Win32Window]::mouse_event([Win32Window]::MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 40
    [Win32Window]::mouse_event([Win32Window]::MOUSEEVENTF_RIGHTUP, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 180
    $null = [Win32Window]::SetCursorPos($originalPoint.X, $originalPoint.Y)
    return $true
}

function Get-BoundCliTarget {
    if (!(Test-Path $CliBindingFile)) {
        return $null
    }

    try {
        $binding = Get-Content -Path $CliBindingFile -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -eq $binding.processId -or $null -eq $binding.mainWindowHandle) {
            Write-Log "  -> Binding file is missing processId/mainWindowHandle. Falling back." -Level "WARN" -Color Yellow
            return $null
        }

        $windowHandle = [System.IntPtr]::new([int64]$binding.mainWindowHandle)
        if (-not [Win32Window]::IsWindow($windowHandle)) {
            Write-Log "  -> Binding file points to a non-existing window handle. Falling back." -Level "WARN" -Color Yellow
            return $null
        }

        $process = Get-Process -Id ([int]$binding.processId) -ErrorAction SilentlyContinue
        if (-not $process) {
            Write-Log "  -> Binding file points to a dead process. Falling back." -Level "WARN" -Color Yellow
            return $null
        }

        return [pscustomobject]@{
            Mode = "binding"
            Process = $process
            WindowHandle = $windowHandle
            Description = "binding pid=$($process.Id), handle=$([int64]$windowHandle)"
        }
    }
    catch {
        Write-Log "  -> Failed to read binding file. Falling back." -Level "WARN" -Color Yellow
        return $null
    }
}

function Get-TitleMatchedCliTarget {
    if (-not $CliWindowTitle) {
        return $null
    }

    $targetProcess = Get-Process | Where-Object { $_.MainWindowTitle -match $CliWindowTitle } | Select-Object -First 1
    if (-not $targetProcess -or $targetProcess.MainWindowHandle -eq 0) {
        return $null
    }

    return [pscustomobject]@{
        Mode = "title-fallback"
        Process = $targetProcess
        WindowHandle = $targetProcess.MainWindowHandle
        Description = "title fallback '$CliWindowTitle' -> pid=$($targetProcess.Id)"
    }
}

function Resolve-CliTarget {
    $bindingTarget = Get-BoundCliTarget
    if ($bindingTarget) {
        return $bindingTarget
    }

    return Get-TitleMatchedCliTarget
}

function Send-ToCliWindow {
    param(
        [string]$Message
    )

    $config = Get-GameConfig
    if ($config.GmBridgeEnabled -and $config.GmBridgeBackend -eq "ConPTYBridge") {
        return (Send-ToGmBridge -Message $Message)
    }

    # Clipboard is the universal fallback for every bridge/window failure path.
    Set-Clipboard -Value $Message
    Write-Log "  -> Clipboard: command copied" -Color DarkGray

    if (-not $AutoPaste) {
        [Console]::Beep(800, 200)
        [Console]::Beep(1000, 200)
        Write-Log "  -> Command copied to clipboard. Paste it manually into the CLI window using the method your terminal supports." -Color Yellow
        return "clipboard"
    }

    $target = Resolve-CliTarget
    if (-not $target) {
        Write-Log "  -> No bound CLI window found. Command left in clipboard." -Level "WARN" -Color Yellow
        [Console]::Beep(400, 300)
        return "unbound"
    }

    try {
        [Win32Window]::ShowWindow($target.WindowHandle, [Win32Window]::SW_RESTORE) | Out-Null
        Start-Sleep -Milliseconds 200
        [Win32Window]::SetForegroundWindow($target.WindowHandle) | Out-Null
        Start-Sleep -Milliseconds 350
        Write-Log "  -> Activated target via $($target.Description)" -Color DarkGray

        if ($PasteMode -eq "CtrlV") {
            [System.Windows.Forms.SendKeys]::SendWait("^v")
            Write-Log "  -> Paste sent via Ctrl+V" -Color DarkGray
        }
        elseif ($PasteMode -eq "ShiftInsert") {
            [System.Windows.Forms.SendKeys]::SendWait("+{INSERT}")
            Write-Log "  -> Paste sent via Shift+Insert" -Color DarkGray
        }
        else {
            if (-not (Invoke-RightClickPaste -WindowHandle $target.WindowHandle)) {
                throw "RightClick paste failed: could not target window coordinates."
            }
            Write-Log "  -> Paste sent via RightClick" -Color DarkGray
        }
        Start-Sleep -Milliseconds 250
        [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
        Write-Log "  -> Enter sent" -Color DarkGray

        Write-Log "  -> Sent to CLI window" -Color Green
        return "sent"
    }
    catch {
        Write-Log "  -> SendKeys failed: $_. Command in clipboard." -Level "WARN" -Color Yellow
        return "failed"
    }
}

function Ensure-CliBootstrapSent {
    if ($script:BootstrapSent) { return $true }

    if (-not $script:LaunchScriptPath) {
        Write-Log "  -> CLI_Launch_Script.md not found; bootstrap message skipped." -Level "WARN" -Color Yellow
        $script:BootstrapSent = $true
        return $true
    }

    $launchScript = Get-Content -Path $script:LaunchScriptPath -Raw -Encoding UTF8
    $message = @"
BOOTSTRAP GM SESSION

This is bootstrap only, not an active turn.
Do NOT write ready/turn_complete.json or ready/turn_error.json yet.
Wait for a real correlated message that explicitly references input\turn_request.json with the current sessionId/requestId/turnNumber.

Read and follow the full launch script below before processing turns:

===== BEGIN CLI_LAUNCH_SCRIPT =====
$launchScript
===== END CLI_LAUNCH_SCRIPT =====
"@
    $dispatch = Send-ToCliWindow -Message $message
    if ($dispatch -eq "sent" -or $dispatch -eq "clipboard") {
        $script:BootstrapSent = $true
        Write-Log "  -> Bootstrap launch script dispatched" -Color Green
        return $true
    }

    return $false
}

function Dispatch-WithRetry {
    param(
        [string]$Message,
        [string]$PendingPath = ""
    )

    while ($true) {
        if ($PendingPath -and !(Test-Path $PendingPath)) {
            return "cancelled"
        }

        $dispatch = Send-ToCliWindow -Message $Message
        if ($dispatch -eq "sent" -or $dispatch -eq "clipboard") {
            return $dispatch
        }

        if ($dispatch -like "bridge-*") {
            Write-Log "  -> Waiting for GM bridge to become available/ready..." -Level "WARN" -Color Yellow
            Start-Sleep -Seconds 1
            continue
        }

        return $dispatch
    }
}

# ═══════════════════════════════════════════════
# Turn Processing
# ═══════════════════════════════════════════════

function Process-Turn {
    param([string]$RequestPath)

    if ($script:IsProcessing) { return }
    $script:IsProcessing = $true
    $script:TurnCount++
    $turnStart = Get-Date

    Start-Sleep -Milliseconds 300
    if (!(Test-Path $RequestPath)) {
        $script:IsProcessing = $false
        return
    }

    try {
        $turnRequest = Get-Content -Path $RequestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $turnNumber = $turnRequest.turnNumber
        $playerAction = if ($turnRequest.playerAction.Length -gt 80) {
            $turnRequest.playerAction.Substring(0, 77) + "..."
        } else { $turnRequest.playerAction }

        Write-Host ""
        Write-Log "Turn #${turnNumber}: $playerAction" -Level "TURN" -Color Green

        # Send processing command to CLI window
        $requestId = if ($turnRequest.requestId) { $turnRequest.requestId } else { "<missing-requestId>" }
        $message = "Process turn #$turnNumber (requestId=$requestId). Read $GameSessionPath\input\turn_request.json and follow CLI_Agent_Daemon_Specification.md phases 0-4. You MUST read TaskGuides/CLI_Step_Main.txt and Examples/E_CLI_Step_Main.txt before writing files.$($script:AfterlifeExamplesDirective) If this turn uses any GM-side [INK_FEATHER_ACTION: TAG], you MUST also read Examples/E_CLI_Ink_Feather_Actions.txt and write output/ink_feather_action_result.json with exact metadata, actionTag, resolved=true, costInFeathers, resolutionType, summary, and stateEvidence. The client validates correlated metadata, valid JSON, realm restrictions, progressionControl/progression report, gm_thoughts_markdown scope/reasoning, and structured actor coverage. Relevant actors in NPC scope MUST cover any structured actor updates such as UpdateNPCs, NPCGoalUpdates, or UpdateGuardians. Use preGeneratedDices1d20 from the FIRST die for normal checks; gachaBaseResult is separate and does not consume visible dice. If playerAction contains [CHAOS_SEA_DIRECT_GACHA], treat it as a neutral direct pull from the Chaos Sea, not a Guardian-mediated pull. Guardian-mediated gacha is limited per Guardian per return from mortal life: Hostile=0, Wary/Neutral=1, Friendly=2, Devoted/Legendary=3. Charges reset only when the Soul returns to the Chaos Sea after a new mortal life. If a Guardian has no remaining charges this return, do NOT emit UpdateGuardians.processGacha for that Guardian. Direct /gacha remains neutral and does NOT consume Guardian charges. progressionControl in the request is authoritative. If progression is processed, write game_state/control/progression_report.json with exact sessionId/requestId/turnNumber copied from the CURRENT turn_request.json plus exact bounded processed cycle counts and new last-* markers. If progressionControl.afterlifeCatchupRequired=true, process only afterlifeCatchupSummaryEventsRequired summary outcomes and do NOT simulate raw elapsed cycles one by one. TERMINAL CHECKLIST: write EXACTLY ONE terminal signal for this request; use either ready/turn_complete.json OR ready/turn_error.json, never both; copy exact sessionId/requestId/turnNumber from the CURRENT turn_request.json; write the terminal signal as the LAST step. If you write both terminal files or wrong metadata, the client will reject the terminal phase as protocol failure and write game_state/control/terminal_protocol_failure_request.json. validation_repair_request.json is only for accepted terminal completion with invalid resulting state."

        $bootstrapSent = Ensure-CliBootstrapSent
        $completionPath = Join-Path $ReadyDir "turn_complete.json"
        $errorPath = Join-Path $ReadyDir "turn_error.json"
        $terminalSignal = Get-CorrelatedTerminalSignal -TurnRequest $turnRequest -CompletionPath $completionPath -ErrorPath $errorPath

        if ($null -eq $terminalSignal) {
            if (-not $bootstrapSent) {
                while (-not (Ensure-CliBootstrapSent)) {
                    if (!(Test-Path $RequestPath)) {
                        Write-Log "  Turn cancelled while waiting for bridge/bootstrap dispatch" -Level "WARN" -Color Yellow
                        return
                    }
                    Start-Sleep -Seconds 1
                }
            }

            $dispatch = Dispatch-WithRetry -Message $message -PendingPath $RequestPath
            if ($dispatch -eq "cancelled") {
                Write-Log "  Turn cancelled while waiting for bridge turn dispatch" -Level "WARN" -Color Yellow
                return
            }
        }
        elseif ($terminalSignal.Kind -eq "conflict") {
            Write-Log "  Detected conflicting correlated terminal signals for the same turn. Waiting stops as protocol failure; client should emit terminal_protocol_failure_request.json." -Level "ERROR" -Color Red
        }
        else {
            Write-Log "  Found correlated terminal signal already present; completing without re-dispatch" -Level "WARN" -Color Yellow
        }

        # Wait for terminal signal
        $elapsed = 0

        while ($null -eq $terminalSignal -and ($TurnTimeout -le 0 -or $elapsed -lt $TurnTimeout)) {
            Start-Sleep -Seconds 1
            $elapsed++

            if (!(Test-Path $RequestPath)) {
                Write-Log "  Turn cancelled by client" -Level "WARN" -Color Yellow
                break
            }

            $terminalSignal = Get-CorrelatedTerminalSignal -TurnRequest $turnRequest -CompletionPath $completionPath -ErrorPath $errorPath

            if ($elapsed % 60 -eq 0) {
                Write-Log "  Waiting... (${elapsed}s)" -Color DarkGray
            }
        }

        if ($TurnTimeout -gt 0 -and $elapsed -ge $TurnTimeout -and $null -eq $terminalSignal) {
            $script:ErrorCount++
            Write-Log "  Timeout after ${elapsed}s" -Level "ERROR" -Color Red
            $timeoutSignal = @{
                sessionId = $turnRequest.sessionId
                requestId = $turnRequest.requestId
                turnNumber = $turnNumber
                status = "error"
                timestamp = (Get-Date).ToUniversalTime().ToString("o")
                error = "Timeout after ${elapsed}s"
            }
            Set-Content -Path $errorPath -Value ($timeoutSignal | ConvertTo-Json -Depth 3) -Encoding UTF8
            $terminalSignal = [pscustomobject]@{
                Path = $errorPath
                Kind = "error"
                Signal = [pscustomobject]$timeoutSignal
            }
        }

        # Check terminal outcome
        if ($null -ne $terminalSignal -and $terminalSignal.Kind -eq "conflict") {
            $duration = ((Get-Date) - $turnStart).TotalSeconds
            Write-Log "  Terminal protocol conflict ($([math]::Round($duration, 1))s): both turn_complete.json and turn_error.json match the same request" -Level "TURN" -Color Red
        }
        elseif ($null -ne $terminalSignal -and $terminalSignal.Kind -eq "success") {
            $duration = ((Get-Date) - $turnStart).TotalSeconds
            Write-Log "  Done ($([math]::Round($duration, 1))s)" -Level "TURN" -Color Green
        }
        elseif ($null -ne $terminalSignal -and $terminalSignal.Kind -eq "error") {
            $duration = ((Get-Date) - $turnStart).TotalSeconds
            try {
                $errorSignal = $terminalSignal.Signal
                $errorMessage = if ($errorSignal.error) { $errorSignal.error } else { "Unknown GM error" }
                Write-Log "  Terminal error ($([math]::Round($duration, 1))s): $errorMessage" -Level "TURN" -Color Yellow
            }
            catch {
                Write-Log "  Terminal error ($([math]::Round($duration, 1))s): unreadable turn_error.json" -Level "TURN" -Color Yellow
            }
        }

        # Cleanup input
        if (Test-Path $RequestPath) { Remove-Item $RequestPath -Force -ErrorAction SilentlyContinue }
    }
    catch {
        $script:ErrorCount++
        Write-Log "  Error: $_" -Level "ERROR" -Color Red
    }
    finally {
        $script:IsProcessing = $false
    }
}

function Process-RepairRequest {
    param([string]$RepairPath)

    if (!(Test-Path $RepairPath)) { return }

    try {
        $fileInfo = Get-Item $RepairPath
        if ($fileInfo.LastWriteTimeUtc -le $script:LastRepairRequestWrite) { return }
        $script:LastRepairRequestWrite = $fileInfo.LastWriteTimeUtc

        $repair = Get-Content -Path $RepairPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $turnNumber = if ($repair.turnNumber) { [int]$repair.turnNumber } else { -1 }
        $requestId = if ($repair.requestId) { $repair.requestId } else { "<missing-requestId>" }
        $attempt = if ($repair.revalidationAttempt) { [int]$repair.revalidationAttempt } else { 1 }
        $hasDiagnosticOnlyMetadata = Test-ProtocolRequestUsesDiagnosticOnlyMetadata -RequestObject $repair

        $summary = @()
        if ($repair.summaryGroups) {
            foreach ($group in @($repair.summaryGroups | Select-Object -First 6)) {
                if ($group) { $summary += "- $group" }
            }
        }

        $topErrors = @()
        if ($repair.errors) {
            foreach ($err in @($repair.errors | Select-Object -First 5)) {
                $code = if ($err.code) { $err.code } else { "validation_error" }
                $category = if ($err.category) { $err.category } else { "StateConsistency" }
                $section = if ($err.section) { $err.section } else { "General" }
                $msg = if ($err.message) { $err.message } else { "Unknown validation error" }
                $expected = if ($err.expected) { " Expected: $($err.expected)" } else { "" }
                $actual = if ($err.actual) { " Actual: $($err.actual)" } else { "" }
                $hint = if ($err.repairHint) { " Hint: $($err.repairHint)" } else { "" }
                $topErrors += "- [$category/$section/$code] $msg$expected$actual$hint"
            }
        }

        $message = "Repair rejected turn #$turnNumber (requestId=$requestId, attempt=$attempt). You MUST reread $GameSessionPath\game_state\control\validation_repair_request.json plus TaskGuides/CLI_Step_Main.txt and Examples/E_CLI_Step_Main.txt.$($script:AfterlifeExamplesDirective) Fix the already written files IN PLACE. Do NOT create a new turn."
        if ($hasDiagnosticOnlyMetadata) {
            $message += " The current repair request marks sessionId/requestId/turnNumber as diagnostic-only sentinel values because validated pending snapshot context is unavailable or invalid. Do NOT copy those sentinel metadata into $GameSessionPath\game_state\control\validation_repair_ready.json. First restore pending snapshot context/authority and then use the freshest client-authored repair request with valid metadata before writing validation_repair_ready.json."
        }
        else {
            $message += " When done, create $GameSessionPath\game_state\control\validation_repair_ready.json with matching sessionId/requestId/turnNumber copied from the CURRENT repair request."
        }
        $message += " If your ready file is malformed or mismatched, the client will reject it and rewrite the repair request again."
        if ($summary.Count -gt 0) {
            $message += "`nMain groups:`n" + ($summary -join "`n")
        }
        if ($topErrors.Count -gt 0) {
            $message += "`nTop issues:`n" + ($topErrors -join "`n")
        }

        Write-Host ""
        Write-Log "Repair request for turn #$turnNumber (attempt $attempt)" -Level "REPAIR" -Color Yellow

        Ensure-CliBootstrapSent
        Send-ToCliWindow -Message $message | Out-Null
    }
    catch {
        $script:ErrorCount++
        Write-Log "Repair watcher error: $_" -Level "ERROR" -Color Red
    }
}

function Get-LaunchScriptPath {
    $candidates = @(
        (Join-Path $PSScriptRoot "Launcher\CLI_Launch_Script.md"),
        (Join-Path (Split-Path $PSScriptRoot -Parent) "Launcher\CLI_Launch_Script.md"),
        (Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) "Launcher\CLI_Launch_Script.md")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

$script:LaunchScriptPath = Get-LaunchScriptPath

function Test-ProtocolRequestUsesDiagnosticOnlyMetadata {
    param([object]$RequestObject)

    if ($null -ne $RequestObject.metadataDiagnosticOnly) {
        return [bool]$RequestObject.metadataDiagnosticOnly
    }

    # Legacy fallback for requests written before metadataDiagnosticOnly was added.
    return $RequestObject.gmInstructions -and $RequestObject.gmInstructions.Contains("служат только для диагностики")
}

function Process-TerminalProtocolFailureRequest {
    param([string]$FailurePath)

    if (!(Test-Path $FailurePath)) { return }

    try {
        $fileInfo = Get-Item $FailurePath
        if ($fileInfo.LastWriteTimeUtc -le $script:LastTerminalProtocolFailureWrite) { return }
        $script:LastTerminalProtocolFailureWrite = $fileInfo.LastWriteTimeUtc

        $failure = Get-Content -Path $FailurePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $turnNumber = if ($failure.turnNumber) { [int]$failure.turnNumber } else { -1 }
        $requestId = if ($failure.requestId) { $failure.requestId } else { "<missing-requestId>" }
        $hasDiagnosticOnlyMetadata = Test-ProtocolRequestUsesDiagnosticOnlyMetadata -RequestObject $failure

        $summary = @()
        if ($failure.summaryGroups) {
            foreach ($group in @($failure.summaryGroups | Select-Object -First 6)) {
                if ($group) { $summary += "- $group" }
            }
        }

        $topErrors = @()
        if ($failure.errors) {
            foreach ($err in @($failure.errors | Select-Object -First 5)) {
                $code = if ($err.code) { $err.code } else { "terminal_protocol_failure" }
                $category = if ($err.category) { $err.category } else { "ProtocolViolation" }
                $section = if ($err.section) { $err.section } else { "General" }
                $msg = if ($err.message) { $err.message } else { "Unknown terminal protocol failure" }
                $expected = if ($err.expected) { " Expected: $($err.expected)" } else { "" }
                $actual = if ($err.actual) { " Actual: $($err.actual)" } else { "" }
                $hint = if ($err.repairHint) { " Hint: $($err.repairHint)" } else { "" }
                $topErrors += "- [$category/$section/$code] $msg$expected$actual$hint"
            }
        }

        $message = "Terminal protocol failure for turn #$turnNumber (requestId=$requestId). You MUST reread $GameSessionPath\game_state\control\terminal_protocol_failure_request.json plus TaskGuides/CLI_Step_Main.txt and Examples/E_CLI_Step_Main.txt.$($script:AfterlifeExamplesDirective) This is NOT validation_repair_request.json and NOT a repair loop. The client already closed the current wait cycle. Do NOT create validation_repair_ready.json. Do NOT create a new turn on your own. Fix your terminal-signal discipline for the NEXT correct client request: exactly one terminal signal, terminal signal written last, never both turn_complete and turn_error for one request."
        if ($hasDiagnosticOnlyMetadata) {
            $message += " The sessionId/requestId/turnNumber in this terminal protocol failure request are diagnostic-only sentinel values because validated pending snapshot context is unavailable or invalid. Do NOT treat them as authoritative correlation metadata for the next step; restore pending snapshot context/authority first and then wait for the freshest correct client request."
        }
        else {
            $message += " Keep exact sessionId/requestId/turnNumber discipline for the NEXT correct client request."
        }
        if ($summary.Count -gt 0) {
            $message += "`nMain groups:`n" + ($summary -join "`n")
        }
        if ($topErrors.Count -gt 0) {
            $message += "`nTop issues:`n" + ($topErrors -join "`n")
        }

        Write-Host ""
        Write-Log "Terminal protocol failure for turn #$turnNumber" -Level "PROTOCOL" -Color Red

        Ensure-CliBootstrapSent
        Send-ToCliWindow -Message $message | Out-Null
    }
    catch {
        $script:ErrorCount++
        Write-Log "Terminal protocol failure watcher error: $_" -Level "ERROR" -Color Red
    }
}

function Read-ReadySignal {
    param(
        [string]$Path,
        [int]$MaxAttempts = 3,
        [int]$DelayMs = 150
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        if (!(Test-Path $Path)) {
            return $null
        }

        try {
            $content = Get-Content -Path $Path -Raw -Encoding UTF8
            if ([string]::IsNullOrWhiteSpace($content)) {
                throw "Ready signal file is empty."
            }

            $signal = $content | ConvertFrom-Json
            if ($null -eq $signal.sessionId -or $null -eq $signal.requestId -or $null -eq $signal.turnNumber) {
                throw "Ready signal metadata is incomplete."
            }

            return $signal
        }
        catch {
            if ($attempt -lt $MaxAttempts) {
                Start-Sleep -Milliseconds $DelayMs
                continue
            }

            return $null
        }
    }

    return $null
}

function Get-CorrelatedTerminalSignal {
    param(
        [psobject]$TurnRequest,
        [string]$CompletionPath,
        [string]$ErrorPath
    )

    $expectedSessionId = [string]$TurnRequest.sessionId
    $expectedRequestId = [string]$TurnRequest.requestId
    $expectedTurnNumber = [int]$TurnRequest.turnNumber

    $candidates = @(
        @{ Path = $CompletionPath; Kind = "success" },
        @{ Path = $ErrorPath; Kind = "error" }
    )

    $matchedSignals = @()

    foreach ($candidate in $candidates) {
        $path = $candidate.Path
        if (!(Test-Path $path)) {
            continue
        }

        $signal = Read-ReadySignal -Path $path
        $fileName = Split-Path $path -Leaf
        if ($null -eq $signal) {
            Write-Log "  Removed unreadable terminal signal artifact: $fileName" -Level "WARN" -Color Yellow
            Remove-Item $path -Force -ErrorAction SilentlyContinue
            continue
        }

        $isMatch =
            ([string]$signal.sessionId -eq $expectedSessionId) -and
            ([string]$signal.requestId -eq $expectedRequestId) -and
            ([int]$signal.turnNumber -eq $expectedTurnNumber)

        if ($isMatch) {
            $matchedSignals += [pscustomobject]@{
                Path = $path
                Kind = $candidate.Kind
                Signal = $signal
            }
            continue
        }

        Write-Log "  Removed stale terminal signal artifact: $fileName (sessionId/requestId/turnNumber mismatch)" -Level "WARN" -Color Yellow
        Remove-Item $path -Force -ErrorAction SilentlyContinue
    }

    if ($matchedSignals.Count -gt 1) {
        return [pscustomobject]@{
            Kind = "conflict"
            Signals = $matchedSignals
        }
    }

    if ($matchedSignals.Count -eq 1) {
        return $matchedSignals[0]
    }

    return $null
}

# ═══════════════════════════════════════════════
# FileSystemWatcher & Main Loop
# ═══════════════════════════════════════════════

$watcher = New-Object System.IO.FileSystemWatcher
$watcher.Path = $InputDir
$watcher.Filter = "turn_request.json"
$watcher.IncludeSubdirectories = $false
$watcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor [System.IO.NotifyFilters]::CreationTime -bor [System.IO.NotifyFilters]::LastWrite
$watcher.EnableRaisingEvents = $true

$repairWatcher = New-Object System.IO.FileSystemWatcher
$repairWatcher.Path = $ControlDir
$repairWatcher.Filter = "validation_repair_request.json"
$repairWatcher.IncludeSubdirectories = $false
$repairWatcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor [System.IO.NotifyFilters]::CreationTime -bor [System.IO.NotifyFilters]::LastWrite
$repairWatcher.EnableRaisingEvents = $true

$terminalProtocolFailureWatcher = New-Object System.IO.FileSystemWatcher
$terminalProtocolFailureWatcher.Path = $ControlDir
$terminalProtocolFailureWatcher.Filter = "terminal_protocol_failure_request.json"
$terminalProtocolFailureWatcher.IncludeSubdirectories = $false
$terminalProtocolFailureWatcher.NotifyFilter = [System.IO.NotifyFilters]::FileName -bor [System.IO.NotifyFilters]::CreationTime -bor [System.IO.NotifyFilters]::LastWrite
$terminalProtocolFailureWatcher.EnableRaisingEvents = $true

$action = {
    $path = $Event.SourceEventArgs.FullPath
    if ($Event.SourceEventArgs.ChangeType -eq "Created" -or $Event.SourceEventArgs.ChangeType -eq "Changed") {
        Process-Turn -RequestPath $path
    }
}

$repairAction = {
    $path = $Event.SourceEventArgs.FullPath
    if ($Event.SourceEventArgs.ChangeType -eq "Created" -or $Event.SourceEventArgs.ChangeType -eq "Changed") {
        Process-RepairRequest -RepairPath $path
    }
}

$terminalProtocolFailureAction = {
    $path = $Event.SourceEventArgs.FullPath
    if ($Event.SourceEventArgs.ChangeType -eq "Created" -or $Event.SourceEventArgs.ChangeType -eq "Changed") {
        Process-TerminalProtocolFailureRequest -FailurePath $path
    }
}

Register-ObjectEvent $watcher "Created" -Action $action | Out-Null
Register-ObjectEvent $watcher "Changed" -Action $action | Out-Null
Register-ObjectEvent $repairWatcher "Created" -Action $repairAction | Out-Null
Register-ObjectEvent $repairWatcher "Changed" -Action $repairAction | Out-Null
Register-ObjectEvent $terminalProtocolFailureWatcher "Created" -Action $terminalProtocolFailureAction | Out-Null
Register-ObjectEvent $terminalProtocolFailureWatcher "Changed" -Action $terminalProtocolFailureAction | Out-Null

Write-Log "Watching: $InputDir" -Color DarkGray

try {
    if (-not $CliWindowTitle) {
        Write-Host ""
        Write-Host "  +-------------------------------------------------+" -ForegroundColor Yellow
        Write-Host "  |  CLIPBOARD MODE                                  |" -ForegroundColor Yellow
        Write-Host "  |  When a turn arrives, the command is copied to   |" -ForegroundColor Yellow
        Write-Host "  |  clipboard. Paste it manually into your CLI.    |" -ForegroundColor Yellow
        Write-Host "  |                                                   |" -ForegroundColor Yellow
        Write-Host "  |  For auto-paste, use:                            |" -ForegroundColor Yellow
        Write-Host "  |  -CliWindowTitle 'claude' -AutoPaste             |" -ForegroundColor Yellow
        Write-Host "  +-------------------------------------------------+" -ForegroundColor Yellow
    }

    Write-Host ""
    Write-Log "Waiting for turns... (Ctrl+C to stop)" -Color Yellow
    Write-Host ""

    Ensure-CliBootstrapSent

    # Process existing request if any
    if (Test-Path $TurnRequestFile) {
        Write-Log "Found pending turn request" -Level "STARTUP" -Color Yellow
        Process-Turn -RequestPath $TurnRequestFile
    }
    if (Test-Path $RepairRequestFile) {
        Write-Log "Found pending repair request" -Level "STARTUP" -Color Yellow
        Process-RepairRequest -RepairPath $RepairRequestFile
    }
    if (Test-Path $TerminalProtocolFailureRequestFile) {
        Write-Log "Found pending terminal protocol failure request" -Level "STARTUP" -Color Yellow
        Process-TerminalProtocolFailureRequest -FailurePath $TerminalProtocolFailureRequestFile
    }

    # Main loop
    $statusTimer = 0
    while ($true) {
        Start-Sleep -Milliseconds $PollingInterval

        if ((Test-Path $TurnRequestFile) -and !$script:IsProcessing) {
            Process-Turn -RequestPath $TurnRequestFile
        }
        if (Test-Path $RepairRequestFile) {
            Process-RepairRequest -RepairPath $RepairRequestFile
        }
        if (Test-Path $TerminalProtocolFailureRequestFile) {
            Process-TerminalProtocolFailureRequest -FailurePath $TerminalProtocolFailureRequestFile
        }

        # Status every 5 minutes
        $statusTimer += $PollingInterval
        if ($statusTimer -ge 300000) {
            $uptime = ((Get-Date) - $script:StartTime)
            Write-Log "Status: ${script:TurnCount} turns, ${script:ErrorCount} errors, uptime $([math]::Floor($uptime.TotalHours))h$($uptime.Minutes)m" -Color DarkGray
            $statusTimer = 0
        }
    }
}
finally {
    $watcher.EnableRaisingEvents = $false
    $watcher.Dispose()
    $repairWatcher.EnableRaisingEvents = $false
    $repairWatcher.Dispose()
    $terminalProtocolFailureWatcher.EnableRaisingEvents = $false
    $terminalProtocolFailureWatcher.Dispose()
    Write-Host ""
    Write-Log "Daemon stopped. Turns: $($script:TurnCount), Errors: $($script:ErrorCount)" -Level "SHUTDOWN" -Color Yellow
}
