# One-shot external .NET 8 boundary. Desktop must never Add-Type the net8 client.
function ConvertTo-BoeBridgeProcessArgument {
    param([string]$Value)
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
function Invoke-BoeBridgeRequestProcess {
    param([string]$CodeRoot,[string]$StatusJson,[string]$RequestJson,[int]$TimeoutMilliseconds=5000)
    if($TimeoutMilliseconds -le 0){throw 'A positive bridge deadline is required.'}
    $clock=[Diagnostics.Stopwatch]::StartNew()
    $candidates=@(Join-Path $CodeRoot 'BookOfEternityClient.dll')+@(@('Debug','Release')|ForEach-Object{Join-Path $CodeRoot "bin/$_/net8.0/BookOfEternityClient.dll"})
    $assembly=$candidates|Where-Object{[IO.File]::Exists($_)}|Select-Object -First 1
    if(!$assembly){throw 'Trusted bridge client assembly is missing.'}
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName='dotnet';$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.StandardOutputEncoding=[Text.UTF8Encoding]::new($false,$true)
    $start.StandardErrorEncoding=[Text.UTF8Encoding]::new($false,$true)
    $start.Arguments=(ConvertTo-BoeBridgeProcessArgument $assembly)+' --gm-bridge-request'
    $process=[Diagnostics.Process]::new();$process.StartInfo=$start
    $started=$false;$stdout=$null;$stderr=$null;$write=$null;$failure=$null;$result=$null
    $id=[Guid]::NewGuid().ToString('N')
    try {
        if(! $process.Start()){throw 'Bridge helper did not start.'};$started=$true
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        $remaining=$TimeoutMilliseconds-[int]$clock.ElapsedMilliseconds
        if($remaining -le 0){throw 'Bridge request deadline elapsed during startup; outcome unknown.'}
        $frame=@{requestId=$id;statusJson=$StatusJson;requestJson=$RequestJson;timeoutMilliseconds=$remaining}|ConvertTo-Json -Depth 8 -Compress
        $bytes=[Text.UTF8Encoding]::new($false).GetBytes($frame+"`n")
        $write=$process.StandardInput.BaseStream.WriteAsync($bytes,0,$bytes.Length)
        $remaining=$TimeoutMilliseconds-[int]$clock.ElapsedMilliseconds
        if($remaining -le 0 -or !$write.Wait($remaining)){throw 'Bridge request write deadline elapsed; outcome unknown.'}
        $write.GetAwaiter().GetResult();$process.StandardInput.Close()
        $remaining=$TimeoutMilliseconds-[int]$clock.ElapsedMilliseconds
        if($remaining -le 0 -or !$process.WaitForExit($remaining)){throw 'Bridge request deadline elapsed; outcome unknown.'}
        $remaining=$TimeoutMilliseconds-[int]$clock.ElapsedMilliseconds
        if($remaining -le 0 -or ![Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout,$stderr),$remaining)){throw 'Bridge reply EOF deadline elapsed; outcome unknown.'}
        $text=$stdout.GetAwaiter().GetResult();$errors=$stderr.GetAwaiter().GetResult()
        if(![string]::IsNullOrWhiteSpace($errors)){throw 'Bridge helper emitted unexpected stderr; outcome unknown.'}
        # ConvertFrom-Json must consume one complete document; helper emits only one frame.
        $reply=$text|ConvertFrom-Json -ErrorAction Stop
        if($reply.requestId -cne $id -or $reply.ok -isnot [bool]){throw 'Bridge helper correlation is invalid; outcome unknown.'}
        if($process.ExitCode -ne 0 -or !$reply.ok){throw 'Bridge request failed or its outcome is unknown; no replay was attempted.'}
        if($reply.response -isnot [string]){throw 'Bridge helper response is missing; outcome unknown.'}
        $result=[pscustomobject]@{ResponseJson=$reply.response;ExitCode=$process.ExitCode;HelperExited=$true;StdoutEof=$true;StderrEof=$true}
    } catch { $failure=$_.Exception }
    finally {
        # Separate cleanup budget, only the exact child Process created above. No retry.
        if($started){
            if(!$process.HasExited){try{$process.Kill()}catch{if(!$failure){$failure=$_.Exception}}}
            $exited=$process.WaitForExit(5000)
            if(!$exited -and !$failure){$failure=[IO.IOException]::new('Bridge helper cleanup did not observe exit.')}
            try{$process.StandardInput.Close()}catch{}
            $tasks=@();if($null -ne $write){$tasks+=,$write};if($null -ne $stdout){$tasks+=,$stdout};if($null -ne $stderr){$tasks+=,$stderr}
            $joined=$false
            try{$joined=[Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]$tasks,5000)}catch{
                # Faulted tasks are terminal and observed; never mark a pending task joined.
                $joined=@($tasks|Where-Object{!$_.IsCompleted}).Count -eq 0
            }
            if(!$joined -and !$failure){$failure=[IO.IOException]::new('Bridge helper cleanup has pending I/O; outcome unknown.')}
            if($failure){
                $failure.Data['HelperExited']=$exited
                $failure.Data['StdoutEof']=($null -ne $stdout -and $stdout.Status -eq [Threading.Tasks.TaskStatus]::RanToCompletion)
                $failure.Data['StderrEof']=($null -ne $stderr -and $stderr.Status -eq [Threading.Tasks.TaskStatus]::RanToCompletion)
            }
        }
        $process.Dispose();$clock.Stop()
    }
    if($failure){throw $failure}
    return $result
}
