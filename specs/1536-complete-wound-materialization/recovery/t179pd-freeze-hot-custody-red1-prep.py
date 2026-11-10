from pathlib import Path
import json,hashlib,shutil,subprocess,sys
stage,sha,catalog,plan=sys.argv[1:];r=Path('/workspace/The-Book-of-Eternity-Reborn');d=r/'specs/1536-complete-wound-materialization/recovery'
assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()==sha and not subprocess.check_output(['git','status','--porcelain'],cwd=r,text=True)
for phase,dirname in [('catalog',catalog),('plan',plan)]:
 run=r/'TestResults/test-categories'/dirname;p=Path('/tmp')/(stage+'-'+phase+'-20261016');ex=json.loads(p.with_suffix('.exit.json').read_text());assert ex['native_exit']==0 and ex['actual_head']==sha
 s=json.loads((run/'summary.json').read_text(encoding='utf-8-sig'));assert s['ExitCode']==0 and s['Tests']['Executed']==0 and s['OwnedTreeCleanupSucceeded'] and not s['TimedOut'] and s['Revision']['Head']==sha and s['Revision']['ChangedFiles']==0
 if phase=='plan':assert s['Selection']['PlannedDescriptors']==2 and s['Selection']['PlannedCases']==3
 shutil.copyfile(p.with_suffix('.log'),run/'stdout.log');shutil.copyfile(p.with_suffix('.exit.json'),run/'native-exit.json')
 subprocess.run(['python','/tmp/t179pb-freeze.py',str(run),stage+'-prep-'+phase+'-20261016',sha,'0'],cwd=r,check=True,stdout=subprocess.PIPE)
paths=['BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.Tests/bin/Debug/net8.0/BookOfEternityClient.Tests.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.dll','BookOfEternityClient.IntegrationTests/bin/Debug/net8.0/BookOfEternityClient.IntegrationTests.dll']
m={'source_sha':sha,'phase':'Pd.3b HS01 initial TEST-ONLY genuine post-original custody fixture; fresh BOTH/catalog/PlanOnly; ZERO actual, missing-owner causal RED not yet run','binaries':[{'path':p,'sha256':hashlib.sha256((r/p).read_bytes()).hexdigest()}for p in paths]};p=d/(stage+'-prep-binaries-20261016.json');assert not p.exists();p.write_text(json.dumps(m,indent=2)+'\n')
receipt=Path('/tmp')/(stage+'-wip-selected-recovery-20261016.json');assert json.loads(receipt.read_text())['sha']==sha;shutil.copyfile(receipt,d/receipt.name)
print(json.dumps(m,indent=2))
