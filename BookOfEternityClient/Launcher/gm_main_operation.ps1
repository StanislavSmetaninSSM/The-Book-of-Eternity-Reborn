# Shared by launcher and daemon in the same PowerShell process. This helper owns
# no canonical file handles. The original C# operation is the sole writer.
$global:BoeMainOperationCodeRoot = Split-Path $PSScriptRoot -Parent

function Read-GmOperationReply {
    param($Context)
    $read = $Context.process.StandardOutput.ReadLineAsync(); $Context.pendingRead=$read
    if (-not $read.Wait(10000)) { $Context.lost = $true; throw 'Original operation reply unavailable; no replay.' }
    $line = $read.Result
    if ($null -eq $line -or [Text.Encoding]::UTF8.GetByteCount($line) -gt 65536) { $Context.lost = $true; throw 'Original operation connection lost; no replay.' }
    return ($line | ConvertFrom-Json -ErrorAction Stop)
}

function Open-GmParticipatingOperation {
    param([string]$SessionPath)
    $session = [IO.Path]::GetFullPath($SessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::GetFileName($session) -cne 'game_session') { throw 'An explicit game_session root is required.' }
    $root = [IO.Path]::GetDirectoryName($session)
    if (-not [IO.Directory]::Exists($root)) { throw 'Participating root must already exist.' }
    $candidates = @(Join-Path $global:BoeMainOperationCodeRoot 'BookOfEternityClient.dll') + @(@('Debug','Release') | ForEach-Object { Join-Path $global:BoeMainOperationCodeRoot "bin/$_/net8.0/BookOfEternityClient.dll" })
    $assembly = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $assembly) { throw 'Built participating client is unavailable.' }
    $start = [Diagnostics.ProcessStartInfo]::new('dotnet')
    $start.UseShellExecute = $false
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    foreach ($arg in @($assembly,'--gm-main-operation','--root',$root)) { [void]$start.ArgumentList.Add($arg) }
    $process = [Diagnostics.Process]::Start($start)
    $context = [pscustomobject]@{ process=$process; errorRead=$process.StandardError.ReadToEndAsync(); session=$session; sequence=0L; closed=$false; lost=$false; disposed=$false; pendingRead=$null; originalClose=$null; closeObserved=$false; closeOutcome=0; terminalClose=$null; lastCommandReply=$null; publicationUncertain=$false; uncertainCommandReply=$null; localScopeCompleted=$false }
    try {
        $ready = Read-GmOperationReply $context
        if (-not $ready.ok -or $ready.state -cne 'active' -or $ready.sequence -ne 0) { throw 'Participating admission refused.' }
        $context.originalClose=$ready.originalClose
        return $context
    } catch {
        Dispose-GmOperationTransport $context
        $code=$context.exitCode
        $diagnostic=$context.diagnostic
        $kind='Unspecified'
        if ($diagnostic -match '^Original participating operation refused or continuation unconfirmed \(([A-Za-z0-9]+; (original-connection|local-admission|read-record|read-endpoint); (Validate|AcquireAsync|EnsureMainBeforeRecovery|Read|ReadBounded|unspecified))\)\.\s*$') { $kind=$Matches[1] }
        throw "Original participating admission unavailable (helper exit $code, $kind)."
    }
}

function Send-GmOperationCommand {
    param($Context,[hashtable]$Command)
    if ($Context.closed -or $Context.lost) { throw 'Original operation is closed or lost; no reconnect.' }
    if ($Context.publicationUncertain -and $Command.action -cne 'close') { throw 'Original command publication is uncertain; later mutation refused before admission.' }
    $nextSequence = $Context.sequence + 1
    $Command.sequence = $nextSequence
    $frame = $Command | ConvertTo-Json -Depth 8 -Compress
    if ([Text.Encoding]::UTF8.GetByteCount($frame) -gt 4194304) { throw 'Participating control frame exceeds its bound.' }
    try {
        $Context.sequence = $nextSequence
        $Context.process.StandardInput.WriteLine($frame); $Context.process.StandardInput.Flush()
        $reply = Read-GmOperationReply $Context
        if ($reply.sequence -ne $Context.sequence) { throw 'Original operation response identity mismatch.' }
    } catch { $Context.lost=$true; throw }
    # A valid negative reply is a command result, not transport loss. Preserve
    # its first actual unknown decision even if a surrounding body catches it.
    $Context.lastCommandReply=$reply
    if($reply.publicationDisposition -ceq 'Uncertain') {
        $Context.publicationUncertain=$true
        if(-not $Context.uncertainCommandReply){$Context.uncertainCommandReply=$reply}
    }
    if($Command.action -ceq 'close') {
        # Only the original C# closing boundary supplies this attempted close.
        # The active originalClose remains the retained grant used by Stop.
        if($reply.completionKind -ceq 'local') {
            if($Context.originalClose -or $reply.terminalClose -or $reply.closeObserved -ne $false -or
                $reply.localScopeCompleted -ne $true -or $reply.localScopeCompleted -isnot [bool] -or
                $null -eq $reply.PSObject.Properties['effectiveOutcome'] -or [int]$reply.effectiveOutcome -notin 0,1,2,3,4,5,6) {
                throw 'Local participating scope completion is invalid.'
            }
            $Context.localScopeCompleted=$true
            $Context.closeOutcome=[int]$reply.effectiveOutcome
        } elseif($reply.completionKind -ceq 'original-main' -and $reply.terminalClose) {
            $close=$reply.terminalClose
            if($close.pinId -cne $Context.originalClose.pinId -or $close.closeId -cne $Context.originalClose.closeId -or
                $close.operationId -cne $Context.originalClose.operationId -or
                $null -eq $close.PSObject.Properties['closingFailed'] -or $close.closingFailed -isnot [bool] -or
                $null -eq $close.PSObject.Properties['outcome'] -or [int]$close.outcome -notin 0,1,2,3,4,5,6 -or
                $reply.closeObserved -isnot [bool]) { throw 'Original terminal close projection is invalid.' }
            foreach($name in @('rootKey','runId','epoch','hostInstanceId','backend','bootId','generationId')) {
                if($close.identity.$name -cne $Context.originalClose.identity.$name){throw 'Original terminal close identity changed.'}
            }
            $Context.terminalClose=$close
            $Context.closeOutcome=[int]$close.outcome
            $Context.closeObserved=$reply.closeObserved
        }
    }
    if (-not $reply.ok) {
        if($reply.daemonReadRefused -eq $true){throw (New-GmDaemonReadFailure 'Original admitted daemon read refused.' $null)}
        throw 'Participating canonical mutation or original close refused.'
    }
    return $reply
}

function Close-GmParticipatingOperation {
    param($Context,[int]$Outcome=0)
    if ($Context.closed -or $Context.lost) { throw 'Original operation cannot be closed with confirmed receipt.' }
    if($Context.publicationUncertain){$Outcome=5}
    $Context.closeOutcome=$Outcome
    try {
        $reply = Send-GmOperationCommand $Context @{action='close';outcome=$Outcome}
        if ($reply.state -cne 'closed-observed' -or -not ($Context.localScopeCompleted -or ($Context.closeObserved -and $Context.terminalClose))) { throw 'Original terminal close or local completion is unconfirmed.' }
        if ($reply.continuationFailed) { throw 'Original operation finalization failed after its established result.' }
    } finally { $Context.closed=$true; $Context.process.StandardInput.Dispose() }
}

function Dispose-GmOperationTransport {
    param($Context)
    if($Context.disposed){return}
    $Context.disposed=$true
    try {$Context.process.StandardInput.Dispose()}catch{}
    if (-not $Context.process.WaitForExit(4000)) { $Context.process.Kill(); $Context.process.WaitForExit() }
    # Join actual stdout/stderr tasks before disposing the owned helper process.
    if($Context.pendingRead){try {[void]$Context.pendingRead.GetAwaiter().GetResult()}catch{}}
    $Context | Add-Member -NotePropertyName exitCode -NotePropertyValue $Context.process.ExitCode -Force
    $Context | Add-Member -NotePropertyName diagnostic -NotePropertyValue $Context.errorRead.GetAwaiter().GetResult() -Force
    $Context.process.Dispose()
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
        } catch {
            if (-not $failure) {
                $message=if($context.closeObserved){'Original operation established a result, but finalization requires follow-up; no replay.'}else{'Original operation established a result but continuation is unconfirmed; no replay.'}
                $continuation=[IO.IOException]::new($message,$_.Exception)
                $continuation.Data['EstablishedOperationResult']=$result
                $continuation.Data['EstablishedOperationOutcome']=$context.closeOutcome
                $continuation.Data['OriginalOperationClose']=$context.terminalClose
                $continuation.Data['OriginalOperationCloseObserved']=$context.closeObserved
                $failure=[Management.Automation.ErrorRecord]::new($continuation,'OriginalOperationContinuationUnconfirmed',[Management.Automation.ErrorCategory]::OperationStopped,$null)
            } else {$failure.Exception.Data['MainOperationCloseFailure']=$_.Exception}
        }
        finally {
            Dispose-GmOperationTransport $context
            $global:BoeMainOperationContext = $null
        }
    }
    if($context.publicationUncertain) {
        if(-not $failure) {
            $unknown=[IO.IOException]::new('Original command publication is uncertain; operation requires follow-up and must not be replayed.')
            $unknown.Data['EstablishedOperationResult']=$result
            $failure=[Management.Automation.ErrorRecord]::new($unknown,'OriginalPublicationUncertain',[Management.Automation.ErrorCategory]::OperationStopped,$null)
        }
        # Preserve an existing body cause rather than replacing it; uncertainty is
        # absorbing but neither a rollback claim nor lost-close transport state.
        $failure.Exception.Data['EstablishedOperationOutcome']=5
        $failure.Exception.Data['OriginalPublicationDecision']=$context.uncertainCommandReply
        $failure.Exception.Data['OriginalOperationClose']=$context.terminalClose
        $failure.Exception.Data['OriginalOperationCloseObserved']=$context.closeObserved
    }
    if(-not $failure -and -not $context.closeObserved -and -not $context.localScopeCompleted) {
        $continuation=[IO.IOException]::new('Original operation established a result but continuation is unconfirmed; no replay.')
        $continuation.Data['EstablishedOperationResult']=$result
        $continuation.Data['EstablishedOperationOutcome']=$context.closeOutcome
        $continuation.Data['OriginalOperationClose']=$context.terminalClose
        $continuation.Data['OriginalOperationCloseObserved']=$context.closeObserved
        $failure=[Management.Automation.ErrorRecord]::new($continuation,'OriginalOperationContinuationUnconfirmed',[Management.Automation.ErrorCategory]::OperationStopped,$null)
    }
    if($failure -and (Test-GmDaemonReadFailure $failure.Exception)) {
        # Preserve the first body cause and the actual immutable closing receipt.
        # An earlier own publication Unknown still overrides the effective body.
        $failure.Exception.Data['EstablishedOperationOutcome']=$context.closeOutcome
        $failure.Exception.Data['OriginalOperationClose']=$context.terminalClose
        $failure.Exception.Data['OriginalOperationCloseObserved']=$context.closeObserved
    }
    if ($failure) { throw $failure }
    return $result
}

function Close-GmParticipatingBeforeStop {
    # Closing is absorbing until the enclosing consumer unwinds. Shutdown must
    # never wait on a pin still held by its own caller.
    if ($global:BoeMainOperationContext) {
        $context=$global:BoeMainOperationContext
        try {
            if(-not $context.closed -and -not $context.lost){Close-GmParticipatingOperation $context -Outcome 1}
        } catch {$script:GmMainClosingDiagnostic=$_.Exception.GetType().Name}
        finally {
            $context.closed=$true
            if(-not $context.closeObserved -and -not $context.localScopeCompleted){$context.lost=$true}
            Dispose-GmOperationTransport $context
        }
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

# Daemon observations share the existing original connection, not helper-role
# authority. Every completed request acquires/releases its own canonical lease.
function New-GmDaemonReadFailure {
    param([string]$Message,[Exception]$Cause)
    $failure=[IO.IOException]::new($Message,$Cause)
    $failure.Data['GmDaemonReadRefused']=$true
    return $failure
}
function Test-GmDaemonReadFailure {
    param($Failure)
    $current=if($Failure -is [Management.Automation.ErrorRecord]){$Failure.Exception}else{$Failure}
    while($current){if($current.Data -and $current.Data.Contains('GmDaemonReadRefused')){return $true};$current=$current.InnerException}
    return $false
}
function Assert-GmNotDaemonReadFailure {
    param($Failure)
    if(Test-GmDaemonReadFailure $Failure){throw $Failure}
}
function Get-GmDaemonBytesHash {
    param([AllowNull()][byte[]]$Bytes)
    if($null -eq $Bytes){return $null}
    $sha=[Security.Cryptography.SHA256]::Create()
    try{return ([BitConverter]::ToString($sha.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant()}
    finally{$sha.Dispose()}
}
function New-GmDaemonPathMap {
    $comparer=if([IO.Path]::DirectorySeparatorChar -eq '\'){[StringComparer]::OrdinalIgnoreCase}else{[StringComparer]::Ordinal}
    return [Collections.Generic.Dictionary[string,object]]::new($comparer)
}
function Invoke-GmDaemonStorageRequest {
    param([hashtable]$Request)
    $context=$global:BoeMainOperationContext
    if(-not $context){throw (New-GmDaemonReadFailure 'An original daemon operation is required.' $null)}
    $bytes=[Text.UTF8Encoding]::new($false,$true).GetBytes((ConvertTo-Json -InputObject $Request -Depth 100 -Compress))
    $transfer=[guid]::NewGuid().ToString('N')
    [void](Send-GmOperationCommand $context @{action='daemon-request-begin';transfer=$transfer;length=[long]$bytes.LongLength;hash=(Get-GmDaemonBytesHash $bytes)})
    for($offset=0L;$offset -lt $bytes.LongLength;$offset+=$count){
        $count=[int][Math]::Min(32768,$bytes.LongLength-$offset)
        [void](Send-GmOperationCommand $context @{action='daemon-request-chunk';transfer=$transfer;offset=$offset;bytes=[Convert]::ToBase64String($bytes,[int]$offset,$count)})
    }
    # Valid negative storage/decision replies stay outside malformed-wire handling.
    $header=Send-GmOperationCommand $context @{action='daemon-request-end';transfer=$transfer}
    $buffer=[IO.MemoryStream]::new()
    try {
        if($header.transfer -cne $transfer -or $header.length -le 0 -or $header.hash -cnotmatch '^[0-9a-f]{64}$'){throw 'Invalid daemon response header.'}
        while($buffer.Length -lt [long]$header.length){
            $reply=Send-GmOperationCommand $context @{action='daemon-response-chunk';transfer=$transfer;offset=$buffer.Length}
            if($reply.transfer -cne $transfer -or $reply.offset -ne $buffer.Length){throw 'Invalid daemon response identity.'}
            $chunk=[Convert]::FromBase64String($reply.bytes)
            if($chunk.Length -lt 1 -or $chunk.Length -gt 32768 -or $buffer.Length+$chunk.Length -gt [long]$header.length){throw 'Invalid daemon response chunk.'}
            $buffer.Write($chunk,0,$chunk.Length)
            if([bool]$reply.complete -ne ($buffer.Length -eq [long]$header.length)){throw 'Invalid daemon response completion.'}
        }
        $result=$buffer.ToArray()
        if((Get-GmDaemonBytesHash $result) -cne $header.hash){throw 'Daemon response hash changed.'}
        $json=[Text.UTF8Encoding]::new($false,$true).GetString($result)
        $payload=ConvertFrom-Json -InputObject $json -ErrorAction Stop
        if($null -eq $payload -or $payload -isnot [pscustomobject] -or $payload.matches -isnot [bool]){throw 'Invalid daemon response object.'}
        if($Request.action -ceq 'snapshot' -and $payload.matches){
            if($payload.generation -cnotmatch '^[0-9a-f]{32}$' -or $payload.files -isnot [array] -or $payload.trees -isnot [array]){throw 'Invalid daemon snapshot shape.'}
            $seen=New-GmDaemonPathMap
            foreach($file in $payload.files){
                if([string]::IsNullOrWhiteSpace([string]$file.path) -or $file.kind -cnotin @('File','Directory','Missing')){throw 'Invalid daemon file observation.'}
                $seen.Add([string]$file.path,$true)
                if($file.kind -ceq 'File'){
                    $content=[Convert]::FromBase64String([string]$file.bytes)
                    if((Get-GmDaemonBytesHash $content) -cne $file.hash -or -not $file.lastWriteTimeUtc){throw 'Invalid daemon file bytes/hash.'}
                    [void][datetime]::Parse([string]$file.lastWriteTimeUtc,[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::RoundtripKind)
                } elseif($null -ne $file.bytes -or $null -ne $file.hash -or $null -ne $file.lastWriteTimeUtc){throw 'Invalid absent/directory observation.'}
            }
        }
        return $payload
    } catch {
        if(Test-GmDaemonReadFailure $_){throw}
        $context.lost=$true
        throw (New-GmDaemonReadFailure 'Original daemon response unavailable; no replay.' $_.Exception)
    } finally {$buffer.Dispose()}
}
function Get-GmDaemonSnapshot {
    param([string[]]$Paths=@(),[string[]]$Trees=@(),$Expected=$null,[string]$Generation=$null)
    if($null -eq $Expected){$Expected=New-GmDaemonPathMap}
    return Invoke-GmParticipatingConsumer $GameSessionPath {Invoke-GmDaemonStorageRequest @{action='snapshot';paths=$Paths;trees=$Trees;expected=$Expected;generation=$Generation}}
}
function Invoke-GmDaemonConditionalMutation {
    param([string]$Path,[AllowNull()][byte[]]$Bytes,$Expected,[string]$Generation,[switch]$Delete)
    $action=if($Delete){'delete-if-current'}else{'write-if-current'}
    return Invoke-GmParticipatingConsumer $GameSessionPath {Invoke-GmDaemonStorageRequest @{action=$action;path=$Path;bytes=$(if($Delete){$null}else{[Convert]::ToBase64String($Bytes)});expected=$Expected;generation=$Generation}}
}
function ConvertTo-GmDaemonRelativePath {
    param([string]$Path)
    $root=[IO.Path]::GetFullPath($GameSessionPath).TrimEnd([IO.Path]::DirectorySeparatorChar)+[IO.Path]::DirectorySeparatorChar
    $absolute=[IO.Path]::GetFullPath($(if([IO.Path]::IsPathRooted($Path)){$Path}else{[IO.Path]::Combine($root,$Path)}))
    $comparison=if([IO.Path]::DirectorySeparatorChar -eq '\'){[StringComparison]::OrdinalIgnoreCase}else{[StringComparison]::Ordinal}
    if(-not $absolute.StartsWith($root,$comparison)){throw (New-GmDaemonReadFailure 'Daemon target is outside the original session.' $null)}
    $relative=$absolute.Substring($root.Length)
    if([IO.Path]::DirectorySeparatorChar -eq '\'){$relative=$relative.Replace('\','/')}
    return $relative
}
function Invoke-GmDaemonSnapshotView {
    param($Snapshot,[scriptblock]$Body)
    if(-not $Snapshot.matches){throw (New-GmDaemonReadFailure 'Daemon snapshot witness changed.' $null)}
    $old=$script:GmDaemonSnapshotView;$map=New-GmDaemonPathMap
    foreach($item in $Snapshot.files){$item|Add-Member -NotePropertyName generation -NotePropertyValue $Snapshot.generation -Force;$map.Add([string]$item.path,$item)}
    try{$script:GmDaemonSnapshotView=$map;return (& $Body)}
    finally{$script:GmDaemonSnapshotView=$old}
}
function Get-GmDaemonFileObservation {
    param([string]$Path)
    $relative=ConvertTo-GmDaemonRelativePath $Path
    if($null -ne $script:GmDaemonSnapshotView){
        if(-not $script:GmDaemonSnapshotView.ContainsKey($relative)){throw (New-GmDaemonReadFailure 'File was not part of the admitted daemon cohort.' $null)}
        return $script:GmDaemonSnapshotView[$relative]
    }
    $snapshot=Get-GmDaemonSnapshot -Paths @($relative)
    if(-not $snapshot.matches -or @($snapshot.files).Count -ne 1){throw (New-GmDaemonReadFailure 'Daemon snapshot is incomplete.' $null)}
    $snapshot.files[0]|Add-Member -NotePropertyName generation -NotePropertyValue $snapshot.generation -Force
    return $snapshot.files[0]
}
function Test-GmDaemonPath {
    param([string]$Path)
    return (Get-GmDaemonFileObservation $Path).kind -cne 'Missing'
}
function Get-GmDaemonFileBytes {
    param([string]$Path)
    $file=Get-GmDaemonFileObservation $Path
    if($file.kind -ceq 'Missing'){return $null}
    if($file.kind -cne 'File'){throw (New-GmDaemonReadFailure 'Daemon file target has the wrong kind.' $null)}
    return ,([Convert]::FromBase64String([string]$file.bytes))
}
function Get-GmDaemonText {
    param([string]$Path)
    $bytes=Get-GmDaemonFileBytes $Path
    if($null -eq $bytes){return $null}
    return [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF)
}
function Get-GmDaemonLines {
    param([string]$Path)
    $text=Get-GmDaemonText $Path
    if($null -eq $text){return}
    $reader=[IO.StringReader]::new($text)
    try{while($null -ne ($line=$reader.ReadLine())){$line}}finally{$reader.Dispose()}
}
function Get-GmDaemonFileInfo {
    param([string]$Path)
    $file=Get-GmDaemonFileObservation $Path
    if($file.kind -ceq 'Missing'){return $null}
    if($file.kind -cne 'File'){throw (New-GmDaemonReadFailure 'Daemon metadata target has the wrong kind.' $null)}
    return [pscustomobject]@{FullName=[IO.Path]::Combine($GameSessionPath,([string]$file.path).Replace('/',[IO.Path]::DirectorySeparatorChar));Length=[Convert]::FromBase64String([string]$file.bytes).LongLength;LastWriteTimeUtc=[datetime]$file.lastWriteTimeUtc;LastWriteTime=([datetime]$file.lastWriteTimeUtc).ToLocalTime();Observation=$file}
}
function Get-GmDaemonTreeFiles {
    param([string]$Path)
    $snapshot=Get-GmDaemonSnapshot -Trees @((ConvertTo-GmDaemonRelativePath $Path))
    if(-not $snapshot.matches){throw (New-GmDaemonReadFailure 'Daemon tree observation changed.' $null)}
    Invoke-GmDaemonSnapshotView $snapshot {foreach($file in $snapshot.files){Get-GmDaemonFileInfo $file.path}}
}

function Remove-GmDaemonObservedFile {
    param($Observation)
    $expected=New-GmDaemonPathMap;$expected.Add([string]$Observation.path,$Observation.hash)
    $result=Invoke-GmDaemonConditionalMutation -Path $Observation.path -Expected $expected -Generation $Observation.generation -Delete
    return [bool]$result.matches
}
