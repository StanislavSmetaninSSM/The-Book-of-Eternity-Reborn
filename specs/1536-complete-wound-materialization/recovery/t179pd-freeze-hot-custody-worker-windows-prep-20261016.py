from pathlib import Path
import json,hashlib,shutil,subprocess
r=Path('/workspace/The-Book-of-Eternity-Reborn');d=r/'specs/1536-complete-wound-materialization/recovery';sha='a09272e91b379adb792450cd9e9d4f2a928d8a9b';stage='t179pd-hot-custody-worker-windows'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()==sha
assert not subprocess.check_output(['git','status','--porcelain'],cwd=r,text=True)
runs={}
for p in (r/'TestResults/test-categories').glob('20261016-22*/summary.json'):
 s=json.loads(p.read_text(encoding='utf-8-sig'))
 if s['Revision']['Head']==sha:
  phase='catalog' if s['DiscoveryOnly'] else 'plan' if s['PlanOnly'] else 'actual'
  assert phase in ['catalog','plan'] and phase not in runs;runs[phase]=(p.parent,s)
assert set(runs)=={'catalog','plan'}
for phase,(run,s) in runs.items():
 p=Path('/tmp')/(stage+'-'+phase+'-20261016');ex=json.loads(p.with_suffix('.exit.json').read_text());assert ex['native_exit']==0 and ex['actual_head']==sha
 assert s['ExitCode']==0 and s['Tests']['Executed']==0 and s['OwnedTreeCleanupSucceeded'] and not s['TimedOut'] and s['Revision']['Head']==sha and s['Revision']['ChangedFiles']==0
 if phase=='plan':assert s['Selection']['PlannedDescriptors']==1 and s['Selection']['PlannedCases']==2
 shutil.copyfile(p.with_suffix('.log'),run/'stdout.log');shutil.copyfile(p.with_suffix('.exit.json'),run/'native-exit.json')
 subprocess.run(['python','/tmp/t179pb-freeze.py',str(run),stage+'-prep-'+phase+'-20261016',sha,'0'],cwd=r,check=True,stdout=subprocess.PIPE)
paths=['BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.Tests.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.IntegrationTests.dll']
m={'source_sha':sha,'phase':'Fresh BOTH/catalog and exact singleton PlanOnly after test-only logger/Windows catalog and gated CI source a092; ZERO tests. Affected SOURCE pending; no actual release. Subsequent CI-only gate correction does not change compiled inputs.','binaries':[{'path':p,'sha256':hashlib.sha256((r/p).read_bytes()).hexdigest()}for p in paths]}
p=d/(stage+'-prep-binaries-20261016.json');assert not p.exists();p.write_text(json.dumps(m,indent=2)+'\n')
name=stage+'-source-delta-recovery-20261016.json';receipt=Path('/tmp')/name;assert json.loads(receipt.read_text())['sha']==sha;assert not (d/name).exists();shutil.copyfile(receipt,d/name)
print(json.dumps({'runs':{k:str(v[0]) for k,v in runs.items()},'binaries':m},indent=2))
