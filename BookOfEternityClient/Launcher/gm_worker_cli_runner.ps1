[CmdletBinding()]
param(
    [string]$AgentCommand = "",
    [switch]$DryRun,
    [string]$PromptOutPath = "",
    [int]$TimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
$Utf8NoBom = New-Object System.Text.UTF8Encoding $false

function Exit-WithError {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message,
        [int]$ExitCode = 2
    )

    [Console]::Error.WriteLine($Message)
    exit $ExitCode
}

function Require-EnvironmentVariable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $value = [Environment]::GetEnvironmentVariable($Name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        Exit-WithError "Missing required environment variable $Name." 2
    }

    return $value
}

function Split-CommandLine {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CommandLine
    )

    $parts = New-Object System.Collections.Generic.List[string]
    $current = New-Object System.Text.StringBuilder
    $inQuotes = $false

    for ($i = 0; $i -lt $CommandLine.Length; $i++) {
        $ch = $CommandLine[$i]
        if ($ch -eq '"') {
            $inQuotes = -not $inQuotes
            continue
        }

        if ([char]::IsWhiteSpace($ch) -and -not $inQuotes) {
            if ($current.Length -gt 0) {
                $parts.Add($current.ToString())
                [void]$current.Clear()
            }
            continue
        }

        [void]$current.Append($ch)
    }

    if ($current.Length -gt 0) {
        $parts.Add($current.ToString())
    }

    return ,$parts.ToArray()
}

function ConvertTo-ProcessArguments {
    param(
        [string[]]$Arguments
    )

    $quoted = New-Object System.Collections.Generic.List[string]
    foreach ($argument in $Arguments) {
        if ($argument -match '[\s"]') {
            $escaped = $argument.Replace('"', '\"')
            $quoted.Add('"' + $escaped + '"')
        }
        else {
            $quoted.Add($argument)
        }
    }

    return [string]::Join(" ", $quoted)
}

function Build-WorkerPrompt {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TaskPath,
        [Parameter(Mandatory = $true)]
        [string]$ProposalPath,
        [Parameter(Mandatory = $true)]
        [string]$SessionPath,
        [Parameter(Mandatory = $true)]
        [string]$TaskJson
    )

    return @"
You are a subordinate GM worker for The Book of Eternity Reborn.
The main GM owns the player turn, final narration, and canonical game state.

Worker runtime paths:
- BOE_WORKER_TASK_PATH: $TaskPath
- BOE_WORKER_PROPOSAL_PATH: $ProposalPath
- BOE_WORKER_SESSION_PATH: $SessionPath

Output contract:
- Write exactly one worker-proposal-v1 JSON object to BOE_WORKER_PROPOSAL_PATH.
- Do not print the proposal instead of writing the file.
- Do not edit canonical game_session files directly.
- Do not overwrite files under game_state, lore, ready, input, or other canonical session roots.
- If the task allows changedFiles, write proposed content under worker_proposals/<proposalId>/... and reference it with contentRef.
- If the task is proposal-only, keep changedFiles empty and return draftText and/or findings.
- Leave schema validation, scope checks, and canonical application to the main GM apply gate.

Raw WorkerTaskPacket JSON begins on the next line.
BEGIN_WORKER_TASK_JSON
$TaskJson
END_WORKER_TASK_JSON
"@
}

try {
    if ($TimeoutSeconds -lt 1) {
        Exit-WithError "TimeoutSeconds must be at least 1." 2
    }

    $taskPath = Require-EnvironmentVariable "BOE_WORKER_TASK_PATH"
    $proposalPath = Require-EnvironmentVariable "BOE_WORKER_PROPOSAL_PATH"
    $sessionPath = Require-EnvironmentVariable "BOE_WORKER_SESSION_PATH"

    if (-not (Test-Path -LiteralPath $taskPath -PathType Leaf)) {
        Exit-WithError "BOE_WORKER_TASK_PATH does not point to an existing file: $taskPath" 2
    }

    if (-not (Test-Path -LiteralPath $sessionPath -PathType Container)) {
        Exit-WithError "BOE_WORKER_SESSION_PATH does not point to an existing directory: $sessionPath" 2
    }

    $proposalDirectory = Split-Path -Parent $proposalPath
    if (-not [string]::IsNullOrWhiteSpace($proposalDirectory) -and -not (Test-Path -LiteralPath $proposalDirectory -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $proposalDirectory | Out-Null
    }

    $taskJson = Get-Content -LiteralPath $taskPath -Raw -Encoding UTF8
    if ([string]::IsNullOrWhiteSpace($taskJson)) {
        Exit-WithError "BOE_WORKER_TASK_PATH is empty: $taskPath" 2
    }

    $prompt = Build-WorkerPrompt `
        -TaskPath $taskPath `
        -ProposalPath $proposalPath `
        -SessionPath $sessionPath `
        -TaskJson $taskJson

    if ($DryRun) {
        if ([string]::IsNullOrWhiteSpace($PromptOutPath)) {
            [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            [Console]::Out.WriteLine($prompt)
        }
        else {
            $promptDirectory = Split-Path -Parent $PromptOutPath
            if (-not [string]::IsNullOrWhiteSpace($promptDirectory) -and -not (Test-Path -LiteralPath $promptDirectory -PathType Container)) {
                New-Item -ItemType Directory -Force -Path $promptDirectory | Out-Null
            }
            [System.IO.File]::WriteAllText($PromptOutPath, $prompt, $Utf8NoBom)
        }

        exit 0
    }

    if ([string]::IsNullOrWhiteSpace($AgentCommand)) {
        Exit-WithError "AgentCommand is required unless -DryRun is used." 2
    }

    $commandParts = Split-CommandLine $AgentCommand
    if ($commandParts.Count -eq 0) {
        Exit-WithError "AgentCommand must contain an executable." 2
    }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $commandParts[0]
    $startInfo.WorkingDirectory = $sessionPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
    $startInfo.StandardErrorEncoding = [System.Text.Encoding]::UTF8
    if ($commandParts.Count -gt 1) {
        $agentArguments = @()
        for ($i = 1; $i -lt $commandParts.Count; $i++) {
            $agentArguments += $commandParts[$i]
        }
        $startInfo.Arguments = ConvertTo-ProcessArguments $agentArguments
    }

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    try {
        if (-not $process.Start()) {
            Exit-WithError "Worker agent command did not start." 4
        }

        $stdoutTask = $process.StandardOutput.ReadToEndAsync()
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($prompt)
        $process.StandardInput.Close()

        $timeoutMs = [Math]::Max(1, $TimeoutSeconds) * 1000
        if (-not $process.WaitForExit($timeoutMs)) {
            try {
                $process.Kill($true)
            }
            catch {
                try { $process.Kill() } catch { }
            }

            Exit-WithError "Worker agent command timed out after $TimeoutSeconds seconds." 3
        }

        $standardOutput = $stdoutTask.GetAwaiter().GetResult()
        $standardError = $stderrTask.GetAwaiter().GetResult()

        if ($process.ExitCode -ne 0) {
            if (-not [string]::IsNullOrWhiteSpace($standardOutput)) {
                [Console]::Out.Write($standardOutput)
            }
            if (-not [string]::IsNullOrWhiteSpace($standardError)) {
                [Console]::Error.Write($standardError)
            }
            Exit-WithError "Worker agent command exited with code $($process.ExitCode)." 4
        }

        if (-not (Test-Path -LiteralPath $proposalPath -PathType Leaf)) {
            Exit-WithError "Worker agent completed without writing BOE_WORKER_PROPOSAL_PATH: $proposalPath" 5
        }

        $proposalInfo = Get-Item -LiteralPath $proposalPath
        if ($proposalInfo.Length -le 0) {
            Exit-WithError "Worker agent wrote an empty BOE_WORKER_PROPOSAL_PATH: $proposalPath" 5
        }

        if (-not [string]::IsNullOrWhiteSpace($standardOutput)) {
            [Console]::Out.Write($standardOutput)
        }
        if (-not [string]::IsNullOrWhiteSpace($standardError)) {
            [Console]::Error.Write($standardError)
        }

        exit 0
    }
    finally {
        $process.Dispose()
    }
}
catch {
    Exit-WithError $_.Exception.Message 2
}
