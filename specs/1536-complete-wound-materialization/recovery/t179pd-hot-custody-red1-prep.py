from pathlib import Path
import subprocess,json,sys,datetime,hashlib
r=Path('/workspace/The-Book-of-Eternity-Reborn')
assert not subprocess.check_output(['git','status','--porcelain'],cwd=r,text=True)
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()
assert head=='e182b6745817bd34ff5247c34f8d469bb71754f1'
phase=sys.argv[1]
argv=['pwsh','-NoLogo','-NoProfile','-File','scripts/test-csharp.ps1']+({'catalog':['-ValidateCatalog'],'plan':['-SelectionFile','tests/selection.json','-PlanOnly','-NoBuild']}[phase])
p=Path('/tmp/t179pd-hot-custody-red1-'+phase+'-20261016');assert not p.with_suffix('.log').exists()
if phase=='plan':
 c=json.loads(Path('/tmp/t179pd-hot-custody-red1-catalog-20261016.exit.json').read_text());assert c['native_exit']==0 and c['actual_head']==head
start=datetime.datetime.now(datetime.timezone.utc)
with p.with_suffix('.log').open('w') as log:
 result=subprocess.run(argv,cwd=r,stdout=log,stderr=subprocess.STDOUT)
data={'argv':argv,'native_exit':result.returncode,'actual_head':head,'compiled_source':head,'started_utc':start.isoformat(),'ended_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'test_execution':'ZERO_REQUIRED','scope':'T179-Pd.3b HS01 test-only causal source before any product implementation'}
p.with_suffix('.exit.json').write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps(data));print('\n'.join(p.with_suffix('.log').read_text().splitlines()[-40:]));sys.exit(result.returncode)
