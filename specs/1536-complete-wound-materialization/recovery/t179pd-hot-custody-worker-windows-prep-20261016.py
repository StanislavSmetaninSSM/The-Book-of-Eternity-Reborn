from pathlib import Path
import subprocess,json,sys,datetime,hashlib
r=Path('/workspace/The-Book-of-Eternity-Reborn')
assert not subprocess.check_output(['git','status','--porcelain'],cwd=r,text=True)
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()
assert head=='a09272e91b379adb792450cd9e9d4f2a928d8a9b'
recovery=json.loads(Path('/tmp/t179pd-hot-custody-worker-windows-source-delta-recovery-20261016.json').read_text());assert recovery['sha']==head and recovery['deleted_paths']==[]
assert not (r/'tests/selections/1536-hot-worker-windows.json').exists()
phase=sys.argv[1]
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1']+({'catalog':['-ValidateCatalog'],'plan':['-Category','fr063-hot-worker-ready-disposition','-PlanOnly','-NoBuild']}[phase])
p=Path('/tmp/t179pd-hot-custody-worker-windows-'+phase+'-20261016');assert not p.with_suffix('.log').exists()
if phase=='plan':
 c=json.loads(Path('/tmp/t179pd-hot-custody-worker-windows-catalog-20261016.exit.json').read_text());assert c['native_exit']==0 and c['actual_head']==head
start=datetime.datetime.now(datetime.timezone.utc)
with p.with_suffix('.log').open('w') as log:
 result=subprocess.run(argv,cwd=r,stdout=log,stderr=subprocess.STDOUT)
data={'argv':argv,'native_exit':result.returncode,'actual_head':head,'compiled_source':head,'started_utc':start.isoformat(),'ended_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'test_execution':'ZERO_REQUIRED','scope':'T179-Pd.3b HS-WORKER-F01/O02 test-only logger and existing Windows host/CI correction. Fresh BOTH/catalog and exact singleton PlanOnly; ZERO actual tests; no Linux worker repeat or Windows actual release.'}
p.with_suffix('.exit.json').write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps(data));print('\n'.join(p.with_suffix('.log').read_text().splitlines()[-40:]));sys.exit(result.returncode)
