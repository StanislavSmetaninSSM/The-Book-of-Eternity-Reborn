[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SelectionFile,
    [string]$CatalogPath = (Join-Path $PSScriptRoot '../tests/categories.json'),
    [string]$GitHubOutput
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'testing/TestCategoryCatalog.psm1') -Force
$catalog = Read-TestCategoryCatalog $CatalogPath
$selection = Get-Content -LiteralPath $SelectionFile -Raw | ConvertFrom-Json -AsHashtable
$matrix = Get-TestCategoryCiMatrix $catalog $selection
$json = $matrix | ConvertTo-Json -Depth 20 -Compress
if ($GitHubOutput) {
    # JSON escaping preserves user text without adding extra output-file records.
    [IO.File]::AppendAllText($GitHubOutput, "matrix=$json`n", [Text.UTF8Encoding]::new($false))
}
$json
