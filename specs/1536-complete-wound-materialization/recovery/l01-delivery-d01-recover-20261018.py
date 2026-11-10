"""Exact D01 GitHub recovery; historical source deltas are explicit current pins."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys

REPO = Path(__file__).resolve().parents[3]
RESTORE = Path('/workspace/1536-cloud-clean-restore')
RECOVERY = 'specs/1536-complete-wound-materialization/recovery/'
BASE = '0292234faa8eb1cd405ade611057be07bafa5e19'
BRANCH = '1536-wound-b2-cloud-20261012'
sha = sys.argv[1]

def git(root, *args):
    return subprocess.check_output(['git', '-C', str(root), *args])

assert git(REPO, 'ls-remote', 'origin', 'refs/heads/' + BRANCH).decode().split()[0] == sha
assert not git(RESTORE, 'status', '--porcelain')
subprocess.run(['git', '-C', str(RESTORE), 'fetch', 'origin', 'refs/heads/' + BRANCH], check=True)
subprocess.run(['git', '-C', str(RESTORE), 'checkout', '--detach', sha], check=True)
assert git(RESTORE, 'rev-parse', 'HEAD').decode().strip() == sha and not git(RESTORE, 'status', '--porcelain')
rows = []
for path in git(REPO, 'diff', '--name-only', BASE, sha).decode().splitlines():
    data = (RESTORE / path).read_bytes()
    assert data == (REPO / path).read_bytes() == git(REPO, 'show', sha + ':' + path)
    rows.append(dict(path=path, bytes=len(data), sha256=hashlib.sha256(data).hexdigest(),
                     gitBlob=git(RESTORE, 'rev-parse', sha + ':' + path).decode().strip(),
                     mode=git(RESTORE, 'ls-tree', sha, '--', path).decode().split()[0]))
packet = json.loads((RESTORE / RECOVERY / 'l01-delivery-d01-source-packet-20261018.json').read_text())
for pin in packet['sourcePins']:
    data = (RESTORE / pin['path']).read_bytes()
    assert len(data) == pin['bytes'] and hashlib.sha256(data).hexdigest() == pin['sha256'], pin['path']
assert hashlib.sha256(json.dumps(packet['sourcePins'], sort_keys=True, separators=(',', ':')).encode()).hexdigest() == packet['sourcePinDigest']
for name in ['t179pd-ni-iobs-l01-source-packet-20261018.json', 't179pd-ni-iobs-l01-cloud-qualification-20261018.json', 't179pd-ni-iobs-l01-method-row-ledger-20261018.json']:
    assert (RESTORE / RECOVERY / name).read_bytes() == git(REPO, 'show', BASE + ':' + RECOVERY + name)
assert (RESTORE / RECOVERY / 't148-m01-source-packet-20261018.json').read_bytes() == git(REPO, 'show', '1817ae5814f06a45cd6df975a9211c380a256df8:' + RECOVERY + 't148-m01-source-packet-20261018.json')
for binding in packet['historicalPinSets']:
    original = json.loads((RESTORE / binding['path']).read_text())
    actual = []
    for pin in original['sourcePins']:
        data = (RESTORE / pin['path']).read_bytes()
        current = dict(path=pin['path'], bytes=len(data), sha256=hashlib.sha256(data).hexdigest())
        if current['bytes'] != pin['bytes'] or current['sha256'] != pin['sha256']:
            actual.append(dict(path=pin['path'], historicalSha256=pin['sha256'], current=current))
    assert actual == binding['explicitCurrentDeltas'] and len(original['sourcePins']) - len(actual) == binding['unchangedPins']
s01 = json.loads((RESTORE / RECOVERY / 'l01-managed-stack-capability-control-20261018.json').read_text())
for pin in s01['sourcePins']:
    data = (RESTORE / pin['path']).read_bytes()
    assert len(data) == pin['bytes'] and hashlib.sha256(data).hexdigest() == pin['sha256']
assert not git(REPO, 'diff', '--name-only', BASE, sha, '--', 'BookOfEternityClient')
result = dict(kind='D01_EXACT_GITHUB_SOURCE_EVIDENCE_RECOVERY', sha=sha, base=BASE,
    checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(), remoteShaVerified=True, clean=True,
    localWriterObjectsCopied=False, changedPaths=len(rows), files=rows, currentPinsVerified=len(packet['sourcePins']),
    sourcePinDigest=packet['sourcePinDigest'], historicalPinSets=packet['historicalPinSets'], S01PinsVerified=11,
    historicalLedgersUnchanged=True, productionChanges=False,
    limits='Source/evidence recovery only; old L01/M01 remain original compiled provenance, old66529 UNKNOWN unavailable. No lost bytes/current cross-build/Windows/game qualification claim.')
out=Path('/tmp/1536-cloud-tools')/('d01-carrier-recovery-'+sha[:8]+'.json')
out.write_text(json.dumps(result,indent=2)+'\n')
print('RECOVERED',sha,len(rows),'paths',len(packet['sourcePins']),'current pins',str(out))
