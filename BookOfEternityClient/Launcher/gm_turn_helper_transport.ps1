# Dedicated short helper role. The default launcher/daemon control context never
# inherits these commands or this canonical lease.
if (-not (Get-Command Dispose-GmOperationTransport -ErrorAction SilentlyContinue)) {
    . (Join-Path $PSScriptRoot 'gm_main_operation.ps1')
}
$script:BoeHelperCodeRoot = Split-Path $PSScriptRoot -Parent
$script:BoeHelperScope = $null
$script:BoeHelperGeneration = $null
$script:BoeHelperFailure = $null
$script:BoePathComparison = if ([IO.Path]::DirectorySeparatorChar -eq '\') {[StringComparison]::OrdinalIgnoreCase} else {[StringComparison]::Ordinal}
$script:BoePathComparer = if ([IO.Path]::DirectorySeparatorChar -eq '\') {[StringComparer]::OrdinalIgnoreCase} else {[StringComparer]::Ordinal}

function New-BoeStorageFailure {
    param([string]$Message,[Exception]$Cause)
    $failure=[InvalidOperationException]::new($Message,$Cause)
    $failure.Data['BoeHelperStorageFailure']=$true
    return $failure
}
function Assert-BoeNotStorageFailure {
    param($Failure)
    $current=$Failure.Exception
    while($current){if($current.Data.Contains('BoeHelperStorageFailure')){throw $Failure};$current=$current.InnerException}
}
function ConvertTo-BoeWindowsArgument {
    param([string]$Value)
    # CommandLineToArgvW/CRT quoting, including quotes and trailing backslashes.
    $builder=[Text.StringBuilder]::new();[void]$builder.Append('"');$slashes=0
    foreach($c in $Value.ToCharArray()) {
        if($c -eq '\'){$slashes++;continue}
        if($c -eq '"'){[void]$builder.Append(('\' * ($slashes*2+1)));[void]$builder.Append('"')}
        else{if($slashes){[void]$builder.Append(('\' * $slashes))};[void]$builder.Append($c)}
        $slashes=0
    }
    if($slashes){[void]$builder.Append(('\' * ($slashes*2)))}
    [void]$builder.Append('"');return $builder.ToString()
}
function Start-BoeHelperProcess {
    param([string]$Root,[AllowNull()][string]$ExpectedGeneration)
    $candidates=@(Join-Path $script:BoeHelperCodeRoot 'BookOfEternityClient.dll')+@(@('Debug','Release')|ForEach-Object{Join-Path $script:BoeHelperCodeRoot "bin/$_/net8.0/BookOfEternityClient.dll"})
    $assembly=$candidates|Where-Object{[IO.File]::Exists($_)}|Select-Object -First 1
    if(-not $assembly){throw (New-BoeStorageFailure 'Built GM helper is unavailable.' $null)}
    $generation=if($ExpectedGeneration){$ExpectedGeneration}else{'initialize'}
    $arguments=@($assembly,'--gm-turn-helper','--root',$Root,'--generation',$generation)
    $start=[Diagnostics.ProcessStartInfo]::new('dotnet');$start.UseShellExecute=$false
    $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    if($start.PSObject.Properties['ArgumentList']){foreach($argument in $arguments){[void]$start.ArgumentList.Add($argument)}}
    else{$start.Arguments=($arguments|ForEach-Object{ConvertTo-BoeWindowsArgument $_}) -join ' '}
    return [Diagnostics.Process]::Start($start)
}
function Read-BoeHelperFrame {
    param($Context)
    try {
        $Context.pendingRead=$Context.process.StandardOutput.ReadLineAsync()
        if(-not $Context.pendingRead.Wait(10000)){throw 'Original helper reply unavailable.'}
        $line=$Context.pendingRead.GetAwaiter().GetResult()
        if($null -eq $line -or [Text.Encoding]::UTF8.GetByteCount($line) -gt 65536){throw 'Original helper reply lost or oversized.'}
        return ConvertFrom-Json -InputObject $line -ErrorAction Stop
    } catch {$Context.lost=$true;throw (New-BoeStorageFailure 'Original helper transport unavailable; no replay.' $_.Exception)}
}
function Send-BoeHelperFrame {
    param($Context,[hashtable]$Command)
    if($Context.closed -or $Context.lost){throw (New-BoeStorageFailure 'Original helper is closed or lost.' $null)}
    if($Context.publicationUncertain -and $Command.action -cne 'close'){throw (New-BoeStorageFailure 'Original helper publication is uncertain; no later command.' $null)}
    $next=$Context.sequence+1L;$Command.sequence=$next
    $frame=ConvertTo-Json -InputObject $Command -Depth 8 -Compress
    if([Text.Encoding]::UTF8.GetByteCount($frame) -gt 65536){throw (New-BoeStorageFailure 'Dedicated helper frame exceeds its bound.' $null)}
    try {
        $Context.sequence=$next;$Context.process.StandardInput.WriteLine($frame);$Context.process.StandardInput.Flush()
        $reply=Read-BoeHelperFrame $Context
        if($reply.sequence -ne $next){throw 'Original helper sequence changed.'}
    } catch {$Context.lost=$true;throw (New-BoeStorageFailure 'Original helper command reply unavailable; no replay.' $_.Exception)}
    $Context.lastCommandReply=$reply
    if($reply.publicationDisposition -ceq 'Uncertain' -or $reply.publicationUncertain -eq $true){
        $Context.publicationUncertain=$true
        if(-not $Context.uncertainCommandReply){$Context.uncertainCommandReply=$reply}
    }
    if(-not $reply.ok){throw (New-BoeStorageFailure 'Dedicated helper storage or protocol operation refused.' $null)}
    return $reply
}
function Complete-BoeHelperTransport {
    param($Context)
    try {Dispose-GmOperationTransport $Context}
    finally {
        if($Context.publicationUncertain -or $Context.lost){
            $script:BoeHelperFailure=New-BoeStorageFailure 'The original GM helper decision requires follow-up; no replay or reinitialization.' $null
        }
    }
}
function Open-BoeHelperScope {
    param([string]$Mode,[string]$SessionPath,[AllowNull()][string]$ExpectedGeneration)
    if($global:BoeMainOperationContext){throw (New-BoeStorageFailure 'A dedicated GM helper cannot nest inside the default participating control.' $null)}
    if($script:BoeHelperFailure){throw $script:BoeHelperFailure}
    $session=[IO.Path]::GetFullPath($SessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if([IO.Path]::GetFileName($session) -cne 'game_session'){throw (New-BoeStorageFailure 'An explicit initialized game_session is required.' $null)}
    $process=Start-BoeHelperProcess ([IO.Path]::GetDirectoryName($session)) $ExpectedGeneration
    $context=[pscustomobject]@{process=$process;errorRead=$process.StandardError.ReadToEndAsync();session=$session;sequence=0L;closed=$false;lost=$false;disposed=$false;pendingRead=$null;originalClose=$null;closeObserved=$false;closeOutcome=0;terminalClose=$null;lastCommandReply=$null;publicationUncertain=$false;uncertainCommandReply=$null;localScopeCompleted=$false;generation=$null;mode=$Mode}
    try {
        $process.StandardInput.WriteLine((@{sequence=0L;action='open';mode=$Mode}|ConvertTo-Json -Compress));$process.StandardInput.Flush()
        $reply=Read-BoeHelperFrame $context
        if($reply.state -ceq 'admission-cancelled' -and $reply.effectiveOutcome -eq 2){
            $cancelled=[OperationCanceledException]::new('Original helper admission was cancelled.')
            $cancelled.Data['BoeHelperStorageFailure']=$true
            $cancelled.Data['OriginalTerminalClose']=$reply.terminalClose
            $cancelled.Data['OriginalCloseObserved']=$reply.closeObserved
            throw $cancelled
        }
        if(-not $reply.ok -or $reply.state -cne 'active' -or $reply.sequence -ne 0 -or $reply.generation -cnotmatch '^[0-9a-f]{32}$' -or ($ExpectedGeneration -and $reply.generation -cne $ExpectedGeneration)){throw 'Helper admission identity is invalid.'}
        $context.originalClose=$reply.originalClose;$context.generation=$reply.generation
        return $context
    } catch {Complete-BoeHelperTransport $context;throw (New-BoeStorageFailure 'Original initialized helper admission refused.' $_.Exception)}
}
function Invoke-BoeHelperScope {
    param([string]$Mode='read',[scriptblock]$Body)
    if($script:BoeHelperScope){
        if($Mode -ceq 'write' -and $script:BoeHelperScope.mode -cne 'write'){throw (New-BoeStorageFailure 'Read-only helper scope cannot publish.' $null)}
        return (& $Body)
    }
    Assert-BoeGmTurnHelperInitialized
    $context=Open-BoeHelperScope $Mode $script:BoeGameSessionPath $script:BoeHelperGeneration
    $script:BoeHelperScope=$context;$value=$null;$failure=$null;$returned=$false
    try {$value=& $Body;$returned=$true}
    catch {$failure=$_}
    finally {
        try {Close-GmParticipatingOperation $context $(if($failure){1}else{0})}
        catch {if(-not $failure){$failure=$_}}
        finally {
            try {Complete-BoeHelperTransport $context}
            finally {
                $script:BoeHelperScope=$null
                if($context.publicationUncertain -or $context.lost){
                    $script:BoeHelperFailure=New-BoeStorageFailure 'The original GM helper decision requires follow-up; no replay or reinitialization.' $null
                }
            }
        }
    }
    if($script:BoeHelperFailure -and -not $failure){$failure=$script:BoeHelperFailure}
    if($failure){
        $exception=if($failure -is [Management.Automation.ErrorRecord]){$failure.Exception}else{$failure}
        if($returned){$exception.Data['EstablishedOperationResult']=$value}
        $exception.Data['EstablishedOperationOutcome']=$context.closeOutcome
        $exception.Data['OriginalTerminalClose']=$context.terminalClose
        $exception.Data['OriginalCloseObserved']=$context.closeObserved
        $exception.Data['OriginalTransportLost']=$context.lost
        throw $failure
    }
    return $value
}
function Invoke-BoeHelperRequest {
    param([hashtable]$Request,[scriptblock]$OnCommitted)
    $context=$script:BoeHelperScope
    if(-not $context){throw (New-BoeStorageFailure 'A dedicated admitted helper scope is required.' $null)}
    $utf8=[Text.UTF8Encoding]::new($false,$true)
    $payload=$utf8.GetBytes((ConvertTo-Json -InputObject $Request -Depth 12 -Compress))
    $id=[Guid]::NewGuid().ToString('N')
    [void](Send-BoeHelperFrame $context @{action='request-begin';transfer=$id;length=[long]$payload.Length;hash=(Get-BoeSha256Hex $payload)})
    for($offset=0L;$offset -lt $payload.LongLength;$offset+=$count){
        $count=[int][Math]::Min(32768L,$payload.LongLength-$offset)
        $chunk=[Convert]::ToBase64String($payload,[int]$offset,$count)
        [void](Send-BoeHelperFrame $context @{action='request-chunk';transfer=$id;offset=$offset;bytes=$chunk})
    }
    $endSequence=$context.sequence+1L
    try {$reply=Send-BoeHelperFrame $context @{action='request-end';transfer=$id}}
    finally {
        # A valid negative continuation reply may still carry the original
        # committed publication. Advance only that exact received decision.
        if($context.lastCommandReply.sequence -eq $endSequence -and
            $context.lastCommandReply.publicationDisposition -ceq 'Committed' -and $OnCommitted){& $OnCommitted}
    }
    if($reply.transfer -cne $id -or $reply.length -lt 0 -or $reply.hash -cnotmatch '^[0-9a-f]{64}$'){$context.lost=$true;throw (New-BoeStorageFailure 'Original helper transfer response is invalid.' $null)}
    $buffer=[IO.MemoryStream]::new()
    try {
        do {
            $chunk=Send-BoeHelperFrame $context @{action='response-chunk';transfer=$id;offset=$buffer.Length}
            if($chunk.transfer -cne $id -or $chunk.offset -ne $buffer.Length){$context.lost=$true;throw (New-BoeStorageFailure 'Original helper response chunk identity changed.' $null)}
            $bytes=[Convert]::FromBase64String($chunk.bytes)
            if($bytes.Length -gt 32768 -or $buffer.Length+$bytes.Length -gt $reply.length -or ($bytes.Length -eq 0 -and $reply.length -ne 0)){$context.lost=$true;throw (New-BoeStorageFailure 'Original helper response chunk length changed.' $null)}
            $buffer.Write($bytes,0,$bytes.Length)
        } while(-not $chunk.complete)
        $bytes=$buffer.ToArray()
        if($bytes.LongLength -ne $reply.length -or (Get-BoeSha256Hex $bytes) -cne $reply.hash){$context.lost=$true;throw (New-BoeStorageFailure 'Original helper response length/hash changed.' $null)}
        return ConvertFrom-BoeJsonMutable -Json ($utf8.GetString($bytes))
    } finally {$buffer.Dispose()}
}
function Read-BoeBytes {
    param([string]$Path)
    if(-not $script:BoeHelperScope){return Invoke-BoeHelperScope -Body {Read-BoeBytes $Path}}
    $result=Invoke-BoeHelperRequest @{action='read';path=$Path}
    if($null -eq $result.bytes){return $null}
    return ,([Convert]::FromBase64String($result.bytes))
}
function Read-BoeText {
    param([string]$Path)
    $bytes=Read-BoeBytes $Path
    if($null -eq $bytes){throw (New-BoeStorageFailure 'Required helper file is absent.' $null)}
    $text=[Text.Encoding]::UTF8.GetString($bytes)
    if($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF){$text=$text.Substring(1)}
    return $text
}
function Test-BoePath {
    param([string]$LiteralPath)
    if(-not $script:BoeHelperScope){return Invoke-BoeHelperScope -Body {Test-BoePath $LiteralPath}}
    return (Invoke-BoeHelperRequest @{action='kind';path=$LiteralPath}) -cne 'Missing'
}
function Get-BoeFiles {
    param([string]$LiteralPath)
    if(-not $script:BoeHelperScope){return Invoke-BoeHelperScope -Body {Get-BoeFiles $LiteralPath}}
    foreach($relative in (Invoke-BoeHelperRequest @{action='list';path=$LiteralPath})){
        [pscustomobject]@{FullName=(Resolve-BoeSessionPath $relative)}
    }
}
