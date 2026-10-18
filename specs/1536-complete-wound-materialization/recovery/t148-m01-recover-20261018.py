"""Exact GitHub carrier recovery with explicitly historical L01 catalog/selection pins."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys

repo = Path(__file__).resolve().parents[3]
restore = Path('/workspace/1536-cloud-clean-restore')
base = '0292234faa8eb1cd405ade611057be07bafa5e19'
branch = '1536-wound-b2-cloud-20261012'
sha = sys.argv[1]


def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args])


assert git(repo, 'ls-remote', 'origin', 'refs/heads/' + branch).decode().split()[0] == sha
subprocess.run(['git', '-C', str(restore), 'fetch', 'origin', 'refs/heads/' + branch], check=True)
subprocess.run(['git', '-C', str(restore), 'checkout', '--detach', sha], check=True)
assert git(restore, 'rev-parse', 'HEAD').decode().strip() == sha and not git(restore, 'status', '--porcelain')
rows = []
for path in git(repo, 'diff', '--name-only', base, sha).decode().splitlines():
    data = (restore / path).read_bytes()
    assert data == (repo / path).read_bytes() == git(repo, 'show', sha + ':' + path)
    rows.append(dict(path=path, bytes=len(data), sha256=hashlib.sha256(data).hexdigest(),
                     gitBlob=git(restore, 'rev-parse', sha + ':' + path).decode().strip(),
                     mode=git(restore, 'ls-tree', sha, '--', path).decode().split()[0]))
recovery = 'specs/1536-complete-wound-materialization/recovery/'
for name in ['t179pd-ni-iobs-l01-source-packet-20261018.json', 't179pd-ni-iobs-l01-cloud-qualification-20261018.json',
             't179pd-ni-iobs-l01-method-row-ledger-20261018.json']:
    assert (restore / recovery / name).read_bytes() == git(repo, 'show', base + ':' + recovery + name)
old = json.loads((restore / recovery / 't179pd-ni-iobs-l01-source-packet-20261018.json').read_text())
exceptions = []
for row in old['sourcePins']:
    data = (restore / row['path']).read_bytes()
    digest = hashlib.sha256(data).hexdigest()
    if digest != row['sha256'] or len(data) != row['bytes']:
        exceptions.append(dict(path=row['path'], historical=row['sha256'], current=digest))
assert {row['path'] for row in exceptions} == {'tests/categories.json', 'tests/selection.json'}
packet = json.loads((restore / recovery / 't148-m01-source-packet-20261018.json').read_text())
for row in packet['sourcePins']:
    data = (restore / row['path']).read_bytes()
    assert len(data) == row['bytes'] and hashlib.sha256(data).hexdigest() == row['sha256'], row['path']
assert not git(repo, 'diff', '--name-only', base, sha, '--', 'BookOfEternityClient')
out = Path('/tmp/1536-cloud-tools') / ('m01-carrier-recovery-' + sha[:8] + '.json')
receipt = dict(kind='M01_EXACT_GITHUB_RECOVERY', sha=sha, base=base,
               checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), clean=True,
               remoteShaVerified=True, localSourceOrObjectReuse=False, changedPaths=len(rows), files=rows,
               historicalL01UnchangedPins=99, historicalL01ExpectedExceptions=exceptions,
               historicalL01LedgersByteIdentical=True, productionChanges=False,
               M01SourcePinsVerified=len(packet['sourcePins']), sourcePinDigest=packet['sourcePinDigest'],
               limits='Source/evidence recovery only; L01 original52/57 provenance retained, no current cross-build qualification or lost bytes recovery.')
out.write_text(json.dumps(receipt, indent=2) + '\n')
print('RECOVERED', sha, len(rows), 'paths', str(out))
