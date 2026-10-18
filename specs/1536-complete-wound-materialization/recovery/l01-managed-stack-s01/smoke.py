"""One owned synthetic capability probe. No arbitrary-PID or gameplay entrypoint."""
from pathlib import Path
import copy
import datetime
import hashlib
import json
import os
import queue
import runpy
import select
import signal
import stat
import subprocess
import sys
import threading
import time
import uuid

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
INVENTORY = HERE.parent / 'l01-managed-stack-capability-inventory-20261018.json'
CONTROL = HERE.parent / 'l01-managed-stack-capability-control-20261018.json'
DOTNET = '/tmp/1536-cloud-tools/dotnet/dotnet'
PWSH = '/tmp/1536-cloud-tools/pwsh/pwsh'
CSC = '/tmp/1536-cloud-tools/dotnet/sdk/10.0.402/Roslyn/bincore/csc.dll'
CLIENT = '/tmp/1536-cloud-tools/dotnet/sdk/10.0.402/Extensions/Microsoft.Diagnostics.NETCore.Client.dll'
CREATEDUMP = '/tmp/1536-cloud-tools/dotnet/shared/Microsoft.NETCore.App/8.0.32/createdump'
# Reuse the already reviewed/proved observer finalizer without changing M01 bytes.
FINALIZE = runpy.run_path(str(HERE.parent / 't148-m01-run-phase-20261018.py'))['finalize']


def digest(data):
    return hashlib.sha256(data).hexdigest()


def image(path):
    path = Path(path)
    data = path.read_bytes()
    return dict(path=str(path), bytes=len(data), sha256=digest(data))


def write(path, value):
    Path(path).write_text(json.dumps(value, indent=2) + '\n')


def identity(process_id):
    raw = Path(f'/proc/{process_id}/stat').read_text()
    assert int(raw.split(' ', 1)[0]) == process_id
    fields = raw[raw.rfind(')') + 2:].split()
    uid = next(line.split()[1] for line in Path(f'/proc/{process_id}/status').read_text().splitlines() if line.startswith('Uid:'))
    return dict(pid=process_id, birthTick=int(fields[19]), parentPid=int(fields[1]), uid=int(uid))


def open_owned(process_id, expected):
    assert identity(process_id) == expected
    handle = os.pidfd_open(process_id)
    try:
        assert identity(process_id) == expected
    except BaseException:
        os.close(handle)
        raise
    return handle


def terminate(handle):
    if not select.select([handle], [], [], 0)[0]:
        signal.pidfd_send_signal(handle, signal.SIGKILL)
    assert select.select([handle], [], [], 5)[0], 'Owned process did not terminate.'


def children(process_id):
    result = set()
    for task in Path(f'/proc/{process_id}/task').glob('*'):
        try:
            result.update(int(value) for value in (task / 'children').read_text().split())
        except FileNotFoundError:
            pass
    return sorted(result)


def environment(phase):
    # Explicit allowlist: no inherited credentials, profiler, startup hook or port.
    env = dict(PATH='/tmp/1536-cloud-tools/dotnet:/usr/bin:/bin', DOTNET_ROOT='/tmp/1536-cloud-tools/dotnet',
               DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1', LANG='C.UTF-8',
               TMPDIR=str(phase / 'tmp'), XDG_CACHE_HOME=str(phase / 'cache'), XDG_CONFIG_HOME=str(phase / 'config'),
               XDG_DATA_HOME=str(phase / 'data'), XDG_RUNTIME_DIR=str(phase / 'runtime'), DOTNET_CLI_HOME=str(phase / 'cli'))
    for key in ['TMPDIR', 'XDG_CACHE_HOME', 'XDG_CONFIG_HOME', 'XDG_DATA_HOME', 'XDG_RUNTIME_DIR', 'DOTNET_CLI_HOME']:
        Path(env[key]).mkdir(mode=0o700)
    return env


def run_owned(command, phase, name, env, timeout):
    result = dict(command=command, nativeExit=None, observerErrors=[])
    out = err = child = handle = owned = None
    try:
        out = (phase / (name + '.stdout')).open('wb')
        err = (phase / (name + '.stderr')).open('wb')
        child = subprocess.Popen(command, cwd=phase, env=env, stdin=subprocess.DEVNULL, stdout=out, stderr=err)
        owned = identity(child.pid)
        handle = open_owned(child.pid, owned)
        result['identity'] = owned
        try:
            result['nativeExit'] = child.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            result['deadlineExceeded'] = True
            terminate(handle)
            result['cleanupExit'] = child.wait(timeout=5)
    except BaseException as error:
        result['observerErrors'].append(repr(error))
    finally:
        if child is not None:
            try:
                if child.poll() is None:
                    if handle is not None:
                        terminate(handle)
                    else:
                        assert owned is not None and identity(child.pid) == owned and owned['parentPid'] == os.getpid()
                        child.kill()  # Only the just-spawned live child, if pidfd setup itself failed.
                    result['cleanupExit'] = child.wait(timeout=5)
                result['joined'] = child.returncode is not None
            except BaseException as error:
                result['observerErrors'].append('Owned subprocess cleanup: ' + repr(error))
        if handle is not None: os.close(handle)
        for stream in [out, err]:
            if stream is not None:
                try: stream.close()
                except BaseException as error: result['observerErrors'].append('Output close: ' + repr(error))
    return result


def collector_metadata(manifest, target_assembly, installed):
    names = ['/tmp/1536-cloud-tools/pwsh/Microsoft.Bcl.AsyncInterfaces.dll',
             '/tmp/1536-cloud-tools/dotnet/sdk/10.0.402/Microsoft.Extensions.Logging.Abstractions.dll', CLIENT]
    write(manifest, dict(LoadedFiles=[next(row for row in installed if row['path'] == name) for name in names],
                         ClientAssembly=CLIENT, TargetAssembly=str(target_assembly)))


def prefixed(path, prefix):
    return [json.loads(line[len(prefix):]) for line in Path(path).read_text().splitlines() if line.startswith(prefix)]


def decode(report, ready, expected_mvid):
    frames = []
    threads = report['payload']['threads']
    for key, leaf, outer in [('WaitTid', 'KnownWaitLeaf', 'KnownWaitOuter'), ('CpuTid', 'KnownCpuLeaf', 'KnownCpuOuter')]:
        thread = next(row for row in threads if int(row['native_thread_id'], 16) == ready[key])
        chain = [row for row in thread['stack_frames'] if row.get('is_managed') in (True, 'true')]
        positions = []
        for name in [leaf, outer]:
            found = [(index, row) for index, row in enumerate(chain)
                     if ('L01OwnedSynthetic.' + name + '(') in row.get('method_name', '')]
            assert len(found) == 1, 'Expected exact real managed frame: ' + name
            index, frame = found[0]
            assert frame['filename'] == 'L01Synthetic.dll' and uuid.UUID(frame['guid'].strip('{}')) == uuid.UUID(expected_mvid)
            token = int(frame['token'], 16)
            assert token & 0xff000000 == 0x06000000 and token & 0x00ffffff
            int(frame['il_offset'], 16)
            positions.append(index)
            frames.append(dict(tid=ready[key], method=frame['method_name'], token=frame['token'], ilOffset=frame['il_offset'],
                               module=frame['filename'], mvid=frame['guid']))
        assert positions[0] < positions[1], 'Leaf/outer stack order differs.'
    return frames


def decoder_proof():
    mvid = 'c4cb724a-22bc-4df2-aa7d-512dd0f27470'
    ready = dict(WaitTid=11, CpuTid=22)
    sample = dict(payload=dict(threads=[dict(native_thread_id=hex(tid), stack_frames=[dict(is_managed='true',
        method_name='L01OwnedSynthetic.' + method + '()', filename='L01Synthetic.dll', guid=mvid,
        token=hex(0x06000001 + index), il_offset='0x1') for index, method in enumerate(methods)])
        for tid, methods in [(11, ['KnownWaitLeaf', 'KnownWaitOuter']), (22, ['KnownCpuLeaf', 'KnownCpuOuter'])]]))
    assert len(decode(sample, ready, mvid)) == 4
    cases = []
    for kind in ['native_only', 'wrong_tid', 'wrong_mvid', 'wrong_module', 'zero_token', 'missing_frame', 'reversed_frames']:
        changed = copy.deepcopy(sample)
        thread = changed['payload']['threads'][0]
        frame = thread['stack_frames'][0]
        if kind == 'native_only': frame['is_managed'] = 'false'
        if kind == 'wrong_tid': thread['native_thread_id'] = '0xff'
        if kind == 'wrong_mvid': frame['guid'] = str(uuid.UUID(int=0))
        if kind == 'wrong_module': frame['filename'] = 'Foreign.dll'
        if kind == 'zero_token': frame['token'] = '0x0'
        if kind == 'missing_frame': thread['stack_frames'].pop()
        if kind == 'reversed_frames': thread['stack_frames'].reverse()
        try:
            decode(changed, ready, mvid)
        except (AssertionError, StopIteration):
            cases.append(dict(case=kind, rejected=True))
        else:
            raise AssertionError('Invalid synthetic report accepted: ' + kind)
    return dict(validCase=True, negativeCases=cases, scope='Decoder source harness only; no live target.')


def prepare(root, control, inventory, source, result):
    phase = root / 'prepare'
    phase.mkdir(mode=0o700)
    env = environment(phase)
    proof = decoder_proof()
    write(phase / 'decoder-proof.json', proof)
    assembly = phase / 'L01Synthetic.dll'
    config = phase / 'L01Synthetic.runtimeconfig.json'
    write(config, dict(runtimeOptions=dict(tfm='net8.0', framework=dict(name='Microsoft.NETCore.App', version='8.0.32'), rollForward='Disable')))
    rsp = phase / 'compile.rsp'
    args = ['-nostdlib+', '-target:exe', '-debug:portable', '-optimize-', '-out:' + str(assembly)]
    args += ['-reference:' + row['path'] for row in inventory['net8ReferenceInputs']]
    args += [str(HERE / 'Synthetic.cs')]
    rsp.write_text('\n'.join(args) + '\n')
    compiler = run_owned([DOTNET, 'exec', CSC, '-noconfig', '@' + str(rsp)], phase, 'compile', env, 60)
    result['compiler'] = compiler
    result['nativeExit'] = compiler['nativeExit']
    write(phase / 'compiler-record.json', compiler)
    assert compiler['nativeExit'] == 0 and not compiler['observerErrors'], 'Original helper compile failed; freeze, do not retry.'
    manifest = phase / 'metadata-manifest.json'
    collector_metadata(manifest, assembly, inventory['availableFiles'])
    metadata = run_owned([PWSH, '-NoLogo', '-NoProfile', '-NonInteractive', '-File', str(HERE / 'Collect.ps1'),
                          '-Manifest', str(manifest), '-MetadataOnly'], phase, 'metadata', env, 15)
    result['metadata'] = metadata
    result['nativeExit'] = metadata['nativeExit']
    write(phase / 'metadata-record.json', metadata)
    assert metadata['nativeExit'] == 0 and not metadata['observerErrors'], 'Original metadata preparation failed; freeze.'
    rows = prefixed(phase / 'metadata.stdout', 'L01_COLLECTOR ')
    abi = next(row for row in rows if row['Stage'] == 'metadata_only')
    for row in abi['Loaded']:
        assert image(row['Path'])['sha256'] == row['Sha256']
    result.update(kind='L01_S01_HELPER_PREPARATION_ZERO_TARGETS', source=source,
                  abi=abi, decoderProof=proof, sourcePins=control['sourcePins'], syntheticTargetStarts=0, diagnosticRequests=0,
                  outputs=[image(p) for p in sorted(phase.iterdir()) if p.is_file()], status='PREPARED_ZERO_TARGETS_PENDING_REVIEW')
    return result


def smoke(root, control, inventory, source):
    prep = json.loads((root / 'prepare/record.json').read_text())
    assert digest((root / 'prepare/record.json').read_bytes()) == control['smokeAdmission']['prepRecordSha256']
    assert prep['compiler']['nativeExit'] == prep['metadata']['nativeExit'] == 0 and prep['sourcePins'] == control['sourcePins']
    for row in prep['outputs']:
        assert image(row['path']) == row
    phase = root / 's01'
    phase.mkdir(mode=0o700)  # One attempt identity; never reuse this directory.
    env = environment(phase)
    nonce = uuid.uuid4().hex
    command = [DOTNET, 'exec', '--runtimeconfig', str(root / 'prepare/L01Synthetic.runtimeconfig.json'),
               str(root / 'prepare/L01Synthetic.dll'), nonce]
    record = dict(kind='L01_S01_ORIGINAL_SYNTHETIC', source=source, preparedSource=prep['source'], nonce=nonce,
                  status='HOLD', nativeExit=None, collector=None, target=None, observerErrors=[], diagnosticRequests=0,
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    write(phase / 'started.json', record)
    start = time.monotonic()
    target = None
    target_handle = None
    readers = []
    events = queue.Queue()

    def drain(stream, path):
        try:
            with path.open('wb') as out:
                for line in iter(stream.readline, b''):
                    out.write(line)
                    out.flush()
                    if line.startswith(b'L01_SYNTHETIC '): events.put(json.loads(line[len(b'L01_SYNTHETIC '):]))
        except BaseException as error:
            record['observerErrors'].append('Stream observer: ' + repr(error))
        finally:
            stream.close()

    def cleanup_createdump():
        owned_children = children(target.pid)
        record['childrenAfterRequest'] = owned_children
        for child_id in owned_children:
            expected = identity(child_id)
            args = Path(f'/proc/{child_id}/cmdline').read_bytes().split(b'\0')
            exe = str(Path(f'/proc/{child_id}/exe').resolve(strict=True))
            assert expected['parentPid'] == target.pid and expected['uid'] == os.geteuid() and exe == CREATEDUMP
            assert str(phase / 'synthetic-normal.core').encode() in args and str(target.pid).encode() in args
            fd = open_owned(child_id, expected)
            try:
                terminate(fd)
                record.setdefault('terminatedOwnedCreatedump', []).append(expected)
            finally:
                os.close(fd)

    try:
        target = subprocess.Popen(command, cwd=phase, env=env, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        record['target'] = dict(pid=target.pid, command=command)
        owned = identity(target.pid)
        target_handle = open_owned(target.pid, owned)
        record['target'] = dict(identity=owned, command=command)
        for stream, name in [(target.stdout, 'target.stdout'), (target.stderr, 'target.stderr')]:
            thread = threading.Thread(target=drain, args=(stream, phase / name), daemon=True)
            thread.start()
            readers.append(thread)
        ready = events.get(timeout=10)
        record['ready'] = ready
        assert ready['Stage'] == 'ready' and ready['Pid'] == target.pid and ready['BirthTick'] == owned['birthTick']
        assert ready['Nonce'] == nonce and ready['Uid'] == os.geteuid() and ready['Iterations'] >= 1000
        assert ready['Mvid'] == prep['abi']['TargetMvid'] and ready['Runtime'] == '.NET 8.0.32'
        assert ready['WaitTid'] != ready['CpuTid']
        socket = phase / 'tmp' / f"dotnet-diagnostic-{target.pid}-{owned['birthTick']}-socket"
        assert len(str(socket).encode()) <= 107 and stat.S_ISSOCK(socket.lstat().st_mode) and socket.lstat().st_uid == os.geteuid()
        manifest = phase / 'collector-manifest.json'
        collector_metadata(manifest, root / 'prepare/L01Synthetic.dll', inventory['availableFiles'])
        m = json.loads(manifest.read_text())
        supervisor = identity(os.getpid())
        m.update(SupervisorPid=os.getpid(), SupervisorBirthTick=supervisor['birthTick'], Uid=os.geteuid(),
                 TargetPid=target.pid, TargetBirthTick=owned['birthTick'], TargetExecutable=DOTNET,
                 TargetCommand=command, Nonce=nonce, Socket=str(socket), DumpPath=str(phase / 'synthetic-normal.core'))
        write(manifest, m)
        assert identity(target.pid) == owned and target.poll() is None
        record['diagnosticRequests'] = 1  # One public invocation; no fallback/retry.
        before = time.monotonic()
        record['collector'] = run_owned([PWSH, '-NoLogo', '-NoProfile', '-NonInteractive', '-File', str(HERE / 'Collect.ps1'),
                                         '-Manifest', str(manifest)], phase, 'collector', env, 20)
        record['nativeExit'] = record['collector']['nativeExit']
        record['requestSeconds'] = time.monotonic() - before
        cleanup_createdump()
        assert record['collector']['nativeExit'] == 0 and not record['collector']['observerErrors'] and not record['childrenAfterRequest']
        report_path = phase / 'synthetic-normal.core.crashreport.json'
        report = json.loads(report_path.read_text())
        record['managedFrames'] = decode(report, ready, prep['abi']['TargetMvid'])
        record['rawStackReport'] = image(report_path)
        record['privateCore'] = image(phase / 'synthetic-normal.core')
        assert record['privateCore']['bytes'] > 0
        record['interpreted'] = 'KnownWaitLeaf/Outer on private stdin and KnownCpuLeaf/Outer on bounded real worker, mapped by native TID/module MVID/method tokens.'
        record['status'] = 'STACKS_INTERPRETED_PENDING_CLEANUP'
    except BaseException as error:
        record['observerErrors'].append(repr(error))
    finally:
        if target is not None:
            try:
                if target.poll() is None:
                    try:
                        cleanup_createdump()
                    except BaseException as error:
                        record['observerErrors'].append('Createdump accounting: ' + repr(error))
                    try:
                        target.stdin.write(('STOP:' + nonce + '\n').encode())
                        target.stdin.flush()
                        target.stdin.close()
                        record['target']['nativeExit'] = target.wait(timeout=5)
                    except BaseException as error:
                        record['observerErrors'].append('Cooperative stop: ' + repr(error))
                    finally:
                        if target.poll() is None:
                            if target_handle is not None:
                                terminate(target_handle)
                            else:
                                assert identity(target.pid)['parentPid'] == os.getpid()
                                target.kill()
                            record['target']['forcedCleanupExit'] = target.wait(timeout=5)
                else:
                    record['target']['nativeExit'] = target.returncode
                for reader in readers:
                    reader.join(timeout=5)
                    assert not reader.is_alive(), 'Owned stdout/stderr EOF not reached.'
                record['target']['joinedEof'] = True
                record['targetEventsAfterReady'] = []
                while not events.empty(): record['targetEventsAfterReady'].append(events.get_nowait())
                assert record['target'].get('nativeExit') == 0
                done = next(e for e in record['targetEventsAfterReady'] if e['Stage'] == 'done')
                assert done['Nonce'] == nonce and done['ValidStop'] and done['Iterations'] >= record['ready']['Iterations']
            except BaseException as error:
                record['observerErrors'].append('Owned cleanup: ' + repr(error))
            finally:
                if target.stdin is not None and not target.stdin.closed: target.stdin.close()
                if target_handle is not None: os.close(target_handle)
        record['durationSeconds'] = time.monotonic() - start
    if record['status'] == 'STACKS_INTERPRETED_PENDING_CLEANUP' and not record['observerErrors']:
        record['status'] = 'PASS_OWNED_SYNTHETIC_MANAGED_STACK_ONLY'
    return record


def main():
    os.umask(0o077)
    mode, source = sys.argv[1:]
    assert mode in ['prepare', 'smoke']
    assert subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=REPO, text=True).strip() == source
    assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=REPO)
    inventory = json.loads(INVENTORY.read_text())
    control = json.loads(CONTROL.read_text())
    for row in control['sourcePins'] + inventory['availableFiles'] + inventory['net8ReferenceInputs']:
        assert image(REPO / row['path'] if not Path(row['path']).is_absolute() else row['path'])['sha256'] == row['sha256']
    root = Path(inventory['plannedRunRoot'])
    assert len((str(root / 's01/tmp') + '/dotnet-diagnostic-' + '9' * 10 + '-' + '9' * 20 + '-socket').encode()) <= 107
    self_fd = os.pidfd_open(os.getpid())
    os.close(self_fd)
    if mode == 'prepare':
        assert control['prepareAdmission'] == 'PLAN_ACCEPTED_HELPER_PREPARATION_ONLY' and not root.exists()
        root.mkdir(mode=0o700)
        record = dict(nativeExit=None, observerErrors=[], status='HOLD_PREPARATION', source=source)
        try:
            prepare(root, control, inventory, source, record)
        except BaseException as error:
            record['observerErrors'].append(repr(error))
    else:
        assert control['smokeAdmission']['status'] == 'SOURCE_PREP_ACCEPTED_S01_ONCE'
        assert root.stat().st_uid == os.geteuid() and stat.S_IMODE(root.stat().st_mode) == 0o700 and not (root / 's01').exists()
        record = smoke(root, control, inventory, source)
    def emit(value):
        payload = (json.dumps({key: value.get(key) for key in ['status', 'nativeExit', 'diagnosticRequests', 'durationSeconds', 'observerErrors']}) + '\n').encode()
        if os.write(1, payload) != len(payload): raise OSError('Incomplete summary write')
    return FINALIZE(record, lambda value: write(root / ('prepare' if mode == 'prepare' else 's01') / 'record.json', value), emit)


if __name__ == '__main__':
    sys.exit(main())
