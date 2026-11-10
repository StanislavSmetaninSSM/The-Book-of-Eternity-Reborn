"""Non-live helper preparation only. There is no live/game/attach entrypoint."""
from pathlib import Path
import hashlib
import json
import os
import runpy
import subprocess
import sys
import time

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[3]
RECOVERY = HERE.parent
ROOT = Path('/tmp/l01d01-76df8b42f6ad4ea2')
HELPER = REPO / 'BookOfEternityClient.IntegrationTests/JointInterfaceDiagnosticDelivery.cs'
S01 = RECOVERY / 'l01-managed-stack-s01/smoke.py'
sha = lambda data: hashlib.sha256(data).hexdigest()


def main():
    mode, source = sys.argv[1:]
    assert mode in ('prepare', 'pure')
    assert subprocess.check_output(['git', '-C', str(REPO), 'rev-parse', 'HEAD']).decode().strip() == source
    assert not subprocess.check_output(['git', '-C', str(REPO), 'status', '--porcelain'])
    assert sha((RECOVERY / 'l01-live-delivery-plan-independent-review-20261018.json').read_bytes()) == '3253e8d6ffa47a76c3e22118f6da0272701c6025dd90ce0bcda7997590e2b863'
    assert sha(S01.read_bytes()) == 'd121731cff4867acf993d034962c9f5591fdcb496307a5fe0e9642b8392da333'
    lib = runpy.run_path(str(S01))
    inventory = json.loads((RECOVERY / 'l01-managed-stack-capability-inventory-20261018.json').read_text())
    for row in inventory['availableFiles'] + inventory['net8ReferenceInputs']:
        b = Path(row['path']).read_bytes()
        assert len(b) == row['bytes'] and sha(b) == row['sha256'], row['path']
    os.umask(0o077)
    assert ROOT.stat().st_uid == os.geteuid() and ROOT.stat().st_mode & 0o777 == 0o700
    phase = ROOT / ('checks01-' + mode)
    phase.mkdir(mode=0o700)
    env = lib['environment'](phase)
    pins = [lib['image'](p) for p in [HELPER, HERE / 'Checks.cs', Path(__file__), S01]]
    record = dict(kind='D01_NONLIVE_HELPER_' + mode.upper(), source=source, sourcePins=pins,
                  fixtureSubprocessTargets=0, gameProcesses=0, diagnosticRequests=0,
                  nativeExit=None, observerErrors=[])
    if mode == 'prepare':
        lib['write'](phase / 'D01Checks.runtimeconfig.json', dict(runtimeOptions=dict(tfm='net8.0',
            framework=dict(name='Microsoft.NETCore.App', version='8.0.32'), rollForward='Disable')))
        args = ['-nostdlib+', '-target:exe', '-debug:portable', '-optimize-', '-out:' + str(phase / 'D01Checks.dll')]
        args += ['-reference:' + row['path'] for row in inventory['net8ReferenceInputs']]
        args += [str(HELPER), str(HERE / 'Checks.cs')]
        rsp = phase / 'compile.rsp'
        rsp.write_text('\n'.join(args) + '\n')
        command = [lib['DOTNET'], 'exec', lib['CSC'], '-noconfig', '@' + str(rsp)]
    else:
        prep_dir = ROOT / 'checks01-prepare'
        prep = json.loads((prep_dir / 'record.json').read_text())
        assert prep['nativeExit'] == 0 and not prep['observerErrors'] and prep['sourcePins'] == pins
        for row in prep['outputs']: assert lib['image'](row['path']) == row
        command = [lib['DOTNET'], 'exec', '--runtimeconfig', str(prep_dir / 'D01Checks.runtimeconfig.json'),
                   str(prep_dir / 'D01Checks.dll'), 'pure']
    lib['write'](phase / 'started.json', dict(record, command=command))
    start = time.monotonic()
    result = lib['run_owned'](command, phase, 'host', env, 60 if mode == 'prepare' else 20)
    record.update(nativeExit=result['nativeExit'], observerErrors=result['observerErrors'], host=result,
                  durationSeconds=time.monotonic() - start)
    if mode == 'prepare':
        record['outputs'] = [lib['image'](p) for p in sorted(phase.iterdir()) if p.is_file()]
        record['status'] = 'PREPARED_NONLIVE_HELPER' if record['nativeExit'] == 0 and not record['observerErrors'] else 'FROZEN_COMPILER_FAILURE'
    else:
        try:
            payload = json.loads((phase / 'host.stdout').read_text())
            record['checks'] = payload['Checks']
            assert len(record['checks']) == 20 and all(x['Passed'] for x in record['checks'])
            assert record['nativeExit'] == 0 and result['joined'] and not record['observerErrors']
            record['status'] = 'PASS_NONLIVE_HELPER20_ONLY'
        except BaseException as error:
            record['observerErrors'].append(repr(error));record['status'] = 'FROZEN_NONLIVE_HELPER_FAILURE'
    return lib['FINALIZE'](record, lambda r: lib['write'](phase / 'record.json', r),
        lambda r: print(json.dumps({k:r[k] for k in ('status','nativeExit','observerErrors','durationSeconds')})))

if __name__ == '__main__':
    sys.exit(main())
