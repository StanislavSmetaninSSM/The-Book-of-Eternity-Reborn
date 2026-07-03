param(
    [Parameter(Mandatory = $true)]
    [string]$Base,

    [Parameter(Mandatory = $true)]
    [string]$Token,

    [string[]]$Commands = @(),

    [string]$CommandsFile,

    [string]$OutputPath,

    [string[]]$ForbiddenPattern = @(),

    [int]$TimeoutSeconds = 10,

    [int]$ReturnStepLimit = 12,

    [int]$PollMilliseconds = 200
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$BaseUrl = $Base.TrimEnd("/")
$Headers = @{ Authorization = "Bearer $Token" }
$artifactCommands = @()
$startedAt = [DateTimeOffset]::UtcNow
$artifactStatus = "running"
$failureMessage = $null

function Get-JsonProperty {
    param(
        [Parameter(Mandatory = $true)]
        $Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Object) {
        return $null
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Invoke-AgentSnapshot {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/agent-console/snapshot"
}

function Invoke-AgentPost {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        $Body
    )

    $uri = "$BaseUrl$Path"
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method Post -Uri $uri -Headers $Headers
    }

    $json = $Body | ConvertTo-Json -Depth 20 -Compress
    Invoke-RestMethod -Method Post -Uri $uri -Headers $Headers -ContentType "application/json" -Body $json
}

function Wait-AgentSnapshot {
    param([int]$Seconds = $TimeoutSeconds)

    $deadline = (Get-Date).AddSeconds($Seconds)
    do {
        try {
            $snapshot = Invoke-AgentSnapshot
            if ($null -ne $snapshot -and $null -ne (Get-JsonProperty $snapshot "screenId")) {
                return $snapshot
            }
        }
        catch {
            # Startup can briefly return no usable snapshot. Keep polling until the bounded deadline.
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for an Agent Console snapshot."
}

function New-SnapshotSummary {
    param($Snapshot)

    $actions = Get-JsonProperty $Snapshot "actions"
    $actionCount = 0
    if ($null -ne $actions) {
        $actionCount = @($actions).Count
    }

    [ordered]@{
        screenId = Get-JsonProperty $Snapshot "screenId"
        inputKind = Get-JsonProperty $Snapshot "inputKind"
        awaitingInput = Get-JsonProperty $Snapshot "awaitingInput"
        mode = Get-JsonProperty $Snapshot "mode"
        title = Get-JsonProperty $Snapshot "title"
        actionCount = $actionCount
    }
}

function Assert-NotTurnPreparing {
    param($Snapshot)

    $screenId = [string](Get-JsonProperty $Snapshot "screenId")
    if ($screenId -eq "turn-preparing") {
        throw "Read-only command sweep reached turn-preparing. Stop: a command audit must not submit or prepare a player turn."
    }
}

function Return-ToGameLoop {
    param(
        [Parameter(Mandatory = $true)]
        [System.Collections.IList]$Trace
    )

    for ($step = 0; $step -lt $ReturnStepLimit; $step++) {
        $snapshot = Wait-AgentSnapshot
        Assert-NotTurnPreparing $snapshot

        $summary = New-SnapshotSummary $snapshot
        $Trace.Add([pscustomobject]$summary) | Out-Null

        $screenId = [string]$summary.screenId
        $inputKind = [string]$summary.inputKind
        $awaitingInput = [bool]$summary.awaitingInput

        if ($screenId -eq "game-loop") {
            if ($inputKind -ne "text") {
                throw "Expected game-loop text input, got inputKind '$inputKind'."
            }

            if (-not $awaitingInput) {
                throw "Game loop is not awaiting input."
            }

            return $snapshot
        }

        if (-not $awaitingInput) {
            throw "Screen '$screenId' is not awaiting input; refusing to continue a read-only command sweep."
        }

        if ($inputKind -eq "text") {
            throw "Screen '$screenId' is a text prompt. Refusing to type or accept defaults during a read-only command sweep."
        }

        if ($inputKind -eq "key" -or $inputKind -eq "menuSelection") {
            Invoke-AgentPost -Path "/api/agent-console/return-to-game-loop-step" -Body $null | Out-Null
            Start-Sleep -Milliseconds $PollMilliseconds
            continue
        }

        throw "Unsupported inputKind '$inputKind' on screen '$screenId' during read-only command sweep."
    }

    throw "Could not return to game-loop within $ReturnStepLimit safe steps."
}

function Wait-CommandResultSnapshot {
    param([string]$Command)

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $snapshot = Wait-AgentSnapshot
        Assert-NotTurnPreparing $snapshot

        $screenId = [string](Get-JsonProperty $snapshot "screenId")
        $inputKind = [string](Get-JsonProperty $snapshot "inputKind")

        if ($screenId -eq "command-processing") {
            Start-Sleep -Milliseconds $PollMilliseconds
            continue
        }

        if ($screenId -ne "game-loop" -or $inputKind -ne "text") {
            return $snapshot
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for command '$Command' to publish a local command screen."
}

function Find-ForbiddenMarkers {
    param($Snapshot)

    $snapshotJson = $Snapshot | ConvertTo-Json -Depth 80 -Compress
    $matches = @()
    foreach ($pattern in $ForbiddenPattern) {
        if ([string]::IsNullOrWhiteSpace($pattern)) {
            continue
        }

        if ($snapshotJson.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            $matches += [pscustomobject]@{
                pattern = $pattern
            }
        }
    }

    return $matches
}

function Write-Artifact {
    if ([string]::IsNullOrWhiteSpace($OutputPath)) {
        return
    }

    $parent = Split-Path -Parent $OutputPath
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Force -Path $parent | Out-Null
    }

    $artifact = [ordered]@{
        helper = "agent-console-readonly-sweep"
        startedAt = $startedAt.ToString("O")
        finishedAt = ([DateTimeOffset]::UtcNow).ToString("O")
        base = $BaseUrl
        status = $artifactStatus
        failure = $failureMessage
        forbiddenMarkers = $ForbiddenPattern
        commands = $artifactCommands
    }

    $artifact | ConvertTo-Json -Depth 80 | Set-Content -Path $OutputPath -Encoding UTF8
}

if (-not [string]::IsNullOrWhiteSpace($CommandsFile)) {
    $fileCommands = Get-Content -Path $CommandsFile |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object { $_.Trim() }
    $Commands = @($Commands) + @($fileCommands)
}

$Commands = @($Commands | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })

if ($Commands.Count -eq 0) {
    throw "Provide at least one command through -Commands or -CommandsFile."
}

try {
    foreach ($command in $Commands) {
        $trace = [System.Collections.ArrayList]::new()
        $entry = [ordered]@{
            command = $command
            status = "running"
            sentAt = ([DateTimeOffset]::UtcNow).ToString("O")
            before = $null
            result = $null
            resultSnapshot = $null
            returnTrace = $trace
            forbiddenMatches = @()
        }

        $before = Return-ToGameLoop -Trace $trace
        $entry["before"] = New-SnapshotSummary $before

        Invoke-AgentPost -Path "/api/agent-console/text" -Body @{ text = $command } | Out-Null
        Start-Sleep -Milliseconds $PollMilliseconds

        $resultSnapshot = Wait-CommandResultSnapshot -Command $command
        $entry["result"] = New-SnapshotSummary $resultSnapshot
        $entry["resultSnapshot"] = $resultSnapshot
        $entry["forbiddenMatches"] = @(Find-ForbiddenMarkers $resultSnapshot)

        if (@($entry["forbiddenMatches"]).Count -gt 0) {
            $entry["status"] = "forbidden-marker-found"
            $artifactCommands += [pscustomobject]$entry
            throw "Command '$command' output contained forbidden markers: $(@($entry["forbiddenMatches"]).pattern -join ', ')"
        }

        Return-ToGameLoop -Trace $trace | Out-Null
        $entry["status"] = "passed"
        $entry["finishedAt"] = ([DateTimeOffset]::UtcNow).ToString("O")
        $artifactCommands += [pscustomobject]$entry
    }

    $artifactStatus = "passed"
}
catch {
    $artifactStatus = "failed"
    $failureMessage = $_.Exception.Message
    Write-Artifact
    throw
}

Write-Artifact
