param(
    [Parameter(Position = 0)]
    [string]$Action = "",
    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Arguments = @(),
    [string]$SessionPath = ""
)

$ErrorActionPreference = "Stop"

function Get-ClientRoot {
    return (Split-Path $PSScriptRoot -Parent)
}

function Get-RepoRoot {
    return (Split-Path (Get-ClientRoot) -Parent)
}

function Resolve-SessionPath {
    param([string]$RawSessionPath)

    $clientRoot = Get-ClientRoot
    if (-not [string]::IsNullOrWhiteSpace($RawSessionPath)) {
        return (Resolve-Path $RawSessionPath).Path
    }

    $defaultPath = Join-Path $clientRoot "game_session"
    if (Test-Path $defaultPath) {
        return (Resolve-Path $defaultPath).Path
    }

    return $defaultPath
}

function New-DefaultGmWorkerBridgeProfiles {
    $runner = 'BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1'
    $codex = 'codex --dangerously-bypass-approvals-and-sandbox'

    return @(
        [ordered]@{
            workerId = "validation_repair_codex"
            displayName = "Codex validation repair"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codex`" -TimeoutSeconds 180"
            role = "validation-repair"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 210
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("validation-repair")
                readPaths = @("game_state/**", "lore/**", "input/**", "ready/**")
                proposalWritePaths = @("game_state/**", "lore/**", "ready/**")
                proposalOnly = $false
                requiresValidation = $true
            }
        },
        [ordered]@{
            workerId = "narrative_draft_gemini"
            displayName = "Gemini narrative drafter"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"gemini`" -TimeoutSeconds 120"
            role = "narrative-draft"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("narrative-draft")
                readPaths = @("game_state/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "analysis_codex"
            displayName = "Codex analysis worker"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codex`" -TimeoutSeconds 120"
            role = "analysis"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("analysis")
                readPaths = @("game_state/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        }
    )
}

function Read-GameConfig {
    param([string]$ResolvedSessionPath)

    $configPath = Join-Path $ResolvedSessionPath "config.json"
    $defaults = [ordered]@{
        GmBridgeEnabled = $true
        GmBridgeBackend = "ConPTYBridge"
        GmCliLaunchCommand = "gemini"
        GmBridgeAutoStart = $false
        GmBridgePipeNameOverride = ""
        GmWorkerBridgeProfiles = New-DefaultGmWorkerBridgeProfiles
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

function Get-BridgeStatusPath {
    param([string]$ResolvedSessionPath)
    return (Join-Path $ResolvedSessionPath "game_state\control\gm_bridge_status.json")
}

function Test-BridgeHelperAlive {
    param([object]$Status)

    if ($null -eq $Status -or $null -eq $Status.helperPid) {
        return $false
    }

    try {
        $null = Get-Process -Id ([int]$Status.helperPid) -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Read-BridgeStatus {
    param([string]$ResolvedSessionPath)
    $statusPath = Get-BridgeStatusPath $ResolvedSessionPath
    if (!(Test-Path $statusPath)) { return $null }

    try {
        $status = Get-Content -Path $statusPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not (Test-BridgeHelperAlive $status)) {
            Remove-Item $statusPath -Force -ErrorAction SilentlyContinue
            return $null
        }
        return $status
    }
    catch {
        return $null
    }
}

function Invoke-BridgeRequest {
    param(
        [string]$ResolvedSessionPath,
        [hashtable]$Payload
    )

    $status = Read-BridgeStatus $ResolvedSessionPath
    if ($null -eq $status -or [string]::IsNullOrWhiteSpace($status.pipeName)) {
        throw "GM bridge status file not found or pipeName is missing."
    }

    $pipe = New-Object System.IO.Pipes.NamedPipeClientStream(".", [string]$status.pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(3000)
        $writer = New-Object System.IO.StreamWriter($pipe, [System.Text.Encoding]::UTF8, 1024, $true)
        $writer.AutoFlush = $true
        $json = $Payload | ConvertTo-Json -Depth 8 -Compress
        $writer.WriteLine($json)
        $writer.Flush()

        $reader = New-Object System.IO.StreamReader($pipe, [System.Text.Encoding]::UTF8, $false, 1024, $true)
        $responseJson = $reader.ReadLine()
        if ([string]::IsNullOrWhiteSpace($responseJson)) {
            throw "Bridge returned an empty response."
        }

        return $responseJson | ConvertFrom-Json
    }
    finally {
        $pipe.Dispose()
    }
}

function Start-Bridge {
    param([string]$ResolvedSessionPath)

    $status = Read-BridgeStatus $ResolvedSessionPath
    if ($status -and $status.helperPid) {
        try {
            $proc = Get-Process -Id ([int]$status.helperPid) -ErrorAction Stop
            Write-Host "GM bridge already running (pid=$($proc.Id))." -ForegroundColor Yellow
            return
        }
        catch {
            # stale status file, continue
        }
    }

    $config = Read-GameConfig $ResolvedSessionPath
    $pipeName = if (-not [string]::IsNullOrWhiteSpace($config.GmBridgePipeNameOverride)) {
        $config.GmBridgePipeNameOverride
    } else {
        "boe-gmbridge-" + [guid]::NewGuid().ToString("N")
    }

    $repoRoot = Get-RepoRoot
    $projectPath = Join-Path $repoRoot "BookOfEternityGMBridge\BookOfEternityGMBridge.csproj"
    if (!(Test-Path $projectPath)) {
        throw "Bridge project not found: $projectPath"
    }

    $bridgeCommand = 'Set-Location "{0}"; dotnet run --project "{1}" -- --host --sessionPath "{2}" --pipeName "{3}"' -f $repoRoot, $projectPath, $ResolvedSessionPath, $pipeName
    $hostScriptTemplate = @'
$ErrorActionPreference = 'Stop'
try {{
    {0}
}}
catch {{
    Write-Host ''
    Write-Host 'Bridge startup failed:' -ForegroundColor Red
    Write-Host $_ -ForegroundColor Red
}}
'@
    $hostScript = $hostScriptTemplate -f $bridgeCommand
    $encodedHostScript = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($hostScript))

    Start-Process -FilePath "powershell.exe" `
        -ArgumentList @("-NoExit", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encodedHostScript) `
        -WorkingDirectory $repoRoot | Out-Null

    Write-Host "GM bridge starting in a new console window..." -ForegroundColor Green
}

$resolvedSessionPath = Resolve-SessionPath $SessionPath

switch ($Action.ToLowerInvariant()) {
    "start-bridge" {
        Start-Bridge -ResolvedSessionPath $resolvedSessionPath
        break
    }
    "status" {
        $status = Read-BridgeStatus $resolvedSessionPath
        if ($null -eq $status) {
            throw "GM bridge status file not found."
        }
        $status | ConvertTo-Json -Depth 8
        break
    }
    "diagnostics" {
        Invoke-BridgeRequest -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "diagnostics"
        } | ConvertTo-Json -Depth 8
        break
    }
    "addtext" {
        $text = ($Arguments -join " ")
        Invoke-BridgeRequest -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "addText"
            text = $text
        } | ConvertTo-Json -Depth 8
        break
    }
    "sendenter" {
        Invoke-BridgeRequest -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "sendEnter"
        } | ConvertTo-Json -Depth 8
        break
    }
    "dispatchprompt" {
        $text = ($Arguments -join " ")
        Invoke-BridgeRequest -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "dispatchPrompt"
            text = $text
            appendEnter = $true
        } | ConvertTo-Json -Depth 8
        break
    }
    "ready" {
        Invoke-BridgeRequest -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "setReady"
            ready = $true
        } | ConvertTo-Json -Depth 8
        break
    }
    "not-ready" {
        Invoke-BridgeRequest -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "setReady"
            ready = $false
        } | ConvertTo-Json -Depth 8
        break
    }
    "restart-shell" {
        Invoke-BridgeRequest -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "restartShell"
        } | ConvertTo-Json -Depth 8
        break
    }
    "shutdown-bridge" {
        Invoke-BridgeRequest -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "shutdown"
        } | ConvertTo-Json -Depth 8
        break
    }
    default {
        Write-Host "Usage:" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 start-bridge [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 status [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 diagnostics [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 addText <text> [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 sendEnter [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 dispatchPrompt <text> [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 ready [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 not-ready [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 restart-shell [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 shutdown-bridge [-SessionPath <path>]" -ForegroundColor Yellow
        exit 1
    }
}
