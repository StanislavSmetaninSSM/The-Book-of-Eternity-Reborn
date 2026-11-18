param([Parameter(Mandatory=$true)][string]$GameSessionPath,[Parameter(Mandatory=$true)][string]$Helper)
$ErrorActionPreference='Stop'
. $Helper
try {
    Invoke-GmParticipatingConsumer $GameSessionPath {
        throw 'Held-ready diagnostic unexpectedly entered the consumer body.'
    }
    throw 'Held-ready diagnostic unexpectedly succeeded.'
} catch {
    # Expected refusal is evidence, not a successful ownership/cleanup claim.
    @{refusal=$_.Exception.ToString();observedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json -Compress
}
