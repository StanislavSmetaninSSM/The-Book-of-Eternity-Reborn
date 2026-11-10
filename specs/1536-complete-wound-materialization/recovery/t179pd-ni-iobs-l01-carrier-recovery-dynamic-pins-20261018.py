from pathlib import Path
import subprocess,json,hashlib,datetime,sys
W=Path('/workspace/The-Book-of-Eternity-Reborn');R=Path('/workspace/1536-cloud-clean-restore');base,sha=sys.argv[1:];branch='1536-wound-b2-cloud-20261012'
def g(root,*args):return subprocess.check_output(['git','-C',str(root),*args])
assert g(W,'ls-remote','origin','refs/heads/'+branch).decode().split()[0]==sha
subprocess.run(['git','-C',str(R),'fetch','origin','refs/heads/'+branch],check=True,stdout=subprocess.DEVNULL)
subprocess.run(['git','-C',str(R),'checkout','--detach',sha],check=True,stdout=subprocess.DEVNULL)
assert g(R,'rev-parse','HEAD').decode().strip()==sha and not g(R,'status','--porcelain')
changed=g(W,'diff','--name-only',base,sha).decode().splitlines();files=[]
for path in changed:
 b=(R/path).read_bytes();assert b==g(W,'show',sha+':'+path)==(W/path).read_bytes();files.append(dict(path=path,bytes=len(b),sha256=hashlib.sha256(b).hexdigest(),gitBlob=g(R,'rev-parse',sha+':'+path).decode().strip(),mode=g(R,'ls-tree',sha,'--',path).decode().split()[0]))
p=json.loads((R/'specs/1536-complete-wound-materialization/recovery/t179pd-ni-iobs-l01-source-packet-20261018.json').read_text())
for row in p['sourcePins']:
 b=(R/row['path']).read_bytes();assert len(b)==row['bytes'] and hashlib.sha256(b).hexdigest()==row['sha256']
rec=dict(kind='CLOUD_CARRIER_EXACT_GITHUB_RECOVERY',sha=sha,base=base,checkedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),directory=str(R),remoteShaVerified=True,clean=True,localSourceOrObjectReuse=False,changedPathsVerified=len(files),files=files,sourcePinsVerified=len(p['sourcePins']),sourcePinDigest=p['pinDigest'],method='Fetch exact task branch directly from GitHub into independent originally empty-directory GitHub clone, checkout exact SHA, clean tree, verify every changed file bytes/SHA256/blob/mode and all current source pins',limits='Source/evidence recovery only. No test/build execution here; no lost corrected-crash bytes recovered.')
out=Path('/tmp/1536-cloud-tools')/('carrier-recovery-'+sha[:8]+'.json');out.write_text(json.dumps(rec,indent=2)+'\n');print('RECOVERED',sha,len(files),'paths',str(out))
