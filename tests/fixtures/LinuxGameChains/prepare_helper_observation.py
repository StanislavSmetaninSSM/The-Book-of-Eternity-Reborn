"""Exact fixture-only observer insertions into two owned shipped scripts.

Original reads, writes, guards, exceptions, joins and timers remain literal.
The substituted source is retained outside ship so the raw archive includes it.
"""
import hashlib
import json
from pathlib import Path


def prepare(repo, out, ship):
    folder = out / "helper-admission-observation"
    folder.mkdir()
    retained = out / "helper-observer-source"
    retained.mkdir()
    launcher = ship / "BookOfEternityClient/Launcher"
    observer = repo / "tests/fixtures/LinuxGameChains/helper_admission_observer.ps1"
    copied = launcher / observer.name
    copied.write_bytes(observer.read_bytes())
    (retained / observer.name).write_bytes(copied.read_bytes())
    substitutions = {
        "gm_turn_helper_transport.ps1": [
            ("    return [Diagnostics.Process]::Start($start)",
             "    Configure-BoeAdmissionFailureCapture $start\n    return [Diagnostics.Process]::Start($start)"),
            ("$script:BoeHelperCodeRoot =", ". (Join-Path $PSScriptRoot 'helper_admission_observer.ps1')\n$script:BoeHelperCodeRoot ="),
            ("        $Context.pendingRead=$Context.process.StandardOutput.ReadLineAsync()",
             "        Write-BoeAdmissionObservation $Context 'read-begin' @{}\n        $Context.pendingRead=$Context.process.StandardOutput.ReadLineAsync()"),
            ("        $line=$Context.pendingRead.GetAwaiter().GetResult()",
             "        $line=$Context.pendingRead.GetAwaiter().GetResult()\n        Observe-BoeAdmissionRead $Context $line"),
            ("    $process=Start-BoeHelperProcess ([IO.Path]::GetDirectoryName($session)) $ExpectedGeneration",
             "    Begin-BoeAdmissionObservation $Mode $session $ExpectedGeneration\n    $process=Start-BoeHelperProcess ([IO.Path]::GetDirectoryName($session)) $ExpectedGeneration"),
            ("    try {\n        $process.StandardInput.WriteLine((@{sequence=0L;action='open';mode=$Mode}|ConvertTo-Json -Compress));$process.StandardInput.Flush()",
             "    Attach-BoeAdmissionObservation $context\n    try {\n        Write-BoeAdmissionObservation $context 'write-begin' @{}\n        $process.StandardInput.WriteLine((@{sequence=0L;action='open';mode=$Mode}|ConvertTo-Json -Compress));$process.StandardInput.Flush()\n        Write-BoeAdmissionObservation $context 'write-return' @{}"),
            ("        if(-not $reply.ok -or $reply.state -cne 'active' -or $reply.sequence -ne 0 -or $reply.generation -cnotmatch '^[0-9a-f]{32}$' -or ($ExpectedGeneration -and $reply.generation -cne $ExpectedGeneration))",
             "        Observe-BoeAdmissionIdentity $context $reply $ExpectedGeneration\n        if(-not $reply.ok -or $reply.state -cne 'active' -or $reply.sequence -ne 0 -or $reply.generation -cnotmatch '^[0-9a-f]{32}$' -or ($ExpectedGeneration -and $reply.generation -cne $ExpectedGeneration))"),
            ("        $context.originalClose=$reply.originalClose;$context.generation=$reply.generation\n        return $context",
             "        $context.originalClose=$reply.originalClose;$context.generation=$reply.generation\n        Write-BoeAdmissionObservation $context 'admission-return' @{ Ok=$reply.ok; State=$reply.state; Sequence=$reply.sequence; Generation=$reply.generation }\n        return $context"),
        ],
        "gm_main_operation.ps1": [
            ("    if($Context.disposed){return}\n    $Context.disposed=$true",
             "    if($Context.disposed){return}\n    if($Context.PSObject.Properties['BoeAdmissionObservation']) { Observe-BoeAdmissionExit $Context 'dispose-begin' }\n    $Context.disposed=$true"),
            ("    if (-not $Context.process.WaitForExit(4000)) { $Context.process.Kill(); $Context.process.WaitForExit() }",
             "    if (-not $Context.process.WaitForExit(4000)) { if($Context.PSObject.Properties['BoeAdmissionObservation']) { Write-BoeAdmissionObservation $Context 'dispose-kill-intent' @{} }; $Context.process.Kill(); $Context.process.WaitForExit() }\n    if($Context.PSObject.Properties['BoeAdmissionObservation']) { Observe-BoeAdmissionExit $Context 'dispose-exit-observed' }"),
            ("    $Context | Add-Member -NotePropertyName diagnostic -NotePropertyValue $Context.errorRead.GetAwaiter().GetResult() -Force\n    $Context.process.Dispose()",
             "    $Context | Add-Member -NotePropertyName diagnostic -NotePropertyValue $Context.errorRead.GetAwaiter().GetResult() -Force\n    if($Context.PSObject.Properties['BoeAdmissionObservation']) { Observe-BoeAdmissionJoined $Context }\n    $Context.process.Dispose()\n    if($Context.PSObject.Properties['BoeAdmissionObservation']) { Complete-BoeAdmissionObservation $Context }"),
        ],
    }
    sources = []
    for name, replacements in substitutions.items():
        target = launcher / name
        original = target.read_bytes()
        source = repo / "BookOfEternityClient/Launcher" / name
        assert original == source.read_bytes(), "Unmatched original shipped helper source: " + name
        text = original.decode("utf-8")
        for before, after in replacements:
            assert text.count(before) == 1, "Observer anchor must occur exactly once: " + name
            text = text.replace(before, after, 1)
        instrumented = text.encode("utf-8")
        target.write_bytes(instrumented)
        (retained / name).write_bytes(instrumented)
        sources.append({"Path": str(source.relative_to(repo)), "OriginalBytes": len(original),
                        "OriginalSHA256": hashlib.sha256(original).hexdigest(), "InstrumentedBytes": len(instrumented),
                        "InstrumentedSHA256": hashlib.sha256(instrumented).hexdigest(),
                        "ExactSubstitutions": [{"Before": a, "After": b} for a, b in replacements]})
    provenance = {"Scope": "Fixture-only actual relay consumer Complete-BoeTurn write-scope seq0 observation; production source unchanged",
                  "ObserverSHA256": hashlib.sha256(observer.read_bytes()).hexdigest(), "Sources": sources,
                  "Caps": {"EventRows": 32, "EventBytes": 1048576, "FullFrameUtf8Bytes": 65536,
                           "OversizePrefixCharacters": 4096, "StderrStoredUtf8BytesMaximum": 262144},
                  "Limit": "Passive file writes can perturb timing. NULL and byte count are copied only after the original read returns; joined stderr/exit only after original joins. Observed exit times are bounds, not exact OS exit timestamps."}
    (out / "helper-observer-provenance.json").write_text(json.dumps(provenance, indent=2) + "\n")
    return {"BOE_TEST_HELPER_ADMISSION_OBSERVER_DIR": str(folder),
            "BOE_TEST_HELPER_TARGET_CONSUMER_SCRIPT": str(repo / "tools/gm-relay/relay-apply-response.ps1")}


def collect(out, consumer_pid):
    folder = out / "helper-admission-observation"
    stem = folder / ("consumer-" + str(consumer_pid))
    problems = []
    events, summary = [], {}
    try:
        events = [json.loads(line) for line in stem.with_suffix(".jsonl").read_text().splitlines()]
    except Exception as error:
        problems.append("Actual event capture unavailable: " + repr(error))
    try:
        summary = json.loads(Path(str(stem) + "-summary.json").read_text())
    except Exception as error:
        problems.append("Actual joined/disposal summary unavailable: " + repr(error))
    reads = [row for row in events if row["Phase"] == "read-return"]
    if len(reads) != 1:
        problems.append("Expected exactly one actual Complete-BoeTurn write-scope seq0 completed read")
    if not events or not all(row["ConsumerPid"] == consumer_pid and row["ScopeId"] == summary.get("ScopeId") for row in events):
        problems.append("Actual consumer/scope correlation incomplete")
    if not summary.get("HelperPid") or summary.get("HelperPid") == consumer_pid or not summary.get("LinuxStarttime") or not summary.get("BootId"):
        problems.append("Actual distinct dedicated managed child Linux identity incomplete")
    if not summary.get("DisposeReturned") or summary.get("CaptureIncomplete", True):
        problems.append("Original disposal did not return or observer capped/truncated/failed")
    if not summary.get("NonceConfigured") or not summary.get("FailureNonce"):
        problems.append("Selected actual child failure-capture nonce was not configured")
    managed_failures = []
    stderr_path = Path(str(stem) + "-stderr.txt")
    try:
        for line in stderr_path.read_text().splitlines():
            if 'boe-helper-fixture-failure' not in line:
                continue
            captured = json.loads(line)
            if captured.get("nonce") != summary.get("FailureNonce") or captured.get("pid") != summary.get("HelperPid"):
                problems.append("Managed exception nonce/PID does not match selected actual child")
            if captured.get("captureIncomplete", True):
                problems.append("Managed exception capture capped/truncated/incomplete")
            managed_failures.append(captured)
        if len(managed_failures) > 2:
            problems.append("Unexpected more than two original managed catch records")
        if summary.get("ExitCode") == 2 and not managed_failures:
            problems.append("Actual managed refusal exit2 without required caught-exception record: capture unavailable, not no exception")
    except Exception as error:
        problems.append("Managed caught-exception parse unavailable: " + repr(error))
    if not any(row["Phase"] == "dispose-joined" and row["Data"]["StderrReadCompleted"] and
               row["Data"]["PendingReadCompleted"] for row in events):
        problems.append("Already-owned stdout/stderr joins not observed complete")
    launches = [row for row in events if row["Phase"] == "launch-begin"]
    identities = [row for row in events if row["Phase"] == "identity-guard-observed"]
    if len(launches) != 1 or "ExpectedGeneration" not in launches[0]["Data"]:
        problems.append("Actual write-scope ExpectedGeneration unavailable")
    if len(identities) > 1:
        problems.append("Ambiguous original write-scope identity observation")
    # NULL/oversize/parse failure/cancellation can exit before the identity guard.
    # Retained actual frame explains whether a missing guard row is expected.
    parsed_reply = None
    if len(reads) == 1 and reads[0]["Data"]["FrameOutcome"] == "within-frame-bound":
        try:
            parsed_reply = json.loads(Path(str(stem) + "-seq0.txt").read_text())
        except Exception:
            pass
        if isinstance(parsed_reply, dict) and not (parsed_reply.get("state") == "admission-cancelled" and parsed_reply.get("effectiveOutcome") == 2) and len(identities) != 1:
            problems.append("Original parseable write reply retained but guard predicate row missing; this does not prove guard reached")
    return {"Summary": summary, "Events": events, "ObservationComplete": not problems,
            "ManagedCaughtExceptions": managed_failures,
            "ManagedExceptionLimit": "No caught record on a healthy admission is expected; absence on a refusal is a causal gap, not proof that no exception occurred. Only original caught/InnerException and three known lifecycle Data links are retained within explicit caps.",
            "MissingObservations": problems, "IdentityGuardObservation": identities[0]["Data"] if len(identities) == 1 else None,
            "ActualExpectedGeneration": launches[0]["Data"].get("ExpectedGeneration") if len(launches) == 1 else None,
            "ReadOutcome": reads[0]["Data"] if len(reads) == 1 else None, "ConsumerPid": consumer_pid,
            "Limit": "Dedicated managed exit code is separate from relay consumer exit. Dispose kill intent, when present, is cleanup; observations do not assign it as the preceding read cause."}
