# One-shot external .NET 8 boundary. Desktop must never Add-Type the net8 client.
function Invoke-BoeBridgeRequestProcess {
    param([string]$CodeRoot,[string]$StatusJson,[string]$RequestJson,[int]$TimeoutMilliseconds)
    throw 'External bridge request helper is not implemented.'
}
