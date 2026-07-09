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
    $codexWorker = 'codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -'

    return @(
        [ordered]@{
            workerId = "validation_repair_codex"
            displayName = "Codex validation repair"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 180"
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
            workerId = "narrative_draft_codex"
            displayName = "Codex narrative drafter"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
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
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
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
        },
        [ordered]@{
            workerId = "guardian_abode_content_codex"
            displayName = "Codex Guardian/Abode content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "guardian-abode-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("guardian-abode-content")
                readPaths = @("game_state/meta/guardians.json", "game_state/meta/guardian_projects.json", "game_state/meta/guardian_abode_residents.json", "game_state/meta/abode_power_journal.json", "game_state/meta/chaos_sea_guardian_politics.json", "game_state/meta/afterlife_chronicles.json", "game_state/control/system_guardian_attraction.json", "game_state/control/afterlife_return_guard.json", "game_state/control/progression_schedule.json", "OtherGuides/Afterlife_Contract_Matrix.md", "Examples/E_CLI_Afterlife_Turns.txt")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "soul_content_codex"
            displayName = "Codex soul content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "soul-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("soul-content")
                readPaths = @("game_state/meta/soul_state.json", "game_state/meta/afterlife_chronicles.json", "game_state/meta/afterlife_global_flags.json", "game_state/control/progression_schedule.json", "game_state/control/pending_dice_state.json", "OtherGuides/Afterlife_Contract_Matrix.md", "Examples/E_CLI_Afterlife_Turns.txt")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "inventory_content_codex"
            displayName = "Codex inventory content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "inventory-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("inventory-content")
                readPaths = @("game_state/core/**", "game_state/inventory/**", "game_state/world/**", "game_state/skills/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "skill_content_codex"
            displayName = "Codex skill content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "skill-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("skill-content")
                readPaths = @("game_state/core/**", "game_state/player/**", "game_state/skills/**", "game_state/combat/**", "game_state/world/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        },
        [ordered]@{
            workerId = "npc_content_codex"
            displayName = "Codex NPC content author"
            launchCommand = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$codexWorker`" -TimeoutSeconds 120"
            role = "npc-content"
            enabled = $false
            launchVisibility = "hidden"
            timeoutSeconds = 150
            maxConcurrentTasks = 1
            permissions = [ordered]@{
                taskTypes = @("npc-content")
                readPaths = @("game_state/core/**", "game_state/npcs/**", "game_state/factions/**", "game_state/quests/**", "game_state/world/**", "lore/**", "Rules/**", "TaskGuides/**")
                proposalWritePaths = @()
                proposalOnly = $true
                requiresValidation = $false
            }
        }
    )
}

function Convert-RetiredCodexLaunchDefaults {
    param([object]$Config)

    $retiredModel = 'gpt-5' + '.5'
    $currentMain = 'codex -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox'
    $retiredMainQuoted = "codex -m $retiredModel -c model_reasoning_effort=`"high`" --dangerously-bypass-approvals-and-sandbox"
    $retiredMainUnquoted = "codex -m $retiredModel -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox"

    if ($Config.GmCliLaunchCommand -ceq $retiredMainQuoted -or $Config.GmCliLaunchCommand -ceq $retiredMainUnquoted) {
        $Config.GmCliLaunchCommand = $currentMain
    }

    $currentWorker = 'codex exec -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -'
    $retiredWorkerQuoted = "codex exec -m $retiredModel -c model_reasoning_effort=`"high`" --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -"
    $retiredWorkerEscapedQuoted = $retiredWorkerQuoted.Replace('"', '\"')
    $retiredWorkerUnquoted = "codex exec -m $retiredModel -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -"

    $runner = 'BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1'
    $standardWorkerIds = @(
        'analysis_codex',
        'guardian_abode_content_codex',
        'inventory_content_codex',
        'narrative_draft_codex',
        'npc_content_codex',
        'skill_content_codex',
        'soul_content_codex'
    )

    foreach ($profile in @($Config.GmWorkerBridgeProfiles)) {
        if ($null -eq $profile -or [string]::IsNullOrWhiteSpace([string]$profile.LaunchCommand)) {
            continue
        }

        $workerId = [string]$profile.WorkerId
        $runnerTimeout = if ($workerId -ceq 'validation_repair_codex') {
            180
        } elseif ($standardWorkerIds -ccontains $workerId) {
            120
        } else {
            $null
        }
        if ($null -eq $runnerTimeout) {
            continue
        }

        $currentLaunch = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$currentWorker`" -TimeoutSeconds $runnerTimeout"
        $retiredQuotedLaunch = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$retiredWorkerEscapedQuoted`" -TimeoutSeconds $runnerTimeout"
        $retiredUnquotedLaunch = "powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File `"$runner`" -AgentCommand `"$retiredWorkerUnquoted`" -TimeoutSeconds $runnerTimeout"
        if ($profile.LaunchCommand -ceq $retiredQuotedLaunch -or $profile.LaunchCommand -ceq $retiredUnquotedLaunch) {
            $profile.LaunchCommand = $currentLaunch
        }
    }

    return $Config
}

function Read-GameConfig {
    param([string]$ResolvedSessionPath)

    $configPath = Join-Path $ResolvedSessionPath "config.json"
    $defaults = [ordered]@{
        GmBridgeEnabled = $true
        GmBridgeBackend = "ConPTYBridge"
        GmCliLaunchCommand = 'codex -m gpt-5.6-terra -c model_reasoning_effort=high --dangerously-bypass-approvals-and-sandbox'
        GmBridgeShellWorkingDirectory = ""
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
                $loaded | Add-Member -NotePropertyName $key -NotePropertyValue $defaults[$key] -Force
            }
        }
        return (Convert-RetiredCodexLaunchDefaults -Config $loaded)
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

function Assert-BridgeResponseOk {
    param(
        [object]$Response
    )

    if ($null -ne $Response.ok -and -not [bool]$Response.ok) {
        $message = if (-not [string]::IsNullOrWhiteSpace($Response.error)) {
            [string]$Response.error
        } elseif ($null -ne $Response.status -and -not [string]::IsNullOrWhiteSpace($Response.status.lastError)) {
            [string]$Response.status.lastError
        } else {
            "Bridge returned ok=false without an error message."
        }

        throw "GM bridge request failed: $message"
    }
}

function Invoke-BridgeRequestChecked {
    param(
        [string]$ResolvedSessionPath,
        [hashtable]$Payload
    )

    $response = Invoke-BridgeRequest -ResolvedSessionPath $ResolvedSessionPath -Payload $Payload
    Assert-BridgeResponseOk -Response $response
    return $response
}

function Test-ProcessAliveById {
    param([int]$ProcessId)

    try {
        $null = Get-Process -Id $ProcessId -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Get-ProcessDescendantIds {
    param([int[]]$RootProcessIds)

    if ($null -eq $RootProcessIds -or $RootProcessIds.Count -eq 0) {
        return @()
    }

    $allProcesses = @()
    try {
        $allProcesses = @(Get-CimInstance Win32_Process -ErrorAction Stop | Select-Object ProcessId, ParentProcessId)
    }
    catch {
        return @()
    }

    $remainingParents = New-Object System.Collections.Generic.Queue[int]
    $seen = New-Object System.Collections.Generic.HashSet[int]
    foreach ($rootProcessId in $RootProcessIds) {
        if ($rootProcessId -gt 0 -and $seen.Add($rootProcessId)) {
            $remainingParents.Enqueue($rootProcessId)
        }
    }

    $descendants = New-Object System.Collections.Generic.List[int]
    while ($remainingParents.Count -gt 0) {
        $parentId = $remainingParents.Dequeue()
        foreach ($process in $allProcesses) {
            if ([int]$process.ParentProcessId -ne $parentId) {
                continue
            }

            $childId = [int]$process.ProcessId
            if ($childId -le 0 -or -not $seen.Add($childId)) {
                continue
            }

            $descendants.Add($childId)
            $remainingParents.Enqueue($childId)
        }
    }

    return @($descendants)
}

function Get-BridgeTrackedProcessIds {
    param([object]$Status)

    if ($null -eq $Status) {
        return @()
    }

    $rootIds = New-Object System.Collections.Generic.List[int]
    foreach ($name in @("helperPid", "shellPid", "cliProcessId")) {
        $value = $Status.$name
        $processId = 0
        if ($null -ne $value -and [int]::TryParse([string]$value, [ref]$processId) -and $processId -gt 0) {
            $rootIds.Add($processId)
        }
    }

    $descendantIds = @(Get-ProcessDescendantIds -RootProcessIds @($rootIds))
    $combined = @($descendantIds) + @($rootIds)
    return @($combined | Where-Object { $_ -gt 0 -and $_ -ne $PID } | Select-Object -Unique)
}

function Wait-TrackedProcessesExit {
    param(
        [int[]]$ProcessIds,
        [int]$TimeoutMilliseconds = 5000
    )

    $deadline = [DateTimeOffset]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
    do {
        $remaining = @($ProcessIds | Where-Object { Test-ProcessAliveById -ProcessId $_ })
        if ($remaining.Count -eq 0) {
            return @()
        }

        Start-Sleep -Milliseconds 200
    } while ([DateTimeOffset]::UtcNow -lt $deadline)

    return @($ProcessIds | Where-Object { Test-ProcessAliveById -ProcessId $_ })
}

function Stop-SessionLocalBridgeProcesses {
    param([object]$Status)

    $processIds = @(Get-BridgeTrackedProcessIds -Status $Status)
    $stoppedProcessIds = New-Object System.Collections.Generic.List[int]
    foreach ($processId in $processIds) {
        if (-not (Test-ProcessAliveById -ProcessId $processId)) {
            continue
        }

        try {
            Stop-Process -Id $processId -Force -ErrorAction Stop
            $stoppedProcessIds.Add($processId)
        }
        catch {
            # Keep going so the report can include every remaining process.
        }
    }

    $remainingProcessIds = @(Wait-TrackedProcessesExit -ProcessIds $processIds -TimeoutMilliseconds 2000)
    return [pscustomobject][ordered]@{
        processIds = @($processIds)
        stoppedProcessIds = @($stoppedProcessIds)
        remainingProcessIds = @($remainingProcessIds)
    }
}

function Remove-BridgeStatusFileIfStopped {
    param([string]$ResolvedSessionPath)

    $statusPath = Get-BridgeStatusPath $ResolvedSessionPath
    Remove-Item $statusPath -Force -ErrorAction SilentlyContinue
}

function New-BridgeShutdownResult {
    param(
        [string]$ResolvedSessionPath,
        [bool]$Ok,
        [string]$Status,
        [bool]$FallbackUsed,
        [object]$BridgeResponse,
        [object]$ProcessReport,
        [string]$ErrorMessage = ""
    )

    $processIds = if ($null -ne $ProcessReport) { @($ProcessReport.processIds) } else { @() }
    $stoppedProcessIds = if ($null -ne $ProcessReport) { @($ProcessReport.stoppedProcessIds) } else { @() }
    $remainingProcessIds = if ($null -ne $ProcessReport) { @($ProcessReport.remainingProcessIds) } else { @() }

    return [pscustomobject][ordered]@{
        ok = $Ok
        command = "shutdown"
        status = $Status
        sessionPath = $ResolvedSessionPath
        fallbackUsed = $FallbackUsed
        error = $ErrorMessage
        processIds = @($processIds)
        stoppedProcessIds = @($stoppedProcessIds)
        remainingProcessIds = @($remainingProcessIds)
        bridgeResponse = $BridgeResponse
    }
}

function Invoke-BridgeShutdown {
    param([string]$ResolvedSessionPath)

    $statusBefore = Read-BridgeStatus $ResolvedSessionPath
    if ($null -eq $statusBefore) {
        return New-BridgeShutdownResult `
            -ResolvedSessionPath $ResolvedSessionPath `
            -Ok $true `
            -Status "already-stopped" `
            -FallbackUsed $false `
            -BridgeResponse $null `
            -ProcessReport $null
    }

    try {
        $response = Invoke-BridgeRequest -ResolvedSessionPath $ResolvedSessionPath -Payload @{
            command = "shutdown"
        }
        Assert-BridgeResponseOk -Response $response

        $trackedStatus = if ($null -ne $response.status) { $response.status } else { $statusBefore }
        $processIds = @(Get-BridgeTrackedProcessIds -Status $trackedStatus)
        $remainingProcessIds = @(Wait-TrackedProcessesExit -ProcessIds $processIds -TimeoutMilliseconds 5000)
        if ($remainingProcessIds.Count -eq 0) {
            Remove-BridgeStatusFileIfStopped -ResolvedSessionPath $ResolvedSessionPath
            return New-BridgeShutdownResult `
                -ResolvedSessionPath $ResolvedSessionPath `
                -Ok $true `
                -Status "graceful-stopped" `
                -FallbackUsed $false `
                -BridgeResponse $response `
                -ProcessReport ([pscustomobject][ordered]@{
                    processIds = @($processIds)
                    stoppedProcessIds = @()
                    remainingProcessIds = @()
                })
        }

        $fallback = Stop-SessionLocalBridgeProcesses -Status $trackedStatus
        $ok = @($fallback.remainingProcessIds).Count -eq 0
        if ($ok) {
            Remove-BridgeStatusFileIfStopped -ResolvedSessionPath $ResolvedSessionPath
        }

        return New-BridgeShutdownResult `
            -ResolvedSessionPath $ResolvedSessionPath `
            -Ok $ok `
            -Status $(if ($ok) { "fallback-stopped" } else { "fallback-failed" }) `
            -FallbackUsed $true `
            -BridgeResponse $response `
            -ProcessReport $fallback `
            -ErrorMessage "Graceful shutdown did not stop all tracked session-local processes within timeout."
    }
    catch {
        $fallback = Stop-SessionLocalBridgeProcesses -Status $statusBefore
        $ok = @($fallback.remainingProcessIds).Count -eq 0
        if ($ok) {
            Remove-BridgeStatusFileIfStopped -ResolvedSessionPath $ResolvedSessionPath
        }

        return New-BridgeShutdownResult `
            -ResolvedSessionPath $ResolvedSessionPath `
            -Ok $ok `
            -Status $(if ($ok) { "fallback-stopped" } else { "fallback-failed" }) `
            -FallbackUsed $true `
            -BridgeResponse $null `
            -ProcessReport $fallback `
            -ErrorMessage $_.Exception.Message
    }
}

function Start-Bridge {
    param(
        [string]$ResolvedSessionPath,
        [bool]$VisibleBridge = $false
    )

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

    $bridgeExe = Join-Path $repoRoot "BookOfEternityGMBridge\bin\Debug\net8.0-windows\BookOfEternityGMBridge.exe"
    $bridgeCommand = if (Test-Path $bridgeExe) {
        'Set-Location "{0}"; & "{1}" --host --sessionPath "{2}" --pipeName "{3}"' -f $repoRoot, $bridgeExe, $ResolvedSessionPath, $pipeName
    }
    else {
        'Set-Location "{0}"; dotnet run --project "{1}" -- --host --sessionPath "{2}" --pipeName "{3}"' -f $repoRoot, $projectPath, $ResolvedSessionPath, $pipeName
    }
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

    $windowStyle = if ($VisibleBridge) { "Normal" } else { "Hidden" }
    $bridgeHostArguments = @()
    if ($VisibleBridge) {
        $bridgeHostArguments += "-NoExit"
    }
    $bridgeHostArguments += @("-ExecutionPolicy", "Bypass", "-EncodedCommand", $encodedHostScript)

    Start-Process -FilePath "powershell.exe" `
        -ArgumentList $bridgeHostArguments `
        -WorkingDirectory $repoRoot `
        -WindowStyle $windowStyle | Out-Null

    if ($VisibleBridge) {
        Write-Host "GM bridge starting in a visible console window..." -ForegroundColor Green
    }
    else {
        Write-Host "GM bridge starting in a hidden console window. Use status/diagnostics to inspect it." -ForegroundColor Green
    }
}

function ConvertTo-PowerShellSingleQuotedLiteral {
    param([string]$Value)
    return "'" + ($Value -replace "'", "''") + "'"
}

function Start-Daemon {
    param(
        [string]$ResolvedSessionPath,
        [string[]]$DaemonArguments
    )

    $visibleDaemon = $false
    $autoPaste = $true
    $turnTimeout = 900
    $pasteMode = "RightClick"
    $basePath = Split-Path $ResolvedSessionPath -Parent
    $logFile = Join-Path $basePath "daemon.log"

    for ($index = 0; $index -lt $DaemonArguments.Count; $index++) {
        $arg = $DaemonArguments[$index]
        if ([string]::Equals($arg, "visible", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($arg, "-visible", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($arg, "--visible", [System.StringComparison]::OrdinalIgnoreCase)) {
            $visibleDaemon = $true
            continue
        }

        if ([string]::Equals($arg, "--no-autopaste", [System.StringComparison]::OrdinalIgnoreCase)) {
            $autoPaste = $false
            continue
        }

        if ([string]::Equals($arg, "--timeout", [System.StringComparison]::OrdinalIgnoreCase)) {
            if ($index + 1 -ge $DaemonArguments.Count) {
                throw "Missing value for --timeout."
            }
            $turnTimeout = [int]$DaemonArguments[++$index]
            continue
        }

        if ([string]::Equals($arg, "--log", [System.StringComparison]::OrdinalIgnoreCase)) {
            if ($index + 1 -ge $DaemonArguments.Count) {
                throw "Missing value for --log."
            }
            $logFile = $DaemonArguments[++$index]
            continue
        }

        if ([string]::Equals($arg, "--paste-mode", [System.StringComparison]::OrdinalIgnoreCase)) {
            if ($index + 1 -ge $DaemonArguments.Count) {
                throw "Missing value for --paste-mode."
            }
            $pasteMode = $DaemonArguments[++$index]
            if (@("RightClick", "ShiftInsert", "CtrlV") -notcontains $pasteMode) {
                throw "Unsupported --paste-mode '$pasteMode'."
            }
            continue
        }

        throw "Unknown start-daemon argument: $arg"
    }

    $daemonScript = Join-Path (Get-ClientRoot) "game_master_daemon.ps1"
    if (!(Test-Path $daemonScript)) {
        throw "GM daemon script not found: $daemonScript"
    }

    $daemonInvocation = "& {0} -GameSessionPath {1} -PasteMode {2} -TurnTimeout {3} -LogFile {4}" -f `
        (ConvertTo-PowerShellSingleQuotedLiteral $daemonScript),
        (ConvertTo-PowerShellSingleQuotedLiteral $ResolvedSessionPath),
        (ConvertTo-PowerShellSingleQuotedLiteral $pasteMode),
        $turnTimeout,
        (ConvertTo-PowerShellSingleQuotedLiteral $logFile)

    if ($autoPaste) {
        $daemonInvocation += " -AutoPaste"
    }

    $hostScript = @"
`$ErrorActionPreference = 'Stop'
$daemonInvocation
"@
    $encodedHostScript = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($hostScript))
    $windowStyle = if ($visibleDaemon) { "Normal" } else { "Hidden" }

    $process = Start-Process -FilePath "powershell.exe" `
        -ArgumentList @("-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encodedHostScript) `
        -WorkingDirectory (Get-RepoRoot) `
        -WindowStyle $windowStyle `
        -PassThru

    [pscustomobject]@{
        ok = $true
        daemonPid = $process.Id
        sessionPath = $ResolvedSessionPath
        logFile = $logFile
        visible = $visibleDaemon
        autoPaste = $autoPaste
        turnTimeout = $turnTimeout
    } | ConvertTo-Json -Depth 4
}

function Invoke-PrepareTurn {
    param(
        [string]$ResolvedSessionPath,
        [string[]]$PrepareArguments
    )

    $repoRoot = Get-RepoRoot
    $clientRoot = Get-ClientRoot
    $clientProject = Join-Path $clientRoot "BookOfEternityClient.csproj"
    if (!(Test-Path $clientProject)) {
        throw "Client project not found: $clientProject"
    }

    $basePath = Split-Path $ResolvedSessionPath -Parent
    $clientExe = Join-Path $clientRoot "bin\Debug\net8.0\BookOfEternityClient.exe"
    $clientArguments = @($basePath, "--prepare-live-turn") + $PrepareArguments

    if (Test-Path $clientExe) {
        & $clientExe @clientArguments
        if ($LASTEXITCODE -ne 0) {
            throw "prepare-turn failed with exit code $LASTEXITCODE."
        }
        return
    }

    dotnet run --project $clientProject -- @clientArguments
    if ($LASTEXITCODE -ne 0) {
        throw "prepare-turn failed with exit code $LASTEXITCODE."
    }
}

$resolvedSessionPath = Resolve-SessionPath $SessionPath

switch ($Action.ToLowerInvariant()) {
    "prepare-turn" {
        Invoke-PrepareTurn -ResolvedSessionPath $resolvedSessionPath -PrepareArguments $Arguments
        break
    }
    "start-bridge" {
        $visibleBridge = @($Arguments | Where-Object {
            [string]::Equals($_, "visible", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($_, "-visible", [System.StringComparison]::OrdinalIgnoreCase) -or
            [string]::Equals($_, "--visible", [System.StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        Start-Bridge -ResolvedSessionPath $resolvedSessionPath -VisibleBridge $visibleBridge
        break
    }
    "start-daemon" {
        Start-Daemon -ResolvedSessionPath $resolvedSessionPath -DaemonArguments $Arguments
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
        Invoke-BridgeRequestChecked -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "diagnostics"
        } | ConvertTo-Json -Depth 8
        break
    }
    "addtext" {
        $text = ($Arguments -join " ")
        Invoke-BridgeRequestChecked -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "addText"
            text = $text
        } | ConvertTo-Json -Depth 8
        break
    }
    "sendenter" {
        Invoke-BridgeRequestChecked -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "sendEnter"
        } | ConvertTo-Json -Depth 8
        break
    }
    "dispatchprompt" {
        $text = ($Arguments -join " ")
        Invoke-BridgeRequestChecked -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "dispatchPrompt"
            text = $text
            appendEnter = $true
        } | ConvertTo-Json -Depth 8
        break
    }
    "ready" {
        Invoke-BridgeRequestChecked -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "setReady"
            ready = $true
        } | ConvertTo-Json -Depth 8
        break
    }
    "not-ready" {
        Invoke-BridgeRequestChecked -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "setReady"
            ready = $false
        } | ConvertTo-Json -Depth 8
        break
    }
    "restart-shell" {
        Invoke-BridgeRequestChecked -ResolvedSessionPath $resolvedSessionPath -Payload @{
            command = "restartShell"
        } | ConvertTo-Json -Depth 8
        break
    }
    "shutdown-bridge" {
        Invoke-BridgeShutdown -ResolvedSessionPath $resolvedSessionPath | ConvertTo-Json -Depth 8
        break
    }
    default {
        Write-Host "Usage:" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 -SessionPath <game_session path> prepare-turn --action <text> [--session-id <id>] [--request-id <id>] [--turn-number <n>] [--dice `"1,2,3`"]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 start-bridge [visible] [-SessionPath <path>]" -ForegroundColor Yellow
        Write-Host "  .\bookofeternity.ps1 start-daemon [visible] [--timeout <seconds>] [--log <path>] [--paste-mode RightClick|ShiftInsert|CtrlV] [--no-autopaste] [-SessionPath <path>]" -ForegroundColor Yellow
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
