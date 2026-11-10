"""Exact G02 source/evidence recovery; old packets and results retain their identity."""
from pathlib import Path
import datetime
import hashlib
import json
import subprocess
import sys

REPO=Path(__file__).resolve().parents[4]
RESTORE=Path('/workspace/1536-cloud-clean-restore')
PREFIX='specs/1536-complete-wound-materialization/recovery/'
BASE='0292234faa8eb1cd405ade611057be07bafa5e19'
PRIOR='4e379a92e27d488bc602f743fc739507d6a96648'
BRANCH='1536-wound-b2-cloud-20261012'
def git(root,*args):return subprocess.check_output(['git','-C',str(root),*args])
def digest(b):return hashlib.sha256(b).hexdigest()

sha=sys.argv[1]
assert git(REPO,'ls-remote','origin','refs/heads/'+BRANCH).decode().split()[0]==sha
assert not git(RESTORE,'status','--porcelain')
subprocess.run(['git','-C',str(RESTORE),'fetch','origin','refs/heads/'+BRANCH],check=True)
subprocess.run(['git','-C',str(RESTORE),'checkout','--detach',sha],check=True)
assert git(RESTORE,'rev-parse','HEAD').decode().strip()==sha and not git(RESTORE,'status','--porcelain')
rows=[]
for p in git(REPO,'diff','--name-only',BASE,sha).decode().splitlines():
    assert not p.endswith(('.core','.crashreport.json')),'Private dump/report must never be in Git.'
    b=(RESTORE/p).read_bytes();assert b==(REPO/p).read_bytes()==git(REPO,'show',sha+':'+p)
    rows.append(dict(path=p,bytes=len(b),sha256=digest(b)))
packet=json.loads((RESTORE/PREFIX/'l01-game-capture-g02/source-packet.json').read_text())
for pin in packet['sourcePins']:
    b=(RESTORE/pin['path']).read_bytes();assert len(b)==pin['bytes'] and digest(b)==pin['sha256'],pin['path']
assert digest(json.dumps(packet['sourcePins'],sort_keys=True,separators=(',',':')).encode())==packet['sourcePinDigest']
prior=json.loads((RESTORE/PREFIX/'l01-delivery-d01-source-packet-20261018.json').read_text())
actual=[]
for pin in prior['sourcePins']:
    b=(RESTORE/pin['path']).read_bytes()
    if digest(b)!=pin['sha256']:actual.append(dict(path=pin['path'],priorSha256=pin['sha256'],sha256=digest(b),bytes=len(b)))
assert actual==packet['explicitD01Deltas']
for name in ('l01-delivery-d01-source-packet-20261018.json','l01-managed-stack-capability-control-20261018.json',
             't179pd-ni-iobs-l01-source-packet-20261018.json','t179pd-ni-iobs-l01-cloud-qualification-20261018.json',
             't179pd-ni-iobs-l01-method-row-ledger-20261018.json','t148-m01-source-packet-20261018.json'):
    p=PREFIX+name;assert (RESTORE/p).read_bytes()==git(REPO,'show',PRIOR+':'+p)
for group in packet['historicalPins']:
    original=json.loads((RESTORE/group['path']).read_text());deltas=[]
    for pin in original['sourcePins']:
        b=(RESTORE/pin['path']).read_bytes()
        if digest(b)!=pin['sha256']:deltas.append(dict(path=pin['path'],priorSha256=pin['sha256'],sha256=digest(b),bytes=len(b)))
    assert deltas==group['explicitCurrentDeltas']
old=json.loads((RESTORE/PREFIX/'l01-game-capture-g01/source-packet.json').read_text());deltas=[]
for pin in old['sourcePins']:
    b=(RESTORE/pin['path']).read_bytes()
    if digest(b)!=pin['sha256']:deltas.append(dict(path=pin['path'],priorSha256=pin['sha256'],sha256=digest(b),bytes=len(b)))
assert deltas==packet['explicitG01Deltas'] and len(deltas)==3
oldbase='46c621fb50fe61e5a662ccaf8f00f3fa0f3c6dc3'
allowed={row['path'] for row in deltas}
changes=git(REPO,'diff','--name-only',oldbase,sha,'--',PREFIX+'l01-game-capture-g01').decode().splitlines()
assert set(changes)==allowed
assert not git(REPO,'diff','--name-only',oldbase,sha,'--','.',':(exclude)specs/1536-complete-wound-materialization/')
assert not git(REPO,'diff','--name-only',PRIOR,sha,'--','BookOfEternityClient','BookOfEternityClient.TestSupport')
result=dict(kind='G02_EXACT_GITHUB_RECOVERY',sha=sha,checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    remoteShaVerified=True,clean=True,localWriterObjectsCopied=False,changedPaths=len(rows),files=rows,
    currentPinsVerified=len(packet['sourcePins']),sourcePinDigest=packet['sourcePinDigest'],explicitD01Deltas=actual,
    historicalLedgersUnchanged=True,productionChanges=False,privateDumpOrRawReportPublished=False,
    limits='Source/sanitized evidence only. Old66529 UNKNOWN unavailable; no private dump/report or full private binary restoration claim.')
out=Path('/tmp/1536-cloud-tools')/('g02-recovery-'+sha[:8]+'.json');out.write_text(json.dumps(result,indent=2)+'\n')
print('RECOVERED',sha,len(rows),'paths',len(packet['sourcePins']),'pins',out)
