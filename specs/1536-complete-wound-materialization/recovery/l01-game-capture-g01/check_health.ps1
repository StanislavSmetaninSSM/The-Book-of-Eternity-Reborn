param([Parameter(Mandatory)][string]$Collector, [Parameter(Mandatory)][string]$ProofRoot)
$ErrorActionPreference = 'Stop'
$tokens = $null; $errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($Collector,[ref]$tokens,[ref]$errors)
if ($errors.Count -ne 0) { throw 'Collector syntax failed' }
$f = $ast.FindAll({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Test-AdmissionHealth' },$true)
if ($f.Count -ne 1) { throw 'Health guard missing/ambiguous' }
Invoke-Expression $f[0].Extent.Text # Only the pure guard, never collector main/API.
$reader = $ast.FindAll({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq 'Read-ParentCancellation' },$true)
if ($reader.Count -ne 1) { throw 'Cancellation reader missing/ambiguous' }
Invoke-Expression $reader[0].Extent.Text # Only private marker I/O, never collector main/API.
$now = [Diagnostics.Stopwatch]::GetTimestamp(); $freq = [Diagnostics.Stopwatch]::Frequency
$m = [pscustomobject]@{ TargetPid=123; TargetBirthTick=456; Root='/tmp/fixture'; Nonce=('a'*32); DeadlineStamp=($now+220*$freq); HealthFloor=1 }
$h = [pscustomobject]@{ Pid=123; BirthTick=456; Root='/tmp/fixture'; Nonce=('a'*32); Cut=1; Sequence=1; Stamp=$now; Frequency=$freq;
    DeadlineStamp=$m.DeadlineStamp; Cancelled=$false; ReceiptAdmitted=$true; PolicyState='ADMITTED';
    Health=[pscustomobject]@{ Uncertain=$false; Eof=$false; RawReadFault=$false; AcceptedSeen=$false; LateReturnSeen=$false;
        Dropped=0; Malformed=0; Oversize=0; IncompleteTail=0; IdentityRejected=0; Duplicates=0; ElapsedRegressions=0; TrackingExhausted=0;
        ObserverFailure=$null; CustodySeen=$true; CapturePhase='UNKNOWN' } }
$rows = [Collections.Generic.List[object]]::new()
if ($null -ne (Test-AdmissionHealth $h $m $now $freq)) { throw 'Healthy exact receipt with CapturePhase UNKNOWN rejected' }
$rows.Add(@{Name='exact receipt UNKNOWN phase allowed';Passed=$true})
if ((Test-AdmissionHealth $h $m $now $freq $true) -cne 'PARENT_OBSERVER_ERROR') { throw 'Published parent cancellation ignored' }
$rows.Add(@{Name='published parent cancel refuses fresh ADMITTED image';Passed=$true})
foreach ($name in @('Uncertain','Eof','RawReadFault','AcceptedSeen','LateReturnSeen')) {
    $c=$h|ConvertTo-Json -Depth 10|ConvertFrom-Json; $c.Health.$name=$true
    if ($null -eq (Test-AdmissionHealth $c $m $now $freq)) { throw "Sticky cancellation missing:$name" }
    $rows.Add(@{Name=$name;Passed=$true})
}
foreach ($name in @('Dropped','Malformed','Oversize','IncompleteTail','IdentityRejected','Duplicates','ElapsedRegressions','TrackingExhausted')) {
    $c=$h|ConvertTo-Json -Depth 10|ConvertFrom-Json; $c.Health.$name=1
    if ($null -eq (Test-AdmissionHealth $c $m $now $freq)) { throw "Gap cancellation missing:$name" }
    $rows.Add(@{Name=$name;Passed=$true})
}
foreach ($kind in @('future','stale','deadline','identity','sequence','missing','cancelled','not-admitted')) {
    $c=$h|ConvertTo-Json -Depth 10|ConvertFrom-Json
    switch ($kind) {
        future {$c.Stamp=$now+1}; stale {$c.Stamp=$now-2*$freq}; deadline {$c.DeadlineStamp=$now+24*$freq}
        identity {$c.Nonce='wrong'}; sequence {$c.Sequence=0}; missing {$c.PSObject.Properties.Remove('ReceiptAdmitted')}
        cancelled {$c.Cancelled=$true}; not-admitted {$c.ReceiptAdmitted=$false}
    }
    if ($null -eq (Test-AdmissionHealth $c $m $now $freq)) { throw "Guard refusal missing:$kind" }
    $rows.Add(@{Name=$kind;Passed=$true})
}
if (Test-Path -LiteralPath $ProofRoot) { throw 'Proof root already exists' }
[void][IO.Directory]::CreateDirectory($ProofRoot)
$marker = Join-Path $ProofRoot 'cancel'
if (Read-ParentCancellation $marker) { throw 'Missing marker reported present' }
$rows.Add(@{Name='only missing marker means absent';Passed=$true})
[IO.File]::WriteAllText($marker,'')
if (-not (Read-ParentCancellation $marker)) { throw 'Existing marker reported absent' }
$rows.Add(@{Name='private existing marker means cancelled';Passed=$true})
$unreadable = Join-Path $ProofRoot 'directory-cannot-be-marker'
[void][IO.Directory]::CreateDirectory($unreadable)
$refused = $false
try { [void](Read-ParentCancellation $unreadable) } catch { $refused = $true }
if (-not $refused) { throw 'Cancellation read uncertainty collapsed to absent' }
$rows.Add(@{Name='private marker I/O uncertainty refuses';Passed=$true})
@{Kind='G01_PURE_COLLECTOR_HEALTH';Passed=$rows.Count;Cases=$rows;Requests=0;GameProcesses=0;
    ClockStamp=[Diagnostics.Stopwatch]::GetTimestamp();ClockFrequency=$freq}|ConvertTo-Json -Depth 10 -Compress
