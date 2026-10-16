from pathlib import Path
import json,subprocess,shutil,hashlib
R=Path('/workspace/The-Book-of-Eternity-Reborn');P='t179pd-hot-custody-red2-owned-first-late-20261016';A='93a72d6df0f59f5b5768b007ca51ad8b712ce9f3';p=Path('/tmp')/(P+'-attempt1');ex=json.loads(p.with_suffix('.exit.json').read_text());runs=[]
for q in (R/'TestResults/test-categories').glob('20261016-19*/summary.json'):
 s=json.loads(q.read_text(encoding='utf-8-sig'))
 if s['Revision']['Head']==A and not s['PlanOnly'] and not s['DiscoveryOnly'] and s['Categories']==['fr063-joint-late-completion']:runs.append(q.parent)
assert len(runs)==1;run=runs[0]
shutil.copyfile(p.with_suffix('.log'),run/'stdout.log');shutil.copyfile(p.with_suffix('.exit.json'),run/'native-exit.json')
subprocess.run(['python','/tmp/t179pb-freeze.py',str(run),P,A,str(ex['native_exit'])],cwd=R,check=True,stdout=subprocess.PIPE)
print('FROZEN_COMPLETE_ACTUAL',str(run),'native',ex['native_exit'])
assert ex['native_exit']==0 and ex['actual_head']==A and ex['compiled_source']=='d474cdd8546f8157b6ffd71840f268062ee05ed0'
assert ex['binaries_unchanged'] and ex['post_head_unchanged'] and ex['post_git_clean'] and all(not v for v in ex['new_scoped_temp_residue'].values())
subprocess.run(['python','/tmp/t179pd-inspect-hot-custody-red2-owned-first-late.py','WORKTREE',A],cwd=R,check=True,stdout=subprocess.PIPE)
subprocess.run(['python','/tmp/t179pd-inspect-hot-custody-red2-owned-late-root-bindings.py'],cwd=R,check=True,stdout=subprocess.PIPE)
print('ROOT_ACTUAL_ORACLES_AND_FULL_BINDINGS_VERIFIED;separateEVIDENCE stillrequired')
