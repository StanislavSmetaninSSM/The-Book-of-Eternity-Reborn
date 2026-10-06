# Shared by launcher and daemon in the same PowerShell process. This helper owns
# no canonical file handles. The original C# operation is the sole writer.
$global:BoeMainOperationCodeRoot = Split-Path $PSScriptRoot -Parent

function Read-GmOperationReply {
    param($Context)
    $read = $Context.process.StandardOutput.ReadLineAsync()
    if (-not $read.Wait(10000)) { $Context.closed = $true; throw 'Original operation reply unavailable; no replay.' }
    $line = $read.Result
    if ($null -eq $line -or $line.Length -gt 65536) { $Context.closed = $true; throw 'Original operation connection lost; no replay.' }
    return ($line | ConvertFrom-Json -ErrorAction Stop)
}

function Open-GmParticipatingOperation {
    param([string]$SessionPath)
    $session = [IO.Path]::GetFullPath($SessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetFileName($session) -cne 'game_session') { throw 'An explicit game_session root is required.' }
    $root = [IO.Path]::GetDirectoryName($session)
    if (-not [IO.Directory]::Exists($root)) { throw 'Participating root must already exist.' }
    $assembly = @('Debug','Release') | ForEach-Object { Join-Path $global:BoeMainOperationCodeRoot "bin/$_/net8.0/BookOfEternityClient.dll" } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $assembly) { throw 'Built participating client is unavailable.' }
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($arg in @($assembly,'--gm-main-operation','--root',$root)) { [void]$start.ArgumentList.Add($arg) }
    $process = [Diagnostics.Process]::Start($start)
    $context = [pscustomobject]@{ process=$process; errorRead=$process.StandardError.ReadToEndAsync(); session=$session; sequence=0L; closed=$false; lost=$false }
    try {
        $ready = Read-GmOperationReply $context
        if (-not $ready.ok -or $ready.state -cne 'active' -or $ready.sequence -ne 0) { throw 'Participating admission refused.' }
        return $context
    } catch {
        $process.StandardInput.Dispose()
        if (-not $process.WaitForExit(4000)) { $process.Kill(); $process.WaitForExit() }
        $process.Dispose()
        throw
    }
}

function Send-GmOperationCommand {
    param($Context,[hashtable]$Command)
    if ($Context.closed -or $Context.lost) { throw 'Original operation is closed or lost; no reconnect.' }
    $Context.sequence++
    $Command.sequence = $Context.sequence
    $frame = $Command | ConvertTo-Json -Depth 8 -Compress
    if ([Text.Encoding]::UTF8.GetByteCount($frame) -gt 4194304) { throw 'Participating control frame exceeds its bound.' }
    try {
        $Context.process.StandardInput.WriteLine($frame); $Context.process.StandardInput.Flush()
        $reply = Read-GmOperationReply $Context
        if ($reply.sequence -ne $Context.sequence) { throw 'Original operation response identity mismatch.' }
    } catch { $Context.lost=$true; throw }
    if (-not $reply.ok) { throw 'Participating canonical mutation refused.' }
    return $reply
}

function Close-GmParticipatingOperation {
    param($Context,[int]$Outcome=0)
    if ($Context.closed -or $Context.lost) { throw 'Original operation cannot be closed with confirmed receipt.' }
    try {
        $reply = Send-GmOperationCommand $Context @{action='close';outcome=$Outcome}
        if ($reply.state -cne 'closed-observed') { throw 'Original terminal close is unconfirmed.' }
    } finally { $Context.closed=$true; $Context.process.StandardInput.Dispose() }
}

function Invoke-GmParticipatingConsumer {
    param([string]$SessionPath,[scriptblock]$Body)
    $session = [IO.Path]::GetFullPath($SessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ($global:BoeMainOperationContext) {
        if ($global:BoeMainOperationContext.session -cne $session -or $global:BoeMainOperationContext.closed -or $global:BoeMainOperationContext.lost) { throw 'Enclosing original operation is unavailable; no reconnect.' }
        return (& $Body)
    }
    $context = Open-GmParticipatingOperation $session
    $global:BoeMainOperationContext = $context
    $failure = $null; $result = $null
    try { $result = & $Body }
    catch { $failure=$_ }
    finally {
        try {
            if (-not $context.closed -and -not $context.lost) { Close-GmParticipatingOperation $context $(if($failure){1}else{0}) }
        } catch { if (-not $failure) { $failure=$_ } }
        finally {
            $context.process.StandardInput.Dispose()
            if (-not $context.process.WaitForExit(4000)) { $context.process.Kill(); $context.process.WaitForExit() }
            $context.process.Dispose()
            $global:BoeMainOperationContext = $null
        }
    }
    if ($failure) { throw $failure }
    return $result
}

function Close-GmParticipatingBeforeStop {
    # Closing is absorbing until the enclosing consumer unwinds. Shutdown must
    # never wait on a pin still held by its own caller.
    if ($global:BoeMainOperationContext -and -not $global:BoeMainOperationContext.closed) {
        Close-GmParticipatingOperation $global:BoeMainOperationContext -Outcome 1
    }
}

function Invoke-GmCanonicalControl {
    param([string]$SessionPath,[string]$Action,[string]$Path,[byte[]]$Bytes)
    Invoke-GmParticipatingConsumer $SessionPath {
        $session = $global:BoeMainOperationContext.session
        $relative = [IO.Path]::GetRelativePath($session,[IO.Path]::GetFullPath($Path)).Replace('\','/')
        $command = @{action=$Action;path=$relative}
        if ($null -ne $Bytes) { $command.bytes=[Convert]::ToBase64String($Bytes) }
        [void](Send-GmOperationCommand $global:BoeMainOperationContext $command)
    }
}

function Write-GmCanonicalText {
    param([string]$SessionPath,[string]$Path,[object]$Value,[switch]$Append)
    $text = (@($Value) -join [Environment]::NewLine) + [Environment]::NewLine
    Invoke-GmCanonicalControl $SessionPath $(if($Append){'append'}else{'write'}) $Path ([Text.UTF8Encoding]::new($false).GetBytes($text))
}
function Remove-GmCanonicalFile {
    param([string]$SessionPath,[string]$Path)
    Invoke-GmCanonicalControl $SessionPath 'delete' $Path $null
}
function Ensure-GmCanonicalDirectory {
    param([string]$SessionPath,[string]$Path)
    Invoke-GmCanonicalControl $SessionPath 'directory' $Path $null
}
function Copy-GmCanonicalFile {
    param([string]$SessionPath,[string]$Source,[string]$Destination)
    # Admission precedes source reading and every destination effect.
    Invoke-GmParticipatingConsumer $SessionPath {
        Invoke-GmCanonicalControl $SessionPath 'write' $Destination ([IO.File]::ReadAllBytes($Source))
    }
}
