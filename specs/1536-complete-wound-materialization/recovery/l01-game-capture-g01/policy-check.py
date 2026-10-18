"""Direct pure-policy RED/GREEN only; no game, child fixture, collector or IPC."""
from pathlib import Path
import hashlib
import json
import runpy
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
REC = HERE.parent

def main():
    mode, source = sys.argv[1:]
    assert mode in ('red', 'green')
    assert subprocess.check_output(['git', '-C', str(REPO), 'rev-parse', 'HEAD'], text=True).strip() == source
    assert not subprocess.check_output(['git', '-C', str(REPO), 'status', '--porcelain'])
    packet = json.loads((REC / 'l01-delivery-d01-source-packet-20261018.json').read_text())
    for row in packet['sourcePins']:
        data = (REPO / row['path']).read_bytes()
        assert len(data) == row['bytes'] and hashlib.sha256(data).hexdigest() == row['sha256']
    lib = runpy.run_path(str(REC / 'l01-managed-stack-s01/smoke.py'))
    summary = runpy.run_path(str(REC / 'l01-delivery-d01/phase.py'))['emit_summary']
    root = Path('/tmp/g01-policy-check') / mode
    root.mkdir(mode=0o700, parents=True)
    inventory = json.loads((REC / 'l01-managed-stack-capability-inventory-20261018.json').read_text())
    for row in inventory['availableFiles'] + inventory['net8ReferenceInputs']:
        data = Path(row['path']).read_bytes()
        assert len(data) == row['bytes'] and hashlib.sha256(data).hexdigest() == row['sha256']
    env = lib['environment'](root)
    lib['write'](root / 'Policy.runtimeconfig.json', dict(runtimeOptions=dict(tfm='net8.0', framework=dict(name='Microsoft.NETCore.App', version='8.0.32'), rollForward='Disable')))
    sources = [REPO / 'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs', REPO / 'BookOfEternityClient.IntegrationTests/JointInterfaceManagedCapturePolicy.cs', HERE / 'PolicyProbe.cs']
    args = ['-nostdlib+', '-target:exe', '-out:' + str(root / 'Policy.dll')]
    args += ['-reference:' + r['path'] for r in inventory['net8ReferenceInputs']]
    args += list(map(str, sources))
    rsp = root / 'compile.rsp'; rsp.write_text('\n'.join(args) + '\n')
    record = dict(kind='G01_POLICY_' + mode.upper(), source=source, nativeExit=None, observerErrors=[], gameProcesses=0, requests=0, sourceImages=[lib['image'](p) for p in sources])
    started = time.monotonic()
    try:
        record['compile'] = lib['run_owned']([lib['DOTNET'], 'exec', lib['CSC'], '-noconfig', '@' + str(rsp)], root, 'compile', env, 60)
        record['nativeExit'] = record['compile']['nativeExit']
        record['observerErrors'].extend(record['compile']['observerErrors'])
        assert record['compile']['nativeExit'] == 0 and not record['compile']['observerErrors']
        record['probe'] = lib['run_owned']([lib['DOTNET'], str(root / 'Policy.dll')], root, 'probe', env, 10)
        record['nativeExit'] = record['probe']['nativeExit']
        record['observerErrors'].extend(record['probe']['observerErrors'])
        record['results'] = json.loads((root / 'probe.stdout').read_text())
    except BaseException as e: record['observerErrors'].append(repr(e))
    record['durationSeconds'] = time.monotonic() - started
    record['status'] = 'EXPECTED_UNIMPLEMENTED_RED' if mode == 'red' and record['nativeExit'] == 1 and not record['observerErrors'] else 'GREEN' if mode == 'green' and record['nativeExit'] == 0 and not record['observerErrors'] else 'HOLD_FIRST_OUTCOME'
    return lib['FINALIZE'](record, lambda r: lib['write'](root/'record.json', r), summary)

if __name__ == '__main__': sys.exit(main())
