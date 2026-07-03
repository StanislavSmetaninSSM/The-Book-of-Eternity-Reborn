param(
    [Parameter(Mandatory = $true)]
    [string]$SessionPath,

    [switch]$RequireBridge,
    [switch]$RequireReadyBridge
)

$ErrorActionPreference = "Stop"

function New-PreflightIssue {
    param(
        [string]$Code,
        [string]$Message,
        [string]$Path = ""
    )

    return [ordered]@{
        code = $Code
        message = $Message
        path = $Path
    }
}

function Read-StatusJson {
    param([string]$Path)

    if (!(Test-Path -LiteralPath $Path)) {
        return $null
    }

    try {
        return Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        return [pscustomobject]@{
            unreadable = $true
            error = $_.Exception.Message
        }
    }
}

function Test-ProcessAlive {
    param([object]$ProcessId)

    $pidValue = 0
    if (-not [int]::TryParse([string]$ProcessId, [ref]$pidValue) -or $pidValue -le 0) {
        return $false
    }

    try {
        $null = Get-Process -Id $pidValue -ErrorAction Stop
        return $true
    }
    catch {
        return $false
    }
}

function Test-ComponentStatus {
    param(
        [string]$Component,
        [string]$Path,
        [object]$Status,
        [string]$PidProperty
    )

    $issues = @()
    if ($null -eq $Status) {
        $issues += New-PreflightIssue `
            -Code "$Component-status-missing" `
            -Message "$Component status file is missing." `
            -Path $Path
        return [ordered]@{
            ok = $false
            issues = $issues
        }
    }

    if ($Status.unreadable) {
        $issues += New-PreflightIssue `
            -Code "$Component-status-unreadable" `
            -Message "$Component status file is unreadable: $($Status.error)" `
            -Path $Path
        return [ordered]@{
            ok = $false
            issues = $issues
        }
    }

    $pidValue = $Status.$PidProperty
    if ($null -eq $pidValue) {
        $issues += New-PreflightIssue `
            -Code "$Component-pid-missing" `
            -Message "$Component status file does not contain $PidProperty." `
            -Path $Path
    }
    elseif (-not (Test-ProcessAlive -ProcessId $pidValue)) {
        $issues += New-PreflightIssue `
            -Code "$Component-dead-pid" `
            -Message "$Component status file points to dead pid $pidValue." `
            -Path $Path
    }

    return [ordered]@{
        ok = $issues.Count -eq 0
        issues = $issues
        status = $Status
    }
}

$resolvedSessionPath = (Resolve-Path -LiteralPath $SessionPath -ErrorAction Stop).Path
$controlPath = Join-Path $resolvedSessionPath "game_state\control"
$daemonStatusPath = Join-Path $controlPath "gm_daemon_status.json"
$bridgeStatusPath = Join-Path $controlPath "gm_bridge_status.json"

$daemonStatus = Read-StatusJson -Path $daemonStatusPath
$daemon = Test-ComponentStatus `
    -Component "daemon" `
    -Path $daemonStatusPath `
    -Status $daemonStatus `
    -PidProperty "pid"

$bridge = [ordered]@{
    ok = $true
    issues = @()
    status = $null
}

if ($RequireBridge -or $RequireReadyBridge) {
    $bridgeStatus = Read-StatusJson -Path $bridgeStatusPath
    $bridge = Test-ComponentStatus `
        -Component "bridge" `
        -Path $bridgeStatusPath `
        -Status $bridgeStatus `
        -PidProperty "helperPid"

    if ($bridge.ok -and $RequireReadyBridge -and -not [bool]$bridge.status.ready) {
        $bridge.ok = $false
        $bridge.issues += New-PreflightIssue `
            -Code "bridge-not-ready" `
            -Message "GM bridge process is alive, but ready is not true." `
            -Path $bridgeStatusPath
    }
}

$issues = @()
$issues += @($daemon.issues)
$issues += @($bridge.issues)

$result = [ordered]@{
    ok = $issues.Count -eq 0
    sessionPath = $resolvedSessionPath
    daemon = $daemon
    bridge = $bridge
    issues = $issues
}

$result | ConvertTo-Json -Depth 12
if (-not $result.ok) {
    exit 1
}
