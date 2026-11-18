param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference = 'Stop'
function Mark([string]$Name) {
    [IO.File]::AppendAllText((Join-Path $Evidence 'markers.log'), "child.$Name`n")
}
try {
    Mark 'try'
    [Console]::WriteLine('FINALLY_CHILD_READY')
    $until = [DateTime]::UtcNow.AddSeconds(8)
    while ([DateTime]::UtcNow -lt $until -and -not [IO.File]::Exists((Join-Path $Evidence 'cooperative-exit'))) {
        Start-Sleep -Milliseconds 30
    }
    if ([IO.File]::Exists((Join-Path $Evidence 'cooperative-exit'))) { Mark 'cooperative-seen' }
    else { Mark 'body-timeout' }
    Mark 'body.exit'
} catch {
    [IO.File]::AppendAllText((Join-Path $Evidence 'errors.log'), "child.body: $($_.Exception)`n")
    throw
} finally {
    try {
        Mark 'finally.enter'
        [IO.File]::WriteAllText((Join-Path $Evidence 'child-finalized'), 'inert diagnostic only')
        Mark 'finally.after-write'
        Mark 'finally.exit'
    } catch {
        [IO.File]::AppendAllText((Join-Path $Evidence 'errors.log'), "child.finally: $($_.Exception)`n")
        throw
    }
}
