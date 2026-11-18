# Qualification-only stdio adapter to the shipped public transport client.
# It owns no game authority and implements no named-pipe/FIFO protocol.
param(
    [Parameter(Mandatory = $true)][string]$ClientAssembly,
    [Parameter(Mandatory = $true)][string]$StatusPath
)
$ErrorActionPreference = 'Stop'
$assembly = [IO.Path]::GetFullPath($ClientAssembly)
if ([IO.Path]::GetFileName($assembly) -cne 'BookOfEternityClient.dll' -or -not [IO.File]::Exists($assembly)) {
    throw 'Explicit shipped client assembly is unavailable.'
}
# The driver supplies this package path. Diagnostic status never chooses code.
Add-Type -Path $assembly
[Console]::Out.WriteLine('{"fixtureReady":true}')
[Console]::Out.Flush()
while ($null -ne ($line = [Console]::In.ReadLine())) {
    $id = $null
    try {
        if ([Text.Encoding]::UTF8.GetByteCount($line) -gt 1048576) { throw 'Fixture request bound exceeded.' }
        $frame = $line | ConvertFrom-Json -ErrorAction Stop
        $id = [string]$frame.id
        if ([string]::IsNullOrEmpty($id) -or $frame.timeoutMilliseconds -lt 1 -or $frame.timeoutMilliseconds -gt 12000) {
            throw 'Invalid fixture request correlation or deadline.'
        }
        $status = [IO.File]::ReadAllText([IO.Path]::GetFullPath($StatusPath))
        $response = [BookOfEternityClient.Services.GmRuntime.BridgeTransportConnector]::RequestAsync(
            $status, [string]$frame.payload, [int]$frame.timeoutMilliseconds, [Threading.CancellationToken]::None
        ).GetAwaiter().GetResult()
        $reply = @{ id = $id; ok = $true; response = $response }
    }
    catch { $reply = @{ id = $id; ok = $false; error = $_.Exception.ToString() } }
    [Console]::Out.WriteLine(($reply | ConvertTo-Json -Depth 5 -Compress))
    [Console]::Out.Flush()
}
