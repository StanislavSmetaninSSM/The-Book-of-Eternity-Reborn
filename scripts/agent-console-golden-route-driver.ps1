param(
    [Parameter(Mandatory = $true)]
    [string]$Base,

    [Parameter(Mandatory = $true)]
    [string]$Token,

    [Parameter(Mandatory = $true)]
    [string]$StepsFile,

    [string]$OutputPath,

    [int]$TimeoutSeconds = 900,

    [int]$PollMilliseconds = 500,

    [int]$ReturnStepLimit = 16,

    [bool]$FailOnUnexpectedAwaitingScreen = $true,

    [string[]]$AutoContinueKeyScreens = @(
        "incarnation-trigger-gate-opened",
        "realm-transition-mortal-life",
        "stat-allocation-finished",
        "life-transition-death",
        "realm-transition-chaos-sea",
        "life-evaluation-rewards"
    )
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$BaseUrl = $Base.TrimEnd("/")
$Headers = @{ Authorization = "Bearer $Token" }
$startedAt = [DateTimeOffset]::UtcNow
$artifactStatus = "running"
$failureMessage = $null
$stepArtifacts = [System.Collections.ArrayList]::new()

<#
step kinds:
- text: wait for a text screen and submit `text`.
- action: wait for a screen and choose an action by `actionId` or `labelPattern`;
  use `activate: true` when a menu action may first move selection and then needs activation.
- defaultAction: accept the current enabled default action.
- keys: send a bounded sequence of key names, useful for stat-allocation.
- returnToGameLoop: safely unwind local command screens through return-to-game-loop-step.

autoContinueKeyScreens:
The driver can automatically accept known local key prompts such as transition
and summary screens. Do not add stat-allocation here: stat-allocation requires
an explicit keys step.

FailOnUnexpectedAwaitingScreen:
When enabled, a route waiting for a concrete screen fails immediately if another
awaiting-input screen appears and is not an approved auto-continue screen. Add an
explicit step, screenIds alternative, or allowIntermediateAwaitingScreens only
when the intermediate prompt is intentional.

notAwaitingInput:
The driver records input rejections such as notAwaitingInput in the artifact and
fails the step instead of sleeping blindly.
#>

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

function Get-JsonArrayProperty {
    param(
        [Parameter(Mandatory = $true)]
        $Object,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = Get-JsonProperty $Object $Name
    if ($null -eq $value) {
        return @()
    }

    return @($value)
}

function Invoke-AgentSnapshot {
    Invoke-RestMethod -Method Get -Uri "$BaseUrl/api/agent-console/snapshot" -Headers $Headers
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

    $json = $Body | ConvertTo-Json -Depth 40 -Compress
    Invoke-RestMethod -Method Post -Uri $uri -Headers $Headers -ContentType "application/json" -Body $json
}

function New-SnapshotSummary {
    param($Snapshot)

    $actions = @(Get-JsonArrayProperty $Snapshot "actions")

    [ordered]@{
        screenId = Get-JsonProperty $Snapshot "screenId"
        inputKind = Get-JsonProperty $Snapshot "inputKind"
        awaitingInput = Get-JsonProperty $Snapshot "awaitingInput"
        mode = Get-JsonProperty $Snapshot "mode"
        title = Get-JsonProperty $Snapshot "title"
        selectedIndex = Get-JsonProperty $Snapshot "selectedIndex"
        actionCount = $actions.Count
    }
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
        helper = "agent-console-golden-route-driver"
        startedAt = $startedAt.ToString("O")
        finishedAt = ([DateTimeOffset]::UtcNow).ToString("O")
        base = $BaseUrl
        status = $artifactStatus
        failure = $failureMessage
        autoContinueKeyScreens = $AutoContinueKeyScreens
        failOnUnexpectedAwaitingScreen = $FailOnUnexpectedAwaitingScreen
        steps = $stepArtifacts
    }

    $artifact | ConvertTo-Json -Depth 100 | Set-Content -Path $OutputPath -Encoding UTF8
}

function Test-ScreenMatches {
    param(
        $Snapshot,
        $Step
    )

    $screenId = [string](Get-JsonProperty $Snapshot "screenId")
    $expectedScreen = Get-JsonProperty $Step "screenId"
    $expectedScreens = @(Get-JsonArrayProperty $Step "screenIds")

    if (-not [string]::IsNullOrWhiteSpace([string]$expectedScreen)) {
        return $screenId -eq [string]$expectedScreen
    }

    if ($expectedScreens.Count -gt 0) {
        foreach ($candidate in $expectedScreens) {
            if ($screenId -eq [string]$candidate) {
                return $true
            }
        }

        return $false
    }

    return $true
}

function Invoke-AutoContinueIfAllowed {
    param(
        $Snapshot,
        [System.Collections.IList]$Trace
    )

    $screenId = [string](Get-JsonProperty $Snapshot "screenId")
    $inputKind = [string](Get-JsonProperty $Snapshot "inputKind")
    $awaitingInput = [bool](Get-JsonProperty $Snapshot "awaitingInput")

    if (-not $awaitingInput) {
        return $false
    }

    if ($AutoContinueKeyScreens -notcontains $screenId) {
        return $false
    }

    if ($inputKind -ne "key" -and $inputKind -ne "confirmation") {
        return $false
    }

    [void]$Trace.Add([pscustomobject]@{
        action = "autoContinueKeyScreens"
        screenId = $screenId
        inputKind = $inputKind
    })

    $result = Invoke-AgentPost -Path "/api/agent-console/default-action" -Body $null
    if (-not [bool](Get-JsonProperty $result "accepted")) {
        throw "Auto-continue failed on screen '$screenId': $([string](Get-JsonProperty $result "message"))"
    }

    Start-Sleep -Milliseconds $PollMilliseconds
    return $true
}

function Test-AllowIntermediateAwaitingScreens {
    param($Step)

    $value = Get-JsonProperty $Step "allowIntermediateAwaitingScreens"
    if ($null -eq $value) {
        return $false
    }

    return [bool]$value
}

function Assert-NoUnexpectedAwaitingScreen {
    param(
        $Snapshot,
        $Step,
        [System.Collections.IList]$Trace
    )

    if (-not $FailOnUnexpectedAwaitingScreen) {
        return
    }

    if (Test-AllowIntermediateAwaitingScreens -Step $Step) {
        return
    }

    if (-not [bool](Get-JsonProperty $Snapshot "awaitingInput")) {
        return
    }

    $screenId = [string](Get-JsonProperty $Snapshot "screenId")
    $inputKind = [string](Get-JsonProperty $Snapshot "inputKind")
    [void]$Trace.Add([pscustomobject]@{
        action = "FailOnUnexpectedAwaitingScreen"
        screenId = $screenId
        inputKind = $inputKind
    })

    throw "Unexpected awaiting screen '$screenId' while waiting for requested step screen. Add an explicit route step, include it in screenIds, or add it to AutoContinueKeyScreens only when it is a safe local key prompt."
}

function Wait-StepScreen {
    param(
        $Step,
        [System.Collections.IList]$Trace
    )

    $timeout = Get-JsonProperty $Step "timeoutSeconds"
    if ($null -eq $timeout) {
        $timeout = $TimeoutSeconds
    }

    $deadline = (Get-Date).AddSeconds([int]$timeout)
    do {
        try {
            $snapshot = Invoke-AgentSnapshot
            if ($null -eq $snapshot -or $null -eq (Get-JsonProperty $snapshot "screenId")) {
                Start-Sleep -Milliseconds $PollMilliseconds
                continue
            }

            [void]$Trace.Add([pscustomobject](New-SnapshotSummary $snapshot))

            if (Invoke-AutoContinueIfAllowed -Snapshot $snapshot -Trace $Trace) {
                continue
            }

            if (Test-ScreenMatches -Snapshot $snapshot -Step $Step) {
                return $snapshot
            }

            Assert-NoUnexpectedAwaitingScreen -Snapshot $snapshot -Step $Step -Trace $Trace
        }
        catch {
            [void]$Trace.Add([pscustomobject]@{
                error = $_.Exception.Message
            })

            if ($_.Exception.Message.StartsWith("Unexpected awaiting screen", [System.StringComparison]::Ordinal)) {
                throw
            }
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for requested step screen."
}

function Assert-AwaitingInput {
    param($Snapshot)

    if (-not [bool](Get-JsonProperty $Snapshot "awaitingInput")) {
        $screenId = [string](Get-JsonProperty $Snapshot "screenId")
        throw "Screen '$screenId' is notAwaitingInput."
    }
}

function Invoke-TextStep {
    param($Step, [System.Collections.IList]$Trace)

    $snapshot = Wait-StepScreen -Step $Step -Trace $Trace
    Assert-AwaitingInput $snapshot
    $inputKind = [string](Get-JsonProperty $snapshot "inputKind")
    if ($inputKind -ne "text") {
        throw "Text step expected inputKind 'text', got '$inputKind'."
    }

    $text = [string](Get-JsonProperty $Step "text")
    if ([string]::IsNullOrWhiteSpace($text)) {
        throw "Text step requires non-empty text."
    }

    return Invoke-AgentPost -Path "/api/agent-console/text" -Body @{ text = $text }
}

function Find-StepAction {
    param($Snapshot, $Step)

    $actionId = [string](Get-JsonProperty $Step "actionId")
    $labelPattern = [string](Get-JsonProperty $Step "labelPattern")
    $actions = @(Get-JsonArrayProperty $Snapshot "actions")

    foreach ($action in $actions) {
        if (-not [string]::IsNullOrWhiteSpace($actionId) -and [string](Get-JsonProperty $action "id") -eq $actionId) {
            return $action
        }

        if (-not [string]::IsNullOrWhiteSpace($labelPattern) -and [string](Get-JsonProperty $action "label") -match $labelPattern) {
            return $action
        }
    }

    throw "Could not find requested action. actionId='$actionId' labelPattern='$labelPattern'."
}

function Invoke-ActionStep {
    param($Step, [System.Collections.IList]$Trace)

    $snapshot = Wait-StepScreen -Step $Step -Trace $Trace
    Assert-AwaitingInput $snapshot
    $action = Find-StepAction -Snapshot $snapshot -Step $Step
    $actionId = [string](Get-JsonProperty $action "id")
    $screenId = [string](Get-JsonProperty $snapshot "screenId")
    $inputKind = [string](Get-JsonProperty $snapshot "inputKind")

    $result = Invoke-AgentPost -Path "/api/agent-console/action" -Body @{
        actionId = $actionId
        screenId = $screenId
        inputKind = $inputKind
    }

    if (-not [bool](Get-JsonProperty $result "accepted")) {
        throw "Action step failed: $([string](Get-JsonProperty $result "message"))"
    }

    $activate = [bool](Get-JsonProperty $Step "activate")
    if ($activate) {
        Start-Sleep -Milliseconds $PollMilliseconds
        $nextSnapshot = Invoke-AgentSnapshot
        [void]$Trace.Add([pscustomobject](New-SnapshotSummary $nextSnapshot))
        if ([string](Get-JsonProperty $nextSnapshot "screenId") -eq $screenId) {
            $second = Invoke-AgentPost -Path "/api/agent-console/action" -Body @{
                actionId = $actionId
                screenId = $screenId
                inputKind = [string](Get-JsonProperty $nextSnapshot "inputKind")
            }

            if (-not [bool](Get-JsonProperty $second "accepted")) {
                throw "Action activation failed: $([string](Get-JsonProperty $second "message"))"
            }
        }
    }

    return $result
}

function Invoke-DefaultActionStep {
    param($Step, [System.Collections.IList]$Trace)

    $snapshot = Wait-StepScreen -Step $Step -Trace $Trace
    Assert-AwaitingInput $snapshot
    $result = Invoke-AgentPost -Path "/api/agent-console/default-action" -Body $null
    if (-not [bool](Get-JsonProperty $result "accepted")) {
        throw "Default action failed: $([string](Get-JsonProperty $result "message"))"
    }

    return $result
}

function Invoke-KeysStep {
    param($Step, [System.Collections.IList]$Trace)

    $snapshot = Wait-StepScreen -Step $Step -Trace $Trace
    Assert-AwaitingInput $snapshot
    $inputKind = [string](Get-JsonProperty $snapshot "inputKind")
    if ($inputKind -ne "key" -and $inputKind -ne "menuSelection") {
        throw "Keys step expected key/menuSelection input, got '$inputKind'."
    }

    $keys = @(Get-JsonArrayProperty $Step "keys")
    if ($keys.Count -eq 0) {
        throw "Keys step requires at least one key."
    }

    foreach ($key in $keys) {
        $result = Invoke-AgentPost -Path "/api/agent-console/key" -Body @{ key = [string]$key }
        if (-not [bool](Get-JsonProperty $result "accepted")) {
            throw "Key '$key' failed: $([string](Get-JsonProperty $result "message"))"
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    }

    return @{ accepted = $true; message = "Queued $($keys.Count) keys." }
}

function Invoke-ReturnToGameLoopStep {
    param($Step, [System.Collections.IList]$Trace)

    for ($i = 0; $i -lt $ReturnStepLimit; $i++) {
        $snapshot = Wait-StepScreen -Step ([pscustomobject]@{}) -Trace $Trace
        $screenId = [string](Get-JsonProperty $snapshot "screenId")
        $inputKind = [string](Get-JsonProperty $snapshot "inputKind")

        if ($screenId -eq "game-loop" -and $inputKind -eq "text" -and [bool](Get-JsonProperty $snapshot "awaitingInput")) {
            return @{ accepted = $true; message = "Already at game-loop." }
        }

        Assert-AwaitingInput $snapshot
        $result = Invoke-AgentPost -Path "/api/agent-console/return-to-game-loop-step" -Body $null
        if (-not [bool](Get-JsonProperty $result "accepted")) {
            throw "returnToGameLoop failed: $([string](Get-JsonProperty $result "message"))"
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    }

    throw "Could not returnToGameLoop within $ReturnStepLimit steps."
}

function Invoke-GoldenRouteStep {
    param($Step, [int]$Index)

    $kind = [string](Get-JsonProperty $Step "kind")
    if ([string]::IsNullOrWhiteSpace($kind)) {
        throw "Step #$Index is missing kind."
    }

    $trace = [System.Collections.ArrayList]::new()
    $entry = [ordered]@{
        index = $Index
        kind = $kind
        name = Get-JsonProperty $Step "name"
        status = "running"
        startedAt = ([DateTimeOffset]::UtcNow).ToString("O")
        finishedAt = $null
        trace = $trace
        result = $null
        failure = $null
    }

    $entryObject = [pscustomobject]$entry
    [void]$stepArtifacts.Add($entryObject)

    try {
        switch ($kind) {
            "text" { $result = Invoke-TextStep -Step $Step -Trace $trace }
            "action" { $result = Invoke-ActionStep -Step $Step -Trace $trace }
            "defaultAction" { $result = Invoke-DefaultActionStep -Step $Step -Trace $trace }
            "keys" { $result = Invoke-KeysStep -Step $Step -Trace $trace }
            "returnToGameLoop" { $result = Invoke-ReturnToGameLoopStep -Step $Step -Trace $trace }
            default { throw "Unsupported step kind '$kind'. Supported step kinds: text, action, defaultAction, keys, returnToGameLoop." }
        }

        $entryObject.status = "passed"
        $entryObject.finishedAt = ([DateTimeOffset]::UtcNow).ToString("O")
        $entryObject.result = $result
    }
    catch {
        $entryObject.status = "failed"
        $entryObject.finishedAt = ([DateTimeOffset]::UtcNow).ToString("O")
        $entryObject.failure = $_.Exception.Message
        throw
    }
}

if (-not (Test-Path -LiteralPath $StepsFile)) {
    throw "StepsFile not found: $StepsFile"
}

$stepsJson = Get-Content -LiteralPath $StepsFile -Raw
$steps = @($stepsJson | ConvertFrom-Json)
if ($steps.Count -eq 0) {
    throw "StepsFile must contain at least one step."
}

try {
    for ($i = 0; $i -lt $steps.Count; $i++) {
        Invoke-GoldenRouteStep -Step $steps[$i] -Index $i
        Write-Artifact
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
