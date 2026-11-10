"""Run one explicitly admitted M01 phase; retain its original native outcome, never auto retry."""
from pathlib import Path
from contextlib import contextmanager
import datetime
import hashlib
import json
import os
import subprocess
import sys
import time

REPO = Path(__file__).resolve().parents[3]
PACKET = Path(__file__).with_name('t148-m01-source-packet-20261018.json')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=REPO, text=True).strip()


def binaries():
    rows = []
    for project in ['BookOfEternityClient.Tests', 'BookOfEternityClient.IntegrationTests']:
        root = REPO / project / 'bin/Debug/net8.0'
        assert root.is_dir(), str(root)
        for file in sorted(root.rglob('*')):
            if file.is_file():
                data = file.read_bytes()
                rows.append(dict(path=str(file.relative_to(REPO)), bytes=len(data), sha256=sha(data)))
    return rows


def capture_native(record, outputs, launch):
    """Keep a known native status even if closing its output files fails."""
    try:
        with outputs() as streams:
            record['nativeExit'] = launch(*streams)
    except BaseException as error:
        record['launchOrControllerError'] = repr(error)
        record['observerErrors'].append('Native launch/output observer: ' + repr(error))


def finalize(record, persist, emit):
    """Observer failures hold native success; they never replace a native failure."""
    try:
        persist(record)
    except BaseException as error:
        record['observerErrors'].append('Record persistence: ' + repr(error))
    try:
        emit(record)
    except BaseException as error:
        record['observerErrors'].append('Summary emission: ' + repr(error))
        # Bounded record update only; never relaunch a native phase.
        try:
            persist(record)
        except BaseException as persistence_error:
            record['observerErrors'].append('Record update: ' + repr(persistence_error))
    native = record['nativeExit']
    return native if native is not None and native != 0 else (2 if native is None or record['observerErrors'] else 0)


def emit_summary(record):
    payload = (json.dumps({key: record.get(key) for key in
                         ['mode', 'nativeExit', 'durationSeconds', 'resultDirectories', 'observerErrors']}) + '\n').encode()
    # No buffered stdout flush at interpreter shutdown can override the native exit.
    if os.write(1, payload) != len(payload):
        raise OSError('Incomplete summary write')


def main():
    mode, source = sys.argv[1:]
    assert mode in {'catalog', 'plan', 'actual'}
    packet = json.loads(PACKET.read_text())
    assert git('rev-parse', 'HEAD') == source and not git('status', '--porcelain')
    for row in packet['sourcePins']:
        data = (REPO / row['path']).read_bytes()
        assert len(data) == row['bytes'] and sha(data) == row['sha256'], row['path']
    release = packet['admissions'][mode]
    assert release['status'] == ('M01_PREP_ACCEPTED_FIRST_SINGLETON' if mode == 'actual' else 'M01_SOURCE_ACCEPTED_ZERO_ONLY')
    root = Path(packet['runRoot'])
    phase = root / mode
    assert not phase.exists(), 'Phase identity already exists; no retry/reuse.'
    prior = None
    if mode != 'catalog':
        prior = json.loads((root / 'catalog/record.json').read_text())
        assert prior['nativeExit'] == 0 and not prior['observerErrors']
        assert prior['sourcePinDigest'] == packet['sourcePinDigest']
        assert binaries() == prior['binaryInventory'], 'Fresh compiled outputs changed.'
        changes = git('diff', '--name-only', prior['compiledSource'], source).splitlines()
        assert all(path.startswith('specs/1536-complete-wound-materialization/') for path in changes), 'Non-document source changed after build.'
    if mode == 'actual':
        prep = json.loads((root / 'plan/record.json').read_text())
        assert prep['nativeExit'] == 0 and not prep['observerErrors']
        assert release['prepPlanRecordSha256'] == sha((root / 'plan/record.json').read_bytes())
        assert release['prepCatalogRecordSha256'] == sha((root / 'catalog/record.json').read_bytes())
    phase.mkdir(parents=True)
    env = os.environ.copy()
    env.update(PATH='/tmp/1536-cloud-tools/pwsh:/tmp/1536-cloud-tools/dotnet:' + env.get('PATH', ''),
               DOTNET_ROOT='/tmp/1536-cloud-tools/dotnet', NUGET_PACKAGES='/tmp/1536-cloud-tools/nuget',
               DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',
               TMPDIR=str(phase / 'tmp'), XDG_RUNTIME_DIR=str(phase / 'runtime'),
               DOTNET_CLI_HOME=str(phase / 'tooling/cli'), XDG_CACHE_HOME=str(phase / 'tooling/cache'),
               XDG_CONFIG_HOME=str(phase / 'tooling/config'), XDG_DATA_HOME=str(phase / 'tooling/data'))
    for key in ['TMPDIR', 'XDG_RUNTIME_DIR', 'DOTNET_CLI_HOME', 'XDG_CACHE_HOME', 'XDG_CONFIG_HOME', 'XDG_DATA_HOME']:
        Path(env[key]).mkdir(parents=True)
    cmd = ['/tmp/1536-cloud-tools/pwsh/pwsh', '-NoProfile', '-File', 'scripts/test-csharp.ps1']
    cmd += ['-ValidateCatalog'] if mode == 'catalog' else ['-Category', packet['category'], '-NoBuild']
    if mode == 'plan':
        cmd += ['-PlanOnly']
    result_root = REPO / 'TestResults/test-categories'
    before = set(result_root.iterdir()) if result_root.exists() else set()
    record = dict(kind='T148_M01_SINGLE_PHASE_ORIGINAL', mode=mode, actualSource=source,
                  compiledSource=source if prior is None else prior['compiledSource'],
                  sourcePinDigest=packet['sourcePinDigest'], command=cmd,
                  startedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
                  phaseRoot=str(phase), fixtureTemp=env['TMPDIR'], nativeExit=None, observerErrors=[])
    (phase / 'started.json').write_text(json.dumps(record, indent=2) + '\n')
    start = time.monotonic()

    @contextmanager
    def outputs():
        with (phase / 'stdout.txt').open('wb') as out, (phase / 'stderr.txt').open('wb') as err:
            yield out, err

    try:
        capture_native(record, outputs, lambda out, err:
                       subprocess.run(cmd, cwd=REPO, env=env, stdout=out, stderr=err).returncode)
    finally:
        record['durationSeconds'] = time.monotonic() - start
        record['endedUtc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        try:
            after = set(result_root.iterdir()) if result_root.exists() else set()
            new = sorted(after - before)
            record['resultDirectories'] = [str(path.relative_to(REPO)) for path in new]
            if len(new) != 1:
                record['observerErrors'].append('Expected exactly one new runner result directory.')
            record['remainingOwnedFixtureRoots'] = [str(path) for path in Path(env['TMPDIR']).glob('boe-*')]
            if record['remainingOwnedFixtureRoots']:
                record['observerErrors'].append('Owned fixture roots remain; do not delete/retry.')
            record['binaryInventory'] = binaries()
        except BaseException as error:
            record['observerErrors'].append(repr(error))
    return finalize(record,
                    lambda value: (phase / 'record.json').write_text(json.dumps(value, indent=2) + '\n'),
                    emit_summary)


if __name__ == '__main__':
    sys.exit(main())
