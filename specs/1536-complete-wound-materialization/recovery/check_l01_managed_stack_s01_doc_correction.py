#!/usr/bin/env python3
"""Narrow read-only check of two documentary precision corrections."""
import json,hashlib,subprocess,datetime
from pathlib import Path
R=Path('/workspace/The-Book-of-Eternity-Reborn');C=Path('/workspace/1536-cloud-clean-restore');P='specs/1536-complete-wound-materialization/';D=P+'recovery/'
OLD='32cfa9ce156a8e5bc5e41c60ad4b1b0e605bfd94';NEW='126f08d44b8360cd7fdc7116120d8ad1aece8f86';out=Path('/tmp/1536-cloud-tools/review/l01-managed-stack-s01-documentary-correction-checks-20261018.json')
checks=[]
def h(b):return hashlib.sha256(b).hexdigest()
def git(*a,root=R):return subprocess.check_output(['git','-C',str(root),*a])
def ck(n,v):
 checks.append({'check':n,'passed':bool(v)})
 if not v:raise AssertionError(n)
def im(p):b=Path(p).read_bytes();return {'path':str(p),'bytes':len(b),'sha256':h(b)}
files=[P+'plan.md',P+'design/l01-managed-stack-capability-plan-20261018.md',D+'l01-managed-stack-s01-original-evidence-20261018.json']
receiptpath=Path('/tmp/1536-cloud-tools/m01-carrier-recovery-126f08d4.json');receipt=json.loads(receiptpath.read_text())
ck('exact current recovery',receipt['sha']==NEW and receipt['remoteShaVerified'] and receipt['clean'] and not receipt['localSourceOrObjectReuse'])
ck('exact three documentary paths',set(git('diff','--name-only',OLD,NEW).decode().splitlines())==set(files))
for root in [R,C]:
 ck('exact clean checkout '+str(root),git('rev-parse','HEAD',root=root).decode().strip()==NEW and git('status','--porcelain',root=root)==b'')
for p in files:
 row=next(x for x in receipt['files'] if x['path']==p);b=(R/p).read_bytes();ls=git('ls-tree',NEW,'--',p).decode().split()
 ck('corrected exact bytes/mode '+p,b==(C/p).read_bytes()==git('show',NEW+':'+p) and h(b)==row['sha256'] and len(b)==row['bytes'] and ls[0]==row['mode'] and ls[2]==row['gitBlob'])
p=files[2];old=json.loads(git('show',OLD+':'+p));new=json.loads((R/p).read_text());copy=json.loads(json.dumps(new));copy.pop('metricLabelCorrection');outcome=copy['outcome'];outcome['requestWallSeconds']=outcome.pop('collectorEnvelopeWallSeconds')
for k in ['collectorEnvelopeScope','apiOnlyDurationSeconds','actualThreadSuspensionDurationSeconds','durationLimits']:outcome.pop(k)
ck('derived index changed only label and declared limits',old==copy)
ck('API-only and suspension explicitly unmeasured',new['outcome']['apiOnlyDurationSeconds'] is None and new['outcome']['actualThreadSuspensionDurationSeconds'] is None and 'UNKNOWN' in new['outcome']['durationLimits'])
ck('original provenance remains exact',new['metricLabelCorrection']['originalEvidenceCarrier']==OLD and new['metricLabelCorrection']['originalRecordUnchanged'] and new['metricLabelCorrection']['originalZipUnchanged'])
plan=(R/files[1]).read_text();top=(R/files[0]).read_text().split('## L01 S01 — SOURCE/PREP')[0]
ck('future pause inference withdrawn','Measure request/suspend interval' not in plan and 'thread-suspension duration remain UNKNOWN' in plan)
ck('mandatory ordered race disposition','capturePhase=UNKNOWN' in plan and 'The cancellation check does not freeze the target' in plan and 'ordering evidence actually proves the phase of capture' in plan)
ck('conditional missing live delivery no release','Prerequisite currently missing' in plan and 'no tee/observer/game execution is released' in plan and 'gameplay release NONE' in plan)
ck('top correct measured scope','envelope1.729s' in top and 'API-only and actual suspension' in top and 'durations UNKNOWN' in top)
ck('unchanged budgets and distinct holds','untouched220s' in plan and '900s category' in plan and 'Windows success would be separate platform evidence' in plan and 'distinct from existing403' in plan)
for p in [D+'l01-managed-stack-s01-originals-20261018.zip',D+'l01-managed-stack-capability-control-20261018.json',D+'l01-managed-stack-helper-preparation-20261018.json']:
 ck('frozen raw/control/preparation '+p,git('show',OLD+':'+p)==(R/p).read_bytes())
result={'kind':'INDEPENDENT_S01_TWO_DOCUMENTARY_CORRECTIONS','originalEvidenceCarrier':OLD,'reviewedCarrier':NEW,'checkedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'checks':checks,'passed':all(x['passed'] for x in checks),'findings':[],'reviewedFiles':[im(R/p) for p in files],'recoveryReceipt':im(receiptpath)}
out.write_text(json.dumps(result,indent=2)+'\n');print(json.dumps({'checks':len(checks),'passed':result['passed'],'output':str(out),'sha256':h(out.read_bytes())}))
