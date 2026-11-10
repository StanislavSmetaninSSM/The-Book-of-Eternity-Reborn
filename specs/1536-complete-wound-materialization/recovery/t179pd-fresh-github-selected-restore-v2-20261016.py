from pathlib import Path
import subprocess,json,hashlib,datetime,sys
R=Path.cwd();sha,dest,receipt=sys.argv[1:];D=Path(dest);assert D.is_absolute()and str(D).startswith('/tmp/')and not D.exists();assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=R,text=True).strip()==sha
assert not subprocess.check_output(['git','status','--porcelain'],cwd=R,text=True)
branch='1536-wound-b2-cloud-20261012';origin=subprocess.check_output(['git','remote','get-url','origin'],cwd=R,text=True).strip();assert origin=='https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git'
remote=subprocess.check_output(['git','ls-remote','origin','refs/heads/'+branch,'refs/heads/main'],cwd=R,text=True);assert sha+'\trefs/heads/'+branch in remote and 'd0241e71e349fbe2020e4a41b6be7c627c81cbb4\trefs/heads/main'in remote
subprocess.run(['git','clone','--quiet','--depth','1','--filter=blob:none','--single-branch','--branch',branch,'--no-checkout',origin,str(D)],check=True)
profile=R/'specs/1536-complete-wound-materialization/recovery/t179pd-observed-reader-final-fresh-source-recovery-20261016.json';patterns=set('/'+row['path']for row in json.loads(profile.read_text())['files']);patterns.update('/'+s for s in subprocess.check_output(['git','diff','--name-only','d13aab44fd3487d95bf6005bb8296dbda54846bb',sha],cwd=R,text=True).splitlines());subprocess.run(['git','-C',str(D),'sparse-checkout','set','--no-cone','--stdin'],input='\n'.join(sorted(patterns))+'\n',text=True,check=True);subprocess.run(['git','-C',str(D),'checkout','--quiet','--detach',sha],check=True)
assert subprocess.check_output(['git','-C',str(D),'rev-parse','HEAD'],text=True).strip()==sha and not(D/'.git/objects/info/alternates').exists();files=[]
for p in D.rglob('*'):
 if '.git'in p.parts or not p.is_file():continue
 rel=str(p.relative_to(D));raw=subprocess.check_output(['git','show',sha+':'+rel],cwd=R);assert p.read_bytes()==raw==(R/rel).read_bytes();files.append({'path':rel,'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw)})
assert not subprocess.check_output(['git','-C',str(D),'status','--porcelain'],text=True)
readback=subprocess.check_output(['git','ls-remote','origin','refs/heads/'+branch,'refs/heads/main'],cwd=R,text=True);assert remote==readback
out={'sha':sha,'directory':str(D),'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'files':files,'count':len(files),'new_directory':True,'clean':True,'noAlternates':True,'source':'NEW GitHub-only shallow filtered sparse clone; no local object/reference/cache transport','remoteReadback':readback,'restoredBuildOrRuntime':'NOT_RUN','scope':'Selected task source/evidence/recovery files, not a full working-tree export'};q=Path(receipt);assert not q.exists();q.write_text(json.dumps(out,indent=2)+'\n');print('FRESH_GITHUB_SELECTED_RESTORE',sha,len(files),'exact files;NEWdirectory/clean/noAlternates/mainunchanged;no restoredruntime')
