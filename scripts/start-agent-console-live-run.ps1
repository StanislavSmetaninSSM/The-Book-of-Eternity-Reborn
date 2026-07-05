param(
    [string]$RunRoot,

    [string]$FixtureSessionPath = "FileSystemExample\game_session",

    [int]$Port = 0,

    [bool]$StartDaemon = $true,

    [bool]$StartBridge = $true,

    [switch]$SkipBuild,

    [int]$SnapshotTimeoutSeconds = 120,

    [int]$BridgeReadyTimeoutSeconds = 180,

    [int]$TurnTimeoutSeconds = 900
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

function New-FreeLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Parse("127.0.0.1"), 0)
    try {
        $listener.Start()
        return [int]$listener.LocalEndpoint.Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-AgentSnapshot {
    param(
        [string]$Base,
        [string]$Token,
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri "$Base/api/agent-console/snapshot" -Headers @{ Authorization = "Bearer $Token" } -TimeoutSec 5
            if ($null -ne $snapshot -and -not [string]::IsNullOrWhiteSpace([string]$snapshot.screenId)) {
                return $snapshot
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
            continue
        }

        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    throw "Agent Console snapshot did not become available within $TimeoutSeconds seconds."
}

function Wait-GmBridgeReady {
    param(
        [string]$SessionPath,
        [int]$TimeoutSeconds
    )

    $statusPath = Join-Path $SessionPath "game_state\control\gm_bridge_status.json"
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        if (Test-Path -LiteralPath $statusPath) {
            try {
                $status = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
                if ($status.ready -eq $true) {
                    return $status
                }
            }
            catch {
                Start-Sleep -Milliseconds 500
                continue
            }
        }

        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    throw "GM bridge did not become ready within $TimeoutSeconds seconds."
}

$repoRoot = (Resolve-Path ".").Path
$fixture = (Resolve-Path $FixtureSessionPath).Path

if ([string]::IsNullOrWhiteSpace($RunRoot)) {
    $RunRoot = Join-Path $env:TEMP ("boe-agent-console-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}

$RunRoot = [System.IO.Path]::GetFullPath($RunRoot)
$sessionPath = Join-Path $RunRoot "game_session"

if (Test-Path -LiteralPath $sessionPath) {
    throw "RunRoot already contains game_session: $sessionPath. Use a fresh RunRoot."
}

New-Item -ItemType Directory -Force -Path $RunRoot | Out-Null
Copy-Item -Recurse -Path $fixture -Destination $sessionPath

if ($Port -le 0) {
    $Port = New-FreeLoopbackPort
}

$base = "http://127.0.0.1:$Port"
$token = ([Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))).ToLowerInvariant()

if (-not $SkipBuild) {
    dotnet build (Join-Path $repoRoot "BookOfEternityClient\BookOfEternityClient.csproj") --no-restore | Out-Null
}

$exePath = Join-Path $repoRoot "BookOfEternityClient\bin\Debug\net8.0\BookOfEternityClient.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Console client executable not found: $exePath. Run without -SkipBuild first."
}

$stdoutPath = Join-Path $RunRoot "stdout.txt"
$stderrPath = Join-Path $RunRoot "stderr.txt"
$clientArgs = @(
    $RunRoot,
    "--agent-console",
    "--agent-url", $base,
    "--agent-token", $token,
    "--plain-output"
)

$client = Start-Process -FilePath $exePath `
    -ArgumentList $clientArgs `
    -WorkingDirectory $repoRoot `
    -RedirectStandardOutput $stdoutPath `
    -RedirectStandardError $stderrPath `
    -WindowStyle Hidden `
    -PassThru

$daemonOutputPath = Join-Path $RunRoot "start-daemon.json"
$daemon = $null
if ($StartDaemon) {
    $daemonOutput = & (Join-Path $repoRoot "BookOfEternityClient\Launcher\bookofeternity.ps1") -SessionPath $sessionPath start-daemon --timeout $TurnTimeoutSeconds --log (Join-Path $RunRoot "daemon.log")
    $daemonOutput | Set-Content -LiteralPath $daemonOutputPath -Encoding UTF8
    if (-not [string]::IsNullOrWhiteSpace(($daemonOutput -join "`n"))) {
        try {
            $daemon = ($daemonOutput -join "`n") | ConvertFrom-Json
        }
        catch {
            $daemon = $null
        }
    }
}

$bridge = $null
if ($StartBridge) {
    $bridgeOutput = & (Join-Path $repoRoot "BookOfEternityClient\Launcher\bookofeternity.ps1") -SessionPath $sessionPath start-bridge
    $bridgeOutput | Set-Content -LiteralPath (Join-Path $RunRoot "start-bridge.txt") -Encoding UTF8
    $bridge = Wait-GmBridgeReady -SessionPath $sessionPath -TimeoutSeconds $BridgeReadyTimeoutSeconds
}

$snapshot = Wait-AgentSnapshot -Base $base -Token $token -TimeoutSeconds $SnapshotTimeoutSeconds

$meta = [ordered]@{
    runRoot = $RunRoot
    sessionPath = $sessionPath
    port = $Port
    base = $base
    token = $token
    startedAt = (Get-Date).ToString("O")
    clientPid = $client.Id
    daemonPid = if ($null -ne $daemon -and $daemon.daemonPid) { [int]$daemon.daemonPid } else { $null }
    bridgeReady = if ($null -ne $bridge) { [bool]$bridge.ready } else { $false }
    initialScreenId = [string]$snapshot.screenId
}

$metaPath = Join-Path $RunRoot "live-meta.json"
$meta | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $metaPath -Encoding UTF8

[pscustomobject]$meta
