param([Parameter(Mandatory)][string]$Assembly,[Parameter(Mandatory)][string]$Root,[string]$StatusPath='')
$ErrorActionPreference='Stop';$guard=$null
try {
 Add-Type -Path $Assembly
 $status=if($StatusPath){[IO.File]::ReadAllText($StatusPath)}else{$null}
 $guard=[BookOfEternityClient.Services.GmRuntime.LinuxDaemonInstanceGuard]::Acquire($Root,$status)
 @{owner=$guard.OwnerId;session=$guard.SessionPath}|ConvertTo-Json -Compress|Write-Output
 [Console]::Out.Flush()
 [void][Console]::ReadLine()
 $guard.Validate()
} catch { [Console]::Error.WriteLine($_.Exception.Message);exit 2 }
finally {if($guard){$guard.Dispose()}}
