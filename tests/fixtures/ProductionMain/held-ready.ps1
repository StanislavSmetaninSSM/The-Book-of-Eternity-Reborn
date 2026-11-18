param([Parameter(Mandatory=$true)][string]$GameSessionPath,[Parameter(Mandatory=$true)][string]$Helper)
$ErrorActionPreference='Stop'
. $Helper
$script:bodyEntered=$false;$returned=$false;$failure=$null
try {
    Invoke-GmParticipatingConsumer $GameSessionPath {$script:bodyEntered=$true}
    $returned=$true
}catch {$failure=$_}
if($script:bodyEntered -or $returned -or $null -eq $failure){throw 'Timed-out readiness executed a body or returned success.'}
if($failure.Exception.Message -notmatch '^Original participating admission unavailable \(helper exit .*\)\.$'){throw 'Original readiness timeout wrapper was replaced.'}
@{bodyEntered=$script:bodyEntered;returned=$returned;refusal=$failure.Exception.ToString();observedAtUtc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json -Compress
