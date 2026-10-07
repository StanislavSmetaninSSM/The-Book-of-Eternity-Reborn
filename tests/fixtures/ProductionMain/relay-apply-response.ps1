param([Parameter(Mandatory)][string]$SessionPath,[Parameter(Mandatory)][string]$RequestPath,[Parameter(Mandatory)][string]$ResponsePath)
$ErrorActionPreference = 'Stop'
# Legacy fixture forwards to the one maintained fixed consumer.
. (Join-Path $PSScriptRoot '../../../tools/gm-relay/relay-apply-response.ps1') -SessionPath $SessionPath -RequestPath $RequestPath -ResponsePath $ResponsePath
