"""One exact owner/resumer request. Never signals the resumer; no arbitrary PID mode."""
from pathlib import Path
import hashlib
import json
import os
import runpy
import select
import signal
import stat
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
REC = HERE.parent
H = lambda b: hashlib.sha256(b).hexdigest()

def request_role(cfg, m):
    assert type(cfg['SyntheticPreApiOracle']) is bool, 'Explicit strict role required'
    own = Path(m['Own'])
    if cfg['SyntheticPreApiOracle']:
        assert cfg['OracleContract'] == 'S01_G01_PREAPI_ZERO_REQUESTS'
        expected = [cfg['Dotnet'], 'exec', '--runtimeconfig', cfg['OracleRuntimeconfig'], cfg['OracleAssembly'], m['Nonce']]
    else:
        expected = [cfg['Dotnet'], 'exec', '--runtimeconfig', cfg['Runtimeconfig'], '--depsfile', cfg['Depsfile'],
                    cfg['SupportAssembly'], 'engine-joint-interface-resume', cfg['IntegrationAssembly'], m['Root'],
                    str(own/'resume.json'), '1', str(own)]
    assert m['TargetCommand'] == expected and m['Cut'] == 1
    return cfg['SyntheticPreApiOracle']

def main():
    started = time.monotonic()
    request_deadline = started + 20
    os.umask(0o077)
    admission = Path(sys.argv[1])
    m = json.loads(admission.read_text())
    cfg_raw = Path(m['SettingsPath']).read_bytes()
    assert H(cfg_raw) == m['SettingsSha256']
    cfg = json.loads(cfg_raw)
    assert cfg['Contract'] == 'EXACT_RECEIPT_FRESH_STICKY_HEALTH_CAPTURE_PHASE_UNKNOWN_ALLOWED'
    assert cfg['RunId'] == m['RunId']
    required={str(REC/'l01-managed-stack-s01/smoke.py'),str(REC/'t148-m01-run-phase-20261018.py'),
              str(REC/'l01-delivery-d01/phase.py'),str(Path(__file__).resolve()),str(HERE/'Collect.ps1')}
    assert required <= {r['path'] for r in cfg['RequestInputs']}
    for row in cfg['RequestInputs']:
        b = Path(row['path']).read_bytes()
        assert len(b) == row['bytes'] and H(b) == row['sha256'], row['path']
    # Imported helper bytes are bound before runpy (no helper main is called).
    lib = runpy.run_path(str(REC/'l01-managed-stack-s01/smoke.py'))
    summary = runpy.run_path(str(REC/'l01-delivery-d01/phase.py'))['emit_summary']
    root = admission.parent
    record = dict(kind='G01_ONE_REQUEST_WATCHDOG', nativeExit=None, observerErrors=[],
        collector=None, diagnosticRequests=0, collectorLaunches=0, apiOutcome='UNREACHED',
        capturePhase='UNKNOWN', suspensionDuration='UNKNOWN', targetResumption='UNKNOWN',
        nativeExitScope='Collector native when known; independent of game/test native outcome.',
        runId=m['RunId'], targetPid=m['TargetPid'], targetBirthTick=m['TargetBirthTick'])
    child = None; out = err = None; collector_fd = None; dumps = {}
    last_sequence = None; last_health_receipt = time.monotonic()
    try:
        own = Path(m['Own']); target = m['TargetPid']
        for p in (own, root):
            s = p.lstat(); assert not p.is_symlink() and s.st_uid == os.geteuid() and stat.S_IMODE(s.st_mode) == 0o700
        assert root == own/'capture' and admission == root/'admission.json'
        assert not (root/'request.once').exists()
        with (root/'request.once').open('x'): pass
        owner = lib['identity'](m['OwnerPid']); me = lib['identity'](os.getpid()); identity = lib['identity'](target)
        assert me['parentPid'] == m['OwnerPid'] == os.getppid()
        assert owner['birthTick'] == m['OwnerBirthTick'] and identity['birthTick'] == m['TargetBirthTick']
        assert identity['parentPid'] == m['OwnerPid'] and owner['uid'] == me['uid'] == identity['uid'] == os.geteuid()
        command = Path(f'/proc/{target}/cmdline').read_bytes().split(b'\0')[:-1]
        assert command == [a.encode() for a in m['TargetCommand']]
        assert str(Path(f'/proc/{target}/exe').resolve(strict=True)) == m['TargetExecutable'] == cfg['Dotnet']
        synthetic = request_role(cfg, m)
        if synthetic:
            assert {cfg['OracleAssembly'], cfg['OracleRuntimeconfig']} <= {r['path'] for r in cfg['RequestInputs']}
        parent_bytes = (own/'parent.json').read_bytes(); parent = json.loads(parent_bytes)
        assert H(parent_bytes) == m['ParentFileSha256']
        assert parent == dict(Nonce=m['Nonce'], ParentPid=m['OwnerPid'], Cut=1, Fault=None, Diagnostic=True)
        cut = json.loads((own/'cut.json').read_text())
        assert cut['Root'] == m['Root'] and cut['Nonce'] == m['Nonce'] and cut['Cut'] == 1 and cut['Pid'] != target
        fixture = Path(m['Root']); assert fixture.is_dir() and fixture.resolve() == fixture and fixture.is_relative_to(own/'tmp')
        socket = own/'tmp'/f"dotnet-diagnostic-{target}-{m['TargetBirthTick']}-socket"
        s = socket.lstat(); assert stat.S_ISSOCK(s.st_mode) and s.st_uid == os.geteuid() and len(str(socket).encode()) <= 107
        assert not Path(m['DumpPath']).exists() and not Path(m['DumpPath']+'.crashreport.json').exists()
        assert m['DumpPath'] == str(root/'game-normal.core') and m['HealthPath'] == str(root/'health.json')
        m.update(WatchdogPid=os.getpid(), WatchdogBirthTick=me['birthTick'], Uid=os.geteuid(), Socket=str(socket),
                 LoadedFiles=cfg['LoadedFiles'], ClientAssembly=cfg['ClientAssembly'], BinaryImages=cfg['BinaryImages'],
                 SyntheticPreApiOracle=synthetic)
        lib['write'](root/'request-manifest.json', m)
        record.update(owner=owner, watchdog=me, targetIdentity=identity, parentFileSha256=m['ParentFileSha256'])
        collector_phase = root/'collector-env'
        collector_phase.mkdir(mode=0o700)
        env = lib['environment'](collector_phase); env['TMPDIR'] = str(own/'tmp')
        env['PATH'] = '/tmp/1536-cloud-tools/pwsh:/tmp/1536-cloud-tools/dotnet:/usr/bin:/bin'

        def health_cancelled():
            nonlocal last_sequence, last_health_receipt
            if (root/'cancel').exists(): return 'PARENT_OBSERVER_ERROR'
            h = json.loads((root/'health.json').read_text())
            assert h['Pid'] == target and h['BirthTick'] == m['TargetBirthTick'] and h['Root'] == m['Root'] and h['Nonce'] == m['Nonce'] and h['Cut'] == 1
            seq = h['Sequence']
            if last_sequence is not None and seq < last_sequence: return 'HEALTH_SEQUENCE_REGRESSION'
            if seq != last_sequence: last_sequence = seq; last_health_receipt = time.monotonic()
            if time.monotonic() - last_health_receipt > 1: return 'NO_FRESH_HEALTH_RECEIPT'
            # Python measures local receipt age only. The PS final guard compares two BCL Stopwatch stamps.
            if h['Cancelled'] or not h['ReceiptAdmitted'] or h['PolicyState'] != 'ADMITTED': return 'PARENT_CANCELLED'
            v = h['Health']
            if any(v[k] for k in ('Uncertain','Eof','RawReadFault','AcceptedSeen','LateReturnSeen','ObserverFailure')): return 'STICKY_CANCELLED'
            if any(v[k] != 0 for k in ('Dropped','Malformed','Oversize','IncompleteTail','IdentityRejected','Duplicates','ElapsedRegressions','TrackingExhausted')): return 'HEALTH_GAP'
            return None

        def observe_children():
            if not Path(f'/proc/{target}').exists(): return
            assert lib['identity'](target) == identity
            for pid in lib['children'](target):
                if pid in dumps: continue
                try:
                    item = lib['identity'](pid)
                    args = Path(f'/proc/{pid}/cmdline').read_bytes().split(b'\0')[:-1]
                    exe = str(Path(f'/proc/{pid}/exe').resolve(strict=True))
                except FileNotFoundError:
                    if not Path(f'/proc/{pid}').exists(): continue
                    raise
                assert item['parentPid'] == target and item['uid'] == os.geteuid() and exe == cfg['Createdump']
                assert str(target).encode() in args and m['DumpPath'].encode() in args
                assert len(dumps) == 0, 'More than one createdump; HOLD without extra signals.'
                dumps[pid] = dict(identity=item, command=[a.decode() for a in args], fd=lib['open_owned'](pid,item))

        cancellation = health_cancelled()
        if cancellation:
            record['cancelledBeforeCollector'] = cancellation
            record['nativeExit'] = 0
        else:
            assert time.monotonic() < request_deadline
            out = (root/'collector.stdout').open('wb'); err = (root/'collector.stderr').open('wb')
            command = [cfg['Pwsh'], '-NoLogo', '-NoProfile', '-NonInteractive', '-File', cfg['Collector'], '-Manifest', str(root/'request-manifest.json')]
            child = subprocess.Popen(command,cwd=root,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err)
            # Account the successful launch before any fallible identity/handle observation.
            record['collectorLaunches'] = 1
            record['collector'] = dict(command=command,pid=child.pid,identity=None,nativeExit=None,joined=False)
            record['diagnosticRequests'] = None; record['apiOutcome'] = 'UNKNOWN_COLLECTOR_OBSERVATION'
            record['collector']['identity'] = lib['identity'](child.pid)
            assert record['collector']['identity']['parentPid'] == os.getpid()
            collector_fd = os.pidfd_open(child.pid)
            while True:
                native = child.poll()
                if native is not None:
                    record['nativeExit'] = record['collector']['nativeExit'] = native
                    record['collector']['joined'] = True
                    break
                observe_children()
                cancellation = health_cancelled()
                if select.select([sys.stdin], [], [], 0)[0]:
                    sys.stdin.readline(); cancellation = 'OWNER_CANCEL_OR_EOF'
                if cancellation or time.monotonic() >= request_deadline:
                    record['stopReason'] = cancellation or 'REQUEST_ENVELOPE20_EXPIRED'
                    break
                time.sleep(min(.02,max(0,request_deadline-time.monotonic())))
            observe_children()
    except BaseException as error:
        record['observerErrors'].append(repr(error))
    finally:
        cleanup_deadline = time.monotonic() + 5
        # Reconcile exact-target createdump children even when collector setup/observation failed.
        if child is not None:
            try:
                assert time.monotonic() < cleanup_deadline, 'Initial cleanup census deadline exhausted; UNKNOWN/HOLD'
                observe_children()
            except BaseException as error: record['observerErrors'].append('Final target-child census: '+repr(error))
        collector_forced = False
        if child is not None:
            try:
                native = child.poll()
                if native is not None:
                    record['nativeExit'] = record['collector']['nativeExit'] = native
                    record['collector']['joined'] = True
                elif collector_fd is None:
                    # This exact direct Popen child is still unreaped, so its PID cannot be reused.
                    # Same own-child fallback as S01; never a target/global PID signal.
                    assert time.monotonic() < cleanup_deadline, 'Direct collector cleanup deadline exhausted; UNKNOWN/HOLD'
                    child.kill(); collector_forced = True
                    record.setdefault('ownedCleanupSignals',[]).append('direct-collector')
            except BaseException as error: record['observerErrors'].append('Direct collector cleanup: '+repr(error))
        # One shared deadline: signal all exact verified handles, then wait within the same remainder.
        owned = []
        if collector_fd is not None: owned.append(('collector', collector_fd))
        for pid,item in dumps.items():
            fd = item['fd']
            if not select.select([fd], [], [], 0)[0]:
                try:
                    assert lib['identity'](pid) == item['identity']
                    assert str(Path(f'/proc/{pid}/exe').resolve(strict=True)) == cfg['Createdump']
                    assert Path(f'/proc/{pid}/cmdline').read_bytes().split(b'\0')[:-1] == [a.encode() for a in item['command']]
                    owned.append(('createdump',fd))
                except BaseException as error: record['observerErrors'].append('Createdump ownership unresolved: '+repr(error))
            else: owned.append(('createdump',fd))
        for name,fd in owned:
            try:
                if not select.select([fd], [], [], 0)[0]:
                    assert time.monotonic() < cleanup_deadline, 'Owned signal deadline exhausted; UNKNOWN/HOLD'
                    signal.pidfd_send_signal(fd,signal.SIGKILL)
                    if name == 'collector': collector_forced = True
                    record.setdefault('ownedCleanupSignals',[]).append(name)
            except BaseException as error: record['observerErrors'].append('Owned signal: '+repr(error))
        for name,fd in owned:
            try:
                if not select.select([fd],[],[],0)[0]:
                    remaining = cleanup_deadline-time.monotonic()
                    assert remaining > 0, name+' cleanup deadline exhausted; UNKNOWN/HOLD'
                    assert select.select([fd],[],[],remaining)[0], name+' terminal UNKNOWN'
            except BaseException as error: record['observerErrors'].append('Owned terminal: '+repr(error))
        if child is not None:
            try:
                code=child.poll()
                if code is None:
                    remaining = cleanup_deadline-time.monotonic()
                    assert remaining > 0, 'Collector join deadline exhausted; UNKNOWN/HOLD'
                    code=child.wait(timeout=remaining)
                if record['collector'] is not None:
                    record['collector']['joined']=True
                    if record['collector']['nativeExit'] is None:
                        if not collector_forced or code != -signal.SIGKILL:
                            record['nativeExit'] = record['collector']['nativeExit'] = code
                        else: record['collector']['cleanupExit']=code
            except BaseException as error: record['observerErrors'].append('Collector join: '+repr(error))
            if record['collector']['joined']:
                try:
                    assert time.monotonic() < cleanup_deadline, 'Post-collector census deadline exhausted; UNKNOWN/HOLD'
                    observe_children() # Revalidates target identity; catches a late child after collector join.
                    record['postCollectorCensusMonotonic'] = time.monotonic()
                except BaseException as error: record['observerErrors'].append('Post-collector target-child census: '+repr(error))
                for pid,item in dumps.items():
                    fd = item['fd']
                    if any(fd == known for _,known in owned): continue
                    try:
                        if not select.select([fd], [], [], 0)[0]:
                            assert lib['identity'](pid) == item['identity']
                            assert str(Path(f'/proc/{pid}/exe').resolve(strict=True)) == cfg['Createdump']
                            assert Path(f'/proc/{pid}/cmdline').read_bytes().split(b'\0')[:-1] == [a.encode() for a in item['command']]
                            assert time.monotonic() < cleanup_deadline, 'Late createdump signal deadline exhausted; UNKNOWN/HOLD'
                            signal.pidfd_send_signal(fd,signal.SIGKILL)
                            record.setdefault('ownedCleanupSignals',[]).append('late-createdump')
                        owned.append(('createdump',fd))
                        if not select.select([fd],[],[],0)[0]:
                            remaining = cleanup_deadline-time.monotonic()
                            assert remaining > 0, 'Late createdump wait deadline exhausted; UNKNOWN/HOLD'
                            assert select.select([fd],[],[],remaining)[0], 'late createdump terminal UNKNOWN'
                    except BaseException as error: record['observerErrors'].append('Late createdump ownership/cleanup unresolved: '+repr(error))
            else: record['postCollectorCensus'] = 'UNKNOWN_COLLECTOR_NOT_JOINED'
        record['createdumpCensusLimit'] = 'Dated exact-target observations only; no proof against future target-service spawn.'
        for stream in (out,err):
            if stream is not None: lib['close_observed'](record,'Collector output close',stream.close)
        if child is not None: lib['observe_collector'](record,lambda:(root/'collector.stdout').read_text())
        record['createdumpIdentities']=[{k:v for k,v in item.items() if k!='fd'} for item in dumps.values()]
        for _,fd in owned: lib['close_observed'](record,'Owned pidfd close',lambda fd=fd:os.close(fd))
        for item in dumps.values():
            if not any(fd==item['fd'] for _,fd in owned): lib['close_observed'](record,'Unresolved pidfd close',lambda fd=item['fd']:os.close(fd))
        record['durationSeconds']=time.monotonic()-started
    record['status']='OBSERVATION_COMPLETE' if record['nativeExit']==0 and not record['observerErrors'] else 'FROZEN_FIRST_OBSERVER_OUTCOME'
    return lib['FINALIZE'](record,lambda r:lib['write'](root/'request-record.json',r),summary)

if __name__=='__main__':sys.exit(main())
