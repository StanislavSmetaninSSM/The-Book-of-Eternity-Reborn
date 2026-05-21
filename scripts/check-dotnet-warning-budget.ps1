param(
    [Parameter(Mandatory = $true)]
    [string]$LogPath,

    [Parameter(Mandatory = $true)]
    [int]$MaxWarnings
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $LogPath)) {
    Write-Error "Build log not found: $LogPath"
}

$lines = Get-Content -LiteralPath $LogPath
$summaryMatches = @()

foreach ($line in $lines) {
    if ($line -match 'Warnings:\s*(\d+)') {
        $summaryMatches += [int]$Matches[1]
        continue
    }

    if ($line -match 'Предупреждений:\s*(\d+)') {
        $summaryMatches += [int]$Matches[1]
    }
}

if ($summaryMatches.Count -gt 0) {
    $warningCount = $summaryMatches[-1]
} else {
    $warningLines = $lines |
        Where-Object { $_ -match '\):\s+warning\s+[A-Za-z]+\d+:' } |
        Sort-Object -Unique
    $warningCount = @($warningLines).Count
}

Write-Host "Dotnet warning budget: $warningCount / $MaxWarnings"

if ($warningCount -gt $MaxWarnings) {
    Write-Error "Dotnet warning budget exceeded: $warningCount warnings found, max allowed is $MaxWarnings."
}
